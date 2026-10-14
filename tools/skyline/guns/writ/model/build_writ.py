"""The Writ's viewmodel (Task 18), built from code in Blender and exported as FBX for Unity:

    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender.exe> -b --factory-startup \
        -P tools/skyline/guns/writ/model/build_writ.py -- <repo>

Writes <repo>/Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/writ/writ.fbx (SkylineViewmodel loads
Resources/SkylineModels/writ/writ). The creator's design (docs/SKYLINE-CONCEPTS.md, "The Writ: final design"): a gothic
courtroom in miniature grown into a pistol silhouette. A fluted stone column wrapped in a parchment scroll is the grip;
a stone colonnade with small gaslamps and a brass plate is the body; the great pointed doorway at the front, glowing
within, is the muzzle; the judge's bench (a plain table, with a sheet of paper on it) sits at the rear. The sights
(the creator, 2026-10-14: "the ADS sight picture is terrible on the Writ, the cross completely blocks it"): a round
brass window on a short stem (rear) and an amber bead on a short brass post on the arch's keystone (front). There is
NOTHING else in the picture: the old brass bar across the window and the tall obelisk behind the bead made a cross over
the target and are gone. The high-backed chair and the floating gavel are gone too (the creator: remove them entirely).

Top-level objects (every part top-level, so each child's local position in Unity is its place in the gun's frame):
  Body       the static gun (stone, wood, brass, lamps, the doorway's glow, the front sight, the rear sight ring on its
             stem, the grip's drum and plinth with the cell's amber band, the bench and the sheet of paper on it)
  DoorL/R    the doorway's two leaves, origins on their hinges (SkylineWritFx throws them open on a shot)
  Inkwell    the back (the creator's pick, 2026-10-14: "A take 2: inkwell and quill"): a stone alcove and shelf set into
             the receiver's rear face, a brass-framed glass jar (the glass is a faint transparent look set in SkylineWritFx)
             with a brass rim; static
  InkLevel   the glowing indigo ink inside the jar, a separate object with its pivot at the bottom of the well: SkylineWritFx
             scales its height to the magazine (the ink is the ammo). Carries the ink's gleam: a bright streak down its
             front and a sheen ring on its surface
  Quill, Quill1, Quill2, Quill3
             the quill, a chain of four segments (the creator: "make the quill larger ... it curl around and extend").
             Quill is the nib and the bare shaft, pivot at the nib / the inkwell's mouth; each next segment has its pivot at
             the end of the one before. Modelled in the hip pose (a gentle curl, leaning back and out to the right, away
             from the sight line); SkylineWritFx bends the chain with WritQuill.Pose (curls and extends in ADS, flicks on a
             shot, goes to the paper and writes in a reload). Its constants are mirrored in Runtime/Skyline/WritQuill.cs:
             change them together
  WritInk00..WritInk29
             the reload's handwriting on the paper: 3 strokes of 10 short ink segments, shown one by one as the nib passes
             (SkylineWritFx switches them on; hidden at rest)
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
REAR_Z, FRONT_Z = -0.085, 0.212       # the rear sight ring's rear face, the arch's front face
BOTTOM, DECK0, DECK1 = -0.036, 0.026, 0.034
RCV_Z0, COL_Z0, COL_Z1, ARCH_Z0 = -0.080, 0.028, 0.155, 0.155
WIN_Z0, WIN_Z1 = -0.085, -0.073      # the chair back's thickness
WIN_R = 0.0135
BEAD_Z = 0.205                        # the obelisk's axis
BEAD_FACE_Z = 0.2020                  # the bead's centre: on the obelisk's rear face, so its whole disc shows from behind
GRIP_TOP = (0.0, -0.035, -0.052)     # on the Breacher's grip axis (top (0,-0.02,-0.0475), bottom (0,-0.125,-0.0775))
GRIP_RAKE = 16.0                      # degrees: the column's bottom swings back
MUZZLE = (0.0, 0.012, 0.214)
BENCH_TOP = 0.0665                    # the table's top: well under the sight line (0.088), outside the window's cone from the eye

# the back's inkwell and quill (WritQuill.cs mirrors these): the well stands on a shelf under the table, right of centre
WELL_X, WELL_Z = 0.012, -0.100
SHELF_Y0, SHELF_Y1 = -0.036, -0.026                   # the shelf's underside (the receiver's bottom) and top
JAR_Y0, INK_Y1 = -0.0217, 0.0                         # the jar's floor and the full ink level
INK_R, GLASS_R = 0.0102, 0.0113
QUILL_PIVOT = (WELL_X, 0.002, WELL_Z)                 # the nib / the well's mouth
# the quill is a chain of SEG_N segments of SEG_LEN each (0.122 m, half as long again as the old 0.0805 single piece), bent
# about one axis (the cross of its direction and the "toward" vector) by QUILL_HIP_CURL degrees in all, spread evenly over
# the joints: segment i points at the base direction turned by CURL * i / (SEG_N - 1). At the hip it leans back and out to the
# right, away from the sight line, and curls outward; WritQuill.cs bends the same chain for ADS and the reload
SEG_N, SEG_LEN = 4, 0.0305
NIB_BELOW = 0.0105                                    # the nib's length below the pivot (dipped in the ink at rest)
QUILL_HIP_DIR = (0.30, 0.82, -0.48)
QUILL_HIP_TOWARD = (0.8, 0.05, -0.6)
QUILL_HIP_CURL = 45.0
# the feather's vane: half-width against the distance along the feather from the nib's pivot (it starts where the bare shaft ends)
VANE = [(0.0305, 0.0006), (0.045, 0.0054), (0.0675, 0.0090), (0.087, 0.0099), (0.105, 0.0069), (0.118, 0.0021), (0.122, 0.0006)]

# the sheet of paper on the table and the handwriting the reload puts on it (WritQuill.cs mirrors these): three strokes
# along the page (u across, v away from the player, in the paper's own frame, which is turned PAPER_YAW degrees), each a wave
# of STROKE_AMP and STROKE_WAVES waves, made of INK_SEGMENTS short pieces
PAPER_X, PAPER_Z, PAPER_YAW, PAPER_SURFACE = 0.002, -0.036, 8.0, 0.0671
PAPER_SIZE = (0.034, 0.026)
STROKE_U0, STROKE_U1 = -0.0125, 0.0115
STROKE_V = (0.0075, 0.0, -0.0075)
STROKE_AMP, STROKE_WAVES = 0.0012, 3.5
STROKE_COUNT, INK_SEGMENTS = 3, 10
INK_WIDTH, INK_THICK = 0.0007, 0.0003

STONE, STONE_DARK, WOOD, MAROON, BRASS, IRON, PARCH, LAMP, DOOR_GLOW, BEAD, CELL, STONE_LIGHT, INK, FEATHER, GLASS, PAPER, GLEAM = range(17)


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
        # the ink: a DEEP indigo with a glow, not a pale blue-violet (the creator: "we lost the brilliant gleam in the indigo
        # ink, and it turned more of a lilac"); the Unity look is set in SkylineWritFx.Looks, keep the two together
        m("Writ_Ink", (0.1, 0.03, 0.5), rough=0.2, emit=(0.25, 0.08, 0.9), strength=2.0),
        m("Writ_Feather", (0.94, 0.94, 0.97), rough=0.8),
        m("Writ_Glass", (0.45, 0.4, 1.0), rough=0.1),
        m("Writ_Paper", (0.93, 0.89, 0.78), rough=0.9),
        m("Writ_InkGleam", (0.85, 0.82, 1.0), rough=0.1, emit=(0.85, 0.8, 1.0), strength=6.0),
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
    P += mag_parts()           # the grip's drum is part of the gun now: nothing drops out of it in a reload
    P += table_parts()
    P += rear_sight_parts()
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
    # the front sight: an amber bead on a short brass post on a plinth. The old tall obelisk behind the bead (and the brass
    # bar across the rear window) made a cross over the target ("the cross completely blocks it"): nothing rises above the
    # bead now, so the sight picture is a round window with one glowing dot in it
    P.append(box((0, 0.0805, BEAD_Z), (0.0084, 0.004, 0.0084), BRASS))
    P.append(frustum_box((0, (0.0825 + 0.0865) / 2, BEAD_Z), (0.0044, 0.0044), (0.0026, 0.0026), 0.0865 - 0.0825, BRASS))
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


def table_parts():
    """The judge's bench, now a plain table with a sheet of paper on it (the creator, 2026-10-14: the high-back chair and the
    gavel are gone; the reload writes on this paper). Its top (BENCH_TOP) is well under the sight line, so from the aimed eye
    it stays outside the window's cone."""
    P = []
    h0, h1 = DECK1, BENCH_TOP - 0.004
    mid = (h0 + h1) / 2
    P.append(box((0, mid, -0.040), (0.052, h1 - h0, 0.034), WOOD))
    P.append(box((0, BENCH_TOP - 0.002, -0.040), (0.057, 0.004, 0.038), WOOD))
    P.append(box((0, BENCH_TOP - 0.0043, -0.0213), (0.057, 0.0012, 0.0012), BRASS))
    for s in (-1, 1):
        for z in (-0.049, -0.031):
            P.append(box((s * 0.0262, mid, z), (0.0008, h1 - h0 - 0.008, 0.012), MAROON))
    for x in (-0.013, 0.013):
        P.append(box((x, mid, -0.0228), (0.017, h1 - h0 - 0.008, 0.0008), MAROON))
    # two sheets of paper, the top one square to the stroke frame (the handwriting lies on it)
    P.append(box((PAPER_X + 0.0006, BENCH_TOP + 0.0002, PAPER_Z - 0.0004), (PAPER_SIZE[0], 0.0004, PAPER_SIZE[1]), PAPER, e=(0, PAPER_YAW - 7.0, 0)))
    P.append(box((PAPER_X, PAPER_SURFACE - 0.0003, PAPER_Z), (PAPER_SIZE[0], 0.0006, PAPER_SIZE[1]), PAPER, e=(0, PAPER_YAW, 0)))
    return P


