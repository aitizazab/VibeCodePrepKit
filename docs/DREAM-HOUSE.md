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
- Lena and Theo are placeholder doodle cut-outs. Drop style-E art in as `Templates/VisitingHours/Art/Dream/dream_lena.png`
  and `dream_theo.png`, and the build uses it.

## The sequence

| Part | What happens | How it plays |
|---|---|---|
| **1. The way in** (chapter *The house*) | The front garden, the house front, the open door. The house plays with her: the hallway **stretches away** as she enters, and its floor is gone. A door **half open onto a slide** takes her down into the kitchen. | Run the gold-striped hallway wall (14 m) and kick off. Slide under the door's 1.15 m lintel and down a 3 m slide. A fall puts her back at the door. |
| **2. Pancakes** | Lena at the stove, Theo at the table set for four (placeholder lines). | Lines, then the game begins. |
| **3. Hide-and-seek** (chapter *Hide and seek*) | Theo counts to ten at the table, then seeks. She hides and finds **five memories**: the height marks on his door frame, the fridge calendar, the fourth chair, his drawing, the photo wall. | The stealth kit (`docs/STEALTH.md`): his cone and meter, the screen bar and word, and the memory count at the top. Hiding spots: the pantry and his wardrobe (closets), the curtains and the coats (bush-like), low cover, shade. Found before the last memory: "Found you!", and he counts again; the memories she found stay found. |
| **The last memory** | Nadia's line, from the creator's notes: "I'm not sure if there's anything left to find. I could let Theo find me here..." | Hooked to `CollectibleCounter.AllFound`. Its ending depends on the route (below). |
| **4. The argument** (chapter *The argument*) | The next time Theo finds her, the game is over and Elias hasn't come. She questions Lena. | The battle isn't built. A placeholder prompt is the debug choice: **1** (pad X) Lena is unsure, **2** (pad Y) Lena condemns Nadia. |
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
3. **The argument battle itself** (`ARGUMENT-BATTLES.md`). It replaces the debug prompt; the outcome goes to
   `DreamHouseGame.Choose`.
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
| Unity PlayMode (`DreamHousePlayTests`) | The way in leads to pancakes, then hide-and-seek. Theo finds a player standing in the open and counts again. The memories count down and the last one speaks. Condemns is kept and brings the point of no return nearer. The autopilot escapes under the umbrella. |

## Known gaps

- **The house escape is short:** about 15 s for the autopilot, against the 75 s in the design notes. Tipping the hub
  itself (animating the rooms over) isn't done: a white-out and a shake cut to the house on its side.
- **The seeker walks one flat floor**, the ground floor. Theo never follows her up or out.
- **No sitting down:** the pancakes are lines while she stands at the table.
- **Lena doesn't watch from the window yet** ("From now on Lena watches from the window").
