# Character sheets for Visiting Hours in style E (2026-10-10): the waking world as clean 3D-modelling turnarounds and
# expression sheets, the dream as cutesy billboards. Designs follow docs/CHARACTERS.md.
# Writes Flow prompts into docs/art/prompts/sheets/.   uv run python tools/art/vh_sheets.py
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "sheets")
os.makedirs(OUT, exist_ok=True)
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
NOTEXT = "No text, no labels, no captions, no numbers, no letters, no logos, no watermark, no signature anywhere in the image."

REAL = ("Style: grounded adult stylised illustration for the waking world of a narrative game: a muted, desaturated, slightly "
        "cold palette, simplified but realistic proportions, clean shapes with soft flat shading, restrained thin line work, "
        "quiet and serious, like a literary graphic novel. Near-future city clothing, practical and a little worn; no "
        "cybernetics, no neon, no gadgets. Not cute, not a realistic painting, not 3D.")
DREAM = ("Style: the cutesy dream register: pastel crayon and doodle drawing, wobbly thick outlines, flat pastel colours with "
         "crayon texture, cute simplified proportions (big round head, small body, simple dot eyes, a tiny mouth), a cheerful "
         "surface with something faintly wrong underneath (too still, too perfect, the eyes a little too round and empty). "
         "Not realistic, not 3D.")
BG = "Background: completely plain flat light grey (#d8d8d8), no scenery, no floor, no cast shadows."
PAPER = "Background: completely plain flat pale off-white paper, no scenery, no floor, no cast shadows."

TURN = ("A character turnaround model sheet for 3D modelling: the same character drawn three times side by side at exactly the "
        "same scale and height on one shared ground line: a FRONT view, a SIDE view (true profile facing left) and a BACK view. "
        "Each is full body from the top of the head to the shoes, standing in a neutral A-pose (arms held straight and slightly "
        "away from the body at about thirty degrees, palms facing the thighs, feet a little apart), neutral expression, eyes "
        "forward. Orthographic: no perspective distortion, no foreshortening, every view the same height. Flat, even, "
        "shadowless lighting so the colours and shapes read clearly. Clean silhouettes with space between the three views.")
def EXPR(n, what):
    return (f"An expression sheet: {n} head-and-shoulders portraits of the same character in one row, at exactly the same scale, "
            f"the same framing and the same slight three-quarter angle, evenly spaced. From left to right: {what}. "
            "Clean, readable faces; the hair, clothes and colours identical in every portrait.")
BILL = ("A billboard figure for a 3D game: full body from head to shoes, standing and facing the viewer straight on, centred, "
        "with a small margin above and below and plenty of empty space either side.")

NADIA = ("The character: Nadia, a woman of twenty-nine, a night-shift nurse. Dark brown hair tied back badly in a messy low bun "
         "with loose strands, tired shadowed eyes, a guarded face. Navy blue hospital scrubs with a grey long-sleeved thermal top "
         "underneath, a worn dark olive city jacket over the scrubs (open, sleeves pushed up a little), a hospital ID badge on a "
         "dark lanyard around her neck (the badge plain and blank), white work sneakers.")
ELIAS = ("The character: Elias, a man of thirty-four, an emergency-room doctor from money. Sharp, lean and well-kept: short neat "
         "dark brown hair, a short well-groomed dark beard, intelligent impatient eyes. Expensive but practical clothes worn without "
         "care: a good charcoal wool overcoat, open, with a small coffee stain on one lapel; a white shirt with the sleeves rolled "
         "to the forearms and the collar open; a plain dark tie loosened; slim dark grey trousers; good dark brown leather shoes, "
         "scuffed. A steel wristwatch.")
LENA = ("The character: Lena, a woman in her early thirties, an artist, warm and quick to laugh. Long wavy chestnut hair loosely "
        "pinned up with a pencil through it, a few strands falling, freckles, bright kind eyes. An oversized rust-coloured linen "
        "shirt with rolled sleeves over a cream t-shirt, dark jeans flecked with paint, worn canvas sneakers, dried paint and clay "
        "on her hands and forearms.")
THEO = ("The character: Theo, a boy of ten. Messy light brown hair, a gap-toothed grin, freckles, curious eyes. A mustard-yellow "
        "hoodie far too big for him (sleeves past his hands, pushed up), faded blue jeans with a patched knee, scuffed red "
        "sneakers.")
MAGS = ("The character: Mags, a Black woman in her mid-fifties, a sleep-lab technician. Close-cropped natural grey hair, reading "
        "glasses with thin dark frames hanging on a beaded cord around her neck, tired but amused eyes, a dry half-smile. A long, "
        "soft, slightly pilled oatmeal-grey cardigan over pale teal scrubs, comfortable white sneakers, a plain white coffee mug "
        "always in one hand.")
WALT = ("The character: Walt, an old man in his mid-seventies, tall and thin with a slight stoop, a kind lined face, a neat white "
        "moustache, a brown tweed flat cap, a faded blue work shirt with rolled sleeves, a tan canvas apron with grease stains, "
        "dark grey trousers, worn brown boots.")

STYLE_REF = "Image 1 is the reference for the drawing style only (line, shading, palette, proportions): match it exactly."
P = {}
def put(name, *parts): P[name] = HEAD + "\n\n".join(p for p in parts if p) + "\n\n" + NOTEXT + "\n"

