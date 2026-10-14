# Skyline Repo M1, round 2: judge C (gun feel)

**Pass:** `out/skyline/20261014-0520/`. My frames and pictures are in `out/skyline/20261014-0520/judge-C/`, named by
scene and time.

**Range timing:** recording time = schedule time + 1.989 s (the marker, `sync-check.json`).

**How the sounds were measured:** I re-measured every gun sound file with the ears (`listen.py measure|compare`), plus
three of my own probes:
- clipping and level per 10 s window of each recording;
- the level of each of the range's 62 scheduled events in the recording;
- `listen.py events` on the Writ's from-empty reload.

## Round-1 defects

| # | Round-1 defect | Now | Evidence |
| --- | --- | --- | --- |
| 1 | The Breacher's shove is in no capture | **Fixed (partly)** | The range ends with three blasts on a live practice drone, 35.5-41.5 s (`judge-C/range-shove-35.5-41.5s-tile.jpg`, `range-shove-shot-36.95s-tile.jpg`). Log: `slid 0.62 / 0.43 / 0.44 m, plates shed 0/0/1`. A hit marker shows on the drone, and a plate flies on the third shot (about 39.0-39.5 s). But the slide reads small: the camera kick moves the view about three times as far as the drone moves (frames 37.00-37.55 s). The arena still uses the Breacher only for the kiosk boost (`judge-C/arena-gun-strip-1fps.jpg`: the orange gun is out about 4-8 s only, in a 170 s recording). See new defect N3. |
| 2 | Every recording clips | **Fixed** | 0 clipped samples in all three recordings. Range: -19.9 LUFS, -2.1 dBTP. Arena: -17.6 LUFS, -2.2 dBTP. Gallery: -22.6 LUFS, -2.1 dBTP. No 10 s window in the arena peaks above -2.3 dBFS. `SKYLINE_MIX limiter` reports a deepest reduction of 1.1-2.0 dB, over 1 dB at most 0.21% of the time. |
| 3 | The Writ shot has no snap, and the hydraulic return is buried | **Fixed, with one artefact** | `writ/shot`: presence + air is 2.9% (was 0.2%); centroid 464 Hz (was 367); there is energy up to 16 kHz through 0.9 s; the room tail is -20 dB at 1.0 s and -35 dB at 2.0 s (`judge-C/writ-shot-vs-ref-candidate.png`). `shot_mech` is now 88% presence (centroid 4.0 kHz, peak -1.3 dBTP), in a band the shot's body doesn't fill, so it should cut through. In level alone (-15.2 dB RMS in the recording against -15.6 just before it at 3.47 s) I can't prove it is heard. The artefact is new defect N4. |
| 4 | Reloads and equips can't be seen | **Fixed** | Writ partial reload (6.4-7.9 s, `judge-C/range-writ-reload-partial-6.4s-tile.jpg`, `range-writ-reload-6.85s-crop.jpg`): the gun cants up, the scroll column and the left arm are on screen. Writ from-empty reload (14.3-16.3 s): the same, plus the rack. Writ equip (2.3-3.2 s): the gun rises and the gavel flies in after it. Breacher from-empty reload (30.0-32.1 s, `judge-C/range-breacher-reload-empty-30.0s-tile.jpg`): the gun rolls, and the left robot hand comes up under the receiver for the cell. |
| 5 | Arms missing at the hip, crude where shown | **Mostly fixed** | Writ at the hip (`frames/SkylineFiringRange-001.6s.jpg`): a robot hand (white plates, steel knuckles) on the scroll column, with a hazard-yellow forearm. With the Breacher (`-020.9s.jpg`) the right hand is on the grip, but the left arm is still a bare orange pole with ring joints, and no hand shows on the pump at the hip. Note: the sheet's forearm is "tow-truck orange with a hazard band at the wrist"; in-game the whole forearm is yellow with black slashes. |
| 6 | The Writ reads as a building block-out, not a pistol | **Fixed** | At the hip it now reads as a pistol at a glance: grip, slide, muzzle arch (`frames/SkylineFiringRange-001.6s.jpg`, `SkylineArena-139.6s.jpg`). The chair has gothic spires, a cross, lancet windows and a medallion. The flat brass slab is gone. In arena light it holds up (`judge-C/arena-writ-flash-1.53s-crop.png`). It covers about 20% of the screen width, against about 25% before. |
| 7 | Two or three hit sounds a shot; the "dark" hit rings | **Fixed** | `SkylineShooting.cs:43-101` plays the gun's own impact; `impact/hit` is only the fallback. A Writ shot plays at most two sounds (the hit plus spot_form or spot_break). Log: `hit sounds dropped (one a shot) 0`. But the Breacher's one hit sound is now inaudible: new defect N2. |
| 8 | The Breacher's effects use placeholder spheres | **Fixed** | The muzzle flash is an orange fireball with flame petals (37.00 s). The smoke and dust are soft particles. The blast ring is a warm, soft ring by the muzzle (`judge-C/range-shove-shot-36.95s-tile.jpg`). It still overlaps the target at 6.5 m (frames 4-6), but it fades in 0.2 s. |
| 9 | Only the reloads were sync-checked | **Not fixed** | Still 14 of 62 events are checked. 13/14 now match: `writ/ReloadEmpty/cell_in@12.933` misses. My onset probe finds it 44 ms after its cue: the sound is there (peak -4.2 dB at 14.92 s), but its 28 ms soft attack lands it just past the 35 ms window. My level probe finds all 30 distinct scheduled sounds well above the bed at their times. |
| 10 | The Writ's handling is 18-25 dB under its shot | **Fixed** | RMS over 120 ms: Writ shot -15.5 dB; mag_out -29.3 (13.8 dB under); seat -25.3 (10.2 under); bolt -27.3. Breacher blast -15.5 dB; its handling runs -23.9 to -28.3 (8-13 under). The two guns now match. `writ/seat` is 56% low and 41% presence + air (a thump and a hiss). |
| 11 | The Writ's effects are hard to read at range | **Fixed in the arena; worse in the range** | Arena (1.53 s, `judge-C/arena-writ-flash-1.53s-crop.png`): an amber flash with rays, glowing tracers and a gold hit marker, which reads well. Range: brass sparks and a scorched disc now show on the plate (3.40-3.65 s, `judge-C/range-writ-marker-check-3.36s.jpg`), but the flash is a flat, hard-edged, opaque pale-yellow disc: new defect N1. |
| 12 | Spots lost at range; the pop looks like the kill | **Fixed** | Practice drone at about 10 m (`judge-C/arena-practice-spot-form-vs-pop.jpg`): the spot forms as a small gold ring with sparks under the lens (2.30 s). The pop is a double gold ring burst (2.85 s). They are distinct and read clearly. |
| 13 | The practice bot's spot lesson fails | **Fixed** | Log: `writ-shot 1`, `dot-writ-spot-made`, then `writ-spot-shot`. |
| 14 | No hit marker on Writ hits in the range | **Not fixed** | There is still no marker in any of 18 frames after the Writ hits a plate (3.36-3.65 s). The Breacher's marker shows on the drone (37.0 s), and the Writ's shows on bots in the arena (1.53 s). |
| 15 | Capture problems | **Not fixed (not my area)** | The arena kiosk segment, 5.4-7.0 s, is still encoder mush (`judge-C/arena-boost-5.4-7s-tile.jpg`). The Breacher's boost there can't be judged. |

