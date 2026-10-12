"""Recolours a Tripo character's base-colour texture by class, to turn one person into another
(docs/redesign/people.md "Rafael and the representative"):

    uv run --with pillow --with numpy python tools/tripo/vh_alter.py <alter.json>

alter.json:
  {"npz": dump, "tex": Color.jpg, "out": new Color.jpg, "preview": front render .png,
   "rough_in": roughness map, "rough_out": new roughness map (optional),
   "classes": [{"name": "apron", "sample": [x, z, r] (metres: texels on front-facing surfaces near x/z) or
                "proto": [r, g, b] (the class colour given), "roughness": 0..1 (optional),
                "zmin": .., "zmax": .. (optional gate on the texel's 3D height), "target": [r, g, b] or null (keep),
                "flatten": 0..1 (how much of the original shading to drop; 0 keeps it all)}, ...]}
Each texel goes to the nearest class prototype (colour distance) among the classes its height allows; a recoloured
texel becomes target * (its luminance / the class's mean luminance) ^ (1 - flatten), so folds, knit and painted
shading survive. Texels outside every island (the padding) are classified by colour alone.
"""
import json
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, __file__.replace("\\", "/").rsplit("/", 1)[0])
import vh_tex as vt

LUM = np.array([0.299, 0.587, 0.114], dtype=np.float32)