# -- phase 1: turnarounds (style reference attached) and Walt (his dream art attached) --
put("sheet-nadia-turn", REAL, "Image 1 shows this same woman: keep her face, hair and build, and add the jacket and lanyard "
    "described here. Match Image 1's drawing style exactly.", NADIA, TURN, BG)
put("sheet-elias-turn", REAL, STYLE_REF + " Do not draw the man from Image 1; draw only the character described.", ELIAS, TURN, BG)
put("sheet-lena-turn", REAL, STYLE_REF + " Do not draw the person from Image 1; draw only the character described.", LENA, TURN, BG)
put("sheet-theo-turn", REAL, STYLE_REF + " Do not draw the person from Image 1; draw only the character described.", THEO, TURN, BG)
put("sheet-mags-turn", REAL, STYLE_REF + " Do not draw the person from Image 1; draw only the character described.", MAGS, TURN, BG)
put("sheet-walt-bb", DREAM, "Image 1 is this same old man in this same style: keep him identical (face, cap, moustache, clothes, "
    "colours, proportions).", WALT, "Introduction pose: calmly wiping his hands on a red shop rag, a slight patient smile.", BILL, PAPER)
put("sheet-walt-expr", DREAM, "Image 1 is this same old man in this same style: keep him identical.", WALT,
    EXPR("three", "gentle (a soft patient smile), sad (eyes lowered, mouth turned down under the moustache), firm (a level, "
         "immovable stare, mouth a straight line)"), PAPER)

# -- phase 2: from each character's chosen turnaround (Image 1); dream art also gets a dream style reference (Image 2) --
CHAR_REF = "Image 1 is this character's model sheet: keep the same face, hair, build, clothes and colours exactly."
DREAM_REF = ("Image 1 is this character's waking-world model sheet: keep the same person (hair, clothes, colours). Image 2 shows "
             "the dream style: redraw the character completely in that style. Do not draw the person from Image 2.")
put("sheet-nadia-expr", REAL, CHAR_REF, NADIA, EXPR("five", "neutral, tired, guarded, holding back tears, a small real smile"), BG)
put("sheet-nadia-bb", DREAM, DREAM_REF, NADIA, "Introduction pose: she stands a little hunched, her phone held in one hand at "
    "her side, the other arm across her body holding that elbow, looking up at the viewer warily.", BILL, PAPER)
put("sheet-elias-expr", REAL, CHAR_REF, ELIAS, EXPR("four", "focused (narrowed eyes, reading something), an abrasive smirk, "
    "exhausted (heavy eyes, slack face after a long shift), a rare warmth (an unguarded gentle smile)"), BG)
put("sheet-elias-bb", DREAM, DREAM_REF, ELIAS, "Introduction pose: relaxed and easy, softer than he ever was awake: hands in "
    "his coat pockets, a wide untroubled smile, head tilted a little.", BILL, PAPER)
put("sheet-elias-bed", REAL, "Image 1 is this man's model sheet: the patient is this same man (face, hair, beard).",
    "The same man lying in a hospital bed in a coma, three-quarter view from the foot of the bed at standing eye height: a "
    "bandage wrapped around his head, a healing cut on one cheekbone, eyes closed, a clear nasal oxygen cannula, a pale blue "
    "hospital gown, an IV line taped to one hand, a pulse-oximeter clip on a finger, white sheets and a pale grey blanket. A "
    "grey adjustable hospital bed with rails and wheels; beside it a bedside monitor on a stand showing only simple glowing "
    "wave lines (no numbers) and an IV pole with a bag. Night, a cold lamp. The whole bed and monitor inside the frame with a "
    "margin; a reference for building the scene in 3D.", BG)
put("sheet-lena-bb", DREAM, DREAM_REF, LENA, "Slightly too perfect. Introduction pose: warm and welcoming, both hands clasped in "
    "front of her (paint on her fingers), a bright knowing smile, standing in the way as if at a doorway.", BILL, PAPER)
put("sheet-theo-bb", DREAM, DREAM_REF + " He is the most cutesy figure of all.", THEO, "Introduction pose: hands lost in his "
    "too-long hoodie sleeves, rocking up on his toes, looking up at the viewer with a gap-toothed grin.", BILL, PAPER)
put("sheet-mags-expr", REAL, CHAR_REF, MAGS, EXPR("three", "amused (a dry half-smile over the mug), evasive (glancing away, lips "
    "pressed), stern (glasses on, a flat hard look over them)"), BG)

# -- phase 3: dream expressions from the chosen dream billboards (Image 1) --
BB_REF = "Image 1 is this character's dream billboard: keep the same character and style exactly."
put("sheet-lena-expr", DREAM, BB_REF, LENA, EXPR("three", "warm (a bright open smile), knowing (a small sad smile, eyes "
    "half-lidded), defensive (chin up, brows down, mouth tight)"), PAPER)
put("sheet-theo-expr", DREAM, BB_REF, THEO, EXPR("three", "curious (head tilted, eyes wide), worried (brows up, biting his lip), "
    "grinning (a huge gap-toothed grin)"), PAPER)

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts")
