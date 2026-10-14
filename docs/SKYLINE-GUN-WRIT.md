# The Writ: identity sheet

Owner: the Writ's identity agent (Skyline Repo M1, Task 18). Spec: `docs/SKYLINE-DESIGN.md` 2.2, 2.3 and the creator's
calls in 14 (cracked-casing spots, the spot cap, the damage-feedback rule). Design: the creator's final concept,
`FableAnimationTests/art/concepts/skyline/writ-final.jpg` (`docs/SKYLINE-CONCEPTS.md`). Data: `SkylineGuns.Writ()`,
`WritTuning` and `UpgradePaths.Writ()` (`Packages/com.vibejam.jamkit/Runtime/Skyline/`). Everything here is a draft for
the creator's eyes and ears: the creator plays every gun before it is final (2.3).

## The fantasy

A writ is a court's order, and this sidearm is a court: a gothic courtroom in miniature grown into the shape of a
pistol. You hold it by a fluted stone column wrapped in a parchment scroll; along its body runs a colonnade lit by tiny
gaslamps, with a brass nameplate on the side; its muzzle is a great pointed doorway with the court's light glowing
behind the doors; at the rear sits the judge's high-backed chair, empty, behind the bench, and over the bench a brass
gavel floats. Every shot is the gavel coming down: a hard wooden knock on a heavy body, the doors thrown open by the
flash, the stone hall answering and settling back into a hush. What it hits, it rules on: a hit on a weak point cracks
the casing into a glowing seam, a spot the court has marked, and the next slug into that seam breaks through for more
than a headshot ever does. The firm's orange robot hands hold it like a precision instrument, and you aim it through
the judge's chair: a round window with one brass bar, crossing a brass obelisk on the far keystone.

## The verbs

- **Mark:** a hit on a weak point (a head or a lens) leaves a weak spot there: the cracked casing, glowing and sparking.
  At most 3 a bot (5 at level 3), each lasts 6 s.
- **Rule:** a hit on a spot breaks through for x2.5 (more than the weak point's x2), and uses the spot up.
- **Aim:** one slug, no spread, full damage to 25 m, 60% by 60 m, reach 80 m. Slow enough (250 RPM) to place every shot.
- **Sight:** the inspect raises the gun to the eye and shows the sight picture: the bar crossing the obelisk.

## Every number and why

| Number | Value | Why |
| --- | --- | --- |
| Fire interval | 0.24 s (250 RPM), semi | Precise, never sprayed; the RPM leaves the gavel strike and the spot effects room to breathe (2.3). Unchanged. |
| Magazine | 8 | Eight rulings, then the drum. Unchanged (balance sims use it). |
| Damage | 22, x2 on a weak point, x2.5 on a spot | Spots outrank headshots by design (2.2; `TheShippedWritKeepsItsIdentity`). Unchanged. |
| Falloff | full to 25 m, 0.6 by 60 m, range 80 m | A sidearm that still reaches across the atrium. Unchanged. |
| Knockback | 0.5 | Barely moves a bot: the Breacher is the shover, the Writ is the scalpel. Unchanged. |
| Hit stop | 0.03 s, 0.08 s on a kill | A beat, not a stall. Unchanged. |
| Camera kick | **2.8° up, 0.6° side, recover rate 11** (was 2.2 / 0.5 / 9) | A heavy precise shot: the gavel lands with weight, and 7% of the kick (0.2°) is left when the next shot is allowed, so the sights are back on the target every time. Still well under the Breacher's 8°. |
| Shake | **0.07** (was 0.06) | A touch more thump to match the new body; still small. |
| Aim assist (pad) | slowdown 5° at 0.5, magnet 3° at 8°/s, 50 m | Unchanged; a precise gun gets a narrow cone. |
| Spots | 2.5x, 3 a bot (5 at level 3), 6 s, consumed on hit, 0.2 m radius | `WritTuning`; the creator's calls (cap by level) and the planner's defaults (consumed, 6 s). Unchanged. |
| Fire clip | 0.22 s: shot at 0 (the thwip). The gavel's hydraulic return event ("mech", 99 ms) and its sound are gone (fix round 2) | The recoil pose is now 7 cm back and 9° up at 12% (was 6 cm / 7° at 15%), settling through 0.45: the hand-cannon weight. |
| Dry fire | 0.25 s, click at 0 | The gavel taps the empty bench (40% of a strike). |
| Equip | 0.55 s: unfold at 0, lock at 0.7 (0.385 s) | The court assembles; the lamps and the doorway light up over the draw. Unchanged times. |
| Reload, partial | 1.3 s (unchanged): drum out 0.09, new drum in (held 12 mm short) 0.28, **windup 0.34 (animation only), SMACK 0.52, brass seat 0.565** | The reload is a gavel smack (fix round 2): see below. The left hand takes the drum as it drops and holds the new one until the seat. |
| Reload, from empty | 1.75 s (unchanged): out 0.07, in 0.2, **windup 0.25, SMACK 0.39, seat 0.435**, bolt 0.8 | The same smack, then the bolt racks the bench and chair (the slide) and re-cocks the gavel. |
| Inspect | 2.4 s: turn 0.1, tap 0.55, settle 0.85 | New pose: show the side (0.18-0.55), tap the plate, **raise to the eye for the sight check (0.68-0.8)**, settle back (0.92). Event times unchanged. |

The home pose: `SkylineWritFx` turns the gun 9° toward the crosshair (it converges about 1 m ahead) and lifts it
1.5 cm on top of the viewmodel's rest, so the colonnade's side shows at the hip; the inspect's sight check undoes it
(`SkylineGuns.WritSightCheck`, `WritSightCheckEuler`).

## The sights

- **Front:** a tall slim brass obelisk on the arch's keystone, an amber glowing bead (3.5 mm) on its rear face at the
  sight line's height (8.8 cm above the gun's root).
