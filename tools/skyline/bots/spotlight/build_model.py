"""Builds the spotlight's model in Blender (background) and exports it for Unity as two FBX files.

    blender -b --factory-startup -P tools/skyline/bots/spotlight/build_model.py -- [--preview out.png]

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
(the front faces -Y) so the FBX lands in Unity with identity rotations (Apply Transform). Colours are CONCIERGE white and
gold (SkylineBotLook's values, written as linear colour so Unity's importer gets them back).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

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


def export(coll_name, path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in COLL[coll_name].all_objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="AUTO", use_custom_props=False)
    print("exported", path, len(COLL[coll_name].all_objects), "objects")


def reimport_check(path: Path, want):
    """Re-imports an FBX into a scratch scene and checks the contract's names and that no object came in rotated."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    got = [o for o in bpy.data.objects if o not in before]
    names = {o.name.split(".")[0] for o in got}
    missing = [n for n in want if n not in names]
    print("reimport", path.name, len(got), "objects; missing:", missing or "none")
    for o in got:
        if o.name.split(".")[0] in want:
            print(f"  {o.name:22s} parent={o.parent.name if o.parent else '-':18s} loc={tuple(round(v, 3) for v in o.matrix_world.translation)}")
    for o in got:
        rot = o.matrix_world.to_euler()
        if o.parent is None and max(abs(rot.x), abs(rot.y), abs(rot.z)) > 1e-3 and o.type == "MESH" and not o.name.startswith("Strut"):
            print("  ROTATED:", o.name, tuple(round(math.degrees(v), 1) for v in rot))
    for o in got:
        bpy.data.objects.remove(o)
    return not missing


def preview(png: Path, target_u, dist=4.2, height=0.6, side=0.55, lens=50):
    """A small Cycles CPU still (no GPU): a camera to the front-right, a key light, a soft world."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 800, 520
    scene.render.filepath = str(png)
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.32, 0.33, 0.36, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world
    t = B(target_u)
    cam_u = (target_u[0] + side * dist, target_u[1] + height, target_u[2] + dist)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = lens
    scene.collection.objects.link(cam)
    cam.location = B(cam_u)
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (t - cam.location).to_track_quat("-Z", "Y")
    scene.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), math.radians(-20), math.radians(-30))
    scene.collection.objects.link(sun)
    bpy.ops.render.render(write_still=True)
    print("preview", png)


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
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    png = Path(args[args.index("--preview") + 1]) if "--preview" in args else None
    bpy.ops.wm.read_factory_settings(use_empty=True)
    OUT.mkdir(parents=True, exist_ok=True)
    build_head()
    build_mount()
    export("head", OUT / "spotlight.fbx")
    export("mount", OUT / "spotlight_mount.fbx")
    ok = reimport_check(OUT / "spotlight.fbx", ["Plate_housing-left", "Plate_housing-right", "WeakPoint", "Lamp", "Face", "Muzzle"])
    ok &= reimport_check(OUT / "spotlight_mount.fbx", ["Plinth", "Levitator"])
    if png:
        # aim the head a little down and to the side, as it would look at a dummy below it
        pivot = bpy.data.objects.new("pivot", None)
        COLL["head"].objects.link(pivot)
        for o in list(COLL["head"].objects):
            if o.parent is None and o is not pivot:
                o.parent = pivot
        pivot.rotation_euler = (math.radians(-14), 0, math.radians(-25))
        preview(png, (0, -0.2, 0), dist=3.6, height=-0.5, side=0.7)
    print("BUILD OK" if ok else "BUILD CHECK FAILED")


main()
