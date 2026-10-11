# The title screen made from the creator's sketch (2026-10-12): Nadia's table at night, seen from above, the tablet's
# screen left plain for the menu. Writes Flow prompts into docs/art/prompts/title-sketch/.
#   uv run python tools/art/vh_title_sketch.py
# Attach, in order: Image 1 the sketch (FableAnimationTests/art/review/2026-10-12/title-screen/creator-sketch.png),
# Image 2 the opening's approved bedroom frame (op-bedroom-pillow-take3.jpg: the drawing style),
# Image 3 Nadia's badge photo (op-badge-photo-take1.jpg).
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "title-sketch")
os.makedirs(OUT, exist_ok=True)

HEAD = "Aspect ratio: 16:9 landscape (wide), exactly like Image 1.\n\n"
NOTEXT = ("No text, no letters, no numbers, no logos, no watermark, no signature anywhere in the image (the sketch's handwritten "
          "labels are only labels: do NOT draw them).")

LAYOUT = (
    "Image 1 is the creator's layout sketch for the game's title screen. Copy its COMPOSITION EXACTLY: the same objects in "
    "the same places, at the same sizes and angles, cropped by the frame edges in the same way. Do not recompose, do not "
    "centre anything, do not add margins, do not zoom out. The camera looks down at a wooden table from high above (a steep, "
    "almost top-down view), as in the sketch.\n"
    "The objects, as placed in Image 1:\n"
    "1. A LARGE TABLET lying flat on the table, slightly rotated: it fills the upper-left two-thirds of the frame and runs off "
    "the LEFT edge and the TOP edge; its top edge rises gently from the left towards the right, its right edge is a near-vertical "
    "line about 70% across the frame (from the top edge down to about 60% of the height), and its bottom edge slopes up from "
    "the lower left (about 87% down at the left edge) to that corner. Its SCREEN IS OFF: plain, empty dark glass with only a "
    "soft faint reflection of the lamp, nothing displayed on it, a thin dark bezel around it.\n"
    "2. A COFFEE MUG at the upper right, seen from above at the same angle as in the sketch (its round rim near the top, its "
    "body below), its handle pointing to the right and CUT OFF by the right edge of the frame.\n"
    "3. A small NOTEPAD at the bottom centre, tilted slightly, CUT OFF by the bottom edge of the frame, with a binding strip "
    "across its top as in the sketch.\n"
    "4. A hospital ID BADGE at the bottom right, a portrait card in a clear holder, CUT OFF by the bottom and right edges; a "
    "lanyard clip at its top left, its strap curling up and to the left and ending in a small loop, exactly as in the sketch.")

STYLE = (
    "Image 2 shows the drawing style to match (the game's opening, the same night): its palette, its clean thin line work, "
    "its soft flat shading and its quiet mood. Do not copy its content.\n"
    "Style: the waking world of a narrative game: grounded but stylised, muted palette, simplified clean shapes with soft flat "
    "shading and restrained thin line work, quiet and calm, like a literary graphic novel. NOT gritty, NOT photographic, no "
    "grime, no film grain, no heavy texture.")

MOOD = (
    "The moment: late at night, after a long hospital shift: a nurse's things left on her kitchen table. Night: the room is "
    "dark and cool blue, and one warm lamp off-frame to the upper right throws a soft warm pool of light across the table and "
    "the mug, falling off into blue shadow towards the edges. Quiet, tired, still.")

DETAILS = (
    "Small true details, kept subtle so the picture stays quiet and readable behind a menu: a faint dried coffee ring on the "
    "wood near the mug, the coffee in the mug half drunk; a ballpoint pen lying across the notepad; a few light scuffs and "
    "worn grain on the table; the badge's photo is the woman in Image 3 (small, head and shoulders), the rest of the card plain "
    "with blank coloured bands; the lanyard plain dark navy fabric.")

P = {}
def put(name, *parts): P[name] = HEAD + "\n\n".join(p for p in parts if p) + "\n\n" + NOTEXT + "\n"

put("ts-title-a", LAYOUT, STYLE, MOOD, DETAILS)
# Round 2 variants (written now so each take's prompt is on record): a stronger hold on the layout, and a calmer table.
put("ts-title-b", LAYOUT,
    "MOST IMPORTANT: the placement. Trace Image 1: the tablet's outline must be where the sketch's tablet is (filling the "
    "upper-left two-thirds, running off the left and top edges), the mug where the sketch's mug is with its handle cut by the "
    "right edge, the notepad cut by the bottom edge at the bottom centre, the badge cut by the bottom-right corner.",
    STYLE, MOOD, DETAILS)
put("ts-title-c", LAYOUT, STYLE, MOOD,
    "Keep the table plain and calm: warm worn wood with soft grain, a faint coffee ring by the mug, a ballpoint pen on the "
    "notepad, nothing else added. The badge card is plain with a small photo of the woman in Image 3. The tablet's dark screen "
    "takes up a large calm area where a game's title and a Play button will be placed.")

# Round 2 (after round 1: the tablet came out level, never rotated as in the sketch, and the mug was drawn straight down):
# the geometry in numbers, and the mug and badge seen at the sketch's angle.
GEOM = (
    "THE GEOMETRY, measured on Image 1 (x from the left edge, y from the top edge, as fractions of the frame):\n"
    "- The tablet is ROTATED about 20 degrees anticlockwise (its right side higher than its left), not level. Its bottom "
    "edge is a long straight line from (x 0, y 0.87) climbing to (x 0.72, y 0.61). Its right edge runs from that corner up "
    "and slightly left to (x 0.62, y 0) where it leaves the top of the frame. Its top edge runs from (x 0, y 0.15) up to "
    "(x 0.47, y 0) and leaves the frame. Its top-left corner and its left end are outside the frame.\n"
    "- The mug stands at x 0.77 to 0.94, y 0.11 to 0.51; we see its body from slightly above and the side (the rim an ellipse "
    "at the top, the body below it), and its handle reaches out to the right and is cut by the right edge at y 0.2 to 0.44.\n"
    "- The notepad: its top edge from (x 0.41, y 0.79) to (x 0.62, y 0.87), its binding just below that edge, its sides going "
    "down and out of the bottom of the frame between x 0.35 and x 0.57.\n"
    "- The badge: a portrait card from (x 0.81, y 0.71) to (x 0.97, y 0.69) at its top, its sides going down out of the bottom "
    "of the frame and its right side out of the right edge; its clip at its top left; the lanyard strap rises from the clip "
    "in an arch up to y 0.6 and curls left and down to a small end at (x 0.76, y 0.76).")
put("ts-title-d", LAYOUT, GEOM,
    "Image 4 is an earlier attempt: keep its look (the drawing, the light, the table, the objects' details) but put everything "
    "where Image 1 and the geometry above say: above all, rotate the tablet.",
    STYLE, MOOD, DETAILS)

# Round 3: from the best round-2 take (ts-title-b-take4 as Image 4), its few misses fixed.
put("ts-title-e", LAYOUT,
    "Image 4 is the best attempt so far: keep it as it is (the drawing, the light, the table, the objects, their places), "
    "changing only these: (1) NO lamp anywhere in the frame (its warm light comes from off-frame, top right); (2) the mug's "
    "handle runs out of the RIGHT edge of the frame, cut off as in Image 1 (move the mug a little to the right); (3) the "
    "tablet's bottom-left corner is OUTSIDE the frame, the tablet running off the left edge as in Image 1.",
    STYLE, MOOD, DETAILS)

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(v)
print("wrote", len(P), "prompts to", os.path.normpath(OUT))
