# Writes the Flow prompts for the Visiting Hours slice art into docs/art/prompts/ (one shared style bible).
#   uv run python tools/art/vh_prompts.py
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts")
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
STYLE = ("Painterly grounded realism for a serious narrative video game: soft gouache and oil paint textures with visible but "
         "controlled brushwork, clean readable silhouettes, muted natural palette, naturalistic proportions. Not anime, not cartoon, "
         "not a photograph, not a 3D render.")
DREAM = ("Lighting: warm late-afternoon golden sunlight from the left, colours slightly too saturated like a fond memory, very soft "
         "hazy edges on the light.")
REAL = "Lighting: cool, flat fluorescent light at night, desaturated blue-grey palette, quiet and cold, soft shadows."
CUT = ("Background: completely plain flat light grey (#d0d0d0), no gradient, no scenery, no floor, no cast shadow on the background, "
       "so the subject can be cut out cleanly. The whole subject is inside the frame with a margin on every side.")
NOTEXT = "No text, no letters, no numbers, no logos, no watermark, no signature anywhere in the image."
SAME = ("Image 1 is the reference for this character: keep exactly the same face, age, white moustache, flat cap, blue work shirt, "
        "grease-stained canvas apron, painting style, colours and lighting.")

P = {}
def item(name, *parts): P[name] = HEAD + "\n\n".join(parts) + "\n\n" + NOTEXT + "\n"

# -- characters --
item("vh-walt-sad", "Character portrait. " + STYLE, SAME,
     "Change only his expression and hands: he looks gently sad and regretful, eyes lowered to one side, inner brows raised, "
     "the corners of his mouth turned down softly under the moustache, no smile. His hands are loosely clasped in front of the "
     "apron. Five fingers on each hand, natural hands.",
     "Framing: waist-up, centred, facing the viewer with a very slight three-quarter turn, the figure filling about 90% of the "
     "frame height, plenty of empty space either side.", DREAM, CUT)
item("vh-walt-firm", "Character portrait. " + STYLE, SAME,
     "Change only his expression and pose: he is firm and immovable but not cruel, looking straight into the viewer's eyes, brows "
     "level and slightly lowered, jaw set, mouth a straight line under the moustache, no smile. His arms are folded across the "
     "apron, standing tall. Five fingers on each hand, natural hands.",
     "Framing: waist-up, centred, facing the viewer straight on, the figure filling about 90% of the frame height, plenty of empty "
     "space either side.", DREAM, CUT)
item("vh-walt-billboard", "Full-body character art. " + STYLE, SAME,
     "Show him full body, from the top of the cap to his shoes, standing upright and facing the viewer straight on, relaxed, one "
     "hand raised to shoulder height in a small friendly wave, the other hand at his side. He wears plain dark grey work trousers "
     "and worn brown leather work boots under the apron. Tall and thin, slightly stooped. Five fingers on each hand.",
     "Framing: the whole figure fits in the frame with a small margin above the cap and below the boots, centred, plenty of empty "
     "space either side.", DREAM, CUT)
item("vh-neighbor-roses", "Full-body character art. " + STYLE,
     "A woman in her early sixties, a friendly neighbour: a wide straw sun hat, short silver-blonde hair, a light lilac blouse with "
     "rolled sleeves, a pale khaki gardening skirt to below the knee, canvas garden shoes, floral cotton gardening gloves. She holds "
     "a galvanised metal watering can in one hand, tilted slightly, and smiles warmly at the viewer. Five fingers on each hand.",
     "Framing: full body from hat to shoes, standing upright and facing the viewer straight on, centred, with a small margin above "
     "and below, plenty of empty space either side.", DREAM, CUT)
item("vh-neighbor-grill", "Full-body character art. " + STYLE,
     "A man in his late forties, a friendly neighbour: heavyset, sunburnt cheeks, short sandy hair, a short-sleeved red and cream "
     "plaid shirt, khaki shorts, white socks and old sneakers. He stands behind a round black kettle barbecue grill on three legs, "
     "holding long metal tongs in one hand and raising the other in a cheerful greeting, grinning at the viewer. No smoke, no fire "
     "visible. Five fingers on each hand.",
     "Framing: the man and the grill together, full body from head to shoes, facing the viewer straight on, centred, with a small "
     "margin above and below, plenty of empty space either side.", DREAM, CUT)
