"""Draws the hill's generated texture packs (Night 1-3, Larch Hill, golden hour) into
Assets/JamKit/Templates/VisitingHours/Dream/Street/Textures/<pack>/ to the layouts in
Assets/JamKit/Templates/VisitingHours/Dream/Editor/Street/Kit/HillAtlas.cs.

    uv run --with pillow --with numpy python tools/art/hill-textures.py            # every pack
    uv run --with pillow --with numpy python tools/art/hill-textures.py gen_signs  # only the packs named

Packs drawn here:
  gen_interiors, gen_interiors_shop, gen_interiors_ward   rooms seen through windows (opaque PNG)
  gen_signs                                               painted signboards (HillAtlas.SignTexts)
  gen_awning_red / _green / _blue                         striped awning canvas, tileable
  gen_leaves, gen_leaves_blossom, gen_ivy                 2 x 2 leaf clumps with alpha (+ normal)
  gen_grass_cards, gen_flowers                            4 x 1 tufts rooted at the bottom edge (+ normal)
  gen_cracks, gen_stains, gen_leaf_decals                 2 x 2 decals with alpha
  gen_manhole, gen_drain                                  one decal each (+ normal)
  gen_white                                               a small plain white PNG
and four surface packs no CC0 library had in a usable form (tileable): hedge, water, glass, painted_wood.

Conventions: UV (0,0) is the bottom-left of the image as Unity reads it; atlas cells are counted with row 0 at the TOP of the
image (HillAtlas.Cell flips rows). Normal maps are OpenGL convention (+Y = up in the image). Every image is deterministic
(fixed seeds) and is written only after it has been drawn completely, so the script is safe to re-run.
Fonts: DM Serif Display and Crimson Text (SIL Open Font License), already in the repo.
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
TEX = ROOT / "Assets/JamKit/Templates/VisitingHours/Dream/Street/Textures"
FONTS = ROOT / "Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentFonts"

SIGN_TEXTS = [
    "ABERNATHY CYCLES", "LARCH HILL BAKERY", "CREST DINER", "ROSA'S FLOWERS", "HILL PHARMACY", "SUDS LAUNDROMAT",
    "THE NEWS NOOK", "PRUITT HARDWARE", "ST. BRIGID'S HOSPITAL", "EMERGENCY", "ST. ANNE'S CHURCH", "LARCH HILL PRIMARY",
    "MILL STREET", "ROWAN STREET", "LARCH STREET", "ROUTE 9  ST. BRIGID'S",
]


# ----------------------------------------------------------------------------------------------------------------------
# Basics
# ----------------------------------------------------------------------------------------------------------------------

def C(h):
    """'#rrggbb' -> float RGB array."""
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def mix(a, b, t):
    return np.asarray(a, np.float32) * (1 - t) + np.asarray(b, np.float32) * t


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def fbm(h, w, beta=2.0, seed=0, lo=1.0, hi=None, aniso=1.0):
    """Tileable spectral noise in [0,1]: white noise shaped by 1/f^beta (beta 2 = soft clouds, 1 = grainy).
    aniso < 1 stretches features along x (wood grain, brush strokes); > 1 along y."""
    rng = np.random.default_rng(seed)
    wn = rng.normal(size=(h, w))
    fy = np.fft.fftfreq(h)[:, None] * h
    fx = np.fft.fftfreq(w)[None, :] * w
    f = np.sqrt((fx / aniso) ** 2 + fy ** 2) if aniso < 1 else np.sqrt(fx ** 2 + (fy * aniso) ** 2)
    f[0, 0] = 1
    amp = 1.0 / np.maximum(f, lo) ** beta
    if hi is not None:
        amp *= np.exp(-(f / hi) ** 2)
    amp[0, 0] = 0
    n = np.real(np.fft.ifft2(np.fft.fft2(wn) * amp))
    n -= n.min()
    n /= max(n.max(), 1e-8)
    return n.astype(np.float32)


def blur(a, sigma, wrap=False):
    """Gaussian blur of a 2-D or 3-D float array (FFT; wrap=True keeps tileable images tileable, otherwise edges are padded)."""
    if sigma <= 0:
        return a
    pad = 0 if wrap else int(sigma * 3) + 1
    if pad:
        widths = ((pad, pad), (pad, pad)) + (((0, 0),) if a.ndim == 3 else ())
        a = np.pad(a, widths, mode="edge")
    h, w = a.shape[:2]
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (math.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    if a.ndim == 3:
        out = np.stack([np.real(np.fft.ifft2(np.fft.fft2(a[..., i]) * g)) for i in range(a.shape[2])], -1)
    else:
        out = np.real(np.fft.ifft2(np.fft.fft2(a) * g))
    if pad:
        out = out[pad:-pad, pad:-pad]
    return out.astype(np.float32)


def normal_from_height(hgt, strength=4.0, wrap=False):
    """OpenGL normal map (+Y up in the image) from a height field in [0,1]; returns RGB in [0,1]."""
    if wrap:
        dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5
        drow = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5
    else:
        dx = np.gradient(hgt, axis=1)
        drow = np.gradient(hgt, axis=0)
    nx = -dx * strength
    ny = drow * strength          # image rows grow downward; +Y is up
    nz = np.ones_like(hgt)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], -1) * 0.5 + 0.5


def down(a, f):
    """Box-downsample a float image by an integer factor (supersampling resolve)."""
    if f == 1:
        return a
    h, w = a.shape[:2]
    if a.ndim == 3:
        return a.reshape(h // f, f, w // f, f, a.shape[2]).mean((1, 3))
    return a.reshape(h // f, f, w // f, f).mean((1, 3))


def to8(a):
    return (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)


def save(path, rgb, alpha=None, quality=None):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if alpha is not None:
        im = Image.fromarray(np.dstack([to8(rgb), to8(alpha)]), "RGBA")
    else:
        im = Image.fromarray(to8(rgb), "RGB")
    tmp = path.with_name(path.stem + ".tmp" + path.suffix)
    if path.suffix.lower() == ".jpg":
        im.save(tmp, quality=quality or 92, subsampling=0)
    else:
        im.save(tmp, optimize=True)
    tmp.replace(path)
    print("  wrote", path.relative_to(ROOT), im.size, path.stat().st_size, "bytes")


class Canvas:
    """A float RGB(+alpha, +height) image with hard-edged shape fills (draw at 2x and resolve with down())."""

    def __init__(self, w, h, rgb=(0, 0, 0), alpha=False, height=False):
        self.w, self.h = w, h
        self.c = np.empty((h, w, 3), np.float32)
        self.c[:] = rgb
        self.a = np.zeros((h, w), np.float32) if alpha else None
        self.ht = np.zeros((h, w), np.float32) if height else None

    def _region(self, xs, ys, pad=2):
        x0 = max(int(math.floor(min(xs))) - pad, 0)
        y0 = max(int(math.floor(min(ys))) - pad, 0)
        x1 = min(int(math.ceil(max(xs))) + pad, self.w)
        y1 = min(int(math.ceil(max(ys))) + pad, self.h)
        return x0, y0, x1, y1

    def shape(self, kind, pts, color, alpha=1.0, width=1, grad=None, hval=None, hmode="replace", fn=None):
        """Fill a 'poly' (points), 'ellipse' (x0,y0,x1,y1) or 'line' (points, width) with a colour.
        grad=(top, bottom) multiplies the colour by a vertical ramp over the shape's box; fn(xx, yy) -> multiplier or RGB array.
        hval writes the height channel ('replace' or 'max')."""
        if kind == "ellipse":
            xs, ys = [pts[0], pts[2]], [pts[1], pts[3]]
        else:
            xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        pad = int(width) + 2
        x0, y0, x1, y1 = self._region(xs, ys, pad)
        if x1 <= x0 or y1 <= y0:
            return None
        m = Image.new("L", (x1 - x0, y1 - y0), 0)
        d = ImageDraw.Draw(m)
        if kind == "poly":
            d.polygon([(x - x0, y - y0) for x, y in pts], fill=255)
        elif kind == "ellipse":
            d.ellipse([pts[0] - x0, pts[1] - y0, pts[2] - x0, pts[3] - y0], fill=255)
        elif kind == "line":
            d.line([(x - x0, y - y0) for x, y in pts], fill=255, width=max(1, int(round(width))), joint="curve")
        m = np.asarray(m, np.float32) * (alpha / 255.0)
        self.comp(x0, y0, m, color, grad, hval, hmode, fn, (min(ys), max(ys)))
        return (x0, y0, m)

    def poly(self, pts, color, **kw):
        return self.shape("poly", pts, color, **kw)

    def ellipse(self, cx, cy, rx, ry, color, **kw):
        return self.shape("ellipse", (cx - rx, cy - ry, cx + rx, cy + ry), color, **kw)

    def line(self, pts, color, width=1, **kw):
        return self.shape("line", pts, color, width=width, **kw)

    def comp(self, x0, y0, m, color, grad=None, hval=None, hmode="replace", fn=None, yr=None):
        h, w = m.shape
        if not m.any():
            return
        reg = self.c[y0:y0 + h, x0:x0 + w]
        col = np.asarray(color, np.float32)
        if fn is not None or grad is not None:
            yy, xx = np.mgrid[y0:y0 + h, x0:x0 + w].astype(np.float32)
            col = np.broadcast_to(col, (h, w, 3)).copy()
            if grad is not None:
                ya, yb = yr if yr else (y0, y0 + h)
                t = np.clip((yy - ya) / max(yb - ya, 1), 0, 1)[..., None]
                col *= grad[0] * (1 - t) + grad[1] * t
            if fn is not None:
                r = np.asarray(fn(xx, yy), np.float32)
                col = col * (r[..., None] if r.ndim == 2 else r)
        mm = m[..., None]
        reg[:] = reg * (1 - mm) + col * mm
        if self.a is not None:
            ra = self.a[y0:y0 + h, x0:x0 + w]
            ra[:] = ra + m * (1 - ra)
        if self.ht is not None and hval is not None:
            rh = self.ht[y0:y0 + h, x0:x0 + w]
            if hmode == "max":
                rh[:] = np.maximum(rh, hval * (m > 0.5))
            else:
                rh[:] = rh * (1 - m) + hval * m

    def glow(self, cx, cy, r, color, strength=1.0, sy=None):
        """Additive soft light (a lamp's glow): Gaussian of radius r (sy: a different vertical radius)."""
        sy = sy or r
        x0, y0 = max(int(cx - 3 * r), 0), max(int(cy - 3 * sy), 0)
        x1, y1 = min(int(cx + 3 * r) + 1, self.w), min(int(cy + 3 * sy) + 1, self.h)
        if x1 <= x0 or y1 <= y0:
            return
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        g = np.exp(-(((xx - cx) / r) ** 2 + ((yy - cy) / sy) ** 2) * 0.5)
        self.c[y0:y1, x0:x1] += g[..., None] * np.asarray(color, np.float32) * strength


# ----------------------------------------------------------------------------------------------------------------------
# Rooms behind windows (gen_interiors, gen_interiors_shop, gen_interiors_ward)
# ----------------------------------------------------------------------------------------------------------------------

class Room:
    """A box room seen through a window that fills the cell: x 0..1 left to right, y 0..1 floor to ceiling,
    z 0 (the window) .. 1 (the back wall). r is the back wall's size relative to the window (smaller = deeper)."""

    def __init__(self, S, seed, vp=(0.5, 0.47), r=0.5):
        self.S, self.vp, self.r = S, vp, r
        self.rng = np.random.default_rng(seed)
        self.cv = Canvas(S, S, (0.3, 0.27, 0.25))
        self.eye = (vp[0], 1 - vp[1])

    def _s(self, z):
        return 1.0 / (1.0 + z * (1.0 / self.r - 1.0))

    def P(self, x, y, z):
        S = self.S
        s = self._s(z)
        vx, vy = self.vp[0] * S, self.vp[1] * S
        return (vx + (x * S - vx) * s, vy + ((1 - y) * S - vy) * s)

    def quad(self, pts3, col, **kw):
        return self.cv.poly([self.P(*p) for p in pts3], col, **kw)

    # planes ------------------------------------------------------------------------------------------------------
    def shell(self, wall, floor, ceil, side=None, wall_fn=None, floor_kind="boards", skirting=(0.92, 0.9, 0.86)):
        side = wall if side is None else side
        S, P = self.S, self.P
        self.cv.poly([P(0, 1, 0), P(1, 1, 0), P(1, 1, 1), P(0, 1, 1)], ceil, grad=(0.85, 1.05))
        self.cv.poly([P(0, 0, 0), P(0, 1, 0), P(0, 1, 1), P(0, 0, 1)], np.asarray(side) * 0.82,
                     fn=lambda xx, yy: 0.8 + 0.25 * np.clip(xx / (self.vp[0] * S), 0, 1))
        self.cv.poly([P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1)], np.asarray(side) * 0.9,
                     fn=lambda xx, yy: 0.8 + 0.25 * np.clip((S - xx) / ((1 - self.vp[0]) * S), 0, 1))
        self.cv.poly([P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1)], wall, grad=(0.96, 1.02), fn=wall_fn)
        self.cv.poly([P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1)], floor, grad=(0.8, 1.05))
        self.corner_ao()
        fl = np.asarray(floor, np.float32)
        if floor_kind == "boards":
            n = 9
            for i in range(1, n):
                x = i / n
                self.quad([(x - 0.002, 0, 0), (x + 0.002, 0, 0), (x + 0.002, 0, 1), (x - 0.002, 0, 1)], fl * 0.6, alpha=0.6)
            for _ in range(14):
                i = self.rng.integers(0, n)
                z = self.rng.uniform(0.05, 0.95)
                self.quad([(i / n, 0, z), ((i + 1) / n, 0, z), ((i + 1) / n, 0, z + 0.006), (i / n, 0, z + 0.006)], fl * 0.6, alpha=0.5)
        elif floor_kind == "tiles":
            n = 8
            for i in range(1, n):
                x = i / n
                self.quad([(x - 0.0015, 0, 0), (x + 0.0015, 0, 0), (x + 0.0015, 0, 1), (x - 0.0015, 0, 1)], fl * 0.75, alpha=0.7)
            for j in range(1, 7):
                z = j / 7
                self.quad([(0, 0, z), (1, 0, z), (1, 0, z + 0.004), (0, 0, z + 0.004)], fl * 0.75, alpha=0.7)
        if skirting is not None:
            sk = np.asarray(skirting, np.float32)
            self.quad([(0, 0, 1), (1, 0, 1), (1, 0.035, 1), (0, 0.035, 1)], sk)
            self.quad([(0, 0, 0), (0, 0.035, 0), (0, 0.035, 1), (0, 0, 1)], sk * 0.8)
            self.quad([(1, 0, 0), (1, 0.035, 0), (1, 0.035, 1), (1, 0, 1)], sk * 0.85)

    def corner_ao(self, strength=0.45, width=0.035):
        """Darken where the room's planes meet (the corners and the back wall's edges), before any furniture."""
        S = self.S
        m = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(m)
        P = self.P
        lw = max(2, int(S * width * self.r))
        for a, b in (((0, 0, 0), (0, 0, 1)), ((1, 0, 0), (1, 0, 1)), ((0, 1, 0), (0, 1, 1)), ((1, 1, 0), (1, 1, 1))):
            d.line([P(*a), P(*b)], fill=255, width=lw * 2)
        d.line([P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), P(0, 0, 1)], fill=255, width=lw)
        mm = blur(np.asarray(m, np.float32) / 255, S * width * 0.6)
        self.cv.c *= (1 - strength * np.clip(mm, 0, 1))[..., None]

    def wallpaper_stripes(self, color, n=16, alpha=0.18, side=True):
        for i in range(n):
            x = (i + 0.25) / n
            self.quad([(x, 0.035, 1), (x + 0.5 / n, 0.035, 1), (x + 0.5 / n, 1, 1), (x, 1, 1)], color, alpha=alpha)
        if side:
            for i in range(10):
                z = (i + 0.25) / 10
                for xw in (0, 1):
                    self.quad([(xw, 0.035, z), (xw, 1, z), (xw, 1, z + 0.05), (xw, 0.035, z + 0.05)], color, alpha=alpha * 0.8)

    def _plane_mask(self, pts3):
        S = self.S
        m = Image.new("L", (S, S), 0)
        ImageDraw.Draw(m).polygon([self.P(*p) for p in pts3], fill=255)
        return np.asarray(m, np.float32) / 255

    def sun_patch(self, win=(0.18, 0.82, 0.25, 0.92), d=(0.25, 0.55, 1.0), strength=0.55, panes=2, warm=(1.0, 0.72, 0.42)):
        """The low sun through the window we look through: a window-shaped warm patch on the floor and back wall."""
        S = self.S
        x0w, x1w, y0w, y1w = win
        dx, dy, dz = d
        mask = np.zeros((S, S), np.float32)
        gap = 0.025
        cols = [(x0w + (x1w - x0w) * i / panes + (gap if i else 0), x0w + (x1w - x0w) * (i + 1) / panes - (gap if i < panes - 1 else 0)) for i in range(panes)]
        rows = [(y0w, (y0w + y1w) / 2 - gap), ((y0w + y1w) / 2 + gap, y1w)]

        def onwall(x, y):  # reaches z = 1
            t = 1.0 / dz
            return (x + dx * t, y - dy * t, 1.0)

        def onfloor(x, y):
            t = y / dy
            return (x + dx * t, 0.0, dz * t)

        wall_m = self._plane_mask([(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)])
        floor_m = self._plane_mask([(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)])
        for (a, b) in cols:
            for (c, e) in rows:
                corners = [(a, c), (b, c), (b, e), (a, e)]
                for f, pm in ((onwall, wall_m), (onfloor, floor_m)):
                    pts = [self.P(*f(x, y)) for x, y in corners]
                    m = Image.new("L", (S, S), 0)
                    ImageDraw.Draw(m).polygon(pts, fill=255)
                    mask = np.maximum(mask, np.asarray(m, np.float32) / 255 * pm)
        mask = blur(mask, S * 0.006)
        c = self.cv.c
        w = np.asarray(warm, np.float32)
        c[:] = c + (c * w * 0.9 + w * 0.06) * (mask[..., None] * strength)

    # furniture -----------------------------------------------------------------------------------------------------
    def box(self, x0, x1, y0, y1, z0, z1, col, top=1.12, side=0.72, bottom=0.55, front_grad=(1.0, 0.85), alpha=1.0, fn=None, shadow=True):
        col = np.asarray(col, np.float32)
        if shadow and y0 <= 0.001 and alpha >= 1.0:   # a soft contact shadow on the floor
            for k, g in enumerate((0.03, 0.018, 0.008)):
                self.floorrect(x0 - g, x1 + g, z0 - g * 0.6, z1 + g, (0.05, 0.04, 0.035), alpha=0.13 + 0.05 * k)
        ex, ey = self.eye
        if x0 > ex:
            self.quad([(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], col * side, alpha=alpha)
        if x1 < ex:
            self.quad([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], col * side, alpha=alpha)
        if y1 < ey:
            self.quad([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], col * top, alpha=alpha)
        if y0 > ey:
            self.quad([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], col * bottom, alpha=alpha)
        return self.quad([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], col, grad=front_grad, alpha=alpha, fn=fn)

    def wallrect(self, x0, x1, y0, y1, col, z=0.999, **kw):
        return self.quad([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], col, **kw)

    def siderect(self, xw, z0, z1, y0, y1, col, **kw):
        return self.quad([(xw, y0, z0), (xw, y1, z0), (xw, y1, z1), (xw, y0, z1)], col, **kw)

    def floorrect(self, x0, x1, z0, z1, col, y=0.002, **kw):
        return self.quad([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], col, **kw)

    def picture(self, x0, x1, y0, y1, frame=(0.25, 0.18, 0.12), kind="landscape", z=0.998):
        rng = self.rng
        self.wallrect(x0 + 0.006, x1 + 0.008, y0 - 0.01, y1 - 0.004, (0.1, 0.08, 0.07), z=z + 0.0009, alpha=0.3)  # shadow
        self.wallrect(x0, x1, y0, y1, frame, z=z)
        fw = (x1 - x0) * 0.09
        ix0, ix1, iy0, iy1 = x0 + fw, x1 - fw, y0 + fw, y1 - fw
        if kind == "landscape":
            self.wallrect(ix0, ix1, iy0, iy1, (0.62, 0.72, 0.8), z=z - 0.0005, grad=(0.9, 1.15))
            hy = iy0 + (iy1 - iy0) * rng.uniform(0.35, 0.5)
            self.wallrect(ix0, ix1, iy0, hy, (0.42, 0.55, 0.3), z=z - 0.0006)
            self.wallrect(ix0, ix1, iy0, iy0 + (hy - iy0) * 0.5, (0.32, 0.45, 0.25), z=z - 0.0007)
            p = self.P(rng.uniform(ix0 + 0.02, ix1 - 0.02), iy0 + (iy1 - iy0) * 0.75, z)
            rr = (ix1 - ix0) * self.S * 0.06 * self._s(z)
            self.cv.ellipse(p[0], p[1], rr, rr, (1.0, 0.85, 0.55))
        elif kind == "abstract":
            self.wallrect(ix0, ix1, iy0, iy1, (0.93, 0.9, 0.84), z=z - 0.0005)
            cols = [C("#d9694f"), C("#3d6e8f"), C("#e8b84a"), C("#7aa36e")]
            for k in range(3):
                a = rng.uniform(ix0, ix1 - 0.03)
                b = rng.uniform(iy0, iy1 - 0.03)
                self.wallrect(a, min(a + rng.uniform(0.03, 0.08), ix1), b, min(b + rng.uniform(0.03, 0.1), iy1), cols[k % 4], z=z - 0.0006 - k * 0.0001)
        elif kind == "portrait":
            self.wallrect(ix0, ix1, iy0, iy1, (0.5, 0.42, 0.34), z=z - 0.0005, grad=(1.1, 0.8))
            cx = (ix0 + ix1) / 2
            p = self.P(cx, iy0 + (iy1 - iy0) * 0.6, z)
            rr = (ix1 - ix0) * self.S * 0.18 * self._s(z)
            p2 = self.P(cx, iy0, z)
            self.cv.ellipse(p2[0], p2[1], rr * 2.2, rr * 1.5, (0.25, 0.22, 0.3))
            self.cv.ellipse(p[0], p[1], rr, rr * 1.2, (0.82, 0.66, 0.55))
        elif kind == "mirror":
            self.wallrect(ix0, ix1, iy0, iy1, (0.75, 0.78, 0.8), z=z - 0.0005, grad=(1.15, 0.85))

    def books(self, x0, x1, y0, y1, z, palette=None, fill=0.95):
        rng = self.rng
        palette = palette or [C("#8c3b32"), C("#2f4d6b"), C("#c9a25a"), C("#3f6b4a"), C("#d8cbb0"), C("#5b3a5a"), C("#a65d3a"), C("#1f2a33"), C("#b8b0a0")]
        x = x0
        while x < x1 - 0.004:
            w = rng.uniform(0.006, 0.016)
            if x + w > x1:
                break
            if rng.random() > fill:
                x += w
                continue
            h = (y1 - y0) * rng.uniform(0.62, 0.95)
            col = palette[rng.integers(len(palette))] * rng.uniform(0.75, 1.1)
            self.box(x, x + w, y0, y0 + h, z, z + 0.03, col, front_grad=(1.05, 0.9))
            if rng.random() < 0.25:
                self.wallrect(x, x + w, y0 + h * 0.7, y0 + h * 0.76, (0.85, 0.75, 0.45), z=z - 0.0005)
            x += w + rng.uniform(0, 0.002)

    def shelf_unit(self, x0, x1, y0, y1, z0, z1, wood, shelves=4, fill="books", items=None):
        wood = np.asarray(wood, np.float32)
        self.box(x0, x1, y0, y1, z0, z1, wood * 0.9)
        t = 0.012
        self.quad([(x0 + t, y0 + t, z0 + 0.0005), (x1 - t, y0 + t, z0 + 0.0005), (x1 - t, y1 - t, z0 + 0.0005), (x0 + t, y1 - t, z0 + 0.0005)], wood * 0.45)
        h = (y1 - y0 - 2 * t) / shelves
        for k in range(shelves):
            yb = y0 + t + k * h
            self.box(x0 + t, x1 - t, yb, yb + 0.012, z0 + 0.0004, z1, wood * 1.05)
            if fill == "books":
                self.books(x0 + t + 0.004, x1 - t - 0.004, yb + 0.012, yb + h - 0.01, z0 + 0.0003)
            elif fill == "goods":
                self.goods(x0 + t + 0.004, x1 - t - 0.004, yb + 0.012, yb + h - 0.012, z0 + 0.0003, items)

    def goods(self, x0, x1, y0, y1, z, palette=None, kind="boxes"):
        rng = self.rng
        palette = palette or [C("#e05a4f"), C("#f2c14e"), C("#4f86c6"), C("#5fad7a"), C("#f4efe6"), C("#e88fb0"), C("#8a6bbf"), C("#f29a4a")]
        x = x0
        while x < x1 - 0.01:
            w = rng.uniform(0.012, 0.03)
            if x + w > x1:
                break
            h = (y1 - y0) * rng.uniform(0.45, 0.9)
            col = palette[rng.integers(len(palette))] * rng.uniform(0.8, 1.08)
            sc = self._s(z) * self.S
            if kind == "boxes":
                self.box(x, x + w, y0, y0 + h, z, z + 0.03, col)
                self.wallrect(x + w * 0.15, x + w * 0.85, y0 + h * 0.35, y0 + h * 0.6, (0.97, 0.96, 0.92), z=z - 0.0004, alpha=0.8)
            elif kind == "jars":
                p0 = self.P(x + w / 2, y0 + h / 2, z)
                rx, ry = w * sc * 0.5, h * sc * 0.5
                self.cv.ellipse(p0[0], p0[1], rx, ry, col, grad=(1.2, 0.8))
                self.cv.ellipse(p0[0] - rx * 0.35, p0[1] - ry * 0.3, rx * 0.18, ry * 0.4, (1, 1, 1), alpha=0.5)
            elif kind == "bread":
                p0 = self.P(x + w / 2, y0 + h * 0.3, z)
                rx, ry = w * sc * 0.62, h * sc * 0.28
                self.cv.ellipse(p0[0], p0[1], rx, ry, C("#c98a45") * rng.uniform(0.85, 1.1), grad=(1.25, 0.75))
                self.cv.line([(p0[0] - rx * 0.4, p0[1] - ry * 0.2), (p0[0] + rx * 0.3, p0[1] - ry * 0.5)], C("#e8c088"), width=max(1, rx * 0.12))
            x += w + rng.uniform(0.001, 0.006)

    def lampshade(self, x, y, z, wt, wb, hh, shade, on=True, glow=1.0):
        pts = [self.P(x - wt, y + hh, z), self.P(x + wt, y + hh, z), self.P(x + wb, y, z), self.P(x - wb, y, z)]
        s = np.asarray(shade, np.float32) * (1.0 if on else 0.55)
        self.cv.poly(pts, s, grad=(0.9, 1.1))
        if on:
            c = self.P(x, y + hh * 0.4, z)
            sc = self._s(z) * self.S
            self.cv.glow(c[0], c[1], sc * 0.09, (1.0, 0.62, 0.3), 0.5 * glow)
            self.cv.glow(c[0], c[1], sc * 0.28, (1.0, 0.55, 0.25), 0.22 * glow)
            b = self.P(x, y, z)
            self.cv.glow(b[0], b[1] + sc * 0.04, sc * 0.12, (1.0, 0.7, 0.4), 0.25 * glow, sy=sc * 0.05)
            t = self.P(x, y + hh, z)
            self.cv.glow(t[0], t[1] - sc * 0.08, sc * 0.08, (1.0, 0.75, 0.45), 0.2 * glow, sy=sc * 0.12)

    def lamp_floor(self, x, z, h=0.55, shade=(1.0, 0.86, 0.62), on=True):
        self.box(x - 0.004, x + 0.004, 0.0, h, z, z + 0.01, (0.15, 0.13, 0.12))
        self.box(x - 0.03, x + 0.03, 0.0, 0.012, z - 0.02, z + 0.03, (0.15, 0.13, 0.12))
        self.lampshade(x, h, z, 0.05, 0.09, 0.09, shade, on)

    def pendant(self, x, z, drop=0.25, shade=(0.95, 0.8, 0.55), w=0.07, on=True, glow=1.0, kind="dome"):
        y = 1 - drop
        self.cv.line([self.P(x, 1, z), self.P(x, y + 0.05, z)], (0.12, 0.1, 0.1), width=max(1, self.S * 0.002))
        sc = self._s(z) * self.S
        if kind == "dome":
            a = self.P(x - w, y, z)
            b = self.P(x + w, y, z)
            t = self.P(x, y + 0.055, z)
            rx = (b[0] - a[0]) / 2
            ry = (a[1] - t[1])
            self.cv.poly([(a[0] + rx * (1 - math.cos(u)), a[1] - ry * math.sin(u)) for u in np.linspace(0, math.pi, 24)], shade, grad=(0.7, 1.0))
            self.cv.ellipse(a[0] + rx, a[1], rx * 0.7, ry * 0.18, (1.0, 0.95, 0.8))
        else:  # glass globe
            c = self.P(x, y + 0.02, z)
            rr = w * sc * 0.6
            self.cv.ellipse(c[0], c[1], rr, rr, (1.0, 0.92, 0.75))
        if on:
            c = self.P(x, y, z)
            self.cv.glow(c[0], c[1] + sc * 0.01, sc * 0.05, (1.0, 0.8, 0.5), 0.7 * glow)
            self.cv.glow(c[0], c[1] + sc * 0.12, sc * 0.2, (1.0, 0.6, 0.3), 0.25 * glow, sy=sc * 0.18)

    def sofa(self, x0, x1, z, col, depth=0.16, cushions=3):
        col = np.asarray(col, np.float32)
        self.box(x0, x1, 0.0, 0.22, z + depth * 0.65, z + depth, col * 0.9)                    # back
        self.box(x0, x1, 0.03, 0.11, z, z + depth, col * 0.95)                                    # base
        w = (x1 - x0 - 0.06) / cushions
        for k in range(cushions):
            a = x0 + 0.03 + k * w
            self.box(a + 0.004, a + w - 0.004, 0.15, 0.25, z + depth * 0.62, z + depth * 0.68, col * 1.0)
            self.box(a + 0.002, a + w - 0.002, 0.11, 0.15, z + 0.005, z + depth * 0.68, col * 1.05)
        self.box(x0 - 0.01, x0 + 0.03, 0.0, 0.16, z, z + depth, col * 0.92)
        self.box(x1 - 0.03, x1 + 0.01, 0.0, 0.16, z, z + depth, col * 0.92)
        rng = self.rng
        for k in range(2):
            px = x0 + 0.07 + k * (x1 - x0 - 0.14)
            p = self.P(px, 0.19, z + depth * 0.55)
            rr = self._s(z) * self.S * 0.035
            self.cv.ellipse(p[0], p[1], rr, rr * 0.85, C(["#e8c46a", "#d9826b", "#86a8c8", "#e9e2d0"][rng.integers(4)]), grad=(1.15, 0.85))

    def table(self, x0, x1, z0, z1, h, col, leg=0.012):
        col = np.asarray(col, np.float32)
        for lx in (x0 + 0.01, x1 - 0.01 - leg):
            self.box(lx, lx + leg, 0, h, z1 - 0.01 - leg, z1 - 0.01, col * 0.7)
        self.box(x0, x1, h - 0.02, h, z0, z1, col)
        for lx in (x0 + 0.01, x1 - 0.01 - leg):
            self.box(lx, lx + leg, 0, h - 0.02, z0 + 0.012, z0 + 0.012 + leg, col * 0.75)

    def chair(self, x, z, col, w=0.07):
        col = np.asarray(col, np.float32)
        self.box(x - w / 2, x + w / 2, 0, 0.22, z + 0.06, z + 0.07, col * 0.85)
        self.box(x - w / 2, x + w / 2, 0.11, 0.13, z, z + 0.07, col)
        for lx in (x - w / 2, x + w / 2 - 0.008):
            self.box(lx, lx + 0.008, 0, 0.11, z, z + 0.008, col * 0.75)

    def plant(self, x, z, h=0.3, pot=(0.75, 0.42, 0.3), leaf=(0.3, 0.45, 0.22), y0=0.0):
        rng = self.rng
        self.box(x - 0.035, x + 0.035, y0, y0 + 0.07, z, z + 0.06, pot)
        sc = self._s(z) * self.S
        px, py = self.P(x, y0 + 0.07, z + 0.03)
        for k in range(26):
            a = rng.uniform(-1.2, 1.2)
            L = rng.uniform(0.4, 1.0) * h
            ex, ey = px + math.sin(a) * L * sc * 0.6, py - math.cos(a) * L * sc
            c = np.asarray(leaf, np.float32) * rng.uniform(0.7, 1.25)
            self.cv.line([(px, py), (ex, ey)], c * 0.8, width=max(1, sc * 0.004))
            self.cv.ellipse(ex, ey, sc * 0.022, sc * 0.012, c)

    def curtains(self, left=0.12, right=0.12, col=(0.85, 0.78, 0.66), sheer=False, rod=True, sway=0.0):
        """Curtains just inside the window, gathered at the sides; folds as soft vertical bands."""
        col = np.asarray(col, np.float32)
        z = 0.02
        S = self.S

        def folds(ph0, period):
            def f(xx, yy):
                ph = (xx / (S * period)) * 2 * math.pi
                v = 0.7 + 0.3 * (0.5 + 0.5 * np.sin(ph + ph0)) ** 1.5 + 0.05 * np.sin(ph * 2.3 + ph0 * 2)
                return v * (0.92 + 0.12 * (yy / S))
            return f
        al = 0.55 if sheer else 1.0
        if left > 0:
            pts = [self.P(-0.01, 1.0, z), self.P(left, 1.0, z), self.P(left * (1 - sway) + 0.004, 0.0, z), self.P(-0.01, 0.0, z)]
            self.cv.poly(pts, col, fn=folds(0.3, 0.06), alpha=al)
            self.cv.line([self.P(left, 1.0, z), self.P(left * (1 - sway) + 0.004, 0.0, z)], col * 1.12, width=S * 0.004, alpha=al)
        if right > 0:
            pts = [self.P(1 - right, 1.0, z), self.P(1.01, 1.0, z), self.P(1.01, 0.0, z), self.P(1 - right * (1 - sway) - 0.004, 0.0, z)]
            self.cv.poly(pts, col, fn=folds(1.7, 0.06), alpha=al)
            self.cv.line([self.P(1 - right, 1.0, z), self.P(1 - right * (1 - sway) - 0.004, 0.0, z)], col * 1.12, width=S * 0.004, alpha=al)
        if rod:
            self.quad([(0, 0.965, z), (1, 0.965, z), (1, 0.985, z), (0, 0.985, z)], (0.2, 0.16, 0.13))

    def finish(self, exposure=1.0, warm=(1.0, 0.96, 0.9), vignette=0.35, grain=0.012, seed=0):
        S = self.S
        c = self.cv.c * exposure * np.asarray(warm, np.float32)
        yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) / S
        v = 1 - vignette * (((xx - 0.5) * 1.6) ** 2 + ((yy - 0.5) * 1.5) ** 2) ** 1.2
        c *= np.clip(v, 0.3, 1)[..., None]
        k = 0.78          # a soft shoulder instead of clipping the lamps
        over = np.maximum(c - k, 0)
        c = np.minimum(c, k) + over / (1 + over / (1 - k))
        rng = np.random.default_rng(seed)
        c += rng.normal(0, grain, (S, S, 1)).astype(np.float32)
        return np.clip(c, 0, 1)