- **Rear:** a round window (2.7 cm across, a brass ring) through the empty judge's chair, one horizontal brass bar
  (0.8 mm) across its middle at the same height.
- **The picture:** with the eye on the line, the bar crosses the obelisk at the bead: a cross with a glowing heart. The
  bar is thinner than the bead looks, so the bead shows above and below it (checked by ray: 0.6 and 1.0 mm off the axis
  at the window both land on the bead).
- **Kept clean:** the gavel floats over the bench's left end and every deck item (rails, posts, finials) stays below
  the line, so nothing but the bar, the bead, the obelisk and the arch's top enters the window. At the hip, the bead,
  the obelisk and the window are all in view from the viewmodel camera (ray checks in task-18-report.md).

## The sound palette (all synthesized: `tools/skyline/guns/writ/synth.py`, recipes in `recipes/`)

| Event | Layers and what each does |
| --- | --- |
| `writ/shot` | **A quill's thwip** (fix round 2; the old gavel-and-hall shot is gone): a short bright airy whoosh sliding 5.5 to 11 kHz (stereo, 5 ms attack, about 90 ms), a soft feather/paper flutter chopped at 95 Hz, a tiny ink flick (a wet tick and a droplet's "plip"), a soft low-mid "thup" for the wrist (nothing under 250 Hz) and a dark air tail of about 0.2 s. 0.4 s in all, peak -8.5 dBFS. |
| `writ/impact_crack` | **The gavel crack, for a weak-spot hit only** (fix round 2): a hard dry hardwood crack (gavel modes pitched up, a thud at 125 Hz, a snap) with a short stone-room ring, 0.45 s. Played 2D from the shooter's side on top of the hit feedback when a shot hits a bot's weak point or a Writ spot; never on an ordinary hit or a miss. |
| `writ/dry_click` | The gavel taps an empty bench: a hollow wooden tock, a tiny latch. |
| `writ/equip_unfold` | The court assembles: a smooth rising hydraulic glide, a servo whirr, three small clicks as the parts unfold. |
| `writ/equip_lock` | A solid latch clunk and a small brass bell: the court is in session. |
| `writ/mag_out` | The stone drum releases: a brass latch, a short pressure hiss, a stone-on-stone scrape. |
| `writ/cell_in` | The new drum meets the column (a contact click) and slides in to a dense brass-and-stone thunk. |
| `writ/seat` | **A brass click** (fix round 2): a bright "tink" of the latch with a second tick as it locks and a soft low "tk" under it, 0.3 s. It lands 58 ms (partial) / 79 ms (empty) after the smack. |
| `writ/smack` | **The reload's gavel smack** (fix round 2): wood on wood, a heavier and lower hit than the crack (modes at 0.82x, a thud at 95 Hz, a snap), a hard dry crack with a short room ring, 0.55 s. |
| `writ/bolt` | The bench and chair rack back (click, slide) and slam home (a chunk), and the gavel re-cocks (a tick). |
| `writ/inspect_turn` / `_tap` / `_settle` | A soft handling swish and shift; two brass tinks as a robot finger taps the nameplate; a soft clunk as it settles. |
| `writ/spot_form` | (Played by the spot itself.) A ceramic crack with three smaller cracks after it, an electric crackle thinning out over 0.35 s, and a tiny seal chime: a writ is issued. |
| `writ/spot_break` | (Played by the spot itself.) A crunchy break-through (a cluster of eleven cracks in 70 ms), a spark sizzle, a low thump. |
| `writ/impact` | An ordinary hit: a dull dark thock (330-1350 Hz), no ring. Ordinary damage stays dark, to the ear too. |
| `writ/impact_weak` | A weak-point hit: the thock with a crisp crack and a short brass ring on top. |
| `writ/spot_pop` | The shooter's confirmation for a spot hit (2D): a seal-stamp THUNK on a desk and a brass hand bell. The gavel came down. |
| `writ/hush` | At rest, while the Writ is out: a hushed court (a soft air with slow swells, a far murmur, a gaslamp's flutter), a 5.5 s loop played very quietly. |

## The visual language

- **Silhouette:** a pistol at a glance, a building on a second look: tall at the back (the chair), long and low in the
  middle (the colonnade), a heavy arched block at the front (the gatehouse), and a column grip raked like a pistol's.
- **Materials:** grey ashlar stone (blocks 2 x 1 cm), dark mahogany, deep maroon panels, polished brass, black iron,
  aged parchment with brown cursive; amber glass in the lamps, the doorway, the bead and the drum's cell band. Textures
  are procedural (`tools/skyline/guns/writ/model/make_textures.py`); the tints and glow are set at run time.
- **The hands:** the firm's robot arms, shared by both guns: white robotic hand plates on graphite joints, the forearm
  a shell in the firm's tow-truck orange with a hazard band at the wrist (yellow with black diagonal stripes), and a
  small amber status lamp.
- **The shot:** the gavel strikes the bench, the doorway's doors burst open on a hot flash (three frames), the bench and
  chair kick back 5 mm, the lamps and the doorway flare, an amber light flashes on the world. The tracer is a stroke of quill ink (`SkylineInkTracer`, fix round 2), not a line.
  On the world: brass sparks and a scorched disc with a brass ring that fades after 5 s.
- **Spots (distinct from ordinary damage):** an ordinary Writ hit on a bot throws only dark flakes. A spot forming is
  stamped on like a seal (a gold ring lands big and presses down to size, six short rays), then it is the cracked casing
  (glowing seam, sparks, wiring; `SkylineSpotMarker`). A spot hit pops: two gold rings racing out, a gold flash, the
  spot's own spark burst. A kill adds a white-gold ring facing you. Glow and sparks on a live bot only ever mean a spot.
- **The charge:** the drum's amber band dims as the magazine empties and flashes white-hot when a new drum seats.

## The upgrade path (`UpgradePaths.Writ()`)

1. **Deeper Ink** (numbers, built): spot hits deal x3.25 instead of x2.5. *Looks and sounds (to build; the FX does not yet read the upgrade):* the seal ring stamps a
   little brighter and the spot's seam glows hotter; the spot pop's bell rings a fifth higher.
2. **Precedent** (augment, not built in M1): after a reload, one extra spot appears on the last bot you hit for every
   two spot hits the last magazine landed (at most 3). *Looks and sounds (to build with it):* on the seat, the doorway flares and the
   new spots are stamped on that bot one after another, each with its seal chime, like a clerk filing judgements.
3. **Case Law** (capstone, not built in M1): any damage can hit the spots (the Breacher's pellets, explosions, a hire's
   attacks), and the cap rises to 5. *Looks and sounds (to build with it):* every spot gains a thin gold outline other damage can see; a
   spot broken by something else plays the gavel's knock over its break, so you hear the court's ruling land.

## The references (loose measuring sticks)

The Writ's reference clips were never picked (the redesign came first). Per Task 1's ruling the first candidate per
event is the loose stick: the shot `writ/shot-destiny2-1-clean.wav` (a Destiny 2 hand cannon, the cocking click cut
off), the reload `writ/reload-partial-cyberpunk-1.wav` (Cyberpunk's Malorian Arms 3516). The Cyberpunk reload is a
10.6 s pool of a dozen handling animations, not one reload, so the Witchfire "Hunger" sidearm reload
(`writ/reload-partial-witchfire-1.wav`) is reported beside it, and the bolt is measured against the cocking click cut
from the shot (`writ/cock-destiny2-1.wav`). Identity came first: the measurements are in `task-18-report.md`.

Out on purpose, and why:
- **The shot is darker than the reference** (centroid 368 Hz against 476, -23%, inside 30%): the gavel's knock lives
  in the mids, but the court's body is a stone-and-hydraulic weight, not a gun crack. Its attack (under 1 ms against
  31 ms) is a gavel strike, sharper than the reference's softened onset; the acceptance's limit is a ceiling.
- **The reloads have more low end than either reference** (24-28% in 60-250 Hz against 4-10%): the drum is stone and the
  seat is hydraulic, and the brief's bar is "a well-oiled hydraulic masterpiece". Their crest is 7-13 dB higher because
  both references sit on a game's ambience bed (Witchfire's never drops below -35 dB) while ours are clean handling with
  silence between the parts; the same bed is Witchfire's 14% under 60 Hz. Against Witchfire the centroid (+20 to 25%)
  and the partial's tail (-17%) are in range; the from-empty tail is longer because it holds the bolt at 1.4 s.
