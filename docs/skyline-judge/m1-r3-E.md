# Skyline Repo M1, round 3 (final): judge E (presentation and performance)

Pass: `out/skyline/20261014-0625/`. My frames, contact sheets and ears pictures are in
`out/skyline/20261014-0625/judge-E/`. Times are video seconds in `rec-SkylineArena.mp4` (180 s). The log has no
timestamps, so I matched each line to the recording by its order in the log and by the subtitle on screen.

Correction to round 2: my round-2 reply gave performance as "6 → 4". The round-2 report says 5, and 5 is the right
figure.

## Round-1 defects, status in round 3

| # | Round-1 defect | Status | Evidence |
|---|---|---|---|
| 1 | The mix clips (-5.9 LUFS, 77k clipped samples) | **Fixed** | See the ears table below. The limiter reports peak-in +1.0 dBFS and 3.0 dB deepest reduction, over 1 dB of reduction only 0.43% of the time. Speech was ducked for 77.8 s. Arena RMS holds at -20 to -29 dBFS in every 5 s window from 0 to 180 s, with no dead air. |
| 2 | Teaching lines miss their moments | **Mostly fixed; new stutter (N2)** | Dot's Writ lesson plays with the practice bot in view (0.3-5 s), and "That glowing crack?" follows as the spot opens (7.5 s). Nan's jump/mantle line plays at the practice counter (12.5-17 s). Dot's boost line plays at the kiosk (25-28 s), and "Ha!" plays after the boost (32 s). Every M1 lesson is now said in its place (`judge-E/sheet-warmup.jpg`, `frames/SkylineArena-032.1s.jpg`). |
| 3 | Subtitles at 26-30 cps | **Fixed** | Lines stay up for their reading time; the 116-character drone notice is shown at 41 s and still at 45 s. |
| 4 | No backing; text under the viewmodel | **Fixed** | Bottom-left panels with speaker tags, clear of the viewmodel in every frame I checked. The ammo counter reads over the Breacher (`frames/SkylineFiringRange-024.1s.jpg`, `-033.7s.jpg`). |
| 5 | Text pops on and off | **Fixed** | Fades and slides. No hard cut seen in the sampled frames. |
| 6 | Build doesn't match the editor | **Fixed** | 0 magenta pixels in all 3 build shots (round 2: 2,216 and 1,476). `build-shot-08s.png` matches `still-SkylineArena.png`. The HUD is complete and there are 0 build errors. One minor gap remains, N7. |
| 7 | Camera and audio don't sell the beats | **Partly fixed** | Audio and HUD feedback are in place: sting and banner at fight start (41 s), chime, hurt sound and vignette. The demo now watches each bot kind: the floor-care unit is in frame for its notice (`judge-E/e-162.jpg`), as is the gondola (109 s). The camera still sits against walls and signs for long stretches (N4). |
| 8 | Speech masked; the drone mastered hot | **Fixed (by measurement)** | Ducking, headroom, an ambience bus. |
| 9 | 1% low over target | **Worse: now a blocker** | 1% low **18.0 ms** (> 16.7 ms). The fights are the problem: food court 19.73 ms, facade 18.07 ms, skylight 18.21 ms (cut off). The warm-up is 9.62 ms. See N1. |
| 10 | 3 s freeze at start | **Not fixed** | `warmupWorstMs` is 2957 ms. The new fade-in start may hide it from the player, but I can't confirm that from these files. |
| 11 | Perf run skips the heaviest fights | **Mostly fixed** | Phases are now measured per fight: two fights complete and the skylight cut off at the 150 s cap (`demoComplete: false`). The skylight's 1% low (18.2 ms) covers only its first 14 s. |
| 12 | Recordings break into blocks in motion | **Fixed** | 512 MB for 180 s, about 22.8 Mbit/s. Fast motion is clean (`judge-E/sheet-warmup.jpg`). |
| 13 | Barks, labels | **Fixed** | Cream bark cards above the viewmodel; labelled bars. |

Ears, round 3:

