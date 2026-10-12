# Dream cut-outs: a figure drawn on plain paper -> a transparent PNG with a white paper border, like a sticker cut out of
# a sketchbook (the dream's billboards). Light work (no rembg): the paper is flood-filled from the image's edges.
#   uv run --with pillow --with numpy --with scipy python tools/art/vh_dream_cutout.py <src.jpg> <out name> [x0 x1]
#   (x0 x1: optional fractions of the width to keep, to drop anything drifting at the sides)
#   [--close 8] close gaps in the outline (white hair on white paper); [--hole-max 0.01] keep big enclosed paper pockets open
# Writes Assets/JamKit/Templates/VisitingHours/Art/Dream/<out name>.png (the name DreamArt looks for), at most 1024 tall.
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

VJ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
OUT = os.path.join(VJ, "Assets", "JamKit", "Templates", "VisitingHours", "Art", "Dream")


def cutout(src, name, x0=0.0, x1=1.0, tol=34, border_frac=0.012, close=0, hole_max=None):
    """close: radius (px) to close gaps in the outline before the flood fill, so white parts drawn on the paper (white
    hair) aren't flooded away. hole_max: paper-coloured pockets enclosed by the figure (a hose looped back to the legs)
    bigger than this fraction of the figure stay transparent instead of being filled with paper. Both off by default."""
    im = Image.open(src if os.path.isabs(src) else os.path.join(INC, src)).convert("RGB")
    W, H = im.size; im = im.crop((int(x0 * W), 0, int(x1 * W), H))
    a = np.asarray(im, dtype=np.int16)
    edge = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
    paper = np.median(edge, axis=0)
    near0 = near = np.abs(a - paper).max(-1) <= tol               # paper-coloured pixels
    if close:
        yy, xx = np.ogrid[-close:close + 1, -close:close + 1]
        near = ~ndimage.binary_closing(~near, structure=(xx * xx + yy * yy) <= close * close)
    lab, _ = ndimage.label(near)
    border_labels = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
    bg = np.isin(lab, border_labels[border_labels > 0])           # paper reachable from the edges
    fig = ~bg
    lab2, n = ndimage.label(fig)
    if n > 1:                                                     # keep the figure, drop specks
        sizes = ndimage.sum(fig, lab2, range(1, n + 1))
        fig = np.isin(lab2, 1 + np.nonzero(sizes >= sizes.max() * 0.02)[0])
    filled = ndimage.binary_fill_holes(fig)
    if hole_max is not None:
        hl, hn = ndimage.label(filled & near & ~bg)                # paper the figure encloses
        if hn:
            idx = range(1, hn + 1)
            area = ndimage.sum(np.ones(fig.shape), hl, idx); paperish = ndimage.mean(near0, hl, idx)
            keep_open = 1 + np.nonzero((area > hole_max * filled.sum()) & (paperish > 0.8))[0]
            filled &= ~np.isin(hl, keep_open)
    fig = filled
    r = max(3, int(border_frac * a.shape[0]))
    yy, xx = np.ogrid[-r:r + 1, -r:r + 1]
    sticker = ndimage.binary_dilation(fig, structure=(xx * xx + yy * yy) <= r * r)
    rgb = np.where(fig[..., None], a, np.array([255, 253, 247]))  # the white paper border around the figure
    alpha = ndimage.gaussian_filter(sticker.astype(np.float32), 1.0)
    out = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255, 0, 255)]).astype(np.uint8)
    ys, xs = np.nonzero(sticker)
    out = out[max(ys.min() - 4, 0):ys.max() + 5, max(xs.min() - 4, 0):xs.max() + 5]
    img = Image.fromarray(out, "RGBA")
    if img.height > 1024: img = img.resize((round(img.width * 1024 / img.height), 1024), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, name + ".png"); img.save(dst + ".tmp", format="PNG", optimize=True); os.replace(dst + ".tmp", dst)
    print(name, img.size)


