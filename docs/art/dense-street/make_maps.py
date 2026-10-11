"""Draws the dense-street layout proposals (docs/DENSE-STREET.md) as SVG plans with a section strip.

    uv run python docs/art/dense-street/make_maps.py

Writes plan-a-deep-block.svg, plan-b-hill.svg, plan-c-close.svg, plan-d-link.svg and distillery-schematic.svg
next to this file. Every size is in metres; heights come from the movement kit (docs/MOVEMENT.md):
fence 1.8 m = a mantle from the ground (mantle 0.4-1.9 m), garage roof 2.7 m = jump + mantle (reach 3.15 m),
eaves 5.2 m = from a garage or porch roof, side passages 4.5 m = wall to wall (4-6 m), slide gaps 1.15 m.
"""
import math
import os
from html import escape

HERE = os.path.dirname(os.path.abspath(__file__))
PX = 7.0          # pixels per metre on the plans
M = 60            # margin (px)
LEGEND_W = 300    # legend column (px)

# Colours: the plan is neutral; routes carry the colour (gold = the roof layer, as gold means "usable" in the dream).
C = {
    "bg": "#f6f1e7", "ink": "#2b2b33", "dim": "#7d7a86", "grid": "#e4dccb",
    "road": "#cfc9c0", "pavement": "#e2ddd3", "lawn": "#d6e6c3", "garden": "#e3eed3", "lane": "#c9c3b8",
    "house": "#f2c9a8", "garage": "#d9c3a5", "shed": "#cdb595", "shop": "#e8a989", "hill": "#c9b7d9",
    "deck": "#c8a27a", "water": "#9ec9e2", "party": "#f7d6e0", "build": "#d8d2c8", "tree": "#9cc28a",
    "static": "#b9b4d6",
    "street": "#8a8a96", "back": "#3f9a52", "roof": "#d19a1c", "under": "#2f7fc0",
    "hide": "#7b4bb5", "mem": "#e0457b", "theo": "#1d8f8f", "watch": "#c0392b", "switch": "#555",
}
ROUTE_NAMES = {
    "street": "Street route (public, watched)",
    "back": "Back route (gardens, fences, the lane)",
    "roof": "Roof route (gold: fast, unseen on back pitches)",
    "under": "Under route (creek bed, culvert, crawl spaces)",
}


def esc(s):
    return escape(str(s), quote=True)


