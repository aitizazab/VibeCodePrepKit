"""Skyline Repo's procedural look textures (Task 21): made here from code, no downloads, no image APIs.

    uv run --with numpy --with pillow python -I tools/skyline/look/make-textures.py Assets/JamKit/Templates/SkylineRepo/Look/Textures

Writes (all tileable unless said):
  SK_BrushedSteel_{Albedo,Normal,Mask}.png  1024, 1 m a repeat: brushed stainless (streaks along U). The gold trim is the same
                                            maps under a gold tint (CONCIERGE's white and gold).
  SK_Hazard_{Albedo,Mask}.png               1024, 1 m: the firm's orange with black hazard stripes, worn (Kestrel & Daughters' kit only).
  SK_Signs.png                              2048 atlas, 4 x 8 cells of 512 x 256 (not tileable): the Galleria's signs, shop fascias,
                                            food-stall menus, CONCIERGE's notices. Glowing letters on dark panels (shown unlit, HDR).
  SK_Posters.png                            2048 atlas, 4 x 4 cells of 512 x 512: lightbox posters and adverts for the shops.
  SK_Products.png                           1024 atlas, 1 x 8 rows of 1024 x 128: shelves of packaged goods (tile along U).
  SK_Facade_{Blue,White,Green,Dark}.png     512, 4 floors x 4 bays (14 m x 12 m a repeat): the far towers' curtain walls.
  SK_EntryMarker.png                        1024 x 512 (not tileable): the gold chevrons on the entry lane's floor plate.
Fonts: DM Serif Display (SIL OFL, in the repo under VisitingHours/Argument/Resources/ArgumentFonts) for CONCIERGE's and the
Galleria's own signs; Bahnschrift (Windows) for shop brands and menus, falling back to Crimson Text (OFL, same folder).
Every word on a sign is a draft for the creator (listed in docs/SKYLINE-LOOK.md).
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
FONT_DIR = os.path.join(REPO, "Assets", "JamKit", "Templates", "VisitingHours", "Argument", "Resources", "ArgumentFonts")
SERIF = os.path.join(FONT_DIR, "DMSerifDisplay-Regular.ttf")
TEXT_FALLBACK = os.path.join(FONT_DIR, "CrimsonText-Bold.ttf")
SANS = "C:/Windows/Fonts/bahnschrift.ttf"
RNG = np.random.default_rng(21)

GOLD = (236, 196, 118)
WHITE = (250, 247, 240)


def font(path, size):
    for p in (path, TEXT_FALLBACK):
        try:
            return ImageFont.truetype(p, size)
        except OSError:
            continue
    return ImageFont.load_default()


def periodic_noise(h, w, scale_y, scale_x, seed):
    """Tileable Gaussian-filtered noise (FFT), normalised to std 1. scale = feature size in pixels per axis."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    filt = np.exp(-((fy * scale_y) ** 2 + (fx * scale_x) ** 2) * 6.0)
    layer = np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((h, w))) * filt))
    return layer / (layer.std() + 1e-6)


def save(img, out, name):
    img.save(os.path.join(out, name), optimize=True)
    print("ok", name, flush=True)


