# Skyline Repo: the pipeline (how to run it)

Built as M0 (docs/SKYLINE-PLAN-M0.md, spec docs/SKYLINE-DESIGN.md section 9). First fully green pass: 2026-10-13 07:07.

## Tests (no Unity, no lock)

    powershell -NoProfile -File tools/skyline/test.ps1

This runs:
- **Skyline's pure C# tests**: the `JamKit.Skyline` assembly, `core-tests/Skyline`.
- **The Movement suite**: the gate for the shared engine.
- **Skyline's Unity-side compile check** (`core-tests/SkylineUnity`), so a pass never dies on a typo.

Skyline is excluded from Visiting Hours' `core-tests/UnityLayer*` checks on purpose. One `Compile Remove` line in each keeps
a Skyline error out of their gate.

## The pass (main session only; never from a subagent)

    powershell -NoProfile -File tools/skyline/pass.ps1          # uncapped on purpose (see below)
    powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest # no Unity: the runner's own checks

Four Unity jobs run one after another. Each takes the shared lock in `docs/UNITY-LOCK.md` for that one job
(`tools/skyline/unity-lock.ps1`) and waits for it when the Visiting Hours session holds it.

| Stage | What | How it runs |
|---|---|---|
| A | compile, Skyline EditMode tests, build the three generated scenes (the arena, the firing range, the enemy gallery) | batch editor, through `capped-run` (30% RAM) |
| B | record the arena (60 s), the firing range (45 s) and the enemy gallery (36 s) to MP4 **with audio**, with their directors' scripted demos; a still each at 2 s, and each `SkylineStillCamera` rendered at 1920x1080 after the scene's recording | graphics editor, **uncapped**, after a 3 GB free-memory check, `-silent-crashes`, normal window |
| C | Windows build of Skyline's scenes only (`-buildTarget Win64`; shared build settings untouched) | batch editor, through `capped-run` |
| D | the built player's frame time over 45 s (after a 3 s warm-up) at 1920x1080, measured in the arena (the first scene in the build; `perf.json` names the scene and anything else fails) | the player, **uncapped**, after the memory check, normal window |
| E | the sync check: the firing range recording's onsets against the director's schedule (`sync-check.json`); the marker is found by its 440 Hz beep, and 90% of the detectable reload events must land within 35 ms (a counted sound with no rendered clip fails it; the note gives the chance of a stray match) | no Unity |

- **Why B and D are uncapped:** under capped-run's memory cap the graphics editor crashed natively (`D3D11 device creation failed 0x8007000e`), as the machine rules warn for GPU programs.
- **B and D are silent on the speakers.** The pass holds the Windows per-app audio session volume of the editor (B) and the player (D) at zero (`tools/skyline/mute-app.ps1`, Core Audio via `Add-Type`), retrying until the session exists. Unity's Recorder captures inside Unity, before the device, so the recordings keep their sound (stage E reads it). Windows may remember an app's volume by exe path, so the volume is put back the moment the stage's result file (`capture.json`, `perf.json`) appears, and before a timed-out stage is killed. If a pass dies mid-stage and Unity stays silent: `powershell -NoProfile -File tools/skyline/mute-app.ps1 -RestoreName Unity`. `-SelfTest` covers the wait loop; **verify on the next real pass** that the speakers stay quiet and `audio-firing-range.png` still shows sound.
- **Stage D's window** starts after a 3 s warm-up and once the start cover has begun to lift (the scene's load-time drawings run behind it). `perf.json` also has `over` (frame counts over 16.7/25/33/50/100 ms), `spikes` (the first 80 frames over 25 ms with what had just happened) and `inkOff`; `pass.ps1 -Only D -Out <folder> -PerfArgs '-skyline-no-ink'` is the A/B run that prices the ink passes. See docs/SKYLINE-LOOK.md, perf round 5.
- **If stage A finds compile errors, B-D are skipped.** The errors may be another session's, since the tree is shared. Without the skip, a graphics editor on a broken compile shows a blocking safe-mode dialog.
- **Outputs** land in `out/skyline/<yyyyMMdd-HHmm>/`:
  - `manifest.json`: per-stage ok, seconds and notes;
  - `editor-batch.json`, `capture.json`, `build.json`, `perf.json`: each job's result;
  - `firing-range-schedule.json`: the range director's sound events, in seconds after the marker beep;
  - `sync-check.json`: stage E's result, and `audio-firing-range.png`, the ears' picture of the range's audio;
  - the recordings (`rec-<scene>.mp4`), the stills (`still-<scene>.png`) and the still cameras' shots (`still-<scene>-<shot>.png`).
