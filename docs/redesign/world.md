# World: Larch Hill, one hill for Nights 1-3 (the World agent's level design)

Status (2026-10-12): **milestone 1 built, not yet seen in Unity.** The whole hill is laid out as data and drawn in code in
CC0 PBR materials; Night 1's critical path is proven end to end by the dotnet autopilot at 20, 60 and 144 fps; the anchors
are published; the shared geometry lint exists; the captures and the recorded run are ready for main to run. Nothing here
is "good" until a judge has seen the captures.

![The plan](world-plan.png)

`world-plan.png` is drawn from the core data (`dotnet test core-tests/DeepBlock/DeepBlock.Tests.csproj --filter
"Name=ExportThePlan"`, then `uv run --with matplotlib python tools/art/hill-plan.py <json> docs/redesign/world-plan.png`).
North (up the hill) is up; the coloured lines are Night 1's legs (0-5) and Night 2's middle-band line (6).

## The idea

The creator's notes: the old hill was three trivial routes, no granular progression, the house at the top, no hospital,
and Night 2 abandoned the hill for a flat plain. They asked for one hill for Nights 1-3, proper progression, the house low
on it ("the house is the tutorial for the movement and stealth"), and their Dunwall-distillery note: a dense space whose
safe traversal is the interest, balanced around our move set.

So Larch Hill is a **hillside of terraces climbed in switchbacks**. Each band is a contour street with a wall above it;
the critical path runs along a band one way through its back yards, lanes and roofs, then climbs to the next band and
comes back the other way. You always see the next step (the next wall, roof or tree) and the far goals: **the Lasname
house** (low on the hill, Night 1's goal) and **St. Brigid's Hospital** crowning the summit (the landmark from everywhere,
Night 3's goal). The gold beacon is gone.

## The bands (Y is the ground; X east, Z north)

| Band | Height | What's there | Nights |
|---|---|---|---|
| The far bank | 0-3 | The arrival on a rise; Creek Lane South; the Lindqvists' front garden (the loop: sprinkler, dog, ball, "Lovely Saturday for it!") | 1 |
| The creek | bed -4.6 | 8 m wide in a 4.6 m stone channel, water 1.2 m deep; the old footbridge out since the flood; the ford steps downstream | 1 |
| The foot | 0 | **Walt's bike shop** (apron, half-open garage door, counter with Saturday's paper, workbench with the ledger, the patched bike), **Walt's yard** (Walt on his low wall), the creek-side terrace, the Foot Square (the Route 9 terminus), the Mill Steps; **the back way**: the Pruitts' (the pool), the Okafors' (a lawn of kids' things), the Hendersons' (the raised garden, the old stable, the slot) | 1 |
| Mill Street | 4 | A contour street over the foot (a 4 m wall and a tall fence); Row B's houses; **Larch Green** (a pocket park: the view up to the house); the allotments (kept low so the arrival sees the house over them) | 1 |
| Back Lane B | 5 | The lane behind Row B west of the green: lock-up garages (the roof line), the **ditch**, Mr. Bell's workshop, **the Old Mill** and its arch with the **shutter on the loop's timer**, the loading bay, the scaffold, **the old oak** (and its treehouse) | 1 |
| Rowan Street | 9 | The house's street; a tall hedge along its edge over the lane. **The block party** (gazebos, bales, a removal truck by a dug-up trench, a bouncy castle, a car and a skip, a trench across the street, a stage); **the Lasname house** (`HillAnchors.HouseOrigin`), its gate, Eli's barbecue in the side garden, the big maple | 1 (the Rewind), 2 (the start) |
| The middle band | 15 / 22 / 29 / 36 | Four terrace streets (Chapel Row, School Lane, Bell Row, Upper Row) with retaining walls between them, house rows, **the Larch Steps** (the spine straight up the hill), **St. Anne's** (bell tower and spire), **Larch Hill Primary** and its playground, **Hill Road** (the Route 9 bus line up the east side, flat at each terrace, steep between; the bus waits at Bell Row) | 2 (the escape), 3 |
| The top | 42-46 | **Crest Road's shops** (bakery, diner, florist, pharmacy, laundromat, news, hardware: awnings, signs, lit interiors, flats over), the forecourt, **St. Brigid's Hospital** (main block, tower with the sign and the helipad, entrance canopy, ambulance bay); its interior comes with Night 3 | 3 |

