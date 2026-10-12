"""A front close-up of a head with a 5 mm grid (labelled every 1 cm in world x / z), for reading face landmarks.
    uv run --with pillow --with numpy python tools/tripo/vh_headgrid.py <dump.npz> <Color.jpg> <out.png> [half_width_m]
"""
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, __file__.rsplit("/", 1)[0].rsplit("\\", 1)[0])
import vh_tex as vt

npz, texp, out = sys.argv[1:4]
m = vt.load(npz)
b = m["bones"]
head = np.array(b["Head"])
top = np.array(b["HeadTop_End"])
hw = float(sys.argv[4]) if len(sys.argv) > 4 else (top[2] - head[2]) * 0.75
cx = head[0]
z0 = head[2] - hw * 0.35
if len(sys.argv) > 5:   # a full-body grid: <half_width> <z0>
    z0 = float(sys.argv[5])
rect = (cx - hw, z0, cx + hw, z0 + hw * 2)
STEP = 0.005 if hw < 0.2 else 0.025
px = 1100
img, tid, bary = vt.render_ortho(m, vt.load_tex(texp, 4096), rect, px)
bg = np.full_like(img[..., :3], 0.3)
comp = img[..., :3] * img[..., 3:] + bg * (1 - img[..., 3:])
im = Image.fromarray(np.clip(comp * 255, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(im)
W, H = im.size
def X(x): return (x - rect[0]) / (rect[2] - rect[0]) * W
def Z(z): return (1 - (z - rect[1]) / (rect[3] - rect[1])) * H
for i in range(int(np.floor(rect[0] / STEP)), int(np.ceil(rect[2] / STEP)) + 1):
    x = i * STEP
    major = i % 2 == 0
    d.line([(X(x), 0), (X(x), H)], fill=(255, 255, 0) if major else (120, 120, 60), width=1)
    if major:
        d.text((X(x) + 2, 2), "%d" % round(x * 1000), fill=(255, 255, 0))
for i in range(int(np.floor(rect[1] / STEP)), int(np.ceil(rect[3] / STEP)) + 1):
    z = i * STEP
    major = i % 2 == 0
    d.line([(0, Z(z)), (W, Z(z))], fill=(0, 255, 255) if major else (60, 120, 120), width=1)
    if major:
        d.text((2, Z(z) + 2), "%d" % round(z * 1000), fill=(0, 255, 255))
im.save(out)
print("rect", [round(v, 4) for v in rect], "head", head.round(4).tolist(), "top", top.round(4).tolist())