def normal_from_height(hgt, strength):
    gy, gx = np.gradient(hgt)
    # Tileable gradient: wrap the edges.
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5
    gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5
    n = np.stack([-gx * strength, gy * strength, np.ones_like(hgt)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB")


def brushed(out):
    s = 1024
    streak = periodic_noise(s, s, 1.0, 180.0, 1) * 0.6 + periodic_noise(s, s, 3.0, 60.0, 2) * 0.4   # long along U (x)
    blotch = periodic_noise(s, s, 220.0, 220.0, 3)
    v = 0.74 + streak * 0.035 + blotch * 0.02
    alb = np.repeat(np.clip(v, 0, 1)[..., None], 3, axis=2) * np.array([0.98, 0.99, 1.0])
    save(Image.fromarray((alb * 255).astype(np.uint8), "RGB"), out, "SK_BrushedSteel_Albedo.png")
    save(normal_from_height(streak * 0.6, 0.9), out, "SK_BrushedSteel_Normal.png")
    smooth = np.clip(0.72 + streak * 0.05 - np.abs(blotch) * 0.04, 0, 1)
    mask = np.stack([np.ones_like(v), np.ones_like(v), np.full_like(v, 0.5), smooth], -1)
    save(Image.fromarray((mask * 255).astype(np.uint8), "RGBA"), out, "SK_BrushedSteel_Mask.png")


def hazard(out):
    s = 1024
    y, x = np.mgrid[0:s, 0:s].astype(np.float32)
    # 45-degree stripes, 8 pairs a repeat (tileable: the period divides the size along both axes).
    phase = ((x + y) / (s / 8.0)) % 1.0
    stripe = phase < 0.5
    wear = periodic_noise(s, s, 60, 60, 4) + periodic_noise(s, s, 8, 8, 5) * 0.5
    orange = np.array([0.93, 0.42, 0.07])
    black = np.array([0.06, 0.06, 0.065])
    alb = np.where(stripe[..., None], orange, black)
    chipped = wear > 2.1   # bare metal where the paint is chipped
    alb = np.where(chipped[..., None], np.array([0.55, 0.55, 0.56]), alb)
    alb = alb * (0.94 + 0.06 * np.clip(periodic_noise(s, s, 120, 120, 6), -2, 2)[..., None] / 2)
    save(Image.fromarray((np.clip(alb, 0, 1) * 255).astype(np.uint8), "RGB"), out, "SK_Hazard_Albedo.png")
    metal = np.where(chipped, 1.0, 0.0)
    smooth = np.where(chipped, 0.6, 0.42) + periodic_noise(s, s, 30, 30, 7) * 0.03
    mask = np.stack([metal, np.ones_like(metal), np.full_like(metal, 0.5), np.clip(smooth, 0, 1)], -1)
    save(Image.fromarray((mask * 255).astype(np.uint8), "RGBA"), out, "SK_Hazard_Mask.png")


def text_size(dr, txt, f, spacing=0):
    w = 0
    for ch in txt:
        b = dr.textbbox((0, 0), ch, font=f)
        w += (b[2] - b[0]) + spacing
    b = dr.textbbox((0, 0), txt, font=f)
    return w - spacing, b[3] - b[1], b[1]


def draw_spaced(dr, xy, txt, f, fill, spacing):
    x, y = xy
    for ch in txt:
        dr.text((x, y), ch, font=f, fill=fill)
        b = dr.textbbox((0, 0), ch, font=f)
        x += (b[2] - b[0]) + spacing if ch != " " else f.size * 0.35 + spacing


def spaced_width(dr, txt, f, spacing):
    return sum((dr.textbbox((0, 0), ch, font=f)[2] - dr.textbbox((0, 0), ch, font=f)[0]) + spacing if ch != " " else f.size * 0.35 + spacing
               for ch in txt) - spacing


def centred(dr, box, txt, f, fill, spacing=0):
    x0, y0, x1, y1 = box
    # Shrink the font until the line fits the box with a margin (no sign text runs off its panel).
    path = getattr(f, "path", None)
    while path and f.size > 12 and spaced_width(dr, txt, f, spacing) > (x1 - x0) * 0.9:
        f = ImageFont.truetype(path, f.size - 2)
    w, h, top = text_size(dr, txt, f, spacing)
    w = sum((dr.textbbox((0, 0), ch, font=f)[2] - dr.textbbox((0, 0), ch, font=f)[0]) + spacing if ch != " " else f.size * 0.35 + spacing
            for ch in txt) - spacing
    draw_spaced(dr, (x0 + (x1 - x0 - w) / 2, y0 + (y1 - y0 - h) / 2 - top), txt, f, fill, spacing)


def panel(w, h, base):
    """A dark sign panel with a soft vertical sheen."""
    a = np.zeros((h, w, 3), np.float32)
    t = np.linspace(0, 1, h)[:, None]
    a[:] = np.array(base)
    a *= (0.8 + 0.4 * (1 - t))[..., None]
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGB")


def glow(img, radius=6, amount=0.6):
    blur = img.filter(ImageFilter.GaussianBlur(radius))
    return Image.blend(img, Image.fromarray(np.clip(np.asarray(img).astype(np.float32) + np.asarray(blur).astype(np.float32) * amount, 0, 255).astype(np.uint8)), 1.0)


# Shop brands: (name, panel colour, letter colour, serif?)
SHOPS = [
    ("VELLUM BOOKS", (20, 28, 40), WHITE, True), ("ORBIT TOYS", (40, 18, 60), (255, 190, 90), False),
    ("HALCYON OPTICS", (14, 32, 36), (150, 235, 230), False), ("NIMBUS TEA", (24, 36, 24), (200, 235, 160), True),
    ("PARCEL & PINE", (38, 26, 18), GOLD, True), ("LUMEN AUDIO", (16, 16, 22), (130, 190, 255), False),
    ("MERIDIAN SHOES", (44, 20, 22), (255, 200, 190), True), ("KITE & CO", (16, 30, 48), (255, 240, 200), False),
    ("BLOOM FLORIST", (40, 22, 34), (255, 170, 210), True), ("AURA SKIN", (34, 30, 30), (255, 226, 214), True),
    ("NOVA PHONES", (10, 18, 30), (120, 220, 255), False), ("SOLSTICE SPORTS", (40, 24, 10), (255, 186, 80), False),
]
MENUS = [("NOODLE BAR", (120, 30, 24)), ("TACO SOL", (20, 70, 60)), ("BAO HAUS", (30, 34, 70)), ("JUICE LAB", (40, 80, 30))]
CONCIERGE = [
    ("THE GALLERIA", "Halcyon Row  ·  est. 2051"),
    ("CONCIERGE", "At your service, always"),
    ("PLEASE DO NOT RUN", "Floor care in progress"),
    ("REMODEL IN PROGRESS", "Thank you for your patience"),
    ("WELCOME, VALUED GUEST", "Your visit is being recorded"),
    ("GALLERIA", ""),
    ("ESPRESSO", "· KIOSK ·"),
    ("FOOD COURT", "▲"),
]


def signs(out):
    cw, ch, cols, rows = 512, 256, 4, 8
    atlas = Image.new("RGB", (cw * cols, ch * rows), (8, 8, 10))
    cells = []
    # Row 0-1: CONCIERGE / the Galleria's own signs (white and gold on deep navy, serif).
    for title, sub in CONCIERGE:
        img = panel(cw, ch, (14, 20, 34))
        dr = ImageDraw.Draw(img)
        dr.rectangle([10, 10, cw - 11, ch - 11], outline=GOLD, width=3)
        dr.rectangle([18, 18, cw - 19, ch - 19], outline=(120, 100, 60), width=1)
        f = font(SERIF, 64 if len(title) < 12 else 46 if len(title) < 18 else 38)
        if sub:
            centred(dr, (20, 30, cw - 20, 150), title, f, WHITE, 4)
            dr.line([(cw / 2 - 90, 160), (cw / 2 + 90, 160)], fill=GOLD, width=2)
            if sub == "▲":   # an arrow drawn, not a glyph (not every font has it)
                dr.polygon([(cw / 2 - 34, 222), (cw / 2, 172), (cw / 2 + 34, 222)], fill=GOLD)
            else:
                centred(dr, (20, 168, cw - 20, 232), sub, font(SERIF, 30), GOLD, 2)
        else:
            centred(dr, (30, 20, cw - 30, ch - 20), title, font(SERIF, 96), GOLD, 10)
        cells.append(glow(img, 5, 0.5))
    # Rows 2-4: shop fascias.
    for name, base, col, serif in SHOPS:
        img = panel(cw, ch, base)
        dr = ImageDraw.Draw(img)
        f = font(SERIF if serif else SANS, 70 if len(name) <= 10 else 54 if len(name) <= 13 else 46)
        centred(dr, (16, 40, cw - 16, ch - 40), name, f, col, 6 if serif else 3)
        dr.line([(40, ch - 44), (cw - 40, ch - 44)], fill=tuple(int(c * 0.5) for c in col), width=2)
        cells.append(glow(img, 6, 0.55))
    # Row 5: food-stall menu boards (title, lines of dishes and prices).
    dishes = ["Signature bowl", "Spicy special", "Combo deal", "Side of greens", "Daily soup", "Iced drink"]
    for title, base in MENUS:
        img = panel(cw, ch, tuple(int(c * 0.5) for c in base))
        dr = ImageDraw.Draw(img)
        dr.rectangle([0, 0, cw, 70], fill=base)
        centred(dr, (0, 4, cw, 66), title, font(SANS, 48), WHITE, 4)
        sf = font(SANS, 21)
        for i, d in enumerate(dishes):
            y = 84 + (i % 3) * 52
            x = 24 if i < 3 else cw // 2 + 12
            dr.text((x, y), d, font=sf, fill=(240, 236, 226))
            price = "%d.%02d" % (6 + (i * 7) % 9, (i * 25) % 100)
            pw = dr.textbbox((0, 0), price, font=sf)[2]
            right = cw // 2 - 14 if i < 3 else cw - 20
            dr.text((right - pw, y), price, font=sf, fill=GOLD)
        cells.append(glow(img, 4, 0.35))
    # Rows 6-7: wayfinding and spare dark cells (index order is fixed: see SkylineLookLibrary.SignCell).
    while len(cells) < cols * rows:
        img = panel(cw, ch, (14, 20, 34))
        cells.append(img)
    for i, c in enumerate(cells[: cols * rows]):
        atlas.paste(c, ((i % cols) * cw, (i // cols) * ch))
    save(atlas, out, "SK_Signs.png")


def posters(out):
    cw, n = 512, 4
    atlas = Image.new("RGB", (cw * n, cw * n))
    palettes = [((255, 120, 80), (255, 210, 120)), ((60, 140, 255), (160, 230, 255)), ((40, 200, 150), (220, 255, 200)),
                ((230, 70, 140), (255, 200, 230)), ((250, 200, 60), (255, 250, 210)), ((120, 90, 230), (230, 210, 255)),
                ((30, 30, 40), GOLD), ((240, 240, 235), (200, 170, 110))]
    words = ["NEW SEASON", "SALE", "SUMMER", "LIMITED", "NOW OPEN", "FRESH", "ICONS", "THE EDIT",
             "FREE GIFT", "MEMBERS", "LAUNCH", "STYLE", "TASTE", "SOUND", "LIGHT", "PLAY"]
    for i in range(n * n):
        a, b = palettes[i % len(palettes)]
        y = np.linspace(0, 1, cw)[:, None, None]
        g = np.array(a) * (1 - y) + np.array(b) * y
        img = Image.fromarray(np.repeat(g, cw, axis=1).astype(np.uint8), "RGB")
        dr = ImageDraw.Draw(img)
        r = RNG.random
        for k in range(3):   # bold shapes: a product silhouette, a disc, a band
            cx, cy, rad = 256 + (r() - 0.5) * 200, 230 + (r() - 0.5) * 120, 70 + r() * 90
            col = tuple(int(c) for c in np.clip(np.array(b) * (0.6 + 0.5 * r()), 0, 255))
            if k == 0:
                dr.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], fill=col)
            elif k == 1:
                dr.rounded_rectangle([cx - rad * 0.5, cy - rad * 1.1, cx + rad * 0.5, cy + rad * 1.1], radius=18, fill=tuple(255 - c // 3 for c in col))
            else:
                by = 380 + r() * 30
                dr.rectangle([0, by, cw, by + 14 + r() * 26], fill=col)
        centred(dr, (20, 400, cw - 20, 490), words[i], font(SANS, 64), (255, 255, 255), 4)
        atlas.paste(img, ((i % n) * cw, (i // n) * cw))
    save(atlas, out, "SK_Posters.png")


def products(out):
    w, rh, rows = 1024, 128, 8
    img = Image.new("RGB", (w, rh * rows), (40, 40, 44))
    dr = ImageDraw.Draw(img)
    for row in range(rows):
        y0 = row * rh
        x = 0
        hue_base = row / rows
        while x < w:
            pw = int(28 + RNG.random() * 44)
            if x + pw > w:
                pw = w - x
            ph = int(rh * (0.55 + RNG.random() * 0.4))
            hsv = ((hue_base + RNG.normal(0, 0.06)) % 1.0, 0.45 + RNG.random() * 0.4, 0.55 + RNG.random() * 0.4)
            col = tuple(int(c * 255) for c in hsv_to_rgb(*hsv))
            dr.rectangle([x + 2, y0 + rh - ph, x + pw - 2, y0 + rh - 4], fill=col)
            dr.rectangle([x + 4, y0 + rh - ph * 0.6, x + pw - 4, y0 + rh - ph * 0.4], fill=(245, 242, 235))
            dr.rectangle([x + 2, y0 + rh - ph, x + 4, y0 + rh - 4], fill=tuple(min(255, c + 40) for c in col))
            x += pw
        dr.rectangle([0, y0 + rh - 4, w, y0 + rh], fill=(200, 200, 205))   # the shelf lip
    save(img, out, "SK_Products.png")


def hsv_to_rgb(h, s, v):
    i = int(h * 6) % 6
    f = h * 6 - int(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    return [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)][i]


def facades(out):
    s = 512
    specs = {
        # name: glass colour, frame colour, spandrel colour, mullions per bay, spandrel share of a floor
        "Blue": ((70, 110, 150), (200, 205, 210), (60, 80, 100), 2, 0.28),
        "White": ((50, 64, 80), (232, 230, 224), (232, 230, 224), 1, 0.45),
        "Green": ((70, 120, 115), (170, 180, 178), (70, 96, 92), 3, 0.22),
        "Dark": ((34, 40, 50), (196, 166, 110), (30, 34, 40), 2, 0.3),
    }
    for seed, (name, (glass, frame, spandrel, mull, sp)) in enumerate(specs.items()):
        y, x = np.mgrid[0:s, 0:s].astype(np.float32)
        floors, bays = 4, 4
        fy = (y / s * floors) % 1.0
        bx = (x / s * bays) % 1.0
        sky = np.linspace(1.15, 0.85, s)[:, None]                 # glass catches the sky brighter near the top of a repeat
        refl = periodic_noise(s, s, 60, 60, 40 + seed) * 0.06
        img = np.ones((s, s, 3), np.float32) * np.array(glass) / 255.0 * (sky + refl)[..., None]
        lit = RNG.random((floors, bays * mull)) < 0.18             # a few rooms with lights or blinds down
        cellx = np.floor(x / s * bays * mull).astype(int)
        celly = np.floor(y / s * floors).astype(int)
        blinds = lit[celly, cellx]
        img = np.where(blinds[..., None], img * 0.7 + np.array([0.75, 0.72, 0.62]) * 0.3, img)
        is_sp = fy > (1.0 - sp)
        img = np.where(is_sp[..., None], np.array(spandrel) / 255.0, img)
        mx = (x / s * bays * mull) % 1.0
        frame_m = (mx < 0.04) | (mx > 0.96) | (fy < 0.015) | (np.abs(fy - (1.0 - sp)) < 0.012)
        img = np.where(frame_m[..., None], np.array(frame) / 255.0, img)
        save(Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGB"), out, "SK_Facade_%s.png" % name)


def entry_marker(out):
    w, h = 1024, 512
    base = np.ones((h, w, 3), np.float32) * np.array([0.2, 0.22, 0.27])
    base *= (0.95 + 0.05 * periodic_noise(h, w, 40, 40, 9)[..., None])
    img = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8), "RGB")
    dr = ImageDraw.Draw(img)
    dr.rectangle([12, 12, w - 13, h - 13], outline=GOLD, width=10)
    for k in range(3):   # chevrons pointing +V (north, into the hall)
        cx, cy = w / 2, 330 - k * 95
        dr.polygon([(cx - 210, cy + 50), (cx, cy - 60), (cx + 210, cy + 50), (cx + 210, cy + 95), (cx, cy - 15), (cx - 210, cy + 95)], fill=GOLD)
    centred(dr, (0, 400, w, 490), "SECURITY RE-DEPLOYMENT", font(SERIF, 46), (245, 236, 210), 6)
    save(img, out, "SK_EntryMarker.png")


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    brushed(out)
    hazard(out)
    signs(out)
    posters(out)
    products(out)
    facades(out)
    entry_marker(out)


if __name__ == "__main__":
    main()
