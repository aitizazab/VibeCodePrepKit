# Research: Titanfall-2-style movement for a first-person chase game (Unity 6.3, URP)

Research only: nothing in the project was changed. Written 2026-10-10.

**Labels used throughout:** **[Doc]** = documented by the developer, engine docs or source code.
**[Comm]** = community measurement, wiki or player claim (unverified). **[Op]** = my recommendation or
opinion (starting points to tune, not facts).

**Short version.** Keep Unity's built-in **CharacterController** (the DOOM-like player already uses
it) and put the movement rules in a **pure C# core** next to `PlayerMotion`. Use Source-engine
`Accelerate` / `AirAccelerate` / `Friction` for ground and air, add slide, wallrun, wall jump, double
jump and mantle as states, and run them on a **fixed 120 Hz step** with the camera interpolated. A
minimum viable version is about 6 states and 6 forgiveness timers (see 7.3).

## 0. Starting point: the JamKit DOOM-like controller

Read from the repo (not changed):

| Item | What it does now | What it means for Titanfall-style movement |
|---|---|---|
| `Assets/JamKit/Templates/Doomlike/Scripts/Actors/DoomPlayer.cs` | `[RequireComponent(CharacterController)]`. **Kinematic**: `controller.Move((vx, vy, vz) * dt)` once per frame. Gravity is applied by hand (`_vy -= gravity*dt`, 30 m/s²); when grounded `_vy = -2`. Radius 0.38, height 1.7, eye 1.5, `stepOffset 0.35`, `slopeLimit 50`, `skinWidth 0.04`, `minMoveDistance 0`. | A good base. But it has **no jump, crouch or sprint input**, and gravity is separate from the planar velocity. |
| Same file, after `Move` | If `CollisionFlags.Sides` is set, the horizontal velocity is replaced by the distance actually moved / dt ("no speed builds up against walls"). | This **kills momentum whenever you graze a wall**, and wallrunning needs exactly that contact. Replace it with a velocity *clip* along the hit normal (4.2). |
| `Packages/com.vibejam.jamkit/Runtime/Core/Doomlike/PlayerMotion.cs` | Pure C# (`System.Numerics`), 2D XZ only. Steps toward `wish * maxSpeed` at `acceleration` 70 m/s². Linear `friction` 45 m/s² when there is no input **or when speed > maxSpeed**. `turnGrip` 1.6 when reversing. Run 9.5 m/s, walk 4.5. | Snappy DOOM feel. The "over max → friction" rule is the opposite of momentum (a slide boost or wall jump bleeds off at 45 m/s²). Air control is the same as ground control. Keep it for the DOOM template and write a **new** 3D core for this game. |
| `DoomGame.Update` → `Simulate(Time.deltaTime)` → `player.Tick` | Variable time step, once per rendered frame. | Fine for DOOM. Source-style air acceleration is **tick-rate dependent** (2.3), so the new core needs a fixed step. |
| `core-tests/Doomlike/*.csproj` (`dotnet test`) | Pure rules are tested outside Unity. | The new movement core can be tested the same way (4.10). |
| Project | Unity 6000.3.25f1, URP 17.3, Input System 1.20, Cinemachine 3.1.7. `Fixed Timestep` 0.02 (50 Hz). Browser builds are a target (`docs/TEMPLATE-DOOMLIKE.md`, "click to play"). | 50 Hz is coarse at 20 m/s (0.4 m per step). Run your own 120 Hz loop (4.9). |

<!-- S1 -->

## 1. What Titanfall 2's pilot movement is

