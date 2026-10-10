# Turns the chosen Flow takes into the Visiting Hours slice art: Assets/JamKit/Templates/VisitingHours/Art/<name>.png
#
#   uv run --with pillow --with numpy python tools/art/vh_process.py --light      # tiles, skies, backgrounds, chroma key (quick)
#   uv run --with "rembg[cpu]" --with pillow --with numpy python tools/art/vh_process.py --cutouts   # rembg (HEAVY: run capped)
#   ... --only name1,name2    process just those outputs
#   ... --sheet               (with --cutouts or alone) write docs/art/vh-final-sheet.jpg: every output over a checkerboard
#
# The picks (which take becomes which file, and how it's processed) are in docs/art/vh-picks.json.
# Safe to re-run: each output is written to <name>.png.tmp and renamed over the old file only when it's finished.
import json, os, sys, glob
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
VJ = os.path.normpath(os.path.join(HERE, "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
OUT = os.path.join(VJ, "Assets", "JamKit", "Templates", "VisitingHours", "Art")
PICKS = json.load(open(os.path.join(VJ, "docs", "art", "vh-picks.json"), encoding="utf8"))
CUT_MODES = {"portrait", "billboard", "prop", "flat"}
args = sys.argv[1:]
only = set(args[args.index("--only") + 1].split(",")) if "--only" in args else None


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, name + ".png"); tmp = dst + ".tmp"
    img.save(tmp, format="PNG", optimize=True); os.replace(tmp, dst)
    print(f"  {name}.png  {img.size[0]}x{img.size[1]} {img.mode}")


def fit(img, max_w, max_h):
    s = min(max_w / img.width, max_h / img.height, 1.0)
    return img if s >= 1.0 else img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)


# -- cutouts --
def bg_colour(rgb):   # median of the four corner patches (the plain grey background)
    a = np.asarray(rgb, dtype=np.float32); k = 24
    return np.median(np.concatenate([a[:k, :k].reshape(-1, 3), a[:k, -k:].reshape(-1, 3), a[-k:, :k].reshape(-1, 3), a[-k:, -k:].reshape(-1, 3)]), axis=0)


