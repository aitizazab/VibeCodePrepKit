"""Face sheet from a build's shape keys (vh_blender.py build → preview.npz), rendered in numpy, front orthographic:
neutral, talking half / open, a blink, then the expressions, each with the face decal's atlas cell on top.

    uv run --with pillow --with numpy python tools/tripo/vh_preview.py <preview.npz> <Color.jpg> <atlas.png|-> <out.png>
"""
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, __file__.replace("\\", "/").rsplit("/", 1)[0])
import vh_tex as vt
import vh_make

COLS = 6


def render(V, T, UV, N, tex, rect, W, H, cell=None, rows=1):
    x0, z0, x1, z1 = rect
    P = V[T]
    pts = np.stack([(P[:, :, 0] - x0) / (x1 - x0) * W, (1 - (P[:, :, 2] - z0) / (z1 - z0)) * H], -1)
    keep = (pts[:, :, 0].max(1) >= 0) & (pts[:, :, 0].min(1) < W) & (pts[:, :, 1].max(1) >= 0) & (pts[:, :, 1].min(1) < H)
    idx = np.nonzero(keep)[0]
    tid, bary = vt.raster(pts[idx], W, H, T[idx], depth=P[idx][:, :, 1], front_smaller=True)
    img = np.zeros((H, W, 4), dtype=np.float32)
    depth = np.full((H, W), np.inf, dtype=np.float32)
    ok = tid >= 0
    t = idx[tid[ok]]
    b = bary[ok]
    uv = (UV[t] * b[:, :, None]).sum(1)
    if cell is not None:
        row, col = cell if isinstance(cell, tuple) else (0, cell)
        uv = np.stack([(col + uv[:, 0]) / COLS, (rows - row - 1 + uv[:, 1]) / rows], 1)
    col = vt.sample(tex, uv)
    n = (N[T[t]] * b[:, :, None]).sum(1)
    n /= np.maximum(1e-8, np.linalg.norm(n, axis=1, keepdims=True))
    L = np.array([0.35, -0.8, 0.45])
    L /= np.linalg.norm(L)
    shade = 0.72 + 0.28 * np.clip((n * L).sum(1), 0, 1)
    img[ok, :3] = col[:, :3] * shade[:, None]
    img[ok, 3] = col[:, 3] if col.shape[1] == 4 else 1
    depth[ok] = (V[T[t]][:, :, 1] * b).sum(1)
    return img, depth


