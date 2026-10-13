"""The floor-care unit's five faces (Task 20): a sunny clean freak whose grin turns into gritted teeth at a "stain".
Writes Resources/SkylineModels/bots/floor-care-unit_face_<Face>.png and a contact sheet in out/skyline-forge/.

    uv run --with pillow python tools/skyline/bots/floor-care-unit/faces.py
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import pixel_faces as pf  # noqa: E402

ID = "floor-care-unit"


def happy():
    f = pf.Face().rows([
        "................",
        "................",
        "................",
        ".....######.....",
        "....##....##....",
        "...##......##...",
        "................",
        "................",
        "................",
        ".ppp....########",
        ".ppp....#+++++++",
        ".........#++++++",
        "..........##tttt",
        "............####",
        "................",
        "................",
    ])
    # a clean sparkle
    f.stamp(27, 1, ["..w..", ".www.", "..w.."]).set(29, 5, "w").set(24, 0, "w")
    return f, {"#": (110, 240, 225), "+": (20, 70, 75), "t": (250, 130, 170), "p": (250, 120, 160), "w": (255, 255, 255)}


def alert():
    f = pf.Face().rows([
        "....###.........",
        "..##............",
        "................",
        "................",
        "....####........",
        "...#....#.......",
        "..#......#......",
        "..#..##..#......",
        "...#.##.#.......",
        "....####........",
        "................",
        "..............##",
        ".............#..",
        ".............#..",
        "..............##",
        "................",
    ])
    # "!" - the stain spotted
    f.stamp(15, 0, ["yy", "yy", "yy", "yy", "..", "yy"])
    return f, {"#": (245, 245, 235), "y": (255, 214, 60)}


def angry():
    f = pf.Face().rows([
        "................",
        "................",
        "..###...........",
        ".....###........",
        "........##......",
        "...######.......",
        "...#######......",
        "....#####.......",
        "................",
        "................",
        ".......#########",
        ".......#.#.#.#.#",
        ".......#########",
        "................",
        "................",
        "................",
    ])
    f.stamp(13, 0, [".r.r.", "rr.rr", ".....", "rr.rr", ".r.r."])   # an anger mark on the brow
    return f, {"#": (255, 60, 40), "r": (255, 150, 120)}


def hurt():
    f = pf.Face().rows([
        "................",
        "................",
        "................",
        "...##...........",
        ".....##.........",
        ".......##.......",
        ".....##.........",
        "...##...........",
        "................",
        "................",
        "................",
    ])
    f.rows([
        "..........##....##....##........",
        "........##..####..####..##......",
    ], top=11, half=False)
    f.stamp(29, 0, [".b.", "bbb", ".b."])                   # a sweat drop
    return f, {"#": (255, 178, 50), "b": (110, 190, 255)}


def wrecked():
    f = pf.Face().rows([
        "................",
        "................",
        "................",
        "...##....##.....",
        "....##..##......",
        ".....####.......",
        ".....####.......",
        "....##..##......",
        "...##....##.....",
        "................",
        "................",
        "................",
        "........########",
    ])
    f.stamp(18, 13, ["tt", "tt"])                               # tongue out: out of order
    f.line([(21, 0), (17, 5), (23, 9), (19, 15)], "k")          # the cracked screen
    f.line([(17, 5), (12, 7)], "k")
    return f, {"#": (110, 112, 118), "t": (150, 95, 110), "k": (190, 200, 210)}


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
