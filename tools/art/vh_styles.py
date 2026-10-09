# Style exploration for Visiting Hours (2026-10-09): the same subjects in five styles, A-E.
# Writes Flow prompts into docs/art/prompts/styles/.   uv run python tools/art/vh_styles.py
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "styles")
os.makedirs(OUT, exist_ok=True)
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
NOTEXT = "No text, no letters, no numbers, no logos, no watermark, no signature anywhere in the image."

STYLE = {
 "A": "Flat cut-paper cartoon style: bold simple flat shapes as if cut from coloured paper, minimal shading (at most one flat "
      "shadow tone per colour), no outlines or only very thin ones, a limited palette of six to eight muted colours, a subtle paper "
      "grain texture, stylised proportions (long thin limbs, simple geometric heads, small dot eyes). Gentle and soft, an indie "
      "adventure game look. Not realistic, not 3D.",
 "B": "Soft storybook cartoon style: rounded, gentle, simplified forms, soft gouache-like shading with visible brush texture, a "
      "warm glowing palette, small kind eyes, chunky friendly proportions (slightly large heads and hands), like a cosy hand-painted "
      "picture book. Warm and tender. Not realistic, not 3D.",
 # round 1 came out as western comic line art with soft blotchy shading (2026-10-09); round 2 pushes the anime cel look
 "C": "Japanese-anime-style character design with clean cel shading: sharp, clean, even dark line art; strictly flat colour "
      "fills with exactly one hard-edged cel shadow tone per colour and a few crisp highlights; no painterly texture, no "
      "gradients, no grime rendering (stains drawn as simple flat shapes); anime facial features (clean simplified nose and "
      "mouth, expressive eyes) on adult proportions, like the standing character sprites of a Japanese visual-novel mystery "
      "game. Not chibi, not realistic, not 3D.",
 "D": "Hand-drawn crayon and pastel sketchbook style: wobbly uneven pencil outlines, waxy crayon and soft pastel texture with "
      "visible paper tooth, a pastel palette, simplified cute proportions (rounded heads, small bodies, simple dot eyes), as if drawn "
      "in a sketchbook: cute on the surface, with a faint unsettling quietness. Not realistic, not 3D.",
 "E-real": "Grounded adult stylised illustration (the waking world): a muted, desaturated, slightly cold palette, simplified but "
      "realistic proportions, clean shapes with soft flat shading, restrained thin line work, quiet and serious, like a literary "
      "graphic novel. Not cute, not realistic painting, not 3D.",
 "E-dream": "Cutesy dream style: pastel crayon and doodle drawing, wobbly thick outlines, flat pastel colours with crayon "
      "texture, cute simplified proportions (big round head, small body, simple dot eyes, a tiny smile), a cheerful surface with "
      "something faintly wrong underneath (too still, too perfect, the eyes a little too empty). Not realistic, not 3D.",
}

WALT = ("An old man in his mid-seventies, tall and thin with a slight stoop, a kind lined face, a neat white moustache, a brown "
        "tweed flat cap, a faded blue work shirt with the sleeves rolled to the elbows, a tan canvas work apron with old grease "
        "stains, dark grey work trousers and worn brown boots. He is the patient old bicycle mechanic who once taught a boy to fix "
        "bikes: standing facing the viewer, calmly wiping his hands on a red shop rag, a slight patient smile.")
NADIA = ("A woman of twenty-nine, a night-shift nurse: navy blue scrubs over a long-sleeved grey thermal top, white work sneakers, "
         "dark brown hair tied back in a messy low bun with loose strands, tired shadowed eyes, a plain blank ID badge clipped to her "
         "top. Standing facing the viewer, guarded and tired: her phone held in one hand at her side, the other arm across her body "
         "holding that elbow.")
MAGS = ("A Black woman in her mid-fifties, a sleep-lab technician: close-cropped natural grey hair, reading glasses hanging on a "
        "beaded cord around her neck, tired but amused eyes and a dry half-smile, a long soft oatmeal-grey cardigan over pale teal "
        "scrubs, comfortable white sneakers. Standing facing the viewer, holding a plain white coffee mug in both hands.")
