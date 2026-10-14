"""The Breacher's viewmodel (Task 19), built from code in Blender and exported as FBX for Unity:

    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender.exe> -b --factory-startup \
        -P tools/skyline/guns/breacher/model/build_breacher.py -- <repo> [--preview out.png]

Writes <repo>/Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/breacher/breacher.fbx (SkylineViewmodel loads
Resources/SkylineModels/breacher/breacher) with the children the game looks for:
  Mag      the pressure cell (the reload's magOffset moves it; SkylineBreacherFx makes its window glow and vents from it)
  Muzzle   an empty at the breaching muzzle's mouth (tracers and the muzzle blast start here)
  CellVent an empty at the cell's vent port (the vent puff after each shot)
Every part is a top-level object (no parenting), so each child's local position in Unity is its place in the gun's frame.

Coordinates below are Unity's (x right, y up, z forward, metres); U() maps them into Blender's frame for the FBX
exporter's -Z forward / Y up conversion (Unity (x, y, z) = Blender (-x, z, -y)). The size matches the grey-box primitive
the viewmodel drew before (body 8.5 x 9.5 x 30 cm, muzzle at z 0.39), so the rest pose and the arms still fit.
The look (docs/SKYLINE-GUN-BREACHER.md): a stubby door-breach shotgun in the firm's hazard orange, a dark steel barrel
ending in a toothed breaching standoff ringed with hazard stripes, hazard-striped side plates, and a fat yellow pressure
cell slung under the receiver with an amber window that shows its charge.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
import bmesh


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    repo = Path(argv[0]) if argv else Path(__file__).resolve().parents[5]
    preview = None
    if "--preview" in argv:
        preview = argv[argv.index("--preview") + 1]
    return repo, preview


def U(x, y, z):
    """A Unity point into Blender's frame."""
    return (-x, -z, y)


# ---------------------------------------------------------------- materials