def rear_sight_parts():
    """The rear sight: a round brass window on a short stem standing on the deck (the old chair back it was cut through is
    gone, and so is the brass bar across it). Nothing crosses the opening; the stem is under the ring, outside the picture."""
    P = []
    zc = (WIN_Z0 + WIN_Z1) / 2
    hole = list(reversed(K.circle(0, SIGHT_Y, WIN_R, n=32)))
    P.append(slab([K.circle(0, SIGHT_Y, 0.0168, n=32), hole], WIN_Z0 - 0.0015, WIN_Z1 + 0.0015, BRASS))
    for k in range(4):                                                   # four rivets on the ring's rear face
        a = math.radians(45 + 90 * k)
        P.append(sphere((0.0152 * math.cos(a), SIGHT_Y + 0.0152 * math.sin(a), WIN_Z0 - 0.0016), 0.0011, BRASS, rings=4, segs=6))
    ring_bottom = SIGHT_Y - 0.0168 + 0.0004
    P.append(box((0, (DECK1 + ring_bottom) / 2, zc), (0.0046, ring_bottom - DECK1, 0.0046), BRASS))
    P.append(box((0, DECK1 + 0.0025, zc), (0.014, 0.005, 0.012), STONE_DARK))
    P.append(box((0, DECK1 + 0.0056, zc), (0.0085, 0.0014, 0.0085), BRASS))
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


