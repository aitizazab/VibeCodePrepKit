"""The Writ's viewmodel (Task 18), built from code in Blender and exported as FBX for Unity:

    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender.exe> -b --factory-startup \
        -P tools/skyline/guns/writ/model/build_writ.py -- <repo>

Writes <repo>/Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/writ/writ.fbx (SkylineViewmodel loads
Resources/SkylineModels/writ/writ). The creator's design (docs/SKYLINE-CONCEPTS.md, "The Writ: final design"): a gothic
courtroom in miniature grown into a pistol silhouette. A fluted stone column wrapped in a parchment scroll is the grip;
a stone colonnade with small gaslamps and a brass plate is the body; the great pointed doorway at the front, glowing
within, is the muzzle; the judge's EMPTY high-backed chair and bench sit at the rear. The sights: a slim brass obelisk
with an amber bead on the arch's keystone (front), and a round window through the chair's back with one horizontal
brass bar (rear). Seen through the window, the bar crosses the obelisk at the bead: a cross. A brass gavel floats over
the bench's left end (toward the player's eye), clear of the sight line.

Top-level objects (every part top-level, so each child's local position in Unity is its place in the gun's frame):
  Body       the static gun (stone, wood, brass, lamps, the doorway's glow, the front sight)
  Bench      the judge's bench and chair with the rear sight: the "slide" (SkylineWritFx kicks it on a shot, racks it
             on the bolt)
  Gavel      the floating gavel; its origin is the head's centre (SkylineWritFx strikes the sound block with it)
  DoorL/R    the doorway's two leaves, origins on their hinges (SkylineWritFx throws them open on a shot)
  Mag        the grip column's lower drum and plinth, with the cell's amber band (the reload's magOffset moves it)
  Inkwell    the back (the creator's pick, 2026-10-14: "A take 2: inkwell and quill"): a stone alcove and shelf set into
             the receiver's rear face under the chair, a brass-framed glass jar (the glass is a transparent look set in
             SkylineWritFx) with a brass rim; static
  InkLevel   the glowing indigo ink inside the jar, a separate object with its pivot at the bottom of the well: SkylineWritFx
             scales its height to the magazine (the ink is the ammo)
  Quill      a feather and nib, a separate object with its pivot at the nib / the inkwell's mouth, modelled in its hip pose
             (leaning back and out of the sight line); SkylineWritFx rotates it (flick on a shot, flat in ADS, dips and
             stirs in a reload). Its constants are mirrored in Runtime/Skyline/WritQuill.cs: change them together
  Muzzle     an empty at the doorway's centre (tracers and the muzzle flash start here)
  SightRear  an empty at the window's centre;  SightFront  an empty at the bead's centre
Same footprint and grip as the Breacher's (the grip column sits on the Breacher's grip axis, 16 degrees raked), so the
shared robot arms' right hand holds both. Materials are named Writ_* (SkylineWritFx sets their textures and glow).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy  # noqa: E402
import writkit as K  # noqa: E402
from writkit import add, box, cylinder, frustum_box, slab, sphere  # noqa: E402

# ---------------------------------------------------------------- the gun's frame (Unity, metres)

SIGHT_Y = 0.088                       # the sight line's height: the window's centre, the bar and the bead
REAR_Z, FRONT_Z = -0.085, 0.212       # the chair back's rear face, the arch's front face
BOTTOM, DECK0, DECK1 = -0.036, 0.026, 0.034
RCV_Z0, COL_Z0, COL_Z1, ARCH_Z0 = -0.080, 0.028, 0.155, 0.155
WIN_Z0, WIN_Z1 = -0.085, -0.073      # the chair back's thickness
WIN_R = 0.0135
BEAD_Z = 0.205                        # the obelisk's axis
BEAD_FACE_Z = 0.2020                  # the bead's centre: on the obelisk's rear face, so its whole disc shows from behind
GRIP_TOP = (0.0, -0.035, -0.052)     # on the Breacher's grip axis (top (0,-0.02,-0.0475), bottom (0,-0.125,-0.0775))
GRIP_RAKE = 16.0                      # degrees: the column's bottom swings back
MUZZLE = (0.0, 0.012, 0.214)
GAVEL_AT = (-0.026, 0.093, -0.040)   # over the bench's LEFT end: in view at the hip, outside the sight picture

# the back's inkwell and quill (WritQuill.cs mirrors these): the well stands on a shelf under the chair, right of centre
WELL_X, WELL_Z = 0.012, -0.100
SHELF_Y0, SHELF_Y1 = -0.036, -0.026                   # the shelf's underside (the receiver's bottom) and top
JAR_Y0, INK_Y1 = -0.0217, 0.0                         # the jar's floor and the full ink level
INK_R, GLASS_R = 0.0102, 0.0113
QUILL_PIVOT = (WELL_X, 0.002, WELL_Z)                 # the nib / the well's mouth
QUILL_HIP_DIR = (0.42, 0.78, -0.46)                   # leaning back toward the player and out, away from the sight line
FEATHER_LEN = 0.0805

STONE, STONE_DARK, WOOD, MAROON, BRASS, IRON, PARCH, LAMP, DOOR_GLOW, BEAD, CELL, STONE_LIGHT, INK, FEATHER, GLASS = range(15)


def materials(tex: Path):
    m = K.material
    return [
        m("Writ_Stone", (0.82, 0.8, 0.78), rough=0.82, image=tex / "writ_stone.png"),
        m("Writ_StoneDark", (0.55, 0.53, 0.52), rough=0.85, image=tex / "writ_stone.png"),
        m("Writ_Wood", (1.0, 1.0, 1.0), rough=0.48, image=tex / "writ_wood.png"),
        m("Writ_Maroon", (0.36, 0.06, 0.07), rough=0.62),
        m("Writ_Brass", (0.83, 0.62, 0.28), rough=0.32, metal=0.95),
        m("Writ_Iron", (0.09, 0.085, 0.08), rough=0.55, metal=0.7),
        m("Writ_Parchment", (1.0, 1.0, 1.0), rough=0.8, image=tex / "writ_parchment.png"),
        m("Writ_LampGlow", (1.0, 0.72, 0.36), rough=0.3, emit=(1.0, 0.55, 0.18), strength=4.0),
        m("Writ_DoorGlow", (1.0, 0.6, 0.25), rough=0.4, emit=(1.0, 0.5, 0.14), strength=5.0),
        m("Writ_Bead", (1.0, 0.62, 0.12), rough=0.2, emit=(1.0, 0.55, 0.08), strength=6.0),
        m("Writ_CellGlow", (1.0, 0.6, 0.18), rough=0.3, emit=(1.0, 0.5, 0.12), strength=5.0),
        m("Writ_StoneLight", (0.9, 0.87, 0.82), rough=0.78, image=tex / "writ_stone.png"),
        m("Writ_Ink", (0.2, 0.1, 0.75), rough=0.25, emit=(0.35, 0.18, 1.0), strength=4.0),
        m("Writ_Feather", (0.94, 0.94, 0.97), rough=0.8),
        m("Writ_Glass", (0.7, 0.85, 1.0), rough=0.1),
    ]


# ---------------------------------------------------------------- pieces


def sconce(x_side: float, y: float, z: float):
    """A wall gaslamp: a brass bracket, an iron lantern frame, amber glass, a little cap."""
    s = 1 if x_side > 0 else -1
    x = x_side
    return [
        box((x + s * 0.0025, y - 0.004, z), (0.005, 0.0016, 0.0016), BRASS),
        box((x + s * 0.0055, y, z), (0.0058, 0.0016, 0.0058), IRON),
        box((x + s * 0.0055, y + 0.0045, z), (0.0046, 0.0074, 0.0046), LAMP),
        box((x + s * 0.0055, y + 0.0045, z), (0.0062, 0.0074, 0.0012), IRON),
        frustum_box((x + s * 0.0055, y + 0.0098, z), (0.0068, 0.0068), (0.001, 0.001), 0.0034, IRON),
        box((x + s * 0.0055, y + 0.0122, z), (0.0016, 0.0016, 0.0016), BRASS),
    ]


def column(x: float, z: float):
    return [
        box((x, BOTTOM + 0.0105, z), (0.0102, 0.003, 0.0102), STONE_LIGHT),
        cylinder((x, -0.0075, z), 0.0042, 0.036, STONE_LIGHT, n=16, star=0.86),
        cylinder((x, 0.0118, z), 0.0042, 0.0034, STONE_LIGHT, n=12, r_end=0.006),
        box((x, 0.0147, z), (0.0112, 0.0026, 0.0112), STONE_LIGHT),
    ]


def body_parts():
    P = []
    mid_col = (COL_Z0 + COL_Z1) / 2
    # the rear receiver: dark wood, a brass nameplate on each side, three iron slats behind it, a brass trim at the back
    P.append(box((0, (BOTTOM + DECK0) / 2, (RCV_Z0 + COL_Z0) / 2), (0.05, DECK0 - BOTTOM, COL_Z0 - RCV_Z0), WOOD))
    # the receiver's rear face (the player sees it under the chair): a brass-framed maroon panel with the court's seal
    # (fix round 1: it was one flat brass slab)
    rz = RCV_Z0 - 0.0006
    for (cy, sy, sx) in ((DECK0 - 0.003, 0.002, 0.046), (BOTTOM + 0.004, 0.002, 0.046)):
        P.append(box((0, cy, rz), (sx, sy, 0.0016), BRASS))
    for sx_ in (-0.022, 0.022):
        P.append(box((sx_, (BOTTOM + DECK0) / 2, rz), (0.002, DECK0 - BOTTOM - 0.006, 0.0016), BRASS))
    P.append(box((0, (BOTTOM + DECK0) / 2, rz + 0.0002), (0.04, DECK0 - BOTTOM - 0.012, 0.0008), MAROON))
    P.append(cylinder((0, (BOTTOM + DECK0) / 2, rz - 0.0008), 0.0085, 0.0018, BRASS, axis="z", n=20))
    P.append(cylinder((0, (BOTTOM + DECK0) / 2, rz - 0.0016), 0.0055, 0.0012, IRON, axis="z", n=16))
    P.append(box((0, (BOTTOM + DECK0) / 2, rz - 0.0022), (0.0016, 0.007, 0.0008), BRASS))         # the seal's scales: a beam
    P.append(box((0, (BOTTOM + DECK0) / 2 + 0.0025, rz - 0.0022), (0.007, 0.0012, 0.0008), BRASS))
    for s in (-1, 1):
        P.append(box((s * 0.0255, 0.011, -0.010), (0.0018, 0.017, 0.052), BRASS))
        P.append(box((s * 0.0262, 0.011, -0.010), (0.0006, 0.012, 0.046), IRON))       # the plate's engraved field
        P.append(box((s * 0.0266, 0.011, -0.010), (0.0004, 0.0102, 0.044), BRASS))
        for k in range(3):
            P.append(box((s * 0.0256, 0.010, -0.066 + 0.0085 * k), (0.002, 0.022, 0.0042), IRON, e=(28, 0, 0)))
        P.append(box((s * 0.0255, -0.022, (RCV_Z0 + COL_Z0) / 2), (0.0016, 0.004, COL_Z0 - RCV_Z0 - 0.006), BRASS))
    # the deck: a dark stone cornice the full length, overhanging the body
    P.append(box((0, (DECK0 + DECK1) / 2, (RCV_Z0 + ARCH_Z0) / 2), (0.058, DECK1 - DECK0, ARCH_Z0 - RCV_Z0 + 0.004), STONE_DARK))
    P.append(box((0, DECK0 - 0.0015, (COL_Z0 + COL_Z1) / 2), (0.062, 0.003, COL_Z1 - COL_Z0), STONE_DARK))
    # the colonnade: an ashlar wall, a stepped base, an architrave, four fluted columns a side
    P.append(box((0, (BOTTOM + 0.006 + 0.022) / 2, mid_col), (0.038, 0.022 - BOTTOM - 0.006, COL_Z1 - COL_Z0), STONE))
    P.append(box((0, BOTTOM + 0.003, mid_col), (0.062, 0.006, COL_Z1 - COL_Z0 + 0.004), STONE_DARK))
    P.append(box((0, BOTTOM + 0.0075, mid_col), (0.056, 0.003, COL_Z1 - COL_Z0), STONE))
    P.append(box((0, 0.0195, mid_col), (0.060, 0.0065, COL_Z1 - COL_Z0 + 0.002), STONE))
    for s in (-1, 1):
        for k in range(4):
            P += column(s * 0.0245, 0.0405 + k * (0.143 - 0.0405) / 3)
    # gaslamps: at the receiver's rear, where the colonnade begins, and where it meets the arch
    for s in (-1, 1):
        P += sconce(s * 0.025, 0.004, -0.068)
        P += sconce(s * 0.031, 0.004, COL_Z0 + 0.002)
        P += sconce(s * 0.031, 0.004, COL_Z1 - 0.004)
    # the top deck: two low wooden rails along the edges (the sight line runs clear between them), newel posts, finials
    for s in (-1, 1):
        x = s * 0.0215
        P.append(box((x, DECK1 + 0.006, 0.075), (0.004, 0.012, 0.15), WOOD))
        P.append(box((x, DECK1 + 0.0125, 0.075), (0.0056, 0.0018, 0.152), WOOD))
        for k in range(5):
            P.append(box((x + s * 0.0021, DECK1 + 0.006, 0.012 + k * 0.032), (0.0006, 0.0075, 0.022), MAROON))
        for z in (-0.001, 0.151):
            P.append(box((x, DECK1 + 0.0085, z), (0.0064, 0.017, 0.0064), WOOD))
            P.append(sphere((x, DECK1 + 0.0195, z), 0.0026, BRASS))
    # the trigger guard (iron) and the trigger (brass)
    P.append(box((0, -0.0625, -0.006), (0.006, 0.0042, 0.07), IRON))
    P.append(box((0, -0.049, 0.0285), (0.006, 0.027, 0.0042), IRON))
    P.append(box((0, -0.042, 0.002), (0.0042, 0.012, 0.0042), BRASS, e=(-12, 0, 0)))
    P.append(box((0, -0.0505, 0.0052), (0.0042, 0.008, 0.0036), BRASS, e=(22, 0, 0)))
    P += arch_parts()
    P += grip_parts()
    return P


def arch_parts():
    P = []
    out_curve, out_apex = K.pointed_arch(0.032, 0.036, c_ratio=0.25)
    outline = [(-0.032, BOTTOM)] + out_curve + [(0.032, BOTTOM)]
    in_curve, in_apex = K.pointed_arch(0.0155, 0.032, c_ratio=0.5)
    opening = [(0.0155, BOTTOM + 0.006)] + list(reversed(in_curve)) + [(-0.0155, BOTTOM + 0.006)]
    P.append(slab([outline], ARCH_Z0, 0.183, STONE))                    # the solid back of the gatehouse
    P.append(slab([outline, opening], 0.183, FRONT_Z, STONE))           # the front, pierced by the doorway
    # the glow within (the court beyond the doors), just in front of the solid back
    P.append(slab([list(reversed(opening))], 0.1832, 0.1838, DOOR_GLOW))
    # two archivolt rings standing proud of the front face, and a lintel shadow line
    for (a0, a1, proud, mat) in ((0.0155, 0.0192, 0.0032, STONE_LIGHT), (0.0215, 0.0245, 0.0016, STONE_DARK)):
        c0, _ = K.pointed_arch(a0, 0.032, c_ratio=0.5 * 0.0155 / a0)
        c1, _ = K.pointed_arch(a1, 0.032, c_ratio=0.5 * 0.0155 / a1)
        ring = [(-a1, 0.004)] + c1 + [(a1, 0.004), (a0, 0.004)] + list(reversed(c0)) + [(-a0, 0.004)]
        P.append(slab([ring], FRONT_Z - 0.001, FRONT_Z + proud, mat))
    # the keystone (wider at the top) and the obelisk's plinth on it
    P.append(frustum_box((0, (in_apex + 0.003 + 0.0785) / 2, FRONT_Z - 0.006), (0.0085, 0.016), (0.0145, 0.016),
                         0.0785 - in_apex - 0.003, STONE_LIGHT))
    # the front sight: a tall slim brass obelisk on a plinth, an amber bead at the sight line's height
    P.append(box((0, 0.0805, BEAD_Z), (0.0084, 0.004, 0.0084), BRASS))
    P.append(frustum_box((0, (0.0825 + 0.112) / 2, BEAD_Z), (0.0052, 0.0052), (0.0032, 0.0032), 0.112 - 0.0825, BRASS))
    P.append(frustum_box((0, 0.1150, BEAD_Z), (0.0032, 0.0032), (0.0002, 0.0002), 0.006, BRASS))
    P.append(sphere((0, SIGHT_Y, BEAD_FACE_Z), 0.0035, BEAD, rings=8, segs=14))
    # two steps out of the doorway
    P.append(box((0, BOTTOM + 0.003, FRONT_Z + 0.007), (0.05, 0.006, 0.014), STONE_DARK))
    P.append(box((0, BOTTOM + 0.0075, FRONT_Z + 0.003), (0.042, 0.003, 0.006), STONE))
    return P


def grip_axis_point(s: float):
    """The point s metres down the grip column's axis from its top."""
    return add(GRIP_TOP, K.rx((0.0, -s, 0.0), GRIP_RAKE))


