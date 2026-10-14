# Skyline Repo M1, round 2: judge E (presentation and performance)

Pass: `out/skyline/20261014-0520/`. My frames, crops, contact sheets and ears pictures are in
`out/skyline/20261014-0520/judge-E/`. Times are video seconds in `rec-SkylineArena.mp4` (170 s). The log still has no
timestamps, so I matched each line to the recording by its order in the log and by the subtitle on screen.

## Round-1 defects

| # | Round-1 defect | Status | Evidence |
|---|---|---|---|
| 1 | The mix clips: -5.9 LUFS, 77k clipped samples | **Fixed** | See the table below. The `SKYLINE_MIX` limiter reports peak-in -1.1 dBFS and 0.01% of the time over 1 dB of reduction; speech was ducked for 55.8 s and the ambience bus runs. |
| 2 | Teaching lines miss their moments | **Partly fixed** | Nothing plays mid-fight long after its moment any more ("dropped" works). But in this recording most of the warm-up lessons are never said at all. See new defect N3. |
| 3 | Subtitles at 26-30 cps | **Fixed** | Reading time is 0.5 s + 15 cps. The 121-character spotlight notice stays up from about 115 s to 120.5 s (`judge-E/end-117.jpg` to `end-120.5.jpg`). |
| 4 | No backing; text under the viewmodel; ammo under the Breacher | **Fixed** | Subtitles are bottom-left on a dark panel with a coloured speaker tag (`judge-E/subs-13.5.jpg`). They clear the viewmodel in every frame I checked. The ammo counter has its own backing and reads over the Breacher (`frames/SkylineFiringRange-024.1s.jpg`, `-030.5s.jpg`: "-- / 4 BREACHER · RELOADING"). The "PLATING"/"CHASSIS" bar labels are tiny and low-contrast (`subs-13.5.jpg`); that is minor. |
| 5 | Text pops on and off | **Fixed** | The code fades and slides text in and out over 0.18 s and 0.32 s (fix report). In the recording, subtitles come and go between my sampled frames with no hard cut visible. |
| 6 | Build doesn't match the editor | **Mostly fixed; one defect remains** | Build shots are DPI-correct at 1920x1080. The HUD, subtitles and bars are fully visible, and the lighting, towers and shops match `still-SkylineArena.png` (`judge-E/sheet-build.jpg`). There are 0 shader errors (`build.json`). **Still magenta:** a solid magenta beam runs beside the gold Writ tracer in `buildcheck/build-shot-08s.png` (2,216 magenta pixels) and `-40s.png` (1,476); crop in `judge-E/build08-beam.png`. The first 60 s of the editor recording, sampled at 10 fps (600 frames), have 0 frames with magenta. So one material on the Writ's shot still goes missing in the build only. |
| 7 | Camera and audio don't sell the beats | **Partly fixed** | Audio and HUD feedback now exist: the fight-start sting and banner ("CONCIERGE · SECURITY NOTICE" at 10 s and 78.9 s), a red damage vignette (`judge-E/end-119.jpg`), and the PA chime before notices. The camera still faces walls at key beats (N4). Hitstop is still off in the demo. |
| 8 | Speech likely masked; the drone mastered hot | **Fixed (by measurement)** | Ducking is active (55.8 s). The mix has headroom, so a voice bus can sit on top. I can't isolate the babble to prove it is heard. |
| 9 | 1% low regressed to 13.46 ms | **Worse** | 1% low **15.88 ms**, average 4.6 ms, worst **28.6 ms**. The ten worst frames are all 19.7-28.6 ms with no GC, all in the food-court fight (`perf.json`). The trend over three passes is 8.85 → 13.46 → 15.88 ms, now 0.8 ms under the 16.7 ms blocker floor. |
| 10 | 3 s freeze at start | **Not fixed** | `warmupWorstMs` is 2972.7 ms. |
| 11 | Perf run skips the heaviest fights | **Not fixed** | The perf log still ends in the food-court fight (`stage-d-perf.log`). The facade fight, the skylight fight and the lift are never measured, though the editor demo now reaches all three. |
| 12 | Recordings break up into blocks in motion | **Worse** | 141 MB for 170 s is about 6.6 Mbit/s at 1080p60, lower than round 1's 8.6. The boost and the doorway run turn to noise (`judge-E/sheet-warmup.jpg`: w-5.5, w-8.5, w-11.5). |
| 13 | Barks cut by the viewmodel; unlabelled bars | **Fixed** | Barks are now cream cards drawn above the viewmodel ("Ooh! A stain!", "Out of... order..." at 117 s). The bars are labelled. |

Ears, round 2 against round 1:

