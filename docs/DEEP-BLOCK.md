# The deep block: a walkable greybox of layout A

Layout A of `docs/DENSE-STREET.md` ("the deep block", the recommended one), built so the creator can walk it before
picking a layout. Larch Street at golden hour, about 124 x 112 m, with its four stacked layers (the street, the back
gardens and lane, the roofs, the creek under the street), the eight per-night switches, and the hide-and-seek garden
with Theo, using the stealth kit (`docs/STEALTH.md`). Greybox: boxes, a gold mark on everything usable.

- Core data: `Packages/com.vibejam.jamkit/Runtime/Core/DeepBlock/DeepBlock.cs` (pieces, switches, nights, the four
  routes with their autopilot cues and reach beats, the hide-and-seek zone).
- The lab: `Assets/JamKit/Templates/DeepBlock/` (`DeepBlockGame`, `DeepBlockPresenter`, `BlockSwitchProp`,
  `DeepBlockBuilder`, `DeepBlockTemplate`, tests).
- Checks: `core-tests/DeepBlock` (`DeepBlockTests`), plus Unity EditMode and PlayMode.

## How to reach it

- "JamKit/Use Template/Deep Block" (batch:
  `JamKit.DeepBlockLab.EditorTools.DeepBlockTemplate.UseFromCommandLine`); "JamKit/Rebuild Shell Scenes" (or
  "JamKit/Deep Block/Rebuild Scene") builds it.
