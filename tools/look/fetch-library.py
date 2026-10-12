"""Download the Look library's source textures (2K) listed in library-sources.json, in parallel.

    uv run python -I tools/look/fetch-library.py <download-dir>

Poly Haven: the colour (jpg), OpenGL normal (png), AO/rough/metal (arm jpg) and, for terrain sets, the height (jpg).
ambientCG: the 2K-JPG zip; only the image files are extracted (by name, into a fresh folder per set). Nothing from a
download is ever run.
"""
import concurrent.futures as cf
import json
import os
import sys
import urllib.request
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
UA = {"User-Agent": "VibeJamKit-look/1.0"}


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


def jobs_for(name, spec, root):
    d = os.path.join(root, name)
    os.makedirs(d, exist_ok=True)
    if spec["src"] == "polyhaven":
        req = urllib.request.Request("https://api.polyhaven.com/files/" + spec["id"], headers=UA)
        files = json.load(urllib.request.urlopen(req, timeout=60))
        want = [("Diffuse", "jpg", "color.jpg"), ("nor_gl", "png", "normal.png"), ("arm", "jpg", "arm.jpg")]
        if spec.get("height"):
            want.append(("Displacement", "jpg", "height.jpg"))
        return [(files[k]["2k"][fmt]["url"], os.path.join(d, out)) for k, fmt, out in want]
    return [("https://ambientcg.com/get?file=%s_2K-JPG.zip" % spec["id"], os.path.join(d, "set.zip"))]


def unzip(path):
    d = os.path.dirname(path)
    with zipfile.ZipFile(path) as z:
        for n in z.namelist():
            base = os.path.basename(n)
            if not base.lower().endswith((".jpg", ".png")) or "/" in n.strip("/"):
                continue
            with z.open(n) as src, open(os.path.join(d, base), "wb") as dst:
                dst.write(src.read())


def main():
    root = sys.argv[1]
    os.makedirs(root, exist_ok=True)
    lib = json.load(open(os.path.join(HERE, "library-sources.json"), encoding="utf-8"))
    jobs = []
    for name, spec in lib.items():
        if name.startswith("_"):
            continue
        jobs += jobs_for(name, spec, root)
    with cf.ThreadPoolExecutor(12) as ex:
        futs = {ex.submit(get, u, p): p for u, p in jobs}
        for f in cf.as_completed(futs):
            p = futs[f]
            try:
                f.result()
                if p.endswith(".zip"):
                    unzip(p)
                print("ok", p, flush=True)
            except Exception as e:  # noqa: BLE001
                print("FAIL", p, e, flush=True)


if __name__ == "__main__":
    main()
