"""Checks the Look's textures and material tints against the target palette in docs/redesign/look.md ("The palette").

Round 2's judge found an autumn palette nobody had measured (the leaf atlas averaged hue 65 degrees: yellow). This
averages the opaque pixels of every atlas cell and every library albedo that feeds a palette entry, applies the material
tint the C# sets (VhLookLibrary / VhLookGround; keep TINTS in step with them), and compares the result with the targets:
hue window (degrees), saturation window, value window, plus the share of "autumn" pixels (hue < 60 and saturation > 0.35)
in the foliage. Exit 1 if anything is outside its window.

    uv run --with numpy --with pillow python -I tools/look/palette-check.py
"""
import colorsys
import os
import sys

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
VH = os.path.join(REPO, "Assets", "JamKit", "Templates", "VisitingHours")
TEX = os.path.join(VH, "Look", "Textures")
LIB = os.path.join(VH, "ThirdParty", "LookLibrary")

# Material tints applied in C#. Material.SetColor takes sRGB and Unity converts it to linear (a Lit/_BaseColor tint of 0.62
# multiplies by 0.34); terrain layers' diffuseRemapMax is a Vector4, used as a linear multiplier. SRGB_TINTS lists the
# former; they're converted before being applied in linear space.
SRGB_TINTS = {"foliage", "grass", "concrete", "asphalt"}
TINTS = {
    "foliage": (0.96, 1.0, 0.94),           # VhLookLibrary.BuildBarkAndFoliage: VHM_Foliage
    "grass": (0.96, 1.0, 0.94),             # VHM_GrassCards
    "lawn": (0.62, 0.95, 1.0),              # VhLookLibrary.Catalogue Grass_Lawn
    "drygrass": (1.0, 0.98, 0.84),          # VhLookGround.BuildTerrain: Grass_DirtMix "DryGold" variant
    "concrete": (0.97, 0.97, 0.97),
    "asphalt": (0.92, 0.92, 0.92),
    "farfields": (1.0, 1.0, 1.0),
}

# Targets: (hue min, hue max) degrees, (sat min, sat max), (value min, value max) of the tinted average, sRGB 0-1.
TARGETS = {
    "leaves (whole atlas)": ((92, 118), (0.4, 0.7), (0.22, 0.42)),
    "leaf cell 0 maple/elm": ((92, 118), (0.4, 0.7), (0.22, 0.42)),
    "leaf cell 1 oak": ((85, 115), (0.4, 0.7), (0.22, 0.42)),
    "leaf cell 2 birch": ((88, 118), (0.4, 0.7), (0.24, 0.46)),
    "leaf cell 3 shrub/hedge": ((95, 125), (0.4, 0.7), (0.2, 0.4)),
    "tree billboards": ((88, 118), (0.4, 0.7), (0.22, 0.45)),
    "grass cards cell 0 (green)": ((88, 112), (0.45, 0.7), (0.3, 0.5)),
    "grass cards cell 1 (dry tips)": ((70, 100), (0.4, 0.65), (0.35, 0.6)),
    "lawn albedo x tint": ((84, 104), (0.5, 0.75), (0.35, 0.5)),
    "dry grass albedo x tint": ((45, 62), (0.35, 0.6), (0.5, 0.7)),
    "concrete sidewalk x tint": ((20, 50), (0.0, 0.15), (0.6, 0.78)),
    "asphalt x tint": ((0, 360), (0.0, 0.1), (0.28, 0.4)),
    "far fields": ((70, 105), (0.35, 0.65), (0.35, 0.55)),
}
AUTUMN_MAX = 0.06   # share of foliage pixels that read as autumn (yellow/orange/brown leaves)


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.maximum(c, 0), 1 / 2.4) - 0.055)