item("vh-dog", "Animal character art. " + STYLE,
     "A friendly golden retriever with a glossy feathered golden coat and a red fabric collar (no tag text), sitting upright and "
     "facing the viewer straight on, head slightly tilted, mouth open in a happy pant with the tongue showing, ears soft. Four legs, "
     "anatomically correct.",
     "Framing: the whole dog, centred, filling about 80% of the frame height, plenty of empty space either side.", DREAM, CUT)
item("vh-elias-bed", "Scene object art. " + STYLE,
     "A man in his mid-thirties lying asleep in a hospital bed, in a coma: lean face, short dark brown hair, a short neat dark beard, "
     "pale skin, eyes closed, peaceful, a clear nasal oxygen cannula, a pale blue patterned hospital gown, a pulse-oximeter clip on "
     "one finger, an IV line taped to the back of one hand. White sheets and a pale blue cotton blanket folded at his chest, the head "
     "of the bed raised slightly. A grey metal-and-plastic adjustable hospital bed with side rails and wheels.",
     "Framing: the whole bed seen in three-quarter view from the foot of the bed, at standing eye height, the bed's full outline "
     "inside the frame with a margin on every side. No monitors, no other furniture, no IV pole, no people.", REAL, CUT)

# -- props (journal item art and in-world props) --
def prop(name, what, light):
    item(name, "Item illustration for a game journal. " + STYLE, what,
         "Framing: the single object, centred, seen from a slight three-quarter angle above, filling about 70% of the frame height, "
         "nothing else in the image.", light, CUT)
prop("vh-chart", "A hospital patient chart: a pale grey plastic clipboard with a metal clip holding a few printed forms, with a small "
     "vital-signs graph line and boxes; all the handwriting and print are only faint, illegible grey marks and lines, no readable "
     "letters or numbers. A blue ballpoint pen clipped to it.", REAL)
prop("vh-effects-bag", "A clear plastic hospital patient belongings bag with a white drawstring, slightly crumpled, holding a folded "
     "dark navy jacket, a worn brown leather wallet, a smartphone with a cracked screen and a keyring with keys; a blank white label "
     "sticker on the bag with nothing written on it.", REAL)
prop("vh-keyring", "A worn keyring: a steel split ring with four keys of different shapes (a house key, a brass key, a car key with a "
     "black plastic head, a small padlock key) and a small worn brown leather tag stamped with a tiny bicycle-wheel emblem, no letters.",
     REAL)
prop("vh-memorial-card", "A folded funeral memorial card, cream paper, standing slightly open, with a thin deep green border and a "
     "small painted spray of white flowers on the front; any lettering is only faint illegible grey lines, no readable letters. The "
     "card is a little creased from being kept in a wallet.", REAL)
prop("vh-police-envelope", "A large manila envelope, flap unsealed and slightly open showing the edges of papers inside, a blank white "
     "label, a faint blank official stamp outline, slightly worn corners.", REAL)
prop("vh-newspaper", "A folded morning newspaper lying with the front page up, held with a thin rubber band, slightly damp at one "
     "corner, as if just thrown onto a porch; the masthead and articles are only illegible grey blocks and lines, no readable letters "
     "or numbers, with a small sunny photo block.", DREAM)
prop("vh-wrench", "An old, well-used steel adjustable wrench, about thirty centimetres long, the steel worn smooth and shiny where "
     "hands held it, faint grease in the jaws, traces of old red paint on the handle.", DREAM)

# -- environment --
def flat(name, what):
    item(name, "A painted theatre-style stage flat for a game set. " + STYLE, what,
         "View: perfectly straight-on, front elevation, almost no perspective, like a painted stage backdrop of a slightly-too-perfect "
         "dream street. The building stands on a clean straight ground line at the bottom with a narrow strip of lawn edge; nothing "
         "behind it.", DREAM,
         "Background: where the sky would be, completely plain flat light grey (#d0d0d0), no clouds, no trees behind, so it can be "
         "cut out cleanly. The whole building is inside the frame with a margin on the left, right and top.")
flat("vh-flat-house-1", "A two-storey white clapboard suburban house with pale blue shutters, a covered front porch with white "
     "columns and three steps, a red front door, a dark grey shingle roof with a brick chimney, neat hedges and a bed of red and pink "
     "roses along the front.")
flat("vh-flat-house-2", "A one-and-a-half-storey butter-yellow suburban house with a wide wraparound front porch, a wooden porch swing "
     "hanging on chains, white railings, a forest-green front door, a steep dark roof with a dormer window, hanging flower baskets and "
     "hydrangea bushes along the front.")