class Plan:
    def __init__(self, title, subtitle, w, h, origin=(0, 0)):
        self.title, self.subtitle, self.w, self.h = title, subtitle, w, h
        self.ox, self.oy = origin
        self.layers = {k: [] for k in ("ground", "build", "lines", "routes", "marks", "text")}
        self.section = None
        self.legend_extra = []
        self.notes = []

    # metres -> px
    def X(self, x):
        return M + (x - self.ox) * PX

    def Y(self, y):
        return M + 70 + (y - self.oy) * PX

    def rect(self, x, y, w, h, fill, label=None, height=None, layer="build", stroke="#6d6458", sw=1, dash=None, rx=0,
             fs=11, opacity=1.0):
        d = f' stroke-dasharray="{dash}"' if dash else ""
        s = (f'<rect x="{self.X(x):.1f}" y="{self.Y(y):.1f}" width="{w * PX:.1f}" height="{h * PX:.1f}" rx="{rx}" '
             f'fill="{fill}" stroke="{stroke}" stroke-width="{sw}"{d} opacity="{opacity}"/>')
        self.layers[layer].append(s)
        if label:
            lines = label.split("\n")
            if height is not None:
                lines.append(f"{height}")
            cy = self.Y(y + h / 2) - (len(lines) - 1) * (fs * 0.6)
            for i, ln in enumerate(lines):
                weight = "600" if i == 0 else "400"
                self.layers["text"].append(
                    f'<text x="{self.X(x + w / 2):.1f}" y="{cy + i * fs * 1.2:.1f}" font-size="{fs}" font-weight="{weight}" '
                    f'text-anchor="middle" dominant-baseline="middle" fill="{C["ink"]}">{esc(ln)}</text>')

    def circle(self, x, y, r, fill, label=None, layer="build", stroke="#6d6458", fs=10):
        self.layers[layer].append(f'<circle cx="{self.X(x):.1f}" cy="{self.Y(y):.1f}" r="{r * PX:.1f}" fill="{fill}" stroke="{stroke}"/>')
        if label:
            self.layers["text"].append(f'<text x="{self.X(x):.1f}" y="{self.Y(y):.1f}" font-size="{fs}" text-anchor="middle" '
                                       f'dominant-baseline="middle" fill="{C["ink"]}">{esc(label)}</text>')

    def poly(self, pts, fill, label=None, layer="build", stroke="#6d6458", lx=None, ly=None, fs=11):
        p = " ".join(f"{self.X(a):.1f},{self.Y(b):.1f}" for a, b in pts)
        self.layers[layer].append(f'<polygon points="{p}" fill="{fill}" stroke="{stroke}"/>')
        if label:
            cx = lx if lx is not None else sum(a for a, _ in pts) / len(pts)
            cy = ly if ly is not None else sum(b for _, b in pts) / len(pts)
            for i, ln in enumerate(label.split("\n")):
                self.layers["text"].append(f'<text x="{self.X(cx):.1f}" y="{self.Y(cy) + i * fs * 1.2:.1f}" font-size="{fs}" '
                                           f'font-weight="{"600" if i == 0 else "400"}" text-anchor="middle" fill="{C["ink"]}">{esc(ln)}</text>')

    def line(self, pts, color, width=2, dash=None, layer="lines", label=None, lpos=None, fs=10, cap="round"):
        p = " ".join(f"{self.X(a):.1f},{self.Y(b):.1f}" for a, b in pts)
        d = f' stroke-dasharray="{dash}"' if dash else ""
        self.layers[layer].append(f'<polyline points="{p}" fill="none" stroke="{color}" stroke-width="{width}" '
                                  f'stroke-linecap="{cap}" stroke-linejoin="round"{d}/>')
        if label:
            lx, ly = lpos if lpos else pts[len(pts) // 2]
            self.text(lx, ly, label, fs=fs, color=color)

    def fence(self, pts, kind):
        style = {"f18": ("#5a4a3a", 2.4, None), "f085": ("#8c7a64", 1.6, "4 3"), "hedge": ("#4f8a3c", 4.5, None),
                 "wallrun": ("#d19a1c", 4.0, None), "wall": ("#6d6458", 3.0, None)}[kind]
        self.line(pts, style[0], style[1], style[2])

    def route(self, pts, kind, width=4.2):
        self.line(pts, C[kind], width, None, layer="routes")
        # arrow head at the end
        (x0, y0), (x1, y1) = pts[-2], pts[-1]
        a = math.atan2(self.Y(y1) - self.Y(y0), self.X(x1) - self.X(x0))
        tx, ty = self.X(x1), self.Y(y1)
        s = 9
        p = f"{tx:.1f},{ty:.1f} {tx - s * math.cos(a - 0.45):.1f},{ty - s * math.sin(a - 0.45):.1f} {tx - s * math.cos(a + 0.45):.1f},{ty - s * math.sin(a + 0.45):.1f}"
        self.layers["routes"].append(f'<polygon points="{p}" fill="{C[kind]}"/>')

    def mark(self, x, y, kind, label):
        cx, cy = self.X(x), self.Y(y)
        if kind == "hide":
            g = f'<rect x="{cx - 8}" y="{cy - 8}" width="16" height="16" rx="3" fill="{C["hide"]}" stroke="#fff" stroke-width="1.5"/>'
        elif kind == "mem":
            pts = []
            for i in range(10):
                r = 10 if i % 2 == 0 else 4.5
                a = -math.pi / 2 + i * math.pi / 5
                pts.append(f"{cx + r * math.cos(a):.1f},{cy + r * math.sin(a):.1f}")
            g = f'<polygon points="{" ".join(pts)}" fill="{C["mem"]}" stroke="#fff" stroke-width="1.2"/>'
        elif kind == "theo":
            g = f'<circle cx="{cx}" cy="{cy}" r="9" fill="{C["theo"]}" stroke="#fff" stroke-width="1.5"/>'
        elif kind == "watch":
            g = f'<circle cx="{cx}" cy="{cy}" r="7" fill="{C["watch"]}" stroke="#fff" stroke-width="1.5"/>'
        elif kind == "switch":
            g = (f'<polygon points="{cx},{cy - 9} {cx + 9},{cy} {cx},{cy + 9} {cx - 9},{cy}" fill="#fff" '
                 f'stroke="{C["switch"]}" stroke-width="2"/>')
        else:
            g = ""
        self.layers["marks"].append(g)
        col = {"hide": "#fff", "mem": "#fff", "theo": "#fff", "watch": "#fff", "switch": C["switch"]}[kind]
        fs = 8.5 if len(label) > 1 else 9.5
        self.layers["marks"].append(f'<text x="{cx}" y="{cy + 0.5}" font-size="{fs}" font-weight="700" text-anchor="middle" '
                                    f'dominant-baseline="middle" fill="{col}">{esc(label)}</text>')

    def cone(self, x, y, heading_deg, fov_deg, rng):
        cx, cy = self.X(x), self.Y(y)
        a0, a1 = math.radians(heading_deg - fov_deg / 2), math.radians(heading_deg + fov_deg / 2)
        r = rng * PX
        p = (f"M{cx:.1f},{cy:.1f} L{cx + r * math.cos(a0):.1f},{cy + r * math.sin(a0):.1f} "
             f"A{r:.1f},{r:.1f} 0 0 1 {cx + r * math.cos(a1):.1f},{cy + r * math.sin(a1):.1f} Z")
        self.layers["lines"].append(f'<path d="{p}" fill="{C["watch"]}" fill-opacity="0.10" stroke="{C["watch"]}" '
                                    f'stroke-opacity="0.45" stroke-dasharray="3 3"/>')

    def sector(self, x, y, r0, r1, a0, a1):
        """A dashed annular sector (angles in degrees from north, clockwise): the hide-and-seek zone on the ring."""
        def P(r, a):
            t = math.radians(a - 90)
            return self.X(x + r * math.cos(t)), self.Y(y + r * math.sin(t))
        (x0, y0), (x1, y1), (x2, y2), (x3, y3) = P(r1, a0), P(r1, a1), P(r0, a1), P(r0, a0)
        big = 1 if (a1 - a0) > 180 else 0
        d = (f"M{x0:.1f},{y0:.1f} A{r1 * PX:.1f},{r1 * PX:.1f} 0 {big} 1 {x1:.1f},{y1:.1f} L{x2:.1f},{y2:.1f} "
             f"A{r0 * PX:.1f},{r0 * PX:.1f} 0 {big} 0 {x3:.1f},{y3:.1f} Z")
        self.layers["lines"].append(f'<path d="{d}" fill="none" stroke="{C["theo"]}" stroke-width="2.2" stroke-dasharray="8 5"/>')

    def text(self, x, y, s, fs=11, color=None, anchor="middle", weight="400", italic=False):
        st = ' font-style="italic"' if italic else ""
        for i, ln in enumerate(s.split("\n")):
            self.layers["text"].append(f'<text x="{self.X(x):.1f}" y="{self.Y(y) + i * fs * 1.2:.1f}" font-size="{fs}" '
                                       f'font-weight="{weight}" text-anchor="{anchor}" fill="{color or C["ink"]}"{st}>{esc(ln)}</text>')

    def svg(self):
        W = int(M * 2 + self.w * PX + LEGEND_W)
        plan_h = 70 + self.h * PX
        sec_h = self.section.height_px() if self.section else 0
        H = int(M * 2 + plan_h + sec_h + 50)
        out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" '
               f'font-family="Segoe UI, Helvetica, Arial, sans-serif">',
               f'<rect width="{W}" height="{H}" fill="{C["bg"]}"/>',
               f'<text x="{M}" y="{M - 18}" font-size="24" font-weight="700" fill="{C["ink"]}">{esc(self.title)}</text>',
               f'<text x="{M}" y="{M + 8}" font-size="14" fill="{C["dim"]}">{esc(self.subtitle)}</text>']
        # 10 m grid
        g = []
        for gx in range(int(self.ox), int(self.ox + self.w) + 1, 10):
            g.append(f'<line x1="{self.X(gx):.1f}" y1="{self.Y(self.oy):.1f}" x2="{self.X(gx):.1f}" y2="{self.Y(self.oy + self.h):.1f}" stroke="{C["grid"]}"/>')
        for gy in range(int(self.oy), int(self.oy + self.h) + 1, 10):
            g.append(f'<line x1="{self.X(self.ox):.1f}" y1="{self.Y(gy):.1f}" x2="{self.X(self.ox + self.w):.1f}" y2="{self.Y(gy):.1f}" stroke="{C["grid"]}"/>')
        out += g
        for k in ("ground", "build", "lines", "routes", "marks", "text"):
            out += self.layers[k]
        # scale bar and north arrow
        sx, sy = self.X(self.ox), self.Y(self.oy + self.h) + 26
        out.append(f'<rect x="{sx}" y="{sy}" width="{10 * PX}" height="6" fill="{C["ink"]}"/>')
        out.append(f'<rect x="{sx + 10 * PX}" y="{sy}" width="{10 * PX}" height="6" fill="#fff" stroke="{C["ink"]}"/>')
        out.append(f'<text x="{sx}" y="{sy + 20}" font-size="11" fill="{C["ink"]}">0</text>')
        out.append(f'<text x="{sx + 10 * PX}" y="{sy + 20}" font-size="11" text-anchor="middle" fill="{C["ink"]}">10</text>')
        out.append(f'<text x="{sx + 20 * PX}" y="{sy + 20}" font-size="11" text-anchor="middle" fill="{C["ink"]}">20 m</text>')
        out.append(f'<text x="{sx + 24 * PX}" y="{sy + 7}" font-size="11" fill="{C["dim"]}">grid 10 m; heights in metres above the street</text>')
        # legend
        lx = M + self.w * PX + 30
        ly = M + 70
        out.append(f'<text x="{lx}" y="{ly}" font-size="15" font-weight="700" fill="{C["ink"]}">Legend</text>')
        ly += 22
        for k in ("street", "back", "roof", "under"):
            out.append(f'<line x1="{lx}" y1="{ly - 4}" x2="{lx + 34}" y2="{ly - 4}" stroke="{C[k]}" stroke-width="4.5"/>')
            out.append(f'<text x="{lx + 42}" y="{ly}" font-size="12" fill="{C["ink"]}">{esc(ROUTE_NAMES[k])}</text>')
            ly += 20
        items = [("f18", "Fence or wall 1.8 m: mantle from the ground"), ("f085", "Picket fence 0.85 m: hop"),
                 ("hedge", "Hedge 1.2 m: hides a crouched Nadia"), ("wallrun", "Runnable wall, 3 m+ (gold stripe)")]
        for kind, txt in items:
            style = {"f18": ("#5a4a3a", 2.4, ""), "f085": ("#8c7a64", 1.6, ' stroke-dasharray="4 3"'),
                     "hedge": ("#4f8a3c", 4.5, ""), "wallrun": ("#d19a1c", 4.0, "")}[kind]
            out.append(f'<line x1="{lx}" y1="{ly - 4}" x2="{lx + 34}" y2="{ly - 4}" stroke="{style[0]}" stroke-width="{style[1]}"{style[2]}/>')
            out.append(f'<text x="{lx + 42}" y="{ly}" font-size="12" fill="{C["ink"]}">{esc(txt)}</text>')
            ly += 20
        marks = [("hide", "H", "Hiding place (hide-and-seek, stealth)"), ("mem", "", "Memory slot"),
                 ("theo", "T", "Theo counts here (home base)"), ("watch", "", "Watcher and sight cone"),
                 ("switch", "S", "Switch: open or shut per night")]
        for kind, lab, txt in marks:
            tmp = Plan("", "", 0, 0)
            tmp.X = lambda v, lx=lx: lx + 12
            tmp.Y = lambda v, ly=ly: ly - 4
            tmp.mark(0, 0, kind, lab)
            out += tmp.layers["marks"]
            out.append(f'<text x="{lx + 42}" y="{ly}" font-size="12" fill="{C["ink"]}">{esc(txt)}</text>')
            ly += 24
        ly += 6
        for txt in self.legend_extra:
            for i, ln in enumerate(txt.split("\n")):
                out.append(f'<text x="{lx}" y="{ly}" font-size="12" fill="{C["ink"]}" font-weight="{"600" if i == 0 else "400"}">{esc(ln)}</text>')
                ly += 16
            ly += 6
        if self.section:
            out += self.section.render(M, M + plan_h + 70)
        out.append("</svg>")
        return "\n".join(out)


