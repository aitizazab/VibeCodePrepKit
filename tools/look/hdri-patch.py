"""Paint a small blemish out of a prepared sky HDRI (the Look agent, Visiting Hours).

Each listed rectangle is refilled row by row with a straight blend between the columns just outside it (in log space,
so bright and dim edges meet evenly), then feathered into the original over a few pixels. Used once for
Sky_Kloppenheim06_4k.hdr: a dark tree-like stub sat on its horizon, left of the sun (the judge's "grey squiggle",
2026-10-12, round 1).

    uv run --with numpy python -I tools/look/hdri-patch.py <file.hdr> x0 y0 x1 y1 [x0 y0 x1 y1 ...]

Coordinates are pixels in the equirect (x right, y down). The file is rewritten in place.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from hdrlib import read_hdr, write_hdr  # noqa: E402


def patch(img, x0, y0, x1, y1, feather=4):
    h, w, _ = img.shape
    out = img.copy()
    left = np.log(np.maximum(img[y0:y1, max(x0 - 3, 0):x0].mean(axis=1), 1e-6))
    right = np.log(np.maximum(img[y0:y1, x1:min(x1 + 3, w)].mean(axis=1), 1e-6))
    t = (np.arange(x0, x1) - x0 + 0.5) / max(x1 - x0, 1)
    fill = np.exp(left[:, None, :] * (1.0 - t[None, :, None]) + right[:, None, :] * t[None, :, None])
    # Feather: full replacement inside, fading to the original over `feather` pixels at the top and bottom edges.
    rows = np.arange(y0, y1)
    wy = np.clip(np.minimum(rows - y0 + 1, y1 - rows) / float(feather), 0.0, 1.0)
    out[y0:y1, x0:x1] = img[y0:y1, x0:x1] * (1.0 - wy[:, None, None]) + fill * wy[:, None, None]
    return out


def main():
    if len(sys.argv) < 6 or (len(sys.argv) - 2) % 4 != 0:
        print(__doc__)
        sys.exit(1)
    path = sys.argv[1]
    img = read_hdr(path)
    vals = list(map(int, sys.argv[2:]))
    for i in range(0, len(vals), 4):
        x0, y0, x1, y1 = vals[i:i + 4]
        img = patch(img, x0, y0, x1, y1)
        print("patched", x0, y0, x1, y1)
    write_hdr(path, img)
    print("wrote", path)


if __name__ == "__main__":
    main()
