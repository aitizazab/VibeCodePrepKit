"""Can a shot reach each bot's weak point? (judge round 1: no Writ shot could reach the drone's lens, because the egg's box
collider swallowed it, so the spot lesson failed.) Reads each bot's FBX without Blender or Unity, rebuilds the box
colliders SkylineBotBodies.FromModel gives every mesh (the mesh's bounds), applies the same weak-point clearance rule
(SkylineBotBodies.ClearWeakPoint: a body or plate box that holds the weak point's centre has its face on the weak point's
side pulled back to the weak point's inner face), and casts rays at the weak point from the arcs a player shoots from.

    uv run --no-project python tools/skyline/bots/weakpoint_reach.py [--no-clear] [--require-front]

Directions are in the bot's own frame (it faces +Z): the judge's four (front; 20 degrees above; 30 degrees to the left;
20 degrees below) and a 20-ray front arc (yaw -30..30 by 15; pitch -20, 0, 20 and 40, since a player looks down on floor units). A ray "reaches" when the first box it
meets belongs to the weak point; it is aimed at the weak point's centre, or (the "any point" count) at any of five points
on its visible face. With --require-front it exits 1 unless every bot is reachable from the front, by any point, on at
least half the arc.
"""
from __future__ import annotations

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_inspect as fi  # noqa: E402

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "JamKit", "Templates",
                    "SkylineRepo", "Resources", "SkylineModels", "bots")
BOTS = ["mall-cop-drone", "floor-care-unit", "spotlight", "window-washer-gondola"]
# SkylineBotBodies.WeakPointClearance: how far behind the weak point's inner face a pulled-back face sits (m)
GAP = 0.0
MIN_THICK = 0.02


def boxes(path):
    _, nodes = fi.parse(path)
    objects = fi.find(nodes, "Objects")[0]["children"]
    conns = fi.find(nodes, "Connections")[0]["children"]
    models, geoms = {}, {}
    for m in fi.find(objects, "Model"):
        p = fi.p70(m)
        models[m["props"][0]] = {"name": m["props"][1].split("\x00")[0], "t": p.get("Lcl Translation", [0, 0, 0]),
                                 "r": p.get("Lcl Rotation", [0, 0, 0]), "s": p.get("Lcl Scaling", [1, 1, 1]),
                                 "parent": None, "geo": None}
    for g in fi.find(objects, "Geometry"):
        vs = None
        for c in g["children"]:
            if c["name"] == "Vertices":
                vs = c["props"][0]
        geoms[g["props"][0]] = vs
    for c in conns:
        if c["props"][0] != "OO":
            continue
        child, parent = c["props"][1], c["props"][2]
        if child in models and parent in models:
            models[child]["parent"] = parent
        elif child in geoms and parent in models:
            models[parent]["geo"] = geoms[child]
    out = []
    for mid, m in models.items():
        if not m["geo"]:
            continue
        # the node's frame in the rig, through its parents (FBX: T * R * S, Euler XYZ, R = Rz Ry Rx)
        rot, off = ident(), [0.0, 0.0, 0.0]
        chain = []
        k = mid
        while k is not None:
            mm = models[k]
            r = mat_mul(euler(mm["r"]), diag(mm["s"]))
            rot = mat_mul(r, rot)
            off = [sum(r[i][j] * off[j] for j in range(3)) + mm["t"][i] for i in range(3)]
            chain.append(mm["name"])
            k = mm["parent"]
        vs = m["geo"]
        xs, ys, zs = vs[0::3], vs[1::3], vs[2::3]
        lo, hi = [min(xs), min(ys), min(zs)], [max(xs), max(ys), max(zs)]
        # FBX (right-handed) to Unity: x mirrored (M R M for rotations, M v for points)
        lo, hi = [-hi[0], lo[1], lo[2]], [-lo[0], hi[1], hi[2]]
        M = diag([-1, 1, 1])
        rot = mat_mul(mat_mul(M, rot), M)
        off = [-off[0], off[1], off[2]]
        zone = "body"
        if "WeakPoint" in chain:
            zone = "wp"
        elif any(n.startswith("Plate_") for n in chain):
            zone = "plate"
        out.append({"name": m["name"], "zone": zone, "lo": lo, "hi": hi, "rot": rot, "off": off})
    return out


def ident():
    return [[1.0, 0, 0], [0, 1.0, 0], [0, 0, 1.0]]


def diag(v):
    return [[v[0], 0, 0], [0, v[1], 0], [0, 0, v[2]]]


def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def euler(deg):
    x, y, z = (math.radians(v) for v in deg)
    rx = [[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]]
    ry = [[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]]
    rz = [[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]]
    return mat_mul(rz, mat_mul(ry, rx))


def to_local(b, p, is_dir=False):
    """A rig-space point (or direction) in the box's own frame (rotation inverse = transpose; scale assumed 1)."""
    q = p if is_dir else [p[i] - b["off"][i] for i in range(3)]
    r = b["rot"]
    return [sum(r[j][i] * q[j] for j in range(3)) for i in range(3)]


