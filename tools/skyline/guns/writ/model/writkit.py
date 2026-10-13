"""Shared helpers for the Writ's and the robot arms' Blender builds (Task 18). Everything is modelled in Unity's frame
(x right, y up, z forward, metres) as plain vertex and face lists ("parts"), then turned into Blender objects in one step:
U() maps a Unity point into Blender's frame for the FBX exporter's -Z forward / Y up conversion (Unity (x, y, z) =
Blender (-x, z, -y)), the same convention as the Breacher (tools/skyline/guns/breacher/model/build_breacher.py).

A part is (verts, faces, material index, uv mode, smooth). UV modes: ("box", tile) projects each face along its main axis
in units of `tile` metres (so a tiling texture keeps one texel size on every face); ("cyl", axis_origin, axis_dir, a0, a1,
y0, y1) wraps a texture once around an axis; None leaves the UVs at 0.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

# ---------------------------------------------------------------- frames


def U(x, y, z):
    """A Unity point into Blender's frame."""
    return (-x, -z, y)


C_U2B = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))          # Blender = C * Unity


def rx(p, deg):
    """Unity's rotation about x (positive pitches forward down): y' = y cos - z sin, z' = y sin + z cos."""
    a = math.radians(deg)
    x, y, z = p
    return (x, y * math.cos(a) - z * math.sin(a), y * math.sin(a) + z * math.cos(a))


def ry(p, deg):
    """Unity's rotation about y (positive turns forward to the right)."""
    a = math.radians(deg)
    x, y, z = p
    return (x * math.cos(a) + z * math.sin(a), y, -x * math.sin(a) + z * math.cos(a))


def rz(p, deg):
    """Unity's rotation about z (positive turns right toward up)."""
    a = math.radians(deg)
    x, y, z = p
    return (x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a), z)


def euler(p, e):
    """Unity's Quaternion.Euler(e) applied to p: z first, then x, then y."""
    return ry(rx(rz(p, e[2]), e[0]), e[1])


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def scale(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def norm(a):
    m = math.sqrt(a[0] ** 2 + a[1] ** 2 + a[2] ** 2) or 1.0
    return (a[0] / m, a[1] / m, a[2] / m)


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


class Part:
    def __init__(self, verts, faces, mat, uv=("box", 0.04), smooth=False):
        self.verts = [tuple(v) for v in verts]
        self.faces = [tuple(f) for f in faces]
        self.mat = mat
        self.uv = uv
        self.smooth = smooth

    def moved(self, fn):
        """A copy with every vertex mapped through fn (a rigid placement)."""
        p = Part([fn(v) for v in self.verts], self.faces, self.mat, self.uv, self.smooth)
        if p.uv and p.uv[0] == "cyl":
            o, d = p.uv[1], p.uv[2]
            p.uv = ("cyl", fn(o), norm(sub(fn(add(o, d)), fn(o))), *p.uv[3:])
        return p

    def rotated(self, e, pivot=(0, 0, 0)):
        return self.moved(lambda v: add(euler(sub(v, pivot), e), pivot))

    def at(self, offset):
        return self.moved(lambda v: add(v, offset))


def place(parts, e=(0, 0, 0), pivot=(0, 0, 0), offset=(0, 0, 0)):
    return [p.rotated(e, pivot).at(offset) for p in parts]


# ---------------------------------------------------------------- primitives (Unity frame)


def box(c, s, mat, e=(0, 0, 0), uv=("box", 0.04)):
    hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
    v = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz), (-hx, -hy, hz), (-hx, hy, -hz), (hx, hy, -hz), (hx, hy, hz), (-hx, hy, hz)]
    f = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return Part([add(euler(p, e), c) for p in v], f, mat, uv)


def frustum_box(c, bottom, top, height, mat, e=(0, 0, 0), uv=("box", 0.04)):
    """A box whose bottom is bottom=(sx, sz) and top top=(sx, sz) (a tapered block or a keystone), centred at c."""
    hb, ht, hh = (bottom[0] / 2, bottom[1] / 2), (top[0] / 2, top[1] / 2), height / 2
    v = [(-hb[0], -hh, -hb[1]), (hb[0], -hh, -hb[1]), (hb[0], -hh, hb[1]), (-hb[0], -hh, hb[1]),
         (-ht[0], hh, -ht[1]), (ht[0], hh, -ht[1]), (ht[0], hh, ht[1]), (-ht[0], hh, ht[1])]
    f = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return Part([add(euler(p, e), c) for p in v], f, mat, uv)


