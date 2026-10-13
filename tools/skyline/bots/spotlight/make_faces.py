"""Draws the spotlight's five face textures (an LED-matrix display, one big eye) by code.

    uv run --with pillow python tools/skyline/bots/spotlight/make_faces.py

Writes Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/bots/spotlight_face_<Happy|Alert|Angry|Hurt|Wrecked>.png
(SkylineBotFace's names, BotFace's order) through the shared LED writer `tools/skyline/bots/pixel_faces.py`: a 32 x 16
matrix. Its cells are drawn 16 px wide here (pixel_faces.CELL set to 16), so each texture is 512 x 256, a power of two
that Unity's default import keeps without resampling. Faces are left-right symmetric, so they read the same whichever
way the model's UVs land.

The spotlight's face is a single searchlight eye in lamp-white: a content crescent while it scans (happy), wide open with
a pinprick pupil when it spots you (alert), a red crosshair under a hard brow while the beam follows and fires (angry),
squeezed shut and glitching when hit (hurt), an X and dead pixels when wrecked.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import pixel_faces as pf  # noqa: E402

pf.CELL = 16                 # 512 x 256: a power of two
BOT = "spotlight"
W, H = pf.W, pf.H            # 32 x 16; y = 0 is the top row
CX = (W - 1) / 2             # 15.5: between the two middle columns, so mirroring is exact

PALETTE = {
    "l": (255, 236, 170),    # the spotlight's own colour: warm lamp white
    "w": (240, 248, 255),
    "r": (255, 52, 40),
    "a": (255, 170, 50),
    "g": (105, 108, 116),
}


def put(f, x, y, ch):
    f.set(math.floor(x + 0.5), math.floor(y + 0.5), ch)   # half up, never banker's rounding


def sym(f, x, y, ch):
    put(f, x, y, ch)
    put(f, W - 1 - x, y, ch)


def line(f, x0, y0, x1, y1, ch, mirror=False):
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(n + 1):
        t = i / n
        (sym if mirror else put)(f, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, ch)


def ring(f, cx, cy, r, ch, thick=1.0, a0=0.0, a1=360.0):
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - cx, y - cy)
            if r - thick / 2 <= d <= r + thick / 2:
                a = math.degrees(math.atan2(cy - y, x - cx)) % 360
                if a0 <= a <= a1:
                    f.set(x, y, ch)


def disc(f, cx, cy, r, ch):
    for y in range(H):
        for x in range(W):
            if math.hypot(x - cx, y - cy) <= r:
                f.set(x, y, ch)


def happy():
    f = pf.Face()
    ring(f, CX, 11.5, 7.6, "l", thick=1.6, a0=30, a1=150)   # a content crescent: a smiling closed eye
    ring(f, CX, 11.5, 6.3, "l", thick=1.0, a0=45, a1=135)
    line(f, 5, 7, 4, 5, "l", mirror=True)                   # lashes
    for i in range(3):
        sym(f, 11 + i * 2, 14, "l")                         # three status lamps
    return f


def alert():
    f = pf.Face()
    ring(f, CX, 7.5, 6.3, "w", thick=1.4)                   # the eye wide open
    disc(f, CX, 7.5, 1.4, "w")                              # a pinprick pupil
    line(f, 2, 2, 7, 0, "w", mirror=True)                   # brows raised high
    return f


def angry():
    f = pf.Face()
    ring(f, CX, 8.0, 5.2, "r", thick=1.3)                   # the beam's crosshair
    line(f, 5, 8, 10, 8, "r", mirror=True)
    for y in (14, 15):
        sym(f, 15, y, "r")
    disc(f, CX, 8.0, 0.9, "r")
    line(f, 3, 0, 13, 3, "r", mirror=True)                  # a hard V brow
    line(f, 3, 1, 13, 4, "r", mirror=True)
    return f


def hurt():
    f = pf.Face()
    line(f, 6, 4, 14, 8, "a", mirror=True)                  # squeezed shut: two chevrons meeting in the middle
    line(f, 6, 12, 14, 8, "a", mirror=True)
    line(f, 6, 5, 12, 8, "a", mirror=True)
    line(f, 6, 11, 12, 8, "a", mirror=True)
    for x, y in ((2, 2), (5, 0), (9, 15), (3, 13), (12, 1), (1, 8)):
        sym(f, x, y, "a")                                   # static
    return f


def wrecked():
    f = pf.Face()
    for dx in (0, 1):
        line(f, 9 + dx, 2, 22 + dx, 13, "g")
        line(f, 22 - dx, 2, 9 - dx, 13, "g")
    for x, y in ((2, 1), (4, 14), (27, 0), (29, 12), (6, 7)):
        sym(f, x, y, "g")                                   # dead pixels
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
