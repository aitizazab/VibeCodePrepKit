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
| Fire clip | 0.22 s: shot at 0, the gavel's hydraulic return ("mech") at 0.45 (99 ms) | The recoil pose is now 7 cm back and 9° up at 12% (was 6 cm / 7° at 15%), settling through 0.45: the hand-cannon weight. |
| Dry fire | 0.25 s, click at 0 | The gavel taps the empty bench (40% of a strike). |
| Equip | 0.55 s: unfold at 0, lock at 0.7 (0.385 s) | The court assembles; the lamps and the doorway light up over the draw. Unchanged times. |
| Reload, partial | 1.3 s: drum out 0.18, new drum in 0.52, hydraulic seat 0.68 | The left hand takes the drum as it drops and brings the new one; unchanged times (the sync check was already exact). |
| Reload, from empty | 1.75 s: out 0.13, in 0.42, seat 0.55, bolt 0.8 | The bolt racks the bench and chair (the slide) and re-cocks the gavel. |
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
| `writ/shot` | **Transient:** the gavel strike, a hardwood knock (wood modes at 540, 1230, 2140, 3350, 5100 Hz) on a dry click. **Body:** a saturated low boom gliding 175 to 68 Hz, a mid-band roar (280-1600 Hz), a thump, a short punch. **Hydraulic:** a pneumatic breath and a brass latch inside the first 15 ms (the well-oiled machine). **Tail:** the colonnade's slap-back (17, 29, 43, 61 ms) and a dark stone hall (RT60 about 2 s, its brightness falling from 2.6 kHz to 420 Hz) that settles into a hush. Wide stereo (0.65, like the reference). |
| `writ/shot_mech` | The gavel's hydraulic return 99 ms after the shot: a piston's sigh sweeping down and an oiled wooden clack. |
| `writ/dry_click` | The gavel taps an empty bench: a hollow wooden tock, a tiny latch. |
| `writ/equip_unfold` | The court assembles: a smooth rising hydraulic glide, a servo whirr, three small clicks as the parts unfold. |
| `writ/equip_lock` | A solid latch clunk and a small brass bell: the court is in session. |
| `writ/mag_out` | The stone drum releases: a brass latch, a short pressure hiss, a stone-on-stone scrape. |
| `writ/cell_in` | The new drum meets the column (a contact click) and slides in to a dense brass-and-stone thunk. |
| `writ/seat` | The hydraulic seat, the "masterpiece" moment: a pressurised "pssh", a heavy lock, the cell humming awake. |
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
  chair kick back 5 mm, the lamps and the doorway flare, an amber light flashes on the world, an amber ribbon follows the
  slug. On the world: brass sparks and a scorched disc with a brass ring that fades after 5 s.
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

1. `Fire.wav` against the Destiny 2 hand cannon (`out/skyline-refs/writ/shot-destiny2-1-clean.wav`): does the shot say
   *gavel* first and *gun* second? Is the stone hall's tail (about 1.9 s) a courtroom, or too much for 250 RPM?
2. `shot_mech` (99 ms after each shot): the gavel's hydraulic return. Oiled and satisfying, or fussy at speed?
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