- **The bolt is sparser than the cocking click** (crest +17 dB, tail -59%): the reference is a loud dense rack; ours
  is two strokes and a tick with air between them, so each part reads at speed. Its bands are within 0.10.

## For the creator's ears

What to listen for, in the files under `out/skyline-forge/writ/` (whole clips) and
`Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/writ/` (single events):

1. (before fix round 2) `Fire.wav` against the Destiny 2 hand cannon (`out/skyline-refs/writ/shot-destiny2-1-clean.wav`): does the shot say
   *gavel* first and *gun* second? Is the stone hall's tail (about 1.9 s) a courtroom, or too much for 250 RPM?
2. (before fix round 2: `shot_mech` is gone with the hydraulic return.)
3. `ReloadPartial.wav` and `ReloadEmpty.wav` against the Witchfire sidearm reload: does `seat` land as the hydraulic
   masterpiece? Is the stone drum heavy enough, or too heavy?
4. `bolt` against the Destiny 2 cocking click (`cock-destiny2-1.wav`): two strokes and a tick; does it want more body?
5. `Equip.wav`: the court assembling, and the little bell on the lock. Charming, or one bell too many?
6. `spot_form` (crack, crackle, seal chime) and `spot_break` (crunch, sizzle, thump): the spot's whole life. Does the
   chime make a spot feel *issued*?
