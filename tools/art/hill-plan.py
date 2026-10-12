"""Draws Larch Hill's plan (docs/redesign/world.md) from the JSON the core test exports.

    dotnet test core-tests/DeepBlock/DeepBlock.Tests.csproj --filter "Name=ExportThePlan"
    uv run --with matplotlib python tools/art/hill-plan.py core-tests/DeepBlock/bin/Debug/net10.0/larch-hill.json docs/redesign/world-plan.png

North (up the hill) is up. Ground is shaded by height; buildings dark; fences and walls thin; Night 1's legs in colour
with their names; the story's spots as dots; the bands labelled down the side.
"""
import json
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle

src, out = sys.argv[1], sys.argv[2]
d = json.load(open(src, encoding="utf-8"))

fig, ax = plt.subplots(figsize=(13, 22), dpi=110)
cmap = plt.get_cmap("YlGn")
surface_colour = {
    "grass": None, "asphalt": "#8c8a9a", "paving": "#d9d0c4", "cobbles": "#b9aca6", "concrete": "#cfc8c2", "gravel": "#cbbfae",
    "dirt": "#b89a7a", "steps": "#e8e0d0", "deck": "#c9a27e", "pool": "#8fc4d8", "creek_bed": "#6f9fb2",
}
pieces = d["pieces"]
for p in pieces:
    st = p.get("st", "")
    if "|" not in st:
        continue
    surf = st.split("|")[0]
    top = p["top"]
    col = surface_colour.get(surf)
    if col is None:
        col = cmap(0.25 + 0.6 * max(0.0, min(1.0, (top + 5) / 55)))
    ax.add_patch(Rectangle((p["x0"], p["z0"]), p["x1"] - p["x0"], p["z1"] - p["z0"], facecolor=col, edgecolor="none", zorder=1))
for p in pieces:
    k, st = p["k"], p.get("st", "")
    if "|" in st or k == "Boundary":
        continue
    w, h = p["x1"] - p["x0"], p["z1"] - p["z0"]
    if k in ("House", "Shop", "Roof") or st in ("@building", "@house"):
        if k == "Roof" and st != "@building":
            continue
        ax.add_patch(Rectangle((p["x0"], p["z0"]), w, h, facecolor="#5b4f6e", edgecolor="#3a3148", lw=0.3, zorder=3, alpha=0.9))
    elif k in ("Fence", "Picket", "BrickWall", "Hedge", "Gate"):
        ax.add_patch(Rectangle((p["x0"], p["z0"]), max(w, 0.4), max(h, 0.4), facecolor="#3d6b45" if k == "Hedge" else "#7a5a48", edgecolor="none", zorder=4))
    elif k == "Water":
        ax.add_patch(Rectangle((p["x0"], p["z0"]), w, h, facecolor="#5aa3c4", edgecolor="none", zorder=2, alpha=0.8))
    elif st == "@prop" or k in ("Prop", "Deck", "Plank", "Post", "Scaffold", "Sheet", "Door", "Tree"):
        ax.add_patch(Rectangle((p["x0"], p["z0"]), w, h, facecolor="#e0a050", edgecolor="none", zorder=4, alpha=0.8))
for x, z, r in d["trees"]:
    ax.add_patch(plt.Circle((x, z), r, color="#2f6b3a", alpha=0.45, zorder=5, lw=0))
colours = ["#e63946", "#f77f00", "#fcbf49", "#2a9d8f", "#4361ee", "#9b5de5", "#f15bb5", "#00bbf9"]
for i, r in enumerate(d["routes"]):
    pts = r["points"]
    xs, zs = [p[0] for p in pts], [p[1] for p in pts]
    c = colours[i % len(colours)]
    ax.plot(xs, zs, color=c, lw=3, zorder=8, solid_capstyle="round")
    ax.annotate(f"{i}. {r['name']}", (xs[len(xs) // 2], zs[len(zs) // 2]), color="white", fontsize=9, weight="bold", zorder=9,
                bbox=dict(boxstyle="round,pad=0.2", fc=c, ec="none", alpha=0.9))
for s in d["spots"]:
    ax.plot(s["x"], s["z"], "o", ms=3, color="#111", zorder=10)
ax.plot(*d["arrival"], "*", ms=16, color="#ffd60a", mec="#333", zorder=11)
ax.plot(*d["house"], "s", ms=12, color="#ff4d6d", mec="#333", zorder=11)
ax.annotate("the Lasname house", d["house"], xytext=(8, -14), textcoords="offset points", fontsize=10, weight="bold", zorder=12)
ax.annotate("arrival", d["arrival"], xytext=(8, -4), textcoords="offset points", fontsize=10, weight="bold", zorder=12)
ax.annotate("St. Brigid's Hospital", (0, 337), ha="center", fontsize=12, weight="bold", color="white", zorder=12)
for z, label in [(-60, "THE FAR BANK (0-3 m)"), (-12, "THE CREEK"), (20, "THE FOOT: Walt's, the back way (0 m)"), (48, "MILL STREET (4 m)"),
                 (97, "BACK LANE B (5 m)"), (108, "ROWAN STREET (9 m): the Rewind"), (160, "CHAPEL ROW (15 m)"), (196, "SCHOOL LANE (22 m)"),
                 (234, "BELL ROW (29 m)"), (268, "UPPER ROW (36 m)"), (288, "CREST ROAD (42 m)"), (315, "THE SUMMIT (46 m)")]:
    ax.text(143, z, label, fontsize=9, va="center", zorder=12)
ax.set_xlim(-145, 205)
ax.set_ylim(-102, 406)
ax.set_aspect("equal")
ax.set_title("Larch Hill: the plan (north = up the hill; metres)", fontsize=14)
ax.grid(True, lw=0.3, alpha=0.4)
fig.tight_layout()
fig.savefig(out)
print("plan:", out)
