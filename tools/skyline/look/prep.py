"""Write Skyline Repo's look textures from the downloads (tools/skyline/look/fetch.py), with Visiting Hours' own
library recipe (tools/look/prep-library.py, imported and called, never edited).

    uv run --with numpy --with pillow python -I tools/skyline/look/prep.py out/skyline-look-src Assets/JamKit/Templates/SkylineRepo/ThirdParty

Per texture set <Name> (sources.json "textures"), in <third-party>/Library/<Name>/:
  <Name>_Albedo.jpg  2K, prep-library's stylize (a 3 px median blended in, colour pushed), plus this file's recolours:
                     "near_white" (prep-library's tint recipe: the material colour is the paint) and "terrazzo_white"
                     (the brown terrazzo turned into CONCIERGE's white terrazzo, its chips kept dark and warm).
  <Name>_Normal.jpg  2K, OpenGL (Unity's convention).
  <Name>_Mask.png    1K RGBA, URP Lit's layout: R metallic, G occlusion, B height, A smoothness.
<third-party>/Library/SK_MacroVariation.png: prep-library's tileable low-frequency noise (the detail map that breaks repeats).
Per model <Name> (sources.json "models"), in <third-party>/Props/<Name>/: <Name>.fbx, <Name>_Albedo.png (RGBA: the alpha map
in A, for the leaves' alpha clip), <Name>_Normal.jpg, <Name>_Mask.png (same packing).
"""
import importlib.util
import json
import os
import shutil
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))


def vh_prep():
    path = os.path.join(REPO, "tools", "look", "prep-library.py")
    spec = importlib.util.spec_from_file_location("vh_prep_library", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def terrazzo_white(img):
    """Brown terrazzo -> a polished white terrazzo: the binder near-white and warm, the chips kept as darker warm-grey and
    a few gold-brown flecks (hue kept at a quarter), so the floor reads as stone, not paint."""
    a = np.asarray(img).astype(np.float32) / 255.0
    lum = a @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    m = float(lum.mean())
    # Chips are the darker-than-binder pixels; push them apart from the binder.
    dev = (lum - m) * 1.9
    base = np.clip(0.85 + dev, 0.18, 1.0)
    grey = np.repeat(base[..., None], 3, axis=2)
    hue = a / max(m, 1e-3) * 0.85
    out = grey * 0.8 + np.clip(hue, 0, 1.2) * 0.2
    out = out * np.array([1.0, 0.985, 0.95], dtype=np.float32)
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def textures(vh, src_root, lib_root, spec):
    os.makedirs(lib_root, exist_ok=True)
    for name, s in spec.items():
        d = os.path.join(src_root, name)
        od = os.path.join(lib_root, name)
        os.makedirs(od, exist_ok=True)
        if s.get("grade"):
            vh.GRADE[name] = tuple(s["grade"])
        if s.get("recolor") == "near_white":
            vh.TINT.add(name)
        alb = vh.stylize(vh.load(os.path.join(d, "color.jpg"), "RGB"), name)
        if s.get("recolor") == "terrazzo_white":
            alb = terrazzo_white(alb)
        alb.save(os.path.join(od, name + "_Albedo.jpg"), quality=90, optimize=True)
        nrm = vh.load(os.path.join(d, "normal.png"), "RGB")
        if nrm.size != (2048, 2048):
            nrm = nrm.resize((2048, 2048), Image.LANCZOS)
        nrm.save(os.path.join(od, name + "_Normal.jpg"), quality=93, optimize=True)
        vh.mask_for(d, "polyhaven").save(os.path.join(od, name + "_Mask.png"), optimize=True)
        print("ok", name, flush=True)
    vh.macro_noise(os.path.join(lib_root, "SK_MacroVariation.png"))
    print("ok macro", flush=True)


def models(src_root, props_root, spec):
    for name, s in spec.items():
        d = os.path.join(src_root, "models", name)
        od = os.path.join(props_root, name)
        os.makedirs(od, exist_ok=True)
        shutil.copyfile(os.path.join(d, "model.fbx"), os.path.join(od, name + ".fbx"))
        col = Image.open(os.path.join(d, "color.jpg")).convert("RGB")
        alpha = os.path.join(d, "alpha.png")
        if os.path.exists(alpha):
            a = Image.open(alpha).convert("L").resize(col.size, Image.LANCZOS)
            rgba = col.copy()
            rgba.putalpha(a)
            rgba.save(os.path.join(od, name + "_Albedo.png"), optimize=True)
        else:
            col.save(os.path.join(od, name + "_Albedo.png"), optimize=True)
        nsrc = next(p for p in (os.path.join(d, "normal.jpg"), os.path.join(d, "normal.png")) if os.path.exists(p))
        Image.open(nsrc).convert("RGB").save(os.path.join(od, name + "_Normal.jpg"), quality=93, optimize=True)
        arm = np.asarray(Image.open(os.path.join(d, "arm.jpg")).convert("RGB")).astype(np.float32) / 255.0
        mask = np.stack([arm[..., 2], arm[..., 0], np.full(arm.shape[:2], 0.5, np.float32), 1.0 - arm[..., 1]], -1)
        Image.fromarray((mask * 255).astype(np.uint8), "RGBA").save(os.path.join(od, name + "_Mask.png"), optimize=True)
        print("ok model", name, flush=True)


def main():
    src_root, third = sys.argv[1], sys.argv[2]
    spec = json.load(open(os.path.join(HERE, "sources.json"), encoding="utf-8"))
    which = sys.argv[3] if len(sys.argv) > 3 else "all"
    vh = vh_prep()
    if which in ("all", "textures"):
        textures(vh, src_root, os.path.join(third, "Library"), spec["textures"])
    if which in ("all", "models"):
        models(src_root, os.path.join(third, "Props"), spec["models"])


if __name__ == "__main__":
    main()
