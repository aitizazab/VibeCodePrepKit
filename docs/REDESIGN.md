# The redesign (2026-10-12): Nights 1-2 to a shipping bar

The creator played Day 1 → Night 1 → Day 2 → Night 2 for the first time on 2026-10-12 and rejected it as shallow.
This doc is the shared brief for every agent working on the redesign. It overrides older docs where they disagree
(NIGHTS-1-2.md, LARCH-HILL.md, DREAM-HOUSE.md, DREAM-ESCAPE.md stay as history and for story facts). The creator's
own writing (STORY-PROSE.md notes, their battle lines) still outranks everything.

## 1. What the creator said (their words, trimmed)

- "It's too shallow. The only interesting part was Lena's battle, and that still has a few flaws. It was mostly just
  talking, it doesn't feel like the gameplay choices really came in as much. I even made some wrong choices, and it
  ended in Lena being unsure, but there was absolutely no gameplay. And the stretches of which there were, were both too
  short. It was more of just an argument, than an argument battle."
- "The skybox is bland, and it doesn't feel like it's golden hour on a Saturday at all."
- "The three routes up the hill are all trivially simple, it's not a maneuverability challenge. And more importantly,
  progression up the hill isn't granular, you just start at the bottom with Walt, and then get up to on top of the hill
  with the Lasnames' house. The hospital above it doesn't even exist. And in Night 2, that hill structure gets completely
  abandoned to just spawn you in a flat plain in front of the house."
- The house: "terribly broken geometry. The opening hallway has things butting in, and all of the hide-and-seek portion
  happens in a few small rooms. Getting found doesn't do anything, it just restarts the hide-and-seek."
- The escape: "hilariously half-baked. Nothing looks good, you phase through so many of the objects, and the main part of
  feeling like you're running down a corridor that always seems to get just a bit longer when you get close to the end,
  doesn't happen at all. Most of the walls you can't even wall-run on, but more importantly, you literally phase right
  through them, and again, falling off doesn't cost anything ... you're not even forced out of the dream, it just resets
  you at a checkpoint."
- The days: "a joke. The space is small, uninteresting to explore, and even interacting with everything takes less than a
  minute. And the choice of talking to him or just sitting down in the chair on Day 2 highlights it especially. There's no
  purpose to this interaction, it doesn't change anything in the story ... And it's just text, with nothing changing on
  screen, like a short animation of sitting down, holding Elias' hand, and getting up after the player puts in an input."
- Bugs: "Looking down seems to sometimes show you your own head model, instead of your chest and legs." "Quitting to title
  seems to continue the music from the previous scene into the title." "Longer strings of text would clip in the boxes of
  the argument battle." "It's unclear how spoken things progress when you click a box, because the changed text just pops
  up, instead of each line popping up at a time, giving the model space to react to it properly, instead of keeping an
  extreme face for an extended [time] while you consider the next argument."
- "Above all, the game just looks super choppy, not an artistic choice of artstyle."
- The order: "Overhaul the main hill scene completely, using it for Nights 1 and 2 (also 3 when we get there), so that it
  has proper progression up the hill, and the house isn't that high on the hill, since the house is the tutorial for the
  movement and stealth. Make it look like a triple-A game, with cinematic lighting, an intricate skybox, detail fidelity
  with the ground, the objects and buildings and their textures, and the people. Have agents judge work done
  antagonistically, keeping everything important about level design, and artistic rendering in mind."
- The method: "if I have to write out prose for every single scene's dialogue, come up with and describe the visuals and
  audio, and create interesting level design and describe it to you on my own, we're going to be here for months ...
  Don't let anything constrain you on delivering a quality viewing experience, and gaming experience, abandon any pretense
  that's blocking or complicating things. Redesign it if you must."

## 2. How we work now

- **We design and write everything.** Level design, set dressing, lighting, audio, and every line of dialogue that the
  creator hasn't written. Write in the voice their existing lines set (Lena's battle text is the reference). Never leave
  "[placeholder]" text in a shipped scene; write the real line. The creator edits by playing.
- **Nothing is done until an adversarial judge passes it.** Each milestone: the builder commits, main runs Unity
  (captures, a recorded autopilot run, the QA lints, the frame-time probe), then a judge agent who did not build it tears
  it apart against section 3's bar. The builder fixes, and the loop repeats until the judge scores every category 8/10 or
  more with no blocker. Then the creator plays.
- **Tests are regression guards, not quality.** Keep them green, but never report "green" as "good".
- **Real assets over code-drawn primitives.** CC0 libraries (Poly Haven, ambientCG, Quaternius, Kenney; licences in
  JAM-ASSETS-AND-PRESENTATION.md), AI-made models (Tripo) and AI art (Gemini) are all fair game. A box with a flat colour
  is never a finished object.

