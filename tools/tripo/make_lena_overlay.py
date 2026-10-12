"""Lena's star watch as an overlay on her dream cut-out (docs/redesign/people.md "Billboards"). The drawing itself is not
touched: this writes a picture-sized PNG, transparent except over her left wrist, that DreamCutoutView prints over
dream_lena.png (Resources/People/Overlays/<picture>.png): the round watch she was drawn with is covered by the star
watch she gives Nadia (a gold five-pointed star, a cream dial, two hands, a wine-red strap), in the drawing's own
crayon manner (soft dark-brown outline, grain).

    uv run --with pillow --with numpy python tools/tripo/make_lena_overlay.py [check.png]
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
ART = os.path.join(REPO, "Assets", "JamKit", "Templates", "VisitingHours", "Art", "Dream", "dream_lena.png")
OUT = os.path.join(REPO, "Assets", "JamKit", "Templates", "VisitingHours", "Characters", "Resources", "People", "Overlays", "dream_lena.png")

# Where the round watch is in the drawing (pixels of the 393 x 1024 picture), measured from it.
WATCH = (305.6, 594.8, 11.5)            # the round case: centre x, y, radius
STRAP = (282, 586.5, 297, 601.5)        # the gold strap's visible run: x0, y0, x1, y1
REGION = (274, 574, 324, 616)           # the overlay's working box
SS = 6


def star(cx, cy, r_out, r_in, rot=0.0, n_per=8):
    pts = []
    for k in range(10):
        a0 = rot + k * math.pi / 5
        r = r_out if k % 2 == 0 else r_in
        pts.append((cx + r * math.sin(a0), cy - r * math.cos(a0)))
    out = []
    for i in range(10):
        p, q = pts[i], pts[(i + 1) % 10]
        for t in np.linspace(0, 1, n_per, endpoint=False):
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def main(check=None):
    art = Image.open(ART).convert("RGBA")
    W, H = art.size
    a = np.asarray(art, dtype=np.float32) / 255.0
    x0, y0, x1, y1 = REGION
    box = a[y0:y1, x0:x1].copy()
    bh, bw = box.shape[:2]
    yy, xx = np.mgrid[0:bh, 0:bw].astype(np.float32)
    cx, cy, cr = WATCH[0] - x0, WATCH[1] - y0, WATCH[2]
    sx0, sy0, sx1, sy1 = STRAP[0] - x0, STRAP[1] - y0, STRAP[2] - x0, STRAP[3] - y0
    hole = (((xx - cx) ** 2 + (yy - cy) ** 2) <= (cr + 1.6) ** 2) | ((xx >= sx0 - 1) & (xx <= sx1 + 1) & (yy >= sy0 - 1) & (yy <= sy1 + 1))
    # Fill the hole from what is round it: above the strap's line the cream sleeve, below it the hand (the notebook's
    # dark cover is not borrowed).
    rgb = box[..., :3]
    lum = rgb @ np.array([0.3, 0.59, 0.11])
    src = (~hole) & (lum > 0.45) & (box[..., 3] > 0.9)
    mid = (sy0 + sy1) / 2
    fill = rgb.copy()
    for side in (yy < mid, yy >= mid):
        s = src & side
        if not s.any():
            continue
        ys, xs = np.nonzero(s)
        hy, hx = np.nonzero(hole & side)
        for py, pxx in zip(hy, hx):
            dd = (ys - py) ** 2 + (xs - pxx) ** 2
            k = np.argsort(dd)[:6]
            w = 1.0 / (np.sqrt(dd[k]) + 0.5)
            fill[py, pxx] = (rgb[ys[k], xs[k]] * w[:, None]).sum(0) / w.sum()
    patch = np.zeros((bh, bw, 4), dtype=np.float32)
    patch[..., :3] = fill
    patch[..., 3] = hole.astype(np.float32)
    patch_img = Image.fromarray(np.clip(patch * 255, 0, 255).astype(np.uint8), "RGBA").resize((bw * SS, bh * SS), Image.BILINEAR)

    lay = Image.new("RGBA", (bw * SS, bh * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    ink = (66, 34, 26, 255)

    def S(p):
        return (p[0] * SS, p[1] * SS)
    # The strap: wine-red leather across the wrist, outlined.
    strap = [S((sx0 - 0.6, sy0 + 0.2)), S((cx - 6, sy0 - 0.4)), S((cx - 6, sy1 + 0.4)), S((sx0 - 0.6, sy1 - 0.2))]
    d.polygon(strap, fill=(128, 26, 44, 255), outline=ink)
    d.line([strap[0], strap[1]], fill=ink, width=int(SS * 1.1))
    d.line([strap[3], strap[2]], fill=ink, width=int(SS * 1.1))
    d.line([S((sx0 + 0.6, mid - 1.5)), S((cx - 7, mid - 1.6))], fill=(160, 52, 70, 255), width=int(SS * 0.6))
    # The star case (gold), the dial (cream), two hands, a wine dot under twelve.
    r_out = 14.5
    d.polygon([S(p) for p in star(cx, cy, r_out + 1.1, (r_out + 1.1) * 0.58)], fill=ink)
    d.polygon([S(p) for p in star(cx, cy, r_out, r_out * 0.58)], fill=(222, 170, 72, 255))
    d.polygon([S(p) for p in star(cx - 0.6, cy - 0.8, r_out * 0.62, r_out * 0.62 * 0.58)], fill=(246, 214, 130, 255))
    d.ellipse([S((cx - 6.2, cy - 6.2)), S((cx + 6.2, cy + 6.2))], fill=ink)
    d.ellipse([S((cx - 5.4, cy - 5.4)), S((cx + 5.4, cy + 5.4))], fill=(250, 240, 214, 255))
    d.line([S((cx, cy)), S((cx - 2.6, cy - 1.6))], fill=(28, 26, 44, 255), width=int(SS * 1.0))
    d.line([S((cx, cy)), S((cx + 0.6, cy - 4.4))], fill=(28, 26, 44, 255), width=int(SS * 0.7))
    d.ellipse([S((cx - 0.7, cy - 4.6)), S((cx + 0.7, cy - 3.2))], fill=(150, 30, 50, 255))
    lay = lay.filter(ImageFilter.GaussianBlur(SS * 0.35))
    g = np.asarray(lay, dtype=np.float32) / 255.0
    rng = np.random.default_rng(11)
    noise = Image.fromarray((rng.random((bh * 2, bw * 2)) * 255).astype(np.uint8)).resize((bw * SS, bh * SS), Image.BICUBIC)
    n = (np.asarray(noise, dtype=np.float32) / 255.0 - 0.5) * 0.14
    g[..., :3] = np.clip(g[..., :3] * (1 + n[..., None]), 0, 1)
    lay = Image.fromarray(np.clip(g * 255, 0, 255).astype(np.uint8), "RGBA")
    comp = Image.alpha_composite(patch_img, lay).resize((bw, bh), Image.LANCZOS)

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(comp, (x0, y0))
    oa = np.asarray(out, dtype=np.float32).copy()
    oa[..., 3] *= (a[..., 3] > 0.5)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(oa.astype(np.uint8), "RGBA").save(OUT)
    if check:
        chk = Image.alpha_composite(art, Image.fromarray(oa.astype(np.uint8), "RGBA")).crop((x0 - 30, y0 - 30, x1 + 30, y1 + 30))
        bg = Image.new("RGBA", chk.size, (90, 90, 90, 255))
        bg.alpha_composite(chk)
        bg.resize((chk.width * 6, chk.height * 6), Image.NEAREST).save(check)
    print("overlay", OUT)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
