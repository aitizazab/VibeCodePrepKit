# The cast lineups for choosing Lena: one row per Lena candidate, each with the same cast, every figure scaled to the same
# standing height and labelled. Each figure keeps a margin of its own background, so paper colours can be compared.
#   uv run --with pillow --with numpy python tools/art/vh_lineup.py
#   -> docs/art/vh-cast-lineup-waking.jpg and docs/art/vh-cast-lineup-dream.jpg
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

VJ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
H = 640                     # standing height of every figure, in pixels
PAD = 14                    # own-background margin kept around each figure

def figure(file, region):
    """Crop one figure: `region` (fractions x0, x1) limits the search (a turnaround's front view; a billboard's centre),
    then the figure's bounding box is found against the image's own background colour."""
    im = Image.open(os.path.join(INC, file)).convert("RGB"); W, Hh = im.size
    x0, x1 = int(region[0] * W), int(region[1] * W); a = np.asarray(im, dtype=np.int16)[:, x0:x1]
    bg = np.median(np.concatenate([a[:20, :20].reshape(-1, 3), a[-20:, :20].reshape(-1, 3)]), axis=0)
    m = np.abs(a - bg).sum(-1) > 45
    cols = np.nonzero(m.sum(0) > 6)[0]; rows = np.nonzero(m.sum(1) > 4)[0]
    box = (x0 + max(cols.min() - PAD, 0), max(rows.min() - PAD, 0), x0 + min(cols.max() + PAD, x1 - x0 - 1), min(rows.max() + PAD, Hh - 1))
    c = im.crop(box); return c.resize((max(1, round(c.width * (H + 2 * PAD) / c.height)), H + 2 * PAD), Image.LANCZOS)

def font(size):
    for f in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f, size)
    return ImageFont.load_default()

def lineup(rows, out):
    f, fr = font(26), font(30); label_w = 300; gap = 24; lab_h = 44
    built = [(title, [(name, figure(file, reg)) for name, file, reg in cast]) for title, cast in rows]
    width = label_w + max(sum(im.width + gap for _, im in figs) for _, figs in built) + gap
    row_h = H + 2 * PAD + lab_h + 30
    S = Image.new("RGB", (width, row_h * len(built) + 20), (120, 120, 126)); d = ImageDraw.Draw(S)
    for r, (title, figs) in enumerate(built):
        y = 20 + r * row_h; d.rectangle([0, y - 10, width, y + row_h - 22], fill=(132, 132, 138) if r % 2 else (120, 120, 126))
        d.multiline_text((16, y + H // 2 - 20), title, font=fr, fill=(255, 235, 150))
        x = label_w
        for name, im in figs:
            S.paste(im, (x, y)); tw = d.textlength(name, font=f)
            d.text((x + (im.width - tw) / 2, y + H + 2 * PAD + 6), name, font=f, fill=(255, 255, 255)); x += im.width + gap
    S.save(out, quality=88); print(out, S.size)

FRONT = (0.0, 0.37)          # a turnaround's front view (the left third)
CENTRE = (0.25, 0.75)        # a billboard's figure (ignores anything drifting at the sides)
waking_cast = [("Nadia", "sheet-nadia-turn-take3.jpg", FRONT), ("Elias", "sheet-elias-turn-take1.jpg", FRONT),
               ("Mags", "sheet-mags-turn-take2.jpg", FRONT), ("Theo", "sheet-theo-turn-take2.jpg", FRONT)]
dream_cast = [("Nadia", "sheet-nadia-bb-take1.jpg", CENTRE), ("Elias", "sheet-elias-bb-take2.jpg", CENTRE),
              ("Theo", "sheet-theo-bb-take1.jpg", CENTRE), ("Walt", "sheet-walt-bb-take1.jpg", CENTRE)]
lenas = [("Lena 4-a\n(my fixed design)", "lena5-turn-take1.jpg", "lena5-bb-take2.jpg"),
         ("Lena B fixed\n(the other agent)", "lenafresh2-turn-take3.jpg", "lenafresh2-bb-take1.jpg"),
         ("Lena B reconciled\n(the other agent)", "lenafresh-recon2-turn-take2.jpg", "lenafresh-recon3-bb-take1.jpg"),
         ("Lena step 4\n(B's face, 4-a clothes)", "lena6-turn-take1.jpg", "lena6-bb-take1.jpg")]
lineup([(t, waking_cast + [("Lena", w, FRONT)]) for t, w, _ in lenas], os.path.join(VJ, "docs", "art", "vh-cast-lineup-waking.jpg"))
NARROW = (0.38, 0.62)        # B's fixed billboard has faded ghost figures at the sides
lineup([(t, dream_cast + [("Lena", b, NARROW if b.startswith("lenafresh2-bb") else CENTRE)]) for t, _, b in lenas], os.path.join(VJ, "docs", "art", "vh-cast-lineup-dream.jpg"))