## 3. The bar (what judges score, 1-10 each)

1. **Lighting and sky:** reads unmistakably as golden hour on a late-summer Saturday: a low warm sun, long soft shadows,
   a rich sky with lit clouds, haze in the distance, bounce light, bloom on highlights. References: Life is Strange: True
   Colors (Haven Springs), Firewatch, Psychonauts 2, It Takes Two.
2. **Material and detail fidelity:** PBR materials with normal maps everywhere; no flat untextured surfaces; edges
   bevelled or trimmed; ground with variation (cracks, grass edges, leaves, kerbs, drains); buildings with trim,
   gutters, window depth, interiors behind glass; props at real-world scale; no repetition visible at a glance.
3. **Art direction:** stylized AAA (high fidelity, stylized shapes and colour, not photoreal), coherent across the scene;
   the dream is warm and nostalgic with something subtly wrong.
4. **Level design:** clear goals and landmarks, granular progression (you always see the next step and the far goal),
   challenge that escalates after teaching, optional harder lines with rewards, no dead ends without a reason, pacing with
   rests and vistas.
5. **Movement challenge:** routes demand chained moves (wall-run → jump → slide → mantle) with timing; failure is
   possible and costs something; skilled play is visibly faster.
6. **Physical integrity:** no interpenetrating meshes, every visible solid has a collider, no phasing, every wall the
   player would expect to run on is wall-runnable, no stuck spots, no holes to fall through.
7. **Consequence:** every fail state and every choice changes something the player can see or feel.
8. **Presentation:** camera, animation and audio sell each beat; no text pops, no frozen extreme faces, no clipped text.
9. **Performance:** 1080p at 60 fps or better on this RTX 3050 laptop at the default quality level (report the frame time).

Blockers (any one fails the milestone): phasing through a wall, an untextured placeholder in view, clipped text, a fail
state with no cost, a soft-lock, a frame time over 25 ms.

## 4. The hill: one map for Nights 1-3

Larch Hill is one continuous hillside neighbourhood, used by Nights 1, 2 and 3, each night covering a higher band.

- **The foot (Night 1):** the creek and its bridge, Walt's bike shop and yard (his bike with the patched rear tyre, his
  ledger with 41 identical lines: "Sat. E., rear tyre, flat. Patched."), the foot square, the first streets.
- **The Lasname house, low on the hill:** about a quarter of the way up. Its block (yards, fences, garages, roofs, the
  tree) is the movement tutorial on Night 1; its inside is the stealth tutorial on Night 2.
- **The middle band (Night 2's escape and after):** steeper streets, stairs between terraces, retaining walls,
  rooftops, a school or church, the bus line. Night 2's escape bursts out of the house into this band.
- **The top (Night 3):** the commercial strip and, crowning the hill, the hospital where Elias worked: the landmark
  visible from everywhere, catching the last sun. Built as a full exterior now; its interior comes with Night 3.
- **Saturday life, on the loop:** neighbours mowing, washing a car, a yard sale, kids on bikes, barbecue smoke,
  sprinklers, the ice-cream truck's tune, a distant little-league game. All of it repeats exactly (the loop is the
  story's first clue).
- **The gold beacon goes:** the hospital is the landmark now.

## 5. Ownership (one owner per area; don't edit another owner's files)

| Agent | Owns |
|---|---|
| Look | Lighting, sky, post-processing, URP settings, the shared material/asset library (`Assets/JamKit/Templates/VisitingHours/ThirdParty/**`, a new `Look/` folder), the soundscape (ambience) |
| World | The hill: layout, terrain, buildings, set dressing, Night 1's flow and challenge, the hill bands for Nights 2-3, Walt's yard, the first-person body (the head-when-looking-down bug) |
| House | The Lasname house interior, hide-and-seek, the escape (the stretching corridor), fail states and being forced out of the dream, Night 2's props (incl. Theo's drawing) |
| Battle | The argument battle's gameplay, UI and text delivery, Lena's and Walt's lines (incl. L5, the `~` drawl) |
| Day | Day 1 and Day 2 (the hospital ward), the bedside scene and its consequences, the chain, title and music hand-offs |
| People | 3D characters (Nadia, Elias, Lena, Theo, Walt, neighbours, ward staff): models, rigs, animation sets, the swap from cut-outs |

## 6. Unity runs

Agents can't run Unity (the subagent rules). Use the dotnet suites in `core-tests/` and the UnityApi / UnityLayerEditor /
UnityLayerPlayer compile checks. When a milestone is ready: commit only your own paths, push, and hand back the exact
Unity stages to run (captures, recorded runs, builds). Main runs them one at a time and sends you the results and the
judge's findings.