def main(npz, colp, atlasp, out):
    d = np.load(npz)
    tex = vt.load_tex(colp, 2048)
    atlas = None
    if atlasp != "-":
        atlas = np.asarray(Image.open(atlasp).convert("RGBA"), dtype=np.float32) / 255.0
    bv = d["body_verts"]
    keys = {k[len("body_key_"):]: d[k] for k in d.files if k.startswith("body_key_")}
    has_dec = "decal_verts" in d.files
    if has_dec:
        dv = d["decal_verts"]
        dkeys = {k[len("decal_key_"):]: d[k] for k in d.files if k.startswith("decal_key_")}
        used = dv[np.unique(d["decal_tris"])]
        lo, hi = used.min(0), used.max(0)
    else:
        # No decal: frame the head from the key deltas' reach.
        moving = np.zeros(len(bv), dtype=bool)
        for k in keys.values():
            moving |= np.abs(k).max(1) > 1e-5
        lo, hi = bv[moving].min(0), bv[moving].max(0)
    cx, cz = (lo[0] + hi[0]) / 2, (lo[2] + hi[2]) / 2
    half = max(hi[0] - lo[0], hi[2] - lo[2]) * 0.62
    rect = (cx - half, cz - half, cx + half, cz + half)
    S = 360
    ex = vh_make.DEFAULT_EXPRESSIONS
    rows = len(ex)
    if atlas is not None and atlas.shape[0] / rows < 200:
        rows = 1   # an old one-row atlas
    shots = [("neutral", {}, (0, 0)), ("talk half", {"MouthOpen": 50}, (0, 1)), ("talk open", {"MouthOpen": 95}, (0, 2)), ("blink", {}, (0, 3))]
    for i, e in enumerate(ex[1:], 1):
        shots.append((e["name"], e["shapes"], (min(i, rows - 1), 0)))
    hw = dict(ex[2]["shapes"]); hw["MouthOpen"] = 95
    shots.append(("happy talking", hw, (min(2, rows - 1), 2)))
    sheet_rows = (len(shots) + 3) // 4
    sheet = Image.new("RGB", (S * 4, S * sheet_rows), (40, 40, 44))
    draw = ImageDraw.Draw(sheet)
    for i, (label, w, cell) in enumerate(shots):
        V = bv.copy()
        for k, v in w.items():
            if k in keys:
                V += keys[k] * (v / 100.0)
        img, depth = render(V, d["body_tris"], d["body_uv"], d["body_normals"], tex, rect, S, S)
        if has_dec and atlas is not None:
            DV = dv.copy()
            for k, v in w.items():
                if k in dkeys:
                    DV += dkeys[k] * (v / 100.0)
            dimg, ddepth = render(DV, d["decal_tris"], d["decal_uv"], d["decal_normals"], atlas, rect, S, S, cell=cell, rows=rows)
            a = dimg[..., 3:] * (ddepth <= depth + 0.002)[..., None]
            img[..., :3] = img[..., :3] * (1 - a) + dimg[..., :3] * a
        bg = np.full((S, S, 3), 0.25, dtype=np.float32)
        comp = img[..., :3] * img[..., 3:] + bg * (1 - img[..., 3:])
        tile = Image.fromarray(np.clip(comp * 255, 0, 255).astype(np.uint8))
        sheet.paste(tile, ((i % 4) * S, (i // 4) * S))
        draw.text(((i % 4) * S + 6, (i // 4) * S + 4), label, fill=(255, 255, 0))
    sheet.save(out)
    print("faces", out)


def rot_z(V, deg):
    a = np.radians(deg)
    c, s_ = np.cos(a), np.sin(a)
    R = np.array([[c, -s_, 0], [s_, c, 0], [0, 0, 1]], dtype=np.float32)
    return V @ R.T


def body_views(npz, colp, out, angles=(0, 35, 90, 180)):
    """Full-body turnaround (front, three-quarter, side, back) with the accessories, ortho, for judging builds."""
    d = np.load(npz)
    tex = vt.load_tex(colp, 2048)
    bv = d["body_verts"]
    lo, hi = bv.min(0), bv.max(0)
    hgt = hi[2] - lo[2]
    H = 900
    W = int(H * 0.55)
    sheet = Image.new("RGB", (W * len(angles), H), (40, 40, 44))
    for i, ang in enumerate(angles):
        V = rot_z(bv, ang)
        N = rot_z(d["body_normals"], ang)
        cx = (V[:, 0].min() + V[:, 0].max()) / 2
        rect = (cx - hgt * 0.275, lo[2] - 0.01, cx + hgt * 0.275, hi[2] + 0.01)
        img, depth = render(V, d["body_tris"], d["body_uv"], N, tex, rect, W, H)
        if "acc_verts" in d.files:
            AV = rot_z(d["acc_verts"], ang)
            T = d["acc_tris"]
            ctex = np.ones((2, len(T), 3), dtype=np.float32)
            ctex[:] = d["acc_cols"][None]
            uvs = np.zeros((len(T), 3, 2), dtype=np.float32)
            uvs[:, :, 0] = ((np.arange(len(T)) + 0.5) / len(T))[:, None]
            uvs[:, :, 1] = 0.5
            an = np.zeros_like(AV)
            tn = np.cross(AV[T[:, 1]] - AV[T[:, 0]], AV[T[:, 2]] - AV[T[:, 0]])
            for k in range(3):
                np.add.at(an, T[:, k], tn)
            an /= np.maximum(1e-9, np.linalg.norm(an, axis=1, keepdims=True))
            aimg, adepth = render(AV, T, uvs, an, ctex, rect, W, H)
            m = (aimg[..., 3] > 0) & (adepth < depth)
            img[m] = aimg[m]
        bg = np.full((H, W, 3), 0.3, dtype=np.float32)
        comp = img[..., :3] * img[..., 3:] + bg * (1 - img[..., 3:])
        sheet.paste(Image.fromarray(np.clip(comp * 255, 0, 255).astype(np.uint8)), (i * W, 0))
    sheet.save(out)
    print("body", out)


if __name__ == "__main__":
    if sys.argv[1] == "body":
        body_views(*sys.argv[2:5])
    else:
        main(*sys.argv[1:5])