class Section:
    """A cut through the plan, drawn as a profile: ground, buildings (with pitched roofs), heights and move arcs."""

    def __init__(self, title, x0, x1, zmin=-3, zmax=9, scale=PX):
        self.title, self.x0, self.x1, self.zmin, self.zmax, self.s = title, x0, x1, zmin, zmax, scale
        self.items = []
        self.arcs = []
        self.labels = []

    def height_px(self):
        return 70 + (self.zmax - self.zmin) * self.s * 2.6 + 40

    def ground(self, pts, fill="#cdbf9f"):
        self.items.append(("ground", pts, fill))

    def block(self, a, b, h, fill, label="", roof=None, base=0.0):
        self.items.append(("block", (a, b, h, roof, base), fill, label))

    def arc(self, a, b, za, zb, peak, color, label="", lx=None, lz=None):
        self.arcs.append((a, b, za, zb, peak, color, label, lx, lz))

    def label(self, x, z, s, color=None, fs=11):
        self.labels.append((x, z, s, color, fs))

    def render(self, ox, oy):
        vs = self.s * 2.6   # vertical exaggeration 2.6x so heights read
        H = (self.zmax - self.zmin) * vs
        top = oy + 44

        def X(x):
            return ox + (x - self.x0) * self.s

        def Z(z):
            return top + (self.zmax - z) * vs

        o = [f'<text x="{ox}" y="{oy + 10}" font-size="16" font-weight="700" fill="{C["ink"]}">{esc(self.title)}</text>',
             f'<text x="{ox}" y="{oy + 26}" font-size="11" fill="{C["dim"]}">heights drawn 2.6x taller than lengths so they read; '
             f'numbers are true metres</text>']
        for z in range(int(self.zmin), int(self.zmax) + 1):
            if z % 1 == 0:
                o.append(f'<line x1="{X(self.x0):.1f}" y1="{Z(z):.1f}" x2="{X(self.x1):.1f}" y2="{Z(z):.1f}" stroke="{C["grid"]}"/>')
                if z % 2 == 0 or z in (-3, -1, 1, 3, 5):
                    o.append(f'<text x="{X(self.x1) + 6:.1f}" y="{Z(z) + 4:.1f}" font-size="10" fill="{C["dim"]}">{z} m</text>')
        for it in self.items:
            if it[0] == "ground":
                pts, fill = it[1], it[2]
                p = [f"{X(a):.1f},{Z(b):.1f}" for a, b in pts]
                p += [f"{X(pts[-1][0]):.1f},{Z(self.zmin):.1f}", f"{X(pts[0][0]):.1f},{Z(self.zmin):.1f}"]
                o.append(f'<polygon points="{" ".join(p)}" fill="{fill}" stroke="#8d7f63"/>')
        for it in self.items:
            if it[0] == "block":
                (a, b, h, roof, base), fill, label = it[1], it[2], it[3]
                o.append(f'<rect x="{X(a):.1f}" y="{Z(base + h):.1f}" width="{(b - a) * self.s:.1f}" height="{h * vs:.1f}" '
                         f'fill="{fill}" stroke="#6d6458"/>')
                if roof:
                    o.append(f'<polygon points="{X(a):.1f},{Z(base + h):.1f} {X((a + b) / 2):.1f},{Z(base + roof):.1f} '
                             f'{X(b):.1f},{Z(base + h):.1f}" fill="{fill}" stroke="#6d6458"/>')
                if label:
                    zt = base + (roof if roof else h)
                    o.append(f'<text x="{X((a + b) / 2):.1f}" y="{Z(zt) - 6:.1f}" font-size="10.5" text-anchor="middle" '
                             f'fill="{C["ink"]}">{esc(label)}</text>')
        for a, b, za, zb, peak, color, label, lx, lz in self.arcs:
            mx = (a + b) / 2
            o.append(f'<path d="M{X(a):.1f},{Z(za):.1f} Q{X(mx):.1f},{Z(peak):.1f} {X(b):.1f},{Z(zb):.1f}" fill="none" '
                     f'stroke="{color}" stroke-width="2.4" stroke-dasharray="6 4"/>')
            o.append(f'<circle cx="{X(b):.1f}" cy="{Z(zb):.1f}" r="3" fill="{color}"/>')
            if label:
                o.append(f'<text x="{X(lx if lx is not None else mx):.1f}" y="{Z(lz if lz is not None else peak) - 4:.1f}" font-size="10.5" text-anchor="middle" fill="{color}" '
                         f'font-weight="600">{esc(label)}</text>')
        for x, z, s, color, fs in self.labels:
            for i, ln in enumerate(s.split("\n")):
                o.append(f'<text x="{X(x):.1f}" y="{Z(z) + i * fs * 1.15:.1f}" font-size="{fs}" text-anchor="middle" '
                         f'fill="{color or C["ink"]}">{esc(ln)}</text>')
        return o


