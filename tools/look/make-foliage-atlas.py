"""Painted foliage and ground-detail atlases for Visiting Hours' look (the Look agent, pass 3).

Pass 3 (judge round 2, "autumn foliage": every leaf yellow/orange under the warm grade; the leaf atlas averaged hue 65
degrees, nobody had checked): every palette here is a late-summer one, checked numerically by tools/look/palette-check.py
against the targets in docs/redesign/look.md ("The palette"). Deep and olive greens, a warm yellow-green only on the
sunlit rim of a clump, about 5% yellowed leaves.

Writes, into Assets/JamKit/Templates/VisitingHours/Look/Textures/:
  VH_LeafClusters.png  1024 x 1024, 2 x 2 cells: leaf CLUMPS for the tree crowns (VhLookTrees). Pass 3: each cell is a dense,
                       rounded clump of leaves filling ~60% of the cell (pass 2's sparse clusters filled 33%: two thirds of
                       every card's pixels were shaded and thrown away), so a crown needs a third as many cards.
                       Cells: 0 broad leaves (maple/elm), 1 lobed leaves (oak), 2 fine small leaves (birch), 3 small dense
                       leaves (shrubs and hedges).
  VH_GrassCards.png    1024 x 1024, 2 x 2: 0 green tuft, 1 green tuft with dry gold tips, 2 wildflowers in grass, 3 low weeds.
  VH_GroundDecals.png  1024 x 1024, 2 x 2: 0 leaf litter (alpha-clipped), 1 oil stain, 2 dirt/water stain, 3 a grime streak.
                       Pass 3: every stain fades to zero alpha well inside its cell (round 2: "hard rectangles").
  VH_TreeBillboards.png 1024 x 1024, 2 x 2: whole-tree silhouettes for the far LOD (0 maple, 1 oak, 2 birch, 3 elm).
  VH_FarFields.png     2048 x 2048 tiling, 2400 m a repeat: a patchwork of fields (pasture, hay, stubble, crops, a little
                       ploughed earth), hedgerows along the field edges and woods, for the distant ring (VhLookBackdrop).
  VH_FarFields.json    the same layout as data ({"tile", "hedges": [x0, y0, x1, y1, ...], "woods": [x0, y0, x1, y1, ...]}
                       in metres of the tile, y = the image's row direction), so the backdrop's 3D hedgerows and woods sit
                       exactly on the painted ones.
  VH_BirchBark.png     512 x 512 tiling: chalk-white birch bark with dark lenticels and black knots (round 2: "birch trunks
                       read as bright yellow sticks": pass 2 tinted the brown bark x2.3).

    uv run --with numpy --with pillow python -I tools/look/make-foliage-atlas.py

Deterministic (fixed seeds). Everything is drawn here, so the textures are the project's own.
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "JamKit", "Templates",
                    "VisitingHours", "Look", "Textures")
SS = 2          # supersampling for antialiased edges
CELL = 512 * SS


def shade(c, k):
    return tuple(int(max(0, min(255, round(v * k)))) for v in c[:3])


def tinted(c, shift):
    return tuple(int(max(0, min(255, round(c[i] * shift[i])))) for i in range(3))


# ---------------- Leaves (summer) ----------------
# sRGB albedo. The mid greens sit at hue ~95-110 degrees: the warm sun (1, 0.77, 0.52) and the grade pull sunlit leaves to
# a yellow-green on screen while shade stays green. Only the rim colours are yellow-green in the albedo itself.

DEEP = [(24, 46, 26), (30, 56, 30), (36, 66, 34)]               # deep inside the clump, under other leaves
MID = [(40, 76, 36), (46, 86, 40), (52, 94, 44), (44, 82, 42)]  # the body of the clump
OLIVE = [(58, 84, 38), (64, 92, 42)]                              # olive variety
RIM = [(72, 112, 48), (80, 120, 50), (90, 128, 54)]               # a lighter green on the clump's rim (the shader adds the sun)
YELLOWED = [(128, 130, 58), (138, 136, 62)]                       # the odd tired leaf (~1.5%, outer top only)

SPECIES_SHIFT = {
    0: (1.0, 1.0, 1.0),     # maple / elm: the reference greens
    1: (1.04, 0.98, 0.92),  # oak: a shade more olive
    2: (1.06, 1.06, 0.98),  # birch: lighter, finer
    3: (0.92, 0.98, 1.0),   # shrub / hedge: a little bluer, darker
}


def leaf_poly(cx, cy, length, width, angle, shape):
    """A leaf outline: a pointed lens (shape 0), a rounder ovate leaf (1), a narrow lance (2), a lobed oak leaf (3)."""
    pts = []
    n = 16
    for i in range(n + 1):
        t = i / n
        if shape == 1:
            w = math.sin(math.pi * t) ** 0.7 * (1.0 - 0.25 * t)
        elif shape == 2:
            w = math.sin(math.pi * t) ** 1.2 * 0.6
        elif shape == 3:
            w = math.sin(math.pi * t) ** 0.8 * (0.75 + 0.25 * math.cos(t * math.pi * 6.0))
        else:
            w = math.sin(math.pi * t) ** 0.85 * (1.0 - 0.1 * t)
        pts.append((t, w))
    out = []
    ca, sa = math.cos(angle), math.sin(angle)
    for t, w in pts:
        x, y = t * length, w * width * 0.5
        out.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    for t, w in reversed(pts):
        x, y = t * length, -w * width * 0.5
        out.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return out


def leaf_clump(rng, kind, count, size, shape):
    """A dense rounded clump of leaves filling most of the cell: an irregular outline of overlapping lobes, packed with
    leaves back to front, darker towards the bottom and the middle (painted occlusion), the top/outer rim lit in a warm
    yellow-green, about 5% yellowed leaves, a few twig stubs showing in the gaps."""
    shift = SPECIES_SHIFT[kind]
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = CELL / 2
    R = CELL * 0.42
    lobes = [(c + rng.uniform(-0.24, 0.24) * R, c + rng.uniform(-0.2, 0.24) * R, R * rng.uniform(0.5, 0.64)) for _ in range(6)]
    lobes.append((c, c + 0.04 * R, R * 0.72))

    def inside(x, y):
        best = -1e9
        for bx, by, br in lobes:
            best = max(best, 1.0 - math.hypot(x - bx, y - by) / br)
        return best

    # Twig stubs from below, under the leaves.
    base = (c + rng.uniform(-0.05, 0.05) * R, c + R * 0.82)
    for _ in range(5):
        bx, by, br = lobes[rng.integers(len(lobes))]
        tx, ty = bx + rng.uniform(-0.4, 0.4) * br, by + rng.uniform(-0.4, 0.3) * br
        d.line([base, ((base[0] + tx) / 2, (base[1] + ty) / 2 + rng.uniform(-0.05, 0.05) * R), (tx, ty)],
               fill=(58, 46, 34, 255), width=int(4 * SS), joint="curve")

    leaves = []
    tries = 0
    while len(leaves) < count and tries < count * 60:
        tries += 1
        x = c + rng.uniform(-1, 1) * R
        y = c + rng.uniform(-1, 1) * R
        k = inside(x, y)
        if k < 0.0:
            continue
        leaves.append((x, y, k))
    # Back to front: deep, low leaves first; the outer, upper ones overlap them.
    leaves.sort(key=lambda p: (-p[2] * 0.6 - (p[1] - c) / R * 0.4))
    for x, y, k in leaves:
        L = size * rng.uniform(0.75, 1.25) * SS
        W = L * (0.55 if shape != 2 else 0.34) * rng.uniform(0.85, 1.15)
        ang = math.atan2(y - c, x - c) + rng.normal(0, 0.8)
        rim = 1.0 - min(1.0, k * 2.2)                   # 1 on the clump's outline, 0 a little inside
        top = max(0.0, (c - y) / R)                      # 1 at the top
        depth = min(1.0, k * 1.6)                        # deep inside the clump
        u = rng.random()
        if u < 0.015 and top > 0.3 and rim > 0.3:
            col = YELLOWED[rng.integers(len(YELLOWED))]
        elif rim * (0.4 + 0.6 * top) > 0.45 and rng.random() < 0.75:
            col = RIM[rng.integers(len(RIM))]
        elif depth > 0.7 and rng.random() < 0.6:
            col = DEEP[rng.integers(len(DEEP))]
        elif rng.random() < 0.25:
            col = OLIVE[rng.integers(len(OLIVE))]
        else:
            col = MID[rng.integers(len(MID))]
        col = tinted(col, shift)
        light = 0.86 + 0.14 * top - 0.12 * max(0.0, (y - c) / R) + rng.uniform(-0.06, 0.06)
        poly = leaf_poly(x, y, L, W, ang, shape)
        d.polygon(poly, fill=shade(col, light) + (255,))
        half = poly[: len(poly) // 2 + 1]
        mid = [(x, y)] + half[len(half) // 3: len(half) * 2 // 3] + [half[-1]]
        d.polygon(mid, fill=shade(col, light * 1.1) + (255,))
        ex, ey = x + math.cos(ang) * L * 0.85, y + math.sin(ang) * L * 0.85
        d.line([(x, y), (ex, ey)], fill=shade(col, light * 0.78) + (255,), width=max(1, int(1.1 * SS)))
    # Round 3: "card edges show as straight lines": leaves reached the cell's border and were cut. Fade the alpha to
    # zero before it (a round window), so every clump has a ragged rounded outline.
    a = np.asarray(img).astype(np.float32)
    a[..., 3] *= window(CELL, 0.86, 0.97)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def atlas(cells, size=1024):
    out = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for i, cell in enumerate(cells):
        # Cell i at (i % 2, i // 2) with v = 0 at the bottom (Unity): row 0 of the image is the top.
        out.alpha_composite(cell, ((i % 2) * CELL, (1 - i // 2) * CELL))
    return out.resize((size, size), Image.LANCZOS)


def bleed(img):
    """Fill transparent pixels' colour from their neighbours (so mips and filtering don't pull in black edges)."""
    a = np.asarray(img).astype(np.float32)
    rgb, al = a[..., :3], a[..., 3:4] / 255.0
    acc, w = rgb * al, al.copy()
    out = rgb.copy()
    filled = al[..., 0] >= 0.5
    for r in (2, 4, 8, 16, 32, 64):
        k = Image.fromarray(np.clip(acc, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(r))
        kw = Image.fromarray(np.clip(w[..., 0] * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(r))
        kk = np.asarray(k).astype(np.float32)
        ww = np.asarray(kw).astype(np.float32)[..., None] / 255.0
        fill = kk / np.maximum(ww, 1e-4)
        empty = (~filled) & (ww[..., 0] > 0.002)
        out[empty] = fill[empty]
        filled = filled | empty
    res = np.concatenate([out, a[..., 3:4]], axis=2)
    return Image.fromarray(np.clip(res, 0, 255).astype(np.uint8), "RGBA")


def leaves():
    rng = np.random.default_rng(11)
    cells = [
        leaf_clump(rng, 0, 700, 34, 0),
        leaf_clump(rng, 1, 640, 36, 3),
        leaf_clump(rng, 2, 1100, 24, 1),
        leaf_clump(rng, 3, 980, 26, 1),
    ]
    return bleed(atlas(cells))


# ---------------- Grass and flowers ----------------

GRASS = [(52, 84, 34), (62, 96, 38), (72, 108, 42), (86, 118, 46), (98, 126, 52)]
DRY_TIP = [(176, 156, 92), (190, 168, 104), (162, 146, 84)]
STRAW = [(170, 150, 90), (184, 162, 100)]


def blade(d, x0, h, lean, w0, col, tip, tip_from=0.5):
    pts_l, pts_r = [], []
    n = 12
    for i in range(n + 1):
        t = i / n
        x = x0 + lean * t * t * h
        y = CELL - 1 - t * h
        w = w0 * (1.0 - t) ** 0.9
        pts_l.append((x - w, y))
        pts_r.append((x + w, y))
    d.polygon(pts_l + pts_r[::-1], fill=col + (255,))
    k = int(n * tip_from)
    d.polygon(pts_l[k:] + [pts_r[-1]] + [((pts_l[i][0] + pts_r[i][0]) / 2, pts_l[i][1]) for i in range(n, k - 1, -1)],
              fill=tip + (255,))


def grass_tuft(rng, blades, hmin, hmax, spread=0.42, dry=0.0):
    """A tuft: green blades (lit upper halves); with dry > 0, that share of blades has straw-gold tips (the top quarter)
    and a few are straw all the way (late summer: green at the root, dry at the tip)."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    items = []
    for _ in range(blades):
        x0 = CELL / 2 + rng.normal(0, spread * CELL * 0.5)
        x0 = min(max(x0, CELL * 0.08), CELL * 0.92)
        items.append((rng.uniform(hmin, hmax) * CELL, x0))
    items.sort(reverse=True)
    for h, x0 in items:
        lean = rng.normal(0, 0.28) + (x0 - CELL / 2) / CELL * 0.6
        k = rng.uniform(0.82, 1.05)
        col = GRASS[rng.integers(len(GRASS))]
        u = rng.random()
        if u < dry * 0.15:
            s = STRAW[rng.integers(len(STRAW))]
            blade(d, x0, h, lean, rng.uniform(4, 7) * SS, shade(s, k * 0.85), shade(s, k * 1.05))
        elif u < dry:
            blade(d, x0, h, lean, rng.uniform(4, 7) * SS, shade(col, k * 0.85), shade(DRY_TIP[rng.integers(len(DRY_TIP))], k), 0.72)
        else:
            blade(d, x0, h, lean, rng.uniform(4, 7) * SS, shade(col, k * 0.85), shade(col, k * 1.12))
    return img


