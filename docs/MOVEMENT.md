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
| Jump, double jump, wall jump | Space | A / Cross (also RB) |
| Slide / crouch | Left Ctrl or C (hold) | B / Circle (hold), or R3 (toggle: press again or jump to stand) |
| Sprint (forward only) | Left Shift (hold) | LB (hold), or **L3 click** (toggle, Titanfall-style: you sprint until you stop moving forward, crouch, slide or respawn; clicking again keeps it on) |
| Wallrun | automatic: jump at a wall *along* it and hold forward | same |
| Mantle | automatic: run or jump at a ledge 0.4 to 1.9 m above your feet | same |
| Lab debug HUD | F3 | View / Select |
| Pause, Comfort and camera | Escape | Start |

**You walk unless you sprint** (the creator, 2026-10-10: `autoSprint` is off by default). On a pad the
two stick clicks pair up the Titanfall way: L3 sprints, R3 crouches and slides.

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

## Why air strafing gains speed

The creator found this and wants it kept as it is (2026-10-10). A regression test
(`SprintTests.AirStrafeGainIsUnchanged`) pins the speed curve.

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
  - The gentle low-speed air control (airControlSpeed 5, airControlAccel 2) adds up to another 0.08 m/s along
    the stick each tick.
- **How fast you gain depends on how fast you turn.** Each add also turns your velocity (by about 4.6° per tick
  at 10 m/s). If the view turns more slowly than that, the projection creeps up toward 0.8 and the adds shrink.
  For example, turning 120°/s while bunny hopping takes you from 9.5 to about 11 m/s in 1 s and about 15.4 m/s
  in 4 s. Straight-line strafing without turning gains nothing.
- **The cap.** Horizontal speed is clamped to **maxHorizontalSpeed = 22 m/s** (about 2.3× sprint), in the air
  and on the ground.