**Engine.** "A heavily modified version of Valve's Source engine powers the game" **[Doc]**
([Wikipedia](https://en.wikipedia.org/wiki/Titanfall_2)). This is why Source movement math (section 2)
is the right base. Every pilot has a jump kit for "parkour, double-jumping, and wall-running", and
wallruns "can be chained together"; sliding is new in TF2 **[Doc]** (same page;
[Titanfall wiki](https://titanfall.wiki.gg/wiki/Titanfall_2)).

**Hard numbers are scarce.** I found **no published, measured Titanfall 2 speeds or timings**. The
only datamined figures are from **Titanfall 1** (2014 beta), and the open "Titanfall constants" I
found belong to a student re-implementation. Treat every number below as a lead, not as Respawn's
tuning.

| Mechanic | What it does | Known numbers | Type |
|---|---|---|---|
| Run / sprint | Sprint is the base fast state | TF1: run 180.6 u/s, sprint 269.85 u/s ([Gameranx](https://gameranx.com/updates/id/20713/article/titanfall-raw-weapon-stats-decrypted-titan-health-and-speed-revealed/), from a "weapon raw stats" forum thread). At ~1 inch per unit that is 4.6 and 6.9 m/s. The article's own mph figures don't match that conversion, so the scale is uncertain. | [Comm], TF1 |
| Jump + double jump | One extra air jump from the jump kit | No numbers found. A player recalls "a little delay after jump to use double jump" ([itch comment](https://aaa-trsh.itch.io/trsh-mvmt/comments)). | [Doc] exists / [Comm] delay |
| Wallrun | Jump at a wall at an angle, hold toward it, run along it, faster than sprint | TF1: **1.75 s** (3.5 s with the Enhanced Parkour Kit), "about double their sprinting speed" ([wiki](https://titanfall.wiki.gg/wiki/Wall_Running)). TF2: after the 2016 tech test Respawn said "We'll be tuning air speed and wall-running speed to be faster" and "Players should once again accumulate and retain more speed when chaining wallruns", after finding side effects "causing players to lose momentum while wallrunning" ([Respawn post via well-played](https://www.well-played.com.au/respawn-release-titanfall-2-technical-test-feedback-blog/), [GamingBolt](https://gamingbolt.com/titanfall-2-dev-to-increase-wallrunning-speed-reintroduce-titan-timer)). | [Doc] TF1 wiki + Respawn statement |
| Wallrun entry, height | Community: TF2 **keeps your vertical velocity** on entry (a jump carries you up the wall); you can only re-jump onto the **same wall once you are lower** than where you first latched ([itch comments](https://aaa-trsh.itch.io/trsh-mvmt/comments)) | none | [Comm] |
| Wall jump | Jump off the wall away from it, keeping the run's speed. There is a "very narrow window" for extra speed at the end of a wall ([Steam thread](https://steamcommunity.com/app/1237970/discussions/0/4298195085740384735)) | none | [Comm] |
| Slide | Crouch while sprinting or moving: a short slide "preserving momentum and allowing for slide-hops" ([wiki](https://titanfall.wiki.gg/wiki/Titanfall_2)). Slide boost: one player recalls a cooldown of "2 seconds iirc" ([itch comment](https://aaa-trsh.itch.io/trsh-mvmt/comments)). | ~2 s boost cooldown | [Doc] / [Comm] |
| Slide hop (bunny hop) | Get airborne, **press crouch just before landing**, jump on touchdown. This keeps, and sometimes increases, momentum and lets you turn corners ([wiki](https://titanfall.wiki.gg/wiki/Slide_Hop)). Mistimed rhythm "loses speed to ground friction" ([Steam](https://steamcommunity.com/app/1237970/discussions/0/4298195085740384735)). Respawn on TF2: "scaling back some of the dampening factors that were limiting bunny-hopping … as we don't want these restrictions to impact normal traversal" ([well-played](https://www.well-played.com.au/respawn-release-titanfall-2-technical-test-feedback-blog/)). | Landing window: frame-tight in Source-style engines (2.4) | [Doc] intent / [Comm] technique |
| Air strafe / lurch | You gain speed by turning while holding strafe, "due to the source engine". **Lurch**: pressing a new direction just after a jump redirects you, but you generally lose speed overall ([Steam](https://steamcommunity.com/app/1237970/discussions/0/4298195085740384735)) | none | [Comm] |
| Mantle | Pulls you onto ledges you reach (you can see it in game) | No documentation found | gap |
| Momentum cap | TF1: chained wallruns reach "almost twice sprint speed" ([Gameranx](https://gameranx.com/updates/id/20713/article/titanfall-raw-weapon-stats-decrypted-titan-health-and-speed-revealed/)) | ~2× sprint | [Comm], TF1 |
| FOV | PC slider 70-110 ([WSGF](https://www.wsgf.org/node/29698)) | 70-110 | [Doc-ish] |

**An example "Titanfall-like" tuning (not TF2 data).** A UCSD CSE 125 student project
([constants](https://cse125.ucsd.edu/2026/cse125g2/docs/TitanfallConstants_8hpp_source.html)) shows
one coherent set, in Quake units (×0.0254 for metres). Slide: entry ≥400 u/s, boost 180-380 u/s,
cooldown 1.5 s, braking 200→400 u/s² over 3 s. Wallrun: min height 50 u, min attach speed 200 u/s,
max 800 u/s, 1.0 s zero-gravity "grip" then a 2.0 s gravity ramp, 5 s limit. Wall jump: 640 u/s up
and 350 u/s out. Camera roll 7.5°. Coyote 0.15 s. Double-jump lockout 0.10 s. Lurch: 12.5% speed
loss, 0.2-0.5 s window. Useful as a shape, not as truth.

**Design intent (developer statements).**
- Respawn COO Dusty Welch: the FPS space felt "tired and stale" with "pop-and-lock gameplay and linear
  levels", so they built around "freedom of movement and parkour"
  ([God is a Geek](https://www.godisageek.com/2016/10/an-interview-with-dusty-welch-chief-operating-officer-for-respawn-entertainment)).
- On the TF1 → TF2 changes: "changes to pilot mobility stemmed from a desire to improve our gunplay,
  and not a desire to slow down movement"; low predictability got players "shot from the back or sides"
  ([well-played](https://www.well-played.com.au/respawn-release-titanfall-2-technical-test-feedback-blog/)).
- In hindsight (Apex): wallrunning was fine in TF2's linear maps but "the counterplay was often
  impossible" in open spaces (Moy Parra, via
  [Charlie Intel](https://www.charlieintel.com/apex-legends/respawn-dev-explains-why-apex-legends-will-never-have-wall-running-75098/)).
  For a single-player chase that is a plus: there is no counterplay to protect.
- Campaign: built with **"action blocks"**, small playable movement vignettes prototyped quickly and
  then torn down. *Into the Abyss* is the case study (GDC 2018, Christopher Dionne,
  [GDC Vault](https://gdcvault.com/play/1025105/Designing-Unforgettable-Titanfall-Single-Player)). The
  team met weekly to show short vignettes built on movement
  ([Rolling Stone](https://www.rollingstone.com/culture/culture-news/titanfall-2-is-going-somewhere-unexpected-with-single-player-campaign-121636/)).

**What makes it feel good.** No developer source lists these for TF2, so this is **[Op]**, backed by
general game-feel sources:
- **Momentum that survives transitions**: slide → jump → wall → jump never drops speed unless you make
  a mistake (this is Respawn's own stated goal, above).
- **Forgiveness**: coyote time, jump buffering and wide wall-jump windows (Celeste's list:
  [Celeste & Forgiveness](https://maddymakesgames.com/articles/celeste_and_forgiveness/); Celeste's
  `JumpGraceTime = 0.1f` in [Player.cs](https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs)).
  Also: forgiving wall detection (catch the wall from a short distance), and an auto-mantle that
  rescues near-miss jumps.
- **Camera**: a small roll away from the wall while wallrunning, FOV widening with speed, a landing
  dip, the camera lowering on a slide. (The camera roll and FOV increase are also what a popular Unity
  wall-run write-up adds: [Medium](https://dude123code.medium.com/finally-a-good-wall-run-in-unity-4de42bcb7289).)
- **Audio**: a wind layer that rises with speed, a slide scrape, a faster wallrun step cadence, a
  landing thud scaled by impact.

<!-- S2 -->

## 2. The physics it descends from: Quake / Source

### 2.1 The actual code [Doc]

From Valve's Source SDK 2013, `src/game/shared/gamemovement.cpp`
([GitHub](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/shared/gamemovement.cpp)),
trimmed:

```c
// Ground: CGameMovement::Accelerate
currentspeed = dot(velocity, wishdir);
addspeed     = wishspeed - currentspeed;
accelspeed   = accel * frametime * wishspeed * surfaceFriction;
if (accelspeed > addspeed) accelspeed = addspeed;    // (returns early if addspeed <= 0)
velocity    += accelspeed * wishdir;

// Air: CGameMovement::AirAccelerate - identical except the wish speed is capped first
if (wishspd > GetAirSpeedCap()) wishspd = GetAirSpeedCap();   // GetAirSpeedCap() returns 30.f
addspeed   = wishspd - dot(velocity, wishdir);
accelspeed = accel * wishspeed * frametime * surfaceFriction;  // note: uses UNCAPPED wishspeed
// clamp to addspeed, add along wishdir as above

// Ground friction: CGameMovement::Friction
control  = (speed < sv_stopspeed) ? sv_stopspeed : speed;
drop    += control * sv_friction * surfaceFriction * frametime;
newspeed = max(speed - drop, 0);  velocity *= newspeed / speed;

// Jump: velocity.z += sqrt(2 * gravity * height)   (268.33 u/s = sqrt(2*800*45) at gravity 800)
```

`GetAirSpeedCap()` returning `30.f` is in
[gamemovement.h](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/shared/gamemovement.h).
Common default cvars are `sv_friction` 4 (5.2 in CS:GO) ([Valve wiki](https://developer.valvesoftware.com/wiki/Sv_friction)),
and `sv_stopspeed` 100, `sv_maxspeed` 320, gravity 800, air accelerate 10 (Quake-family defaults in the
[ezQuake manual](https://www.quaddicted.com/files/webarchive/ezquake.sourceforge.net_20121129/docs/index.html%3Fvars-serverside-physics.html)).
Defaults differ per game (CS:GO/CS2 use `sv_airaccelerate` 12: [csdb](https://csdb.gg/commands/sv_airaccelerate)).
In metres (1 u ≈ 1 inch, the usual convention): 320 u/s ≈ 8.1 m/s, stopspeed ≈ 2.5 m/s,
gravity ≈ 20.3 m/s², air cap 30 u/s ≈ **0.76 m/s**.

### 2.2 Why the formulas behave the way they do

- **Ground accel** is exponential toward `wishspeed`: the rate is `accel·wishspeed` m/s², so
  `accel = 10` reaches full speed in about 0.1 s. Input alone can never push the component of
  velocity along `wishdir` past `wishspeed`.
- **Friction** is proportional to speed above `stopspeed` (an exponential decay with time constant
  `1/friction`). Below `stopspeed` it becomes a constant deceleration, so you actually stop.
- **The cap only limits the projection** `dot(v, wishdir)`, not `|v|`. "The max is not strictly
  adhered to due to strafejumping" ([Flafla2, Bunnyhopping from the Programmer's Perspective, CC BY 4.0](https://adrianb.io/2015/02/14/bunnyhop.html)).

### 2.3 Air strafing, in one line of math

For an added amount `a` along a unit `w`, with `p = dot(v, w)`: `|v'|² = |v|² + 2a·p + a²`. The clamp
gives `a = min(A, cap − p)`, where `A = airAccel·wishspeed·dt`. If the player turns so that
`p = cap − A` (nearly perpendicular when `cap` is small), each tick adds:

```
Δ(|v|²) = 2A(cap − A) + A² = 2A·cap − A²   → at most cap² per tick (when A = cap)
speed after N ticks ≈ sqrt(v0² + N·cap²)    → gain per second ≈ tickrate · cap² / (2|v|)
```

Two consequences:
1. **Speed gain gets slower the faster you go** (it divides by `|v|`). That is a natural soft cap.
2. **It depends on tick rate**, because the cap applies per tick. At 0.76 m/s, 100 Hz, from 10 m/s the
   best case is about 2.9 m/s per second; at 50 Hz it halves. So **the simulation must use a fixed
   step** (4.9).

### 2.4 Bunny hopping and the landing window [Doc + Comm]

Friction runs only while grounded. On a landing tick where jump is pressed, the jump code takes you
off the ground before friction runs, so no speed is lost: "on landing there is a one-frame window"
([Flafla2](https://adrianb.io/2015/02/14/bunnyhop.html)). Air-strafing between hops adds speed, and the
hops avoid friction. Speed exploits like this have been patched game by game. For example, HL2's
"accelerated hop" came from a jump boost that was capped without regard to direction
([SourceRuns wiki](https://wiki.sourceruns.org/en/Source/Accelerated_Hop)). **For us:** a frame-tight
window is too hard for a narrative game, so widen it into a **landing friction grace window** (4.3, 4.5).
TF2's "crouch before landing" slide hop is the same idea: a slide has almost no friction, so landing
into one keeps your speed.

<!-- S3 -->

## 3. Unity implementation options

**Facts used:**
- `CharacterController.Move` "does not use gravity"; "a collision constrains the Move"; it returns
  `CollisionFlags` (Sides/Above/Below) **[Doc]**
  ([Move](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/CharacterController.Move.html)).
- It "does not react to forces on its own". Skin width should be "at least greater than 0.01 and more
  than 10% of the Radius" **[Doc]**
  ([manual](https://docs.unity3d.com/6000.3/Documentation/Manual/class-CharacterController.html)).
- Underneath is the PhysX CCT: "a kinematic controller" using "collide and slide" with **sweep tests**.
  This avoids the tunnelling that discrete physics checks suffer **[Doc]**
  ([PhysX docs](https://nvidia-omniverse.github.io/PhysX/physx/5.4.1/docs/CharacterControllers.html)).
  PhysX has platform "riding" flags, but Unity's component does not expose them, so moving platforms
  are done by hand.
- Rigidbodies jitter when physics runs slower than the frame rate. Interpolate "has a time lag of one
  physics update" **[Doc]**
  ([interpolation](https://docs.unity3d.com/6000.3/Documentation/Manual/rigidbody-interpolation.html)).
  Fast bodies need CCD, which "does not always deliver physically accurate collision results"
  ([CCD](https://docs.unity3d.com/6000.3/Documentation/Manual/ContinuousCollisionDetection.html)).
- Philippe St-Amand's **Kinematic Character Controller (KCC)**: **free** on the Asset Store,
  **v3.4.4, last release 7 Jun 2022**, made for Unity 2020.3, **Standard Asset Store EULA**. It is
  **not open source** **[Doc]** ([Asset Store](https://assetstore.unity.com/packages/tools/physics/kinematic-character-controller-99131)).
  It is collide-and-slide, and rigidbody interaction "must be explicitly scripted"
  ([forum](https://discussions.unity.com/t/released-kinematic-character-controller/678434)). Its
  successor, Unity's `com.unity.charactercontroller`, is **ECS (Entities) only**
  ([docs](https://docs.unity3d.com/Packages/com.unity.charactercontroller@1.4/manual/index.html),
  [samples](https://github.com/Unity-Technologies/CharacterControllerSamples)).
- **Starter Assets – First Person**: free, built on the built-in CharacterController, needs the Input
  System and Cinemachine **[Doc]**
  ([Unity blog](https://unity.com/blog/games/say-hello-to-the-new-starter-asset-packages)). A Unity 6
  URP sample issue with the FirstPersonController was fixed in 6000.4.0a5
  ([issue](https://issuetracker-mig.prd.it.unity3d.com/issues/urp-package-samples-scenes-using-firstpersoncontroller-not-working-in-unity-6)).

**Comparison** (the cells are [Op], built on the facts above):

| | CharacterController (built-in) | Rigidbody | KCC (St-Amand) | Starter Assets FPS |
|---|---|---|---|---|
| Model | Kinematic, you own velocity | Physics solver owns velocity | Kinematic, you own velocity via callbacks | CC plus a simple script |
| Wallrun | Easy: you set the velocity; casts find the wall | Fights wall friction (needs a 0-friction material), gravity toggling, solver jitter | Easy, with good grounding/ledge info | Not included; you rewrite most of it |
| Slopes / stairs | `slopeLimit`, `stepOffset` built in; `isGrounded` can flicker (add your own ground cast) | Manual stairs, slides down slopes unless countered | Best of the four (step and ledge handling) | Same as CC |
| Moving platforms | Manual (add the platform's delta) | Free via friction (sort of) | Built in | Manual |
| Fast speeds | Sweeps, so no tunnelling against static geometry | Needs CCD; still imperfect | Sweeps | Sweeps |
| Determinism / frame rate | As deterministic as your step: call `Move` in your own fixed loop | Tied to FixedUpdate (50 Hz here) plus interpolation | Has its own fixed-update and interpolation system | Per-frame `Update` |
| Pure C# testable core | Yes: the core outputs velocity, Unity only moves | Hard (the solver is the logic) | Partly (logic lives in its callbacks) | No |
| Cost for this team | **Zero**: already in DoomPlayer | Medium-high tuning time | Import + learn the API; 2022, untested on 6.3; EULA (fine in a private repo, but no redistributing its source) | Low start, but you would replace it |

**Recommendation [Op]: the built-in CharacterController plus a pure C# movement core.**
1. The team's player, tests and docs already use it, and no third-party licence enters the repo.
2. Titanfall movement is about *authored velocity*, not simulated physics. Kinematic is the right
   model, and the Source formulas drop straight in.
3. The sweep-based `Move` handles 20+ m/s without tunnelling.
4. Its weaknesses (a flaky `isGrounded`, no platforms) can be fixed with a ground cast and a platform
   delta.

Switch to KCC only if moving platforms or complex stairs become central. Avoid Rigidbody for the
player.

<!-- S4 -->

## 4. Implementation blueprint

Every number here is a **[Op] starting point** to tune in play. Units are metres, seconds and m/s,
with gravity `g = 24 m/s²` (between Source's ~20 and the template's 30).

### 4.1 States

```
          jump / walk off edge                   wall found (4.4)
 Ground ───────────────────────► Air ───────────────────────────► Wallrun
  ▲  │ crouch & speed≥6.5          │  ▲ land                         │ jump → Air (wall jump)
  │  ▼                             │  └──────── end/timeout/away ────┤
 Slide ◄── land with crouch held ──┘                                 │
   │ jump → Air (keeps speed)       ledge found (4.7) from Air/Ground/Wallrun ──► Mantle ──► Ground
```

Sprint is a flag inside Ground, not a separate state. For a chase I recommend **auto-sprint** when
moving forward (option to turn it off). This frees a button and matters for gamepad (7.2).

### 4.2 Shared pieces (pure C#)

```csharp
static Vector3 Accelerate(Vector3 v, Vector3 wishDir, float wishSpeed, float accel, float dt) {
    float add = wishSpeed - Vector3.Dot(v, wishDir);
    if (add <= 0) return v;
    return v + wishDir * MathF.Min(accel * wishSpeed * dt, add);           // Source Accelerate
}
static Vector3 AirAccelerate(Vector3 v, Vector3 wishDir, float wishSpeed, float accel, float cap, float dt) {
    float add = MathF.Min(wishSpeed, cap) - Vector3.Dot(v, wishDir);
    if (add <= 0) return v;
    return v + wishDir * MathF.Min(accel * wishSpeed * dt, add);           // Source AirAccelerate
}
static Vector3 Friction(Vector3 vH, float friction, float stopSpeed, float dt) {
    float s = vH.Length(); if (s < 1e-4f) return Vector3.Zero;
    float drop = MathF.Max(s, stopSpeed) * friction * dt;
    return vH * (MathF.Max(s - drop, 0) / s);
}
static Vector3 Clip(Vector3 v, Vector3 n) { float d = Vector3.Dot(v, n); return d < 0 ? v - n * d : v; }
```

`Clip` replaces DoomPlayer's "zero the velocity on side hits". Collect the hit normals from
`OnControllerColliderHit` during `Move`, then clip. Grazing a wall then keeps the speed along it.

### 4.3 Per-state velocity update

| State | Update per step (h = 1/120 s) | Starting numbers |
|---|---|---|
| **Ground** | If within the landing grace: no friction. Else if `speed > sprint`: decay only the excess, `v = v·(sprint + (s−sprint)·e^(−k·h))/s`. Else `Friction`. Then `Accelerate(wish, sprint or walk, accel)`. Snap down to the ground on slopes when not jumping. | walk 5, **sprint 9.5** (the template's run speed), accel 10, friction 6, stopSpeed 2.5, overspeed decay `k` 1.5/s, landing grace **0.15 s** |
| **Jump** | `vy = sqrt(2·g·H)`; keep horizontal | H 1.25 m → **7.75 m/s** |
| **Double jump** | Once per airtime (refreshed on ground and on wallrun). `vy = max(vy, 6.9)`. Redirect horizontal: `vH = lerp(vH, wishDir·|vH|, 0.5)` (a TF-like "lurch"). Locked for 0.1 s after the first jump. | 1.0 m extra height, lockout 0.10 s |
| **Air** | `AirAccelerate(wish, sprint, airAccel, airCap)` (air strafing) plus a gentle low-speed control `Accelerate(wish, 5, 2)` so standing jumps can steer. `vy -= g·h`. Hard cap on horizontal speed. | airAccel 12, **airCap 0.8 m/s**, cap **22 m/s**, terminal fall 40 m/s |
| **Slide** | Entry: crouch pressed, grounded, `speed ≥ 6.5`. **Boost**: if 2 s have passed since the last boost, `s += 3` (only up to 13 total). Deceleration `2.5 m/s²` for 0.6 s, then `6 m/s²`. On slopes add `g·sinθ` downhill. Turn the velocity toward the wish at ≤ 60°/s. Exit: `s < 4` → crouched Ground; crouch released → Ground; jump → Air with `vy = 0.9·jumpV` and **all horizontal speed kept**. | capsule height 1.0 m, eye 0.8 m (blend over 0.12 s); check headroom before standing |
| **Slide hop** | Crouch pressed in the air within **0.2 s** of landing (or held) and `speed ≥ 6.5` → land straight into Slide: no landing friction, boost if off cooldown. | the Titanfall technique, made forgiving |
| **Wallrun** | See 4.4 | run 12 m/s, duration 1.75 s |
| **Mantle** | See 4.7 | 0.25-0.35 s |

**Level-design gap metrics that follow from these numbers** (flat ground, no air strafing):
- Single jump: airtime 0.65 s → about **6 m** at 9.5 m/s.
- Jump plus double jump at the apex: about 1.05 s → about **10 m** at 9.5, **13.5 m** at 13 (slide-boosted).
- A full wallrun at 12 m/s for 1.75 s covers about **21 m**.
- Mantle reach: up to 1.9 m above the feet.

### 4.4 Wall detection and wallrun

**Detection** (each step, in Air, plus Wallrun to keep contact): SphereCast with radius 0.25 from
chest height (feet + 1.0 m) toward ±right and ±45° forward-diagonals, length `radius + 0.35 m`. Wall
detection is forgiving in both reach and angle. Accept a hit when **all** hold:

| Check | Value |
|---|---|
| Wall is near-vertical | `|n.y| ≤ 0.25` |
| High enough | ray down: ≥ **0.6 m** above ground |
| Fast enough | horizontal speed ≥ **5 m/s** |
| Moving *along* it, not into it | `dot(v̂H, t̂) ≥ 0.34` (within 70° of the tangent; head-on → mantle instead) |
| Not pushing away | `dot(wish, −n) ≥ −0.1` |
| Same-wall rule (TF2 community rule) | Same wall (`dot(n, lastN) > 0.95`, plane offset < 0.5 m) only if lower than the last attach height minus 0.5 m, or after touching ground |
| Tag/layer | Optional `Runnable` layer: lets designers decide (6.4) |

**On entry:** tangent `t̂ = normalize(cross(up, n))`, flipped so that `dot(t̂, vH) > 0`. Redirect
all horizontal speed: `vH = t̂·|vH|`. **Keep upward `vy`** (TF2 community observation) and clamp
downward `vy` to ≥ −1. Refresh the double jump.

**Each step** (`t` = time on the wall, `T` = 1.75 s):

```
along   = dot(v, t̂)
along   = along < run ? min(run, along + 6·h)                // accelerate up to 12 m/s
                      : max(run, along − 1·h)                // keep extra momentum, bleed slowly
gScale  = lerp(0.1, 0.8, smoothstep(0.2, T, t))              // near-weightless, then sinking
vy      = max(vy − g·gScale·h, −lerp(1, 6, t/T))              // sink speed limited, grows with time
v       = t̂·along + up·vy − n·0.5                            // small push keeps contact
```

**Exit** when `t ≥ T`, the wall is lost for > 0.1 s (bridges seams and short gaps), `along < 4`,
grounded, the wish points away (`dot(wish, n) > 0.7` for 0.1 s), crouch is pressed (drop off), or
jump (wall jump).

**Wall jump:**

```
vH = t̂·max(along, run) + n·5.5             // keep run speed, push off
if dot(lookH, n) > 0: vH = slerp(vH, lookH·|vH|, 0.3)   // steer toward where the player looks
vy = 7.75                                    // same as a ground jump
```

**Wall coyote:** within **0.2 s** after falling off a wall, jump still counts as a wall jump (using
the stored `n` and `t̂`).

### 4.5 Forgiveness timers

| Timer | Value | Source / reason |
|---|---|---|
| Coyote (ground) | **0.12 s** | Celeste 0.1 s ([Player.cs](https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs)); student TF-like 0.15 s |
| Coyote (wall) | 0.2 s | [Op] wall exits are harder to time |
| Jump buffer | **0.12 s** (consumed on landing, wall attach or the mantle end) | Celeste technique ([article](https://maddymakesgames.com/articles/celeste_and_forgiveness/)) |
| Landing friction grace | **0.15 s** | [Op] widens Source's one-frame bhop window |
| Slide-hop crouch buffer | 0.2 s | [Op] |
| Wall lost grace | 0.1 s | [Op] |
| Double-jump lockout | 0.1 s | student 0.10 s; [Comm] TF delay |

### 4.6 Speed caps

Horizontal hard cap 22 m/s (about 2.3× sprint, near TF1's "almost twice sprint"). Air strafing
stays within it, and its gain shrinks by itself as speed rises (2.3).

### 4.7 Mantle

When pressing forward (`dot(wish, fwd) > 0.5`) in Air, Ground or at the end of a Wallrun:
1. Forward SphereCast at chest height finds a wall (`|n.y| < 0.3`) within 0.6 m.
2. A downward cast from `hit − n·0.35 + up·2.1` finds a top (`n.y > 0.7`) at a ledge height of
   **0.5-1.9 m** above the feet.
3. `Physics.CheckCapsule` confirms the capsule fits on top.
4. `vy > −10`.

Then lock input and move along two segments, up then forward 0.5 m, over `0.2 + 0.07·height` s.
Leave with `forward · max(0.6·entrySpeed, 5)`. The camera pitches down 3° and back.

### 4.8 Camera and feel

| Effect | Formula | Start | Setting |
|---|---|---|---|
| Wallrun roll | `roll → ±8°` (top of view tilts away from the wall), smoothed `1 − e^(−10·dt)` | 8° | **slider 0-100%, default on; toggle** |
| FOV by speed | `fov = base + 8°·smoothstep(9.5, 22, speed)` (+3° while sliding), smoothed at 6/s | base vertical 68° (≈100° horizontal at 16:9) | base FOV slider; "speed FOV" slider 0-100% |
| Landing dip | `dip = 0.1 m · clamp01((|vy| − 5)/15)`, critically damped spring back (ω 20) | 0.1 m | follows the head-bob slider |
| Slide | eye 1.5 → 0.8 m over 0.12 s; roll 2° toward the steer side | | |
| Head bob | the existing DoomPlayer bob on Ground only; off in Air/Slide/Wallrun | existing `bobAmount` | existing |
| Audio | wind loop `vol = smoothstep(6, 18, speed)`; slide scrape loop; wallrun steps at 1.5× cadence; landing thud scaled by `dip` | | |

### 4.9 Frame-rate independence

- Run the movement core on a **fixed 120 Hz step** with an accumulator inside the per-frame
  `Simulate`, at most 6 steps per frame. Render with interpolation ([Fix Your Timestep](https://gafferongames.com/post/fix_your_timestep/):
  "the renderer produces time and the simulation consumes it in discrete dt sized steps").
- Call `controller.Move(v·h)` and the casts inside each step. Calling `Move` several times per frame
  is fine.
- Store the previous and current positions. Offset the camera rig by `lerp(prev, cur, α) − cur`,
  where `α = acc/h`.
- **Mouse look stays per frame** (applied to yaw/pitch at once). The step reads the latest yaw for
  the wish direction. Camera effects (roll, FOV, dip) are smoothed per frame with `1 − e^(−k·dt)`.
- Don't use the project's 50 Hz `FixedUpdate`. 0.4 m per step at 20 m/s is coarse for wall casts and
  halves the air-strafe gain (2.3). Changing `Time.fixedDeltaTime` globally would also change every
  other physics cost.

### 4.10 What is pure C# and what is Unity

| Pure C# (`Runtime/Core/Movement`, `System.Numerics`, dotnet-tested like `PlayerMotion`) | Unity side (`Templates/<Game>/Scripts`) |
|---|---|
| `MoveDef` (all the numbers above, serializable into a config asset) | Input System actions: move, look, jump, crouch/slide, sprint (optional) |
| `MoveInput` struct (wish, jump pressed/held, crouch, look yaw) | **Sensing** → `MoveSense` struct: grounded + ground normal (SphereCast down), height above ground, wall hits (normal, point, distance), ledge result, headroom |
| `MoveSense` struct (filled by Unity) | `CharacterController.Move`, hit normals from `OnControllerColliderHit`, capsule resize for slides |
| `MoveCore.Step(ref MoveState, in MoveInput, in MoveSense, MoveDef, h)` → velocity, events (jumped, landed + impact, wallrunStart/End(side), slideStart, mantle) and camera targets (roll, fovKick, eyeHeight, dip) | The fixed-step accumulator and interpolation; camera rig, FOV, roll; audio; settings |
| `Accelerate`, `AirAccelerate`, `Friction`, `Clip`, the timers, the wall rules, the slide-boost cooldown, the same-wall rule | Moving-platform delta (if needed) |

**Example tests:**
- Air strafing at the optimal angle adds at most `cap²` to `|v|²` per tick.
- Input alone never takes ground speed past sprint.
- The same input at 30 and 144 render-fps gives the same position (fixed step).
- The slide boost honours its cooldown.
- A wallrun ends at T.
- A jump 0.11 s after walking off a ledge works, and one at 0.13 s doesn't.
- A buffered jump fires on landing.
- The same wall can't be re-attached at the same height.

<!-- S5 -->

## 5. Open-source references and tutorials

Licence, last push and stars come from the GitHub API on 2026-10-10. The quality notes are **[Op]**,
from READMEs and structure, not from a full code review. "No licence" means read-only: you may learn
from it, but not copy it.

| Repo | Licence | Last push | ★ | One-line judgement |
|---|---|---|---|---|
| [Olezen/UnitySourceMovement](https://github.com/Olezen/UnitySourceMovement) | MIT | 2021-11 | 388 | **Best formula reference.** A Source port (from Fragsurf) with bhop, surf, crouch-jump and optional slide. No wallrun. Older Unity. |
| [IsaiahKelly/quake3-movement-for-unity](https://github.com/IsaiahKelly/quake3-movement-for-unity) | Unlicense | 2021-02 | 105 | Small, readable Quake 3 strafe-jump port. A good first read. |
| [WiggleWizard/quake3-movement-unity3d](https://github.com/WiggleWizard/quake3-movement-unity3d) | none | 2021-09 | 303 | The original Q3 port the above is based on. Read-only. |
| [Fragsurf/Fragsurf-2](https://github.com/Fragsurf/Fragsurf-2) | "other" | 2021-12 | 96 | A full surf/bhop game. Heavy; check the licence before borrowing. |
| [nicholas-maltbie/OpenKCC](https://github.com/nicholas-maltbie/OpenKCC) | MIT | 2023-09 | 799 | An open-source kinematic collide-and-slide controller. A good architecture reference; several custom package dependencies. |
| [DaniDevy/FPS_Movement_Rigidbody](https://github.com/DaniDevy/FPS_Movement_Rigidbody) | MIT | 2022-10 | 524 | The Rigidbody approach (Karlson-style). Shows the counter-movement hacks a Rigidbody needs. |
| [Unity-Technologies/CharacterControllerSamples](https://github.com/Unity-Technologies/CharacterControllerSamples) | Unity licence | 2026-08 | 407 | ECS only. The "Platformer" sample is a state-heavy controller worth reading for structure. |
| [Egecekic/Unity-3d-Movements](https://github.com/Egecekic/Unity-3d-Movements) | none | 2025-12 | 34 | Slide, wallrun, wall jump, climb. Small and recent. Read-only. |
| [ozata/unity-ultimate-titanfall-2-wallrun](https://github.com/ozata/unity-ultimate-titanfall-2-wallrun), [Malthbern/TitanfallMovementTek](https://github.com/Malthbern/TitanfallMovementTek) | none | 2022 / 2021 | 3 / 7 | Tiny, unproven. Skip. |
| [NoelFB/Celeste `Player.cs`](https://github.com/NoelFB/Celeste) | MIT (this code only) | 2022-10 | n/a | 2D, but **the** reference for forgiveness timers and state structure. |
| KCC on the [Asset Store](https://assetstore.unity.com/packages/tools/physics/kinematic-character-controller-99131) | Asset Store EULA (free) | 2022-06 | n/a | Mature and widely used; last updated for 2020.3. |

No credible open-source *Titanfall-complete* Unity controller turned up. Paid ones exist on Fab
(Unreal) and the Asset Store.

**Reading and video:**
- Flafla2, ["Bunnyhopping from the Programmer's Perspective"](https://adrianb.io/2015/02/14/bunnyhop.html):
  the clearest explanation, with Unity code.
- Momentum Mod, [bhop guide](https://docs.momentum-mod.org/guide/gamemodes/bhop/): the player's view
  of the same physics.
- Maddy Thorson, ["Celeste & Forgiveness"](https://maddymakesgames.com/articles/celeste_and_forgiveness/).
- Glenn Fiedler, ["Fix Your Timestep!"](https://gafferongames.com/post/fix_your_timestep/).
- Christopher Dionne, GDC 2018, ["Designing Unforgettable Titanfall Single Player Levels with Action Blocks"](https://gdcvault.com/play/1025105/Designing-Unforgettable-Titanfall-Single-Player).
- Unity Learn, [Creating an FPS Wall Run Mechanic](https://learn.unity.com/tutorial/creating-an-fps-wall-run-mechanic-workshop-video-1)
  (beginner level).
- ["Finally, a good wall run in Unity"](https://dude123code.medium.com/finally-a-good-wall-run-in-unity-4de42bcb7289)
  (Rigidbody, with camera tilt and FOV).

<!-- S6 -->

## 6. Designing escape and chase sequences around it

### 6.1 What the reference games do

| Game / level | What makes it work | Source |
|---|---|---|
| **Titanfall 2 – Into the Abyss / Effect and Cause** | Built from small movement-first "action blocks", prototyped fast and thrown away freely. *Effect and Cause* (designer Jake Keating) puts two exactly aligned maps under one time-shift button. Each period is a threat to the other (a waxed floor in the past is a burning chasm in the present). Enemies leave a blue particle where they stood, to help tracking. | [GDC Vault](https://gdcvault.com/play/1025105/Designing-Unforgettable-Titanfall-Single-Player), [Wikipedia](https://en.wikipedia.org/wiki/Effect_and_Cause) |
| **Mirror's Edge** | **Runner Vision**: objects that offer movement fade to red as you approach. Catalyst kept "follow the red" and added Off / Classic / Full settings ([EA](https://www.ea.com/news/runners-vision-in-mirrors-edge-catalyst)). The chase is the game's engine: "If you slow down, they will catch you; if you get stuck, they will catch you" ([Cleary](https://www.gamedeveloper.com/design/analysis-chase-scenes-momentum-and-hidden-treasures)). Its known failure: in chases the next route was often **behind or beside the player**. The fix is to "place where the player has to go within view as soon as they turn the corner" ([World of Level Design](https://worldofleveldesign.com/categories/level_design_tutorials/one-mistake-in-mirrors-edge.php)). | see links |
| **Ghostrunner** | One-hit death, but "we make a lot of checkpoints, so each section is like a mini-puzzle" (lead level designer Marcin Kluzek). HP was avoided because it would make play less dynamic. | [Inverse](https://www.inverse.com/gaming/ghostrunner-interview-cyberpunk-hotline-miami), [TechRaptor](https://techraptor.net/gaming/features/ghostrunner-cyberpunk-new-heights-interview) |
| **Neon White** | The intended main path had to be understood **at a glance**: a clean, low-detail style with negative space. Levels are easy to finish, and faster routes reward replay. Each encounter was blocked out alone and then assembled. | [Game Developer](https://www.gamedeveloper.com/road-to-igf-2023/-neon-white-and-designing-for-player-creativity), [Game Informer](https://gameinformer.com/feature/2022/11/25/how-a-neon-white-level-is-made) |

### 6.2 Readability (guide the eye at 10-20 m/s)

All of the following is [Op], drawn from the cases above.
- **The next move is visible on entry.** Every corner, landing and wall-end shows the next surface
  inside the central ~60° of view. Never put the route behind the player during a chase.
- **One "go" colour**, used only for traversal. In a dream world: a glow or tint on runnable walls,
  mantle ledges and slide gaps (Runner Vision). Keep it off decoration. Offer an intensity setting
  like Catalyst's.
- **Light the exit, darken the dead ends.** Use the brightest value at the goal, plus motion (falling
  debris, birds, fleeing figures) pointing the way.
- **Lines of perspective** (rails, cables, pipes) aimed at the next surface.
- **Space for speed**: corridors ≥ 3 m wide, turns ≥ 90° only with a slide or wall to carry momentum
  through them, and no knee-high clutter on the main line.

### 6.3 Pacing and the pursuer

- **Script the beats, regulate the gap.** Big moments (a collapse, a near-miss grab) fire from
  **player-position triggers**, never timers, so they always happen just behind or beside the player.
- Between beats, rubber-band the pursuer [Op]:
  ```
  gap     = distanceAlongSpline(player) − distanceAlongSpline(pursuer)
  speed_p = clamp(playerSpeedAvg + k·(gap − targetGap), minP, maxP)
  ```
  Example: targetGap 12 m, k 0.6/s, minP 6 m/s, maxP = sprint + 2. It catches the player only if
  `gap < 2 m` for more than 0.75 s. That only happens when the player stops or falls, never as a
  surprise speed burst.
- Measure progress along a **spline** laid down the route, not as straight-line distance.
- Rhythm: about 20-40 s of chase, then a short breather (a closed door, a lift, a dialogue line),
  then the next chase. Chases are set pieces; quieter space connects them
  ([Level Design Book, pacing](https://book.leveldesignbook.com/process/preproduction/pacing)).
- **Escalate the movement vocabulary**: introduce wallrun safely (no pit under it), then over a pit,
  then chained, then inside the chase.
- **Dream-world collapses**: the floor behind the player dissolves and walls fold *toward the route*.
  The collapse direction itself points the way.

### 6.4 Telegraphing wallrun surfaces

- **Runnable = one consistent look** (material, trim, glow), flat, at least 3 m tall, at least about
  15 m long (a 1.75 s run at 12 m/s covers about 21 m).
- Put the runnable layer on those walls so the code accepts only those (4.4 table, last row). Players
  then learn the rule in one try.
- The take-off spot is visible from the approach. A gap the player cannot clear on foot (> 6 m, from
  4.3) is the clearest "use the wall" signal.

### 6.5 Fail states that don't kill the flow

- **Checkpoints every ~10-20 s** of chase, and an instant restart (fade under 0.5 s, no load). The
  pursuer is placed back at `targetGap`.
- **Forgiveness first**: wide mantle reach, wall coyote and the jump buffer rescue most near-misses.
  A fall into a pit can be a "dream drop" that respawns you a few metres back instead of a death
  screen.
- After 2-3 deaths at the same spot, quietly widen that spot (pursuer slower, a ledge highlighted).

<!-- S7 -->

## 7. Risks for this team

### 7.1 Motion sickness

Xbox Accessibility Guideline 117: "Provide adjustable field-of-view settings". It also says to avoid,
or allow disabling, camera shake, bobbing and motion blur, and to avoid "repetitive side-to-side or
up-and-down on-screen movement" **[Doc]**
([XAG 117](https://devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/117)). A TF2 VR
mod reviewer notes that wallrunning motion is hard to make comfortable
([Road to VR](https://roadtovr.com/?p=232796)). Ship these in the Settings menu from day one:

| Setting | Default | Range |
|---|---|---|
| Field of view | 68° vertical | 55-90° |
| Speed FOV effect | 100% | 0-100% |
| Wallrun camera roll | 100% | 0-100% (0 = off) |
| Head bob and landing dip | 100% | 0-100% (already exists as `bobAmount`) |
| Screen shake | 100% | 0-100% |
| Traversal highlight | Classic | Off / Classic / Full |

Keep the crosshair or a centre dot visible during fast motion (it gives the eye a fixed reference)
[Op], and use no motion blur by default.

### 7.2 Keyboard + mouse and gamepad parity

| Action | KB+M | Gamepad | Note [Op] |
|---|---|---|---|
| Jump / double / wall jump | Space | A / Cross | |
| Slide (crouch) | Left Ctrl or C | **B / Circle** (TF2's console default, per [Twinfinite](https://twinfinite.net/guides/titanfall-2-how-to-slide/)) | Slide-hop timing is easier with a face button than with a stick click |
| Sprint | auto (toggle option), Shift | auto, L3 | Auto-sprint avoids holding L3 at speed |
| Wallrun | automatic (hold toward/along) | automatic | No button: the same on both |
| Mantle | automatic (hold forward) | automatic | |

- Stick input gives a wish of length < 1. Treat it above 0.7 forward as full, so gamepad players
  reach sprint and slide speed.
- Air strafing on a stick works much worse than with a mouse. Don't make any required route depend
  on strafe gains: the speed sources that matter are slide boost, wallrun and wall jump, which are
  input-neutral.
- Gamepad look needs acceleration and a slight aim assist if there is shooting.

### 7.3 Scope

| Tier | Contents | Estimate [Op] |
|---|---|---|
| **MVP (3-day jam)** | Fixed-step pure core with Ground/Sprint, Air (Source accel), Jump + Double jump, **Slide with boost**, **Wallrun + wall jump**, **simple mantle**. Coyote, jump buffer, landing grace, wall coyote. FOV kick + roll + the 3 comfort sliders. Core tests. A grey-box chase: one spline pursuer with a rubber band, trigger-driven collapses, checkpoints. | Day 1 core + ground/air/jump/slide + tests; day 2 wallrun, mantle, camera; day 3 grey-box chase and tuning |
| Nice next | Slide hop with a crouch buffer, lurch, same-wall rule, slopes boosting slides, audio layers, traversal highlight | +1-2 days |
| Full | Moving platforms (platform delta), grapple, ziplines, wall climb, slide-boost fatigue, speedrun timer, a gamepad aim assist pass | later |

**Biggest risks:**
1. Tuning time is the real cost. Expose every number in a config asset and add a live debug panel
   showing speed and state.
2. Wall detection edge cases (corners, thin pillars, seams). The 0.1 s "wall lost" grace and the
   `Runnable` layer contain them.
3. A camera that is interpolated wrongly causes judder at high speed. Test at 30 and 144 fps early.
4. The template's own `DoomPlayer` must not be broken. Build a new player class that reuses its
   look, bob and health code, rather than editing it.

## Uncertain or unverified

- **No Titanfall 2 speeds, timings or caps were found published.** The numbers here are Titanfall 1
  (wiki, 2014 datamine), a student project, or my own. Measuring in game (the Gauntlet, with a
  speedometer mod) would be the way to get real ones.
- The Source unit → metre conversion (1 u ≈ 1 inch) is the usual convention. The Valve Dimensions page
  returned 403, so it was not re-checked. Gameranx's mph figures imply a different scale.
- TF2 details from players (the slide-boost cooldown of ~2 s "iirc", vertical velocity kept on a
  wallrun, the same-wall rule, the double-jump delay) are single comments, not measurements.
- Whether KCC 3.4.4 (made for 2020.3) runs cleanly on Unity 6.3 is unverified. Its Asset Store table
  shows URP "not compatible", but it is scripts only, so this probably doesn't matter; test it before
  relying on it.
- The repo quality notes in section 5 are from READMEs and metadata, not code review.
- Every value in section 4 is a starting point, not a measured or proven value.