def flowers(rng):
    img = grass_tuft(rng, 46, 0.25, 0.62, dry=0.15)
    d = ImageDraw.Draw(img)
    kinds = [((246, 244, 232), (232, 196, 70), 9), ((240, 200, 40), (220, 160, 30), 8), ((150, 160, 224), (120, 120, 190), 9),
             ((236, 220, 236), (220, 170, 70), 7)]
    for _ in range(16):
        x = CELL / 2 + rng.normal(0, CELL * 0.22)
        x = min(max(x, CELL * 0.1), CELL * 0.9)
        h = rng.uniform(0.3, 0.75) * CELL
        top = (x + rng.normal(0, 0.06) * h, CELL - h)
        d.line([(x, CELL - 1), top], fill=(64, 98, 40, 255), width=int(3 * SS))
        petal, centre, r = kinds[rng.integers(len(kinds))]
        r *= SS * rng.uniform(0.9, 1.3)
        for p in range(8):
            a = p / 8 * 2 * math.pi + rng.uniform(0, 0.3)
            px, py = top[0] + math.cos(a) * r, top[1] + math.sin(a) * r * 0.75
            d.ellipse([px - r * 0.55, py - r * 0.4, px + r * 0.55, py + r * 0.4], fill=petal + (255,))
        d.ellipse([top[0] - r * 0.45, top[1] - r * 0.4, top[0] + r * 0.45, top[1] + r * 0.4], fill=centre + (255,))
    return img


