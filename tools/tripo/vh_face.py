"""A character's talking face: the hybrid cartoon face (docs/redesign/people.md "Faces").

    uv run --with pillow --with numpy python tools/tripo/vh_face.py <face.json>

The approach is BasilDownfall's route 1 "hybrid cartoon face" (FableAnimationTests/BasilDownfall/art/comic-style/faces/
PIPELINE.md, approved by the creator for Tripo models on 2026-10-07), carried over to a real-time decal:
1. PAINT OUT the model's own painted eyes, brows and lips (Tripo paints them flat onto the skin): each region is
   inpainted from the skin round it (a push-pull fill in a front projection, written back to every visible texel), and
   the normal map goes flat there. The base texture is clean skin, so nothing in it fights the drawing, the decal never
   carries skin (no mask edge, nothing to colour-match) and the closed eye is the skin itself.
2. DRAW the features per expression into an atlas: brows (tapered strokes with hair marks), eyes (sclera, iris, pupil,
   two highlights, the upper lash line with its flick, the crease, the lower lid line; lids that close by the
   expression's amount), and the mouth (a lip line, lips for those who have them, the open mouth with teeth and
   tongue, shaped by the expression). Rows = expressions (row 0 = the first), 6 columns = mouth closed / half / open,
   then the same with the eyes shut. VhFace picks the cell; the decal is a skinned copy of the face's front that
   carries the jaw, cheek and brow shape keys, so the drawing moves with the face in 3D.

face.json (millimetres of the model's own world x / z; the character faces -Y; +x is the picture's right):
  {"npz", "tex_in" / "tex" (the colour map in / out), "normal_in" / "normal" (the normal map in / out), "atlas", "block", "check",
   "decal_exclude" (out .npz: vertices the decal must not cover, e.g. glasses frames),
   "rect": [x0, z0, x1, z1], "eyes": [[x, z], [x, z]] (rough centres; found in an eyebox) or "eye_override":
   [[x, z, a, b], ...] (explicit centres and half sizes), "brows", "mouth", "mouthL", "mouthR",
   "draw": {...} the character's design (DESIGN below), "clean": [[x, z, rx, rz], ...] extra paint-out ellipses,
   "lenses": [[x, z, rx, rz], ...] (glasses: cleaned to skin with a faint glare), "keep": [[x, z, rx, rz], ...]
   (never painted: a lock of hair in front of the face), "frame_protos": [[r, g, b], ...]
   (colours of the glasses / cords that the decal must not cover and the fill must not borrow),
   "expressions": [names], "jaw", "smile", "frown", "brow" (shape-key strengths, fractions of the eye distance)}
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, __file__.replace("\\", "/").rsplit("/", 1)[0])
import vh_tex as vt

CELL_H = 448          # atlas cell height (px); 9 rows x 448 = 4032 <= 4096
SS = 3                # supersampling for the drawing
EXPRESSIONS = ["neutral", "warm", "happy", "sad", "worried", "hurt", "angry", "surprised", "doubtful"]

# Per expression: brow = [inner raise, outer raise, extra arch, knot] in eye half-heights (knot pulls the inner ends
# toward the nose); eye: open (1 = the rest opening), lower (the lower lid rising), pupil (scale), look ([x, z] in eye
# units), inner_down / outer_down (the upper lid slanting); mouth: corner (raise, in mouth half-widths), width (scale),
# open (rest opening, eye half-heights), shape ("line" | "grin" | "o" | "grimace"), asym ([left, right] extra corner
# raise). brow_lr overrides the brows per side ([left], [right]).
EXPR = {
    "neutral":   {"brow": [0.0, 0.0, 0.0, 0.0], "eye": {"open": 1.0, "lower": 0.0}, "mouth": {"corner": 0.04, "width": 1.0, "open": 0.0, "shape": "line"}},
    "warm":      {"brow": [0.15, 0.1, 0.12, 0.0], "eye": {"open": 0.88, "lower": 0.3}, "mouth": {"corner": 0.22, "width": 1.04, "open": 0.0, "shape": "line"}},
    "happy":     {"brow": [0.55, 0.35, 0.2, 0.0], "eye": {"open": 0.72, "lower": 0.55}, "mouth": {"corner": 0.3, "width": 1.1, "open": 0.42, "shape": "grin"}},
    "sad":       {"brow": [0.95, -0.45, -0.1, 0.25], "eye": {"open": 0.78, "lower": 0.05, "outer_down": 0.35, "look": [0.0, -0.35]}, "mouth": {"corner": -0.26, "width": 0.92, "open": 0.0, "shape": "line"}},
    "worried":   {"brow": [0.85, 0.05, 0.05, 0.4], "eye": {"open": 1.12, "lower": 0.0, "pupil": 0.85}, "mouth": {"corner": -0.12, "width": 0.78, "open": 0.14, "shape": "grimace"}},
    "hurt":      {"brow": [1.1, -0.55, -0.15, 0.45], "eye": {"open": 0.55, "lower": 0.35, "outer_down": 0.25}, "mouth": {"corner": -0.28, "width": 1.02, "open": 0.32, "shape": "grimace"}},
    "angry":     {"brow": [-1.0, 0.45, -0.1, 0.55], "eye": {"open": 0.8, "lower": 0.2, "inner_down": 0.55, "pupil": 0.85}, "mouth": {"corner": -0.2, "width": 0.88, "open": 0.0, "shape": "line"}},
    "surprised": {"brow": [1.3, 1.15, 0.35, 0.0], "eye": {"open": 1.38, "lower": 0.0, "pupil": 0.7}, "mouth": {"corner": 0.0, "width": 0.5, "open": 0.75, "shape": "o"}},
    "doubtful":  {"brow": [0.0, 0.0, 0.0, 0.0], "brow_lr": [[0.75, 0.6, 0.2], [-0.35, -0.1, 0.0]], "eye": {"open": 0.82, "lower": 0.15, "look": [0.55, 0.05]}, "mouth": {"corner": 0.0, "width": 0.92, "open": 0.0, "shape": "line", "asym": [0.32, -0.12]}},
}

# The character's design. Colours sRGB 0..1. eye_scale multiplies the detected painted eye; brow_thick and
# mouth_line_w are mm of the model; lash_w is a fraction of the eye's half height.
DESIGN = {
    "eye_scale": [1.12, 1.12], "eye_tilt": 0.12, "rest_lid": 0.0,
    "iris": [0.36, 0.22, 0.12], "iris_dark": [0.12, 0.07, 0.04], "sclera": [0.97, 0.95, 0.92],
    "lash": [0.09, 0.06, 0.05], "lash_w": 0.24, "lower_line": 0.35, "crease": 0.35, "iris_r": 0.82, "pupil_r": 0.42,
    "brow": [0.22, 0.14, 0.09], "brow_thick": 3.4, "brow_len": 1.0, "brow_arch": 0.25,
    "lip_line": [0.24, 0.1, 0.09], "lip": [0.62, 0.36, 0.33], "lip_alpha": 0.55, "lipstick": False,
    "mouth_line_w": 1.5, "closed": False,
}


# ------------------------------------------------------------------------------------------------------- the fill
def push_pull(img, w):
    """Fills img where w is 0 from the weighted pixels round it (a smooth membrane): pyramids of (img * w, w)."""
    levels = []
    a, b = img * w[..., None], w.copy()
    while min(b.shape) > 2:
        levels.append((a, b))
        h, wd = (b.shape[0] + 1) // 2 * 2, (b.shape[1] + 1) // 2 * 2
        a2 = np.zeros((h, wd, a.shape[2]), dtype=np.float32)
        b2 = np.zeros((h, wd), dtype=np.float32)
        a2[:a.shape[0], :a.shape[1]] = a
        b2[:b.shape[0], :b.shape[1]] = b
        a = a2.reshape(h // 2, 2, wd // 2, 2, -1).sum((1, 3))
        b = b2.reshape(h // 2, 2, wd // 2, 2).sum((1, 3))
    fill = a / np.maximum(b, 1e-6)[..., None]
    for a, b in reversed(levels):
        up = np.repeat(np.repeat(fill, 2, 0), 2, 1)[:b.shape[0], :b.shape[1]]
        up = Image.fromarray(np.clip(up * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.0))
        up = np.asarray(up, dtype=np.float32) / 255.0
        here = a / np.maximum(b, 1e-6)[..., None]
        k = np.clip(b, 0, 1)[..., None]
        fill = here * k + up * (1 - k)
    return fill


def ellipse_mask(W, H, px, items, feather):
    """Union of soft ellipses (mm: x, z, rx, rz) in a W x H front projection; feather in mm."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    m = np.zeros((H, W), dtype=np.float32)
    for x, z, rx, rz in items:
        cx, cy = px((x, z))
        sx, sy = px((x + rx, z - rz))
        ax, ay = max(1.0, abs(sx - cx)), max(1.0, abs(sy - cy))
        f = feather / max(rx, rz)
        d = np.sqrt(((xx - cx) / ax) ** 2 + ((yy - cy) / ay) ** 2)
        m = np.maximum(m, np.clip((1 + f - d) / (2 * f), 0, 1))
    return m