def raked(parts):
    """Parts built around a vertical axis through the origin (y down the column) placed on the raked grip axis."""
    return K.place(parts, e=(GRIP_RAKE, 0, 0), offset=GRIP_TOP)


def grip_parts():
    P = []
    # the capital: an abacus, a flared bell, eight acanthus leaves
    P.append(box((0, -0.003, 0), (0.046, 0.006, 0.05), STONE_DARK))
    P.append(cylinder((0, -0.012, 0), 0.0165, 0.012, STONE_LIGHT, n=20, r_end=0.0225))
    for k in range(8):
        leaf = box((0, -0.0125, 0.0185), (0.0072, 0.011, 0.0028), STONE_LIGHT, e=(-22, 0, 0))
        P.append(leaf.rotated((0, 45 * k + 22.5, 0)))
    # the fluted shaft (the drum below it is the Mag)
    P.append(cylinder((0, -0.041, 0), 0.0195, 0.046, STONE, n=24, star=0.88))
    # the scroll: a thin parchment shell wrapped around the front, ragged at the edges, curled at one end
    P += scroll(0.0204, -0.024, -0.058, -150.0, 150.0)
    return raked(P)


def scroll(r, y0, y1, a0, a1, n=28):
    import random
    rnd = random.Random(7)
    outer, inner = [], []
    for i in range(n + 1):
        t = math.radians(a0 + (a1 - a0) * i / n)
        jt, jb = rnd.uniform(-0.0012, 0.0012), rnd.uniform(-0.0012, 0.0012)
        sx, sz = math.sin(t), math.cos(t)
        outer.append(((r + 0.0007) * sx, y0 + jt, (r + 0.0007) * sz))
        outer.append(((r + 0.0007) * sx, y1 + jb, (r + 0.0007) * sz))
        inner.append((r * sx, y0 + jt, r * sz))
        inner.append((r * sx, y1 + jb, r * sz))
    vs = outer + inner
    m = len(outer)
    faces = []
    for i in range(n):
        a, b, c, d = 2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2
        faces.append((a, d, c, b))                       # outside
        faces.append((m + a, m + b, m + c, m + d))       # inside
        faces.append((a, m + a, m + d, d))               # top edge
        faces.append((b, c, m + c, m + b))               # bottom edge
    faces.append((0, 1, m + 1, m))
    faces.append((2 * n, m + 2 * n, m + 2 * n + 1, 2 * n + 1))
    part = K.Part(vs, faces, PARCH, uv=("cyl", (0, 0, 0), (0, 1, 0), a0 - 10, a1 + 10, y1 - 0.004, y0 + 0.004), smooth=True)
    # the curl: a little roll standing proud at the a1 end
    t = math.radians(a1)
    curl = cylinder(((r + 0.0024) * math.sin(t), (y0 + y1) / 2, (r + 0.0024) * math.cos(t)), 0.0021, y0 - y1 + 0.001, PARCH,
                    n=10, uv=("box", 0.03))
    return [part, curl]


