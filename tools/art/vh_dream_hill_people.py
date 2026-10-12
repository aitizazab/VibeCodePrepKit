# Art for Larch Hill's Saturday (2026-10-13): the dream's paper cut-outs for the HillPersonSpot roles that had no art
# (docs/redesign/world.md "Saturday on the loop"; judge hill-1 item 4: "about three people across 25 stills").
# Same register as the approved cast (vh_dream_art.py): Image 1 = Walt's dream billboard sheet, for the style only.
# Writes Flow prompts into docs/art/prompts/dream/hill-*.txt.   uv run python tools/art/vh_dream_hill_people.py
# Generate:  PDIR=docs/art/prompts/dream bash tools/art/vh_generate.sh hill-party-gran:3:art/incoming/sheet-walt-bb-take1.jpg ...
# Cut out:   uv run --with pillow --with numpy --with scipy python tools/art/vh_dream_cutout.py hill-party-gran-take2.jpg dream_party_gran
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "dream")
os.makedirs(OUT, exist_ok=True)
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
NOTEXT = "No text, no letters, no numbers, no logos, no watermark, no signature anywhere in the image."
DREAM = ("Style: the cutesy dream register of a narrative game, exactly like Image 1: pastel crayon and pencil sketchbook drawing, "
         "wobbly thick dark outlines, flat pastel colours with visible crayon texture, chibi proportions (a big round head about a "
         "third of the height, a small body, simple dot eyes, a tiny mouth). A hand-drawn sketchbook look, not a clean polished "
         "render. Not realistic, not 3D.")
STYLE_ONLY = "Image 1 is the reference for the drawing style only: do not draw the old man from Image 1, and give this person none of his clothes."
BILL = ("A billboard cut-out for a 3D game: ONE figure, full body from head to feet, facing the viewer, centred, with a margin "
        "above and below and plenty of empty space either side. Its own unique pose that says what they are doing, and one strong "
        "defining colour.")
BILL_PROP = ("A billboard cut-out for a 3D game: ONE figure{with}, whole and uncropped from head to feet{and}, centred, with a margin "
             "all round and plenty of empty space either side. Its own unique pose that says what they are doing, and one strong "
             "defining colour.")
PAPER = "Background: completely plain flat pale off-white paper, no scenery, no floor, no ground line, no cast shadow."

P = {}
def put(name, *parts): P[name] = HEAD + "\n\n".join(parts) + "\n\n" + NOTEXT + "\n"
def person(name, who, bill=BILL): put(name, DREAM, STYLE_ONLY, who, bill, PAPER)

# ---- The block party (Rowan Street): dancers, talkers, someone at a table; mixed ages ----
person("hill-party-gran",
    "The character: a tiny sprightly grandmother in her late seventies dancing at a street party, a neat cloud of white curly hair, "
    "round glasses, a string of pearls, a knee-length TANGERINE-ORANGE dress with a full skirt (her defining colour), white "
    "cardigan tied round her shoulders, low white shoes. Pose: dancing a twist, knees bent, hips swung to one side, both elbows "
    "out and fists pumping, the skirt flaring, beaming with her eyes shut in delight.")
person("hill-party-chatter",
    "The character: a lanky young man in his late twenties, Black, short twists of hair, a short beard, a TEAL short-sleeved "
    "bowling shirt with one cream stripe down each side of the front (his defining colour), rolled light jeans, white sneakers. "
    "Pose: mid-story at a party, a red paper cup in one hand held at chest height, the other hand thrown out wide to show how big "
    "something was, eyebrows up, mouth open laughing.")
person("hill-party-teen",
    "The character: a fifteen-year-old girl, East Asian, a high messy ponytail with a scrunchie, a LIME-GREEN cropped hoodie (her "
    "defining colour), denim shorts, striped knee socks, chunky sneakers, a pointy paper party hat. Pose: dancing badly on purpose, "
    "one arm straight up pointing at the sky, the other hand on her hip, one knee lifted, tongue poking out.")
person("hill-party-girl",
    "The character: a little girl about six years old, light brown skin, two puffy pigtails with ribbons, a BUBBLEGUM-PINK party "
    "dress with a puffy skirt (her defining colour), white socks, red sandals. Pose: standing on tiptoe, holding the string of a "
    "big round sky-blue balloon floating above her head with both hands, looking up at it in wonder. The balloon and its string "
    "are inside the frame.")
person("hill-party-oldman",
    "The character: a stout old man in his eighties, very pale, bald on top with a white fringe and big white eyebrows, a NAVY-AND-"
    "WHITE striped sailor shirt (his defining colour), tan trousers held up with braces, brown slippers. Pose: SITTING on an "
    "unseen seat at knee height (draw no seat), facing the viewer, knees forward and apart, feet flat, a thick slice of watermelon "
    "held up in both hands about to take a bite, eyes twinkling. Nothing under him, no chair, no bench, no bale.",
    BILL_PROP.format(**{"with": " sitting", "and": ""}))
person("hill-party-pie",
    "The character: a plump cheerful woman in her mid-forties, Latina, dark curly bob, big hoop earrings, a SKY-BLUE gingham "
    "sundress (her defining colour) with a white apron sash, flat sandals. Pose: proudly carrying a big round lattice cherry pie "
    "out in front of her with both hands in oven mitts, leaning back a little under its weight, chin up, bright smile, like she's "
    "showing it to everyone.")

