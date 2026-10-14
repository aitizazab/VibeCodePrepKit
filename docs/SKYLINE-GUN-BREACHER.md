# The Breacher: identity sheet

Owner: the Breacher's identity agent (Skyline Repo M1, Task 19). Spec: `docs/SKYLINE-DESIGN.md` 2.2 and 2.3. Data:
`SkylineGuns.Breacher()` and `BreacherBoostDef` (`Packages/com.vibejam.jamkit/Runtime/Skyline/`). Everything here is a
draft for the creator's ears and eyes: the creator plays every gun before it is final (2.3).

## The fantasy

A door-breach shotgun from the repo firm's kit: a stubby hazard-orange brick with a toothed breaching muzzle, built to
put a door through the room behind it. It doesn't pierce, it shoves. Every shot is a slab of pressure: bots slide back
and their plates come off, the camera jolts, the mall answers with a long dark roar. And because the push goes both
ways, the Breacher is also your best climbing tool: fired at the floor in mid-air it throws you upward, like a
rocket jump in TF2 (the soldier fires a rocket at his own feet and rides the blast) or Destiny 2's Micro-Missile
grenade launchers. Between shots its yellow pressure cell vents with a hiss and its window glows with what charge is
left.

## The verbs

- **Shove:** a hit pushes a bot back (15 m/s shared across the pellets that land), tipping and spinning it; a shed plate flies off along the shot.
- **Breach:** close range only; nine pellets in a 5° cone, full damage to 6 m, a quarter of it by 18 m, nothing past 30 m.
- **Boost:** fired downward in mid-air, the blast lifts you (see the rules below). Fired flat in mid-air, it shoves you
  backward instead.
- **Brace:** fired on the ground, crouched, or wall-running, the blast is eaten by the surface (a small backward nudge,
  no lift).

## The boost's rules in plain words

These are the creator's calls (2026-10-13), already in the code; nothing here changed them.

1. Only a shot fired **in mid-air** lifts you. On the ground (standing or crouched) or wall-running, the floor or the
   wall takes the blast: you keep 20% of the backward push and never rise (`bracedScale` 0.2).
2. The shot must point **downward** to lift: more than a little past level (the reversed aim's upward part above
   `minLift` 0.35, about 20° below the horizon). Straight down gives the full lift.
3. A straight-down shot lifts you about **1 m** (`boostHeight`), the jump upgrade's height. It **sets** your rise speed
   rather than adding to it, and it cancels a fall first, so chained shots climb in steps instead of rocketing.
4. There is **no limit per airtime**: the fire rate (one shot every 0.75 s), the 4-shell cell and the reload are the
   limits. (The old one-boost-per-airtime rule is still a data switch, `oneBoostPerAirtime`, off.)
5. **No self-damage** (`selfDamage` 0), and a boost **doesn't give back your double jump**.
6. The flat part of the push is 6 m/s backward (`selfPushSpeed`), capped by the engine's top speed (22 m/s).
7. **Jump and fire on the same frame:** the shot comes first (braced, so mostly eaten), then a normal ground jump, so
   your double jump is kept.

## Every number and why

