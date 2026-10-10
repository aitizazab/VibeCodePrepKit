# Style-E art for the opening and title scenes (docs/OPENING-AND-TITLE.md), and Lena's redo (blonde, long loose waves),
# 2026-10-10. Writes Flow prompts into docs/art/prompts/opening/.   uv run python tools/art/vh_opening.py
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "opening")
os.makedirs(OUT, exist_ok=True)
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
NOTEXT = "No text, no letters, no numbers, no logos, no watermark, no signature anywhere in the image."

REAL = ("Style: the waking world of a narrative game: grounded but stylised, muted and slightly cold palette, simplified clean "
        "shapes with soft flat shading and restrained thin line work, quiet and calm, like a literary graphic novel. NOT gritty, "
        "NOT photographic, no grime, no film grain, no heavy texture. Near-future city, no cybernetics, no neon gadgets.")
ROOM = (REAL + " This is a concept frame for building a stylised low-detail 3D room: clear simple forms, readable materials, "
        "uncluttered.")
DREAM = ("Style: the cutesy dream register: pastel crayon and doodle drawing, wobbly thick outlines, flat pastel colours with "
         "crayon texture, cute simplified proportions (big round head, small body, simple dot eyes, a tiny mouth). Cute and "
         "gentle; only a faint quietness underneath. Not realistic, not 3D.")
TEX = ("A seamless tileable texture for a stylised 3D game, in the muted waking-world palette (soft desaturated colours, slightly "
       "cool), simplified and clean with soft painted shading, not photographic, not gritty. Perfectly flat, top-down "
       "orthographic, evenly lit edge to edge, no vignette, no perspective, no shadows, uniform detail so it tiles without "
       "visible repeats; the pattern fills the entire frame.")
BG = "Background: completely plain flat light grey (#d8d8d8), no scenery, no floor, no cast shadows."
PAPER = "Background: completely plain flat pale off-white paper, no scenery, no floor, no cast shadows."

LENA = ("The character: Lena, a woman in her early thirties, an artist, warm and quick to laugh. LONG, LOOSE, WAVY BLONDE HAIR "
        "falling past her shoulders, worn down and loose (no bun, no ponytail, nothing tied up), a pencil tucked behind one ear, "
        "freckles, bright kind eyes. An oversized rust-coloured linen shirt with rolled sleeves over a cream t-shirt, dark jeans "
        "flecked with paint, worn canvas sneakers, dried paint and clay on her hands and forearms.")
HAIR = ("IMPORTANT: change her hair from the reference: she is now BLONDE (warm honey blonde) with long loose waves worn down, "
        "and the pencil is tucked behind her ear, not in her hair.")
TURN = ("A character turnaround model sheet for 3D modelling: the same character drawn three times side by side at exactly the "
        "same scale and height on one shared ground line: a FRONT view, a SIDE view (true profile) and a BACK view. Each is full "
        "body from the top of the head to the shoes, standing in a neutral A-pose (arms straight and slightly away from the body, "
        "palms facing the thighs, feet a little apart), neutral expression. Orthographic, no perspective distortion, flat even "
        "shadowless lighting, clean silhouettes with space between the views.")
BILL = ("A billboard figure for a 3D game: full body from head to shoes, standing and facing the viewer straight on, centred, "
        "with a small margin above and below and plenty of empty space either side.")

NADIA_PHOTO = ("Nadia, a woman of twenty-nine with dark brown hair tied back in a messy low bun and tired eyes (Image 1 is her "
               "model sheet: same face and hair)")

P = {}
def put(name, *parts): P[name] = HEAD + "\n\n".join(p for p in parts if p) + "\n\n" + NOTEXT + "\n"

# -- Lena, redone --
put("lena2-turn", "Image 1 is Lena's previous model sheet: keep her face, freckles, build, clothes and colours.", HAIR, REAL,
    LENA, TURN, BG)
