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