# --------------------------------------------------------------------------------------------------- the drawing
class Cell:
    """One supersampled cell; draws in mm."""

    def __init__(self, W, H, rect):
        self.W, self.H, self.rect = W * SS, H * SS, rect
        self.img = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))

    def p(self, x, z):
        x0, z0, x1, z1 = self.rect
        return ((x - x0) / (x1 - x0) * self.W, (1 - (z - z0) / (z1 - z0)) * self.H)

    def s(self, mm):
        x0, z0, x1, z1 = self.rect
        return mm / (x1 - x0) * self.W

    def layer(self):
        return Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))

    def over(self, lay, clip=None):
        if clip is not None:
            a = np.asarray(lay).copy()
            a[..., 3] = (a[..., 3].astype(np.uint16) * np.asarray(clip) // 255).astype(np.uint8)
            lay = Image.fromarray(a)
        self.img = Image.alpha_composite(self.img, lay)

    def done(self):
        return self.img.resize((self.W // SS, self.H // SS), Image.LANCZOS)


def rgba(c, a=1.0):
    return (int(c[0] * 255), int(c[1] * 255), int(c[2] * 255), int(round(255 * max(0.0, min(1.0, a)))))


def stroke(cell, pts_mm, widths_mm, col, alpha=1.0, round_caps=True):
    """A tapered stroke along a polyline (mm) with per-point widths (mm), as a layer."""
    P = np.array([cell.p(x, z) for x, z in pts_mm])
    Wd = np.array([cell.s(w) for w in widths_mm])
    T = np.gradient(P, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True) + 1e-9
    Nn = np.c_[-T[:, 1], T[:, 0]]
    L = P + Nn * (Wd[:, None] / 2)
    R = P - Nn * (Wd[:, None] / 2)
    lay = cell.layer()
    d = ImageDraw.Draw(lay)
    d.polygon([tuple(q) for q in np.r_[L, R[::-1]]], fill=rgba(col, alpha))
    if round_caps:
        for q, w in ((P[0], Wd[0]), (P[-1], Wd[-1])):
            d.ellipse([q[0] - w / 2, q[1] - w / 2, q[0] + w / 2, q[1] + w / 2], fill=rgba(col, alpha))
    return lay


def poly_mask(cell, pts_mm):
    m = Image.new("L", (cell.W, cell.H), 0)
    ImageDraw.Draw(m).polygon([cell.p(x, z) for x, z in pts_mm], fill=255)
    return m


def eye_curves(e, ex, n=48):
    """The eye's opening in mm: x, the upper and the lower lid over u in [-1, 1] (u = +1 the outer corner)."""
    cx, cz, a, b, side = e["x"], e["z"], e["a"], e["b"], e["side"]
    u = np.linspace(-1, 1, n)
    xs = cx + u * a * side
    base = e["tilt"] * u * b
    op = ex.get("open", 1.0) * (1.0 - e.get("rest_lid", 0.0))
    shape = (1 - u ** 2) ** 0.55
    peak = np.clip(1 - (u + 0.25) ** 2, 0, None) ** 0.55          # the top a little toward the nose
    upper = base + b * op * (0.6 * shape + 0.4 * peak)
    upper -= b * ex.get("inner_down", 0.0) * (1 - u) * 0.5 * shape
    upper -= b * ex.get("outer_down", 0.0) * (1 + u) * 0.5 * shape
    lower = base - b * 0.85 * (1 - u ** 2) ** 0.8 * (1 - ex.get("lower", 0.0))
    upper = np.maximum(upper, lower + 0.02 * b)
    return xs, cz + upper, cz + lower


def draw_eye(cell, e, ex, D, closed=False, happy_closed=False):
    cx, cz, a, b, side = e["x"], e["z"], e["a"], e["b"], e["side"]
    lash_w = D["lash_w"] * b
    if closed:
        u = np.linspace(-1.05, 1.1, 40)
        uc = np.clip(u, -1, 1)
        bend = 0.4 if happy_closed else -0.3
        z = cz - 0.2 * b + e["tilt"] * u * b + bend * b * (1 - uc ** 2) ** 0.8
        xs = cx + u * a * side
        w = lash_w * (0.6 + 0.5 * np.clip((u + 1) / 2, 0, 1))
        cell.over(stroke(cell, list(zip(xs, z)), w, D["lash"]))
        fx = [xs[-1], xs[-1] + side * a * 0.14]
        fz = [z[-1], z[-1] + b * 0.2]
        cell.over(stroke(cell, list(zip(fx, fz)), [lash_w * 0.7, lash_w * 0.15], D["lash"]))
        if not happy_closed:   # three short lashes hanging from the closed lid
            for t in (0.15, 0.45, 0.75):
                i = int((t + 1.05) / 2.15 * 39)
                lx = [xs[i], xs[i] + side * a * 0.06]
                lz = [z[i] - lash_w * 0.3, z[i] - b * 0.22]
                cell.over(stroke(cell, list(zip(lx, lz)), [lash_w * 0.35, lash_w * 0.12], D["lash"], 0.8))
        return
    xs, up, lo = eye_curves(e, ex)
    outline = list(zip(xs, up)) + list(zip(xs[::-1], lo[::-1]))
    clip = poly_mask(cell, outline)
    lay = cell.layer()
    ImageDraw.Draw(lay).polygon([cell.p(x, z) for x, z in outline], fill=rgba(D["sclera"]))
    cell.over(lay)
    cell.over(stroke(cell, list(zip(xs, up - b * 0.16)), [b * 0.42] * len(xs), D["lash"], 0.2), clip)
    look = ex.get("look", [0.0, 0.0])
    ix, iz = cx + look[0] * a * 0.42, cz - 0.04 * b + look[1] * b * 0.35
    ri = D["iris_r"] * b
    rp = ri * D["pupil_r"] * ex.get("pupil", 1.0)
    lay = cell.layer()
    d = ImageDraw.Draw(lay)
    c = cell.p(ix, iz)
    for k in range(10):   # the iris: a dark rim to a lighter middle
        t = k / 9.0
        rr = cell.s(ri * (1 - 0.55 * t))
        col = np.array(D["iris_dark"]) * (1 - min(1, t * 2.2)) + np.array(D["iris"]) * min(1, t * 2.2)
        d.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr], fill=rgba(col))
    rr = cell.s(rp)
    d.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr], fill=rgba([0.04, 0.03, 0.03]))
    for hx, hz, hr in ((-0.38, 0.4, 0.26), (0.32, -0.34, 0.11)):
        q = cell.p(ix + hx * ri, iz + hz * ri)
        h = cell.s(hr * ri)
        d.ellipse([q[0] - h, q[1] - h, q[0] + h, q[1] + h], fill=(255, 255, 252, 240))
    cell.over(lay, clip)
    u = np.linspace(-1, 1, len(xs))
    w = lash_w * (0.45 + 0.75 * np.clip((u + 1) / 2, 0, 1) ** 1.2)
    cell.over(stroke(cell, list(zip(xs, up + lash_w * 0.25)), w, D["lash"]))
    fx = [xs[-1] - side * a * 0.04, xs[-1] + side * a * 0.18]
    fz = [up[-1] + lash_w * 0.2, up[-1] + b * 0.3]
    cell.over(stroke(cell, list(zip(fx, fz)), [w[-1] * 0.9, lash_w * 0.15], D["lash"]))
    if D["crease"] > 0 and ex.get("open", 1.0) > 0.5:
        sel = slice(len(xs) // 6, len(xs) - len(xs) // 10)
        cz2 = up[sel] + b * (0.42 + 0.12 * ex.get("open", 1.0))
        cell.over(stroke(cell, list(zip(xs[sel], cz2)), [b * 0.07] * len(cz2), D["lash"], D["crease"]))
    if D["lower_line"] > 0:
        sel = slice(len(xs) // 5, len(xs) - 2)
        cell.over(stroke(cell, list(zip(xs[sel], lo[sel] - b * 0.04)), [b * 0.08] * len(xs[sel]), D["lash"], D["lower_line"]))


def draw_brow(cell, br, raise_, D):
    """br: the brow's rest centre (mm), its length, unit and side; raise_ = [inner, outer, arch, knot]."""
    bx, bz, L, unit, side = br["x"], br["z"], br["len"], br["unit"], br["side"]
    inner, outer, arch, knot = (list(raise_) + [0.0] * 4)[:4]
    u = np.linspace(0, 1, 32)                  # 0 = the inner end, 1 = the outer end
    x_in = bx - side * L * 0.5 + side * knot * unit * 0.6
    x_out = bx + side * L * 0.5
    xs = x_in + (x_out - x_in) * u
    zs = bz + (inner * (1 - u) + outer * u) * unit + (D["brow_arch"] + arch) * unit * np.sin(np.pi * u ** 0.8)
    zs -= 0.12 * unit * u ** 2
    th = D["brow_thick"]
    w = th * (1.0 - 0.6 * u ** 1.4) * np.clip(u * 6 + 0.55, 0, 1)
    cell.over(stroke(cell, list(zip(xs, zs)), w, D["brow"], 0.97))
    rng = np.random.default_rng(int(abs(bx) * 10 + 7))
    lay = cell.layer()
    d = ImageDraw.Draw(lay)
    dark = np.array(D["brow"]) * 0.6
    for _ in range(9):
        i = int(rng.uniform(0.1, 0.9) * 31)
        p0 = cell.p(xs[i], zs[i] - w[i] * 0.35)
        p1 = cell.p(xs[i] + side * w[i] * 0.9, zs[i] + w[i] * 0.3)
        d.line([p0, p1], fill=rgba(dark, 0.55), width=max(1, int(cell.s(th * 0.12))))
    cell.over(lay)


def mouth_curve(m, ex, n=48):
    mo = ex["mouth"]
    hw = m["hw"] * mo.get("width", 1.0)
    u = np.linspace(-1, 1, n)
    asym = mo.get("asym", [0.0, 0.0])
    corner = mo.get("corner", 0.0) + np.where(u < 0, asym[0], asym[1])
    z = m["z"] + corner * m["hw"] * u ** 2 * 0.9
    return m["x"] + u * hw, z, u, hw


def draw_mouth(cell, m, ex, level, D, unit):
    """level 0 = the expression's own mouth, 1 = talking half open, 2 = talking open."""
    mo = ex["mouth"]
    xs, zl, u, hw = mouth_curve(m, ex)
    shape = mo.get("shape", "line")
    H = (mo.get("open", 0.0) + (0.0, 0.5, 0.95)[level]) * unit
    lipc = D["lip"]
    lw = D["mouth_line_w"]
    if H < 0.08 * unit:
        if D["lipstick"]:
            top = zl + unit * 0.2 * (1 - u ** 2) ** 0.6 * (1 - 0.35 * np.exp(-(u * 4) ** 2))
            bot = zl - unit * 0.3 * (1 - u ** 2) ** 0.7
            lay = cell.layer()
            ImageDraw.Draw(lay).polygon([cell.p(x, z) for x, z in list(zip(xs, top)) + list(zip(xs[::-1], bot[::-1]))], fill=rgba(lipc, 0.95))
            cell.over(lay)
            cell.over(stroke(cell, list(zip(xs[16:32], (zl - unit * 0.17)[16:32])), [unit * 0.05] * 16, [1, 0.9, 0.9], 0.35))
        else:
            sel = slice(10, 38)
            bot = zl - unit * 0.24 * (1 - u ** 2) ** 0.7
            lay = cell.layer()
            pts = list(zip(xs[sel], zl[sel])) + list(zip(xs[sel][::-1], bot[sel][::-1]))
            ImageDraw.Draw(lay).polygon([cell.p(x, z) for x, z in pts], fill=rgba(lipc, D["lip_alpha"]))
            cell.over(lay.filter(ImageFilter.GaussianBlur(cell.s(0.5))))
        w = lw * (0.35 + 0.65 * (1 - np.abs(u)) ** 0.5)
        cell.over(stroke(cell, list(zip(xs, zl)), w, D["lip_line"]))
        c = mo.get("corner", 0.0)
        if abs(c) > 0.12:
            for sgn, i in ((-1, 0), (1, -1)):
                x, z = xs[i], zl[i]
                tx = [x - sgn * unit * 0.05, x + sgn * unit * 0.12]
                tz = [z, z + np.sign(c) * unit * 0.22]
                cell.over(stroke(cell, list(zip(tx, tz)), [lw * 0.5, lw * 0.2], D["lip_line"], 0.7))
        return
    if shape == "o":
        top = zl + H * 0.28 * (1 - u ** 2) ** 0.5
        bot = zl - H * (1 - u ** 2) ** 0.5
    elif shape == "grin":
        top = zl + H * 0.12 * (1 - u ** 2) ** 0.5
        bot = zl - H * (1 - u ** 2) ** 0.85
    elif shape == "grimace":
        top = zl + H * 0.18 * (1 - u ** 2) ** 0.4
        bot = zl - H * 0.8 * (1 - u ** 2) ** 0.35
    else:
        top = zl + H * 0.15 * (1 - u ** 2) ** 0.6
        bot = zl - H * (1 - u ** 2) ** 0.7
    outline = list(zip(xs, top)) + list(zip(xs[::-1], bot[::-1]))
    clip = poly_mask(cell, outline)
    lay = cell.layer()
    ImageDraw.Draw(lay).polygon([cell.p(x, z) for x, z in outline], fill=rgba([0.2, 0.06, 0.07]))
    cell.over(lay)
    teeth = cell.layer()
    d = ImageDraw.Draw(teeth)
    t_h = min(H * 0.32, unit * 0.32) if shape != "grimace" else H * 0.45
    tw = 0.82 if shape != "o" else 0.0
    tpts = [(m["x"] + v * hw * tw, z) for v, z in zip(u, top + unit * 0.05)] + \
           [(m["x"] + v * hw * tw, z) for v, z in zip(u[::-1], (top - t_h)[::-1])]
    if tw > 0:
        d.polygon([cell.p(x, z) for x, z in tpts], fill=rgba([0.95, 0.93, 0.88]))
    if shape in ("grimace", "grin") and H > unit * 0.4:
        bpts = [(m["x"] + v * hw * tw * 0.9, z) for v, z in zip(u, bot + t_h * 0.6)] + \
               [(m["x"] + v * hw * tw * 0.9, z) for v, z in zip(u[::-1], (bot - unit * 0.05)[::-1])]
        d.polygon([cell.p(x, z) for x, z in bpts], fill=rgba([0.9, 0.88, 0.83]))
    cell.over(teeth, clip)
    if shape != "grimace" and H > unit * 0.35:
        tongue = cell.layer()
        tc = cell.p(m["x"], float(bot[len(bot) // 2]) + H * 0.12)
        rx, ry = cell.s(hw * 0.5), cell.s(H * 0.3)
        ImageDraw.Draw(tongue).ellipse([tc[0] - rx, tc[1] - ry, tc[0] + rx, tc[1] + ry], fill=rgba([0.72, 0.32, 0.33]))
        cell.over(tongue, clip)
    lip_col = lipc if D["lipstick"] else np.array(D["lip_line"]) * 0.6 + np.array(lipc) * 0.4
    cell.over(stroke(cell, list(zip(xs, top + lw * 0.2)), lw * (0.5 + 0.6 * (1 - np.abs(u))) * (1.6 if D["lipstick"] else 0.9), lip_col))
    cell.over(stroke(cell, list(zip(xs, bot - lw * 0.3)), lw * (0.4 + 1.1 * (1 - np.abs(u)) ** 0.6) * (1.8 if D["lipstick"] else 1.0),
                     lipc, 0.95))
    for i in (0, -1):
        q = (xs[i], zl[i])
        cell.over(stroke(cell, [q, (q[0] + np.sign(xs[i] - m["x"]) * unit * 0.06, q[1])], [lw * 0.7, lw * 0.3], D["lip_line"]))


def draw_cell(CW, CH, rect, name, c, eyes, brows, mouth, D, unit):
    ex = EXPR.get(name, EXPR["neutral"])
    cell = Cell(CW, CH, rect)
    shut = c >= 3 or D["closed"]
    for b in brows:
        rz = list(ex["brow"])
        if "brow_lr" in ex:
            rz = list(ex["brow_lr"][0 if b["side"] < 0 else 1]) + [0.0]
        draw_brow(cell, b, rz, D)
    for e in eyes:
        draw_eye(cell, e, ex["eye"], D, closed=shut, happy_closed=(shut and name == "happy" and not D["closed"]))
    draw_mouth(cell, mouth, ex, c % 3, D, unit)
    return cell.done()


# -------------------------------------------------------------------------------------------------------- the main
def main(spec_path):
    s = json.load(open(spec_path, encoding="utf8"))
    D = dict(DESIGN)
    D.update(s.get("draw", {}))
    m = vt.load(s["npz"])
    names = s.get("expressions") or EXPRESSIONS
    x0, z0, x1, z1 = [v / 1000.0 for v in s["rect"]]

    # The front projection of the face (unshaded texture colour), its depth.
    tex4 = vt.load_tex(s.get("tex_in", s["tex"]))
    N = tex4.shape[0]
    tex2 = np.asarray(Image.fromarray(np.clip(tex4 * 255, 0, 255).astype(np.uint8)).resize((2048, 2048), Image.LANCZOS), dtype=np.float32) / 255.0
    _, tid, bary = vt.render_ortho(m, tex2, (x0, z0, x1, z1), 1024)
    H, W = tid.shape
    ok = tid >= 0
    tri = m["tris"][tid[ok]]
    uvp = (m["tri_uv"][tid[ok]] * bary[ok][:, :, None]).sum(1)
    face = np.zeros((H, W, 3), dtype=np.float32)
    face[ok] = vt.sample(tex2, uvp)
    depth = np.full((H, W), np.inf, dtype=np.float32)
    depth[ok] = (m["verts"][tri][:, :, 1] * bary[ok]).sum(1)

    def px(p):
        return ((p[0] / 1000.0 - x0) / (x1 - x0) * W, (1 - (p[1] / 1000.0 - z0) / (z1 - z0)) * H)

    def world(p):
        c, r = px(p)
        c = int(np.clip(round(c), 0, W - 1))
        r = int(np.clip(round(r), 0, H - 1))
        win = depth[max(0, r - 3):r + 4, max(0, c - 3):c + 4]
        y = float(np.min(win)) if np.isfinite(win).any() else float(np.min(depth[np.isfinite(depth)]))
        return [p[0] / 1000.0, y, p[1] / 1000.0]

    # The painted eyes (centre and half sizes, mm): found as in round 1, or given.
    eyes, painted = [], []
    mmpp = (x1 - x0) * 1000.0 / W
    for e in s["eyes"]:
        bw, bh = s.get("eyebox", [30, 18])
        ca, ra = px((e[0] - bw / 2, e[1] + bh / 2))
        cb, rb = px((e[0] + bw / 2, e[1] - bh / 2))
        ca, ra, cb, rb = int(ca), int(ra), int(cb), int(rb)
        box = face[ra:rb, ca:cb]
        border = np.concatenate([box[0], box[-1], box[:, 0], box[:, -1]])
        skin = np.median(border, axis=0)
        mask = np.linalg.norm(box - skin, axis=2) > s.get("eye_threshold", 0.2)
        ys, xs = np.nonzero(mask)
        if len(xs) < 10:
            raise SystemExit("no eye found near %s" % e)
        xa, xb = np.percentile(xs, [2, 98])
        ya, yb = np.percentile(ys, [2, 98])
        cxp, cyp = (xa + xb) / 2 + ca, (ya + yb) / 2 + ra
        painted.append({"x": (x0 + cxp / W * (x1 - x0)) * 1000.0, "z": (z1 - cyp / H * (z1 - z0)) * 1000.0,
                        "a": ((xb - xa) / 2 + 1) * mmpp, "b": ((yb - ya) / 2 + 1) * mmpp})
    if s.get("eye_override"):
        eyes = [{"x": x, "z": z, "a": a, "b": b} for x, z, a, b in s["eye_override"]]
    else:
        eyes = [dict(e) for e in painted]
        if s.get("symmetric_eyes", True):   # one size and height for both (the detection differs by a pixel or two)
            for k in ("a", "b", "z"):
                v = float(np.mean([e[k] for e in eyes]))
                for e in eyes:
                    e[k] = v
    eyes.sort(key=lambda e: e["x"])
    mid_x = (eyes[0]["x"] + eyes[1]["x"]) / 2
    for e in eyes:
        e["side"] = 1 if e["x"] > mid_x else -1
        e["a"] *= D["eye_scale"][0]
        e["b"] *= D["eye_scale"][1]
        e["tilt"] = D["eye_tilt"]
        e["rest_lid"] = D["rest_lid"]
    unit = float(np.mean([e["b"] for e in eyes]))          # the eye's half height: the drawing's unit
    brow_src = s.get("brows_draw") or s["brows"]
    brows = [{"x": b[0], "z": b[1], "len": 2.0 * float(np.mean([e["a"] for e in eyes])) * 1.08 * D["brow_len"],
              "unit": unit, "side": 1 if b[0] > mid_x else -1} for b in sorted(brow_src, key=lambda q: q[0])]
    mL, mR = sorted([s["mouthL"], s["mouthR"]], key=lambda q: q[0])
    mouth = {"x": s["mouth"][0], "z": s["mouth"][1], "hw": (mR[0] - mL[0]) / 2.0}

    # 1. Paint out the painted eyes, brows and lips (and any extra ellipses), inpainted from the skin round them.
    ec = s.get("eye_clean", [1.55, 1.9])
    clean = [[e["x"], e["z"], e["a"] * ec[0], e["b"] * ec[1] + 1.5] for e in painted]
    clean += [[b[0], b[1], brows[0]["len"] * s.get("brow_clean", [0.72, 1.2])[0], max(6.0, unit * s.get("brow_clean", [0.72, 1.2])[1])] for b in s["brows"]]
    clean += [[mouth["x"], mouth["z"] - 1.0, mouth["hw"] * 1.25, max(6.0, unit * 0.8)]]
    clean += s.get("clean", [])
    lenses = s.get("lenses", [])
    M = ellipse_mask(W, H, px, clean + lenses, s.get("feather", 2.5))
    frame = np.zeros((H, W), dtype=bool)
    for pr in s.get("frame_protos", []):
        frame |= np.linalg.norm(face - np.array(pr, dtype=np.float32), axis=2) < s.get("frame_dist", 0.14)
    frame &= ok
    if lenses:   # only round the glasses: the eyes' own dark paint must still be painted out
        frame &= ellipse_mask(W, H, px, [[x, z, rx + 5, rz + 5] for x, z, rx, rz in lenses], 1.0) > 0.5
    M = M * (~frame)
    if s.get("keep"):   # never painted: a lock of hair hanging in front of the face (its skin is hidden anyway)
        M = M * (1.0 - ellipse_mask(W, H, px, s["keep"], 1.5))
    src_w = ((1.0 - np.clip(M * 1.6, 0, 1)) * ok * (~frame)).astype(np.float32)
    fill = push_pull(face, src_w)
    # Regions filled from one material only (a beard over the chin's painted gap): sources are the pixels near the
    # proto colour, and a little of that material's own grain is added back so the patch isn't flat.
    rng = np.random.default_rng(5)
    for fr in s.get("fill_regions", []):
        Mr = ellipse_mask(W, H, px, [fr["ellipse"]], fr.get("feather", 2.0))
        near = (np.linalg.norm(face - np.array(fr["proto"], dtype=np.float32), axis=2) < fr.get("dist", 0.16)) & ok
        sw = (near * (1.0 - np.clip(Mr * 1.6, 0, 1))).astype(np.float32)
        fr_fill = push_pull(face, sw)
        sd = float(np.std(face[near] @ np.array([0.3, 0.59, 0.11]))) if near.any() else 0.0
        grain = Image.fromarray((rng.random((H // 3 + 1, W // 3 + 1)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
        grain = (np.asarray(grain, dtype=np.float32) / 255.0 - 0.5) * sd * 1.6
        fr_fill = np.clip(fr_fill * (1 + grain[..., None]), 0, 1)
        fill = fill * (1 - Mr[..., None]) + fr_fill * Mr[..., None]
        M = np.maximum(M, Mr * (~frame))
    if lenses:   # a faint glare across each lens: a soft diagonal band
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        G = np.zeros((H, W), dtype=np.float32)
        for x, z, rx, rz in lenses:
            cx, cy = px((x, z))
            ax = abs(px((x + rx, z))[0] - cx)
            band = np.exp(-(((xx - cx) + (yy - cy) * 0.9 + ax * 0.25) / (ax * 0.16)) ** 2)
            G = np.maximum(G, band * ellipse_mask(W, H, px, [[x, z, rx, rz]], 1.5))
        fill = fill * (1 - G[..., None] * 0.16) + G[..., None] * 0.16

    R = 2048
    okR, pos, nrm, _ = vt.raster_uv(m, R)
    cpx = (pos[..., 0] - x0) / (x1 - x0) * W
    cpy = (1 - (pos[..., 2] - z0) / (z1 - z0)) * H
    inside = okR & (cpx >= 0) & (cpx < W - 1) & (cpy >= 0) & (cpy < H - 1)
    ci = np.clip(cpx.astype(int), 0, W - 1)
    ri = np.clip(cpy.astype(int), 0, H - 1)
    visible = inside & (pos[..., 1] <= depth[ri, ci] + 0.0015)
    wt = np.where(visible, M[ri, ci], 0.0).astype(np.float32)
    col = fill[ri, ci]
    have = okR.copy()
    for _ in range(4):   # into the UV gutters, so mip levels don't bring the old paint back
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sw = np.roll(np.roll(wt, dy, 0), dx, 1)
            sc = np.roll(np.roll(col, dy, 0), dx, 1)
            sh = np.roll(np.roll(have, dy, 0), dx, 1)
            take = ~have & sh
            wt[take] = sw[take]
            col[take] = sc[take]
            have |= take
    f = N // R
    out = tex4
    for r0 in range(0, R, 256):
        ws = np.repeat(np.repeat(wt[r0:r0 + 256], f, 0), f, 1)[..., None]
        cs = np.repeat(np.repeat(col[r0:r0 + 256], f, 0), f, 1)
        o = out[r0 * f:(r0 + 256) * f]
        o[:] = o * (1 - ws) + cs * ws
    Image.fromarray(np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)).save(s["tex"], quality=95)
    nin = s.get("normal_in", s.get("normal"))
    if s.get("normal") and nin and os.path.exists(nin):
        nm = np.asarray(Image.open(nin).convert("RGB"), dtype=np.float32) / 255.0
        fN = max(1, nm.shape[0] // R)
        flat = np.array([0.5, 0.5, 1.0], dtype=np.float32)
        for r0 in range(0, R, 256):
            ws = np.repeat(np.repeat(wt[r0:r0 + 256], fN, 0), fN, 1)[..., None] * 0.85
            o = nm[r0 * fN:(r0 + 256) * fN]
            o[:] = o * (1 - ws) + flat * ws
        Image.fromarray(np.clip(nm * 255 + 0.5, 0, 255).astype(np.uint8)).save(s["normal"])
    print("paint-out: %d texels at %d" % (int((wt > 0.5).sum()), R))

    # The decal must not cover the glasses' frames or cords: those vertices (dump order) are listed.
    if s.get("decal_exclude"):
        V = m["verts"]
        T = m["tris"]
        colv = vt.sample(tex2, m["tri_uv"].reshape(-1, 2))
        acc = np.zeros((len(V), 3))
        cnt = np.zeros(len(V))
        np.add.at(acc, T.ravel(), colv[:, :3])
        np.add.at(cnt, T.ravel(), 1)
        vcol = acc / np.maximum(1, cnt)[:, None]
        exv = np.zeros(len(V), dtype=bool)
        for pr in s.get("frame_protos", []):
            exv |= np.linalg.norm(vcol - np.array(pr), axis=1) < s.get("frame_dist", 0.14) * 1.1
        exv &= (V[:, 2] > z0) & (V[:, 2] < z1) & (V[:, 0] > x0) & (V[:, 0] < x1)
        if lenses:
            zone = np.zeros(len(V), dtype=bool)
            for x, z, rx, rz in lenses:
                zone |= ((V[:, 0] * 1000 - x) / (rx + 5)) ** 2 + ((V[:, 2] * 1000 - z) / (rz + 5)) ** 2 <= 1
            exv &= zone
        np.savez_compressed(s["decal_exclude"], index=np.nonzero(exv)[0].astype(np.int32))
        print("decal exclude:", int(exv.sum()), "vertices")

    # 2. The atlas: rows = expressions, 6 columns.
    CH = CELL_H
    CW = int(round(CH * (x1 - x0) / (z1 - z0)))
    atlas = Image.new("RGBA", (CW * 6, CH * len(names)), (0, 0, 0, 0))
    rect = tuple(s["rect"])
    for r, name in enumerate(names):
        for c in range(6):
            atlas.paste(draw_cell(CW, CH, rect, name, c, eyes, brows, mouth, D, unit), (c * CW, r * CH))
    atlas.save(s["atlas"])

    # The check sheet: the cleaned face (flat) with every cell over it; rows = expressions.
    base = np.zeros((H, W, 3), dtype=np.float32)
    small = np.asarray(Image.fromarray(np.clip(out * 255, 0, 255).astype(np.uint8)).resize((2048, 2048)), dtype=np.float32) / 255.0
    base[ok] = vt.sample(small, uvp)
    base_img = Image.fromarray(np.clip(base * 255, 0, 255).astype(np.uint8)).resize((CW, CH), Image.LANCZOS).convert("RGBA")
    sheet = Image.new("RGB", (CW * 6, CH * len(names)), (30, 30, 34))
    for r in range(len(names)):
        for c in range(6):
            tile = Image.alpha_composite(base_img, atlas.crop((c * CW, r * CH, (c + 1) * CW, (r + 1) * CH)))
            sheet.paste(tile.convert("RGB"), (c * CW, r * CH))
        ImageDraw.Draw(sheet).text((6, r * CH + 4), names[r], fill=(255, 255, 0))
    sheet.resize((sheet.width // 2, sheet.height // 2), Image.LANCZOS).save(s["check"])

    eL, eR = eyes[1], eyes[0]
    block = {"eyeL": world((eL["x"], eL["z"])), "eyeR": world((eR["x"], eR["z"])),
             "browL": world((brows[1]["x"], brows[1]["z"])), "browR": world((brows[0]["x"], brows[0]["z"])),
             "mouth": world(s["mouth"]), "mouthL": world(mR), "mouthR": world(mL), "decal": [x0, z0, x1, z1],
             "eyes_mm": [[round(e[k], 2) for k in ("x", "z", "a", "b")] for e in eyes], "rows": len(names),
             "expressions": names}
    if s.get("decal_exclude"):
        block["decal_exclude"] = s["decal_exclude"]
    for k in ("jaw", "smile", "frown", "brow"):
        if k in s:
            block[k] = s[k]
    json.dump(block, open(s["block"], "w"), indent=1)
    print("eyes (mm)", block["eyes_mm"], "atlas", atlas.size, "rows", len(names))


if __name__ == "__main__":
    main(sys.argv[1])
