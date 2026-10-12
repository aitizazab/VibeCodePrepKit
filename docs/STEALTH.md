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
7. Optional:
   - **A lookout** (a trampoline): set `SeekerAgent.lookout` (and its platform height, bounce height and period). In
     hide-and-seek it counts there, and its sweeps come back to it every few stops to bounce for a few seconds,
     turning; its eye (and body) rise with each bounce, so it sees over fences only near the top
     (`SeekerBrain.SetLookout`, `EyeLift`, `OnLookout`).
   - **Sight-only blockers**: **StealthSightBlocker** boxes block sight but not movement (sheets on a washing line,
     smoke).

The seeker walks on a flat floor (its start height) and moves itself (no NavMesh, no physics body): give it waypoints
around every obstacle. It thinks only while `GameRoot.Input.GameplayAllowed` (not paused or loading).

## Sight and the detection meter

Visibility (0 unseen, up to about 1.3) is the product of:

| Factor | Value |
|---|---|
| Zone | the cone (±40°): 1; the peripheral band (to ±75°): 0.3; behind, within 1.4 m: 1 (sensed); else 0 |
| Line of sight | three points on the body (20%, 55%, 92% of its height): 1/3 each. Crouched behind 1.1 m cover: 0; standing: the head (1/3). Peeking: see below |
| Distance | 1 up to 5 m, falling to 0.3 at 18 m; nothing past 18 m |
| Posture | crouched 0.55, sliding 0.8; at 7 m/s or faster ×1.3, standing still ×0.75 |
| Light | 0.25 in full shade, 1 lit |
| Hiding spot | bush 0.12 (1 within 1.5 m of the seeker); under / closet 0 |
| Vertical limit | `verticalHalfAngle`: 90 (none) by default. A child who "doesn't look up" uses 15: above or below that, only the close sense works (a roof or a treehouse a few metres off is safe) |

The meter fills at `fillRate` 1.25/s × visibility (×1.5 while suspicious or searching): 0.8 s at best up close in the
open, never instantly. Unseen for 1.2 s, it drains at 0.3/s. At 0.3 an unaware seeker becomes suspicious; full is
found.

### Peeking

