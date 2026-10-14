"""Checks the Writ and the robot arms by eye and by ray, from the exported FBX files (Task 18): CPU renders (Cycles on the
CPU, no GPU) and the sight-line check from the viewmodel camera.

    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender.exe> -b --factory-startup \
        -P tools/skyline/guns/writ/model/preview.py -- <repo> <out dir> <view>[,<view>...]

Views:
  side      the Writ alone, three-quarter from the left (like the concept)
  hip       the Writ and the arms from the viewmodel camera at the rest pose (SkylineViewmodel.WritRest)
  sight     the inspect's sight check: the gun centred so the eye looks along the sight line through the window
  breacher  the Breacher and the arms (left hand moved onto the pump) from the viewmodel camera at its rest pose
  arms      the Writ and the arms three-quarter from the front left
The viewmodel camera (task-14-report.md): 60 degrees vertical, 16:9, near 0.01 m; the gun's root sits at the rest offset
in the camera's frame, so in the gun's frame the eye is at -rest. Prints SIGHT_* lines (first hits, screen positions).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import writkit as K  # noqa: E402
import build_arms as A  # noqa: E402
import build_writ as W  # noqa: E402

# SkylineViewmodel's rests (fix round 1: moved out and up so the hands and the grip show; the Writ's convergence yaw is
# now its rest rotation, not SkylineWritFx's "home" layer)
WRIT_REST, WRIT_REST_EULER = (0.2, -0.13, 0.5), (0.0, -9.0, 0.0)
BREACHER_REST, BREACHER_REST_EULER = (0.2, -0.13, 0.5), (0.0, -5.0, 0.0)
# poses to check (offset, euler, mag offset) from SkylineGuns.Writ(): the reload's held pose (the gun tilted 16 degrees up, the
# hands and the grip still: no magazine) and the equip's middle
POSES = {
    "reload": ((0.0, 0.012, -0.015), (-16.0, 0.0, 0.0), (0.0, 0.0, 0.0)),
    "equip": ((-0.03, 0.03, -0.04), (-5.0, -35.0, -8.0), (0.0, 0.0, 0.0)),
}
SIGHT_Y, SIGHT_REAR_Z, SIGHT_FRONT_Z = 0.088, -0.079, 0.202
SIGHT_EYE_DIST = 0.13
# the Writ's home pose, layered on the viewmodel's by SkylineWritFx (HomeYaw / HomeOffset): a convergence yaw toward the
# crosshair, so the colonnade's side shows, and a small lift
TAN = math.tan(math.radians(30))
ASPECT = 16 / 9


def models(repo: Path):
    return repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels"


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def imp(path: Path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), axis_forward="-Z", axis_up="Y")
    return [o for o in bpy.data.objects if o not in before]


def by_name(objs, name):
    for o in objs:
        if o.name.split(".")[0] == name:
            return o
    return None


def unity_matrix(origin, fwd, up):
    x, y, z = A.look_rotation(fwd, up)
    m = Matrix(((x[0], y[0], z[0]), (x[1], y[1], z[1]), (x[2], y[2], z[2])))
    return m


SHOULDER_R, SHOULDER_L = (0.30, -0.42, 0.0), (-0.22, -0.44, 0.02)   # camera frame: SkylineRobotArms.ShoulderR / ShoulderL


def side_objs(arms, side):
    return [o for o in (by_name(arms, "Hand" + side), by_name(arms, "Forearm" + side), by_name(arms, "Elbow" + side)) if o is not None]


def aim_forearms(arms, pos, R):
    """As SkylineRobotArms does: each forearm turned about its wrist so its elbow points at the shoulder."""
    for side, sh in (("R", SHOULDER_R), ("L", SHOULDER_L)):
        fa, el = by_name(arms, "Forearm" + side), by_name(arms, "Elbow" + side)
        if fa is None or el is None:
            continue
        bpy.context.view_layer.update()
        shoulder = K.C_U2B @ (R.inverted() @ (Vector(sh) - pos))
        wrist = fa.matrix_world.translation.copy()
        rot = (el.matrix_world.translation - wrist).rotation_difference(shoulder - wrist).to_matrix().to_4x4()
        m = Matrix.Translation(wrist) @ rot @ Matrix.Translation(-wrist)
        fa.matrix_world = m @ fa.matrix_world
        el.matrix_world = m @ el.matrix_world


def repose(ob, from_pose, to_pose):
    """Moves a hand object from one pose to another (both in the gun's Unity frame), as SkylineRobotArms does."""
    r0 = unity_matrix(*from_pose)
    r1 = unity_matrix(*to_pose)
    d = r1 @ r0.inverted()                     # the change of rotation, Unity frame
    db = K.C_U2B @ d @ K.C_U2B.inverted()      # in Blender's frame
    p0 = K.C_U2B @ Vector(from_pose[0])
    p1 = K.C_U2B @ Vector(to_pose[0])
    m = Matrix.Translation(p1) @ db.to_4x4() @ Matrix.Translation(-p0)
    ob.matrix_world = m @ ob.matrix_world


def lights(dark=False):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.08, 0.06, 0.06, 1) if dark else (0.42, 0.45, 0.5, 1)
    bg.inputs["Strength"].default_value = 0.7 if dark else 1.0
    scene.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(40), math.radians(-20), math.radians(-35))
    bpy.context.collection.objects.link(sun)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
    fill.data.energy = 0.9
    fill.data.color = (1.0, 0.7, 0.45)
    fill.rotation_euler = (math.radians(70), math.radians(30), math.radians(150))
    bpy.context.collection.objects.link(fill)