- **Keeping it through landings.**
  - For **0.15 s after landing** (landingGrace), while the stick is pushed, there is no ground friction and no
    overspeed fade. If you let go of the stick, friction starts at once.
  - A jump pressed up to **0.12 s before landing** (jumpBuffer) fires on the landing step itself, so a bunny
    hop never touches friction.
  - Jumping never changes horizontal speed.
  - On the ground past the grace, speed above sprint fades by only 1.5 per second of the excess (overspeedDecay)
    while you hold a direction, so a short run between hops costs little.

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
| `MovementCamera` (on the rig) | speedFov 8° from 9.5 to 22 m/s, **slideFovScale 1.1 over 0.25 s in and out** (TF2; slideFov extra degrees 0), fovRate 6; sprintKickFov 4° over sprintKickTime 0.35 s; wallRoll 9° away from the wall, wallCatchKick 4° (the lean-in on contact), slideRoll 2°, roll spring 14 rad/s damping 0.65; landDip 0.12 m from 4 to 18 m/s impact (spring 20), **slideEntryDip 0.05 m** (TF2: the sudden dip into a slide); bobAmount 0.035 m; mantleNod 3° |
| `MovementBody` (on the camera) | The first-person body and its feel (**The body** below): `rig` (a MovementBodyRig; the lab and the escape wire Nadia's model), `feelCamera`; the placeholder capsules' colours and proportions when no rig is wired. Every feel number is in the tuning asset's **Body** section (`BodyFeelDef`). |
| `SpeedLines` (on the camera) | 56 edge streaks from 12 to 20 m/s, alpha up to 0.35, never in the centre of the screen |
| `MovementAudio` (on the body) | windVolume 0.5, windHighVolume 0.4, slideVolume 0.45, wallVolume 0.3, stepVolume 0.32, effectsVolume 0.8, pitchVariance 0.05, volumeVariance 0.12. What each sound is and when it plays: **Sound** below. |
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
  - aims her head where the camera looks;
  - reaches her arms and legs to the targets with two-bone IK (the elbows and knees towards their hints, the
    collarbone helping a far hand by up to 20°).

  In first person the visible copy is cut away within 0.24 m of the camera (the head, the hair and the neck: the
  waking shader's `_CameraCutout`, set per frame). A shadow-only copy shares the bones and casts the whole body's
  shadow, head included. She has no colliders and sits on Ignore Raycast, so the movement is exactly as before. The
  builders wire her with `MovementBodySetup.Wire` (package editor code; the paths come from the template).

**What she does (the creator judges these).** Evidence for each move is in RESEARCH-MOVEMENT 8.7. **TF2** means
Titanfall 2's game files or its runners; **ours** means no source, our own choice, kept subtle.

| Move | What you see | Source |
|---|---|---|
| **Wallrun: the lean-in** | A probe looks 0.25 s ahead along her path (her velocity, plus gravity in the air). When a runnable wall is coming beside her, the wall-side hand **reaches out before contact**, eased by closeness. It **plants on the contact frame** at the wall point beside her plus 0.35 m along the run (less when her arm can't reach that far: the hand must really touch the wall), then drags along the wall. If no catch happens, the reach comes back smoothly. The other arm blends to a one-handed pose over 0.2 s, and back over 0.35 s after the wall. | TF2: the lean-in is "super telegraphed", and runners time the 83 ms wall kick by the hand; one-handed 0.2 / 0.35 s. Momentum Mod: 0.25 s look-ahead |
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

**Still placeholders:** the loops and the boost are synthesised, not recorded; there is one surface (concrete)
for footsteps; no mantle grunt or breathing; no music stems by speed; the dream-static, collapse, checkpoint and
caught sounds (`mv_static`, `mv_collapse`, `mv_checkpoint`, `mv_caught`) are untouched generated placeholders.

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
| `core-tests/Movement` (`dotnet test core-tests/Movement/Movement.Tests.csproj`) | Source formulas (air strafe at most cap² per tick, tick-rate dependence), opt-in sprint (walk by default, Shift held or the L3 toggle reach sprint, forward only, the toggle ends on stop, pull-back, crouch and respawn and survives a jump), the sprint kick (sprint within 0.4 s from a near-stop, never past sprint, only below 60 %, 2 s cooldown, readies the slide boost), the air-strafe speed curve pinned, ground (never past sprint by input, overspeed lingers, landing grace), jumps (heights, lockout, coyote 0.11 works and 0.13 doesn't, buffer), slide (boost, cooldown, cap decay, slope, slide jump keeps speed, slide hop, fits under a bar), mantle (heights, rescue), wallrun with Titanfall 2's numbers (8.64 from above and below, 1.75 s, away ends it, neutral brakes, the wall-jump impulses, crouch kick, wall kick, same-wall rule, 25° rule, wall coyote, head-on isn't a wallrun), the momentum chain, identical paths at 30/60/144/240 fps, the driver's latching and interpolation, the course (autopilot finishes, every checkpoint to the next, chain at 30 and 144 fps, research sizes), pursuer and route |
| `core-tests/Movement` (`MoveSoundTests`) | the generated sounds: at least 3 distinct variants per one-shot, peaks under -1 dBFS, no click at either end, seamless loops (the wrap is a typical step, no level jump across it), the double jump brighter than the jump; the mix rules: footsteps heavier with speed, landing tiers, wind rising with speed, the slide fading, round-robin never repeating |
| Unity EditMode (`MovementAudioEditorTests`) | every movement event has clips and a generated fallback, at least 3 variants per one-shot, the recorded CC0 sets are there and mono, the loops are seamless, nothing peaks above -1 dBFS |
| Unity EditMode (`MovementLabEditorTests`) | chapters and attract script, sprint opt-in in the tuning and the Shift, LB, L3 and R3 bindings, scene/tuning/preset/materials, Build Settings, every player part, sound ids and the Crouch binding |
| Unity PlayMode (`MovementLabPlayTests`) | the scripted chain keeps its speed on the real CharacterController, the autopilot finishes the course, standing still in the chase gets you caught and put back, falling respawns, chapters jump cleanly, comfort settings reach the camera and the pause screen |

## Known gaps

- Air strafing works but no route depends on it (it's weak on a stick, by design).
- No strafe "lurch", no moving platforms, no ziplines or grapple.
- No post-processing comfort effects (vignette at speed, optional motion blur): the lab camera has
  post-processing off. Add one "speed" Volume if wanted (RESEARCH-MOVEMENT 8.5).
- No music stems by speed tier.
- The body is drawn by the main camera, not an overlay camera: a hand can clip into a wall you press your face
  against. Her lanyard and hair don't swing (no bones of their own on the model).
- The body is Nadia's model in the lab and the escape (the capsules are the fallback); the dream-static is a placeholder for the art pass; the sound's remaining placeholders are listed under **Sound**.