# ---------------------------------------------------------------------------------------------------------------
# Proposal A: the deep block. The street is the front layer; behind the north houses are back gardens and a rear
# lane; above them the roofs. The creek runs behind the south houses and under the street through a culvert.
# x runs along the street (0 = Walt's shop at the bottom, 120 = the hill house gate at the top); y runs north to south.
# ---------------------------------------------------------------------------------------------------------------
def plan_a():
    p = Plan("A. The deep block: front, back and above",
             "Larch Street built backwards and upwards: three stacked routes along one block, cross-linked every 20-25 m. "
             "Walt's shop at the bottom (west), the hill house at the top (east).", 124, 112)
    # ground bands
    p.rect(0, 0, 100, 5, "#b8a48c", "Garages of Birch Lane (boundary): brick backs 3.2 m, runnable", layer="ground", fs=10)
    p.rect(0, 5, 100, 5, C["lane"], None, layer="ground")
    p.text(50, 8.3, "THE LANE  5 m wide, tarmac, falls 2 m toward Walt's end: a slide lane", fs=11, weight="600")
    p.rect(0, 10, 100, 36, C["garden"], None, layer="ground")
    p.rect(0, 46, 100, 6, C["lawn"], None, layer="ground")
    p.rect(0, 52, 124, 2, C["pavement"], None, layer="ground")
    p.rect(0, 54, 124, 8, C["road"], None, layer="ground")
    p.rect(0, 62, 124, 2, C["pavement"], None, layer="ground")
    p.text(10, 58.6, "LARCH STREET", fs=12, weight="700", anchor="start")
    p.rect(0, 64, 100, 6, C["lawn"], None, layer="ground")
    p.rect(0, 82, 100, 18, C["garden"], None, layer="ground")
    p.rect(0, 100, 124, 3, "#cdbf9f", None, layer="ground")
    p.rect(0, 103, 124, 6, C["water"], "THE CREEK  gully 2 m deep, 5.5 m wide (Nadia's bike jump)", layer="ground", fs=11)
    # the empty lot and hill path (east)
    p.rect(100, 0, 24, 52, "#dfd6c4", None, layer="ground")
    p.text(112, 3, "HILL PATH\nterraces 1.5 m:\nthe mantle stair", fs=10)
    p.rect(104, 12, 18, 14, C["hill"], "Hill house gate\n(chained)\nseen from\neverywhere", "+6.0", fs=10)
    p.rect(100, 64, 24, 36, "#dfd6c4", None, layer="ground")
    # north houses with side passages A and B
    for (x0, name, h) in ((4, "House 1\nMrs. Lindqvist", "eaves 5.2, ridge 8"),
                          (24.5, "House 3\n(key hook at\nthe back door)", "eaves 5.2, ridge 8"),
                          (45, "House 5", "eaves 5.2, ridge 8")):
        p.rect(x0, 34, 16, 12, C["house"], name, h, fs=10)
        p.rect(x0 + 3, 46, 7, 2, "#e9b48f", None)  # porch
        p.text(x0 + 6.5, 49.6, "porch roof 2.9", fs=9)
    p.text(22.25, 40, "A", fs=13, weight="700", color=C["roof"])
    p.text(42.75, 40, "B", fs=13, weight="700", color=C["roof"])
    p.text(22.25, 43.5, "4.5 m", fs=9, color=C["dim"])
    p.text(42.75, 43.5, "4.5 m", fs=9, color=C["dim"])
    # half-built extension of house 5
    p.rect(61, 32, 10, 14, C["build"], "Half-built\nextension\nblock walls 3 m,\nno roof", None, fs=9, dash="5 3")
    p.rect(71, 34, 3, 12, "#bdb4a5", None, stroke="#6d6458")
    p.text(72.5, 31.5, "scaffold\n2 / 4 / 6", fs=9)
    # garages on the lane
    for gx, lab in ((2, None), (8, None), (26, None), (34, None), (47, None), (53, None)):
        p.rect(gx, 10, 6, 6, C["garage"], lab, fs=9)
    p.text(8, 12.6, "garages, flat roofs 2.7", fs=8.5)
    p.text(32, 12.6, "garages 2.7 (S1 = door)", fs=8.5)
    p.text(53, 12.6, "garages 2.7", fs=8.5)
    # gardens: G1 Lindqvist
    p.rect(3, 20, 6, 6, "#e7f0f2", "Green-\nhouse 2.4", fs=9, stroke="#7d9aa3")
    p.circle(15, 24, 3.2, C["tree"], None)
    p.rect(13.5, 22.5, 3, 3, "#b98c5e", None)
    p.text(15, 28.7, "oak + treehouse 3.0", fs=9)
    p.rect(4, 31, 16, 3, C["deck"], "deck 1.2 (crawl under)", fs=9)
    p.fence([(1, 18), (11, 18)], "hedge")
    # G3
    p.circle(32, 24, 2.2, "#c2d8ee", "tramp-\noline", fs=8)
    p.line([(26, 29), (38, 29)], "#9aa0a6", 1.2, "2 2")
    p.text(32, 30.6, "washing line: sheets block sight", fs=8.5, color=C["dim"])
    p.rect(37.5, 18, 5, 4, C["shed"], None, fs=9)
    p.text(40, 23.4, "shed 2.2", fs=8.5)
    p.rect(24.5, 31, 16, 3, C["deck"], "deck 1.2 (crawl under)", fs=9)
    p.rect(21, 30, 2.5, 3, "#8c8c8c", None)
    p.text(22.25, 29.2, "bins", fs=8.5)
    # G5 builders
    p.rect(47, 20, 5, 3, "#c9b08a", "pallets 1.0", fs=8.5)
    p.rect(54, 22, 4, 4, "#e6d7a8", "sand", fs=8.5)
    p.rect(64, 14, 6, 3, "#a7a39b", "skip 1.6", fs=8.5)
    # fences between north plots (1.8) and along the lane (1.8 with gates)
    for fx in (22.25, 42.75, 62.5):
        p.fence([(fx, 10), (fx, 34)], "f18")
    p.fence([(0, 16.2), (100, 16.2)], "f18")
    p.fence([(0, 52), (100, 52)], "f085")
    # runnable walls
    p.fence([(0, 5), (98, 5)], "wallrun")
    p.fence([(61.2, 32), (61.2, 46)], "wallrun")
    p.fence([(20, 34), (20, 46)], "wallrun")
    p.fence([(24.5, 34), (24.5, 46)], "wallrun")
    p.fence([(40.5, 34), (40.5, 46)], "wallrun")
    p.fence([(45, 34), (45, 46)], "wallrun")
    # south side: Walt's shop, houses 2 and 4, the snicket, the culvert
    p.rect(2, 70, 18, 12, C["shop"], "Walt's shop\nAbernathy Cycles\nflat roof 4.0", None, fs=10)
    p.rect(6, 69.2, 5, 0.8, "#6d6458", None)
    p.text(8.5, 66.8, "door half open 1.15: slide", fs=9)
    p.rect(20, 72, 6, 8, "#d9c9b0", "yard,\nlean-to\n2.4", fs=8.5)
    p.rect(28, 70, 16, 12, C["house"], "House 2\nMr. Okafor", "eaves 5.2, ridge 8", fs=10)
    p.rect(48, 70, 16, 12, C["house"], "House 4", "eaves 5.2, ridge 8", fs=10)
    p.text(46, 76, "SNICKET\n4 m", fs=9.5, weight="600")
    p.fence([(44, 70), (44, 82)], "wallrun")
    p.fence([(48, 70), (48, 82)], "wallrun")
    p.fence([(0, 64), (100, 64)], "f085")
    for fx in (26.5, 46):
        p.fence([(fx, 82), (fx, 100)], "f18")
    p.rect(30, 86, 10, 4, C["deck"], "deck on stilts 1.2", fs=9)
    p.rect(52, 88, 4, 3, C["shed"], "shed", fs=8.5)
    p.rect(66, 84, 30, 14, "#d3e2c0", "Allotment strip:\nbean rows 1.8,\ncold frames", fs=9.5)
    # the culvert: the creek turns north under the street into the empty lot (the pond)
    p.rect(86, 52, 6, 51, "#b3d6ea", None, layer="ground", opacity=0.75)
    p.rect(86, 54, 6, 8, "#7fb1d1", None, stroke="#2f7fc0", dash="4 2")
    p.text(89, 50.5, "CULVERT\n1.15 m:\nslide", fs=9, color=C["under"], weight="600")
    p.rect(80, 28, 18, 14, "#a9d0e6", "the pond\n(empty lot)", fs=9.5)
    p.text(90, 22, "EMPTY LOT\nrubble, a car on\nblocks (roof 1.5)", fs=9.5)
    # the block party on the road
    p.rect(38, 54.5, 44, 7, C["party"], None, layer="ground", stroke="#d48aa3", dash="5 3")
    p.rect(52, 55, 8, 6, "#f3b8c9", "marquee\nroof 2.6", fs=9)
    p.rect(41, 56.5, 9, 1.5, "#fff", None)
    p.rect(62, 56.5, 9, 1.5, "#fff", None)
    p.text(66, 60.5, "BLOCK PARTY: trestles, bunting", fs=9.5, weight="600")
    p.rect(36.5, 55, 1, 6, "#d48aa3", None)
    p.rect(82.5, 55, 1, 6, "#d48aa3", None)
    # watchers and cones (the street is the watched layer)
    p.mark(13, 49.5, "watch", "")
    p.cone(13, 49.5, 90, 110, 16)
    p.text(13, 52.6, "Mrs. Lindqvist", fs=8.5, color=C["watch"])
    p.mark(57, 58, "watch", "")
    p.cone(57, 58, -90, 120, 18)
    p.text(57, 66.3, "Mr. Okafor at the grill (smoke hides house 5's front)", fs=8.5, color=C["watch"])
    p.mark(65, 58, "watch", "")
    p.cone(65, 58, 200, 90, 20)
    p.text(70, 57, "Elias", fs=8.5, color=C["watch"])
    # routes from Walt's shop (bottom) to the hill gate (top): four stacked lines
    p.route([(6, 63), (30, 63), (36, 61.5), (84, 61.5), (98, 58), (106, 50), (110, 30)], "street")
    p.route([(21, 63), (22.25, 50), (22.25, 37), (14, 30), (8, 18), (8, 7.5), (40, 7.5), (80, 7.5), (98, 7.5), (104, 18)], "back")
    p.route([(8.5, 47.5), (11, 44), (12, 36.6), (22.25, 36.6), (32, 36.6), (42.75, 36.6), (58, 36.6), (61.2, 44),
             (61.2, 33), (68, 26), (82, 18), (101, 14)], "roof")
    p.text(33, 35.4, "the back pitch: hidden from the street", fs=8.5, color=C["roof"], weight="600")
    p.route([(8.5, 69.5), (10, 86), (20, 101.5), (60, 106), (89, 106), (89, 62), (89, 52), (89, 44), (100, 36)], "under")
    # hide-and-seek zone (gardens of houses 1 and 3 plus the lane's west half)
    p.rect(0.5, 5.5, 42, 28.8, "none", None, layer="lines", stroke=C["theo"], sw=2.2, dash="8 5")
    p.text(1.5, 12.5, "", fs=10)
    p.mark(32, 24, "theo", "T")
    hides = [(6.5, 33, "H1"), (15, 24, "H2"), (4.5, 23, "H3"), (35, 33, "H4"), (40, 20, "H5"), (22.25, 31.5, "H6"),
             (29, 13, "H7"), (5, 13, "H8")]
    for x, y, lab in hides:
        p.mark(x, y, "hide", lab)
    mems = [(9, 19, ""), (13, 33.6, ""), (30, 29.6, ""), (41, 12.5, ""), (17, 7.5, ""), (22.25, 45, "")]
    for x, y, lab in mems:
        p.mark(x, y, "mem", lab)
    # street memories (the existing four) and later ones
    for x, y in ((8.5, 67), (35, 50), (58, 54), (89, 40), (72.5, 30), (104, 32)):
        p.mark(x, y, "mem", "")
    # switches (open or shut per night)
    for x, y, lab in ((29, 16.2, "S1"), (22.25, 52, "S2"), (32, 34, "S3"), (72.5, 42, "S4"), (89, 101, "S5"),
                      (98, 7.5, "S6"), (46, 64, "S7"), (10.8, 69.6, "S8")):
        p.mark(x, y, "switch", lab)
    p.legend_extra = [
        "Hide-and-seek zone (dashed teal):\ngardens of houses 1 and 3 + the lane's\nwest half, 42 x 29 m. Theo counts on\nthe trampoline; 8 hiding places, 6 memories.",
        "Switches tonight might be:\nS1 garage door G3 (garden to lane)\nS2 side gate A   S3 house 3 back door\nS4 scaffold ladder   S5 plank over the creek\nS6 lane's top gate   S7 snicket gate\nS8 Walt's door (half open / shut)",
        "Kit sizes (MOVEMENT.md):\nfence 1.8 = mantle; garage 2.7 = jump+mantle\n(reach 3.15); eaves 5.2 from a porch or\ngarage roof; side passages 4.5 = wall to wall\n(4-6); house side walls 12 m = one wallrun\n(8-15); roof gaps 4.5 = single jump (6.1);\nslide gaps 1.15; double jump 9.9 m.",
    ]
    s = Section("Section through house 3 (x = 32): lane wall to creek, looking east", -2, 112, zmin=-3, zmax=10)
    s.ground([(-2, 0), (103, 0), (103, -2), (109, -2), (109, 0), (112, 0)])
    s.block(0, 5, 3.2, "#b8a48c", "wall 3.2")
    s.block(10, 16, 2.7, C["garage"], "garage 2.7")
    s.label(7.5, -1.0, "lane", C["dim"], 10)
    s.block(16.1, 16.3, 1.8, "#5a4a3a")
    s.block(31, 34, 1.2, C["deck"], "deck 1.2")
    s.block(34, 46, 5.2, C["house"], "house 3: eaves 5.2, ridge 8", roof=8)
    s.block(46, 48, 2.9, "#e9b48f", "porch 2.9")
    s.block(51.9, 52.1, 0.85, "#8c7a64")
    s.block(52, 64, 0.02, C["road"])
    s.block(55, 61, 2.6, C["party"], "marquee 2.6")
    s.block(70, 82, 5.2, C["house"], "house 2", roof=8)
    s.block(86, 90, 1.2, C["deck"], "stilt deck")
    s.arc(7, 11, 0, 2.7, 3.7, C["back"], "jump + mantle", lx=4, lz=4.6)
    s.arc(49.5, 47.5, 0, 2.9, 3.8, C["roof"], "jump + mantle", lx=52, lz=4.3)
    s.arc(47, 45.2, 2.9, 5.2, 6.3, C["roof"], "porch roof -> eaves", lx=52, lz=6.7)
    s.arc(97, 111, 0, 0, 1.25, C["under"], "5.5 m creek jump", lx=104, lz=2.0)
    s.label(106, -1.6, "creek bed -2", C["under"], 10)
    s.label(23, 3.0, "Theo on the trampoline:\nsees over the fences at the top", C["theo"], 10)
    s.label(40, 9.2, "back pitch: hidden from the street", C["roof"], 10)
    s.label(76, 9.2, "street watchers see the front pitches", C["watch"], 10)
    p.section = s
    return p