def mag_parts():
    P = [
        cylinder((0, -0.073, 0), 0.0195, 0.018, STONE, n=24, star=0.88),
        cylinder((0, -0.0655, 0), 0.0201, 0.0032, CELL, n=24),                  # the cell's amber band at the seam
        cylinder((0, -0.0835, 0), 0.0212, 0.003, STONE_LIGHT, n=24),
        cylinder((0, -0.0868, 0), 0.0236, 0.0036, STONE_LIGHT, n=24, r_end=0.0222),
        box((0, -0.0945, 0), (0.05, 0.012, 0.05), STONE_DARK),
        box((0, -0.1005, 0), (0.052, 0.0012, 0.052), BRASS),
    ]
    return raked(P)


def bench_parts():
    P = []
    # the judge's bench: panelled wood with maroon insets, an overhanging top, a brass edge, a sound block on its left end
    P.append(box((0, 0.053, -0.040), (0.052, 0.038, 0.034), WOOD))
    P.append(box((0, 0.0735, -0.040), (0.057, 0.004, 0.038), WOOD))
    P.append(box((0, 0.0712, -0.0213), (0.057, 0.0012, 0.0012), BRASS))
    for s in (-1, 1):
        for z in (-0.049, -0.031):
            P.append(box((s * 0.0262, 0.052, z), (0.0008, 0.022, 0.012), MAROON))
    for x in (-0.013, 0.013):
        P.append(box((x, 0.052, -0.0228), (0.017, 0.022, 0.0008), MAROON))
    P.append(cylinder((GAVEL_AT[0], 0.0775, -0.040), 0.0062, 0.004, WOOD, n=16))
    # the empty chair: seat, cushion, the high back pierced by the round window (the rear sight)
    P.append(box((0, 0.042, -0.0655), (0.044, 0.016, 0.015), WOOD))
    P.append(box((0, 0.0515, -0.0655), (0.040, 0.003, 0.013), MAROON))
    back = [(-0.022, DECK1), (0.022, DECK1), (0.022, 0.112), (-0.022, 0.112)]
    hole = list(reversed(K.circle(0, SIGHT_Y, WIN_R, n=32)))
    P.append(slab([back, hole], WIN_Z0, WIN_Z1, WOOD))
    ring_out = K.circle(0, SIGHT_Y, 0.0168, n=32)
    P.append(slab([ring_out, list(reversed(K.circle(0, SIGHT_Y, WIN_R, n=32)))], WIN_Z0 - 0.0015, WIN_Z1 + 0.0015, BRASS))
    P.append(box((0, SIGHT_Y, (WIN_Z0 + WIN_Z1) / 2), (2 * WIN_R + 0.002, 0.0008, 0.003), BRASS))      # the bar (thin: the bead shows above and below it)
    # the chair back's rear face (what the player sees at the hip): two gothic lancet panels, a pointed-arch tracery
    # frame around the round window, brass rivets (fix round 1: it was a plain box)
    for x in (-0.0085, 0.0085):
        lancet, _ = K.pointed_arch(0.0055, 0.056, c_ratio=0.5, n=6)
        loop = [(x - 0.0055, 0.039)] + [(x + px, py) for (px, py) in lancet] + [(x + 0.0055, 0.039)]
        P.append(slab([_ccw(loop)], WIN_Z0 - 0.0009, WIN_Z0 + 0.0002, MAROON))
        frame, _ = K.pointed_arch(0.0068, 0.056, c_ratio=0.5, n=6)
        floop = [(x - 0.0068, 0.0375)] + [(x + px, py) for (px, py) in frame] + [(x + 0.0068, 0.0375)]
        P.append(slab([_ccw(floop)], WIN_Z0 - 0.0006, WIN_Z0 + 0.0002, IRON))
    P.append(box((0, 0.0375, WIN_Z0 - 0.0008), (0.034, 0.0018, 0.0012), BRASS))
    outer, _ = K.pointed_arch(0.0195, 0.082, c_ratio=0.5, n=10)
    inner, _ = K.pointed_arch(0.0172, 0.082, c_ratio=0.5 * 0.0195 / 0.0172, n=10)
    tracery = [(-0.0195, 0.07)] + outer + [(0.0195, 0.07), (0.0172, 0.07)] + list(reversed(inner)) + [(-0.0172, 0.07)]
    P.append(slab([_ccw(tracery)], WIN_Z0 - 0.0012, WIN_Z0 + 0.0002, IRON))
    for k in range(4):
        a = math.radians(45 + 90 * k)
        P.append(sphere((0.0152 * math.cos(a), SIGHT_Y + 0.0152 * math.sin(a), WIN_Z0 - 0.0016), 0.0011, BRASS, rings=4, segs=6))
    for s in (-1, 1):
        P.append(box((s * 0.025, (DECK1 + 0.122) / 2, -0.079), (0.006, 0.122 - DECK1, 0.0085), WOOD))
        P.append(box((s * 0.025, 0.1235, -0.079), (0.0075, 0.003, 0.0095), BRASS))
        P.append(frustum_box((s * 0.025, 0.1335, -0.079), (0.0058, 0.0058), (0.0004, 0.0004), 0.017, WOOD))   # a pinnacle
        for k in range(3):                                                                                   # its crockets
            P.append(box((s * 0.025, 0.128 + 0.004 * k, -0.079), (0.0062 - 0.0014 * k, 0.0012, 0.0012), BRASS, e=(0, 45, 0)))
        P.append(sphere((s * 0.025, 0.1425, -0.079), 0.0013, BRASS, rings=4, segs=6))
    # the crest: a cornice, a little pediment, a brass medallion
    P.append(box((0, 0.1145, -0.079), (0.056, 0.005, 0.016), WOOD))
    P.append(box((0, 0.1172, -0.079), (0.058, 0.0012, 0.0172), BRASS))
    gable = [(-0.021, 0.1175), (0.021, 0.1175), (0.0, 0.1365)]
    P.append(slab([gable], -0.083, -0.075, WOOD, uv=("box", 0.05)))
    for side in (-1, 1):                                                       # a brass edge up each slope of the gable
        P.append(box((side * 0.0105, 0.127, -0.0835), (0.024, 0.0014, 0.0012), BRASS, e=(0, 0, -side * 42.0)))
    P.append(cylinder((0, 0.1235, -0.0835), 0.0042, 0.0016, BRASS, axis="z", n=16))     # the court's medallion
    P.append(cylinder((0, 0.1235, -0.0846), 0.0026, 0.0008, IRON, axis="z", n=12))
    P.append(box((0, 0.1405, -0.079), (0.0016, 0.008, 0.0016), BRASS))               # a cross finial on the apex
    P.append(box((0, 0.1415, -0.079), (0.0055, 0.0014, 0.0014), BRASS))
    return P


