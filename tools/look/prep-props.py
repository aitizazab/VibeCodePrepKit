"""Props for the Look library (Poly Haven models, CC0) and the procedural leaf atlas.

    uv run --with numpy --with pillow python -I tools/look/prep-props.py <models-download-dir> <ThirdParty/LookProps> <Look/Textures>

Models: <id>/model.fbx, color.jpg, normal.jpg, arm.jpg (downloaded from api.polyhaven.com/files/<id>) are written as
<Name>/<Name>.fbx, _Albedo.jpg, _Normal.jpg, _Mask.png (the same packing as the texture library).
Leaf atlas: VH_LeafClusters.png, a 2x2 atlas of painted leaf clusters (RGBA, alpha-tested) for the stylized trees.
"""
import math
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PROPS = {"covered_car": "CoveredCar", "water_manhole_cover": "ManholeCover"}


def props(src, out):
    for pid, name in PROPS.items():
        d = os.path.join(src, pid)
        od = os.path.join(out, name)
        os.makedirs(od, exist_ok=True)
        shutil.copyfile(os.path.join(d, "model.fbx"), os.path.join(od, name + ".fbx"))
        shutil.copyfile(os.path.join(d, "color.jpg"), os.path.join(od, name + "_Albedo.jpg"))
        shutil.copyfile(os.path.join(d, "normal.jpg"), os.path.join(od, name + "_Normal.jpg"))
        arm = np.asarray(Image.open(os.path.join(d, "arm.jpg")).convert("RGB")).astype(np.float32) / 255.0
        mask = np.stack([arm[..., 2], arm[..., 0], np.full(arm.shape[:2], 0.5, np.float32), 1.0 - arm[..., 1]], -1)
        Image.fromarray((mask * 255).astype(np.uint8), "RGBA").save(os.path.join(od, name + "_Mask.png"), optimize=True)
        print("ok", name)


def leaf(draw, cx, cy, length, width, angle, color):
    """A leaf: a pointed ellipse with a midrib, drawn as a polygon."""
    pts = []
    for i in range(24):
        t = i / 23.0
        x = (t - 0.5) * length
        w = width * math.sin(math.pi * t) ** 0.8 * (1.0 - 0.25 * t)
        pts.append((x, w))
    for i in range(23, -1, -1):
        t = i / 23.0
        x = (t - 0.5) * length
        w = width * math.sin(math.pi * t) ** 0.8 * (1.0 - 0.25 * t)
        pts.append((x, -w))
    ca, sa = math.cos(angle), math.sin(angle)
    poly = [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]
    draw.polygon(poly, fill=color)
    rib = tuple(max(0, c - 18) for c in color[:3]) + (255,)
    draw.line([(cx - length * 0.45 * ca, cy - length * 0.45 * sa), (cx + length * 0.45 * ca, cy + length * 0.45 * sa)], fill=rib, width=2)


def leaf_atlas(out_dir, size=1024, seed=11):
    rng = np.random.default_rng(seed)
    os.makedirs(out_dir, exist_ok=True)
    half = size // 2
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    # Stylized late-summer greens: a deep shade, a mid, a sunlit yellow-green, a few drying leaves.
    palette = [(52, 92, 38), (72, 118, 44), (96, 140, 52), (126, 160, 62), (150, 168, 70), (168, 150, 70)]
    weights = np.array([0.18, 0.28, 0.26, 0.17, 0.08, 0.03])
    for cell in range(4):
        ox, oy = (cell % 2) * half, (cell // 2) * half
        layer = Image.new("RGBA", (half, half), (0, 0, 0, 0))
        dr = ImageDraw.Draw(layer)
        n = 70 + cell * 12
        for i in range(n):
            # Leaves fill a rounded clump; later (top) leaves are lighter, giving the cluster volume.
            r = half * 0.38 * math.sqrt(rng.random())
            a = rng.random() * 2 * math.pi
            cx = half / 2 + r * math.cos(a)
            cy = half / 2 + r * math.sin(a) * 0.9
            k = i / n
            idx = rng.choice(len(palette), p=weights)
            base = np.array(palette[idx], np.float32) * (0.78 + 0.4 * k) * (0.92 + 0.16 * rng.random())
            color = tuple(int(c) for c in np.clip(base, 0, 255)) + (255,)
            length = half * (0.13 + 0.06 * rng.random())
            leaf(dr, cx, cy, length, length * 0.32, a + rng.normal(0, 0.6), color)
        layer = layer.filter(ImageFilter.SMOOTH)
        img.alpha_composite(layer, (ox, oy))
    img.save(os.path.join(out_dir, "VH_LeafClusters.png"), optimize=True)
    print("ok leaves")


def main():
    props(sys.argv[1], sys.argv[2])
    leaf_atlas(sys.argv[3])


if __name__ == "__main__":
    main()
