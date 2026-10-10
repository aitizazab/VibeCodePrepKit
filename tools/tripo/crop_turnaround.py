"""Crop the views of a style-E turnaround sheet into square pictures for Tripo's multi-view input.

    uv run --with pillow --with numpy python crop_turnaround.py <sheet.jpg> <outdir> [name1 name2 ...]

Finds each figure as a column run of non-background pixels (the sheets have a plain light-grey ground),
pads it to a square with the sheet's own background colour and saves it at 1536 x 1536.
Default names: front side back (left to right on the sheet).
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

src, outdir = Path(sys.argv[1]), Path(sys.argv[2])
names = sys.argv[3:] or ["front", "side", "back"]
im = Image.open(src).convert("RGB")
a = np.asarray(im).astype(np.int16)
H, W, _ = a.shape
# background: median of the border
border = np.concatenate([a[:8].reshape(-1, 3), a[-8:].reshape(-1, 3), a[:, :8].reshape(-1, 3), a[:, -8:].reshape(-1, 3)])
bg = np.median(border, axis=0)
fg = np.abs(a - bg).max(axis=2) > 28
cols = fg.sum(axis=0) > 3
runs, x = [], 0
while x < W:
    if cols[x]:
        s = x
        while x < W and cols[x]:
            x += 1
        runs.append([s, x])
    else:
        x += 1
# merge runs separated by small gaps, keep the big ones
merged = []
for r in runs:
    if merged and r[0] - merged[-1][1] < 40:
        merged[-1][1] = r[1]
    else:
        merged.append(r)
merged = [r for r in merged if r[1] - r[0] > 80]
print("figures (x ranges):", merged)
if len(merged) != len(names):
    sys.exit(f"found {len(merged)} figures, expected {len(names)}")
outdir.mkdir(parents=True, exist_ok=True)
bgc = tuple(int(v) for v in bg)
for (x0, x1), n in zip(merged, names):
    rows = np.where(fg[:, x0:x1].sum(axis=1) > 2)[0]
    y0, y1 = rows[0], rows[-1] + 1
    h, w = y1 - y0, x1 - x0
    side = int(max(h, w) * 1.08)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    canvas = Image.new("RGB", (side, side), bgc)
    crop = im.crop((x0, y0, x1, y1))
    canvas.paste(crop, (side // 2 - (cx - x0), side // 2 - (cy - y0)))
    canvas = canvas.resize((1536, 1536), Image.LANCZOS)
    out = outdir / f"{n}.png"
    canvas.save(out)
    print(n, "bbox", (x0, y0, x1, y1), "->", out)
