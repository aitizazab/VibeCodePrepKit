"""Draws the spotlight's five face textures (an LED-matrix display, one big eye) by code.

    uv run python tools/skyline/bots/spotlight/make_faces.py

Writes Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/bots/spotlight_face_<Happy|Alert|Angry|Hurt|Wrecked>.png
(SkylineBotFace's names, BotFace's order). The display is 32 x 20 LEDs, drawn 16 px per LED (512 x 320) so it stays crisp
under Unity's default bilinear import. Faces are left-right symmetric, so they read the same whichever way the model's
UVs land.

The spotlight's face is a single searchlight eye in lamp-white: a content crescent while it scans (happy), wide open with
a pinprick pupil when it spots you (alert), a red crosshair under a hard brow while the beam follows and fires (angry),
squeezed shut and glitching when hit (hurt), an X and dead pixels when wrecked.
"""
from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "bots"
BOT = "spotlight"
W, H, CELL = 32, 20, 16
BG = (10, 12, 18)
OFF = (24, 28, 38)          # an unlit LED: the matrix stays visible

LAMP = (255, 236, 170)      # the spotlight's own colour: warm lamp white
WHITE = (240, 248, 255)
RED = (255, 52, 40)
AMBER = (255, 170, 50)
GREY = (105, 108, 116)


class Grid:
    def __init__(self):
        self.px = [[None] * W for _ in range(H)]   # y = 0 is the top row

    def set(self, x, y, c):
        x, y = math.floor(x + 0.5), math.floor(y + 0.5)   # half up, never banker's rounding
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c

    def sym(self, x, y, c):
        """Set (x, y) and its mirror (W-1-x, y)."""
        self.set(x, y, c)
        self.set(W - 1 - x, y, c)

    def line(self, x0, y0, x1, y1, c, mirror=False):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for i in range(n + 1):
            t = i / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            (self.sym if mirror else self.set)(x, y, c)

    def ring(self, cx, cy, r, c, thick=1.0, a0=0.0, a1=360.0):
        for y in range(H):
            for x in range(W):
                d = math.hypot(x - cx, (y - cy) * 1.0)
                if r - thick / 2 <= d <= r + thick / 2:
                    a = math.degrees(math.atan2(cy - y, x - cx)) % 360
                    if (a0 <= a <= a1) if a0 <= a1 else (a >= a0 or a <= a1):
                        self.set(x, y, c)

    def disc(self, cx, cy, r, c):
        for y in range(H):
            for x in range(W):
                if math.hypot(x - cx, y - cy) <= r:
                    self.set(x, y, c)


def write_png(path: Path, grid: Grid):
    w, h = W * CELL, H * CELL
    rows = []
    for py in range(h):
        gy, iy = divmod(py, CELL)
        row = bytearray([0])
        for px in range(w):
            gx, ix = divmod(px, CELL)
            c = grid.px[gy][gx]
            # an LED: a rounded square inset 2 px, the lit ones with a soft 1 px halo
            dx, dy = min(ix, CELL - 1 - ix), min(iy, CELL - 1 - iy)
            inside = dx >= 2 and dy >= 2 and not (dx == 2 and dy == 2)
            if c is None:
                col = OFF if inside else BG
            elif inside:
                hot = 1.0 if (dx >= 4 and dy >= 4) else 0.86
                col = tuple(min(255, int(v * hot)) for v in c)
            else:
                col = tuple(int(b + (v - b) * 0.28) for v, b in zip(c, BG))
            row += bytes(col)
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    path.write_bytes(png)


CX, CY = 15.5, 10.0   # the eye's centre (between the two middle columns, so mirroring is exact)


def status_bar(g, c, n=3):
    """Three little lamps along the bottom edge (the unit's status), mirrored."""
    for i in range(n):
        g.sym(11 + i * 2, 18, c)


def happy():
    g = Grid()
    # a content crescent: the lower lid curves up, like a smiling closed eye, wide
    g.ring(CX, 15.0, 9.0, LAMP, thick=1.6, a0=30, a1=150)
    g.ring(CX, 15.0, 7.6, LAMP, thick=1.0, a0=45, a1=135)
    # lashes / glints
    g.line(5, 9, 4, 7, LAMP, mirror=True)
    status_bar(g, LAMP)
    return g


def alert():
    g = Grid()
    g.ring(CX, CY, 7.6, WHITE, thick=1.4)          # the eye wide open
    g.disc(CX, CY, 1.6, WHITE)                     # a pinprick pupil
    g.line(9, 1, 13, 0, WHITE, mirror=True)        # brows raised high
    status_bar(g, WHITE)
    return g


def angry():
    g = Grid()
    # a crosshair under a hard V brow: the beam's lock
    g.ring(CX, 11.0, 6.2, RED, thick=1.3)
    g.line(15.5 - 9.5, 11, 15.5 - 3.5, 11, RED, mirror=True)       # horizontal hairs, through the ring
    for y in range(13, 20):
        g.sym(15, y, RED)
    g.disc(CX, 11.0, 1.0, RED)
    g.line(3, 1, 13, 5, RED, mirror=True)          # the brow
    g.line(3, 2, 13, 6, RED, mirror=True)
    return g


def hurt():
    g = Grid()
    # squeezed shut: two sharp chevrons meeting in the middle, a crack of static across
    g.line(6, 6, 14, 10, AMBER, mirror=True)
    g.line(6, 14, 14, 10, AMBER, mirror=True)
    g.line(6, 7, 12, 10, AMBER, mirror=True)
    g.line(6, 13, 12, 10, AMBER, mirror=True)
    for x, y in ((2, 3), (5, 1), (9, 17), (3, 16), (12, 2), (1, 10)):
        g.sym(x, y, AMBER)
    return g


def wrecked():
    g = Grid()
    g.line(9, 4, 22, 16, GREY)
    g.line(22, 4, 9, 16, GREY)
    g.line(10, 4, 23, 16, GREY)
    g.line(21, 4, 8, 16, GREY)
    for x, y in ((2, 2), (4, 17), (27, 1), (29, 15), (6, 9)):
        g.sym(x, y, GREY)
    return g


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, f in (("Happy", happy), ("Alert", alert), ("Angry", angry), ("Hurt", hurt), ("Wrecked", wrecked)):
        write_png(OUT / f"{BOT}_face_{name}.png", f())
    print("wrote 5 faces into", OUT)


if __name__ == "__main__":
    main()
