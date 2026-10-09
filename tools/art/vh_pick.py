# vh_pick.py <output> <takeN> [note] [--reject takeM "why" ...]: record a choice in docs/art/vh-picks.json
import json, sys, os, re
f = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "vh-picks.json")
d = json.load(open(f, encoding="utf8")); a = sys.argv[1:]; out, take = a[0], a[1]; rest = a[2:]
p = d[out]; stem = re.sub(r"-take\d+\.\w+$", "", p["src"]); p["src"] = f"{stem}-{take}.jpg"
i = 0
while i < len(rest):
    if rest[i] == "--reject": p["rejects"][rest[i + 1]] = rest[i + 2]; i += 3
    else: p["note"] = rest[i]; i += 1
json.dump(d, open(f, "w", encoding="utf8"), indent=1); print(out, "->", p["src"], p["rejects"])