GLEAM_DIR = K.norm((-0.38, 0.0, -0.93))              # the side of the jar that faces the player (behind, a little left)


def ink_parts():
    """The ink: a glowing deep-indigo cylinder from the jar's floor to the full level (the object's pivot is its bottom), with
    its gleam (the creator: "we lost the brilliant gleam in the indigo ink"): a bright streak down the side that faces the
    player, a short second glint under it, and a sheen ring on the surface. The pieces scale with the level, so the streak
    always runs the height of the ink and the sheen rides on its surface."""
    mid = (JAR_Y0 + INK_Y1) / 2
    r = INK_R + 0.0004
    gx, gz = GLEAM_DIR[0], GLEAM_DIR[2]
    yaw = math.degrees(math.atan2(gx, gz))
    P = [cylinder((WELL_X, mid, WELL_Z), INK_R, INK_Y1 - JAR_Y0, INK, n=24)]
    P.append(box((WELL_X + gx * r, mid + 0.0004, WELL_Z + gz * r), (0.0012, INK_Y1 - JAR_Y0 - 0.0045, 0.0006), GLEAM, e=(0, yaw, 0)))
    P.append(box((WELL_X + gx * r, JAR_Y0 + 0.0020, WELL_Z + gz * r), (0.0012, 0.0012, 0.0006), GLEAM, e=(0, yaw, 0)))
    P.append(slab([K.circle(WELL_X, WELL_Z, 0.0094, n=24), list(reversed(K.circle(WELL_X, WELL_Z, 0.0066, n=24)))],
                  INK_Y1 - 0.0002, INK_Y1 + 0.0003, GLEAM, plane="xz"))
    return P


