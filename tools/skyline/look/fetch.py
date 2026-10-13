"""Download Skyline Repo's look sources (Task 21) listed in sources.json: Poly Haven only (CC0).

    uv run python -I tools/skyline/look/fetch.py out/skyline-look-src

Textures (2K): colour (jpg), OpenGL normal (png), AO/rough/metal (arm jpg) into <dir>/<Name>/ with the names
tools/look/prep-library.py reads (color.jpg, normal.png, arm.jpg).
The HDRI (4K .hdr) into <dir>/hdri/<id>_<res>.hdr.
Models (1K): the FBX and its diffuse / OpenGL normal / arm / alpha maps into <dir>/models/<Name>/ (model.fbx, color.jpg,
normal.jpg|png, arm.jpg, alpha.png). Nothing from a download is ever run. Re-running skips files already there.
"""
import concurrent.futures as cf
import json
import os
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
UA = {"User-Agent": "VibeJamKit-skyline-look/1.0"}


def api(asset_id):
    req = urllib.request.Request("https://api.polyhaven.com/files/" + asset_id, headers=UA)
    return json.load(urllib.request.urlopen(req, timeout=60))


def get(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=600) as r, open(path + ".part", "wb") as f:
        while True:
            b = r.read(1 << 16)
            if not b:
                break
            f.write(b)
    os.replace(path + ".part", path)
    return path


def first(entry, formats):
    for fmt in formats:
        if fmt in entry:
            return entry[fmt]["url"], fmt
    raise KeyError("none of %s in %s" % (formats, list(entry.keys())))


def jobs(root, src):
    out = []
    for name, spec in src["textures"].items():
        d = os.path.join(root, name)
        os.makedirs(d, exist_ok=True)
        files = api(spec["id"])
        for key, fmts, fname in (("Diffuse", ["jpg"], "color.jpg"), ("nor_gl", ["png", "jpg"], "normal.png"), ("arm", ["jpg"], "arm.jpg")):
            url, _ = first(files[key]["2k"], fmts)
            out.append((url, os.path.join(d, fname)))
    hd = os.path.join(root, "hdri")
    os.makedirs(hd, exist_ok=True)
    for name, spec in src["hdri"].items():
        files = api(spec["id"])
        url = files["hdri"][spec["res"]]["hdr"]["url"]
        out.append((url, os.path.join(hd, "%s_%s.hdr" % (spec["id"], spec["res"]))))
    for name, spec in src["models"].items():
        d = os.path.join(root, "models", name)
        os.makedirs(d, exist_ok=True)
        files = api(spec["id"])
        res = spec["res"]
        out.append((files["fbx"][res]["fbx"]["url"], os.path.join(d, "model.fbx")))
        for key, fmts, base in (("Diffuse", ["jpg", "png"], "color"), ("nor_gl", ["jpg", "png"], "normal"), ("arm", ["jpg", "png"], "arm"),
                                ("Alpha", ["png", "jpg"], "alpha")):
            if key not in files:
                continue
            url, fmt = first(files[key][res], fmts)
            out.append((url, os.path.join(d, base + "." + fmt)))
    return out


def main():
    root = sys.argv[1]
    os.makedirs(root, exist_ok=True)
    src = json.load(open(os.path.join(HERE, "sources.json"), encoding="utf-8"))
    todo = jobs(root, src)
    bad = 0
    with cf.ThreadPoolExecutor(8) as ex:
        futs = {ex.submit(get, u, p): p for u, p in todo}
        for f in cf.as_completed(futs):
            p = futs[f]
            try:
                f.result()
                print("ok", p, os.path.getsize(p), flush=True)
            except Exception as e:  # noqa: BLE001
                bad += 1
                print("FAIL", p, e, flush=True)
    print("done", len(todo) - bad, "of", len(todo))
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
