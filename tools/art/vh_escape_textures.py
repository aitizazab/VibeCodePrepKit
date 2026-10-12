"""The escape's own surfaces (House agent, round 2): wallpapers for the escape's rooms (each room its own palette, not the
oxblood everywhere), Theo's glow stars, a paper calendar. Tileable 512 px tiles; the builder imports them with Repeat wrap
(the creator's oxblood stripe comes in through DreamArt with Clamp wrap, which is what drew the bright seam down the middle
of the escape's floors and ceilings: one 0.5 m tile, then the edge pixels smeared to the walls).

    uv run --with pillow --with numpy python -I tools/art/vh_escape_textures.py Assets/JamKit/Templates/VisitingHours/Dream/House/Escape/Textures
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

N = 512


def paper_noise(seed, amount):
    rng = np.random.default_rng(seed)
    n = rng.normal(0.0, 1.0, (N // 8, N // 8))
    img = Image.fromarray(((n - n.min()) / (n.max() - n.min()) * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)
    fine = rng.normal(0.0, 1.0, (N, N))
    a = (np.asarray(img, dtype=np.float32) / 255.0 - 0.5) * 2.0
    return (a * 0.7 + fine * 0.3) * amount


def to_img(arr):
    return Image.fromarray(np.clip(arr * 255.0, 0, 255).astype(np.uint8))


def stripe_paper(base, stripe, accent, widths, seed, motif=None):
    """Vertical stripes (tile repeats in x), a hairline accent each side of the wide stripe, paper grain, an optional
    small motif in the plain band."""
    rgb = np.zeros((N, N, 3), np.float32)
    rgb[:] = np.array(base, np.float32) / 255.0
    x = np.arange(N)
    wide0, wide1, hair = widths
    band = (x >= wide0) & (x < wide1)
    rgb[:, band] = np.array(stripe, np.float32) / 255.0
    for edge in (wide0 - hair * 3, wide1 + hair * 2):
        sel = (x >= edge) & (x < edge + hair)
        rgb[:, sel] = np.array(accent, np.float32) / 255.0
    img = to_img(rgb)
    if motif is not None:
        d = ImageDraw.Draw(img)
        cx = (wide1 + N + wide0) // 2 % N
        for k in range(4):
            cy = k * (N // 4) + N // 8
            motif(d, cx, cy)
            motif(d, cx, cy + N // 8 - N // 8)
    arr = np.asarray(img, np.float32) / 255.0
    arr += paper_noise(seed, 0.025)[:, :, None]
    return to_img(arr)


def sprig(colour):
    def draw(d, cx, cy):
        d.ellipse([cx - 9, cy - 4, cx + 9, cy + 4], fill=colour)
        d.ellipse([cx - 4, cy - 12, cx + 4, cy + 12], fill=colour)
        d.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=(250, 244, 226))
    return draw


def trellis(base, line, dot, seed):
    img = Image.new("RGB", (N, N), base)
    d = ImageDraw.Draw(img)
    step = N // 4
    for k in range(-4, 9):
        o = k * step
        d.line([(o, 0), (o + N, N)], fill=line, width=5)
        d.line([(o + N, 0), (o, N)], fill=line, width=5)
    for i in range(5):
        for j in range(5):
            x, y = i * step, j * step
            d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=dot)
            d.ellipse([x + step // 2 - 5, y + step // 2 - 5, x + step // 2 + 5, y + step // 2 + 5], outline=dot, width=2)
    img = img.filter(ImageFilter.GaussianBlur(0.8))
    arr = np.asarray(img, np.float32) / 255.0 + paper_noise(seed, 0.02)[:, :, None]
    return to_img(arr)


def theo_sky(seed):
    rng = np.random.default_rng(seed)
    img = Image.new("RGB", (N, N), (178, 206, 228))
    d = ImageDraw.Draw(img)
    # soft clouds, tiled (drawn wrapped)
    for _ in range(7):
        cx, cy, r = rng.integers(0, N), rng.integers(0, N), rng.integers(26, 46)
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                for k in range(4):
                    dx = (k - 1.5) * r * 0.7
                    d.ellipse([cx + ox + dx - r, cy + oy - r * 0.6, cx + ox + dx + r, cy + oy + r * 0.6], fill=(232, 240, 246))
    img = img.filter(ImageFilter.GaussianBlur(2.5))
    d = ImageDraw.Draw(img)
    for _ in range(26):
        cx, cy, r = rng.integers(0, N), rng.integers(0, N), rng.integers(5, 10)
        pts = []
        for k in range(10):
            a = -math.pi / 2 + k * math.pi / 5
            rr = r if k % 2 == 0 else r * 0.45
            pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
        d.polygon(pts, fill=(250, 228, 150))
    arr = np.asarray(img, np.float32) / 255.0 + paper_noise(seed, 0.02)[:, :, None]
    return to_img(arr)


def glow_stars(seed):
    """Stick-on glow stars (RGBA, alpha-clipped): a sparse scatter, each a five-point star."""
    rng = np.random.default_rng(seed)
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(22):
        cx, cy = rng.integers(20, N - 20), rng.integers(20, N - 20)
        r = rng.integers(9, 22)
        rot = rng.uniform(0, math.pi)
        pts = []
        for k in range(10):
            a = rot + k * math.pi / 5
            rr = r if k % 2 == 0 else r * 0.42
            pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
        d.polygon(pts, fill=(255, 255, 255, 255))
    for _ in range(14):   # a crescent moon or two
        pass
    cx, cy = rng.integers(60, N - 60), rng.integers(60, N - 60)
    d.ellipse([cx - 30, cy - 30, cx + 30, cy + 30], fill=(255, 255, 255, 255))
    d.ellipse([cx - 14, cy - 34, cx + 40, cy + 22], fill=(0, 0, 0, 0))
    return img


def main(out):
    os.makedirs(out, exist_ok=True)
    # The dining room: sage stripe with cream bands and a little sprig.
    stripe_paper((150, 170, 138), (214, 214, 188), (120, 138, 108), (40, 150, 4), 1, sprig((120, 140, 110))).save(os.path.join(out, "escape_wallpaper_sage.png"))
    # The stairs: dusty blue stripe.
    stripe_paper((138, 158, 182), (196, 206, 214), (98, 116, 140), (60, 140, 3), 2).save(os.path.join(out, "escape_wallpaper_blue.png"))
    # The corridor: a warm butter trellis (the front door's light at its end).
    trellis((226, 204, 156), (196, 160, 104), (176, 124, 78), 3).save(os.path.join(out, "escape_wallpaper_trellis.png"))
    # Theo's room: sky and stars.
    theo_sky(4).save(os.path.join(out, "escape_wallpaper_theo.png"))
    glow_stars(5).save(os.path.join(out, "escape_glow_stars.png"))
    print("wrote", out)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
