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

## 8. Feel: how speed is shown and heard

Labels as before: **[Doc]** developer or engine docs, **[Comm]** community or press, **[Op]** my
recommendation. Few studios document feel in numbers, so most parameters here are [Op] starting
points.

### 8.1 The visible first-person body

| Game | What you see | Source |
|---|---|---|
| Mirror's Edge | Arms, legs and torso are visible; looking down you see yourself. A preview found that seeing her feet "came in handy during an early jump". DICE's fix for sickness was to "animate ones perceptions of movement and to not try and replicate the exact body movements". Its tuck-and-roll (a quick full camera pitch) was called "rather disorienting". | [Wurb blog](https://www.wurb.com/stack/?p=669), [Wikipedia](https://en.wikipedia.org/wiki/Mirror%27s_Edge), [Engadget (legs carried into BF3)](https://www.engadget.com/2012-08-13-how-mirrors-edge-gave-legs-and-more-to-battlefield-3.html) [Comm] |
| Titanfall 2 | Arms and weapon only (no source shows legs). **There is a hand on the wall**: on approach the camera tilts and the view model "reaches a hand out to touch the wall" (the "lean-in"). Runners call it "super telegraphed": it shows "almost exactly when you're going to touch the wall", and they time wall kicks by it. In the game files a wallrun switches the weapon to a one-handed pose (0.2 s blend in, 0.35 s out). Slide: the gun tilts sideways (already in the air if crouch is held), the camera dips, and the FOV scales ×1.1 over 0.25 s. Details and sources in 8.7. | [bryonato 13:38, 16:38](https://www.youtube.com/watch?v=C_cXYyA4LaU), [Tvcx 0:32, 2:07](https://www.youtube.com/watch?v=uw-Do_JW3bI) [Comm]; game files (8.7) [Comm] |
| Apex Legends | Extended TF2's knee slide so it keeps speed downhill and chains into jumps (SI feature, now offline). The movement Legend Axle's "Drift" adds speed and lateral control while sliding. | [EA](https://www.ea.com/news/apex-legends-s29-meet-axle) [Doc] |

**What to build for the jam** [Op, revised against the evidence in 8.7]: an **arms-only rig**, mostly
procedural (springs plus a few poses), not a full body. Lower in priority order:
1. **Sprint**: arms pump in sync with the footstep phase (reuse DoomPlayer's `_bob` phase).
   Amplitude about 4 cm forward and back, 1.5 cm vertical; zero in the air. Keep the camera itself
   almost still (bob is the most reported cause of nausea, 8.7). The amplitude is still my guess.
2. **Wallrun**: the wall-side hand **starts reaching before contact** (predict the hit about 0.25 s
   ahead), and plants on the wall on the contact frame, at about `wallHit.point + t̂·0.35 m`
   (Two-Bone IK, or a lerped transform). That hand leaves its swing pose, as TF2's weapon goes
   one-handed. The plant is a timing cue for the wall kick, so it must land on the contact tick.
3. **Slide**: the arms **cant sideways** and the camera dips, with a ×1.1 FOV over 0.25 s (TF2).
   Show the cant already in the air when crouch is held, as a "slide coming" cue. Shins and feet in the
   bottom of the screen are optional (Mirror's Edge shows legs; TF2 shows none, as far as I found).
4. **Jump / double jump**: no fixed lift. The arms trail vertical velocity: they sink up to about
   1.5 cm while rising and float up while falling (TF2 sway). On the double jump, play one clear pose
   and **hold it about 0.25-0.3 s** so it reads (Destiny holds key poses 7-10 frames).
5. **Landing**: the arms take the same dip spring as the camera (4.8), about one frame later, scaled
   by fall height, with no dip on small drops.

### 8.2 Camera and screen effects

| Effect | Reception (what the community says) | Starting parameters [Op] |
|---|---|---|
| **FOV widening with speed** | Widening the FOV "makes the perspective deform faster, which gives a sense of motion"; many games do it subtly ([GDQuest](https://www.gdquest.com/library/glossary/field_of_view)). Players generally find a *higher* base FOV reduces sickness ([Ghostrunner guide](https://www.gamerevolution.com/?p=663792): start around 90). | +8° vertical at the 22 m/s cap (4.8), smoothed. Above about 100° horizontal, consider URP **Panini Projection**, which "helps render perspective views in scenes with a very wide field of view" ([URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Post-Processing-Panini-Projection.html)). |
| **Wallrun roll** | Subtle tilt is standard. Big camera rotations (Mirror's Edge's roll) disorient. | 8°, slider (4.8). |
| **Landing dip** | Standard and rarely criticised. | 0.1 m max (4.8). |
| **Shake** | Trauma model from Squirrel Eiserloh's GDC 2016 "Juicing Your Cameras With Math": noise-driven offsets ∝ trauma², trauma decays. Bevy's reference uses decay 0.5/s, shake = trauma², max roll 10°, noise speed 20 ([Bevy](https://bevy.org/examples/camera/2d-screen-shake/)). | Far smaller in first person: max roll 1.5°, translation 0.02 m. Only on hard landings, collapses and near-miss grabs; **never continuous at speed**. Slider. |
| **Speed lines** | A classic cheap cue: a BioWare developer recalls "just added some wind lines" to make sprinting horses feel faster ([Yahoo/Kotaku report](https://sg.news.yahoo.com/vile-treachery-ran-citadel-mass-214034627.html)) [Comm]. | Edge-only streaks, alpha `0.35·smoothstep(12, 20, speed)`. Never inside the central ~40% of the screen. |
| **Motion blur** | The setting players most often turn off for nausea ([Lemmy thread](https://lemmy.ndlug.org/post/1628435/3245359), [Doom Steam thread](https://steamcommunity.com/app/782330/discussions/0/2264691750486371019)); the Ghostrunner guide suggests "Off" as the starting point [Comm]. Some like it in Doom Eternal. | URP Motion Blur **Camera Only** mode, Intensity 0.15, Clamp 0.02 (the default is 0.05) ([URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Post-Processing-Motion-Blur.html)). **Default off**, offered as an option. |
| **Vignette at speed** | Research backs it: dynamically narrowing the FOV during fast motion "significantly reduced" VR sickness, and most participants didn't notice (Fernandes & Feiner, IEEE VR 2016, via [IEEE Spectrum](https://spectrum.ieee.org/dynamic-field-of-view-restriction-makes-virtual-reality-less-barfy)) [Doc, VR study]. | Intensity 0.15 → 0.32 with speed, smoothness 0.4. This doubles as a comfort feature, so keep it even when other effects are off. |
| **Chromatic aberration** | Commonly disabled along with blur and DoF ([Lemmy](https://lemmy.ndlug.org/post/1628435/3245359)) [Comm]. | Only as a 0.2 s pulse on slide boost or wall jump, intensity ≤ 0.3. Off when "reduce effects" is on. |

Note the tension: a **wider** FOV sells speed, while a **narrower** visible field (vignette) reduces
sickness. Doing both (a modest FOV kick plus a soft vignette) gives speed without much extra optic
flow at the edges [Op].

### 8.3 Audio

| Sound | How it behaves | Starting parameters [Op] |
|---|---|---|
| **Wind** | The main speed cue. Titanfall 2's audio team used "variables like speed, angle, distance, or health at runtime to create a much more dynamic mix" (Tyler Parsons, [A Sound Effect](https://asoundeffect.com/titanfall-2-sound/)) [Doc]. | Loop. Volume `smoothstep(6, 18, speed)`; low-pass cutoff 800 Hz → 8 kHz with speed; pitch 0.9 → 1.15. Slight extra on the side away from a wallrun wall. |
| **Footsteps** | Cadence from distance travelled (DoomPlayer already does this with `footstepStride` 2.3 m). | At sprint, a stride of about 2.0 m gives ~4.7 steps/s; random pitch ±5%. |
| **Slide** | TF2 has dedicated slide Foley (Parsons, same source). | A scrape loop starting with a short "drop" transient. Volume and pitch follow speed and fade out by 4 m/s. Boost: an extra "whoosh" layer. |
| **Wallrun** | | Steps at 1.5× cadence, harder and brighter; plus a quiet shoe scrape loop. A "catch" transient on attach (the most important one: it confirms the wall took you). |
| **Jump / double jump** | TF2's double jump is a jump-kit thrust ([Wikipedia](https://en.wikipedia.org/wiki/Titanfall_2)). | Jump: a cloth/breath whoosh. Double jump: a distinct thruster-like puff (in a dream world, a chime or "air pop"). The two must sound clearly different. |
| **Landing** | | Thud gain `clamp01((|vy| − 3)/15)`, plus a low-frequency layer above 12 m/s. Soft landing into a slide = no thud, just the slide start. |
| **Music** | Mirror's Edge Catalyst's score was described as adaptive, adjusting its tempo to the player's pace ([Xboxygen](https://www.xboxygen.com/?p=84006)) [Comm]. | Vertical layering is easier than tempo: 3 stems (base, drive, peak) whose volumes follow speed tiers (< 8, 8-13, > 13 m/s), with 1 s attack and 2 s release so a brief stumble doesn't drop the music. |

### 8.4 Why going fast *stays* fun

| Principle | Evidence |
|---|---|
| **Speed is kept when you chain well** | Respawn's TF2 goal: "accumulate and retain more speed when chaining wallruns" (section 1) [Doc]. |
| **Restraint makes choices meaningful** | Doom Eternal's Hugo Martin: the dash has a cooldown and doesn't recharge in mid-air, deliberately, so players make smarter choices; traversal is a skill puzzle that becomes "second nature" ([Stevivor](https://stevivor.com/features/interviews/id-software-on-the-slayer-2-0-in-doom-eternal), [GameSpot](https://www.gamespot.com/articles/why-doom-eternal-incorporates-puzzle-platforming/1100-6473287/)) [Doc, via press]. |
| **Mastery through replay** | Neon White: levels are easy to finish but reward finding faster routes on repeat ([Game Developer](https://www.gamedeveloper.com/road-to-igf-2023/-neon-white-and-designing-for-player-creativity)) [Doc]. |
| **Cheap failure keeps the flow** | Ghostrunner: one-hit death plus many checkpoints makes "each section … like a mini-puzzle" ([Inverse](https://www.inverse.com/gaming/ghostrunner-interview-cyberpunk-hotline-miami)) [Doc]. |

Rules for this game [Op]:
1. **Every move outputs speed into the next one.** Wall-jump exits point at the next surface. Slide
   ends feed into a jump. A mantle keeps 60% of the speed.
2. **Speed tiers you can feel**: 3 tiers (running, fast, flying), each adding a sound layer, the FOV
   step and speed lines. Players notice they've "levelled up" their momentum without a speedometer.
3. **Losing speed is felt, not punished**: the wind drops and a music stem fades, but nothing fails.
4. **Risk lines**: a faster line runs near the hazard (along the collapsing edge, a wallrun over the
   pit), with a safe slower line beside it.
5. **Visible reward**: in a chase, speed buys a bigger gap, which you can see and hear (the pursuer's
   sound fades), plus small dream-world payoffs (the world blooms or colours brighten at the flying
   tier).

### 8.5 Unity URP implementation notes

- **Arms on an overlay camera.** URP camera stacking: "A camera stack consists of a Base Camera and
  one or more Overlay Cameras" ([URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/cameras/camera-stacking-concepts.html)).
  Render the arms layer on an overlay camera with a **fixed FOV (~60° vertical)** so the speed FOV
  kick doesn't stretch the arms and the arms never clip into walls [Op].
- **One "speed" Volume.** A second global Volume holding Vignette, Motion Blur (Camera Only) and
  Chromatic Aberration. Drive `volume.weight = speed01 · userEffectsSlider` from script. The base
  Volume keeps the grade, and the RetroCamera pixel look stays separate.
- **Speed lines**: a camera-parented ParticleSystem with *Stretched Billboard* particles spawned in a
  ring outside the central area, flying past the camera; emission rate ∝ `speed01`. Or a full-screen
  Shader Graph material through URP's Full Screen Pass renderer feature. The particles are cheaper to
  tune.
- **Shake / dip / roll** as additive offsets on the existing `rig` transform (DoomPlayer already
  separates `head` pitch from `rig` bob), applied after the fixed-step interpolation (4.9).
  Cinemachine 3 (in the project) has Impulse, but the player doesn't use Cinemachine, so a custom
  30-line trauma shake is simpler.
- **Audio**: one AudioMixer group "Movement" with exposed parameters (wind volume, wind cutoff, music
  stems). Drive them from the movement core's output (`speed`, `state`, events) once per frame with
  smoothing. Movement sounds are 2D (spatial blend 0) and doppler is off.
- **Browser builds**: motion blur and chromatic aberration cost full-screen passes. Gate them behind a
  quality check and keep them off by default there.

**Settings to add (extends 7.1):** *Reduce effects* (one toggle: blur, CA, speed lines and shake off;
vignette kept), *Speed lines* 0-100%, *Motion blur* off/low.

### 8.6 Videos likely to contain measured Titanfall 2 numbers (to transcribe)

Found with a YouTube search via yt-dlp on 2026-10-10. Length and date come from the video metadata.
"Why" is my guess, from each video's description and chapter list. Transcribed since: #1 and #2
(section 9), #4 and #6 (8.7; #6 has nothing on numbers or body animation).

| # | Video | Channel | Length | Date | Why it likely has numbers |
|---|---|---|---|---|---|
| 1 | [Jumping off walls is really complicated, actually](https://www.youtube.com/watch?v=Vk_RaEgux2Q) | zweek (speedrunner) | 27:49 | 2026-01 | Described as "a certified spreadsheet gaming moment". Chapters "The Lab", "Anatomy of a Wallkick", "Building our Model". The most likely source of measured wall-jump and wallkick velocities. |
| 2 | [The ULTIMATE Movement Guide for Titanfall 2](https://www.youtube.com/watch?v=C_cXYyA4LaU) | bryonato | 43:44 | 2021-12 | The full reference: sliding, slidehopping, bhopping, air strafing, strafe lurch, "Rules of the Wall", wall-jump arc, wallkicks, **Coyote Time**, edge/end boost, crouch kicks. Likely states windows and speeds. |
| 3 | [The mechanic that shaped Titanfall speedruns](https://www.youtube.com/watch?v=AjPSwwnKm1o) | zweek | 19:56 | 2022-04 | Strafe lurch explained in depth, including keyboard vs controller and TAS: likely lurch strength and timing windows. |
| 4 | [Titanfall 2 – Tvcx's Ultimate Movement Guide](https://www.youtube.com/watch?v=uw-Do_JW3bI) | Tvcx | 16:05 | 2021-07 | Its first chapter is **"Different Speeds"** (0:21-1:48): likely speedometer readings for walk, sprint, slide, bhop and stim. |
| 5 | [ADVANCED Movement Tutorial for TITANFALL 2](https://www.youtube.com/watch?v=0YHiBXUTHwU) | bryonato | 13:53 | 2020-03 | Spring jump, forced-end boost, no-lurch double jump and wall jump, circle jumps: probably speed gains per technique. |
| 6 | [How Titanfall 2 Made Movement the Star of the Show – Art of the Level](https://www.youtube.com/watch?v=279q6NzJkrk) | IGN (with Respawn designer David Shaver) | 15:34 | 2022-09 | **Developer** commentary on *Into the Abyss*: design intent and teaching movement, probably not raw numbers. Best for 1 and 6 of this doc. |

Backups: [The PERFECT Titanfall 2 Movement Guide](https://www.youtube.com/watch?v=SF3SAJ4jvRY) (Ask
Heinzy, 20:25, with a "Source Thoughts" chapter); [The tech that nearly ruined Titanfall Speedruns](https://www.youtube.com/watch?v=Cwa0qbDx2dA)
(zweek, 23:44); [Iniquity's Movement Guide](https://www.youtube.com/watch?v=yvzXFQKwH8k) (6:17, 2016,
1.4 M views, mostly technique).

### 8.7 First-person body animation: evidence

Researched 2026-10-10. Labels: **[Doc]** = a developer talk, a developer quoted in an interview, or
engine source code; **[Comm]** = a player, a speedrunner, press, or **game files extracted by modders**
(real data, but not checked against the retail build); **[Obs]** = my own observation of footage. There is
**no [Obs] here**: I can't watch video, so every behaviour below comes from a spoken or written source.

**Sources** (timestamps are into the video):
- **Overwatch.** Matthew Boehm (Blizzard), *The First Person Animation of Overwatch*, GDC 2017
  ([video](https://www.youtube.com/watch?v=7t0hLZd_8Z4)). Transcribed. [Doc]
- **Destiny.** David Helsby (Bungie), *The Art of Destiny's First-Person Animation*, GDC 2015
  ([video](https://www.youtube.com/watch?v=CXFLu8cityA)). Transcribed. [Doc]
- **Dying Light.** Bartosz Kulon (Techland, lead player-mechanics programmer), *Parkour: How to Improve
  Freedom of Movement in First-Person Games*, GDC 2018 ([video](https://www.youtube.com/watch?v=uikbLyi-cug)).
  Transcribed. [Doc]
- **Titanfall 2, Respawn.** IGN's *Art of the Level* with designer David Shaver (8.6 #6). Transcribed:
  it says **nothing about body animation**.
- **Titanfall 2, runners.** bryonato (8.6 #2) and Tvcx (8.6 #4), transcribed earlier. [Comm]
- **Titanfall 2 game files.** Player settings `pilot_base.set` and `pilot_mp.set`, the R-201 weapon
  script, and `springs.txt`, from [Syampuuh/Titanfall2](https://github.com/Syampuuh/Titanfall2/tree/main/scripts).
  Decompiled view-model QC files (animation lists, blend times, state nodes) from
  [MarkMuravev/titanfall2_source2](https://github.com/MarkMuravev/titanfall2_source2/tree/main/models/weapons).
  The decompiled animation curves in that repo are broken (zeros, NaN), so **no amplitudes came from
  them**, only names, fps and blend times. [Comm]
- **Apex Legends game files.** Weapon scripts from [Mauler125/scripts_r5](https://github.com/Mauler125/scripts_r5/tree/main/weapons)
  (the R5Reloaded modding project), including the empty-handed fists. [Comm]
- **Mirror's Edge.**
  - A DICE producer in a [2008 interview](https://www.indianvideogamer.com/features/the-most-comprehensive-mirrors-edge-interview/333) [Doc, via press].
  - Senior animator Tobias Dahl in [AWN](https://www.awn.com/vfxworld/mirrors-edge-leap-faith) [Doc, via press].
  - [Engadget 2008](https://www.engadget.com/2008/07/17/how-mirrors-edge-fights-simulation-sickness/) [Comm].
  - The [Wurb review](https://www.wurb.com/stack/?p=669) [Comm].
  - The DICE/EA GDC talk *Creating First Person Movement for Mirror's Edge* (Dahl and Lagre,
    [GDC Vault](https://gdcvault.com/play/1012171/Creating-First-Person-Movement-for)) is members-only and
    not on YouTube, so it was **not seen**.
- **Mirror's Edge Catalyst.**
  - Design director Erik Odeldahl at [EA](https://www.ea.com/en-au/amp/news/the-world-from-faiths-perspective) [Doc].
  - Producer Jeremy Miller at [Uproxx](https://uproxx.com/?p=1614484) [Doc, via press].
- **Source engine code.** `CalcViewModelLag` in the
  [Source SDK 2013](https://github.com/ValveSoftware/source-sdk-2013) (`baseviewmodel_shared.cpp`).
  Titanfall runs on a modified Source engine. [Doc]
- **Momentum Mod.** Its open-source wallrun ([momentum-mod/game](https://github.com/momentum-mod/game),
  `mom_gamemovement.cpp`) is a fan re-implementation in the Titanfall style, not Respawn's code. [Comm]

#### Per move

**Wallrun catch (approaching the wall, contact)**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | "Nearly any time you approach a wall … your camera will tilt slightly and the player view model reaches a hand out to touch the wall." Runners call it the "lean-in". | **Starts before contact**: "a super telegraphed animation that lets you know almost exactly when you're going to touch the wall". Then "as soon as you hit the wall … your arm attaches to the wall like your hand … that's how you know when to jump off". Lead time not stated. | bryonato 13:38, 16:38; Tvcx 0:32 [Comm] |
| Titanfall 2 | A wallrun puts the weapon into the **one-handed** state, freeing a hand: `activitymodifier wallrun` → node `onehanded`. | Switch to one-handed: fade in **0.2 s**, fade out **0.35 s**; animations at 30 fps. | `ptpov_car101.qc` [Comm, game files] |
| Momentum Mod | Lean-in state "about to start wall running". | Traces **0.25 s** ahead (velocity plus gravity). Roll eases in by closeness `c(2−c)`, toward 14° × sin(yaw to the wall). | code [Comm] |
| Dying Light | Not a wallrun, but the same "reach" idea: the climb's "begin" state reaches hands and legs to the ledge. In 2018 Techland's own wish list included hands that "touch walls" when running near obstacles. | | Kulon 09:28, 25:27 [Doc] |

**During the wallrun**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | Camera tilted. Near the 1.75 s limit "the camera [is] gradually tilting back to a straight up and down vertical position"; runners time end-boosts by that and by the wallrun sound. | 1.75 s limit (`wallrun_timeLimit 1.75`) | bryonato 23:26 [Comm]; `pilot_mp.set` [Comm, game files] |
| Mirror's Edge | Body visible; when something can be grabbed, Faith "will reach towards it with a visible hand". | | Wurb [Comm] |

**Wall kick (jump off the wall)**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | No source describes the arms on the kick itself. The hand plant (above) is the cue players use to time it. | The kick must come within **5 ticks (83 ms)** of contact (section 9). | bryonato 16:12 [Comm] |
| Mirror's Edge | A kick from a wallrun "inherits your speed and momentum from the wallrun". | | [critpoints](https://critpoints.net/2020/10/31/mirrors-edge-review/) [Comm] |

**Slide**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | "When your gun is tilted to the side it means you're going to slide." It shows **already in the air** when crouch is held. | | Tvcx 2:07 [Comm] |
| Titanfall 2 | "The camera will do a sudden dip as you enter the slide animation." | | bryonato 38:10 [Comm] |
| Titanfall 2 | FOV widens while sliding. The view model's sliding state is the `land_crouch` node. | `slideFOVScale 1.1`, lerp in **0.25 s**, out **0.25 s**. Crouch view height 38 u vs 60 u standing. | `pilot_mp.set`, `pilot_base.set`, `ptpov_car101.qc` [Comm, game files] |
| Titanfall 2 | Legs: **no source** says they show, and the files list arms-only view models. | | unknown |
| Mirror's Edge | Legs and torso visible. Crouch in mid-air "coil[s] up, lifting your legs". | | Wurb, critpoints [Comm] |

**Jump and double jump**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | Dedicated view-model transitions `switch_to_jump` and `switch_to_land`, with crouch and aim variants. Their motion is unknown (the decompiled curves are broken). | 30 fps | `ptpov_car101.qc` [Comm, game files] |
| Titanfall 2 | The procedural sway trails **vertical velocity**: moving up sets the view model's z to −1 u, moving down to +1 u, clamped at about ±0.6 u. So the gun **sinks while rising and floats while falling**; it does not lift on the jump. | ≈ **1.5 cm** maximum | R-201 `sway_move_up/down_translate_z`, `sway_max_z` [Comm, game files] |
| Titanfall 2 | Double jump: a jump-kit thrust (sound, rumble `rumbleOnDoubleJump`). No source describes the arms. | | unknown |
| Apex Legends | The double jump has its own view-model overlay activity `ACT_VM_DOUBLEJUMP`. | | [r5sdk `shared_activity.h`](https://github.com/Mauler125/r5sdk) [Comm] |
| Destiny | Jump and land **overlay animations** on top of the base pose, driven by the third-person body's data. Without them "it feels like you're just kind of a floating camera". | | Helsby 07:00, 13:00 [Doc] |

**Mantle**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | Hand-authored first-person mantles for **4 ledge heights** (below, level, above, high-above), each with a crouch version. A "mantle idle" blends between ledge-angle-left, straight and ledge-angle-right poses. There is a separate mantle jump-off. | Fade in/out **0.2 s**, 30 fps | `pov_pete_core.qc` [Comm, game files] |
| Dying Light | A "begin" state where the player "is reaching his hands and legs to grab the ledge". Its length is **scaled to the speed before the climb** ("to avoid changing speed as much as possible"), and the view is blended toward the ledge's forward. The second hand is placed by **IK at the ledge edge** found in screen space. They loosened the animations to get "like 40 centimeters of freedom". | ±40 cm IK range | Kulon 09:28-09:55, 14:41-15:03 [Doc] |
| Dying Light | A climb as a cinematic "looked okay, but played horrible". Fixes: a shorter sequence, the player can walk while the camera is still rising, and the head stays free to look around. | | Kulon 11:11-12:12 [Doc] |
| Mirror's Edge Catalyst | The first game's "fairly long non-interruptable animations" gave too little control, so Catalyst moved to a branching system that can be cut into. | | Miller, Uproxx [Doc, via press] |

**Landing**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | The view kick is scaled by fall distance. The view punch is a spring. | `viewkickFallDistMin 10`, `Max 70`, `MaxScale 12` (units not stated). `punch_pilot`: spring constant 65, damping 9 → about **1.07 Hz**, half-life **0.15 s**, damping ratio ≈ 0.56 (formulas are in the file's own comments). Whether the landing uses this spring is my inference. | `pilot_base.set`, `springs.txt` [Comm, game files] |
| Destiny | A land overlay animation (see Jump). | | Helsby 13:00 [Doc] |
| Mirror's Edge | "If you're falling from a height and you expect the screen to move or bob and it does, that's good and the brain is happy." The roll after a big drop snaps the view back to the same heading. | | DICE producer, 2008 [Doc, via press] |

**Sprint**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 | An authored sprint loop with the weapon lowered, and a small view roll. | Cycle `sprintcycle_time` **0.55 s** (two steps). Footsteps every **275 ms** sprinting, 400 ms running. `sprinttiltMaxRoll` **2°**. `sprintViewOffset −6`. Sprint start delay 0.2 s, start blend 0.8 s (0.2 s fast), end blend **0.15 s**. Walk bob: 0.4 s cycle, 0.19 u (0.5 cm) vertical, 0.1 u (0.25 cm) sideways. | `pilot_base.set`, R-201 script [Comm, game files] |
| Titanfall 2 | A looping "wind" layer that moves only the camera and view root (`weights_wind_loop_layer`). It is probably a speed buffet; its trigger is not known. | 30 fps loop | `ptpov_car101.qc` [Comm, game files] |
| Apex Legends (fists) | Empty-handed bob: 0.45 s cycle, 0.075 u (0.19 cm) vertical, 0.05 u sideways. | | `mp_weapon_melee_survival.txt` [Comm, game files] |
| Overwatch | Run cycles are authored per hero, with additive "run leans" when running sideways or forward and back. Camera motion is animated from the root, plus "little extra" on a camera bone, "nothing too heavy". | | Boehm 06:36, 22:26 [Doc] |
| Mirror's Edge | Head bob from the 2008 GDC build was **removed**. The view was moved to "your eyes and not your head". | | Engadget 2008 [Comm] |

**Turning (look sway)**

| Game | What the body does | Timing / numbers | Source |
|---|---|---|---|
| Titanfall 2 (R-201, hip fire) | The view model counter-rotates and rolls on look input. | Turn: yaw ∓2.5°, **roll ±4°**, pitch ±3° (looking up also rolls −0.8°). Clamps: pitch ±3°, yaw ±2.5°, roll ±4°; translation ±0.5 u (1.3 cm), z ±0.6 u (1.5 cm). Strafing: roll ∓4°, sideways ±1 u. Gains: translation 2.5, rotation 7 (probably a follow rate per second; not documented). | R-201 script [Comm, game files] |
| Apex Legends (fists) | Snappier and larger. | Clamps: yaw ±3.5°, pitch ±3°, roll −2°/+3°. Gains: translation **10**, rotation **12**. The empty-hand "gloves" add `sway_turn_angle_factor −0.5` and `sway_gain 15`. | Apex weapon scripts [Comm, game files] |
| Source engine | The view model lags the view direction. | Follows at **5 /s** (time constant 0.2 s). Offset = 5 u × the change in the forward vector, about 2.2 cm for a 10° lag. Catch-up speeds up past a difference of 1.5. | `CalcViewModelLag` [Doc] |
| Overwatch | "Spring-based" additive aim and roll poses with spring values **tuned per hero for character** (tight for McCree, loose for Roadhog, "flows and snaps" for Zenyatta). There are also look-up/down overlays: look up and Winston's arm leaves the screen; look down and you see more of it. | | Boehm 19:55-22:12, 24:03 [Doc] |

#### Cross-cutting craft (all moves)

- **The arms' FOV is fixed and separate from the world FOV.**
  - Overwatch separates them so players' FOV sliders don't stretch the poses (Boehm 17:09).
  - Destiny: world 72°, arms **77°**, "locked from the beginning of the project until the end" (Helsby 42:04).
  - TF2's files have `fov 70`, `viewmodelfov 70`.
  - This confirms 8.5. [Doc]
- **Immediate feedback.** "The minute that button gets pushed, you want to start seeing motion on the
  screen" (Destiny pillar 1). Other pillars: preserve motion between actions, and "no speed bumps"
  (Helsby 03:24-05:21). [Doc]
- **Translation, not rotation, for camera accents.** Rotation moves the far world and the horizon a lot,
  "especially camera roll". Translation is large up close and fades with distance (Helsby 28:53-30:00).
  Overwatch adds translation "without rotating too far … you get that seasickness in … the roll"
  (Boehm 24:35). [Doc]
- **The head leads.** Destiny's camera leads the hit "maybe just by one frame, but it really makes a
  difference" (Helsby 32:46). [Doc]
- **Hold key poses so they read.** Hold a limb on screen "seven to ten frames", because "in first person,
  really the only information that you have about your character … is the visual information"
  (Helsby 17:56-18:52). [Doc]
- **Keep the combat corridor and the edges clear.**
  - Don't pose actions in the peripheral band (sides and top) or across the central strip
    (Helsby 14:59-16:24).
  - Overwatch's Winston gives "hints" of a limb "without flashing in your peripheral vision" (Boehm 23:47).
  - TF2's wall hand breaks this on purpose, as a gameplay cue. [Doc]
- **Depth.** Elements far from the camera look small and "weaker"; too close feels "claustrophobic"
  (Helsby 57:24). [Doc]
- **Snap and stretch on contact frames.** On Overwatch's fire and melee contact frames the arm is "really
  stretch[ed] … and snap[ped] back". Camera Z translation gives impact, but watch the collision: McCree's
  face went through walls in the alpha (Boehm 26:48, 31:57). [Doc]
- **Clipping.** Dying Light's hands went into geometry; they "scaled down the hands … with camera
  projection" (Kulon 14:04). URP's overlay camera does the same job. [Doc]
- **Animate perception, not the body.** "The key to succeeding with first-person animation is to animate
  what you perceive … what your eyes do, not your head and body" (Dahl, AWN). A head-mounted GoPro is
  not how we see: our eyes stabilise the image (Helsby 25:01-26:14). [Doc]

#### What players and developers report (good and bad)

- **No body feels like a floating camera.**
  - Destiny without first-person animation: "you're just kind of a floating camera" (Helsby 07:00) [Doc].
  - Dying Light's Maciej Binkowski: without limbs "you'd just feel like your character was a floating
    camera" ([MCV](https://www.mcvuk.com/development-news/the-vaulting-dead-implementing-first-person-parkour-in-dying-light/)) [Doc, via press].
  - The Mirror's Edge producer found a "static floating camera" in another game "so jarring because I
    didn't get the feel of the feet and the body" [Doc, via press].
- **Seeing the limbs helps.**
  - A preview called Faith's arms and legs "the tools that enable her to accomplish her goals" [Comm].
  - A Catalyst player: "If you grab onto a ledge, you'll see her hands … I found it made the game vastly
    more immersive" ([review](https://www.goodreads.com/author_blog_posts/14878645-review-mirror-s-edge-catalyst)) [Comm].
  - Odeldahl: "In real life, we are so used to using our arms and legs without looking at them, but in a
    game that just doesn't work" [Doc].
- **Camera motion causes nausea.**
  - Mirror's Edge: a shoulder-driven head plane "made me feel sick in about 15 seconds". Fixes: a reticle
    steadied by a distant focal point, a wide FOV, and the rule that whatever the player expects must
    happen [Doc, via press].
  - Dying Light: a Polygon reporter left the booth sick. Testing found "frequent and often camera target
    movement", and the "camera moving backward when player body was moving forward" (a small backward
    bump in fast forward moves, removed). A centre dot and a longer distance from the screen also helped
    (Kulon 19:01-21:54) [Doc].
  - Apex Legends at launch: players reported nausea from head bobbing. Respawn asked whether it was worse
    "Sprinting? When weapon is drawn? When weapon is holstered?" ([devtracker](https://devtrackers.gg/apex-legends/p/f7a09eaf-abandoning-the-game-due-to-head-bobbing-head-aches-nausea-persisting-for-hours)) [Comm + Doc].
  - Destiny: "even if your camera motion is going to make only 10% of the population sick … you've just
    lost 100,000 people" (Helsby 24:41) [Doc].
- **Settings.** Xbox Accessibility Guideline 117: "Avoid or allow disabling camera shake, camera bobbing,
  motion blur", and avoid repetitive on-screen motion "(weapon sway, camera bobbing)"
  ([XAG 117](https://devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/117)) [Doc].
- **Control.** Losing control during an animation "feels really bad" (Kulon 11:24) [Doc]. Catalyst made
  its moves interruptible [Doc, via press].

#### Recommendations for our body rig (each tagged with its evidence)

1. **Wall hand: anticipate, then plant on contact.** Predict wall contact about 0.25 s ahead (velocity
   plus gravity). Start the reach and the camera roll then, eased by closeness. Plant the hand on the
   contact frame; don't snap it there after contact.
   - Evidence: TF2 lean-in "telegraphed" before contact, with the hand attached at contact (bryonato,
     Tvcx); Momentum Mod 0.25 s look-ahead; Dying Light's "begin" reach.
   - The plant is the player's **wall-kick timing cue** (83 ms window), so it must be exact.
   - Blend the wall arm out of its run pose in about 0.2 s and back in about 0.35 s (TF2's one-handed
     switch).
2. **Slide: cant the arms, dip the camera, FOV ×1.1 over 0.25 s.** Show the cant as soon as crouch is
   held in the air. Legs in view are optional. (TF2: Tvcx, bryonato, `slideFOVScale`; Mirror's Edge legs.)
3. **Vertical inertia instead of a fixed jump lift.** Offset the arms against vertical velocity: down
   while rising, up while falling, clamped at about 1.5 cm. Optionally add a short jump overlay on the
   input frame (TF2 sway; Destiny overlays; Destiny pillar 1).
4. **Hold the double-jump pose about 0.25-0.3 s**, not a 0.15 s sweep, so it reads (Destiny's 7-10
   frames). The shape of the pose is still [Op]; Apex gives the double jump its own overlay.
5. **Mantle: per-height poses plus hand IK on the lip.** Allow about ±40 cm of IK correction. Scale the
   reach time to approach speed. Blend the view toward the ledge but keep mouse look free. Let the player
   move before the climb ends. (TF2's 4 heights with 0.2 s fades; Dying Light; Catalyst.)
6. **Look sway: rotation clamps of about ±3° yaw/pitch and ±4° roll, translation ≤ 1.5 cm.** Use a fast
   rotation follow (about 7-12 /s) and a slower translation follow (2.5-10 /s). Roll the arms ±4° when
   strafing. Use Overwatch's per-character tuning to make it feel like *her*. (TF2 and Apex scripts;
   Source lag 5 /s; Overwatch.)
7. **Camera accents by translation, not roll.** Wallrun roll stays ≤ 8° (4.8). Sprint roll ≤ 2° (TF2
   `sprinttiltMaxRoll`). Never move the camera backward while the body moves forward. (Destiny; Overwatch;
   Dying Light; Mirror's Edge.)
8. **Landing: camera first, arms about one frame later, scaled by fall height.** For a spring, start near
   TF2's view-punch spring (about 1.1 Hz, half-life 0.15 s, damping ratio 0.56). (TF2 files; Destiny's
   "head leads by one frame".)
9. **Arm FOV fixed** (Destiny 77° vs world 72°; Overwatch; TF2), drawn by the overlay camera so the hands
   never clip (Dying Light).
10. **Sprint: keep camera bob tiny.** Put the motion in the arms, out of the top and side bands. TF2's
   sprint cycle is 0.55 s per two steps at 6.6 m/s, so tie our pump to our own step interval. (Apex
   nausea reports; XAG 117; Destiny's peripheral bands; TF2 files.) The 4 cm and 1.5 cm amplitudes remain
   [Op]: no game publishes its sprint-arm amplitude.

#### Still unknown

- How far ahead of contact TF2's wall hand starts, its exact pose, and which hand reaches when the wall
  is on the weapon side.
- What TF2's arms do on the wall kick, the double jump, and landing: the dedicated animations exist, but
  their decompiled curves are unusable.
- Whether TF2 shows legs at any time.
- Units and use of TF2's `viewkickFallDist*`, `sprintViewOffset` and sway "gain" values.
- Doom (2016) and Eternal, and Ghostrunner: **no developer material on first-person arm animation was
  found**. Ghostrunner has only a player guide (FOV around 90, motion blur off; see 8.2).
- The Mirror's Edge GDC talk and *Giving Purpose to First-Person Animation* (Ryan Duffin, GDC 2013) are
  members-only on GDC Vault.
- Cross-check, outside body animation: TF2's `pilot_mp.set` has `wallrun_timeLimit 1.75` and
  `wallrunJumpOutwardSpeed 205`, matching section 9's measurements. It also has
  `wallrunJumpInputDirSpeed 80` (measured: 75) and `wallrunMaxSpeedHorizontal 420` (measured goal: 340).
  The first two confirm section 9. The last two differ, and I can't tell which of the file's numbers the
  measured 340 u/s goal comes from.

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
- Section 8: Titanfall 2's wallrun hand and slide cues are now sourced, from runners' guides and the
  game's files (8.7). No developer statement exists, and the decompiled animation curves are unusable,
  so their amplitudes and timings are still unknown (see "Still unknown" in 8.7). Its movement audio
  beyond the slide Foley is still unsourced. Most feel parameters in 8.2-8.4 are my own starting points;
  the reception notes are player anecdotes, except the FOV-restriction study and the developer talks
  in 8.7.
- The 8.6 videos are chosen from titles, chapters and descriptions only. Whether they state numbers is a guess until they are transcribed.

## 9. Measured numbers from the community (transcribed 2026-10-10)
Sources, transcribed locally with Whisper:
- zweek, *Jumping off walls is really complicated, actually* (https://www.youtube.com/watch?v=Vk_RaEgux2Q). He
  measured the game with scripts that call the game's own `getVelocity` and `getOrigin` functions.
- bryonato and zweek, *The ULTIMATE Movement Guide for Titanfall 2* (https://www.youtube.com/watch?v=C_cXYyA4LaU).

Units are Source units, converted at 1 u = 1 inch = 0.0254 m. The two sources cross-check: zweek's 340 u/s wallrun
goal works out to 31 km/h against bryonato's "around 28 to 30 km/h", and his +123 u/s best wall kick works out to
11 km/h against bryonato's "around 10 to 11 km/h". That agreement supports the conversion.

| Mechanic | Measured | Metric |
|---|---|---|
| Simulation rate | 60 ticks/s | |
| Wallrun duration | 1.75 s maximum; **forward input is required** | |
| Wallrun speed | Wall friction or acceleration moves you towards a **goal of 340 u/s**, from above or below | 8.64 m/s (31 km/h) |
| Wallrun input rules | Forward: approach the goal speed and hold it. Away from the wall: the wallrun ends at once. Into the wall or neutral: friction to 0. Backwards: faster friction, then you accelerate the other way | |
| Wall-jump impulse, along the normal | **205 u/s, always** | 5.21 m/s |
| Wall-jump impulse, forward along the wall | up to **75 u/s**, sine of the input angle; the input is projected flat onto the wall | 1.91 m/s |
| Wall-jump impulse, inward | up to **172 u/s** × (wishdir · −normal), only when the input points into the wall, otherwise 0 | 4.37 m/s |
| Best wall-kick speed gain | +218 u/s from a standstill; **+123 u/s at 340 u/s**; tends to +75 u/s as speed grows (the normal part becomes perpendicular to velocity) | +3.1 m/s at wallrun speed |
| Wall-kick timing | must happen **within 5 ticks** (83 ms) of contact; earlier jumps lose less to wall friction | |
| Crouch kick | an extra **70 u/s** along the wall normal (about +3 km/h; up to about +13 km/h in total) | 1.78 m/s |
| Re-grabbing the same wall | only at a **lower** contact point than the jump, or on a wall curving **25° or more** | |
| Slide boost | needs speed above a threshold (not stated); boosts up to a cap that **decays the longer you slide**; **~2 s cooldown** before the next boost | |
| Slide hops | jump on every landing with crouch held; each landing loses more speed the longer you've slid | |
| Wall contact feel | "lean-in": the camera tilts and **the view model reaches a hand out to touch the wall**. Players time wall kicks by that hand | |
| Inputs | jump and crouch must be held for at least 1 tick to register | |

How to use these in our controller:
- Use **8.6 m/s as the wallrun goal speed** and **1.75 s as the maximum duration**.
- The wall jump is **5.2 m/s along the normal, plus up to 1.9 m/s along the wall, plus up to 4.4 m/s inward**.
- Give the wall kick a 5-tick (83 ms) speed-preserving window.
- Treat these as Titanfall's values and scale to taste: our dream is a chase, not a speedrun.
