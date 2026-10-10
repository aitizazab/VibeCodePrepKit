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
| The Movement Lab (feel-test course, chase) | `Assets/JamKit/Templates/MovementLab/` (course data in core: `LabCourse`, `CourseAutopilot`, `PathLine`/`Pursuer`) |
| Tests | `core-tests/Movement` (dotnet), `Assets/JamKit/Templates/MovementLab/Tests` (Unity EditMode and PlayMode) |

Run the lab on its own: **JamKit > Use Template > Movement Lab**, then Play (switch back with
**Use Template > Visiting Hours**). Batch: `-executeMethod JamKit.MovementLab.EditorTools.MovementLabTemplate.UseFromCommandLine`.

## Controls

| Action | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | WASD, arrows | left stick (70 % pushed counts as full) |
| Look | mouse | right stick (response curve, faster after holding it fully over) |
| Jump, double jump, wall jump | Space | A / Cross (also RB) |
| Slide / crouch | Left Ctrl or C (hold) | B / Circle (hold), or R3 (toggle: press again or jump to stand) |
| Sprint | automatic when moving forward (Left Shift if auto-sprint is off) | automatic (LB) |
| Wallrun | automatic: jump at a wall *along* it and hold forward | same |
| Mantle | automatic: run or jump at a ledge 0.4 to 1.9 m above your feet | same |
| Lab debug HUD | F3 | View / Select |
| Pause, Comfort and camera | Escape | Start |

The shell's `Dash` action is the jump; the `Crouch` action is new (Ctrl, C, pad B). Older input
assets without it get it added at runtime.

## The states

| State | What it is | How you get in / out |
|---|---|---|
| **Grounded** | Walking. Source acceleration and friction. | Landing, mantle end. |
| **Sprint** | Grounded at sprint speed (automatic when moving forward). | Moving forward faster than walk. |
| **Slide** | Crouched, low friction, a speed boost on a 2 s cooldown, downhill slopes speed you up. | Crouch while running at 6.5 m/s or more, or land with crouch held (slide hop). Ends below 4 m/s or when you let go of crouch. Jumping out keeps **all** your horizontal speed. |
| **Air** | Gravity, Source air strafing (small cap: slow, real gains only by turning), gentle low-speed steering. | Jump, walk off a ledge, leave a wall. |
| **Wallrun** | Along a near-vertical wall: you settle toward 8.64 m/s, weightless at first and sinking later, for at most 1.75 s. | Jump at a wall moving along it (not head-on), holding forward, 0.6 m or more above the ground. Ends by time, if the wall ends, if you push away, crouch, or stop holding forward. Jump = wall jump. |
| **Mantle** | Pulled up onto a ledge in 0.2 + 0.07 s per metre. | Running or falling into a ledge 0.4 to 1.9 m above your feet while holding forward. You leave at 80 % of your speed (at least 5 m/s). |

**Momentum rules** (what keeps your speed): a jump never touches horizontal speed; landing has 0.15 s
with no friction (time to jump again or slide); speed over sprint fades slowly (1.5/s) instead of being
braked, and you can still steer it; catching a wall keeps your full speed (turned along the wall); a
wall jump *adds* speed (about +3.1 m/s at wallrun speed); the double jump turns you toward the stick
without losing speed; collisions only remove the part of your velocity going into the surface.
**The wall kick:** a jump in the first 83 ms on a wall keeps almost everything (staying on the wall
lets wall friction pull a fast run down toward 8.64 m/s), so quick kicks are the fast line.

## Every tunable number

