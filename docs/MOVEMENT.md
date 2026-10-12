# Movement: Titanfall-style first-person movement and the Movement Lab

Fast, momentum-based first-person movement for the Visiting Hours dream sequences (escapes and chases):
sprint, slide, jump and double jump, wallrun and wall jump, mantle. The point is that **speed carries
from one move into the next**: slide → jump → wallrun → wall kick → double jump without losing it.

The research behind every number is `docs/RESEARCH-MOVEMENT.md` (sections 4 and 9: Source formulas,
Titanfall 2's measured wall numbers; section 8: feel). This page is the user's guide.

## Where things are

| What | Where |
|---|---|
| The rules (pure C#, no Unity, tested with dotnet) | `Packages/com.vibejam.jamkit/Runtime/Core/Movement/` (`MoveSim`, `MoveDriver`, `MoveDef`, `MoveMath`) |
| The Unity side, for any template | `Packages/com.vibejam.jamkit/Runtime/Unity/Movement/` (`MovementController`, `MovementCamera`, `MovementBody`, `MovementAudio`, `SpeedLines`, `MovementComfortScreen`, `MovementTuning`, `MovementSurface`) |
| Speed music | `MovementMusic` (stems by speed tier, muffled when slow), `Runtime/Core/Movement/MusicStems.cs` (`StemMixer`: tier selection, bar-quantised switching, fades; the zero-phase low-pass), `MoveMusicSynth.cs` (placeholder stems); real stems go in `Assets/JamKit/Audio/MusicStems/Resources/JamKitMusicStems/<set>/` |
| Sound | `MovementAudio` (the mix), `Runtime/Core/Movement/MoveSounds.cs` (generated loops and fallbacks, the mix rules `MoveSoundMix`), recorded variants in `Runtime/Audio/Movement/Resources/JamKitMovementAudio/` (built by `tools/audio/make-movement-sfx.py`; sources and licences in `Runtime/Audio/ThirdParty/LICENSES.md`) |
| The Movement Lab (feel-test course, chase) | `Assets/JamKit/Templates/MovementLab/` (course data in core: `LabCourse`, `CourseAutopilot`, `PathLine`/`Pursuer`) |
| Tests | `core-tests/Movement` (dotnet), `Assets/JamKit/Templates/MovementLab/Tests` (Unity EditMode and PlayMode) |

Run the lab on its own: **JamKit > Use Template > Movement Lab**, then Play (switch back with
**Use Template > Visiting Hours**). Batch: `-executeMethod JamKit.MovementLab.EditorTools.MovementLabTemplate.UseFromCommandLine`.

## Controls

| Action | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | WASD, arrows | left stick (70 % pushed counts as full) |
| Look | mouse | right stick (response curve, faster after holding it fully over) |
| Jump, double jump, wall jump | Space | A / Cross (also RB, except in Visiting Hours, where RB peeks) |
| Slide / crouch | Left Ctrl or C (hold) | B / Circle (hold), or R3 (toggle: press again or jump to stand) |
| Sprint (forward only) | Left Shift (hold) | **L3 click** (toggle, Titanfall-style: you sprint until you stop moving forward, crouch, slide or respawn; clicking again keeps it on); also LB (hold) except in Visiting Hours, where LB peeks |
| Peek left / right (Visiting Hours) | Q / F (hold) | LB / RB (hold) |
| Wallrun | automatic: jump at a wall *along* it and hold forward | same |
| Mantle | automatic: run or jump at a ledge 0.4 to 1.9 m above your feet | same |
| Lab debug HUD | F3 | d-pad down |
| Gauntlet: restart, ghost (rebindable in Settings > Controls) | R, G | View (Select), d-pad left |
| Dream tool (its chapter only): fire, restart the chapter (both rebindable) | left mouse, R | RT, View (Select) |
| Pause, Comfort and camera | Escape | Start |

**You walk unless you sprint** (the creator, 2026-10-10: `autoSprint` is off by default). On a pad the
two stick clicks pair up the Titanfall way: L3 sprints, R3 crouches and slides.

**Visiting Hours puts the bumpers on peeking** (the creator, 2026-10-12, on a pad: "In the daytime sequences, I
couldn't run normally, I had to use the Left Bumper ... add peeking functionality ... binded to the bumpers"). So in
Visiting Hours, dream and waking world alike: L3 (click) or Shift sprints, A or Space jumps, LB / RB (Q / F) peek.
`ControlBindings.SetPeekOnBumpers` (docs/SHELL.md) does it; the Movement Lab keeps LB on sprint while it runs. The
waking world (St. Brigid's, on the DOOM-like body) hurries the same way: L3 clicked hurries until she stops walking
forward, Shift (or L3 held, with "Sprint on L3: Hold") while held; LB no longer does. It keeps its own speeds (walk
2.2, hurry 3.9 m/s) and has no jump.

The shell's `Dash` action is the jump; `Crouch` (Ctrl, C, pad B), `CrouchToggle` (R3) and `SprintToggle`
(L3) are added at runtime to older input assets that lack them.

**Rebinding** (Pause or Title > Settings > **Controls**, docs/SHELL.md): every row above that's a button
can be moved to another key or pad button, keyboard and pad separately: Jump / Dash, Crouch / Slide
(hold), Crouch / Slide (toggle), Sprint (hold), Sprint (toggle), plus Move and Look's sticks. The two
toggles have an empty keyboard cell, so a keyboard player can add a crouch or sprint toggle key. Two
lines under the table choose what the stick clicks do: **Crouch / Slide on R3: Toggle / Hold** and
**Sprint on L3: Toggle / Hold**. Toggle is the default above; Hold gives R3 to the hold crouch (crouch
and slide while R3 is pressed in, like B) and L3 to the hold sprint. Rebinds and the two choices are
saved and load before the first scene.

**Peeking** (`PeekLean` in the core, `MovementController` in the dream, `VisitingHoursGame` on the DOOM-like body in the
waking world). Hold LB / RB (Q / F) to lean left / right round a corner:
- The eyes slide **0.4 m** sideways and the view rolls **4 degrees** toward the lean, eased in and out (a half cosine)
  over **0.17 s**. Let go and it eases back the same way; switching sides passes through upright.
- **Walls stop it**: each frame a sphere (radius **0.18 m**, the clearance) is swept from the eyes on the body's centre
  line toward the lean point; the lean is cut at once to the room it finds (never eased into a wall), so the camera
  can't see or poke through one. Flat against a wall: no lean, no roll.
- You can lean standing, crouched or walking, but leaning you only **walk slowly** (half a full push of the stick:
  2.5 m/s in the dream, 1.1 in the ward). A **sprint** (held or toggled), a **jump**, a slide, a wallrun or a mantle
  lets it ease back (a step down a stair, a moment in the air under 0.12 s that isn't a jump, doesn't).
- **The body leans with it**: the first-person body's spine bends up to **26 degrees** toward the lean and the hips take
  the rest, so the eyes stay on the head and looking down still shows her chest and legs under her
  (`MovementBodyFrame.lean`, `MixamoBodyRig`).
- **Comfort**: the roll is x Camera roll x Motion effects, and none with Reduce effects; the slide itself is gameplay
  and is never scaled.
- **Stealth**: Theo's sight takes her head where the lean put it (`StealthTarget.lean`, docs/STEALTH.md "Peeking"), so
  a head round a door frame is a small silhouette and stepping out is all of her.
- Theo's first count teaches it ("[LB / RB]  Hold to peek round corners", in the keys of the device in use) until she
  peeks once.
- Numbers: `PeekLeanDef` (the controller's `peek`, the waking game's `peek`). Tests: `core-tests/Movement`
  `PeekLeanTests`; PlayMode `DreamPeekPlayTests` and `VisitingHoursControlsPlayTests` (a real test gamepad and keyboard).

**The sprint kick.** Sprinting on the ground from well under sprint speed (below 60 %: after a bad landing,
running into a wall or ledge, out of a mantle, from a standstill) pushes you off at once to 75 % of sprint
along the stick, and ground acceleration does the rest: sprint speed in about 0.05 s. It never takes you past
sprint (high speed still only comes from slides, wallruns, wall kicks and air strafing), it has a 2 s
cooldown, and it readies the slide boost, so a slide straight after it gets its +3 m/s. You feel it as a
short FOV punch (4°), a push-off sound (a hard, low footstep and a body thud under a little whoosh), and the arms pumping back.
Why it readies the boost: plain ground acceleration already gets you back to sprint in about 0.15 s, so
what really made a stumble feel dead was the slide boost's 2 s cooldown (no +3 m/s on the next slide). The
kick's cooldown equals the boost's, so it never boosts more often than sliding alone would.

## The states

| State | What it is | How you get in / out |
|---|---|---|
| **Grounded** | Walking. Source acceleration and friction. | Landing, mantle end. |
| **Sprint** | Grounded at sprint speed, moving forward with Sprint held or toggled. | Moving forward faster than walk with sprint on; a sprint kick pushes you into it from a stumble. |
| **Slide** | Crouched, low friction, a speed boost on a 2 s cooldown, downhill slopes speed you up. | Crouch while running at 6.5 m/s or more, or land with crouch held (slide hop). Ends below 4 m/s or when you let go of crouch. Jumping out keeps your horizontal speed, but for 3% of what's above sprint (a slide hop beats a plain hop's 8%). |
| **Air** | Gravity, Source air strafing (small cap: slow, real gains only by turning with strafe input; W alone only turns your flight above 5 m/s), gentle low-speed steering. | Jump, walk off a ledge, leave a wall. |
| **Wallrun** | Along a near-vertical wall: you settle toward 8.64 m/s, weightless at first and sinking later, for at most 1.75 s. | Jump at a wall moving along it (not head-on), holding forward, 0.6 m or more above the ground. Ends by time, if the wall ends, if you push away, crouch, or stop holding forward. Jump = wall jump. |
| **Mantle** | Pulled up onto a ledge in 0.2 + 0.07 s per metre. | Running or falling into a ledge 0.4 to 1.9 m above your feet while holding forward. You leave at 80 % of your speed (at least 5 m/s). |

**Momentum rules** (what keeps your speed): a jump at sprint speed or below never touches horizontal speed;
above sprint a ground jump is free under the default Mild hop-cost preset (the Current preset keeps all but 8% of
the excess, a slide hop 3%: see "Hop-cost presets");
landing has 0.15 s with no friction, but speed over sprint fades from the landing tick (1.5/s of the excess)
instead of being braked, and you can still steer it; catching a wall keeps your full speed (turned along the
wall); a wall jump *adds* speed (about +3.1 m/s at wallrun speed); the double jump turns you toward the stick
without losing speed, except right after a ground jump (the hop-and-steer pays a little); collisions only
remove the part of your velocity going into the surface.
**The wall kick:** a jump in the first 83 ms on a wall keeps almost everything (staying on the wall
lets wall friction pull a fast run down toward 8.64 m/s), so quick kicks are the fast line.

## Why air strafing gains speed

The creator found this and wanted it kept (2026-10-10). On 2026-10-11 they found keyboard and mouse far too
easy (a first Gauntlet run in 24 s against 31.42 on a controller) and picked two changes, below in "Air
strafing and bunny hops". Strafing with strafe input is unchanged: `SprintTests.AirStrafeGainIsUnchanged` still
pins the old speed curve with the hop costs switched off.

- **What the air step does** (`MoveMath.AirAccelerate`, Source's formula). Each 1/120 s tick, it looks only at
  how fast you already move *along the stick's direction* (the projection of your velocity on it). It tops that
  projection up to at most **airCap = 0.8 m/s**. The size of one top-up is limited by the *full* wish speed:
  airAccel × sprint speed / tick rate = 12 × 9.5 / 120 ≈ **0.95 m/s**. So each tick adds
  min(0.95, 0.8 − projection) along the stick.
- **Why that grows your speed.** Hold a strafe direction (A or D, or the stick sideways) and turn the view the
  same way. The stick's direction then stays nearly at right angles to your velocity, and the projection stays
  near 0. Every tick then adds up to about 0.8 m/s *sideways*. A sideways add never slows you; it lengthens
  your velocity by Pythagoras: √(v² + 0.8²).
  - At 10 m/s that is about +0.03 m/s per tick.
  - The air control (airControlSpeed 5, airControlAccel 2) adds up to another 0.08 m/s along the stick each
    tick. It is a second Source accelerate with a 5 m/s cap instead of 0.8, so it strafes too, and it did so
    with W alone (see below).
- **How fast you gain depends on how fast you turn.** Each add also turns your velocity (by about 4.6° per tick
  at 10 m/s). If the view turns more slowly than that, the projection creeps up toward 0.8 and the adds shrink.
  For example, turning 120°/s while bunny hopping takes you from 9.5 to about 11 m/s in 1 s and about 15.4 m/s
  in 4 s. Straight-line strafing without turning gains nothing.
- **The cap.** Horizontal speed is clamped to **maxHorizontalSpeed = 22 m/s** (about 2.3× sprint), in the air
  and on the ground.
- **Keeping it through landings.**
  - For **0.15 s after landing** (landingGrace), while the stick is pushed, there is no ground friction. Since
    2026-10-11 the overspeed fade runs during it (it used to freeze your speed). If you let go of the stick,
    friction starts at once.
  - A jump pressed up to **0.12 s before landing** (jumpBuffer) fires on the landing step itself, so a bunny
    hop never touches friction. (A press in the air fires the double jump if you still have it: hoppers spend
    it first.)
  - A ground jump above sprint is free under Mild (the default); under Current it costs 8% of the excess (a slide
    hop 3%). At or below sprint it's always free. See "Hop-cost presets".
  - On the ground, speed above sprint fades by only 1.5 per second of the excess (overspeedDecay)
    while you hold a direction, so a short run between hops costs little.

## Air strafing and bunny hops (2026-10-11)

**What the creator found** (keyboard and mouse, the Movement Lab): holding only W and moving the mouse gained
speed; at the cap, chained jumps (with a slide now and then) held top speed; air turns cost nothing; the first
keyboard Gauntlet took 24 s against 31.42 on a controller. Their words: the sticks are hard because "you have to
continuously move the small joysticks at different speeds", and "KBM didn't even need to hold A or D". Bunny
hopping was "easy as anything", steered by the double jump right after the jump, and cost no momentum at all,
while a slide at speed always burns a bit.

**Why** (the investigation, from the code):
- The **air control** (`MoveSim.StepAir`, airControlSpeed 5 / airControlAccel 2) was a plain Source accelerate
  with no air cap: a second air strafe with a 5 m/s cap, six times airCap. With W held and the view 63-76° off
  your flight (a steady mouse turn), it added up to 10 m/s² along the view: +4.6 m/s per second at 9.5 m/s,
  +2.4 at 19 m/s, with only a 30°/s turn at speed. The real air strafe also gains with W alone if the view sits
  about 90° off the flight (it's A/D strafing with the camera turned).
- The real air strafe's cap applies per tick, and the lab runs 120 ticks a second: about 1.8× a 66-tick Source
  server's gain (RESEARCH-MOVEMENT 2.3).
- Nothing in the air takes speed away; with the view 90° off, the flight turns 450°/s at 13 m/s *and* gains.
- Hops were free: the jump buffer fires on the landing tick before friction, and the 0.15 s grace froze speed.
- A pad looks at 190°/s (304 boosted, with a response curve); the mouse has no limit. The best W-only mouse
  rate was about 550°/s, the best A/D rates 350-550°/s.

**What changed** (the creator picked options 1 and 5 of the investigation):

1. **Above 5 m/s, speed gained in the air needs strafe input** (`airStrafeSideShare` 0.5). The move input must
   point at least 30° sideways (its sideways share 0.5: A or D, W+A, a stick pushed sideways or diagonally) for
   the full gain, less in proportion, none for forward alone. Both air pushes (the air strafe and the air
   control) keep their direction, so W alone still **turns your flight toward the view**; it just adds no speed.
   Below 5 m/s nothing changed: standing and slow jumps aim as before.
   - Option 1 was "air control redirect-only". Redirect-only on its own would have cut **controller** strafing
     25-40% (a pad at 190°/s: +2.7 m/s in a second from 9.5 became +1.7), against the creator's "don't paralyze
     controller air strafing". So the air control may still add speed *with strafe input*
     (`airControlStrafeGain` 1); set it to 0 for the strict redirect-only version.
   - Measured through the real sim (`AirAndHopTests.MouseAgainstPadBeforeAndAfter`, the best rate up to each
     look limit):

| Look | W only, 1 s from 9.5 m/s | A/D or stick sideways, 2 s from 13 | Straight course, 3 s from 13 (mean / end) |
|---|---|---|---|
| Mouse | 13.10 → **9.50** | 18.78 → 18.78 | 16.92 / 19.49, unchanged |
| Pad, 304°/s | 12.18 → **9.50** | 18.59 → 18.59 | 16.58 / 20.10, unchanged |
| Pad, 190°/s | 11.31 → **9.50** | 17.45 → 17.45 | 15.76 / 18.64, unchanged |

   - Side effect: with W alone, spinning the view very fast (720°/s and up) now loses more than before (13 →
     6.7 m/s in a second at 720°/s, was 10.1): the backward half of the spin still brakes, and nothing wins it
     back. Holding W while looking behind you brakes, as it always did.
2. **Bunny hops cost a little, mistakes cost more, slides do better.** (This is now the **Current** preset; the
   default became **Mild** the same day: see "Hop-cost presets" below.)
   - **A clean hop** (a ground jump, buffered onto the landing tick) keeps all speed up to sprint and loses
     **8% of the speed above it** (`hopSpeedLoss`): 0.28 m/s at 13, 0.52 at 16, 0.84 at 20, about 1.0 at 22. A
     0.4 s slide at speed burns 1.0. Jumps at running speed are free, so ordinary platforming is untouched.
   - **A slide hop** (crouch pressed before landing, jump out of the slide) loses **3%** (`slideHopSpeedLoss`),
     plus a tick of slide drag, and gets the slide boost if it's ready: 12 → 12.90 (a plain hop: 11.80),
     16 → 15.81 (15.48), 20 → 19.69 (19.16).
   - **A late hop** pays its time on the ground: the grace no longer freezes speed (`landingGraceDecay` 1: the
     1.5/s overspeed fade runs from the landing tick), then the 8%. At 18 m/s: clean 17.32, 0.08 s late 16.40,
     0.25 s late 14.87 (a 0.08 s late hop used to keep all 18). Pressing too early fires the double jump
     instead, so you land without a jump and pay the same way.
   - **Hop and steer**: a double jump within **0.35 s** of a ground jump (`doubleJumpHopWindow`) pays for its
     turn: **10% of the speed above sprint per 90° the flight turns** (`doubleJumpHopTurnLoss`). The usual
     redirect, stick square to the flight, turns it 45°: 5%, 0.28 m/s at 16 m/s. Later double jumps and wall
     jumps turn for free. Set it to 0 to remove it.
   - Untouched: the jump buffer (0.12 s), coyote time, the wall jump and wall kick, the slide boost.
   - The Gauntlet's autopilot is 0.8 s slower for it (41.75 s; it hops a few times above sprint).

### Hop-cost presets (2026-10-11)

The creator on a controller, after the costs above: "My new first run through was 33s through the DT portion [best
28.17]... the slower increase in air strafing wasn't felt as much as the insane bleed-off from bunny hops." The air
rule passed; the hop bleed was too strong. So the hop costs became three presets, switched in game with **F6** or
**LB held + d-pad up** (shown on the lab's F3 / d-pad-down HUD, `HopCostSwitch`), kept in the save
(`movement_hop_cost`), default **Mild**. The air rule is the same in all three.

| Preset | Clean hop | Slide hop | Late hop | Double jump right after a hop |
|---|---|---|---|---|
| **Off** | free | free | free inside the 0.15 s grace (before 2026-10-11) | free |
| **Mild** (default) | 2% of the speed above sprint (since 2026-10-12; free before) | 1% | the overspeed fade at half rate (0.75/s of the excess) from the landing tick | the first 45° free, then 10% of the excess per 90° |
| **Current** (8454370) | 8% of the speed above sprint | 3% | the full 1.5/s fade, then the 8% | 10% of the excess per 90° from the first degree |

What each costs, from the core sim and the autopilot (`HopPresetTests`, printed in the test output):

| Preset | 10 hops from 20 m/s, clean | each 0.08 s late | slide hops | 45° hop-steer at 16 m/s | 75° | Gauntlet autopilot | Dream tool autopilot |
|---|---|---|---|---|---|---|---|
| Off | 20.00 | 20.00 | 20.00 | 0 | 0 | 40.93 s | 44.07 s |
| Mild | 20.00 | 15.83 | 20.00 | 0 | 0.19 m/s | 41.32 s | 44.15 s |
| Current | 14.06 | 11.16 | 16.84 | 0.30 m/s | 0.48 m/s | 41.75 s | 44.23 s |

The autopilot barely hops above sprint, so its times move by under a second; a player chaining hops at speed feels
the 10-hop column. (Ten hops from 20 m/s take about 10 s, each flight with a double jump at the top.)

How it works: the presets live in core `HopCostPresets`; the switch sets a global override that every `MoveSim`
reads instead of its def's hop fields (`hopSpeedLoss`, `slideHopSpeedLoss`, `landingGraceDecay`,
`doubleJumpHopWindow`, `doubleJumpHopTurnLoss`, `doubleJumpHopTurnFree`), so no tuning asset is written. Without the
switch (another game, the core tests) a sim uses its def, whose defaults are Mild.

**What to judge (the creator):** controller strafing on the stick (sideways or diagonal) should feel the same
as before; W alone should steer but never speed you up; a hop chain should bleed a little unless you slide-hop
or strafe it back; a late hop should sting. All of it is in the tuning asset to adjust.

## One shared tuning (2026-10-11)

The creator, after the Mild preset (a dream tool run of 26.45 s on a controller, best before 28.17): "this movement
is good enough to implement universally". So every game that uses the movement now shares **one** tuning asset:
`Packages/com.vibejam.jamkit/Runtime/Unity/Movement/Resources/JamKitMovementTuning.asset`
(`MovementTuning.SharedAssetPath`, loaded at runtime as `MovementTuning.Shared`).
- **Who uses it:** the Movement Lab, the dream escape and the dream house, the Stealth Lab and the Deep Block. Their
  builders all point at it (`MovementLabBuilder.TuningPath`, `DreamEscapeBuilder.TuningPath`,
  `StealthLabBuilder.MovementTuningPath`, `DeepBlockBuilder.MovementTuningPath`).
- **No tuning set:** a `MovementController` with no tuning of its own uses it too.
- **The hop cost:** the Mild preset is the default, and F6 / LB + d-pad up still switches it in the lab and the dream
  escape, for the keyboard-and-mouse check.
- **It's the lab's asset, moved:** it is the Movement Lab's own tuning asset moved into the package (same GUID, so the
  lab's scene still points at it). One fix came with it:
  - **What was wrong:** the lab's asset was made before the 2026-10-10 wall-feel change (the creator's controller
    notes), so it still had the old wall values: `wallRunFriction` 2, `wallProbeReach` 0.5, `wallMinTowardDot` -0.1,
    `wallAwayDot` 0.5. Every other scene, and every test, used the new ones (1.2, 0.6, -0.2, 0.8).
  - **The fix:** the asset upgrade (`MovementTuning.defaultsVersion` 2) gives it the new wall values.
  - **The result:** the shared asset now equals `MoveDef`'s defaults (`SharedTuningTests`), so every dotnet course
    test and autopilot pin is what the scenes play.
  - **What changes in the lab:** walls keep more speed on entry and catch a little more easily than the lab had
    before. To put any of the four back, edit the shared asset.
- **Route times (the Deep Block's, in the sim) are unchanged:** street 14.2 s, back 18.4 s, roofs 14.9 s, under
  22.1 s. They ran on the defaults before, and the shared asset equals them now.
- **The dream umbrella's "soaked" movement** still makes its runtime copy of whatever tuning the player has, which is
  now the shared one.

## One standard for mouse and stick (2026-10-12)

The creator on keyboard and mouse, with the shared tuning: "I'm gaining speed just by turning my mouse while
sprinting on the ground. I can get to 19 m/s and then continue that momentum for completely free just jumping." Then a
dream tool run of 20.53 s, against 26.45 s on a controller.

**The root cause:**
- Ground acceleration was Source's: it adds speed along the wish direction until the velocity's *projection* on it
  reaches the wish speed, with no limit on the resulting speed.
- Above sprint the ground step has no friction, only the slow 1.5/s overspeed fade.
- So a view kept 45-60° ahead of the run kept the projection under 9.5 m/s and kept adding sideways speed:
  - a 360°/s sweep went from 9.5 m/s to the 22 m/s cap in 10 s;
  - the view held 55° ahead of the run reached 15.7 m/s;
  - **a stick at its boosted 304°/s reached 19.9 m/s too**.
- The mouse did it easily because it turns as fast as the hand.
- Mild's clean hops were free, so the speed then carried.

**The fixes:**
1. **Ground acceleration never raises the speed past the larger of what you had and the wish speed** (`MoveSim`,
   the ground step): it brings you up to sprint and steers, never more.
   - Above sprint a turn keeps your speed (the 220°/s ground turn) and the overspeed fade runs as before.
   - Pinned at every sweep rate, flicks included, the stick's too (`GroundStrafeTests`, and input-driven on both
     devices in `MouseAndStickSpeedPlayTests`).
2. **Hops bleed a little:** Mild now costs 2% of the speed above sprint per clean hop, 1% per slide hop (it was free).
   - Ten hops from 19 m/s end at about 17.3 m/s; from 20 m/s, 18.1.
   - **The fair carry, as the tests say it:** a chain keeps most of a speed you earned, but at least 1 m/s goes over
     ten hops.
   - Late hops cost more, as before; Current and Off are unchanged.
3. **An air-strafe turn-rate equalizer** (`airStrafeTurnCap`, 320°/s, just above a stick's 304):
   - An air-strafe gain made while the view turns faster than that counts only in proportion (at 640°/s, half).
   - A stick never turns that fast, so the controller's strafing is exactly as before (tested at 120, 190 and 304°/s).
   - The best mouse strafe (any rate) is now +5.69 m/s in 2 s from 13 m/s, against the best stick's +5.59.

**Why not separate PC and controller movement.**
- The exploit wasn't a device difference: the stick could do it too, only harder. Separate tuning would have left it in.
- Once it's fixed, the two devices strafe within 2% of each other at their best, so a split would buy nothing.
- A split would also cost:
  - two feels that change the moment you pick up the other device;
  - two sets of numbers to keep balanced;
  - routes and records that no longer compare.
- The one input difference left, the mouse turning faster than any stick, is answered by the equalizer, which is
  invisible on a stick.
- If the creator still wants device profiles after feeling this, the hop presets can be split per device in a few
  lines.

**The lab's panel** (F3 / d-pad down) now shows:
- the input in use (keyboard and mouse, or gamepad);
- how fast the view is turning;
- above 320°/s, the share of the air strafe that still counts.

The big speed readout shows the sprint cap holding.

**Times:**
- The autopilot runs the dream tool chapter in 44.17 s and the Gauntlet in 41.37 s (Mild).
- The Deep Block's four routes moved a little: street 14.2 to 14.3 s, back 18.4 to 18.7, roofs 14.9 to 15.7,
  under 22.1 to 22.8.
- The autopilot never ground-strafed, so these barely move. A human keyboard-and-mouse run should now land near the
  controller's time.
- **For the creator to compare:** run the dream tool chapter (presenter chapter "Dream tool") on keyboard and mouse,
  then on a controller.
  - Expect both near 25-27 s, not 20.
  - Holding W and sweeping the mouse no longer gains anything: watch the panel's speed stay at 9.5 while sprinting.

## Profiles: dream and daytime (2026-10-11)

The creator: "in the daytime sections, Nadia can't double jump, wallrun, slide, have jump fatigue, and the air
strafing coefficient should be turned down as well". A **movement profile** (core `MoveProfile`) sits on top of the
shared tuning:

| | Dream (default) | Daytime |
|---|---|---|
| Double jump | yes | no |
| Wallrun | yes | no |
| Slide | yes | no: crouch at speed only crouches (no slide boost, no slide hop) |
| Air strafe | ×1 | **×0.3** (`airStrafeScale`) on both the per-tick cap and the acceleration |
| Jump fatigue | off | **on** (`jumpFatigue`): jumps each within 0.8 s of landing from the last form a chain; the first **3** are full height (`jumpFatigueFreeJumps`), then each is 25% of the height lower, down to 50%; standing 0.8 s resets the chain |

- **Why 0.3 for the air strafe:** the speed gained per tick goes with the square of the cap, so 0.3 keeps about 9% of
  the dream's gain.
  - Measured: the best strafe in a second from 9.5 m/s gains 3.96 m/s in the dream and 0.60 in daytime
    (`TheDaytimeAirStrafeIsWellDown`).
  - A daytime jump still bends a little toward the stick, but strafing can't build speed.
  - The slow-jump air control (below 5 m/s) is untouched, so a standing jump can still be aimed.
- **Jump fatigue: confirmed 2026-10-12.** The creator: "Jump fatigue should be on for daytime, but only kick in after
  3 jumps, not the current 1-2." So three quick jumps in a row are full height and the fourth is the first lower one
  (before, the second already was). The chain rule is unchanged: a jump counts as "quick" if she stood on the ground
  0.8 s or less (`jumpFatigueRecover`) before it, and standing longer than that starts a fresh chain. The flag and the
  numbers are in the shared asset's `daytime` profile.
  - Heights in a chain: 100%, 100%, 100%, 75%, 50% (the floor), 50%...; measured on flat ground: 1.22, 1.22, 1.22,
    0.91, 0.60 and 0.60 m, and after 1.5 s standing 1.22 m again (`JumpFatigueSparesThreeQuickJumpsLowersTheFourthAndRestingResetsIt`).
- **Where the values live:** the daytime values are in the shared asset (`MovementTuning.daytime`), so they are edited
  in one place.
- **In code:** a controller picks its profile with `MovementController.profile` (`MoveProfileKind.Dream` or
  `Daytime`), or `SetProfile(...)` at runtime. A `MoveSim` with no profile allows everything.
- **Where it applies:**
  - Every scene built on the movement today is a dream or a lab, and uses Dream.
  - Visiting Hours' daytime (waking-world) sections, St. Brigid's and the booth, don't use the movement kit at all.
    They walk on the DOOM-like's controller (`DoomPlayer`), which has no jump, double jump, slide or wallrun. They
    take the dream's sprint keys (L3 click, Shift) and its peek (see "Controls").
  - When a waking section moves onto the movement kit, set its controller's `profile` to `Daytime`.
- **Try it in the Movement Lab (or the dream escape):** **F7** or **LB + d-pad right** switches the player between
  the two profiles (`MovementProfileSwitch`). The F3 / d-pad-down panel shows "Movement: Daytime" or "Dream". It
  isn't saved: each scene starts in its own profile.

## Every tunable number

All numbers live in one `MoveDef` (core), held by a **MovementTuning** asset
(the shared one, `Packages/com.vibejam.jamkit/Runtime/Unity/Movement/Resources/JamKitMovementTuning.asset`, which
every game uses; create more with
Assets > Create > JamKit > Movement Tuning). Edit it in Play mode: changes apply at once.
Metres, seconds, m/s, degrees.

### Simulation and body
| Field | Default | What it does |
|---|---|---|
| tickRate | 120 | Fixed simulation steps per second. Air strafing depends on it; don't change it per platform. |
| maxStepsPerFrame | 6 | A long hitch runs at most this many steps (the rest is dropped). |
| radius / standHeight / slideHeight | 0.38 / 1.7 / 1.0 | The capsule. Sliding fits under 1.15 m bars. |
| eyeHeight / slideEyeHeight / eyeBlendTime | 1.5 / 0.8 / 0.12 | Where the camera sits, and how fast it drops into a slide. |
| slopeLimit | 50 | Steepest walkable ground. |
| groundSnap / groundEpsilon | 0.4 / 0.08 | Ground within 0.4 m keeps you stuck to slopes and small drops; within 0.08 counts as standing. |

### Ground
| Field | Default | What it does |
|---|---|---|
| walkSpeed / sprintSpeed / crouchSpeed | 5 / 9.5 / 3 | Wish speeds. Sprint is the DOOM-like's run speed. |
| autoSprint | **off** | Off: walk unless Sprint is held or toggled. On: moving forward always sprints. Tuning assets made before 2026-10-10 are switched off once by the builders (`MovementTuning.defaultsVersion`). |
| sprintForwardMin | 0.3 | Sprint only runs forward-ish (the stick's forward share at least this). |
| sprintToggleStopForward | 0.2 | The L3 toggle ends on the ground when the stick's forward part drops below this. |
| kickBelow | 0.6 | Sprinting below this share of sprint speed on the ground gives a sprint kick. |
| kickSpeed | 0.75 | The kick sets your speed along the stick to this share of sprint at once (never above sprint). |
| kickCooldown | 2 | Seconds between kicks (equal to the slide boost's). |
| kickRearmsSlideBoost | on | The kick readies the slide boost. |
| groundAccel | 10 | Source accelerate: full speed in about 0.1 s. |
| friction / stopSpeed | 6 / 2.5 | Source friction; constant below stopSpeed so you stop. |
| overspeedDecay | 1.5 | Speed above sprint fades at this rate (per second, of the excess) while you hold a direction. |
| groundTurnRate | 220 | Above sprint you still turn toward the stick this fast (deg/s) without losing speed. |
| landingGrace | 0.15 | No friction right after landing. |
| landingGraceDecay | 0.5 | How much of overspeedDecay runs during the grace (1: a late hop pays its time on the ground; 0: the old frozen grace). Mild 0.5, Current 1, Off 0. |
| stickFullAt | 0.7 | Stick push that counts as full (gamepad parity). |

### Jumps and air
| Field | Default | What it does |
|---|---|---|
| gravity | 24 | m/s². |
| jumpHeight | 1.25 | 7.75 m/s up. |
| doubleJumpHeight | 1.0 | 6.93 m/s up; one per airtime, refreshed on the ground, on a wall and by a mantle. |
| doubleJumpLockout | 0.1 | Not this soon after a jump (a buffered press fires when it ends). |
| doubleJumpRedirect | 0.5 | How far the double jump turns your flight toward the stick (speed kept). |
| doubleJumpMinSpeed | 4 | A standing double jump still pushes you this fast toward the stick. |
| doubleJumpForwardBoost | 1 | The double jump also pushes you this much along the stick (or the view, stick neutral and moving forward), never past sprint: getting started without a wall nearby (the creator, 2026-10-10). |
| coyoteTime | 0.12 | Jump still works this long after walking off a ledge. |
| jumpBuffer | 0.12 | A press this early still fires on landing, on a wall or at the end of a mantle. |
| hopSpeedLoss / slideHopSpeedLoss | 0 / 0 | A ground jump (out of a slide) loses this share of the speed above sprint. 0 = free hops (Mild, Off); Current 0.08 / 0.03. |
| doubleJumpHopWindow / doubleJumpHopTurnLoss / doubleJumpHopTurnFree | 0.35 / 0.1 / 45 | A double jump this soon after a ground jump loses this share of the speed above sprint per 90° it turns your flight, past the first doubleJumpHopTurnFree degrees (Mild 45, Current 0; Off: no loss). The in-game hop-cost preset overrides all the hop fields. |
| airAccel / airCap | 12 / 0.8 | Source air strafing (cap 0.8 m/s = 30 Quake units). |
| airControlSpeed / airControlAccel | 5 / 2 | Steering of slow jumps; above 5 m/s it adds speed only with strafe input. |
| airStrafeSideShare | 0.5 | Above 5 m/s, speed gained in the air needs the move input this far sideways for the full gain (W alone: none, a turn only). 0 = pure Source. |
| airControlStrafeGain | 1 | 1: the air control adds speed with strafe input (controller strafing as before); 0: redirect only. |
| maxHorizontalSpeed | 22 | Hard cap (about 2.3x sprint). |
| terminalFall | 40 | Fastest fall. |

### Slide
| Field | Default | What it does |
|---|---|---|
| slideMinSpeed | 6.5 | Crouch at this speed or more slides. |
| slideBoost / slideBoostCooldown | 3 / 2 | +3 m/s when a slide starts, once every 2 s (Titanfall 2's ~2 s). |
| slideBoostMaxSpeed / slideCapDecay | 13 / 1.5 | The boost never takes you past 13 m/s; that cap drops 1.5 m/s per second of recent sliding (Titanfall 2: the cap decays the longer you slide) and recovers off the slide. |
| slideDecelEarly / slideEarlyTime / slideDecel | 2.5 / 0.6 / 4 | Slide braking: gentle for 0.6 s, then firmer. |
| slideSlopeScale | 1.5 | Downhill pull on slopes (1.5x gravity along the slope): slide ramps clearly speed you up. |
| slideExitSpeed | 4 | The slide ends below this. |
| slideTurnRate | 60 | Steering while sliding, deg/s. |
| slideJumpScale | 0.9 | A slide jump is 90 % of a full jump's height; horizontal speed is all kept. |
| slideHopBuffer | 0.2 | Crouch pressed this long before landing lands you straight into a slide (slide hop). |

### Wallrun and wall jump (Titanfall 2 measured values, RESEARCH-MOVEMENT section 9)
| Field | Default | What it does |
|---|---|---|
| wallMinSpeed / wallMinHeight | 5 / 0.6 | You need this speed and this much air under you. |
| wallMaxNormalY | 0.25 | Walls must be near vertical. |
| wallMinAlongDot | 0.34 | Moving along the wall (within 70°), not into it (head-on is a mantle). |
| wallMinTowardDot / wallForwardDot | -0.2 / 0.3 | To start a wallrun the stick may not push away, and must point forward along the wall (Titanfall 2: forward input to start). |
| wallHoldForwardDot | 0.1 | Once on the wall, any stick with a forward part along it, or held at an angle while the view looks along it, counts as forward. Only clear backward input (or no stick) brakes. |
| wallCatchBuffer | 0.1 | A catch refused only for the stick's direction is retried this long, reaching 0.4 m further, as the stick comes round. |
| wallProbeHeight / wallProbeRadius / wallProbeReach | 1.0 / 0.25 / **0.6** | The forgiving wall search (0.5 until 2026-10-10): from chest height, up to 0.6 m beyond your body. |
| **wallRunSpeed** | **8.64** | The goal speed (340 u/s): slower runs accelerate to it, faster ones are pulled down to it by wall friction. |
| wallRunAccel / wallRunFriction | 6 / **1.2** | Acceleration up to the goal; above it, after the grace, the excess fades at 1.2/s (was 2: Research 9 measures the 340 u/s goal, not a rate, and 2 bled speed fast). |
| **wallSpeedGrace** | **0.5** | The first half second on a wall keeps all your speed (no friction); a kick in it keeps the entry speed. A 14 m/s entry is 14.0 at 0.45 s, about 11 at 1.2 s. |
| wallNeutralFriction | 4 | No forward input (or into the wall): braking to a stop (backwards: twice). |
| **wallRunDuration** | **1.75** | The longest run. |
| wallGravityStart / wallGravityEnd | 0.1 / 0.8 | Nearly weightless at first, sinking later (upward speed from a jump is kept at full gravity). |
| wallSinkStart / wallSinkEnd | 1 / 6 | Fastest sink, growing with time on the wall. |
| wallStick | 0.5 | A small push keeps contact. |
| wallLostGrace | 0.1 | Seams and short gaps in a wall don't end the run. |
| wallExitSpeed | 4 | The run ends below this along the wall. |
| wallAwayDot / wallAwayTime | 0.8 / 0.06 | The stick pointing more than about 53° away from the wall for 0.06 s ends the run (Titanfall 2 lets go at once; 0.5 / at once until 2026-10-10: a stick sweeping round dropped controller players). |
| **wallJumpOut** | **5.21** | Every wall jump pushes this much along the wall normal (205 u/s). |
| **wallJumpAlong** | **1.91** | Plus up to this along the wall, by how much the stick points along it (75 u/s). |
| **wallJumpInward** | **4.37** | Minus up to this along the normal when the stick points into the wall (172 u/s x dot). |
| **wallJumpCrouchKick** | **1.78** | Crouch held during the wall jump adds this along the normal (70 u/s). |
| wallJumpUp | 7.75 | Upward speed of a wall jump. |
| **wallKickWindow** | **0.083** | A wall jump within 5 ticks (at 60 Hz) of contact counts as a wall kick. |
| wallCoyote | 0.2 | A jump just after leaving a wall still wall-jumps. |
| sameWallDrop / sameWallAngle | 0.1 / 25 | The same wall takes you again only lower than where you left it; a wall turned 25° or more counts as a new wall. |

### Mantle
| Field | Default | What it does |
|---|---|---|
| mantleMinHeight / mantleMaxHeight | 0.4 / 1.9 | Ledge heights above your feet it climbs. |
| mantleReach | 0.6 | How close the ledge face must be. |
| mantleMaxFall | 10 | Not when falling faster than this. |
| mantleBaseTime / mantlePerMetre | 0.2 / 0.07 | Duration. |
| mantleSpeedKeep / mantleExitSpeed | 0.8 / 5 | You leave at 80 % of your entry speed, at least 5 m/s. |

### Feel components (inspector fields)
| Component | Fields (defaults) |
|---|---|
| `MovementCamera` (on the rig) | speedFov 8° from 9.5 to 22 m/s, **slideFovScale 1.1 over 0.25 s in and out** (TF2; slideFov extra degrees 0), fovRate 6; sprintKickFov 4° over sprintKickTime 0.35 s; wallRoll 9° away from the wall, wallCatchKick 4° (the lean-in on contact), slideRoll 2°, roll spring 14 rad/s damping 0.65; landDip 0.12 m from 4 to 18 m/s impact (spring 20), **slideEntryDip 0.05 m** (TF2: the sudden dip into a slide); bobAmount 0.035 m; mantleNod 3° |
| `MovementBody` (on the camera) | The first-person body and its feel (**The body** below): `rig` (a MovementBodyRig; the lab and the escape wire Nadia's model), `feelCamera`; the placeholder capsules' colours and proportions when no rig is wired. Every feel number is in the tuning asset's **Body** section (`BodyFeelDef`). |
| `SpeedLines` (on the camera) | 56 edge streaks from 12 to 20 m/s, alpha up to 0.35, never in the centre of the screen |
| `MovementAudio` (on the body) | windVolume 0.5, windHighVolume 0.4, slideVolume 0.45, wallVolume 0.3, stepVolume 0.32, effectsVolume 0.8, pitchVariance 0.05, volumeVariance 0.12; speed: music (the MovementMusic it drives), speedFeel (whose tier it follows; found on the camera), musicVolume 0.45, supersonicVolume 0.14, worldDuck 0.45, breathVolume 0.05, breathBuild 8 s, breathRecover 10 s. What each sound is and when it plays: **Sound** and **Speed music** below. |
| `MovementController` | mouseDegreesPerPixel 0.09, gamepadDegreesPerSecond 190 (x1.6 boost after 0.25 s fully over), maxPitch 85, managePointerLock, onlyMarkedWalls, worldMask, skinWidth 0.04, stepOffset 0.35 standing, **crouchStepOffset 0** (the CharacterController's step-up pass lifts the capsule by the step offset before it sweeps forward: with 0.35 while sliding, a slide stopped dead at the face of a 1.15 m bar) |

## The body

**Who:** in the Movement Lab and the dream escape the first-person body is **Nadia's real model** (Visiting Hours'
`Models/Nadia/Nadia.fbx`, Tripo, Mixamo bones), in the style-E waking material with her colour texture. In the dream
she's the one real 3D thing in a billboard world: a stranger inside the dream. She wears what's on the model (the
olive jacket over navy scrubs, the lanyard and badge). **A casual-clothes variant may come later** (a second model or
texture; the builders take a path). Without the model the PLACEHOLDER capsule arms (and slide legs) draw instead, so
other templates, tests and headless runs work the same.

**How it's built (three parts):**
- **The feel**, `Runtime/Core/Movement/BodyFeel.cs`. This is plain C#, tested in `core-tests/Movement`. It turns the
  movement into a pose each frame: where the hands go (camera space), where the feet go (body space), and the torso's
  lean, the landing dip, the sway, the breath and the lanyard swing. Every number is in **`BodyFeelDef`**, commented,
  in the tuning asset's **Body** section (`MovementTuning.body`), editable in Play mode.
- **The seam**, `MovementBodyRig`. `MovementBody` (on the camera) fills the feel's input and hands the pose to its
  `rig`. When no rig is wired, or the rig isn't ready, it poses the capsules.
- **The rig**, `MixamoBodyRig`, for any Mixamo-boned model. Each frame it:
  - puts the model back in its bind pose;
  - stands her at the body's feet, facing its yaw, and leans her torso;
  - puts her eyes on the controller's head, plus the body's dip;
  - aims her head where the camera looks: fully in the F4 orbit view, but in first person the neck follows only a
    quarter of the pitch, at most 20° from her torso (`neckFollow`, `neckFollowMax`), so her shadow nods a little;
  - reaches her arms and legs to the targets with two-bone IK (the elbows and knees towards their hints, the
    collarbone helping a far hand by up to 20°).

  In first person the visible copy is cut away inside one sphere (the waking shader's `_CameraCutout`, set per
  frame) around two things: her head, hair and neck wherever they're posed, and 0.1 m around the camera
  (`cameraClearance`: the collar right under it). The head's sphere (`headCentre`, `headRadius`, in the head bone's
  frame; about 0.18 m for Nadia) is measured from the mesh by `MovementBodySetup.Wire` when the builders wire her
  (her mesh isn't Read/Write, so a player can't measure it) and saved with the scene; an unmeasured rig estimates it
  from the bones. On a frame the body isn't posed in, the visible copy hides. A shadow-only copy shares the bones and
  casts the whole body's shadow, head included. She has no colliders and sits on Ignore Raycast, so the movement is
  exactly as before. The builders wire her with `MovementBodySetup.Wire` (package editor code; the paths come from the
  template).

  **Her own head looking down (fixed 2026-10-12).** The neck used to follow the whole pitch, which swung her face
  forward and down around the neck, out of a 0.24 m cut-away centred on the camera: looking 60° or more down (50° in a slide,
  where the neck also undid the 30° recline), 300 to 2,000 of her head vertices were in view. The neck now follows a
  quarter of the pitch and the cut-away goes where the head is; `NadiaHeadCutoutEditorTests` poses her real mesh at
  every pitch, standing, sliding and mantling, and checks every head and hair vertex is inside the cut-away and her
  chest outside it looking 70° down.

**What she does (the creator judges these).** Evidence for each move is in RESEARCH-MOVEMENT 8.7. **TF2** means
Titanfall 2's game files or its runners; **ours** means no source, our own choice, kept subtle.

| Move | What you see | Source |
|---|---|---|
| **Wallrun: the lean-in** | A probe looks 0.25 s ahead along her path (her velocity, plus gravity in the air) and, at each point, casts for walls the way the sim catches one (sideways and diagonally, out to the catch reach). When a runnable wall will come within catch reach, the wall-side hand **reaches out before contact**, eased by closeness. It **plants on the contact frame** at the wall point beside her plus 0.35 m along the run (less when her arm can't reach that far: the hand must really touch the wall). A catch can come from up to about 1 m off the wall: then the arm stretches towards the wall, and the hand plants as the wall stick brings her close enough. It drags along the wall. If no catch happens, the reach comes back smoothly. The other arm blends to a one-handed pose over 0.2 s, and back over 0.35 s after the wall. | TF2: the lean-in is "super telegraphed", and runners time the 83 ms wall kick by the hand; one-handed 0.2 / 0.35 s. Momentum Mod: 0.25 s look-ahead |
| **Wall kick** | The wall hand shoves off the wall on the kick frame. | ours |
| **Running** | The arms pump in step with the footsteps, about 4 cm forward and back and 1.5 cm up; each arm is at its furthest at each footfall. The legs run on the **same stride phase as the footstep sounds**. The camera stays nearly still. Zero pump in the air. | TF2: the sprint cycle is two steps. Bob and roll cause sickness (Destiny, Overwatch, Dying Light, Apex). The amplitudes are ours |
| **Look sway** | The arms lag turns (up to 3° yaw and 3° pitch), roll with them (up to 4°) and slide up to 1.3 cm. Rotation follows at 9 /s, translation at 6 /s. Strafing rolls the arms 4°. | TF2 R-201 and Apex fists; bare hands sit between the two |
| **Jump** | **No lift.** The arms sink while rising and float while falling, up to 1.5 cm. | TF2 R-201 vertical sway |
| **Double jump** | A "push off the air" pose, down and back, **held 0.27 s** then released over 0.12 s; the legs tuck. | Destiny: hold key poses 7-10 frames. Apex has its own double-jump overlay. The pose's shape is ours |
| **Landing** | The camera dips first; the body takes the same dip **0.03 s later**, plus a view-punch spring scaled by the fall (TF2's: constant 65, damping 9). | Destiny: "the head leads by a frame"; TF2 `springs.txt` |
| **Slide** | The arms **tilt 18° sideways** (no spread) and drop a little. The tilt shows **already in the air while crouch is held**: "a slide is coming". She reclines 30°. The camera dips sharply as the slide starts, and the view widens ×1.1 over 0.25 s in and 0.25 s out (`MovementCamera.slideFovScale`, `slideEntryDip`). The leading foot rises into the bottom of the screen; `slideLegs` turns the legs off. | TF2: "gun tilted = you're going to slide", the sudden dip, `slideFOVScale 1.1`. No source shows TF2's legs; Mirror's Edge shows them |
| **Mantle** | The pose blends by ledge height (a low and a high pose). The hands are moved onto the ledge's lip by IK, at most 40 cm off the pose, over a reach time set by the approach speed (0.3 m ÷ speed, between 0.06 and 0.22 s). Mouse look is never locked. | TF2: 4 ledge heights; Dying Light: IK on the edge with about 40 cm of freedom, the reach scaled to the speed |
| **Sprint kick** | Plant and shove: the trailing arm drives back and down, the leading one forward, the torso leans in 6° and recovers. | ours |
| **Speed tiers** (under 8, 8-13, over 13 m/s) | **Ours, no source, subtle.** At "fast" she leans 1.5° and the pump is 85%. At "flying" she leans 3°, the pump is 70% and the arms draw back 4 cm. | none (TF2 only lowers the weapon in sprint) |
| **Turning** | She leans into turns, up to 3° from the sideways acceleration. | ours |
| **Still** | A slow breath (the arms and the chest). | ours |

**Arm field of view.** Destiny, Overwatch and Titanfall draw their arms on a separate camera with a fixed FOV. Ours is
a whole body on the main camera, so the hands keep their real size and the Comfort FOV (55-90°) changes how much of
her you see. If the shoulders or hands look stretched at wide FOV, the fix is Dying Light's: scale the hand targets
towards the camera by the FOV ratio (the placeholder capsules already do this).

**Gestures and gameplay hooks.** `MovementBody.Gesture(hand, worldPoint, 0.2f)` lifts a hand towards a point: a
quick raise over the first 30%, then a blend back (`BodyFeelDef.gestureReach`, `gestureRise`, `gestureSpring`). The
dream tool's pulse uses it for her right hand. A contact cue on that hand wins: the wall hand and the ledge hands stay
put. For anything else, `MovementBody.PoseReady` hands a component the pose each frame before it's drawn.

**Comfort.** Everything above scales with the **Motion effects** setting, except the contact cues, which stay at 0:
the wall hand's reach and plant, the ledge hands, the slide tilt and legs. The camera is never moved by the body.

**The orbit camera: F4** (editor and development builds) shows her from outside, orbiting the player. Q / E turn
it and the mouse wheel moves it in or out. Her head shows in this view.

**For the sound side:** the legs use `MovementController.StridePhase`, and each foot plants as it crosses a whole
number, which is the frame `MovementController.Stepped` is true and the footstep plays. Foot 0 (left) plants on
even numbers, foot 1 (right) on odd ones; `MovementBody.Pose.footPlanted` says which foot is down. No change to the
audio is needed.

**Not done:** the lanyard swing spring exists (`Pose.lanyardPitch/Roll`, the rig's `lanyardBone`), but Nadia's lanyard
and badge are part of her one skinned mesh with no bone of their own, so nothing swings yet. Her hair has no bones
either, and it's cut away in first person.

## Sound

`MovementAudio` plays every movement sound in 2D (no doppler) at the shell's effects volume, silent in batch mode.
One-shots go through 10 voices of its own; each play picks a **round-robin variant** (random, never the one just
played), a random pitch of ±5 % and up to 12 % less volume, so nothing repeats exactly. Every clip peaks at -3 dBFS
or lower (loops -6 dBFS) and the loudest moments of a run mix to about -9 dBFS: nothing clips.

Where each clip comes from, best first: a clip under the same id in the game's **Audio Registry** (replaces the
set); else the **recorded variants** `JamKitMovementAudio/<id>_0, _1, ...` (Resources, in the package, so every build
has them); else the **generated** variants (`MoveSounds`). Delete a file and the generated sound plays.

| Sound (id) | When | What it is now | Source |
|---|---|---|---|
| Footsteps (`mv_step`, 5 variants) | each stride (about 3.3 steps/s at walk, 4.9 at sprint; 1.5x on a wall) | concrete footsteps; louder and 8 % lower from walk to sprint; on a wall 10 % brighter and harder | Kenney Impact Sounds (CC0) |
| Sprint weight (`mv_step_heavy`, 5) | under each step, growing from none at walk to about half at sprint | a low body thump (low-passed soft impact) | Kenney Impact Sounds |
| Sprint kick (`mv_kick`, 4) | the push-off from a stumble | a hard, low footstep and body thud, a cloth flick, under a quiet boost whoosh | Kenney Impact + RPG Audio |
| Jump (`mv_jump`, 4) | ground and slide jumps | a shoe scuff under a cloth swish | Kenney RPG Audio |
| Double jump (`mv_djump`, 4) | the air jump | a jump-kit **puff**: a short burst of thruster hiss over a low whump, clearly unlike the jump | Kenney Sci-Fi Sounds + a whump made in code |
| Wall kick (`mv_walljump`, 4) | every wall jump; a wall kick (within 83 ms of contact) plays louder and 6 % higher | a sharp **snap** (a hard shoe on the wall) and a little whoosh | Kenney Impact + RPG Audio |
| Wallrun catch (`mv_catch`, 4) | a wall takes you | the hand planting on the wall, then the swish of **leaning in** | Kenney Impact + RPG Audio |
| Wallrun rub (`mv_wallrun`, loop) | while wallrunning | a quiet shoe rub with grit, louder toward wallrun speed | made in code |
| Slide drop (`mv_slidein`, 4) | a slide starts (also landing into a slide) | a body thud, a gritty scuff, cloth; louder at speed | Kenney Impact + RPG Audio |
| Slide scrape (`mv_slide`, loop) | while sliding | a gravel scrape (noise, grit and a low rumble); **fades with speed**, silent by 4 m/s, pitch 0.85 to 1.2 | made in code |
| Slide boost (`mv_boost`, 4) | the +3 m/s boost | a rising whoosh | made in code |
| Mantle (`mv_mantle`, 4) | a mantle starts | hands gripping, the pull up (cloth), a foot landing on top. No grunt (no CC0 voice yet) | Kenney RPG Audio |
| Landings (`mv_land_soft` 4, `mv_land` 5, `mv_land_hard` 5) | landing (not into a slide) | by fall speed: under 3.5 m/s a footstep; under 8.5 (a plain jump) **soft**, two quick feet; under 12.5 (jump + double jump) **normal**, a thud; above, **hard**, a heavy thud with a body crumple. Volume 0.5 to 1 with the fall | Kenney Impact + RPG Audio |
| Wind (`mv_wind`, loop) | always, by speed | a low wind body with slow gusts; comes in from 5 m/s, full at 16 | made in code |
| Air rush (`mv_wind_high`, loop) | always, by speed | a bright rush with a faint whistle on top of the wind; comes in from 11 m/s, full at 21: **the "going fast" layer** | made in code |

Both wind layers rise in pitch with speed (0.9 to 1.15), are 12 % louder in the air, lean 30 % to the side away
from a wallrun wall, rise quickly and fall slowly (2/s up, 0.9/s down) so a stumble doesn't drop them. The two
layers crossfading stand in for a low-pass opening with speed (which WebGL can't do). The rules are
`MoveSoundMix` in the core (dotnet-tested); the recorded sets are rebuilt by
`uv run --with numpy python tools/audio/make-movement-sfx.py <packs dir>`.

**Still placeholders:** the speed music's stems (composed with the creator later; see **Speed music**); the loops, the boost, the supersonic whistle and the breath are synthesised, not recorded; there is one surface (concrete)
for footsteps; no mantle grunt; the dream-static, collapse, checkpoint and
caught sounds (`mv_static`, `mv_collapse`, `mv_checkpoint`, `mv_caught`) are untouched generated placeholders.

## Speed music

The creator (2026-10-10): "the background music getting more excited the faster you're going". Built from
docs/SPEED-FEEL.md's A1, A2, A4, A5 and A6 (A3, the crossing stinger, left out by choice). Everything follows the
shared speed tiers (`SpeedTier`: running, **fast** from 8 m/s, **flying** from 13 m/s, with hysteresis and a 0.6 s
drop delay, so a stumble doesn't drop a tier). **One tier, one tick:** `MovementAudio` reads the very tier the
speed visuals tick (`SpeedFeel` on the camera, thresholds from the tuning asset's Speed Tiers), after they tick it
each frame, so the boom and the music agree on when flying starts. In a scene with no `SpeedFeel` (other
templates, bare tests) it ticks a tier of its own from the same tuning asset's thresholds.

| Layer | What it does |
|---|---|
| **A1 Stems by tier** (`MovementMusic`) | Up to four stems of one length and tempo play in sync: **base** always, **drive** from fast, **peak** while flying, and an optional **tension** stem the game drives itself. Stems switch only on a **bar line** (the next one after the tier changes) and fade in over 1 s, out over 2 s. |
| **A2 The music opens** | Every stem plays beside a muffled (low-passed, 700 Hz) copy of itself, in step; speed crossfades from muffled at a standstill to full and bright at flying speed (held full while the flying tier lasts). Pre-rendered, so it works on WebGL; the low-pass runs forwards and backwards, so the two copies line up and the crossfade doesn't comb. |
| **A4 Supersonic** (`mv_supersonic`) | While flying: a high shimmering whistle over the air rush (sines a fifth and an octave apart, each with a twin 1.5 Hz off, a slow vibrato, a breath of noise), following the flying blend, a touch higher the faster you go, leaning away from a wallrun wall. Made in code. |
| **A5 Tunnel focus** | While flying, world and ambient sounds duck (by `worldDuck`, 0.45: down to 55 %), smoothly with the flying blend: the shell's ambience and sound effects (`AudioService.WorldGain`) and the scene's own sources (the static's hiss and the like; found every 2 s). Not the music, not the wind or the movement's own sounds. |
| **A6 Nadia's breath** (`mv_breath_calm`, `mv_breath_fast`) | Muted on purpose: a texture, not a feature (`breathVolume` 0.05, 0 = off). A calm breath while moving; panting takes over with **exertion**, which builds with time spent fast (full after 8 s of flying, 16 s of fast) and recovers over 10 s. No breath-in at the crossing (it would be a stinger). Made in code. |

**Every level is in one place:** the `MovementAudio` inspector on the player (the speed block above: music,
supersonic, duck, breath; plus the movement sounds). `MovementMusic` holds the stems' own balance (`stemLevels`
1, 0.85, 0.7, 0.7) and the fades (`mix`: fadeIn 1, fadeOut 2, quantizeBeats 4). The music follows the Music
volume setting, everything else the Effects volume, and all of it the Master volume. The stems peak at -6 dBFS or
lower and sit under the movement sounds; nothing clips.

### The real music: where it goes

The placeholder stems are made in code (`MoveMusicSynth`: D minor, i-VI-III-VII, 104 bpm, four bars; the escape
uses its own pad, pulse and music box from `DreamSynth` with the placeholder peak). The real music is composed with
the creator; to drop it in:

1. Export the stems from one session, **same length and start**, looping on bar lines.
2. Put them in `Assets/JamKit/Audio/MusicStems/Resources/JamKitMusicStems/<set>/` as `base`, `drive`, `peak`
   (and `tension`, the escape's music box part), .wav or .ogg. The sets: **`lab`** (the Movement Lab) and
   **`dream`** (the dream escape). The folder's README says the same.
3. Import as **Decompress On Load**, no Normalize. Set the tempo on the scene's `MovementMusic` (`bpm`,
   `beatsPerBar`) if it isn't 104 bpm 4/4.
4. Optional: your own muffled copies beside them (`base_muffled` ...). Otherwise the game makes them at load (not
   on WebGL: there the music only muffles with supplied copies).

A missing stem falls back to its placeholder, so stems can arrive one at a time.

### In the dream escape

`DreamChaseMusic` now rides on `MovementMusic`. **Speed** picks the stems (the pad, then the pulse at fast, the
arpeggio at flying) and opens the music; **the gap** to the static adds tension on top: the pulse is pushed in as
the static closes even when you're slow (a floor of the threat), the music box (the tension stem, never speed's)
grows from half to full and slides out of tune, and the music opens partway (60 % of the threat). Once the static
falls back the music box is put back in step.

## Comfort settings

Esc > **Comfort and camera** (saved with the other settings, in `Settings`):

| Setting | Default | Range | Affects |
|---|---|---|---|
| Field of view | 68° vertical (≈100° horizontal at 16:9) | 55-90 | the camera |
| Camera roll | 100 % | 0-100 % | wallrun lean and slide roll |
| Head bob and landing dip | 100 % | 0-100 % | bob, landing dip, mantle nod |
| Speed effects | 100 % | 0-100 % | the FOV widening with speed and the speed lines |
| Speed lines | on | on/off | the streaks |

A centre dot always stays on screen (a fixed point for the eye). No motion blur, no vignette, no
chromatic aberration (not built; see Known gaps). Screen shake uses the shell's Screen shake slider and
only fires on collapses.

## Using the movement in a scene (Visiting Hours dream sequences)

1. A body GameObject with **CharacterController** + **MovementController** (assign the shared MovementTuning,
   `MovementTuning.SharedAssetPath`, or leave it empty to use it; set `profile` to Daytime for a waking-world section).
   Children: **Head** (assign to `head`) > **Rig** (add **MovementCamera**) > **Camera** (add
   **MovementBody** and **SpeedLines**; assign `cam`). Add **MovementAudio** to the body. Point each
   component's `body` at the controller. `MovementLabBuilder.BuildScene` does exactly this.
2. In your game script's `Start`: `MovementComfortScreen.Ensure()`; in `OnDestroy`: `MovementComfortScreen.Remove()`.
3. Place, respawn: `controller.Teleport(feet, yaw, velocity)`. Hand control to a script: `controller.Autopilot = sim => input`
   (or `InputEnabled = false`). Read `Speed`, `Mode`, `FrameEvents`, `Sim` for gameplay and UI.
4. Walls are runnable by default; put **MovementSurface** (runnable off) on walls that shouldn't be, or tick
   `onlyMarkedWalls` and mark the runnable ones (a consistent look for them helps players, see below).

## Sizing a level (what the numbers give you)

On flat ground, no air strafing (RESEARCH-MOVEMENT 4.3 with these values):

| Move | Distance / height | Use it for |
|---|---|---|
| Single jump | 1.25 m high; 0.65 s airtime; **about 6 m** at 9.5 m/s, 8 m out of a slide | gaps up to ~5.5 m (keep margin) |
| Jump + double jump at the apex | 2.25 m high; ~1.05 s; **about 10 m** at 9.5, 13 m at 12.5 | gaps of 8-9 m |
| Wallrun | 1.75 s; **about 15 m** at the 8.64 goal, ~17 m entering at 12 | walls 8-15 m to run in one go; longer walls (the lab's 21 m) need a kick or a drop off |
| Wall jump | +3.1 m/s at wallrun speed; 5.2 m/s sideways | parallel walls 4-6 m apart chain (wall to wall), 3.5 m apart climb (chimney: ~1.2 m up per kick) |
| Mantle | 0.4-1.9 m above the feet | ledges; also rescues jumps that come up a little short |
| Slide | under bars 1.15 m high; +3 m/s once per 2 s | slide gaps, ramps (a 16 m ramp dropping 3 m adds about 2-3 m/s) |
| Pursuer | keeps ~12 m behind; catches only after 0.75 s under 2 m | it never surprises a player who keeps moving |

Rules of thumb (RESEARCH-MOVEMENT 6): the next surface is visible as you land; one colour means "you can
use this" (the lab: teal runnable walls, amber ledges, blue slide ramps, pink slide-under bars); give 6-10 m
of run-up before a lateral move (above sprint you turn at most ~220°/s and air steering barely moves you);
checkpoints every 10-20 s of chase; collapses fire by position, behind the player.

## The Gauntlet (the Movement Lab's obstacle course)

The lab opens on the start pad of **the Gauntlet**, a timed obstacle course after Titanfall 2's Pilot's
Gauntlet. Cross the gate and the clock starts; cross the finish line and it stops.

**How to play it**
1. Run through the gate. Hold Shift (LB) to sprint, or click L3.
2. Pass over every glowing gold plate (the touch markers). Each one you miss adds **2 s** at the finish.
3. Each section's end shows a split: green if you're ahead of your best, red if you're behind.
4. Fall, and you come back at the start of the section, already running. The clock keeps going.
5. **R** or the pad's **View** button restarts at once from the start pad.
6. The translucent figure is **a ghost to race**. **G** or the pad's **d-pad left** cycles it: **your best**
   (cool blue) → **the developer's** (gold: the creator's real best run, shipped with the lab) → **off**, skipping
   one that doesn't exist yet. The HUD names the one you're racing ("Ghost: the developer's"). With no best of
   your own, you race the developer's. (The d-pad doesn't move you: gameplay Move is the left stick only,
   docs/SHELL.md.)

Both are the shell's **Restart** and **Ghost** actions, so they can be rebound in Settings > Controls
("Restart run", "Show ghost": rows that only show while the Gauntlet is loaded), and the hints and the
end screen name whatever keys they're on.
7. The board by the start pad lists the target times:
   - **the developer's time, 31.42 s (1 plate missed)**: the creator's real best, 29.42 on the clock + 2 s for
     the plate, penalty shown honestly;
   - **Mags's time, 47.5 s**: 1.5× the developer's, rounded to the nearest 2.5 s (`DeveloperRecords.MagsTimeFor`);
   - **your best**.
8. At the finish, the end screen shows your time and every split.

Your best time, splits and ghost are kept in the save (`GauntletClock.SaveKey`). Autopilot and attract runs
never count. Finish however you like: nothing gates a line or a technique (air strafing included).

**The developer's records.** `Data/DeveloperRecords.json` (a TextAsset, core type `DeveloperRecords`, read by
`DeveloperRecordsAsset`) holds the creator's best Gauntlet run (time, splits, missed plates, the ghost at 20 Hz)
and best dream tool run, copied from their save (`tools/movementlab/creator-records-2026-10-10.json`). The
builder wires it into `GauntletRun` and `DreamToolRun`. To ship a faster run, copy the new
`movementlab_gauntlet` / `movementlab_dreamtool` records from a save into it; the board, Mags's time and the
developer's ghost follow. The autopilot's time (`AutopilotTime`) is a test driver's pace, only in tests and logs.

**The route.** About 470 m along +Z. Every section has a safe line beside the fast line the autopilot runs
(RESEARCH-MOVEMENT 8.4 rule 4).

*The teaching half:*

| # | Section | Fast line | Safe line |
|---|---|---|---|
| 1 | Sprint and kick | hop the 1.0 m vault without breaking stride | mantle it and sprint: the **sprint kick** puts you straight back to speed |
| 2 | Slide | slide under the 1.15 m bar and down a 16 m, 3 m ramp | crouch-walk under, run down |
| 3 | Jumps | a 5.5 m single gap, then an 8.5 m double | two short hops by a stepping stone |
| 4 | Wallrun | the 14 m striped wall on the right over a pit, kick off it | three stepping stones on the left |
| 5 | Wall to wall | left wall → kick → right wall → kick and double jump (walls 5 m apart) | four stones on the far left |
| 6 | The climb | the **chimney**: kick up between walls 3.5 m apart, 4 m | the **mantle stair**: 1.0, 1.35 and 1.65 m |

*The combo half:*

| # | Section | Fast line | Safe line |
|---|---|---|---|
| 7 | Combo: walls, slide, mantle | right wall → kick → left wall → kick, land sliding down a ramp, mantle a 1.35 m ledge (right wall first: the chimney throws you out fast and to the right; the top deck before it is 34 m to line up) | stones on the far lefht, then the same slide and mantle |
| 8 | Combo: slide-hops, or the skip | slide-hop down three steps (4 m gaps) without leaving the slide, mantle twice back up | run the steps |
| 9 | Finale | the 21 m wall on the left, kick and double jump down onto the finish | stones on the right |

**The skip (section 8).** Experts can take a 12.5 m gap on the right onto a raised deck. A double jump at sprint
speed only reaches 9.9 m; only momentum clears 12.5 m: a slide boost (the 13 m/s cap reaches 13.5 m), slide-hops
or air strafing.

**Touch markers.** There are nine, one per section, all where the two lines meet.

**Times** (the autopilot in the dotnet sim, 60 fps, pinned by a test):
- **41.3 s** clean under the Mild hop-cost preset (2026-10-11; Off 40.9, Current 41.75; 43.0 before the wall grace, 2026-10-10). The splits before the hop costs were 3.2, 7.0, 11.3, 16.1, 20.2, 24.1, 29.5, 36.3 and 41.0 s.
- The autopilot doesn't take the skip or the fastest kicks, so a strong player should beat it.
- A first run takes 60–90 s.

**Readability.** The escape's single "go" colour, gold, appears only on things you use: a stripe along every
runnable wall (sized from the floor before it), a lip on every ledge's front edge, and the plates. The start
gate and the finish arch frame the course, and nothing decorative stands in the lanes.

**Building on it.** `GauntletCourse.Make()` (core) appends the Gauntlet after the old course at z 600, in the
same format: pieces, lane, autopilot cues, checkpoints (section starts), sections, markers, and beats. The
beats are checked by `CourseReach`, the reach and clearance rules shared with the dream escape.
- When you change the course, re-run `GauntletTests`: the frame-rate sweep, the section-to-section runs,
  reach, and the safe lines.
- Then set `GauntletCourse.AutopilotTime` to the time the first test prints (a test pin, not a board time).

### Gauntlet notes (the research, brief)

Confirmed by sources:
- **Penalties and goal times.** The Gauntlet adds **2 s for each target left alive** (15 of them). The goal times
  are **56.65 s** and **33.65 s**, and 33.65 s puts you in the leaderboard's top 3 (an achievement)
  ([TrueAchievements](https://www.trueachievements.com/game/Titanfall-2/walkthrough/11),
  [Gameranx](https://gameranx.com/features/id/80008/article/titanfall-2-complete-achievements-trophies-guide-100-walkthrough/)).
- **The ghost.** Beating "Pilot Anderson's Gauntlet ghost recorder time" is an achievement of its own, so the
  game has a ghost to race ([StrategyWiki](https://strategywiki.org/wiki/Titanfall_2/The_Pilot%27s_Gauntlet)).
- **Advice.** Guides tell you to stay above 35 km/h by staying in the air and wallrunning
  ([TrueAchievements](https://www.trueachievements.com/game/Titanfall-2/walkthrough/11)).
- **Speedrun records.**
  - 14 s (Rbrite, 2018: [Guinness](https://www.guinnessworldrecords.com/world-records/514353-fastest-completion-of-titanfall-2-gauntlet)).
  - 12.0 s with a grenade boost (2019).
  - 11.1 s (Cash Mayo, May 2021: [PC Gamer](https://www.pcgamer.com/au/titanfall-2-speedrunner-reclaims-the-gauntlet-record-by-a-hair)).
  - The speed-tech rivalry inspired Apex Legends' Octane ([PC Gamer](https://www.pcgamer.com/au/apex-legends-new-hero-octane-is-a-tribute-to-a-real-life-titanfall-speedrunner)).
- **What we took from it:** the time penalty per missed target (2 s); targets that are required points of the
  route (our touch markers); a board of goal times; a ghost to race; and the instant restart.

From memory, unverified (the StrategyWiki and TrueAchievements pages refused automated fetching, and I
transcribed no videos):
- The course is a loop that brings you back to the start line, with a leaderboard screen beside it.
- The timer runs from the line to the line.
- It opens with wallruns over pits, then rooms of holographic targets, a slide and mantles.

## Dream tool prototype

**The question it tests (the creator, 2026-10-10):** does a one-button action on top of the Titanfall-style movement
feel good, or is it too many actions at once? "Shooting tiles out in front of you to make a way through, or solving
a puzzle while running."

**The fiction (placeholder):** a gun doesn't fit Nadia, so it's a **dream tool**. She points, and a pulse of light
pops glass, unfolds the dream or flips it: the dream bends to her will. The look is a placeholder: pastel glass, gold
sockets, pink and blue platforms.

**How to play it**
1. Open presenter mode (**Ctrl+Shift+P**, or hold L3+R3 on a pad) and pick the chapter **Dream tool**. You can also
   walk on past the Gauntlet's finish: the walkway leads to the chapter's start pad.
2. Cross the gate and the chapter's own clock starts. It has three splits and a best time, and **R** or **View**
   restarts the chapter (the same rebindable Restart action as the Gauntlet).
3. **Fire** (left mouse or RT; rebind it under Controls) sends the pulse. Point roughly at a target: the aim locks
   onto the target nearest the crosshair within 6° on a mouse or 8° on a pad, measured from the target's edge, and a
   gold bracket shows the lock. There's a 0.25 s cooldown and a 40 m range.
4. What the pulse does:
   - It **pops a glass tile**.
   - It **builds from a gold socket**: a bridge or a runnable wall.
   - It **flips a switch**: pink or blue, only one colour is solid at a time. The diamond shows the colour a hit
     will make solid, and the HUD says which colour is solid now.
5. Falls put you back at the last checkpoint. Everything ahead of it is restored, in the checkpoint's colour.

**The route.** Three sections after the Gauntlet, from z 1144 (`DreamToolCourse.Origin`). Every section has a safe
line (stop, then shoot) and a fast line (shoot on the move).

| # | Section | Beats | Safe line | Fast line |
|---|---|---|---|---|
| 1 | **Clear the way** | Three doorways filled with pastel glass tiles: one seen from the start pad on a grounded sprint; one after a 5.5 m gap, shot at the top of the jump; one shot during a long wallrun glide | Stop at the glass and shoot it point-blank | Shoot each from about 20 m without breaking stride |
| 2 | **Build the way** | A socket unfolds a bridge over an 18 m chasm. A socket raises a runnable wall over an 18 m pit. Neither can be crossed any other way. Then the expert moment (below) | Stop at the edge, shoot, cross. After the long wall, take the three-step mantle stair | Shoot each socket from the run-up |
| 3 | **Running puzzle** | Pink is solid at first. Flip on the deck: the pink barrier goes and the blue steps appear. Flip again **mid-glide** on a grey wall: the pink landing appears and the blue steps behind you go. Flip again **at the top of the drop**: the blue landing appears and the pink you just left goes | Flip while standing on grey (the deck, the stones beside the wall, the stone beside the drop). Grey is always solid | Flip on the move |

**The pacing.** A shot replaces a movement input instead of stacking on one. Every required shot comes when the
movement asks for nothing else: a grounded sprint, a glide along a wall, the top of a jump or a drop. No required
shot falls during a wall kick or while air strafing.
- **The one expert moment does stack, and it's optional.** During the long wallrun in section 2, a socket far ahead
  and up to the left (about 12° off your line) raises a second wall. Kick across to it, run it, then kick and double
  jump over the mantle stair. That's a shot, a wallrun and a wall kick inside about one second.
- It saves about **0.7 s** and two of the three mantles, and you reach the puzzle at about 10 m/s instead of 8.

The seconds between required shots (the autopilot's clean run in the dotnet sim at 60 fps, clock time):

| Shot | When | Since the previous | Moment |
|---|---|---|---|
| Glass 1 | 1.5 s | 1.5 s | grounded sprint, 22 m out |
| Glass 2 | 5.7 s | 4.2 s | top of the jump, 18 m out |
| Glass 3 | 10.9 s | 5.2 s | mid-glide on the wall, 25 m out |
| Bridge socket | 14.5 s | 3.6 s | the run-up, 28 m out |
| Wall socket | 18.5 s | 4.0 s | the run-up, 20 m out |
| (expert socket) | 25.1 s | (6.6 s) | on the long wall, 29 m out: optional |
| Switch 1 | 29.6 s | 11.1 s | grounded sprint on the deck, 13 m out |
| Switch 2 | 37.0 s | 7.4 s | mid-glide on the grey wall, 18 m out |
| Switch 3 | 40.4 s | 3.4 s | the top of the drop, 17 m out |

Eight required shots in 44 s. That's one every **3.4 to 11 s, 5 s on average**, never closer than 3.4 s (a test
keeps them at least 2.5 s apart).

**Times:**
- **The developer's time, 28.17 s:** the creator's real best (about 30 s without air strafing), from
  `Data/DeveloperRecords.json`.
- **Mags's time:** 42.5 s (1.5×, to the nearest 2.5 s).
- The autopilot's clean run on the expert line, 44.0 s (`DreamToolCourse.AutopilotTime`, the main line 44.7 s),
  is a test pin only.
- Your best, splits and ghost are saved under its own key (`DreamToolRun.SaveKey`). Autopilot and attract runs
  never count.
- **The ghost** works as in the Gauntlet: G / d-pad left cycles your best → the developer's → off, and the HUD
  names it. Every run records one (it used to save no samples); a new best keeps it. The creator's record has no
  dream tool ghost yet, so "the developer's" is skipped until one is shipped in the records.

**What to judge (the creator):**
1. **Clear the way.** Does popping glass on the sprint feel like a reward, or like a chore you have to remember?
   Does shooting at the top of the jump feel natural?
2. **Build the way.** Is planning while moving (seeing a gap and a socket, building before you get there) the fun
   kind of thinking at this speed?
3. **The puzzle.** Is the flip mid-glide exciting, or one thing too many? Is the flip at the top of the drop (a
   window under a second) fair?
4. **The expert moment.** Is a shot during a wallrun, right before a kick, too much? This answers the worry
   directly.
5. **The aim assist.** Is 6° (mouse) and 8° (pad) generous enough that you never feel you're aiming? If it's too
   sticky or too loose, tune `DreamTool.def` on the lab's Movement Lab object.
6. **Spacing.** Is about 5 s between shots right? Shorter would add more actions; longer and the tool fades into
   the background.

**Where things are.**
- The core, plain C# and dotnet-tested:
  - `Runtime/Core/Movement/DreamToolCourse.cs`: the chapter's data.
  - `DreamTool.cs`: the state, the aim assist and the autopilot's trigger.
  - `DreamToolSounds.cs`: the generated placeholder sounds (`dt_fire`, `dt_lock`, `dt_pop`, `dt_build`, `dt_flip`,
    `dt_dry`). Any of them can be replaced through the Audio Registry.
- The lab, `Assets/JamKit/Templates/MovementLab/`:
  - `Scripts/DreamTool.cs`: input, aim, beam, pulse, sound.
  - `Scripts/DreamToolRun.cs`: the state, the clock, the HUD.
  - `Scripts/DreamToolPieceView.cs` and `Scripts/DreamToolTargetView.cs`.
  - `Editor/DreamToolBuilder.cs`.
- The run reuses the Gauntlet's clock. `GauntletCourse.Chapter` makes an empty timed run, and a `GauntletClock`
  runs it. `GauntletCourse.End` (z 1120) is where the Gauntlet's HUD and restart stop.
- The autopilot can't aim, so the tool has a test hook: while the course autopilot drives, `DreamTool.autoFire`
  fires at each cue's target when it's in range (`DreamToolPilot`).
- Tests:
  - `core-tests/Movement`, `DreamToolTests`:
    - the autopilot clears the chapter at 20 to 300 fps and with jitter, and the main line works too;
    - every checkpoint runs to the next, and a held fire leaves the first pane blocking the lane;
    - every target can be shot from the route, in range and in sight;
    - the builds close gaps nothing else crosses, and the reach checks pass with them built;
    - the colour flip, the aim assist and the sounds.
  - Unity EditMode, `DreamToolEditorTests`: the wiring.
  - Unity PlayMode, `DreamToolPlayTests`:
    - the board shows the developer's 28.17 and Mags's 42.50; the autopilot clears all three sections, records a
      ghost covering the run, and changes neither your best nor your best ghost;
    - a missed tile blocks the lane until it's shot, and the restart puts it back;
    - a flip makes the other colour solid, by its collider and a raycast.

**Not done:**
- Nadia's arm doesn't rise on a shot; the pulse starts at her right hand's pose position. `MovementBody` has no hook
  between the feel's pose and the rig. A small one, an `OnPose` callback before `rig.Apply`, would let the tool lift
  her hand toward the target for about 0.2 s.
- The fiction (what the tool is, why she has it) is open.

## Free roam: the old course

A grey-box course along +Z, z 0 to 530, before the Gauntlet (about 500 m, ~48 s for the autopilot). Its
chapters below reach it. **Run-up and slide** (40 m, a slide
ramp, a bar to slide under), **Gaps** (5.5 m single, 8.5 m double), **Wallruns** (8 m over solid floor,
14 m over a pit, 21 m over a pit, then a kick and a double jump), **Momentum chain** (slide → slide jump
→ wallrun → wall kick → double jump), **Chimney** (wall-kick between walls 3.5 m apart to climb 4 m),
**Ledges** (1.0, 1.5 and 1.85 m mantles), **Chase** (a curtain of dream-static rubber-bands 12 m behind
you along the route; the ledges fall away, then the walls fold in; a slide ramp, a bar, gaps, a wallrun,
a double gap, a wall-to-wall finale), **Finish** (your time and best, then back to the start).

- Falling below the course ("dream drop") or being caught: a 0.4 s fade and you're back at the last
  checkpoint, running at sprint; collapses after it are put back.
- **Wall catch diagnostic:** when a runnable wall within reach (or up to 0.8 m beyond it) in the air isn't caught, the movement records why (`MoveSim.LastCatchMiss`): just out of reach, too slow (under 5 m/s), too low (under 0.6 m), crouch held, too head-on (more than 70° off the wall; 75° was tried and caught the end faces of walls), no stick, the stick pointing away, the stick not forward, or the same wall higher than you left it. F3 shows the last one ("Wall not caught: ..."); a development build logs `JAMKIT_WALLCATCH` (once a second at most) with the trace.
- Debug HUD (F3 / d-pad down; View now restarts the Gauntlet): speed m/s and km/h, state, wall side, slide boost and double jump, an 8 s
  speed graph with lines at walk 5, wallrun 8.64, sprint 9.5 and the 13 m/s boost cap, section and time,
  and the chase gap bar.
- Presenter chapters: **Gauntlet start, Gauntlet: teaching half, Gauntlet: combo half, Gauntlet: finish**, then the
  free roam's **Course start, Wallrun section, Momentum chain, Chimney, Chase**. Attract mode
  runs the Gauntlet, then the momentum chain, with the autopilot. The debug menu's Next jumps to the next checkpoint.
- Smoke run: the template's driver plays the momentum chain with the autopilot and fails the run if it
  falls or doesn't get well past sprint.
- The course is data (`LabCourse.Build`), so the dotnet tests run the same autopilot through the same
  boxes in a fake world: it finishes with no falls (`LabCourseTests`).

## Tests

| Where | What |
|---|---|
| `core-tests/Movement` (`dotnet test core-tests/Movement/Movement.Tests.csproj`) | Source formulas (air strafe at most cap² per tick, tick-rate dependence), opt-in sprint (walk by default, Shift held or the L3 toggle reach sprint, forward only, the toggle ends on stop, pull-back, crouch and respawn and survives a jump), the sprint kick (sprint within 0.4 s from a near-stop, never past sprint, only below 60 %, 2 s cooldown, readies the slide boost), the air-strafe speed curve pinned (hop costs off), the air and hop rules (`AirAndHopTests`: W alone gains nothing at any mouse rate but still turns, slow jumps aim as before, strafe input exactly as before at 120-540°/s, the mouse-against-pad table, hop costs against a slide's, late hops, slide hops beat plain hops, the hop-and-steer cost), ground (never past sprint by input, overspeed lingers, landing grace), jumps (heights, lockout, coyote 0.11 works and 0.13 doesn't, buffer), slide (boost, cooldown, cap decay, slope, slide jump keeps speed, slide hop, fits under a bar), mantle (heights, rescue), wallrun with Titanfall 2's numbers (8.64 from above and below, 1.75 s, away ends it, neutral brakes, the wall-jump impulses, crouch kick, wall kick, same-wall rule, 25° rule, wall coyote, head-on isn't a wallrun), the momentum chain, identical paths at 30/60/144/240 fps, the driver's latching and interpolation, the course (autopilot finishes, every checkpoint to the next, chain at 30 and 144 fps, research sizes), pursuer and route |
| `core-tests/Movement` (`MoveSoundTests`) | the generated sounds: at least 3 distinct variants per one-shot, peaks under -1 dBFS, no click at either end, seamless loops (the wrap is a typical step, no level jump across it), the double jump brighter than the jump; the mix rules: footsteps heavier with speed, landing tiers, wind rising with speed, the slide fading, round-robin never repeating |
| `core-tests/Movement` (`SpeedMusicTests`) | the speed music: stems picked by tier, switched only on bar lines (or beats when set), 1 s in and 2 s out, floors; the placeholder stems one length (the escape's), seamless and under -1 dBFS; the muffled copy darker, seamless and in phase; the opening by speed; the duck amount; the breath's exertion |
| Unity EditMode (`MovementAudioEditorTests`) | every movement event has clips and a generated fallback, at least 3 variants per one-shot, the recorded CC0 sets are there and mono, the loops are seamless, nothing peaks above -1 dBFS; the lab scene drives the speed music (stem set `lab`); the stems and their muffled copies exist, in step. `DreamEscapeEditorTests`: the escape's chase music rides on the stems and the player drives them; the peak stem is the chase loops' length |
| Unity PlayMode (`MovementMusicPlayTests`) | the autopilot's run raises the stems playing (to drive at least), every switch lands on a bar line, the sound reads the visuals' own tier every frame, and every boom finds the music already at flying |
| Unity EditMode (`MovementLabEditorTests`) | chapters and attract script, sprint opt-in in the tuning and the Shift, LB, L3 and R3 bindings, scene/tuning/preset/materials, Build Settings, every player part, sound ids and the Crouch binding |
| Unity EditMode (`NadiaHeadCutoutEditorTests`) | Nadia's first-person head: her real mesh skinned on the CPU at every pitch from 85° up to 85° down, standing, sliding and mantling, with the camera on her eyes and dipped 0.12 m ahead of the body: every head and hair vertex inside the cut-away the material gets; her chest outside it looking 70° down; the old full nod showed her head; no cut-away in the orbit view |
| `core-tests/Movement` (`WallFeelTests`) | the creator's controller notes: the first 0.5 s on a wall keeps all the speed, then 1.2/s; a kick in the grace keeps the entry speed; a stick at 50° into or away from the wall, at 80°, or the view 50° off keeps the run and its speed; only clear backward input or no stick brakes; a 30 ms flick away doesn't drop you, holding away does; the catch says why it refused (too slow, stick away, no stick, head-on, out of reach, the same wall) and clears on a catch; a catch refused for the stick is retried 0.1 s with more reach; the double jump adds +1 m/s up to sprint, nothing above it, along the view with the stick neutral; the Gauntlet's slide ramp adds over 2 m/s (measured +3.4) |
| `core-tests/Movement` (`GauntletTests`) | the Gauntlet: the autopilot runs it clean (no falls, every marker) at 20, 60, 144 and 300 fps and with jittery frames, in its pinned `AutopilotTime`; Mags's time is 1.5× the developer's, to the nearest 2.5 s; every section reaches the next; reach and the slide-gap clearance (an old 0.35 m crouched step offset is caught); every move is used, mantles at three heights, a marker in every section; the safe lines are hoppable and stay off the fast lane; the old course is kept unchanged before it; the clock starts at the gate, splits, penalises a missed marker 2 s, resets on restart, keeps the best; the ghost records at a fixed rate and replays |
| Unity EditMode (`GauntletEditorTests`) | the run loop, ghost and one plate per marker are wired; the gate and finish stand there; a gold stripe on every runnable wall; the materials compile; the route fits the lab's real tuning and controller; the best record round-trips through the save; the developer's records ship (31.42 with 1 missed, a 589-sample ghost from the gate, the dream tool's 28.17, Mags 47.5 / 42.5) and both runs are wired to them, the dream tool with its own ghost figure |
| Unity PlayMode (`GauntletPlayTests`) | the lab opens on the start pad, ready; the autopilot runs the Gauntlet touching every marker and recording a ghost (not counted as your best) and gets the end screen; the board shows the developer's real 31.42 (1 plate missed) and Mags's 47.50, not the autopilot's time; the ghost cycles your best → the developer's → off, and the developer's figure follows the creator's run once the clock runs; the restart puts the clock, markers and player back; skipping sections adds the missed markers' penalty; the chapters land on their sections |
| Unity PlayMode (`MovementLabPlayTests`) | the scripted chain keeps its speed on the real CharacterController, the autopilot finishes the course, standing still in the chase gets you caught and put back, falling respawns, chapters jump cleanly, comfort settings reach the camera and the pause screen |

## Known gaps

- Air strafing works; only the Gauntlet's optional skip rewards it (it's weak on a stick, by design). Since
  2026-10-11 it needs strafe input above 5 m/s, and hops cost a little; the mouse still out-turns a pad when
  strafing with A/D (the creator chose not to cap the mouse).
- The Gauntlet: no online leaderboard; the developer's time and ghost are the creator's best as of 2026-10-10, copied by hand into `Data/DeveloperRecords.json`; the dream tool has no developer's ghost yet. (Restart and the ghost toggle are rebindable shell actions; without the shell, in a bare test scene, `GauntletRun` reads R / View and G / d-pad left from the devices.)
- No strafe "lurch", no moving platforms, no ziplines or grapple.
- No post-processing comfort effects (vignette at speed, optional motion blur): the lab camera has
  post-processing off. Add one "speed" Volume if wanted (RESEARCH-MOVEMENT 8.5).
- The body is drawn by the main camera, not an overlay camera: a hand can clip into a wall you press your face
  against. Her lanyard and hair don't swing (no bones of their own on the model).
- The body is Nadia's model in the lab and the escape (the capsules are the fallback); the dream-static is a placeholder for the art pass; the sound's remaining placeholders are listed under **Sound**.