def gavel_parts():
    """Around its own origin (the head's centre): the head along z with two brass bands, the handle out to the right."""
    P = [
        cylinder((0, 0, 0), 0.0058, 0.024, WOOD, axis="z", n=16),
        cylinder((0, 0, 0.0085), 0.0062, 0.0022, BRASS, axis="z", n=16),
        cylinder((0, 0, -0.0085), 0.0062, 0.0022, BRASS, axis="z", n=16),
    ]
    d = K.norm((-0.78, 0.22, 0.58))
    handle = cylinder((0, 0.016, 0), 0.0017, 0.032, WOOD, axis="y", n=10)
    # rotate the +y handle onto d, then shift it out of the head
    yaw = math.degrees(math.atan2(d[0], d[2]))
    pitch = -math.degrees(math.asin(d[1]))
    handle = handle.rotated((90 + pitch, yaw, 0))
    P.append(handle)
    P.append(sphere(K.scale(d, 0.034), 0.0026, WOOD))
    return P


def door_leaf(side: int):
    """One leaf of the doorway (left side -1, right +1), built in place; its hinge is at x = side * 0.0155."""
    in_curve, _ = K.pointed_arch(0.0155, 0.032, c_ratio=0.5)
    half = [p for p in in_curve if p[0] * side >= -1e-6]
    if side < 0:
        outline = [(0.0, BOTTOM + 0.006), (-0.0155, BOTTOM + 0.006)] + half
    else:
        outline = [(0.0155, BOTTOM + 0.006), (0.0, BOTTOM + 0.006)] + half
    outline = [(side * 0.0004 if abs(x) < 1e-6 else x, y) for (x, y) in outline]
    leaf = slab([_ccw(outline)], 0.1852, 0.1876, WOOD, uv=("box", 0.03))
    bands = [box((side * 0.0078, y, 0.1879), (0.0145, 0.0016, 0.0006), IRON) for y in (-0.016, 0.018)]
    ring = cylinder((side * 0.003, 0.004, 0.1884), 0.0018, 0.0008, BRASS, axis="z", n=10)
    return [leaf] + bands + [ring]


