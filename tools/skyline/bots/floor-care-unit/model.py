"""The floor-care unit's model (Task 20), built in Blender and exported as FBX for
Resources/SkylineModels/bots/floor-care-unit.fbx. A one-metre commercial floor robot: a white disc on a rubber skirt,
a gold domed lid and a gold front bumper (the two plates), a tilted face screen on a neck with an amber beacon, the
acid canister on its back-left shoulder (the weak point: green, so it reads as the thing to shoot), a hose down to the
spray nozzle at the front, and a spinning side brush at the front-right.

    blender.exe -b --factory-startup -P tools/skyline/bots/floor-care-unit/model.py -- <out.fbx> [preview.json]

Unity contract (Scripts/Bots/SkylineBotBodies.cs): origin on the floor, centre 0.4 m up; children Plate_lid,
Plate_bumper, WeakPoint, Face, Nozzle, and Brush (the spinner: its local Y spin is set by SkylineBot).
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import blender_kit as k  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "Assets", "JamKit", "Templates",
                                         "SkylineRepo", "Resources", "SkylineModels", "bots", "floor-care-unit.fbx")
PREVIEW = args[1] if len(args) > 1 else None

k.reset()
white = k.mat("CONCIERGE_White", k.WHITE, 0.0, 0.35)
gold = k.mat("CONCIERGE_Gold", k.GOLD, 0.8, 0.3)
dark = k.mat("Rubber_Dark", k.DARK, 0.0, 0.8)
grey = k.mat("Steel_Grey", k.GREY, 0.6, 0.45)
acid = k.mat("Acid_Glass", k.ACID, 0.0, 0.1)
amber = k.mat("Beacon_Amber", (1.0, 0.62, 0.12), 0.0, 0.2)
bristle = k.mat("Bristle", (0.78, 0.8, 0.84), 0.0, 0.9)

# no root node: every part is a top-level node (the FBX exporter bakes the axis conversion only one level deep, so a
# grandchild would come in rotated); Unity makes the prefab root, named after the file, at identity
root = None

# body: skirt, chassis, deck, wheels
k.cyl("Skirt", 0.485, 0.07, (0, 0.055, 0), dark, root, verts=56, bevel=0.012)
k.cyl("Chassis", 0.5, 0.22, (0, 0.19, 0), white, root, verts=64, bevel=0.035)
k.cyl("Deck", 0.36, 0.02, (0, 0.305, 0), grey, root, verts=48)
for side, x in (("L", -0.43), ("R", 0.43)):
    k.cyl("Wheel" + side, 0.065, 0.05, (x, 0.065, -0.02), dark, root, axis="x", verts=24, bevel=0.01)

# plate 0: the gold lid (a low dome)
lid = k.cyl("Plate_lid", 0.44, 0.05, (0, 0.335, -0.02), gold, root, verts=64, bevel=0.022)
k.cyl("LidHandle", 0.07, 0.02, (0.0, 0.37, -0.3), grey, lid, verts=24, bevel=0.006)

# plate 1: the gold front bumper, a curved band round the front
k.arc_slab("Plate_bumper", 0.49, 0.535, 0.1, 0.25, -68, 68, gold, root, segs=28, bevel=0.012)

# the head: a neck, a white housing tilted back, a dark bezel, the face screen, an amber beacon
k.cyl("Neck", 0.055, 0.2, (0, 0.42, 0.15), grey, root, verts=20)
TILT = 18.0
HOUSING = (0, 0.6, 0.16)
k.box("Housing", (0.6, 0.34, 0.14), HOUSING, white, root, bevel=0.045, tilt=TILT, segments=3)
front, n = k.tilted_front(HOUSING, 0.14, TILT)
k.box("Bezel", (0.55, 0.285, 0.03), k.along(front, n, 0.0), dark, root, bevel=0.012, tilt=TILT)
k.face_plane("Face", 0.5, 0.25, k.along(front, n, 0.0165), root, tilt=TILT)
k.sphere("Beacon", 0.045, (0.0, 0.79, 0.12), amber, root, scale=(1, 0.8, 1))
k.cyl("BeaconBase", 0.055, 0.025, (0.0, 0.77, 0.12), grey, root, verts=24)

# the weak point: the acid canister on the back-left shoulder, with steel caps and a hose to the nozzle. Set out to the
# left and standing tall (judge round 1 fix: from the front the head hid most of it; tools/skyline/bots/weakpoint_reach.py
# now finds it from all four of the judge's angles)
TANK = (-0.32, 0.54, -0.2)
wp = k.empty("WeakPoint", TANK, root)
k.cyl("AcidTank", 0.12, 0.38, TANK, acid, wp, verts=32)
k.cyl("TankCapTop", 0.13, 0.04, (TANK[0], 0.745, TANK[2]), grey, wp, verts=32, bevel=0.01)
k.cyl("TankCapBottom", 0.13, 0.04, (TANK[0], 0.355, TANK[2]), grey, wp, verts=32, bevel=0.01)
k.cyl("TankValve", 0.025, 0.06, (TANK[0], 0.79, TANK[2]), dark, wp, verts=12)
k.tube("Hose1", (-0.39, 0.37, -0.12), (-0.47, 0.3, 0.12), 0.022, dark, root)
k.tube("Hose2", (-0.47, 0.3, 0.12), (-0.36, 0.17, 0.42), 0.022, dark, root)
k.tube("Hose3", (-0.36, 0.17, 0.42), (-0.05, 0.09, 0.5), 0.022, dark, root)

# the spray nozzle low at the front, under the bumper
k.cyl("NozzleBody", 0.04, 0.14, (0, 0.085, 0.53), grey, root, axis="z", verts=20, bevel=0.008)
k.cyl("NozzleTip", 0.028, 0.05, (0, 0.085, 0.615), dark, root, axis="z", verts=16, r2=0.018)
k.empty("Nozzle", (0, 0.085, 0.65), root)

# the side brush (the spinner): a hub and three splayed bristle tufts, origin at the hub
HUB = (0.33, 0.035, 0.38)
hub = k.cyl("Brush", 0.045, 0.035, HUB, grey, None, verts=20)
tufts = []
for i, ang in enumerate((0, 120, 240)):
    a = math.radians(ang)
    dx, dz = -math.sin(a) * 0.12, math.cos(a) * 0.12   # the tuft's long axis after its turn about y
    t = k.box("Tuft%d" % i, (0.03, 0.012, 0.22), (HUB[0] + dx, 0.022, HUB[2] + dz), bristle, None, tilt=ang, tilt_axis="y")
    tufts.append(t)
brush = k.merge([hub] + tufts, "Brush")
k.set_parent(brush, root)

k.export(os.path.abspath(OUT), PREVIEW)