7. `spot_pop` (stamp and bell): the payoff of the gun. Is the bell the right reward, or should it be heavier?
8. `impact` (dull) against `impact_weak` (crack and ring): can you tell them apart without looking?
9. `hush`: the quiet court while the Writ is out (played at a tenth of full volume). Keep it, or drop it?
10. `Inspect.wav`: the two brass taps on the nameplate.

## Fix round 1 (judge C, 2026-10-14): what changed

- **The shot cracks.** A new crack layer: a sharp report, the gavel's brass band ringing like a struck nail, and the
  slug's short whiz. There are also bright early reflections (an "air" stem, 3.5-13 kHz, fading over about 0.5 s) and
  a stone room tail that holds (a second slope; about -17 dB at 1 s and -23 dB at 1.5 s, like the reference).
  Presence plus air is 2.9% (was 0.2%); centroid 464 Hz (reference 476).
- **The hydraulics sing.** The gavel's return is now a held pneumatic "psshh" in 2-8 kHz with a brass piston ka-chunk
  and a latch tick. It lifts its window above the shot's own first 99 ms (above 2 kHz: +4.2 dB over the shot alone).
- **The handling sits 9-14 dB under the shot,** like the Breacher's (was 18-25 dB). The seat's "pssh" is real now (low
  band 56%, was 95%).
