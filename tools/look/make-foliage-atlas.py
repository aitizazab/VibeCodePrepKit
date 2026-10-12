"""Painted foliage and ground-detail atlases for Visiting Hours' look (the Look agent, pass 2).

Writes, into Assets/JamKit/Templates/VisitingHours/Look/Textures/:
  VH_LeafClusters.png  1024 x 1024, 2 x 2 cells: leaf clusters for the tree crowns (VhLookTrees). Many small leaves
                       (pass 1's were a few big ones, "oversized leaf cards"), olive and warm greens that sit in the
                       golden-hour grade, darker towards the cluster's base (painted occlusion), lighter at the rim.
                       Cells: 0 broad leaves (maple/linden), 1 broad leaves, warmer (oak), 2 fine small leaves (birch),
                       3 long narrow leaves (willow/locust).
  VH_GrassCards.png    1024 x 1024, 2 x 2: 0 green grass tuft, 1 dry gold tuft, 2 wildflowers in grass (clover, dandelion,
                       chicory), 3 low weeds (clover trefoils, plantain). Cards stand on the ground, bottom edge = root.
  VH_GroundDecals.png  1024 x 1024, 2 x 2: 0 leaf litter (alpha-clipped), 1 oil stain, 2 dirt/water stain, 3 a grime streak
                       (soft alpha, alpha-blended).
  VH_TreeBillboards.png 1024 x 1024, 2 x 2: whole-tree silhouettes (trunk and crown) for the trees' far LOD, a crossed
                       billboard: 0 maple, 1 oak, 2 birch, 3 elm (VhLookTrees.Species order). A cell is 10 m wide and tall, the
                       ground at its bottom edge.
  VH_FarFields.png     1024 x 1024 tiling: patchy fields, hedgerows and dry grass for the distant terrain ring (VhLookBackdrop),
                       seen through haze at 300 m - 3 km.

    uv run --with numpy --with pillow python -I tools/look/make-foliage-atlas.py

Deterministic (fixed seeds). Everything is drawn here, so the textures are the project's own.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "JamKit", "Templates",
                    "VisitingHours", "Look", "Textures")
SS = 2          # supersampling for antialiased edges
CELL = 512 * SS


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def shade(c, k):
    return tuple(int(max(0, min(255, round(v * k)))) for v in c[:3])


# ---------------- Leaves ----------------

OLIVE = [(70, 80, 34), (92, 104, 44), (112, 126, 52), (132, 144, 60), (150, 154, 68), (168, 164, 78)]
WARM = [(84, 86, 36), (108, 110, 44), (130, 128, 52), (154, 146, 62), (176, 160, 72)]
FINE = [(96, 112, 46), (118, 136, 56), (140, 156, 66), (162, 170, 78), (182, 180, 92)]
NARROW = [(78, 92, 40), (100, 116, 50), (124, 138, 60), (146, 152, 70)]


def leaf_poly(cx, cy, length, width, angle, shape):
    """A leaf outline: a pointed lens (shape 0), a rounder ovate leaf (1), a narrow lance (2)."""
    pts = []
    n = 14
    for i in range(n + 1):
        t = i / n
        # half-width profile along the leaf: 0 at the stalk and the tip
        if shape == 1:
            w = math.sin(math.pi * t) ** 0.7 * (1.0 - 0.25 * t)
        elif shape == 2:
            w = math.sin(math.pi * t) ** 1.2 * 0.6
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


def leaf_cluster(rng, palette, count, size, shape, lobes, twig=(78, 60, 40)):
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = CELL / 2
    R = CELL * 0.43
    # The cluster's outline: a few overlapping lobes (kept inside the cell).
    blobs = [(c + rng.uniform(-0.25, 0.25) * R, c + rng.uniform(-0.2, 0.25) * R, R * rng.uniform(0.5, 0.68)) for _ in range(lobes)]
    blobs.append((c, c + R * 0.05, R * 0.62))

    def inside(x, y):
        best = -1e9
        for bx, by, br in blobs:
            best = max(best, 1.0 - math.hypot(x - bx, y - by) / br)
        return best

    # Twigs from the base, under the leaves.
    base = (c + rng.uniform(-0.05, 0.05) * R, c + R * 0.95)
    for _ in range(7):
        bx, by, br = blobs[rng.integers(len(blobs))]
        tx, ty = bx + rng.uniform(-0.5, 0.5) * br, by + rng.uniform(-0.6, 0.3) * br
        mx, my = (base[0] + tx) / 2 + rng.uniform(-0.1, 0.1) * R, (base[1] + ty) / 2
        d.line([base, (mx, my), (tx, ty)], fill=twig + (255,), width=int(5 * SS), joint="curve")

    placed = 0
    tries = 0
    leaves = []
    while placed < count and tries < count * 40:
        tries += 1
        x = c + rng.uniform(-1, 1) * R
        y = c + rng.uniform(-1, 1) * R
        k = inside(x, y)
        if k < 0.04:
            continue
        leaves.append((x, y, k))
        placed += 1
    # Back to front: leaves lower in the cluster first (they're behind), so the top/rim ones overlap them.
    leaves.sort(key=lambda p: (p[2] * 0.3 - (p[1] - c) / R))
    for x, y, k in leaves:
        L = size * rng.uniform(0.75, 1.25) * SS
        W = L * (0.55 if shape != 2 else 0.32) * rng.uniform(0.85, 1.15)
        # Leaves point outward from the cluster's centre, with scatter.
        ang = math.atan2(y - c, x - c) + rng.normal(0, 0.7)
        # Colour: base from the palette; darker deep inside and low (painted occlusion), lighter at the rim and the top.
        col = palette[rng.integers(len(palette))]
        rim = 1.0 - min(1.0, k * 1.6)
        top = max(0.0, (c - y) / R)
        light = 0.72 + 0.32 * rim + 0.18 * top + rng.uniform(-0.06, 0.06)
        base_col = shade(col, light)
        tip_col = shade(col, light * 1.12)
        poly = leaf_poly(x, y, L, W, ang, shape)
        d.polygon(poly, fill=base_col + (255,))
        # A lighter far half (the leaf's lit side) and a midrib.
        half = poly[: len(poly) // 2 + 1]
        mid = [(x, y)] + half[len(half) // 3: len(half) * 2 // 3] + [half[-1]]
        d.polygon(mid, fill=tip_col + (255,))
        ex, ey = x + math.cos(ang) * L * 0.9, y + math.sin(ang) * L * 0.9
        d.line([(x, y), (ex, ey)], fill=shade(col, light * 0.82) + (255,), width=max(1, int(1.2 * SS)))
    return img


def atlas(cells):
    out = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for i, cell in enumerate(cells):
        # Cell i at (i % 2, i // 2) with v = 0 at the bottom (Unity): row 0 of the image is the top, so cells 0-1 go at the
        # image's bottom half.
        ox = (i % 2) * CELL
        oy = (1 - i // 2) * CELL
        out.alpha_composite(cell, (ox, oy))
    return out.resize((1024, 1024), Image.LANCZOS)


def bleed(img):
    """Fill transparent pixels' colour from their neighbours (so mips and filtering don't pull in black edges)."""
    a = np.asarray(img).astype(np.float32)
    rgb, al = a[..., :3], a[..., 3:4] / 255.0
    acc, w = rgb * al, al.copy()
    out = rgb.copy()
    for r in (2, 4, 8, 16, 32):
        k = Image.fromarray(np.clip(acc, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(r))
        kw = Image.fromarray(np.clip(w[..., 0] * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(r))
        kk = np.asarray(k).astype(np.float32)
        ww = np.asarray(kw).astype(np.float32)[..., None] / 255.0
        fill = kk / np.maximum(ww, 1e-4)
        empty = (al[..., 0] < 0.5) & (ww[..., 0] > 0.002) & (out.sum(axis=2) == rgb.sum(axis=2))
        out[empty] = fill[empty]
    res = np.concatenate([out, a[..., 3:4]], axis=2)
    return Image.fromarray(np.clip(res, 0, 255).astype(np.uint8), "RGBA")


def leaves():
    rng = np.random.default_rng(11)
    cells = [
        leaf_cluster(rng, OLIVE, 210, 52, 0, 4),
        leaf_cluster(rng, WARM, 190, 56, 1, 4, twig=(84, 64, 44)),
        leaf_cluster(rng, FINE, 320, 36, 1, 5, twig=(96, 86, 74)),
        leaf_cluster(rng, NARROW, 230, 62, 2, 4),
    ]
    return bleed(atlas(cells))


# ---------------- Grass and flowers ----------------

GRASS = [(86, 104, 40), (104, 122, 48), (124, 138, 56), (144, 150, 66), (160, 160, 78)]
DRY = [(150, 128, 70), (172, 148, 82), (190, 166, 96), (140, 132, 72), (120, 120, 60)]


def blade(d, rng, x0, h, lean, w0, col, tip):
    pts_l, pts_r = [], []
    n = 10
    for i in range(n + 1):
        t = i / n
        x = x0 + lean * t * t * h
        y = CELL - 1 - t * h
        w = w0 * (1.0 - t) ** 0.9
        pts_l.append((x - w, y))
        pts_r.append((x + w, y))
    d.polygon(pts_l + pts_r[::-1], fill=col + (255,))
    # The lit upper half of the blade.
    k = n // 2
    d.polygon(pts_l[k:] + [pts_r[-1]] + [((pts_l[i][0] + pts_r[i][0]) / 2, pts_l[i][1]) for i in range(n, k - 1, -1)],
              fill=tip + (255,))


def grass_tuft(rng, palette, blades, hmin, hmax, spread=0.42):
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    items = []
    for _ in range(blades):
        x0 = CELL / 2 + rng.normal(0, spread * CELL * 0.5)
        x0 = min(max(x0, CELL * 0.08), CELL * 0.92)
        h = rng.uniform(hmin, hmax) * CELL
        items.append((h, x0))
    items.sort(reverse=True)   # tall ones behind
    for h, x0 in items:
        lean = rng.normal(0, 0.28) + (x0 - CELL / 2) / CELL * 0.6
        col = palette[rng.integers(len(palette))]
        k = rng.uniform(0.8, 1.05)
        blade(d, rng, x0, h, lean, rng.uniform(4, 7) * SS, shade(col, k * 0.85), shade(col, k * 1.1))
    return img


def flowers(rng):
    img = grass_tuft(rng, GRASS, 46, 0.25, 0.62)
    d = ImageDraw.Draw(img)
    kinds = [((246, 244, 232), (232, 196, 70), 9), ((240, 200, 40), (220, 160, 30), 8), ((150, 160, 224), (120, 120, 190), 9),
             ((236, 220, 236), (220, 170, 70), 7)]
    for _ in range(16):
        x = CELL / 2 + rng.normal(0, CELL * 0.22)
        x = min(max(x, CELL * 0.1), CELL * 0.9)
        h = rng.uniform(0.3, 0.75) * CELL
        lean = rng.normal(0, 0.06) * h
        top = (x + lean, CELL - h)
        d.line([(x, CELL - 1), top], fill=(92, 112, 44, 255), width=int(3 * SS))
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
    # Short grass behind, trefoils and plantain rosettes low in front.
    tuft = grass_tuft(rng, GRASS, 40, 0.15, 0.4, 0.5)
    img.alpha_composite(tuft)
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
    cells = [grass_tuft(rng, GRASS, 90, 0.3, 0.92), grass_tuft(rng, DRY, 80, 0.3, 0.95), flowers(rng), weeds(rng)]
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


def blotch(rng, colour, strength, ring=0.0, elong=1.0):
    size = CELL
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    cx = cy = size / 2
    r = np.hypot((xx - cx) / elong, yy - cy) / (size * 0.42)
    n = noise(rng, size, [128, 48, 16])
    f = np.clip(1.0 - r + n * 0.55, 0, 1)
    a = np.clip(f * 1.6, 0, 1) ** 1.3 * strength
    if ring > 0:
        a += np.exp(-((f - 0.12) / 0.05) ** 2) * ring
    a = np.clip(a, 0, 1)
    rgb = np.zeros((size, size, 3), np.float32)
    rgb[:] = colour
    rgb *= (1.0 + n[..., None] * 0.4)
    out = np.concatenate([np.clip(rgb, 0, 255), a[..., None] * 255], axis=2)
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def streak(rng):
    size = CELL
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    n = noise(rng, size, [96, 24, 8])
    across = np.abs(xx - size / 2) / (size * 0.32)
    down = yy / size   # stronger at the bottom (where water runs off)
    a = np.clip((1.0 - across + n * 0.4), 0, 1) * np.clip(0.25 + down, 0, 1) * 0.75
    rgb = np.zeros((size, size, 3), np.float32)
    rgb[:] = (52, 46, 38)
    out = np.concatenate([rgb, np.clip(a, 0, 1)[..., None] * 255], axis=2)
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def litter(rng):
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cols = [(150, 96, 48), (176, 124, 56), (196, 150, 70), (120, 104, 56), (110, 76, 44), (160, 140, 72)]
    for _ in range(70):
        # Clumped towards the middle (a drift against a kerb or fence), sparser outward.
        x = CELL / 2 + rng.normal(0, CELL * 0.2)
        y = CELL / 2 + rng.normal(0, CELL * 0.2)
        if not (CELL * 0.06 < x < CELL * 0.94 and CELL * 0.06 < y < CELL * 0.94):
            continue
        L = rng.uniform(26, 44) * SS
        col = shade(cols[rng.integers(len(cols))], rng.uniform(0.85, 1.1))
        ang = rng.uniform(0, 2 * math.pi)
        d.polygon(leaf_poly(x, y, L, L * 0.6, ang, rng.integers(2)), fill=col + (255,))
        d.line([(x, y), (x + math.cos(ang) * L, y + math.sin(ang) * L)], fill=shade(col, 0.75) + (255,), width=max(1, SS))
    return img


def decals():
    rng = np.random.default_rng(37)
    cells = [litter(rng), blotch(rng, (24, 20, 18), 0.78, ring=0.12), blotch(rng, (92, 74, 52), 0.5, ring=0.25, elong=1.4),
             streak(rng)]
    out = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for i, cell in enumerate(cells):
        out.paste(cell, ((i % 2) * CELL, (1 - i // 2) * CELL))
    out = out.resize((1024, 1024), Image.LANCZOS)
    return bleed(out)


# ---------------- Whole-tree billboards ----------------

def tree_silhouette(rng, palette, kind, leaf):
    """A tree seen from the side: crown lobes (by species) filled with leaves, the trunk and limbs below/through."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = CELL / 10.0   # pixels per metre
    base = CELL - 1
    cx = CELL / 2
    # (crown centre height, half width, half height, fork height, trunk radius) per species, metres (a 10 m tree).
    shape = {0: (6.4, 3.8, 3.3, 2.8, 0.36), 1: (6.6, 4.7, 2.9, 2.3, 0.42), 2: (6.2, 2.4, 3.7, 2.6, 0.2), 3: (7.0, 4.3, 2.8, 3.0, 0.36)}[kind]
    cy, rx, ry, fork, tr = shape
    bark = (88, 70, 52) if kind != 2 else (206, 200, 186)
    top = cy + ry * (0.9 if kind == 2 else 0.6)
    d.polygon([(cx - tr * m * 1.6, base), (cx - tr * m, base - 0.6 * m), (cx - tr * 0.6 * m, base - top * m),
               (cx + tr * 0.6 * m, base - top * m), (cx + tr * m, base - 0.6 * m), (cx + tr * m * 1.6, base)], fill=bark + (255,))
    for k in range(5):
        a = -math.pi / 2 + (k - 2) * (0.35 if kind == 3 else 0.5)
        x0, y0 = cx, base - fork * m
        x1 = cx + math.cos(a) * rx * 0.75 * m
        y1 = base - (cy + math.sin(-a) * 0.2 * ry) * m - ry * 0.3 * m
        d.line([(x0, y0), (x1, y1)], fill=bark + (255,), width=int(tr * m * 0.5))
    blobs = []
    for _ in range(9):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.2, 0.75)
        bx = cx + math.cos(a) * rx * r * m
        by = base - (cy + math.sin(a) * ry * r) * m
        blobs.append((bx, by, min(rx, ry) * rng.uniform(0.45, 0.6) * m))
    blobs.append((cx, base - cy * m, min(rx, ry) * 0.7 * m))

    def inside(x, y):
        e = ((x - cx) / (rx * m)) ** 2 + ((y - (base - cy * m)) / (ry * m)) ** 2
        if e > 1:
            return -1
        return max(1 - math.hypot(x - bx, y - by) / br for bx, by, br in blobs)

    leaves = []
    for _ in range(2600):
        x = cx + rng.uniform(-1, 1) * rx * m
        y = base - (cy + rng.uniform(-1, 1) * ry) * m
        k = inside(x, y)
        if k > 0.02:
            leaves.append((x, y, k))
    leaves.sort(key=lambda p: -p[1])
    for x, y, k in leaves:
        L = leaf * rng.uniform(0.8, 1.2) * SS
        col = palette[rng.integers(len(palette))]
        rim = 1.0 - min(1.0, k * 1.4)
        tp = max(0.0, (base - cy * m - y) / (ry * m))
        light = 0.7 + 0.3 * rim + 0.2 * tp + rng.uniform(-0.06, 0.06)
        ang = math.atan2(y - (base - cy * m), x - cx) + rng.normal(0, 0.8)
        d.polygon(leaf_poly(x, y, L, L * 0.55, ang, 1), fill=shade(col, light) + (255,))
    return img


