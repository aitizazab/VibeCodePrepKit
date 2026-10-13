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
| A | compile, Skyline EditMode tests, build the generated scenes | batch editor, through `capped-run` (30% RAM) |
| B | record the firing range and the enemy gallery to MP4 **with audio**, plus a still each | graphics editor, **uncapped**, after a 3 GB free-memory check, `-silent-crashes`, normal window |
| C | Windows build of Skyline's scenes only (`-buildTarget Win64`; shared build settings untouched) | batch editor, through `capped-run` |
| D | the built player's frame time over 20 s at 1920x1080 | the player, **uncapped**, after the memory check, normal window |
| E | the ears check that the firing-range recording carries the 440 Hz test tone | no Unity |

- **Why B and D are uncapped:** under capped-run's memory cap the graphics editor crashed natively (`D3D11 device creation failed 0x8007000e`), as the machine rules warn for GPU programs.
- **If stage A finds compile errors, B-D are skipped.** The errors may be another session's, since the tree is shared. Without the skip, a graphics editor on a broken compile shows a blocking safe-mode dialog.
- **Outputs** land in `out/skyline/<yyyyMMdd-HHmm>/`:
  - `manifest.json`: per-stage ok, seconds and notes;
  - `editor-batch.json`, `capture.json`, `build.json`, `perf.json`: each job's result;
  - the recordings, the stills, and the audio check's JSON and picture.
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
