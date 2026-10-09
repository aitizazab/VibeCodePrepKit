# Builds docs/art/vh-review.json (for FableAnimationTests/art-tools/review-page.mjs) from docs/art/vh-picks.json:
# each output's chosen take, its alternates and rejects, grouped into characters / props / environment / UI.
#   uv run python tools/art/vh_review_spec.py
import json, os, glob, re
HERE = os.path.dirname(os.path.abspath(__file__)); VJ = os.path.normpath(os.path.join(HERE, "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
P = json.load(open(os.path.join(VJ, "docs", "art", "vh-picks.json"), encoding="utf8"))
meta = P.get("_review", {})
SECTIONS = [("Characters", "characters"), ("Props (journal items and in-world)", "props"), ("Environment", "environment"), ("UI and effects", "ui")]
sections = []
final = os.path.join(VJ, "docs", "art", "vh-final-sheet.jpg")
if os.path.exists(final):
    sections.append({"title": "The processed files (cut out, tiled), over a checkerboard", "cols": 1, "items": [
        {"file": final, "label": "Everything in Assets/JamKit/Templates/VisitingHours/Art/", "verdict": "pick"}]})
for title, key in SECTIONS:
    items = []
    for name, p in P.items():
        if name.startswith("_") or p.get("section") != key: continue
        stem = re.sub(r"-take\d+\.\w+$", "", p["src"])
        takes = sorted(glob.glob(os.path.join(INC, stem + "-take*.*")), key=lambda f: int(re.search(r"take(\d+)", f).group(1)))
        takes = [f for f in takes if re.search(r"-take\d+\.(jpg|png)$", f)] + [os.path.join(INC, a) for a in p.get("also", [])]
        rej = p.get("rejects", {})
        for f in takes:
            b = os.path.basename(f); t = re.search(r"(take\d+)", b).group(1)
            if b == p["src"]: items.append({"file": f, "label": f"{name}  ({t})", "verdict": "pick", "note": p.get("note", "")})
            elif b in rej: items.append({"file": f, "label": f"{name}  ({b[3:-4]})", "verdict": "reject", "note": rej[b]})
            elif t in rej: items.append({"file": f, "label": f"{name}  ({t})", "verdict": "reject", "note": rej[t]})
            else: items.append({"file": f, "label": f"{name}  ({t})", "verdict": "alt"})
    sections.append({"title": title, "cols": 3, "items": items})
spec = {"name": "visiting-hours-art", "title": "Visiting Hours: the slice art",
        "subtitle": meta.get("subtitle", ""), "questions": meta.get("questions", []), "notes": meta.get("notes", []), "sections": sections}
dst = os.path.join(VJ, "docs", "art", "vh-review.json")
json.dump(spec, open(dst, "w", encoding="utf8"), indent=1); print(dst, sum(len(s["items"]) for s in sections), "tiles")