# ---------------------------------------------------------------------------------------------------------------
# Proposal B: the hill. The street climbs 12 m from Walt's shop to the hill house. Terraces above the street,
# houses below it on the creek side, the creek bed under the street at the bottom.
# ---------------------------------------------------------------------------------------------------------------
def plan_b():
    p = Plan("B. The hill: the street climbs, the block is built into the slope",
             "Larch Street rises 12 m from Walt's shop (west, at the creek) to the hill house (east). Height does the work: "
             "terraces above the street, houses below it, the creek bed under it.", 124, 100)
    # bands (north = uphill): Top Path, terraces, street, the downhill houses, the creek bed
    p.rect(0, 0, 124, 6, "#d7cfbe", None, layer="ground")
    p.text(64, 3.6, "THE TOP PATH  (+9 above the street, behind a 2.7 wall): sees the whole street, the stalking height", fs=11, weight="600")
    p.rect(0, 6, 124, 30, "#e6eed9", None, layer="ground")
    p.rect(0, 36, 124, 4, C["lawn"], None, layer="ground")
    p.rect(0, 40, 124, 10, C["road"], None, layer="ground")
    p.text(56, 45.6, "LARCH STREET  climbing 10 %: +0 at Walt's, +12 at the gate", fs=12, weight="700", anchor="start")
    p.rect(0, 50, 124, 4, C["pavement"], None, layer="ground")
    p.rect(0, 54, 124, 30, "#e3eed3", None, layer="ground")
    p.rect(0, 84, 124, 8, C["water"], None, layer="ground")
    p.text(66, 88.6, "THE CREEK BED  (3 m below the street; dry and stony in summer): the low route", fs=11, weight="600")
    p.rect(0, 92, 124, 8, "#cdbf9f", None, layer="ground")
    # the ravine on the west edge: the creek comes down off the hill and passes under the street
    p.rect(0, 6, 8, 78, "#b3d6ea", None, layer="ground", opacity=0.8)
    p.rect(0, 40, 8, 10, "#7fb1d1", None, stroke="#2f7fc0", dash="4 2")
    p.text(4, 20, "RAVINE\n(wooded)", fs=8.5, color=C["under"], weight="600")
    p.text(4, 37.6, "culvert", fs=8.5, color=C["under"], weight="600")
    p.text(4, 52.6, "1.15:\nslide", fs=8.5, color=C["under"], weight="600")
    # retaining walls on the uphill side (each a mantle from the terrace below)
    for y, h in ((36, "front wall 2.7 (= the garage roofs)"), (26, "terrace wall 1.8"), (13, "terrace wall 1.8")):
        p.fence([(8, y), (96, y)], "f18")
        p.text(95, y - 0.7, h, fs=8.5, color=C["dim"], anchor="end")
    # uphill houses on the middle terrace, garages dug into the front wall (their roofs are the front terraces)
    for x0, name in ((12, "House 1  Lindqvist"), (32, "House 3"), (58, "House 5")):
        p.rect(x0, 15, 16, 10, C["house"], name, "eaves 5.2 above its terrace", fs=9.5)
        p.rect(x0 + 4, 36, 8, 4, C["garage"], None)
    p.text(20, 33.8, "dug-in garages: roofs = front terraces (+2.7)", fs=8.5, anchor="start")
    p.rect(74, 15, 8, 10, C["build"], "half-built\nextension", fs=8.5, dash="5 3")
    # the Steps between houses 3 and 5
    p.rect(50, 6, 5, 34, "#efe2c8", None, stroke="#a98f62")
    for yy in range(8, 40, 3):
        p.line([(50, yy), (55, yy)], "#a98f62", 1)
    p.text(56, 30.5, "THE STEPS: public stair, landings\nevery 1.5 m; ramp edge = slide", fs=9, anchor="start", weight="600")
    p.fence([(48.2, 15), (48.2, 25)], "wallrun")
    p.fence([(56.8, 15), (56.8, 25)], "wallrun")
    # the hill house at the top (east, high)
    p.rect(98, 6, 24, 30, C["hill"], "Hill house\n(gate chained)\n+15 above Walt's", fs=10)
    # downhill side: entered from the street at the upper floor, gardens dropping to the creek
    p.rect(10, 54, 18, 12, C["shop"], "Walt's shop\nstreet door +0,\nworkshop -3", fs=9.5)
    for x0, name in ((32, "House 2  Okafor"), (58, "House 4")):
        p.rect(x0, 56, 16, 12, C["house"], name, "street floor +0, garden floor -3", fs=9.5)
        p.rect(x0 + 6, 50, 3, 6, "#c8b49a", None)
        p.rect(x0, 68, 16, 4, C["deck"], "stilt deck: crawl under (1.4)", fs=8.5)
    p.text(53, 77, "SNICKET\nsteps\ndown 3 m", fs=9, weight="600")
    p.fence([(48.2, 56), (48.2, 84)], "wallrun")
    p.fence([(57.8, 56), (57.8, 84)], "wallrun")
    p.rect(76, 58, 18, 22, "#d3e2c0", "allotments\nstepping down", fs=9)
    # block party on the street's one flat landing, outside house 3
    p.rect(28, 40.5, 22, 9, C["party"], None, stroke="#d48aa3", dash="5 3")
    p.text(39, 43.6, "BLOCK PARTY (the street's", fs=9.5, weight="600")
    p.text(39, 45.0, "only flat landing)", fs=9.5)
    # watchers
    p.mark(42, 47, "watch", "")
    p.cone(42, 47, 0, 150, 22)
    p.text(42, 51.8, "Okafor at the grill", fs=8.5, color=C["watch"])
    p.mark(20, 29, "watch", "")
    p.cone(20, 29, 90, 100, 12)
    p.text(26, 29.5, "Lindqvist", fs=8.5, color=C["watch"], anchor="start")
    # routes: Walt's (bottom left) to the hill gate (top right)
    p.route([(14, 47.5), (60, 47.5), (92, 46), (100, 38)], "street")
    p.route([(26, 38), (30, 31), (44, 30), (52.5, 24), (52.5, 10), (70, 9), (97, 9)], "back")
    p.route([(14, 38), (14, 20), (28, 20), (48.2, 20), (58, 20), (74, 20), (86, 14), (97, 14)], "roof")
    p.route([(18, 66), (14, 88), (60, 88), (104, 88), (114, 60), (114, 37)], "under")
    # hide-and-seek on the Steps and the terraces of houses 3 and 5
    p.rect(30, 6, 46, 30, "none", None, layer="lines", stroke=C["theo"], sw=2.2, dash="8 5")
    p.mark(52.5, 27.5, "theo", "T")
    for x, y, lab in ((36, 28, "H1"), (40, 10, "H2"), (62, 10, "H3"), (68, 28, "H4"), (72, 33, "H5"), (34, 33, "H6")):
        p.mark(x, y, "hide", lab)
    for x, y in ((44, 9), (47, 33), (64, 33), (70, 9), (31, 20), (78, 26)):
        p.mark(x, y, "mem", "")
    for x, y, lab in ((52.5, 38, "S1"), (4, 45, "S2"), (53, 56, "S3"), (97, 30, "S4")):
        p.mark(x, y, "switch", lab)
    p.legend_extra = [
        "Three heights instead of three bands:\nthe creek bed (-3, hidden), the street (0,\nwatched), the terraces and Top Path\n(+2 to +15, see everything).",
        "Like Bottle Street under Clavering\nBoulevard: the creek runs under the\nstreet through the culvert.",
        "Costs: sloped terrain to author; the\nwhole street is visible from above,\nso stealth on the street is hard.",
    ]
    s = Section("Section through houses 3 and 2 (x = 40): Top Path to creek bed, looking east", -2, 100, zmin=-4, zmax=14)
    s.ground([(-2, 9), (6, 9), (6, 6.3), (13, 6.3), (13, 4.5), (26, 4.5), (26, 2.7), (40, 2.7), (40, 0), (54, 0),
              (56, -1.0), (58, -1.0), (68, -1.0), (72, -3.0), (84, -3.0), (84, -3.5), (92, -3.5), (92, -2.4), (100, -2.4)])
    s.block(15, 25, 5.2, C["house"], "house 3 (on its terrace)", roof=7.8, base=4.5)
    s.block(36, 40, 2.69, C["garage"], "dug-in garage", base=0.0)
    s.block(41, 49, 2.6, C["party"], "party", base=0.0)
    s.block(56, 68, 5.6, C["house"], "house 2: street floor 0, garden floor -3", roof=8.0, base=-3.0)
    s.block(68, 72, 1.4, C["deck"], "", base=-3.0)
    s.arc(44, 39.5, 0, 2.7, 3.7, C["back"], "jump + mantle 2.7", lx=47, lz=4.4)
    s.arc(28, 25.5, 2.7, 4.5, 5.4, C["back"], "mantle 1.8", lx=31, lz=6.0)
    s.arc(74, 86, -3, -3.5, -2.0, C["under"], "", lx=80, lz=-1.6)
    s.label(88, -3.2, "creek", C["under"], 10)
    s.label(70, -2.2, "crawl", C["under"], 9)
    s.label(3, 10.4, "Top Path +9: the stalking height", C["roof"], 10)
    s.label(47, 1.0, "street 0", C["dim"], 10)
    p.section = s
    return p


