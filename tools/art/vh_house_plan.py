# Night 2's house as labelled floor plans (the main floor and the attic), from core DreamHouse:
#   dotnet run -c Release --project tools/house-plan -- <plan.json>
#   uv run --with pillow python tools/art/vh_house_plan.py <plan.json> <out dir>
# Writes <out dir>/house-plan-main.png and house-plan-attic.png: rooms (with ceiling heights), walls and doorways,
# furniture, the stairs and the hatch, hiding spots, the five memories, Theo's walking graph, the loops.
import json, os, sys
from PIL import Image, ImageDraw, ImageFont

P = json.load(open(sys.argv[1], encoding="utf8"))
OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)
S = 64                     # pixels per metre
X0, X1, Z0, Z1 = P["hub"]
MX, MZ = 1.6, 2.6          # margins (m)
W, H = int((X1 - X0 + 2 * MX) * S), int((Z1 - Z0 + 2 * MZ) * S) + 230


def font(size, bold=False):
    for f in (("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"), "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def px(x, z):  # plan view: north (+z) up, east (+x) right
    return ((x - X0 + MX) * S, (Z1 + MZ - z) * S)


def rect(d, x0, z0, x1, z1, **kw):
    z0 = max(z0, Z0 - 1.4)   # the slide's walls run far south: only the house
    if z1 <= z0: return
    a, b = px(x0, z1), px(x1, z0)
    d.rectangle([a, b], **kw)


ROOM_COL = {"Kitchen and dining": (253, 236, 186), "Living room": (205, 232, 205), "Bay window": (205, 232, 205), "Hall": (246, 208, 212),
            "Pantry": (238, 230, 214), "Parents' room": (212, 218, 246), "Bath": (200, 236, 236), "Attic stairs": (226, 204, 186),
            "Theo's room": (200, 222, 250), "Attic": (226, 210, 192)}
INK, WALL, DOOR, FURN = (60, 50, 80), (70, 58, 92), (170, 150, 190), (196, 176, 160)
SPOT, MEM, GRAPH, LOOP = (232, 128, 40), (214, 60, 110), (70, 120, 210), (40, 160, 110)


def plan(attic):
    img = Image.new("RGB", (W, H), (250, 248, 244))
    d = ImageDraw.Draw(img)
    fy = P["attic"] if attic else P["ground"]
    title = "The attic (Lena's observatory: a proposal; the family's stored life)" if attic else "The main floor (pancakes and hide-and-seek)"
    d.text((20, 14), title, font=font(30, True), fill=INK)
    # Rooms.
    for r in P["rooms"]:
        if (r["name"] == "Attic") != attic: continue
        rect(d, r["x0"], r["z0"], r["x1"], r["z1"], fill=ROOM_COL.get(r["name"], (230, 230, 230)))
    # The attic's roof: the eaves (low), the ridge, the dormer.
    if attic:
        hh = 1.0
        ox = P.get("obsX1", X0)
        for (za, zb) in ((Z0, Z0 + hh * 1.3), (Z1 - hh * 1.3, Z1)):
            rect(d, ox, za, X1, zb, fill=(205, 186, 170))
        d.line([px(ox, P["ridgeZ"]), px(X1, P["ridgeZ"])], fill=(150, 110, 90), width=4)
        # The observatory: the roof cut and raised over it (full height wall to wall).
        oz0, oz1 = P.get("obsZ", [Z0, Z1])
        rect(d, X0, oz0, ox, oz1, fill=(214, 220, 240), outline=(60, 70, 160), width=4)
        d.text(px(X0 + 0.2, oz1 + 1.6), f"the observatory tower: {P.get('obsHeight', 0):.1f} m high inside", font=font(14, True), fill=(60, 70, 160))
        if "sky" in P:
            sx, sz, sr = P["sky"]
            a0, b0 = px(sx - sr, sz + sr), px(sx + sr, sz - sr)
            d.ellipse([a0[0], a0[1], b0[0], b0[1]], outline=(60, 70, 160), width=2)
            d.text(px(sx + sr + 0.1, sz + 0.2), "crescent skylight", font=font(13), fill=(60, 70, 160))
        d.text(px(X1 - 3.0, P["ridgeZ"] + 0.15), "ridge (3.4 m)", font=font(16), fill=(120, 90, 70))
        d.text(px(ox + 0.3, Z0 + 0.15), "low eaves: the roof slopes down to 0.8 m", font=font(15), fill=(120, 90, 70))
        dx0, dx1, dz0 = P["dormer"]
        rect(d, dx0, dz0, dx1, Z1, outline=(120, 90, 70), width=3)
        d.text(px(dx0 + 0.1, Z1 - 0.6), "dormer (over the stairs)", font=font(15), fill=(120, 90, 70))
    # Pieces at this level.
    for p in P["pieces"]:
        mn, mx = p["min"], p["max"]
        if attic:
            if mx[1] <= fy + 0.05 or mn[1] > fy + 2.5 or mn[2] < Z0 - 0.4 or mx[2] > Z1 + 0.4: continue
        else:
            if mn[1] > fy + 2.0 or mx[1] <= fy + 0.05: continue
        if p["look"] in ("Floor", "Ceiling", "RoofStep", "Roof", "Lawn", "Stairs") or p["kind"] == "Floor": continue
        if p["look"] in ("Wall", "Chimney", "KneeWall", "Gable", "Railing"):
            low = mx[1] - fy < 1.2   # a half wall or a railing
            rect(d, mn[0], mn[2], mx[0], mx[2], fill=(150, 130, 170) if low else WALL)
        elif p["look"] == "Header":
            rect(d, mn[0], mn[2], mx[0], mx[2], fill=DOOR)
        elif p["look"] == "Curtain":
            rect(d, mn[0], mn[2], mx[0], mx[2], outline=(220, 120, 140), width=2)
        elif p["solid"]:
            rect(d, mn[0], mn[2], mx[0], mx[2], fill=FURN, outline=(150, 128, 112))
            if attic and not p["name"].startswith("Boxes"):
                x, y = px(mn[0], mn[2])
                label = {"Observatory platform": "the observatory (telescope, Lena's chair, star charts)",
                         "Old wardrobe (attic, under a dust sheet)": "old wardrobe (dust sheet)"}.get(p["name"], p["name"])
                d.text((x + 3, y + 2), label, font=font(13, True), fill=INK)
    if attic:
        d.text(px(X0 + 0.1, P["ridgeZ"] + 0.35), "round window", font=font(14, True), fill=(60, 70, 160))
        # The wall of stored things across the attic, and its one gap (the sheets on a line).
        a, b = px(-2.55, 77.0), px(-2.55, 80.0)
        for k in range(0, int(b[1] - a[1]) if b[1] > a[1] else int(a[1] - b[1]), 12):
            y0 = min(a[1], b[1]) + k
            d.line([(a[0], y0), (a[0], y0 + 6)], fill=(200, 120, 140), width=4)
        x, y = px(-2.55, 78.5)
        d.text((x + 10, y - 10), "sheets on a line: the way through", font=font(14, True), fill=(200, 90, 120))
        x, y = px(-2.2, 74.2)
        d.text((x + 6, y - 8), "a wall of stored things (boxes)", font=font(14, True), fill=INK)
        x, y = px(3.2, 83.3)
        d.text((x + 4, y - 8), "boxes under the eaves", font=font(13, True), fill=INK)
        x, y = px(-2.2, 72.9)
        d.text((x + 4, y - 8), "boxes under the eaves", font=font(13, True), fill=INK)
    # The stairs (with their climb) and the hatch.
    sx0, sx1, sz0, sz1 = P["stairs"]
    rect(d, sx0, sz0, sx1, sz1, fill=(210, 180, 150), outline=INK)
    for i in range(1, 13):
        z = sz1 - (sz1 - sz0) * i / 13
        d.line([px(sx0, z), px(sx1, z)], fill=(150, 120, 100), width=1)
    a, b = px((sx0 + sx1) / 2, sz1 - 0.3), px((sx0 + sx1) / 2, sz0 + 0.2)
    d.line([a, b], fill=INK, width=3)
    d.polygon([(b[0] - 8, b[1] + 14), (b[0] + 8, b[1] + 14), b], fill=INK)
    d.text(px(sx1 + 0.05, sz1 - 0.1) if not attic else px(sx1 + 0.05, sz0 + 0.5), "stairs UP to the attic" if not attic else "stairs arrive here", font=font(15, True), fill=INK)
    hx0, hx1, hz0, hz1 = P["hatch"]
    rect(d, hx0, hz0, hx1, hz1, outline=(60, 90, 200), width=3)
    d.text(px(hx0 - 0.2, hz0 - 0.1), "hatch (drop down)" if attic else "hatch: the attic seen from below", font=font(14, True), fill=(60, 90, 200))
    # Room names and ceilings.
    for r in P["rooms"]:
        if (r["name"] == "Attic") != attic or r["name"] in ("Bay window", "Attic stairs"): continue
        cx, cz = (r["x0"] + r["x1"]) / 2, (r["z0"] + r["z1"]) / 2
        if r["name"] == "Hall": cx = -3.6
        if r["name"] == "Pantry": cz += 0.6
        tx, ty = px(cx, cz)
        f = font(17, True)
        tw = d.textlength(r["name"], font=f)
        d.text((tx - tw / 2, ty - 12), r["name"], font=f, fill=INK)
        if not attic:
            sub = f"ceiling {r['ceiling']:.1f} m"
            f2 = font(14)
            d.text((tx - d.textlength(sub, font=f2) / 2, ty + 8), sub, font=f2, fill=INK)
    if not attic:
        # Theo's walking graph.
        nodes = P["nodes"]
        for a_, b_ in P["edges"]:
            d.line([px(nodes[a_][0], nodes[a_][2]), px(nodes[b_][0], nodes[b_][2])], fill=GRAPH, width=1)
        for n in nodes:
            x, y = px(n[0], n[2]); d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=GRAPH)
        # The loops (two ways between rooms).
        loops = [[(-1, 73.4), (1.6, 74.4), (4.0, 73.1), (5.4, 77.1), (5.5, 79.1), (-0.7, 79.2), (-2.5, 79.0), (-2.5, 77.5), (-1, 73.4)],
                 [(-5.3, 77.6), (-6.2, 78.6), (-6.2, 79.4), (-4.7, 79.4), (-2.5, 79.0), (-2.5, 77.5), (-5.3, 77.6)]]
        for lp in loops:
            pts = [px(x, z) for x, z in lp]
            d.line(pts, fill=LOOP, width=4, joint="curve")
        # Theo counting, Lena at the stove, the arrival (the slide).
        x, y = px(P["theo"][0], P["theo"][2]); d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(250, 190, 60), outline=INK, width=2)
        d.text((x + 10, y - 2), "Theo counts", font=font(14, True), fill=INK)
        x, y = px(P["lena"][0], P["lena"][2]); d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(250, 220, 140), outline=INK, width=2)
        d.text((x + 10, y - 20), "Lena (stove)", font=font(14, True), fill=INK)
        ax, ay = px(0, P["hub"][2] - 1.6)
        bx, by = px(0, P["arrive"][2])
        d.line([(ax, ay), (bx, by)], fill=INK, width=4)
        d.polygon([(bx - 9, by + 16), (bx + 9, by + 16), (bx, by)], fill=INK)
        d.text((ax - 330, ay - 26), "the way in: the slide lands in the kitchen", font=font(15, True), fill=INK)
    # Hiding spots and memories at this level.
    for s in P["spots"]:
        c, sz = s["center"], s["size"]
        is_attic = c[1] > P["ground"] + 2.5
        if is_attic != attic: continue
        rect(d, c[0] - sz[0] / 2, c[2] - sz[2] / 2, c[0] + sz[0] / 2, c[2] + sz[2] / 2, outline=SPOT, width=3)
        lx, ly = px(c[0] - sz[0] / 2, c[2] + sz[2] / 2)
        d.text((lx + 3, ly + 2), "hide: " + s["name"].replace("The ", "the "), font=font(13, True), fill=SPOT)
    for i, m in enumerate(P["memories"]):
        if (m["at"][1] > P["ground"] + 2.9) != attic: continue
        x, y = px(m["at"][0], m["at"][2])
        d.regular_polygon((x, y, 11), 5, fill=MEM)
        d.text((x + 9, y + 4), f"{i + 1}. {m['name']}", font=font(13, True), fill=MEM)
    note = ("A vertical loop: up the steep stairs from Theo's room, along the attic, and down through the hatch into the hall."
            if not attic else "Arrive at the top of the stairs on the storage side; through the sheets, the observatory's round window glows.")
    d.text((20, H - 250), note, font=font(17, True), fill=LOOP if not attic else INK)
    # Scale and legend.
    lx, ly = 20, H - 210
    d.line([(lx, ly), (lx + 5 * S, ly)], fill=INK, width=3)
    d.text((lx, ly + 6), "5 m", font=font(15), fill=INK)
    items = [(WALL, "wall"), (DOOR, "doorway (the wall over it)"), ((150, 130, 170), "half wall / railing"), (FURN, "furniture (solid, climbable)"),
             (SPOT, "hiding spot"), (MEM, "memory (five to find)"), (GRAPH, "Theo's walking paths"), (LOOP, "loops: two ways round")]
    for k, (col, lab) in enumerate(items):
        x = lx + (k % 4) * 300; y = ly + 40 + (k // 4) * 34
        d.rectangle([x, y, x + 26, y + 18], fill=col)
        d.text((x + 34, y - 2), lab, font=font(16), fill=INK)
    name = "house-plan-attic.png" if attic else "house-plan-main.png"
    img.save(os.path.join(OUT, name))
    print(name, img.size)


plan(False)
plan(True)
