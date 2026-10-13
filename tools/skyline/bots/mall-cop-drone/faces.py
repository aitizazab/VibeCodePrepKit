"""The mall-cop drone's five faces (Task 20): officious. A bored half-lidded patrol stare with a body-cam REC light, a
raised-brow "Halt!" under a siren bar, crosshair eyes and a shouting mouth when it fires, a glitch when hit, static when
wrecked. Writes Resources/SkylineModels/bots/mall-cop-drone_face_<Face>.png and a contact sheet in out/skyline-forge/.

    uv run --with pillow python tools/skyline/bots/mall-cop-drone/faces.py
"""
from __future__ import annotations

import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import pixel_faces as pf  # noqa: E402

ID = "mall-cop-drone"
RED, BLUE = (255, 50, 50), (60, 120, 255)


def happy():
    f = pf.Face().rows([
        "................",
        "................",
        "...#######......",
        "................",
        "..##########....",
        "..#..####..#....",
        "...#.####.#.....",
        "....######......",
        "................",
        "................",
    ])
    f.rows(["................................",
            "..................##............",
            "............######..............",
            ], top=10, half=False)                      # a smug little smirk
    f.stamp(1, 0, ["rr", "rr"])                         # body-cam REC light
    return f, {"#": (90, 160, 255), "r": RED}


def alert():
    f = pf.Face()
    f.rows([
        "................",
        "................",
        "................",
        "................",
        "...######.......",
        "...#....#.......",
        "...#.##.#.......",
        "...#.##.#.......",
        "...#....#.......",
        "...######.......",
        "................",
        "...............#",
        "..............#.",
        "..............#.",
        "...............#",
    ])
    f.line([(2, 1), (8, 0)], "#")                       # one brow raised...
    f.line([(23, 3), (29, 2)], "#")                     # ...the other level and stern
    f.stamp(10, 0, ["rrrrr", "rrrrr"]).stamp(17, 0, ["bbbbb", "bbbbb"])   # the siren bar
    return f, {"#": (245, 245, 240), "r": RED, "b": BLUE}


def angry():
    f = pf.Face().rows([
        "................",
        ".###............",
        "....###.........",
        ".......##.......",
        ".....#..........",
        "...#####........",
        "...#...#........",
        ".###.#.###......",
        "...#...#........",
        "...#####........",
        ".....#..........",
        "................",
        ".........#######",
        ".........#rrrrrr",
        ".........#######",
        "................",
    ])
    return f, {"#": (255, 55, 40), "r": (120, 15, 15)}


def hurt():
    f = pf.Face().rows([
        "................",
        "................",
        ".......##.......",
        ".....##.........",
        "................",
        "..########......",
        "................",
        "................",
        "................",
    ])
    # the glitch: a band of the face torn sideways
    for x in range(W := pf.W):
        if f.grid[5][x] == "#":
            f.set(x, 5, ".")
            f.set(min(W - 1, x + 3), 6, "g")
    f.rows(["........gg..gg..g...g..gg.......",
            ], top=8, half=False)
    f.stamp(12, 11, ["..####..", ".#....#.", "#......#"])     # an offended frown
    return f, {"#": (255, 178, 50), "g": (255, 225, 140)}


def wrecked():
    f = pf.Face().rows([
        "................",
        "................",
        "................",
        "...##....##.....",
        ".....#..#.......",
        "......##........",
        ".....#..#.......",
        "...##....##.....",
        "................",
        "................",
        "................",
        "................",
        ".........#######",
    ])
    rnd = random.Random(7)
    for _ in range(70):                                  # static
        x, y = rnd.randrange(pf.W), rnd.randrange(pf.H)
        if f.grid[y][x] == ".":
            f.set(x, y, "s" if rnd.random() < 0.6 else "d")
    return f, {"#": (120, 122, 128), "s": (60, 64, 72), "d": (95, 98, 106)}


def main() -> int:
    images = {}
    for name, fn in zip(pf.FACES, (happy, alert, angry, hurt, wrecked)):
        face, pal = fn()
        images[name] = face.png(pf.OUT / f"{ID}_face_{name}.png", pal)
    pf.sheet(images, pf.ROOT / "out" / "skyline-forge" / "bots" / f"{ID}_faces.png")
    print(f"wrote {len(images)} faces for {ID}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