def cylinder(c, r, length, mat, axis="y", n=16, r_end=None, star=None, caps=True, uv=("box", 0.04), smooth=True):
    """A cylinder (or a frustum with r_end) centred at c along Unity's x, y or z axis. star=k makes 2*... flutes: every
    other ring vertex is pulled in to k * r (a fluted column)."""
    re_ = r if r_end is None else r_end
    ring0, ring1 = [], []
    for i in range(n):
        t = 2 * math.pi * i / n
        k = (star if (star and i % 2) else 1.0)
        u, w = math.cos(t) * k, math.sin(t) * k
        a0, a1 = (u * r, w * r), (u * re_, w * re_)
        if axis == "y":
            ring0.append((c[0] + a0[0], c[1] - length / 2, c[2] + a0[1]))
            ring1.append((c[0] + a1[0], c[1] + length / 2, c[2] + a1[1]))
        elif axis == "z":
            ring0.append((c[0] + a0[0], c[1] + a0[1], c[2] - length / 2))
            ring1.append((c[0] + a1[0], c[1] + a1[1], c[2] + length / 2))
        else:
            ring0.append((c[0] - length / 2, c[1] + a0[0], c[2] + a0[1]))
            ring1.append((c[0] + length / 2, c[1] + a1[0], c[2] + a1[1]))
    vs = ring0 + ring1
    faces = []
    if caps:
        faces += [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return Part(vs, faces, mat, uv, smooth)


def sphere(c, r, mat, rings=6, segs=10, uv=None):
    vs = [(c[0], c[1] - r, c[2])]
    for i in range(1, rings):
        phi = math.pi * i / rings - math.pi / 2
        for j in range(segs):
            th = 2 * math.pi * j / segs
            vs.append((c[0] + r * math.cos(phi) * math.cos(th), c[1] + r * math.sin(phi), c[2] + r * math.cos(phi) * math.sin(th)))
    vs.append((c[0], c[1] + r, c[2]))
    faces = []
    for j in range(segs):
        faces.append((0, 1 + (j + 1) % segs, 1 + j))
    for i in range(rings - 2):
        a, b = 1 + i * segs, 1 + (i + 1) * segs
        for j in range(segs):
            k = (j + 1) % segs
            faces.append((a + j, a + k, b + k, b + j))
    top = len(vs) - 1
    last = 1 + (rings - 2) * segs
    for j in range(segs):
        faces.append((last + j, last + (j + 1) % segs, top))
    return Part(vs, faces, mat, uv, smooth=True)


def _fill(loops):
    """Triangulates the region bounded by 2D loops (the first is the outline, the rest holes) with Blender's scanfill;
    returns (points, triangles)."""
    bm = bmesh.new()
    edges = []
    pts = []
    for loop in loops:
        vs = [bm.verts.new((p[0], p[1], 0.0)) for p in loop]
        pts += [tuple(p) for p in loop]
        for i in range(len(vs)):
            edges.append(bm.edges.new((vs[i], vs[(i + 1) % len(vs)])))
    bm.verts.index_update()
    res = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=(0, 0, 1))
    tris = [tuple(v.index for v in f.verts) for f in res["geom"] if isinstance(f, bmesh.types.BMFace)]
    bm.free()
    return pts, tris


def slab(loops, z0, z1, mat, plane="xy", uv=("box", 0.04)):
    """A flat shape (outline + holes, 2D loops) extruded between z0 and z1. plane "xy": the loops are (x, y) and the
    depth runs along z; plane "zy": the loops are (z, y) and the depth runs along x; plane "xz": (x, z), depth along y."""
    pts, tris = _fill(loops)
    n = len(pts)

    def P(p, d):
        if plane == "xy":
            return (p[0], p[1], d)
        if plane == "zy":
            return (d, p[1], p[0])
        return (p[0], d, p[1])

    vs = [P(p, z0) for p in pts] + [P(p, z1) for p in pts]
    faces = [tuple(reversed(t)) for t in tris] + [tuple(i + n for i in t) for t in tris]
    start = 0
    for loop in loops:
        m = len(loop)
        for i in range(m):
            a, b = start + i, start + (i + 1) % m
            faces.append((a, b, b + n, a + n))
        start += m
    return Part(vs, faces, mat, uv)


def circle(cx, cy, r, n=24, a0=0.0):
    return [(cx + r * math.cos(a0 + 2 * math.pi * i / n), cy + r * math.sin(a0 + 2 * math.pi * i / n)) for i in range(n)]


