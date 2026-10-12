"""One character, end to end (docs/redesign/people.md "The pipeline"). Config: tools/tripo/people/<Name>.json.

    uv run --with pillow --with numpy python tools/tripo/vh_make.py prep   <Name>   # recolour, back fixes, face, build spec
    uv run --with pillow --with numpy python tools/tripo/vh_make.py face   <Name>   # only the face step again (after prep)
    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender> -b --factory-startup -P tools/tripo/vh_blender.py -- build <spec>
    uv run --with pillow --with numpy python tools/tripo/vh_make.py finish <Name>   # textures + sidecar into Models/<Name>/, preview

prep prints the exact Blender command. Sources (Tripo's FBX exports and their dumps) live in
FableAnimationTests/.work/people-src/<src>/ (not in git: re-export them from Tripo's Assets panel for free).
"""
import glob
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(os.path.dirname(REPO), "FableAnimationTests", ".work", "people-src")
MODELS = os.path.join(REPO, "Assets", "JamKit", "Templates", "VisitingHours", "Models")
BLENDER = os.path.join(os.path.dirname(REPO), "FableAnimationTests", "BasilDownfall", "tools", "blender", "blender.exe")
PY = ["uv", "run", "--with", "pillow", "--with", "numpy", "python"]


def cfg(name):
    c = json.load(open(os.path.join(HERE, "people", name + ".json"), encoding="utf8"))
    src = os.path.join(SRC, c["src"])
    c["_fbx"] = glob.glob(os.path.join(src, "*.fbx"))[0]
    fbm = glob.glob(os.path.join(src, "*.fbm"))[0]
    c["_tex"] = {"Color": os.path.join(fbm, "Color.jpg"), "Normal": os.path.join(fbm, "Normal.png"),
                 "roughness": glob.glob(os.path.join(fbm, "*roughness*"))[0], "metallic": glob.glob(os.path.join(fbm, "*metallic*"))[0]}
    c["_dump"] = os.path.join(src, "dump.npz")
    c["_work"] = os.path.join(SRC, "out", name)
    os.makedirs(c["_work"], exist_ok=True)
    return c


def run(args):
    print(">", " ".join(args[:6]), "...")
    subprocess.run(args, check=True)


def prep(name):
    c = cfg(name)
    w = c["_work"]
    color = c["_tex"]["Color"]
    rough = c["_tex"]["roughness"]
    spec = {"name": name, "fbx": c["_fbx"], "out": os.path.join(w, name + ".fbx"), "preview": os.path.join(w, "preview.npz"),
            "info": os.path.join(w, "build.json")}
    if c.get("alter"):
        a = dict(c["alter"])
        a.update({"npz": c["_dump"], "tex": color, "out": os.path.join(w, name + "_Color.jpg"),
                  "preview": os.path.join(w, "alter-front.png"), "rough_in": rough, "rough_out": os.path.join(w, name + "_roughness.jpg")})
        if a.get("vertex_moves") or a.get("back_of_head"):
            a["vertex_moves_out"] = os.path.join(w, "vertex_moves.npz")
            spec["vertex_moves"] = a["vertex_moves_out"]
        if a.get("back_of_head"):
            a["back_of_head"] = dict(a["back_of_head"], normal_in=c["_tex"]["Normal"], normal_out=os.path.join(w, name + "_Normal.png"))
        json.dump(a, open(os.path.join(w, "alter.json"), "w"), indent=1)
        run(PY + [os.path.join(HERE, "vh_alter.py"), os.path.join(w, "alter.json")])
        color = a["out"]
    # Keep the alter step's maps: the face step reads them and writes the final ones (so it can re-run alone).
    normal = os.path.join(w, name + "_Normal.png")
    shutil.copy2(color, os.path.join(w, name + "_Color_alter.jpg"))
    shutil.copy2(normal if os.path.exists(normal) else c["_tex"]["Normal"], os.path.join(w, name + "_Normal_alter.png"))
    if c.get("face"):
        spec["face"] = face_step(name, c)
    for k in ("proportions", "no_inherit", "delete", "accessories", "moves"):
        if c.get(k):
            spec[k] = c[k]
    path = os.path.join(w, "build-spec.json")
    json.dump(spec, open(path, "w"), indent=1)
    print("NEXT: node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- \"%s\" -b --factory-startup -P \"%s\" -- build \"%s\""
          % (BLENDER, os.path.join(HERE, "vh_blender.py"), path))


def face_step(name, c=None):
    """The hybrid cartoon face (vh_face.py): paints the model's own eyes, brows and lips out of the alter step's maps
    and draws the expression atlas. Returns the landmark block for the Blender build."""
    c = c or cfg(name)
    w = c["_work"]
    f = dict(c["face"])
    f.update({"npz": c["_dump"], "tex_in": os.path.join(w, name + "_Color_alter.jpg"), "tex": os.path.join(w, name + "_Color.jpg"),
              "normal_in": os.path.join(w, name + "_Normal_alter.png"), "normal": os.path.join(w, name + "_Normal.png"),
              "atlas": os.path.join(w, name + "_FaceAtlas.png"), "block": os.path.join(w, "face-block.json"),
              "check": os.path.join(w, "face-check.png"), "decal_exclude": os.path.join(w, "decal-exclude.npz"),
              "expressions": [e["name"] for e in (c.get("expressions") or DEFAULT_EXPRESSIONS)]})
    json.dump(f, open(os.path.join(w, "face.json"), "w"), indent=1)
    run(PY + [os.path.join(HERE, "vh_face.py"), os.path.join(w, "face.json")])
    return json.load(open(f["block"]))


