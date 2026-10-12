"""A character's talking face, numpy side (docs/redesign/people.md "Faces"):

    uv run --with pillow --with numpy python tools/tripo/vh_face.py <face.json>

face.json (landmarks read off vh_headgrid.py's close-up, in millimetres of world x / z; the character faces -Y):
  {"npz": dump, "tex": Color.jpg, "atlas": out .png, "block": out .json, "check": out .png,
   "rect": [x0, z0, x1, z1],            the decal's rectangle (front projection), mm
   "eyes": [[x, z], [x, z]],            rough eye centres (the eye is found around each)
   "eyebox": [w, h],                    the search box for an eye, mm
   "brows": [[x, z], [x, z]], "mouth": [x, z], "mouthL": [x, z], "mouthR": [x, z],
   "lips": [half_width_mm, depth_mm],   the open mouth's size in the rest pose (it stretches with MouthOpen)
   "lids": true,                        draw closed lids (false for someone whose eyes are painted shut)
   "jaw": 0.15, "smile": 0.16, ...}     shape-key strengths (fractions of the eye distance) passed to Blender
Writes the decal atlas (6 cells in a row, 512 px each: mouth closed / half / open, then the same with the eyes shut;
see VhFace), the landmark block for vh_blender.py (world metres, with each landmark's depth found on the surface), and a
check image (the face with the found eyes and the lids drawn).
"""
import json
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, __file__.replace("\\", "/").rsplit("/", 1)[0])
import vh_tex as vt

CELL = 512


