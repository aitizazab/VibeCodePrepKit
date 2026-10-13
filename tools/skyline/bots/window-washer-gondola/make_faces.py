"""Draws the window-washer gondola's five face textures (an LED-matrix display: two eyes and a big moustache) by code.

    uv run python tools/skyline/bots/window-washer-gondola/make_faces.py

Writes Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/bots/window-washer-gondola_face_<Happy|Alert|Angry|Hurt|Wrecked>.png
(SkylineBotFace's names, BotFace's order). The display is 32 x 20 LEDs, drawn 16 px per LED (512 x 320) so it stays crisp
under Unity's default bilinear import. Faces are left-right symmetric (the sweat drop aside), so they read the same
whichever way the model's UVs land.

The gondola is a cheery old window-washer in hi-vis orange: smiling ^ ^ eyes over a bushy moustache while it works
(happy), eyebrows up and an "o" under the moustache when it spots you (alert), hard brows and a bristling moustache when
it means business (angry), screwed-up eyes and a sweat drop when hit (hurt), X eyes and a drooping moustache when wrecked.
"""
from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "bots"
BOT = "window-washer-gondola"
W, H, CELL = 32, 20, 16
BG = (10, 12, 18)
OFF = (24, 28, 38)          # an unlit LED: the matrix stays visible



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



ORANGE = (255, 138, 28)     # the gondola's own colour: hi-vis orange
YELLOW = (255, 220, 70)
RED = (255, 52, 40)
AMBER = (255, 176, 60)
BLUE = (110, 190, 255)
GREY = (105, 108, 116)

LX = 9                      # the left eye's centre column; the right eye mirrors it (W - 1 - LX = 22)

# The moustache's left half, column by column: (x, top row, rows thick). A handlebar: full in the middle with a parting,
# tapering and sweeping up to curled tips. The right half mirrors it.
MOUSTACHE = ((15, 15, 2), (14, 14, 3), (13, 14, 3), (12, 14, 3), (11, 14, 3), (10, 14, 3), (9, 14, 2), (8, 14, 2),
             (7, 13, 2), (6, 12, 2), (5, 11, 2), (4, 11, 1))


def moustache(g, c, droop=0.0, bristle=False):
    """The moustache; droop pulls the tips down (a sad, limp handlebar), bristle stands a few hairs up."""
    for x, top, thick in MOUSTACHE:
        if droop and x < 10:
            top += round(droop * (10 - x))
        for y in range(top, top + thick):
            g.sym(x, y, c)
    if bristle:
        for x in (10, 12, 14):
            g.sym(x, 13, c)


def eyes_ring(g, c, r=2.6):
    g.ring(LX, 6.0, r, c, thick=1.1)
    g.ring(W - 1 - LX, 6.0, r, c, thick=1.1)


def happy():
    g = Grid()
    for dx in (-2, -1, 0, 1, 2):               # ^ ^
        y = 4 + abs(dx)
        g.sym(LX + dx, y, ORANGE)
        g.sym(LX + dx, y + 1, ORANGE)
    moustache(g, ORANGE)
    g.line(13, 18, 15, 18, ORANGE, mirror=True)   # the grin under it
    g.sym(12, 17, ORANGE)
    return g


def alert():
    g = Grid()
    eyes_ring(g, YELLOW)                       # O O
    g.sym(LX, 6, YELLOW)
    g.line(6, 1, 12, 0, YELLOW, mirror=True)   # brows shot up
    moustache(g, YELLOW)
    g.ring(15.5, 17.9, 1.2, YELLOW, thick=1.0) # "o"
    return g


def angry():
    g = Grid()
    g.line(5, 2, 13, 5, RED, mirror=True)      # hard brows down to the middle
    g.line(5, 3, 13, 6, RED, mirror=True)
    for dx in (-1, 0, 1):
        g.sym(LX + dx, 8, RED)
        g.sym(LX + dx, 9, RED)
    moustache(g, RED, bristle=True)
    g.line(12, 18, 15, 18, RED, mirror=True)   # a flat, set mouth
    return g


def hurt():
    g = Grid()
    g.line(7, 3, 11, 5, AMBER, mirror=True)    # > <
    g.line(7, 7, 11, 5, AMBER, mirror=True)
    moustache(g, AMBER, droop=0.7)
    g.line(13, 19, 15, 18, AMBER, mirror=True) # a wobbly wince
    for x, y in ((28, 2), (28, 3), (27, 4), (28, 4), (29, 4), (28, 5)):
        g.set(x, y, BLUE)                      # the sweat drop
    return g


def wrecked():
    g = Grid()
    for d in range(-2, 3):                     # X X
        g.sym(LX + d, 6 + d, GREY)
        g.sym(LX + d, 6 - d, GREY)
    moustache(g, GREY, droop=1.4)
    g.line(13, 19, 15, 19, GREY, mirror=True)
    return g


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, f in (("Happy", happy), ("Alert", alert), ("Angry", angry), ("Hurt", hurt), ("Wrecked", wrecked)):
        write_png(OUT / f"{BOT}_face_{name}.png", f())
    print("wrote 5 faces into", OUT)


if __name__ == "__main__":
    main()
