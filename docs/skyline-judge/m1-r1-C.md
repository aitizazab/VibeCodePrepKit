# Skyline Repo M1, round 1: judge C (gun feel)

Pass: `out/skyline/20261014-0326/`. My frames and pictures: `out/skyline/20261014-0326/judge-C/` (named by scene and
time). Recording times below are the recording's own clock; for the range, recording time = schedule time + 1.984 s
(the marker, `sync-check.json`). Sounds were measured with the ears (`listen.py measure|compare`) and two small probes
of my own: clipping per 5 s window, and the level of each scheduled range event in the recording.

**Scope:** spec 2.2-2.3 and bar 10.7 (gun feel), for both guns: looks, sounds, numbers and handling sets, effects,
hit feedback.

## Defects, most severe first

### 1. The Breacher's identity, the shove, appears in no capture (Breacher, demo player/capture)
- **What:** no recording shows a Breacher shot hitting a bot. The range has only static paper plates. The gallery's
  shooter fires only Writ hits (`SkylineGalleryDirector.cs:16`, "an invisible shooter by the camera lands Writ hits").
  In the arena the Breacher is out only from about 5.0 to 8.5 s, for the one kiosk boost (`judge-C/arena-gun-strip.jpg`:
  the yellow gun shows in the first 8 cells only, out of 240 at 2 fps; log: `swap-breacher`, `boost-fire`, then
  `swap-writ`; the only other mentions are the `dot-breacher-boost` / `dot-boost-done` lines).
- **Why it matters:** two of the three ways the spec wants knockback felt (bots slide, plates fly along the shot) and
  the Breacher's whole hit side (`breacher/impact`, chips, plate kicks) can't be judged. The creator, watching the
  arena demo, will see a shotgun that is never fired at an enemy.
- **Owner:** demo player/capture (the arena demo and gallery need a Breacher segment); Breacher.

### 2. Every recording clips; the arena mix is slammed (audio mix)
- **What:** the range recording peaks at +1.1 dBTP with 410 clipped samples. The Breacher blasts are louder than full
  scale (mono peak +3.7 dB at 17.95 s), and so is the Writ's first shot (+0.8 dB at 3.37 s). The arena recording is
  integrated **-5.9 LUFS** with **77,233 clipped samples** and a true peak of +1.5 dBTP. Every 5 s window clips except
  80-90 s, and fights run -8 dBFS RMS (60-75 s: 8,204 / 7,509 / 6,720 clipped samples per window). The gallery has
  3,903. `SkylineSounds.cs` has no mixer group, no limiter and no ducking: `PlayOneShot` at clip volume 1.0, and shots
  are already mastered to -1.4 dBTP (Breacher) and -3.0 dBTP (Writ).
- **Why it matters:** the "phenomenal" bar can't survive hard clipping. The Writ's dark body and the Breacher's held
  boom turn into distortion as soon as bot sounds, hit layers and the 2.5 s blast tails overlap (at 80 RPM, three
  Breacher tails overlap). Headphones at a normal volume will be painful.
- **Owner:** audio mix.

### 3. The Writ's shot has no snap, and its "well-oiled hydraulic" part is buried (Writ, sound)
- **What:** `writ/shot.wav` is 99.7% energy below 2 kHz: presence 0.2%, air 0.0%, rolloff 717 Hz, centroid 367 Hz.
  The Destiny 2 hand-cannon reference has 3.2% presence, with energy up to 16 kHz through its first 0.5 s
  (`judge-C/writ-shot-vs-ref-*.png`). Ours has nothing above about 4 kHz after 0.15 s. The identity sheet's "hardwood
  knock with modes at 2140, 3350, 5100 Hz" and "brass latch inside the first 15 ms" don't show in the measurement. The
  reference also holds a room tail at -20 to -35 dB from 0.5 to 2 s; ours falls in a straight line to -40 dB by 1.0 s.
  The hydraulic return `shot_mech` (peak -13.5 dBTP, 89% mid band) lands 99 ms after the shot, while the shot's own
  body is still 10 dB louder. In the range recording the 120 ms before the mech is already at -11.4 dB RMS and the mech
  window is -12.9 dB (3.47 s), so it adds nothing you can measure.
- **Why it matters:** the creator's bar is "a well-oiled hydraulic masterpiece slinging nails". This is a round, dark
  thoom with no crack and no audible machine. It is the gun the demo fires most.
- **Owner:** Writ.