flat("vh-flat-house-3", "A two-storey warm red-brick suburban house with white window frames, window boxes full of geraniums, a dark "
     "green front door under a small gabled porch roof, a slate roof, and low boxwood hedges along the front.")
flat("vh-flat-bike-garage", "A detached wooden workshop garage painted barn red with white trim, its wide double doors swung fully "
     "open, showing a warm, tidy bicycle repair workshop inside: bicycles hanging on wall hooks, a wheel in a truing stand, a long "
     "wooden workbench with hand tools on a pegboard, a lamp. Above the doors, a long empty painted wooden sign board with a plain "
     "cream face and nothing on it. A bicycle leaning by the door.")

def tex(name, what, light):
    item(name, "A seamless tileable texture for a game, " + STYLE, what,
         "View: perfectly top-down orthographic, evenly lit across the whole frame, no vignette, no perspective, no horizon, no objects, "
         "no cast shadows, the surface fills the entire frame edge to edge, uniform detail density so it tiles without visible repeats.",
         light)
tex("vh-tex-grass", "A lush, freshly mown suburban lawn: dense short grass blades, slightly too green and too even, like a dream of "
    "a perfect lawn, with very subtle variation in tone, painted in soft gouache strokes.",
    "Colour: warm golden late-afternoon light, slightly too saturated.")
tex("vh-tex-road", "Old suburban street asphalt: dark warm grey, fine aggregate texture, a few faint hairline cracks and subtle patches, "
    "no lane markings, painted in soft gouache strokes.", "Colour: warm golden late-afternoon light, slightly too saturated.")
tex("vh-tex-sidewalk", "Plain poured concrete pavement surface: light warm grey, fine sandy texture, subtle stains and speckles, no slab "
    "joints, no lines, no cracks, painted in soft gouache strokes.", "Colour: warm golden late-afternoon light, slightly too saturated.")
tex("vh-tex-hospital-floor", "Hospital corridor sheet-vinyl flooring with no seams and no tile lines: pale grey-green with a fine "
    "speckled flecked pattern, a faint polished sheen, painted in soft gouache strokes.",
    "Colour: cool, flat fluorescent light, desaturated blue-grey.")
tex("vh-tex-hospital-wall", "A painted institutional hospital wall: flat pale sage-green paint over smooth plaster, very subtle roller "
    "texture and faint scuffs, no lines, no fixtures, painted in soft gouache strokes.",
    "Colour: cool, flat fluorescent light, desaturated blue-grey.")
item("vh-sky-dream", "A panoramic sky backdrop for a game. " + STYLE,
     "A wide late-afternoon summer sky over a suburb: deep warm blue at the top fading to peach, gold and soft rose near the horizon, "
     "a few soft painterly cumulus clouds lit gold from the low sun on the left, slightly too saturated and faintly hazy like a fond "
     "memory. Only sky: no ground, no trees, no buildings, no birds, no sun disc; the bottom edge is a soft hazy glow.")
item("vh-sky-night", "A panoramic sky backdrop for a game. " + STYLE,
     "A wide night sky over a city: deep blue-black at the top, a few faint stars, thin dark clouds, a dull orange sodium-light glow "
     "from the city along the bottom edge. Quiet, cold and lonely. Only sky: no ground, no buildings, no moon, no trees.")
item("vh-ceiling-dark", "A first-person view for the opening of a game. " + STYLE,
     "Lying in bed in a dark bedroom at five in the morning, looking straight up at the ceiling: a plain white ceiling in near "
     "darkness, a simple round ceiling light fixture switched off, a faint thin diagonal stripe of orange streetlight falling across the "
     "ceiling through a gap in the curtains, the top edge of a wall and curtain rail at one side. Very dark, cold, desaturated blue "
     "night tones, soft painterly gradients, almost black at the corners.")
item("vh-title-news", "A title screen background for a serious narrative game. " + STYLE,
     "A dim, quiet living room at night lit only by an old flat-screen television on a low wooden cabinet. The television shows a "
     "late-night news shot of a dark wet country road with a crashed car in the distance and the blurred red and blue lights of "
     "emergency vehicles and police tape; the TV screen has no text, no captions, no ticker, no logo. The cold blue and red light from "
     "the screen spills onto the edge of a sofa, an empty mug and a folded blanket. Desaturated, quiet, sorrowful. The television "
     "is centred and large, taking about half the frame width.")