def dir_euler(d):
    """The Unity Euler angles (x, y, 0) that turn +Y onto the direction d."""
    d = K.norm(d)
    tilt = math.degrees(math.atan2(math.hypot(d[0], d[2]), d[1]))
    yaw = math.degrees(math.atan2(-d[0], -d[2])) if math.hypot(d[0], d[2]) > 1e-9 else 0.0
    return (-tilt, yaw, 0.0)


def _arch_loop(cx, a, ys, bottom, c_abs):
    """A pointed arch's outline standing on `bottom` (springing at ys), centred on x = cx, with the arch's centre offset fixed."""
    curve, apex = K.pointed_arch(a, ys, c_ratio=c_abs / a)
    return [(cx - a, bottom)] + [(cx + x, y) for (x, y) in curve] + [(cx + a, bottom)], apex


def inkwell_parts():
    """The back's alcove and jar (static): a stone shelf from the receiver's bottom, a pointed-arch alcove of masonry
    against its rear face (a stone frame proud of it) and, standing on the shelf, a brass-framed glass jar with a brass
    rim. Under the deck (0.026) and well below the sight line (0.088)."""
    P = []
    x, z = WELL_X, WELL_Z
    a_out, a_in, c = 0.0165, 0.0128, 0.00825
    # the shelf, and the arched backing against the receiver's rear face (z -0.080) with a stone frame in front of it
    P.append(box((x, (SHELF_Y0 + SHELF_Y1) / 2, (-0.0763 + -0.1108) / 2), (0.036, SHELF_Y1 - SHELF_Y0, 0.0345), STONE_DARK))
    outer, _ = _arch_loop(x, a_out, -0.002, SHELF_Y1, c)
    P.append(slab([outer], -0.0875, -0.0798, STONE))
    inner, _ = _arch_loop(x, a_in, -0.002, SHELF_Y1 + 0.0045, c)
    P.append(slab([outer, list(reversed(inner))], -0.0888, -0.0875, STONE_LIGHT))
    # the jar: brass base rings, four brass ribs, the glass body and shoulder, a brass rim with an opening
    P.append(cylinder((x, SHELF_Y1 + 0.0021, z), 0.0128, 0.0042, BRASS, n=24))
    P.append(cylinder((x, JAR_Y0 + 0.0006, z), 0.0122, 0.0012, BRASS, n=24))
    for k in range(4):
        ang = math.radians(45 + 90 * k)
        P.append(box((x + 0.0118 * math.cos(ang), (JAR_Y0 + 0.0052) / 2, z + 0.0118 * math.sin(ang)),
                     (0.0016, 0.0052 - JAR_Y0, 0.0016), BRASS))
    P.append(cylinder((x, (JAR_Y0 + INK_Y1) / 2, z), GLASS_R, INK_Y1 - JAR_Y0, GLASS, n=24, caps=False))
    P.append(cylinder((x, 0.0025, z), GLASS_R, 0.005, GLASS, n=24, r_end=0.0082, caps=False))
    for (r0, r1, y0, y1) in ((0.0112, 0.0076, 0.0050, 0.0072), (0.0098, 0.0076, 0.0072, 0.0092)):
        P.append(slab([K.circle(x, z, r0, n=24), list(reversed(K.circle(x, z, r1, n=24)))], y0, y1, BRASS, plane="xz"))
    return P


