"""Draws the window-washer gondola's five face textures (an LED-matrix display: two eyes and a big moustache) by code.

    uv run --with pillow python tools/skyline/bots/window-washer-gondola/make_faces.py

Writes Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/bots/window-washer-gondola_face_<Face>.png
(SkylineBotFace's names, BotFace's order) through the shared LED writer `tools/skyline/bots/pixel_faces.py`: a 32 x 16
matrix, its cells drawn 16 px wide here (pixel_faces.CELL set to 16), so each texture is 512 x 256, a power of two that
Unity's default import keeps without resampling. Faces are left-right symmetric (the sweat drop aside).

The gondola is a cheery old window-washer with a handlebar moustache. Its colour is CONCIERGE's gold (the firm's orange
belongs to the player's things, spec 1.3): gold ^ ^ eyes and moustache while it works (happy), white O O eyes and brows
up when it spots you (alert), hard red brows and a bristling moustache when it means business (angry), a pale soapy
blue with screwed-up eyes and a sweat drop when hit (hurt: blue, not amber, so it can't be mistaken for its gold calm),
grey X X eyes and a drooping moustache when wrecked.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import pixel_faces as pf  # noqa: E402

pf.CELL = 16                 # 512 x 256: a power of two
BOT = "window-washer-gondola"
W, H = pf.W, pf.H            # 32 x 16; y = 0 is the top row

PALETTE = {
    "o": (255, 206, 84),     # the gondola's own colour: CONCIERGE gold
    "w": (240, 248, 255),
    "r": (255, 52, 40),
    "u": (150, 205, 255),    # hurt: pale soapy blue
    "b": (90, 160, 255),     # the sweat drop
    "g": (105, 108, 116),
}

LX = 9                       # the left eye's centre column; the right eye mirrors it (W - 1 - LX = 22)

# The moustache's left half, column by column: (x, top row, rows thick). A handlebar: full in the middle with a parting,
# tapering and sweeping up to curled tips. The right half mirrors it.
MOUSTACHE = ((15, 11, 2), (14, 10, 3), (13, 10, 3), (12, 10, 3), (11, 10, 3), (10, 10, 3), (9, 10, 2), (8, 10, 2),
             (7, 9, 2), (6, 8, 2), (5, 7, 2), (4, 7, 1))


def put(f, x, y, ch):
    f.set(math.floor(x + 0.5), math.floor(y + 0.5), ch)


def sym(f, x, y, ch):
    put(f, x, y, ch)
    put(f, W - 1 - x, y, ch)


def line(f, x0, y0, x1, y1, ch, mirror=False):
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(n + 1):
        t = i / n
        (sym if mirror else put)(f, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, ch)


def ring(f, cx, cy, r, ch, thick=1.0):
    for y in range(H):
        for x in range(W):
            if r - thick / 2 <= math.hypot(x - cx, y - cy) <= r + thick / 2:
                f.set(x, y, ch)


def moustache(f, ch, droop=0.0, bristle=False):
    """The moustache; droop pulls the tips down (a limp handlebar), bristle stands a few hairs up."""
    for x, top, thick in MOUSTACHE:
        if droop and x < 10:
            top += round(droop * (10 - x))
        for y in range(top, top + thick):
            sym(f, x, y, ch)
    if bristle:
        for x in (10, 12, 14):
            sym(f, x, 9, ch)


def happy():
    f = pf.Face()
    for dx in (-2, -1, 0, 1, 2):               # ^ ^
        y = 2 + abs(dx)
        sym(f, LX + dx, y, "o")
        sym(f, LX + dx, y + 1, "o")
    moustache(f, "o")
    line(f, 13, 14, 15, 14, "o", mirror=True)   # the grin under it
    sym(f, 12, 13, "o")
    return f


def alert():
    f = pf.Face()
    ring(f, LX, 4.0, 2.4, "w", thick=1.1)      # O O
    ring(f, W - 1 - LX, 4.0, 2.4, "w", thick=1.1)
    sym(f, LX, 4, "w")
    line(f, 6, 0, 12, 0, "w", mirror=True)     # brows shot up
    moustache(f, "w")
    ring(f, 15.5, 14.0, 1.1, "w", thick=1.0)   # "o"
    return f


def angry():
    f = pf.Face()
    line(f, 5, 1, 13, 4, "r", mirror=True)     # hard brows down to the middle
    line(f, 5, 2, 13, 5, "r", mirror=True)
    for dx in (-1, 0, 1):
        sym(f, LX + dx, 6, "r")
        sym(f, LX + dx, 7, "r")
    moustache(f, "r", bristle=True)
    line(f, 12, 14, 15, 14, "r", mirror=True)  # a flat, set mouth
    return f


def hurt():
    f = pf.Face()
    line(f, 7, 2, 11, 4, "u", mirror=True)     # > <
    line(f, 7, 6, 11, 4, "u", mirror=True)
    moustache(f, "u", droop=0.7)
    line(f, 13, 15, 15, 14, "u", mirror=True)  # a wobbly wince
    f.stamp(27, 0, (".b.", ".b.", "bbb", ".b."))   # the sweat drop
    return f


def wrecked():
    f = pf.Face()
    for d in range(-2, 3):                     # X X
        sym(f, LX + d, 4 + d, "g")
        sym(f, LX + d, 4 - d, "g")
    moustache(f, "g", droop=1.0)
    line(f, 13, 15, 15, 15, "g", mirror=True)
    return f


def main():
    faces = {}
    for name, make in (("Happy", happy), ("Alert", alert), ("Angry", angry), ("Hurt", hurt), ("Wrecked", wrecked)):
        faces[name] = make().png(pf.OUT / f"{BOT}_face_{name}.png", PALETTE)
    print("wrote 5 faces (512 x 256) into", pf.OUT)
    return faces


if __name__ == "__main__":
    faces = main()
    if len(sys.argv) > 1:
        pf.sheet(faces, Path(sys.argv[1]))
