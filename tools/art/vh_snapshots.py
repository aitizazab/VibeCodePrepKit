# The opening's two personal photos, redone 2026-10-11 to read as real candid phone snapshots (the creator: the old ones
# looked "very AI generated"): the framed photo on Nadia's bedside table, and Elias's picture behind the incoming call.
# Two registers each: a real photograph, and the waking style-E illustration composed like a snapshot (to fit the 3D room).
# Writes Flow prompts into docs/art/prompts/snapshots/.   uv run python tools/art/vh_snapshots.py
import os
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "art", "prompts", "snapshots")
os.makedirs(OUT, exist_ok=True)
HEAD = "Aspect ratio: 16:9 landscape.\n\n"
NOTEXT = "No text, no letters, no numbers, no logos, no watermark, no signature, no date stamp anywhere in the image."

REF = ("Image 1 is Nadia's model sheet and Image 2 is her older brother Elias's model sheet. Use them ONLY for who these two "
       "people are (faces, hair, his short beard, her dark hair); they are a few years younger here. Do not copy the drawing style, "
       "the poses or the clothes.")
REF_ELIAS = ("Image 1 is Elias's model sheet. Use it ONLY for who he is (face, short dark hair, short beard); do not copy the "
             "drawing style, the pose or the clothes.")
PHOTO = ("A real, unedited smartphone photo, taken casually by someone who was there, the kind of picture people keep: NOT a "
         "professional photo, not a portrait session, not a stock photo. Slightly off-level, imperfect framing, something cut off "
         "at an edge. Ordinary mixed light (warm indoor bulbs against cool light from a window or a screen), uneven white balance. "
         "A little softness, slight motion blur on whatever moved, visible phone-sensor noise in the shadows and mild JPEG "
         "compression. Natural, unretouched skin with texture and pores, no glossy or airbrushed skin, no studio lighting, no "
         "symmetric pose, no creamy artificial background blur: the background is only slightly soft and full of real, ordinary "
         "clutter. Hands natural, five fingers each, partly hidden is fine.")
DRAWN = ("Drawn in the game's waking-world style: grounded, muted and stylised like a literary graphic novel, clean shapes with "
         "soft flat shading and thin line work, NOT a photograph. But composed exactly like a candid phone snapshot someone "
         "actually took: slightly off-level, imperfect framing, something cut off at an edge, ordinary mixed light, a lived-in "
         "cluttered background, nobody posing. Hands natural, five fingers each, partly hidden is fine.")
FRAME = ("The moment: a brother and sister a few years ago, on a small, cluttered apartment balcony or at a crowded kitchen "
         "counter late on a summer evening. He (mid-thirties, short dark hair, short beard, rumpled t-shirt) is mid-laugh, head "
         "tipped back, half turned away; she (late twenties, dark brown hair loosely tied back, a hoodie) is elbowing him and "
         "laughing with her eyes squeezed shut, a bottle of beer in her other hand. Neither is looking at the camera. Behind them: "
         "plants in pots, a string of cheap fairy lights, a drying rack with laundry, a neighbour's lit window. Keep the two of them "
         "close together within the middle half of the wide frame (the picture will be cropped to a small upright photo frame).")
CALLER = ("The moment: a contact picture a sister took of her brother. He (mid-thirties, short dark hair, short beard, tired "
          "eyes) sits in a hospital staff break room at night in navy scrubs, caught mid-bite eating noodles out of a takeaway "
          "carton with chopsticks, glancing up at the camera with a deadpan, mildly annoyed look. Harsh flat fluorescent light from "
          "above, a vending machine glow at one side, a corkboard with pinned papers (nothing readable), a microwave, a half-drunk "
          "paper cup of coffee. Keep him within the middle third of the wide frame, his head in the upper part (the picture will be "
          "cropped to a tall phone screen, with the caller's name drawn over the top and buttons over the bottom).")

# Round 2 of the framed photo: the first round spread the two across the wide frame, so no upright crop could hold both.
FRAME2 = ("The moment: a brother and sister a few years ago, on a small, cluttered apartment balcony late on a summer evening. "
          "They are squeezed together: he (mid-thirties, short dark hair, short beard, rumpled t-shirt) has hooked his arm around "
          "her neck in a playful headlock from behind and is laughing with his head tipped back; she (late twenties, dark brown "
          "hair loosely tied back, a grey hoodie) is laughing with her eyes squeezed shut, half-heartedly pushing his arm, a bottle "
          "of beer in her other hand. Neither is looking at the camera. Their two heads are close together, one above the other, "
          "and both of them fit inside the CENTRAL THIRD of the wide frame (the picture will be cropped to a small upright photo "
          "frame), with real balcony clutter either side: plants in pots, cheap fairy lights, a laundry rack, a neighbour's lit "
          "window.")

P = {
    "snap-frame2-photo": HEAD + "\n\n".join([REF, PHOTO, FRAME2, NOTEXT]) + "\n",
    "snap-frame-photo": HEAD + "\n\n".join([REF, PHOTO, FRAME, NOTEXT]) + "\n",
    "snap-frame-drawn": HEAD + "\n\n".join([REF, DRAWN, FRAME, NOTEXT]) + "\n",
    "snap-caller-photo": HEAD + "\n\n".join([REF_ELIAS, PHOTO, CALLER, NOTEXT]) + "\n",
    "snap-caller-drawn": HEAD + "\n\n".join([REF_ELIAS, DRAWN, CALLER, NOTEXT]) + "\n",
}
for k, v in P.items():
    with open(os.path.join(OUT, k + ".txt"), "w", encoding="utf8", newline="\n") as f: f.write(v)
print(len(P), "prompts")