def eye_camera(eye, yaw=0.0):
    """The viewmodel camera at eye (gun frame) looking along the gun frame's +z turned by -yaw (the gun is yawed by yaw in
    the camera's frame)."""
    cd = bpy.data.cameras.new("cam")
    cd.sensor_fit = "VERTICAL"
    cd.angle_y = math.radians(60)
    cd.clip_start = 0.01
    cam = bpy.data.objects.new("cam", cd)
    bpy.context.collection.objects.link(cam)
    base = bpy.data.objects.new("tmp", None)
    base.rotation_euler = (math.radians(90), 0.0, math.radians(180))
    bpy.context.collection.objects.link(base)
    bpy.context.view_layer.update()
    a = math.radians(-yaw)
    ry_u = Matrix(((math.cos(a), 0, math.sin(a)), (0, 1, 0), (-math.sin(a), 0, math.cos(a))))
    rb = K.C_U2B @ ry_u @ K.C_U2B.inverted()
    cam.matrix_world = Matrix.Translation(Vector(K.U(*eye))) @ rb.to_4x4() @ base.matrix_world.to_3x3().to_4x4()
    bpy.data.objects.remove(base)
    bpy.context.scene.camera = cam


def umat(e):
    """Unity's Quaternion.Euler(e) as a 3x3 matrix (columns: where x, y, z go)."""
    cols = [K.euler(v, e) for v in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
    return Matrix(((cols[0][0], cols[1][0], cols[2][0]), (cols[0][1], cols[1][1], cols[2][1]), (cols[0][2], cols[1][2], cols[2][2])))


def gun_pose(rest, rest_e, off=(0, 0, 0), e=(0, 0, 0)):
    """The gun root in the camera's frame: position rest + off, rotation Euler(rest_e) * Euler(e) (SkylineViewmodel)."""
    return Vector(K.add(rest, off)), umat(rest_e) @ umat(e)


def eye_in_gun(pos, R):
    return tuple(R.inverted() @ (-pos))


def fp_camera(pos, R):
    """The viewmodel camera, placed in the gun's frame."""
    cd = bpy.data.cameras.new("cam")
    cd.sensor_fit = "VERTICAL"
    cd.angle_y = math.radians(60)
    cd.clip_start = 0.01
    cam = bpy.data.objects.new("cam", cd)
    bpy.context.collection.objects.link(cam)
    base = Matrix.Rotation(math.radians(180), 4, "Z") @ Matrix.Rotation(math.radians(90), 4, "X")
    rb = K.C_U2B @ R.inverted() @ K.C_U2B.inverted()
    cam.matrix_world = Matrix.Translation(K.C_U2B @ Vector(eye_in_gun(pos, R))) @ rb.to_4x4() @ base
    bpy.context.scene.camera = cam


def screen_of(pos, R, p):
    q = R @ Vector(p) + pos
    return None if q.z <= 0 else (q.x / q.z / (TAN * ASPECT), q.y / q.z / TAN)


def retint(objs, repo):
    """Gives the imported Writ the look SkylineWritFx gives it in Unity (each texture times its tint)."""
    K.TINT_IN_SHADER = True
    table = {m.name: m for m in W.materials(models(repo) / "writ")}
    for o in objs:
        for slot in getattr(o, "material_slots", []):
            if slot.material is not None and slot.material.name.split(".")[0] in table:
                slot.material = table[slot.material.name.split(".")[0]]


def orbit_camera(pos, target, lens=55):
    cd = bpy.data.cameras.new("cam")
    cd.lens = lens
    cd.clip_start = 0.01
    cam = bpy.data.objects.new("cam", cd)
    cam.location = K.U(*pos)
    bpy.context.collection.objects.link(cam)
    t = bpy.data.objects.new("target", None)
    t.location = K.U(*target)
    bpy.context.collection.objects.link(t)
    con = cam.constraints.new("TRACK_TO")
    con.target = t
    con.track_axis = "TRACK_NEGATIVE_Z"
    con.up_axis = "UP_Y"
    bpy.context.scene.camera = cam


def render(path: Path, w=960, h=540, samples=20):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    print("SIGHT_RENDER", path)


def first_hit(origin_u, target_u, max_d=None):
    scene = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()
    o = K.C_U2B @ Vector(origin_u)
    t = K.C_U2B @ Vector(target_u)
    d = t - o
    dist = d.length if max_d is None else max_d
    hit, loc, _n, idx, ob, _m = scene.ray_cast(dg, o, d.normalized(), distance=dist + 0.002)
    if not hit:
        return None, None, None
    mat = None
    try:
        me = ob.data
        mi = me.polygons[idx].material_index
        mat = ob.material_slots[mi].name if mi < len(ob.material_slots) else None
    except Exception:  # noqa: BLE001
        pass
    return ob.name.split(".")[0], mat, (loc - o).length


def screen(eye, p, yaw=0.0):
    q = K.ry(K.sub(p, eye), yaw)
    if q[2] <= 0:
        return None
    return (q[0] / q[2] / (TAN * ASPECT), q[1] / q[2] / TAN)


def report_hip(eye, label, yaw=0.0):
    targets = {
        "bead (front sight)": (0, SIGHT_Y, SIGHT_FRONT_Z),
        "window centre (rear sight)": (0, SIGHT_Y + 0.006, SIGHT_REAR_Z),
        "muzzle": (0, 0.012, 0.214),
        "paper on the table": (0.002, 0.0671, -0.036),
        "grip base": (0, -0.131, -0.08),
    }
    for name, p in targets.items():
        hit, mat, dist = first_hit(eye, p)
        want = math.dist(eye, p)
        clear_ = hit is None or dist >= want - 0.004
        s = screen(eye, p, yaw)
        print(f"SIGHT_{label} {name}: first hit {hit} / {mat} at {dist if dist is None else round(dist, 4)} m "
              f"(target {round(want, 4)} m) -> {'visible' if clear_ else 'BLOCKED'}; screen {tuple(round(v, 3) for v in s)}")


def report_sight(eye):
    """Rays from the sight eye through the window: what fills the sight picture."""
    counts = {}
    n = 0
    for iy in range(-12, 13):
        for ix in range(-12, 13):
            px, py = ix * 0.0011, iy * 0.0011
            if px * px + py * py > 0.0127 ** 2:
                continue
            n += 1
            hit, mat, dist = first_hit(eye, (px, SIGHT_Y + py, SIGHT_REAR_Z), max_d=3.0)
            if hit is None:
                key = "open sky"
            else:
                key = f"{hit}/{mat}"
            counts[key] = counts.get(key, 0) + 1
    for k, v in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"SIGHT_PICTURE {k}: {v} of {n} rays ({100 * v / n:.0f}%)")
    for dy in (0.0, 0.0006, -0.0006, 0.001, -0.001, 0.002):
        hit, mat, dist = first_hit(eye, (0, SIGHT_Y + dy, SIGHT_REAR_Z), max_d=3.0)
        print(f"SIGHT_AXIS dy={dy * 1000:+.1f} mm at the window: first hit {hit} / {mat} at {dist if dist is None else round(dist, 4)} m")