# ---------------------------------------------------------------------------------------------------------------
# Proposal C: the close. The street ends in a turning circle with the block party in the middle; a back way loops
# behind every house; the hill house sits above the head of the close.
# ---------------------------------------------------------------------------------------------------------------
def plan_c():
    p = Plan("C. The close: a loop around the block party",
             "Larch Street ends in a turning circle: the party is the watched hub, the Back Way loops behind every house. "
             "100 x 100 m.", 104, 104)
    cx, cy = 52, 54
    p.circle(cx, cy, 46, "#cdbf9f", None, layer="ground")
    p.circle(cx, cy, 43, "#b8a48c", None, layer="ground")
    p.circle(cx, cy, 39, C["lane"], None, layer="ground")
    p.circle(cx, cy, 35.5, C["garden"], None, layer="ground")
    p.circle(cx, cy, 20, C["lawn"], None, layer="ground")
    p.circle(cx, cy, 16, C["road"], None, layer="ground")
    p.circle(cx, cy, 8, "#cde4b6", None, layer="ground")
    p.text(cx, cy - 9.6, "", fs=9)
    # the stem road out (south) to the bus stop
    p.rect(46, 70, 12, 34, C["road"], None, layer="ground")
    p.text(52, 101, "the stem: out to the bus stop\n(the escape route)", fs=10, weight="600")
    p.text(cx, cy + 37.5, "", fs=10)
    # hub: party on the island
    p.rect(cx - 4, cy - 3, 8, 6, "#f3b8c9", "marquee\n2.6", fs=9)
    p.circle(cx + 5, cy + 5, 2, C["tree"], None)
    p.text(cx, cy - 10.5, "THE PARTY ISLAND (the hub)", fs=10, weight="600")
    # houses around the ring (angle from north, clockwise), Walt's shop by the stem
    names = [(-110, "Walt's\nshop 4.0"), (-60, "House 1"), (-10, "House 3"), (40, "House 5\n+ext."), (95, "House 2"),
             (145, "House 4")]
    for ang, name in names:
        a = math.radians(ang - 90)
        r = 27
        hx, hy = cx + r * math.cos(a), cy + r * math.sin(a)
        pts = []
        for da, rr in ((-9, 22), (9, 22), (9, 33), (-9, 33)):
            aa = math.radians(ang + da - 90)
            pts.append((cx + rr * math.cos(aa), cy + rr * math.sin(aa)))
        p.poly(pts, C["shop"] if "Walt" in name else C["house"], name, lx=hx, ly=hy - 1, fs=9.5)
        # side passage wall to wall between neighbours (4 m)
    for ang in (-85, -35, 15, 67, 120, 170):
        a = math.radians(ang - 90)
        p.line([(cx + 21 * math.cos(a), cy + 21 * math.sin(a)), (cx + 35.5 * math.cos(a), cy + 35.5 * math.sin(a))],
               C["roof"], 4)
    p.text(cx + 30, cy + 44.5, "THE BACK WAY: 4 m ring alley, garage backs 2.7 / outer wall 3.2", fs=9.5, weight="600")
    # hill house above the head of the close
    p.rect(40, 0.5, 24, 6, C["hill"], "hill house gate (chained), +6 above the ring", fs=9.5)
    # watchers at the party
    for wx, wy, hd in ((cx - 6, cy + 2, 150), (cx + 6, cy - 2, -30), (cx, cy + 6, 90)):
        p.mark(wx, wy, "watch", "")
        p.cone(wx, wy, hd, 120, 18)
    # routes: ring road (street), back way (back), roof ring (roof), under (crawl spaces + the drain)
    def arc_pts(r, a0, a1, n=14):
        return [(cx + r * math.cos(math.radians(a - 90)), cy + r * math.sin(math.radians(a - 90))) for a in
                [a0 + (a1 - a0) * i / n for i in range(n + 1)]]
    p.route(arc_pts(18, 175, 20), "street")
    p.route(arc_pts(41, 185, 360 - 5), "back")
    p.route(arc_pts(28, 200, 330), "roof")
    p.route([(52, 99), (60, 92)] + arc_pts(44.5, 160, 60), "under")
    p.sector(cx, cy, 20.5, 45, -75, 75)
    p.mark(52, 20, "theo", "T")
    for i, ang in enumerate((-40, -20, 0, 20, 40, 60)):
        a = math.radians(ang - 90)
        p.mark(cx + 38 * math.cos(a), cy + 38 * math.sin(a), "hide", f"H{i + 1}")
    for ang in (-130, -70, -5, 55, 110, 160):
        a = math.radians(ang - 90)
        p.mark(cx + 31 * math.cos(a), cy + 31 * math.sin(a), "mem", "")
    for ang, lab in ((-85, "S1"), (15, "S2"), (120, "S3")):
        a = math.radians(ang - 90)
        p.mark(cx + 39 * math.cos(a), cy + 39 * math.sin(a), "switch", lab)
    p.legend_extra = [
        "A loop instead of a line: Theo and the\nwatchers circle; you circle the other way\n(the oldest hide-and-seek trick).",
        "The party island sees the whole ring road\n(a panopticon): the street layer is the\nmost watched of all three proposals.",
        "Costs: the story's linear street and the\n120 m escape move to the stem; less like\nthe slice we have.",
    ]
    s = Section("Section from the island to the outer wall (north), looking west", 0, 50, zmin=-1, zmax=9)
    s.ground([(0, 0), (50, 0)])
    s.block(0, 8, 2.6, "#f3b8c9", "marquee 2.6")
    s.block(22, 33, 5.2, C["house"], "house 3", roof=8)
    s.block(33, 36, 2.7, C["garage"], "garage 2.7")
    s.block(39.5, 43, 3.2, "#b8a48c", "outer wall 3.2")
    s.arc(36, 39.5, 1.0, 2.2, 3.0, C["back"], "kick")
    s.arc(39.5, 36, 2.2, 3.4, 4.2, C["back"], "")
    p.section = s
    return p