## Night 1, beat by beat (the critical path)

The dotnet autopilot's times are a skilled run; a first-time player takes 5-10 times longer with the lines, the looking
and the failures. Total path ~480 m; the night runs about **14-18 minutes plus Walt's argument**.

| Time (first play) | Beat | Taught / tested | In view | Rest / vista |
|---|---|---|---|---|
| 0:00-0:45 | **The arrival** on the far bank's rise. Mags: "You're in... He's up that hill somewhere." The Lindqvists' loop behind her (heard twice: `mem_neighbours_loop`). | - | Walt's shop across the creek, the house above its roof, St. Brigid's on top | **Vista**: the whole hill |
| 0:45-1:15 | **The creek** (8 m) | **Double jump** (Mags teaches). Fail: into the water; wading is slow (no double jump or wall-run, 2.6 m/s); out by the ford steps downstream, back up the bank: ~20 s | Walt's apron, the shop's half-open door | |
| 1:15-1:40 | **Walt's garage door** (1.15 m gap) | **Slide** (Mags teaches) | The paper on the counter, the bench, the patched bike | Inside the shop: a quiet room |
| 1:40-2:20 | **The shop**: Saturday's paper (`mem_saturday_paper`: with the loop, `ded_same_saturday`); the patched bike; Walt's ledger (optional: out only when the bedside's "Walt's shop" topic put it there) | - | | |
| 2:20-~7:00 | **Walt's yard**: "Mr. Abernathy. Sitting on his wall" (`mem_walt_alive` + Day 1's card: `ded_walt_dead`); his argument (the battle agent's `walt_night1`) | The argument tutorial (Mags coaches) | | |
| 7:00-7:30 | **Walt gets up**: "Over the wall, the way you kids always went." | **Mantle** (Walt teaches: his 1.6 m low wall) | The Pruitts' pool against Mill Street's wall | |
| 7:30-8:30 | **The Pruitts' pool** (12 m, against a 4 m wall) | **Wall-run** (Mags teaches). Fail: into the pool; steps at the shallow end, back to the patio (~6 s) | The pergola beam | |
| 8:30-9:00 | **The fallen pergola** (1.15 m beam) | **Slide** again, straight off the wall-run (the first combination) | | |
| 9:00-9:40 | **The Hendersons' wall** (2.4-2.9 m over the Okafors' lawn) | **Jump and grab** (jump, then mantle) | The old stable and the slot | |
| 9:40-10:40 | **The slot** behind the stable (3.5 m wide, 4.55 m up to the street) | **Wall-jump** (Mags teaches: "Kick off one into the other"). Fail: back to the slot's floor, retry | Mill Street above | |
| 10:40-11:30 | **Mill Street**: Mr. Okonkwo washing his car, kids on bikes, the allotments. Mags: "There's the house... that's St. Brigid's on top." | - | **The house** over the green, the hospital | **Rest + vista** (back down over the foot to the creek) |
| 11:30-12:00 | **Up Larch Green** to the lane | - | The house, closer | Rest |
| 12:00-13:30 | **The garage roofs**: crates up, a 3 m gap, a 5 m gap, the **ditch** (8 m: double jump) | **Test 1**: jump, double jump, mantle in a chain. Fail: down into the lane (walk on and climb back at the crates in each gap) or into the ditch (4.4 m deep: out by its steps at the far end, round through the Bells' garden: ~20 s) | The workshop, the Old Mill | |
| 13:30-14:00 | **The Old Mill's shutter** (up 5 s, down for 7, on the loop's 12 s clock) | **Timing**: slide under it as it comes down, or wait for it | | |
| 14:00-14:40 | **The loading bay** (8.4 m, along Rowan Street's wall) then **the scaffold's bar** then **the old oak** | **Test 2: wall-run, then slide, then mantle, mantle, mantle** in one line | Eli at his barbecue, far up Rowan Street | Optional: up round the trunk to **the treehouse** ("NADS + ELI", `mem_treehouse`); the lane's **nail** (`mem_the_nail`) for walkers |
| 14:40-16:00 | **Rowan Street: the Rewind.** "Eli!" He heard; so did the dream. A wall of gold light sweeps east down the street: whatever it touches goes back to the start of the day. Through the block party: the gazebo's curtain, the bales, along the truck over the trench, the skip, the trench across the street, the stage, to the gate | **The set piece**: everything chained, against the clock. Caught: a white rewind back to the street's end; the party snaps back; each catch slows the next wave (7.2 m/s, then 6.8, 6.4... never under 5.6) | The house, the whole way | |
| 16:00-17:00 | **The gate**: Eli at the stove through the window, Theo setting the table; he looks up; Lena steps between them and takes him in; the door; the porch light; Mags: "His heart rate's up. That's enough for tonight. You found the house." | - | | The night's last image |

**Failure always costs something and is never a free mid-air reset**: water (slow wading and the way out), a fall from a
roof or wall (the band below: walk round and climb back), the Rewind (back to the street's end). Out of the world only
happens through a bug: then she's put back at the start of the band she was on.

**Optional lines and rewards** (they feed Night 2's battle; the battle agent wires them):
- `mem_walt_ledger`: Walt's ledger, 41 identical lines "Sat. E., rear tyre, flat. Patched." (when the bedside's "Walt's
  shop" put it out).
- `mem_the_nail`: in Back Lane B outside Mr. Bell's workshop, for those who walk the lane: "A roofing nail, point up, in
  the same crack. His tyre never stood a chance." With the ledger: the flat tyre every Saturday (L3c).
- `mem_treehouse`: the treehouse at the top of the old oak (the harder climb round the trunk): "NADS + ELI. NO GIRLS."
  He crossed the last bit out for her: the dream still keeps her name (against "you're the intruder").
- Skilled lines: the roof line over the lane (faster than the lane), a slide-boosted double jump over the pool, the
  truck's roof in the Rewind, the garage roofs straight to Rowan Street west of the oak.

**The bedside changes the night** (docs/redesign/day.md, read through `Chain.HasDreamChange`):
- "Walt's shop": the ledger lies open on his bench (otherwise it's put away);
- "the creek": the old plank is back across the creek (a walk instead of the double jump, X 106);
- "his patients": St. Brigid's windows are lit;
- "just sit": Walt greets her warmly, by name.

**Saturday on the loop**: the Lindqvists' sprinkler, dog and ball and the line (`NeighbourLoop`, the same every time;
Night 2 replays it); Mr. Okonkwo's car wash, the Fairbanks' mower, the Ruizes' yard sale, two kids on bikes riding Mill
Street, the old man in the allotments, the block party (dancers, a grill, the lemonade stall), barbecue smoke over the
party and Eli's grill, the shutter's clock, the Route 9 bus waiting. Each is a `HillPersonSpot` (role, activity, a loop
path): **billboards** (docs/REDESIGN.md section 7: only Nadia and Elias are 3D in the dream) through the People agent's
`DreamCutoutView`; Eli at his grill becomes his 3D model when it lands.

## Nights 2 and 3 on the hill

- **Night 2** starts on Rowan Street outside the house (`HillAnchors.Night2Start`, `DreamStreetGame.night = 2`): the loop
  replays exactly ("Morning, Nadia! Lovely Saturday for it!"), the party is there, no Rewind; reaching the gate fires
  `DreamStreetGame.Night2AtTheDoor` for the House agent's night. The escape bursts out of the house onto **Chapel Row**
  (`HillAnchors.EscapeExit`) and its line runs up the middle band to the **Route 9 stop on Hill Road** at Bell Row
  (`HillAnchors.EscapeEnd`): the Chapel Steps behind the Lasnames', the terrace walls (6-7 m between terraces: wall-jumps
  and the steps), the rooftops of the rows, St. Anne's buttresses and roofs, the school's playground and flat roof, Hill
  Road. Milestone 1 blocks the band out and gives a simple line (`LarchHill.escapeLine`); the escape's run is the House
  agent's and its middle-band challenges are World's next milestone.
- **Night 3** starts on the summit: Crest Road's shops (walkable awnings, flat roofs), the forecourt, the hospital's
  entrance (`HillAnchors.HospitalEntrance`); the interior comes with Night 3.

## Anchors (for the House agent and anyone placing things on the hill)

The numbers live in code: `Packages/com.vibejam.jamkit/Runtime/Core/DeepBlock/HillAnchors.cs` (namespace
`JamKit.DeepBlock`, a static class, usable from core, the Dream scripts and the editor). World axes: X east, Z north (up
the hill), Y up; the creek's north bank is Y 0.

| Anchor | Where | What |
|---|---|---|
| `HouseOrigin` | (24, 9.6, 122), root yaw 0 | The Lasname house's root: the front door's threshold. The house's own frame is `DreamHouseExterior`'s (front faces local -Z), so on the hill its front faces **south, down the hill, onto Rowan Street**. |
| `LotX0..LotZ1` | X 10-42, Z 116-146 | The Lasnames' lot: front garden, side garden (the barbecue, east), back garden. The hill puts nothing inside it but fences, gates and ground. |
| `HouseMax*` | X 14-36, Z 122-142, up to Y 31.6 | The most the house's body may take. The round 5 exterior fills X 16.4-30, Z 122-137.4. |
| `FrontGate` | (24, 9, 116.3) | The front garden's gate on Rowan Street. Night 1 ends here. |
| `BackDoor`, `BackGate` | (24, 9.6, 137.4), (29, 9.6, 144) | The back door (movable along the back wall) and the foot of the Chapel Steps, which climb east along the middle band's first wall (X 30-40) to Chapel Row. |
| `Night2Start` | (8, 9, 110), yaw 60 | Night 2 starts on Rowan Street outside the house, the loop replaying. |
| `EscapeExit` | (24, 15, 153), yaw 0 | Where Night 2's escape bursts out into the middle band: Chapel Row (the first terrace above the house). |
| `EscapeEnd` | (110, 29.15, 226) | The end of the middle band's escape line: the Route 9 bus stop on Hill Road (Bell Row's level). |
| `Night1Arrival` | (128, 3, -42), yaw -28 | The rise on the creek's far bank: Walt's shop across the water, the house above its roof, the hospital on top. |
| `HospitalEntrance` | (0, 46, 322), yaw 180 | St. Brigid's main doors on the summit (Night 3). |

Band heights: the foot 0, Mill Street 4 (Back Lane B 5), Rowan Street 9, the middle band's terraces 15 / 22 / 29 / 36,
Crest Road 42, the summit 46. In the hill scene the house's outside is `DreamHouseExterior.Build` (called by name; a kit
house of the same footprint stands in if it's missing) and its colliders are the round 5 body (walls 4.5 m, a roof to
7.1 m, the tower to 10.9 m): tell World if the house's outside changes shape.

