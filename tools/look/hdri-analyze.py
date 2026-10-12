"""Analyse candidate sky HDRIs: the sun's direction and strength, the sky's colours, and a warm-graded preview strip
(towards the sun, 90 degrees off, away from the sun) so a candidate can be judged as a game sky, not a thumbnail.

    uv run --with numpy --with pillow python -I tools/look/hdri-analyze.py <out-dir> <file.hdr> [...]
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hdrlib  # noqa: E402


def aces(x):
    a, b, c, d, e = 2.51, 0.03, 2.43, 0.59, 0.14
    return np.clip((x * (a * x + b)) / (x * (c * x + d) + e), 0, 1)


def view(rgb, yaw_deg, pitch_deg=8.0, fov=70.0, w=640, h=360):
    H, W, _ = rgb.shape
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    t = np.tan(np.radians(fov) / 2)
    vx = (xs / w * 2 - 1) * t * w / h
    vy = (1 - ys / h * 2) * t
    d = np.stack([vx, vy, np.ones_like(vx)], -1)
    d /= np.linalg.norm(d, axis=-1, keepdims=True)
    p = np.radians(pitch_deg)
    y = d[..., 1] * np.cos(p) + d[..., 2] * np.sin(p)
    z = -d[..., 1] * np.sin(p) + d[..., 2] * np.cos(p)
    x = d[..., 0]
    a = np.radians(yaw_deg)
    x, z = x * np.cos(a) + z * np.sin(a), -x * np.sin(a) + z * np.cos(a)
    az = np.arctan2(x, z)
    el = np.arcsin(np.clip(y, -1, 1))
    u = (az / (2 * np.pi) + 0.5) % 1.0
    v = 0.5 - el / np.pi
    px = np.clip((u * W).astype(int), 0, W - 1)
    py = np.clip((v * H).astype(int), 0, H - 1)
    return rgb[py, px]


def analyse(path):
    rgb = hdrlib.read_hdr(path)
    H, W, _ = rgb.shape
    lum = hdrlib.luminance(rgb)
    iy, ix = np.unravel_index(np.argmax(lum), lum.shape)
    peak = float(lum[iy, ix])
    # Sun centroid: pixels within 50% of the peak.
    m = lum > peak * 0.5
    ys, xs = np.nonzero(m)
    cy, cx = ys.mean(), xs.mean()
    u, v = (cx + 0.5) / W, (cy + 0.5) / H
    el = (0.5 - v) * 180.0
    az = (u - 0.5) * 360.0
    # Sky statistics (upper hemisphere, solid-angle weighted), with the sun disc clamped out.
    vv = (np.arange(H) + 0.5) / H
    elev = (0.5 - vv) * np.pi
    wgt = np.cos(elev)[:, None] * np.ones((1, W))
    upper = elev[:, None] > 0
    clamped = np.minimum(rgb, 16.0)
    def avg(mask):
        ww = (wgt * mask)[..., None]
        return (clamped * ww).sum((0, 1)) / ww.sum()
    sky = avg(upper)
    horizon = avg((elev[:, None] > 0) & (elev[:, None] < np.radians(8)))
    zenith = avg(elev[:, None] > np.radians(60))
    # The sun's energy above the clamp (what the directional light should carry).
    excess = np.maximum(rgb - 16.0, 0) * wgt[..., None] * (2 * np.pi / W) * (np.pi / H)
    sun_irr = excess.sum((0, 1))
    return rgb, dict(file=os.path.basename(path), peak=peak, sun_el=el, sun_az=az, sky=sky.tolist(),
                     horizon=horizon.tolist(), zenith=zenith.tolist(), sun_irradiance=sun_irr.tolist())


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    rows = []
    for p in sys.argv[2:]:
        rgb, info = analyse(p)
        print(json.dumps(info))
        sky = np.array(info["sky"])
        expo = 0.55 / max(hdrlib.luminance(sky), 1e-4)
        warm = np.array([1.08, 1.0, 0.88])
        strip = [view(rgb, info["sun_az"] + d, pitch_deg=10) for d in (0, 70, 180)]
        strip = np.concatenate(strip, axis=1) * expo * warm
        img = (aces(strip) ** (1 / 2.2) * 255).astype(np.uint8)
        rows.append(img)
        Image.fromarray(img).save(os.path.join(out, os.path.basename(p)[:-4] + ".jpg"), quality=85)
    Image.fromarray(np.concatenate(rows, axis=0)).save(os.path.join(out, "strips.jpg"), quality=80)


if __name__ == "__main__":
    main()
