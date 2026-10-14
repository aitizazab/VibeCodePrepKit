# Skyline Repo M1, round 1: judge E (presentation and performance)

Pass: `out/skyline/20261014-0326/`. My frames, crops, contact sheets and ears pictures are in `out/skyline/20261014-0326/judge-E/`.
Arena recording times are video seconds (`rec-SkylineArena.mp4`). The `SKYLINE_*` lines in `stage-b-capture.log` carry no
timestamps, so I matched each line to the recording by its order in the log and by the subtitle on screen.

## Defects, most severe first

1. **The mix clips nearly all the time and is far too loud.**
   - Evidence: `listen.py measure rec-SkylineArena.mp4` gives integrated loudness **-5.9 LUFS**, sample peak **+1.40 dBFS**,
     true peak +1.53 dBTP, **77,233 clipped samples** and a crest factor of only 12 dB. The peak is over 0 dBFS in about
     95 of the 120 one-second windows (ffmpeg astats). RMS sits at -6 to -10 dBFS through every fight (10-32 s, 39-52 s,
     55-80 s, 102-115 s). In `judge-E/ears-Arena.png` the waveform is pinned at ±1 for whole fights. The gallery measures
     -9.8 LUFS with 3,903 clipped samples, constant over-full-scale peaks while the drone is on stage (3-8 s), and peaks up
     to +1.9 dBFS. The firing range measures -15.5 LUFS with 410 clipped samples: **a single Breacher shot with nothing
     else playing goes over 0 dBFS** (+0.5 to +0.8 dBFS at 17.9, 18.9, 19.9, 26.1, 27.0 and 27.9 s).
   - Cause: each source file is fine on its own (peaks -1 to -3 dBFS). The problem is the sum. There is no AudioMixer,
     limiter, ducking or bus gain: `SkylineSounds.PlayAt/PlayViewmodel` plays every one-shot at volume 1.
   - Why it matters: the player hears harsh digital distortion in every fight, and the layered gun sounds that
     section 2.3 calls "phenomenal" turn into a smeared wall of sound.
   - Owner: audio mix.
2. **The teaching lines miss their moments.** Spec 6.8 says Dot teaches the Writ's spots at the practice bot and the boost
   at the kiosk. Neither happens there.
   - **Writ spots:** the demo fires three Writ shots at the practice bot at about 1-4 s, while Nan's general warm-up line
     plays (ammo 7, 6 then 5 of 8 in `judge-E/sheet-early.jpg`). The log then says `writ-no-spot`, so the practice bot
     never grew a spot. Dot's "Practice bot, dead ahead! Tag its lens..." only plays at **about 11-15 s**, after the player
     has gone through the doorway into the Galleria and is facing a wall (`judge-E/arena-12-subs.jpg`, `arena-13.jpg`).
   - **Boost:** the boost and mantle happen at about 7.5-9 s, under Nan's double-jump line (`judge-E/sheet-beats.jpg`).
     Dot's "Breacher kicks both ways... There's scrap on top of that kiosk" plays at **25 s, in the middle of the
     food-court fight**, with the Writ out and the player facing a wall (`frames/SkylineArena-025.0s.jpg`). "Ha! One boost
     a jump" follows a few seconds later.
   - **Spot made:** "That glowy bit? That's the spot. Shoot it!" plays at **about 64.6-65 s, after that bot is already a
     wreck** saying "Out of... order..." (`frames/SkylineArena-065.0s.jpg`, `judge-E/b-64.6.jpg`).
   - Cause, from the code:
     - Both of Nan's lines trigger on `WarmupStart` (`Encounters.cs` TeachingBeats), so they fill the first ~10 s.
     - `TeachingQueue` plays one line at a time and lets combat notices jump the queue.
     - `FirstSpotCreated` isn't treated as combat, so the spot line waits behind the spotlight notice.
     - A hint is only dropped when it is more than 20 s stale. Dot's boost line was queued at about 7 s and played at
       25 s, just inside that window.
   - Why it matters: this is the M1 bar for teaching. A player who acts on these lines is told to do things they already
     did, or to shoot a spot that no longer exists.
   - Owner: HUD/presentation (teaching queue), with the demo player (`writ-no-spot`).