| Number | Value | Why |
| --- | --- | --- |
| Fire rate | 80 RPM (0.75 s) | Leaves the blast's half-second body and the vent room to breathe (2.3); also the boost's real limit. |
| Cell (magazine) | 4 shells | Four boosts or four shoves, then a reload: a climbing route has to plan its cell. |
| Pellets x damage | 9 x 9 | 81 at point blank: a plate and a chunk of chassis. Under the Writ's precision per shot fired at range. |
| Spread | 5° cone | Tight enough that 6 m is a sure hit, loose enough that 15 m isn't. |
| Falloff | full to 6 m, 25% by 18 m, range 30 m | A close-range gun; at range you'd rather swap to the Writ (2.1's designed swap moment). |
| Weak point | x1.25 | A shotgun doesn't reward precision much; the Writ owns weak points. |
| Bot knockback | 15 m/s per shot (was 9) | The identity: a bot slides about a metre at 6.5 m, about 3 m at point blank. It is shared by the pellets that land (1.7 m/s each), and SkylineShoveLurch tips and spins the bot's body with it (judge round 2, N3). |
| Hit stop | 0.05 s (kill 0.1 s) | Weight on contact without stalling the next shot. |
| Camera kick | 8° up, 2° side, recovers at rate 7 | **Changed by Task 19** (was 6.5° / 1.5° / 6): "the camera kicks hard". At rate 7 the kick is back to 0.5% before the next shot. |
| Shake | 0.3 trauma per shot | **Changed by Task 19** (was 0.22): the blast should rattle the view: five times the Writ's 0.06 per shot, and 0.4 a second at full rate against the Writ's 0.25. |
| Recoil pose | gun back 17 cm, up 5 cm, 22° muzzle rise at 7% of the fire clip | **Changed by Task 19** (was 14 cm, 4 cm, 18° at 8%): a harder, earlier jolt to match the sound's punch. |
| Aim assist (pad) | slow 0.6 inside 8°, pull 12°/s inside 5°, 20 m | Wide and gentle: a shotgun's cone does the rest. |
| Equip | 0.7 s: grab at 15%, lock at 75% | A heavy gun takes a moment; the lock is the cell arming. |
| Fire clip | 0.6 s: blast at 0, vent at 40% (0.24 s) | The vent lands after the blast's held body starts to fall. |
| Dry fire | 0.3 s | A dead clunk and a fizzle, then the from-empty reload starts. |
| Reload (partial) | 1.6 s: cell out 15%, cell in 50%, seat 66% | A cell swap. The gun lifts 7 cm up and in, nose up 10°, rolled 55° clockwise (as the player sees it), so the cell under the receiver faces the camera; it drops 16 cm toward the middle of the view and the robot arms' left hand brings the new one (fix round 1, judge C4). |
| Reload (from empty) | 2.1 s: cell out 12%, cell in 42%, seat 56%, pump 82% | The swap plus a pump to chamber. |
| Inspect | 2.6 s: turn 10%, knock 50%, settle 85% | A knuckle on the cell: the gun's personality beat. |
| Boost | 6 m/s flat, 1 m lift, min lift 0.35, braced 20% | The creator's calls (above); unchanged. |

## The sound palette (per event)

Every sound is synthesized: `tools/skyline/guns/breacher/synth.py` makes the source layers (numpy: sines, noise,
filters, saturation, struck-metal modes) into `samples/`, and writes the forge recipes that layer them
(`recipes/*.json`); `render_all.py --gun breacher` renders them into `Resources/SkylineAudio/breacher/`. No reference
clip is read by any of it.

- **blast** (the shot, 2.5 s): eight layers.
  1. *Crack*: a 45 ms burst of 2.5-14 kHz noise, the charge going off.
  2. *Air*: a 0.2 s mid-band rush (350 Hz-4 kHz), the blast of gas out of the barrel.
  3. *Punch*: a sine falling 170 to 72 Hz, the chest hit.
  4. *Boom*: the identity layer, a saturated low tone falling 104 to 66 Hz (with a fifth above it and a rumble riding
     it) that **holds near full level for about 0.45 s** before it falls, the way the Fourth Horseman's body holds.
     High-passed at 55 Hz so the weight sits in 60-250 Hz.
  5. *Roar*: pressed-flat broadband pressure noise (250 Hz-6 kHz) held with the boom, so the blast has mids and bite
     instead of only weight.
  6. *Debris*: 34 grit ticks in the first 0.35 s, then a few fragments landing up to 1.3 s.
  7. and 8. *The mall*: two dark room tails (60-650 Hz, about 2.4 s to -60 dB), panned left and right for width, plus
     (fix round 1) a long low hang (70-400 Hz) that holds the tail at about -40 dB to 2 s as the Fourth Horseman's does,
     with a
     soft slap-back off a far wall at 0.66 s.
- **vent_hiss** (0.65 s): the cell venting. A valve pop, a fluttering pressurised hiss (2.5-9.5 kHz), a short low
  hiss, and a falling whistle.