# --- the eight homes ---------------------------------------------------------------------------------------------------

def room_living(S, seed):
    R = Room(S, seed, vp=(0.46, 0.5), r=0.52)
    R.shell(C("#c9a98a"), C("#7a5236"), C("#e2d3c0"))
    R.wallpaper_stripes(C("#e9d6bd"), n=18, alpha=0.22)
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(-0.35, 0.42, 1.0), strength=0.6)
    R.floorrect(0.18, 0.82, 0.35, 0.86, C("#9b4f3f"))
    R.floorrect(0.21, 0.79, 0.38, 0.83, C("#c27b5a"), alpha=0.5)
    R.picture(0.32, 0.62, 0.48, 0.72, kind="landscape")
    R.box(0.74, 0.86, 0.0, 0.12, 0.84, 0.95, C("#5a3e2c"))       # side table + table lamp
    R.box(0.785, 0.815, 0.12, 0.2, 0.88, 0.9, C("#dcd2c0"))
    R.lampshade(0.8, 0.2, 0.89, 0.035, 0.05, 0.07, (1.0, 0.88, 0.66))
    R.lamp_floor(0.14, 0.88, h=0.5)
    R.sofa(0.22, 0.72, 0.74, C("#5e7a8c"))
    R.table(0.34, 0.6, 0.45, 0.58, 0.1, C("#6b4a32"))
    R.box(0.4, 0.45, 0.1, 0.13, 0.5, 0.53, C("#c94f3d"))          # books on the coffee table
    R.box(0.47, 0.53, 0.1, 0.11, 0.49, 0.53, C("#e8e0d0"))
    R.plant(0.9, 0.3, h=0.38)
    R.box(-0.02, 0.1, 0.0, 0.3, 0.1, 0.24, C("#8a5a3a"))           # an armchair's edge in the foreground
    R.box(-0.02, 0.12, 0.0, 0.14, 0.05, 0.25, C("#a06a44"))
    R.curtains(0.1, 0.09, col=C("#d8b98a"))
    return R.finish(exposure=0.95, seed=seed)