def face_only(name):
    """Re-runs only the face step (after a prep) and rewrites the build spec's face block."""
    c = cfg(name)
    path = os.path.join(c["_work"], "build-spec.json")
    spec = json.load(open(path))
    spec["face"] = face_step(name, c)
    json.dump(spec, open(path, "w"), indent=1)
    print("NEXT: node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- \"%s\" -b --factory-startup -P \"%s\" -- build \"%s\""
          % (BLENDER, os.path.join(HERE, "vh_blender.py"), path))


def finish(name):
    c = cfg(name)
    w = c["_work"]
    dst = os.path.join(MODELS, name)
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(os.path.join(w, name + ".fbx"), os.path.join(dst, name + ".fbx"))
    altered = c.get("alter") is not None
    shutil.copy2(os.path.join(w, name + "_Color.jpg") if altered else c["_tex"]["Color"], os.path.join(dst, name + "_Color.jpg"))
    fixed_normal = os.path.join(w, name + "_Normal.png")
    shutil.copy2(fixed_normal if os.path.exists(fixed_normal) else c["_tex"]["Normal"], os.path.join(dst, name + "_Normal.png"))
    shutil.copy2(os.path.join(w, name + "_roughness.jpg") if altered else c["_tex"]["roughness"], os.path.join(dst, name + "_roughness.jpg"))
    shutil.copy2(c["_tex"]["metallic"], os.path.join(dst, name + "_metallic.jpg"))
    sidecar(name, c)
    print("Models/%s:" % name, sorted(os.listdir(dst)))
    run(PY + [os.path.join(HERE, "vh_preview.py"), os.path.join(w, "preview.npz"), os.path.join(dst, name + "_Color.jpg"),
              os.path.join(dst, name + "_FaceAtlas.png") if c.get("face") else "-", os.path.join(w, "faces.png")])
    run(PY + [os.path.join(HERE, "vh_preview.py"), "body", os.path.join(w, "preview.npz"), os.path.join(dst, name + "_Color.jpg"),
              os.path.join(w, "body.png")])


def sidecar(name, c=None):
    """Models/<Name>/<Name>.people.json: what VhPeopleBuild needs beyond the FBX (JsonUtility-friendly: lists, no maps)."""
    c = c or cfg(name)
    w = c["_work"]
    dst = os.path.join(MODELS, name)
    info = json.load(open(os.path.join(w, "build.json")))
    side = {"name": name, "height": c["height"], "idle": c.get("idle", "idle"), "colour": c.get("colour", ""),
            "note": c.get("note", ""), "hasFace": bool(c.get("face")), "decal": "", "atlas": "", "columns": 6,
            "rows": len(c.get("expressions") or DEFAULT_EXPRESSIONS),
            "blink": True, "expressions": [], "accessories": []}
    if c.get("face"):
        shutil.copy2(os.path.join(w, name + "_FaceAtlas.png"), os.path.join(dst, name + "_FaceAtlas.png"))
        side.update({"decal": info["decal"], "atlas": name + "_FaceAtlas.png", "blink": bool(c["face"].get("lids", True))})
        for e in c.get("expressions") or DEFAULT_EXPRESSIONS:
            side["expressions"].append({"name": e["name"], "shapes": [{"shape": k, "weight": v} for k, v in e["shapes"].items()]})
    for it in c.get("accessories", []):
        side["accessories"].append({"name": it["name"], "color": list(it.get("color", [0.5, 0.5, 0.5])),
                                    "smoothness": it.get("smoothness", 0.4), "metallic": it.get("metallic", 0.0)})
    json.dump(side, open(os.path.join(dst, name + ".people.json"), "w"), indent=1)


# Expressions: blendshape weights (0-100) per named expression; the first is the default. VhFace blends between them,
# and the decal atlas has one row per expression in this order (vh_face.py EXPR draws each). The keys move the face in
# 3D under the drawing: the cheeks and mouth corners (Smile / Frown), the brow ridge (Brows*), the jaw (MouthOpen).
DEFAULT_EXPRESSIONS = [
    {"name": "neutral", "shapes": {}},
    {"name": "warm", "shapes": {"Smile": 55}},
    {"name": "happy", "shapes": {"Smile": 75, "BrowsUp": 35}},
    {"name": "sad", "shapes": {"Frown": 80, "BrowsSad": 100}},
    {"name": "worried", "shapes": {"BrowsSad": 85, "Frown": 35, "MouthOpen": 6}},
    {"name": "hurt", "shapes": {"BrowsSad": 100, "Frown": 70, "MouthOpen": 12}},
    {"name": "angry", "shapes": {"BrowsDown": 100, "Frown": 55}},
    {"name": "surprised", "shapes": {"BrowsUp": 100, "MouthOpen": 35}},
    {"name": "doubtful", "shapes": {"BrowsDown": 30, "Frown": 25, "Smile": 25}},
]

if __name__ == "__main__":
    {"prep": prep, "face": face_only, "finish": finish, "sidecar": sidecar}[sys.argv[1]](sys.argv[2])
