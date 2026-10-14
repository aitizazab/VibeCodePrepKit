# Skyline Repo: work log

Newest first. Every agent result and every Unity pass gets an entry (what was done, numbers, paths, decisions needed).

## 2026-10-13 early morning: M0 done (first green SkylinePass at 07:07)

- **Final whole-branch review (Opus): ready with fixes.** Two critical findings:
  - Skyline broke Visiting Hours' core-tests compile gate (their globs picked up SkylineRepo).
  - The recording editor crashed natively under the memory cap.

  One fix wave addressed everything (commits 11c3ebe, bf1ed0f, 4ae6f96, b3b1d29, a1693a5), and a re-review found
  nothing new broken. The Visiting Hours session agreed to the two `Compile Remove` lines and wasn't blocked: its
  overnight runs skip that step.
- **First Unity runs:**
  - **Pass 1** found a compile break in Visiting Hours' DreamHouseBuilder.cs (a missing render-pipeline reference).
    That session fixed it at our report (9daceca).
  - **Pass 2:** stage A was green, then the graphics editor crashed under the cap.
  - **Pass 3 (out/skyline/20261013-0707): all green.**
    - A: 28/28 tests in Unity.
    - B: both scenes recorded, with audio.
    - C: the build succeeded.
    - D: average 1.33 ms, 1% low 3.27 ms at 1920x1080, RTX 3050.
    - E: the 440 Hz tone was found in the recording.
- **Forge-to-ears round trip:** `test_tone.json` measured a 440 Hz centroid, a 450 ms tail, -9.0 dBFS and no clipping.
- **M0 exit criteria (spec 11.1): all met.**
  1. The assembly and tests build; Visiting Hours' tests are unchanged and their gate is fixed.
  2. The decision log and the Ledger's recording side exist, with tests.
  3. The headless combat sim exists.
  4. SkylinePass runs end to end with a manifest.
  5. The forge renders and the ears report.
- **The creator's XXL voice run:** paused at 06:31 at their request, so Skyline's pass could use the GPU and memory.
  Restarted at 07:33 once the pass was done.
- How to run everything: docs/SKYLINE-PIPELINE.md. Technical decisions are logged in the spec's section 14.

## 2026-10-13 (later): M0 being built (plan docs/SKYLINE-PLAN-M0.md, one agent per task, reviewed task by task)