def room_kitchen(S, seed):
    R = Room(S, seed, vp=(0.55, 0.48), r=0.55)
    R.shell(C("#e3dccb"), C("#b9ab98"), C("#f0ebe2"), side=C("#d9d2c0"), floor_kind="tiles")
    for i in range(24):                                               # tiled splashback
        R.wallrect(i / 24, i / 24 + 0.002, 0.3, 0.46, C("#bdb5a5"), alpha=0.6)
    for j in range(5):
        R.wallrect(0, 1, 0.3 + j * 0.032, 0.302 + j * 0.032, C("#bdb5a5"), alpha=0.6)
    # the back window with the bright afternoon outside
    R.wallrect(0.36, 0.64, 0.48, 0.8, C("#f4f1ea"))
    R.wallrect(0.38, 0.62, 0.5, 0.78, C("#cfe2ee"), grad=(0.95, 1.15), z=0.9985)
    p = R.P(0.45, 0.58, 0.998)
    R.cv.ellipse(p[0], p[1], S * 0.05, S * 0.04, C("#8fb07a"), alpha=0.9)
    R.wallrect(0.38, 0.62, 0.5, 0.54, C("#9fbf88"), z=0.9982, alpha=0.8)
    R.wallrect(0.497, 0.503, 0.5, 0.78, C("#f4f1ea"), z=0.998)
    R.wallrect(0.38, 0.62, 0.637, 0.643, C("#f4f1ea"), z=0.998)
    R.cv.glow(*R.P(0.5, 0.64, 1.0), S * 0.12, (1.0, 0.86, 0.6), 0.35)
    R.sun_patch(win=(0.2, 0.8, 0.35, 0.95), d=(0.3, 0.38, 1.0), strength=0.45)
    cab = C("#6f8f84")
    R.box(0.0, 1.0, 0.0, 0.27, 0.86, 1.0, cab)                         # base cabinets
    for i in range(7):
        a = i / 7
        R.wallrect(a + 0.008, a + 1 / 7 - 0.008, 0.03, 0.24, cab * 1.08, z=0.8595)
        q = R.P(a + 1 / 14, 0.21, 0.859)
        R.cv.ellipse(q[0], q[1], S * 0.004, S * 0.004, C("#d9c9a0"))
    R.box(-0.01, 1.01, 0.27, 0.3, 0.84, 1.0, C("#ece4d6"))             # worktop
    for (a, b) in ((0.0, 0.33), (0.67, 1.0)):                          # upper cabinets
        R.box(a, b, 0.55, 0.85, 0.92, 1.0, cab * 1.05)
        for k in range(2):
            u = a + (b - a) * k / 2
            R.wallrect(u + 0.01, u + (b - a) / 2 - 0.01, 0.57, 0.83, cab * 1.12, z=0.9195)
    R.box(0.08, 0.16, 0.3, 0.4, 0.9, 0.95, C("#c74b3b"))              # kettle
    R.box(0.7, 0.86, 0.3, 0.42, 0.9, 0.97, C("#f4f2ee"))              # bread bin
    R.goods(0.2, 0.32, 0.3, 0.4, 0.92, kind="jars", palette=[C("#e8b84a"), C("#b9d0d8"), C("#d97b5a")])
    R.plant(0.6, 0.93, h=0.12, y0=0.3)
    R.chair(0.3, 0.52, C("#a57a52"))
    R.chair(0.62, 0.52, C("#a57a52"))
    R.table(0.22, 0.72, 0.3, 0.5, 0.2, C("#c79a6a"))
    R.box(0.4, 0.5, 0.2, 0.24, 0.36, 0.42, C("#e8e3d8"))              # fruit bowl
    for k in range(4):
        q = R.P(0.42 + k * 0.02, 0.245, 0.39)
        R.cv.ellipse(q[0], q[1], S * 0.012, S * 0.011, [C("#e0802e"), C("#c83c2c"), C("#e9c84a"), C("#8db04a")][k], grad=(1.2, 0.8))
    R.pendant(0.47, 0.42, drop=0.28, shade=C("#e8dccb"), glow=0.8)
    R.curtains(0.06, 0.06, col=C("#f0e6d0"), sheer=True)
    return R.finish(exposure=1.0, seed=seed)


def room_bedroom(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.5)
    R.shell(C("#a9b7c6"), C("#8a6446"), C("#dcd8d4"), side=C("#9eacbb"))
    R.wallpaper_stripes(C("#c4cfda"), n=22, alpha=0.18)
    R.sun_patch(win=(0.2, 0.8, 0.3, 0.92), d=(-0.3, 0.45, 1.0), strength=0.55)
    R.box(0.22, 0.78, 0.0, 0.34, 0.97, 1.0, C("#5c4434"))              # headboard
    R.box(0.24, 0.76, 0.0, 0.1, 0.55, 0.97, C("#6b4c38"))              # bed frame
    R.box(0.25, 0.75, 0.1, 0.16, 0.56, 0.96, C("#f2eee6"))             # mattress
    for x in (0.3, 0.52):
        p = R.P(x + 0.09, 0.2, 0.93)
        R.cv.ellipse(p[0], p[1], S * 0.07 * R._s(0.93), S * 0.035 * R._s(0.93), C("#f7f3ea"), grad=(1.1, 0.85))
    R.box(0.24, 0.76, 0.1, 0.18, 0.54, 0.84, C("#c9776a"), front_grad=(1.05, 0.8))   # duvet
    R.box(0.26, 0.74, 0.17, 0.185, 0.55, 0.62, C("#e8d9c0"))
    R.picture(0.4, 0.6, 0.48, 0.62, kind="abstract")
    for x in (0.1, 0.84):
        R.box(x, x + 0.1, 0.0, 0.13, 0.88, 0.98, C("#7a5a40"))
    R.box(0.875, 0.905, 0.13, 0.2, 0.9, 0.92, C("#e6d8b8"))
    R.lampshade(0.89, 0.2, 0.91, 0.03, 0.045, 0.06, (1.0, 0.85, 0.6))
    R.box(0.0, 0.1, 0.0, 0.62, 0.4, 0.7, C("#8d6b4e"))                 # wardrobe on the left wall
    R.curtains(0.16, 0.16, col=C("#4f6d8f"))
    R.curtains(0.22, 0.22, col=C("#efe8dc"), sheer=True, rod=False)
    return R.finish(exposure=0.92, seed=seed)


def room_stairwell(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.48)
    R.shell(C("#d6c2a4"), C("#6e4a33"), C("#e9e0d2"), side=C("#cdb89a"))
    R.sun_patch(win=(0.2, 0.8, 0.3, 0.95), d=(0.4, 0.5, 1.0), strength=0.5)
    R.picture(0.3, 0.42, 0.66, 0.8, kind="portrait")
    R.picture(0.5, 0.6, 0.74, 0.86, kind="landscape")
    n = 11                                                              # stairs rising left to right along the back wall
    tread = C("#7a5038")
    for i in range(n):
        x0 = 0.12 + i * 0.065
        y1 = 0.06 + i * 0.06
        R.box(x0, 1.0, 0.0, y1, 0.82, 1.0, tread * (0.9 + 0.1 * (i % 2)))
        R.box(x0, 1.0, y1 - 0.012, y1, 0.8, 1.0, C("#f0e8da"))
    R.quad([(0.12, 0.0, 0.8), (0.12 + n * 0.065, n * 0.06, 0.8), (0.12 + n * 0.065, n * 0.06 + 0.04, 0.8), (0.12, 0.04, 0.8)], C("#f0e8da"))
    for i in range(n):
        x = 0.14 + i * 0.065
        R.box(x, x + 0.008, 0.06 + i * 0.06, 0.06 + i * 0.06 + 0.26, 0.79, 0.8, C("#f4efe6"))
    R.box(0.1, 0.13, 0.0, 0.36, 0.78, 0.81, C("#5a3a28"))              # newel post + handrail
    R.quad([(0.11, 0.33, 0.785), (0.12 + n * 0.065, 0.06 + n * 0.06 + 0.26, 0.785), (0.12 + n * 0.065, 0.06 + n * 0.06 + 0.29, 0.785), (0.11, 0.36, 0.785)], C("#5a3a28"))
    R.floorrect(0.3, 0.6, 0.0, 0.78, C("#7c3b35"))                      # runner
    R.floorrect(0.32, 0.58, 0.0, 0.78, C("#a8584b"), alpha=0.5)
    R.box(0.02, 0.14, 0.0, 0.1, 0.4, 0.55, C("#6a4a34"))                # shoe bench + coat stand
    R.box(0.03, 0.05, 0.0, 0.55, 0.35, 0.37, C("#3a2a22"))
    p = R.P(0.04, 0.5, 0.36)
    R.cv.ellipse(p[0] + S * 0.02, p[1] + S * 0.06, S * 0.03, S * 0.08, C("#3f5a6f"))
    R.pendant(0.5, 0.55, drop=0.22, kind="globe", glow=1.1)
    R.curtains(0.05, 0.05, col=C("#e9dfcc"), sheer=True)
    return R.finish(exposure=0.95, seed=seed)


def room_dining(S, seed):
    R = Room(S, seed, vp=(0.5, 0.46), r=0.52)
    R.shell(C("#8f9f7a"), C("#6b4a32"), C("#e6dccb"), side=C("#87976f"))
    R.quad([(0, 0.0, 0.9995), (1, 0.0, 0.9995), (1, 0.32, 0.9995), (0, 0.32, 0.9995)], C("#ece2cc"))   # wainscot
    R.quad([(0, 0.32, 0.999), (1, 0.32, 0.999), (1, 0.335, 0.999), (0, 0.335, 0.999)], C("#f6efe0"))
    for i in range(12):
        R.wallrect(i / 12 + 0.012, (i + 1) / 12 - 0.012, 0.05, 0.28, C("#dccfb4"), z=0.9985, alpha=0.6)
    R.sun_patch(win=(0.2, 0.8, 0.3, 0.92), d=(0.35, 0.45, 1.0), strength=0.55)
    R.picture(0.18, 0.5, 0.45, 0.72, kind="landscape", frame=C("#c9a25a"))
    R.picture(0.66, 0.9, 0.48, 0.66, kind="mirror", frame=C("#c9a25a"))
    R.box(0.62, 0.95, 0.0, 0.32, 0.9, 1.0, C("#5b3d2b"))                # sideboard
    for k in range(3):
        R.wallrect(0.64 + k * 0.105, 0.73 + k * 0.105, 0.04, 0.28, C("#6a4733"), z=0.8995)
    R.goods(0.65, 0.9, 0.32, 0.42, 0.93, kind="jars", palette=[C("#e8e3d8"), C("#b9d0d8"), C("#c9a25a")])
    for x in (0.3, 0.5, 0.7):
        R.chair(x, 0.7, C("#6b4a32"))
    R.table(0.18, 0.82, 0.38, 0.68, 0.2, C("#7a5136"))
    R.box(0.2, 0.8, 0.2, 0.203, 0.4, 0.66, C("#f2ede2"))               # tablecloth
    for x in (0.32, 0.5, 0.68):
        q = R.P(x, 0.205, 0.5)
        R.cv.ellipse(q[0], q[1], S * 0.025, S * 0.008, C("#f7f4ee"))
    R.box(0.47, 0.53, 0.2, 0.26, 0.5, 0.54, C("#e2a84a"))              # vase of flowers
    q = R.P(0.5, 0.29, 0.52)
    for k in range(9):
        R.cv.ellipse(q[0] + R.rng.normal(0, S * 0.015), q[1] + R.rng.normal(0, S * 0.01), S * 0.01, S * 0.01, [C("#e05a4f"), C("#f2c14e"), C("#f4efe6")][k % 3])
    R.pendant(0.5, 0.52, drop=0.3, shade=C("#c99a4a"), w=0.1, glow=1.2)
    R.chair(0.25, 0.25, C("#5a3d2b"))
    R.chair(0.75, 0.25, C("#5a3d2b"))
    R.curtains(0.12, 0.12, col=C("#b3574a"))
    return R.finish(exposure=0.95, seed=seed)