## New defects, most severe first

### N1. The Writ's muzzle flash is a flat, opaque yellow disc in the firing range (Writ, effects; range scene)
- **What:** at 3.39 s (`judge-C/range-writ-flash-3.39s-crop.png`) the flash is a hard-edged, solid pale-yellow circle
  with flat white rectangular rays. It is about 170 px across at 1080p and lasts three frames (`judge-C/range-writ-shot1-3.34s-tile.jpg`).
  The same flash in the arena (1.53 s) is a soft amber glow. So the effect relies on the arena's HDR grade and bloom,
  and the range camera renders it as a clipped flat shape.
- **Why it matters:** the firing range is where the creator tries each gun first. There, the Writ fires a
  placeholder-looking disc on every shot.
- **Owner:** Writ (make the flash hold up without bloom), or the range scene (give it the arena's post stack).

### N2. The Breacher's only hit sound is inaudible (Breacher, hit feedback, audio)
- **What:** with the one-sound rule, a Breacher hit plays only `breacher/impact`. That clip is 99.6% in the low band,
  with a centroid of 103 Hz and 0.2% presence. The sheet's "nine hard ticks spread over 25 ms" don't show in the
  measurement. It sounds in the same instant as the blast, which is 72% in the same 60-250 Hz band at a higher level,
  so it is masked. On laptop speakers, 100 Hz is near silence anyway.
- **Why it matters:** a Breacher shot that lands sounds the same as one that misses. Only the marker tells you.
  Before this round, the dropped `impact/hit` tick at least carried it.
- **Owner:** Breacher (give `impact` an audible transient in the 2-5 kHz band).

### N3. The shove is shown only in the range, and reads small (Breacher, demo player/capture)
- **What:** the arena's 170 s demo never fires the Breacher at a bot (see round-1 defect 1). In the range the slide is
  0.4-0.6 m a shot. The identity sheet's own fantasy is "a bot slides about a body length". Under an 8° camera kick,
  the drone's slide is hard to see (36.95-37.55 s).
- **Why it matters:** the Breacher's identity verb is still the least visible thing in the captures.
- **Owner:** demo player (a Breacher beat in a fight), Breacher (whether 9 m/s should read bigger on a drone).

### N4. The Writ shot's high air cuts off abruptly at 0.9 s (Writ, sound)
- **What:** in the spectrogram (`judge-C/writ-shot-vs-ref-candidate.png`) everything above about 3 kHz stops in one
  frame at 0.9 s. The new "air" stem ends hard instead of fading. It sits about 20 dB under the peak, but it falls
  inside the 1.0 s window where the next shot's own transient doesn't always land: 250 RPM is one shot every 0.24 s,
  but single shots are common.
- **Why it matters:** on headphones, a single shot's shimmer stops with a click-like edge.
- **Owner:** Writ (fade the stem's end).

### N5. The Breacher's reload parts are mostly hiss (Breacher, sound)
- **What:** `cell_in` is 72% air (centroid 11.2 kHz), `seat` is 77% air (centroid 11.3 kHz), and `cell_out` is
  38% air. The sheet promises "a clack with high steel modes ... a low hydraulic thump", but the seat's low band is
  0.7%.
- **Why it matters:** the "heavy cell" seats with a thin hiss, and the reload has no weight. This was the same in round 1;
  I only flagged the Writ's seat then.
- **Owner:** Breacher.

### N6. The from-empty Writ reload's cell_in lands 44 ms after its cue (Writ, handling)
- **What:** the one sync-check miss (see round-1 defect 9). The 28 ms attack of `cell_in` makes it read late against
  the animation. The partial reload's `cell_in` matched.
- **Owner:** Writ (a sharper onset, or move the event earlier).

### Notes (not defects in my categories)
- The arena recording's last 47 s (about 123-170 s) is a static view of a flat mint-green surface at -62 dBFS
  (`frames/SkylineArena-139.6s.jpg`, the lift). It's for the presentation and capture judges.
- In the range, the Breacher's blast ring sits on the drone at 6.5 m (frames 4-6 of `range-shove-shot-36.95s-tile.jpg`).
  It is soft and short now, so it's acceptable.

## Scores (1-10; 8 is the bar)

| Category | Round 1 | Round 2 | Why |
| --- | --- | --- | --- |
| Looks (models, sights, arms) | 4 | **7** | The Writ now reads as the creator's courthouse pistol, with the grip and hand on screen. The sight picture is still clean. The Breacher is still a plain brick, and its left arm is a pole. |
| Sounds | 4 | **6** | No clipping; the Writ shot has snap and a room; the handling of the two guns is balanced. But the Breacher's hit is inaudible, its reload is hiss, and the Writ's air ends abruptly. |
| Numbers and handling sets | 6 | **7** | Reloads and equips are visible and complete. The sync check is 13/14 and still covers only reloads. |
| Effects | 4 | **6** | Breacher flash and smoke fixed; Writ impacts and spots read; the flat disc in the range; the shove reads small. |
| Hit feedback | 5 | **6** | One sound a shot, and markers on bots. The Breacher's hit is silent in practice; still no Writ marker on range plates. |
| **Bar 10.7, gun feel (overall)** | **4** | **6** | |

## Spec 10 blockers I see
- **None firm.** Last round's grey-sphere placeholder is fixed.
- N1 (the flat disc) is placeholder-looking but range-only and three frames long. I list it for the controller and
  don't count it.

## Not covered by the spec (gaps to raise, not to patch)
- **The range's look:** the spec doesn't say whether the firing range must use the arena's post stack. Effects tuned
  for bloom break without it (N1).
- **Hit-sound audibility:** the spec sets "one hit sound a shot" only through this round's rule. It sets no
  requirement that the hit sound be audible over its own gun's shot, for example a minimum level in 1-5 kHz (N2).
- **Knockback readability:** how far a shove must move a bot on screen, per bot size and camera kick, isn't specified (N3).
- **Sync-check coverage:** still reloads only. Equip, fire/mech/vent, dry fire and inspect aren't checked, and the
  range's shove segment isn't in the schedule at all.
- **The arms' colours:** the Writ's sheet (orange forearm, hazard band at the wrist) differs from the shipped arms (a
  hazard-yellow forearm). The spec doesn't pin the shared arms' look.
