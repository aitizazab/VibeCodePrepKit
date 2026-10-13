"""The Unit's robot arms (Task 18), shared by both guns, built from code in Blender and exported as FBX for Unity:

    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender.exe> -b --factory-startup \
        -P tools/skyline/guns/writ/model/build_arms.py -- <repo>

Writes <repo>/Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/arms/arms.fbx (SkylineViewmodel instantiates
Resources/SkylineModels/arms/arms under each gun's root). Two top-level objects:
  HandR  the right hand on the grip, its forearm running back out of the bottom right of the view
  HandL  the left hand, its forearm running back out of the bottom left
Each hand's origin is the centre of what it holds. The look (spec 1.2 / the creator's concept inset): white robotic hand
plates on graphite joints, the forearm a shell in the firm's tow-truck orange with a hazard band (yellow with black
diagonal stripes) at the wrist and a small amber status lamp.

The FBX holds the Writ's hold. The right hand holds both guns as it is (the Writ's grip column sits on the Breacher's grip
axis); the left hand cups the Writ's grip under the right hand, and for the Breacher SkylineRobotArms moves it onto the
pump (POSES below; the C# copy is SkylineRobotArms.Poses: keep the two in step).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import writkit as K  # noqa: E402
from writkit import add, box, cylinder, sphere, sub  # noqa: E402

ORANGE, YELLOW, STRIPE, WHITE, JOINT, PAD, LAMP = range(7)

GRIP_TOP = (0.0, -0.035, -0.052)
GRIP_RAKE = 16.0

# Hand poses in the gun's frame: (origin, forward, up). The hand's own frame: y up along what it holds, z toward the side
# its fingers wrap over, x toward its palm (right hand) / away from it (left hand). Unity's LookRotation(forward, up).
RAKED_FWD = (0.0, -math.sin(math.radians(GRIP_RAKE)), math.cos(math.radians(GRIP_RAKE)))
RAKED_UP = (0.0, math.cos(math.radians(GRIP_RAKE)), math.sin(math.radians(GRIP_RAKE)))


def grip_point(s):
    return add(GRIP_TOP, K.rx((0.0, -s, 0.0), GRIP_RAKE))


POSES = {
    "HandR": (grip_point(0.035), RAKED_FWD, RAKED_UP),                              # both guns
    "HandL.writ": (add(grip_point(0.047), (-0.004, 0.0, 0.004)), K.norm(K.ry(RAKED_FWD, -14)), RAKED_UP),
    "HandL.breacher": ((0.0, -0.016, 0.218), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0)),      # under the pump, along it
}


def look_rotation(fwd, up):
    """The 3x3 rotation (columns = the hand's x, y, z in the gun's frame) of Unity's Quaternion.LookRotation."""
    z = K.norm(fwd)
    x = K.norm(K.cross(up, z))       # Unity is left-handed: x = up x forward
    y = K.cross(z, x)
    return x, y, z


def posed(parts, pose):
    o, fwd, up = pose
    x, y, z = look_rotation(fwd, up)

    def f(v):
        return add(o, add(add(K.scale(x, v[0]), K.scale(y, v[1])), K.scale(z, v[2])))
    return [p.moved(f) for p in parts]


# ---------------------------------------------------------------- pieces (in the hand's own frame)


def seg(a, b, w, h, mat):
    """A finger segment: a box from a to b, w wide (across the finger) and h thick."""
    d = sub(b, a)
    L = math.sqrt(K.dot(d, d))
    dn = K.scale(d, 1 / L)
    yaw = math.degrees(math.atan2(dn[0], dn[2]))
    pitch = -math.degrees(math.asin(max(-1.0, min(1.0, dn[1]))))
    return box(K.scale(add(a, b), 0.5), (w, h, L * 1.02), mat, e=(pitch, yaw, 0))


def finger(points, w, h):
    P = []
    for i in range(len(points) - 1):
        P.append(seg(points[i], points[i + 1], w, h, WHITE))
    for i, p in enumerate(points[:-1]):
        P.append(sphere(p, min(w, h) * 0.52, JOINT, rings=5, segs=8))
    P.append(sphere(points[-1], min(w, h) * 0.45, WHITE, rings=5, segs=8))
    return P


def wrap(radius, y, angles):
    return [(radius * math.sin(math.radians(a)), y, radius * math.cos(math.radians(a))) for a in angles]