# ---------------------------------------------------------------- the quill chain


def rot_axis(v, axis, deg):
    """v turned about the unit `axis` by `deg` degrees (Rodrigues; the same algebra as System.Numerics in WritQuill.cs)."""
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    k = K.norm(axis)
    return K.add(K.add(K.scale(v, c), K.scale(K.cross(k, v), s)), K.scale(k, K.dot(k, v) * (1 - c)))


def chain_frames(base, d, toward, curl, n=SEG_N, seg_len=SEG_LEN):
    """The quill chain for a pose (the maths of WritQuill.Joints): the joints (n + 1 points) and each segment's frame (x the
    bend axis, y along the segment, z = x cross y). Segment i points at d turned about the bend axis by curl * i / (n - 1)."""
    axis = K.norm(K.cross(d, toward))
    joints, frames = [base], []
    p = base
    for i in range(n):
        di = rot_axis(d, axis, curl * i / (n - 1))
        frames.append((axis, di, K.cross(axis, di)))
        p = K.add(p, K.scale(di, seg_len))
        joints.append(p)
    return joints, frames


def vane_w(y):
    if y <= VANE[0][0]:
        return VANE[0][1]
    if y >= VANE[-1][0]:
        return VANE[-1][1]
    for (y0, w0), (y1, w1) in zip(VANE, VANE[1:]):
        if y0 <= y <= y1:
            return w0 + (w1 - w0) * (y - y0) / (y1 - y0)
    return 0.0


def _vane(outline, tilt):
    """(verts, faces, material index) of one thin vane half in the XY plane, tipped `tilt` degrees about Y."""
    sl = slab([_ccw(outline)], -0.0003, 0.0003, FEATHER)
    sl = sl.rotated((0, tilt, 0))
    return sl.verts, sl.faces, sl.mat


