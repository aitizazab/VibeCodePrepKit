"""Prepare a Poly Haven sky HDRI for Visiting Hours' golden-hour rig.

    uv run --with numpy python -I tools/look/hdri-prep.py <in.hdr> <out.hdr> <out-info.json> [clamp=12]

- Finds the sun (the centroid of the brightest pixels) and reports its elevation and azimuth in Unity terms, so the
  directional light can be aimed down the same line (VhLookSky.sunElevation / sunAzimuth).
- Clamps the sun out of the sky (every pixel to `clamp`), so that ambient light and reflections baked from the sky don't
  count the sun twice: the sun's light is the directional light's, and the sky shader draws the disc.
- Writes the sky's average, horizon and zenith colours (for fog and the ambient fallback).
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hdrlib  # noqa: E402


def main():
    src, dst, info_path = sys.argv[1:4]
    clamp = float(sys.argv[4]) if len(sys.argv) > 4 else 12.0
    rgb = hdrlib.read_hdr(src)
    H, W, _ = rgb.shape
    lum = hdrlib.luminance(rgb)
    peak = float(lum.max())
    m = lum > peak * 0.5
    ys, xs = np.nonzero(m)
    u, v = (xs.mean() + 0.5) / W, (ys.mean() + 0.5) / H
    sun_el = float((0.5 - v) * 180.0)
    sun_az = float((u - 0.5) * 360.0)
    vv = (np.arange(H) + 0.5) / H
    elev = (0.5 - vv) * np.pi
    wgt = np.cos(elev)[:, None] * np.ones((1, W))
    clamped = np.minimum(rgb, clamp)
    # Soft shoulder rather than a hard clip, so the glow round the sun stays smooth.
    over = rgb > clamp * 0.5
    soft = np.where(over, clamp * 0.5 + (clamp * 0.5) * (1 - np.exp(-(rgb - clamp * 0.5) / (clamp * 0.5))), rgb)
    clamped = soft.astype(np.float32)

    def avg(mask):
        ww = (wgt * mask)[..., None]
        return [float(x) for x in (clamped * ww).sum((0, 1)) / ww.sum()]

    e = elev[:, None]
    info = dict(source=os.path.basename(src), width=W, height=H, peak=peak, clamp=clamp,
                sun_elevation=sun_el, sun_azimuth=sun_az,
                sky_average=avg(e > 0), horizon=avg((e > 0) & (e < np.radians(6))),
                low_sky=avg((e > np.radians(6)) & (e < np.radians(25))), zenith=avg(e > np.radians(60)),
                ground=avg(e < 0),
                sun_side_horizon=avg((e > 0) & (e < np.radians(10)) & (np.abs(((np.arange(W)[None, :] + 0.5) / W - u + 0.5) % 1 - 0.5) < 0.08)))
    hdrlib.write_hdr(dst, clamped)
    with open(info_path, "w", encoding="utf-8") as f:
        json.dump(info, f, indent=2)
    print(json.dumps(info))


if __name__ == "__main__":
    main()
