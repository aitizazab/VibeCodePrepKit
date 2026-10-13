"""The Writ's three surface textures (Task 18), made from numbers (no image source, no image API):

    uv run --with numpy --with pillow python tools/skyline/guns/writ/model/make_textures.py

Writes into Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/writ/:
  writ_stone.png      tiling grey ashlar: running-bond blocks 2 x 1 cm (the tile is 4 x 4 cm of the gun), dark mortar,
                      per-block tone, pitting and worn bright edges (the colonnade, the arch, the grip column)
  writ_wood.png       tiling dark mahogany grain (the tile is 5 x 5 cm): the bench, the chair, the receiver
  writ_parchment.png  the scroll on the grip: aged paper with burnt ragged edges and six lines of brown cursive (512 x 256,
                      wrapped once around the column)
The model's UVs are box-projected in tile units (build_writ.py), so one texel is the same size on every face. Deterministic
(fixed seeds).
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "writ"


def fractal(size: int, seed: int, octaves: int = 5, base: int = 4) -> np.ndarray:
    """Tiling value noise in [0, 1] (each octave is a smooth upsample of a periodic random grid)."""
    rng = np.random.default_rng(seed)
    acc = np.zeros((size, size))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        n = base * 2 ** o
        grid = rng.random((n, n))
        # periodic bicubic-ish upsample through the FFT keeps the tile seamless
        spec = np.fft.fft2(grid)
        big = np.zeros((size, size), dtype=complex)
        h = n // 2
        big[:h, :h] = spec[:h, :h]
        big[:h, -h:] = spec[:h, -h:]
        big[-h:, :h] = spec[-h:, :h]
        big[-h:, -h:] = spec[-h:, -h:]
        layer = np.real(np.fft.ifft2(big))
        layer = (layer - layer.min()) / (layer.max() - layer.min() + 1e-9)
        acc += layer * amp
        total += amp
        amp *= 0.5
    return acc / total


def stone(size: int = 256) -> Image.Image:
    rng = np.random.default_rng(11)
    rows, cols = 4, 2
    y, x = np.mgrid[0:size, 0:size] / size
    row = np.floor(y * rows).astype(int)
    shift = (row % 2) * 0.5 / cols
    xs = (x + shift) % 1.0
    col = np.floor(xs * cols).astype(int)
    fy = y * rows - row
    fx = xs * cols - col
    block = row * cols + col
    tones = rng.uniform(0.78, 1.08, rows * cols)
    warm = rng.uniform(-0.03, 0.03, rows * cols)
    edge = np.minimum(np.minimum(fx, 1 - fx) * (size / cols), np.minimum(fy, 1 - fy) * (size / rows))   # px to the mortar
    mortar = edge < 1.6
    n1 = fractal(size, 3)
    n2 = fractal(size, 4, octaves=3, base=16)
    base = np.array([0.50, 0.485, 0.465])
    lum = tones[block] * (0.82 + 0.3 * n1) * (0.9 + 0.12 * n2)
    lum *= np.clip(0.72 + 0.28 * (edge / 5.0), 0.72, 1.0)                     # darker toward the joints
    lum += np.where((edge > 1.6) & (edge < 2.8), 0.07, 0.0)                    # a worn bright lip on each block
    pits = rng.random((size, size)) < 0.004
    lum = np.where(pits, lum * 0.55, lum)
    img = np.stack([lum * (base[0] + warm[block]), lum * base[1], lum * (base[2] - warm[block])], axis=-1)
    img[mortar] = np.array([0.13, 0.125, 0.12]) * (0.8 + 0.4 * n2[mortar])[:, None]
    return Image.fromarray((np.clip(img, 0, 1) ** (1 / 1.0) * 255).astype(np.uint8)).filter(ImageFilter.SMOOTH)


def wood(size: int = 256) -> Image.Image:
    y, x = np.mgrid[0:size, 0:size] / size
    warp = fractal(size, 21, octaves=4, base=2)
    fine = fractal(size, 22, octaves=3, base=32)
    rings = np.sin((y * 26 + warp * 1.1 + 0.05 * np.sin(x * np.pi * 2 * 3)) * np.pi * 2)
    grain = 0.5 + 0.5 * rings
    lum = 0.7 + 0.25 * grain + 0.15 * (fine - 0.5)
    pores = fractal(size, 23, octaves=2, base=64)
    lum *= np.where(pores > 0.78, 0.8, 1.0)
    base = np.array([0.34, 0.165, 0.095])
    img = base[None, None, :] * lum[..., None]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def cursive(draw: ImageDraw.ImageDraw, rng: np.random.Generator, x0: float, y0: float, width: float, ink) -> None:
    """One line of illegible cursive: words of looping strokes with gaps between them."""
    x = x0
    while x < x0 + width - 20:
        word = rng.uniform(22, 64)
        pts = []
        t = 0.0
        amp = rng.uniform(3.0, 5.5)
        freq = rng.uniform(0.7, 0.95)
        while t < word:
            loop = np.sin(t * freq) * amp + np.sin(t * freq * 2.7 + 1.3) * amp * 0.35
            ascender = 7.0 if rng.random() < 0.035 else 0.0
            pts.append((x + t + np.cos(t * freq) * 1.6, y0 + loop - ascender))
            t += 0.6
        draw.line(pts, fill=ink, width=2, joint="curve")
        x += word + rng.uniform(7, 13)


def parchment(w: int = 512, h: int = 256) -> Image.Image:
    rng = np.random.default_rng(31)
    n = fractal(512, 32)[:h, :w] if h <= 512 else fractal(h, 32)
    blot = fractal(512, 33, octaves=3, base=3)[:h, :w]
    base = np.array([0.87, 0.75, 0.50])
    lum = 0.86 + 0.18 * n - 0.16 * np.clip(blot - 0.62, 0, 1) * 3
    yy = np.arange(h)[:, None] / h
    rag = fractal(512, 34, octaves=4, base=8)[0, :w][None, :]
    burn = np.clip(1 - np.minimum(yy, 1 - yy) * 7.0 + (rag - 0.5) * 0.5, 0, 1)   # scorched toward the ragged edges
    img = base[None, None, :] * lum[..., None]
    img = img * (1 - 0.65 * burn[..., None]) + np.array([0.25, 0.13, 0.05]) * 0.65 * burn[..., None]
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    ink = (52, 33, 20)
    for k in range(6):
        cursive(d, rng, 24, 48 + k * 30, w - 48, ink)
    return im.filter(ImageFilter.SMOOTH)


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    for name, im in (("writ_stone.png", stone()), ("writ_wood.png", wood()), ("writ_parchment.png", parchment())):
        im.save(OUT / name)
        print("WRIT_TEXTURE", OUT / name, im.size)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
