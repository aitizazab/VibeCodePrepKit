"""The mall-cop drone's model (Task 20), built in Blender and exported as FBX for
Resources/SkylineModels/bots/mall-cop-drone.fbx. A flying white egg wearing a mall cop's peaked cap: the gold crown with a
white star badge (plate "shell") and the gold visor brim over its face screen (plate "lens-guard"); the body-cam lens
under the screen (the weak point); a taser pod with two prongs under its chin; a radio whip antenna; two ducted rotors on
struts either side.

    blender.exe -b --factory-startup -P tools/skyline/bots/mall-cop-drone/model.py -- <out.fbx> [preview.json]

Unity contract (Scripts/Bots/SkylineBotBodies.cs): origin at the centre; children Plate_shell, Plate_lens-guard,
WeakPoint, Face, Muzzle, and Rotors (the spinner: SkylineBot spins each child about its own local Y).
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import blender_kit as k  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "Assets", "JamKit", "Templates",
                                         "SkylineRepo", "Resources", "SkylineModels", "bots", "mall-cop-drone.fbx")
PREVIEW = args[1] if len(args) > 1 else None

k.reset()
white = k.mat("CONCIERGE_White", k.WHITE, 0.0, 0.35)
gold = k.mat("CONCIERGE_Gold", k.GOLD, 0.8, 0.3)
dark = k.mat("Trim_Dark", k.DARK, 0.2, 0.5)
grey = k.mat("Steel_Grey", k.GREY, 0.6, 0.45)
lens = k.mat("Lens_Glass", k.LENS, 0.0, 0.05)
red = k.mat("Rec_Red", (0.9, 0.12, 0.1), 0.0, 0.3)
blade = k.mat("Rotor_Blade", (0.3, 0.31, 0.34), 0.3, 0.5)

# no root node: every part is a top-level node (the FBX exporter bakes the axis conversion only one level deep, so a
# grandchild would come in rotated); Unity makes the prefab root, named after the file, at identity
root = None

# the body: a white egg
k.sphere("Chassis", 0.36, (0, -0.02, 0), white, root, scale=(1.0, 0.86, 1.04), segs=40, rings=20)
k.torus("Waist", 0.345, 0.018, (0, -0.06, 0), dark, root, segs=48, minor_segs=8)

# the face: a dark bezel on the front, the screen tilted a little down toward the people below. It stands proud of the
# egg (judge round 2: the egg's front, z 0.374, pushed through the old screen at 0.337 and left only the eye corners): the
# face now sits about 1 cm in front of the egg's surface everywhere across it
TILT = -8.0
SCREEN = (0, 0.0, 0.35)
k.box("Bezel", (0.46, 0.25, 0.07), SCREEN, dark, root, bevel=0.03, tilt=TILT, segments=3)
front, n = k.tilted_front(SCREEN, 0.07, TILT)
k.face_plane("Face", 0.42, 0.21, k.along(front, n, 0.002), root, tilt=TILT)

# plate 0, the shell: the cap's gold crown (a squashed dome over the top and back) with a white star badge and a dark band
CROWN_Y = 0.12
shell = k.sphere("Plate_shell", 0.37, (0, CROWN_Y, -0.035), gold, root, scale=(1.02, 0.62, 1.0), segs=40, rings=20)
k.cut_below(shell, CROWN_Y)
k.torus("CapBand", 0.37, 0.024, (0, CROWN_Y + 0.02, -0.035), dark, shell, segs=48, minor_segs=8)
k.star("Badge", 0.055, 0.024, 0.02, (0, 0.235, 0.3), white, shell)

# plate 1, the lens-guard: the cap's gold visor brim jutting over the screen. Kept above the screen and short (judge round
# 1: the old brim reached 14 cm past the screen and drooped over its top, so at eye level and below the face read as a
# black band): its front edge sits 3 cm above the face's top and 6-7 cm in front of the screen, still a peaked cap.
k.arc_slab("Plate_lens-guard", 0.02, 0.24, 0.162, 0.185, -80, 80, gold, root, segs=24, center=(0, 0.16), bevel=0.008, droop=0.025)

# the weak point: the body-cam lens under the screen (blue glass in a steel ring), a red REC light beside it
# (brought forward with the screen, so the bezel's lip never shades it)
wp = k.empty("WeakPoint", (0, -0.2, 0.33), root)
k.sphere("CamLens", 0.062, (0, -0.2, 0.33), lens, wp, scale=(1, 1, 0.6), segs=24, rings=12)
k.torus("CamRing", 0.066, 0.014, (0, -0.2, 0.33), grey, wp, axis="z", segs=24, minor_segs=8)
k.sphere("RecLight", 0.016, (0.1, -0.2, 0.33), red, root, segs=12, rings=6)

# the taser pod under the chin, two prongs, the muzzle between them
k.cyl("TaserPod", 0.055, 0.2, (0, -0.31, 0.17), grey, root, axis="z", verts=24, bevel=0.015)
for side, x in (("L", -0.028), ("R", 0.028)):
    k.cyl("Prong" + side, 0.009, 0.07, (x, -0.31, 0.3), gold, root, axis="z", verts=10)
k.empty("Muzzle", (0, -0.31, 0.34), root)

# radio whip antenna on the back
k.tube("Antenna", (0.12, 0.12, -0.3), (0.2, 0.42, -0.42), 0.007, dark, root)
k.sphere("AntennaTip", 0.016, (0.2, 0.42, -0.42), dark, root, segs=12, rings=6)

# struts and rotor ducts either side; the rotors under "Rotors" (each blade set's origin at its hub)
rotors = k.empty("Rotors", (0, 0.06, 0), root)
for side, x in (("L", -0.6), ("R", 0.6)):
    sx = -1 if x < 0 else 1
    k.tube("Strut" + side, (sx * 0.28, 0.0, 0), (sx * 0.45, 0.06, 0), 0.025, grey, root)
    k.torus("Duct" + side, 0.175, 0.03, (x, 0.06, 0), white, root, segs=40, minor_segs=10)
    k.cyl("Motor" + side, 0.04, 0.06, (x, 0.04, 0), grey, root, verts=16, bevel=0.008)
    hub = k.cyl("Rotor" + side, 0.022, 0.025, (x, 0.075, 0), dark, None, verts=12)
    b1 = k.box("Blade%sA" % side, (0.3, 0.006, 0.04), (x, 0.075, 0), blade, None, bevel=0.0, tilt=8, tilt_axis="x")
    r = k.merge([hub, b1], "Rotor" + side)
    k.set_parent(r, rotors)

k.export(os.path.abspath(OUT), PREVIEW)