def room_study(S, seed):
    R = Room(S, seed, vp=(0.5, 0.48), r=0.5)
    R.shell(C("#7a5a48"), C("#5a3a28"), C("#d6cbbb"), side=C("#6e5242"))
    R.sun_patch(win=(0.2, 0.8, 0.3, 0.92), d=(-0.3, 0.5, 1.0), strength=0.5)
    R.shelf_unit(0.02, 0.4, 0.0, 0.94, 0.94, 1.0, C("#5a3a26"), shelves=6)
    R.shelf_unit(0.6, 0.98, 0.0, 0.94, 0.94, 1.0, C("#5a3a26"), shelves=6)
    R.picture(0.43, 0.57, 0.5, 0.68, kind="portrait")
    for side_x in (0.0, 1.0):                                           # books along the side walls
        o = 1 if side_x == 0 else -1
        for k in range(5):
            y = 0.04 + k * 0.18
            R.siderect(side_x + 0.001 * o, 0.45, 0.95, y, y + 0.012, C("#4a2e1e"))
            for j in range(16):
                z0 = 0.47 + j * 0.03
                h = R.rng.uniform(0.09, 0.15)
                col = [C("#8c3b32"), C("#2f4d6b"), C("#c9a25a"), C("#3f6b4a"), C("#d8cbb0"), C("#5b3a5a")][R.rng.integers(6)]
                R.siderect(side_x + 0.002 * o, z0, z0 + 0.024, y + 0.012, y + 0.012 + h, col * R.rng.uniform(0.7, 1.0))
    R.box(0.3, 0.7, 0.0, 0.2, 0.62, 0.78, C("#4e3020"))                # desk with a green banker's lamp
    R.box(0.28, 0.72, 0.2, 0.225, 0.6, 0.8, C("#5f3b27"))
    R.box(0.36, 0.48, 0.225, 0.235, 0.66, 0.74, C("#f2eee4"))
    R.box(0.6, 0.64, 0.225, 0.3, 0.7, 0.74, C("#b8a060"))
    R.lampshade(0.62, 0.3, 0.72, 0.03, 0.06, 0.04, C("#2f6b4a"), glow=0.75)
    R.cv.glow(*R.P(0.55, 0.23, 0.7), S * 0.1, (1.0, 0.75, 0.4), 0.3, sy=S * 0.03)
    R.chair(0.5, 0.45, C("#6b2f26"), w=0.12)
    R.box(0.44, 0.56, 0.12, 0.3, 0.5, 0.53, C("#7b3a2c"))
    R.plant(0.1, 0.25, h=0.35)
    R.curtains(0.1, 0.1, col=C("#6b3a30"))
    return R.finish(exposure=0.98, seed=seed)


def room_child(S, seed):
    R = Room(S, seed, vp=(0.48, 0.5), r=0.5)
    R.shell(C("#f0d27a"), C("#a8865f"), C("#f4efe4"), side=C("#ebc96e"))
    R.sun_patch(win=(0.2, 0.8, 0.3, 0.92), d=(0.35, 0.5, 1.0), strength=0.5)
    for (a, b, c, d, col) in ((0.08, 0.26, 0.5, 0.78, C("#3f6fb5")), (0.3, 0.44, 0.56, 0.76, C("#e05a4f")), (0.74, 0.92, 0.52, 0.74, C("#5fad7a"))):
        R.wallrect(a, b, c, d, col)                                     # posters with a star
        R.wallrect(a + 0.015, b - 0.015, c + 0.015, d - 0.015, col * 1.15, z=0.9985)
        p = R.P((a + b) / 2, (c + d) / 2, 0.998)
        rr = (b - a) * S * R._s(1) * 0.25
        star = [(p[0] + rr * (1 if k % 2 == 0 else 0.45) * math.cos(k * math.pi / 5 - math.pi / 2),
                 p[1] + rr * (1 if k % 2 == 0 else 0.45) * math.sin(k * math.pi / 5 - math.pi / 2)) for k in range(10)]
        R.cv.poly(star, C("#fff2b0"))
    pts = [R.P(0.02 + k * 0.08, 0.9 - 0.03 * math.sin(k * 0.9), 0.999) for k in range(13)]   # bunting
    R.cv.line(pts, C("#7a6a5a"), width=2)
    for k in range(12):
        a, b = pts[k], pts[k + 1]
        R.cv.poly([a, b, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2 + S * 0.04)], [C("#e05a4f"), C("#4f86c6"), C("#f2c14e"), C("#5fad7a")][k % 4])
    R.box(0.04, 0.4, 0.0, 0.25, 0.9, 1.0, C("#f4f0e8"))                 # toy shelf
    for k in range(5):
        R.box(0.06 + k * 0.068, 0.11 + k * 0.068, 0.02, 0.1, 0.885, 0.9, [C("#e05a4f"), C("#4f86c6"), C("#f2c14e"), C("#5fad7a"), C("#e88fb0")][k])
    R.goods(0.06, 0.38, 0.25, 0.36, 0.93, palette=[C("#e05a4f"), C("#4f86c6"), C("#f2c14e"), C("#5fad7a"), C("#e88fb0")])
    R.box(0.5, 0.98, 0.0, 0.22, 0.95, 1.0, C("#f4f2ee"))                # bed with a bright duvet
    R.box(0.52, 0.98, 0.0, 0.08, 0.6, 0.95, C("#e8e4dc"))
    p = R.P(0.75, 0.17, 0.9)
    R.cv.ellipse(p[0], p[1], S * 0.07 * R._s(0.9), S * 0.03 * R._s(0.9), C("#f7f3ea"))
    R.box(0.52, 0.98, 0.08, 0.15, 0.6, 0.86, C("#4f86c6"), fn=lambda xx, yy: 0.85 + 0.15 * (np.sin(xx * 0.15) > 0))
    for (x, z, col) in ((0.3, 0.5, C("#e05a4f")), (0.42, 0.58, C("#f2c14e")), (0.36, 0.62, C("#4f86c6"))):
        R.box(x, x + 0.05, 0.0, 0.05, z, z + 0.05, col)                 # blocks + a ball
    q = R.P(0.6, 0.05, 0.45)
    R.cv.ellipse(q[0], q[1], S * 0.04, S * 0.04, C("#e05a4f"), grad=(1.3, 0.7))
    R.cv.line([(q[0] - S * 0.04, q[1]), (q[0] + S * 0.04, q[1])], C("#f4f0e8"), width=S * 0.006)
    R.pendant(0.48, 0.55, drop=0.2, shade=C("#f2c14e"), glow=0.9)
    R.curtains(0.11, 0.11, col=C("#e9a0a0"))
    return R.finish(exposure=1.0, seed=seed)


def room_dim(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.5)
    R.shell(C("#7f8590"), C("#5a4a40"), C("#a8a8ac"), side=C("#777d88"))
    R.picture(0.4, 0.56, 0.5, 0.64, kind="mirror", frame=C("#3a3a3a"))
    R.box(0.2, 0.7, 0.0, 0.3, 0.86, 1.0, C("#4a4a52"))
    R.sofa(0.25, 0.7, 0.7, C("#55606a"))
    R.box(0.0, 0.12, 0.0, 0.6, 0.6, 0.8, C("#4a4038"))
    R.sun_patch(win=(0.42, 0.58, 0.3, 0.92), d=(0.2, 0.5, 1.0), strength=0.45, panes=1)
    R.curtains(0.33, 0.33, col=C("#6e6a78"), sway=0.06)                 # half drawn: a gap of light down the middle
    R.curtains(0.38, 0.0, col=C("#c9c4bc"), sheer=True, rod=False)
    return R.finish(exposure=0.85, warm=(0.95, 0.95, 1.0), seed=seed, vignette=0.45)


# --- shops ------------------------------------------------------------------------------------------------------------

def shop_shell(R, wall, floor, ceil, side=None, lights=2, warm=(1.0, 0.95, 0.85), floor_kind="tiles"):
    R.shell(wall, floor, ceil, side=side, floor_kind=floor_kind, skirting=None)
    S = R.S
    for k in range(lights):                     # ceiling light panels
        z = 0.25 + k * 0.5 / max(lights - 1, 1)
        R.quad([(0.3, 0.999, z), (0.7, 0.999, z), (0.7, 0.999, z + 0.07), (0.3, 0.999, z + 0.07)], (1.0, 0.98, 0.92))
        c = R.P(0.5, 0.99, z + 0.035)
        R.cv.glow(c[0], c[1], S * 0.18 * R._s(z), warm, 0.18, sy=S * 0.06 * R._s(z))


def shop_bakery(S, seed):
    R = Room(S, seed, vp=(0.5, 0.48), r=0.55)
    shop_shell(R, C("#f0e2c8"), C("#c9b9a0"), C("#f6efe2"), side=C("#ead8b8"))
    for k in range(3):                                                  # bread shelves on the back wall
        R.box(0.08, 0.92, 0.22 + k * 0.2, 0.235 + k * 0.2, 0.94, 1.0, C("#8a5a36"))
        R.goods(0.1, 0.9, 0.235 + k * 0.2, 0.235 + k * 0.2 + 0.12, 0.95, kind="bread")
    R.wallrect(0.3, 0.7, 0.84, 0.97, C("#3a2a22"))                      # chalk board
    for k in range(3):
        R.wallrect(0.33, 0.33 + R.rng.uniform(0.15, 0.32), 0.865 + k * 0.03, 0.873 + k * 0.03, C("#efe8dc"), z=0.998)
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(0.3, 0.45, 1.0), strength=0.4)
    R.box(0.08, 0.92, 0.0, 0.2, 0.5, 0.62, C("#efe6d6"))                # glass display counter with cakes
    for k in range(7):
        x = 0.13 + k * 0.11
        p = R.P(x + 0.03, 0.23, 0.53)
        R.cv.ellipse(p[0], p[1], S * 0.03, S * 0.016, [C("#e8b0b8"), C("#f2dca0"), C("#7a4a30"), C("#f4f0e6")][k % 4], grad=(1.2, 0.85))
    R.box(0.1, 0.9, 0.2, 0.3, 0.5, 0.6, C("#e8f0f2"), alpha=0.3)
    R.box(0.08, 0.92, 0.3, 0.31, 0.5, 0.62, C("#d8cfc0"))
    R.box(0.75, 0.86, 0.31, 0.38, 0.54, 0.58, C("#4a4a4a"))             # till
    R.pendant(0.3, 0.55, drop=0.25, shade=C("#2f4d4a"), glow=0.8)
    R.pendant(0.7, 0.55, drop=0.25, shade=C("#2f4d4a"), glow=0.8)
    R.box(0.02, 0.14, 0.0, 0.22, 0.12, 0.22, C("#b8885a"))              # bread baskets in the window
    R.goods(0.02, 0.14, 0.22, 0.3, 0.12, kind="bread")
    return R.finish(exposure=1.0, seed=seed, vignette=0.25)


def shop_diner(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.5)
    shop_shell(R, C("#e9dcc6"), C("#d8d0c4"), C("#f2ece2"), side=C("#a8463c"), lights=3)
    for i in range(10):                                                 # checker floor
        for j in range(8):
            if (i + j) % 2 == 0:
                R.floorrect(i / 10, (i + 1) / 10, j / 8, (j + 1) / 8, C("#2a2a2e"))
    R.quad([(0, 0.3, 0.9995), (1, 0.3, 0.9995), (1, 0.34, 0.9995), (0, 0.34, 0.9995)], C("#b8bcc0"))   # chrome strip
    R.wallrect(0.15, 0.85, 0.62, 0.86, C("#2a2622"))                    # menu board
    for k in range(6):
        R.wallrect(0.19 + (k % 2) * 0.34, 0.19 + (k % 2) * 0.34 + R.rng.uniform(0.15, 0.28), 0.67 + (k // 2) * 0.06, 0.68 + (k // 2) * 0.06, C("#f4e8c8"), z=0.998)
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.92), d=(0.35, 0.45, 1.0), strength=0.4)
    R.box(0.0, 1.0, 0.0, 0.22, 0.75, 0.84, C("#c4473b"))                # counter
    R.box(0.0, 1.0, 0.22, 0.24, 0.73, 0.86, C("#e8e4dc"))
    R.box(0.2, 0.3, 0.24, 0.34, 0.78, 0.82, C("#b8bcc0"))               # coffee machine
    R.box(0.62, 0.7, 0.24, 0.32, 0.77, 0.82, C("#f0ece2"), alpha=0.6)   # cake stand
    for k in range(6):                                                  # stools
        x = 0.1 + k * 0.16
        R.box(x - 0.005, x + 0.005, 0.0, 0.14, 0.68, 0.69, C("#b8bcc0"))
        p = R.P(x, 0.15, 0.68)
        R.cv.ellipse(p[0], p[1], S * 0.03, S * 0.012, C("#c4473b"), grad=(1.2, 0.8))
    R.box(0.0, 0.18, 0.0, 0.3, 0.5, 0.56, C("#a8463c"))                 # a booth on the left
    R.box(0.02, 0.2, 0.18, 0.2, 0.28, 0.48, C("#e8e4dc"))
    R.box(0.0, 0.16, 0.0, 0.13, 0.26, 0.5, C("#b84d42"))
    R.box(0.0, 0.18, 0.0, 0.3, 0.2, 0.26, C("#a8463c"))
    R.pendant(0.5, 0.5, drop=0.2, kind="globe", glow=0.9)
    return R.finish(exposure=1.0, seed=seed, vignette=0.25)


def shop_florist(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.55)
    shop_shell(R, C("#e6eadc"), C("#9a8f80"), C("#f2f0ea"), side=C("#d8decc"), floor_kind="boards")
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(-0.3, 0.45, 1.0), strength=0.5)
    rng = R.rng
    flower_cols = [C("#e05a6f"), C("#f2c14e"), C("#f4efe6"), C("#e88fb0"), C("#8a6bbf"), C("#f29a4a"), C("#d9405a")]

    def bucket(x, z, y0=0.0, n=14):
        R.box(x - 0.04, x + 0.04, y0, y0 + 0.09, z, z + 0.05, C("#7a8a96"), shadow=False)
        base = R.P(x, y0 + 0.09, z)
        sc = R._s(z) * S
        col = flower_cols[rng.integers(len(flower_cols))]
        for k in range(6):
            R.cv.ellipse(base[0] + rng.normal(0, sc * 0.03), base[1] - rng.uniform(0.0, 0.05) * sc, sc * 0.016, sc * 0.008, C("#4a7a3a"))
        for k in range(n):
            ex = base[0] + rng.normal(0, sc * 0.03)
            ey = base[1] - rng.uniform(0.04, 0.12) * sc
            R.cv.line([base, (ex, ey)], C("#3f6b3a"), width=max(1, sc * 0.003))
            R.cv.ellipse(ex, ey, sc * 0.013, sc * 0.012, col * rng.uniform(0.8, 1.1), grad=(1.2, 0.85))
    for t in range(2, -1, -1):                                          # tiered stand along the back wall, top tier furthest back
        z0 = 0.8 + t * 0.06
        R.box(0.05, 0.95, 0.0, 0.08 + t * 0.14, z0, 1.0, C("#8a6a4a"))
        for k in range(8):
            bucket(0.1 + k * 0.11, z0 + 0.005, y0=0.08 + t * 0.14, n=10)
    for x in (0.2, 0.5, 0.8):                                           # hanging plants
        R.plant(x, 0.6, h=0.14, y0=0.72)
    R.box(0.6, 0.95, 0.0, 0.22, 0.45, 0.58, C("#6a5040"))               # counter
    R.box(0.62, 0.72, 0.22, 0.24, 0.48, 0.55, C("#e9e0cf"))
    for x in (0.1, 0.25):
        bucket(x, 0.3, n=16)
    R.plant(0.92, 0.2, h=0.4)
    return R.finish(exposure=1.0, seed=seed, vignette=0.25)