def ink_parts():
    """The ink: a glowing indigo cylinder from the jar's floor to the full level (the object's pivot is its bottom)."""
    return [cylinder((WELL_X, (JAR_Y0 + INK_Y1) / 2, WELL_Z), INK_R, INK_Y1 - JAR_Y0, INK, n=24)]


def quill_parts():
    """Built about the nib / mouth pivot with the feather along +Y, then turned to the hip lean: a cream shaft, a dark iron
    nib below the pivot (dipped in the ink), and a feather vane of two tilted halves with a pointed tip."""
    right = [(0.0004, 0.020), (0.0036, 0.030), (0.0060, 0.045), (0.0066, 0.058), (0.0046, 0.070), (0.0014, 0.0785), (0.0004, FEATHER_LEN)]
    left = [(-px, py) for (px, py) in reversed(right)]
    parts = [
        cylinder((0, 0.0365, 0), 0.0007, 0.0845, FEATHER, n=8, r_end=0.0004),
        frustum_box((0, -0.0035, 0), (0.0006, 0.0006), (0.0016, 0.0016), 0.007, IRON, e=(0, 45, 0)),
        K.Part(*_vane(right, 12.0)), K.Part(*_vane(left, -12.0)),
    ]
    e = dir_euler(QUILL_HIP_DIR)
    return [p.rotated(e).at(QUILL_PIVOT) for p in parts]