- **Exit code:** 0 whenever a manifest was written; whether the pass passed is in the manifest's `ok`.
- **Pruning:** captures go in `out/`. Prune old runs after judging, because disk space is tight.

**The shared GameRoot.** It belongs to Visiting Hours and auto-creates in every scene, where it starts a run and applies
and saves the creator's settings. `SkylineSceneGuard` removes it in Skyline's generated scenes before it can act, so
Skyline runs never touch Visiting Hours' `save.json`.

## The sound forge

    cd tools/skyline/soundforge
    uv run python soundforge.py render recipes/test_tone.json -o out.wav
    uv run python soundforge.py timeline <timeline.json> --duration 1.8 -o reload.wav
    uv run --group dev pytest -q

- **Recipes** stack layers: sines with sweeps, white or pink noise, and samples. Each layer has an envelope, gain, a constant-power pan (centre is -3 dB) and filters.
- **Timelines** place recipes at fractions of a duration, so a faster reload rescales its sound events with its animation.
- **Every input error names its layer or event.**

## The ears (a global skill: `~/.claude/skills/ears`)

    uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py measure clip.wav --png clip.png
    uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py compare take.wav reference.wav --png cmp.png
    uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py events reload.wav [--range-db 50]

- **What it is:** measurements, pictures and reference comparison (layers 1-2 of spec 9.5). It reads video through ffmpeg.
- **Not built yet:** the classifier + Gemini judge waits for an API key.
- **Reporting rules** are in its SKILL.md: every claim cites a number or a picture.

## First green pass (2026-10-13, out/skyline/20261013-0707)

- **A:** 28/28 tests.
- **B:** both scenes recorded.
- **C:** build OK.
- **D:** average 1.33 ms, 1% low 3.27 ms at 1920x1080 on the RTX 3050 (empty scenes; the blocker is 16.7 ms).
- **E:** the tone was found (centroid 440 Hz, peak -9.0 dBFS).
- **Forge-to-ears round trip:** `test_tone.json` measured 440 Hz centroid, a 450 ms tail, -9.0 dBFS and no clipping.

## The mix (code: `Assets/JamKit/Templates/SkylineRepo/Scripts/Audio/`, pure half in `Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineMixPolicy.cs`)

The mix pass of 2026-10-14 (the creator: "the sounds just bunch together into a cacophony of harsh sounds"). Identities are untouched;
only how the sounds combine changed. What was wrong, measured on `out/skyline/20261014-full/rec-SkylineArena.mp4` (3 min):

- The recording's limiter log: peak in +1.4 dBFS, deepest reduction 3.4 dB, 918 voices started, 32 loops adopted. In the fights
  (about 58-126 s) the 2-5 kHz band is 13-45% of the energy per 2 s window (3-5% on the traversal before it), 327 onsets above 2 kHz
  in 54-130 s (4.3 a second). The gallery's drone segment (4-10 s) is 16-22% in 2-5 kHz: its telegraph (68% of its energy in 2-5 kHz,
  -13.7 dBFS RMS) and attack repeat every 0.85 s.
- **Barks were the loudest thing in a fight and on no bus.** A bot's babble (square/saw blips) played at 0.9 through the bot's own
  source: no limit on how many at once (a group waking = five at once, about -17 dBFS RMS together) and 14 dB over the player's gun.
