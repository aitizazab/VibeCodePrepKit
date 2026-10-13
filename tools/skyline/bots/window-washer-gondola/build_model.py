"""Builds the window-washer gondola's model in Blender (background) and exports it for Unity.

    blender -b --factory-startup -P tools/skyline/bots/window-washer-gondola/build_model.py -- [--preview-dir <dir>]

(run it through the GPU wrapper / capped-run, as the machine's rules say). Writes
Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/bots/window-washer-gondola.fbx.

The model's origin is the point riding the rail (the pure GondolaRail's position; SkylineBot aims at it and sees from it),
so the platform's working middle sits around it. It faces +Z, away from its wall. Children named by the Unity contract
(SkylineBotBodies): Plate_bucket-left, Plate_bucket-right (the PlateDefs' ids, in order: the two gold buckets),
WeakPoint (the winch's exposed copper coil under the trolley), Face (the control box's display, UV 0-1), Rail (the
trolley that rides the rail bar SkylineBot draws 0.1 m above it), MineDrop (the mine crate's wall side, where each mine
is tossed from) and SqueegeeArm (the arm's pivot on the top rail; SkylineBot swings it about X: back to wind up, forward to
slap).

Every number below is in Unity's frame (metres; +Y up, +Z the bot's front, +X its right): the helpers convert to Blender
(the front faces -Y) and every node keeps an identity rotation; the FBX is written by blender_kit.export, the
convention all four bots share. Colours are CONCIERGE white and
gold (SkylineBotLook's values, written as linear colour so Unity's importer gets them back); no orange anywhere
(the firm's colour, spec 1.3).
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


# ------------------------------------------------------------------------------------------------ the gondola

BEACON = ("Beacon_Gold", (0.95, 0.80, 0.40), 0.3)   # a gold lamp, not hi-vis orange: orange is the firm's (spec 1.3)
SUDS = ("Suds", (0.86, 0.93, 0.98), 0.7)
MINE_RED = ("Mine_LED", (0.95, 0.12, 0.08), 0.4)


def side(s):
    return "L" if s < 0 else "R"


def build(c="gondola"):
    new_collection(c)
    # the platform: a white deck on a dark frame, a gold kick plate along the front
    box("Deck", (2.2, 0.08, 0.7), (0, -0.42, 0.15), WHITE, c, bevel=0.02)
    box("DeckFrame", (2.0, 0.06, 0.55), (0, -0.49, 0.15), DARK, c, bevel=0.01)
    box("KickPlate", (2.2, 0.12, 0.025), (0, -0.33, 0.5), GOLD, c, bevel=0.008)
    for s in (-1, 1):
        box("ToeBoard" + side(s), (0.025, 0.12, 0.7), (s * 1.09, -0.33, 0.15), GOLD, c, bevel=0.008)
    # the railing: white posts, a gold top rail
    for i, x in enumerate((-1.07, -0.36, 0.36, 1.07)):
        cyl(f"Post{i}", 0.022, 0.022, 0.63, "y", (x, -0.065, 0.47), WHITE, c, segs=12)
    cyl("TopRail", 0.028, 0.028, 2.2, "x", (0, 0.25, 0.47), GOLD, c, segs=16)
    cyl("MidRail", 0.018, 0.018, 2.16, "x", (0, -0.07, 0.47), WHITE, c, segs=12)
    cyl("BackRail", 0.018, 0.018, 2.16, "x", (0, 0.0, -0.18), WHITE, c, segs=12)
    for s in (-1, 1):
        cyl("SideRail" + side(s), 0.028, 0.028, 0.66, "z", (s * 1.07, 0.25, 0.145), GOLD, c, segs=16)
    # the stirrups at each end, and the cables up to the winch
    for s in (-1, 1):
        n = side(s)
        apex = (s * 1.02, 0.6, 0.14)
        rod("StirrupBack" + n, (s * 1.07, -0.38, -0.18), apex, 0.024, WHITE, c)
        rod("StirrupFront" + n, (s * 1.07, -0.38, 0.47), apex, 0.024, WHITE, c)
        sphere("StirrupEye" + n, 0.045, apex, GOLD, c)
        rod("Cable" + n, apex, (s * 0.13, 0.96, -0.4), 0.011, DARK, c, segs=8)
    # the trolley on the rail bar (SkylineBot draws the bar 0.1 m above Rail's origin, 0.06 thick)
    rail = box("Rail", (0.56, 0.14, 0.24), (0, 1.27, -0.55), DARK, c, bevel=0.02)
    for s in (-1, 1):
        box("Cheek" + side(s), (0.5, 0.26, 0.025), (0, 1.43, -0.55 + s * 0.075), WHITE, c, parent=rail, bevel=0.008)
        cyl("Wheel" + side(s), 0.05, 0.05, 0.1, "z", (s * 0.17, 1.48, -0.55), GOLD, c, parent=rail, segs=16)
    # the winch hanging under it: the copper coil drum is the weak point
    for s in (-1, 1):
        box("WinchHanger" + side(s), (0.03, 0.24, 0.1), (s * 0.21, 1.11, -0.47), WHITE, c, bevel=0.006)
        cyl("WinchFlange" + side(s), 0.16, 0.16, 0.025, "x", (s * 0.185, 0.98, -0.42), GOLD, c, segs=28)
    cyl("WeakPoint", 0.125, 0.125, 0.34, "x", (0, 0.98, -0.42), COPPER, c, segs=28)
    box("WinchMotor", (0.14, 0.15, 0.18), (0.29, 0.98, -0.42), DARK, c, bevel=0.015)
    # the gold buckets (the plates), soap suds and wire handles; a spare squeegee in the left one
    for s, pid in ((-1, "bucket-left"), (1, "bucket-right")):
        b = cyl(f"Plate_{pid}", 0.14, 0.18, 0.3, "y", (s * 0.8, -0.23, 0.12), GOLD, c, segs=28)
        torus(f"Rim_{pid}", 0.18, 0.016, "y", (s * 0.8, -0.08, 0.12), GOLD, c, parent=b, segs=28, ring=8)
        sphere(f"Suds_{pid}", 0.165, (s * 0.8, -0.09, 0.12), SUDS, c, parent=b, scale=(1, 0.28, 1))
        rod(f"Handle_{pid}", (s * 0.8 - 0.17, -0.08, 0.12), (s * 0.8, 0.1, 0.12), 0.008, DARK, c, parent=b, segs=6)
        rod(f"Handle2_{pid}", (s * 0.8, 0.1, 0.12), (s * 0.8 + 0.17, -0.08, 0.12), 0.008, DARK, c, parent=b, segs=6)
        if s < 0:
            rod("SpareSqueegee", (-0.86, -0.1, 0.1), (-0.95, 0.28, 0.0), 0.015, DARK, c, parent=b, segs=8)
    # the control box: the face's display in a gold frame, a gold beacon on top
    box("ControlBox", (0.5, 0.42, 0.18), (0, -0.16, 0.36), WHITE, c, bevel=0.03)
    box("ScreenFrame", (0.5, 0.32, 0.012), (0, -0.12, 0.452), GOLD, c, bevel=0.004)
    box("Screen", (0.46, 0.28, 0.02), (0, -0.12, 0.456), SCREEN, c, bevel=0.004)
    face_plane("Face", 0.44, 0.26, (0, -0.12, 0.468), c)
    cyl("BeaconBase", 0.055, 0.055, 0.03, "y", (0.16, 0.065, 0.36), DARK, c, segs=16)
    cyl("Beacon", 0.045, 0.04, 0.08, "y", (0.16, 0.12, 0.36), BEACON, c, segs=16)
    for k in range(3):   # control buttons
        cyl(f"Button{k}", 0.018, 0.018, 0.015, "z", (-0.15 + k * 0.05, -0.33, 0.456), GOLD if k else WHITE, c, segs=10)
    # the mine crate, three disc mines on top; mines are tossed from its wall side
    box("MineCrate", (0.3, 0.2, 0.26), (-0.38, -0.28, 0.02), DARK, c, bevel=0.015)
    for k in range(3):
        m = cyl(f"Mine{k}", 0.11, 0.11, 0.035, "y", (-0.38, -0.16 + k * 0.04, 0.02), DARK, c, segs=24)
        torus(f"MineRim{k}", 0.105, 0.008, "y", (-0.38, -0.16 + k * 0.04, 0.02), GOLD, c, parent=m, segs=24, ring=6)
    sphere("MineLed", 0.02, (-0.38, -0.075, 0.02), MINE_RED, c)
    empty("MineDrop", (-0.38, -0.22, -0.12), c)
    # the squeegee: an arm hinged on the top rail
    sq = empty("SqueegeeArm", (0.55, 0.25, 0.47), c)
    cyl("SqueegeePole", 0.02, 0.02, 0.56, "y", (0.55, 0.53, 0.47), WHITE, c, parent=sq, segs=12)
    box("SqueegeeHolder", (0.5, 0.05, 0.05), (0.55, 0.82, 0.47), GOLD, c, parent=sq, bevel=0.01)
    box("SqueegeeBlade", (0.52, 0.025, 0.07), (0.55, 0.855, 0.47), DARK, c, parent=sq, bevel=0.006)
    cyl("SqueegeeHinge", 0.04, 0.04, 0.07, "x", (0.55, 0.25, 0.47), DARK, c, segs=16)


def main():
    """One export through blender_kit.export (one FBX convention for all four bots), with a preview JSON for
    tools/skyline/bots/preview.py (no GPU) in the folder passed as --preview-dir."""
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    pdir = Path(args[args.index("--preview-dir") + 1]) if "--preview-dir" in args else None
    OUT.mkdir(parents=True, exist_ok=True)
    fresh()
    build()
    # compact folds only: the railing, the winch's hangers and flanges and the stirrups stay separate, or their joined
    # box colliders would cover the buckets (plates) and the copper coil (the weak point)
    fold("Deck", ["DeckFrame", "KickPlate", "ToeBoardL", "ToeBoardR"])
    fold("Rail", ["CheekL", "CheekR", "WheelL", "WheelR"])
    fold("ControlBox", ["ScreenFrame", "Screen", "BeaconBase", "Beacon", "Button0", "Button1", "Button2"])
    fold("MineCrate", ["Mine0", "Mine1", "Mine2", "MineRim0", "MineRim1", "MineRim2", "MineLed"])
    fold("SqueegeePole", ["SqueegeeHolder", "SqueegeeBlade"])
    bpy.data.objects["SqueegeePole"].name = "Squeegee"
    for s, n in ((-1, "L"), (1, "R")):
        fold("Cable" + n, ["StirrupEye" + n])
    for pid in ("bucket-left", "bucket-right"):
        extra = ["SpareSqueegee"] if pid == "bucket-left" else []
        fold(f"Plate_{pid}", [f"Rim_{pid}", f"Suds_{pid}", f"Handle_{pid}", f"Handle2_{pid}"] + extra)
    bk.export(str(OUT / "window-washer-gondola.fbx"), str(pdir / "window-washer-gondola.json") if pdir else None)
    print("BUILD OK")


main()
