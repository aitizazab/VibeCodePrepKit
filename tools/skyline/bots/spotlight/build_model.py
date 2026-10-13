"""Builds the spotlight's model in Blender (background) and exports it for Unity as two FBX files.

    blender -b --factory-startup -P tools/skyline/bots/spotlight/build_model.py -- [--preview-dir <dir>]

(run it through the GPU wrapper / capped-run, as the machine's rules say). Writes, under
Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/bots/:

- spotlight.fbx: the turning head (SkylineBot pitches and yaws this whole model; its origin is the head's centre, the pivot).
  Children named by the Unity contract (SkylineBotBodies): Plate_housing-left, Plate_housing-right (the PlateDefs' ids, in
  order), WeakPoint (the big lens), Lamp (where the beam leaves the lens), Face (the visor's display, UV 0-1), Muzzle (the
  coil rifle's tip).
- spotlight_mount.fbx: the fixed stand (a plinth and a levitator plate the head hovers over). It never turns:
  SkylineBotBodies loads it as the Mount beside the head. Its bottom is 0.88 m under the head's centre, where
  the gallery's column top is.

Every number below is in Unity's frame (metres; +Y up, +Z the bot's front, +X its right): the helpers convert to Blender
(the front faces -Y) and every node keeps an identity rotation; the FBX is written by blender_kit.export, the
convention all four bots share. Colours are CONCIERGE white and
gold (SkylineBotLook's values, written as linear colour so Unity's importer gets them back).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import blender_kit as bk  # noqa: E402  (the shared bot toolchain: merge, export, preview dump)

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "bots"

# ------------------------------------------------------------------------------------------------ helpers (Unity frame)


def B(u):
    """A Unity-frame point (x right, y up, z front) in Blender's frame (the model's front faces -Y)."""
    return Vector((-u[0], -u[2], u[1]))


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


_mats = {}


def mat(name, srgb, rough=0.55, metal=0.0):
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*[srgb_to_linear(v) for v in srgb], 1.0)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*[srgb_to_linear(v) for v in srgb], 1.0)
    _mats[name] = m
    return m


WHITE = ("Concierge_White", (0.93, 0.92, 0.88), 0.45)
GOLD = ("Concierge_Gold", (0.86, 0.66, 0.22), 0.35)
DARK = ("Gunmetal", (0.16, 0.16, 0.19), 0.5)
SCREEN = ("Screen", (0.05, 0.06, 0.08), 0.3)
GLASS = ("LensGlass", (0.95, 0.93, 0.75), 0.08)
FACE = ("Face", (1.0, 1.0, 1.0), 0.6)
COPPER = ("Coil_Copper", (0.80, 0.42, 0.20), 0.35)


def M(spec):
    return mat(spec[0], spec[1], spec[2])


_world = {}   # object name -> its world position (Blender frame), so children can be placed in world terms
COLL = {}


def link(obj, coll, parent=None, world=None):
    COLL[coll].objects.link(obj)
    w = world if world is not None else Vector((0, 0, 0))
    _world[obj.name] = w
    if parent is not None:
        obj.parent = parent
        obj.location = w - _world[parent.name]
    else:
        obj.location = w
    return obj


def mesh_obj(name, bm, material, smooth_axis=None, smooth_all=False):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    for f in bm.faces:
        if smooth_all:
            f.smooth = True
        elif smooth_axis is not None:
            f.smooth = abs(f.normal.dot(smooth_axis)) < 0.9
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    return bpy.data.objects.new(name, me)


def _axis_rot(axis):
    """The rotation taking Blender +Z to the Unity axis `axis` ('x', 'y' or 'z', positive)."""
    if axis == "y":
        return Matrix.Identity(4)
    if axis == "x":            # Unity +X is Blender -X
        return Matrix.Rotation(math.radians(-90), 4, "Y")
    return Matrix.Rotation(math.radians(90), 4, "X")   # Unity +Z is Blender -Y


def box(name, size, pos, spec, coll, parent=None, bevel=0.012):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((size[0], size[2], size[1])), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=min(bevel, min(size) * 0.45), segments=2,
                        affect="EDGES", profile=0.5, clamp_overlap=True)
    return link(mesh_obj(name, bm, M(spec)), coll, parent, B(pos))


def cyl(name, r1, r2, length, axis, pos, spec, coll, parent=None, segs=32):
    """A cylinder (or cone: r1 at the axis's negative end, r2 at its positive end) along a Unity axis."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=length)
    rot = _axis_rot(axis)
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    ax = (rot @ Vector((0, 0, 1, 0))).to_3d()
    return link(mesh_obj(name, bm, M(spec), smooth_axis=ax), coll, parent, B(pos))


def sphere(name, radius, pos, spec, coll, parent=None, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=radius)
    bmesh.ops.scale(bm, vec=Vector((scale[0], scale[2], scale[1])), verts=bm.verts)
    return link(mesh_obj(name, bm, M(spec), smooth_all=True), coll, parent, B(pos))


def torus(name, major, minor, axis, pos, spec, coll, parent=None, segs=48, ring=12):
    bm = bmesh.new()
    grid = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        row = []
        for j in range(ring):
            b = 2 * math.pi * j / ring
            r = major + minor * math.cos(b)
            row.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        grid.append(row)
    for i in range(segs):
        for j in range(ring):
            a, b2 = grid[i][j], grid[(i + 1) % segs][j]
            c, d = grid[(i + 1) % segs][(j + 1) % ring], grid[i][(j + 1) % ring]
            bm.faces.new((a, b2, c, d))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.transform(bm, matrix=_axis_rot(axis), verts=bm.verts)
    return link(mesh_obj(name, bm, M(spec), smooth_all=True), coll, parent, B(pos))


def face_plane(name, w, h, pos, coll, parent=None):
    """The display: a quad facing the bot's front, UV 0-1 with u running left to right as seen from the front."""
    bm = bmesh.new()
    # Blender frame: the front is -Y; seen from the front, the viewer's left is Blender -X.
    vs = [bm.verts.new(p) for p in ((-w / 2, 0, -h / 2), (w / 2, 0, -h / 2), (w / 2, 0, h / 2), (-w / 2, 0, h / 2))]
    f = bm.faces.new(vs)
    uv = bm.loops.layers.uv.new("UVMap")
    for loop, (u, v) in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop[uv].uv = (u, v)
    bm.normal_update()
    assert f.normal.y < -0.99, "the face must look toward the front (-Y in Blender)"
    return link(mesh_obj(name, bm, M(FACE)), coll, parent, B(pos))


def empty(name, pos, coll, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.08
    return link(e, coll, parent, B(pos))


def rod(name, a, b, radius, spec, coll, parent=None, segs=12):
    """A straight rod between two Unity-frame points. The rotation stays on the object (not baked into the mesh), so
    Unity's box collider for it hugs the rod instead of boxing its whole diagonal."""
    pa, pb = B(a), B(b)
    d = pb - pa
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius, radius2=radius, depth=d.length)
    o = mesh_obj(name, bm, M(spec), smooth_axis=Vector((0, 0, 1)))
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return link(o, coll, parent, (pa + pb) / 2)


def new_collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    COLL[name] = c
    return c


def fold(target, others):
    """Joins the named meshes into `target` (blender_kit.merge, the shared toolchain), so the bot spawns with a few
    renderers and colliders instead of dozens (review Minor 10). Only compact groups are folded: a fold's box collider
    must not swallow a plate or the weak point."""
    objs = [bpy.data.objects[target]] + [bpy.data.objects[n] for n in others]
    bk.merge(objs, target)


def fresh():
    bk.reset()
    _mats.clear()
    _world.clear()
    COLL.clear()


# ------------------------------------------------------------------------------------------------ the spotlight

def build_head(c="head"):
    new_collection(c)
    # the drum: a lamp housing widening to the front, the lens in a gold bezel
    cyl("Drum", 0.31, 0.37, 0.74, "z", (0, 0, -0.03), WHITE, c, segs=40)
    torus("Bezel", 0.36, 0.045, "z", (0, 0, 0.35), GOLD, c)
    lens = sphere("WeakPoint", 0.33, (0, 0, 0.37), GLASS, c, scale=(1, 1, 0.22))
    cyl("Reflector", 0.07, 0.11, 0.06, "z", (0, 0, 0.40), GOLD, c, parent=lens, segs=24)   # the arc lamp behind the glass
    empty("Lamp", (0, 0, 0.46), c)
    # the rear: cooling fins and a cap
    for i in range(4):
        cyl(f"Fin{i}", 0.29, 0.29, 0.018, "z", (0, 0, -0.42 - i * 0.042), DARK, c, segs=40)
    cyl("RearCap", 0.2, 0.11, 0.07, "z", (0, 0, -0.6), WHITE, c, segs=32)
    cyl("FinCore", 0.15, 0.15, 0.2, "z", (0, 0, -0.5), DARK, c, segs=24)
    torus("DrumBand", 0.335, 0.022, "z", (0, 0, -0.36), GOLD, c, segs=40, ring=8)
    # the plates: gold side housings, vented, with a pivot hub each
    for s, pid in ((-1, "housing-left"), (1, "housing-right")):
        p = box(f"Plate_{pid}", (0.08, 0.56, 0.62), (s * 0.40, 0, -0.04), GOLD, c, bevel=0.025)
        for k in range(4):
            box(f"Vent_{pid}_{k}", (0.012, 0.035, 0.38), (s * 0.445, -0.12 + k * 0.08, -0.06), DARK, c, parent=p, bevel=0.004)
        cyl(f"Hub_{pid}", 0.07, 0.07, 0.03, "x", (s * 0.452, 0.19, 0.18), WHITE, c, parent=p, segs=20)
    # the visor on top: the face's display under a gold brim
    box("Neck", (0.16, 0.14, 0.14), (0, 0.36, 0.0), DARK, c, bevel=0.01)
    box("Visor", (0.52, 0.32, 0.16), (0, 0.5, 0.04), WHITE, c, bevel=0.03)
    box("VisorScreen", (0.44, 0.26, 0.02), (0, 0.5, 0.12), SCREEN, c, bevel=0.004)
    face_plane("Face", 0.40, 0.235, (0, 0.5, 0.132), c)
    box("VisorBrim", (0.56, 0.03, 0.1), (0, 0.675, 0.09), GOLD, c, bevel=0.008)
    # the coil rifle, slung low on the right (off the centre line, so it never swings into the stand)
    rx, ry = 0.42, -0.38
    box("RifleBracket", (0.2, 0.07, 0.1), (0.27, -0.33, 0.02), DARK, c, bevel=0.01)
    box("Receiver", (0.12, 0.13, 0.38), (rx, ry, 0.02), DARK, c, bevel=0.015)
    box("Battery", (0.09, 0.08, 0.16), (rx, ry - 0.1, -0.06), GOLD, c, bevel=0.012)
    cyl("Barrel", 0.032, 0.032, 0.44, "z", (rx, ry, 0.42), DARK, c, segs=16)
    for k in range(3):   # the capacitor coils: what makes it heavy-electric
        torus(f"Coil{k}", 0.052, 0.017, "z", (rx, ry, 0.28 + k * 0.08), COPPER, c, segs=24, ring=8)
    cyl("MuzzleBrake", 0.05, 0.045, 0.08, "z", (rx, ry, 0.64), GOLD, c, segs=16)
    empty("Muzzle", (rx, ry, 0.7), c)


def build_mount(c="mount"):
    """The stand. The head turns and tips about its centre (yaw any way, pitch -60 to 70 degrees), sweeping a ball of
    about 0.72 m radius (the rifle is off to the side), so nothing of the stand rises into that ball: the head hovers
    over a levitator plate, CONCIERGE-style, rather than hanging on a yoke it would cut through."""
    new_collection(c)
    cyl("Plinth", 0.36, 0.32, 0.12, "y", (0, -0.82, 0), DARK, c, segs=40)
    cyl("PlinthTrim", 0.33, 0.33, 0.025, "y", (0, -0.752, 0), GOLD, c, segs=40)
    cyl("Levitator", 0.25, 0.22, 0.035, "y", (0, -0.73, 0), WHITE, c, segs=40)
    cyl("Emitter", 0.15, 0.15, 0.012, "y", (0, -0.708, 0), SCREEN, c, segs=40)
    torus("EmitterRing", 0.165, 0.014, "y", (0, -0.707, 0), GOLD, c, segs=40, ring=8)
    for k in range(3):   # three short prongs around the plate
        a = math.radians(120 * k + 60)
        box(f"Prong{k}", (0.05, 0.06, 0.05), (0.29 * math.sin(a), -0.72, 0.29 * math.cos(a)), GOLD, c, bevel=0.012)


def main():
    """Two exports through blender_kit.export (one FBX convention for all four bots): the head, then the stand. Each
    also dumps a preview JSON for tools/skyline/bots/preview.py (no GPU) beside the out folder passed as --preview-dir."""
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    pdir = Path(args[args.index("--preview-dir") + 1]) if "--preview-dir" in args else None
    OUT.mkdir(parents=True, exist_ok=True)
    fresh()
    build_head()
    fold("Drum", ["Bezel", "DrumBand", "Fin0", "Fin1", "Fin2", "Fin3", "RearCap", "FinCore"])
    fold("Visor", ["Neck", "VisorScreen", "VisorBrim"])
    fold("Receiver", ["RifleBracket", "Battery", "Barrel", "Coil0", "Coil1", "Coil2", "MuzzleBrake"])
    bpy.data.objects["Receiver"].name = "Rifle"
    fold("WeakPoint", ["Reflector"])
    for pid in ("housing-left", "housing-right"):
        fold(f"Plate_{pid}", [f"Vent_{pid}_{k}" for k in range(4)] + [f"Hub_{pid}"])
    bk.export(str(OUT / "spotlight.fbx"), str(pdir / "spotlight.json") if pdir else None)
    fresh()
    build_mount()
    fold("Plinth", ["PlinthTrim", "Levitator", "Emitter", "EmitterRing", "Prong0", "Prong1", "Prong2"])
    bk.export(str(OUT / "spotlight_mount.fbx"), str(pdir / "spotlight_mount.json") if pdir else None)
    print("BUILD OK")


main()
