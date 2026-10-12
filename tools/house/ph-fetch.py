"""Fetch Poly Haven (CC0) models at 1k into Dream/House/Models and add them to house-assets.json (House round 2).

    uv run python -I tools/house/ph-fetch.py id=role[,alt_role...] [id=role ...]

For each model: the 1k FBX and the textures its FBX includes (the paths it expects, under <id>/textures/), then a
manifest entry like the existing ones (dims_m from Poly Haven's info: Blender X, Y, Z mm -> Unity X, Z, Y metres; tris;
textures; materials grouped by the material key in the file names; authors; role and alt_roles). Skips files already
there. Appends to LICENSES.md. Never writes .meta files (Unity makes them).
"""
import json
import os
import sys
import urllib.request

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
HOUSE = os.path.join(REPO, "Assets", "JamKit", "Templates", "VisitingHours", "Dream", "House")
API = "https://api.polyhaven.com"
UA = {"User-Agent": "VibeJamKit-house-fetch/1.0"}


def get_json(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))


def download(url, dst):
    if os.path.exists(dst) and os.path.getsize(dst) > 0:
        return False
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
        data = r.read()
    if not data:
        raise RuntimeError("empty body: " + url)
    with open(dst, "wb") as f:
        f.write(data)
    return True


def material_key(model_id, fname):
    # textures/<id>[_<material>]_<map>_1k.<ext>
    stem = os.path.splitext(os.path.basename(fname))[0]
    if stem.endswith("_1k"):
        stem = stem[:-3]
    rest = stem[len(model_id):].lstrip("_") if stem.startswith(model_id) else stem
    maps = ["diff", "nor_gl", "nor_dx", "rough", "roughness", "metal", "metallic", "ao", "arm", "alpha", "opacity", "disp", "spec"]
    for m in sorted(maps, key=len, reverse=True):
        if rest == m:
            return "_", m
        if rest.endswith("_" + m):
            return rest[: -len(m) - 1], m
    return "_", rest


def main(args):
    manifest_path = os.path.join(HOUSE, "house-assets.json")
    manifest = json.load(open(manifest_path, encoding="utf-8"))
    have = {m["id"]: m for m in manifest["models"]}
    lic = []
    for arg in args:
        mid, roles = arg.split("=", 1)
        roles = roles.split(",")
        info = get_json(f"{API}/info/{mid}")
        files = get_json(f"{API}/files/{mid}")
        fbx = files["fbx"]["1k"]["fbx"]
        base = os.path.join(HOUSE, "Models", mid)
        fbx_rel = f"Models/{mid}/{mid}_1k.fbx"
        got = download(fbx["url"], os.path.join(HOUSE, fbx_rel.replace("/", os.sep)))
        textures, materials = [], {}
        for rel, inc in fbx.get("include", {}).items():
            if rel.lower().endswith(".exr") and "nor_dx" in rel:
                continue
            download(inc["url"], os.path.join(base, rel.replace("/", os.sep)))
            trel = f"Models/{mid}/{rel}"
            textures.append(trel)
            key, mp = material_key(mid, rel)
            materials.setdefault(key, {})["nor_gl" if mp == "nor_gl" else mp] = trel
        d = info.get("dimensions") or [0, 0, 0]
        entry = {
            "id": mid, "role": roles[0], "name": info.get("name", mid), "source": "polyhaven",
            "url": f"https://polyhaven.com/a/{mid}", "fbx": fbx_rel,
            "dims_m": [round(d[0] / 1000.0, 3), round(d[2] / 1000.0, 3), round(d[1] / 1000.0, 3)],
            "tris": info.get("polycount", 0), "textures": sorted(textures), "materials": materials,
            "authors": sorted((info.get("authors") or {}).keys()),
        }
        if len(roles) > 1:
            entry["alt_roles"] = roles[1:]
        entry["note"] = "House round 2 (2026-10-13)."
        if mid in have:
            have[mid].update(entry)
        else:
            manifest["models"].append(entry)
            have[mid] = entry
        lic.append(f"- {info.get('name', mid)} (`{mid}`), Poly Haven, CC0, by {', '.join(entry['authors'])}: https://polyhaven.com/a/{mid}")
        print(("fetched " if got else "had ") + mid, entry["dims_m"], "->", ", ".join(roles))
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=1, ensure_ascii=False)
        f.write("\n")
    lic_path = os.path.join(HOUSE, "LICENSES.md")
    text = open(lic_path, encoding="utf-8").read()
    add = [l for l in lic if l.split("`")[1] not in text]
    if add:
        with open(lic_path, "a", encoding="utf-8", newline="\n") as f:
            f.write("\n## House round 2 (2026-10-13)\n\n" + "\n".join(add) + "\n")


if __name__ == "__main__":
    main(sys.argv[1:])
