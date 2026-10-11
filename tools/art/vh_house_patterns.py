# Wallpaper and ceiling pattern tiles for Night 2's house (the finish options of 2026-10-12, round 3): seamless 512 px
# tiles, drawn at 2x and filtered down. Colour tiles (dream_wall_*.png) are the wallpaper's actual colours (the capture
# variants put them on a white material); grey tiles (dream_ceiling_*.png) are brightness patterns (mean about 0.5) on
# the ceiling's colour. Also writes a swatch sheet for the review page.
#   uv run --with pillow python tools/art/vh_house_patterns.py <art dir> [<swatch sheet .png>]
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

N = 1024           # drawing size (2x)
OUT = 512

# id, name, background, wainscot (shown on the swatch), tile metres (DreamCaptureVariants keeps the same numbers)
WALLS = [
    ("forest-damask", "Forest damask", (38, 74, 56), (226, 214, 188)),
    ("oxblood-stripe", "Oxblood regency stripe", (100, 28, 34), (88, 60, 44)),
    ("navy-sprig", "Navy sprig (small floral)", (30, 42, 82), (236, 234, 228)),
    ("plum-trellis", "Plum trellis (geometric)", (78, 40, 72), (170, 160, 176)),
    ("teal-botanical", "Teal botanical (Morris-like)", (22, 70, 74), (196, 150, 80)),
    ("umber-fans", "Umber fans (deco geometric)", (82, 56, 38), (40, 70, 56)),
    ("sage-stripe", "Sage pinstripe (mid-tone)", (150, 168, 138), (44, 78, 60)),
    ("rose-sprig", "Dusty rose sprig (mid-tone)", (190, 140, 134), (106, 34, 40)),
]


def tiled(draw_fn, bg):
    img = Image.new("RGB", (N, N), bg)
    d = ImageDraw.Draw(img)
    for ox in (-N, 0, N):
        for oy in (-N, 0, N):
            draw_fn(d, ox, oy)
    return img.resize((OUT, OUT), Image.LANCZOS)


def ell(d, cx, cy, rx, ry, fill, outline=None, width=1):
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=fill, outline=outline, width=width)