def world_aabb(b):
    pts = []
    for x in (b["lo"][0], b["hi"][0]):
        for y in (b["lo"][1], b["hi"][1]):
            for z in (b["lo"][2], b["hi"][2]):
                pts.append([sum(b["rot"][i][j] * (x, y, z)[j] for j in range(3)) + b["off"][i] for i in range(3)])
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def union(bs):
    ws = [world_aabb(b) for b in bs]
    lo = [min(w[0][i] for w in ws) for i in range(3)]
    hi = [max(w[1][i] for w in ws) for i in range(3)]
    return lo, hi


def clear(bs):
    """SkylineBotBodies.ClearWeakPoint, mirrored: in each box's own frame."""
    wl, wh = union([b for b in bs if b["zone"] == "wp"])
    wcw = [(wl[i] + wh[i]) / 2 for i in range(3)]
    corners = [[x, y, z] for x in (wl[0], wh[0]) for y in (wl[1], wh[1]) for z in (wl[2], wh[2])]
    changed = []
    for b in bs:
        if b["zone"] == "wp":
            continue
        wc = to_local(b, wcw)
        if not all(b["lo"][i] <= wc[i] <= b["hi"][i] for i in range(3)):
            continue
        lc = [to_local(b, c) for c in corners]
        cl = [min(c[i] for c in lc) for i in range(3)]
        ch = [max(c[i] for c in lc) for i in range(3)]
        cc = [(b["lo"][i] + b["hi"][i]) / 2 for i in range(3)]
        ext = [max(1e-6, (b["hi"][i] - b["lo"][i]) / 2) for i in range(3)]
        a = max(range(3), key=lambda i: abs(wc[i] - cc[i]) / ext[i])
        if wc[a] >= cc[a]:
            b["hi"][a] = max(b["lo"][a] + MIN_THICK, cl[a] - GAP)
        else:
            b["lo"][a] = min(b["hi"][a] - MIN_THICK, ch[a] + GAP)
        changed.append(b["name"])
    return changed


def ray_box(o, d, lo, hi):
    tmin, tmax = -1e9, 1e9
    for i in range(3):
        if abs(d[i]) < 1e-12:
            if o[i] < lo[i] or o[i] > hi[i]:
                return None
            continue
        t1, t2 = (lo[i] - o[i]) / d[i], (hi[i] - o[i]) / d[i]
        if t1 > t2:
            t1, t2 = t2, t1
        tmin, tmax = max(tmin, t1), min(tmax, t2)
    if tmax < max(tmin, 0):
        return None
    return max(tmin, 0)


def first(bs, o, d):
    best, hit = None, None
    for b in bs:
        t = ray_box(to_local(b, o), to_local(b, d, True), b["lo"], b["hi"])
        if t is not None and (best is None or t < best):
            best, hit = t, b
    return hit


def direction(yaw, pitch):
    """The direction the shot travels, from a shooter at (yaw, pitch) in front of the bot (pitch > 0: from above)."""
    y, p = math.radians(yaw), math.radians(pitch)
    frm = [math.sin(y) * math.cos(p), math.sin(p), math.cos(y) * math.cos(p)]
    return [-v for v in frm]


def reaches(bs, d, any_point):
    wl, wh = union([b for b in bs if b["zone"] == "wp"])
    wc = [(wl[i] + wh[i]) / 2 for i in range(3)]
    ext = [(wh[i] - wl[i]) / 2 for i in range(3)]
    aims = [wc]
    if any_point:
        for sx, sy in ((0.6, 0), (-0.6, 0), (0, 0.6), (0, -0.6)):
            aims.append([wc[0] + sx * ext[0], wc[1] + sy * ext[1], wc[2]])
    for a in aims:
        o = [a[i] - d[i] * 5 for i in range(3)]
        h = first(bs, o, d)
        if h is not None and h["zone"] == "wp":
            return True, h["name"]
    h = first(bs, [wc[i] - d[i] * 5 for i in range(3)], d)
    return False, h["name"] if h else "-"


JUDGE = [("front", 0, 0), ("20 above", 0, 20), ("30 left", -30, 0), ("20 below", 0, -20)]
ARC = [(y, p) for p in (-20, 0, 20, 40) for y in (-30, -15, 0, 15, 30)]


def main(argv):
    do_clear = "--no-clear" not in argv
    ok = True
    for bot in BOTS:
        bs = boxes(os.path.join(ROOT, bot + ".fbx"))
        changed = clear(bs) if do_clear else []
        j_c = [reaches(bs, direction(y, p), False) for _, y, p in JUDGE]
        a_c = sum(reaches(bs, direction(y, p), False)[0] for y, p in ARC)
        a_a = sum(reaches(bs, direction(y, p), True)[0] for y, p in ARC)
        print(f"{bot}: judge's 4 at the centre {sum(r for r, _ in j_c)}/4 "
              f"({', '.join(n + ('' if r else ' blocked by ' + h) for (n, _, _), (r, h) in zip(JUDGE, j_c))}); "
              f"front arc {a_c}/{len(ARC)} at the centre, {a_a}/{len(ARC)} at any point"
              + (f"; cleared: {', '.join(changed)}" if changed else ""))
        if a_a * 2 < len(ARC):
            ok = False
    if "--require-front" in argv and not ok:
        print("FAIL: a weak point is reachable on less than half the front arc")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
