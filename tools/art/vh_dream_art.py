# Art for the dream scenes (Night 2's house and the dream escape), 2026-10-11, pass 1: the cast's missing dream cut-outs
# (the two neighbours and the dog) and the house's story pictures (the photo wall, the fridge calendar, Theo's drawing).
# Writes Flow prompts into docs/art/prompts/dream/.   uv run python tools/art/vh_dream_art.py
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "dream")
os.makedirs(OUT, exist_ok=True)
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
NOTEXT = "No text, no letters, no numbers, no logos, no watermark, no signature anywhere in the image."
DREAM = ("Style: the cutesy dream register of a narrative game, exactly like Image 1: pastel crayon and pencil sketchbook drawing, "
         "wobbly thick dark outlines, flat pastel colours with visible crayon texture, chibi proportions (a big round head about a "
         "third of the height, a small body, simple dot eyes, a tiny mouth). A hand-drawn sketchbook look, not a clean polished "
         "render. Not realistic, not 3D.")
STYLE_ONLY = "Image 1 is the reference for the drawing style only: do not draw the old man from Image 1."
BILL = ("A billboard cut-out for a 3D game: ONE figure, full body from head to feet, facing the viewer, centred, with a margin "
        "above and below and plenty of empty space either side. Its own unique pose that says who it is, and one strong "
        "defining colour.")
PAPER = "Background: completely plain flat pale off-white paper, no scenery, no floor, no cast shadow."

P = {}
def put(name, *parts): P[name] = HEAD + "\n\n".join(parts) + "\n\n" + NOTEXT + "\n"

put("dream-neighbour-roses", DREAM, STYLE_ONLY,
    "The character: a friendly neighbour in her early sixties, a wide straw sun hat, short silver-blonde hair, a LILAC blouse "
    "(her defining colour), a pale khaki skirt, floral gardening gloves. Pose: watering roses that aren't there, tipping a big "
    "galvanised watering can forward with both hands, smiling a little too long at the viewer.", BILL, PAPER)
put("dream-neighbour-grill", DREAM, STYLE_ONLY,
    "The character: a jolly heavyset neighbour in his late forties, sunburnt cheeks, short sandy hair, a RED-and-cream plaid "
    "short-sleeved shirt (his defining colour), khaki shorts, white socks, old sneakers. Pose: one hand raised high in a big wave, "
    "the other holding long barbecue tongs up like a trophy, grinning. No grill, no smoke.", BILL, PAPER)
put("dream-dog", DREAM, STYLE_ONLY,
    "The character: a golden retriever drawn in the same chibi crayon style: a big round head, dot eyes, a tiny tongue out, a "
    "fluffy golden coat (its defining colour) and a red collar with no tag. Pose: sitting upright facing the viewer, head tilted, "
    "very still and patient, waiting.",
    "A billboard cut-out for a 3D game: ONE dog, whole body, facing the viewer, centred, with a margin all round and plenty of "
    "empty space either side.", PAPER)

PIC = ("Style: drawn in the dream's pastel crayon sketchbook style like Image 1 (wobbly thick outlines, flat pastel crayon colour, "
       "chibi people with big round heads and dot eyes). " + "Image 1 is the reference for the drawing style only.")
put("dream-photo-wall", PIC,
    "Six separate family snapshots, each one its own landscape rectangle with a thin white photo border, laid out in a neat grid "
    "of 3 columns by 2 rows with wide plain gaps between them, all the same size, on a plain pale off-white background. The "
    "family: a father with short dark hair and a short beard, a mother with long honey-blonde waves, and their son (messy light "
    "brown hair, a mustard-yellow hoodie). The six snapshots: 1) the couple's wedding day under a tree; 2) the mother holding the "
    "newborn baby, the father asleep in a chair beside them; 3) the boy aged about five on his first bicycle, the father running "
    "behind holding the seat; 4) the three of them on a beach, the boy on his father's shoulders; 5) the boy blowing out ten "
    "birthday candles, the parents and his aunt (dark brown hair in a messy bun) clapping; 6) the father and the boy asleep on "
    "the sofa under one blanket. Warm, happy, slightly too perfect.")
put("dream-calendar", PIC,
    "A family's paper wall calendar pinned to a fridge door with two round magnets, seen straight on and filling most of the "
    "frame (a tall page, centred, plain pale background around it): a month grid of empty squares, a few squares marked with "
    "little crayon doodles (a pancake stack, a small star, a heart, a bicycle wheel), one row circled in red crayon, and a "
    "child's sticker of a sun in the corner. Any numbers or words are only illegible crayon squiggles.")
put("dream-theo-drawing", PIC,
    "A ten-year-old boy's crayon drawing on a sheet of paper, filling the frame, seen straight on from above: his family "
    "holding hands in front of their house under a big yellow sun: a dad with a beard, a mum with long yellow hair, and the boy "
    "in a yellow hoodie in the middle; a fourth smaller figure stands a little apart at the edge with dark hair in a bun; green "
    "scribbled grass, a blue scribbled sky. Drawn like a real child's drawing (wobbly, proud, uneven). Any writing is only "
    "illegible scribbles.")

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts")
