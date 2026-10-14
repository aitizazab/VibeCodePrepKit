"""Checks the afternoon grade's colour-by-owner rule without Unity (fix round 1, judge A 6).

    uv run --with numpy python -I tools/skyline/look/grade-check.py

A numpy copy of URP 17's LDR grade for ACES (LutBuilderHdr.shader + Color.hlsl's AcesTonemap, the fitted RRT/ODT): post
exposure, contrast in ACEScc, hue vs hue in the grading space (ACEScg HSV), saturation, then the tonemap. White balance,
split toning and the small shadows/highlights tints are left out (a degree or two). It prints the sRGB hue of the firm's
orange (the Breacher, 0.95 / 0.45 / 0.08) and CONCIERGE's gold (the bots, 0.86 / 0.66 / 0.22) at several light levels,
with and without the hue curve. The rule to keep: orange within hue 20-35 and gold within 40-50 where the sun hits them
(light 2.0-2.8; sunlit white terrazzo is ~2.2). Keep the numbers here in step with SkylineLook.Afternoon().
"""
import colorsys
import math

import numpy as np

EXPOSURE, CONTRAST, SATURATION = 0.1, 12.0, 6.0
# SkylineLook.HueVsHueKeys: (hue in degrees, shift in degrees), piecewise linear.
HUE_CURVE = [(0, 0), (10, -4), (22, -8), (32, -6), (40, -2), (48, -2), (60, 0)]


def M(*v):
    return np.array(v, dtype=float).reshape(3, 3)


SRGB_2_AP0 = M(0.4397010, 0.3829780, 0.1773350, 0.0897923, 0.8134230, 0.0967616, 0.0175440, 0.1115440, 0.8707040)
AP0_2_AP1 = M(1.4514393161, -0.2365107469, -0.2149285693, -0.0765537734, 1.1762296998, -0.0996759264, 0.0083161484, -0.0060324498, 0.9977163014)
AP1_2_AP0 = M(0.6954522414, 0.1406786965, 0.1638690622, 0.0447945634, 0.8596711185, 0.0955343182, -0.0055258826, 0.0040252103, 1.0015006723)
AP1_2_XYZ = M(0.6624541811, 0.1340042065, 0.1561876870, 0.2722287168, 0.6740817658, 0.0536895174, -0.0055746495, 0.0040607335, 1.0103391003)
XYZ_2_AP1 = M(1.6410233797, -0.3248032942, -0.2364246952, -0.6636628587, 1.6153315917, 0.0167563477, 0.0117218943, -0.0082844420, 0.9883948585)
XYZ_2_709 = M(3.2409699419, -1.5373831776, -0.4986107603, -0.9692436363, 1.8759675015, 0.0415550574, 0.0556300797, -0.2039769589, 1.0569715142)
D60_2_D65 = M(0.98722400, -0.00611327, 0.0159533, -0.00759836, 1.00186000, 0.0053302, 0.00307257, -0.00509595, 1.0816800)
AP1_Y = np.array([0.272229, 0.674082, 0.0536895])


def _sat(c):
    return (max(max(c), 1e-4) - max(min(c), 1e-4)) / max(max(c), 1e-2)


def _yc(c):
    r, g, b = c
    k = max(b * (b - g) + g * (g - r) + r * (r - b), 0.0)
    return (b + g + r + 1.75 * math.sqrt(k)) / 3


def _hue(c):
    r, g, b = c
    if r == g == b:
        return 0.0
    h = math.degrees(math.atan2(math.sqrt(3) * (g - b), 2 * r - g - b))
    return h + 360 if h < 0 else h


def _sigmoid(x):
    t = max(1 - abs(x / 2), 0)
    return (1 + np.sign(x) * (1 - t * t)) / 2