| Recording | Integrated | Sample peak | True peak | Clipped samples | Crest |
|---|---|---|---|---|---|
| Arena | -18.2 LUFS | -1.53 dBFS | -1.43 dBTP | **0** | 21.0 dB |
| Firing range | -18.8 LUFS | -2.32 dBFS | -2.26 dBTP | 0 | 23.2 dB |
| Gallery | -22.5 LUFS | -2.32 dBFS | -2.18 dBTP | 0 | 24.0 dB |

## Remaining and new defects, most severe first ("5 min" = a player notices it in the first five minutes)

N1. **[5 min] Performance fails in every fight, with one 0.2 s freeze** (`perf.json`, a build at 1080p, PC quality,
   render scale 1).
   - **Overall:** 1% low 18.0 ms, against the 16.7 ms floor and the 8.3 ms target.
   - **Per fight:** food court 19.73 ms, facade 18.07 ms, skylight 18.21 ms. The warm-up alone passes at 9.62 ms.
   - **Worst frame:** a **197.3 ms hitch at 50.1 s, right after CONCIERGE's floor-care notice** starts (14 bots, 1 wreck,
     no GC). The player sees a visible freeze in the first fight, at the moment a notice starts.
   - **Spikes:** ten more of 21-25 ms through all three fights, at 5-15 bots and up to 29 wrecks.
   - Average 4.9 ms, so the cost is in spikes and the slow tail, not the steady state.
   - The coordinator says this is being fixed in parallel. Judged on this file, it fails.
   - Owner: perf (the hitch: likely the notice's first-use work, i.e. the chime, the voice or the banner; that is my
     inference).

N2. **[5 min] Lines stutter: Nan's opener is said four times, and lines cut each other and restart.**
   - The log has "nan-warmup" said 4 times, cut 5 times and dropped once. On screen, "Unit's live. Warm up here, then the
     Galleria's through the far doors." appears at about **10 s, 22 s, 35 s and 38 s** (`judge-E/w-10.jpg`, `w-22.jpg`,
     `w-35.jpg`, `w-38.jpg`). The last time, the player is already at the Galleria doorway.
   - In the food court, CONCIERGE's drone notice is cut by a repeat of Dot's "That glowing crack? That's the spot. Shoot
     it!" (45 s, said over a close-up of a sign, `frames/SkylineArena-045.0s.jpg`), then restarts. The log reads
     "concierge-drone cut", then "dot-writ-spot-made", then "concierge-drone".
   - "concierge-next-fight" is cut twice, and "concierge-lift-open" and "concierge-floor-care" are each said and then
     cut.
   - Why it matters: the queue's "a cut line goes back to the front" rule replays whole lines. Players hear the same
     opener repeatedly, and the text box keeps resetting.
   - Owner: HUD/presentation (teaching queue). Don't replay a line that has been up for more than about 60% of its
     reading time; let it finish instead.

N3. **[5 min] Untextured pieces fill the screen during a lesson and at the ending.**
   - Nan's jump/mantle lesson plays with the camera close up on the practice counter's flat beige face for about 5 s
     (12.5-17 s, `judge-E/w-12.5.jpg` to `w-17.jpg`).
   - The service lift's plain walls are in view at the end (`judge-E/e-172.jpg`).
   - Both are among the five untextured pieces the brief says are in the lint. They are less glaring than round 2's
     46 s mint void, but they are in view, and the counter is the first thing the player is taught on.
   - Owner: arena look.

N4. **The camera still frames walls through most of the fights.** The demo player faces walls or point-blank signs at
   19.3, 45.0, 57.9, 70.7, 83.6, 135.0 and 147.9 s: seven of the fourteen pack frames (`judge-E/sheet-a.jpg`,
   `sheet-b.jpg`). This is the recording, not the game, but the creator judges from it. Owner: demo player/capture.

