"""The People pipeline's numpy side: works on a mesh dump from vh_blender.py (positions, triangles, UVs, normals, bones).

    uv run --with pillow --with numpy python tools/tripo/vh_tex.py front <dump.npz> <Color.jpg> <out.png> [x0 z0 x1 z1] [px]
        an orthographic front view (the character faces -Y), textured, flat-lit: the face's landmark sheet
    uv run --with pillow --with numpy python tools/tripo/vh_tex.py side <dump.npz> <Color.jpg> <out.png> [y0 z0 y1 z1] [px]

Library: raster_uv (UV space → triangle, barycentrics, 3D position, normal per texel) for texture edits driven by
3D regions (vh_alter.py), render_ortho for the front views and the face decal (vh_face.py).
"""
import json
import sys

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def load(npz):
    d = np.load(npz)
    return {"verts": d["verts"], "tris": d["tris"], "tri_uv": d["tri_uv"], "normals": d["normals"],
            "bones": json.loads(str(d["bones"]))}


def raster(points2d, res_w, res_h, tris, depth=None, front_smaller=True):
    """Rasterises triangles given per-triangle-corner pixel coordinates (T,3,2). Returns tri id (-1 = none) and
    barycentrics per pixel. With depth (T,3), keeps the nearest (smaller if front_smaller)."""
    tid = np.full((res_h, res_w), -1, dtype=np.int32)
    bary = np.zeros((res_h, res_w, 3), dtype=np.float32)
    zbuf = np.full((res_h, res_w), np.inf, dtype=np.float32) if depth is not None else None
    P = points2d
    x0 = np.clip(np.floor(P[:, :, 0].min(1)).astype(int), 0, res_w - 1)
    x1 = np.clip(np.ceil(P[:, :, 0].max(1)).astype(int), 0, res_w - 1)
    y0 = np.clip(np.floor(P[:, :, 1].min(1)).astype(int), 0, res_h - 1)
    y1 = np.clip(np.ceil(P[:, :, 1].max(1)).astype(int), 0, res_h - 1)
    for t in range(len(P)):
        if x1[t] < x0[t] or y1[t] < y0[t]:
            continue
        a, b, c = P[t]
        ys, xs = np.mgrid[y0[t]:y1[t] + 1, x0[t]:x1[t] + 1]
        px = xs + 0.5
        py = ys + 0.5
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        w0 = ((b[1] - c[1]) * (px - c[0]) + (c[0] - b[0]) * (py - c[1])) / den
        w1 = ((c[1] - a[1]) * (px - c[0]) + (a[0] - c[0]) * (py - c[1])) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -1e-4) & (w1 >= -1e-4) & (w2 >= -1e-4)
        if not inside.any():
            continue
        if depth is not None:
            z = w0 * depth[t, 0] + w1 * depth[t, 1] + w2 * depth[t, 2]
            if not front_smaller:
                z = -z
            sub = zbuf[ys, xs]
            inside &= z < sub
            if not inside.any():
                continue
            zbuf[ys[inside], xs[inside]] = z[inside]
        tid[ys[inside], xs[inside]] = t
        bary[ys[inside], xs[inside]] = np.stack([w0, w1, w2], -1)[inside]
    return tid, bary


def raster_uv(m, res):
    """UV space at res x res (row 0 = the top of the image = v 1): per texel the triangle, its 3D position and normal."""
    uv = m["tri_uv"]
    pts = np.stack([uv[:, :, 0] * res, (1 - uv[:, :, 1]) * res], -1)
    tid, bary = raster(pts, res, res, m["tris"])
    pos = np.zeros((res, res, 3), dtype=np.float32)
    nrm = np.zeros((res, res, 3), dtype=np.float32)
    ok = tid >= 0
    tv = m["tris"][tid[ok]]
    b = bary[ok]
    pos[ok] = (m["verts"][tv] * b[:, :, None]).sum(1)
    n = (m["normals"][tv] * b[:, :, None]).sum(1)
    nrm[ok] = n / np.maximum(1e-8, np.linalg.norm(n, axis=1, keepdims=True))
    return ok, pos, nrm, tid


