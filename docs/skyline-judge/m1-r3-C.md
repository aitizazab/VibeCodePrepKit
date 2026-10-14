# Skyline Repo M1, round 3 (last): judge C (gun feel)

**Pass:** `out/skyline/20261014-0625/`. My frames and pictures are in `out/skyline/20261014-0625/judge-C/`.

**Range timing:** recording time = schedule time + 1.989 s.

**How the sounds were measured:** the changed sound files with the ears (`listen.py measure`), plus my own probes of
the recordings (clipping and loudness, each scheduled event's level).

## Status of every earlier defect

| From | Defect | Round 3 | Evidence |
| --- | --- | --- | --- |
| r1-1 / r2-N3 | The Breacher's shove is never shown, and reads small | **Fixed** | **Arena:** the demo now uses the Breacher in fights (`food-court breacher-on`, `skylight breacher-on` twice; the orange gun is out at about 46-51 s and 154-165 s, `judge-C/arena-gun-strip-1fps.jpg`). At point blank on floor-care units (46.2-48.0 s, `judge-C/arena-breacher-shot-46.2s-tile.jpg`) you see the fire flash, chips and dust, gold lids flying, the bot sliding back, and the lurch tipping it. **Range:** the drone slides 0.88 / 0.77 / 0.70 m (was 0.4-0.6) and tips visibly (36.95-37.75 s, `judge-C/range-shove-shot-36.95s-tile.jpg`). No plate shed on the range drone this time (`plates shed 0`). |
| r1-2 | Clipping | **Fixed** | 0 clipped samples. Range: -18.8 LUFS, -2.3 dBTP. Arena: -18.2 LUFS, -1.4 dBTP. Gallery: -22.5 LUFS, -2.2 dBTP. Limiter: deepest reduction 3.0 dB, over 1 dB 0.43% of the time. |
| r1-3 / r2-N4 | The Writ shot is dark, the mech buried, and the air cuts off at 0.9 s | **Fixed** | `writ/shot`: 2.8 s, presence + air 2.5%, centroid 464 Hz. The tail falls smoothly to -80 dB by 2.75 s with no edge in the spectrogram (`judge-C/writ-shot.png`). `shot_mech` is 88% presence. |
| r1-4 | Reloads and equips can't be seen | **Fixed** (as of round 2) | The Breacher's reload in the arena shows both robot hands working the cell (`judge-C/arena-breacher-48.6s.jpg`). |
| r1-5 | Arms | **Fixed** | Both guns have both robot hands on screen, in hazard yellow and orange (48.6 s). |
| r1-6 | The Writ reads as a block-out | **Fixed** (as of round 2) | The courthouse pistol, with the grip in hand. |
| r1-7 | Two or three hit sounds a shot | **Fixed** | Log: `hit sounds dropped (one a shot) 0`. |
| r2-N2 | The Breacher's hit is inaudible | **Fixed** | `breacher/impact`: 97% presence, centroid 2.8 kHz, -14.1 LUFS (was 103 Hz, 99.6% low). It sits in the band the blast leaves open. |
| r1-8 | The Breacher's placeholder spheres | **Fixed** (as of round 2) | |
| r1-9 / r2-N6 | Sync | **Fixed** | `sync-check.json`: 14/14 within 35 ms (median 9.5, worst 19.2). The Writ's `cell_in` now has a 0.6 ms attack (was 28). It still checks only the reloads (14 of 62 events); my level probe finds all 30 distinct scheduled sounds above the bed at their times. |
| r1-10 | Handling level | **Fixed** | RMS over 120 ms: Writ shot -15.1 dB, reload and inspect -23.9 to -31.0. Breacher blast -12.3 dB, handling -22.2 to -32.7. |
| r1-11 / r2-N1 | The Writ's flash: hidden, then a flat disc in the range | **Fixed, but now small** | In the range (3.39 s, `judge-C/range-writ-flash-3.39s-crop.png`) it is a soft amber glow and streaks: no disc, no hard edges. In the arena (6.75 s, `judge-C/arena-writ-shot-6.2s-tile.jpg`) it is a small amber burst at the arch, and the glowing tracer carries the shot. See R1 below. |
| r1-12 | Spots | **Fixed** (as of round 2) | In the range the Writ now forms a spot on the plate's bullseye (3.40-3.60 s): a gold ring. |
| r1-13 | The practice bot's spot lesson | **Fixed** | `writ-shot 1..3`, then `writ-spot-shot`. |
| r1-14 | No Writ marker on range plates | **Fixed** | A yellow weak-point X shows at 3.39 s (`range-writ-flash-3.39s-crop.png`). The cause was missing scripts on the plates' hit zones (fix report). |
| r2-N5 | The Breacher's reload is mostly hiss | **Fixed** | `seat`: centroid 470 Hz, 63% mid, 29% low, 1.4% air (was 77% air). `cell_out`: 49% low. `cell_in`: 78% presence, 8% air (was 72% air). |

## Remaining defects, most severe first

None of these are blockers. For the creator, I mark which ones a player would notice in the first five minutes.

### R1. The Writ's muzzle flash is now small (Writ, effects): first five minutes, yes
- **What:** the round-2 fix traded the hard disc for a soft sprite. In the arena (6.75 s) the flash is a small amber
  burst about the size of the arch, behind the gatehouse, for three frames. The hit marker and the glowing tracer are
  what you actually see. In round 2's arena the flash was a big ray burst (`out/skyline/20261014-0520/judge-C/arena-writ-flash-1.53s-crop.png`).
- **Why it matters:** the Writ is a hand cannon at 250 RPM. Its spec wants the visuals to "breathe" between shots, and
  the shot now looks lighter than it sounds.
- **Owner:** Writ (flash scale and brightness).

### R2. For the creator's ears: the Breacher's hit crunch may be louder than its blast (Breacher, sound): first five minutes, likely
- **What:** `breacher/impact` is -14.1 LUFS over its 0.4 s, while the 2.5 s blast is -18.6 LUFS. It is pressed with
  tanh (crest 14.8 dB, the lowest of any gun sound), and it plays at full volume on every shot that lands, at point
  blank in every Breacher fight.
- **Why it matters:** the hit should confirm the blast, not cover it. A pressed 2.8 kHz crunch on every shot may tire
  the ear. Measurement can't settle this; the creator's ears decide.
- **Owner:** Breacher (or a -3 dB trim in the mix).

### R3. The Breacher still looks like a plain orange brick (Breacher, look): first five minutes, yes
- **What:** it hasn't changed since round 1 except the redder orange. It is box forms with one hazard plate and a grey
  end block (`judge-C/arena-breacher-48.6s.jpg`). Next to the Writ's detail it reads as a placeholder. The
  identity sheet's toothed breaching standoff is a yellow ring, and the cell is mostly out of view.
- **Why it matters:** the two guns don't look like they belong to one art direction: a detailed diorama pistol and a
  block-out shotgun.
- **Owner:** Breacher (model). Its identity (the shove) is now clear in play; this is look only.

### R4. The range drone sheds no plate (Breacher, effects): first five minutes, no
- **What:** round 2 shed one plate in three shots; this pass sheds none (`plates shed 0`). In the arena, plates do fly
  (gold lids at 46.5-47.5 s). Only the range's showcase misses it.
- **Owner:** the range's shove segment (Breacher).

### R5. The sync check still covers only the reloads (capture): not seen by a player
- 14 of 62 events. Equip, fire, mech, vent, dry fire and inspect are unchecked, and the shove segment isn't in the
  schedule.

### Notes outside my categories
- In the arena the demo stares at a blank sandstone wall from about 10 to 18 s while a teaching line plays
  (`judge-C/arena-4-20s-tile.jpg`).
- Two floor-care units overlap each other at 48.6 s (`arena-breacher-48.6s.jpg`).

## Scores (1-10; 8 is the bar)

| Category | Round 1 | Round 2 | Round 3 |
| --- | --- | --- | --- |
| Looks (models, sights, arms) | 4 | 7 | **7** (the Breacher is still a brick) |
| Sounds | 4 | 6 | **8** (every measured fault fixed; R2 is taste) |
| Numbers and handling sets | 6 | 7 | **8** |
| Effects | 4 | 6 | **7** (the Writ's flash is small; the shove now reads) |
| Hit feedback | 5 | 6 | **8** (one audible sound a shot, markers on plates and bots, distinct spots) |
| **Bar 10.7, gun feel (overall)** | **4** | **6** | **7.5** |

## Spec 10 blockers in my scope
**None.**

## Not covered by the spec (gaps to raise, not to patch)
- **A shared art bar for weapons:** the spec sets each gun's identity, but not a common level of model detail. The
  Writ and the Breacher now differ visibly (R3).
- **A hit-sound loudness rule:** how loud a hit sound may be against its own gun's shot (R2).
- **Muzzle-flash size per gun class** (R1).
- **Sync-check coverage:** beyond reloads, and including any scripted segment.