if __name__ == "__main__":
    a = sys.argv[1:]; kw = {}
    for flag, key, typ in (("--close", "close", int), ("--hole-max", "hole_max", float)):
        if flag in a: i = a.index(flag); kw[key] = typ(a[i + 1]); del a[i:i + 2]
    cutout(a[0], a[1], *(float(v) for v in a[2:4]), **kw)


def pattern(src, name, contrast=0.22, size=512):
    """A tileable texture -> a grey pattern with mean brightness 0.5 for the Pastel shader's _MainTex (it varies the
    pastel colour's brightness, so the dream palette stays). `src` must already tile (the opening's textures do)."""
    im = Image.open(src).convert("L").resize((size, size), Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    a = 0.5 + (a - a.mean()) / max(a.std(), 1e-3) * contrast
    img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "L")
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, name + ".png"); img.save(dst + ".tmp", format="PNG", optimize=True); os.replace(dst + ".tmp", dst)
    print(name, img.size, "mean 0.5")


def _save_rgb(img, name):
    os.makedirs(OUT, exist_ok=True)
    if max(img.size) > 1024: img.thumbnail((1024, 1024), Image.LANCZOS)
    dst = os.path.join(OUT, name + ".png"); img.save(dst + ".tmp", format="PNG", optimize=True); os.replace(dst + ".tmp", dst)
    print(name, img.size)


def _centre_crop(img, aspect):
    w, h = img.size
    if w / h > aspect: nw = int(round(h * aspect)); return img.crop(((w - nw) // 2, 0, (w - nw) // 2 + nw, h))
    nh = int(round(w / aspect)); return img.crop((0, (h - nh) // 2, w, (h - nh) // 2 + nh))


def photo_grid(src, prefix, aspect, count=6, tol=34, inset=0.0):
    """A sheet of separate pictures on plain paper (a grid) -> one file per picture, reading order (rows top to bottom,
    left to right): <prefix>1 ... <prefix>N, each centre-cropped to `aspect` (width / height)."""
    im = Image.open(os.path.join(INC, src)).convert("RGB"); a = np.asarray(im, dtype=np.int16)
    edge = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
    fig = np.abs(a - np.median(edge, axis=0)).max(-1) > tol
    fig = ndimage.binary_closing(fig, iterations=4)
    lab, n = ndimage.label(fig)
    boxes = sorted(ndimage.find_objects(lab), key=lambda s: (s[0].stop - s[0].start) * (s[1].stop - s[1].start), reverse=True)[:count]
    if len(boxes) < count: raise SystemExit(f"found {len(boxes)} pictures, not {count}")
    rows = sorted(boxes, key=lambda s: s[0].start)
    rowh = np.median([s[0].stop - s[0].start for s in rows])
    ordered = sorted(boxes, key=lambda s: (round(s[0].start / rowh), s[1].start))
    for i, s in enumerate(ordered, 1):
        y0, y1, x0, x1 = s[0].start, s[0].stop, s[1].start, s[1].stop
        dx, dy = int((x1 - x0) * inset), int((y1 - y0) * inset)
        _save_rgb(_centre_crop(im.crop((x0 + dx, y0 + dy, x1 - dx, y1 - dy)), aspect), f"{prefix}{i}")


def picture(src, name, aspect, box=None, erase=()):
    """One picture: optional fractional `box` (x0, y0, x1, y1) to crop to first, `erase` rectangles (fractions of the
    source) painted over with the paper colour (stray lettering), then centre-cropped to `aspect`."""
    im = Image.open(os.path.join(INC, src)).convert("RGB"); W, H = im.size
    if erase:
        a = np.asarray(im).copy(); paper = np.median(np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3)]), axis=0)
        for x0, y0, x1, y1 in erase: a[int(y0 * H):int(y1 * H), int(x0 * W):int(x1 * W)] = paper
        im = Image.fromarray(a)
    if box: im = im.crop((int(box[0] * W), int(box[1] * H), int(box[2] * W), int(box[3] * H)))
    _save_rgb(_centre_crop(im, aspect), name)