3. **The subtitles show too briefly to read.**
   - Evidence: `TeachingQueue.LineSeconds` is length / 15 chars per second, but clamped to **at most 4 s**. Most lines are
     105-121 characters, so they are shown for 4 s at **26-30 characters per second**:
     - nan-warmup: 114 characters, 28.5 cps
     - dot-writ-spots: 119 characters, 29.8 cps
     - concierge-spotlight: 121 characters, 30.2 cps
   - That is about twice the code's own 15 cps reading rate. The babble voice is cut to the same 4 s
     (`SkylineBabble.Speak` → `Cap`).
   - Why it matters: in a fight, a player can't read 120 characters in 4 s, and CONCIERGE's safety notices are the only
     way each bot is taught.
   - Owner: HUD/presentation.
4. **Subtitles and HUD text are hard to read and covered by the viewmodel.**
   - Text style: the subtitle and HUD labels are bold white or yellow with no shadow, outline or backing panel
     (`SkylineHud.Build`). On the sunlit white floor and walls the speaker name ("Dot" in yellow) almost disappears
     (`frames/SkylineArena-025.0s.jpg`, `-065.0s.jpg`), and so does the line itself (`judge-E/b-64.6.jpg`).
   - Subtitles under the viewmodel: the subtitle sits bottom-centre, straight over the viewmodel. "ledge's lip to mantle
     up" and "practice" vanish into the yellow Breacher (`judge-E/arena-6.5-subs.jpg`). "with the Writ and it leaves a
     weak... hurts more than the lens" runs across the Writ's body (`judge-E/arena-12-subs.jpg`).
   - HUD under the viewmodel: the Breacher covers the ammo counter in the range ("1 / 4" under the gun at 28.1 s, and the
     white "BREACHER" label on the orange gun at 20.6-43.1 s, `judge-E/sheet-range.jpg`). The reload "--" and the "WRIT"
     label are unreadable on bright floors (`frames/SkylineArena-115.0s.jpg`, `-105.0s.jpg`).
   - Why it matters: the bar's "no clipped text" rule is about text the player can't read. This text is drawn but can't
     be read.
   - Owner: HUD/presentation (Breacher viewmodel placement for the range overlap).
5. **Text pops on and off.** `SkylineHud.Say` and its `Update` switch the speaker and line between `DisplayStyle.Flex`
   and `None`, with no fade or slide. Section 10.11 says "no text pops". Every subtitle in the recordings appears and
   disappears in a single frame. Owner: HUD/presentation.
6. **The build doesn't match the editor recordings** (`buildcheck/build-shot-*.png` against the arena stills and frames,
   `judge-E/sheet-build.jpg`).
   - **Magenta error-shader materials in view:**
     - the Writ's tracer and lens beam, and the practice bot's bark bubble (`build-shot-08s.png`)
     - floating magenta shards around the food court (`build-shot-40s.png`)

     In the editor the tracer is gold and the bubble is pale grey. `stage-c-build.log` has 289 errors, "Shader error in
     'Universal Render Pipeline/Lit': **out of memory while parsing**", on transparent, alpha-premultiply and emission
     variants. This goes beyond the accepted "probe-volume variants, but it renders": transparent Lit materials don't
     render in the build.
   - **The far towers are flat white blocks** in the build (`build-shot-08s.png`, `-40s.png`). In the editor they are
     blue glass with reflections (`still-SkylineArena.png`, `frames/SkylineArena-045.0s.jpg`).
   - **HUD and subtitles cut off at the bottom of the screen:**
     - The build runs windowed, with a title bar, in a window as large as the 1920x1080 display.
     - The screenshot shows no plating or chassis bars and no ammo counter.
     - At 20 s Dot's speaker name sits on the very bottom pixel row and the line itself is below the visible area
       (`judge-E/build20-bottom.png`).

     So the build the creator plays shows clipped text and loses the HUD. I couldn't confirm this beyond the screenshots,
     because I'm not allowed to launch the build.
   - Owner: perf/build (shader compile under the memory cap; window mode), arena look (towers).
