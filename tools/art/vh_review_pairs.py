# Builds docs/art/vh-styles-review.json (for FableAnimationTests/art-tools/review-page.mjs) from docs/art/vh-styles-picks.json:
# one section per style, each subject's chosen take and one alternate.
#   uv run python tools/art/vh_styles_review.py
import json, os
HERE = os.path.dirname(os.path.abspath(__file__)); VJ = os.path.normpath(os.path.join(HERE, "..", ".."))
INC = r"C:\Users\User\Desktop\yougotgamesonyophone\FableAnimationTests\art\incoming"
P = json.load(open(os.path.join(VJ, "docs", "art", "vh-styles-picks.json"), encoding="utf8"))
sections = []
for sec in P["sections"]:
    items = []
    for it in sec["items"]:
        items.append({"file": os.path.join(INC, it["pick"]), "label": it["label"], "verdict": "pick", "note": it.get("note", "")})
        if it.get("alt"): items.append({"file": os.path.join(INC, it["alt"]), "label": it["label"] + " (alternate)", "verdict": "alt", "note": it.get("alt_note", "")})
    sections.append({"title": sec["title"], "cols": sec.get("cols", 4), "items": items})
spec = {"name": "visiting-hours-styles", "title": "Visiting Hours: five style options", "subtitle": P["subtitle"],
        "questions": P["questions"], "notes": P["notes"], "sections": sections}
dst = os.path.join(VJ, "docs", "art", "vh-styles-review.json")
json.dump(spec, open(dst, "w", encoding="utf8"), indent=1); print(dst, sum(len(s["items"]) for s in sections), "tiles")