# -- UI --
item("vh-phone", "A UI asset for a game. " + STYLE,
     "A modern smartphone seen perfectly front-on and upright, a plain black frame with thin bezels and a slightly scuffed dark "
     "grey protective case, a small camera hole at the top of the screen. The screen is switched on but completely blank: a plain "
     "flat, very dark grey-blue glow with nothing on it, no icons, no clock, no notifications.",
     "Framing: the whole phone, centred, filling about 85% of the frame height, plenty of empty space either side.", REAL, CUT)
item("vh-static-wasp", "A creature design for a game. " + STYLE,
     "A small wasp-like creature made entirely of black-and-white television static: its body, wings and stinger formed from "
     "crackling grey noise grain and thin horizontal scanlines, the edges flickering and breaking up into loose pixels of static, two "
     "faint pale glowing points for eyes. Eerie but small and fragile. Seen from the front-side, wings spread, hovering.",
     "Framing: the creature centred, filling about 60% of the frame height.",
     "Background: completely plain flat chroma-key green (#00b140), no gradient, no shadow, so it can be keyed out cleanly.")
item("vh-journal-page", "A UI texture for a game journal. " + STYLE,
     "A top-down view of an open, blank notebook spread filling the entire frame edge to edge: two facing pages of slightly worn "
     "cream paper with faint pale blue ruled lines and a thin faded red margin line on each page, a soft shadow in the centre gutter, "
     "a few very faint coffee-ring stains and soft paper grain. Nothing written on the pages.",
     "Lighting: soft, even, neutral warm-white, evenly lit, no vignette.")

# -- needs the chosen Elias take attached as Image 1 --
item("vh-family-photo", "Item illustration for a game journal. " + STYLE,
     "Image 1 shows the man (asleep in hospital); use it only for his face, hair and short dark beard, healthy and awake here.",
     "A framed family photograph standing on a bedside table, seen straight on: a simple light oak wooden frame with a cream mat. "
     "The photograph inside is a sunny summer beach snapshot of a happy family laughing together: the same man from Image 1, in his "
     "mid-thirties, short dark brown hair, short neat dark beard, a faded grey t-shirt, laughing with his arm around a woman in her "
     "early thirties with long wavy chestnut hair blowing in the wind, freckles and a pale yellow sundress, and a ten-year-old boy "
     "with messy light brown hair and a striped t-shirt, grinning with a gap tooth, squeezed in between "
     "them. Blue sea, bright sand. The photo inside the frame is warm and sunny; the frame itself sits in cool, flat night "
     "fluorescent light. Five fingers on every hand.",
     "Framing: the whole frame, centred, filling about 75% of the frame height, nothing else in the image.", CUT)

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts:", " ".join(P))

# -- second round (2026-10-09): retries after the first look --
P2 = {}
P2["vh-tex-grass-b"] = HEAD + ("A seamless tileable texture for a game: a perfect suburban lawn photographed straight down from directly above, "
    "camera pointing at the ground, so only the tips of dense, short, freshly mown grass blades are seen, evenly spread over the "
    "whole frame. Painterly: soft gouache and oil strokes, clean and stylised but grounded. Colour: a rich, slightly too saturated "
    "fresh green with warm golden late-afternoon highlights, like a dream of a perfect lawn; very subtle tonal variation. "
    "Perfectly top-down orthographic, evenly lit edge to edge, no perspective, no horizon, no vignette, no flowers, no objects, "
    "no shadows, uniform detail so it tiles without visible repeats.\n\n" + NOTEXT + "\n")
P2["vh-static-wasp-b"] = HEAD + ("A creature design for a game, painterly, with soft gouache texture. A small wasp-shaped creature whose whole "
    "body is made of analogue television static: not an insect with fur and veins, but a flat wasp silhouette (head, thorax, "
    "striped abdomen, stinger, two pairs of wings) filled entirely with flickering black-and-white TV snow, crossed by thin "
    "horizontal scanlines and a few bright horizontal glitch tears, its edges dissolving into loose grains of static. Two small "
    "pale glowing points for eyes. Seen from the front, wings spread, hovering, symmetrical. Eerie but small and fragile.\n\n"
    "Framing: the creature centred, filling about 60% of the frame height.\n\nBackground: completely plain flat chroma-key green "
    "(#00b140), no gradient, no shadow, so it can be keyed out cleanly.\n\n" + NOTEXT + "\n")
for k, v in P2.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