person("hill-party-dad",
    "The character: a balding dad in his mid-forties, pale, a little paunch, a neat brown goatee, sunglasses pushed up on his "
    "head, a COBALT-BLUE polo shirt with a white collar (his defining colour), beige cargo shorts, socks with sandals. Pose: "
    "embarrassing dad dancing, both hands making finger guns pointed off to one side, one knee bent and the hip popped, "
    "mouth in a proud 'o', completely sure he looks cool.")
person("hill-party-mum",
    "The character: a young mother in her early thirties, South Asian, long black hair in a thick side braid, small gold "
    "earrings, a PLUM-PURPLE wrap dress (her defining colour), flat gold sandals. Pose: SITTING on an unseen seat at knee "
    "height (draw no seat), facing the viewer, legs crossed at the knee, a paper plate with a hot dog balanced on her lap, "
    "throwing her head back laughing at a joke, one hand over her heart. Nothing under her, no chair, no bench, no bale.",
    BILL_PROP.format(**{"with": " sitting", "and": ""}))

# ---- Mill Street: kids on bikes, the car wash ----
person("hill-kid-bike-boy",
    "The character: a boy about ten years old, freckled, sandy hair sticking out under a backwards CHERRY-RED baseball cap, a "
    "cherry-red T-shirt (his defining colour), khaki shorts with scabbed knees, sneakers. Pose: riding a small cherry-red BMX bike "
    "fast, seen in a three-quarter view from the front-side so both the bike's two wheels and his face read, standing up on the "
    "pedals, leaning forward over the handlebars, grinning, hair blown back.",
    BILL_PROP.format(**{"with": " on his bicycle", "and": " including both wheels"}))
person("hill-kid-bike-girl",
    "The character: a girl about nine years old, dark brown skin, two short braids, a VIOLET bicycle helmet and a violet T-shirt "
    "(her defining colour), yellow shorts, high-top sneakers. Pose: riding a violet bicycle with a white basket and long ribbon "
    "streamers flying from the handlebars, seen in a three-quarter view from the front-side so both wheels and her face read, "
    "sitting up proudly, one hand off the handlebar giving a big wave.",
    BILL_PROP.format(**{"with": " on her bicycle", "and": " including both wheels"}))
person("hill-carwash",
    "The character: Mr. Okonkwo, a broad friendly Nigerian dad in his late forties, very dark skin, short greying hair, a ROYAL-"
    "PURPLE polo shirt (his defining colour) with a wet patch, grey shorts, rubber flip-flops. Pose: washing a car that isn't "
    "there: in one hand a green garden hose pointed sideways, a clear arc of water spraying out of it, in the other hand a big "
    "fat yellow sponge dripping suds, a few soap bubbles floating, laughing. No car, no bucket.",
    BILL_PROP.format(**{"with": " with his hose", "and": " including the whole arc of water"}))

# ---- Row B: the mower, the yard sale ----
person("hill-mower",
    "The character: Mrs. Fairbanks, a wiry energetic woman in her early sixties, pale and sunburnt, short grey hair under a white "
    "sun visor, big sunglasses pushed up, a HOT-MAGENTA zip-up tracksuit top (her defining colour), white shorts, white sneakers. "
    "Pose: pushing an old-fashioned green push lawnmower in front of her at a march, seen in a three-quarter view so the whole "
    "mower and her face read, both hands on the handle, elbows pumping, chin up, very determined.",
    BILL_PROP.format(**{"with": " with her lawnmower", "and": " including the whole mower"}))
person("hill-yardsale-seller",
    "The character: Mr. Ruiz, a round jolly man in his fifties, Mexican-American, a big grey moustache, a straw fedora, a CORAL-"
    "PINK Hawaiian shirt with white hibiscus flowers (his defining colour), a canvas money pouch belted at his waist, cream "
    "trousers, sandals. Pose: a salesman's flourish, holding up an old brass table lamp with a fringed shade in one hand to show "
    "it off, the other palm open toward it, eyebrows raised, as if saying 'a real bargain'.")
person("hill-yardsale-browser",
    "The character: a young woman in her early twenties browsing a yard sale, tall and slim, pale with lots of freckles, a "
    "copper-red bob, round tortoiseshell glasses, a FOREST-GREEN corduroy jacket (her defining colour) over a cream T-shirt, a "
    "long brown skirt, ankle boots, a canvas tote bag over her shoulder. Pose: weight on one leg, holding an old vinyl record "
    "sleeve up close to her face with both hands, peering at it with intense concentration, lips pursed.")

# ---- The lemonade stall, the bus ----
person("hill-lemonade-kid",
    "The character: a boy about seven years old, South Asian, a too-big floppy straw sun hat, a LEMON-YELLOW T-shirt (his "
    "defining colour), blue shorts, sandals. Pose: a little salesman, leaning forward and holding out a paper cup of pink "
    "lemonade toward the viewer with both hands at arm's length, a huge gap-toothed hopeful smile. No stall, no table, no sign.")
person("hill-bus-driver",
    "The character: a big kindly bus driver in his late fifties, a ruddy face, a bushy ginger-grey beard, a peaked driver's cap and "
    "a MAROON uniform jacket with brass buttons (his defining colour) over a white shirt and a dark tie, grey trousers, black "
    "shoes. Pose: SITTING at the wheel of a bus that isn't there (draw no seat, no wheel, no bus), facing the viewer, knees "
    "forward, both hands held out in front at chest height gripping an invisible steering wheel, turning his head a little to "
    "give the viewer a warm wink.",
    BILL_PROP.format(**{"with": " sitting", "and": ""}))

for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts:", " ".join(P))