def main(spec_path):
    s = json.load(open(spec_path, encoding="utf8"))
    m = vt.load(s["npz"])
    tex = vt.load_tex(s["tex"], 4096)
    x0, z0, x1, z1 = [v / 1000.0 for v in s["rect"]]
    # The rectangle is square in cells: CELL px across its height; width follows.
    img, tid, bary = vt.render_ortho(m, tex, (x0, z0, x1, z1), CELL)
    H, W = img.shape[:2]
    # Depth (world y) per pixel, from the front surface.
    ok = tid >= 0
    ypix = np.full((H, W), np.nan, dtype=np.float32)
    tv = m["tris"][tid[ok]]
    ypix[ok] = (m["verts"][tv][:, :, 1] * bary[ok]).sum(1)

    def px(p):  # mm -> pixel (col, row)
        return ((p[0] / 1000.0 - x0) / (x1 - x0) * W, (1 - (p[1] / 1000.0 - z0) / (z1 - z0)) * H)

    def world(p):  # mm (x, z) -> world metres with the surface depth
        c, r = px(p)
        c = int(np.clip(round(c), 0, W - 1))
        r = int(np.clip(round(r), 0, H - 1))
        win = ypix[max(0, r - 3):r + 4, max(0, c - 3):c + 4]
        y = float(np.nanmin(win)) if np.isfinite(win).any() else float(np.nanmin(ypix))
        return [p[0] / 1000.0, y, p[1] / 1000.0]

    rgb = img[..., :3]
    eyes = []
    for e in s["eyes"]:
        bw, bh = s.get("eyebox", [30, 18])
        ca, ra = px((e[0] - bw / 2, e[1] + bh / 2))
        cb, rb = px((e[0] + bw / 2, e[1] - bh / 2))
        ca, ra, cb, rb = int(ca), int(ra), int(cb), int(rb)
        box = rgb[ra:rb, ca:cb]
        border = np.concatenate([box[0], box[-1], box[:, 0], box[:, -1]])
        skin = np.median(border, axis=0)
        dist = np.linalg.norm(box - skin, axis=2)
        mask = dist > s.get("eye_threshold", 0.2)
        ys, xs = np.nonzero(mask)
        if len(xs) < 10:
            raise SystemExit("no eye found near %s" % e)
        # Trim stray pixels: keep the central 96% in each axis.
        xa, xb = np.percentile(xs, [2, 98])
        ya, yb = np.percentile(ys, [2, 98])
        eyes.append({"c": ((xa + xb) / 2 + ca, (ya + yb) / 2 + ra), "a": (xb - xa) / 2 + 2, "b": (yb - ya) / 2 + 2,
                     "skin": skin, "lash": np.percentile(box[mask].reshape(-1, 3), 3, axis=0)})

    atlas = Image.new("RGBA", (CELL * 6 * W // H if W != H else CELL * 6, CELL), (0, 0, 0, 0))
    cw = atlas.width // 6

    # The open mouth (cells 1, 2, 4, 5): drawn compressed into the jaw's hinge band; MouthOpen stretches it open.
    mouth_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    mc = px(s["mouth"])
    half_w = s["lips"][0] / 1000.0 / (x1 - x0) * W
    depth = s["lips"][1] / 1000.0 / (z1 - z0) * H
    d = ImageDraw.Draw(mouth_layer)
    top = mc[1] - depth * 0.08
    bot = mc[1] + depth
    lip_col = tuple(int(v * 255) for v in vt.sample(tex, np.array([[0.5, 0.5]]))[0][:3])  # replaced below
    # The lower lip's colour: the rendered pixels just under the lip line, median.
    r0 = int(mc[1] + depth * 0.4)
    lip = np.median(rgb[r0:r0 + max(2, int(depth * 0.4)), int(mc[0] - half_w * 0.4):int(mc[0] + half_w * 0.4)].reshape(-1, 3), axis=0)
    lip_col = tuple(int(v * 255) for v in np.clip(lip * 0.92, 0, 1))
    d.ellipse([mc[0] - half_w, top - depth * 0.9, mc[0] + half_w, bot], fill=lip_col + (255,))                 # lower lip rim
    d.ellipse([mc[0] - half_w * 0.9, top - depth * 0.9, mc[0] + half_w * 0.9, bot - depth * 0.28], fill=(52, 20, 22, 255))  # the mouth
    d.ellipse([mc[0] - half_w * 0.55, bot - depth * 0.62, mc[0] + half_w * 0.55, bot - depth * 0.22], fill=(118, 52, 56, 255))  # tongue
    d.rectangle([mc[0] - half_w * 0.62, top - depth, mc[0] + half_w * 0.62, top + depth * 0.2], fill=(236, 228, 214, 255))      # upper teeth
    # Cut off everything above the lip line (the upper lip stays painted), soften the edges.
    mm = np.array(mouth_layer)
    mm[: int(np.floor(top)), :, 3] = 0
    mouth_layer = Image.fromarray(mm).filter(ImageFilter.GaussianBlur(max(0.6, depth * 0.06)))

    lids_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if s.get("lids", True):
        for e in eyes:
            cx, cy = e["c"]
            a, b = e["a"] * s.get("lid_scale", [1.06, 1.12])[0], e["b"] * s.get("lid_scale", [1.06, 1.12])[1]
            # The lid's skin: the ring just above the eye, median, a shade darker toward the lash line.
            yy, xx = np.mgrid[0:H, 0:W]
            ring = (((xx - cx) / (a * 1.1)) ** 2 + ((yy - cy) / (b * 1.7)) ** 2 <= 1) & (((xx - cx) / a) ** 2 + ((yy - cy) / b) ** 2 > 1.05) & (yy < cy)
            if ring.any():
                rp = rgb[ring]
                lum = rp @ np.array([0.3, 0.59, 0.11])
                skin = rp[lum >= np.percentile(lum, 60)].mean(0)   # the lit lid, not the crease or the brow
            else:
                skin = e["skin"]
            lid = np.zeros((H, W, 4), dtype=np.float32)
            inside = ((xx - cx) / a) ** 2 + ((yy - cy) / b) ** 2
            alpha = np.clip((1.0 - inside) / 0.12, 0, 1)
            shade = 1.0 - 0.10 * np.clip((yy - (cy - b)) / (2 * b), 0, 1)
            lid[..., :3] = skin * shade[..., None]
            lid[..., 3] = alpha
            # The lash line: a curve dipping down at the middle, a little below the centre.
            t = np.clip((xx - cx) / a, -1, 1)
            line_y = cy + b * 0.25 + b * 0.32 * (1 - t ** 2)
            thick = max(1.2, b * 0.13) * (0.35 + 0.65 * (1 - t ** 2))
            lash = np.clip(1 - np.abs(yy - line_y) / thick, 0, 1) * (np.abs((xx - cx) / a) <= 1.02)
            lid[..., :3] = lid[..., :3] * (1 - lash[..., None]) + e["lash"] * lash[..., None]
            lid[..., 3] = np.maximum(lid[..., 3], lash)
            li = Image.fromarray(np.clip(lid * 255, 0, 255).astype(np.uint8), "RGBA")
            lids_layer = Image.alpha_composite(lids_layer, li)
        lids_layer = lids_layer.filter(ImageFilter.GaussianBlur(0.7))

    empty = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    cells = [empty, mouth_layer, mouth_layer, lids_layer, Image.alpha_composite(lids_layer, mouth_layer), Image.alpha_composite(lids_layer, mouth_layer)]
    atlas = Image.new("RGBA", (W * 6, H), (0, 0, 0, 0))
    for i, c in enumerate(cells):
        atlas.paste(c, (i * W, 0))
    atlas.save(s["atlas"])

    # The check image: the face, the found eyes, then the face with the lids and the open mouth over it.
    base = Image.fromarray(np.clip(img * 255, 0, 255).astype(np.uint8), "RGBA")
    chk = base.copy()
    dd = ImageDraw.Draw(chk)
    for e in eyes:
        cx, cy = e["c"]
        dd.ellipse([cx - e["a"], cy - e["b"], cx + e["a"], cy + e["b"]], outline=(0, 255, 0, 255))
    for key in ("mouth", "mouthL", "mouthR"):
        c, r = px(s[key])
        dd.ellipse([c - 3, r - 3, c + 3, r + 3], outline=(255, 0, 255, 255))
    for b in s["brows"]:
        c, r = px(b)
        dd.ellipse([c - 3, r - 3, c + 3, r + 3], outline=(0, 200, 255, 255))
    over = Image.alpha_composite(base, cells[5])
    sheet = Image.new("RGBA", (W * 2, H), (40, 40, 40, 255))
    sheet.paste(chk, (0, 0))
    sheet.paste(over, (W, 0))
    sheet.save(s["check"])

    def eye_mm(e):
        c, r = e["c"]
        return [(x0 + c / W * (x1 - x0)) * 1000.0, (z1 - r / H * (z1 - z0)) * 1000.0]
    e0, e1 = eye_mm(eyes[0]), eye_mm(eyes[1])
    L, R = (e0, e1) if e0[0] > e1[0] else (e1, e0)
    bL, bR = (s["brows"][0], s["brows"][1]) if s["brows"][0][0] > s["brows"][1][0] else (s["brows"][1], s["brows"][0])
    block = {"eyeL": world(L), "eyeR": world(R), "browL": world(bL), "browR": world(bR), "mouth": world(s["mouth"]),
             "mouthL": world(s["mouthL"]), "mouthR": world(s["mouthR"]), "decal": [x0, z0, x1, z1],
             "eyes_px": [[round(v, 1) for v in (e["c"][0], e["c"][1], e["a"], e["b"])] for e in eyes]}
    for k in ("jaw", "smile", "frown", "brow"):
        if k in s:
            block[k] = s[k]
    json.dump(block, open(s["block"], "w"), indent=1)
    print("eyes", block["eyes_px"], "atlas", atlas.size)


if __name__ == "__main__":
    main(sys.argv[1])
