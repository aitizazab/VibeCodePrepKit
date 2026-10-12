# Night 2's house as labelled plans, from core DreamHouse and HouseEscape (docs/redesign/house.md):
#   dotnet run -c Release --project tools/house-plan -- <plan.json>
#   uv run --with pillow python tools/art/vh_house_plan.py <plan.json> <out dir>
# Writes <out dir>/house-plan-floors.png (the basement, ground floor, upper floor and attic, cut 1.2 m above each floor:
# rooms, walls, doorways, windows, furniture, stairs, hiding spots, the memories and where the house re-hides them,
# Theo's walking graph, lamps), house-plan-site.png (the lot: the front garden, the house and garage, the back yard,
# the treehouse, the shed, Rowan Street, where she arrives and where Chapel Row's door lets her out) and
# house-escape-route.png (the escape along its route: each room's floor height, the corridor that stretches, the beats).
import json, os, sys
from PIL import Image, ImageDraw, ImageFont

P = json.load(open(sys.argv[1], encoding="utf8"))
OUT = sys.argv[2]
os.makedirs(OUT, exist_ok=True)

INK = (40, 34, 30)
PAPER = (250, 247, 240)
WALL = (52, 46, 42)
FURN = (150, 140, 128)
GLASS = (90, 150, 210)
GOLD = (214, 160, 30)
GREEN = (40, 140, 80)
RED = (200, 70, 60)
LAMP = (240, 200, 70)
ROOM_FILLS = [(244, 226, 208), (226, 236, 222), (222, 230, 242), (242, 226, 232), (236, 232, 214), (228, 222, 240), (246, 236, 214)]