def aces(a):
    """Color.hlsl AcesTonemap (the fitted version URP uses), input ACES2065-1, output linear Rec.709."""
    s = _sat(a)
    y = _yc(a)
    gain = 0.05 * _sigmoid((s - 0.4) / 0.2)
    glow = gain if y <= 2 / 3 * 0.08 else 0.0 if y >= 0.16 else gain * (0.08 / y - 0.5)
    a = a * (1 + glow)
    h = _hue(a)
    ch = h if h <= 180 else h - 360
    w = np.clip(1 - abs(2 * ch / 135), 0, 1)
    w = w * w * (3 - 2 * w)
    w *= w
    a = a.copy()
    a[0] += w * s * (0.03 - a[0]) * (1 - 0.82)
    cg = np.maximum(0, AP0_2_AP1 @ a)
    cg = np.dot(cg, AP1_Y) + (cg - np.dot(cg, AP1_Y)) * 0.96
    p = (cg * (cg + 0.0245786) - 0.000090537) / (cg * (0.983729 * cg + 0.4329510) + 0.238081)
    X = AP1_2_XYZ @ p
    sm = max(X.sum(), 1e-4)
    x, yy, Y = X[0] / sm, X[1] / sm, max(X[1], 0) ** 0.9811
    m = Y / max(yy, 1e-4)
    lin = XYZ_2_AP1 @ np.array([x * m, Y, (1 - x - yy) * m])
    lin = np.dot(lin, AP1_Y) + (lin - np.dot(lin, AP1_Y)) * 0.93
    return XYZ_2_709 @ (D60_2_D65 @ (AP1_2_XYZ @ lin))


def grade(c, curve):
    c = np.asarray(c, float) * 2 ** EXPOSURE
    mid = 0.4135884
    cc = (np.log2(np.maximum(SRGB_2_AP0 @ c, 1e-10)) + 9.72) / 17.52
    cc = (cc - mid) * (1 + CONTRAST / 100) + mid
    cg = np.maximum(AP0_2_AP1 @ (2 ** (cc * 17.52 - 9.72)), 0)
    if curve:
        top = max(cg.max(), 1e-9)
        h, s, v = colorsys.rgb_to_hsv(*(cg / top))
        d = h * 360
        d = (d + float(np.interp(d, [p[0] for p in HUE_CURVE], [p[1] for p in HUE_CURVE], left=0, right=0))) % 360
        cg = np.array(colorsys.hsv_to_rgb(d / 360, s, v)) * top
    luma = np.dot(cg, AP1_Y)
    cg = luma + (cg - luma) * (1 + SATURATION / 100)
    return aces(AP1_2_AP0 @ np.maximum(cg, 0))


def srgb_hue(lin):
    c = np.clip(lin, 0, 1)
    s = np.where(c <= 0.0031308, 12.92 * c, 1.055 * c ** (1 / 2.4) - 0.055)
    h, sat, v = colorsys.rgb_to_hsv(*s)
    return h * 360, sat, v


def to_linear(srgb):
    s = np.asarray(srgb, float)
    return np.where(s <= 0.04045, s / 12.92, ((s + 0.055) / 1.055) ** 2.4)


def main():
    swatches = {"orange": to_linear([0.95, 0.45, 0.08]), "gold": to_linear([0.86, 0.66, 0.22]), "white": np.full(3, 0.8)}
    ok = True
    for curve in (False, True):
        print("hue curve", "on" if curve else "off")
        for light in (0.6, 1.2, 2.0, 2.8):
            row = []
            for name, c in swatches.items():
                h, s, v = srgb_hue(grade(c * light, curve))
                row.append("%s hue %5.1f sat %.2f val %.2f" % (name, h, s, v))
                if curve and light >= 2.0:
                    if name == "orange" and not 20 <= h <= 35.5:
                        ok = False
                    if name == "gold" and not 40 <= h <= 50:
                        ok = False
            print("  light %.1f: %s" % (light, " | ".join(row)))
    print("RULE", "OK" if ok else "BROKEN", "(orange 20-35, gold 40-50 in the sun, with the curve)")


if __name__ == "__main__":
    main()