| Recording | Integrated | Sample peak | True peak | Clipped samples | Crest |
|---|---|---|---|---|---|
| Arena | -17.6 LUFS (was -5.9) | -2.29 dBFS (was +1.40) | -2.17 dBTP | **0** (was 77,233) | 21.1 dB (was 12.0) |
| Firing range | -19.9 LUFS | -2.18 dBFS | -2.13 dBTP | **0** (was 410) | 24.0 dB |
| Gallery | -22.6 LUFS | -2.10 dBFS | -2.10 dBTP | **0** (was 3,903) | 22.9 dB |

## New and remaining defects, most severe first

N1. **The recording ends on 46 s of an untextured mint-green void.**
   - From about 124 s to the end (170 s), the screen is a flat mint plane plus the viewmodel and HUD. The camera is
     inside the service lift, the "far goal" (`frames/SkylineArena-127.5s.jpg` to `-163.9s.jpg`, `judge-E/end-124.jpg`).
     At 121.5-122.5 s the lift's shutter shows as a plain white box with the mint showing through
     (`judge-E/end-121.5.jpg`, `end-122.5.jpg`).
   - The audio over this stretch: a steady block at -12.9 dBFS peak from 121 s to 128 s (a single loop), then
     **41 s of near-silence at about -63 dBFS RMS** (`judge-E/ears-Arena.png`).
   - The brief lists the lift's walls and shutter in the lint. On screen, the run's climax is the far goal, and it is
     what the creator watches last: 27% of the recording on a placeholder with no sound.
   - Owner: arena look and layout (the lift), demo player/capture (the recording doesn't end at "exit reached").

N2. **A magenta beam in the build only.** This is round-1 defect 6, continued; see the table above
   (`build-shot-08s.png`, `-40s.png`). The build compiles every shader now, so this is most likely a material or shader
   that isn't included in the build, such as one loaded by name. Owner: Writ FX / build.

N3. **The warm-up teaches neither the boost nor the floor-care unit** (spec 6.8). Lines that played, from the log, in order:

   1. dot-writ-spot-made
   2. nan-warmup
   3. dot-breacher-boost **dropped**
   4. nan-warmup cut
   5. dot-boost-done
   6. nan-warmup dropped
   7. concierge-drone
   8. concierge-floor-care **dropped rearmed**
   9. concierge-gondola
   10. concierge-spotlight
   11. concierge-floor-care **dropped rearmed**
   12. concierge-lift-open

   - **Writ spots:** the demo makes a spot before Nan opens. Dot's reaction "That glowing crack? That's the spot. Shoot
     it!" lands while the spot is up (1.5-3.5 s, `judge-E/sheet-warmup.jpg`), which works. Dot's actual lesson ("Tag its
     lens with the Writ...") is never said, and Dot speaks before Nan's opener.
   - **Breacher boost:** Nan's double-jump and mantle line is never logged. Dot's boost line is dropped. The only boost
     line is the payoff "Ha! Doesn't hurt you, either. Shoot again mid-air..." at about 8.5-11.5 s. By then the player is
     walking through the Galleria doorway, and the fight-start banner arrives at 10 s. Nan's opener "Warm up here" plays
     at 5.5-7 s, while the boost is already happening.

     Cause: the only hint slot during the kiosk moment went to Nan's delayed opener (hints play first come, first said),
     and the kiosk hints were dropped once the top was reached. A real player who lingers would hear them. A player who
     moves at the demo's pace gets no boost lesson.
   - **Floor-care unit:** its safety notice ("operates at ankle height. Guests are advised to jump.") is dropped twice,
     in the food court and in the skylight fight. It is never said, although floor-care units are the most numerous bot
     (six stripped in this run, and a pack of them at 115-120 s). Spec 6.8: every enemy's notice the first time it appears.
   - **Plating:** "Strip it! Scrap's armour!" never plays because the plating never breaks in this run. The stripping
     itself happens six times without a word, and the plating-break presentation (sound, flash, "NO PLATING") goes
     unseen.
   - Owner: HUD/presentation (queue priorities: Nan should open first, and the kiosk hints should outrank the opener at
     the kiosk), demo player (pace).

N4. **The camera still doesn't frame the beats.**
   - CONCIERGE's drone notice plays over a close-up of a floor corner (`frames/SkylineArena-018.2s.jpg`).
   - In the skylight fight the camera is pressed against a wood-grain wall (`frames/SkylineArena-103.2s.jpg`), and it
     faces signs or walls at 54.6 s and 66.8 s.
   - The boost is a close-up of the espresso kiosk's shelves (`frames/SkylineArena-006.1s.jpg`).
   - The spotlight notice again plays over a pack of floor-care units with no spotlight on screen (`judge-E/end-117.jpg`
     to `end-120.5.jpg`). The notice is gated on a bot of that kind being alive, not on it being seen.
   - Owner: demo player/capture, HUD/presentation.

N5. **Performance is heading for the blocker.**
   - The 1% low is now 15.88 ms (round-1 defect 9), with 28.6 ms spikes at the fight start ("food-court to-hide" 28.6,
     after the concierge-drone line 22.8, "group 2 woke" 20.5/20.7).
   - No frame has a GC, so the cost is per-frame work that grows in a fight. Candidates are new this batch: the
     HUD-drawn barks with a line-of-sight check each frame, the damage vignette, the mix's voice cap and ducking, and the
     per-bot wreck debris. I can't profile; this is inference.
   - The heavier facade and skylight fights are unmeasured, so the real 1% low on this floor is likely over 16.7 ms.
   - "Disabling TAA and STP because camera is stacked" is still logged 166 times in the perf run and 125 times in the
     build check.
   - Owner: perf.

N6. **The build check still runs in a window with a title bar.** `buildcheck/build-shot-*.png` show the "VibeJamKit"
   title bar and window buttons, and `buildcheck/player.log` has no `SKYLINE_SCREEN` line. The perf run did go borderless
   (`stage-d-perf.log`: "doesn't fit: borderless full screen at 1920x1080"). The HUD isn't cut off in these shots, so
   this is minor, but the build check doesn't show what a double-click gives. Owner: build/capture.

N7. **Minor:**
   - The "PLATING"/"CHASSIS" labels are about 9 px, grey and orange on a dark grey panel (`judge-E/subs-13.5.jpg`), and
     barely legible.
   - The fight-start banner is small and sits top-centre, far from where the player's eyes are during a fight
     (`frames/SkylineArena-078.9s.jpg`).

## Scores (8 is the bar)

| Category | r1 | r2 | Why |
|---|---|---|---|
| 11. Presentation (overall) | 3 | **5** | HUD, subtitles and mix are now near the bar. Held back by the 46 s mint void at the end, the magenta beam in the build, the missing boost and floor-care lessons, and the camera facing walls. |
| – HUD and text | 4 | 8 | Backings, fades, a readable reading speed, labels, ammo over the Breacher |
| – Teaching beats on time (6.8) | 2 | 4 | Nothing plays late any more; but the boost lesson, Nan's jump line and the floor-care notice never play |
| – Camera and audio selling beats | 3 | 5 | Sting, banner, chime, hurt and vignette added; the camera still frames walls, and hitstop is off |
| – Overall mix | 2 | 8 | -17.6 LUFS, 0 clipped samples, -2.2 dBTP, ducking, an ambience bus. The last 41 s are dead air (N1). |
| – Sense of place | 6 | 7 | Brighter, varied shops and towers, an ambience bed. The far goal is an untextured void. |
| – Build matches editor | 3 | 7 | Matches, except the magenta beam |
| 12. Performance | 6 | **5** | 1% low 15.88 ms: passes the floor by 0.8 ms, fails the 8.3 ms target, and worse for the second pass running; 28.6 ms fight-start spikes; 3 s start freeze; the heavy fights are unmeasured |

## Spec 10 blockers I see: 2

1. **An untextured placeholder in view:** the service lift's mint interior and its white shutter fill the screen for the
   last 46 s of the arena recording (`frames/SkylineArena-127.5s.jpg` to `-163.9s.jpg`). It is known in the lint, but
   it is in view.
2. **An untextured placeholder in view (build):** the magenta beam in `buildcheck/build-shot-08s.png` and `-40s.png`.

Not blockers:
- **1% low:** 15.88 ms is under 16.7 ms. Given the trend and the unmeasured fights, I expect it to cross next pass
  unless it is fixed.
- **Clipped text:** I found none.

## Not covered by the spec (gaps to raise)

- **Perf:**
  - no rule that the perf demo covers the heaviest encounter (still only the food court)
  - no limit on the start-up freeze (2.97 s, excluded from the window)
  - no rule on per-batch regressions (8.85 → 13.46 → 15.88 ms)
- **Teaching priorities:** the spec says who teaches what, and where, but not what to do when two lessons compete for
  one moment. Here Nan's opener beat the kiosk lessons at the kiosk, and Dot's reaction beat Nan's opener. Nor does the
  spec say whether a notice the player never heard ("dropped rearmed" twice) counts as taught.
- **CONCIERGE notices:** the spec says "the first time that bot appears", not whether it must be on screen. The gate is
  "alive in a fight", so notices play over the wrong bots.
- **Recordings:**
  - no bitrate floor (6.6 Mbit/s breaks up in motion)
  - no rule to end the recording when the run ends (46 s of void)
  - hitstop is still off in every recording, so judges never see it
- **The build check:** no rule that it launches the way a player's double-click does (borderless) and not in a titled
  window.