put("lena2-bb", "Image 1 is Lena's new waking-world model sheet: keep the same person (blonde loose waves, pencil behind her "
    "ear, clothes, colours). Image 2 shows the dream style (an earlier dream billboard of her, with the old hair): redraw her in "
    "that style, with the NEW blonde loose hair.", DREAM, LENA, "Introduction pose: warm and welcoming, both hands clasped in "
    "front of her (paint on her fingers), a bright knowing smile, standing as if at a doorway.", BILL, PAPER)
put("lena2-expr", "Image 1 is Lena's dream billboard: keep the same character and style exactly (blonde loose waves, pencil "
    "behind her ear).", DREAM, LENA,
    "An expression sheet: three head-and-shoulders portraits of her in one row, at exactly the same scale and framing, evenly "
    "spaced. From left to right: warm (a bright open smile), knowing (a small sad smile, eyes half-lidded), defensive (chin up, "
    "brows down, mouth tight). The hair, clothes and colours identical in every portrait.", PAPER)

# -- the opening: concept frames for the 3D build --
STYLE_REF = "Image 1 is the reference for the drawing style only (palette, line, shading): match it. Do not copy its content."
BEDROOM = ("Nadia's small, modest city apartment bedroom at three in the morning: a single bed against the wall with a rumpled "
           "duvet and a knitted throw blanket, a small wooden nightstand with a lamp (off), a glass of water and a smartphone, "
           "horizontal window blinds letting in thin stripes of cold blue-white streetlight that fall across the wall and ceiling, "
           "a chair with clothes and navy scrubs draped over it, a few framed pictures. Dark, quiet, cold blue night tones.")
put("op-bedroom-pillow", ROOM, STYLE_REF, BEDROOM,
    "View: first person, lying on the pillow, head turned towards the nightstand at mattress height: the edge of the pillow and "
    "duvet soft in the foreground, the nightstand close, the phone lying on it face up with its screen glowing a soft white-blue "
    "(a plain glowing screen, nothing readable on it) and lighting the nightstand top. The streetlight stripes from the blinds "
    "on the wall behind.")
put("op-bedroom-overhead", ROOM, "Image 1 is this same bedroom from the pillow: keep the same room, furniture, colours and light.",
    BEDROOM, "View: straight down from directly above the nightstand, a top-down overhead shot: the phone lying face up on the "
    "nightstand, its screen glowing softly (plain, nothing readable), the glass of water and the lamp base seen from above, and "
    "beside it the edge of the bed with a person-shaped lump curled up under the duvet and throw, only some dark brown hair "
    "visible on the pillow. Blind stripes of streetlight across everything.")
put("op-title-table", ROOM, STYLE_REF,
    "The title screen of the game: a close-up of a small wooden kitchen table the morning after, dim grey early-morning light "
    "from a window, quiet and sad. On the table, arranged so each object reads clearly: a tablet propped up on its stand showing "
    "a night-time news shot of a road with blurred red and blue emergency lights (no text, no captions, no logos on the screen); "
    "a half-drunk mug of milky coffee seen from above at an angle, a swirl of milk on its surface; a ballpoint pen lying on a "
    "small lined notepad; and a hospital ID badge on a dark lanyard, its card face blank. Seen from a seated eye height slightly "
    "above the table. Muted, soft, a little cold.")

# -- textures and images the 3D scenes use directly --
NEWS = ("A frame of a late-night television news video, filling the whole image: a dark wet city ring road at night seen from "
        "behind police tape, the blurred red and blue lights of emergency vehicles, a burnt-out car on its side further down the "
        "road with thin smoke, wet asphalt reflecting the lights, and in the distance a skyline of tall cold corporate towers with "
        "a few lit windows. No text, no captions, no ticker, no channel logo, no people's faces. Shot like a news camera, slightly "
        "zoomed.")