def font(size, bold=False):
    for f in (("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"), "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


F_ROOM, F_SMALL, F_TITLE, F_NUM = font(19, True), font(15), font(30, True), font(14, True)
STOREYS = P["storeys"]
FLOORS = [s[1] for s in STOREYS]


def storey_of(y):
    k = 0
    for i, f in enumerate(FLOORS):
        if y >= f - 0.3:
            k = i
    return k


class Panel:
    def __init__(self, img, ox, oy, x0, x1, z0, z1, scale):
        self.d = ImageDraw.Draw(img)
        self.ox, self.oy, self.x0, self.z1, self.s = ox, oy, x0, z1, scale

    def px(self, x, z):
        return (self.ox + (x - self.x0) * self.s, self.oy + (self.z1 - z) * self.s)

    def rect(self, x0, z0, x1, z1, **kw):
        a, b = self.px(min(x0, x1), max(z0, z1)), self.px(max(x0, x1), min(z0, z1))
        self.d.rectangle([a, b], **kw)

    def line(self, p, q, **kw):
        self.d.line([self.px(*p), self.px(*q)], **kw)

    def dot(self, x, z, r, **kw):
        cx, cy = self.px(x, z)
        self.d.ellipse([cx - r, cy - r, cx + r, cy + r], **kw)

    def text(self, x, z, t, f, fill=INK, anchor="mm"):
        self.d.text(self.px(x, z), t, font=f, fill=fill, anchor=anchor)


def star(pn, x, z, r, fill, outline=INK):
    import math
    cx, cy = pn.px(x, z)
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    pn.d.polygon(pts, fill=fill, outline=outline)


def cut(part, floor):
    """The part crosses the plan's cut 1.2 m above this floor (or stands on it, for furniture)."""
    lo, hi = part["min"][1], part["max"][1]
    return lo <= floor + 1.2 <= hi


def on_floor(part, floor):
    lo = part["min"][1]
    return floor - 0.05 <= lo <= floor + 1.3


def short(name, n=34):
    return name if len(name) <= n else name[: n - 1] + "..."


MEM = P["memories"]
SPOTS = P["spots"]
INSIDE = [r for r in P["rooms"] if not r["outside"]]


def draw_storey(img, k, ox, oy, x0, x1, z0, z1, scale):
    pn = Panel(img, ox, oy, x0, x1, z0, z1, scale)
    floor = FLOORS[k]
    pn.d.text((ox, oy - 44), STOREYS[k][0] + "  (floor %+.1f m)" % floor, font=F_TITLE, fill=INK)
    rooms = [r for r in INSIDE if r["storey"] == k and abs(r["floor"] - floor) < 1.0 and r["rects"][0][0] >= x0 - 0.5 and r["rects"][0][1] <= x1 + 0.5 and r["rects"][0][2] >= z0 - 0.5 and r["rects"][0][3] <= z1 + 0.5]
    for i, r in enumerate(rooms):
        for (a, b, c, d) in r["rects"]:
            pn.rect(a, c, b, d, fill=ROOM_FILLS[i % len(ROOM_FILLS)])
    for p in P["parts"]:
        look = p["look"]
        if look in ("Rug",) and on_floor(p, floor):
            pn.rect(p["min"][0], p["min"][2], p["max"][0], p["max"][2], outline=(200, 180, 160), width=1)
    for p in P["parts"]:
        look = p["look"]
        mn, mx = p["min"], p["max"]
        if mx[0] < x0 or mn[0] > x1 or mx[2] < z0 or mn[2] > z1:
            continue
        if look in ("Furniture", "Cupboard", "Cloth", "Wood", "Chimney") and (on_floor(p, floor) or (look == "Chimney" and cut(p, floor))):
            if look == "Chimney":
                pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(120, 100, 90), outline=WALL)
            elif look == "Cloth":
                pn.rect(mn[0], mn[2], mx[0], mx[2], outline=(170, 120, 160), width=2)
            else:
                pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(236, 230, 220), outline=FURN, width=2)
        elif look in ("Wall", "Rail", "GarageDoor") and cut(p, floor):
            pn.rect(mn[0], mn[2], mx[0], mx[2], fill=WALL if look != "Rail" else (120, 110, 100))
        elif look == "Glass" and cut(p, floor):
            pn.rect(mn[0], mn[2], mx[0], mx[2], fill=GLASS)
    # Stairs: treads and an arrow up.
    for s in P["stairs"]:
        a, b, c, d = s["rect"]
        if not (s["bottom"] - 0.1 <= floor + 1.2 <= s["top"] + 1.2) or s["name"].startswith("The treehouse"):
            continue
        if not (floor - 0.1 <= s["bottom"] <= floor + 0.1 or floor - 0.1 <= s["top"] <= floor + 0.1):
            continue
        pn.rect(a, c, b, d, fill=(255, 252, 245), outline=INK, width=2)
        n = 12
        for i in range(1, n):
            if s["alongX"]:
                x = a + (b - a) * i / n
                pn.line((x, c), (x, d), fill=(150, 140, 130), width=1)
            else:
                z = c + (d - c) * i / n
                pn.line((a, z), (b, z), fill=(150, 140, 130), width=1)
        going_up = abs(s["bottom"] - floor) < 0.1
        label = ("up" if going_up else "down") + " to " + STOREYS[storey_of(s["top"] if going_up else s["bottom"])][0].lower()
        if s["alongX"]:
            pn.line((a + 0.2, (c + d) / 2), (b - 0.2, (c + d) / 2), fill=INK, width=2)
        else:
            pn.line(((a + b) / 2, c + 0.2), ((a + b) / 2, d - 0.2), fill=INK, width=2)
        pn.text((a + b) / 2, (c + d) / 2, label, F_SMALL, fill=RED)
    # Theo's walking graph on this floor.
    nodes = P["nodes"]
    inside = lambda q: x0 <= q[0] <= x1 and z0 <= q[2] <= z1
    for a, b in P["links"]:
        pa, pb = nodes[a]["p"], nodes[b]["p"]
        if storey_of(pa[1]) == k and storey_of(pb[1]) == k and inside(pa) and inside(pb):
            pn.line((pa[0], pa[2]), (pb[0], pb[2]), fill=(230, 150, 140), width=1)
    for n in nodes:
        if storey_of(n["p"][1]) == k and inside(n["p"]):
            pn.dot(n["p"][0], n["p"][2], 3, fill=(220, 110, 100))
    for l in P["lamps"]:
        if storey_of(l["p"][1] - 1.5) == k and inside(l["p"]):
            pn.dot(l["p"][0], l["p"][2], 5, fill=LAMP, outline=INK)
    # Room names.
    for r in rooms:
        a, b, c, d = max(r["rects"], key=lambda q: (q[1] - q[0]) * (q[3] - q[2]))
        name = r["name"]
        f = F_ROOM if (b - a) > 2.2 else F_SMALL
        pn.text((a + b) / 2, (c + d) / 2 + 0.25, short(name, 26), f)
        pn.text((a + b) / 2, (c + d) / 2 - 0.25, "%.1f x %.1f m, %.1f m high" % (b - a, d - c, r["ceiling"] - r["floor"]), F_SMALL, fill=(110, 100, 90))
    # Hiding spots.
    for i, s in enumerate(SPOTS):
        c = s["center"]
        if storey_of(c[1] - s["size"][1] / 2) != k or not (x0 <= c[0] <= x1 and z0 <= c[2] <= z1):
            continue
        hx, hz = s["size"][0] / 2, s["size"][2] / 2
        pn.rect(c[0] - hx, c[2] - hz, c[0] + hx, c[2] + hz, outline=GREEN, width=3)
        pn.dot(c[0], c[2], 11, fill=GREEN)
        pn.text(c[0], c[2], "H%d" % (i + 1), F_NUM, fill=(255, 255, 255))
    # Memories (stars) and where the house re-hides them (rings).
    for i, m in enumerate(MEM):
        for alt in m["alts"]:
            q = alt["p"]
            if storey_of(q[1] - 0.2) == k and x0 <= q[0] <= x1 and z0 <= q[2] <= z1:
                pn.dot(q[0], q[2], 10, outline=GOLD, width=3)
                pn.text(q[0], q[2], str(i + 1), F_NUM, fill=GOLD)
        p = m["p"]
        if storey_of(p[1] - 0.2) == k and x0 <= p[0] <= x1 and z0 <= p[2] <= z1:
            star(pn, p[0], p[2], 16, GOLD)
            pn.text(p[0], p[2] - 0.45, str(i + 1), F_NUM, fill=INK)
    # Points of the story.
    pts = P["points"]
    if k == 1:
        pn.dot(pts["theoStart"][0], pts["theoStart"][2], 9, fill=RED)
        pn.text(pts["theoStart"][0] - 0.2, pts["theoStart"][2] - 0.5, "Theo counts", F_SMALL, fill=RED)
        pn.dot(pts["nadiaChair"][0], pts["nadiaChair"][2], 9, fill=(80, 60, 160))
        pn.text(pts["nadiaChair"][0] + 0.2, pts["nadiaChair"][2] + 0.55, "her chair", F_SMALL, fill=(80, 60, 160))
        pn.dot(pts["lenaAt"][0], pts["lenaAt"][2], 9, fill=(160, 40, 80))
        pn.text(pts["lenaAt"][0], pts["lenaAt"][2] - 0.5, "Lena", F_SMALL, fill=(160, 40, 80))
        pn.text(pts["frontDoor"][0], pts["frontDoor"][2] - 0.8, "front door", F_SMALL, fill=RED)
    if k == 3:
        q = pts["lenaWindow"]
        pn.dot(q[0], q[2], 9, fill=(160, 40, 80))
        pn.text(q[0] + 0.3, q[2] - 0.6, "Lena at the round window (after)", F_SMALL, fill=(160, 40, 80))
    # Scale bar.
    bx, bz = x0 + 0.3, z0 + 0.3
    pn.line((bx, bz), (bx + 5, bz), fill=INK, width=4)
    pn.text(bx + 2.5, bz + 0.35, "5 m", F_SMALL)


