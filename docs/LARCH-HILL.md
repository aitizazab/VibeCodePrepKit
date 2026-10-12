# Night 1: Larch Street on a hill (history)

**Superseded on 2026-10-12 by the redesign: [docs/redesign/world.md](redesign/world.md)** (Larch Hill, one hill for
Nights 1-3: the switchbacks through the back yards, the house low on the hill, St. Brigid's on the summit, the Rewind).
What follows is the first hill (the four routes and the beacon) as it was built, kept for its history.


This is the build of docs/NIGHTS-1-2.md, "Night 1: The Street" (2026-10-13 overnight). The creator didn't love the
flat deep block ("it didn't reveal how large the setting was") and offered "work vertically and set it on a hill".
So layout A's four routes are laid on a slope, with Walt's bike shop at the bottom by the creek and the Lasname house
with its tower at the top.

- **Core data:** `Packages/com.vibejam.jamkit/Runtime/Core/DeepBlock/LarchHill.cs` (the hill's boxes, the routes,
  the story's spots and the views) and `LarchStreetStory.cs` (the phases, the memories and deductions, the neighbours'
  loop, every line).
- **The scene:** `Assets/JamKit/Templates/VisitingHours/Dream/` holds `Scripts/Street/DreamStreetGame.cs`,
  `DreamStreetPresenter.cs`, `Editor/Street/DreamStreetBuilder.cs`, `DreamStreetTemplate.cs` and
  `DreamCaptureStreet.cs`. It is built by "JamKit/Rebuild Shell Scenes" (or "JamKit/Dream Street/Rebuild Scene")
  as `DreamStreet`, the name the chain looks for first after "LarchStreet".
- **Tests:**
  - `core-tests/DeepBlock` (`LarchHillTests`): the reach, the autopilot on every route at 20, 60 and 144 fps, the gate,
    the views and the story.
  - Unity: `DreamStreetEditorTests` and `DreamStreetPlayTests`.
- **Stills:** `tools/street-capture.ps1` (nine views: the bottom, the middle, the top, the rooftops, the ravine's culvert
  and falls).
- **The plan** for review pages: `dotnet test core-tests/DeepBlock/DeepBlock.Tests.csproj --filter "Name=ExportThePlan"`
  writes `larch-plan.json` next to the test assembly; `uv run python tools/art/night1-hill-plan.py <json> <out.svg>` draws
  it (north up, heights shaded, the routes over it). The review spec is `docs/art/vh-night1-hill-review.json`.

## The hill

World axes: X runs east, Z runs north (up the hill), Y is up. The foot is at Y 0.

| Level | Height | Z | What's there |
|---|---|---|---|
| The far bank | 0 | -44 to -8 | Where she arrives, facing the whole climb; the neighbours' house and the loop in its garden |
| The creek | bed -2.5 | -8 to 0 | 8 m wide: the first double jump |
| The foot | 0 | 0 to 34 | Walt's bike shop, its yard (Saturday's paper on the bench) and gate, the foot square, the creek lane, four houses, road leg 1 |
| Terrace 1 | 12 | 34 to 68 | Four houses with porches and gardens, a neighbour at his grill, road leg 2 |
| Terrace 2 | 24 | 68 to 102 | Four houses, road leg 3 |
| The top | 36 | 102 to 176 | The lookout (4.8 m), the knoll (40) with the house and its tower, the barbecue |

**The bottom.**
- The creek runs past the shop. A 5 m flood wall makes the shop's apron the only landing; a grate and the bank fence
  stop her going round.
- The shop's garage door is half open (1.15 m under it: the first slide). Its back door opens into the yard, where
  Walt stands in the gate to the hill. That gate is a switch that stays shut until his argument.
- The view from the far bank takes in the whole climb, the tower at the top. The flood wall is drawn 1.2 m high so it
  doesn't hide the foot (the first stills, 2026-10-12); the dream holds it at 5 m above that (an invisible wall).
- The world's edges have walls and hedges you can see: the ravine's east wall and the west wall at each level's height,
  a tall hedge behind the top, a hedge along the far bank, the far bank and the creek past the hedge.

**The four ways up**, all from the foot square behind Walt. The times are the dotnet autopilot's at 60 fps:

| Route | Line | Moves | Length | Autopilot |
|---|---|---|---|---|
| **The road** | Leg 1 east (to 12 m), the east hairpin north, leg 2 west (to 24), the west hairpin, leg 3 east onto the top | Two switchbacks, 10° at most | 308 m | 32.5 s |
| **The garden stairs and back lanes** | Three hedged flights (29-35°) on alternating sides, each ending on a back lane with a 1.5 m garden wall to climb | 3 mantles | 91 m | 10.3 s |
| **The rooftops** | A crate, the bike shed (4.25 m), then nine row houses stepping up the hill, each roof 3.6 m above the last across a 3 m alley | 9 double jumps + mantles: the chain uphill | 81 m | 11.6 s |
| **The creek ravine** | The hub corridor, east along the creek lane, a 2 m drop into the ravine, the culvert under the east hairpin, then up 28 falls (1.36 m, 4 m apart) | A 10 m slide, 28 mantles | 254 m | 30.2 s |