def forearm(start, direction, length=0.27):
    """An octagonal shell from start along direction: white cuff, hazard band, orange shell, graphite elbow, amber lamp."""
    d = K.norm(direction)
    yaw = math.degrees(math.atan2(d[0], d[2]))
    pitch = -math.degrees(math.asin(d[1]))
    P = []

    def along(t0, t1, r0, r1, mat, n=8):
        c = add(start, K.scale(d, (t0 + t1) / 2))
        return cylinder((0, 0, 0), r0, t1 - t0, mat, axis="z", n=n, r_end=r1, smooth=False).rotated((pitch, yaw, 0)).at(c)

    P.append(along(0.0, 0.016, 0.0155, 0.0165, JOINT, n=12))          # the wrist joint
    P.append(along(0.014, 0.026, 0.0195, 0.020, WHITE))                # cuff
    P.append(along(0.026, 0.074, 0.0205, 0.0215, YELLOW))              # hazard band
    for k in range(4):                                                  # black diagonal stripes over it
        c = add(start, K.scale(d, 0.034 + 0.011 * k))
        ring = cylinder((0, 0, 0), 0.0219, 0.0042, STRIPE, axis="z", n=8, smooth=False)
        P.append(ring.rotated((0, 0, 0)).rotated((32, 0, 0)).rotated((pitch, yaw, 0)).at(c))
    P.append(along(0.074, length - 0.02, 0.0215, 0.0265, ORANGE))      # the orange shell
    P.append(along(0.1, 0.104, 0.0222, 0.0222, JOINT))                 # a panel seam
    P.append(along(0.17, 0.174, 0.0242, 0.0242, JOINT))
    P.append(along(length - 0.02, length, 0.024, 0.018, JOINT, n=12))  # the elbow cap
    lamp_at = add(start, K.scale(d, 0.088))
    up = K.norm(K.cross(K.cross(d, (0, 1, 0)), d)) if abs(d[1]) < 0.95 else (0, 0, 1)
    P.append(box(add(lamp_at, K.scale(up, 0.0212)), (0.006, 0.003, 0.009), LAMP, e=(pitch, yaw, 0)))
    return P


def right_hand():
    P = []
    # palm: a white back plate on the grip's right side over a dark pad, the web over the backstrap
    P.append(box((0.031, -0.003, -0.006), (0.008, 0.054, 0.046), WHITE))
    P.append(box((0.0245, -0.003, -0.006), (0.005, 0.05, 0.042), PAD))
    P.append(box((0.016, 0.019, -0.027), (0.03, 0.015, 0.013), WHITE, e=(0, -20, 0)))
    P.append(box((0.036, -0.003, -0.006), (0.002, 0.03, 0.026), JOINT))         # a hatch on the back of the hand
    # three fingers wrapped round the front strap under the guard, the little one smaller
    for y, w in ((0.010, 0.0112), (-0.006, 0.011), (-0.021, 0.0104), (-0.034, 0.0092)):
        P += finger(wrap(0.0305, y, (72, 34, -8, -50)), w, 0.0102)
    # the index finger along the right side to the trigger
    P += finger([(0.027, 0.026, 0.018), (0.028, 0.033, 0.038), (0.017, 0.038, 0.051), (0.006, 0.039, 0.056)], 0.0105, 0.0098)
    # the thumb along the left side, high, pointing forward
    P += finger([(-0.008, 0.024, -0.028), (-0.024, 0.026, -0.008), (-0.027, 0.028, 0.012)], 0.0118, 0.011)
    P += forearm((0.024, -0.024, -0.036), (0.36, -0.78, -0.51))
    return P


def left_hand():
    """A mirror of the right hand without the extended index: four fingers wrap whatever it holds (wider), palm on the
    near side."""
    P = []
    P.append(box((-0.035, -0.003, -0.006), (0.008, 0.054, 0.048), WHITE))
    P.append(box((-0.0285, -0.003, -0.006), (0.005, 0.05, 0.044), PAD))
    P.append(box((-0.040, -0.003, -0.006), (0.002, 0.03, 0.026), JOINT))
    for y, w in ((0.019, 0.0112), (0.004, 0.0112), (-0.011, 0.011), (-0.025, 0.0098)):
        P += finger(wrap(0.0405, y, (-74, -36, 2, 40)), w, 0.0102)
    P += finger([(0.006, 0.026, -0.034), (0.022, 0.028, -0.016), (0.027, 0.03, 0.004)], 0.0118, 0.011)
    P += forearm((-0.028, -0.024, -0.038), (-0.36, -0.78, -0.51))
    return P


def materials():
    m = K.material
    return [
        m("Arms_Orange", (0.93, 0.36, 0.04), rough=0.42),
        m("Arms_Yellow", (1.0, 0.78, 0.1), rough=0.4),
        m("Arms_Stripe", (0.025, 0.025, 0.03), rough=0.6),
        m("Arms_White", (0.9, 0.9, 0.88), rough=0.33),
        m("Arms_Joint", (0.11, 0.11, 0.12), rough=0.4, metal=0.6),
        m("Arms_Pad", (0.05, 0.05, 0.055), rough=0.8),
        m("Arms_Lamp", (1.0, 0.6, 0.15), rough=0.3, emit=(1.0, 0.55, 0.1), strength=5.0),
    ]


def build():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = materials()
    for name, parts, pose in (("HandR", right_hand(), POSES["HandR"]), ("HandL", left_hand(), POSES["HandL.writ"])):
        K.to_object(name, posed(parts, pose), mats, origin=pose[0], bevel=0.0004)


def main():
    repo, _ = K.repo_from_argv()
    build()
    out = repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "arms" / "arms.fbx"
    K.export_fbx(out, "ARMS_MODEL")
    for k, (o, f, u) in POSES.items():
        print("ARMS_POSE", k, "origin", tuple(round(v, 4) for v in o), "forward", tuple(round(v, 4) for v in f),
              "up", tuple(round(v, 4) for v in u))


if __name__ == "__main__":
    main()
