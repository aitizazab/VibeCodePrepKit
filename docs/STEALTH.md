# The stealth kit

A reusable stealth layer for any jam game that uses the movement kit (`docs/MOVEMENT.md`): seekers with vision cones
and hearing, a detection meter that fills over time, alert states with a last-known-position search, hiding spots,
readable feedback on screen and on the seeker, and a collectibles counter. No story in it: Visiting Hours (Theo's
hide-and-seek tutorial, the "anything left to find?" line) builds on top. The **Stealth Lab** is its test yard.

- Core (pure C#, `Packages/com.vibejam.jamkit/Runtime/Core/Stealth/`): `StealthDef` (every number),
  `StealthSight` + `DetectionMeter`, `SeekerBrain`, `WaypointGraph`, `StealthNoiseRules`, `CollectibleSet`,
  `StealthBoxWorld`, `StealthYard` (the lab's layout as data).
- Unity (`Runtime/Unity/Stealth/`): `SeekerAgent`, `StealthPlayer`, `HidingSpotView`, `StealthLightZone`,
  `SeekerOverlay`, `StealthHud`, `CollectibleCounter` + `Collectible`, `StealthTuning`, `PhysicsStealthWorld`;
  the shader `JamKit/Stealth/Overlay`.
- The lab (`Assets/JamKit/Templates/StealthLab/`): `StealthLabGame`, `StealthLabPresenter`, `StealthLabBuilder`,
  `StealthLabTemplate`, tests.

## Drop it into a game

1. On the player (a `MovementController`): add **StealthPlayer** (set `body`; optionally a `StealthTuning` for the
   noise numbers). Untick `findable` for cutscenes or safe rooms.
2. For each NPC: an object with **SeekerAgent** (no colliders on it), a child-less **waypoints** object whose children
   are points on its floor (it joins those within `connectDistance` that have a clear walk, by physics), a `patrol`
   loop of child indices, `mode` (Patrol or HideAndSeek), a `StealthTuning`. Add **SeekerOverlay** with two materials
   on `JamKit/Stealth/Overlay` (cone: `_ZTestMode` 4, meter: 8) for the readable cone and meter.
3. Hiding spots: an empty with **HidingSpotView** (kind, box size, an `approach` child where a seeker stands to check
   it). Light and shade: **StealthLightZone** boxes (0 dark .. 1 lit); `StealthLightZone.Ambient` for the rest.
4. Collectibles: **Collectible** components (an `id`, a spinning child `visual`, no collider) under a
   **CollectibleCounter**. Ask `CollectibleCounter.Current.Remaining` / `AnyLeft` / `Summary`; subscribe to
   `ItemCollected` and `AllFound` (or the `onAllFound` UnityEvent), which fires once when the last one is found.
5. The screen indicator: `new StealthHud(GameRoot.Hud.Root)` and call `Tick()` every frame.
6. React: `SeekerAgent.Found` / `onFound` (the game decides: caught, restart, a scene), `StateChanged`;
   `SeekerAgent.Restart()`, `SetMode()`, `Frozen`. A scripted alarm: `seeker.Brain.Alert(position)`.

The seeker walks on a flat floor (its start height) and moves itself (no NavMesh, no physics body): give it waypoints
around every obstacle. It thinks only while `GameRoot.Input.GameplayAllowed` (not paused or loading).

## Sight and the detection meter

Visibility (0 unseen, up to about 1.3) is the product of:

| Factor | Value |
|---|---|
| Zone | the cone (±40°): 1; the peripheral band (to ±75°): 0.3; behind, within 1.4 m: 1 (sensed); else 0 |
| Line of sight | three points on the body (20%, 55%, 92% of its height): 1/3 each. Crouched behind 1.1 m cover: 0; standing: the head (1/3) |
| Distance | 1 up to 5 m, falling to 0.3 at 18 m; nothing past 18 m |
| Posture | crouched 0.55, sliding 0.8; at 7 m/s or faster ×1.3, standing still ×0.75 |
| Light | 0.25 in full shade, 1 lit |
| Hiding spot | bush 0.12 (1 within 1.5 m of the seeker); under / closet 0 |

The meter fills at `fillRate` 1.25/s × visibility (×1.5 while suspicious or searching): 0.8 s at best up close in the
open, never instantly. Unseen for 1.2 s, it drains at 0.3/s. At 0.3 an unaware seeker becomes suspicious; full is
found.

## Alert states

`Unaware → Suspicious → Searching → Found`, and back to `Unaware`:

- **Unaware**: patrols (pauses 1.2 s at each point), or in hide-and-seek counts 10 s facing away (blind), then seeks.
- **Suspicious** (the meter passed 0.3, or it heard a noise): turns to the spot for 0.6 s, then walks over (2.6 m/s).
- **Searching** (it reached the spot): looks around for 2.4 s, then checks the hiding spots within 10 m of the last
  known position, nearest first (1.1 s each: it opens the one you're in), then two nearby points. A patrol gives up
  after 16 s and returns to its loop. Hide-and-seek sweeps every spot and every walking point (a nearest-next tour),
  over and over, until it finds you. A glimpse while searching sends it straight there.
- **Found** (the meter is full, or it opened your spot): fires `Found`. A patrol chases (4.6 m/s); out of sight for
  2 s it searches the last known position. Hide-and-seek stops: the round is over.

## Noise

From the movement kit (`StealthNoiseRules`, per frame): a landing at 7 m/s or more downward (12 m, louder the harder),
sprint steps (7 m every 0.35 s), wallrun steps (9 m), a slide's start (5 m). Walking, crouching and soft landings are
silent. A seeker hears a noise within its radius × `hearingScale`, half that through a wall.

## Readable feedback

- On the seeker: the vision cone on the ground (bright cone, faint periphery, cut short by walls at hip height) and a
  meter above its head that shows through walls; colours: white unaware, amber suspicious, orange searching, red found.
  While counting, the cone is hidden and the meter shows the count running down.
- On screen (`StealthHud`): a bar at the top that fills with the most alert seeker's meter, a word over it ("Unseen",
  "Hidden: closet", "Suspicious ?", "Searching", "Found !", "Counting 7"), and the collectibles ("2 / 5 found").

## The Stealth Lab

"JamKit/Use Template/Stealth Lab" (batch: `JamKit.StealthLab.EditorTools.StealthLabTemplate.UseFromCommandLine`);
built by "JamKit/Rebuild Shell Scenes" (or "JamKit/Stealth Lab/Rebuild Scene"). Presenter chapters: **Hide and seek**,
**Patrol**. R / View restarts the round.

A fenced 48 × 36 m garden at dusk (core `StealthYard`): the house (its south face runnable), a wardrobe-closet against
it, the shed with a crate to mantle up, a 14 m freestanding wall to wallrun with a lookout past its end, a deck to slide
under (1.15 m underside, like the Movement Lab's bars), a garden fence with a slide-under gap, three low walls, three
bushes, a lamp-lit middle, a dark corner under a canopy and a dark alley behind the house. Five collectibles, one per
route: the shed roof (mantle), the lookout (wallrun), under the deck (slide), the closet, the alley.

- **Hide and seek** (the default): the seeker counts to ten, then seeks. Found: the round's over. All five without
  being found: you win.
- **Patrol**: a guard walks a loop. Get the five without being found.

## Every tunable number

All in `StealthDef` (a `StealthTuning` asset; the lab's is `Templates/StealthLab/Data/StealthLabStealth.asset`).

| Field | Default | What it does |
|---|---|---|
| eyeHeight / sightRange | 1.6 / 18 | Eye above the feet; nothing past this is seen. |
| coneHalfAngle / peripheralHalfAngle / peripheralScale | 40 / 75 / 0.3 | The cone and the weaker band beside it. |
| closeRange | 1.4 | Sensed in any direction this close. |
| nearRange / farScale | 5 / 0.3 | Full rate to 5 m, then down to 0.3 at sightRange. |
| crouchScale / slideScale / fastScale / fastSpeed / stillScale / stillSpeed | 0.55 / 0.8 / 1.3 / 7 / 0.75 / 0.4 | Posture. |
| shadowScale | 0.25 | Sight in full shade. |
| bushScale / bushRevealRange | 0.12 / 1.5 | In a bush, unless the seeker is right there. |
| sampleLow / sampleMid / sampleHigh | 0.2 / 0.55 / 0.92 | Body points for line of sight. |
| fillRate / alertFillScale | 1.25 / 1.5 | Meter per second at visibility 1; faster when alert. |
| drainDelay / drainRate | 1.2 / 0.3 | The meter holds, then drains. |
| suspiciousAt / noiseBump | 0.3 / 0.3 | Unaware → suspicious; what a noise adds. |
| reactTime / lookAroundTime / lookAroundAngle | 0.6 / 2.4 / 70 | Turning to look; looking around. |
| spotSearchRadius / checkSpotTime / searchTime | 10 / 1.1 / 16 | The search. |
| loseSightTime / catchRange / countTime | 2 / 1.3 / 10 | Losing you; chasing; hide-and-seek's count. |
| patrolSpeed / investigateSpeed / searchSpeed / chaseSpeed / turnRate | 1.8 / 2.6 / 3.2 / 4.6 / 200 | Walking. |
| arriveRadius / patrolPause | 0.35 / 1.2 | |
| hearingScale / occludedHearing | 1 / 0.5 | |
| hardLandingSpeed / landingNoiseRadius / sprintNoiseRadius / footstepEvery / wallrunNoiseRadius / slideNoiseRadius | 7 / 12 / 7 / 0.35 / 9 / 5 | Noise. |

## Tests

| Where | What |
|---|---|
| `core-tests/Stealth` (`StealthSenseTests`) | the cone, periphery and close sense; distance; low cover hides a crouched body, shows a standing head; posture, light, bush, closet; the meter fills over 0.8 s (never at once) and drains after its delay; the box world's line of sight; noise from hard landings, sprint, wallrun, slide, not walking |
| `core-tests/Stealth` (`SeekerTests`) | a glimpse: suspicious, searching, unaware again; steady sight: found in about a second, lost: searching; a noise behind turns it and sends it there; a search opens the closet you're in; in the yard: a stationary player in the open is found, one in the closet is found by the sweep, the dark alley and under the deck are reached, the seeker never walks through a wall; a patrol walks its whole loop; the counter (remaining, any left, the last announced once, reset) |
| `core-tests/Stealth` (`StealthYardTests`) | the graph is one piece and reaches every spot; nothing inside a solid box; the routes fit the movement kit (mantle heights, the 1.15 m underside, the wall's length, the lookout past the wall); light and shade |
| Unity EditMode (`StealthLabEditorTests`) | the scene is wired from the yard; solid vs see-through boxes; the overlay shader compiles; the lab is in Build Settings |
| Unity PlayMode (`StealthLabPlayTests`) | the physics graph is one piece; a seeker finds a player standing still in the open (and the HUD says so); the closet hides you until the sweep opens it; the five collectibles count down and announce the last; the restart |

## Known gaps

- The seeker walks a flat floor over its waypoints (no stairs, no NavMesh); it can't climb to a roof, it waits below
  and finds you by sight.
- One player target (`StealthPlayer.Current`). Several seekers work, but don't talk to each other yet.
- Grey-box art: capsule seeker, box yard.
