"""Write the Look library's game textures from the downloaded sources (tools/look/fetch-library.py).

    uv run --with numpy --with pillow python -I tools/look/prep-library.py <download-dir> <out-dir>

Per set <Name> (in <out-dir>/<Name>/):
  <Name>_Albedo.jpg  2K. Stylized a touch: fine noise simplified (a 3 px median blended in), colour enriched. Sets marked
                     "tint" are made near-white so the material's base colour is the paint colour.
  <Name>_Normal.jpg  2K, OpenGL convention (Unity's).
  <Name>_Mask.png    1K RGBA in URP Lit's layout: R metallic, G occlusion, B height (terrain layers), A smoothness.
                     URP Lit reads metallic (R) and smoothness (A) from it as the metallic map, and occlusion (G) from it
                     as the occlusion map; a TerrainLayer reads it as its mask map.
Plus <out-dir>/VH_MacroVariation.png: a tileable low-frequency value noise (the materials' detail map, breaking repeats).
"""
import glob
import json
import os
import sys

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
TINT = {"Clapboard_Painted", "Plaster_Painted", "Stucco", "Plaster_Interior"}
# Per-set colour push: (saturation, brightness).
GRADE = {
    "Grass_Lawn": (1.18, 1.05), "Grass_DirtMix": (1.12, 1.03), "Brick_Red": (1.12, 1.0), "Siding_WoodRed": (1.1, 1.0),
    "Tree_Bark": (1.05, 0.95), "Asphalt": (0.9, 1.0), "Road_LaneLine": (0.9, 1.0),
    "Roof_ShinglesAsphalt": (0.85, 1.55), "Gravel": (0.85, 0.8),
}


def find(d, *keys):
    for k in keys:
        hits = sorted(glob.glob(os.path.join(d, "*" + k + "*")))
        hits = [h for h in hits if not h.endswith(".zip")]
        if hits:
            return hits[0]
    return None


def load(path, mode):
    return Image.open(path).convert(mode)


def stylize(img, name):
    img = img.resize((2048, 2048), Image.LANCZOS) if img.size != (2048, 2048) else img
    med = img.filter(ImageFilter.MedianFilter(3))
    img = Image.blend(img, med, 0.45)
    sat, bri = GRADE.get(name, (1.08, 1.0))
    img = ImageEnhance.Color(img).enhance(sat)
    if bri != 1.0:
        img = ImageEnhance.Brightness(img).enhance(bri)
    if name in TINT:
        a = np.asarray(img).astype(np.float32) / 255.0
        lum = a @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        # Keep the variation, centre it on a light grey, drop most of the hue (the material tint is the paint).
        lum = 0.86 + (lum - lum.mean()) * 0.9
        grey = np.repeat(lum[..., None], 3, axis=2)
        a = grey * 0.85 + a / max(a.mean(), 1e-3) * 0.86 * 0.15
        img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return img


def mask_for(d, src, size=1024):
    def ch(path, default):
        if path is None:
            return np.full((size, size), default, np.float32)
        return np.asarray(load(path, "L").resize((size, size), Image.LANCZOS)).astype(np.float32) / 255.0

    if src == "polyhaven":
        arm = np.asarray(load(os.path.join(d, "arm.jpg"), "RGB").resize((size, size), Image.LANCZOS)).astype(np.float32) / 255.0
        ao, rough, metal = arm[..., 0], arm[..., 1], arm[..., 2]
        height = ch(find(d, "height"), 0.5)
    else:
        ao = ch(find(d, "AmbientOcclusion"), 1.0)
        rough = ch(find(d, "Roughness"), 0.7)
        metal = ch(find(d, "Metalness"), 0.0)
        height = ch(find(d, "Displacement"), 0.5)
    # Stylized: AO a touch softer (SSAO and the probes add their own), roughness slightly evened out.
    ao = 1.0 - (1.0 - ao) * 0.8
    smooth = 1.0 - (rough * 0.85 + rough.mean() * 0.15)
    out = np.stack([metal, ao, height, smooth], axis=-1)
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")


def macro_noise(path, size=512, seed=7):
    rng = np.random.default_rng(seed)
    acc = np.zeros((size, size), np.float32)
    for octave, amp in ((4, 1.0), (8, 0.5), (16, 0.25), (32, 0.12)):
        f = np.fft.fftfreq(size)[:, None] ** 2 + np.fft.fftfreq(size)[None, :] ** 2
        noise = np.fft.fft2(rng.standard_normal((size, size)))
        filt = np.exp(-f * (size / octave) ** 2 * 6.0)
        layer = np.real(np.fft.ifft2(noise * filt))   # periodic, so it tiles
        acc += amp * layer / (layer.std() + 1e-6)
    acc = acc / (acc.std() + 1e-6)
    # Unity's detail albedo multiplies by 2x the map: 0.5 = no change. Keep the swing gentle (about +-12%).
    img = np.clip(0.5 + acc * 0.06, 0, 1)
    Image.fromarray((img * 255).astype(np.uint8), "L").convert("RGB").save(path)


def main():
    src_root, out_root = sys.argv[1], sys.argv[2]
    lib = json.load(open(os.path.join(HERE, "library-sources.json"), encoding="utf-8"))
    os.makedirs(out_root, exist_ok=True)
    for name, spec in lib.items():
        if name.startswith("_"):
            continue
        d = os.path.join(src_root, name)
        od = os.path.join(out_root, name)
        os.makedirs(od, exist_ok=True)
        if spec["src"] == "polyhaven":
            color, normal = os.path.join(d, "color.jpg"), os.path.join(d, "normal.png")
        else:
            color, normal = find(d, "_Color"), find(d, "NormalGL")
        alb = stylize(load(color, "RGB"), name)
        alb.save(os.path.join(od, name + "_Albedo.jpg"), quality=90, optimize=True)
        nrm = load(normal, "RGB")
        if nrm.size != (2048, 2048):
            nrm = nrm.resize((2048, 2048), Image.LANCZOS)
        nrm.save(os.path.join(od, name + "_Normal.jpg"), quality=93, optimize=True)
        mask_for(d, spec["src"]).save(os.path.join(od, name + "_Mask.png"), optimize=True)
        print("ok", name, flush=True)
    macro_noise(os.path.join(out_root, "VH_MacroVariation.png"))
    print("ok macro")


if __name__ == "__main__":
    main()