put("op-news-a", REAL, NEWS)
for k, change in [("b", "the emergency lights have turned: the red and blue glows have swapped sides and the reflections on "
                   "the road have changed accordingly"),
                  ("c", "the emergency lights are at a dimmer moment in their flash cycle and the smoke from the car has drifted "
                   "a little to the right"),
                  ("d", "the emergency lights flash brighter (more red than blue) and the camera has drifted very slightly to "
                   "the left")]:
    put(f"op-news-{k}", "Image 1 is a frame of a news video: this is the SAME shot a moment later. Keep everything identical "
        "(composition, road, car, tape, skyline, colours, style); change only this: " + change + ".", REAL, NEWS)
put("op-coffee", REAL, "A top-down view, straight down, of the surface of a cup of milky coffee filling the frame edge to edge "
    "(no rim, no mug visible): warm tan coffee with a swirl of white milk foam drawn in latte-art style that clearly spells the "
    "single word \"Quit\" in flowing cursive letters, centred, legible, with a few soft swirling tails of milk around it. "
    "Soft, flat, slightly cool morning light.")
P["op-coffee"] = P["op-coffee"].replace(NOTEXT, "Apart from the one word \"Quit\" in the milk, no text, no logos, no watermark.")
put("op-notepad", REAL, "A top-down view of a single blank page of a small lined notepad filling the whole frame edge to edge: "
    "off-white paper, faint pale blue ruled lines, a thin red margin line, a slightly used look (a soft crease, a faint coffee "
    "ring near one corner, a little pencil smudge), nothing written on it. Flat even light.")
put("op-badge", REAL, "The face of a hospital staff ID badge, seen perfectly straight on and flat, filling most of the frame "
    "(a vertical card, centred, plain light grey around it): a rounded-corner plastic card with a calm teal header band and a "
    "small simple abstract emblem (a soft cross shape inside a circle, no letters), a blank light-grey rectangular slot where "
    "the photo goes, and several blank pale panels and thin lines where the printed name and details go (all empty), a small "
    "blank barcode-like strip area left plain, a punched slot at the top for the lanyard clip. Clean, slightly worn edges.")
put("op-badge-photo", REAL, "Image 1 is Nadia's model sheet.", f"A small ID-badge photo of {NADIA_PHOTO}: head and shoulders, "
    "facing the camera straight on, a neutral tired half-smile, navy scrubs collar visible, against a plain flat pale blue "
    "backdrop, evenly lit like an ID photo. Centred, the head and shoulders filling the middle of the frame.")
put("op-phone-wallpaper", REAL, "Image 1 is Nadia's model sheet and Image 2 is her older brother Elias's model sheet: keep their "
    "faces and hair (they are a few years younger here).",
    "A cosy, happy snapshot from a few years ago, used as a phone lock-screen picture: the brother and sister squeezed together "
    "on a sofa or at a small table, laughing, his arm around her shoulders, her leaning into him, both in casual clothes (a "
    "sweater, a hoodie), warm lamp light, a birthday cake with candles or mugs on the table. Compose it with the two of them "
    "close together in the middle of the frame (it will be cropped to a tall phone screen). Warmer than the rest of the waking "
    "world: a remembered good day.")
for name, what in [("tex-wallpaper", "Faded apartment wallpaper: a small, calm repeating pattern of tiny muted sage and grey-blue "
                    "leaves on a pale warm grey ground, slightly worn."),
                   ("tex-woodfloor", "A wooden floor: medium-width oak floorboards running vertically, soft muted honey-grey "
                    "tones, gentle grain, slightly worn, board seams evenly spaced."),
                   ("tex-bedsheet", "Soft cloth: muted pale grey-blue cotton with a subtle fine weave, "
                    "very subtle, no creases."),
                   ("tex-knit", "A knitted blanket: a chunky cable-knit pattern in muted oatmeal-grey wool, soft, evenly "
                    "repeating rows."),
                   ("tex-tablewood", "A wooden table top: smooth, slightly worn walnut-brown planks running horizontally, soft "
                    "grain, a muted warm-grey cast, a few faint marks.")]:
    put(name, TEX, what)

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts")