def quill_segment(i):
    """Segment i in its own frame (y along the segment, its pivot at the origin): the shaft with a ball at its far end; the
    first also has the iron nib below the pivot; the others carry the feather's vane (two tilted halves), overlapping the next
    segment's by a hair so a bend leaves no gap."""
    total = SEG_N * SEG_LEN
    y0 = i * SEG_LEN
    r_a, r_b = 0.0010 - 0.0004 * y0 / total, 0.0010 - 0.0004 * (y0 + SEG_LEN) / total
    parts = [cylinder((0, SEG_LEN / 2, 0), r_a, SEG_LEN + 0.0008, FEATHER, n=8, r_end=r_b)]
    if i < SEG_N - 1:
        parts.append(sphere((0, SEG_LEN, 0), r_b * 1.25, FEATHER, rings=4, segs=8))
    if i == 0:
        parts.append(frustum_box((0, -NIB_BELOW / 2, 0), (0.0006, 0.0006), (0.0024, 0.0024), NIB_BELOW, IRON, e=(0, 45, 0)))
    ya = max(VANE[0][0], y0 - (0.0004 if i > 0 else 0.0))
    yb = min(VANE[-1][0], y0 + SEG_LEN + (0.0004 if i < SEG_N - 1 else 0.0))
    if yb > ya + 1e-6:
        ys = [ya] + [y for (y, _) in VANE if ya < y < yb] + [yb]
        edge = [(vane_w(y), y - y0) for y in ys]
        right = [(0.0004, ya - y0)] + edge + [(0.0004, yb - y0)]
        left = [(-x, y) for (x, y) in reversed(right)]
        parts.append(K.Part(*_vane(right, 12.0)))
        parts.append(K.Part(*_vane(left, -12.0)))
    return parts


def quill_objects():
    """[(name, parts, joint)]: the four segments in the hip pose, each placed in the gun's frame, origin at its joint."""
    joints, frames = chain_frames(QUILL_PIVOT, K.norm(QUILL_HIP_DIR), K.norm(QUILL_HIP_TOWARD), QUILL_HIP_CURL)
    out = []
    for i in range(SEG_N):
        ax, ay, az = frames[i]
        j = joints[i]

        def place(v, j=j, ax=ax, ay=ay, az=az):
            return K.add(j, K.add(K.add(K.scale(ax, v[0]), K.scale(ay, v[1])), K.scale(az, v[2])))

        out.append(("Quill" if i == 0 else "Quill%d" % i, [p.moved(place) for p in quill_segment(i)], j))
    return out


# ---------------------------------------------------------------- the paper and its handwriting


def paper_point(u, v):
    """A point on the paper (the paper's own frame turned PAPER_YAW degrees about y): u across the page, v away from the player."""
    return K.add((PAPER_X, PAPER_SURFACE, PAPER_Z), K.ry((u, 0.0, v), PAPER_YAW))


def stroke_point(k, t):
    """Where stroke k (0..STROKE_COUNT-1) is at t (0..1) along its length: a left-to-right wave, the lines running down the page."""
    u = STROKE_U0 + (STROKE_U1 - STROKE_U0) * t
    v = STROKE_V[k] + STROKE_AMP * math.sin(2 * math.pi * STROKE_WAVES * t)
    return paper_point(u, v)


def ink_segment_objects():
    """[(name, parts, centre)]: the handwriting's 30 pieces, each a thin flat box lying on the paper."""
    out = []
    for k in range(STROKE_COUNT):
        for j in range(INK_SEGMENTS):
            p0, p1 = stroke_point(k, j / INK_SEGMENTS), stroke_point(k, (j + 1) / INK_SEGMENTS)
            d = K.sub(p1, p0)
            length = math.hypot(d[0], d[2]) + 0.0004
            yaw = math.degrees(math.atan2(d[0], d[2]))
            c = K.scale(K.add(p0, p1), 0.5)
            c = (c[0], PAPER_SURFACE + INK_THICK / 2, c[2])
            out.append(("WritInk%02d" % (k * INK_SEGMENTS + j), [box(c, (INK_WIDTH, INK_THICK, length), INK, e=(0, yaw, 0))], c))
    return out


def _ccw(loop):
    area = sum(loop[i][0] * loop[(i + 1) % len(loop)][1] - loop[(i + 1) % len(loop)][0] * loop[i][1] for i in range(len(loop)))
    return loop if area > 0 else list(reversed(loop))


# ---------------------------------------------------------------- the sight picture, checked at export time