def leaf(d, cx, cy, length, width, angle, fill, vein=None):
    pts = []
    for i in range(25):
        t = i / 24 * math.pi
        x, y = (i / 24 - 0.5) * length, math.sin(t) * width * 0.5
        pts.append((x, y))
    for i in range(24, -1, -1):
        t = i / 24 * math.pi
        pts.append(((i / 24 - 0.5) * length, -math.sin(t) * width * 0.5))
    ca, sa = math.cos(angle), math.sin(angle)
    d.polygon([(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts], fill=fill)
    if vein:
        d.line([(cx - length * 0.45 * ca, cy - length * 0.45 * sa), (cx + length * 0.45 * ca, cy + length * 0.45 * sa)], fill=vein, width=3)


def flower(d, cx, cy, r, petal, centre, n=5, rot=0.0):
    for k in range(n):
        a = rot + k * 2 * math.pi / n
        ell(d, cx + math.cos(a) * r * 0.6, cy + math.sin(a) * r * 0.6, r * 0.45, r * 0.45, petal)
    ell(d, cx, cy, r * 0.3, r * 0.3, centre)


def damask(bg):
    hi, lo = (62, 108, 80), (30, 60, 45)

    def motif(d, cx, cy, s):
        # A mirrored ogee motif: a central drop, two side leaves, scrolls, a crown.
        for sx in (-1, 1):
            leaf(d, cx + sx * 70 * s, cy + 10 * s, 150 * s, 60 * s, math.radians(sx * 60), hi)
            leaf(d, cx + sx * 40 * s, cy - 110 * s, 110 * s, 40 * s, math.radians(sx * -35), hi)
            ell(d, cx + sx * 105 * s, cy - 60 * s, 22 * s, 22 * s, None, hi, int(9 * s))
            ell(d, cx + sx * 60 * s, cy + 120 * s, 16 * s, 16 * s, hi)
        d.polygon([(cx, cy - 170 * s), (cx + 45 * s, cy - 40 * s), (cx, cy + 150 * s), (cx - 45 * s, cy - 40 * s)], fill=hi)
        d.polygon([(cx, cy - 120 * s), (cx + 22 * s, cy - 40 * s), (cx, cy + 90 * s), (cx - 22 * s, cy - 40 * s)], fill=lo)
        ell(d, cx, cy - 195 * s, 18 * s, 18 * s, hi)

    def draw(d, ox, oy):
        motif(d, ox + N * 0.5, oy + N * 0.27, 1.45)
        motif(d, ox + 0, oy + N * 0.77, 1.45)
        motif(d, ox + N, oy + N * 0.77, 1.45)
        for (x, y) in ((0, N * 0.27), (N * 0.5, N * 0.77), (N, N * 0.27)):
            flower(d, ox + x, oy + y, 60, hi, lo, 4, math.radians(45))
            for sx in (-1, 1):
                leaf(d, ox + x + sx * 90, oy + y, 110, 36, 0, hi)
    return tiled(draw, bg)


def stripes(bg, bands):
    img = Image.new("RGB", (N, N), bg)
    d = ImageDraw.Draw(img)
    for x0, w, col in bands:
        d.rectangle([x0 * N, 0, (x0 + w) * N, N], fill=col)
    return img.resize((OUT, OUT), Image.LANCZOS)


def sprig(bg, petal, centre, leafc, petal2=None):
    def one(d, cx, cy, s, rot, p):
        d.line([(cx, cy), (cx + 40 * s * math.cos(rot + 1.9), cy + 40 * s * math.sin(rot + 1.9))], fill=leafc, width=int(6 * s))
        leaf(d, cx + 30 * s * math.cos(rot + 2.4), cy + 30 * s * math.sin(rot + 2.4), 46 * s, 20 * s, rot + 2.4, leafc)
        leaf(d, cx + 30 * s * math.cos(rot + 1.3), cy + 30 * s * math.sin(rot + 1.3), 40 * s, 18 * s, rot + 1.3, leafc)
        flower(d, cx, cy, 30 * s, p, centre, 5, rot)

    rnd = random.Random(4)
    spots = []
    for gy in range(4):
        for gx in range(4):
            spots.append(((gx + (0.5 if gy % 2 else 0)) * N / 4, (gy + 0.5) * N / 4, rnd.uniform(0, 6.28), gx % 2 == gy % 2))

    def draw(d, ox, oy):
        for (x, y, rot, alt) in spots:
            one(d, ox + x, oy + y, 1.0, rot, petal2 if (alt and petal2) else petal)
    return tiled(draw, bg)


def trellis(bg, line, dot):
    def draw(d, ox, oy):
        step = N / 4
        for k in range(-4, 9):
            d.line([(ox + k * step, oy), (ox + k * step + N, oy + N)], fill=line, width=10)
            d.line([(ox + k * step, oy + N), (ox + k * step + N, oy)], fill=line, width=10)
        for i in range(5):
            for j in range(5):
                for (x, y) in ((i * step, j * step), (i * step + step / 2, j * step + step / 2)):
                    ell(d, ox + x, oy + y, 14, 14, dot)
    return tiled(draw, bg)


def botanical(bg, vine, leaf1, leaf2, flower1, flower2, centre):
    def draw(d, ox, oy):
        # Two wavy vines climbing, leaves along them, flowers at the crests.
        for col, x0, phase in ((0, N * 0.25, 0.0), (1, N * 0.75, math.pi)):
            pts = []
            for i in range(0, 101):
                y = i / 100 * N
                x = x0 + math.sin(y / N * 2 * math.pi + phase) * N * 0.14
                pts.append((ox + x, oy + y))
            d.line(pts, fill=vine, width=12)
            for i in range(14):
                y = (i + 0.5) / 14 * N
                x = x0 + math.sin(y / N * 2 * math.pi + phase) * N * 0.14
                slope = math.cos(y / N * 2 * math.pi + phase) * 0.9
                side = 1 if i % 2 else -1
                ang = math.atan2(1, slope * 0.5) + side * 0.9
                leaf(d, ox + x + side * 60, oy + y, 160, 60, ang, leaf1 if i % 3 else leaf2, vine)
            for k, y in enumerate((N * 0.25, N * 0.75)):
                x = x0 + math.sin(y / N * 2 * math.pi + phase) * N * 0.14
                fc = flower1 if (k + col) % 2 == 0 else flower2
                flower(d, ox + x, oy + y, 90, fc, centre, 6, 0.3)
                flower(d, ox + x, oy + y, 42, centre, fc, 6, 0.0)
            # small buds between the vines
            for y in (N * 0.0, N * 0.5):
                ell(d, ox + N * 0.5 + (x0 - N * 0.5) * 0.0, oy + y + N * 0.25 * col, 16, 16, flower2)
    return tiled(draw, bg)


def fans(bg, arc, gold):
    def draw(d, ox, oy):
        r = N / 8
        for row in range(-1, 9):
            for i in range(-1, 6):
                cx = ox + i * 2 * r + (r if row % 2 else 0)
                cy = oy + row * r
                for k, rr in enumerate((r, r * 0.78, r * 0.56, r * 0.34)):
                    d.pieslice([cx - rr, cy - rr, cx + rr, cy + rr], 180, 360, fill=arc if k % 2 == 0 else bg)
                d.arc([cx - r, cy - r, cx + r, cy + r], 180, 360, fill=gold, width=6)
                for a in range(1, 6):
                    aa = math.pi + a * math.pi / 6
                    d.line([(cx, cy), (cx + math.cos(aa) * r * 0.95, cy + math.sin(aa) * r * 0.95)], fill=gold, width=3)
    return tiled(draw, bg)


def wall_tiles():
    out = {}
    out["forest-damask"] = damask((38, 74, 56))
    out["oxblood-stripe"] = stripes((100, 28, 34), [(0.0, 0.32, (118, 38, 44)), (0.34, 0.012, (176, 140, 80)), (0.5, 0.12, (86, 22, 28)), (0.64, 0.012, (176, 140, 80))])
    out["navy-sprig"] = sprig((30, 42, 82), (232, 222, 196), (214, 170, 80), (86, 128, 116), (214, 170, 80))
    out["plum-trellis"] = trellis((78, 40, 72), (118, 76, 108), (196, 156, 92))
    out["teal-botanical"] = botanical((22, 70, 74), (44, 96, 84), (66, 124, 96), (104, 150, 104), (220, 120, 96), (232, 214, 176), (240, 196, 110))
    out["umber-fans"] = fans((82, 56, 38), (112, 80, 52), (198, 160, 96))
    out["sage-stripe"] = stripes((150, 168, 138), [(0.0, 0.2, (138, 156, 126)), (0.24, 0.01, (226, 224, 204)), (0.5, 0.2, (138, 156, 126)), (0.74, 0.01, (226, 224, 204))])
    out["rose-sprig"] = sprig((190, 140, 134), (238, 216, 204), (150, 84, 88), (128, 132, 104), (150, 84, 88))
    return out


def ceiling_tin():
    img = Image.new("L", (N, N), 128)
    d = ImageDraw.Draw(img)
    for i in range(2):
        for j in range(2):
            x0, y0, s = i * N / 2, j * N / 2, N / 2
            # a raised square border: light top-left edges, dark bottom-right
            d.rectangle([x0 + 8, y0 + 8, x0 + s - 8, y0 + s - 8], outline=170, width=10)
            d.line([(x0 + 20, y0 + s - 20), (x0 + s - 20, y0 + s - 20), (x0 + s - 20, y0 + 20)], fill=88, width=8)
            cx, cy = x0 + s / 2, y0 + s / 2
            for rr, v in ((150, 160), (120, 100), (90, 165), (52, 95), (24, 175)):
                ell(d, cx, cy, rr, rr, None, v, 8)
            for a in range(8):
                aa = a * math.pi / 4
                ell(d, cx + math.cos(aa) * 185, cy + math.sin(aa) * 185, 16, 16, 160)
    return img.filter(ImageFilter.GaussianBlur(2)).resize((OUT, OUT), Image.LANCZOS)


def ceiling_paper():
    img = Image.new("L", (N, N), 128)
    d = ImageDraw.Draw(img)
    rnd = random.Random(9)
    for gy in range(6):
        for gx in range(6):
            x = (gx + (0.5 if gy % 2 else 0)) * N / 6
            y = (gy + 0.5) * N / 6
            for ox in (-N, 0, N):
                for oy in (-N, 0, N):
                    pts = []
                    for k in range(10):
                        r = 22 if k % 2 == 0 else 9
                        a = k * math.pi / 5 + rnd.random() * 0.0
                        pts.append((ox + x + math.cos(a) * r, oy + y + math.sin(a) * r))
                    d.polygon(pts, fill=200)
    return img.resize((OUT, OUT), Image.LANCZOS)


def main():
    art = sys.argv[1]
    os.makedirs(art, exist_ok=True)
    tiles = wall_tiles()
    for key, img in tiles.items():
        img.save(os.path.join(art, f"dream_wall_{key}.png"))
    ceiling_tin().save(os.path.join(art, "dream_ceiling_tin.png"))
    ceiling_paper().save(os.path.join(art, "dream_ceiling_paper.png"))
    print("tiles written:", len(tiles) + 2)
    if len(sys.argv) > 2:
        sw, pad = 300, 20
        sheet = Image.new("RGB", (4 * (sw + pad) + pad, 2 * (sw + 120 + pad) + pad), (250, 248, 244))
        d = ImageDraw.Draw(sheet)
        try:
            f = ImageFont.truetype("segoeuib.ttf", 18)
        except OSError:
            f = ImageFont.load_default()
        for k, (key, name, bg, wains) in enumerate(WALLS):
            x, y = pad + (k % 4) * (sw + pad), pad + (k // 4) * (sw + 120 + pad)
            tile = tiles[key].resize((sw, sw))
            sheet.paste(tile, (x, y))
            d.rectangle([x, y + sw - 70, x + sw, y + sw], fill=wains)
            d.rectangle([x, y + sw - 74, x + sw, y + sw - 66], fill=(240, 236, 226))
            d.text((x, y + sw + 8), f"{k + 1}. {name}", font=f, fill=(50, 40, 60))
        sheet.save(sys.argv[2])
        print("swatches:", sys.argv[2])


if __name__ == "__main__":
    main()