# -- Lena, round 2 (the creator's notes, 2026-10-10): a face and clothes clearly unlike Nadia's, sleeves DOWN, the pencil on her ear --
LENA3 = ("The character: Lena, a woman in her early thirties, a ceramicist, warm and quick to laugh. Her FACE must be clearly "
         "different from a narrow, angular, hollow-cheeked face: Lena has a soft, ROUND, broad face with full cheeks and a rounded "
         "chin, a short slightly upturned button nose, soft rounded light-brown eyebrows, large WIDE-SET light green-grey eyes, a "
         "wide full-lipped mouth that smiles easily, and freckles across her nose and cheeks. Long, loose, wavy honey-blonde hair "
         "worn down past her shoulders, the hair on one side TUCKED BEHIND HER EAR, and a pencil resting ON TOP OF THAT EAR, "
         "deliberately tucked behind it and clearly visible (not lost in the hair). Clothes, a ceramicist's look: a long-sleeved "
         "dusty-rose (muted mauve-pink) cotton midi dress with the SLEEVES DOWN TO THE WRISTS, buttoned cuffs, NOT rolled or "
         "pushed up; over it a natural ecru canvas work apron tied at the waist, smeared with pale grey clay; brown leather ankle "
         "boots. Dried clay on her fingers. No jacket, no shirt over a t-shirt, no jeans.")
SLEEVES = "Her sleeves are full length and down to her wrists in every view: never rolled up, never pushed up."
put("lena3-turn", "Image 1 is the reference for the drawing style and the model-sheet layout only (line, shading, palette, "
    "A-pose, three views): match them, but do NOT copy the person, face or clothes from Image 1.", REAL, LENA3, SLEEVES, TURN, BG)
put("lena3-bb", "Image 1 is Lena's waking-world model sheet: keep the same person (round face, blonde waves tucked behind one "
    "ear with the pencil on that ear, dusty-rose long-sleeved dress, clay-smeared apron, colours). Image 2 shows the dream style: "
    "redraw her completely in that style. Do not draw the person from Image 2.", DREAM, LENA3, SLEEVES,
    "Introduction pose: warm and welcoming, both hands clasped in front of her apron (clay on her fingers), a bright knowing "
    "smile, standing as if at a doorway. Cute and gentle.", BILL, PAPER)
put("lena3-expr", "Image 1 is Lena's dream billboard: keep the same character and style exactly (round face, blonde waves, the "
    "pencil on her ear, the dusty-rose dress and apron).", DREAM, LENA3,
    "An expression sheet: three head-and-shoulders portraits of her in one row, at exactly the same scale and framing, evenly "
    "spaced. From left to right: warm (a bright open smile), knowing (a small sad smile, eyes half-lidded), defensive (chin up, "
    "brows down, mouth tight). The hair, the pencil, the clothes and the colours identical in every portrait.", PAPER)
for k in ("lena3-turn", "lena3-bb", "lena3-expr"):
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(P[k])

# -- Lena, round 3 (the creator's notes on round 2, 2026-10-10): leisure not labour, mysterious, an integrated nose, the pencil
#    as a habit of an ordered, curious mind, a small dream ear. Proposed passion: she keeps notebooks of the city's vanishing places.
LENA4 = ("The character: Lena, a woman in her early thirties who married into money and has leisure: composed, rested, quietly "
         "curious, a little unreadable. Her face: soft, full and round, rested (no tiredness), a rounded chin, freckles across "
         "her nose and cheeks, large wide-set green-grey eyes with a calm, level, knowing look, a closed-lipped half-smile that "
         "doesn't explain itself. Her NOSE is small and softly modelled INTO the face with the same skin tones and gentle shading as "
         "her cheeks, no separate outline: it belongs to the face, never a shape pasted on top. Long, loose, wavy honey-blonde hair "
         "worn down past her shoulders, the right side neatly tucked behind her right ear, and resting on top of that ear a "
         "neatly sharpened slim dark-green pencil, placed precisely and deliberately, the habit of someone who notes things down "
         "and keeps things in order. Clothes: quality, considered and a little private, never flashy, no workwear: a fitted "
         "cream fine-knit high-neck sweater with the SLEEVES DOWN TO THE WRISTS; a long, softly pleated deep wine-red midi skirt; "
         "slim dark-brown leather ankle boots; a thin gold watch worn with its face on the INSIDE of her left wrist; a small old "
         "brass key on a fine chain around her neck. Posture: upright, relaxed, self-contained.")
