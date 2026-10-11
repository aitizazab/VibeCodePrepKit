# Title-screen mock-ups from the sketch takes (2026-10-12): the working title and a Play button laid on the tablet's dark
# screen, turned with the tablet, so the creator sees each take as the title screen. Not shipped art: review only.
#   uv run --with pillow python tools/art/vh_title_mock.py <take.jpg> <out.jpg> <cx> <cy> <degrees>
# cx, cy: the screen's visible centre (fractions of the frame); degrees: the tablet's turn (anticlockwise).
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

src, out = sys.argv[1], sys.argv[2]
cx, cy, deg = float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
im = Image.open(src).convert("RGBA")
W, H = im.size
layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(layer)
title_font = ImageFont.truetype("C:/Windows/Fonts/georgia.ttf", int(H * 0.085))
button_font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", int(H * 0.038))
x0, y0 = W * 0.5, H * 0.5

def centred(text, font, y, fill):
    w = d.textlength(text, font=font)
    d.text((x0 - w / 2, y), text, font=font, fill=fill)

ink = (226, 231, 238, 235)
centred("Visiting Hours", title_font, y0 - H * 0.13, ink)
d.line([(x0 - W * 0.09, y0 - H * 0.015), (x0 + W * 0.09, y0 - H * 0.015)], fill=(226, 231, 238, 120), width=max(2, H // 600))
bw, bh = W * 0.105, H * 0.072
by = y0 + H * 0.07
d.rounded_rectangle([x0 - bw / 2, by, x0 + bw / 2, by + bh], radius=int(bh * 0.5), outline=ink, width=max(3, H // 400),
                    fill=(255, 255, 255, 18))
tri = H * 0.018
tx = x0 - d.textlength("Play", font=button_font) / 2 - tri * 0.9
d.polygon([(tx - tri * 0.6, by + bh / 2 - tri * 0.7), (tx - tri * 0.6, by + bh / 2 + tri * 0.7), (tx + tri * 0.6, by + bh / 2)], fill=ink)
w = d.textlength("Play", font=button_font)
d.text((x0 - w / 2 + tri * 0.4, by + bh * 0.14), "Play", font=button_font, fill=ink)

# A soft screen glow behind the type, then turn the whole layer with the tablet and move it onto the screen.
glow = layer.filter(ImageFilter.GaussianBlur(H * 0.012))
both = Image.alpha_composite(glow, layer).rotate(deg, resample=Image.BICUBIC, center=(x0, y0))
placed = Image.new("RGBA", (W, H), (0, 0, 0, 0))
placed.paste(both, (int(cx * W - x0), int(cy * H - y0)), both)
Image.alpha_composite(im, placed).convert("RGB").save(out, quality=92)
print("wrote", out)