def shop_pharmacy(S, seed):
    R = Room(S, seed, vp=(0.5, 0.48), r=0.52)
    shop_shell(R, C("#f2f4f2"), C("#d8dcd8"), C("#f6f8f6"), side=C("#e6ece8"), lights=3, warm=(0.95, 1.0, 1.0))
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(0.3, 0.45, 1.0), strength=0.35)
    pal = [C("#4f86c6"), C("#f4efe6"), C("#5fad7a"), C("#e05a4f"), C("#f2c14e"), C("#bcd4e6"), C("#e88fb0")]
    R.shelf_unit(0.02, 0.98, 0.0, 0.8, 0.93, 1.0, C("#f2f2ee"), shelves=5, fill="goods")
    R.wallrect(0.42, 0.58, 0.83, 0.97, C("#3a9a5a"))                    # green cross
    R.wallrect(0.48, 0.52, 0.85, 0.95, (1, 1, 1), z=0.998)
    R.wallrect(0.44, 0.56, 0.88, 0.92, (1, 1, 1), z=0.998)
    for side_x in (0.0, 1.0):
        o = 1 if side_x == 0 else -1
        for k in range(4):
            y = 0.05 + k * 0.17
            R.siderect(side_x + 0.002 * o, 0.35, 0.92, y, y + 0.012, C("#e8e8e4"))
            for j in range(20):
                z0 = 0.36 + j * 0.028
                h = R.rng.uniform(0.06, 0.12)
                R.siderect(side_x + 0.003 * o, z0, z0 + 0.022, y + 0.012, y + 0.012 + h, pal[R.rng.integers(len(pal))])
    R.box(0.3, 0.7, 0.0, 0.22, 0.6, 0.7, C("#f0f0ec"))                  # counter
    R.box(0.28, 0.72, 0.22, 0.24, 0.58, 0.72, C("#3a9a5a"))
    R.box(0.36, 0.44, 0.24, 0.3, 0.62, 0.66, C("#e05a4f"))
    return R.finish(exposure=1.0, warm=(0.98, 0.98, 0.96), seed=seed, vignette=0.25)


def shop_laundromat(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.45)
    shop_shell(R, C("#cfe2e8"), C("#d6d6d0"), C("#f2f4f4"), side=C("#bcd8e0"), lights=3, warm=(0.95, 1.0, 1.0))
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(0.3, 0.45, 1.0), strength=0.4)
    R.wallrect(0.25, 0.75, 0.6, 0.75, C("#f2c14e"))                      # price board
    for k in range(3):
        R.wallrect(0.28, 0.28 + R.rng.uniform(0.2, 0.44), 0.63 + k * 0.035, 0.645 + k * 0.035, C("#3a3a3a"), z=0.998)
    for k in range(5):                                                  # washers along the back
        x = 0.2 + k * 0.12
        R.box(x, x + 0.11, 0.0, 0.25, 0.92, 1.0, C("#f0f0ee"))
        p = R.P(x + 0.055, 0.13, 0.92)
        sc = R._s(0.92) * S
        R.cv.ellipse(p[0], p[1], sc * 0.04, sc * 0.04, C("#8a9aa6"))
        R.cv.ellipse(p[0], p[1], sc * 0.03, sc * 0.03, C("#3a5f7a"), grad=(1.3, 0.8))
    for i in range(5, -1, -1):                                          # and along both side walls, far to near
        z0 = 0.3 + i * 0.11
        for side in (0, 1):
            x0, x1 = (0.0, 0.14) if side == 0 else (0.86, 1.0)
            R.box(x0, x1, 0.0, 0.26, z0, z0 + 0.1, C("#f4f4f2"))
            xf = x1 if side == 0 else x0
            p = R.P(xf, 0.15, z0 + 0.05)
            sc = R._s(z0 + 0.05) * S
            R.cv.ellipse(p[0], p[1], sc * 0.018, sc * 0.05, C("#7a8a96"))
            R.cv.ellipse(p[0], p[1], sc * 0.013, sc * 0.038, C("#2f4a5a"))
    R.box(0.35, 0.65, 0.0, 0.2, 0.45, 0.55, C("#e0e4e6"))               # folding table with laundry
    R.box(0.4, 0.5, 0.2, 0.25, 0.47, 0.52, C("#f4c4c0"))
    R.box(0.52, 0.6, 0.2, 0.23, 0.47, 0.52, C("#bcd4e6"))
    return R.finish(exposure=1.0, warm=(0.97, 0.99, 1.0), seed=seed, vignette=0.25)


def shop_news(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.52)
    shop_shell(R, C("#e8d8c0"), C("#8a6a50"), C("#f2ebe0"), side=C("#dcc8a8"), floor_kind="boards")
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(-0.3, 0.45, 1.0), strength=0.5)
    pal = [C("#e05a4f"), C("#f2c14e"), C("#4f86c6"), C("#5fad7a"), C("#f4efe6"), C("#e88fb0"), C("#2a2a2a"), C("#f29a4a")]
    R.box(0.02, 0.98, 0.0, 0.85, 0.95, 1.0, C("#6a4a32"))               # magazine wall
    for row in range(6):
        y = 0.08 + row * 0.13
        R.box(0.04, 0.96, y - 0.015, y - 0.005, 0.9, 0.95, C("#7a5a40"))
        x = 0.05
        while x < 0.93:
            w = R.rng.uniform(0.05, 0.07)
            col = pal[R.rng.integers(len(pal))]
            R.wallrect(x, x + w, y, y + 0.1, col, z=0.948)
            R.wallrect(x + 0.006, x + w - 0.006, y + 0.075, y + 0.09, (1, 1, 1) if col.mean() < 0.6 else (0.15, 0.15, 0.15), z=0.9475, alpha=0.85)
            R.wallrect(x + 0.01, x + w - 0.01, y + 0.02, y + 0.06, col * 0.7, z=0.9475, alpha=0.6)
            x += w + 0.008
    R.box(0.6, 0.98, 0.0, 0.24, 0.55, 0.68, C("#7a5a40"))               # counter with sweet jars
    R.goods(0.62, 0.95, 0.24, 0.31, 0.58, kind="jars")
    R.box(0.05, 0.3, 0.0, 0.18, 0.45, 0.6, C("#5a4030"))                # newspaper stand
    for k in range(4):
        R.box(0.07 + k * 0.058, 0.12 + k * 0.058, 0.18, 0.2, 0.47, 0.57, C("#ece8de"))
        R.box(0.075 + k * 0.058, 0.115 + k * 0.058, 0.2, 0.202, 0.49, 0.52, C("#2a2a2a"))
    R.pendant(0.5, 0.5, drop=0.22, glow=0.9)
    return R.finish(exposure=0.98, seed=seed, vignette=0.28)