def pixels(path, cell=None):
    im = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255.0
    if cell is not None:
        h, w = im.shape[:2]
        cx, cy = cell % 2, cell // 2
        # Cell row 0 is the image's bottom half (Unity's v = 0 at the bottom).
        im = im[(1 - cy) * h // 2:(2 - cy) * h // 2, cx * w // 2:(cx + 1) * w // 2]
    px = im.reshape(-1, 4)
    return px[px[:, 3] > 0.5][:, :3]


def tinted_average(px, tint, srgb_tint=False):
    t = np.array(tint, np.float64)
    if srgb_tint:
        t = srgb_to_lin(t)
    lin = srgb_to_lin(px).mean(axis=0) * t
    return lin_to_srgb(np.clip(lin, 0, 1))


def hsv(c):
    h, s, v = colorsys.rgb_to_hsv(*[float(x) for x in c])
    return h * 360.0, s, v


def autumn_share(px):
    out = np.array([colorsys.rgb_to_hsv(*p) for p in px[:: max(1, len(px) // 20000)]])
    return float(np.mean((out[:, 0] * 360 < 60) & (out[:, 1] > 0.35)))


def main():
    rows = []
    leaf = os.path.join(TEX, "VH_LeafClusters.png")
    rows.append(("leaves (whole atlas)", pixels(leaf), "foliage"))
    for i, n in enumerate(["leaf cell 0 maple/elm", "leaf cell 1 oak", "leaf cell 2 birch", "leaf cell 3 shrub/hedge"]):
        rows.append((n, pixels(leaf, i), "foliage"))
    rows.append(("tree billboards", pixels(os.path.join(TEX, "VH_TreeBillboards.png")), "foliage"))
    grass = os.path.join(TEX, "VH_GrassCards.png")
    rows.append(("grass cards cell 0 (green)", pixels(grass, 0), "grass"))
    rows.append(("grass cards cell 1 (dry tips)", pixels(grass, 1), "grass"))
    rows.append(("lawn albedo x tint", pixels(os.path.join(LIB, "Grass_Lawn", "Grass_Lawn_Albedo.jpg")), "lawn"))
    rows.append(("dry grass albedo x tint", pixels(os.path.join(LIB, "Grass_DirtMix", "Grass_DirtMix_Albedo.jpg")), "drygrass"))
    rows.append(("concrete sidewalk x tint", pixels(os.path.join(LIB, "Concrete_Sidewalk", "Concrete_Sidewalk_Albedo.jpg")), "concrete"))
    rows.append(("asphalt x tint", pixels(os.path.join(LIB, "Asphalt", "Asphalt_Albedo.jpg")), "asphalt"))
    rows.append(("far fields", pixels(os.path.join(TEX, "VH_FarFields.png")), "farfields"))
    bad = 0
    print("%-32s %-16s %7s %5s %5s  %s" % ("entry", "avg sRGB", "hue", "sat", "val", "target"))
    for name, px, key in rows:
        avg = tinted_average(px, TINTS[key], key in SRGB_TINTS)
        h, s, v = hsv(avg)
        (h0, h1), (s0, s1), (v0, v1) = TARGETS[name]
        ok = h0 <= h <= h1 and s0 <= s <= s1 and v0 <= v <= v1
        bad += 0 if ok else 1
        print("%-32s %-16s %7.1f %5.2f %5.2f  hue %d-%d sat %.2f-%.2f val %.2f-%.2f %s" % (
            name, tuple(int(round(x * 255)) for x in avg), h, s, v, h0, h1, s0, s1, v0, v1, "ok" if ok else "OUT"))
    share = autumn_share(pixels(leaf))
    ok = share <= AUTUMN_MAX
    bad += 0 if ok else 1
    print("autumn-coloured leaf pixels: %.1f%% (max %.0f%%) %s" % (share * 100, AUTUMN_MAX * 100, "ok" if ok else "OUT"))
    for name in ["VH_LeafClusters.png", "VH_TreeBillboards.png", "VH_GrassCards.png"]:
        a = np.asarray(Image.open(os.path.join(TEX, name)).convert("RGBA"))[..., 3]
        print("opaque coverage %-22s %.0f%%" % (name, 100 * float(np.mean(a > 115))))
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