- **The framing.** The rests moved out and up (`SkylineViewmodel`: the Writ at 0.2, -0.13, 0.5, turned 9 degrees toward
  the crosshair; the Breacher at the same place, turned 5 degrees), so the robot hand on the column grip shows at the
  hip and the gun reads as a pistol. The old "home" layer in `SkylineWritFx` is gone.
- **The arms.** Each forearm is its own piece, aimed every frame at a shoulder below the screen, so it always runs out
  of the bottom of the view. The Breacher's left hand cups the pump with its fingers up the near side.
- **The reload shows its mechanism.** The gun lifts and rolls so the column's base faces you. The drum drops 8 cm (not
  25) with the left hand on it, spinning out as it goes; the new drum goes in a quarter turn off and twists home on the
  hydraulic seat, with a kick.
- **The draw assembles the court:** the bench and chair rise, the gavel drops in, the doors swing shut, the lamps catch.
- **The look.** The chair back the player sees is carved: gothic lancet panels, pointed-arch tracery round the window,
  brass rivets, pinnacles with crockets on its posts, a gable with a brass edge and a cross finial. The receiver's rear
  is a brass-framed maroon panel with the court's seal (it was one flat brass slab).
- **Effects at range:** the muzzle burst sits in front of the gatehouse and above its arch, so it shows from behind.
  World hits get a hot flash, 10 sparks and a decal twice the size. The spot's seal and pop grow with distance (x1 to
  6 m, then in proportion). A kill is a white starburst, not a ring, so it can't be taken for a spot's gold pop.

### One hit sound per shot (the layering rule, agreed for both guns)

For a shot that lands on a bot, the shooter's side plays exactly one hit layer: the gun's own impact (`writ/impact`,
`writ/impact_weak` or `breacher/impact`). The generic `impact/hit` is for guns without their own and is skipped for the
Writ and the Breacher. A Writ spot event adds the spot's own world sound as the one allowed second layer: a forming spot
plays `impact_weak` and `spot_form`; a spot hit plays `spot_pop` (in place of the impact) and `spot_break`. A world hit
plays `impact/world` once. So a hit is one sound, and a spot event is two.

## Fix round 2 (the creator's playtest, 2026-10-14): what changed

The creator's calls: the reload was "a flop ... instead of something like a smack of the gavel"; the sound "quite grating";
"the gavel crack only when a weak spot is shot; otherwise the shot is a small thwip, like shooting a small quill"; the
tracers looked like Chamber's Headhunter, so "ink from a quill"; and the view was narrow.