SLEEVES4 = ("Her sleeves are full length and down to her wrists in every view: never rolled or pushed up. No apron, no clay, no "
            "paint, nothing that looks like work clothes.")
put("lena4-turn", "Image 1 is Lena's previous model sheet: keep her face shape (full and round), freckles, honey-blonde waves and "
    "the drawing style and sheet layout. CHANGE her clothes, expression and nose as described here.", REAL, LENA4, SLEEVES4, TURN, BG)
put("lena4-bb", "Image 1 is Lena's new waking-world model sheet: keep the same person (face, hair tucked behind the right ear with "
    "the pencil on that ear, the cream sweater, wine-red skirt, key necklace, colours). Image 2 shows the dream style (an older "
    "dream billboard of her, in old clothes): redraw her completely in that style, in her NEW clothes. Do not copy Image 2's "
    "apron or dress.", DREAM, LENA4, SLEEVES4,
    "Her ear is SMALL and sits close against her head, partly under the hair, never sticking out; the pencil still visible on it. "
    "Introduction pose: composed and gently welcoming, holding a slim closed dark-leather notebook against her middle with both "
    "hands, a small closed-lipped half-smile that keeps something back. Cute, calm, not sinister.", BILL, PAPER)
put("lena4-expr", "Image 1 is Lena's dream billboard: keep the same character and style exactly (round face, blonde waves tucked "
    "behind a small close ear with the pencil on it, the cream high-neck sweater, the key necklace).", DREAM, LENA4,
    "Her ear is small and close to her head. An expression sheet: three head-and-shoulders portraits of her in one row, at "
    "exactly the same scale and framing, evenly spaced. From left to right: warm (a soft open smile), knowing (a small closed "
    "half-smile, eyes calm and level, giving nothing away), defensive (chin up, a cool, guarded look, mouth set). The hair, the "
    "pencil, the clothes and the colours identical in every portrait.", PAPER)
for k in ("lena4-turn", "lena4-bb", "lena4-expr"):
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(P[k])

# -- Lena, round 4 step 1 (the creator's notes on round 3, 2026-10-10): no key necklace (it read as a cross), the notebook never
#    held to her chest, practical clothes for going out at night instead of the skirt.
LENA5 = LENA4.replace("a long, softly pleated deep wine-red midi skirt; slim dark-brown leather ankle boots; a thin gold watch worn "
                      "with its face on the INSIDE of her left wrist; a small old brass key on a fine chain around her neck.",
                      "slim, straight-leg deep wine-red wool trousers, well cut; practical slim dark-brown leather lace-up ankle "
                      "boots made for walking the city at night; a thin gold watch worn with its face on the INSIDE of her left "
                      "wrist. NO necklace, NO pendant, NO chain, nothing at all on her chest or neck.")
assert LENA5 != LENA4
NOCROSS = ("Nothing on her chest, no jewellery except the watch, and no crossed straps, crossed lines or cross-like shapes anywhere "
           "in the image.")
put("lena5-turn", "Image 1 is Lena's previous model sheet: keep her face, freckles, honey-blonde waves, the pencil on her right "
    "ear, the cream sweater, the drawing style and sheet layout. CHANGE: remove the key necklace completely, and replace the skirt "
    "with the trousers and boots described here.", REAL, LENA5, SLEEVES4, NOCROSS, TURN, BG)
