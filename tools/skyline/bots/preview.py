"""A quick flat-shaded preview of a bot model without a GPU (Task 20): reads the triangles model.py dumped (Unity
coordinates, material colours) and draws four views with the painter's algorithm. Not a render of the game's look:
a check of shape, proportion, orientation (the face must be on the +Z side) and plate placement.

    uv run --with pillow --with numpy python tools/skyline/bots/preview.py <preview.json> <out.png> [face.png]
"""
from __future__ import annotations

import json
import math
import sys

import numpy as np
from PIL import Image, ImageDraw

LIGHT = np.array([0.4, 0.8, 0.45])
LIGHT /= np.linalg.norm(LIGHT)


def view_matrix(yaw_deg: float, pitch_deg: float) -> np.ndarray:
    """Camera looking at the origin from yaw (0 = from the front, +Z) and pitch (positive = from above)."""
    y, p = math.radians(yaw_deg), math.radians(pitch_deg)
    fwd = -np.array([math.sin(y) * math.cos(p), math.sin(p), math.cos(y) * math.cos(p)])   # camera looks along fwd
    # Unity is left-handed: right = up x forward
    right = np.cross([0, 1, 0], fwd)
    right /= np.linalg.norm(right)
    up = np.cross(fwd, right)
    return np.stack([right, up, fwd])


def draw(tris, yaw, pitch, size, scale, center, face_img=None):
    img = Image.new("RGB", (size, size), (52, 54, 60))
    d = ImageDraw.Draw(img)
    m = view_matrix(yaw, pitch)
    items = []
    for t in tris:
        v = np.array(t["v"]) - center
        cam = v @ m.T
        # Blender's counter-clockwise triangles, mirrored into Unity's axes: the outward normal is -(b-a) x (c-a)
        n = -np.cross(v[1] - v[0], v[2] - v[0])
        ln = np.linalg.norm(n)
        if ln < 1e-12:
            continue
        n /= ln
        if n @ m[2] > 0:                                  # facing away
            continue
        shade = 0.35 + 0.65 * max(0.0, float(n @ LIGHT))
        c = (np.array(t["c"]) ** (1 / 2.2)) * shade      # the material colours are linear
        pts = [(size / 2 + p[0] * scale, size / 2 - p[1] * scale) for p in cam]
        items.append((cam[:, 2].mean(), pts, tuple(int(min(255, x * 255)) for x in c), t["o"]))
    items.sort(key=lambda it: -it[0])
    for _, pts, col, name in items:
        if name == "Face" and face_img is not None:
            col = (90, 230, 215)
        d.polygon(pts, fill=col)
    return img


def main(argv):
    data = json.load(open(argv[0]))
    tris = data["tris"]
    allv = np.array([v for t in tris for v in t["v"]])
    center = (allv.max(0) + allv.min(0)) / 2
    extent = (allv.max(0) - allv.min(0)).max()
    size = 420
    scale = size * 0.8 / extent
    views = [("front", 0, 8), ("3/4 front-right", 40, 25), ("side (right)", 90, 0), ("from above, back-left", 220, 50)]
    sheet = Image.new("RGB", (size * len(views), size + 24), (30, 30, 30))
    dd = ImageDraw.Draw(sheet)
    for i, (label, yaw, pitch) in enumerate(views):
        sheet.paste(draw(tris, yaw, pitch, size, scale, center), (i * size, 24))
        dd.text((i * size + 8, 6), label, fill=(230, 230, 230))
    sheet.save(argv[1])
    for n in data["nodes"]:
        print(f"{n['name']:22s} parent={n['parent']!s:16s} {n['type']:6s} unity=({n['unity'][0]:+.3f},{n['unity'][1]:+.3f},{n['unity'][2]:+.3f}) rot={n['rot']}")
    print("bounds", allv.min(0).round(3), allv.max(0).round(3), "tris", len(tris))


if __name__ == "__main__":
    main(sys.argv[1:])