# ---------------------------------------------------------------------------------------------------------------
# D: the option the creator dislikes, for comparison. The street stays thin; Walt's back door leads to a dense place.
# ---------------------------------------------------------------------------------------------------------------
def plan_d():
    p = Plan("D. (For comparison) Link to a second, dense place",
             "The street stays as the slice has it; Walt's back door opens on the Yards behind the high street, built "
             "dense. Not recommended: it spends the density away from where the story is.", 124, 70)
    p.rect(0, 30, 124, 10, C["road"], None, layer="ground")
    p.text(60, 35.6, "LARCH STREET (as now: 120 m, five houses, open lawns)", fs=12, weight="700")
    for x0 in (14, 46, 78):
        p.rect(x0, 18, 14, 10, C["house"], None)
    for x0 in (40, 72):
        p.rect(x0, 42, 14, 10, C["house"], None)
    p.rect(2, 42, 14, 12, C["shop"], "Walt's shop", fs=10)
    p.line([(9, 54), (9, 62)], C["under"], 3, "6 4")
    p.rect(0, 60, 60, 10, "#c9c0d6", "THE YARDS (a second, dense place): sheds, roofs, yards, the canal", fs=10)
    p.legend_extra = ["Linking keeps the street easy to build\nbut the dense place has no reason to\nbe in Elias's Saturday, and the street\nstays empty: the creator's objection."]
    return p