- **equip_rack**: a grip tick and the gun's weight caught in the hand (a mid rustle), with the pump's loose rattle.
- **equip_lock**: a latch, the receiver's body knock, a sling settle, and a short rising whine as the cell arms.
- **dry_clunk**: a dull hammer clunk with no ring, and a tired fizzle where the blast should be.
- **cell_out**: a latch tick with a steel ring, a pressure release (pssht), the cell scraping out of its rails, and a
  soft knock as it leaves.
- **cell_in**: the cell bottoming in its rails (a clack with high steel modes), then a ratchet and a scrape as it
  settles, then a small second tick: the Witchfire shell inserts' tick-scrape-thump pattern, as one heavier part. The
  clack comes first so the sync check finds the event's moment.
- **seat**: the lock's hard clack, a low hydraulic thump, and the hiss of the cell charging the chamber, cut off by a
  valve shutting.
- **pump** (from empty): two strokes, back and then a harder forward slam, both with steel ring.
- **inspect_turn**: a slow creak of the grip and a soft slide of the pump.
- **inspect_knock**: a knuckle on the cell, twice: a hollow gas-cylinder tonk (410 Hz).
- **inspect_settle**: a light clack as it comes back to rest.
- **impact** (`breacher/impact`, played by `SkylineBreacherFx` once per shot that lands on a bot, at the hit): nine hard
  ticks spread over 25 ms (the pellets) on top of a deep body slam (150 to 55 Hz): the knockback heard.
- **boost** (`breacher/boost`, played on a boost): the blast's pressure caught under your feet (a low whump, 112 to
  58 Hz) and the rush of air as you rise (noise whose low-pass opens from 500 Hz to 4.5 kHz over 0.4 s). It plays on
  top of the blast; quieter (0.6) for a flat air shot that shoves rather than lifts.

The generic impacts (Task 19's ruling; every gun's pellets play them through `SkylineShooting`):
- **impact/hit**: a bullet on a bot's plating: a 2.5 ms tick and a short steel ring (2.4, 3.9, 5.6, 7.4 kHz).
- **impact/world**: a strike on the mall: a dull short crack, a low knock and a little falling grit; no ring.
  Recipes in `tools/skyline/impact/recipes/`, rendered by `tools/skyline/impact/render.py`.

## The visual language

- **Silhouette:** a short, fat brick. Hazard-orange receiver with a squared brace at the back; a dark steel top shroud
  with four vent slots; a short steel barrel ending in a toothed **breaching standoff** ringed with yellow-and-black
  hazard bands; a ribbed dark pump; a raked dark grip. Hazard-striped yellow plates on both sides of the receiver (the
  firm's orange with hazard stripes, the repo kit's shared language).
- **The cell** (`Mag`): a fat yellow cylinder slung under the receiver, with black hazard bands, steel end caps, a vent
  nozzle on its front right, and an amber **charge window** on each side.