- **The reload is a gavel smack.** The small gavel on the roof rises over a `windup` event (animation only), falls in
  the last 3% of the clip before the `smack` event (39 ms in the partial, 52 ms in the empty reload), and smacks the
  sound block; the gun recoils in its pose frames (3.5 cm down, 3 cm back, the muzzle 8° down), the camera gets a
  0.7° kick and 0.04 shake (a quarter of a shot's), the lamps and the doorway flare, and the drum, held 12 mm short, is
  driven home; 58 ms (79 ms) later the `seat` event twists it home and flashes the amber band white-hot, with a brass
  click. Total times are unchanged (1.3 s, 1.75 s). The `smack` event carries `writ/smack`, so the range director's
  schedule and the sync check see it like any other counted sound (the ears' onsets on the forge render: smack at
  0.676 s and seat at 0.7346 s in the partial, exactly the schedule).
  **Where it lives (so a quill-and-ink animation can replace it):** the events and pose frames are data in
  `SkylineGuns.Writ()` (`ReloadPartial`, `ReloadEmpty`); the gavel's curve is the pure `SkylineGuns.WritGavelSmack`
  (unit-tested); the gavel/lamp/camera presentation is `SkylineWritFx` (`GavelSmack`, the `smack` case in `OnCue`).
  Swapping the animation for another means changing those and keeping a sounded `smack`-like event at the strike, so
  `tools/skyline/guns/writ/Reload*.timeline.json`, the sound schedule and the sync check follow from the clip data
  (regenerate the timelines with the explicit test `WriteTheForgeTimelines`).
- **The sounds** (all in `synth.py`; the old hydraulic shot stems and `shot_mech` are deleted): the shot is a quill's
  thwip, the gavel crack is `impact_crack`, the reload has `smack` and a brass `seat`. The mix's buses and limiter are
  unchanged (`writ/impact_crack` joins the Impacts bus by its name).
- **The crack on a weak-spot hit only:** `SkylineWritFx.OnHit` plays `writ/impact_crack` (2D, volume 0.9) when
  `r.writ.spotHit || info.zone == WeakPoint`, on top of the hit feedback (`impact_weak` / `spot_pop`, and the spot's own
  `spot_form` / `spot_break`). An ordinary hit and a miss do not play it. The layering rule above gains this one 2D layer.
- **The ink tracer:** `SkylineInkTracer` (in `SkylineTracer.cs`): a ribbon of dark indigo ink (#2A1F5C, a wet sheen of
  #5B4BB4 along its upper edge that dries first, a near-black #0B0817 outline under it), drawn out from the muzzle in
  0.04 s, 2.2 cm wide at the start and tapering to a point, never thinner than about 2 px; an irregular brush edge;
  five droplets that flick off its first 60% and fall a little (gravity 3.5 m/s², scaled up with distance) and fade; the
  whole stroke dries and fades in 0.35 s. It replaces both the old cream bar (for the Writ; the Breacher and the bots
  keep theirs) and the Writ's amber ribbon. The material is `Sprites/Default` (always in the build), vertex-coloured, so
  no keyword is turned on at run time.
- **Field of view:** a "Field of view" slider on the pause menu's Look page, 60 to 110° vertical, 68° by default (what
  the game had; `SkylineOptions.fieldOfView`, saved in the options string as `fov=`, older saves keep 68). The movement
  camera sets its own view every frame; `SkylineViewmodel` multiplies it by `option / 68`, and aiming down sights
  narrows from there by the gun's own factor as before. The guns' overlay camera is unchanged, so the guns stay one size.

### The shot's numbers, old against new (the ears, `listen.py measure` and a Welch spectrum over the first 0.5 s)

| | old `writ/shot` | new `writ/shot` (thwip) |
| --- | --- | --- |
| Length, peak, loudness | 2.8 s, -1.6 dBFS, -19.2 LUFS | 0.4 s, -8.5 dBFS, -27.8 LUFS |
| Tail to -40 dB | 2329 ms | 85 ms |
| Spectral centroid | 464 Hz | 5331 Hz |
| Band power, 0-250 Hz | -23.9 dBFS | -83.7 dBFS |
| Band power, 250-2000 Hz | -24.4 | -46.4 |
| **Band power, 2-5 kHz** | **-29.9** | **-48.1** (18 dB less) |
| Band power, 5-10 kHz | -25.3 | -41.4 |
| Loudest 5 ms of the 2-5 kHz band | -19.8 dBFS | -25.9 dBFS (6 dB less) |
| Narrow peak in 2-5 kHz over its neighbours | 0.6 dB at 3.8 kHz | 0.9 dB at 4.8 kHz (no spike) |

The new shot sits 7 dB under the old one's peak and 8.6 LU quieter on purpose ("a small thwip"); if it is too small in
play, raise the recipe's `THWIP_MIX` (in `synth.py`), not the bus. The other new sounds: `smack` peak -1.6 dBFS,
centroid 525 Hz; `impact_crack` peak -4.5 dBFS, centroid 653 Hz, attack 0.12 ms.

## The back: inkwell and quill (the creator's pick, 2026-10-14)

The back above the grip, facing the player, was "a boring box". The creator picked the concept "A take 2: inkwell and
quill" (`FableAnimationTests/art/review/2026-10-14/skyline-writ-back-a-inkwell-take2.jpg`): a brass-and-glass inkwell of
glowing indigo ink set into the masonry under the clock tower, like part of the building, with a quill leaning back over
the hand. Their notes: the quill blocked the aim-down-sights sightline a bit, and they wanted it tied into the animations
and abilities. The front of the gun is unchanged.

**The model** (`tools/skyline/guns/writ/model/build_writ.py`, `inkwell_parts` / `ink_parts` / `quill_parts`; re-export
`writ.fbx` with that script). Three new top-level objects, all in the gun's frame like the rest:

- `Inkwell` (static): a stone shelf from the receiver's bottom and a pointed-arch alcove of masonry against its rear face (a
  lighter stone frame proud of it), a brass-framed glass jar standing on the shelf (brass base rings, four ribs, a glass body
  and shoulder, a brass rim with an opening). It sits right of centre on the rear face (x 0.012, z -0.100), under the deck
  (0.026) and 7 cm under the sight line (0.088). The seal on the rear panel is left as it was, partly behind the alcove.