# ---------------------------------------------------------------------------------------------------------------
# The Distillery District as a layered schematic (not to scale), from the Dishonored wiki's mission pages.
# ---------------------------------------------------------------------------------------------------------------
def distillery():
    W, H = 1180, 760
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" '
         f'font-family="Segoe UI, Helvetica, Arial, sans-serif">', f'<rect width="{W}" height="{H}" fill="{C["bg"]}"/>',
         f'<text x="40" y="44" font-size="24" font-weight="700" fill="{C["ink"]}">How the Distillery District stacks its routes '
         f'(schematic, not to scale)</text>',
         f'<text x="40" y="68" font-size="13" fill="{C["dim"]}">From the Dishonored wiki\'s pages for High Overseer Campbell and '
         f'House of Pleasure (sources in DENSE-STREET.md). Left: the river; right: the two goals.</text>']
    bands = [(95, 175, "#f7ebc9", "ROOFS AND LEDGES (Blink 10-15 m)"), (185, 455, "#ece6da", "STREETS (the boulevard is watched)"),
             (465, 545, "#dbe8f2", "UNDER (the lower street, sewers, rat tunnels)")]
    for y0, y1, col, lab in bands:
        o.append(f'<rect x="30" y="{y0}" width="{W - 60}" height="{y1 - y0}" rx="8" fill="{col}"/>')
        o.append(f'<text x="44" y="{y0 + 20}" font-size="13" font-weight="700" fill="{C["dim"]}">{esc(lab)}</text>')

    def node(x, y, w, h, title, sub="", col="#fff", stroke="#6d6458"):
        o.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="7" fill="{col}" stroke="{stroke}" stroke-width="1.6"/>')
        o.append(f'<text x="{x + w / 2}" y="{y + 18}" font-size="12.5" font-weight="700" text-anchor="middle" fill="{C["ink"]}">{esc(title)}</text>')
        for i, ln in enumerate(sub.split("\n") if sub else []):
            o.append(f'<text x="{x + w / 2}" y="{y + 34 + i * 14}" font-size="10.5" text-anchor="middle" fill="{C["ink"]}">{esc(ln)}</text>')

    def edge(pts, col, w=3, dash=None, label=None, lp=None):
        d = f' stroke-dasharray="{dash}"' if dash else ""
        o.append(f'<polyline points="{" ".join(f"{a},{b}" for a, b in pts)}" fill="none" stroke="{col}" stroke-width="{w}" '
                 f'stroke-linejoin="round"{d}/>')
        if label:
            x, y = lp
            o.append(f'<text x="{x}" y="{y}" font-size="10.5" fill="{col}" font-weight="600" text-anchor="middle">{esc(label)}</text>')

    # street band nodes
    node(50, 300, 140, 64, "Endoria Street", "waterfront, Samuel's drop-off\nGranny Rags' apartment")
    node(230, 210, 150, 64, "Bottle Street", "gang territory (neutral in\nmission 3); the distillery yard")
    node(230, 360, 150, 70, "Dunwall Distillery", "Slackjaw; planks overhead\n-> roof -> pipes -> inside")
    node(420, 300, 140, 64, "Bloodox Way", "dead end: Griff's shop\n(rescue = a merchant)")
    node(600, 205, 210, 64, "John Clavering Boulevard", "the main street; 2 walls of light;\nwatchtower (mission 3)")
    node(600, 300, 100, 60, "Galvani's", "offices: a side\nquest twice", col="#fdf3e6")
    node(710, 300, 100, 60, "Art dealer", "faces Galvani's;\n2 ways in", col="#fdf3e6")
    node(600, 380, 210, 56, "Gaff Street", "alley off Bottle St's lower half:\nwalk round the 2nd wall of light")
    node(900, 200, 250, 92, "Holger Square: the Office", "4 ways in: high window,\nsewer + kennels, front door,\nthe back yard")
    node(900, 330, 250, 110, "The Golden Cat", "front door; rooftop via the hotel:\nleft (sign, gazebo, window) or right\n(balcony, hole, vent, then rat tunnel\nor ledge); VIP exit at the back")
    # roof band
    node(380, 110, 200, 52, "Rooftops and balconies", "Blink across, over the walls of light")
    node(860, 110, 170, 52, "Captain's Chair Hotel", "roof access (key from Slackjaw)")
    # under band
    node(230, 478, 260, 52, "Bottle Street, lower section", "runs underneath Clavering Boulevard")
    node(640, 478, 170, 52, "Sewers", "under the Office courtyard")
    node(900, 478, 250, 52, "Rat tunnel", "into the Golden Cat's washroom")
    # edges: main (street), alternative (roof), under
    edge([(190, 332), (230, 242)], C["street"])
    edge([(380, 242), (600, 237)], C["street"], label="through the walls of light", lp=(490, 230))
    edge([(810, 237), (900, 246)], C["street"])
    edge([(810, 237), (900, 360)], C["street"])
    edge([(305, 274), (305, 360)], C["back"])
    edge([(380, 268), (420, 320)], C["back"])
    edge([(305, 430), (305, 478)], C["under"])
    edge([(490, 504), (600, 504), (705, 436)], C["under"], label="Gaff Street bypass", lp=(560, 498))
    edge([(705, 436), (705, 360)], C["back"])
    edge([(305, 210), (420, 162)], C["roof"], label="climb", lp=(350, 180))
    edge([(580, 136), (860, 136)], C["roof"], label="roof to roof (Blink)", lp=(720, 130))
    edge([(1030, 136), (1100, 136), (1100, 330)], C["roof"])
    edge([(580, 150), (1000, 200)], C["roof"], dash="6 4", label="high window", lp=(800, 168))
    edge([(810, 504), (900, 504)], C["under"])
    edge([(725, 478), (960, 292)], C["under"], dash="6 4", label="sewer + kennels", lp=(860, 400))
    edge([(1025, 478), (1025, 440)], C["under"])
    # side-quest payouts
    o.append(f'<text x="40" y="585" font-size="15" font-weight="700" fill="{C["ink"]}">What to notice</text>')
    notes = [
        "1. Three stacked layers along one corridor: the watched boulevard, the gang street that runs UNDER it, and the roofs over it.",
        "2. Every goal has 3-4 ways in (the Office: 4; the Golden Cat: front, two rooftop lines, a rat tunnel, the VIP exit).",
        "3. Side content pays out in routes: Slackjaw's errand gives the hotel key (the rooftop way in); saving a woman gives a back-door key.",
        "4. Side content sits ON the alternative routes (Granny Rags on Endoria, Griff at Bloodox's dead end, Galvani on the boulevard).",
        "5. The same district is played twice, changed: a new watchtower, a gate shut, Granny's door locked, weepers below, assassins on the roofs.",
    ]
    for i, n in enumerate(notes):
        o.append(f'<text x="40" y="{612 + i * 24}" font-size="13" fill="{C["ink"]}">{esc(n)}</text>')
    o.append("</svg>")
    return "\n".join(o)


def main():
    out = {
        "plan-a-deep-block.svg": plan_a().svg(),
        "plan-b-hill.svg": plan_b().svg(),
        "plan-c-close.svg": plan_c().svg(),
        "plan-d-link.svg": plan_d().svg(),
        "distillery-schematic.svg": distillery(),
    }
    for name, svg in out.items():
        with open(os.path.join(HERE, name), "w", encoding="utf-8") as f:
            f.write(svg)
        print("wrote", name, len(svg))


if __name__ == "__main__":
    main()