N5. **[5 min] The floor-care notice arrives two fights late.** Floor-care units first appear in the food court
   (around 41 s; the log has "concierge-floor-care dropped rearmed" in that fight). The notice is said only in the
   skylight fight (about 160-164 s), with a floor-care in frame (`judge-E/e-162.jpg`). Spec 6.8: "the first time that bot
   appears". The perf build said it at 50 s, in the food court, so the timing depends on the run; in the recording it is
   two fights late. Owner: HUD/presentation (queue: a notice for a bot on screen should outrank a repeated hint or
   reaction).

N6. **A flat ending.** The lift-open line is said at about 175 s, as the player is already sliding under the shutter
   (`judge-E/e-175.jpg`, `e-177.jpg`). The "vista" is a look back at the hall from inside a plain lift
   (`judge-E/e-179.5.jpg`). There's no sting, banner or music cue for clearing the floor. Owner: arena layout,
   HUD/presentation.

N7. **The build check still runs in a titled window.** `buildcheck/build-shot-*.png` show the title bar and window
   buttons, and `buildcheck/player.log` has no `SKYLINE_SCREEN` line. The perf run went borderless. Nothing is clipped.
   Owner: build/capture.

N8. **Minor:** the "PLATING"/"CHASSIS" labels are about 9 px and low-contrast. Dot's "That glowing crack?" is said again
   in the food court as if it were new.

## Scores (8 is the bar)

| Category | r1 | r2 | r3 | Why |
|---|---|---|---|---|
| 11. Presentation (overall) | 3 | 5 | **6** | HUD, subtitles, mix and build parity are at the bar. Held back by the line stutter, the late floor-care notice, untextured pieces in view, the camera on walls and the flat ending. |
| – HUD and text | 4 | 8 | 8 | |
| – Teaching beats (6.8) | 2 | 4 | 6 | Every lesson is now said at its place, but Nan repeats 4 times, lines cut and replay, and the floor-care notice is two fights late |
| – Camera and audio selling beats | 3 | 5 | 6 | Sting, banner, chime, hurt and vignette; bots watched for their notices. Half the fight frames are walls; no end-of-floor cue. |
| – Overall mix | 2 | 8 | 8 | -18.2 LUFS, 0 clipped, -1.4 dBTP, ducking, no dead air |
| – Sense of place | 6 | 7 | 7 | Glass hall, varied shops, ambience. Untextured counter and lift. |
| – Build matches editor | 3 | 7 | 9 | No magenta; matches the stills |
| 12. Performance | 6 | 5 | **3** | 1% low 18.0 ms, over the 16.7 ms floor; all three fights over 18 ms; a 197 ms freeze; the 2.96 s start freeze unchanged |

## Spec 10 blockers: 2

1. **1% lows over 16.7 ms in a build:** 18.0 ms overall, 19.73 ms in the food court (`perf.json`).
2. **An untextured placeholder in view:** the practice counter fills the screen during Nan's lesson (12.5-17 s), and the
   lift's walls are in view at the end (172 s). Both are known in the lint, but they are in view.

## For the creator's short list: what a player notices in the first five minutes

1. Stutters in every fight, and a visible 0.2 s freeze when CONCIERGE's floor-care notice starts.
2. Nan's opening line repeats four times, and lines get cut off and restart.
3. The first jump lesson is staged against an untextured counter face.
4. The floor-care unit's safety notice doesn't come in the first fight where floor-care units appear.

The HUD, subtitles, mix and build fidelity no longer stand out.

## Not covered by the spec (gaps to raise)

- **Line interruption:** no rule for what happens when a line is cut, so whole lines replay. The spec should say a line
  replays once at most, and never after most of it has been seen.
- **Hitches:** no limit on a single frame in a build. A 197 ms freeze passes any 1% low rule if it happens once.
- **Start freeze:** no limit (2.96 s, excluded from the window), and no check that the fade-in covers it.
- **Perf scope:** the perf demo stops at a 150 s cap before the skylight fight ends. No rule says it must finish.
- **End of floor:** no rule for how clearing the floor is presented (sting, music or banner at the lift).
- **The build check:** no rule that it launches the way a player's double-click does (borderless), not titled.
