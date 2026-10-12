# Night 2, redesigned: the house, hide-and-seek and the escape

House agent, 2026-10-12. Answers the creator's rejection of Night 2 (`docs/REDESIGN.md`): the broken geometry, the few
small rooms, being found costing nothing, the half-baked escape (phasing, the corridor that never stretched, walls that
don't run, falling costing nothing). Everything below is built and tested in dotnet; **none of it has been seen in Unity
yet** (see "Not verified" at the end).

![The floors](house-plan-floors.png)

`house-plan-floors.png` (the four storeys, cut 1.2 m above each floor), `house-plan-site.png` (the lot) and
`house-escape-route.png` (the escape along its route) are drawn from the code itself:
`dotnet run -c Release --project tools/house-plan -- plan.json` then
`uv run --with pillow python tools/art/vh_house_plan.py plan.json docs/redesign`.

## 1. The house

A real family house, in its own frame (front door at the origin, facing the street at -z), placed on the hill at World's
`HillAnchors.HouseOrigin` (24, 9.6, 122), yaw 0. 31 rooms, 678 m2 indoors, 522 parts, every one with a collider.

| Storey | Floor | Rooms |
|---|---|---|
| Basement | -2.8 m | the rec room (ping-pong, the old couch), the furnace room, the basement corridor, Elias's workshop (his bench, his radio), the laundry |
| Ground | 0 | the front hall (the stairs, the photo wall), the living room (the fire, the window seat), the dining room (the table set for four), the kitchen, the pantry, the den (Elias's desk), the back hall (the coats, the basement stairs), the cupboard under the stairs; the mudroom and the garage (the car under its cover) |
| Upper | 3.2 m | the upper hall, Theo's room (his desk, THE BAD DREAM on his bookcase, the height marks on his door frame), the bathroom, the guest room made up for her, Elias and Lena's room with its walk-in closet and en-suite, the attic stairs |
| Attic | 6.1 m | the attic (the family's stored life, the dormer), the top of the attic stairs, Lena's observatory in the tower (the telescope at the round window) |
| Outside | | the front garden and path, the back yard (the oak and Theo's treehouse, the sheets on the line, the trampoline, the shed), Rowan Street |

**Why a treehouse:** it earns its place as the hide-and-seek's farthest hiding place and where today's paper is, folded
into Theo's hat (the memory that ties Night 1's Saturday to this one).

**How it's built** (`Runtime/Core/DreamEscape/DreamHouse*.cs`, engine-free): a kit of walls with openings, slabs with
holes, stairs (steps, each a collider), furniture boxes and props, all as oriented boxes. `HouseLint` checks every pair
for overlap (separating axis); the only allowances are stair ends into floors (<= 0.4 m), ramps against walls
(<= 0.12 m), roof slabs meeting (<= 0.45 m) and the chimney through the roof. **The lint finds 0 overlaps**, checked in
dotnet (`DreamHouseTests`), by `tools/house-plan` (exit code 2 on any) and logged on every rebuild (`JAMKIT_HOUSE_LINT`).
`DreamHouse.WalkClear` and the walking-graph tests prove every room and every hiding place is reachable on foot.

**What it looks like** (`Dream/Editor/House/`): the Look agent's house-interior lighting (`VhLook.ApplyHouseInterior`),
Look's materials and metre UVs; 50 Poly Haven CC0 models (sofas, beds, tables, chairs, the fridge, lamps, shelves...)
fitted into their boxes (`Dream/House/house-assets.json`, `LICENSES.md`) and 16 CC0 surfaces (floorboards, tiles,
plaster, brick, carpet); built where no free model fit (the wardrobes, the washer and dryer, the telescope, the
umbrella stand, the treehouse, the oak via `VhLookTrees`). The wallpaper is the creator's oxblood stripe. 35 lamps, each
room lit as a room (`HouseFurnishing.Lamps`, with reflection probes). Lena and Theo are the cast's cut-outs through
People's `DreamCutoutView` (REDESIGN 7: only Nadia and Elias are 3D in the dream).

**Collision:** every solid part has a box collider on a layer she collides with (the old glass went through her: it was
on Ignore Raycast; now it blocks). **67 walls run** (every wall longer than 1.5 m, marked `MovementSurface.runnable`, the
player's `onlyMarkedWalls`).

## 2. Hide-and-seek (5-8 minutes)

**The rules** (`HideAndSeekRound`):
- She sits at the fourth chair: her place card is the first memory (1 of 8 on the counter, top of the screen, with three
  red pips for the catches).
- Theo counts at the table, face in his arms: 20 s, then 15 s, then 12 s after each catch. He peeks once, like a kid
  ("I wasn't peeking! ...Much.") and remembers which floor he saw her go to.
- She finds the memories (look at one and press Interact). Each becomes an evidence card for the argument.
- **Getting found costs:** the house takes back the last memory she found and hides it again in one of its other places
  (the one farthest from where she was caught, its unused places first). Theo counts again where he found her, and he
  gets quicker: faster walk, more hiding places checked, quicker to see. Mags says what it costs.
- **The third catch ends the round** with whatever she holds ("...You win.").
- **Every memory found:** Nadia's line (the creator's, word for word, with its two endings by the Despair route), and
  Theo comes straight for her; his finding her ends the round.
- Then his question, the creator's: **"Is Dad going to be sad?"** (Yes / No, I promise / say nothing; kept in the save,
  `vh_theo_dad_sad`; his reply and his parting line follow the answer).
- Theo's room is "the base" until she's found three memories (unless she told Elias about Theo's team).

**Theo** (`TheoBrain`, engine-free, multi-floor): searches room by room the way a ten-year-old would: rooms he hasn't
tried yet first, the floor he glimpsed her on, the room a noise came from, his favourites. In each room he looks round
and checks some of the hiding places (opening cupboards, pulling back curtains), not all. He **hears** her (running,
landing, sliding, a wall run; through walls less, through floors at 35 %) and goes to look ("I heard that!"). He
**sees** with the stealth kit's sight and meter (eyes at 1.2 m; crouching, the dark and cloth slow it): the eye at the top
right fills while he sees her, full is "found you". He calls into rooms, giggles, hums, says "Not here..." (lines in
`DreamHouseLines`). He walks a 234-point graph; stairs are links. No allocation per tick.

**17 hiding places:** the cupboard under the stairs, the pantry, the coats in the back hall, behind the den curtains,
Theo's wardrobe, behind the shower curtain, the guest wardrobe, the walk-in closet, the en-suite shower, behind the
furnace, behind the washing, behind the car, the attic boxes, Lena's observatory, the shed, behind the sheets on the line,
the treehouse.

**The memories and their cards** (each in its first place, then the two places the house re-hides it):

| # | Memory | Where | Re-hidden in | Card |
|---|---|---|---|---|
| 1 | Her place card at the fourth chair | dining room | hall, Theo's room | `mem_fourth_chair` |
| 2 | The fridge calendar ("Pancakes!" on Sundays, a key on Thursdays) | kitchen | den, laundry | `mem_calendar`; with the Saturday paper (found tonight or carried from Night 1) it becomes `ded_saturday_off` |
| 3 | The photo of the four of them at the lake | front hall | guest room, rec room | `fact_photo` |
| 4 | Theo's height marks, stopping at ten | Theo's door frame | kitchen, basement corridor | `mem_height_marks` |
| 5 | THE BAD DREAM (Elias in scrubs walking out of the door; Theo and Lena at the table) | Theo's bookcase | rec room, guest room | `mem_bad_dream` |
| 6 | Today's paper, folded into Theo's hat | the treehouse | front garden, garage | half of `ded_saturday_off` (not a card itself) |
| 7 | Elias's radio: the same forecast, word for word | the workshop | kitchen, garage | `ded_same_saturday` |
| 8 | Lena's star wheel, set to a Thursday | the observatory | their room, the attic | `mem_lena_thursdays` |

**For Battle:** `mem_lena_thursdays` is a new card (Lena's Thursdays); `mem_bad_dream` now means the redrawn picture
(Elias in surgeon's scrubs leaving, not a monster).

**What she told Elias at his bedside (Day 2) changes the house** (`DreamChanges`, wired): "the lake" puts the photo on
the hall wall already, found; "Theo's team" opens Theo's room from the start; "Lena" has her waiting at the window,
colder; "just sit" makes her gentler and gives Nadia 5 s more each count.

**How long:** the dotnet round (`TheWholeRoundPlaysOut`: a sprinting bot that ignores stealth going for the nearest
memory) is caught three times in 111-202 s with 5 of 8 memories. A player who crouches, hides and reads the lines should
take the 5-8 minutes asked for, but **that's an estimate, not measured**.

## 3. The argument

As before: Elias never comes down ("His plate's still there. Cold."), she questions Lena with the cards she holds (the
Battle agent's real-time battle via `DreamHouseGame.StartBattle`, id `lena_night2`). Unsure or Condemns sets the escape.

## 4. The escape (2-3 minutes)

The house throws her out: a fever-dream run through rooms that can't fit in the house, unexpectedly long for how small
it looks (562 m of route). Built upright far below the house (local (0, -260, 400): the hill must leave that space
empty). The house folds shut behind her (a rubber-banded pursuer that only catches someone who stalls).

![The route](house-escape-route.png)

| Room | Route (m) | What happens |
|---|---|---|
| The dining room tips | 0-38 | the table slides, the room rolls 90 degrees: the walls are the floor ("The walls are the floor--") |
| The hall turns over | 38-136 | the hall rolls under her as she runs it (a framed camera roll), the photo wall underfoot |
| The stairs flatten | 136-189 | the stairs flatten into a slide; off its lip in slow motion |
| The corridor | 189-275 | **the corridor that gets longer as she nears its end** (below) |
| Theo's room, upside down | 275-315 | his glow stars under her feet |
| The kitchen, come round again | 315-451 | rolled -90: the kitchen again, bigger |
| The attic, open to the stars | 451-540 | no roof: the night sky (Lena's stars), over a gap in slow motion |
| The last hall | 540-562 | the front door the right way up; her umbrella by it |

**The corridor** (`StretchCorridor`): the creator's beat, now real. Its last two bays are the same; when she reaches
10 m from the door slower than 11 m/s, the game moves her (and the fold) back one 20 m bay under a dolly zoom: the door
has pulled away. The first approach always stretches (the reveal: "It's... longer."). Fast enough (a slide into it, a
wall kick, slide hops down its 4 % slope) and the door bursts open ("Got you."). Each stretch lets the fold 2.5 m nearer.
Lena unsure: after three stretches Theo holds the door ("In here! It's a shortcut, I promise!"). Lena condemning: it wants
11.5 m/s and the fold gains 4 m a stretch. Mags coaches it line by line.

**Cinematic:** title cards ("The house tips", "The corridor"), slow motion at the four lips (0.3-0.4x), the hall's
framed roll, the corridor's dolly zoom, a held look back at the window (`HouseCameraFx`), and each room's light, fog and
sound faded in as she enters it; the movement camera's own speed lean and FOV underneath.

**Timing:** the dotnet autopilot runs it in 61-63 s at 20-300 fps with two stretches, never falling. A player at a run,
stretching more and losing speed in the rolls, should take 2-3 minutes (an estimate). Sprinting alone never opens the
corridor: the house closes on her (`HouseEscapeTests`).

**Out onto the hill:** through the last hall's front door she comes out of the dream's door onto Chapel Row
(`HillAnchors.EscapeExit` (24, 15, 153), the hill's middle band) in the rain. The umbrella works as in Night 1
(`docs/DREAM-ESCAPE.md`). A beat out of the door, a held look back: **Lena in the tower's round window**, watching her
go. At the end of the rain path, Night 2 ends (`DreamHouseGame.Escaped`; standing alone, a new visit).

## 5. Falling costs: thrown out of the dream

Falling (below the escape's floor, or off Chapel Row) or the fold catching her **throws her out**:
- slow motion, the camera rolls, Mags: "Nadia-- I've got you. I'm pulling you out.";
- black, then the booth (`DreamHouseLines.ThrownOutBooth`: "His heart went through the roof when you fell. I had to.
  That's a night we don't get back. And they'll remember how you left.") and the card "Thrown out of the dream. Despair
  +1. Lena's mind is made up against you. Night 2's ending is lost.";
- **the costs:** Lena's outcome hardens to Condemns (`vh_lena_condemns`, `Chain.SetLenaOutcome("Condemns")`), the flag
  `vh_n2_thrown_out`, no umbrella, no Lena at the window; the chain ends the night with `Chain.ForceOut(reason)` (Day's
  cost: Despair a step, the crash call the next day, one fewer of Mags's nights). There are no checkpoints back.

## 6. The lines

Every line is written, in `DreamHouseLines` (`Packages/com.vibejam.jamkit/Runtime/Core/DreamEscape/DreamHouseStory.cs`):
the street, the door, the hall breathing, pancakes, Theo's count, peeks, calls into each room, hearing, losing her,
giggles, "found you" (three), the catch's cost (Mags), each memory's thought, the creator's all-found line and its two
endings (kept word for word), Theo's question (the creator's) with the three answers and replies, Elias not coming down,
Lena unsure / condemning, Theo's parting by her answer, the house tipping, each escape room, the corridor's stretches,
breaking through, Theo's door, the umbrella, Lena at the window, thrown out (the fall, the booth, the costs), and the
bedside variants. No placeholders. Day's booth can play `ThrownOutBooth` when `vh_n2_thrown_out` is set.

## 7. Where things are

- Core (dotnet-tested): `Runtime/Core/DreamEscape/` `DreamHouse.cs` (types and kit), `DreamHousePlan.cs` (the plan),
  `DreamHouseFurnish.cs` (furniture, spots, memories, Theo's nodes, lamps), `HouseLint.cs`, `HideAndSeekRound.cs`,
  `TheoBrain.cs`, `HouseWalker.cs` (the autopilot's legs), `HouseEscape.cs`, `DreamHouseStory.cs` (phases and lines).
- Unity: `Dream/Scripts/House/` (`DreamHouseGame` and its partials `.HideAndSeek`, `.Escape`, `.Drive` (chapters,
  autopilot, recorder); `TheoView`, `HouseMemoryView`, `HouseHud`, `HouseCameraFx`); `Dream/Editor/House/`
  (`DreamHouseBuilder`, `HouseGeometry`, `HouseFurnishing`, `HouseMaterials`, `HouseMeshes`, `HouseEscapeBuilder`,
  `DreamHouseExterior` (World calls `Build(Transform)` to put the house on the hill), `DreamHouseCapture`).
- Assets: `Dream/House/` (Poly Haven models and surfaces, CC0; `house-assets.json`, `LICENSES.md`).
- Tests: `core-tests/DreamEscape` (`DreamHouseTests`, `HouseEscapeTests`), EditMode `DreamHouseEditorTests`, PlayMode
  `DreamHousePlayTests`.

## 8. Captures

`tools/house-capture.ps1` (through the memory cap; `-Rebuild` rebuilds the scene first):
- **stills** (`DreamHouseCapture.StillsFromCommandLine`), 1920x1080 at her eye height (1.5 m), into
  `out/house-captures/<time>/` with `shots.txt`: 25 of the house (Rowan Street, the porch, every room, THE BAD DREAM from
  the desk, the height marks, the attic, the observatory, the basement, the garage, the yard, the treehouse, the house from
  the garden), 4 hide-and-seek moments (Theo counting; Theo in the kitchen seen from the pantry; Theo in his room seen from
  his wardrobe; a memory's glow in the dark basement), each escape room from its start plus the corridor's door and the
  umbrella, and Chapel Row (the path in the rain; Lena at the window);
- **recorded runs** (`DreamHouseCapture.RecordFromCommandLine`, `-houseRecord <dir> -houseRecordRun all|hideandseek|escape`):
  the scene plays itself on the autopilot, a JPG every 0.5 s of game time into `out/house-runs/<time>/hide-and-seek/`
  and `escape/`, with `run.txt` (memories, catches, stretches, by time).
`tools/dream-capture.ps1` shoots the same house stills and then the dream escape's.

## Not verified

None of this has been opened in Unity: the look (model scale and axes, URP Lit setup of the downloaded textures, the
lamps), the gable and roof windings, ramp and OBB collider orientation, frame time with this many objects (35 lamps,
175 model parts), the escape's lighting and fog far below, the recorder in play mode, and Theo's tuning against a human.
The lint, the reach and the timings above are dotnet's.