### 4. Reloads and equips can't be seen: no drum, no cell, no hands (Writ, Breacher; viewmodel)
- **What:** in the Writ's partial reload (`judge-C/range-writ-reload-partial-6.45s-tile.jpg`, 6.45-7.65 s) the gun
  only tilts and bobs. The drum, the grip column and any left hand stay below the frame, though the sheet says "the
  left hand takes the drum as it drops and brings the new one". The Breacher's from-empty reload (30.0-32.0 s) is a dip
  and a return: the cell under the receiver, the pump and any hand never come on screen. The Writ's equip (2.3-3.1 s) is
  a plain slide up from below with no "court assembling", and the Breacher's (16.8-17.8 s) is the same. The sounds fire
  on time (point 9), but they have nothing visible to sync with.
- **Why it matters:** the spec makes reloads "a timeline of events (mag out, cell in, hydraulic seat, bolt)" that the
  player sees and hears together. Here the player hears a mechanical event and sees a gun nod, so the handling set
  reads as unfinished.
- **Owner:** Writ, Breacher (viewmodel poses and framing).

### 5. The robot arms are missing at the hip, and crude where they show (both guns, viewmodel)
- **What:** at rest with the Writ (`frames/SkylineFiringRange-001.9s.jpg`, `-005.6s`, `-013.1s`; every arena frame) no
  hand or forearm is visible, only a sliver of white at the bottom edge. The grip, the gun's defining scroll-wrapped
  column, is never on screen. The hand appears only in the inspect (`judge-C/range-writ-inspect-side-9.0s.jpg`), as
  white boxes and grey cylinders around a thin black wire-like trigger guard. The firm's orange forearm with the hazard
  band is not visible there. The Breacher shows a bare orange stick with a yellow-and-black band
  (`frames/SkylineFiringRange-020.6s.jpg`) and no hand on the gun. Its sheet says it uses "the viewmodel's primitive
  arms for now".
- **Why it matters:** "the viewmodel is robot arms" is the fiction (spec 2.3; you are a robot Unit). The concept's inset
  (`writ-final.jpg`) shows the robot hand on the column grip. In-game the gun floats.
