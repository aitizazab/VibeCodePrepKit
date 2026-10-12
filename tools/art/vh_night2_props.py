# Night 2's props drawn for the re-plot (docs/NIGHTS-1-2.md): PLACEHOLDER art, made from the dream's existing crayon
# pictures plus crayon strokes, until the creator wants them redrawn.
#  - dream_pic_place_card.png: Nadia's place card, her name in Theo's handwriting (the fourth chair).
#  - dream_pic_calendar_n2.png: the fridge calendar, "Pancakes!" on every Sunday, small stars on Lena's Thursdays.
#  - dream_pic_bad_dream.png: Theo's drawing "THE BAD DREAM": the family, and a woman in scrubs who comes to take them.
#   uv run --with pillow python tools/art/vh_night2_props.py <Art/Dream dir> [--force]
# A picture that's already there is skipped (2026-10-13: the place card and THE BAD DREAM are real drawings now, deba0c3,
# and a re-run must not overwrite them); --force redraws all three placeholders over whatever is there.
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
FORCE = "--force" in sys.argv[1:]
if not ARGS:
    sys.exit("usage: vh_night2_props.py <Art/Dream dir> [--force]")
ART = ARGS[0]
HAND = "C:/Windows/Fonts/Inkfree.ttf"
rnd = random.Random(7)


def font(size):
    try:
        return ImageFont.truetype(HAND, size)
    except OSError:
        return ImageFont.load_default()


def crayon_line(d, pts, colour, width, passes=3):
    """A crayon stroke: a few jittered passes, a little see-through."""
    for k in range(passes):
        j = [(x + rnd.uniform(-width * 0.25, width * 0.25), y + rnd.uniform(-width * 0.25, width * 0.25)) for x, y in pts]
        c = colour + (150 + rnd.randint(-30, 40),)
        d.line(j, fill=c, width=max(1, int(width * rnd.uniform(0.7, 1.0))), joint="curve")


def crayon_fill(d, box, colour, step=6, angle=0.6):
    """Hatched crayon fill inside a box (x0, y0, x1, y1)."""
    x0, y0, x1, y1 = box
    for t in range(int(x0 - (y1 - y0)), int(x1), step):
        a = (t, y1)
        b = (t + (y1 - y0) * angle, y0)
        crayon_line(d, [a, b], colour, step * 0.9, passes=1)


def crayon_text(img, xy, text, size, colour, angle=0.0):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    f = font(size)
    for k in range(3):
        d.text((xy[0] + rnd.uniform(-1.5, 1.5), xy[1] + rnd.uniform(-1.5, 1.5)), text, font=f, fill=colour + (170,))
    if angle:
        layer = layer.rotate(angle, center=xy, resample=Image.BICUBIC)
    img.alpha_composite(layer)


def place_card():
    W, H = 640, 400
    img = Image.new("RGBA", (W, H), (250, 246, 236, 255))
    d = ImageDraw.Draw(img, "RGBA")
    d.rectangle([4, 4, W - 5, H - 5], outline=(120, 100, 90, 255), width=4)
    d.line([(4, 70), (W - 5, 70)], fill=(200, 190, 175, 255), width=3)   # the fold
    crayon_text(img, (120, 120), "Nadia", 170, (60, 90, 170), angle=-3)
    for (x, y, c) in ((70, 320, (230, 180, 40)), (560, 300, (220, 90, 110))):
        pts = []
        for i in range(11):
            a = -math.pi / 2 + i * math.pi / 5
            r = 34 if i % 2 == 0 else 15
            pts.append((x + math.cos(a) * r, y + math.sin(a) * r))
        crayon_line(d, pts, c, 6)
    img.convert("RGB").save(os.path.join(ART, "dream_pic_place_card.png"))