STREET = ("A first-person view at standing eye height from the sidewalk, looking down a quiet suburban street at golden hour on a "
          "Saturday: real three-dimensional houses with porches set back behind green lawns on both sides, a sprinkler arcing water, "
          "mature trees, picket fences, a barbecue with a thin wisp of smoke and a small figure far down the street, a golden "
          "retriever on a lawn, long warm shadows across the road. Warm late-afternoon light, a little too perfect, like a fond "
          "memory. Concept art target for a first-person 3D game: readable simple shapes, clear materials, uncluttered.")
CORRIDOR = ("A first-person view at standing eye height down a hospital corridor at night: pale green walls, a speckled vinyl floor "
            "reflecting the overhead fluorescent ceiling panels, a handrail along one wall, closed doors with small windows, a lit "
            "nurses' station counter at the far end, a wheelchair parked against the wall, a blank green exit sign shape. Quiet, empty "
            "and cold, flat cool fluorescent light. Concept art target for a first-person 3D game: readable simple shapes, clear "
            "materials, uncluttered.")

def bg(style):
    paper = style in ("D", "E-dream")
    return ("Background: completely plain flat " + ("pale off-white paper" if paper else "light grey (#d0d0d0)") +
            ", no scenery, no floor, no cast shadow. Full body from head to shoes, centred, with a small margin above and below and "
            "plenty of empty space either side.")

CHAR_REF = "Image 1 shows this same style (and is the reference for the drawing style only): match its line, shading, palette and proportions exactly."
P = {}
def put(name, *parts): P[name] = HEAD + "\n\n".join(p for p in parts if p) + "\n\n" + NOTEXT + "\n"

for s in "ABCD":
    st = "Character art for a narrative video game. " + STYLE[s]
    put(f"style-{s}-walt", st, WALT, bg(s))
    put(f"style-{s}-nadia", st, CHAR_REF + " Do not draw the man from Image 1; draw only the woman described.", NADIA, bg(s))
    put(f"style-{s}-mags", st, CHAR_REF + " Do not draw the man from Image 1; draw only the woman described.", MAGS, bg(s))
    put(f"style-{s}-street", "Environment concept art for a first-person video game. " + STYLE[s],
        CHAR_REF + " Do not put the man from Image 1 in the scene.", STREET)
    put(f"style-{s}-corridor", "Environment concept art for a first-person video game. " + STYLE[s],
        CHAR_REF + " Do not put the man from Image 1 in the scene.", CORRIDOR)

# E, the split: the waking world grounded, the dream cutesy. Same characters in both.
put("style-E-walt-real", "Character art for a narrative video game. " + STYLE["E-real"], WALT, bg("E-real"))
put("style-E-walt-dream", "Character art for a narrative video game. " + STYLE["E-dream"],
    "Image 1 is the same old man drawn in the game's waking-world style: keep him the same person (cap, moustache, shirt, apron, "
    "rag, pose and colours) but redraw him completely in the cutesy dream style described here.", WALT, bg("E-dream"))
put("style-E-nadia-real", "Character art for a narrative video game. " + STYLE["E-real"],
    CHAR_REF + " Do not draw the man from Image 1; draw only the woman described.", NADIA, bg("E-real"))
put("style-E-nadia-dream", "Character art for a narrative video game. " + STYLE["E-dream"],
    "Image 1 is this same woman drawn in the waking-world style: keep her the same person (hair, scrubs, badge, phone, pose and "
    "colours). Image 2 shows the cutesy dream style: redraw her completely in that style. Do not draw the man from Image 2.",
    NADIA, bg("E-dream"))
put("style-E-street", "Environment concept art for a first-person video game. " + STYLE["E-dream"],
    CHAR_REF + " Do not put the man from Image 1 in the scene.", STREET)
put("style-E-corridor", "Environment concept art for a first-person video game. " + STYLE["E-real"],
    CHAR_REF + " Do not put the man from Image 1 in the scene.", CORRIDOR)

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts")
