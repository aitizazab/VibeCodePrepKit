# Builds a review spec (for FableAnimationTests/art-tools/review-page.mjs) from a picks file: sections of subjects,
# each with its chosen take and one alternate.
#   uv run python tools/art/vh_review_pairs.py [styles|sheets]    (docs/art/vh-<kind>-picks.json -> vh-<kind>-review.json)
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); VJ = os.path.normpath(os.path.join(HERE, "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
KIND = sys.argv[1] if len(sys.argv) > 1 else "styles"
P = json.load(open(os.path.join(VJ, "docs", "art", f"vh-{KIND}-picks.json"), encoding="utf8"))
sections = []
sheet = os.path.join(VJ, "docs", "art", f"vh-{KIND}-sheet.jpg")   # processed files, when this kind has them
if os.path.exists(sheet):
    sections.append({"title": "The processed files (textures tiled 2x2)", "cols": 1, "items": [{"file": sheet, "label": "What the game uses", "verdict": "pick"}]})
for sec in P["sections"]:
    items = []
    for it in sec["items"]:
        items.append({"file": os.path.join(INC, it["pick"]), "label": it["label"], "verdict": "pick", "note": it.get("note", "")})
        if it.get("alt"): items.append({"file": os.path.join(INC, it["alt"]), "label": it["label"] + " (alternate)", "verdict": "alt", "note": it.get("alt_note", "")})
    sections.append({"title": sec["title"], "cols": sec.get("cols", 4), "items": items})
spec = {"name": P.get("name", "visiting-hours-styles"), "title": P.get("title", "Visiting Hours: five style options"), "subtitle": P["subtitle"],
        "questions": P["questions"], "notes": P["notes"], "sections": sections}
dst = os.path.join(VJ, "docs", "art", f"vh-{KIND}-review.json")
json.dump(spec, open(dst, "w", encoding="utf8"), indent=1); print(dst, sum(len(s["items"]) for s in sections), "tiles")
