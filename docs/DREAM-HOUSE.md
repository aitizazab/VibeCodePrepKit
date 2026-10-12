# Night 2's house: a playable greybox

The house sequence the creator endorsed in their notes on `STORY-PROSE.md` (2026-10-11, "Sunday pancakes" and "I like the
gameplay sequence"). Nadia comes to the family home in the dream, sits down to pancakes, plays hide-and-seek with Theo
while she looks for memories, questions Lena when Elias doesn't come, and is thrown out as the house tips over. She goes
out the front door into the rain.

It's a standalone scene in the dream escape's folder and assemblies:
- **JamKit > Use Template > Dream House**, then Play. Switch back with **Use Template > Visiting Hours**.
- Batch: `-executeMethod JamKit.DreamEscape.EditorTools.DreamHouseTemplate.UseFromCommandLine`.
- It's built by Rebuild Shell Scenes, or by **JamKit > Dream House > Rebuild Scene**.

**Placeholders:**
- Every line of dialogue is a placeholder, marked `[placeholder]`. The dialogue is the creator's to write, in
  `DreamHouseLines` (`Runtime/Core/DreamEscape/DreamHouseStory.cs`).
- **Art (2026-10-11, pass 1):** Lena and Theo are style-E dream cut-outs, `Templates/VisitingHours/Art/Dream/dream_lena.png`
  and `dream_theo.png` (Lena is the creator's pick, round 4-a), with a white sticker border made by
  `tools/art/vh_dream_cutout.py`. The story pictures are drop-ins too, used when present (the plain cards stay otherwise):
  the photo wall `dream_pic_photo_1` to `_6` (top row 1 to 3, left to right), `dream_pic_calendar` (the fridge) and
  `dream_pic_theo_drawing` (his desk).
- **Art pass 2 (2026-10-11), all visual over the unchanged blockout** (colliders, names and routes as they were):
  - real furniture (Kenney's Furniture Kit, CC0) fitted into the gameplay boxes, whose renderers are switched off: the
    counter, stove and sink, the fridge, the dining table and four chairs (facing the table), the sofa, Theo's bed and
    desk, the study desk (`Dream/Editor/House/DreamHouseDressing.cs`), drawn in the new `JamKit/Dream/PastelMesh`
    shader (the dream's pastel steps and crayon grain with a pencil outline);
  - wallpaper and floorboards: an optional world-projected pattern in `JamKit/Dream/Pastel` (`_TexStrength`, 0 by
    default, so nothing else changes), from `Art/Dream/dream_pattern_wallpaper.png` and `dream_pattern_floorboards.png`;
  - the two front doors: white frames, a doorstep, coral panelled double doors swung open the way she goes;
  - the umbrella as real meshes (`Dream/Editor/DreamProps.cs`): a furled canopy and a J-hook handle by the door, an
    open canopy of eight scalloped panels over her (shared with the dream escape).

  Still boxes: the walls, stairs, closets, curtains, the house front and the tipped house's rooms.

## Architecture and level design (2026-10-12)
The creator, on the decorated house: "there's simply too much space, and ... the wall design is bad, both as in the walls
just being an uninteresting box for every single room, and only one floor that we explore (when we need the attic too),
and the space doesn't feel exciting to explore." So the house was redesigned as architecture, in core `DreamHouse`
(the plan, the rooms, the pieces, Theo's graph, the spots, the memories), with the finish in
`Dream/Editor/House/DreamHouseRooms.cs` and the decoration in `DreamHouseDecor.cs`.

**The plan** (15 x 12 m; the old ground floor was 24 x 28 m). Plans: `tools/house-plan` (dotnet, writes JSON from the
core) and `tools/art/vh_house_plan.py` (draws them).
- **The main floor:** the kitchen and dining room (open plan; the slide from the way in lands in it), the living room
  (a bay window with a window seat, a fireplace in a chimney breast), a narrow hall (2.4 m), the pantry between the
  kitchen and the hall, the parents' room with its en-suite bath, Theo's room. Ceilings 2.3 to 3.0 m by room.
- **The attic** (under a gable roof, its ridge 3.4 m, the eaves down to 0.8 m): up steep stairs in their own slot,
  from a narrow door in Theo's room; down through a hatch into the hall. Lena's observatory at the west end (a round
  window full of stars, her telescope, chair, star charts, notebook). **Approved** by the creator (2026-10-12), with a
  raised roof section over it: Lena is a stargazer. Whether it ties to her Thursdays and the calendar's keys is
  undecided. And the family's stored life to the east (Theo's old crib, Elias's old bike, boxes, a rocking
  horse, the Christmas box). It's always night in Lena's attic: the one place the dream exaggerates.
  **Two spaces, not one big floor** (second round, same day): a wall of stored things (stacked boxes, an old wardrobe
  under a dust sheet) runs across the attic, stepped down under the roof, with one gap hung with old sheets on a line.
  The storage side holds the stairs and the hatch (the loop never needs the observatory); the observatory is the room
  you find through the sheets, fairy lights along the ridge leading there. Boxes fill the low eaves on the storage
  side, so the walkable attic is the band under the ridge; the chimney stack rises through it at the east end.
  `TheAtticIsAVerticalLoopTheMovementCanDo` checks the wall and the gap.
- **Walls with character** (no room is a box): the bay; the chimney breast; an archway and a pass-through over a half
  wall between the kitchen and the living room; doorways of different widths with real headers; the low cupboard
  under the stairs; each room's own wallpaper over wainscoting (tiles in the kitchen and bath), chair rails, skirting,
  picture rails and cornices in the taller rooms; the attic's sloped ceilings, rafters, ridge beam, collar ties, dormer.

**The level-design rules used** (from the usual practice: landmarks, loops, gating and teasing, compression and
release, rewarding curiosity; sources: Totten, *An Architectural Approach to Level Design*
([Routledge](https://www.routledge.com/An-Architectural-Approach-to-Level-Design/Totten/p/book/9780815361367)); a
2026 thesis on denial and reward ([SMU Guildhall](https://scholar.smu.edu/guildhall_leveldesign_etds/44));
[CDM: universal level design techniques](https://blogs.thecdm.ca/techspeak/?p=2488)):
- **Landmarks:** a focal point per room (the fireplace, the table, the bed, the bunk); the attic's starry window as
  the house's one big landmark.
- **Loops:** the kitchen to the living room through the archway, or round by the hall; the pantry's two doors; the
  vertical loop (up the stairs from Theo's room, along the attic, down the hatch into the hall).
- **Teasing:** the open hatch in the hall's ceiling shows the attic's starlight from below before she finds the way up;
  the bulb at the top of the stairs.
- **Compression and release:** the narrow hall and the steep, narrow stairs open into the attic's storage side; the
  gap in the sheets opens into the observatory under the starry window.
- **Rewarding curiosity:** Theo's drawing is on top of his bookcase (climb his desk); the cupboard under the stairs;
  the attic's storage to climb.
- **Light leads the eye:** the fireplace, the pendants over each room's group, the stair bulb, the starry windows.
- **Movement:** designed to the current movement and stealth values (unchanged): the stairs at 46 degrees (the slope
  limit is 50), the desk a 0.75 m mantle, door headers above standing height, the cupboard's door at crouch height.

**Hide-and-seek** stays on the main floor (Theo's walking graph); the attic is one big hiding spot he checks from the
foot of its stairs, and its floor blocks his sight (as do the ceilings and walls: `TheosSightStopsAtWallsCeilingsAndTheAttic`).
Hiding spots: the pantry, the window seat, the coats, the cupboard under the stairs, the bath curtain, Theo's wardrobe,
the attic.

**Finish options (round 3, 2026-10-12; nothing chosen yet).** The creator asked for darker wallpapers, a ceiling
pattern, a "bezel" where the walls meet the ceiling, and higher ceilings. The options are capture-only
(`DreamCaptureVariants`, `tools/dream-capture.ps1 -Variants all`): each is applied to the opened scene in memory and
never saved. Eight wallpapers with matching wainscots (tiles from `tools/art/vh_house_patterns.py`, `Art/Dream/dream_wall_*`):
forest damask, oxblood regency stripe, navy sprig, plum trellis, teal botanical (Morris-like), umber fans, and two mid-tones
(sage pinstripe, dusty rose sprig). Ceilings: pressed tin, plaster roses round the pendants, beams, coffers, a starry
paper. Cornices: a cove, a stepped crown, a dentil band. Ceilings +0.4 m and +0.8 m (a preview: everything from the
ceilings up moves up, the walls grow). The attic's underside: painted or dark-stained boards.

**Higher ceilings and the movement** (the values stay): a ground jump lifts her head to 2.95 m (1.7 m tall, 1.25 m
jump), a double jump to 3.95 m. Today only the living room (3.0 m) clears a ground jump; at +0.4 m the kitchen, living
room, parents' room and Theo's room do (not the hall, bath, pantry or bay); at +0.8 m every room does; no room clears a double jump. Sight: Theo's view between rooms is
cut by walls, headers and ceilings, and the doorways keep their height (the headers grow upward), so his sightlines
don't change; only the attic moves further up. The cost is the attic stairs: today they climb 3.3 m over 3.2 m (46
degrees, the limit for the test is 48); +0.4 m needs about 3.6 m of run, +0.8 m about 4.0 m, so the north rooms get
deeper or the stairs get a landing. The hatch drop grows by the same amount (it's one-way already).

**Eye heights** (the creator: "why are we taller than Lena?"). Nadia's eye is 1.5 m above her feet (MoveDef.eyeHeight;
her capsule is 1.7 m). The stills were taken from 1.6-1.65 m until round 3; they now use 1.5 m. The cut-outs are sized by
their eyes: the drawings have big heads (Lena's eyes 18.6% of the card down from its top, Theo's 26.2%), so at the old
card heights (1.72 m, 1.38 m) Lena's eyes were at 1.40 m, under Nadia's. Now Lena's eyes are at 1.60 m (a 1.72 m
woman's; card 1.97 m) and Theo's at 1.20 m (a ten-year-old's; card 1.63 m). `DreamHouseBuilder.LenaEye`/`TheoEye`.

**Round 4 (2026-10-12): the picks built in, the observatory raised, the house from outside.**
- **The finishes** (the creator's picks from the options page): the oxblood regency stripe in every room with a walnut
  wainscot (the kitchen's and the bath's tiles stay; the stretched hallway and the tipped house use the same paper),
  exposed beams on every ceiling, the stepped crown, painted tongue and groove in the attic. `DreamCaptureVariants`
  still shows the alternatives (`-Variants all`).
- **Ceilings +0.4 m, built for real** (`DreamHouse.CeilingRaise`): every room's ceiling and the attic's floor go up
  (rooms 2.7 to 3.4 m); doors keep their 2.1 m, so the headers grow; the windows are taller; the pendants hang on longer
  cords; the picture rails sit 0.42 m under the ceiling. The attic stairs climb 3.7 m, so the north rooms are 0.4 m
  deeper (`HubZ1` 84.4, the ridge over z 78.2) and the stairs run 3.6 m (46 degrees, as before). A ground jump (her head
  at 2.95 m) now clears the kitchen, living room, parents' room and Theo's room, not the hall, bath, pantry or bay.
- **The observatory** (the creator: "a cut-off in the sloped roof so it remains high-roofed in the observatory"): the
  attic west of x = -2.9 rises to a flat boarded ceiling 4.2 m over its floor, full height from wall to wall, with a
  skylight over the telescope, clerestory windows in its long walls and, where it meets the sloped roof, a wall from the
  slopes up to its ceiling. The dormer moved over the top of the attic stairs. Tested: a double jump fits under its
  ceiling everywhere, even by its walls, and the ceiling stops anything higher (`TheAtticIsAVerticalLoopTheMovementCanDo`).
- **The front door** is at the hall's east end (a red double door, like the way in's), the coats on the hall's north
  wall beside it.
- **The house from outside** (`DreamHouseExterior`): the front garden looks at the house's real shape, the plan turned
  so the hall's east end faces the garden (x = hall z - 79.4, z = 8.2 - hall x; the right side 0.8 m wider than the
  plan so the stretched hallway's wall stays inside). The living room's two tall windows either side of an outside brick
  chimney (its stack and pots above the roof), the bay round the left side under a steeper roof, Theo's small window, a
  shingled front gable, the dormer on the right slope, and at the back the observatory's raised block with its
  clerestories, the round window and a little dome over the skylight (its windows show the attic's night). Plinth,
  clapboard, corner boards, fascia, bargeboards, gutters and downpipes, shutters, a porch on posts with a lantern.
  Tall hedges close the garden at the house's corners (colliders, so the routes outside don't change); a lawn and trees
  behind. Every window outside is one of the rooms' windows (`DreamHouseRooms.HouseWindows`).

**Round 5 (2026-10-12): the observatory as a tower, bigger garden-side windows, colours for the oxblood.**
- **The tower** (the creator: "more like a tower ... dark polished wood, or a dark stone"): the observatory is now a tower
  over the middle of the attic's west end (5 m across, `ObsZ0`..`ObsZ1`), its ceiling 5 m over the attic's floor; the
  sloped roof runs on either side of it. Outside it rises from the ground at the back past the ridge, in dark stone (the
  capture's `-Variants tower` shows dark polished wood), under a steep slate roof with a brass finial.
- **The crescent skylight** (the creator's example, a moon for Lena's stars): 2.6 m across, cut into the tower's ceiling
  over the telescope, with a light well, a rim and a pane of night sky; it shows on the tower's roof outside too.
- **The garden-facing wall's windows** are as big as the other side's (the parents' room 1.8 x 1.6 m, Theo's 1.6 x 1.6 m,
  the bath 1.2 x 0.9 m); Theo's front window too (0.9 x 0.7 m). Inside and outside share `DreamHouseRooms.HouseWindows`.
- **The "holes" by the front door** were the wall's jambs flickering against the door frame (two faces in one plane);
  the frame now wraps the jambs, and the outside walls no longer share a plane with the front wall.
- **Colours for the oxblood and walnut:** the slide into the kitchen (and the attic stairs' ramp) walnut, curtains
  ochre, the window seat teal, rugs navy (kitchen), mustard (living room), teal (hall runner), cream (parents' room),
  the shower curtain off-white.

## Interior notes (art pass 5, 2026-10-12: a lived-in home)
The creator: "it's not at all furnished... for the space Elias spent most of his life in, his memories of it are rather
bland." The house is now decorated as a home (`Dream/Editor/House/DreamHouseDecor.cs`), decoration only: no colliders,
nothing on Theo's walking paths, the hiding spots, the routes or the run walls.

The rules used, from feng shui and ordinary interior-design advice (guidelines, not tested findings):
- **A focal point per room**, and the main seat facing it with a solid back: the sofa faces the new fireplace; the table,
  the bed and the desk anchor the others. (Feng shui's "command position": the main seat backed by a wall, seeing the
  room. [livingetc](https://www.livingetc.com/advice/feng-shui-command-position),
  [Homes & Gardens](https://homesandgardens.com/interior-design/feng-shui-furniture-placement))
- **Furniture in groups, each anchored by a rug** (at least the front legs on it): the sofa, coffee table and armchair;
  the dining table; Theo's room; the desk. ([Planner 5D](https://planner5d.com/blog/how-to-decorate-a-living-room/),
  [Castlery](https://www.castlery.com/ca/blog/guide-to-layering-rugs))
- **Clear walkways of about a metre** where people walk: here, Theo's walking graph and the player's routes.
  ([livingetc](https://www.livingetc.com/advice/living-room-feng-shui))
- **Light in layers**: a pendant over each room's group, table and floor lamps lower down, the fire and candles.
  ([Planner 5D](https://planner5d.com/blog/how-to-decorate-a-living-room/))
- **Soft and round against the hard edges, and things at different heights**: rugs, a teddy, plants, fruit, a mirror
  over the mantel, shelves of books.

What a memory of this home holds: the fireplace (mantel clock, candles, the siblings' photo, a mirror), an armchair with its
lamp, bookcases, plants, the fridge with the children's drawings and notes, the counter's kettle, toaster, mugs and fruit,
a clock, a long hallway runner with the brother's and sister's childhood photos, Theo's teddy, toy chest and toys.

## The sequence

| Part | What happens | How it plays |
|---|---|---|
| **1. The way in** (chapter *The house*) | The front garden, the house front, the open door. The house plays with her: the hallway **stretches away** as she enters, and its floor is gone. A door **half open onto a slide** takes her down into the kitchen. | Run the gold-striped hallway wall (14 m) and kick off. Slide under the door's 1.15 m lintel and down a 3 m slide. A fall puts her back at the door. |
| **2. Pancakes** | Lena at the stove, Theo at the table set for four (placeholder lines). | Lines, then the game begins. |
| **3. Hide-and-seek** (chapter *Hide and seek*) | Theo counts to ten at the table, then seeks. She hides and finds **five memories**: the height marks on his door frame, the fridge calendar, the fourth chair, his drawing, the photo wall. | The stealth kit (`docs/STEALTH.md`): his cone and meter, the screen bar and word, and the memory count at the top. Hiding spots: the pantry and his wardrobe (closets), the curtains and the coats (bush-like), low cover, shade. Found before the last memory: "Found you!", and he counts again; the memories she found stay found. |
| **The last memory** | Nadia's line, from the creator's notes: "I'm not sure if there's anything left to find. I could let Theo find me here..." | Hooked to `CollectibleCounter.AllFound`. Its ending depends on the route (below). |
| **4. The argument** (chapter *The argument*) | The next time Theo finds her, the game is over and Elias hasn't come. She questions Lena. | **The argument battle prototype** (`ArgumentHost`, `DreamLenaDemo`, `docs/ARGUMENT-PROTOTYPE.md`) runs over the house while the house waits. When its end card closes, its stance decides: **Condemns** means Lena condemns Nadia; **Amenable** or **Unsure** means she's unsure. **Debug skip:** F7 ends it with Lena unsure, F8 with Lena condemning Nadia. The autopilot (smoke, attract, tests) skips the battle as unsure, and with `useArgumentBattle` off the old prompt's 1 / 2 (pad X / Y) choose. |
| **5. The house tips** (chapter *The house tips*) | The house kicks her out either way. She runs the house on its side: the hallway's floor is a wall to run, the doorframes are walls to kick between (5 m apart), the staircase is a slide. | The house **folds shut behind her** (a rubber-banded pursuer: 12 m gap, catches only after 0.75 s under 2 m). Caught or fallen, she's back at the last checkpoint, running. |
| **6. The front door** (chapter *The front door*) | The closed umbrella glows beside the door. Out of the door it rains. | The same as the dream escape's (`docs/DREAM-ESCAPE.md` "The umbrella and the rain"). The dream tool takes the umbrella. Without it the rain soaks her, and going on is the **Despair** choice (`vh_despair`). |
| **Out** | At the end of the rain path the light swells. | The story takes over (`DreamHouseGame.Escaped`), or the visit loops back to the front garden. |

The autopilot clears the escape, from the tip-over to the end of the rain, in about 15 s.

## The routes (Despair)

- **Going on into the rain without the umbrella** sets the save's `vh_despair` (the Despair route, which ends with the
  Long Night). Read it with `DreamEscapeGame.Despair`.
- **Lena condemning Nadia** sets `vh_lena_condemns` (read it with `DreamHouseGame.LenaCondemned`). It makes the Despair
  route easier to reach:
  - the point of no return is **3 m** into the rain instead of 8;
  - the rain soaks her in **3 s** instead of 5.

  Both flags persist across visits.
- **The last memory's line** has two endings from the creator's notes, both kept in `DreamHouseLines.AllFoundEndings`:
  1. "but what does it matter, when he's not even real?"
  2. "it would be nice to see his smile again." (The notes have "it would be nice to his smile again"; I added "see".)

  Which one is the Despair route's is the creator's open question. `DreamHouseLines.DespairEnding` picks it: 0 for
  now, the more hopeless one. The line uses the Despair flag as it stands when the last memory is found.

## For the creator to decide

1. **The two endings of the last-memory line:** which is Despair's.
2. **What "condemns" should change** beyond the rain. Now it only brings the point of no return nearer and soaks faster.
3. **The argument battle's Night 2 content.** Wired in: the prototype's Lena battle plays here, and its stance maps to
   the house's two outcomes in `DreamHouseGame.OutcomeOf`.
4. **All the dialogue.**
5. **The memories' contents:** what each shows. The fridge calendar's keys are the open question from the notes.
6. **Whether the doors should also lead back to the kitchen** (the house playing harder). Now the house plays by stretching
   the hallway and opening onto the slide.

## Where things are

| What | Where |
|---|---|
| The house as data (pure, dotnet-tested) | `Runtime/Core/DreamEscape/DreamHouse.cs` (the pieces on a `LabCourse`, the route, the stealth layout, the memories, the front door's `DreamRainSpot`) |
| The story (phases, lines, the routes) | `Runtime/Core/DreamEscape/DreamHouseStory.cs` (`DreamHouseStory`, `DreamHouseLines`) |
| The umbrella and the rain (shared with the dream escape) | `DreamRain` (core), `Dream/Scripts/DreamUmbrella.cs` with any `IDreamRainHost` |
| Runtime | `Dream/Scripts/House/`: `DreamHouseGame`, `DreamHousePresenter`, `DreamFaceCamera` |
| The builder and the template switch | `Dream/Editor/House/`: `DreamHouseBuilder`, `DreamHouseTemplate`; the cut-outs in `DreamArt` |
| Generated (not committed) | `Dream/Scenes/DreamHouse.unity`, `Dream/DreamHouseGameConfig.asset`, the house's materials in `Dream/Materials/` |

## Tests

| Where | What |
|---|---|
| `core-tests/DreamEscape` (`DreamHouseTests`) | The autopilot gets through the way in at 20 to 300 fps and with jitter. It escapes the tipping house into the rain at 20 to 300 fps and with jitter, never caught. Every escape checkpoint runs to the next. The route fits the reach. Theo's graph reaches every room, spot and memory. Theo finds a player standing still (in three rooms) and one in the pantry. Every memory can be picked up. The story's phases. Both endings, and every line marked a placeholder. Condemns makes Despair easier. The umbrella sits beside the front door. |
| Unity EditMode (`DreamHouseEditorTests`) | The chapters and attract script; the scene, the preset and Build Settings; the wiring (Theo, the spots, light, the five memories, the hallway end, the fold, the umbrella, Lena); the route fits the real tuning and controller. |
| Unity PlayMode (`DreamHousePlayTests`) | The way in leads to pancakes, then hide-and-seek. Theo finds a player standing in the open and counts again. The memories count down and the last one speaks. Condemns is kept and brings the point of no return nearer. The argument battle, played through by its own autopilot, decides the outcome by its stance. The autopilot escapes under the umbrella. |

## Known gaps

- **The house escape is short:** about 15 s for the autopilot, against the 75 s in the design notes. Tipping the hub
  itself (animating the rooms over) isn't done: a white-out and a shake cut to the house on its side.
- **The seeker walks one flat floor**, the ground floor. Theo never follows her up or out.
- **No sitting down:** the pancakes are lines while she stands at the table.
- **Lena doesn't watch from the window yet** ("From now on Lena watches from the window").