def pointed_arch(a, ys, c_ratio=0.5, n=9):
    """The curve of a pointed (two-centred) arch of half-span a springing at ys, from (-a, ys) over the apex to (a, ys)."""
    c = c_ratio * a
    R = a + c
    h = math.sqrt(R * R - c * c)
    left = []
    a_start = math.pi            # the left arc is centred at (+c, ys); from angle pi (the springing) to the apex
    a_apex = math.atan2(h, -c)
    for i in range(n + 1):
        t = a_start + (a_apex - a_start) * i / n
        left.append((c + R * math.cos(t), ys + R * math.sin(t)))
    right = [(-x, y) for (x, y) in reversed(left[:-1])]
    return left + right, ys + h


# ---------------------------------------------------------------- materials


TINT_IN_SHADER = False   # the FBX exporter only writes a texture linked straight into Base Color; the preview sets True to
                         # draw image x tint (what SkylineWritFx does in Unity)


def material(name, rgb, rough=0.5, metal=0.0, emit=None, strength=0.0, image=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        key = "Emission Color" if "Emission Color" in b.inputs else "Emission"
        b.inputs[key].default_value = (*emit, 1.0)
        b.inputs["Emission Strength"].default_value = strength
    if image is not None and Path(image).is_file():
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(image), check_existing=True)
        if not TINT_IN_SHADER:
            nt.links.new(tex.outputs["Color"], b.inputs["Base Color"])
            m.diffuse_color = (*rgb, 1.0)
            return m
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[6].default_value = (*rgb, 1.0)
        nt.links.new(tex.outputs["Color"], mix.inputs[7])
        nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    m.diffuse_color = (*rgb, 1.0)
    return m


# ---------------------------------------------------------------- objects


def to_object(name, parts, mats, origin=(0, 0, 0), bevel=0.0):
    """One Blender object from parts (Unity frame), with its origin at `origin` (Unity frame)."""
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    for p in parts:
        base = [bm.verts.new(U(*sub(v, origin))) for v in p.verts]
        for f in p.faces:
            try:
                face = bm.faces.new([base[i] for i in f])
            except ValueError:
                continue
            face.material_index = p.mat
            face.smooth = p.smooth
            uvs = _uvs(p, f)
            for loop, uv in zip(face.loops, uvs):
                loop[uvl].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    ob.location = U(*origin)
    bpy.context.collection.objects.link(ob)
    if bevel > 0:
        mod = ob.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
        mod.harden_normals = False
    return ob


def _uvs(p, f):
    vs = [p.verts[i] for i in f]
    if not p.uv:
        return [(0.0, 0.0)] * len(vs)
    if p.uv[0] == "box":
        tile = p.uv[1]
        n = (0.0, 0.0, 0.0)
        for i in range(len(vs)):
            n = add(n, cross(sub(vs[i], vs[0]), sub(vs[(i + 1) % len(vs)], vs[0])))
        ax = max(range(3), key=lambda k: abs(n[k]))
        out = []
        for v in vs:
            if ax == 0:
                out.append((v[2] / tile, v[1] / tile))
            elif ax == 1:
                out.append((v[0] / tile, v[2] / tile))
            else:
                out.append((v[0] / tile, v[1] / tile))
        return out
    _, o, d, a0, a1, y0, y1 = p.uv
    # an axis frame: d up, e1/e2 around it
    ref = (0.0, 0.0, 1.0) if abs(d[2]) < 0.9 else (1.0, 0.0, 0.0)
    e1 = norm(sub(ref, scale(d, dot(ref, d))))
    e2 = cross(d, e1)
    out = []
    for v in vs:
        q = sub(v, o)
        h = dot(q, d)
        ang = math.degrees(math.atan2(dot(q, e2), dot(q, e1)))
        out.append(((ang - a0) / (a1 - a0), (h - y0) / (y1 - y0)))
    return out


def empty(name, c):
    ob = bpy.data.objects.new(name, None)
    ob.location = U(*c)
    ob.empty_display_size = 0.01
    bpy.context.collection.objects.link(ob)
    return ob


def export_fbx(path: Path, tag: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=False, object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode="STRIP",
        embed_textures=False)
    print(tag, "wrote", path)
    total = 0
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            dg = bpy.context.evaluated_depsgraph_get()
            me = ob.evaluated_get(dg).to_mesh()
            print(tag, ob.name, "verts", len(me.vertices), "tris", sum(len(p.vertices) - 2 for p in me.polygons))
            total += len(me.vertices)
    print(tag, "total verts", total)


def repo_from_argv():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    repo = Path(argv[0]) if argv else Path(__file__).resolve().parents[5]
    return repo, argv[1:]