def shop_hardware(S, seed):
    R = Room(S, seed, vp=(0.5, 0.48), r=0.5)
    shop_shell(R, C("#d8d2c4"), C("#9a948a"), C("#ecebe6"), side=C("#cdc6b6"), lights=3, floor_kind="none")
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(0.3, 0.45, 1.0), strength=0.4)
    R.wallrect(0.05, 0.95, 0.45, 0.95, C("#c9a878"))                    # pegboard with tools
    rng = R.rng
    for i in range(40):
        for j in range(14):
            p = R.P(0.07 + i * 0.022, 0.47 + j * 0.034, 0.998)
            R.cv.ellipse(p[0], p[1], 1.2, 1.2, C("#6a5236"))
    for k in range(18):
        x = rng.uniform(0.08, 0.9)
        y = rng.uniform(0.5, 0.85)
        kind = rng.integers(3)
        col = [C("#c4473b"), C("#3a3a3a"), C("#e8b84a"), C("#4f86c6"), C("#9aa0a6")][rng.integers(5)]
        if kind == 0:   # hammer / wrench
            R.wallrect(x, x + 0.008, y, y + 0.08, col, z=0.997)
            R.wallrect(x - 0.012, x + 0.02, y + 0.075, y + 0.09, C("#8a9096"), z=0.9968)
        elif kind == 1:  # saw
            R.wallrect(x, x + 0.05, y, y + 0.02, C("#b8bcc0"), z=0.997)
            R.wallrect(x + 0.05, x + 0.065, y - 0.005, y + 0.025, col, z=0.9968)
        else:            # coil of rope / tape
            p = R.P(x, y, 0.997)
            R.cv.ellipse(p[0], p[1], S * 0.012, S * 0.012, col)
            R.cv.ellipse(p[0], p[1], S * 0.005, S * 0.005, C("#c9a878"))
    R.shelf_unit(0.05, 0.95, 0.0, 0.42, 0.93, 1.0, C("#7a7e84"), shelves=3, fill="goods")
    R.box(0.0, 0.2, 0.0, 0.45, 0.3, 0.75, C("#6a6e74"))                 # an aisle shelf of paint cans
    for k in range(3):
        R.box(0.2, 0.215, 0.03 + k * 0.14, 0.04 + k * 0.14, 0.3, 0.75, C("#8a8e94"))
    for k in range(9):
        p = R.P(0.205, 0.075 + (k % 3) * 0.14, 0.36 + (k // 3) * 0.14)
        sc = R._s(0.36 + (k // 3) * 0.14) * S
        R.cv.ellipse(p[0], p[1], sc * 0.01, sc * 0.03, [C("#e05a4f"), C("#f4efe6"), C("#4f86c6"), C("#5fad7a")][k % 4])
    R.box(0.6, 0.98, 0.0, 0.24, 0.5, 0.62, C("#5a4030"))                # counter + till
    R.box(0.82, 0.92, 0.24, 0.32, 0.53, 0.57, C("#3a3a3a"))
    return R.finish(exposure=0.98, seed=seed, vignette=0.28)


def shop_bikes(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.5)
    shop_shell(R, C("#c8c2b8"), C("#6a6460"), C("#dedad4"), side=C("#b8b0a4"), lights=2, floor_kind="none")
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(-0.3, 0.45, 1.0), strength=0.5)
    rng = R.rng

    def bike(cx, cy, z, scale, col):
        sc = R._s(z) * S * scale
        p = R.P(cx, cy, z)
        r = sc * 0.055
        for dx in (-0.085, 0.085):
            q = (p[0] + dx * sc, p[1])
            R.cv.ellipse(q[0], q[1], r, r, C("#1e1e1e"))
            R.cv.ellipse(q[0], q[1], r * 0.86, r * 0.86, C("#c8c2b8") * 0.8, alpha=0.85)
            R.cv.ellipse(q[0], q[1], r * 0.12, r * 0.12, C("#9aa0a6"))
        a = (p[0] - 0.085 * sc, p[1])
        b = (p[0] + 0.085 * sc, p[1])
        sp = (p[0] - 0.02 * sc, p[1] - 0.06 * sc)
        hb = (p[0] + 0.06 * sc, p[1] - 0.07 * sc)
        bb = (p[0], p[1])
        w = max(2, sc * 0.007)
        R.cv.line([a, sp, hb, b], col, width=w)
        R.cv.line([a, bb, sp], col, width=w)
        R.cv.line([bb, hb], col, width=w)
        R.cv.line([(sp[0] - sc * 0.015, sp[1] - sc * 0.01), (sp[0] + sc * 0.015, sp[1] - sc * 0.01)], C("#2a2a2a"), width=w * 1.5)
        R.cv.line([hb, (hb[0] + sc * 0.01, hb[1] - sc * 0.02)], C("#2a2a2a"), width=w)
    R.wallrect(0.05, 0.95, 0.58, 0.6, C("#5a5a5a"))
    for k, col in enumerate([C("#c4473b"), C("#4f86c6"), C("#e8b84a"), C("#3f6b4a")]):   # bikes hung on the back wall
        bike(0.17 + k * 0.22, 0.72, 0.99, 0.75, col)
    R.siderect(0.999, 0.4, 0.9, 0.3, 0.7, C("#3a3e44"))                 # tool wall on the right
    for j in range(12):
        z0 = 0.42 + j * 0.04
        R.siderect(0.997, z0, z0 + 0.01, rng.uniform(0.4, 0.5), rng.uniform(0.55, 0.66), [C("#c4473b"), C("#9aa0a6"), C("#e8b84a")][j % 3])
    R.box(0.0, 0.25, 0.0, 0.24, 0.6, 0.8, C("#5a4030"))                 # workbench
    R.box(0.02, 0.1, 0.24, 0.3, 0.65, 0.7, C("#c4473b"))
    R.box(0.47, 0.49, 0.0, 0.2, 0.5, 0.52, C("#3a3a3a"))                # a bike on the stand
    bike(0.48, 0.28, 0.5, 1.0, C("#2f6b8a"))
    p = R.P(0.85, 0.08, 0.3)                                            # a spare wheel leaning in front
    R.cv.ellipse(p[0], p[1], S * 0.07, S * 0.07, C("#1e1e1e"))
    R.cv.ellipse(p[0], p[1], S * 0.058, S * 0.058, C("#6a6460") * 0.9)
    R.pendant(0.35, 0.5, drop=0.22, shade=C("#3a3e44"), glow=1.0)
    R.pendant(0.7, 0.5, drop=0.22, shade=C("#3a3e44"), glow=1.0)
    return R.finish(exposure=0.98, seed=seed, vignette=0.3)


# --- the hospital -------------------------------------------------------------------------------------------------------

def ward_ward(S, seed):
    R = Room(S, seed, vp=(0.5, 0.5), r=0.45)
    shop_shell(R, C("#dfe8e4"), C("#c9d0cc"), C("#f2f4f2"), side=C("#d4e0dc"), lights=3, warm=(0.95, 1.0, 1.0))
    R.sun_patch(win=(0.15, 0.85, 0.3, 0.95), d=(0.35, 0.45, 1.0), strength=0.45)
    R.quad([(0, 0.0, 0.9995), (1, 0.0, 0.9995), (1, 0.06, 0.9995), (0, 0.06, 0.9995)], C("#9ab8b0"))
    R.box(0.44, 0.56, 0.0, 0.5, 0.98, 1.0, C("#e4ece8"))                # door at the back
    R.wallrect(0.47, 0.53, 0.32, 0.45, C("#bcd8e4"), z=0.979)
    R.wallrect(0.15, 0.32, 0.55, 0.75, C("#f4f0e6"))                    # notice board
    for k in range(4):
        R.wallrect(0.17 + (k % 2) * 0.08, 0.23 + (k % 2) * 0.08, 0.58 + (k // 2) * 0.08, 0.64 + (k // 2) * 0.08, [C("#f2c14e"), C("#e05a4f"), C("#4f86c6"), C("#5fad7a")][k], z=0.998)
    R.quad([(0.0, 0.94, 0.3), (1.0, 0.94, 0.3), (1.0, 0.95, 0.3), (0.0, 0.95, 0.3)], C("#b8bcc0"))
    for i in range(2, -1, -1):                                          # beds along both walls, far to near
        z0 = 0.35 + i * 0.22
        for side in (0, 1):
            x0, x1 = (0.0, 0.3) if side == 0 else (0.7, 1.0)
            xw = 0.001 if side == 0 else 0.999
            o = 1 if side == 0 else -1
            R.siderect(xw, z0 + 0.02, z0 + 0.14, 0.45, 0.62, C("#5a6a70"))           # wall panel + monitor
            R.siderect(xw + 0.001 * o, z0 + 0.04, z0 + 0.1, 0.52, 0.6, C("#1a2a2a"))
            R.siderect(xw + 0.0015 * o, z0 + 0.045, z0 + 0.095, 0.555, 0.565, C("#4adf8a"))
            R.box(x0, x1, 0.0, 0.12, z0 + 0.01, z0 + 0.13, C("#b8c4c8"))
            R.box(x0, x1, 0.12, 0.16, z0, z0 + 0.14, C("#f4f6f6"))
            R.box(x0 + (0.0 if side == 0 else 0.1), x1 - (0.1 if side == 0 else 0.0), 0.16, 0.19, z0 + 0.005, z0 + 0.135, C("#9fc4d8"))
            if i > 0:                                                    # privacy curtain between beds
                zc = z0 - 0.04
                R.quad([(x0, 0.05, zc), (x1, 0.05, zc), (x1, 0.93, zc), (x0, 0.93, zc)], C("#a8cfc4"),
                       fn=lambda xx, yy: 0.82 + 0.18 * np.sin(xx * 0.06) ** 2)
    R.box(0.62, 0.66, 0.0, 0.42, 0.55, 0.57, C("#b8bcc0"))              # drip stand
    R.box(0.6, 0.68, 0.36, 0.42, 0.555, 0.565, C("#e8f4f8"), alpha=0.7)
    return R.finish(exposure=1.0, warm=(0.97, 0.99, 1.0), seed=seed, vignette=0.25)


def ward_corridor(S, seed):
    R = Room(S, seed, vp=(0.5, 0.48), r=0.18)
    R.shell(C("#e8ece8"), C("#b9c2c0"), C("#f2f4f2"), side=C("#dce4e0"), floor_kind="none", skirting=(0.55, 0.65, 0.62))
    R.wallrect(0.25, 0.75, 0.3, 0.85, C("#f6efe0"), grad=(1.1, 0.95))   # the bright window at the end
    R.cv.glow(*R.P(0.5, 0.55, 1.0), S * 0.08, (1.0, 0.85, 0.6), 0.5)
    for k in range(4, -1, -1):                                          # doors and handrails, far to near
        z0 = 0.08 + k * 0.19
        for xw in (0.0, 1.0):
            o = 1 if xw == 0 else -1
            R.siderect(xw, z0, z0 + 0.09, 0.0, 0.78, C("#7fa8a0"))
            R.siderect(xw + 0.001 * o, z0 + 0.012, z0 + 0.078, 0.02, 0.76, C("#9cc0b8"))
            R.siderect(xw + 0.002 * o, z0 + 0.03, z0 + 0.06, 0.45, 0.65, C("#cfe4ec"))
            R.siderect(xw + 0.001 * o, z0 + 0.09, z0 + 0.19, 0.36, 0.38, C("#b8bcc0"))
    for k in range(6, -1, -1):                                          # ceiling lights, mirrored in the polished floor
        z0 = 0.05 + k * 0.14
        R.quad([(0.38, 0.999, z0), (0.62, 0.999, z0), (0.62, 0.999, z0 + 0.05), (0.38, 0.999, z0 + 0.05)], (1.0, 1.0, 0.97))
        c = R.P(0.5, 0.99, z0 + 0.025)
        R.cv.glow(c[0], c[1], S * 0.12 * R._s(z0), (0.95, 1.0, 1.0), 0.22, sy=S * 0.05 * R._s(z0))
        f = R.P(0.5, 0.0, z0 + 0.025)
        R.cv.glow(f[0], f[1], S * 0.07 * R._s(z0), (0.95, 0.98, 1.0), 0.12, sy=S * 0.03 * R._s(z0))
    R.box(0.06, 0.16, 0.0, 0.3, 0.3, 0.36, C("#e0e6e8"))                # trolley
    R.box(0.84, 0.96, 0.0, 0.12, 0.55, 0.62, C("#7a8a96"))              # chairs
    return R.finish(exposure=1.0, warm=(0.97, 0.99, 1.0), seed=seed, vignette=0.3)


def atlas(painters, cols, rows, cell, ss=2, seed=10):
    out = np.zeros((rows * cell, cols * cell, 3), np.float32)
    for i, fn in enumerate(painters):
        c, r = i % cols, i // cols
        out[r * cell:(r + 1) * cell, c * cell:(c + 1) * cell] = down(fn(cell * ss, seed + i * 17), ss)
        print("   cell", i, fn.__name__)
    return out


def gen_interiors():
    img = atlas([room_living, room_kitchen, room_bedroom, room_stairwell, room_dining, room_study, room_child, room_dim], 4, 2, 512)
    save(TEX / "gen_interiors/gen_interiors_color.png", img)


def gen_interiors_shop():
    img = atlas([shop_bakery, shop_diner, shop_florist, shop_pharmacy, shop_laundromat, shop_news, shop_hardware, shop_bikes], 4, 2, 512, seed=50)
    save(TEX / "gen_interiors_shop/gen_interiors_shop_color.png", img)


def gen_interiors_ward():
    img = atlas([ward_ward, ward_corridor], 2, 1, 512, seed=90)
    save(TEX / "gen_interiors_ward/gen_interiors_ward_color.png", img)


# ----------------------------------------------------------------------------------------------------------------------
# Signs
# ----------------------------------------------------------------------------------------------------------------------

def mask_of(w, h, draw):
    """A float mask in [0,1] from a PIL drawing callback."""
    m = Image.new("L", (w, h), 0)
    draw(ImageDraw.Draw(m))
    return np.asarray(m, np.float32) / 255


def over(img, m, col):
    return img * (1 - m[..., None]) + np.asarray(col, np.float32) * m[..., None]


def gen_signs():
    W, RH, ss = 1024, 128, 2
    serif = str(FONTS / "DMSerifDisplay-Regular.ttf")
    bold = str(FONTS / "CrimsonText-Bold.ttf")
    cream = C("#f6ecd2")
    white = (1.0, 1.0, 1.0)
    # (board colour, lettering, border, font, style) per row of HillAtlas.SignTexts
    styles = [
        (C("#1f4f57"), cream, cream, serif, "board"),            # ABERNATHY CYCLES: deep teal
        (C("#5a2a22"), cream, C("#d9b46a"), serif, "board"),     # bakery: chocolate, gold rule
        (C("#8e2a2a"), cream, cream, serif, "board"),            # diner: deep red
        (C("#2d4a32"), cream, C("#e8b8c0"), serif, "board"),     # florist: deep green, pink rule
        (C("#1d5a4a"), white, white, serif, "board"),            # pharmacy: deep green
        (C("#23406e"), cream, cream, serif, "board"),            # laundromat: deep blue
        (C("#4a2a4a"), cream, C("#d9b46a"), serif, "board"),     # news nook: plum
        (C("#2a3a2a"), C("#f2d27a"), C("#f2d27a"), serif, "board"),   # hardware: forest + yellow
        (C("#14366b"), white, white, bold, "panel"),             # hospital: white on deep blue
        (C("#b3221e"), white, white, bold, "panel"),             # EMERGENCY: white on red
        (C("#3a3440"), cream, C("#c9b07a"), serif, "board"),     # church: slate
        (C("#1e3d5c"), cream, C("#f2c14e"), serif, "board"),     # school: navy + yellow
        (C("#1f5e3a"), white, white, bold, "street"),            # street names: green
        (C("#1f5e3a"), white, white, bold, "street"),
        (C("#1f5e3a"), white, white, bold, "street"),
        (C("#1d4f8a"), white, white, bold, "bus"),               # bus stop: a round-ended plate
    ]
    out = np.zeros((RH * 16, W, 3), np.float32)
    alpha = np.ones((RH * 16, W), np.float32)
    for i, (text, (board, ink, border, font, style)) in enumerate(zip(SIGN_TEXTS, styles)):
        w, h = W * ss, RH * ss
        grain = fbm(h, w, beta=1.6, seed=400 + i, aniso=0.15)
        streak = fbm(h, w, beta=1.2, seed=500 + i, aniso=0.08)
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        edge = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)) / (h * 0.5)
        if style == "board":   # painted wood: grain along the board, brush streaks, darker edges
            shade = (0.9 + 0.12 * grain + 0.05 * streak) * (0.86 + 0.14 * smoothstep(0, 0.25, edge)) * (1.04 - 0.08 * yy / h)
        else:                  # enamel / reflective sheeting: smoother
            shade = (0.97 + 0.04 * grain) * (0.93 + 0.07 * smoothstep(0, 0.15, edge)) * (1.03 - 0.06 * yy / h)
        img = board[None, None, :] * shade[..., None]
        a = np.ones((h, w), np.float32)
        if style == "board":
            m1, m2 = int(h * 0.09), int(h * 0.15)
            img = over(img, mask_of(w, h, lambda d: d.rectangle([m1, m1, w - m1, h - m1], outline=255, width=max(2, int(h * 0.022)))), border)
            img = over(img, 0.55 * mask_of(w, h, lambda d: d.rectangle([m2, m2, w - m2, h - m2], outline=255, width=max(1, int(h * 0.008)))), border)
        elif style == "street":
            m1 = int(h * 0.07)
            img = over(img, mask_of(w, h, lambda d: d.rounded_rectangle([m1, m1, w - m1, h - m1], radius=int(h * 0.12), outline=255, width=max(2, int(h * 0.03)))), border)
        elif style == "panel":
            m1 = int(h * 0.06)
            img = over(img, mask_of(w, h, lambda d: d.rectangle([m1, m1, w - m1, h - m1], outline=255, width=max(2, int(h * 0.018)))), border)
        left = 0
        if style == "bus":
            # a round-ended plate on a brushed backing; alpha is 0 outside the plate for a cutout
            m1, m3 = int(h * 0.04), int(h * 0.1)
            pm = mask_of(w, h, lambda d: d.rounded_rectangle([m1, m1, w - m1, h - m1], radius=(h - 2 * m1) // 2, fill=255))
            img = over(img, mask_of(w, h, lambda d: d.rounded_rectangle([m3, m3, w - m3, h - m3], radius=(h - 2 * m3) // 2, outline=255, width=max(2, int(h * 0.03)))), white)
            backing = np.asarray((0.78, 0.79, 0.81), np.float32)[None, None, :] * (0.92 + 0.1 * fbm(h, w, beta=1.0, seed=777, aniso=0.05))[..., None]
            img = backing * (1 - pm[..., None]) + img * pm[..., None]
            a = pm
            cx, cy, rr = int(h * 0.5) + m1, h // 2, int(h * 0.3)       # bus pictogram in a white roundel
            img = over(img, mask_of(w, h, lambda d: d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=255)), white)
            bw, bh = rr * 1.1, rr * 0.95

            def bus(d):
                d.rounded_rectangle([cx - bw / 2, cy - bh / 2 - rr * 0.08, cx + bw / 2, cy + bh / 2 - rr * 0.08], radius=int(rr * 0.15), fill=255)
                d.rectangle([cx - bw / 2 + rr * 0.12, cy - bh / 2 + rr * 0.06, cx + bw / 2 - rr * 0.12, cy - rr * 0.05], fill=0)
                for sx in (-1, 1):
                    d.ellipse([cx + sx * bw * 0.28 - rr * 0.12, cy + bh / 2 - rr * 0.2, cx + sx * bw * 0.28 + rr * 0.12, cy + bh / 2 + rr * 0.04], fill=255)
            img = over(img, mask_of(w, h, bus), board)
            left = int(h * 1.1)
        # lettering, fitted to the board with a little tracking
        maxw = (w - left) * (0.86 if style != "bus" else 0.84)
        maxh = h * (0.5 if style in ("board", "panel") else 0.56)
        size = int(h * 0.75)
        spacing = 0.06 if style != "board" else 0.04
        while True:
            f = ImageFont.truetype(font, size)
            widths = [f.getlength(ch) for ch in text]
            tw = sum(widths) + spacing * size * (len(text) - 1)
            bbox = f.getbbox("HAMBURG")
            th = bbox[3] - bbox[1]
            if (tw <= maxw and th <= maxh) or size < 10:
                break
            size -= 2
        tl = Image.new("L", (w, h), 0)
        td = ImageDraw.Draw(tl)
        x = left + ((w - left) - tw) / 2
        y = h / 2 - (bbox[1] + bbox[3]) / 2
        for ch, cw in zip(text, widths):
            td.text((x, y), ch, font=f, fill=255)
            x += cw + spacing * size
        tm = np.asarray(tl, np.float32) / 255
        if style == "board":   # painted lettering: a soft drop shadow and slightly uneven paint
            sh = blur(np.roll(np.roll(tm, int(h * 0.025), 0), int(h * 0.02), 1), h * 0.01)
            img = img * (1 - 0.4 * sh[..., None])
            tm = tm * (0.92 + 0.08 * grain)
        img = over(img, tm, ink)
        out[i * RH:(i + 1) * RH] = down(img, ss)
        alpha[i * RH:(i + 1) * RH] = down(a, ss)
        print("   sign", i, text, "font size", size // ss)
    save(TEX / "gen_signs/gen_signs_color.png", out, alpha)


# ----------------------------------------------------------------------------------------------------------------------
# Awnings
# ----------------------------------------------------------------------------------------------------------------------

def awning(name, col, stripes=4, size=1024, seed=0):
    h = w = size
    cream = C("#f3ead6")
    period = w / stripes
    ph = ((np.arange(w, dtype=np.float32) + 0.5) % period) / period
    soft = 1.2 / period
    band = smoothstep(0.5 - soft, 0.5 + soft, ph) * (1 - smoothstep(1 - soft * 2, 1.0, ph))
    band = np.broadcast_to(band[None, :], (h, w))
    base = mix(cream[None, None, :], np.asarray(col, np.float32)[None, None, :], band[..., None])
    weave_x = 0.5 + 0.5 * np.sin(np.arange(w)[None, :] * 2 * math.pi / 4.0)
    weave_y = 0.5 + 0.5 * np.sin(np.arange(h)[:, None] * 2 * math.pi / 4.0)
    weave = (weave_x * weave_y + (1 - weave_x) * (1 - weave_y)).astype(np.float32)
    n1 = fbm(h, w, beta=2.2, seed=seed + 1)
    n2 = fbm(h, w, beta=1.0, seed=seed + 2, aniso=0.25)
    img = base * ((0.96 + 0.04 * weave) * (0.93 + 0.07 * n1) * (0.97 + 0.04 * n2))[..., None]
    img = mix(img, np.full(3, float(img.mean()), np.float32), 0.06)   # sun-faded a touch
    save(TEX / f"{name}/{name}_color.png", img)
    save(TEX / f"{name}/{name}_normal.png", normal_from_height(blur(0.5 * weave + 0.25 * n2, 0.7, wrap=True), 1.5, wrap=True))


def gen_awnings():
    awning("gen_awning_red", C("#b83a32"), seed=11)
    awning("gen_awning_green", C("#2f6b4a"), seed=12)
    awning("gen_awning_blue", C("#2f5a8f"), seed=13)


# ----------------------------------------------------------------------------------------------------------------------
# Foliage: leaf clumps, ivy, grass and flower tufts
# ----------------------------------------------------------------------------------------------------------------------

def leaf_shape(cx, cy, L, W, ang, n=9, back=0.12):
    """A pointed oval leaf whose stem end sits near (cx, cy), pointing along ang: (left half, right half, midrib)."""
    ca, sa = math.cos(ang), math.sin(ang)
    prof = []
    for i in range(n + 1):
        t = i / n
        wd = W * (math.sin(math.pi * min(t * 1.08, 1.0)) ** 0.8) * (1.0 - 0.3 * t)
        prof.append((t * L - back * L, wd))

    def tr(u, v):
        return (cx + u * ca - v * sa, cy + u * sa + v * ca)
    l_half = [tr(u, 0) for (u, _) in prof[::-1]] + [tr(u, wd) for (u, wd) in prof]
    r_half = [tr(u, 0) for (u, _) in prof[::-1]] + [tr(u, -wd) for (u, wd) in prof]
    spine = [tr(prof[0][0], 0), tr(prof[-1][0], 0)]
    return l_half, r_half, spine


def ivy_shape(cx, cy, R_, ang, lobes=5):
    pts = []
    for k in range(48):
        phi = k / 48 * 2 * math.pi
        rr = R_ * (0.62 + 0.38 * abs(math.cos(phi * lobes / 2)) ** 0.6) * (1.0 if math.cos(phi) > -0.7 else 0.75)
        pts.append((cx + rr * math.cos(phi + ang), cy + rr * math.sin(phi + ang)))
    return pts


def clump(cv, cx, cy, R_, rng, palette, kind="leaves", n=360, blossom=0.0):
    """One rounded clump: an opaque irregular core, then leaves from the inside out (inner ones darker),
    lighter on top. Writes colour, alpha and height."""
    th = np.linspace(0, 2 * math.pi, 64, endpoint=False)
    wob = np.zeros_like(th)
    for k, a in ((2, 0.08), (3, 0.07), (5, 0.05), (7, 0.03)):
        wob += a * np.sin(k * th + rng.uniform(0, 6.28))
    squash = 0.92 if kind != "ivy" else 1.0

    def rb(t):
        return R_ * (1 + float(np.interp(t % (2 * math.pi), np.append(th, 2 * math.pi), np.append(wob, wob[0]))))
    dark, mid, light = palette[:3]
    core = [(cx + 0.8 * rb(t) * math.cos(t), cy + 0.8 * rb(t) * math.sin(t) * squash) for t in th]
    cv.poly(core, dark, hval=0.25, fn=lambda xx, yy: 0.75 + 0.35 * np.clip((cy - yy) / R_ * 0.5 + 0.5, 0, 1))
    items = []
    for i in range(n):
        t = rng.uniform(0, 2 * math.pi)
        rr = rb(t) * (0.05 + 1.0 * rng.random() ** 0.5)
        # front-ness: leaves near the middle face the viewer (drawn last), edge leaves stick out at the silhouette
        items.append((0.45 * (1 - min(rr / R_, 1)) + rng.random() * 0.6, cx + rr * math.cos(t), cy + rr * math.sin(t) * squash, t))
    items.sort(key=lambda o: o[0])
    for depth, x, y, t in items:
        up = float(np.clip((cy - y) / R_ * 0.6 + 0.5, 0, 1))            # 1 at the top of the clump
        lit = float(np.clip(0.18 + 0.6 * up + 0.35 * min(depth, 1.0) + rng.normal(0, 0.1), 0, 1.25))
        col = mix(dark, mid, min(lit, 1.0)) if lit < 1.0 else mix(mid, light, min((lit - 1.0) * 4, 1.0))
        col = col * rng.uniform(0.92, 1.08)
        hv = 0.35 + 0.6 * min(depth, 1.0) + rng.uniform(-0.05, 0.05)
        if blossom and rng.random() < blossom:              # a small five-petal flower
            pc = palette[3 + rng.integers(len(palette) - 3)] * (0.8 + 0.25 * up)
            pr = R_ * rng.uniform(0.045, 0.065)
            a0 = rng.uniform(0, 6.28)
            for k in range(5):
                a = a0 + k * 2 * math.pi / 5
                cv.ellipse(x + math.cos(a) * pr * 0.7, y + math.sin(a) * pr * 0.7, pr * 0.6, pr * 0.6, pc * rng.uniform(0.92, 1.05), hval=hv + 0.05)
            cv.ellipse(x, y, pr * 0.3, pr * 0.3, C("#f2c14e"), hval=hv + 0.08)
            continue
        ang = t + rng.normal(0, 0.7)
        if kind == "ivy":
            R2 = R_ * rng.uniform(0.07, 0.11)
            cv.poly(ivy_shape(x, y, R2, ang + math.pi / 2), col, hval=hv)
            for k in (-1, 0, 1):
                a = ang + math.pi + k * 0.8
                cv.line([(x, y), (x + math.cos(a) * R2 * 0.7, y + math.sin(a) * R2 * 0.7)], col * 1.3, width=max(1, R2 * 0.07), alpha=0.6)
        else:
            L = R_ * rng.uniform(0.14, 0.21)
            lh, rh, spine = leaf_shape(x, y, L, L * rng.uniform(0.26, 0.34), ang)
            s = math.sin(ang)                                # the half facing up catches more light: a folded leaf
            cv.poly(lh, col * (1.08 if s < 0 else 0.86), hval=hv + 0.04)
            cv.poly(rh, col * (0.86 if s < 0 else 1.08), hval=hv - 0.04)
            cv.line(spine, col * 1.2, width=max(1, L * 0.03), alpha=0.5)


def bleed(rgb, a, iters=10):
    """Spread opaque colours into transparent pixels (keeps cutout edges from going dark in the mips)."""
    known = a > 0.05
    acc = rgb * known[..., None]
    wgt = known.astype(np.float32)
    out = rgb.copy()
    filled = known.copy()
    for k in range(iters):
        s = 2.0 * (1.6 ** k)
        acc_b = blur(acc, s)
        w_b = blur(wgt, s)
        ok = (w_b > 1e-3) & ~filled
        out[ok] = acc_b[ok] / w_b[ok][..., None]
        filled |= ok
    return out


def foliage_atlas(name, palettes, kind="leaves", blossom=0.0, seed=0, n=560):
    size, ss = 1024, 2
    cell = size // 2 * ss
    cv = Canvas(size * ss, size * ss, palettes[0][0], alpha=True, height=True)
    for i in range(4):
        rng = np.random.default_rng(seed + i * 31)
        c, r = i % 2, i // 2
        cx, cy = c * cell + cell / 2, r * cell + cell / 2 + cell * 0.02
        clump(cv, cx, cy, cell * (0.37 if kind != "ivy" else 0.39), rng, palettes[i % len(palettes)], kind=kind, n=n, blossom=blossom)
    rgb, a, ht = down(cv.c, ss), down(cv.a, ss), down(cv.ht, ss)
    save(TEX / f"{name}/{name}_color.png", bleed(rgb, a), a)
    save(TEX / f"{name}/{name}_normal.png", normal_from_height(blur(ht, 1.0), 3.0))


def gen_leaves():
    green = [C("#2c4a1e"), C("#5f8a34"), C("#a9cc62")]
    green2 = [C("#2a461f"), C("#56823a"), C("#9cc46a")]
    yellowish = [C("#33501f"), C("#6f9238"), C("#c2d470")]
    foliage_atlas("gen_leaves", [green, green2, yellowish, green], seed=1000)


def gen_leaves_blossom():
    pink = [C("#2f4a22"), C("#5a7f3a"), C("#93b866"), C("#f2b8c8"), C("#f7d6e0"), C("#e88fb0"), C("#fbf0f2")]
    white = [C("#2f4a22"), C("#5a7f3a"), C("#93b866"), C("#f7f2ea"), C("#fbe8ee"), C("#f2c8d4")]
    foliage_atlas("gen_leaves_blossom", [pink, white, pink, white], blossom=0.45, seed=2000)


def gen_ivy():
    pal = [C("#16301a"), C("#2f5a2c"), C("#6f9a4a")]
    pal2 = [C("#1a321a"), C("#355f2e"), C("#7fa652")]
    foliage_atlas("gen_ivy", [pal, pal2, pal, pal2], kind="ivy", seed=3000, n=420)


def blade(cv, bx, by, height, width, lean, curl, c_base, c_tip, segs=9, hv=0.5):
    """A tapered, curving blade from (bx, by) upward; colour ramps from the base to the tip. Returns the tip."""
    pts = []
    ang = -math.pi / 2 + lean
    x, y = bx, by
    step = height / segs
    for s in range(segs + 1):
        pts.append((x, y))
        ang += curl / segs
        x += math.cos(ang) * step
        y += math.sin(ang) * step
    for s in range(segs):
        t0, t1 = s / segs, (s + 1) / segs
        w0 = width * (1 - t0) ** 0.85 + 0.6
        w1 = width * (1 - t1) ** 0.85 + 0.6
        (x0, y0), (x1, y1) = pts[s], pts[s + 1]
        dx, dy = x1 - x0, y1 - y0
        l = math.hypot(dx, dy) + 1e-6
        nx, ny = -dy / l, dx / l
        quad = [(x0 + nx * w0, y0 + ny * w0), (x1 + nx * w1, y1 + ny * w1), (x1 - nx * w1, y1 - ny * w1), (x0 - nx * w0, y0 - ny * w0)]
        tm = (t0 + t1) / 2
        col = mix(c_base, c_tip, tm ** 0.8)
        cv.poly(quad, col, hval=hv + 0.3 * tm)
        cv.line([(x0 + nx * w0 * 0.35, y0 + ny * w0 * 0.35), (x1 + nx * w1 * 0.35, y1 + ny * w1 * 0.35)], col * 1.12, width=max(1, w0 * 0.6), alpha=0.6)
    return pts[-1]


def tuft_grass(lush=1.0, dry=0.0, tall=1.0, n=150, seed_heads=False):
    def paint(cv, x0, cw, H, rng):
        base_cols = (C("#2a4a1c"), C("#3f6a26"))
        tip_cols = (C("#8fb04a"), C("#b6c86a"), C("#a8c05a"))
        dry_base, dry_tip = C("#7a6a3a"), C("#e0c888")
        for k in range(n):
            u = rng.normal(0.5, 0.17)
            bx = x0 + cw * float(np.clip(u, 0.06, 0.94))
            off = abs(u - 0.5) * 2
            h = H * tall * rng.uniform(0.45, 0.95) * (1 - 0.45 * min(off, 1))
            lean = (u - 0.5) * 1.1 + rng.normal(0, 0.18)
            curl = rng.normal(0, 0.5) + lean * 0.6
            w = cw * rng.uniform(0.012, 0.022)
            if rng.random() < dry:
                cb, ct = dry_base * rng.uniform(0.9, 1.1), dry_tip * rng.uniform(0.85, 1.05)
            else:
                cb = base_cols[rng.integers(2)] * rng.uniform(0.85, 1.1)
                ct = tip_cols[rng.integers(3)] * rng.uniform(0.85, 1.08) * lush
            tip = blade(cv, bx, H + 2, h, w, lean, curl, cb, ct, hv=rng.uniform(0.2, 0.5))
            if seed_heads and rng.random() < 0.18:
                for j in range(7):
                    cv.ellipse(tip[0] + rng.normal(0, cw * 0.01), tip[1] + j * cw * 0.012, cw * 0.008, cw * 0.016, C("#d8c890") * rng.uniform(0.85, 1.05), hval=0.9)
    return paint


def tuft_flowers(kind):
    def paint(cv, x0, cw, H, rng):
        stem = C("#3f6a2a")
        leaf_b, leaf_t = C("#2a4a1c"), C("#7aa04a")
        for k in range(55):                                  # foliage at the base
            u = rng.normal(0.5, 0.16)
            bx = x0 + cw * float(np.clip(u, 0.08, 0.92))
            blade(cv, bx, H + 2, H * rng.uniform(0.2, 0.42), cw * rng.uniform(0.02, 0.035), (u - 0.5) * 1.3 + rng.normal(0, 0.2), rng.normal(0, 0.6),
                  leaf_b, leaf_t * rng.uniform(0.85, 1.1), segs=6, hv=0.3)
        heads = {"daisy": 16, "lavender": 22, "rudbeckia": 13, "cosmos": 12}[kind]
        for k in range(heads):
            u = rng.normal(0.5, 0.15)
            bx = x0 + cw * float(np.clip(u, 0.1, 0.9))
            h = H * rng.uniform(0.45, 0.9)
            lean = (u - 0.5) * 0.8 + rng.normal(0, 0.12)
            tx, ty = blade(cv, bx, H + 2, h, cw * 0.008, lean, rng.normal(0, 0.25), stem * 0.8, stem * 1.2, segs=8, hv=0.5)
            r = cw * rng.uniform(0.055, 0.075)
            if kind == "daisy":
                for j in range(14):
                    a = j / 14 * 2 * math.pi
                    cv.ellipse(tx + math.cos(a) * r * 0.7, ty + math.sin(a) * r * 0.45, r * 0.42, r * 0.2, C("#f8f6ee") * rng.uniform(0.9, 1.0), hval=0.85)
                cv.ellipse(tx, ty, r * 0.3, r * 0.24, C("#f2b632"), hval=0.95)
            elif kind == "rudbeckia":
                for j in range(12):
                    a = j / 12 * 2 * math.pi
                    cv.ellipse(tx + math.cos(a) * r * 0.75, ty + math.sin(a) * r * 0.5 + r * 0.12, r * 0.45, r * 0.18, C("#f2b21e") * rng.uniform(0.92, 1.05), hval=0.85)
                cv.ellipse(tx, ty, r * 0.32, r * 0.28, C("#4a2a14"), hval=0.95)
            elif kind == "cosmos":
                pc = [C("#e88fb0"), C("#f4c4d4"), C("#d9407a"), C("#f8f2f4")][rng.integers(4)]
                for j in range(8):
                    a = j / 8 * 2 * math.pi + rng.uniform(0, 0.3)
                    cv.ellipse(tx + math.cos(a) * r * 0.6, ty + math.sin(a) * r * 0.42, r * 0.42, r * 0.26, pc * rng.uniform(0.9, 1.05), hval=0.85)
                cv.ellipse(tx, ty, r * 0.2, r * 0.17, C("#f2c14e"), hval=0.95)
            else:  # lavender
                for j in range(9):
                    cv.ellipse(tx + rng.normal(0, r * 0.08), ty + j * r * 0.3, r * 0.2, r * 0.26, C("#8a6bbf") * rng.uniform(0.8, 1.1), hval=0.9)
    return paint


def tuft_atlas(name, painters, seed):
    W, H, ss = 1024, 512, 2
    cv = Canvas(W * ss, H * ss, C("#3a5a22"), alpha=True, height=True)
    for i, p in enumerate(painters):
        p(cv, i * 256 * ss, 256 * ss, H * ss, np.random.default_rng(seed + i * 13))
    rgb, a, ht = down(cv.c, ss), down(cv.a, ss), down(cv.ht, ss)
    save(TEX / f"{name}/{name}_color.png", bleed(rgb, a), a)
    save(TEX / f"{name}/{name}_normal.png", normal_from_height(blur(ht, 0.8), 3.0))


def gen_grass_cards():
    tuft_atlas("gen_grass_cards", [tuft_grass(n=170), tuft_grass(dry=0.35, n=150), tuft_grass(n=110, dry=0.2, seed_heads=True),
                                   tuft_grass(tall=0.55, n=190, lush=1.05)], seed=4000)


def gen_flowers():
    tuft_atlas("gen_flowers", [tuft_flowers("daisy"), tuft_flowers("lavender"), tuft_flowers("rudbeckia"), tuft_flowers("cosmos")], seed=5000)


# ----------------------------------------------------------------------------------------------------------------------
# Decals
# ----------------------------------------------------------------------------------------------------------------------

def cell_falloff(n, inner=0.62, outer=0.95):
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32) / n
    d = np.sqrt((xx - 0.5) ** 2 + (yy - 0.5) ** 2) * 2
    return 1 - smoothstep(inner, outer, d)


def decal_atlas(name, painter, seed):
    size, ss = 1024, 2
    cell = size // 2 * ss
    rgb = np.zeros((size * ss, size * ss, 3), np.float32)
    a = np.zeros((size * ss, size * ss), np.float32)
    for i in range(4):
        c, r = i % 2, i // 2
        crgb, ca = painter(i, cell, np.random.default_rng(seed + i * 7))
        rgb[r * cell:(r + 1) * cell, c * cell:(c + 1) * cell] = crgb
        a[r * cell:(r + 1) * cell, c * cell:(c + 1) * cell] = ca
    rgb, a = down(rgb, ss), down(a, ss)
    save(TEX / f"{name}/{name}_color.png", rgb, a)


def paint_crack(i, n, rng):
    m = Image.new("L", (n, n), 0)
    halo = Image.new("L", (n, n), 0)
    d, dh = ImageDraw.Draw(m), ImageDraw.Draw(halo)

    def walk(x, y, ang, steps, width, depth):
        pts = [(x, y)]
        for s in range(steps):
            ang += rng.normal(0, 0.32)
            st = n * 0.008 * rng.uniform(0.6, 1.4)
            x += math.cos(ang) * st
            y += math.sin(ang) * st
            pts.append((x, y))
            wd = max(1.0, width * (1 - s / steps) ** 0.6)
            d.line(pts[-2:], fill=255, width=int(round(wd)))
            dh.line(pts[-2:], fill=255, width=int(round(wd * 4 + 4)))
            if depth < 3 and rng.random() < 0.05:
                walk(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.2), int(steps * rng.uniform(0.2, 0.45)), wd * 0.7, depth + 1)
    for k in range([1, 2, 1, 3][i]):
        ang = rng.uniform(0, 2 * math.pi)
        x, y = n / 2 + rng.normal(0, n * 0.05), n / 2 + rng.normal(0, n * 0.05)
        walk(x, y, ang, int(rng.uniform(40, 70)), n * 0.006, 0)
        walk(x, y, ang + math.pi + rng.normal(0, 0.3), int(rng.uniform(30, 60)), n * 0.005, 0)
    mm = blur(np.asarray(m, np.float32) / 255, 0.8)
    hm = blur(np.asarray(halo, np.float32) / 255, n * 0.006)
    a = np.clip(mm * 0.85 + hm * 0.14, 0, 1) * cell_falloff(n)
    rgb = mix(C("#4a4440")[None, None, :], C("#1a1714")[None, None, :], np.clip(mm * 1.4, 0, 1)[..., None])
    return rgb.astype(np.float32), a


def paint_stain(i, n, rng):
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32) / n
    d = np.sqrt((xx - 0.5) ** 2 + (yy - 0.5) ** 2) * 2
    nz = fbm(n, n, beta=2.0, seed=int(rng.integers(1_000_000)))
    nz2 = fbm(n, n, beta=1.3, seed=int(rng.integers(1_000_000)))
    if i == 0:      # oil stain: dark, a darker rim
        sh = d + (nz - 0.5) * 0.7
        m = 1 - smoothstep(0.45, 0.62, sh)
        rim = np.exp(-((sh - 0.52) / 0.05) ** 2) * 0.4
        a = np.clip(m * (0.22 + 0.1 * nz2) + rim * m * 0.4, 0, 0.4)
        rgb = np.broadcast_to(C("#16141a"), (n, n, 3))
    elif i == 1:    # dried puddle: a faint fill and a tide line
        sh = d * 1.1 + (nz - 0.5) * 0.9
        m = 1 - smoothstep(0.55, 0.62, sh)
        rim = np.exp(-((sh - 0.58) / 0.025) ** 2)
        a = np.clip(m * 0.1 + rim * 0.28, 0, 0.35) * (0.7 + 0.3 * nz2)
        rgb = np.broadcast_to(C("#3a3430"), (n, n, 3))
    elif i == 2:    # grime / dirt splash: blotchy
        sh = d + (nz - 0.5) * 1.1
        m = 1 - smoothstep(0.35, 0.8, sh)
        a = np.clip(m * smoothstep(0.35, 0.75, nz2) * 0.45, 0, 0.4)
        rgb = np.broadcast_to(C("#4a3a2c"), (n, n, 3))
    else:           # moss / algae: green-brown, for walls and kerbs
        sh = d + (nz - 0.5) * 1.0
        m = 1 - smoothstep(0.3, 0.85, sh)
        speck = smoothstep(0.55, 0.8, fbm(n, n, beta=0.8, seed=int(rng.integers(1_000_000))))
        a = np.clip(m * (0.25 * smoothstep(0.3, 0.7, nz2) + 0.25 * speck), 0, 0.45)
        rgb = mix(C("#3e4a24")[None, None, :], C("#5f6a32")[None, None, :], nz2[..., None])
    a = a * cell_falloff(n, 0.7, 0.98)
    return np.asarray(rgb, np.float32), a.astype(np.float32)


def maple_shape(cx, cy, R_, ang):
    pts = []
    for k in range(60):
        phi = k / 60 * 2 * math.pi
        rr = R_ * (0.42 + 0.58 * abs(math.cos(phi * 2.5)) ** 2.2)
        if math.sin(phi) > 0.8:
            rr *= 0.5
        pts.append((cx + rr * math.cos(phi + ang), cy + rr * math.sin(phi + ang)))
    return pts


def paint_leaf_decal(i, n, rng):
    cv = Canvas(n, n, C("#5a5a2a"), alpha=True)
    sh = Canvas(n, n, (0, 0, 0), alpha=True)
    cols = [C("#7a9a3a"), C("#a8b84a"), C("#d8b440"), C("#c97a32"), C("#8a5a2a"), C("#6a8a34"), C("#e0c060")]
    leaves = []
    for k in range([12, 20, 9, 26][i]):
        r = n * 0.32 * math.sqrt(rng.random())
        t = rng.uniform(0, 2 * math.pi)
        leaves.append((n / 2 + r * math.cos(t), n / 2 + r * math.sin(t), n * rng.uniform(0.11, 0.17), rng.uniform(0, 2 * math.pi),
                       cols[rng.integers(len(cols))] * rng.uniform(0.85, 1.1), rng.random() < 0.3))
    off = n * 0.008
    for (x, y, L, ang, col, maple) in leaves:                # soft contact shadows first
        if maple:
            sh.poly(maple_shape(x + off, y + off, L * 0.55, ang), (0, 0, 0))
        else:
            lh, rh, _ = leaf_shape(x + off, y + off, L, L * 0.32, ang)
            sh.poly(lh, (0, 0, 0))
            sh.poly(rh, (0, 0, 0))
    for (x, y, L, ang, col, maple) in leaves:
        if maple:
            cv.poly(maple_shape(x, y, L * 0.55, ang), col)
            sa_ = ang + math.pi / 2
            cv.line([(x, y), (x + math.cos(sa_) * L * 0.5, y + math.sin(sa_) * L * 0.5)], col * 0.6, width=max(1, L * 0.03))
            for k in range(5):
                a = ang + (k - 2) * 0.75 - math.pi / 2
                cv.line([(x, y), (x + math.cos(a) * L * 0.4, y + math.sin(a) * L * 0.4)], col * 0.75, width=max(1, L * 0.02), alpha=0.7)
        else:
            lh, rh, spine = leaf_shape(x, y, L, L * 0.32, ang)
            cv.poly(lh, col * 1.05)
            cv.poly(rh, col * 0.9)
            cv.line(spine, col * 0.7, width=max(1, L * 0.025), alpha=0.7)
    sa = blur(sh.a, n * 0.006) * 0.35
    a = np.clip(cv.a + sa * (1 - cv.a), 0, 1)
    rgb = cv.c * cv.a[..., None] + C("#1e1a14") * (1 - cv.a)[..., None]
    return rgb, a


def gen_cracks():
    decal_atlas("gen_cracks", paint_crack, seed=6000)


def gen_stains():
    decal_atlas("gen_stains", paint_stain, seed=6100)


def gen_leaf_decals():
    decal_atlas("gen_leaf_decals", paint_leaf_decal, seed=6200)


def gen_manhole():
    n, ss = 512, 2
    N = n * ss
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    cx = cy = N / 2
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / (N / 2)
    R_ = 0.86
    hgt = np.zeros((N, N), np.float32)
    frame = (r < 0.97) & (r > R_ + 0.025)
    gap = (r <= R_ + 0.025) & (r > R_)
    lid = r <= R_
    hgt[frame] = 0.55
    hgt[gap] = 0.1
    hgt[lid] = 0.45
    ring = (np.abs(r - 0.72) < 0.018) | (np.abs(r - 0.3) < 0.018)       # concentric rings and raised studs
    hgt[ring & lid] = 0.75
    g = 0.08
    gx = np.abs(((xx / N - 0.5) / g) % 1 - 0.5)
    gy = np.abs(((yy / N - 0.5) / g) % 1 - 0.5)
    hgt[(gx < 0.22) & (gy < 0.22) & (r < 0.69) & (r > 0.33)] = 0.72
    f = ImageFont.truetype(str(FONTS / "CrimsonText-Bold.ttf"), int(N * 0.09))
    tm = mask_of(N, N, lambda d: d.text((cx - d.textlength("SEWER", font=f) / 2, cy - N * 0.065), "SEWER", font=f, fill=255))
    hgt = np.maximum(hgt, tm * 0.75 * lid)
    for k in range(2):                                                  # lifting holes
        px, py = cx + math.cos(k * math.pi) * N * 0.5 * 0.55, cy
        hgt[(np.sqrt((xx - px) ** 2 + (yy - py) ** 2) < N * 0.022) & lid] = 0.15
    hs = blur(hgt, 1.2)
    wear = fbm(N, N, beta=1.8, seed=71)
    rust = fbm(N, N, beta=1.4, seed=72)
    rgb = mix(C("#2a2a2e")[None, None, :], C("#3a3836")[None, None, :], wear[..., None])
    rgb = mix(rgb, C("#6a6866"), smoothstep(0.6, 0.74, hs)[..., None] * 0.8)        # raised tops polished by tyres
    rgb = mix(rgb, C("#6a4630"), (smoothstep(0.62, 0.85, rust) * 0.35)[..., None])
    rgb = rgb * (0.55 + 0.45 * np.clip(hs / 0.6, 0, 1))[..., None]
    rgb = np.where(gap[..., None], C("#0e0d0c"), rgb)
    collar = (r >= 0.97) & (r < 1.0)
    rgb = np.where(collar[..., None], C("#2a2826"), rgb)
    a = np.clip((1.0 - r) / 0.03, 0, 1)
    a = np.where(collar, a * 0.6, a)
    rgb, a, hs = down(rgb, ss), down(a, ss), down(hs, ss)
    save(TEX / "gen_manhole/gen_manhole_color.png", bleed(rgb, a, 4), a)
    save(TEX / "gen_manhole/gen_manhole_normal.png", normal_from_height(hs, 6.0))


def gen_drain():
    n, ss = 512, 2
    N = n * ss
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N
    x0, x1, y0, y1 = 0.04, 0.96, 0.2, 0.8
    inside = (xx > x0) & (xx < x1) & (yy > y0) & (yy < y1)
    fx0, fx1, fy0, fy1 = x0 + 0.05, x1 - 0.05, y0 + 0.06, y1 - 0.06
    grate = (xx > fx0) & (xx < fx1) & (yy > fy0) & (yy < fy1)
    u = (xx - fx0) / (fx1 - fx0) * 11
    slot = grate & (np.abs(u % 1 - 0.5) < 0.3) & (yy > fy0 + 0.03) & (yy < fy1 - 0.03) & ~(np.abs(yy - 0.5) < 0.012)
    hgt = np.zeros((N, N), np.float32)
    hgt[inside] = 0.6
    hgt[grate] = 0.5
    hgt[slot] = 0.0
    hs = blur(hgt, 1.5)
    wear = fbm(N, N, beta=1.8, seed=81)
    rust = fbm(N, N, beta=1.3, seed=82)
    rgb = mix(C("#2c2b2c")[None, None, :], C("#3e3a36")[None, None, :], wear[..., None])
    rgb = mix(rgb, C("#6a4a30"), (smoothstep(0.6, 0.85, rust) * 0.4)[..., None])
    rgb = mix(rgb, C("#5e5c5a"), smoothstep(0.55, 0.6, hs)[..., None] * 0.5)
    rgb = np.where(slot[..., None], C("#070706") * (1 + 0.5 * wear[..., None]), rgb)
    dx = np.maximum(np.maximum(x0 - xx, xx - x1), 0)                    # a soft dirty surround in the gutter
    dy = np.maximum(np.maximum(y0 - yy, yy - y1), 0)
    dist = np.sqrt(dx ** 2 + dy ** 2)
    a = np.where(inside, 1.0, np.clip(1 - dist / 0.035, 0, 1) * 0.45 * (0.6 + 0.4 * wear))
    rgb = np.where(inside[..., None], rgb, C("#2a2622"))
    rgb, a, hs = down(rgb, ss), down(a, ss), down(hs, ss)
    save(TEX / "gen_drain/gen_drain_color.png", rgb, a)
    save(TEX / "gen_drain/gen_drain_normal.png", normal_from_height(hs, 6.0))


def gen_white():
    save(TEX / "gen_white/gen_white_color.png", np.ones((16, 16, 3), np.float32))


# ----------------------------------------------------------------------------------------------------------------------
# Tileable surfaces with no fitting CC0 download: hedge, water, glass, painted wood
# ----------------------------------------------------------------------------------------------------------------------

def hedge():
    T, ss = 1024, 2
    N = T * ss
    rng = np.random.default_rng(7000)
    base = fbm(N, N, beta=2.0, seed=7001)
    cv = Canvas(N, N, (0, 0, 0), height=True)
    cv.c[:] = mix(C("#1a2c12")[None, None, :], C("#2c4a1e")[None, None, :], base[..., None])
    cv.ht[:] = 0.1 + 0.2 * base
    dark, mid, light = C("#2e4e20"), C("#5c8a36"), C("#a6c862")
    lightmap = fbm(N, N, beta=2.4, seed=7002)
    n = 5200
    for k in range(n):
        x, y = rng.uniform(0, N), rng.uniform(0, N)
        L = N * rng.uniform(0.022, 0.034)
        ang = rng.uniform(0, 2 * math.pi)
        depth = k / n
        lit = float(np.clip(0.2 + 0.55 * depth + 0.45 * (lightmap[int(y) % N, int(x) % N] - 0.5) + rng.normal(0, 0.1), 0, 1.2))
        col = (mix(dark, mid, min(lit, 1.0)) if lit < 1 else mix(mid, light, min((lit - 1) * 5, 1))) * rng.uniform(0.92, 1.06)
        s = math.sin(ang)
        for ox in (-N, 0, N):                     # wrap around the edges so the texture tiles
            for oy in (-N, 0, N):
                px, py = x + ox, y + oy
                if -L < px < N + L and -L < py < N + L:
                    lh, rh, _ = leaf_shape(px, py, L, L * 0.38, ang)
                    cv.poly(lh, col * (1.07 if s < 0 else 0.88), hval=0.3 + 0.6 * depth + 0.03)
                    cv.poly(rh, col * (0.88 if s < 0 else 1.07), hval=0.3 + 0.6 * depth - 0.03)
    rgb, ht = down(cv.c, ss), down(cv.ht, ss)
    save(TEX / "hedge/hedge_color.jpg", rgb, quality=92)
    save(TEX / "hedge/hedge_normal.png", normal_from_height(blur(ht, 0.8, wrap=True), 3.0, wrap=True))
    ao = np.clip(0.45 + 0.75 * blur(ht, 3, wrap=True), 0, 1)
    save(TEX / "hedge/hedge_ao.jpg", np.repeat(ao[..., None], 3, -1), quality=90)


def water():
    T = 1024
    yy, xx = np.mgrid[0:T, 0:T].astype(np.float32) / T
    h = np.zeros((T, T), np.float32)
    rng = np.random.default_rng(7100)
    for k in range(14):        # integer wave vectors keep it tileable: a gentle flow plus cross ripples
        kx, ky = int(rng.integers(-6, 7)), int(rng.integers(1, 9))
        h += (1.0 / math.hypot(kx, ky)) * np.sin(2 * math.pi * (kx * xx + ky * yy) + rng.uniform(0, 6.28)).astype(np.float32)
    h = (h - h.min()) / (h.max() - h.min())
    h = 0.6 * h + 0.3 * fbm(T, T, beta=2.2, seed=7101, lo=3) + 0.1 * fbm(T, T, beta=1.6, seed=7102, lo=8)
    save(TEX / "water/water_normal.png", normal_from_height(blur(h, 1.0, wrap=True), 14.0, wrap=True))
    col = mix(C("#c4d6dc")[None, None, :], C("#dde8ea")[None, None, :], fbm(T // 4, T // 4, beta=2.0, seed=7103)[..., None])
    save(TEX / "water/water_color.jpg", col, quality=90)


def glass():
    T = 1024
    grime = fbm(T, T, beta=2.0, seed=7200)
    streak = fbm(T, T, beta=1.5, seed=7201, aniso=6.0)        # faint vertical rain streaks
    dust = fbm(T, T, beta=0.6, seed=7202)
    g = smoothstep(0.45, 0.9, grime) * 0.6 + smoothstep(0.5, 0.95, streak) * 0.4 + smoothstep(0.75, 0.95, dust) * 0.3
    v = 1.0 - 0.1 * g
    save(TEX / "glass/glass_color.png", np.stack([v, v * 0.995, v * 0.985], -1))
    save(TEX / "glass/glass_normal.png", normal_from_height(fbm(T, T, beta=3.0, seed=7203, lo=2), 3.0, wrap=True))


def painted_wood():
    T = 1024
    grain = fbm(T, T, beta=1.4, seed=7301, aniso=0.06)           # long streaks along x (the board's length)
    grain2 = fbm(T, T, beta=2.0, seed=7302, aniso=0.15)
    brush = fbm(T, T, beta=1.1, seed=7303, aniso=0.03)
    blot = fbm(T, T, beta=2.2, seed=7304)
    chips = smoothstep(0.86, 0.9, fbm(T, T, beta=1.6, seed=7305) * 0.6 + blot * 0.4)
    v = 0.9 + 0.035 * (grain - 0.5) + 0.03 * (brush - 0.5) - 0.04 * smoothstep(0.55, 0.95, blot)
    rgb = np.stack([v, v * 0.99, v * 0.965], -1)
    rgb = mix(rgb, mix(C("#8a7a68")[None, None, :], C("#6a5a4a")[None, None, :], grain2[..., None]), chips[..., None])
    edge = np.clip(blur(chips, 2.0, wrap=True) - chips, 0, 1)
    rgb = rgb * (1 - 0.35 * edge[..., None])
    save(TEX / "painted_wood/painted_wood_color.jpg", rgb, quality=92)
    h = 0.5 + 0.18 * (grain - 0.5) + 0.12 * (brush - 0.5) - 0.25 * chips
    save(TEX / "painted_wood/painted_wood_normal.png", normal_from_height(blur(h, 0.8, wrap=True), 6.0, wrap=True))


# ----------------------------------------------------------------------------------------------------------------------

PACKS = {
    "gen_interiors": gen_interiors, "gen_interiors_shop": gen_interiors_shop, "gen_interiors_ward": gen_interiors_ward,
    "gen_signs": gen_signs, "gen_awnings": gen_awnings, "gen_leaves": gen_leaves, "gen_leaves_blossom": gen_leaves_blossom,
    "gen_ivy": gen_ivy, "gen_grass_cards": gen_grass_cards, "gen_flowers": gen_flowers, "gen_cracks": gen_cracks,
    "gen_stains": gen_stains, "gen_leaf_decals": gen_leaf_decals, "gen_manhole": gen_manhole, "gen_drain": gen_drain,
    "gen_white": gen_white, "hedge": hedge, "water": water, "glass": glass, "painted_wood": painted_wood,
}


def main(argv):
    keys = []
    for name in argv or list(PACKS):
        key = "gen_awnings" if name.startswith("gen_awning") else name
        if key not in PACKS:
            sys.exit(f"unknown pack {name}; known: {', '.join(PACKS)}")
        if key not in keys:
            keys.append(key)
    for key in keys:
        print(key)
        PACKS[key]()


if __name__ == "__main__":
    main(sys.argv[1:])