def cutout(rgb, session):
    from rembg import remove
    mask = remove(rgb, session=session, only_mask=True, post_process_mask=False)
    a = np.asarray(mask, dtype=np.float32) / 255.0
    a = np.clip((a - 0.04) / 0.92, 0, 1)            # clean faint haze, keep soft edges
    c = np.asarray(rgb, dtype=np.float32); bg = bg_colour(rgb)
    # colour decontamination on the soft edge: remove the grey background that's mixed into it
    aa = np.maximum(a, 1e-3)[..., None]
    dec = np.clip((c - (1 - aa) * bg) / aa, 0, 255)
    edge = ((a > 0) & (a < 0.98))[..., None]
    c = np.where(edge, dec, c)
    out = np.dstack([c, a * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def trim(img, pad=(16, 16, 16, 16)):   # pad = left, top, right, bottom
    a = np.asarray(img.split()[-1]) > 40           # ignore faint haze and lone specks: a row/column needs a few solid pixels
    cols = np.nonzero(a.sum(0) >= 3)[0]; rows = np.nonzero(a.sum(1) >= 3)[0]
    x0, x1, y0, y1 = cols.min(), cols.max() + 1, rows.min(), rows.max() + 1
    l, t, r, b = pad
    box = (max(0, x0 - l), max(0, y0 - t), min(img.width, x1 + r), min(img.height, y1 + b))
    return img.crop(box)


# -- tiles and panoramas --
def flatten_light(a, strength=0.85):   # remove the large-scale light falloff so a tile doesn't show its repeat
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    blur = np.asarray(img.filter(ImageFilter.GaussianBlur(a.shape[0] / 6)), dtype=np.float32) + 1
    mean = a.reshape(-1, 3).mean(0)
    return a * ((mean / blur) ** strength)


def seamless_axis(a, axis, band):
    n = a.shape[axis]; b = np.roll(a, n // 2, axis=axis)
    x = np.arange(n, dtype=np.float32); d = np.minimum(x, n - 1 - x) / band   # 0 at the edges, 1 at `band` px in
    m = np.clip(d, 0, 1); m = m * m * (3 - 2 * m)                          # smoothstep
    shape = [1, 1, 1]; shape[axis] = n
    m = m.reshape(shape)
    return a * m + b * (1 - m)


def tile(rgb, size=1024, period=None):
    if period and period[1] is None:   # repeats left-right only: cut one period square, blend top-bottom
        px = period[0]; a = np.asarray(rgb.crop((0, 0, px, min(px, rgb.height))), dtype=np.float32)
        a = seamless_axis(a, 0, a.shape[0] // 5)
        return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS)
    if period:   # Flow sometimes paints an already-repeating texture: cut whole periods (near-square), no blending needed
        px, py = period; k = max(1, round(px / py))
        return rgb.crop((0, 0, px, py * k)).resize((size, size), Image.LANCZOS)
    w, h = rgb.size; s = min(w, h)
    a = np.asarray(rgb.crop(((w - s) // 2, (h - s) // 2, (w - s) // 2 + s, (h - s) // 2 + s)), dtype=np.float32)
    a = flatten_light(a)
    a = seamless_axis(a, 1, s // 5); a = seamless_axis(a, 0, s // 5)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS)


def sky(rgb):   # 2:1, wraps left-right (a cylinder around the street)
    w, h = rgb.size; hh = w // 2
    a = np.asarray(rgb.crop((0, (h - hh) // 2, w, (h - hh) // 2 + hh)), dtype=np.float32)
    a = seamless_axis(a, 1, w // 5)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).resize((2048, 1024), Image.LANCZOS)


def chroma(rgb):   # plain green background -> alpha, with despill
    a = np.asarray(rgb, dtype=np.float32); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    greenness = g - np.maximum(r, b)
    alpha = 1 - np.clip((greenness - 25) / 60, 0, 1)
    g2 = np.minimum(g, np.maximum(r, b) + 6)          # despill
    out = np.dstack([r, g2, b, alpha * 255])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


def process(name, p, session):
    src = os.path.join(INC, p["src"]); rgb = Image.open(src).convert("RGB")
    if "crop" in p:   # optional fractional crop [x0, y0, x1, y1] before anything else
        x0, y0, x1, y1 = p["crop"]; rgb = rgb.crop((int(x0 * rgb.width), int(y0 * rgb.height), int(x1 * rgb.width), int(y1 * rgb.height)))
    m = p["mode"]
    if m == "portrait": img = fit(trim(cutout(rgb, session)), 2048, 1600)
    elif m == "billboard": img = fit(trim(cutout(rgb, session), (16, 16, 16, 2)), 2048, 1600)
    elif m == "prop": img = fit(trim(cutout(rgb, session)), 1024, 1024)
    elif m == "flat": img = fit(trim(cutout(rgb, session), (8, 8, 8, 0)), 2048, 2048)
    elif m == "tile": img = tile(rgb, period=p.get("period"))
    elif m == "sky": img = sky(rgb)
    elif m == "full": img = rgb.resize((1920, 1080), Image.LANCZOS)
    elif m == "key": img = fit(trim(chroma(rgb)), 1024, 1024)
    else: raise SystemExit(f"unknown mode {m} for {name}")
    save(img, name)


def sheet():
    files = sorted(glob.glob(os.path.join(OUT, "*.png"))); n = len(files)
    cols = 6; cw, ch = 300, 260; rows = (n + cols - 1) // cols
    S = Image.new("RGB", (cols * cw, rows * ch), (30, 30, 30)); d = ImageDraw.Draw(S)
    chk = Image.new("RGB", (cw - 10, ch - 30)); cd = ImageDraw.Draw(chk)
    for y in range(0, ch, 16):
        for x in range(0, cw, 16): cd.rectangle([x, y, x + 15, y + 15], fill=(200, 200, 200) if (x // 16 + y // 16) % 2 else (150, 150, 150))
    for i, f in enumerate(files):
        x, y = (i % cols) * cw, (i // cols) * ch; im = Image.open(f).convert("RGBA"); im.thumbnail((cw - 10, ch - 30))
        S.paste(chk, (x + 5, y + 5)); S.paste(im, (x + 5 + (cw - 10 - im.width) // 2, y + 5 + (ch - 30 - im.height) // 2), im)
        d.text((x + 6, y + ch - 22), os.path.basename(f)[:-4], fill=(255, 255, 255))
    dst = os.path.join(VJ, "docs", "art", "vh-final-sheet.jpg"); S.save(dst, quality=88); print("sheet:", dst, n, "files")


if __name__ == "__main__":
    todo = {k: v for k, v in PICKS.items() if not k.startswith("_") and (only is None or k in only)}
    if "--light" in args:
        for k, v in todo.items():
            if v["mode"] not in CUT_MODES: process(k, v, None)
    if "--cutouts" in args:
        from rembg import new_session
        session = new_session("isnet-general-use")
        for k, v in todo.items():
            if v["mode"] in CUT_MODES: process(k, v, session)
    if "--sheet" in args: sheet()