def sample(tex, uv):
    """Bilinear sample of an HxWxC float array at UVs (N,2)."""
    h, w = tex.shape[:2]
    x = np.clip(uv[:, 0] * w - 0.5, 0, w - 1.001)
    y = np.clip((1 - uv[:, 1]) * h - 0.5, 0, h - 1.001)
    x0 = x.astype(int)
    y0 = y.astype(int)
    fx = (x - x0)[:, None]
    fy = (y - y0)[:, None]
    c = (tex[y0, x0] * (1 - fx) * (1 - fy) + tex[y0, x0 + 1] * fx * (1 - fy) + tex[y0 + 1, x0] * (1 - fx) * fy
         + tex[y0 + 1, x0 + 1] * fx * fy)
    return c


def render_ortho(m, tex, rect, px, view="front"):
    """Orthographic textured render. front: looking along +Y (the character faces -Y), x right, z up;
    side: looking along -X from +X (her left side... the character's right is -X), y right, z up.
    rect = (a0, z0, a1, z1) in metres; px = output height in pixels. Returns RGBA float image and the tri/bary maps."""
    a0, z0, a1, z1 = rect
    h = px
    w = int(round(px * (a1 - a0) / (z1 - z0)))
    V = m["verts"]
    T = m["tris"]
    if view == "front":
        ax, dz, front_smaller = 0, 1, True
    else:
        ax, dz, front_smaller = 1, 0, False
    P = V[T]
    pts = np.stack([(P[:, :, ax] - a0) / (a1 - a0) * w, (1 - (P[:, :, 2] - z0) / (z1 - z0)) * h], -1)
    keep = (pts[:, :, 0].max(1) >= 0) & (pts[:, :, 0].min(1) < w) & (pts[:, :, 1].max(1) >= 0) & (pts[:, :, 1].min(1) < h)
    idx = np.nonzero(keep)[0]
    tid, bary = raster(pts[idx], w, h, T[idx], depth=P[idx][:, :, dz], front_smaller=front_smaller)
    img = np.zeros((h, w, 4), dtype=np.float32)
    ok = tid >= 0
    t = idx[tid[ok]]
    b = bary[ok]
    uv = (m["tri_uv"][t] * b[:, :, None]).sum(1)
    col = sample(tex, uv)
    n = (m["normals"][T[t]] * b[:, :, None]).sum(1)
    n /= np.maximum(1e-8, np.linalg.norm(n, axis=1, keepdims=True))
    light = np.array([0.35, -0.8, 0.45] if view == "front" else [0.8, 0.35, 0.45])
    light /= np.linalg.norm(light)
    shade = 0.72 + 0.28 * np.clip((n * light).sum(1), 0, 1)
    img[ok, :3] = col[:, :3] * shade[:, None]
    img[ok, 3] = 1
    full_t = np.full(tid.shape, -1, dtype=np.int32)
    full_t[ok] = t
    return img, full_t, bary


def load_tex(path, size=None):
    im = Image.open(path).convert("RGB")
    if size:
        im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def save(img, path):
    a = np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)
    Image.fromarray(a, "RGBA" if a.shape[2] == 4 else "RGB").save(path)


def bounds(m):
    V = m["verts"]
    return V.min(0), V.max(0)


if __name__ == "__main__":
    mode, npz, texp, out = sys.argv[1:5]
    m = load(npz)
    tex = load_tex(texp, 2048)
    lo, hi = bounds(m)
    if mode in ("front", "side"):
        if len(sys.argv) >= 9:
            rect = tuple(float(a) for a in sys.argv[5:9])
        else:
            pad = 0.02
            rect = (lo[0] - pad, lo[2] - pad, hi[0] + pad, hi[2] + pad) if mode == "front" else (lo[1] - pad, lo[2] - pad, hi[1] + pad, hi[2] + pad)
        px = int(sys.argv[9]) if len(sys.argv) >= 10 else 1024
        img, _, _ = render_ortho(m, tex, rect, px, mode)
        bg = np.ones_like(img)
        bg[..., :3] = 0.35
        comp = img[..., :3] * img[..., 3:] + bg[..., :3] * (1 - img[..., 3:])
        save(comp, out)
        print("rect", rect, "bounds", lo.round(3).tolist(), hi.round(3).tolist())
        print("bones", {k: [round(x, 3) for x in v] for k, v in m["bones"].items() if k in ("Head", "Neck", "HeadTop_End", "Hips", "LeftEye", "RightEye")})