def main(path):
    s = json.load(open(path, encoding="utf8"))
    m = vt.load(s["npz"])
    tex = vt.load_tex(s["tex"])            # full size (4096)
    N = tex.shape[0]
    R = 1024
    ok, pos, nrm, _ = vt.raster_uv(m, R)
    # Upsample the low-res maps to the texture (nearest).
    idx = (np.arange(N) * R // N)
    okN = ok[idx][:, idx]
    zN = pos[..., 2][idx][:, idx]
    yN = pos[..., 1][idx][:, idx]
    xN = pos[..., 0][idx][:, idx]

    # Prototypes: the median colour of front-facing texels near each sample point (x, z); a class may list several
    # ("samples") and is then the nearest of them.
    small = tex[(np.arange(R) * N // R)][:, (np.arange(R) * N // R)]
    protos, owner = [], []
    for k, c in enumerate(s["classes"]):
        if "proto" in c:
            for pr in (c["proto"] if isinstance(c["proto"][0], list) else [c["proto"]]):
                protos.append(np.array(pr, dtype=np.float32))
                owner.append(k)
            continue
        for x, z, r in (c["samples"] if "samples" in c else [c["sample"]]):
            sel = ok & (np.hypot(pos[..., 0] - x, pos[..., 2] - z) < r) & (nrm[..., 1] < -0.3 if c.get("front", True) else True)
            if "ymax" in c:
                sel &= pos[..., 1] < c["ymax"]
            if sel.sum() < 4:
                raise SystemExit("class %s: no texels near its sample %s" % (c["name"], (x, z)))
            cols = small[sel]
            lum = cols @ LUM
            cols = cols[(lum >= np.percentile(lum, 20)) & (lum <= np.percentile(lum, 80))]
            protos.append(np.median(cols, axis=0))
            owner.append(k)
        print("class", c["name"], "protos", [np.round(p, 2).tolist() for p, o in zip(protos, owner) if o == k])
    PP = np.array(protos, dtype=np.float32)
    owner = np.array(owner)
    P = np.array([PP[owner == k][0] for k in range(len(s["classes"]))], dtype=np.float32)
    chroma = s.get("metric") == "chroma"

    def feat(a):
        if not chroma:
            return a * np.sqrt(np.array([1.0, 1.4, 0.8], dtype=np.float32))
        l = a @ LUM
        dn = l + 0.08
        return np.stack([l * 0.8, (a[..., 0] - a[..., 1]) / dn, (a[..., 1] - a[..., 2]) / dn], -1)
    FP = feat(PP)

    # Classify, in row bands to keep the memory sane.
    out = tex.copy()
    cls = np.zeros((N, N), dtype=np.int8)
    band = 256
    K = len(s["classes"])
    for r0 in range(0, N, band):
        t = feat(tex[r0:r0 + band])
        dp = ((t[:, :, None, :] - FP[None, None]) ** 2).sum(-1)        # (b, N, protos)
        d = np.full(dp.shape[:2] + (K,), np.inf, dtype=np.float32)
        for k in range(K):
            d[..., k] = dp[..., owner == k].min(-1)
        zz = zN[r0:r0 + band]
        yy = yN[r0:r0 + band]
        inside = okN[r0:r0 + band]
        for k, c in enumerate(s["classes"]):
            gate = np.ones(zz.shape, dtype=bool)
            if "zmin" in c:
                gate &= zz >= c["zmin"]
            if "zmax" in c:
                gate &= zz <= c["zmax"]
            if "ymax" in c:
                gate &= yy <= c["ymax"]
            if "ymin" in c:
                gate &= yy >= c["ymin"]
            if "poly" in c:    # only inside a front-projected polygon [[x, z], ...] (a blouse's V, a lapel), front half
                gate &= in_poly(xN[r0:r0 + band], zz, c["poly"]) & (yy < c.get("poly_ymax", 0.0))
            if "zskip" in c:   # a band this class may not take (hair under a bandage)
                gate &= (zz < c["zskip"][0]) | (zz > c["zskip"][1])
            if "not_in" in c:  # ellipsoids this class may not take: [cx, cy, cz, rx, ry, rz] (the face, for hair)
                for e in c["not_in"]:
                    xx_ = xN[r0:r0 + band]
                    gate &= ((xx_ - e[0]) / e[3]) ** 2 + ((yy - e[1]) / e[4]) ** 2 + ((zz - e[2]) / e[5]) ** 2 > 1
            d[..., k] = np.where(gate | ~inside, d[..., k], np.inf)
            if c.get("islands_only"):
                d[..., k] = np.where(inside, d[..., k], np.inf)
        cls[r0:r0 + band] = np.argmin(d, axis=-1)

    if s.get("despeckle"):
        cls = despeckle(cls, len(s["classes"]), s["despeckle"])
    if s.get("pad_from_islands", True):
        # The padding round each UV island takes its island's class (bilinear and mip sampling reach into it).
        cls = cls.copy()
        have = okN.copy()
        for _ in range(12):
            for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                sc = np.roll(np.roll(cls, dy, 0), dx, 1)
                sh = np.roll(np.roll(have, dy, 0), dx, 1)
                take = ~have & sh
                cls[take] = sc[take]
                have |= take
    lum = tex @ LUM
    for k, c in enumerate(s["classes"]):
        if c.get("target") is None:
            continue
        sel = cls == k
        if not sel.any():
            continue
        mean = float(np.median(lum[sel]))
        ratio = np.clip(lum[sel] / max(1e-3, mean), 0, 3) ** (1.0 - c.get("flatten", 0.0))
        tgt = np.array(c["target"], dtype=np.float32)
        new = np.clip(tgt[None] * ratio[:, None], 0, 1)
        mix = c.get("mix", 1.0)
        out[sel] = tex[sel] * (1 - mix) + new * mix
        print("recoloured", c["name"], int(sel.sum()), "texels")
    delta = np.zeros_like(m["verts"])
    s["_tex_orig"] = tex
    s["_normal_W"] = None
    if s.get("back_paint"):
        delta += back_paint(s, m, out, P)
    if s.get("back_of_head"):
        delta += back_of_head(s, m, out, P)
    Image.fromarray(np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)).save(s["out"], quality=95)
    # Roughness: classes with "roughness" set get that value (e.g. a plastic apron); the rest keep the original map.
    if s.get("rough_in") and s.get("rough_out"):
        rough = np.asarray(Image.open(s["rough_in"]).convert("L"), dtype=np.float32) / 255.0
        if rough.shape[0] != N:
            rough = np.asarray(Image.open(s["rough_in"]).convert("L").resize((N, N)), dtype=np.float32) / 255.0
        for k, c in enumerate(s["classes"]):
            if "roughness" in c:
                rough[cls == k] = c["roughness"]
        # Regions repainted from 3D (the back of the head's hair, the garment backs) get their own roughness: the
        # hair matte, the fabric plain (Tripo's roughness there still traced the front's print).
        for Wr, val in s.get("_rough_w", []):
            blend_up(rough.reshape(N, N, 1), Wr, np.full(Wr.shape + (1,), val, dtype=np.float32))
        Image.fromarray(np.clip(rough * 255 + 0.5, 0, 255).astype(np.uint8)).save(s["rough_out"], quality=95)

    if s.get("vertex_moves"):
        delta += vertex_moves(s, m, tex, P)
    if s.get("vertex_moves_out"):
        np.savez_compressed(s["vertex_moves_out"], delta=delta.astype(np.float32))

    if s.get("preview"):
        lo, hi = vt.bounds(m)
        small_out = out[(np.arange(2048) * N // 2048)][:, (np.arange(2048) * N // 2048)]
        img, _, _ = vt.render_ortho(m, small_out, (lo[0] - 0.02, lo[2] - 0.02, hi[0] + 0.02, hi[2] + 0.02), 1024)
        bg = np.full_like(img[..., :3], 0.35)
        vt.save(img[..., :3] * img[..., 3:] + bg * (1 - img[..., 3:]), s["preview"])


def in_poly(x, z, poly):
    """Point-in-polygon (even-odd) for arrays of x, z against [[x, z], ...]."""
    inside = np.zeros(x.shape, dtype=bool)
    n = len(poly)
    for i in range(n):
        x1, z1 = poly[i]
        x2, z2 = poly[(i + 1) % n]
        cross = ((z1 > z) != (z2 > z))
        xint = x1 + (z - z1) * (x2 - x1) / ((z2 - z1) if z2 != z1 else 1e-9)
        inside ^= cross & (x < xint)
    return inside


def vertex_moves(s, m, tex, P):
    """Pulls classes of vertices in along their normals (trim a beard, flatten hair). Each vertex's colour is the mean
    of the texture at its corners; it belongs to a move's class by nearest prototype (as the texels do). The weights
    are smoothed over the mesh so the surface eases in. Writes s["vertex_moves_out"] (npz, delta: V x 3, metres)."""
    V = m["verts"]
    T = m["tris"]
    uv = m["tri_uv"].reshape(-1, 2)
    col = vt.sample(tex, uv)
    acc = np.zeros((len(V), 3), dtype=np.float64)
    cnt = np.zeros(len(V), dtype=np.float64)
    np.add.at(acc, T.ravel(), col)
    np.add.at(cnt, T.ravel(), 1)
    vcol = (acc / np.maximum(1, cnt)[:, None]).astype(np.float32)
    d = ((vcol[:, None, :] - P[None]) ** 2 * np.array([1.0, 1.4, 0.8])).sum(-1)   # (vertex moves: first protos)
    names = [c["name"] for c in s["classes"]]
    for k, c in enumerate(s["classes"]):
        gate = np.ones(len(V), dtype=bool)
        if "zmin" in c:
            gate &= V[:, 2] >= c["zmin"]
        if "zmax" in c:
            gate &= V[:, 2] <= c["zmax"]
        d[~gate, k] = np.inf
    vcls = np.argmin(d, axis=1)
    # Neighbours (edges of the triangles) for smoothing.
    e = np.concatenate([T[:, [0, 1]], T[:, [1, 2]], T[:, [2, 0]]])
    e = np.concatenate([e, e[:, ::-1]])
    deg = np.bincount(e[:, 0], minlength=len(V)).astype(np.float32)
    delta = np.zeros_like(V)
    for mv in s["vertex_moves"]:
        wanted = mv["class"] if isinstance(mv["class"], list) else [mv["class"]]
        w = np.isin(vcls, [names.index(n) for n in wanted]).astype(np.float32)
        if "zmin" in mv:
            w *= V[:, 2] >= mv["zmin"]
        if "zmax" in mv:
            w *= V[:, 2] <= mv["zmax"]
        if "ymax" in mv:
            w *= V[:, 1] <= mv["ymax"]
        for _ in range(mv.get("smooth", 6)):
            nb = np.zeros_like(w)
            np.add.at(nb, e[:, 0], w[e[:, 1]])
            w = 0.5 * w + 0.5 * nb / np.maximum(1, deg)
        delta -= m["normals"] * (w * mv["inward"])[:, None]
        print("vertex move", mv["class"], "verts >0.5:", int((w > 0.5).sum()), "max", round(float(w.max()), 3))
    return delta


def class_colour(s, P, name):
    names = [c["name"] for c in s["classes"]]
    c = s["classes"][names.index(name)]
    return np.array(c["target"] if c.get("target") is not None else P[names.index(name)], dtype=np.float32)


def taubin(V, T, w, iters, lam=0.5, mu=-0.53):
    """Smooths vertices by weight w (0..1) without shrinking (Taubin): returns the moved positions."""
    e = np.concatenate([T[:, [0, 1]], T[:, [1, 2]], T[:, [2, 0]]])
    e = np.unique(np.sort(e, axis=1), axis=0)
    e = np.concatenate([e, e[:, ::-1]])
    deg = np.bincount(e[:, 0], minlength=len(V)).astype(np.float32)[:, None]
    X = V.astype(np.float64).copy()
    for _ in range(iters):
        for f in (lam, mu):
            nb = np.zeros_like(X)
            np.add.at(nb, e[:, 0], X[e[:, 1]])
            L = nb / np.maximum(1, deg) - X
            X += f * w[:, None] * L
    return X.astype(np.float32)


def despeckle(cls, K, radius):
    """Majority filter on the class map (a (2r+1)^2 box): isolated texels take their neighbourhood's class."""
    best = np.zeros(cls.shape, dtype=np.uint8)
    arg = cls.copy()
    for k in range(K):
        m = (cls == k).astype(np.uint8)
        if not m.any():
            continue
        acc = np.zeros(cls.shape, dtype=np.uint8)
        for dy in range(-radius, radius + 1):
            row = np.roll(m, dy, 0)
            for dx in range(-radius, radius + 1):
                acc += np.roll(row, dx, 1)
        take = acc > best
        best[take] = acc[take]
        arg[take] = k
    print("despeckled: changed", int((arg != cls).sum()), "texels")
    return arg


def blend_up(out, w, col):
    """out (N x N x 3) = lerp(out, col, w) with w and col at a lower resolution (nearest upsampling), in row bands."""
    N, R = out.shape[0], w.shape[0]
    f = N // R
    for r0 in range(0, R, 128):
        ws = np.repeat(np.repeat(w[r0:r0 + 128], f, 0), f, 1)[..., None]
        cs = np.repeat(np.repeat(np.asarray(col[r0:r0 + 128]), f, 0), f, 1)
        o = out[r0 * f:(r0 + 128) * f]
        o[:] = o * (1 - ws) + cs * ws


def grow(w, col, have, n=4):
    """Grows a repaint (weights and colours) a few texels into uncovered texels (the gaps between UV islands)."""
    for _ in range(n):
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sw = np.roll(np.roll(w, dy, 0), dx, 1)
            sc = np.roll(np.roll(col, dy, 0), dx, 1)
            sh = np.roll(np.roll(have, dy, 0), dx, 1)
            take = ~have & sh
            w[take] = sw[take]
            col[take] = sc[take]
            have |= take
    return w, col


def back_paint(s, m, out, P):
    """Tripo painted the front onto the back too (a bib, pockets, a cardigan's opening on the back). Each rule repaints
    the back of a body region as a plain garment back: the class's colour with soft shading from the surface normal,
    a gentle fold or knit pattern; the normal map goes flat there.
    rules: [{"zmin", "zmax", "xmax", "ymin" (behind this depth), "class", "pattern": "plain"|"knit", "strength"}]"""
    N = out.shape[0]
    R = 2048
    ok, pos, nrm, _ = vt.raster_uv(m, R)
    Wall = np.zeros((R, R), dtype=np.float32)
    COL = np.zeros((R, R, 3), dtype=np.float32)
    for r in s["back_paint"]:
        y, z, x = pos[..., 1], pos[..., 2], pos[..., 0]
        w = np.clip((nrm[..., 1] - 0.05) / 0.35, 0, 1)
        w *= np.clip((y - r["ymin"]) / 0.02, 0, 1)
        w *= np.clip((z - r["zmin"]) / 0.015, 0, 1) * np.clip((r["zmax"] - z) / 0.015, 0, 1)
        w *= np.clip((r["xmax"] - np.abs(x)) / 0.02, 0, 1)
        w = (w * ok).astype(np.float32)
        base = class_colour(s, P, r["class"])
        shade = 0.88 + 0.10 * np.clip(nrm[..., 2] * 0.5 + 0.5, 0, 1) + 0.05 * np.clip(-nrm[..., 0] * np.sign(x + 1e-6), -1, 1)
        if r.get("pattern") == "knit":
            pat = 0.5 + 0.5 * np.sin(x * 900) * np.sin(z * 700 + np.sin(x * 300) * 2)
        else:
            pat = 0.5 + 0.5 * np.sin(x * 60 + np.sin(z * 40) * 2.5) * np.sin(z * 25)
        col = base[None, None] * (shade * (1 + (pat - 0.5) * r.get("strength", 0.12)))[..., None]
        sel = w > Wall
        COL[sel] = col[sel]
        Wall = np.maximum(Wall, w)
    Wall, COL = grow(Wall, COL, ok.copy())
    blend_up(out, Wall, COL)
    s["_normal_W"] = Wall
    s.setdefault("_rough_w", []).append((Wall, s.get("back_roughness", 0.82)))
    print("back paint: repainted", int((Wall > 0.5).sum()), "texels at 2048")
    # The front's relief on the back (a bib's edge, a pocket, a placket) is in the mesh too: smooth it away there.
    V = m["verts"]
    Nv = m["normals"]
    wv = np.zeros(len(V), dtype=np.float32)
    iters = 0
    for r in s["back_paint"]:
        if not r.get("smooth"):
            continue
        iters = max(iters, r["smooth"])
        w = np.clip((Nv[:, 1] - 0.05) / 0.35, 0, 1) * np.clip((V[:, 1] - r["ymin"]) / 0.02, 0, 1)
        w *= np.clip((V[:, 2] - r["zmin"]) / 0.015, 0, 1) * np.clip((r["zmax"] - V[:, 2]) / 0.015, 0, 1)
        w *= np.clip((r["xmax"] - np.abs(V[:, 0])) / 0.02, 0, 1)
        wv = np.maximum(wv, w.astype(np.float32))
    if iters == 0:
        return np.zeros_like(V)
    X = taubin(V, m["tris"], wv, iters)
    print("back paint: smoothed", int((wv > 0.5).sum()), "verts, max move", round(float(np.abs(X - V).max()) * 1000, 2), "mm")
    return X - V


def back_of_head(s, m, out, P):
    """Tripo's heads come out two-faced (a second, mirrored face on the back of the skull, in the mesh and the paint).
    This rounds the back of the skull (Taubin smoothing, weighted) and repaints it: hair with a painted strand or curl
    pattern above the nape's hairline, skin below it, bands (a bandage) across; the normal map goes flat there.
    b = {"center": [x, y, z], "y0": start behind the ears, "y1": full, "zmin": the neck's cut, "radius": m,
         "hairline": z, "hair": class, "skin": class, "pattern": "strands"|"curls", "bands": [{zmin, zmax, color}],
         "smooth": iterations, "normal_in": .., "normal_out": ..}"""
    b = s["back_of_head"]
    c = np.array(b["center"], dtype=np.float32)
    N = out.shape[0]
    R = 2048
    ok, pos, nrm, _ = vt.raster_uv(m, R)

    def region(p):
        y = p[..., 1]
        w = np.clip((y - b["y0"]) / (b["y1"] - b["y0"]), 0, 1)
        w = w * w * (3 - 2 * w)
        w *= np.clip((p[..., 2] - b["zmin"]) / b.get("zramp", 0.02), 0, 1)
        w *= np.linalg.norm(p - c, axis=-1) < b["radius"]
        return w

    w = region(pos) * ok
    # Colours, from 3D: hair above the hairline (with a soft, wavy edge), skin below, bands over.
    hair = class_colour(s, P, b["hair"])
    skin = class_colour(s, P, b["skin"])
    d = pos - c
    theta = np.arctan2(d[..., 0], d[..., 1])
    z = pos[..., 2]
    if b.get("pattern", "strands") == "curls":
        # Tight curls as painted on the front: round tufts (cellular noise in 3D: bright crowns, dark gaps between).
        cell = b.get("curl_size", 0.0045)
        q = np.zeros(z.shape + (3,), dtype=np.float32)
        sel = region(pos) > 0
        P3 = pos[sel] / cell
        base = np.floor(P3)
        best = np.full(len(P3), 9.0, dtype=np.float32)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    cc = base + np.array([dx, dy, dz], dtype=np.float32)
                    h = np.sin(cc @ np.array([127.1, 311.7, 74.7])) * 43758.5453
                    jit = np.stack([np.modf(np.abs(h))[0], np.modf(np.abs(h * 1.37))[0], np.modf(np.abs(h * 1.91))[0]], -1)
                    d = np.linalg.norm(P3 - (cc + jit), axis=1)
                    best = np.minimum(best, d)
        pat = np.zeros(z.shape, dtype=np.float32)
        pat[sel] = np.clip(1.25 - best * 1.1, 0, 1) ** 0.8
        del q
    else:
        pat = 0.5 + 0.5 * np.sin(theta * 70 + 9 * np.sin(z * 55) + 3 * np.sin(theta * 13))
        pat = 0.6 * pat + 0.4 * (0.5 + 0.5 * np.sin(theta * 160 + 5 * np.sin(z * 120)))
    shade = 0.8 + 0.18 * np.clip(nrm[..., 2], -1, 1)
    hair_col = hair[None, None] * (shade * (0.86 + 0.28 * (pat - 0.5) * b.get("pattern_strength", 1.0)))[..., None]
    edge = b["hairline"] + 0.006 * np.sin(theta * 9)
    t = np.clip((z - edge) / b.get("hairline_soft", 0.008) + 0.5, 0, 1)
    skin_col = skin[None, None] * (0.85 + 0.15 * np.clip(nrm[..., 2] * 0.5 + 0.5, 0, 1))[..., None]
    col = hair_col * t[..., None] + skin_col * (1 - t[..., None])
    for band in b.get("bands", []):
        bz = (z > band["zmin"] + 0.004 * np.sin(theta * 3)) & (z < band["zmax"] + 0.004 * np.sin(theta * 3 + 1))
        bc = (class_colour(s, P, band["class"]) if "class" in band else np.array(band["color"], dtype=np.float32))[None, None] * (0.86 + 0.14 * np.clip(nrm[..., 2], -1, 1))[..., None]
        col[bz] = bc[bz]
    # Grow the repaint a few texels into the gaps between islands (else the old paint shows as seams).
    w = w.astype(np.float32)
    have = ok.copy()
    for _ in range(4):
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sw = np.roll(np.roll(w, dy, 0), dx, 1)
            sc = np.roll(np.roll(col, dy, 0), dx, 1)
            sh = np.roll(np.roll(have, dy, 0), dx, 1)
            take = ~have & sh
            w[take] = sw[take]
            col[take] = sc[take]
            have |= take
    # Up to the texture's size (each 2048 texel covers 2x2).
    blend_up(out, w, col)
    # Matte hair: the repainted region's roughness (hair above the hairline, skin below keeps a softer value).
    s.setdefault("_rough_w", []).append((w * t.astype(np.float32), b.get("roughness", 0.86)))
    if b.get("normal_in") and b.get("normal_out"):
        Wn = w if s.get("_normal_W") is None else np.maximum(w, s["_normal_W"])
        nm = np.asarray(Image.open(b["normal_in"]).convert("RGB"), dtype=np.float32) / 255.0
        if nm.shape[0] != N:
            nm = np.asarray(Image.open(b["normal_in"]).convert("RGB").resize((N, N)), dtype=np.float32) / 255.0
        flat = np.zeros(Wn.shape + (3,), dtype=np.float32)
        flat[:] = [0.5, 0.5, 1.0]
        # Strands in the normal map where the hair is: grooves along the hair's fall (a height field from the same
        # pattern, differentiated in UV space: tangent x = +u, y = +v).
        hs = b.get("strand_normal", 0.0)
        if hs > 0:
            hgt = pat.astype(np.float32)
            gy, gx = np.gradient(hgt)
            k = hs * 6.0
            nrm_t = np.stack([-gx * k, gy * k, np.ones_like(hgt)], -1)
            nrm_t /= np.linalg.norm(nrm_t, axis=-1, keepdims=True)
            hairw = (t * ok).astype(np.float32)[..., None]
            flat = flat * (1 - hairw) + (nrm_t * 0.5 + 0.5) * hairw
        blend_up(nm, Wn, flat)
        Image.fromarray(np.clip(nm * 255 + 0.5, 0, 255).astype(np.uint8)).save(b["normal_out"])
    print("back of head: repainted", int((w > 0.5).sum()), "texels at 2048")
    # The skull: the back's vertices go onto an ellipsoid fitted to the back of the head (the second face's nose, brow
    # and chin go with it), then a light smoothing hides the seam.
    V = m["verts"]
    wv = region(V)
    rel = V - c
    fit = (wv > 0.5) & (rel[:, 2] > -0.03)
    A = rel[fit] ** 2
    inv_r2, *_ = np.linalg.lstsq(A, np.ones(len(A)), rcond=None)
    radii = 1.0 / np.sqrt(np.maximum(inv_r2, 1e-6))
    radii *= b.get("skull_scale", 1.0)
    u = rel / np.maximum(1e-6, np.linalg.norm(rel, axis=1, keepdims=True))
    re = 1.0 / np.sqrt(((u / radii) ** 2).sum(1))
    target = c + u * re[:, None]
    # Below the occiput the back curves in to the neck: in the horizontal plane, the radius from the neck's axis
    # blends from the neck's ellipse (at zmin) to the skull's slice (at z_occ).
    nk = b.get("neck")
    if nk:
        z_occ = b.get("z_occ", c[2] - 0.035)
        A = np.array([c[0], nk["center_y"]], dtype=np.float32)
        h = V[:, :2] - A
        hd = h / np.maximum(1e-6, np.linalg.norm(h, axis=1, keepdims=True))
        r_neck = 1.0 / np.sqrt((hd[:, 0] / nk["rx"]) ** 2 + (hd[:, 1] / nk["ry"]) ** 2)
        k = np.sqrt(max(1e-4, 1.0 - ((z_occ - c[2]) / radii[2]) ** 2))
        ex, ey = radii[0] * k, radii[1] * k
        # Ray from A along hd to the skull slice's ellipse (centre c.xy): solve |(A + t hd - c.xy) / e| = 1.
        o = A - c[:2]
        qa = (hd[:, 0] / ex) ** 2 + (hd[:, 1] / ey) ** 2
        qb = 2 * (o[0] * hd[:, 0] / ex ** 2 + o[1] * hd[:, 1] / ey ** 2)
        qc = (o[0] / ex) ** 2 + (o[1] / ey) ** 2 - 1
        r_skull = (-qb + np.sqrt(np.maximum(0, qb * qb - 4 * qa * qc))) / (2 * qa)
        t = np.clip((V[:, 2] - b["zmin"]) / (z_occ - b["zmin"]), 0, 1)
        t = t * t * (3 - 2 * t)
        r = r_neck + (r_skull - r_neck) * t
        low = V[:, 2] < z_occ
        target[low, :2] = A + hd[low] * r[low, None]
        target[low, 2] = V[low, 2]
    # Thin dark things of the second face (its glasses' frames) would lie on the new skull and z-fight: sink them.
    sk = b.get("sink_dark")
    if sk:
        T = m["tris"]
        colv = vt.sample(s["_tex_orig"], m["tri_uv"].reshape(-1, 2))
        acc = np.zeros((len(V), 3))
        cnt = np.zeros(len(V))
        np.add.at(acc, T.ravel(), colv[:, :3])
        np.add.at(cnt, T.ravel(), 1)
        lum = (acc / np.maximum(1, cnt)[:, None]) @ LUM
        sink = (lum < sk["lum"]) & (V[:, 2] > sk["zmin"]) & (V[:, 2] < sk["zmax"]) & (wv > 0.5)
        dirv = target - c
        dirv /= np.maximum(1e-6, np.linalg.norm(dirv, axis=1, keepdims=True))
        target[sink] -= dirv[sink] * sk["depth"]
        print("back of head: sank", int(sink.sum()), "dark verts")
    X = V + (target - V) * wv[:, None]
    X = taubin(X, m["tris"], np.clip(wv * 1.5, 0, 1) * (wv > 0.02), b.get("smooth", 12))
    # The hair mass: the rounded skull is wrapped in hair of real thickness, thicker at the crown, thinning toward the
    # nape, in clumps that run with the fall, ending at the hairline (just above the collar) in a soft lip.
    hm = b.get("hair_mass")
    if hm:
        d3 = X - c
        dirv = d3 / np.maximum(1e-6, np.linalg.norm(d3, axis=1, keepdims=True))
        zv = X[:, 2]
        th = np.interp(zv, [b["hairline"], c[2], c[2] + 0.06], [hm["nape"], hm["mid"], hm["crown"]])
        thv = np.arctan2(d3[:, 0], d3[:, 1])
        clump = 0.5 + 0.5 * np.sin(thv * hm.get("clumps", 11) + 2.5 * np.sin(zv * 60 + thv * 2))
        th = th + hm.get("clump", 0.002) * (clump - 0.5)
        edge = b["hairline"] + 0.006 * np.sin(thv * 9)
        hv = np.clip((zv - edge) / hm.get("lip", 0.006), 0, 1)
        hv = hv * hv * (3 - 2 * hv)
        X = X + dirv * (th * hv * np.clip(wv * 1.4, 0, 1))[:, None]
        print("back of head: hair mass up to", round(float((th * hv * wv).max()) * 1000, 1), "mm")
    print("back of head: skull radii", np.round(radii, 4), "moved", int((wv > 0.5).sum()), "verts")
    return X - V


if __name__ == "__main__":
    main(sys.argv[1])