7. **The camera and audio don't sell the beats.**
   - Fight start: the food-court fight starts (log: encounter start, groups 0/1 woke) while the camera faces a pink
     poster and a wall (`judge-E/arena-12.jpg`, `-13.jpg`). There's no fight-start sting and no music.
   - The boost: the camera is a few centimetres from the "KIOSK" and "ESPRESSO" signs (`judge-E/b-7.6.jpg` to
     `b-8.2.jpg`). On screen it reads as bumping into a sign, not as a rocket hop.
   - Plating breaks: when the Unit's plating breaks, the only reaction is a 0.6 s bar flash and a red wedge.
     `SkylinePlayer` (around line 325) plays no hit sound, no break sound and no shake for incoming damage.
   - Kills: the demo turns hitstop off (`SkylineArenaDemo.cs:124`, and in `SkylineRangeDirector.cs:78`), so no recording
     shows the kill hitstop that the spec counts on.
   - CONCIERGE's notices have no PA chime and play with no bot of that type on screen:
     - the drone notice at about 15 s plays over a close-up of the CONCIERGE sign (`frames/SkylineArena-015.0s.jpg`)
     - the spotlight notice plays over a pack of floor-care units (`judge-E/b-62.6.jpg` to `b-63.8.jpg`)
   - Owner: HUD/presentation, audio mix, demo player/capture.
8. **The speech is likely masked.**
   - The line voices are synthesized babble at volume 0.26-0.42 (`SkylineBabble.cs:28-34`), with no ducking. They play
     over a bed at -6 to -10 dBFS RMS that is already clipping. For example, the concierge-drone and concierge-floor-care
     notices at about 15-20 s play over peaks of +0.6 to +0.9 dBFS.
   - The drone's own sounds are mastered about 8-10 dB hotter than every other bot's: alert mean -10.1 dB, hurt -12.9 dB,
     telegraph -13.7 dB, against -18 to -27 dB for the others.
   - The Writ's handling sounds sit at -35 to -42 dB mean (mag_out -41.6, cell_in -38.8, bolt -36.2), so reloads in a
     fight will be inaudible.
   - I can't isolate the babble from the mixed track, so whether it can be heard is unproven. The level numbers make
     masking very likely.
   - Owner: audio mix.
9. **The 1% low regressed to 13.46 ms, well over the 8.3 ms target.**
   - `perf.json`: average 3.9 ms (good), 1% low 13.46 ms, worst frame 21.6 ms.
   - The previous pass (`out/skyline/20261014-0003/perf.json`) measured average 2.55 ms and 1% low 8.85 ms over the same
     ~45 s window: **+4.6 ms on the 1% low in one batch**.
   - Six of the ten worst frames are at or over 16.7 ms. They all fall on beats:
     - the mantle (21.6 ms, with a GC)
     - fight start, "food-court to-hide" (21.0 ms) and "group 1 woke" (16.8 ms)
     - repeated "food-court engage" (16.2-17.7 ms)

     So the hitches land exactly when a fight starts.
   - `stage-d-perf.log` prints "Disabling TAA and STP because camera is stacked" 197 times in about 50 s. The viewmodel's
     camera stack turns off TAA (visible shimmer risk on the skylight mullions), and the player build logs the message
     every few frames.
   - Owner: perf.
10. **The game freezes for about 3 s at start.** `warmupWorstMs` is 2957.7 ms (2919 ms in the previous pass). The probe
    skips the first 3 s, so this freeze never counts against the 1% low, but the player sees it every time the scene
    starts. Owner: perf.
11. **The perf run doesn't cover the heaviest content.** The perf demo's log ends in the food-court fight
    (`stage-d-perf.log`). The skylight fight never runs: five or more floor-care units plus drones, acid puddles, the
    densest moment in `frames/SkylineArena-055.0s.jpg`-`065.0s.jpg`. Neither does the facade fight. The 1% low is
    measured on the easy half of the arena. Owner: perf / demo player.
12. **The recordings break up into compression blocks during fast motion.** `rec-SkylineArena.mp4` is 128 MB for 120 s
    (about 8.6 Mbit/s at 1080p60). During the boost the frames turn into blocks (`judge-E/b-7.9.jpg` to `b-8.8.jpg`), and
    the creator judges from these recordings. Owner: demo player/capture.