put("lena5-bb", "Image 1 is Lena's new waking-world model sheet: keep the same person (face, hair tucked behind the right ear with "
    "the pencil on that ear, the cream sweater, wine-red trousers, boots, colours). Image 2 shows the dream style (an older dream "
    "billboard of her, in old clothes): redraw her completely in that style, in her NEW clothes. Do not copy Image 2's skirt, "
    "necklace or pose.", DREAM, LENA5, SLEEVES4, NOCROSS,
    "Her ear is SMALL and sits close against her head, partly under the hair, never sticking out; the pencil still visible on it. "
    "Introduction pose: composed and at ease, one slim dark-leather notebook held loosely in ONE hand down at her side (not held "
    "to her chest), the other hand relaxed, weight on one leg, a small closed-lipped half-smile that keeps something back. Cute, "
    "calm, not sinister.", BILL, PAPER)
put("lena5-expr", "Image 1 is Lena's dream billboard: keep the same character and style exactly (round face, blonde waves tucked "
    "behind a small close ear with the pencil on it, the cream high-neck sweater, no necklace).", DREAM, LENA5, NOCROSS,
    "Her ear is small and close to her head. An expression sheet: exactly three head-and-shoulders portraits of her in one row, "
    "nothing else in the image, at exactly the same scale and framing, evenly spaced. From left to right: warm (a soft open "
    "smile), knowing (a small closed half-smile, eyes calm and level, giving nothing away), defensive (chin up, a cool, guarded "
    "look, mouth set). The hair, the pencil, the clothes and the colours identical in every portrait.", PAPER)
for k in ("lena5-turn", "lena5-bb", "lena5-expr"):
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(P[k])

# -- Lena, round 4 step 4: the other agent's fixed Lena (face, green eyes, honey-ash hair, level gaze) in my 4-a clothes, described
#    in words. Only her own sheet is attached (attaching both Lenas made the model copy one face wholesale).
CLOTHES6 = ("Clothes (CHANGE to these, described here, not the ones in Image 1): a fitted cream fine-knit high-neck sweater with "
            "the SLEEVES DOWN TO THE WRISTS; slim, straight-leg deep wine-red wool trousers, well cut; practical slim dark-brown "
            "leather lace-up ankle boots; a thin gold watch worn with its face on the INSIDE of her left wrist. NO coat, no jacket, "
            "NO necklace, NO pendant, no chain, nothing on her chest or neck.")
KEEP6 = ("Keep EXACTLY her face from Image 1: the round face, the green eyes, the level, calm, non-playful gaze, the honey-ash "
         "loose waves, her freckles if any, and a neatly sharpened slim pencil resting on top of her right ear with the hair "
         "tucked behind that ear.")
put("lena6-turn", "Image 1 is Lena's model sheet.", KEEP6, CLOTHES6, REAL, SLEEVES4, NOCROSS, TURN, BG)
put("lena6-bb", "Image 1 is Lena's dream billboard (the dream style and her face).", KEEP6, CLOTHES6, DREAM, SLEEVES4, NOCROSS,
    "Her ear is SMALL and sits close against her head, partly under the hair, never sticking out; the pencil still visible on it. "
    "ONE single figure in the image, centred, no faded duplicates or ghost figures. Introduction pose: composed and at ease, ONE "
    "slim dark-leather notebook held loosely in one hand down at her side (not held to her chest), the other hand relaxed, a "
    "small closed-lipped half-smile, a level gaze. Cute, calm, not sinister.", BILL, PAPER)
put("lena6-expr", "Image 1 is Lena's dream billboard: keep the same character, clothes and style exactly (no coat, no "
    "necklace).", DREAM, NOCROSS,
    "Her ear is small and close to her head, the pencil on it. An expression sheet: exactly three head-and-shoulders portraits of "
    "her in one row, nothing else in the image, at exactly the same scale and framing, evenly spaced. From left to right: warm (a "
    "soft open smile), knowing (a small closed half-smile, eyes calm and level, giving nothing away), defensive (chin up, a cool, "
    "guarded look, mouth set). The hair, the pencil, the clothes and the colours identical in every portrait.", PAPER)
for k in ("lena6-turn", "lena6-bb", "lena6-expr"):
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(P[k])