def _vane(outline, tilt):
    """(verts, faces, material) of one thin vane half in the XY plane, its inner edge on the shaft, tipped `tilt` degrees about Y."""
    sl = slab([_ccw(outline)], -0.0003, 0.0003, FEATHER)
    sl = sl.rotated((0, tilt, 0))
    return sl.verts, sl.faces, sl.mat


def _ccw(loop):
    area = sum(loop[i][0] * loop[(i + 1) % len(loop)][1] - loop[(i + 1) % len(loop)][0] * loop[i][1] for i in range(len(loop)))
    return loop if area > 0 else list(reversed(loop))


def build(repo: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex = repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "writ"
    mats = materials(tex)
    K.to_object("Body", body_parts(), mats, bevel=0.0005)
    K.to_object("Bench", bench_parts(), mats, origin=(0, 0.07, -0.06), bevel=0.0005)
    gavel = [p.rotated((-10, 20, 0)).at(GAVEL_AT) for p in gavel_parts()]      # hovering, its head tipped back
    K.to_object("Gavel", gavel, mats, origin=GAVEL_AT)
    for side, name in ((-1, "DoorL"), (1, "DoorR")):
        hinge = (side * 0.0155, 0.0, 0.1864)
        ajar = [p.rotated((0, side * 9.0, 0), pivot=hinge) for p in door_leaf(side)]   # ajar outward: a crack of light between them
        K.to_object(name, ajar, mats, origin=hinge)
    K.to_object("Mag", mag_parts(), mats, origin=grip_axis_point(0.08))
    K.to_object("Inkwell", inkwell_parts(), mats, bevel=0.0005)
    K.to_object("InkLevel", ink_parts(), mats, origin=(WELL_X, JAR_Y0, WELL_Z))
    K.to_object("Quill", quill_parts(), mats, origin=QUILL_PIVOT)
    K.empty("Muzzle", MUZZLE)
    K.empty("SightRear", (0.0, SIGHT_Y, (WIN_Z0 + WIN_Z1) / 2))
    K.empty("SightFront", (0.0, SIGHT_Y, BEAD_FACE_Z))


def main():
    repo, _ = K.repo_from_argv()
    build(repo)
    out = repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "writ" / "writ.fbx"
    K.export_fbx(out, "WRIT_MODEL")


if __name__ == "__main__":
    main()