The target can carry a **lean** (`StealthTarget.lean`: where the head is, sideways from the body's centre line; the
movement kit's peek, docs/MOVEMENT.md "Peeking"). While it's 0.1 m or more (`peekMinLean`):
- the head sample is looked for **where the eyes really are** (the lean added), the chest sample 40% of the way there
  (`peekChestShare`), the low sample stays;
- when **only the head** is in sight, the line of sight counts **0.15** (`peekExposure`: a head round a door frame)
  instead of a third.

So peeking round a door frame is a small target and stepping out is all of her. In the open a lean hides nothing (all
three samples clear count as before). Measured (`StealthPeekTests`, default eyes 3.8 m away, standing still): behind
the frame 0, peeking 0.11, stepped out 0.75: the meter is full in **7.2 s** of steady sight peeking against **1.1 s**
stepped out. With Theo's eyes at the house's own door frames (`HousePeekTests`, 39 frames): a peek is at most 0.21 of
stepping out (at the living room door 0.11 against 0.53: found in 9.4 s against 2.0 s). Night 2's hide-and-seek gives
Theo her lean every frame (`DreamHouseGame.HerTarget`); `DreamPeekPlayTests` measures it in the real house with a test
gamepad.

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
  Being found comes first, then being hidden (so a hide-and-seek seeker, always searching, still tells you you're
  hidden), then the count and the seeker's state.

## The Stealth Lab

"JamKit/Use Template/Stealth Lab" (batch: `JamKit.StealthLab.EditorTools.StealthLabTemplate.UseFromCommandLine`);
built by "JamKit/Rebuild Shell Scenes" (or "JamKit/Stealth Lab/Rebuild Scene"). Presenter chapters: **Hide and seek**,
**Patrol**, one per answer below, **Caught: the subquest fails** and **Recommended**. R / View restarts the round.

A fenced 48 × 36 m garden at dusk (core `StealthYard`): the house (its south face runnable), a wardrobe-closet against
it, the shed with a crate to mantle up, a 14 m freestanding wall to wallrun with a lookout past its end, a deck to slide
under (1.15 m underside, like the Movement Lab's bars), a garden fence with a slide-under gap, three low walls, three
bushes, a lamp-lit middle, a dark corner under a canopy and a dark alley behind the house. Five collectibles, one per
route: the shed roof (mantle), the lookout (wallrun), under the deck (slide), the closet, the alley.

- **Hide and seek** (the default): the seeker counts to ten, then seeks. Found: the round's over. All five without
  being found: you win.
- **Patrol**: a guard walks a loop (three with key 5). Get the five without being caught. A guard who finds you
  catches you, and the encounter's `onCaught` decides the cost (see "Being caught" below): back to the last checkpoint
  (the default), or the subquest fails. Checkpoints are the green pads: the start, the dark corner under the canopy and
  the alley behind the house.

## When the movement outruns the guards (2026-10-11)

**The problem.** The creator, in the Stealth Lab: "the movement completely trivializes Stealth, because I move so
fast, one single guard can only be in one place, and his range is limited on top of that too. I'm literally running
circles behind him." At sprint (9.5 m/s), let alone 13-22 m/s, you cross a guard's 18 m sight in two seconds; his
turn (200°/s) and his meter (0.8 s at best) never catch up; and the original kit hears only sprint steps (7 m),
wallruns (9 m) and hard landings (12 m), while a double jump is silent.

The answers are **rules** in the kit (`StealthRules`, `StealthRules.Shared` for a scene), each off by default, each
switchable live in the Stealth Lab (keys **1-5**, or LB + d-pad up / right / down / left and LB + Y; a presenter
chapter each; the panel bottom left shows which are on), and they combine. Key **6** (LB + X) picks what a catch
costs (see "Being caught").

### A. Loud movement (`loudMovement`)

- Noise scales with what you do (`StealthNoiseRules`, StealthDef's "loud" numbers):

| Move | Radius |
|---|---|
| Walking | 2.5 m a step |
| Crouch-walking | silent |
| Sprinting | 5 + 0.8 × speed: 12.6 m at 9.5 m/s, 21 m at 20 |
| A slide's start | 11 m |
| A hard landing | 17-30 m |
| A double jump | **24 m** |
| A wallrun step or a wall kick | **22 m** |

  The last two are well beyond sight range.
- **A guard who hears turns wary, not alert.**
  - He goes to look (suspicious, then searching).
  - For 20 s, even back on his patrol, his cone is 40% wider (±56° instead of ±40°), he turns 60% faster, and his
    meter fills 30% faster.
  - His meter and cone show a pale yellow while he's wary.
- **Repeated noise escalates:** three noises within 8 s and he is alert. He searches the last spot at once, the meter
  already at 0.6.
- **Readable:**
  - Every noise you make draws a ring on the ground (`NoiseRings`), growing to its radius: you learn what carries how
    far.
  - The movement kit's footsteps and landings play as before, and a wary guard gives a little "blip".
- **Counterplay:** walk, crouch, hide, and spend the dream powers where nobody hears.

### B. The dream pushes back (`dreamPushesBack`)

Of the options, I picked the two that feel fair but scary, because both are readable and in your control:
1. **The guards call each other** (`SeekerSquad` in core; in Unity each `SeekerAgent` passes it on).
   - What happens: when a guard grows more alert (suspicious, searching or found), every other guard within 26 m turns
     wary and comes to the spot.
   - Why it's fair: one guard can't be everywhere, but a seen player draws the area, and you can watch it happen (the
     message, the cones turning).
2. **The faster you move, the more the dream notices you.**
   - What happens: the guards' sight range and meter rate grow with your speed, by ×1 + 0.06 per m/s over 5. That's
     ×1.27 at sprint (23 m) and ×2 at 22 m/s (36 m).
   - Why it's fair: speed has a cost you choose. Slow down near guards and they're as they were.
- **Rejected:** blinking a new watcher in, and a guard snap-turning to your noise. Both feel like the game cheating
  rather than the dream answering, and snap turns are what makes stealth "feel judged" (DENSE-STREET's Arkane notes).

### C. Movement crippled, with a reason

1. **C1, a daytime section** (`daytimeSection`): the movement's daytime profile (`docs/MOVEMENT.md`: no double jump,
   wallrun or slide, the air strafe down, jump fatigue). The reason is that she's awake: Nadia is just a nurse
   sneaking through St. Brigid's. The HUD says "Daytime: no dream powers".
2. **C2, being watched dims her lucidity** (`lucidityFade`; core `LucidityFade`, Unity `StealthPlayerRules`).
   - **When it fades:** within 10 m of any guard, or 20 m of a wary or alert one.
   - **What fades:** her dream powers, over 0.4 s: no double jump, wallrun or slide, and a 6.5 m/s speed cap.
   - **Coming back:** away from the guards the powers return over 1.5 s.
   - **On screen:** the edges darken as it fades, and the HUD says "Watched: the dream's powers fade".
   - **What it achieves:** you can't run circles behind him, but you're free away from guards. The movement is still
     the way across the yard; it just isn't a way round a guard.

### Three guards

The lab's fifth option puts two more guards on overlapping patrols of the yard's west and east halves (patrol rounds).
One guard can't hold a space against this movement however he's tuned.

**Fixed 2026-10-12:** the creator pressed 5 and only one guard spawned. The lab opens in hide and seek, and the key only
recorded the choice for the next patrol round, while the panel already said "[on]". Key 5 now starts a patrol round at
once, with three guards or back to one (`StealthLabPlayTests.KeyFiveFromTheOpeningRoundSpawnsTheThreeGuards` presses the
key on a test keyboard and counts the live guards).

### My recommendation

**A + C2, with three guards, and a catch restarts at the checkpoint** (the presenter chapter "Recommended"):
- A makes the dream powers a choice with a cost (they're loud), not a free escape.
- C2 stops the one thing that breaks stealth, circling a guard, while leaving the movement for getting around.
- Three guards make the yard a space to read rather than one man to dodge.
- B's call is the next lever if that's still too easy; its speed-noticing is the strongest single nerf, but it
  punishes the movement everywhere, which is why it isn't in the base set.
- C1 is for the waking world, where the story already takes the powers away.

## Being caught (2026-10-12)

The creator: "if the goal is to force stealth, we restart the checkpoint, or cause the subquest to fail, and there's
not really another option, since we don't have a true combat system." So a catch has exactly two outcomes, chosen per
encounter (core `CaughtOutcome`, the lab's `StealthLabGame.onCaught`):

- **`RestartCheckpoint`**: back to the last checkpoint. The guards go home and start their rounds again. Anything picked
  up since that checkpoint is put back where it was.
- **`FailSubquest`**: the subquest this encounter belongs to fails. The main story goes on; in the lab the round ends
  with the fail message and R starts it again.

**The caught moment** (core `CaughtBeat`, 0.8 s + 0.35 s + 0.45 s):
1. A guard reaching "found" catches you.
2. Everyone freezes for a short beat: the guards stop, and she's held still. The sting (`mv_caught`), a small shake and
   "Caught!" play. The screen stays clear, so you see who caught you.
3. Then, for a restart: a fade to black, the restart at full black (never seen), and the fade back in, with a line
   saying where you are and how many finds were dropped.
4. Or, for a failed subquest: the fail message and the "lose" sound.

**Checkpoints** (core `CheckpointLog` and `StealthCheckpoint`, Unity `StealthCheckpointZone`):
- A spawn point (feet and facing) with a trigger box above it. The feet inside the box reach it; no collider is needed.
- The latest one reached is the spawn, and what you had then is banked.
- A checkpoint is never taken while a guard is chasing you, so a restart never drops you back into the chase.
- The lab's three (`StealthYard.checkpoints`, green pads) are out of every guard's sight from his home (a core test
  checks it): the start, the dark corner under the canopy, and the alley behind the house.

**Dropped:** the softer push-backs as the cost of a catch (powers cut while chased, a lucidity drain). C2's lucidity
dimming near guards stays, as warning feedback only.

## The baseline for level design (2026-10-12)

The creator: "Maybe we tune these values once we actually build the first tutorial space, where we set the values
according to the level design, and then the rest we create level design according to the values we now have." So these
are today's values, unchanged, for the level designer to work from. The first tutorial space will tune them, and later
spaces are built around the result. A ring is drawn for every noise of 2 m or more (`NoiseRings.minRadius`); a noise
is heard within its radius, half that through a wall.

| Action | Noise radius (kit) | With A, loud movement | Ring colour | What a guard does |
|---|---|---|---|---|
| Walking, crouch-walking | silent | 2.5 m a step walking (every 0.56 s); crouched silent | pale blue-white | hears: meter +0.3 (to the suspicious line), turns, goes to look, wary for 20 s |
| Sprinting | 7 m a step (every 0.35 s) | 5 + 0.8 × speed (12.6 m at 9.5 m/s, 21 m at 20) | pale blue-white | the same |
| A slide's start | 5 m | 11 m | pale blue-white | the same |
| A hard landing (7 m/s or more down) | 12-19 m (× impact / 7, at most ×1.6) | 17-30 m (also × 1.4, at most ×1.8) | gold | the same |
| A wallrun step | 9 m a step | 22 m | orange | the same |
| A wall kick | silent | 22 m | orange | the same |
| A double jump | silent | 24 m | orange | the same |
| Three noises within 8 s | | | | alert: searches the last one's spot at once, meter at 0.6 |
| Seen, standing in the open within 5 m, in full light | | | | meter full in 0.8 s (1.25 a second); suspicious at 0.3 (0.24 s) |
| ... still | | | | × 0.75 (1.1 s) |
| ... crouched | | | | × 0.55 (1.5 s) |
| ... moving at 7 m/s or more | | | | × 1.3 (0.6 s) |
| ... at 18 m (sight's end; full rate to 5 m) | | | | × 0.3 (2.7 s) |
| ... in full shade | | | | × 0.25 (3.2 s) |
| ... in the periphery (40-75° off his facing) | | | | × 0.3 |
| ... in a bush (beyond 1.5 m) | | | | × 0.12 |
| ... while he's suspicious or searching | | | | × 1.5; wary × 1.3, his cone ±56° instead of ±40° |
| Within 1.4 m, any direction | | | | sensed, as if in his cone |
| Unseen | | | | the meter holds 1.2 s, then drains 0.3 a second |
| Found (meter full) | | | | **caught**: the encounter's outcome |

The guard's meter colours: white unaware, pale yellow wary, amber suspicious, orange searching, red found. A patrol's
search gives up after 16 s.

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
| peekExposure / peekChestShare / peekMinLean | 0.15 / 0.4 / 0.1 | Peeking: the head alone counts this much; the chest moves this share of the lean; a smaller lean isn't a peek. |
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
| loudWalkRadius / loudSprintBase / loudSprintPerSpeed / loudDoubleJumpRadius / loudWallrunRadius / loudSlideRadius / loudLandingScale | 2.5 / 5 / 0.8 / 24 / 22 / 11 / 1.4 | Answer A's noise. |
| waryTime / waryConeScale / waryTurnScale / waryFillScale | 20 / 1.4 / 1.6 / 1.3 | A wary guard (answers A and B). |
| escalateNoises / escalateWindow / escalateMeter | 3 / 8 / 0.6 | Repeated noise escalates to alert. |
| callRadius / noticePerSpeed / noticeFromSpeed | 26 / 0.06 / 5 | Answer B. |
| lucidityRadius / lucidityAlertRadius / lucidityFadeTime / lucidityRecoverTime / watchedSpeedCap | 10 / 20 / 0.4 / 1.5 / 6.5 | Answer C2. |

## Tests

| Where | What |
|---|---|
| `core-tests/Stealth` (`StealthSenseTests`) | the cone, periphery and close sense; distance; low cover hides a crouched body, shows a standing head; posture, light, bush, closet; the meter fills over 0.8 s (never at once) and drains after its delay; the box world's line of sight; noise from hard landings, sprint, wallrun, slide, not walking |
| `core-tests/Stealth` (`SeekerTests`) | a glimpse: suspicious, searching, unaware again; steady sight: found in about a second, lost: searching; a noise behind turns it and sends it there; a search opens the closet you're in; in the yard: a stationary player in the open is found, one in the closet is found by the sweep, the dark alley and under the deck are reached, the seeker never walks through a wall; a patrol walks its whole loop; the counter (remaining, any left, the last announced once, reset) |
| `core-tests/Stealth` (`StealthAnswersTests`) | loud movement is heard beyond sight, walking slowly stays quiet, the original kit stays silent; a heard noise makes a guard wary (not found) with a wider cone that calms after 20 s; repeated noise escalates; a suspicious guard calls the near guard, not the far one; the faster you move the farther the dream notices; near a guard the powers fade and away they come back, a wary guard watches from farther; the watched speed cap holds |
| `core-tests/Stealth` (`StealthCheckpointTests`) | a checkpoint is taken only when not chased, once, and banks what you have; a restart puts back only what was picked up since; the caught beat is clear, then fades, restarts at full black and fades back; a failed subquest has no fade; the yard's checkpoints are open ground no guard sees from his home |
| `core-tests/Stealth` (`StealthYardTests`) | the graph is one piece and reaches every spot; nothing inside a solid box; the routes fit the movement kit (mantle heights, the 1.15 m underside, the wall's length, the lookout past the wall); light and shade |
| Unity EditMode (`StealthLabEditorTests`) | the scene is wired from the yard (the checkpoints too, pads without colliders); solid vs see-through boxes; the overlay shader compiles; the lab is in Build Settings |
| Unity PlayMode (`StealthLabPlayTests`) | the physics graph is one piece; a seeker finds a player standing still in the open (and the HUD says so); the closet hides you until the sweep opens it; the five collectibles count down and announce the last; the restart; key 5 from the opening round spawns three live guards and back to one; a catch freezes, fades, restarts at the alley's checkpoint and drops only the find picked up after it; with `FailSubquest` a catch ends the round with no restart |

## Known gaps

- The seeker walks a flat floor over its waypoints (no stairs, no NavMesh); it can't climb to a roof, it waits below
  and finds you by sight.
- One player target (`StealthPlayer.Current`). Several seekers work, but don't talk to each other yet.
- Grey-box art: capsule seeker, box yard.