## How the hill is made

- **Core data** (`Packages/com.vibejam.jamkit/Runtime/Core/DeepBlock/`): `LarchHill.cs` (helpers, the switches, the
  Rewind's numbers), `LarchHill.Foot.cs`, `.Lower.cs`, `.Upper.cs` (the layout), `.Night.cs` (spots, routes with the
  autopilot's cues, the 25 camera views, the bands), `HillWorld.cs` (houses, trees, props, buildings as data, and
  `HillColliders`, which makes their colliders: what you see is what you stand on), `HillAnchors.cs`, `LarchStreetStory.cs`
  (the night's state and every line). Ground tiles never overlap and cover the whole hill; a tile's sides that stand above
  its neighbours are drawn as retaining walls or kerbs.
- **The drawing** (`Assets/JamKit/Templates/VisitingHours/Dream/Editor/Street/`): `DreamStreetBuilder.cs` (the scene),
  `DreamHillDrawing.cs` (ground, steps, retaining walls with copings, kerbs, scatter of grass tufts, flowers, leaves,
  cracks, stains, manholes and drains, road markings, the party's things, wires, bunting, the backdrop), and the kit in
  `Kit/`: `HillMesh` (a mesh sink: bevelled boxes, tubes, cards, world-metre UVs, one renderer per material per 40 m
  chunk), `HillMat`/`HillMaterials` (every material id, its CC0 texture pack, tint and tiling; URP Lit/Unlit only; a Look
  material at `Look/Materials/Hill/<id>.mat` overrides one), `HillAtlas` (the generated atlases' cells), and the drawings
  `HillHouses`, `HillTrees`, `HillFences`, `HillProps`, `HillLandmarks` (`HillKitPreview` builds a preview scene of them).
  Every wall runs (`onlyMarkedWalls` off); slopes are convex wedge colliders.
- **Textures** (`Dream/Street/Textures/`, licences in its `LICENSES.md`): 25 CC0 packs from Poly Haven and ambientCG
  (colour, OpenGL normal, AO), and generated packs from `tools/art/hill-textures.py` (window interiors, shop and ward
  interiors, signs in the repo's OFL fonts, awnings, leaf clusters, grass and flower cards, ivy, decals).
- **Light** is the Look agent's: `VhLook.ApplyGoldenHour` (sun at bearing 250, 10 degrees up), `VhLook.SetupCamera`,
  reflection probes per street; the far scenery is named "(backdrop)" for their bake.

## The geometry lint (shared with the House agent)

`Assets/JamKit/Templates/VisitingHours/Dream/Editor/Lint/DreamGeometryLint.cs`: five checks on the open scene, JSON and a
readable list to `out/lint/<time>/<scene>.json|.txt`: **butting-in** (interpenetrating solids, and renderers poking into
other objects' colliders), **no-collider** (visible solids within reach with no collider), **holes** (gaps under walkable
surfaces, narrow slots), **not-runnable** (walls by a route or a drop that won't wall-run), **no-fall-volume** (edges you
could fall out of the world from). The hill's routes come from `DreamStreetGame.LintRoutes()`. Mark deliberate overlaps
with `LintGroup`/"[group]", exclusions with `LintIgnore` (a reason is required), fall catchers with `DreamFallVolume`.
`-executeMethod JamKit.DreamEscape.EditorTools.DreamGeometryLint.RunFromCommandLine -lintScene <scene> [-lintOut <dir>]`;
menu JamKit/QA/Geometry Lint (open scene).

## Tests

- `core-tests/DeepBlock` (`LarchHillTests`, 22 tests): every leg under the autopilot at 20/60/144 fps; every beat within
  the movement's reach; the way in needs the double jump and the slide; the Rewind lets a clean run through by 4+ m and
  catches a 6 s stop; the shutter is a real wait; **no band can be skipped** (nothing you can stand on within 10.5 m of a
  band's edge reaches over it with a jump, a double jump and a mantle); tiles never overlap and cover the hill; nothing to
  walk to is inside a solid; the hospital is in sight from 60%+ of the night (the way in 88%, the green 81%, the Rewind
  89%; the slot and the lane's far end are the dips) and the house from the arrival; the anchors; the story.
- Unity: `DreamStreetEditorTests` (wiring, colliders, every material has its texture, nothing on the noise fallback) and
  `DreamStreetPlayTests` (the way in, each leg on the real controller, the Rewind, the gate, the loop and the paper, the
  bedside changes; `RecordTheNight1CriticalPath`, Explicit, the recorded run).

## Captures

- **Stills**: `tools/street-capture.ps1 [-Build] [-DreamChanges]` (25 views at 1920 x 1080 through the Look rig) to
  `out/hill-captures/<time>/` (`hill-captures-changed` with the bedside's changes on).
- **Recorded run**: the PlayMode test `JamKit.DreamEscape.Tests.DreamStreetPlayTests.RecordTheNight1CriticalPath` (a frame
  every 0.5 s and `timeline.csv`) to `out/hill-run/<time>/`.

## Known gaps (milestone 1)

- **Thin fences can't be vaulted**: the movement's mantle probes 0.35 m past a face, so anything thinner (a 12 cm board
  fence) is a wall, not a ledge. The lessons use 0.5 m garden walls. Suggested movement fix: probe at several depths.
- The middle band's rows are regular and its challenge lines aren't designed yet (milestone 2, with the House agent's
  escape). The top band is blocked out only.
- Characters are billboards and spots; no 3D Elias at the grill yet.
- Unseen in Unity: shapes, materials and performance (1080p/60 on the RTX 3050) wait on main's captures and the
  frame-time probe.
