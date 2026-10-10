# The opening and title scenes' images and textures: chosen takes -> Assets/JamKit/Templates/VisitingHours/Art/Opening/<name>.png
# (light work: crops, resizes and seamless tiles; no background removal).
#   uv run --with pillow --with numpy python tools/art/vh_opening_process.py [--sheet]
# Picks: docs/art/vh-opening-picks.json, under "outputs": {name: {src, mode, crop?, period?}}. Modes:
#   tile (1024 seamless; "period" [px, py] when Flow painted it already repeating), full (1920x1080),
#   square (centre square, 1024), tall (centre 9:16 crop, for the phone), crop (just the "crop" box, longest side <= 2048).
import json, os, sys, glob
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vh_process import tile, fit, VJ, INC   # shared helpers (seamless tiling)

OUT = os.path.join(VJ, "Assets", "JamKit", "Templates", "VisitingHours", "Art", "Opening")
PICKS = json.load(open(os.path.join(VJ, "docs", "art", "vh-opening-picks.json"), encoding="utf8"))["outputs"]


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, name + ".png"); tmp = dst + ".tmp"
    img.save(tmp, format="PNG", optimize=True); os.replace(tmp, dst); print(f"  {name}.png  {img.size[0]}x{img.size[1]}")


def weave(colour, size=1024, cell=8, seed=7):
    # A procedural plain-weave cloth (tileable by construction): a stand-in where Flow refused the prompt.
    rng = np.random.default_rng(seed); y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    over = ((x // cell + y // cell) % 2).astype(np.float32)                         # which thread is on top
    along = np.where(over > 0, np.sin(np.pi * (x % cell) / cell), np.sin(np.pi * (y % cell) / cell))
    shade = 0.92 + 0.08 * along                                                      # each thread rounded
    shade *= 1 + 0.025 * np.repeat(np.repeat(rng.standard_normal((size // cell, size // cell)), cell, 0), cell, 1)
    a = np.clip(np.array(colour, np.float32)[None, None, :] * shade[..., None], 0, 255).astype(np.uint8)
    return Image.fromarray(a)


for name, p in PICKS.items():
    if p["mode"] == "weave": save(weave(p["colour"]), name); continue
    im = Image.open(os.path.join(INC, p["src"])).convert("RGB")
    if "crop" in p:
        x0, y0, x1, y1 = p["crop"]; im = im.crop((int(x0 * im.width), int(y0 * im.height), int(x1 * im.width), int(y1 * im.height)))
    m = p["mode"]
    if m == "tile": out = tile(im, period=p.get("period"))
    elif m == "full": out = im.resize((1920, 1080), Image.LANCZOS)
    elif m == "square":
        s = min(im.size); out = im.crop(((im.width - s) // 2, (im.height - s) // 2, (im.width + s) // 2, (im.height + s) // 2)).resize((1024, 1024), Image.LANCZOS)
    elif m == "tall":
        w = im.height * 9 // 16; c = p.get("centre", 0.5); x0 = max(0, min(im.width - w, int(im.width * c - w / 2)))
        out = im.crop((x0, 0, x0 + w, im.height))
    elif m == "crop": out = fit(im, 2048, 2048)
    else: raise SystemExit(f"unknown mode {m}")
    save(out, name)

if "--sheet" in sys.argv:   # a quick look: every output, textures tiled 2x2
    files = sorted(glob.glob(os.path.join(OUT, "*.png"))); cw, ch = 360, 300; cols = 5; rows = (len(files) + cols - 1) // cols
    S = Image.new("RGB", (cols * cw, rows * ch), (30, 30, 30)); d = ImageDraw.Draw(S)
    for i, f in enumerate(files):
        im = Image.open(f).convert("RGB"); x, y = (i % cols) * cw, (i // cols) * ch
        if os.path.basename(f).startswith("tex_"):
            t = im.resize((130, 130)); im = Image.new("RGB", (260, 260))
            for a in (0, 130):
                for b in (0, 130): im.paste(t, (a, b))
        im.thumbnail((cw - 10, ch - 30)); S.paste(im, (x + 5, y + 5)); d.text((x + 6, y + ch - 22), os.path.basename(f)[:-4], fill=(255, 255, 0))
    dst = os.path.join(VJ, "docs", "art", "vh-opening-sheet.jpg"); S.save(dst, quality=88); print("sheet:", dst)