- **Model:** `Resources/SkylineModels/breacher/breacher.fbx`, built from code by
  `tools/skyline/guns/breacher/model/build_breacher.py` (Blender, background mode). 1,816 vertices. Children: `Mag`
  (the cell), `Muzzle` (an empty at the standoff's mouth), `CellVent` (an empty at the vent nozzle), `Receiver`
  (everything else). Same size as the old grey-box gun, so the rest pose and the arms still fit.
- **Arms:** the primitive robot arms (graphite forearms with a pale wrist band). The shared robot arms are Task 18's
  and Task 18 is not running, so the Breacher uses the viewmodel's primitive arms for now.
- **VFX** (`Fx/Breacher/SkylineBreacherFx.cs`, added to every SkylinePlayer when a Skyline scene loads):
  - *Muzzle blast* (viewmodel): a hot orange fireball with two smaller balls down the barrel's line and a star of five
    flame petals, for 0.065 s (about four frames), then a thin wisp of smoke off the muzzle (alpha 0.3, half a second).
    All the smoke, dust, vapour, flame and pressure rings are soft particles (fix round 1, judge C blocker): camera-facing
    quads with a generated soft texture, drawn unlit and alpha-blended with the always-included Sprites/Default shader,
    fading as they grow. Nothing is a solid grey ball any more.
  - *Flash*: a warm point light at the muzzle for 0.07 s, so the blast lights the room.
  - *Pressure ring and blast cloud*: a soft warm ring centred on the muzzle (low right of the view), 0.7 m out along
    the aim, growing to 0.55 m and gone in 0.2 s, plus four puffs of blast smoke thrown about a metre out of the muzzle.
    It reads as the blast leaving the barrel, not a reticle over the target (judge C8).
  - *Pellet hits on a bot*: three dark chips and flakes of its white-and-gold casing and a dull grey dust puff at every
    pellet that lands. No glow and no sparks: on a live bot, sparks and glow mean a Writ spot (the spec's damage-feedback
    rule; the controller's ruling, 2026-10-14, over the brief's "pellet sparks"). Where the centre of the spread lands
    on the world within 4 m: a dust kick (six chunks and a puff). No sparks on the world either, since the effect can't
    tell metal from tile.
  - *Plates fly*: a plate the Breacher sheds is kicked along the shot (7 m/s plus 2 m/s up, with spin) on top of the
    bots' own throw.
  - *Boost*: a blast under your feet. If the spread hits a floor within 6 m: a glowing ring racing out over the floor to
    2.6 m, eighteen dust chunks skidding outward, ten soft dust clouds rolling up to about a metre, and a flash of light
    on the floor. With no floor below: two soft pale rings and ten puffs of vapour bursting out under you.
  - *The cell's vent*: at the "vent" event, five puffs of vapour jet sideways out of the vent port.
  - *The cell's window*: amber when full, dark when spent (it shows the shells left), dims for a moment when a shot
    draws on it, and flashes white-hot when a fresh cell seats.

## The upgrade path (`UpgradePaths.Breacher()`)

1. **Quick Cell** (numbers, built): reloads 25% faster. The reload's sound events and animation rescale with it (the
   forge timelines are fractions of the clip), so the cell-out, cell-in, seat and pump keep their sync. Look and sound:
   the same parts, tighter; the seat's charging hiss is shorter because the clip is. Nothing new to make.
2. **Eviction Notice** (augment, not built): a bot knocked into a wall or another bot takes impact damage, and so does
   the bot it hits. Look and sound to make when it is built: a crunch where the bot meets the wall (a dull metal
   "clang-thud" with a little grit, `breacher/eviction`), a dust puff and a crack decal at the contact point, and the
   bot's hit flash on both bots. The shove becomes a weapon.
3. **Forced Entry** (capstone, not built): a boost's blast knocks back and damages bots within 4 m of where you fired
   it. Look and sound when built: the boost's floor ring turns hot orange and grows to the 4 m damage radius (so you can
   read what it reached), the dust chunks become sparks, and the boost gains a heavier layer (the blast's boom under the
   whump). The movement tool becomes an attack.

## The reference comparison