**The way in** (arrival to Walt, 41 m) takes 4.0 s: the creek's double jump, then the slide under the garage door.
Every route also finishes at 20 and 144 fps.

**The landmark.** The house's tower stands on a 4 m knoll at the top. From the far bank it is in plain sight, and the
lookout sees the far bank, the shop and the roofs at the foot.
- On the road, the stairs and the ravine you walk under 12 m retaining walls, so the tower itself is hidden from most
  of the way. It shows from 18% of the road, 10% of the stairs, 8% of the rooftops and none of the ravine.
- **The beacon is main's call** (made overnight, swappable). A soft column of the dream's gold light, 110 m tall,
  stands up out of the tower. With it the house is found from 91% of the road, 76% of the stairs, 100% of the rooftops
  and 74% of the ravine. The rest is the bottoms of the cuts and right under the walls. The test asks for at least
  70% on every route.

## The night

Every line is a **placeholder**, marked so on screen. The lines quoted from the plan are main's.

1. **Arrival.** Mags: "You're lighter in here." and "The creek. Jump, then jump again in the air."
   - In the neighbours' garden on the far bank, right where she arrives, the neighbours' loop runs: the sprinkler
     sweeping, the ball thrown, the dog fetching, and "Lovely Saturday for it!". It is `NeighbourLoop`, a fixed 24 s
     afternoon whose start point is fixed per night, so Night 2 replays it exactly with `new NeighbourLoop(1)`.
   - Heard twice (within the far bank), the line is `mem_neighbours_loop`.
2. **The apron.** Mags: "The door's half up. Run at it and crouch: slide under."
3. **The yard.**
   - Walt waves. That gives the memory `mem_walt_alive`, and with Day 1's memorial card (`fact_effects`) the deduction
     `ded_walt_dead` ("Walt died in 2015. This is memory.").
   - Standalone, the card counts as known (`assumeDay1Facts`). In the chain it is read from the dream journal.
   - Saturday's paper lies on the bench in his yard (`mem_saturday_paper`, the id Night 2 reads). With the loop that
     makes `ded_same_saturday` ("He's living one Saturday, over and over.").
   - Both halves are below Walt's gate (main, 2026-10-13: his optional premise is part of the tutorial, so it has to be
     breakable on the first pass). A core test and a PlayMode test check it.
4. **Walt.** "Your brother's having a good day. Let him have it. Come back tomorrow."
   - His battle is the argument agent's (`ArgumentHost.BeginBattle("walt_night1", ...)`, its context from
     `DreamBattles.Context`). It gets the journal ids she carries; truths and keepsakes won go into the journal.
   - The outcome maps Convinced to persuaded, Pressed or ComposureLost to pressed, and anything else to heard out.
   - Under the autopilot, or if the battle can't start, a debug choice stands in: 1, 2 or 3; F7 is heard out.
   - In every outcome he steps aside and the gate opens. The outcome is kept in the save entry `vh_walt_night1`.
5. **The climb.**
   - On terrace 1, by the east hairpin, a neighbour at his grill chats as she passes (one line).
   - Walt's ledger on the workbench is optional (`mem_walt_ledger`).
6. **The top.** Elias (his dream cut-out, `dream_elias`, eyes at 1.66 m: a little taller than Lena) is at the barbecue on the side lawn by the knoll and goes in as she
   arrives.
7. **The garden gate.** No chase:
   - She is held at the gate. Through the window Theo sets the table and Elias stands at the stove.
   - He looks up and sees her. Lena steps between them and takes him inside.
   - The door closes and the porch light goes out.
   - Mags: "His heart's up. That's enough for tonight. You found the house."
   - The screen fades. `mem_the_house` goes into the journal and the flag `vh_night1_done` is set.
   - Then `DreamStreetGame.NightEnded` fires. Without a listener, the night starts again.

**Saving.** Everything learnt goes into the save's shared dream journal (`DreamJournal`, the save entry
`vh_dream_journal`): `fact_effects`, `mem_walt_alive`, `ded_walt_dead`, `mem_saturday_paper`, `mem_neighbours_loop`,
`ded_same_saturday`, `mem_walt_ledger` and `mem_the_house`.

**Debug and presenter.**
- Chapters: The creek, Walt, The road, The garden stairs, The rooftops, The creek ravine, The top. Each route chapter
  starts at the route's start with Walt already aside.
- The debug skip jumps to Walt, settles his argument, then jumps to the top.
- `DreamStreetGame.Drive(route)` runs the course autopilot.
- Falling off the hill puts her back at the last place reached: the arrival, the yard, the foot square or the top.

## What's open

- **Placeholders.** The shop's sign has
  no lettering.
- **The chain.** The night says it's over through `DreamStreetGame.NightEnded`. The chain's runner listens to the house
  and the escape, so it needs that event too, or this game can call `Chain.Done()` once the chain's code is committed.
- **The stealth kit's values are untouched.** Night 1 has no guards. The back lanes are the space Night 2+ stealth
  uses.