- The bots' one-shots measure -10 (drone alert) to -27 dBFS RMS in the WAVs, the guns' -30 to -34; after the old bus (-11 dB) a
  bot's alert/telegraph/attack/hurt was 6-10 dB louder than the Writ's shot in RMS (peaks about equal).
- `player/*` and `concierge/*` fell into the Bots category (-11 dB): the player's own hurt cue was quieter than the bots.
- Every bot's idle loop stayed audible (up to its 25 m rolloff).

What changed (numbers in `SkylineMix.cs`, rules in `SkylineMixRules`):

- **Voice gate** (`SkylineVoiceGate`): per kind at most N at once with a minimum gap, the most audible (nearest) bot wins, a
  newcomer needs 1.5x the audibility of what it displaces: alert 2 (gap 0.2 s), telegraph 3, attack 3, hurt 2, break (plate shed,
  wreck, mine blast, acid) 2, minor 2, six bot one-shots in all; a bot sound repeats no faster than 0.12 s (was 0.04 s), a world
  impact no faster than 0.08 s. **Barks:** one at a time, nearest wins, never while a teaching line is spoken (the bubble still shows),
  at `BarkGain` 0.3 (was 0.9, off every bus), the babble of the four bots low-passed at 3 kHz when synthesized.
- **Beds:** only the nearest 3 idle loops are heard, the rest fade over 0.6 s and are muted; ambience bus -8 dB (was -6) and the
  gondola, spotlight and floor-care beds trimmed (-4, -3, -2 dB).
- **Harsh band:** a broad peaking cut (3.4 kHz, Q 0.6) on every bot one-shot, set per sound on its world voice (`SkylineVoiceEq`):
  -4 dB by default, -5 dB for floor-care alert (37% in 2-5 kHz). The four sounds that were grating on their own were replaced by the
  creator's picks (drone telegraph = rotor spool, floor-care hurt = tank slosh, spotlight hurt = dying bulb, spotlight attack = the
  rifle muffled at 1.5 kHz with static and crackle; 2-5 kHz share 68/54/59/33% down to 0.1/0.0/0.4/3.7%), so they take no cut. Modelled
  on the old WAVs before the replacement: drone telegraph 2-5 kHz level -32.4 to -37.6 dBFS, spotlight attack -34.1 to -42.6.
- **Levels:** Bots bus -12 dB (was -11), the hottest sounds trimmed to a common -21..-23 dBFS RMS before the bus; Guns, Handling,
  Impacts and the new **Feedback** bus (`player/*`, `concierge/*`) at -2 dB (were -4; Feedback was -11).
- **Ducking:** the teaching line ducks bots -8 dB and beds -9 dB (was -6); the player's shot (0.30 s), impact (0.18 s) and hurt cue
  (0.35 s) duck the bots -5 dB and the beds -4 dB (15 ms in, 300 ms out); a playing bark ducks too. The handling never ducks.
- **Logs:** `SKYLINE_MIX ...` now also reports gate drops, barks played/dropped and the sidechain, and a second line
  `SKYLINE_MIX per-sound starts name=count ...` (which sounds crowd a recording).

Offline model (the shipped WAVs mixed through the old and new numbers, the real C# gate run on the same fight script, seven bots,
15 s, 3 seeds): old peak +1.6 dBFS (limiter needing 3.6 dB: the recording's own log said +1.4/3.4), new -6.6 to -6.9 dBFS, no
limiter work; 2-5 kHz RMS -27.4 to -40.2 dBFS; the player's gun 120 ms after a shot was 9-10 dB *under* the bots, now 7-9 dB *over*;
bark audio 19 of 19 played, now 6-8; bot sounds 214 of 221 played, now 136-148. Unit tests: `SkylineMixPolicyTests`.

Compare in the next recording (`rec-SkylineArena.mp4`): 58-70 s (the food court's wake and first exchange), 78-90 s, 104-112 s and 142-148 s
(the loudest limiter moments), against the old 2-5 kHz and peak numbers; the new log's limiter line should read a peak-in well under 0 dBFS.