def main():
    repo, rest = K.repo_from_argv()
    out = Path(rest[0])
    out.mkdir(parents=True, exist_ok=True)
    views = rest[1].split(",") if len(rest) > 1 else ["side", "hip", "sight"]
    M = models(repo)
    for view in views:
        clear()
        if view == "breacher":
            imp(M / "breacher" / "breacher.fbx")
            arms = imp(M / "arms" / "arms.fbx")
            for o in side_objs(arms, "L"):
                repose(o, A.POSES["HandL.writ"], A.POSES["HandL.breacher"])
            pos, R = gun_pose(BREACHER_REST, BREACHER_REST_EULER)
            aim_forearms(arms, pos, R)
            for name, p in (("right fist", (0.0, -0.07, -0.06)), ("left hand", A.POSES["HandL.breacher"][0]), ("muzzle", (0, 0.035, 0.40)), ("top", (0, 0.09, 0.12))):
                print("SIGHT_BREACHER", name, "screen", tuple(round(v, 3) for v in screen_of(pos, R, p)))
            lights()
            fp_camera(pos, R)
            render(out / "fp-breacher.png")
            continue
        objs = imp(M / "writ" / "writ.fbx")
        retint(objs, repo)
        arms = []
        if view in ("hip", "sight", "arms") or view in POSES:
            arms = imp(M / "arms" / "arms.fbx")
        lights(dark=view == "side")
        if view == "side":
            orbit_camera((-0.62, 0.16, 0.12), (0, -0.01, 0.07), lens=50)
            render(out / "side.png")
        elif view == "arms":
            orbit_camera((-0.45, 0.05, 0.45), (0, -0.04, 0.02), lens=40)
            render(out / "arms.png")
        elif view == "hip" or view in POSES:
            off, e, mag = POSES.get(view, ((0, 0, 0), (0, 0, 0), (0, 0, 0)))
            pos, R = gun_pose(WRIT_REST, WRIT_REST_EULER, off, e)
            if any(mag):
                for o in [by_name(objs, "Mag")] + side_objs(arms, "L"):
                    if o is not None:
                        o.location = o.location + K.C_U2B @ Vector(mag)
            if arms:
                aim_forearms(arms, pos, R)
            eye = eye_in_gun(pos, R)
            if view == "hip":
                report_hip(eye, "HIP", 0.0)
            for name, p in (("right fist", (0.01, -0.07, -0.06)), ("grip base", (0, -0.131, -0.08)), ("drum", K.add((0, -0.115, -0.075), mag)),
                            ("paper", (0.002, 0.0671, -0.036)), ("bead", (0, SIGHT_Y, SIGHT_FRONT_Z)), ("muzzle", (0, 0.012, 0.214))):
                sc = screen_of(pos, R, p)
                print(f"SIGHT_SCREEN {view} {name}", None if sc is None else tuple(round(v, 3) for v in sc))
            fp_camera(pos, R)
            render(out / f"fp-{view}.png")
        elif view == "sight":
            eye = (0.0, SIGHT_Y, SIGHT_REAR_Z - SIGHT_EYE_DIST)
            report_sight(eye)
            eye_camera(eye)
            render(out / "fp-sight.png")


main()
