# Dream cut-outs: a figure drawn on plain paper -> a transparent PNG with a white paper border, like a sticker cut out of
# a sketchbook (the dream's billboards). Light work (no rembg): the paper is flood-filled from the image's edges.
#   uv run --with pillow --with numpy --with scipy python tools/art/vh_dream_cutout.py <src.jpg> <out name> [x0 x1]
#   (x0 x1: optional fractions of the width to keep, to drop anything drifting at the sides)
# Writes Assets/JamKit/Templates/VisitingHours/Art/Dream/<out name>.png (the name DreamArt looks for), at most 1024 tall.
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

VJ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
OUT = os.path.join(VJ, "Assets", "JamKit", "Templates", "VisitingHours", "Art", "Dream")


def cutout(src, name, x0=0.0, x1=1.0, tol=34, border_frac=0.012):
    im = Image.open(src if os.path.isabs(src) else os.path.join(INC, src)).convert("RGB")
    W, H = im.size; im = im.crop((int(x0 * W), 0, int(x1 * W), H))
    a = np.asarray(im, dtype=np.int16)
    edge = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
    paper = np.median(edge, axis=0)
    near = np.abs(a - paper).max(-1) <= tol                       # paper-coloured pixels
    lab, _ = ndimage.label(near)
    border_labels = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
    bg = np.isin(lab, border_labels[border_labels > 0])           # paper reachable from the edges
    fig = ~bg
    lab2, n = ndimage.label(fig)
    if n > 1:                                                     # keep the figure, drop specks
        sizes = ndimage.sum(fig, lab2, range(1, n + 1))
        fig = np.isin(lab2, 1 + np.nonzero(sizes >= sizes.max() * 0.02)[0])
    fig = ndimage.binary_fill_holes(fig)
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
    a = sys.argv[1:]
    cutout(a[0], a[1], *(float(v) for v in a[2:4]))


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