def billboards():
    rng = np.random.default_rng(41)
    cells = [tree_silhouette(rng, OLIVE, 0, 26), tree_silhouette(rng, WARM, 1, 28), tree_silhouette(rng, FINE, 2, 20),
             tree_silhouette(rng, NARROW, 3, 26)]
    return bleed(atlas(cells))


# ---------------- Far fields ----------------

def far_fields():
    rng = np.random.default_rng(5)
    size = 1024

    def tile_noise(cells):
        n = rng.random((cells, cells)).astype(np.float32)
        n = np.tile(n, (3, 3))
        img = Image.fromarray((n * 255).astype(np.uint8)).resize((size * 3, size * 3), Image.BICUBIC)
        return np.asarray(img).astype(np.float32)[size:2 * size, size:2 * size] / 255.0

    big = tile_noise(6) * 0.6 + tile_noise(14) * 0.3 + tile_noise(48) * 0.1
    fine = tile_noise(96) * 0.6 + tile_noise(256) * 0.4
    green = np.array([92, 110, 50], np.float32)
    olive = np.array([110, 112, 56], np.float32)
    gold = np.array([170, 150, 86], np.float32)
    dark = np.array([58, 70, 36], np.float32)
    t = np.clip((big - 0.3) / 0.4, 0, 1)[..., None] * 0.8
    rgb = green * (1 - t) + gold * t
    m = np.clip((big - 0.55) / 0.2, 0, 1)[..., None] * 0.5
    rgb = rgb * (1 - m) + olive * m
    # Hedgerows / tree lines: thin dark bands along noise contours.
    h = np.exp(-((big - 0.48) / 0.03) ** 2)[..., None] * 0.22
    rgb = rgb * (1 - h) + dark * h
    rgb *= (0.88 + fine[..., None] * 0.24)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")


def main():
    os.makedirs(ROOT, exist_ok=True)
    leaves().save(os.path.join(ROOT, "VH_LeafClusters.png"))
    grass().save(os.path.join(ROOT, "VH_GrassCards.png"))
    decals().save(os.path.join(ROOT, "VH_GroundDecals.png"))
    far_fields().save(os.path.join(ROOT, "VH_FarFields.png"))
    billboards().save(os.path.join(ROOT, "VH_TreeBillboards.png"))
    print("wrote", ROOT)


if __name__ == "__main__":
    main()