Measured with the ears (`listen.py compare`, the creator's picks, `docs/SKYLINE-REFS.md`); the acceptance rule is Task
18's: true peak at most -1 dBTP, the shot's attack at most max(5 ms, 1.5x the reference's), crest within 4 dB, tail
within 35%, centroid within 30%, every band share (sub/low/mid/presence/air) within 0.12.

| Clip | Reference | Peak (dBTP) | Crest (dB) | Attack (ms) | Tail (ms) | Centroid (Hz) | Bands sub/low/mid/pres/air | Result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Fire (blast + vent) | Fourth Horseman single shot | -1.4 (ref -4.0) | 20.3 (17.3, +3.0) | 4 (109) | 2434 (2369, +3%) | 642 (570, +13%) | .02/.73/.16/.08/.02 (.02/.76/.15/.05/.02, max 0.03) | all in |
| blast alone | Fourth Horseman single shot | -1.4 | 20.4 (+3.1) | 4 | 2434 (+3%) | 492 (-14%) | .02/.76/.16/.05/.01 (max 0.01) | all in |
| ReloadPartial | Witchfire Echo reload | -3.0 (-9.3) | 26.1 (25.3, +0.8) | 1 (199) | 331 (2798, -88%) | 3866 (4771, -19%) | .00/.13/.24/.51/.13 (.03/.07/.25/.42/.23, max 0.10) | in, except the tail |
| ReloadEmpty | Witchfire Echo reload | -3.0 | 26.1 (+0.8) | 1 | 752 (-73%) | 3968 (-17%) | .00/.14/.28/.44/.15 (max 0.09) | in, except the tail |
| Equip | Witchfire Echo equip (clean) | -6.0 (-14.9) | 20.8 (19.4, +1.4) | 19 (258) | 694 (291, +138%) | 1261 (1548, -19%) | .00/.07/.78/.14/.01 (.02/.09/.73/.12/.04, max 0.05) | in, except the tail |

Out on purpose:
- **The reload tails:** the reference is three seconds of five shell inserts in a row over a game ambience bed (it never
  falls below -30 dB), so its "tail" is the whole run. Ours is one cell swap that ends; matching it would mean padding
  the reload with noise.
- **The equip tail:** the reference is one 0.56 s sound; the Breacher's equip is two events 0.42 s apart (the grab and
  the lock, with the cell's arming whine), so its tail runs past the second one.
- **The blast is louder than the reference** (-1.4 against -4.0 dBTP): the forge masters every shot to just under
  -1 dBTP; the level in the game is the mixer's call.
- **Single parts against single shell inserts** don't compare: the ears' spectrum weights the middle of a clip most (a
  Hann window over the whole file), so a lone 0.3 s part is measured mostly by its tail. `cell_in` alone measures
  11.2 kHz against the inserts' 4.5 kHz for that reason; inside the reload clips, where it sits mid-clip, the
  comparison above is the meaningful one.

Reload sync (`listen.py events` on the rendered clips; every event found at its planned time, to the millisecond):
- ReloadPartial: planned 0.240, 0.800, 1.056 s; found 0.240, 0.800, 1.056 (extra onsets at 0.309-0.437 are the cell
  scraping out, and 0.914 the cell settling, inside their own events).
- ReloadEmpty: planned 0.252, 0.882, 1.176, 1.722 s; found 0.252, 0.882, 1.176, 1.722 (plus the pump's second
  stroke at 1.827).
- Equip 0.105 and 0.525 (planned 0.105, 0.525); Inspect 0.260, 1.300, 2.210 (planned the same; 0.559 and 1.42 are
  the creak and the second knock); DryFire 0.000.

## For the creator's ears

- **The blast** (`Resources/SkylineAudio/breacher/blast.wav`, against the Fourth Horseman): does the held low body
  feel like the Horseman's weight? It holds for about 0.45 s, then falls; is the crack on top too polite (it sits
  under the body, as the reference's does), or do you want more snap?
- **The vent** (in the fire clip at 0.24 s): the cell's hiss and falling whistle after each shot. Is it the Breacher's
  signature, or too much at 80 RPM?
- **The reload** (`out/skyline-forge/breacher/ReloadPartial.wav` and `ReloadEmpty.wav`, against the Witchfire Echo):
  it is brighter and more metallic than the first cut, aimed at the Echo's crisp shell inserts. Does the cell feel
  heavy enough when it seats (the seat's charging hiss is the cell's identity)?
- **The equip** (`Equip.wav`, against the Echo's equip): the grab's rustle and the lock with the cell's rising whine.
- **The boost** (`boost.wav`, on top of the blast): does a boost sound like a blast under your feet, and different from
  a plain shot?
- **The hits:** `breacher/impact.wav` (the shotgun's slam on a bot), `impact/hit.wav` (any bullet on plating, a tick
  and a short ring), `impact/world.wav` (a dull strike on concrete or tile).
- **The inspect knock** (`inspect_knock.wav`): a knuckle on a gas cylinder; a personality beat, no reference.

## Rebuilding

    uv run --project tools/skyline/soundforge python tools/skyline/guns/breacher/synth.py      # layers + recipes
    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py --gun breacher
    uv run --project tools/skyline/soundforge python tools/skyline/impact/render.py
    node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <blender.exe> -b --factory-startup \
        -P tools/skyline/guns/breacher/model/build_breacher.py -- <repo> [--preview out.png]

(Blender: `FableAnimationTests/BasilDownfall/tools/blender/blender.exe`; the build takes about 6 s with the preview.)

## Fix round 1 (judge round 1, 2026-10-14)

- **The colour:** the firm's orange is now (0.70, 0.19, 0.02), rough 0.6 (was (0.93, 0.36, 0.04), rough 0.42). In the sun
  through ACES the old orange clipped to lemon yellow (hue 50, the bots' gold). The deeper, redder, matter orange keeps
  its hue even when the light pushes it. The receiver also has panel seams and rivets now.
- **The blast is mastered to -2.9 dBTP** (was -1.4), to leave headroom for the hit layers and the bots, and its late
  debris is softer and duller (no more broadband clicks at 1 s).
- **The hit-sound rule** (proposed to the Writ side and the mix): one hit sound a shot. A gun that has its own impact
  plays only that (the Breacher's `breacher/impact`; the Writ's `writ/impact`, `impact_weak` or `spot_pop`).
  `impact/hit` is the fallback for guns without one, and `impact/world` stays for the world.

## Fix round 2 (judge round 2, 2026-10-14)

- **The hit** (`breacher/impact`, N2): it is rebuilt to cut through the blast. It has a saturated crunch of casing
  (1.2-5 kHz), nine pellets rattling on plating over 45 ms (hard ticks with steel rings at 1.9-4.6 kHz), and a loose
  rattle settling to 0.25 s, all pressed. The low knock is now small. Over the blast's first 120 ms it adds +7.5 dB in
  1-6 kHz (the old one added +0.1 dB).
- **The shove** (N3): bot knockback is 15 m/s (was 9), plates are kicked at 11 m/s plus 3 up (was 7 plus 2), and a sprung
  lurch tips each bot's body back up to 38° and spins it, ringing out in about half a second.
- **The reload** (N5): every part now leads with a mechanical clack. `cell_out` and `cell_in` add a solid mid knock of a
  heavy part. `seat` adds a held hydraulic press (a low rumble and a mid groan) under a hiss that is 10 dB lower. This
  moves the reload away from the Witchfire Echo's airy brightness (centroid about 1.3 kHz against 4.8 kHz). Weight was
  the call here; the creator's ears decide between the two.

## Fix round 3 (judge round 3, 2026-10-14)

- **PBR surfaces** (`Fx/Breacher/SkylineBreacherLook.cs`, made in code at scene start, no texture assets):
  - the firm's orange, the hazard yellow and the stripes' black are painted metal: a mottled coat with grime,
    chipped and scuffed through to bright bare steel along every edge of every part, a few chips and 26 scratches
    across the faces, the chips recessed in a normal map;
  - dark gunmetal (barrel, shroud, ribs, sights, rail, clamps) has brushed streaks and edges worn bright;
  - a dark stippled polymer for the grip, the pump and the standoff.

  The model now has UVs, so every part's edge falls on the texture's worn border. The colours are unchanged, so the
  orange stays the deep red one that holds its hue in the sun.
- **Shape detail:** front and rear sights, a charging handle in a slot, a trigger, a notched accessory rail on the
  left, two barrel clamps with a perforated heat-shield strap, the cell's guard rails, and six bolt heads on the
  standoff (2,824 vertices, was 2,120).
- **The hit:** `breacher/impact` now sits 6.8 LU under the blast (-25.4 against -18.6 LUFS; it had been 4.5 LU *over*
  it) and is only lightly rounded (crest 17.6 dB). It starts 40 ms in, the pellets' flight and the sound coming back,
  which lands it after the blast's crack. It is heard by its band: in that window it adds +2.2 dB in 2-4 kHz over the
  blast alone.