- `InkLevel`: the glowing indigo ink, a cylinder from the jar's floor to the full level with its **pivot at the bottom of
  the well**, so scaling its height is the level.
- `Quill`: shaft, iron nib and a feather vane (two tilted halves), a separate object with its **pivot at the nib, the
  well's mouth** (0.012, 0.002, -0.100). It is modelled in the hip pose: leaning back toward the player and out to the
  right, away from the sight line (axis 0.42, 0.78, -0.46).
- New materials `Writ_Ink` (flat indigo with the glow treatment the lamps get: an emission set at run time), `Writ_Feather`
  and `Writ_Glass`. The glass is a pale, 20% alpha, unlit `Sprites/Default` copy made in `SkylineWritFx.Copy` (always in a
  build, so the ink shows through under any lighting). Flat painted colours, no new textures.

**The behaviour** (maths in `Runtime/Skyline/WritQuill.cs`, tested in `Tests/Skyline/WritQuillTests.cs`; applied by
`SkylineWritFx.AnimateBack`):

- **The ink is the ammo.** The `InkLevel` height scale follows the magazine fraction (full 1, half 0.53, empty a thin film
  of 0.06, never nothing), smoothed (rises at 12/s, falls at 8/s, a refill at 16/s) with a small spring slosh kicked by each
  shot, the reload's smack and the gun's own movement (the change of the gun's velocity).
- **Each shot the quill flicks**: a quick swing toward the muzzle (30 degrees at the hip) in 0.04 s and back to rest by 0.12 s,
  on the same beat as the gavel's strike (`_shotAge`); a dry fire flicks at 40%. The quill's thwip and the ink tracer stay.
- **At the hip the quill leans out of the sight line; in ADS it lies flat**, nearly horizontal, pointing back at the player
  and a little out, 6 to 8 cm under the sight window (blended by `SkylinePlayer.AimEased`); the flick is 70% smaller there.
- **The reload keeps the gavel smack** and the quill joins it: as the drum drops (from `mag_out`) the ink sinks to 10% (the
  well is empty with the drum out), the quill dips 6 mm into the well and stirs once (one full turn, 14 degrees off
  vertical) before the gavel's `windup`, then returns to its lean; on the `seat` event the ink refills to full and glows
  white-hot for a moment, with the cell's amber band.
- **A weak-spot hit (the spot pop)** makes the quill do a double flick, 0.26 s in all; never in ADS (only when the aim is
  below 20%).
- Nothing here has a motion-scale option to respect (the file has none); every step is skipped when a frame has no time.

**Checked without Unity** (`WritQuillTests`): the ink mapping (full, half, empty, the drum-out sink, the refill on the
seat), the pose states (hip lean, ADS flat, a flick returning to rest, the flourish under 0.3 s with two peaks, the reload's
dip and stir), the sight line (the eye from `AimDownSights.SolveSights`: every point of the quill's envelope stays more
than 15 degrees outside the window's cone, and below the window, for every flick from hip-to-ADS blend 0.8 to 1), and that
the Python's constants match `WritQuill.cs`. The change list for Unity's pass: the FBX import scale and axes of the three new
objects, the glass's look, and the ADS and hip views (the quill and the hand around the shelf).