- Presenter chapters (Esc): **Street route**, **Back route**, **Roof route**, **Under route** (each puts you at the
  route's start and times you to the hill gate), **Hide and seek**, **Free roam**.
- Debug keys:
  - **F7** (or LB + d-pad right): the night, 1 to 4. The switch props open and shut.
  - **F8** (or LB + d-pad left): the course autopilot runs the current route (it starts the street route if none).
  - **F3** (d-pad down): the panel (the night, each switch open or shut, the route time, Theo's state).
  - **R** (View): run the route again, or restart hide and seek.

## The four routes (night 1)

Every route ends on the hill gate's apron (6.0 m, in front of the chained gate). The autopilot times are from the
dotnet sim at 60 fps (`TheAutopilotRunsTheRouteToTheHillGate`, since the 2026-10-12 ground-speed fix); the doc's rough times are its estimates.

| Route | Line | Moves | Length | Autopilot | Doc |
|---|---|---|---|---|---|
| **The street** | the south pavement, hop the party's two sawhorses (0.85), across the road, up the hill path's mantle stair | 2 hops, 4 mantles (1.5 m terraces) | 128 m | 14.3 s | ~13 s |
| **The back** | across to side gate A (S2), up side passage A (4.5 m), garden 1, over the hedge (1.2, mantle), through garage 8 (its lane door half open: slide), up the lane (flat, then rising 2 m), through its top gate (S6), up the lot's steps | 3-4 mantles, a slide | 171 m | 18.7 s | ~20-25 s |
| **The roofs** | house 1's porch roof (2.9: jump + mantle), its eaves (5.2: jump + mantle), over the ridge to the back pitch, along it, jumping passages A and B (4.5 m), down the scaffold's decks (6, 4, 2), out of the extension, a wallrun along the unfinished wing's 14 m block wall, wall kick + double jump to the skip, the builders' crate, the lot's steps | 4 jumps + mantles, 2 roof gaps, a wallrun, a wall kick, a double jump | 131 m | 15.7 s | ~18 s |
| **Under** | through Walt's half-open door (S8: slide), out his back door, down the creek bank, along the creek bed (under the plank, S5), north up the channel and through the culvert under the street (1.15 m: slide), the pond, out by its step, up the mantle stair | 2 slides, a 2 m drop, 5 mantles | 214 m | 22.8 s | ~25 s |

Every move is checked against the movement's reach by `CourseReach` (`EveryRouteBeatFitsTheMovementsReach`): mantles
0.4 to 1.7 m (a jump + mantle reaches 3.15), roof gaps under a single jump (6.1 m at sprint), the slide gaps' 1.15 m,
the wall 8 to 21 m long and at least 3 m tall. The routes also finish at 20 and 144 fps.

The roof route is the fastest despite being the longest climb: the back pitch and the scaffold carry speed. On the real
CharacterController the PlayMode test runs all four (route times go to its output).

## The kit sizes (the doc's, kept)

| Element | Size |
|---|---|
| Garden fences | 1.8 m (a mantle from the ground; gold cap) |
| Pickets along the street | 0.85 m (a hop) |
| Hedge in garden 1 | 1.2 m |
| Garage roofs | 2.7 m |
| Porch roofs | 2.9 m |
| House eaves / ridge | 5.2 / 8.0 m |
| Side passages A and B | 4.5 m (wall to wall; a roof gap) |
| House side walls | 12 m (one wallrun) |
| The unfinished wing's block wall | 14 m long, 3.6 m tall, runnable (gold stripe) |
| The creek | 6 m wide, 2 m deep; the culvert 1.15 m clear |
| Decks | top 1.3, underside 1.15 (crawl or slide under) |
| The lane | 5 m wide, between the 3.2 m brick backs of Birch Lane (runnable) and the garages |

## The nights (the eight switches)

| Switch | Prop | Open | Shut |
|---|---|---|---|
| S1 | garage G3's back door (garden 3 to the lane) | gone | a door |
| S2 | side gate A (passage A's mouth) | a gap in the pickets | a 2.2 m gate (jump + mantle) |
| S3 | house 3's back door | open, a panel aside | shut |
| S4 | the scaffold ladder | down (a step up to deck 2) | up (gone) |
| S5 | the plank over the creek | there | gone (jump the 6 m or drop in) |
| S6 | the lane's top gate | open | 3.2 m, as tall as the lane wall: the back route is closed |
| S7 | the snicket gate | open | a 2.2 m gate |
| S8 | Walt's door | half open (slide under) | shut |
| (sheets) | sheets on garden 3's washing line | down | up: they block sight, not movement |

| Night | What's open |
|---|---|
| 1 | everything; nobody hunts |
| 2 | everything; hide and seek with Theo |
| 3 | S1, S2, S6, S7 shut; the sheets up (the roofs and the creek are the ways through) |
| 4 | also S3 shut, the ladder up, no plank, Walt's door shut (the back layer closed) |

These presets are mine, from the doc's examples (DENSE-STREET 4, "Across nights"); the creator picks the real ones.
The static (a layer closed by the dream) isn't modelled yet.

## Hide and seek (the gardens of houses 1 and 3, the lane's west half: 42 x 29 m)

- **Theo** is a seeker from the stealth kit (`DeepBlock.TheoDef`):
  - A child's eye, 1.1 m up.
  - **He doesn't look up:** a vertical limit of ±15° (`StealthDef.verticalHalfAngle`). A garage roof (2.7) or the
    treehouse (3.0) is out of his sight unless he's far away.
  - He counts 10 s on the trampoline.
- **The trampoline is his lookout** (`SeekerBrain.SetLookout`):
  - His sweeps come back to it every third stop, and he bounces there for 4 s, turning.
  - His eye rises from 2.0 m (on the mat) to 3.3 m at the top of each bounce (0.8 s).
  - From the mat the 1.8 m fences hide you; near the top he sees heads over them
    (`AtTheTopOfABounceTheoSeesOverTheFenceAndAtTheBottomHeDoesnt`).
- **The eight hiding places:**

| Place | Kind |
|---|---|
| H1 under house 1's deck | under |
| H2 the treehouse | closet |
| H3 the greenhouse (glass) | bush: you show a little |
| H4 under house 3's deck | under |
| H5 the shed | closet |
| H6 behind the bins | bush |
| H7 on garage G3's roof | bush, and above his sight |
| H8 inside garage 2 (open to the lane) | closet |

  He checks each one on his sweep and opens the one you're in.
- **Six memories**, one near each kind of place, with the counter on screen (`CollectibleCounter`; its `AllFound`
  event is where Visiting Hours hooks its line).
- **Being found** is "Found you!", a laugh, and a recount after 3 s. Never a fail.
- **Measured in the sim:**
  - He finds a player standing in the open in about 70 s (10 s of counting included).
  - He finds a player under house 1's deck in about 95 s.
  - A full sweep of the zone takes about 2-3 minutes, inside the doc's 3-6 minute game.

**Re-checked against the stealth kit's final numbers** (the doc used drafts):
- **Sight:** range 18 m. From the trampoline the zone's west end (30 m) and the lane are out of range, so he has to
  walk the zone.
- **Noise radii:** sprinting 7 m, a wallrun 9 m, a hard landing 12 m (7 m/s or faster downward).
  - Dropping from the treehouse (3 m) lands at about 12 m/s, so it is heard across a garden. Climb down instead.
  - A wallrun on the lane wall is heard from the lane's half of the zone.
- **Meter:** fills in about 0.8 s at best in the open, slower far away. A glimpse at the top of a bounce (a head over
  a fence) fills it slowly, and he comes to look.
- **Verdict:** the zone's sizes hold. Nothing needed changing.

## Where this greybox departs from the doc's plan, and why

| What | The doc | Here | Why |
|---|---|---|---|
| Axes | map x along the street, y north to south | world Z = map x, X = map y | the movement's courses run along +Z |
| Roof pitch | 35° | 28° (5.2 to 8.0 over 5.2 m), with a 0.8 m flat strip along each eaves | mantling straight onto a slope fails (the capsule clips it); the strip is the gutter you land on. 12 m deep houses with these heights give 28° |
| The lane's slope | falls 2 m toward Walt's | flat for its west 40 m (the hide-and-seek half), then rising 2 m to the hill end | Theo walks a flat floor |
| "Out through the garage" | through a garage | garage 8 (x 8-14) open at both ends, its lane door half open | the back route's slide |
| The roof route's tail | down the scaffold, wallrun the extension's 14 m block wall at x 61.2, double jump to the skip | the scaffold's decks step east inside the extension (6, 4, 2), out through a gap in its east wall; the 14 m block wall is the extension's unfinished wing, freestanding in the builders' lot (map y 20, x 72-86); the skip at x 89-94 | x 61.2 is house 5's own wall, and the scaffold stood where the run-up would be |
| The hill | the hill path, terraces 1.5 m | a mantle stair of 1.5 m terraces from the street up to the gate's apron (6.0); the lot north of the gate at the lane's height (2.0), with steps 3.2 and 4.6 up its west side; a builders' crate (1.6) | every route ends at the gate by mantles within reach |
| The culvert | 1.15 m, under the street | covered from map y 52 to 64 (the road and both pavements), the channel open north of it to the pond; a step (-0.6) to climb out of the pond | the doc's shape, made climbable |
| Walt's shop | flat roof 4.0, door half open | hollow, with a back door (x 14-16) and a skylight gap | the under route starts through the shop |
| The bins | in passage A | on garden 3's side of the divider | they blocked the back route |
| The divider fence | 1.8 between gardens 1 and 3 | with a 2 m garden gate | Theo walks between the gardens |
| The hedge | x 1-11 | x 1-13 | the back route crosses it |
| The allotment | x 66-96 | x 66-84 | the channel north to the culvert runs at x 86-92 |
| The trampoline | a prop | seen, no collider | Theo walks onto it |
| Garage roofs as hiding (H7), garage hiding (H8) | "on a garage roof", "behind a garage in the lane" | H7 on G3's roof, H8 inside garage 2 (open to the lane) | |
| One memory | map (41, 12.5) | (43.6, 12.5) | the doc's point is inside a garage |

## Tests

| Where | What |
|---|---|
| `core-tests/DeepBlock` (`DeepBlockTests`) | every route's beats fit `CourseReach`; the autopilot runs each route to the gate (60, 20, 144 fps); route starts and Theo's points, check points and memories clear of solids; the doc's kit sizes; the nights open and shut the switches (night 4's lane gate too tall to climb); at the top of a bounce Theo sees over the fence and from the mat he doesn't; he doesn't look up at a garage roof (a grown-up would see you); he finds a player in the open garden and one under the deck; his walking map is one piece and reaches every hiding place |
| Unity EditMode (`DeepBlockEditorTests`) | the scene is wired from the block (every piece, colliders where solid, five roof prisms with colliders, the switch props, gold on usable pieces, Theo with his points, lookout and ±15° tuning, eight places, six memories, the player); the lab is in Build Settings |
| Unity PlayMode (`DeepBlockPlayTests`) | the autopilot runs each route to the gate on the CharacterController; the night selector opens and shuts the lane gate and the panel says so; Theo finds a player in the open garden and counts again |