def mat(name, rgb, rough=0.5, metal=0.0, emit=None, strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        key = "Emission Color" if "Emission Color" in b.inputs else "Emission"
        b.inputs[key].default_value = (*emit, 1.0)
        b.inputs["Emission Strength"].default_value = strength
    m.diffuse_color = (*rgb, 1.0)
    return m


# ---------------------------------------------------------------- geometry helpers (all in Unity coordinates)

def face_uvs(face, verts):
    """Each flat face spans the whole texture (0..1 on its two widest axes), so the material's worn border (chipped
    paint, bright metal) lands on every edge of the part (SkylineBreacherFx's generated wear textures)."""
    pts = [verts[i] for i in face]
    ax, ay, az = (pts[1][k] - pts[0][k] for k in range(3))
    bx, by, bz = (pts[2][k] - pts[0][k] for k in range(3))
    n = (abs(ay * bz - az * by), abs(az * bx - ax * bz), abs(ax * by - ay * bx))
    drop = n.index(max(n))
    keep = [k for k in range(3) if k != drop]
    lo = [min(p[k] for p in pts) for k in keep]
    hi = [max(p[k] for p in pts) for k in keep]
    return [tuple((p[k] - lo[j]) / max(hi[j] - lo[j], 1e-6) for j, k in enumerate(keep)) for p in pts]


def mesh_object(name, verts, faces, material, bevel=0.0, uvs=None):
    """uvs: one list of (u, v) per face, in the face's vertex order; None: face_uvs for every face."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([U(*v) for v in verts], [], faces)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # outward, whatever the winding (U() mirrors x)
    bm.to_mesh(me)
    bm.free()
    layer = me.uv_layers.new(name="UVMap")
    for k, poly in enumerate(me.polygons):   # recalc may reverse a face's loops: match by vertex, not by order
        face = faces[k]
        face_uv = uvs[k] if uvs is not None else face_uvs(face, verts)
        by_vertex = dict(zip(face, face_uv))
        for li in poly.loop_indices:
            layer.data[li].uv = by_vertex[me.loops[li].vertex_index]
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    if bevel > 0:
        mod = ob.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
    return ob


def prism(name, bottom, top, material, bevel=0.0):
    """A six-sided solid between two quads (each 4 corners, counter-clockwise seen from outside the bottom)."""
    verts = list(bottom) + list(top)
    faces = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return mesh_object(name, verts, faces, material, bevel)


def box(name, c, s, material, bevel=0.0, tilt_x=0.0):
    """An axis-aligned box (centre c, size s), optionally tilted about the x axis (degrees, positive tips its top
    forward)."""
    cx, cy, cz = c
    hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
    a = math.radians(tilt_x)

    def p(dx, dy, dz):
        y = dy * math.cos(a) - dz * math.sin(a)
        z = dy * math.sin(a) + dz * math.cos(a)
        return (cx + dx, cy + y, cz + z)

    bottom = [p(-hx, -hy, -hz), p(hx, -hy, -hz), p(hx, -hy, hz), p(-hx, -hy, hz)]
    top = [p(-hx, hy, -hz), p(hx, hy, -hz), p(hx, hy, hz), p(-hx, hy, hz)]
    return prism(name, bottom, top, material, bevel)


def cylinder(name, c, radius, length, material, axis="z", verts=24, radius_end=None):
    """A cylinder (or a cone frustum with radius_end) centred at c along Unity's x or z axis."""
    re_ = radius if radius_end is None else radius_end
    ring0, ring1 = [], []
    for i in range(verts):
        t = 2 * math.pi * i / verts
        u, v = math.cos(t), math.sin(t)
        if axis == "z":
            ring0.append((c[0] + radius * u, c[1] + radius * v, c[2] - length / 2))
            ring1.append((c[0] + re_ * u, c[1] + re_ * v, c[2] + length / 2))
        else:
            ring0.append((c[0] - length / 2, c[1] + radius * u, c[2] + radius * v))
            ring1.append((c[0] + length / 2, c[1] + re_ * u, c[2] + re_ * v))
    vs = ring0 + ring1
    faces = [tuple(range(verts - 1, -1, -1)), tuple(range(verts, 2 * verts))]
    cap = lambda idx: [(0.5 + 0.5 * math.cos(2 * math.pi * (i % verts) / verts), 0.5 + 0.5 * math.sin(2 * math.pi * (i % verts) / verts)) for i in idx]
    uvs = [cap(faces[0]), cap(faces[1])]
    for i in range(verts):
        j = (i + 1) % verts
        faces.append((i, j, verts + j, verts + i))
        u0, u1 = 0.2 + 0.6 * i / verts, 0.2 + 0.6 * (i + 1) / verts   # kept off the texture's worn border: wear only at the rims
        uvs.append([(u0, 0.0), (u1, 0.0), (u1, 1.0), (u0, 1.0)])
    ob = mesh_object(name, vs, faces, material, uvs=uvs)
    for poly in ob.data.polygons:
        poly.use_smooth = len(poly.vertices) == 4
    return ob


def empty(name, c):
    ob = bpy.data.objects.new(name, None)
    ob.location = U(*c)
    ob.empty_display_size = 0.01
    bpy.context.collection.objects.link(ob)
    return ob


def join(name, objects, origin):
    """Joins objects into one (modifiers applied) and puts its origin at `origin` (Unity coords)."""
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects:
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        for mod in list(ob.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    bpy.context.scene.cursor.location = U(*origin)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    return ob


# ---------------------------------------------------------------- the gun

def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # The firm's orange, deep and a little red (hue about 16 degrees, linear value 0.70): sunlit through ACES the old
    # (0.93, 0.36, 0.04) clipped to lemon yellow (hue 50, the bots' gold; judge round 1, A6). Matte, so the sun's
    # highlight doesn't wash it out either.
    orange = mat("Breacher_Orange", (0.70, 0.19, 0.02), rough=0.6)
    seam = mat("Breacher_Seam", (0.12, 0.05, 0.02), rough=0.8)
    steel = mat("Breacher_Steel", (0.2, 0.2, 0.22), rough=0.32, metal=0.85)
    dark = mat("Breacher_Dark", (0.07, 0.07, 0.08), rough=0.75)
    yellow = mat("Breacher_Cell", (1.0, 0.78, 0.1), rough=0.38)
    black = mat("Breacher_Stripe", (0.025, 0.025, 0.03), rough=0.6)
    glow = mat("Breacher_CellGlow", (1.0, 0.55, 0.12), rough=0.3, emit=(1.0, 0.5, 0.1), strength=6.0)

    body = []
    # receiver, the brace behind it, the top shroud with its vent slots
    body.append(box("Receiver", (0, 0.02, 0.05), (0.085, 0.095, 0.30), orange, bevel=0.006))
    body.append(box("Brace", (0, 0.025, -0.135), (0.07, 0.07, 0.05), orange, bevel=0.006))
    body.append(box("BracePad", (0, 0.025, -0.165), (0.074, 0.076, 0.014), dark, bevel=0.003))
    body.append(box("Shroud", (0, 0.078, 0.12), (0.062, 0.022, 0.22), steel, bevel=0.004))
    for i in range(4):
        body.append(box(f"Slot{i}", (0, 0.0895, 0.05 + 0.045 * i), (0.042, 0.004, 0.02), black))
    # panel seams and rivets on the receiver's sides, so it isn't a flat slab
    for side in (-1, 1):
        x = side * 0.0432
        for z in (0.06, 0.15):
            body.append(box(f"Seam{side}{z}", (x + side * 0.0006, 0.02, z), (0.0016, 0.09, 0.003), seam))
        body.append(box(f"SeamH{side}", (x + side * 0.0006, -0.018, 0.1), (0.0016, 0.003, 0.2), seam))
        for z in (0.035, 0.085, 0.125, 0.175):
            for y in (0.055, -0.008):
                body.append(cylinder(f"Rivet{side}{z}{y}", (x + side * 0.002, y, z), 0.0035, 0.004, steel, axis="x", verts=8))
    # hazard-striped side plates (yellow with four black diagonals each side)
    for side in (-1, 1):
        x = side * 0.0435
        body.append(box(f"Plate{side}", (x + side * 0.001, 0.022, -0.02), (0.003, 0.05, 0.11), yellow))
        for k in range(4):
            body.append(box(f"Stripe{side}{k}", (x + side * 0.0028, 0.022, -0.065 + 0.03 * k), (0.002, 0.044, 0.011), black,
                            tilt_x=40.0))
    # barrel, breaching standoff (hazard rings, teeth)
    body.append(cylinder("Barrel", (0, 0.035, 0.27), 0.026, 0.14, steel))
    body.append(cylinder("Standoff", (0, 0.035, 0.36), 0.036, 0.05, dark, radius_end=0.034))
    for k, m_ in enumerate((yellow, black, yellow, black, yellow)):
        body.append(cylinder(f"Ring{k}", (0, 0.035, 0.338 + 0.008 * k), 0.0372, 0.008, m_))
    for k in range(6):
        t = 2 * math.pi * k / 6 + math.pi / 6
        body.append(box(f"Tooth{k}", (0.03 * math.cos(t), 0.035 + 0.03 * math.sin(t), 0.392), (0.012, 0.012, 0.016), steel,
                        bevel=0.002))
    # pump with ribs, grip, trigger guard
    body.append(box("Pump", (0, -0.018, 0.225), (0.074, 0.044, 0.11), dark, bevel=0.006))
    for k in range(4):
        body.append(box(f"Rib{k}", (0, -0.018, 0.19 + 0.024 * k), (0.078, 0.048, 0.008), steel, bevel=0.002))
    body.append(prism("Grip",
                      [(-0.022, -0.125, -0.105), (0.022, -0.125, -0.105), (0.022, -0.125, -0.05), (-0.022, -0.125, -0.05)],
                      [(-0.022, -0.02, -0.075), (0.022, -0.02, -0.075), (0.022, -0.02, -0.02), (-0.022, -0.02, -0.02)],
                      dark, bevel=0.006))
    body.append(box("Guard", (0, -0.042, -0.005), (0.012, 0.012, 0.055), steel))
    body.append(box("GuardFront", (0, -0.034, 0.02), (0.012, 0.024, 0.008), steel))
    # detail (judge round 3: "a plain orange brick"): sights, a charging handle, a trigger, an accessory rail, barrel
    # clamps with a heat-shield strap, the cell's guard rails and bolt heads on the standoff
    body.append(box("SightFront", (0, 0.097, 0.222), (0.006, 0.016, 0.01), steel, bevel=0.0015))
    body.append(box("SightRear", (0, 0.094, 0.025), (0.026, 0.01, 0.008), steel, bevel=0.0015))
    body.append(box("SightNotch", (0, 0.1, 0.025), (0.008, 0.006, 0.0085), black))
    body.append(box("ChargeSlot", (0.0436, 0.045, 0.1), (0.002, 0.012, 0.08), black))
    body.append(box("ChargeHandle", (0.052, 0.045, 0.125), (0.016, 0.01, 0.014), steel, bevel=0.002))
    body.append(cylinder("ChargeKnob", (0.062, 0.045, 0.125), 0.007, 0.012, dark, axis="x", verts=12))
    body.append(box("Trigger", (0, -0.04, 0.0), (0.006, 0.022, 0.008), steel, bevel=0.001))
    body.append(box("Rail", (-0.0455, 0.045, 0.1), (0.006, 0.012, 0.16), steel, bevel=0.0015))
    for k in range(6):
        body.append(box(f"RailNotch{k}", (-0.0486, 0.045, 0.035 + 0.026 * k), (0.002, 0.012, 0.006), black))
    for z in (0.215, 0.3):
        body.append(cylinder(f"Clamp{z}", (0, 0.035, z), 0.03, 0.01, dark, verts=16))
    body.append(box("HeatStrap", (0, 0.064, 0.26), (0.03, 0.004, 0.1), steel, bevel=0.001))
    for k in range(4):
        body.append(box(f"HeatHole{k}", (0, 0.0665, 0.222 + 0.025 * k), (0.016, 0.0015, 0.01), black))
    for side in (-1, 1):
        body.append(box(f"CellGuard{side}", (side * 0.026, -0.084, 0.07), (0.004, 0.006, 0.13), steel, bevel=0.001))
    for k in range(6):
        t = 2 * math.pi * k / 6
        body.append(cylinder(f"Bolt{k}", (0.031 * math.cos(t), 0.035 + 0.031 * math.sin(t), 0.386), 0.003, 0.004, steel, verts=6))
    # the cell's cradle (stays on the gun when the cell drops)
    body.append(box("Cradle", (0, -0.03, 0.07), (0.07, 0.012, 0.13), steel, bevel=0.003))
    receiver = join("Receiver", body, (0, 0, 0))

    # the cell: a fat yellow cylinder with black hazard bands, domed caps, an amber charge window and a vent nozzle
    cell = [
        cylinder("CellBody", (0, -0.055, 0.07), 0.027, 0.11, yellow),
        cylinder("CapBack", (0, -0.055, 0.0075), 0.027, 0.015, steel, radius_end=0.027),
        cylinder("CapBack2", (0, -0.055, -0.004), 0.022, 0.008, steel, radius_end=0.016),
        cylinder("CapFront", (0, -0.055, 0.1325), 0.027, 0.015, steel),
        cylinder("CapFront2", (0, -0.055, 0.144), 0.022, 0.008, steel, radius_end=0.016),
        cylinder("BandA", (0, -0.055, 0.035), 0.0285, 0.009, black),
        cylinder("BandB", (0, -0.055, 0.105), 0.0285, 0.009, black),
        box("Window", (0.0262, -0.055, 0.07), (0.006, 0.016, 0.05), glow),
        box("WindowL", (-0.0262, -0.055, 0.07), (0.006, 0.016, 0.05), glow),
        cylinder("VentNozzle", (0.03, -0.062, 0.128), 0.006, 0.014, steel, axis="x"),
    ]
    mag = join("Mag", cell, (0, -0.055, 0.07))

    empty("Muzzle", (0, 0.035, 0.40))
    empty("CellVent", (0.038, -0.062, 0.128))
    return receiver, mag


def export(repo: Path):
    out = repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "breacher" / "breacher.fbx"
    out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=str(out), use_selection=False, object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode="STRIP",
        embed_textures=False)
    print("BREACHER_MODEL wrote", out)
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            print("BREACHER_MODEL", ob.name, "verts", len(ob.data.vertices), "faces", len(ob.data.polygons))


def preview(path: str):
    """A small three-quarter render (Cycles on the CPU, no GPU) to check the shape by eye."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 960, 540
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.32, 0.34, 0.38, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = U(0.62, 0.26, 0.52)
    target = bpy.data.objects.new("target", None)
    target.location = U(0, 0.0, 0.11)
    bpy.context.collection.objects.link(target)
    con = cam.constraints.new("TRACK_TO")
    con.target = target
    con.track_axis = "TRACK_NEGATIVE_Z"
    con.up_axis = "UP_Y"
    scene.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(40))
    bpy.context.collection.objects.link(sun)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("BREACHER_MODEL preview", path)


def main():
    repo, prev = args()
    build()
    export(repo)
    if prev:
        bpy.ops.object.select_all(action="DESELECT")
        preview(prev)


main()