- **Owner:** both guns (shared arms; Task 18's arms never reached the Breacher).

### 6. The Writ reads as a building block-out, not as the creator's design or a pistol at a glance (Writ, look)
- **What:** compared with `FableAnimationTests/art/concepts/skyline/writ-final.jpg`, the parts are there: the arch
  muzzle, the colonnade with lamps, the chair with the round window and bar, the gavel and the obelisk with the amber
  bead. But they are blocky, flat-shaded boxes. The chair is a plain box with a pediment (the concept's carved crest,
  pinnacles and panels are gone), and under the chair is a flat untextured-looking brass slab
  (`judge-C/range-writ-shot1-3.38s-crop.jpg`). The hip view (`frames/SkylineFiringRange-001.9s.jpg`) is a long
  dark-red box taking about 25% of the screen's width and 45% of its height at lower right. With the grip hidden
  (point 5), nothing in that view says "pistol". The only pistol read is the inspect's side view (9.0 s).
- **Why it matters:** the creator chose this design for its "pistol at a glance, a building on a second look" quality
  (the identity sheet's own silhouette line). In first person it is the reverse.
- **Owner:** Writ (model detail; viewmodel pose and FOV).

### 7. A Writ hit plays two or three hit sounds, and the "dark" ordinary hit rings (hit feedback, audio)
- **What:** `SkylineShooting.cs:93-96` plays `impact/hit` (a tick and steel ring: 99.4% presence, centroid 2.6 kHz) on
  every shot that lands on a hittable. `SkylineWritFx.OnHit` (`SkylineWritFx.cs:469-477`) then also plays `writ/impact`
  (dark thock, centroid 339 Hz), `impact_weak` or `spot_pop`, and `SkylineSpotMarker` adds `spot_form` /
  `spot_break` (`:39`, `:97`). The Breacher plays `impact/hit` plus `breacher/impact` (`SkylineBreacherFx.cs:350`). So a
  hit is 2 sounds, and a spot hit 3 (impact/hit, spot_pop, spot_break).
- **Why it matters:** the brief asks for one hit sound per shot. The spec's own rule, "ordinary damage stays dark, to
  the ear too" (Writ sheet), is undone because every ordinary hit also rings the steel ring. `impact` vs
  `impact_weak` (centroid 339 against 2172 Hz) is a clean distinction, but it gets masked. The stacked layers also feed
  the clipping in point 2.
- **Owner:** audio mix, hit feedback.

### 8. The Breacher's effects use placeholder geometry (Breacher, effects)
- **What:** the muzzle "smoke puffs" are opaque, lit grey spheres beside the muzzle (`judge-C/range-breacher-blast1-tile.jpg`,
  frames 3-12; `judge-C/range-breacher-after-18.15s-crop.jpg`), and the "dust puffs" on the target are grey balls stuck
  to its face for over 0.3 s. The shockwave ring is a flat, unlit, uniformly white circle 2.6 m out
  (`judge-C/range-breacher-blast-17.95s.jpg`). It reads as a HUD reticle, not a blast, and after the camera kick it
  drifts off the target (tile frame 5). The muzzle flash is six thin white spikes with no hot colour, for one or two
  frames.
- **Why it matters:** a door-breach blast should look like a blast. Grey balls read as untextured placeholders, which
  is a spec blocker category (see blockers).
- **Owner:** Breacher.

### 9. Only the reloads were sync-checked (handling set, capture)
- **What:** `sync-check.json` reports "14/14 events within 35 ms", but `synccheck.py` counts only the reload clips
  (14 of 62 events; `RELOAD_CLIPS`). Equip, fire and mech/vent, dry fire and inspect were not checked. My own probe of
  the range recording found every one of the 62 scheduled sounds clearly above the bed at its time (for example the
  Writ dry click at 13.90 s is -8.8 dB peak; the Breacher pump at 31.65 s is -2.9 dB). So the events fire, but nobody
  has checked them against the animation, and the animation is partly invisible (point 4).
- **Owner:** demo player/capture.

### 10. The Writ's handling is 18-25 dB under its shot, and much quieter than the Breacher's (audio mix, Writ)
- **What:** these are 120 ms RMS levels in the range recording.

  | Event | Writ | Breacher |
  | --- | --- | --- |
  | Shot | -11.1 dB | -7.9 dB |
  | mag_out / cell_out | -36.5 dB | -23.6 dB |
  | cell_in | -31.7 dB | -24.2 dB |
  | seat | -29.6 dB | -20.0 dB |
  | Inspect tap / knock | -33.1 dB | -21.6 dB |

  The Writ's "masterpiece moment" `seat` is also 95.3% low band (centroid 291 Hz, -32.9 LUFS) with 2.6%
  presence and air, so the sheet's "pressurised pssh" isn't there.
- **Why it matters:** on laptop speakers the Writ's reload is a faint low thump. Under any bot sound it's gone.
- **Owner:** Writ (levels and layers); audio mix (the handling level rule).

### 11. The Writ's effects are hard to read at range (Writ, effects)
- **What:** at 3.38 s (`judge-C/range-writ-shot1-3.38s-crop.jpg`) the shot shows only the amber ribbon. The doorway
  "doors burst open" flash faces away from the camera, behind the arch. In the arena the flash shows as an orange flare
  (`judge-C/arena-practicebot-shots-tile.jpg`, about 1.5 s). On a range plate at about 12 m, the impact is a grey-brown
  blob about 5 px across (`judge-C/range-writ-11.13s-crosshair-zoom.png`). No brass sparks or ring are readable.
- **Why it matters:** the precise gun's feedback should land on the target. The best part of the flash is on the side
  the player never sees.
- **Owner:** Writ.

### 12. Spot creation and pop: readable up close, lost at range, and the pop looks like the kill ring (Writ, effects)
- **What:** in the gallery (6.6-9.6 s, `judge-C/gallery-drone-spot-closeup.jpg`) the drone's spot is a tiny yellow fleck
  under the body, and the spot hit shows as a few white sparks; the "two gold rings racing out" are not visible at the
  gallery camera's distance. In the arena at point blank (63.8-66.2 s, `judge-C/arena-63.8-66.2s-tile.jpg`) the gold
  ring and flash read strongly, but the spot pop and the kill ring are both gold rings on the same bot within frames
  of each other, so the two can't be told apart.
- **Why it matters:** the spot is the Writ's whole identity. A spot formed at a mid-range engagement distance must
  read.
- **Owner:** Writ.

### 13. The arena's first Writ teaching beat fails (Writ, demo player)
- **What:** the demo fires three weak-point shots at the practice bot (`SKYLINE_DEMO writ-shot 1..3`, then
  `writ-no-spot`). The hits land (white X markers in `judge-C/arena-practicebot-shots-tile.jpg`), but no spot forms
  while the subtitle says "Tag its lens with the Writ and it leaves a weak spot" (arena 10-13 s, `arena-8-32s-tile.jpg`).
- **Why it matters:** the first lesson of the gun's identity is shown failing. Either the lens isn't the weak point's
  collider, or the aim is off.
- **Owner:** Writ (weak-point colliders and the spot rule) or demo player (aim point).

### 14. No hit marker on Writ hits in the range (hit feedback)
- **What:** after Writ hits on the range plates (3.36-3.65 s; 11.07-11.37 s, `judge-C/range-writ-marker-check-11.07s.jpg`)
  no marker shows in any of 18 frames. The Breacher's white X shows at 17.95 s, and Writ markers do show on the arena's
  practice bot. `SkylineTargetPlate.TakeHit` returns `hit = true`, so I can't tell the cause from here.
- **Owner:** hit feedback / HUD.

### 15. Capture problems that hamper this judging (demo player/capture)
- Arena 5-12 s is blocky and noisy (encoder starvation; `judge-C/arena-0-9s-tile.jpg`, `arena-boost-7.3-8.7s-tile.jpg`).
  That is the only Breacher footage in the arena, including the boost.
- The range's pad-look segment snaps between targets in one frame (11.08 to 11.10 s), which is the "insta-flick" the
  creator already disliked.

## What works
- **The sight picture is clean.** At the inspect's sight check (9.92 s, `judge-C/range-writ-sightcheck-9.92s-crop.jpg`)
  the brass bar crosses the obelisk at the glowing bead, on the target, through the round window, with nothing else in
  the window. It is exactly the creator's call. But it's only an inspect pose, and the chair box blocks about a third of
  the screen while it's up.
- **The Breacher blast is near the Fourth Horseman.** Its bands are .02/.76/.16/.05/.01 against .02/.76/.15/.05/.02,
  and its body holds near full level to about 0.45 s (`judge-C/breacher-blast-vs-horseman-*.png`). Two gaps: the
  reference holds a -35 to -40 dB room tail to 2.5 s where ours falls to -60, and our debris ticks at 0.95-1.3 s are
  broadband clicks.
- **The Writ's impact and impact_weak are clearly different** (centroid 339 against 2172 Hz), until point 7 masks them.
- **The Breacher's boost shows a blast underfoot:** a flash and a glowing ring on the kiosk floor at 8.0-8.3 s
  (`judge-C/arena-boost-7.3-8.7s-tile.jpg`), and the log has `boost-ledge reached`.
- **Camera kick is visible for both guns and recovers.** The Breacher's is big (the view jumps about 15% of the screen
  height, then returns by about 0.35 s); the Writ's is small.
- **Tracers read:** an amber ribbon for the Writ, nine thin lines for the Breacher.
- **The numbers are coherent with the identities.** Writ: 250 RPM, 8 rounds, spots x2.5 above weak points x2.
  Breacher: 80 RPM, 4 shells, 9 x 9 pellets, a 9 m/s shove. The handling sets are complete in data, and every event
  sounds in the range.

## Scores (1-10; 8 is the bar)

| Category | Score | Why, in one line |
| --- | --- | --- |
| Looks (models, sights, arms) | 4 | Sights clean; the Writ is a block-out of the design with the grip never seen; the Breacher is a plain orange box; the arms are absent at the hip or crude |
| Sounds | 4 | The Breacher blast is close to its reference, but the Writ shot has no snap, the hydraulics are buried, the Writ's handling is too quiet, and every recording clips |
| Numbers and handling sets | 6 | Complete and on time in sound; the reloads and equips have nothing visible to sync with; only the reloads were checked |
| Effects | 4 | Tracers and kick OK; the Breacher uses grey-sphere puffs and a UI-like ring; the Writ's flash faces away; spots don't read at range; the Breacher on bots is never shown |
| Hit feedback | 5 | Markers exist and are colour-coded; 2-3 hit sounds a shot, a ringing "dark" hit, no marker on range plates |
| **Bar 10.7, gun feel (overall)** | **4** | |

## Spec 10 blockers I see
1. **An untextured placeholder in view:** the Breacher's muzzle smoke and hit dust are plain grey lit spheres, held for
   up to 0.5 s in the centre of view (range 17.97-18.40 s). I count this as **1 blocker**, pending the controller's ruling
   on whether VFX primitives count.

No other blocker category falls in my scope. The clipping (point 2) isn't on the blocker list, but I'd treat it as one.

## Not covered by the spec (gaps to raise, not to patch)
- **A master bus and loudness target:** a limiter or headroom rule, a target loudness for a fight, and how much stacked
  one-shots may sum to. The spec has none, and the build clips.
- **A handling-level rule:** how far below its shot a gun's reload and inspect should sit, and parity between guns.
- **Hit-sound layering:** the spec says "hit markers with layered sound" but sets no cap on how many sounds one shot
  plays, and no rule for which wins.
- **Viewmodel framing:** screen coverage, viewmodel FOV, and that the grip, the hands and the reloaded part (drum or
  cell) must be on screen during a reload.
- **Aim-down-sights:** the Writ's designed sight exists only in the inspect. The spec never says whether a precise
  sidearm aims down sights (or deliberately doesn't).
- **Capture coverage:** the pass has no required segment where each gun's identity verb is shown on a bot (the
  Breacher's shove), and the sync check counts only reloads.
- **Spot pop vs kill ring:** the spec says spots are distinct from ordinary damage, but not that a spot pop must read
  differently from a kill.