def weeds(rng):
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    img.alpha_composite(grass_tuft(rng, 40, 0.15, 0.4, 0.5))
    for _ in range(26):
        x = rng.uniform(0.1, 0.9) * CELL
        y = CELL - rng.uniform(0.02, 0.22) * CELL
        col = shade(GRASS[rng.integers(len(GRASS))], rng.uniform(0.85, 1.1))
        r = rng.uniform(10, 16) * SS
        for k in range(3):
            a = -math.pi / 2 + (k - 1) * 2.1 + rng.normal(0, 0.2)
            cx, cy = x + math.cos(a) * r * 0.8, y + math.sin(a) * r * 0.6
            d.ellipse([cx - r * 0.6, cy - r * 0.45, cx + r * 0.6, cy + r * 0.45], fill=col + (255,))
        d.line([(x, y), (x, CELL - 1)], fill=shade(col, 0.8) + (255,), width=int(2 * SS))
    return img


def grass():
    rng = np.random.default_rng(23)
    cells = [grass_tuft(rng, 90, 0.3, 0.92), grass_tuft(rng, 84, 0.3, 0.95, dry=0.55), flowers(rng), weeds(rng)]
    return bleed(atlas(cells))


# ---------------- Ground decals ----------------

def noise(rng, size, scales):
    acc = np.zeros((size, size), np.float32)
    amp = 1.0
    for s in scales:
        n = rng.random((max(2, size // s), max(2, size // s))).astype(np.float32)
        img = Image.fromarray((n * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
        acc += (np.asarray(img).astype(np.float32) / 255.0 - 0.5) * amp
        amp *= 0.5
    return acc


def window(size, inner=0.62, outer=0.9, elong=1.0):
    """1 in the middle, a smooth fall to exactly 0 at `outer` of the half-size: no stain ever reaches its cell's edge."""
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    r = np.hypot((xx - size / 2) / (size / 2) / elong, (yy - size / 2) / (size / 2))
    t = np.clip((outer - r) / (outer - inner), 0, 1)
    return t * t * (3 - 2 * t)


def blotch(rng, colour, strength, ring=0.0, elong=1.0):
    size = CELL
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    r = np.hypot((xx - size / 2) / elong, yy - size / 2) / (size * 0.36)
    n = noise(rng, size, [128, 48, 16])
    f = np.clip(1.0 - r + n * 0.5, 0, 1)
    a = np.clip(f * 1.5, 0, 1) ** 1.4 * strength
    if ring > 0:
        a += np.exp(-((f - 0.12) / 0.06) ** 2) * ring
    a = np.clip(a, 0, 1) * window(size, 0.5, 0.92, min(1.0, elong))
    rgb = np.zeros((size, size, 3), np.float32)
    rgb[:] = colour
    rgb *= (1.0 + n[..., None] * 0.4)
    return Image.fromarray(np.concatenate([np.clip(rgb, 0, 255), a[..., None] * 255], axis=2).astype(np.uint8), "RGBA")


def streak(rng):
    size = CELL
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    n = noise(rng, size, [96, 24, 8])
    across = np.abs(xx - size / 2) / (size * 0.3)
    down = yy / size
    a = np.clip((1.0 - across + n * 0.4), 0, 1) * np.clip(0.25 + down, 0, 1) * 0.75
    # Soft on every side (pass 2 ran it to the cell's bottom edge: a hard line under every drain).
    edge_x = np.clip((size * 0.46 - np.abs(xx - size / 2)) / (size * 0.12), 0, 1)
    edge_y = np.clip(yy / (size * 0.1), 0, 1) * np.clip((size - 1 - yy) / (size * 0.14), 0, 1)
    a = a * edge_x * edge_y
    rgb = np.zeros((size, size, 3), np.float32)
    rgb[:] = (52, 46, 38)
    return Image.fromarray(np.concatenate([rgb, np.clip(a, 0, 1)[..., None] * 255], axis=2).astype(np.uint8), "RGBA")


def litter(rng):
    """Fallen leaves: late summer, so mostly dry brown and a few green ones (no orange drifts)."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cols = [(118, 94, 58), (132, 108, 66), (104, 86, 56), (96, 108, 52), (84, 98, 46), (140, 122, 74)]
    for _ in range(70):
        x = CELL / 2 + rng.normal(0, CELL * 0.18)
        y = CELL / 2 + rng.normal(0, CELL * 0.18)
        if not (CELL * 0.1 < x < CELL * 0.9 and CELL * 0.1 < y < CELL * 0.9):
            continue
        L = rng.uniform(26, 44) * SS
        col = shade(cols[rng.integers(len(cols))], rng.uniform(0.85, 1.1))
        ang = rng.uniform(0, 2 * math.pi)
        d.polygon(leaf_poly(x, y, L, L * 0.6, ang, int(rng.integers(2))), fill=col + (255,))
        d.line([(x, y), (x + math.cos(ang) * L, y + math.sin(ang) * L)], fill=shade(col, 0.75) + (255,), width=max(1, SS))
    return img


def decals():
    rng = np.random.default_rng(37)
    cells = [litter(rng), blotch(rng, (24, 20, 18), 0.7, ring=0.1), blotch(rng, (92, 74, 52), 0.45, ring=0.2, elong=1.4),
             streak(rng)]
    out = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for i, cell in enumerate(cells):
        out.paste(cell, ((i % 2) * CELL, (1 - i // 2) * CELL))
    return bleed(out.resize((1024, 1024), Image.LANCZOS))


# ---------------- Whole-tree billboards ----------------

def tree_silhouette(rng, kind, leaf):
    """A tree seen from the side: crown clumps (by species), the trunk and limbs below/through, summer greens."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = CELL / 10.0
    base = CELL - 1
    cx = CELL / 2
    # (crown centre height, half width, half height, fork height, trunk radius) per species, metres (a 10 m tree).
    cy, rx, ry, fork, tr = {0: (6.2, 3.9, 3.3, 2.6, 0.34), 1: (6.4, 4.7, 3.0, 2.3, 0.42), 2: (5.9, 3.0, 3.9, 2.4, 0.2),
                            3: (6.9, 4.4, 2.9, 3.0, 0.34)}[kind]
    bark = (74, 64, 50) if kind != 2 else (214, 210, 200)
    top = cy + ry * (0.9 if kind == 2 else 0.5)
    d.polygon([(cx - tr * m * 1.6, base), (cx - tr * m, base - 0.6 * m), (cx - tr * 0.6 * m, base - top * m),
               (cx + tr * 0.6 * m, base - top * m), (cx + tr * m, base - 0.6 * m), (cx + tr * m * 1.6, base)], fill=bark + (255,))
    if kind == 2:
        for _ in range(14):
            y = base - rng.uniform(0.4, top) * m
            w = tr * m * rng.uniform(0.5, 1.1)
            d.line([(cx - w, y), (cx + w * rng.uniform(0.2, 1.0), y)], fill=(40, 38, 36, 255), width=int(rng.uniform(1.5, 3) * SS))
    for k in range(5):
        a = -math.pi / 2 + (k - 2) * (0.35 if kind == 3 else 0.5)
        x1 = cx + math.cos(a) * rx * 0.75 * m
        y1 = base - (cy - ry * 0.3) * m
        d.line([(cx, base - fork * m), (x1, y1)], fill=bark + (255,), width=int(tr * m * 0.5))
    # Clumps: a ring of big lobes round the outline and a few inside (the crown reads as clumps, with gaps).
    blobs = []
    for i in range(11):
        a = i / 11 * 2 * math.pi + rng.uniform(-0.2, 0.2)
        r = rng.uniform(0.55, 0.78)
        blobs.append((cx + math.cos(a) * rx * r * m, base - (cy + math.sin(a) * ry * r) * m, min(rx, ry) * rng.uniform(0.34, 0.46) * m))
    for _ in range(4):
        blobs.append((cx + rng.uniform(-0.4, 0.4) * rx * m, base - (cy + rng.uniform(-0.3, 0.4) * ry) * m, min(rx, ry) * 0.45 * m))

    def inside(x, y):
        return max(1 - math.hypot(x - bx, y - by) / br for bx, by, br in blobs)

    pts = []
    for _ in range(3800):
        x = cx + rng.uniform(-1.05, 1.05) * rx * m
        y = base - (cy + rng.uniform(-1.05, 1.05) * ry) * m
        k = inside(x, y)
        if k > 0.0:
            pts.append((x, y, k))
    pts.sort(key=lambda p: -p[2])
    for x, y, k in pts:
        L = leaf * rng.uniform(0.8, 1.2) * SS
        rim = 1.0 - min(1.0, k * 2.0)
        tp = max(0.0, (base - cy * m - y) / (ry * m))
        u = rng.random()
        if u < 0.04:
            col = YELLOWED[0]
        elif rim * (0.4 + 0.6 * tp) > 0.4 and rng.random() < 0.7:
            col = RIM[rng.integers(len(RIM))]
        elif k > 0.55:
            col = DEEP[rng.integers(len(DEEP))]
        else:
            col = MID[rng.integers(len(MID))]
        col = tinted(col, SPECIES_SHIFT[0 if kind == 3 else kind])
        light = 0.85 + 0.15 * tp + rng.uniform(-0.06, 0.06)
        ang = rng.uniform(0, 2 * math.pi)
        d.polygon(leaf_poly(x, y, L, L * 0.55, ang, 1), fill=shade(col, light) + (255,))
    return img


def billboards():
    rng = np.random.default_rng(41)
    return bleed(atlas([tree_silhouette(rng, k, 22 if k != 2 else 18) for k in range(4)]))


# ---------------- Birch bark ----------------

def birch_bark():
    """512 x 512, tiling, 0.6 m a repeat round and along the trunk: chalk white with grey tones, horizontal dark
    lenticels, a few black knots and scars."""
    rng = np.random.default_rng(53)
    size = 512
    n = noise(rng, size, [64, 16, 4])
    base = np.zeros((size, size, 3), np.float32)
    base[:] = (214, 210, 200)
    base *= (0.94 + n[..., None] * 0.18)
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")
    big = Image.new("RGB", (size * 3, size * 3))
    for i in range(3):
        for j in range(3):
            big.paste(img, (i * size, j * size))
    d = ImageDraw.Draw(big)
    for _ in range(120):
        x, y = rng.uniform(0, size), rng.uniform(0, size)
        w, h = rng.uniform(6, 34), rng.uniform(1.5, 4)
        g = int(rng.uniform(40, 80))
        for ox in (0, size, 2 * size):
            for oy in (0, size, 2 * size):
                d.ellipse([x + ox - w, y + oy - h, x + ox + w, y + oy + h], fill=(g, g, g))
    for _ in range(9):
        x, y = rng.uniform(0, size), rng.uniform(0, size)
        r = rng.uniform(6, 14)
        for ox in (0, size, 2 * size):
            for oy in (0, size, 2 * size):
                d.polygon([(x + ox - r * 1.6, y + oy), (x + ox, y + oy - r * 0.6), (x + ox + r * 1.6, y + oy), (x + ox, y + oy + r * 0.9)],
                          fill=(26, 24, 22))
    return big.crop((size, size, 2 * size, 2 * size)).filter(ImageFilter.GaussianBlur(0.6))


# ---------------- Far fields: a patchwork of fields, hedgerows and woods ----------------

FIELD_KINDS = [
    # name, share, sRGB colour, row stripes
    ("pasture", 0.34, (78, 108, 46), False),
    ("hay", 0.18, (118, 128, 58), False),
    ("stubble", 0.17, (176, 156, 96), True),
    ("crop", 0.17, (56, 90, 38), True),
    ("ploughed", 0.05, (120, 98, 70), True),
    ("meadow", 0.09, (96, 118, 50), False),
]
WOODS = (40, 60, 30)
HEDGE = (44, 64, 30)


def field_layout(rng, tile=2400.0):
    """Blocks from an irregular grid (cuts 250-450 m apart), each split recursively into fields of 90-260 m.
    Returns (fields: (x0, y0, x1, y1, kind), hedges: (x0, y0, x1, y1), woods: (x0, y0, x1, y1)) in metres of the tile."""
    def cuts():
        out, x = [0.0], 0.0
        while True:
            x += rng.uniform(250, 450)
            if x > tile - 200:
                break
            out.append(x)
        out.append(tile)
        return out
    xs, ys = cuts(), cuts()
    fields, hedges, woods = [], [], []
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            x0, x1, y0, y1 = xs[i], xs[i + 1], ys[j], ys[j + 1]
            if rng.random() < 0.09:
                woods.append((x0 + 6, y0 + 6, x1 - 6, y1 - 6))
                fields.append((x0, y0, x1, y1, "woods"))
                continue
            stack = [(x0, y0, x1, y1)]
            while stack:
                a0, b0, a1, b1 = stack.pop()
                w, h = a1 - a0, b1 - b0
                if max(w, h) < rng.uniform(90, 260):
                    r = rng.random()
                    acc = 0.0
                    kind = FIELD_KINDS[0][0]
                    for name, share, _, _ in FIELD_KINDS:
                        acc += share
                        if r < acc:
                            kind = name
                            break
                    if rng.random() < 0.04:
                        woods.append((a0 + 5, b0 + 5, a1 - 5, b1 - 5))
                        kind = "woods"
                    fields.append((a0, b0, a1, b1, kind))
                    continue
                if w >= h:
                    s = a0 + w * rng.uniform(0.32, 0.68)
                    stack += [(a0, b0, s, b1), (s, b0, a1, b1)]
                    if rng.random() < 0.55:
                        hedges.append((s, b0, s, b1))
                else:
                    s = b0 + h * rng.uniform(0.32, 0.68)
                    stack += [(a0, b0, a1, s), (a0, s, a1, b1)]
                    if rng.random() < 0.55:
                        hedges.append((a0, s, a1, s))
    # Every block edge is a hedgerow (the grid's lines, with a few gaps: gateways).
    for x in xs[:-1]:
        for j in range(len(ys) - 1):
            if rng.random() > 0.08:
                hedges.append((x, ys[j], x, ys[j + 1]))
    for y in ys[:-1]:
        for i in range(len(xs) - 1):
            if rng.random() > 0.08:
                hedges.append((xs[i], y, xs[i + 1], y))
    return fields, hedges, woods


def far_fields():
    rng = np.random.default_rng(5)
    tile = 2400.0
    size = 2048
    px = size / tile
    fields, hedges, woods = field_layout(rng, tile)
    colours = {name: c for name, _, c, _ in FIELD_KINDS}
    stripes = {name: s for name, _, _, s in FIELD_KINDS}
    colours["woods"] = WOODS
    stripes["woods"] = False

    def tile_noise(cells):
        n = rng.random((cells, cells)).astype(np.float32)
        n = np.tile(n, (3, 3))
        img = Image.fromarray((n * 255).astype(np.uint8)).resize((size * 3, size * 3), Image.BICUBIC)
        return np.asarray(img).astype(np.float32)[size:2 * size, size:2 * size] / 255.0

    rgb = np.zeros((size, size, 3), np.float32)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    for x0, y0, x1, y1, kind in fields:
        i0, i1 = int(round(x0 * px)), int(round(x1 * px))
        j0, j1 = int(round(y0 * px)), int(round(y1 * px))
        rgb[j0:j1, i0:i1] = np.array(colours[kind], np.float32) * rng.uniform(0.9, 1.08)
        if stripes[kind]:
            # Faint rows along the field's long side (a 6 m period).
            coord = yy[j0:j1, i0:i1] if (x1 - x0) >= (y1 - y0) else xx[j0:j1, i0:i1]
            rgb[j0:j1, i0:i1] *= (1.0 + 0.035 * np.sin(coord / (6.0 * px) * 2 * math.pi))[..., None]
    fine = tile_noise(96) * 0.55 + tile_noise(320) * 0.45
    rgb *= (0.9 + fine[..., None] * 0.2)
    rgb *= (0.94 + tile_noise(8)[..., None] * 0.12)
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    # Woods: darker, mottled canopy.
    for x0, y0, x1, y1 in woods:
        for _ in range(int((x1 - x0) * (y1 - y0) / 220)):
            cxp, cyp = rng.uniform(x0, x1) * px, rng.uniform(y0, y1) * px
            r = rng.uniform(3, 6)
            d.ellipse([cxp - r, cyp - r, cxp + r, cyp + r], fill=shade(WOODS, rng.uniform(0.8, 1.25)))
    # Hedgerows: a dark band of knobbly crowns along each (drawn wrapped, so the tile still tiles).
    for x0, y0, x1, y1 in hedges:
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / 9))
        for k in range(n):
            t = (k + rng.random()) / n
            x, y = (x0 + (x1 - x0) * t) * px, (y0 + (y1 - y0) * t) * px
            r = rng.uniform(1.8, 3.6)
            c = shade(HEDGE, rng.uniform(0.8, 1.2))
            for ox in (-size, 0, size):
                for oy in (-size, 0, size):
                    d.ellipse([x + ox - r, y + oy - r, x + ox + r, y + oy + r], fill=c)
    # Flat arrays (x0, y0, x1, y1 per item): Unity's JsonUtility reads flat arrays, not nested ones.
    return img, {"tile": tile, "hedges": [round(v, 1) for h in hedges for v in h],
                 "woods": [round(v, 1) for w in woods for v in w]}


def main():
    os.makedirs(ROOT, exist_ok=True)
    leaves().save(os.path.join(ROOT, "VH_LeafClusters.png"))
    grass().save(os.path.join(ROOT, "VH_GrassCards.png"))
    decals().save(os.path.join(ROOT, "VH_GroundDecals.png"))
    img, layout = far_fields()
    img.save(os.path.join(ROOT, "VH_FarFields.png"))
    with open(os.path.join(ROOT, "VH_FarFields.json"), "w") as f:
        json.dump(layout, f, separators=(",", ":"))
    billboards().save(os.path.join(ROOT, "VH_TreeBillboards.png"))
    birch_bark().save(os.path.join(ROOT, "VH_BirchBark.png"))
    print("wrote", ROOT, "hedges", len(layout["hedges"]) // 4, "woods", len(layout["woods"]) // 4)


if __name__ == "__main__":
    main()