def calendar():
    base = Image.open(os.path.join(ART, "dream_pic_calendar.png")).convert("RGBA")
    W, H = base.size
    d = ImageDraw.Draw(base, "RGBA")
    paper = base.getpixel((W // 2, H - 30))
    # Clear the old grid, keep the paper, the magnets and the sun sticker at the top.
    top = int(H * 0.25)
    d.rectangle([20, top, W - 20, H - 40], fill=paper)
    crayon_text(base, (W * 0.36, top - 70), "MAY", 64, (200, 70, 70))
    cols, rows = 7, 5
    gx0, gx1, gy0, gy1 = 30, W - 30, top + 50, H - 60
    cw, rh = (gx1 - gx0) / cols, (gy1 - gy0) / rows
    for i, lab in enumerate("SMTWTFS"):
        crayon_text(base, (gx0 + cw * i + cw * 0.35, top + 5), lab, 34, (70, 60, 80))
    for r in range(rows + 1):
        crayon_line(d, [(gx0, gy0 + rh * r), (gx1, gy0 + rh * r)], (60, 55, 70), 3, passes=2)
    for c in range(cols + 1):
        crayon_line(d, [(gx0 + cw * c, gy0), (gx0 + cw * c, gy1)], (60, 55, 70), 3, passes=2)
    day = 1
    for r in range(rows):
        for c in range(cols):
            x, y = gx0 + cw * c, gy0 + rh * r
            crayon_text(base, (x + 6, y + 2), str(day), 22, (90, 80, 90))
            if c == 0:   # Sunday: Pancakes!
                crayon_text(base, (x + 2, y + rh * 0.34), "Pancakes!", 24, (205, 105, 35), angle=6)
                for k in range(3):
                    cy = y + rh * 0.82 - k * 5
                    d.ellipse([x + cw * 0.3, cy - 5, x + cw * 0.7, cy + 5], fill=(225, 165, 90, 230), outline=(150, 100, 60, 255))
            if c == 4:   # Lena's Thursdays: small stars
                sx, sy = x + cw * 0.55, y + rh * 0.6
                pts = []
                for i in range(11):
                    a = -math.pi / 2 + i * math.pi / 5
                    rr = 14 if i % 2 == 0 else 6
                    pts.append((sx + math.cos(a) * rr, sy + math.sin(a) * rr))
                d.polygon(pts, fill=(245, 205, 70, 230), outline=(180, 140, 40, 255))
            day += 1
            if day > 31:
                day = 1
    base.convert("RGB").save(os.path.join(ART, "dream_pic_calendar_n2.png"))


def hatch_poly(d, poly, colour, step=5, angle_deg=55, alpha=200):
    """Crayon hatching clipped to a polygon (drawn on a mask, then lines)."""
    xs = [p[0] for p in poly]; ys = [p[1] for p in poly]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    mask = Image.new("L", d._image.size, 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    layer = Image.new("RGBA", d._image.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    a = math.radians(angle_deg)
    span = (x1 - x0) + (y1 - y0)
    for t in range(int(-span), int(span), step):
        cx, cy = x0 + t, y1
        ex, ey = cx + math.cos(a) * span * 1.5, cy - math.sin(a) * span * 1.5
        ld.line([(cx + rnd.uniform(-1, 1), cy), (ex, ey)], fill=colour + (alpha + rnd.randint(-40, 30),), width=step - 1)
    d._image.paste(layer, (0, 0), Image.composite(layer, Image.new("RGBA", layer.size, (0, 0, 0, 0)), mask).getchannel("A"))


def bad_dream():
    img = Image.open(os.path.join(ART, "dream_pic_theo_drawing.png")).convert("RGBA")
    W, H = img.size
    d = ImageDraw.Draw(img, "RGBA")
    # Night over the sunny sky: scribbled dark crayon at a slant, thicker in places; a crescent moon, a few stars.
    sky = [(0, 0), (W, 0), (W, H * 0.3), (W * 0.62, H * 0.36), (W * 0.62, H * 0.48), (0, H * 0.5)]
    hatch_poly(d, sky, (34, 36, 86), step=6, angle_deg=28, alpha=215)
    hatch_poly(d, sky, (20, 20, 50), step=11, angle_deg=-35, alpha=120)
    cx, cy, r = W * 0.13, H * 0.15, H * 0.075
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(250, 238, 170, 255))
    d.ellipse([cx - r * 0.55, cy - r * 1.05, cx + r * 1.45, cy + r * 0.95], fill=(30, 32, 80, 255))
    for (sx, sy) in ((W * 0.32, H * 0.1), (W * 0.45, H * 0.24), (W * 0.55, H * 0.08), (W * 0.24, H * 0.32)):
        pts = []
        for i in range(11):
            a = -math.pi / 2 + i * math.pi / 5
            rr = 13 if i % 2 == 0 else 5
            pts.append((sx + math.cos(a) * rr, sy + math.sin(a) * rr))
        d.polygon(pts, fill=(250, 225, 110, 255))
    # The woman in scrubs: tall, drawn bigger than the family, coming in from the right, an arm reaching for them.
    fx, top = W * 0.83, H * 0.2
    skin, teal, dark = (238, 196, 170), (40, 150, 150), (24, 80, 84)
    body = [(fx - 62, top + 120), (fx + 62, top + 120), (fx + 72, top + 330), (fx - 72, top + 330)]
    hatch_poly(d, body, teal, step=5, angle_deg=60, alpha=230)
    crayon_line(d, body + [body[0]], dark, 6)
    crayon_line(d, [(fx - 28, top + 120), (fx, top + 165), (fx + 28, top + 120)], dark, 5)
    for lx in (-42, 10):
        leg = [(fx + lx, top + 330), (fx + lx + 34, top + 330), (fx + lx + 36, top + 500), (fx + lx + 2, top + 500)]
        hatch_poly(d, leg, teal, step=5, angle_deg=70, alpha=230)
        crayon_line(d, leg + [leg[0]], dark, 5)
        crayon_line(d, [(fx + lx - 6, top + 505), (fx + lx + 40, top + 505)], (40, 40, 50), 12)
    arm = [(fx - 60, top + 130), (fx - 250, top + 215), (fx - 245, top + 245), (fx - 55, top + 165)]
    hatch_poly(d, arm, teal, step=5, angle_deg=20, alpha=230)
    crayon_line(d, arm + [arm[0]], dark, 5)
    for k in range(4):   # fingers, reaching
        crayon_line(d, [(fx - 250, top + 222 + k * 7), (fx - 285, top + 205 + k * 12)], skin, 6, passes=2)
    crayon_line(d, [(fx + 62, top + 135), (fx + 95, top + 300)], teal, 26, passes=2)
    d.ellipse([fx - 45, top + 25, fx + 45, top + 120], fill=skin + (255,), outline=(120, 80, 60, 255), width=4)
    hatch_poly(d, [(fx - 50, top + 60), (fx - 40, top + 10), (fx + 40, top + 10), (fx + 50, top + 60), (fx, top + 35)], (50, 34, 28), step=4, angle_deg=80, alpha=240)
    d.ellipse([fx - 22, top + 62, fx - 10, top + 74], fill=(30, 30, 40, 255))
    d.ellipse([fx + 10, top + 62, fx + 22, top + 74], fill=(30, 30, 40, 255))
    crayon_line(d, [(fx - 16, top + 98), (fx + 16, top + 98)], (150, 50, 50), 4)
    # The title, big careful capitals.
    title = Image.new("RGBA", img.size, (0, 0, 0, 0))
    td = ImageDraw.Draw(title)
    f = font(96)
    for k in range(4):
        td.text((W * 0.2 + rnd.uniform(-2, 2), H * 0.02 + rnd.uniform(-2, 2)), "THE BAD DREAM", font=f, fill=(215, 45, 45, 190), stroke_width=2, stroke_fill=(215, 45, 45, 120))
    img.alpha_composite(title)
    img.convert("RGB").save(os.path.join(ART, "dream_pic_bad_dream.png"))


written, skipped = [], []
for name, draw in (("dream_pic_place_card.png", place_card), ("dream_pic_calendar_n2.png", calendar), ("dream_pic_bad_dream.png", bad_dream)):
    if os.path.exists(os.path.join(ART, name)) and not FORCE:
        skipped.append(name)
        continue
    draw()
    written.append(name)
print("written: " + (", ".join(written) or "nothing"))
if skipped:
    print("skipped (already there; --force to redraw): " + ", ".join(skipped))
