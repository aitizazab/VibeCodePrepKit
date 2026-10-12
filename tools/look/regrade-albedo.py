"""Regrade a few library albedos after the judge's round 1 (the Look agent, Visiting Hours, pass 2).

  Concrete_Sidewalk, Concrete_Kerb: pale warm-grey concrete, the brightest surface on the street at golden hour (they
      were a dark purple-brown, darker than the road).
  CoveredCar: a lighter, faded fabric cover (it read as a navy blob; a lighter cover shows its folds).
  ManholeCover: dark cast iron (it read as an orange disc).

Each: desaturate towards its own luminance, scale to a target mean (sRGB), multiply by a tint. Runs once per file: the
processed files are listed in tools/look/regrade-albedo.done, and a file already listed is skipped.

    uv run --with numpy --with pillow python -I tools/look/regrade-albedo.py
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..", "Assets", "JamKit", "Templates", "VisitingHours", "ThirdParty")
DONE = os.path.join(HERE, "regrade-albedo.done")

# name, folder, saturation kept, target mean (sRGB), tint
JOBS = [
    ("Concrete_Sidewalk", "LookLibrary", 0.35, 0.68, (1.0, 0.985, 0.95)),
    ("Concrete_Kerb", "LookLibrary", 0.35, 0.64, (1.0, 0.98, 0.95)),
    ("CoveredCar", "LookProps", 0.25, 0.56, (0.97, 0.97, 1.0)),
    ("ManholeCover", "LookProps", 0.2, 0.25, (0.95, 0.95, 1.0)),
]


def main():
    done = set()
    if os.path.exists(DONE):
        with open(DONE, encoding="utf-8") as f:
            done = {l.strip() for l in f if l.strip()}
    for name, folder, sat, target, tint in JOBS:
        path = os.path.normpath(os.path.join(ROOT, folder, name, name + "_Albedo.jpg"))
        if name in done:
            print("skip (already regraded)", name)
            continue
        img = np.asarray(Image.open(path).convert("RGB")).astype(np.float32) / 255.0
        lin = img ** 2.2
        lum = (lin * np.array([0.2126, 0.7152, 0.0722])).sum(axis=2, keepdims=True)
        lin = lum + (lin - lum) * sat
        srgb = np.clip(lin, 0, 1) ** (1 / 2.2)
        mean = srgb.mean()
        # Scale in linear so contrast is kept, to hit the target sRGB mean.
        k = (target / max(mean, 1e-4)) ** 2.2
        out = np.clip(np.clip(lin * k, 0, 1) ** (1 / 2.2) * np.array(tint), 0, 1)
        Image.fromarray((out * 255 + 0.5).astype(np.uint8)).save(path, quality=92)
        print("%-18s mean %.2f -> %.2f" % (name, img.mean(), out.mean()))
        done.add(name)
    with open(DONE, "w", encoding="utf-8") as f:
        f.write("\n".join(sorted(done)) + "\n")


if __name__ == "__main__":
    main()