- **Done and reviewed:**
  - **The Skyline C# assembly:**
    - its own assembly and test project (`tools/skyline/test.ps1`; 40 Skyline tests, and Movement's 201 still pass);
    - the decision-event log;
    - the Ledger's recording side (combat-only; wall use measured by opportunity);
    - the aim models and the spotlight's telegraphed shot, plus the never-mix rule;
    - frame stats.
  - **The headless combat sim and dodge report.** The first measurement was misleading: the jinking bot moved towards and away from the shooter, which barely changes where a leading shot must go. The bot was fixed to reverse across the line of fire, averaged over 5 seeds.
    - Result: a straight-line bot takes 12 hits a minute from one leading drone, and a jinking bot 2.0.
    - The ratio is 6.0, against M1's target of about 2. M1 tunes it with real encounters (enemies on 2+ bearings).
    - Also for M1: as tuned, the spotlight's lag lets straight running dodge it too.
  - **The sound forge** (`tools/skyline/soundforge`, 17 tests). Review fixes: a true constant-power pan (it had a 3 dB jump at hard pan), and every bad input now gives an error that names the layer or event.
  - **The ears skill** (`~/.claude/skills/ears`, 17 tests): measurements, pictures, reference comparison and sound-event timing. Review fixes:
    - the event detector no longer mistakes bass ripple for hits (it found 10 events in a 3-shot clip);
    - a video with no audio track gives a clean error;
    - out-of-phase stereo is detected;
    - comparing against a silent take says so.
- **In progress:** the Unity lock helper (a race fix so two sessions can't both take a dead lock), the Unity recording/build/perf jobs (under review), then the `SkylinePass` runner and the first Unity pass.
- Free memory was 2.1 GB at this point, so the first pass waits for memory through the cap.

## 2026-10-13

- **Design brainstorm with the creator, finished.** Six sections were approved one by one and written up as
  `docs/SKYLINE-DESIGN.md` (the spec), which now waits for the creator's review.
  - **Player and cast:** the player pilots a repo bot from the family firm's van, and everything in the building is a bot.
  - **The party:** the hires are AIs with personalities.
  - **Weapons:** the Bailiff kit plus district prototypes.
  - **CONCIERGE's Ledger:** the run Ledger resets each run, the Guest Profile persists, and habits are scored 60% against
    the room and 40% against players at the same difficulty.
  - **Baja, the secret boss:** six predictors, Gullible on Easy/Normal and Clever on Hard.
  - **Rooms:** 16 normal rooms and 8 special ones.
  - **Build order:** milestones M0 to M4.
- **Research: "ears" for judging sound.** A subagent compared four layers: measurements, spectrogram images, local
  classifiers (CLAP, Audiobox Aesthetics) and Gemini over the API.
  - Gemini downsamples audio and mixes it to mono (by its docs; reading this as about 8 kHz is unverified).
  - Local audio-LLMs don't fit the 6 GB RTX 3050.
  - **The creator's calls:**
    - Layers 3 and 4 combine into one judge that reports confidently only when both agree.
    - Comparing against references is the main tool.
    - Budget is $5-10 a month on a Gemini API key; the key still has to be created by them.
    - The ears get built as a global skill.
- **The spec was reviewed independently instead of by the creator** (they were busy, and it's about 7,200 words).
  - A fresh agent checked it against the brief, the creator's rules and the code, and found 11 technical or clarity
    problems. All are fixed and logged in the spec's section 14.
  - The ones that mattered most:
    - Batch mode mutes audio, so audio captures now come from a windowed built player.
    - The spec carried a stale 60 fps bar; it's now the creator's 120 fps / 16.7 ms 1%-lows rule.
    - The engine edits are listed, with a test gate.
    - Skyline gets its own assembly.
    - M0 and M1 have exit criteria.
  - 7 questions for the creator are listed in section 14, with defaults used until they answer.
- **Next:** the implementation plan for M0 and M1; then stop until after the hackathon.

## 2026-10-13 (afternoon): M1 headless tasks

- **Done and reviewed:** Tasks 7-12, 9b (the window-washer gondola), CC (the creator's calls: recovered scrap, Writ
  spots 3/3/5, Breacher limited only by fire rate) and 11b (big Normal fights with a headless FightSim where the player
  shoots back). The SDD ledger (`.superpowers/sdd/SKYLINE-PLAN-M1/progress.md`) has every commit and ruling.
- **11b's balance, 30 seeds per fight, a bot with human timing:** clean (no chassis lost) 28/30 food court, 30/30
  skylight, 30/30 facade; standing in the open wrecks in 2.4-4.2 s; the no-cover control always fails. Dodge band on
  400 held-out seeds: drones at 12 m 2.43, spotlights 1.90 (target 1.6-2.5).
- **Parked for the creator's play (Task 24):** the skylight and facade are gentle for a skilled player (plating pressure
  tests ignored as gaps); the drone band's interval tops 2.55; floor-care is fast (5 m/s, sweeps every 1.2 s).
- **The creator's calls today** are in the spec's section 14, including per-floor music (comical to serious; floor 1 a
  Subway Surfers reimagining, rights question pending).
- **Next:** Task 13 (scenes and the pass) is with an implementer; Tasks 14-17 are Unity, run by the main session.

## 2026-10-13 (evening): M1 code complete through Task 16

- **Done and reviewed:** Task 13 (scenes, pass, still cameras, sync check, contracts), Task 14 (the player and both
  guns, the firing range showcase), Task 15 (the four bots incl. the gondola; FightSim is the behaviour reference;
  cracked-casing spots; sparking wrecks; the gallery), Task 16 (the arena from `HallCover()`, three fights started from
  the entry zone, the teaching-line queue, the demo with a watchdog, the lint). Nothing has run in Unity yet.
- **The ears** judge is done (CLAP + Gemini, measurements overrule claims); the secrets guard is the rewritten .mjs, live.
- **Budget:** the creator raised the stop to 15% weekly. Stopped at 6% for a PC restart.
- **Next:** Task 17, the main session's Unity pass (metas, stage A, recordings, the checklists in task-13..16-report.md).

## 2026-10-14 (just after midnight): Task 17 done, the first floor is playable in grey-box

- **Pass 1** (20261013-1850) failed perf and the sync check. Two fixes by ownership: the range director timed on
  `unscaledTime` while the recording follows `Time.time` (dropped Breacher presses, sync looking 1.3 s late); the arena
  demo never cleared the food court, and the perf window counted the warm-up hitch. Plus a missing-sound cache.
- **Pass 2** (20261014-0003, after waiting ~2 h for free memory) is green on every stage: 286 tests, all three fights
  cleared in the demo, 1% low 8.85 ms at 1080p (target 16.7), 14/14 reload sounds within 35 ms of their frames.
- **Still missing** (content, Tasks 18-21): bot models (primitives), faces, bot and hit sounds, the Writ's spot sounds.
- **Build to play:** `out/skyline-play/2026-10-14-greybox/SkylineRepo.exe` (opens on the arena).
- **The Writ** takes the courtroom diorama direction (round 4 refines it); the clockwork heart is kept for another gun
  (`docs/SKYLINE-CONCEPTS.md`). The PopularMMOs intro is Spag Heddy's "Pink Koeks".

## 2026-10-14 (overnight): content, three judge rounds, the first floor ready to play

- **Content (Tasks 18-21):** the Writ in the creator's courtroom design (round 5, take 1: the round-window sight), the
  shared robot arms, both guns' effects and sounds; the four bots' models, faces, sounds and barks; the arena's sunny
  white-and-gold mall look. Then a moving drone patrol, idle work for the floor-care unit and the gondola, a human-like
  demo player.
- **Judges (Task 23), three rounds:** scores went from 3-6 to 6-8. Big finds: the build ran Visiting Hours' quality
  level at 80% resolution; a stale shader cache gave magenta materials in the build; every wall rendered at half
  brightness (detail maps imported as sRGB); the mix clipped (now a limiter, -18 LUFS, 0 clipped); one cover piece won
  every fight (now a route: food court, facade, skylight, then the service lift).
- **Final pass** (20261014-0800): every stage green; 1% low 14.7 ms (worst fight 16.3), all three fights cleared.
- **Build to play:** `out/skyline-play/2026-10-14-m1/SkylineRepo.exe`. Lines to rewrite:
  `docs/SKYLINE-DRAFT-LINES-OVERNIGHT.md`. Judge reports: `docs/skyline-judge/`.
- **Memory:** whole Unity passes kept being stopped for low memory while the session was idle; `pass.ps1 -Only` now runs
  the stages one at a time.