def sight_check(objects):
    """The creator's call: "the ADS sight picture is terrible, the cross completely blocks it". With the eye on the sight line,
    13 cm behind the window, lists every object that has a vertex, an edge midpoint or a face centre inside the window's
    opening cone between the window and the gatehouse (the bead and the gatehouse behind it are the picture; anything nearer
    is in the way). Prints SIGHT_CLEAR when nothing is."""
    eye = (0.0, SIGHT_Y, (WIN_Z0 + WIN_Z1) / 2 - 0.13)
    # the clear opening is 6.2 degrees across its radius; things that only touch its very rim (the front newel posts' finials,
    # 0.5 degrees in) frame the picture rather than cross it
    opening = math.degrees(math.atan2(WIN_R, WIN_Z0 - 0.0015 - eye[2])) - 0.5
    blockers = {}
    for name, parts in objects.items():
        for part in parts:
            pts = list(part.verts)
            for f in part.faces:
                vs = [part.verts[i] for i in f]
                pts.append(K.scale(tuple(map(sum, zip(*vs))), 1.0 / len(vs)))
                pts += [K.scale(K.add(vs[i], vs[(i + 1) % len(vs)]), 0.5) for i in range(len(vs))]
            for p in pts:
                if not (WIN_Z1 + 0.002 < p[2] < ARCH_Z0 - 0.003):      # the gatehouse and the bead's post are the picture's background
                    continue
                v = K.sub(p, eye)
                off = math.degrees(math.atan2(math.hypot(v[0], v[1]), v[2]))
                if off < opening:
                    blockers.setdefault(name, []).append((round(off, 2), tuple(round(c, 4) for c in p)))
    if not blockers:
        print("WRIT_MODEL SIGHT_CLEAR: nothing between the window and the gatehouse is inside its %.1f degree opening" % opening)
    for name, hits in blockers.items():
        hits.sort()
        print("WRIT_MODEL SIGHT_BLOCKED %s: %d points inside the %.1f degree opening, nearest the line: %s" % (name, len(hits), opening, hits[:4]))
    return blockers


def build(repo: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex = repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "writ"
    mats = materials(tex)
    objects = {"Body": body_parts(), "Inkwell": inkwell_parts(), "InkLevel": ink_parts()}
    quill = quill_objects()
    ink = ink_segment_objects()
    K.to_object("Body", objects["Body"], mats, bevel=0.0005)
    for side, name in ((-1, "DoorL"), (1, "DoorR")):
        hinge = (side * 0.0155, 0.0, 0.1864)
        ajar = [p.rotated((0, side * 9.0, 0), pivot=hinge) for p in door_leaf(side)]   # ajar outward: a crack of light between them
        K.to_object(name, ajar, mats, origin=hinge)
    K.to_object("Inkwell", objects["Inkwell"], mats, bevel=0.0005)
    K.to_object("InkLevel", objects["InkLevel"], mats, origin=(WELL_X, JAR_Y0, WELL_Z))
    for name, parts, joint in quill:
        K.to_object(name, parts, mats, origin=joint)
    for name, parts, centre in ink:
        K.to_object(name, parts, mats, origin=centre)
    K.empty("Muzzle", MUZZLE)
    K.empty("SightRear", (0.0, SIGHT_Y, (WIN_Z0 + WIN_Z1) / 2))
    K.empty("SightFront", (0.0, SIGHT_Y, BEAD_FACE_Z))
    sight_check({"Body": objects["Body"], "Inkwell": objects["Inkwell"], "InkLevel": objects["InkLevel"],
                 "Quill (hip pose)": [p for _, ps, _ in quill for p in ps], "Handwriting": [p for _, ps, _ in ink for p in ps]})


def main():
    repo, _ = K.repo_from_argv()
    build(repo)
    out = repo / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineModels" / "writ" / "writ.fbx"
    K.export_fbx(out, "WRIT_MODEL")


if __name__ == "__main__":
    main()