13. **Smaller issues:**
    - The world-space bark bubbles are drawn under the viewmodel and get cut by it ("Ooh! A stain!" at 63.4 s, "Out of...
      order..." at 64.6 s). In the gallery the bark at 28.4 s ("Lights on you. Smile.") is too small to read.
    - The HUD bars are unlabelled, so the player can't tell plating (blue) from chassis (orange). Dot's line "Scrap's
      armour" teaches a word the HUD never shows.
    - At 85 s a sign's underside shows a smeared black band (`frames/SkylineArena-085.0s.jpg`). Owner: arena look.

Seen in passing, for the encounters judge: in the skylight fight, drones are stripped as it starts (`stripped
mall-cop-drone#12/#13` right after `encounter skylight start`) and CONCIERGE's spotlight notice plays in the same fight.
Check this against the rule that drones and the spotlight never share an encounter.

## Scores (8 is the bar)

| Category | Score | Why |
|---|---|---|
| 11. Presentation (overall) | **3** | Lines miss their moments; text pops, reads at about 29 cps and has no backing; the mix clips; the build loses the HUD and shows magenta |
| – HUD and text | 4 | The layout is clean, but nothing makes it readable and it overlaps the viewmodel |
| – Teaching beats on time (6.8) | 2 | Only CONCIERGE's notices land roughly on time; all three of Dot's gun lessons miss |
| – Camera and audio selling beats | 3 | Fight start, the boost and plating breaks aren't sold; hitstop is off in the recordings |
| – Overall mix | 2 | -5.9 LUFS, 77k clipped samples, no mixer, limiter or ducking |
| – Sense of place | 6 | The skylight, towers, signage and shopfronts read as a glass mall; there's no ambience bed, music or PA, and no life |
| – Build matches editor | 3 | Magenta materials, white towers, HUD and subtitles off-screen |
| 12. Performance | **6** | Average 3.9 ms passes; 1% low 13.46 ms passes the floor but fails the 8.3 ms target and regressed 4.6 ms; 21 ms hitches on fight start; 3 s freeze at start; the heaviest fights aren't measured |

## Spec 10 blockers I see: 2

1. **Clipped text:** in the build, the subtitle is cut off at the bottom of the screen and the HUD is off-screen
   (`buildcheck/build-shot-20s.png`, `judge-E/build20-bottom.png`). This rests on the screenshots alone; I couldn't
   launch the build to confirm.
2. **An untextured placeholder in view:** the magenta error-shader materials in the build (`buildcheck/build-shot-08s.png`,
   `-40s.png`).

Not a blocker: the 1% low is 13.46 ms, under 16.7 ms in a build.

## Not covered by the spec (gaps to raise)

- **Subtitles:** no reading-speed standard (the code's 15 cps conflicts with its 4 s cap), no contrast or backing
  standard, and no rule that subtitles must stay clear of the viewmodel.
- **Audio:** no loudness target or headroom rule (for example -16 to -14 LUFS integrated, true peak ≤ -1 dBTP), no
  limiter on the master, and no ducking of SFX under speech.
- **Sound of the place:** no ambience bed, music, fight sting or CONCIERGE PA chime. Section 2.3 covers guns only.
- **Player damage feedback:** no hit or plating-break sound and no camera reaction. The spec lists feedback for the
  player's shots only.
- **Perf scope:**
  - no rule that the perf demo covers the heaviest encounter
  - no limit on the start-up freeze (warm-up worst is excluded)
  - no rule on per-batch regressions
- **The build:** no rule for the window mode (fullscreen against windowed at the display's size). The build pipeline's
  shader compile runs out of memory under the 30% cap, and no rule says a build with shader errors fails.
- **Recordings:** no bitrate or quality floor, though the creator judges from them. Hitstop is turned off in every
  recording, so judges never see it.
- **Teaching:** no rule for what happens to a warm-up hint whose moment has passed. A hint that is more than about 5 s
  late should be dropped, not played mid-fight.
