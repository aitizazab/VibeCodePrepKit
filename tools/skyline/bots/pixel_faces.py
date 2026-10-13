"""Pixel-art bot faces for the face screens (Task 20). A face is a 32 x 16 grid of palette letters, drawn as an LED
matrix (each cell an 8 x 8 block: a 7 x 7 lit square and a dark gap), written as a 256 x 128 PNG (powers of two, so
Unity's default import keeps the cells square and sharp enough at bilinear filtering). Unlit cells are drawn as dim LEDs,
so the screen reads as a display even where the face is empty.

    uv run --with pillow python tools/skyline/bots/<id>/faces.py

Rows are written as strings; `half` rows are the left 16 columns, mirrored to make a symmetric face.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

W, H, CELL = 32, 16, 8
ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "bots"
FACES = ("Happy", "Alert", "Angry", "Hurt", "Wrecked")

SCREEN = (12, 16, 24)          # the glass between LEDs
OFF = (24, 31, 44)             # an unlit LED


class Face:
    def __init__(self):
        self.grid = [["." for _ in range(W)] for _ in range(H)]

    def rows(self, rows, top: int = 0, half: bool = True):
        """Draw rows from `top`. Each row is 16 letters (mirrored) when half, else 32. '.' is transparent."""
        for j, r in enumerate(rows):
            full = r + r[::-1] if half else r
            if len(full) != W:
                raise ValueError(f"row {top + j} is {len(full)} cells, not {W}: {r!r}")
            for i, ch in enumerate(full):
                if ch != ".":
                    self.set(i, top + j, ch)
        return self

    def set(self, x: int, y: int, ch: str):
        if 0 <= x < W and 0 <= y < H:
            self.grid[y][x] = ch
        return self

    def stamp(self, x: int, y: int, rows):
        """A small asymmetric sprite (rows of letters, '.' transparent) with its top-left at (x, y)."""
        for j, r in enumerate(rows):
            for i, ch in enumerate(r):
                if ch != ".":
                    self.set(x + i, y + j, ch)
        return self

    def line(self, pts, ch: str):
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = max(abs(x1 - x0), abs(y1 - y0), 1)
            for k in range(n + 1):
                self.set(round(x0 + (x1 - x0) * k / n), round(y0 + (y1 - y0) * k / n), ch)
        return self

    def png(self, path: Path, palette: dict):
        img = Image.new("RGB", (W * CELL, H * CELL), SCREEN)
        px = img.load()
        for y in range(H):
            for x in range(W):
                ch = self.grid[y][x]
                c = palette.get(ch, OFF) if ch != "." else OFF
                for v in range(CELL - 1):
                    for u in range(CELL - 1):
                        # a lit LED is brighter in its middle; rows 0-15 top to bottom
                        edge = u in (0, CELL - 2) or v in (0, CELL - 2)
                        k = 0.82 if (edge and ch != ".") else 1.0
                        px[x * CELL + u, y * CELL + v] = tuple(int(min(255, ci * k)) for ci in c)
        path.parent.mkdir(parents=True, exist_ok=True)
        img.save(path)
        return img


def sheet(faces: dict, path: Path):
    """All five faces side by side (a contact sheet for the report; not shipped)."""
    pad = 8
    img = Image.new("RGB", (len(faces) * (W * CELL + pad) + pad, H * CELL + 2 * pad), (40, 40, 40))
    for i, im in enumerate(faces.values()):
        img.paste(im, (pad + i * (W * CELL + pad), pad))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
