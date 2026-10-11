# Battle sprites (the argument battle's opponents)

The opponent's sprite: `Argument/Resources/ArgumentArt/<who>_<expression>.png`, 1024 x 1536 RGBA, the figure
filling the height (see `Argument/Scripts/ArgumentArt.cs`). Every expression of one opponent shares one pose,
framing and scale, so they swap without a jump.

## Every opponent gets
- **antagonistic**: the pose the battle starts with: the body, not only the face, showing how they stand
  against Nadia (Lena: arms folded, notebook tucked under her arm, chin up, looking down at you).
  Same framing scale as the other expressions, so the switch to them doesn't change her size.
- neutral, angry, rattled, softened, hurt (+ pleading if cheap).

## How Lena's were made (repeat for the next opponent)
1. Crop the character's chosen dream billboard to three-quarter length (head to mid-thigh), 16:9.
2. Nano Banana Pro redraws it crisp on flat blue (#0050ff): `lena-sprite-neutral.txt`.
3. Each expression is an edit of that one drawing that changes only the face (`lena-sprite-<expression>.txt`);
   the antagonistic pose is an edit that changes the pose and keeps the head's size and place
   (`lena-sprite-antagonistic.txt`).
4. Cut out: `FableAnimationTests/art-tools/sprite-key.py` (blue key, despill, 3 px choke, one shared crop),
   then onto the 1024 x 1536 canvas, bottom flush, scaled to the height.
5. Battle looks (exploration, 2026-10-11): `FableAnimationTests/art-tools/sprite-treat.py <mode>`:
   graphite, accent (graphite + the defining colour), screentone, screentone-red, torn (paper cut-out).
   Image processing on the cut-out, so all expressions stay pixel-aligned.
   Round 2 (the creator liked the screentone; a lone red patch looked odd; push the hair too):
   `FableAnimationTests/art-tools/sprite-ink.py <inkhair|whitehair|duo|pattern>`. Not installed until picked.

Attach only the character's own chosen sheets; never another design (it gets copied).