All numbers live in one `MoveDef` (core), held by a **MovementTuning** asset
(`Assets/JamKit/Templates/MovementLab/Data/MovementLabTuning.asset` for the lab; create more with
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
| autoSprint | on | Moving forward sprints. Off: hold Sprint. |
| groundAccel | 10 | Source accelerate: full speed in about 0.1 s. |
| friction / stopSpeed | 6 / 2.5 | Source friction; constant below stopSpeed so you stop. |
| overspeedDecay | 1.5 | Speed above sprint fades at this rate (per second, of the excess) while you hold a direction. |
| groundTurnRate | 220 | Above sprint you still turn toward the stick this fast (deg/s) without losing speed. |
| landingGrace | 0.15 | No friction right after landing. |
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
| coyoteTime | 0.12 | Jump still works this long after walking off a ledge. |
| jumpBuffer | 0.12 | A press this early still fires on landing, on a wall or at the end of a mantle. |
| airAccel / airCap | 12 / 0.8 | Source air strafing (cap 0.8 m/s = 30 Quake units). |
| airControlSpeed / airControlAccel | 5 / 2 | Gentle steering of slow jumps (does nothing at speed). |
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
| wallMinTowardDot / wallForwardDot | -0.1 / 0.3 | The stick may not push away, and must point forward along the wall. |
| wallProbeHeight / wallProbeRadius / wallProbeReach | 1.0 / 0.25 / 0.5 | The forgiving wall search: from chest height, up to 0.5 m beyond your body. |
| **wallRunSpeed** | **8.64** | The goal speed (340 u/s): slower runs accelerate to it, faster ones are pulled down to it by wall friction. |
| wallRunAccel / wallRunFriction | 6 / 2 | Acceleration up to the goal; above it, the excess fades at 2/s. |
| wallNeutralFriction | 4 | No forward input (or into the wall): braking to a stop (backwards: twice). |
| **wallRunDuration** | **1.75** | The longest run. |
| wallGravityStart / wallGravityEnd | 0.1 / 0.8 | Nearly weightless at first, sinking later (upward speed from a jump is kept at full gravity). |
| wallSinkStart / wallSinkEnd | 1 / 6 | Fastest sink, growing with time on the wall. |
| wallStick | 0.5 | A small push keeps contact. |
| wallLostGrace | 0.1 | Seams and short gaps in a wall don't end the run. |
| wallExitSpeed | 4 | The run ends below this along the wall. |
| wallAwayDot | 0.5 | The stick pointing away from the wall ends the run at once. |
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
| `MovementCamera` (on the rig) | speedFov 8° from 9.5 to 22 m/s, slideFov 3°, fovRate 6; wallRoll 9° away from the wall, wallCatchKick 4° (the lean-in on contact), slideRoll 2°, roll spring 14 rad/s damping 0.65; landDip 0.12 m from 4 to 18 m/s impact (spring 20); bobAmount 0.035 m; mantleNod 3° |
| `MovementBody` (on the camera) | PLACEHOLDER arms and slide legs: colours, proportions; sprint pump with the stride, jump lift, double-jump sweep, **wall-side hand reaching out and planting on the wall on contact**, slide balance pose with legs out, mantle plant |
| `SpeedLines` (on the camera) | 56 edge streaks from 12 to 20 m/s, alpha up to 0.35, never in the centre of the screen |
| `MovementAudio` (on the body) | wind loop (volume smoothstep 6..18 m/s, pitch up with speed), slide scrape, wallrun scrape, footsteps on the stride (about 4.9/s at sprint, 1.5x on a wall), catch, jump, double jump (distinct), wall jump, boost, mantle, landing thud by impact (none when landing into a slide). Ids `mv_*` in the Audio Registry replace the placeholders. |
| `MovementController` | mouseDegreesPerPixel 0.09, gamepadDegreesPerSecond 190 (x1.6 boost after 0.25 s fully over), maxPitch 85, managePointerLock, onlyMarkedWalls, worldMask, skinWidth 0.04, stepOffset 0.35 standing, **crouchStepOffset 0** (the CharacterController's step-up pass lifts the capsule by the step offset before it sweeps forward: with 0.35 while sliding, a slide stopped dead at the face of a 1.15 m bar) |

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

1. A body GameObject with **CharacterController** + **MovementController** (assign a MovementTuning).
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

## The Movement Lab

A grey-box course along +Z (about 500 m, ~48 s for the autopilot): **Run-up and slide** (40 m, a slide
ramp, a bar to slide under), **Gaps** (5.5 m single, 8.5 m double), **Wallruns** (8 m over solid floor,
14 m over a pit, 21 m over a pit, then a kick and a double jump), **Momentum chain** (slide → slide jump
→ wallrun → wall kick → double jump), **Chimney** (wall-kick between walls 3.5 m apart to climb 4 m),
**Ledges** (1.0, 1.5 and 1.85 m mantles), **Chase** (a curtain of dream-static rubber-bands 12 m behind
you along the route; the ledges fall away, then the walls fold in; a slide ramp, a bar, gaps, a wallrun,
a double gap, a wall-to-wall finale), **Finish** (your time and best, then back to the start).

- Falling below the course ("dream drop") or being caught: a 0.4 s fade and you're back at the last
  checkpoint, running at sprint; collapses after it are put back.
- Debug HUD (F3 / View): speed m/s and km/h, state, wall side, slide boost and double jump, an 8 s
  speed graph with lines at walk 5, wallrun 8.64, sprint 9.5 and the 13 m/s boost cap, section and time,
  and the chase gap bar.
- Presenter chapters: **Course start, Wallrun section, Momentum chain, Chimney, Chase**. Attract mode
  runs the course with the autopilot. The debug menu's Next jumps to the next checkpoint.
- Smoke run: the template's driver plays the momentum chain with the autopilot and fails the run if it
  falls or doesn't get well past sprint.
- The course is data (`LabCourse.Build`), so the dotnet tests run the same autopilot through the same
  boxes in a fake world: it finishes with no falls (`LabCourseTests`).

## Tests

| Where | What |
|---|---|
| `core-tests/Movement` (`dotnet test core-tests/Movement/Movement.Tests.csproj`) | Source formulas (air strafe at most cap² per tick, tick-rate dependence), ground (never past sprint by input, overspeed lingers, landing grace), jumps (heights, lockout, coyote 0.11 works and 0.13 doesn't, buffer), slide (boost, cooldown, cap decay, slope, slide jump keeps speed, slide hop, fits under a bar), mantle (heights, rescue), wallrun with Titanfall 2's numbers (8.64 from above and below, 1.75 s, away ends it, neutral brakes, the wall-jump impulses, crouch kick, wall kick, same-wall rule, 25° rule, wall coyote, head-on isn't a wallrun), the momentum chain, identical paths at 30/60/144/240 fps, the driver's latching and interpolation, the course (autopilot finishes, every checkpoint to the next, chain at 30 and 144 fps, research sizes), pursuer and route |
| Unity EditMode (`MovementLabEditorTests`) | chapters and attract script, scene/tuning/preset/materials, Build Settings, every player part, sound ids and the Crouch binding |
| Unity PlayMode (`MovementLabPlayTests`) | the scripted chain keeps its speed on the real CharacterController, the autopilot finishes the course, standing still in the chase gets you caught and put back, falling respawns, chapters jump cleanly, comfort settings reach the camera and the pause screen |

## Known gaps

- Air strafing works but no route depends on it (it's weak on a stick, by design).
- No strafe "lurch", no moving platforms, no ziplines or grapple.
- No post-processing comfort effects (vignette at speed, optional motion blur): the lab camera has
  post-processing off. Add one "speed" Volume if wanted (RESEARCH-MOVEMENT 8.5).
- No music stems by speed tier.
- The arms are drawn by the main camera (scaled against the FOV kick), not an overlay camera: they can
  clip into a wall you press your face against.
- The body, the dream-static and every sound are placeholders for the art and audio passes.