# ---------- The floors: 2 x 2 ----------
X0, X1, Z0, Z1 = -8.6, 13.4, -1.6, 12.4
S = 58
PW, PH = int((X1 - X0) * S), int((Z1 - Z0) * S)
GAP, TOP, LEG = 90, 70, 330
img = Image.new("RGB", (PW * 2 + GAP * 3, (PH + TOP) * 2 + GAP + LEG), PAPER)
# ground floor top left, upper floor top right, basement bottom left, attic bottom right
layout = {1: (0, 0), 2: (1, 0), 0: (0, 1), 3: (1, 1)}
for k, (cx, cy) in layout.items():
    ox = GAP + cx * (PW + GAP)
    oy = TOP + cy * (PH + TOP + GAP // 2)
    draw_storey(img, k, ox, oy, X0, X1, Z0, Z1, S)
d = ImageDraw.Draw(img)
ly = (PH + TOP) * 2 + GAP // 2 + 20
d.text((GAP, ly), "Memories (gold stars; rings = where the house re-hides one when Theo finds her)", font=F_ROOM, fill=INK)
for i, m in enumerate(MEM):
    alts = ", ".join(a["room"] for a in m["alts"])
    d.text((GAP + (i % 2) * (PW + GAP), ly + 34 + (i // 2) * 26), "%d. %s  (%s; again: %s)" % (i + 1, m["name"], m["room"], alts), font=F_SMALL, fill=INK)
hy = ly + 34 + 4 * 26 + 18
d.text((GAP, hy), "Hiding places (green)", font=F_ROOM, fill=INK)
for i, s in enumerate(SPOTS):
    col, row = i % 3, i // 3
    d.text((GAP + col * ((PW * 2 + GAP) // 3), hy + 30 + row * 22), "H%d. %s" % (i + 1, short(s["name"], 52)), font=F_SMALL, fill=INK)
d.text((img.width - GAP, 18), "Night 2: the Lasname house. Plans cut 1.2 m above each floor. Red dots: Theo's walking graph. Yellow: lamps. Blue: glass.", font=F_SMALL, fill=(110, 100, 90), anchor="ra")
img.save(os.path.join(OUT, "house-plan-floors.png"))

# ---------- The site ----------
YX0, YX1, FZ0, YZ1 = P["yard"]
SX0, SX1, SZ0, SZ1 = YX0 - 3, YX1 + 3, P["streetZ0"] - 1, YZ1 + 2
S2 = 30
img2 = Image.new("RGB", (int((SX1 - SX0) * S2) + 160, int((SZ1 - SZ0) * S2) + 140), PAPER)
pn = Panel(img2, 80, 90, SX0, SX1, SZ0, SZ1, S2)
pn.d.text((80, 24), "Night 2: the lot, at ground level", font=F_TITLE, fill=INK)
for r in P["rooms"]:
    if r["outside"]:
        fill = (214, 232, 200) if "garden" in r["name"] or "yard" in r["name"] else (210, 206, 200)
        for (a, b, c, dd) in r["rects"]:
            pn.rect(a, c, b, dd, fill=fill)
for p in P["parts"]:
    look, mn, mx = p["look"], p["min"], p["max"]
    if look in ("Lawn",):
        pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(200, 226, 184))
for p in P["parts"]:
    look, mn, mx = p["look"], p["min"], p["max"]
    if look in ("Street",):
        pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(150, 146, 140))
    elif look in ("Path", "Porch"):
        pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(214, 200, 180))
    elif look in ("Fence",):
        pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(150, 110, 70))
    elif look in ("Hedge",):
        pn.rect(mn[0], mn[2], mx[0], mx[2], fill=(80, 130, 70))
    elif look == "Tree":
        cx, cz = (mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2
        pn.dot(cx, cz, 4.5 * S2, fill=(150, 190, 130), outline=(90, 130, 80))
        pn.dot(cx, cz, 0.5 * S2, fill=(110, 80, 50))
    elif look == "Trampoline":
        cx, cz = (mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2
        pn.dot(cx, cz, (mx[0] - mn[0]) / 2 * S2, outline=(40, 80, 140), width=3)
for r in INSIDE:
    if r["storey"] in (1, 2) and r["floor"] < 3.5:
        for (a, b, c, dd) in r["rects"]:
            pn.rect(a, c, b, dd, fill=(236, 226, 212), outline=WALL, width=2)
for r in INSIDE:
    if r["name"] in ("The garage", "The shed", "Theo's treehouse", "The mudroom"):
        a, b, c, dd = r["rects"][0]
        pn.text((a + b) / 2, (c + dd) / 2, r["name"], F_ROOM)
fx0, fx1, fz0, fz1 = P["footprint"]
pn.text((fx0 + fx1) / 2, (fz0 + fz1) / 2, "the house (3 storeys + basement)", F_ROOM)
pts = P["points"]
pn.dot(pts["arrival"][0], pts["arrival"][2], 10, fill=(80, 60, 160))
pn.text(pts["arrival"][0], pts["arrival"][2] - 1.0, "she arrives (Rowan Street)", F_SMALL, fill=(80, 60, 160))
pn.dot(pts["exitDoor"][0], min(pts["exitDoor"][2], SZ1 - 1), 10, fill=RED)
pn.text(pts["exitDoor"][0], min(pts["exitDoor"][2], SZ1 - 1) + 1.0, "the dream's door onto Chapel Row (the escape's end), %.1f m up" % pts["exitDoor"][1], F_SMALL, fill=RED)
for i, s in enumerate(SPOTS):
    c = s["center"]
    if (c[0] > fx1 + 0.2 and c[0] > 12.2) or c[2] > fz1 + 0.2 or c[2] < fz0 - 0.2 or c[0] < fx0 - 0.2:
        pn.dot(c[0], c[2], 10, fill=GREEN)
        pn.text(c[0], c[2], "H%d" % (i + 1), F_NUM, fill=(255, 255, 255))
for i, m in enumerate(MEM):
    p = m["p"]
    if p[2] > fz1 + 0.2 or p[0] > 12.2 or p[0] < fx0 - 0.2:
        star(pn, p[0], p[2], 14, GOLD)
        pn.text(p[0], p[2] - 0.8, str(i + 1), F_NUM)
pn.line((SX0 + 1, SZ0 + 1.5), (SX0 + 11, SZ0 + 1.5), fill=INK, width=4)
pn.text(SX0 + 6, SZ0 + 2.2, "10 m", F_SMALL)
img2.save(os.path.join(OUT, "house-plan-site.png"))

# ---------- The escape route ----------
E = P["escape"]
L = E["frontDoorS"] + 12
W3, H3 = 2400, 760
img3 = Image.new("RGB", (W3, H3), PAPER)
d3 = ImageDraw.Draw(img3)
d3.text((40, 20), "The escape: the house throws her out (along the route; a player at a run ~9 m/s, 2-3 minutes with the corridor)", font=F_TITLE, fill=INK)
ys = [r["floor"] for r in E["rooms"]] + [r["ceiling"] for r in E["rooms"]]
ymin, ymax = min(ys) - 2, max(ys) + 2
def ex(s): return 60 + s / L * (W3 - 120)
def ey(y): return 120 + (ymax - y) / (ymax - ymin) * (H3 - 330)
cols = [(244, 214, 190), (214, 228, 244), (230, 222, 206), (200, 196, 214), (236, 210, 222), (220, 236, 214), (40, 50, 90), (226, 222, 200)]
for i, r in enumerate(E["rooms"]):
    d3.rectangle([ex(r["s0"]), ey(r["ceiling"]), ex(r["s1"]), ey(r["floor"])], fill=cols[i % len(cols)], outline=INK)
    d3.line([ex(r["s0"]), ey(r["floor"]), ex(r["s1"]), ey(r["floor"])], fill=INK, width=4)
    tx = (ex(r["s0"]) + ex(r["s1"])) / 2
    d3.text((tx, ey(r["ceiling"]) - 16), r["name"], font=F_SMALL, fill=INK, anchor="mm")
    note = ("rolls %d" % r["roll"]) if r["roll"] else ""
    if r["open"]:
        note = "open to the stars"
    if note:
        d3.text((tx, ey(r["floor"]) + 16), note, font=F_SMALL, fill=(110, 100, 90), anchor="mm")
c = E["corridor"]
d3.rectangle([ex(c["triggerS"]), ey(ymax - 1), ex(c["doorS"]), ey(ymin + 1)], outline=RED, width=2)
d3.text((ex(c["triggerS"]), ey(ymin + 1) + 30), "the corridor stretches %d m each time she nears its door below %.1f m/s" % (c["bay"], c["threshold"]), font=F_SMALL, fill=RED)
for i, b in enumerate(E["beats"]):
    x = ex(b["s"])
    d3.line([x, H3 - 190, x, H3 - 150], fill=INK, width=2)
    d3.text((x, H3 - 140 + (i % 3) * 20), "%s (%s)" % (b["name"], b["kind"]), font=F_SMALL, fill=INK, anchor="mm")
for cp in E["checkpoints"]:
    x = ex(cp["s"])
    d3.polygon([(x, H3 - 200), (x - 7, H3 - 214), (x + 7, H3 - 214)], fill=GREEN)
d3.text((40, H3 - 40), "Green: checkpoints (chapters). Below the floor line is the void: falling throws her out of the dream (Mags pulls her out; Lena condemns; the ending is lost).", font=F_SMALL, fill=(110, 100, 90))
img3.save(os.path.join(OUT, "house-escape-route.png"))
print("wrote", OUT)
