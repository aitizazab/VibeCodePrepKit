# Skyline Repo M1 (combat feel) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build M1 of the spec: one arena, the Writ and the Breacher with full handling sets, three bots (mall-cop
drone, floor-care unit, spotlight), the engine's opt-in outside-push hook, and the dodging measurement on Normal, then
take it to the Skyline bar through judges and the creator's play session.

**Architecture:** Everything that can be pure C# is pure C# in `JamKit.Skyline` and tested with `dotnet test` first
(handling timelines, the gun state machine, weak spots, the Breacher's boost rules, enemy bodies and brains, encounter
rules, the dodge harness). The one engine change (a push hook on `MoveSim`, plus an input hook on
`MovementController`) is opt-in and gated by the Movement suite and the Dream Escape / Deep Block route times. Unity
code under `Assets/JamKit/Templates/SkylineRepo/` only plays what the pure systems decide (viewmodel poses and sounds
from the timelines, bots from their brains), built from code each pass and verified by one `SkylinePass` per batch.
Content (models, VFX, layered sounds, the arena's look) comes from identity agents measured against SkylinePass
captures and reference clips with the ears.

**Tech Stack:** C# 9 (Unity 6000.3's language), NUnit 3 (Unity runs 3.5: only `Assert.That`, `Is.*`, `Does.*`,
`Assert.Throws`, `Assert.Ignore`, `[Explicit]`), .NET 10 SDK for `core-tests`, Unity 6000.3.25f1 + URP 17.3 + Input
System 1.20 + Unity Recorder 5.1.7, Python 3.12 via `uv` (the sound forge, the ears), PowerShell 5.1, ffmpeg, yt-dlp.

**Spec:** `docs/SKYLINE-DESIGN.md`. M1 is section 11 and 11.2. Read sections 2 (combat), 6.8 (teaching map, M1 rows),
8 (engine changes), 9 (how it's built), 10 (the bar) and 14 (changes; the pending questions and their defaults) before
starting. The pipeline how-to is `docs/SKYLINE-PIPELINE.md`; the M0 plan `docs/SKYLINE-PLAN-M0.md` shows the
conventions; M0's deferred notes are in `.superpowers/sdd/SKYLINE-PLAN-M0/progress.md`.

**Dry run (2026-10-13, while writing this plan):** the pure C# and engine code of Tasks 2-12 was extracted from this
document into a scratch copy (never the repo), compiled with the repo's own sources, and tested: 137 Skyline tests pass
(M0's included), the forge files round-trip, the first-cut sounds render, the synccheck tests pass, and
the Movement suite with the push hook passes 197 of 198 (the one left is `SharedTuningTests`, ignored outside the repo), all 9 `OutsidePushTests` included. The dodge numbers in Task 11 and the onset times in Task 5 are that run's measurements.

## Amendments (controller rulings after an independent plan review, 2026-10-13) — these override the task text

The review re-ran the chain in its own scratch copy: every task's tests pass at its own point (Skyline 149/149 at the
end), and Movement is 210/210 with the hook, SharedTuningTests included (the "197 of 198" above was a dry-run artifact).

1. **Commits:** every commit is `git commit -m "..." -- <the same explicit paths>` (path-limited), never a bare
   `git commit` after `git add`, so another session's or agent's staged files can never be swept in. Content agents
   (Tasks 18-21) do NOT commit: they hand their file list back and the main session commits.
2. **Task 12 (engine edits in shared files):** before touching MoveSim.cs / MovementController.cs, `git diff HEAD --stat`
   on both must be empty; prepare the edits in a scratch copy, run the gate there, then copy both files over in one step
   and commit immediately; afterwards `git show --stat HEAD` must list only those files. The controller notifies the
   Visiting Hours session before the copy-over. Prefer the smaller shared edit: drop `allowOutsidePush` and `AddPush`
   from MovementController (Skyline sets `Sim.OutsidePushEnabled` / calls `Sim.AddPush` itself); keep only the opt-in
   `InputOverride` there. `AddPush` returns a bool (false when the engine drops it: mantle or ignored states), and the
   Breacher's boost charge is spent only when it returns true.
3. **Engine gate:** `engine-gate.ps1` also runs `--filter SharedTuningTests` and requires Passed 1, Skipped 0.
4. **Sync check (Tasks 13/14/17):** the range director timestamps with `Time.unscaledTime` and the demo disables hit
   stop; the marker is found by its 440 Hz energy (not `onsets[0]`); detectability is judged from the forge-rendered
   clips, and only reload events count toward the 90%.
5. **Encounter geometry:** bearings and the dodge harness use each zone's ENTRY point as the anchor (`EncounterDodge`
   translates spawns by the anchor); re-tune so every encounter has >= 90° spread from its entry; report the new ratios.
6. **Clean line:** add a scripted clean-line route per encounter with a headless test showing zero hits on Normal; if a
   route can't be found, record it as a gap for the creator rather than weakening the test.
7. **Parallel content agents:** split shared data into per-gun and per-bot files so no two agents edit one file; CREDITS
   lines are collected by the main session; each agent that builds tests passes its own
   `-p:BaseIntermediateOutputPath=<unique>` so `core-tests/*/obj` isn't contended.
8. **asmdef references:** Task 13 adds `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime`
   where Tasks 14 and 21 need them; the main session runs stage A alone right after Task 13 and again before Tasks 14-16
   are committed, so a Unity compile error never sits on the shared main.
9. **Perf window:** stage D measures the arena (start the perf demo at the arena entry, or measure ~45 s covering it).
10. **Missing-sound check:** Task 14's "no SKYLINE_SOUND missing" covers `writ/` and `breacher/` only until Task 20.
11. **Aim assist:** the demo includes a `lookFromPad` segment logging `SKYLINE_AIMASSIST target turnScale`, checked in
    Task 17.
12. **Teaching:** add Nan's warm-up beat teaching the double jump + mantle the kiosk ledge needs (spec 6.8 row 1).
13. **Design calls taken by the planner** (Writ spots consumed on hit, max 3, 6 s; the spotlight beam sweeping at its
    lock speed; the dodge band 1.6-2.5 with a 3° drone aim error and floor-care excluded; walls open in M1;
    strip-by-touch; a wrecked Unit restarts the warm-up) stand as tunable data defaults and go to the creator in the
    morning report, before Task 14.

## Global Constraints

- C# 9 only: no file-scoped namespaces, no `global using`, no records, no nullable annotations, no implicit usings.
- NUnit 3.5 APIs only: `Assert.That(x, Is.EqualTo(y).Within(z))`, `Is.InRange`, `Does.Contain`, `Assert.Throws<T>`,
  `Assert.Ignore`, `[TestCase]`, `[Explicit]`. Nothing from NUnit 3.10+ (no `Assert.Multiple`).
- Pure Skyline C# goes in `Packages/com.vibejam.jamkit/Runtime/Skyline/` (assembly `JamKit.Skyline`, references only
  `JamKit.Core`, `noEngineReferences: true`); its tests in `Packages/com.vibejam.jamkit/Tests/Skyline/`. Never add
  Skyline to `core-tests/All` (spec 9.1).
- Unity-side code goes only in `Assets/JamKit/Templates/SkylineRepo/`. Never edit Visiting Hours' files. Visiting
  Hours' Look library (`docs/redesign/look.md`) is reused by calling its API or copying its patterns.
- **Shared engine files** (spec 8) are edited only in Task 12, and only these two: `Runtime/Core/Movement/MoveSim.cs`
  and `Runtime/Unity/Movement/MovementController.cs`, plus one new test file
  `Tests/Core/Movement/OutsidePushTests.cs`. `MoveDef.cs`, `MoveTypes.cs` and the shared tuning asset are **not**
  touched (the push is enabled per sim, not per def, so `SharedTuningTests` is unaffected). Everything new is
  **opt-in, default off**.
- The outside push's written rules (spec 8, verbatim): "applied at the start of a step; an upward part forces leaving
  the ground and ends a wall-run; ignored during a mantle; respects the maximum speed (`CapFlat`, 22 m/s); doesn't
  refresh the double jump."
- **The engine gate** (spec 8): "the full Movement test suite, plus the Dream Escape and Deep Block route-time pins,
  unchanged." Tool: `tools/skyline/engine-gate.ps1` (Task 12).
- Aim rule (2.6): an encounter never combines "leads you" with "fires at where you are now" (the mall-cop drone and the
  spotlight never share an encounter). Every encounter spawns on at least two bearings 90 degrees or more apart, and
  covers at most two of the three layers (floor, walls, air).
- Dodging (2.6, 11.2): "on Normal the straight-line bot takes about twice the hits of the jinking bot, and neither
  goes unhit." This plan reads "about twice" as a ratio within **[1.6, 2.5]**.
- Performance (10): "consistent 120 fps at 1080p, or a bare minimum of 60 fps"; target 8.3 ms a frame; **1% lows at or
  under 16.7 ms in a build** (blocker); a scene must open in the editor within the 30% memory cap (4.6 GB) (blocker).
- Tuning numbers live in data (`SkylineGuns`, `SkylineEnemies`, `Difficulties`, `GalleriaArena`), never in Unity
  components.
- Sound: shipped sound is synthesized, recorded or CC0 (2.3). **Reference clips are measuring sticks only**: stored
  under `out/skyline-refs/` (git-ignored by `/out/`), never committed, never in `Assets/`, never shipped.
- Assets: CC0 first (Quaternius, Kenney, Poly Haven, ambientCG; licences in `docs/JAM-ASSETS-AND-PRESENTATION.md`).
  Tripo (`tools/tripo/gen.mjs`) only where CC0 can't express an identity, and only after the creator answers the credit
  question (Task 18-21 briefs). Log every non-CC0 file in `Assets/JamKit/Templates/SkylineRepo/CREDITS.md`.
- **Never hand-write Unity `.meta` files.** Unity creates them on its next import (the main session's pass); they're
  committed then.
- Dialogue: lines in this plan are **drafts** for the creator to rewrite, except the two the spec quotes verbatim
  (CONCIERGE's floor-care notice, Dot's "Strip it! Scrap's armour!"). They live in one data file (Task 10).
- Python runs through `uv` (bare `python` fails here). Heavy jobs go through
  `uv run python ~/.local/bin/capped-run.py --session skyline-repo -- <command>`; GPU jobs one at a time.
- Unity runs only in the main session, one job per lock hold (`docs/UNITY-LOCK.md`), through
  `tools/skyline/pass.ps1`. Subagents write code and hand back; they never launch Unity, never touch the lock folder,
  never wait or poll.
- Git: work on `main` (shared with the Visiting Hours session); stage explicit paths only (`git add -- <paths>`), never
  `git add -A`, never the already-modified `.unity` scenes outside SkylineRepo. Commit messages end with a blank line
  and `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Disk is tight: captures go to `out/skyline/<stamp>/` and are pruned after judging; `out/skyline-refs/` and
  `out/skyline-forge/` are kept.
- Gemini (ears layers 3-4) is out of M1 unless the creator has made the API key (pending question 1, default: no).
- The secrets guard hook blocks some text patterns; if a write is blocked, rename the variable or rephrase.

## Review Focus

1. **A long frame or a low frame rate during a reload** (a hitch, 20 fps on a weak machine): every sound event of the
   clip must still fire exactly once, in order, and the ammo must arrive with the clip's last event, never before.
   Tests: Task 2 (`ALongStepFiresEveryEventItCrossesOnceInOrder`, `TheSameEventsFireAt20And144Fps`), Task 4
   (`ReloadSpeedRescalesTheReloadAndItsEvents` checks the ammo timing).
2. **Swapping guns or being interrupted mid-reload**: the reload is cancelled, no late "seat" or "bolt" sound plays
   from the holstered gun, and its ammo is unchanged. Test: Task 4 (`SwappingMidReloadCancelsItWithNoLateSounds`).
3. **A Breacher shot at an odd moment**: during a mantle (ignored), on a wall-run (ends it, no free wall jump), while
   falling fast (cancels the fall, once), twice in one airtime (the second does nothing), after the double jump is
   spent (not refreshed). Tests: Task 7 and Task 12 (`OutsidePushTests`, `BreacherBoostSimTests`).
4. **Damage nobody planned for**: NaN, zero or negative damage, hits on a wrecked bot, hits where a plate was already
   shed, scrap from a stale wreck. Nothing breaks, nothing double-counts. Tests: Task 8.
5. **Aim assist with targets in odd places**: behind the player, beside them, at the eye, out of range, several in
   view. No NaN, never assists toward something behind you, picks the closest in angle, never pulls past the centre.
   Line of sight is the Unity side's job (it passes only visible targets). Tests: Task 3.

---

## How the work is ordered (and where it can stop)

Work may stop at any task boundary (the session stops when its weekly budget hits a threshold), so the cheapest,
most certain value comes first:

| Phase | Tasks | Kind | Verified by | Rough share of M1's Claude budget |
|---|---|---|---|---|
| 0 | 1 | content (reference clips; runs in parallel from the start) | the ears, the creator's approval | 4% |
| 1 | 2-11 | **pure C#** | `dotnet test` (tools/skyline/test.ps1) | 16% |
| 2 | 12 | **engine** | the engine gate + `dotnet test` | 4% |
| 3 | 13-17 | **Unity** (one batch, one pass) | SkylinePass (main session) | 28% |
| 4 | 18-22 | **content** (identity agents, one pass) | SkylinePass captures + the ears | 30% |
| 5 | 23-24 | **judging**, then the creator plays | judges against spec 10, the creator | 18% |

**Parallelism:** Task 1 runs alongside everything (it writes only `docs/SKYLINE-REFS.md` to git and files under
`out/`). Tasks 2-12 are one C# chain with one git writer at a time (a read-only review may overlap the next task).
Tasks 13-16 are one Unity batch, split across agents by file ownership: 13 owns the pass plumbing (`Editor/SkylineScenes.cs`,
`SkylinePassBatch.cs`, `SkylineBuild.cs`, `Editor/Recording/`, `Scripts/Common/`, `Scripts/Capture/`,
`SkylinePerfProbe.cs`, `tools/skyline/pass.ps1`, `synccheck.py`); 14 owns `Scripts/Player/`, `Scripts/Audio/`,
`Scripts/Fx/`, `Scripts/Hud/`, `Scripts/Range/`, `Editor/SkylinePlayerBuilder.cs`, `Editor/SkylineRangeBuilder.cs`;
15 owns `Scripts/Bots/`, `Scripts/Voice/`, `Editor/SkylineGalleryBuilder.cs`; 16 owns `Scripts/Arena/`,
`Editor/SkylineArenaBuilder.cs`, `Editor/SkylineLint.cs` and the lint lines of `SkylinePassBatch.cs`. Run 13 first
(it fixes the shared contracts), then 14 and 15 in parallel, then 16 (it uses both). Task 17 (the pass) is the main session's. Tasks 18-21 are four parallel identity agents (disjoint
folders), then Task 22 (the pass) and Task 23 (judges) are the main session's.

## File map

Pure C# (`Packages/com.vibejam.jamkit/Runtime/Skyline/`, tests in `Packages/com.vibejam.jamkit/Tests/Skyline/`):

| File | Responsibility | Task |
|---|---|---|
| `HandlingTimeline.cs` | clip kinds, events at fractions, pose keys, the clip player | 2 |
| `GunFeel.cs` | aim assist (pad), camera kick | 3 |
| `SkylineGun.cs` | `GunDef`, `SkylineGun` (WeaponState + handling set), `Loadout`, `GunCue`, `GunDecisions` | 4 |
| `SkylineGuns.cs` | the Writ's and the Breacher's data; `ForgeExport` (timelines for the sound forge) | 5 |
| `WritSpots.cs` | weak spots, `Writ.Resolve`, upgrade paths | 6 |
| `Breacher.cs` | the self-knockback push and the one-boost-per-airtime charge | 7 |
| `Enemies.cs` | `EnemyDef`, the three bots' data, `EnemyBody` (plates), `PlayerArmor`, `Wreck` | 8 |
| `BotBrain.cs` | bot states, faces, attack cycle; `AcidSweep` | 9 |
| `Aim.cs` (modify) | the spotlight beam locks its sweep, not just its point | 9 |
| `Encounters.cs` | `EncounterDef`, `EncounterRules`, `DifficultyDef`, `GalleriaArena`, `TeachingBeats` | 10 |
| `CombatSim.cs` (modify), `EncounterDodge.cs` | the dodge harness on the arena's encounters | 11 |

Engine (shared, Task 12): `Runtime/Core/Movement/MoveSim.cs`, `Runtime/Unity/Movement/MovementController.cs`,
`Tests/Core/Movement/OutsidePushTests.cs`; tool `tools/skyline/engine-gate.ps1`.

Tools: `tools/skyline/guns/firstcut.py`, `tools/skyline/guns/render_all.py`, `tools/skyline/guns/<gun>/` (recipes and
timelines), `tools/skyline/soundforge/synccheck.py` (+ tests), `tools/skyline/pass.ps1` (stage E), `docs/SKYLINE-REFS.md`.

Unity (`Assets/JamKit/Templates/SkylineRepo/`): `Editor/SkylineScenes.cs`, `Editor/SkylineArenaBuilder.cs`,
`Editor/SkylineRangeBuilder.cs`, `Editor/SkylineGalleryBuilder.cs`, `Editor/SkylineLint.cs`,
`Editor/Recording/SkylineCapture.cs`, `Scripts/Player/*`, `Scripts/Audio/*`, `Scripts/Fx/*`, `Scripts/Hud/*`,
`Scripts/Bots/*`, `Scripts/Arena/*`, `Scripts/Range/*`, `Scripts/Voice/*`, `Scripts/Capture/*`,
`Resources/SkylineAudio/<gun>/*.wav` (rendered by the forge).

---

### Task 1: Candidate reference clips (content; parallel from the start)

An agent assembles **candidate** reference clips for the Writ and the Breacher from the three games the creator named
(Destiny 2, Witchfire, Cyberpunk 2077), trimmed to the shot and reload events, plus a list for the creator to approve
or replace. Measuring sticks only.

**Files:**
- Create: `docs/SKYLINE-REFS.md` (committed: the list)
- Create (not in git): `out/skyline-refs/raw/*`, `out/skyline-refs/writ/*.wav`, `out/skyline-refs/breacher/*.wav`,
  `out/skyline-refs/sheets/*.png`

**Interfaces:**
- Consumes: `~/.local/bin/yt-dlp.exe`, ffmpeg (on PATH), the ears skill
  (`uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py measure|events|compare`).
- Produces: `out/skyline-refs/<gun>/<event>-<game>-<n>.wav` (48 kHz WAV, mono or stereo as the source, one event per
  file; reloads as one whole clip each), with `<gun>` in `writ|breacher` and `<event>` in
  `shot|dry|reload-partial|reload-empty|equip|inspect|impact`; `docs/SKYLINE-REFS.md` with one row per clip. Tasks 18-19
  compare against the rows marked approved.

- [ ] **Step 1: Find candidates.** For each gun's identity (Writ: a precise sidearm, hand-cannon weight, a low RPM;
  Breacher: a door-breach shotgun, heavy knockback, a mechanical cell reload), search each game's footage, preferring
  sound showcases with no music and no voice:
  ```bash
  ~/.local/bin/yt-dlp.exe "ytsearch10:Destiny 2 hand cannon sound showcase" --flat-playlist --print "%(id)s | %(duration)s | %(title)s"
  ~/.local/bin/yt-dlp.exe "ytsearch10:Witchfire revolver gameplay sounds no commentary" --flat-playlist --print "%(id)s | %(duration)s | %(title)s"
  ~/.local/bin/yt-dlp.exe "ytsearch10:Cyberpunk 2077 shotgun sounds" --flat-playlist --print "%(id)s | %(duration)s | %(title)s"
  ```
  Good starting points: Destiny 2 hand cannons (Ace of Spades, Fatebringer) and shotguns (Felwinter's Lie);
  Cyberpunk 2077's Malorian Arms 3516 and the Carnage; Witchfire's revolver and shotgun. Aim for 2-3 candidates per
  event per gun, at least one from each game where the game has a fitting weapon.

- [ ] **Step 2: Download only the needed section, as audio.**
  ```bash
  mkdir -p out/skyline-refs/raw
  ~/.local/bin/yt-dlp.exe -f bestaudio --download-sections "*01:02-01:20" -x --audio-format wav \
    -o "out/skyline-refs/raw/%(id)s-%(section_start)s.%(ext)s" "https://www.youtube.com/watch?v=<id>"
  ```

- [ ] **Step 3: Trim each event.** Find onsets with the ears, then cut with ffmpeg (a 20 ms pre-roll, the full tail):
  ```bash
  uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py events out/skyline-refs/raw/<file>.wav --range-db 50
  ffmpeg -y -ss <onset-0.02> -t <length> -i out/skyline-refs/raw/<file>.wav -ar 48000 out/skyline-refs/writ/shot-destiny2-1.wav
  ```
  A reload is cut as one clip from the first handling sound to the last.

- [ ] **Step 4: Measure every clip and reject bad ones.** Run `listen.py measure <clip> --png out/skyline-refs/sheets/<name>.png`.
  Reject a clip when it clips (true peak above 0 dBTP), carries music or speech (read the spectrogram: steady tonal
  lines or speech formants under the event), or its event is masked by another sound. Keep the measurement line
  (peak, LUFS, attack, tail, centroid, band shares) for the list.

- [ ] **Step 5: Write `docs/SKYLINE-REFS.md`.** A short intro (measuring sticks only, never shipped, stored in
  `out/skyline-refs/`, re-downloadable from the table), then one table per gun with columns: event, file, game,
  weapon, video URL + time range, why it fits the identity, measurements, **Approved?** (blank: the creator fills it in
  with yes / no / a replacement). End with the question for the creator: "Approve or replace these; the Writ and
  Breacher agents compare against the approved ones (Tasks 18-19). Until you answer, they use the first candidate of
  each event."

- [ ] **Step 6: Commit the list only.**
  ```bash
  git add -- docs/SKYLINE-REFS.md
  git commit -m "Skyline M1: candidate reference clips for the Writ and the Breacher (list only; clips stay in out/skyline-refs)

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
  ```

---
### Task 2: Handling timelines and pose frames (pure C#)

A clip of a handling set (equip, fire, dry fire, partial reload, from-empty reload, inspect) is a length, events at
fixed fractions of it, and pose frames the viewmodel eases between. A player plays one clip at a time, frame-rate
independent, and rescales with a speed (a reload upgrade) while the events keep their fractions (spec 2.3).

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/HandlingTimeline.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/HandlingTimelineTests.cs`

**Interfaces:**
- Consumes: `System.Numerics.Vector3`.
- Produces: `enum HandlingClipKind { Equip, Fire, DryFire, ReloadPartial, ReloadEmpty, Inspect }`;
  `struct HandlingEvent { float at; string id; string sound; HandlingEvent(float at, string id, string sound) }`;
  `struct PoseFrame { float at; Vector3 gunOffset, gunEuler, magOffset; PoseFrame(float at, Vector3 gunOffset, Vector3
  gunEuler, Vector3 magOffset) }`; `class HandlingClip { HandlingClipKind kind; float seconds; HandlingEvent[] events;
  PoseFrame[] pose; HandlingClip(); HandlingClip(HandlingClipKind, float, HandlingEvent[], PoseFrame[]); string
  Validate(); PoseFrame SamplePose(float t01) }`; `class HandlingPlayer { HandlingClip Clip; float Duration; float
  Elapsed; bool Playing; float Progress01; PoseFrame Pose; void Play(HandlingClip clip, float speed = 1f); void Stop();
  int Advance(float dt, List<HandlingEvent> fired); static float EventTime(HandlingClip clip, int index, float speed =
  1f) }`. Sound names are `"<gun>/<name>"` (Task 5 maps them to forge recipes and Unity clips).

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/HandlingTimelineTests.cs`:
```csharp
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class HandlingTimelineTests
    {
        static HandlingClip Reload() => new HandlingClip(HandlingClipKind.ReloadEmpty, 2f,
            new[]
            {
                new HandlingEvent(0f, "start", "test/start"),
                new HandlingEvent(0.25f, "mag_out", "test/mag_out"),
                new HandlingEvent(0.5f, "cell_in", "test/cell_in"),
                new HandlingEvent(1f, "bolt", "test/bolt"),
            },
            new[]
            {
                new PoseFrame(0f, Vector3.Zero, Vector3.Zero, Vector3.Zero),
                new PoseFrame(1f, new Vector3(1f, 0f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0f, -1f, 0f)),
            });

        /// <summary>Plays the clip in steps of <paramref name="dt"/>; each event's id with the time it fired.</summary>
        static List<KeyValuePair<string, float>> Times(HandlingClip clip, float dt, float speed = 1f)
        {
            var p = new HandlingPlayer();
            p.Play(clip, speed);
            var fired = new List<HandlingEvent>();
            var times = new List<KeyValuePair<string, float>>();
            float t = 0f;
            p.Advance(0f, fired);
            foreach (var e in fired) times.Add(new KeyValuePair<string, float>(e.id, t));
            for (int i = 0; i < 100000 && p.Playing; i++)
            {
                fired.Clear();
                p.Advance(dt, fired);
                t += dt;
                foreach (var e in fired) times.Add(new KeyValuePair<string, float>(e.id, t));
            }
            return times;
        }

        [Test]
        public void EventsFireInOrderAtTheirFractionsOfTheClip()
        {
            var times = Times(Reload(), 1f / 120f);
            Assert.That(times.Count, Is.EqualTo(4));
            string[] ids = { "start", "mag_out", "cell_in", "bolt" };
            float[] due = { 0f, 0.5f, 1f, 2f };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(times[i].Key, Is.EqualTo(ids[i]));
                Assert.That(times[i].Value, Is.EqualTo(due[i]).Within(1f / 120f + 1e-4f), ids[i]);
            }
        }

        [Test]
        public void ALongStepFiresEveryEventItCrossesOnceInOrder()
        {
            var p = new HandlingPlayer();
            p.Play(Reload());
            var fired = new List<HandlingEvent>();
            Assert.That(p.Advance(5f, fired), Is.EqualTo(4));
            Assert.That(fired[0].id, Is.EqualTo("start"));
            Assert.That(fired[3].id, Is.EqualTo("bolt"));
            Assert.That(p.Advance(1f, fired), Is.EqualTo(0), "nothing fires twice");
            Assert.That(p.Playing, Is.False);
            Assert.That(p.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void AFasterReloadRescalesTheTimelineAndKeepsTheFractions()
        {
            var times = Times(Reload(), 1f / 240f, 1.25f);
            Assert.That(times[2].Key, Is.EqualTo("cell_in"));
            Assert.That(times[2].Value, Is.EqualTo(0.8f).Within(1f / 240f + 1e-4f), "0.5 of 2 s / 1.25");
            Assert.That(times[3].Value, Is.EqualTo(1.6f).Within(1f / 240f + 1e-4f));
            Assert.That(HandlingPlayer.EventTime(Reload(), 2, 1.25f), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void TheSameEventsFireAt20And144Fps()
        {
            var slow = Times(Reload(), 1f / 20f);
            var fast = Times(Reload(), 1f / 144f);
            Assert.That(slow.Count, Is.EqualTo(fast.Count));
            for (int i = 0; i < slow.Count; i++)
            {
                Assert.That(slow[i].Key, Is.EqualTo(fast[i].Key));
                Assert.That(slow[i].Value, Is.EqualTo(fast[i].Value).Within(1f / 20f + 1e-4f));
            }
        }

        [Test]
        public void StoppingDropsTheRestOfTheClip()
        {
            var p = new HandlingPlayer();
            p.Play(Reload());
            var fired = new List<HandlingEvent>();
            p.Advance(0.6f, fired);
            Assert.That(fired.Count, Is.EqualTo(2));
            p.Stop();
            Assert.That(p.Advance(5f, fired), Is.EqualTo(0));
            Assert.That(p.Clip, Is.Null);
        }

        [Test]
        public void ABrokenStepIsIgnored()
        {
            var p = new HandlingPlayer();
            p.Play(Reload());
            var fired = new List<HandlingEvent>();
            p.Advance(float.NaN, fired);
            p.Advance(-1f, fired);
            Assert.That(fired.Count, Is.EqualTo(1), "only the event at 0");
            Assert.That(p.Elapsed, Is.EqualTo(0f));
        }

        [Test]
        public void ThePoseIsEasedBetweenFramesAndHeldOutsideThem()
        {
            var c = Reload();
            Assert.That(c.SamplePose(0.5f).gunOffset.X, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(c.SamplePose(0.25f).gunOffset.X, Is.EqualTo(0.15625f).Within(1e-5f), "smoothstep, not linear");
            Assert.That(c.SamplePose(1.5f).magOffset.Y, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(new HandlingClip(HandlingClipKind.Fire, 0.2f, null, null).SamplePose(0.5f).gunEuler, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ValidateNamesWhatIsWrong()
        {
            Assert.That(Reload().Validate(), Is.Null);
            Assert.That(new HandlingClip(HandlingClipKind.Fire, 0f, null, null).Validate(), Does.Contain("seconds"));
            var outOfRange = new HandlingClip(HandlingClipKind.Inspect, 1f, new[] { new HandlingEvent(1.2f, "spin", "x/spin") }, null);
            Assert.That(outOfRange.Validate(), Does.Contain("spin").And.Contain("[0, 1]"));
            var unordered = new HandlingClip(HandlingClipKind.Inspect, 1f, new[] { new HandlingEvent(0.5f, "a", null), new HandlingEvent(0.2f, "b", null) }, null);
            Assert.That(unordered.Validate(), Does.Contain("order"));
            var twice = new HandlingClip(HandlingClipKind.Inspect, 1f, new[] { new HandlingEvent(0.1f, "a", null), new HandlingEvent(0.2f, "a", null) }, null);
            Assert.That(twice.Validate(), Does.Contain("twice"));
            var badPose = new HandlingClip(HandlingClipKind.Inspect, 1f, null, new[] { new PoseFrame(0.6f, Vector3.Zero, Vector3.Zero, Vector3.Zero), new PoseFrame(0.3f, Vector3.Zero, Vector3.Zero, Vector3.Zero) });
            Assert.That(badPose.Validate(), Does.Contain("pose frame 1"));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~HandlingTimelineTests"`
Expected: build FAILS with `CS0246: The type or namespace name 'HandlingClip' could not be found`.

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/HandlingTimeline.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The clips of a gun's handling set (docs/SKYLINE-DESIGN.md 2.3), each with sound and code animation.</summary>
    public enum HandlingClipKind { Equip, Fire, DryFire, ReloadPartial, ReloadEmpty, Inspect }

    /// <summary>One event of a clip (mag out, cell in, hydraulic seat, bolt...) at a fixed fraction of the clip's length,
    /// so a faster reload moves its sounds with its animation.</summary>
    public struct HandlingEvent
    {
        /// <summary>0..1 of the clip's length.</summary>
        public float at;
        /// <summary>The event's name inside its clip ("mag_out").</summary>
        public string id;
        /// <summary>The sound it plays, "&lt;gun&gt;/&lt;name&gt;": the forge recipe tools/skyline/guns/&lt;gun&gt;/recipes/&lt;name&gt;.json
        /// and the Unity clip Resources/SkylineAudio/&lt;gun&gt;/&lt;name&gt;. Null or empty: an animation-only event.</summary>
        public string sound;

        public HandlingEvent(float at, string id, string sound)
        {
            this.at = at;
            this.id = id;
            this.sound = sound;
        }
    }

    /// <summary>The viewmodel's offset from its rest pose at a fraction of a clip: the gun's position (metres, camera space:
    /// x right, y up, z forward), its rotation (Euler degrees: x pitches the muzzle down, y yaws it right, z rolls it), and
    /// the magazine's (or cell's) offset from its seat (gun space). The robot arms follow the gun.</summary>
    public struct PoseFrame
    {
        public float at;
        public Vector3 gunOffset;
        public Vector3 gunEuler;
        public Vector3 magOffset;

        public PoseFrame(float at, Vector3 gunOffset, Vector3 gunEuler, Vector3 magOffset)
        {
            this.at = at;
            this.gunOffset = gunOffset;
            this.gunEuler = gunEuler;
            this.magOffset = magOffset;
        }
    }

    /// <summary>One clip of a handling set: its length, its events and its pose frames (both in order of <c>at</c>).</summary>
    public sealed class HandlingClip
    {
        public HandlingClipKind kind;
        public float seconds;
        public HandlingEvent[] events = Array.Empty<HandlingEvent>();
        public PoseFrame[] pose = Array.Empty<PoseFrame>();

        public HandlingClip() { }

        public HandlingClip(HandlingClipKind kind, float seconds, HandlingEvent[] events, PoseFrame[] pose)
        {
            this.kind = kind;
            this.seconds = seconds;
            this.events = events ?? Array.Empty<HandlingEvent>();
            this.pose = pose ?? Array.Empty<PoseFrame>();
        }

        /// <summary>Null when the clip is usable; otherwise what's wrong, naming the clip and the event or frame.</summary>
        public string Validate()
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) return $"{kind}: seconds must be a number > 0, got {seconds}";
            var ids = new HashSet<string>();
            float last = 0f;
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                string name = $"{kind} event {i} ({e.id ?? "no id"})";
                if (!(e.at >= 0f && e.at <= 1f)) return $"{name}: at must be within [0, 1], got {e.at}";
                if (e.at < last) return $"{name}: events must be in order of at ({e.at} comes after {last})";
                if (string.IsNullOrEmpty(e.id)) return $"{name}: missing id";
                if (!ids.Add(e.id)) return $"{name}: the id is used twice in this clip";
                last = e.at;
            }
            last = 0f;
            for (int i = 0; i < pose.Length; i++)
            {
                float at = pose[i].at;
                if (!(at >= 0f && at <= 1f)) return $"{kind} pose frame {i}: at must be within [0, 1], got {at}";
                if (at < last) return $"{kind} pose frame {i}: frames must be in order of at ({at} comes after {last})";
                last = at;
            }
            return null;
        }

        /// <summary>The pose at <paramref name="t01"/> of the clip: eased (smoothstep) between the frames around it, held
        /// before the first frame and after the last; the rest pose (all zero) when the clip has no frames.</summary>
        public PoseFrame SamplePose(float t01)
        {
            if (float.IsNaN(t01)) t01 = 0f;
            if (pose.Length == 0) return new PoseFrame(t01, Vector3.Zero, Vector3.Zero, Vector3.Zero);
            if (t01 <= pose[0].at) return Held(pose[0], t01);
            var final = pose[pose.Length - 1];
            if (t01 >= final.at) return Held(final, t01);
            int i = 1;
            while (pose[i].at < t01) i++;
            var a = pose[i - 1];
            var b = pose[i];
            float span = b.at - a.at;
            float s = span > 1e-6f ? (t01 - a.at) / span : 1f;
            s = s * s * (3f - 2f * s);
            return new PoseFrame(t01, Vector3.Lerp(a.gunOffset, b.gunOffset, s), Vector3.Lerp(a.gunEuler, b.gunEuler, s),
                Vector3.Lerp(a.magOffset, b.magOffset, s));
        }

        static PoseFrame Held(PoseFrame f, float t01) => new PoseFrame(t01, f.gunOffset, f.gunEuler, f.magOffset);
    }

    /// <summary>Plays one clip at a time and reports the events it crosses. Frame-rate independent: a long step fires every
    /// event it crosses, in order, exactly once.</summary>
    public sealed class HandlingPlayer
    {
        HandlingClip _clip;
        float _duration, _t;
        int _next;

        public HandlingClip Clip => _clip;
        /// <summary>The clip's length after the speed scale (s).</summary>
        public float Duration => _duration;
        public float Elapsed => _t;
        public bool Playing => _clip != null && _t < _duration;
        public float Progress01 => _clip == null ? 0f : (_duration > 0f ? Math.Min(1f, _t / _duration) : 1f);
        public PoseFrame Pose => _clip == null ? new PoseFrame(0f, Vector3.Zero, Vector3.Zero, Vector3.Zero) : _clip.SamplePose(Progress01);

        /// <summary>Starts <paramref name="clip"/> from its beginning. <paramref name="speed"/> above 1 plays it faster (a
        /// reload-speed upgrade): it lasts seconds / speed and its events keep their fractions. Events at 0 fire on the next
        /// <see cref="Advance"/>, even one of 0 seconds.</summary>
        public void Play(HandlingClip clip, float speed = 1f)
        {
            _clip = clip ?? throw new ArgumentNullException(nameof(clip));
            if (!(speed > 0.01f)) speed = 0.01f;
            _duration = clip.seconds / speed;
            _t = 0f;
            _next = 0;
        }

        /// <summary>Stops without firing the rest of the clip's events (a swap, a cancelled reload).</summary>
        public void Stop()
        {
            _clip = null;
            _duration = 0f;
            _t = 0f;
            _next = 0;
        }

        /// <summary>Advances by <paramref name="dt"/> (NaN and negative count as 0) and appends the events now due to
        /// <paramref name="fired"/> (if given), in order. Returns how many fired.</summary>
        public int Advance(float dt, List<HandlingEvent> fired)
        {
            if (_clip == null) return 0;
            if (dt > 0f) _t = float.IsInfinity(dt) ? _duration : _t + dt;
            float p = Progress01;
            int n = 0;
            var ev = _clip.events;
            while (_next < ev.Length && ev[_next].at <= p + 1e-6f)
            {
                fired?.Add(ev[_next]);
                _next++;
                n++;
            }
            return n;
        }

        /// <summary>When event <paramref name="index"/> of <paramref name="clip"/> fires at <paramref name="speed"/>: seconds
        /// from the clip's start.</summary>
        public static float EventTime(HandlingClip clip, int index, float speed = 1f) =>
            clip.events[index].at * clip.seconds / (speed > 0.01f ? speed : 0.01f);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~HandlingTimelineTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/HandlingTimeline.cs Packages/com.vibejam.jamkit/Tests/Skyline/HandlingTimelineTests.cs
git commit -m "Skyline M1: handling timelines (events at fractions of a clip, eased pose frames, a frame-rate-independent player)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 3: Aim assist and camera kick (pure C#)

Pad aim assist (2.4: "slowdown near targets and mild magnetism, tuned per gun") and the camera kick that recovers
(2.3). Both are small pure models the Unity player applies.

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/GunFeel.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/GunFeelTests.cs`

**Interfaces:**
- Consumes: `System.Numerics.Vector3`.
- Produces: `class AimAssistDef { float slowdownConeDeg, slowdownScale, magnetConeDeg, magnetDegPerSec, range }`;
  `struct AimTarget { Vector3 center; float radius; AimTarget(Vector3, float) }`; `struct AimAssistResult { float
  turnScale, yawNudge, pitchNudge; int target }`; `static class AimAssist { float DeltaAngle(float a, float b); void
  Angles(Vector3 eye, Vector3 point, out float yaw, out float pitch); AimAssistResult Compute(Vector3 eye, float yaw,
  float pitch, IReadOnlyList<AimTarget> targets, bool playerTurningOrMoving, float dt, AimAssistDef d) }`;
  `class CameraKickDef { float pitchDeg, yawDeg, recoverRate }`; `class CameraKick { float Pitch, Yaw; void
  Kick(CameraKickDef d, float side, float scale = 1f); void Tick(float dt); void Clear() }`. Angles follow Unity: yaw 0
  looks along +Z and 90 along +X; positive pitch looks **down**.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/GunFeelTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class GunFeelTests
    {
        static readonly AimAssistDef Def = new AimAssistDef
        {
            slowdownConeDeg = 6f, slowdownScale = 0.55f, magnetConeDeg = 3f, magnetDegPerSec = 8f, range = 40f,
        };

        static List<AimTarget> One(Vector3 c, float r = 0.5f) => new List<AimTarget> { new AimTarget(c, r) };

        static Vector3 AtYaw(float deg, float dist) =>
            new Vector3(MathF.Sin(deg * MathF.PI / 180f) * dist, 0f, MathF.Cos(deg * MathF.PI / 180f) * dist);

        [Test]
        public void NoTargetsMeansNoAssist()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, new List<AimTarget>(), true, 0.016f, Def);
            Assert.That(r.turnScale, Is.EqualTo(1f));
            Assert.That(r.yawNudge, Is.EqualTo(0f));
            Assert.That(r.target, Is.EqualTo(-1));
        }

        [Test]
        public void ATargetOnTheCrosshairSlowsTheStick()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, 0f, 10f)), false, 0.016f, Def);
            Assert.That(r.turnScale, Is.EqualTo(0.55f));
            Assert.That(r.target, Is.EqualTo(0));
        }

        [Test]
        public void ATargetOutsideTheConeOrOutOfRangeIsIgnored()
        {
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, One(AtYaw(30f, 10f)), true, 0.016f, Def).turnScale, Is.EqualTo(1f));
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, 0f, 100f)), true, 0.016f, Def).target, Is.EqualTo(-1));
        }

        [Test]
        public void TheMagnetPullsTowardTheCentreButNeverPastIt()
        {
            var t = One(AtYaw(2f, 10f), 0f);
            var whole = AimAssist.Compute(Vector3.Zero, 0f, 0f, t, true, 1f, Def);
            Assert.That(whole.yawNudge, Is.EqualTo(2f).Within(1e-3f), "8 deg/s for 1 s, but the centre is only 2 deg away");
            var part = AimAssist.Compute(Vector3.Zero, 0f, 0f, t, true, 0.1f, Def);
            Assert.That(part.yawNudge, Is.EqualTo(0.8f).Within(1e-3f));
            Assert.That(part.pitchNudge, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ATargetBelowPullsThePitchDown()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, -0.3f, 10f), 0f), true, 0.1f, Def);
            Assert.That(r.pitchNudge, Is.GreaterThan(0f), "positive pitch looks down");
        }

        [Test]
        public void NoPullWhenThePlayerIsStill()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(AtYaw(2f, 10f), 0f), false, 1f, Def);
            Assert.That(r.yawNudge, Is.EqualTo(0f));
            Assert.That(r.turnScale, Is.EqualTo(0.55f), "the slowdown still applies");
        }

        [Test]
        public void TheClosestTargetInAngleWins()
        {
            var targets = new List<AimTarget> { new AimTarget(AtYaw(5f, 10f), 0f), new AimTarget(AtYaw(-1f, 30f), 0f) };
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, targets, true, 0.016f, Def).target, Is.EqualTo(1));
        }

        [Test]
        public void TargetsBehindOrOnTheEyeAreNeverAssisted()
        {
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, 0f, -10f), 20f), true, 0.016f, Def).target, Is.EqualTo(-1));
            var onEye = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(Vector3.Zero), true, 0.016f, Def);
            Assert.That(onEye.target, Is.EqualTo(-1));
            Assert.That(float.IsNaN(onEye.yawNudge), Is.False);
        }

        [Test]
        public void DeltaAngleWrapsTheShortWay()
        {
            Assert.That(AimAssist.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(AimAssist.DeltaAngle(10f, 350f), Is.EqualTo(-20f).Within(1e-4f));
            Assert.That(AimAssist.DeltaAngle(0f, 180f), Is.EqualTo(180f).Within(1e-4f));
        }

        [Test]
        public void TheKickRaisesTheViewAndRecoversByItself()
        {
            var d = new CameraKickDef { pitchDeg = 2f, yawDeg = 0.5f, recoverRate = 10f };
            var k = new CameraKick();
            k.Kick(d, 1f);
            Assert.That(k.Pitch, Is.EqualTo(-2f), "negative pitch is up");
            Assert.That(k.Yaw, Is.EqualTo(0.5f));
            for (int i = 0; i < 18; i++) k.Tick(1f / 60f);    // 0.3 s = 3 / rate
            Assert.That(MathF.Abs(k.Pitch), Is.LessThan(2f * 0.06f));
        }

        [Test]
        public void TheRecoveryIsTheSameAtAnyFrameRate()
        {
            var d = new CameraKickDef();
            var a = new CameraKick();
            var b = new CameraKick();
            a.Kick(d, -1f);
            b.Kick(d, -1f);
            for (int i = 0; i < 60; i++) a.Tick(1f / 60f);
            b.Tick(1f);
            Assert.That(a.Pitch, Is.EqualTo(b.Pitch).Within(1e-4f));
            Assert.That(a.Yaw, Is.EqualTo(b.Yaw).Within(1e-4f));
        }

        [Test]
        public void ABrokenSideGivesNoSidewaysKick()
        {
            var k = new CameraKick();
            k.Kick(new CameraKickDef(), float.NaN);
            Assert.That(k.Yaw, Is.EqualTo(0f));
            Assert.That(k.Pitch, Is.LessThan(0f));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~GunFeelTests"`
Expected: build FAILS (`AimAssistDef` not found).

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/GunFeel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>Controller aim assist for one gun (docs/SKYLINE-DESIGN.md 2.4): the stick turns slower near a target, and a
    /// mild pull toward it while the player is turning or moving. Pad only; the caller decides that.</summary>
    public sealed class AimAssistDef
    {
        /// <summary>Within this angle (degrees) of a target's edge, the stick's turn rate is scaled by <see cref="slowdownScale"/>.</summary>
        public float slowdownConeDeg = 6f;
        public float slowdownScale = 0.55f;
        /// <summary>Within this angle of a target's edge, the view is pulled toward its centre at up to <see cref="magnetDegPerSec"/>.</summary>
        public float magnetConeDeg = 3f;
        public float magnetDegPerSec = 8f;
        /// <summary>Targets farther than this (m) get no assist.</summary>
        public float range = 40f;
    }

    /// <summary>Something aim assist can settle on: a bot's centre and radius (m). The caller passes only targets in line of sight.</summary>
    public struct AimTarget
    {
        public Vector3 center;
        public float radius;

        public AimTarget(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    public struct AimAssistResult
    {
        /// <summary>Multiply the stick's turn rate by this (1 = no assist).</summary>
        public float turnScale;
        /// <summary>Degrees to add to the view this frame (yaw right, pitch down: Unity's convention).</summary>
        public float yawNudge, pitchNudge;
        /// <summary>Index of the target assisted, or -1.</summary>
        public int target;
    }

    public static class AimAssist
    {
        const float Rad2Deg = 180f / MathF.PI;

        /// <summary>The signed smallest difference b - a in degrees, in (-180, 180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            else if (d <= -180f) d += 360f;
            return d;
        }

        /// <summary>The yaw (0 along +Z, 90 along +X) and pitch (positive looks down) from <paramref name="eye"/> to <paramref name="point"/>.</summary>
        public static void Angles(Vector3 eye, Vector3 point, out float yaw, out float pitch)
        {
            Vector3 d = point - eye;
            float flat = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
            yaw = MathF.Atan2(d.X, d.Z) * Rad2Deg;
            pitch = MathF.Atan2(-d.Y, flat) * Rad2Deg;
        }

        /// <summary>The assist for this frame. Picks the target whose edge is closest in angle to the crosshair (within
        /// range, in front), slows the stick inside <see cref="AimAssistDef.slowdownConeDeg"/>, and while
        /// <paramref name="playerTurningOrMoving"/> nudges the view toward the target's centre inside
        /// <see cref="AimAssistDef.magnetConeDeg"/>, never past it.</summary>
        public static AimAssistResult Compute(Vector3 eye, float yaw, float pitch, IReadOnlyList<AimTarget> targets,
            bool playerTurningOrMoving, float dt, AimAssistDef d)
        {
            var r = new AimAssistResult { turnScale = 1f, target = -1 };
            if (targets == null || d == null) return r;
            float bestEdge = float.MaxValue, bestDy = 0f, bestDp = 0f, bestCentre = 0f;
            int best = -1;
            for (int i = 0; i < targets.Count; i++)
            {
                float dist = Vector3.Distance(eye, targets[i].center);
                if (!(dist > 1e-3f) || dist > d.range) continue;
                Angles(eye, targets[i].center, out float ty, out float tp);
                float dy = DeltaAngle(yaw, ty);
                float dp = tp - pitch;
                float centre = MathF.Sqrt(dy * dy + dp * dp);
                if (!(centre <= 90f)) continue;                                   // beside or behind the view: never
                float half = MathF.Atan(MathF.Max(0f, targets[i].radius) / dist) * Rad2Deg;
                float edge = MathF.Max(0f, centre - half);
                if (edge < bestEdge)
                {
                    bestEdge = edge;
                    bestDy = dy;
                    bestDp = dp;
                    bestCentre = centre;
                    best = i;
                }
            }
            if (best < 0 || (bestEdge > d.slowdownConeDeg && bestEdge > d.magnetConeDeg)) return r;
            r.target = best;
            if (bestEdge <= d.slowdownConeDeg) r.turnScale = d.slowdownScale;
            if (playerTurningOrMoving && bestEdge <= d.magnetConeDeg && bestCentre > 1e-4f && dt > 0f)
            {
                float step = MathF.Min(d.magnetDegPerSec * dt, bestCentre);
                r.yawNudge = bestDy / bestCentre * step;
                r.pitchNudge = bestDp / bestCentre * step;
            }
            return r;
        }
    }

    /// <summary>A gun's camera kick (2.3: "camera kick that recovers").</summary>
    public sealed class CameraKickDef
    {
        /// <summary>Degrees the view kicks up per shot.</summary>
        public float pitchDeg = 1.5f;
        /// <summary>Most degrees of sideways jitter per shot.</summary>
        public float yawDeg = 0.4f;
        /// <summary>How fast it recovers (1/s, exponential): about 95% back after 3 / this seconds.</summary>
        public float recoverRate = 9f;
    }

    /// <summary>The view's kick offset in degrees, added on top of the player's aim and never written into it, so the aim
    /// recovers by itself. Negative pitch is up (Unity's convention).</summary>
    public sealed class CameraKick
    {
        float _rate = 9f;

        public float Pitch { get; private set; }
        public float Yaw { get; private set; }

        /// <summary>One shot's kick. <paramref name="side"/> in -1..1 (from the caller's seeded random) picks the jitter.</summary>
        public void Kick(CameraKickDef d, float side, float scale = 1f)
        {
            if (d == null || !(scale > 0f)) return;
            if (float.IsNaN(side)) side = 0f;
            side = Math.Max(-1f, Math.Min(1f, side));
            Pitch -= d.pitchDeg * scale;
            Yaw += d.yawDeg * side * scale;
            _rate = Math.Max(0.1f, d.recoverRate);
        }

        public void Tick(float dt)
        {
            if (!(dt > 0f)) return;
            float k = MathF.Exp(-_rate * dt);
            Pitch *= k;
            Yaw *= k;
        }

        public void Clear()
        {
            Pitch = 0f;
            Yaw = 0f;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~GunFeelTests"`
Expected: PASS (12 tests).

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/GunFeel.cs Packages/com.vibejam.jamkit/Tests/Skyline/GunFeelTests.cs
git commit -m "Skyline M1: pad aim assist (slowdown, mild magnetism) and a camera kick that recovers

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: The Skyline gun: WeaponState plus the handling set (pure C#)

`SkylineGun` wraps the existing `JamKit.Combat.WeaponState` (ammo, fire timing, reloads) and drives the handling set:
equip blocks firing until its clip ends; each shot plays the fire clip; an empty magazine dry fires and then reloads
from empty; a reload with rounds left is the partial reload; inspect plays only when idle. **Reload times are the
reload clips' lengths** (one number for balance, animation and sound), scaled by an upgradeable `ReloadSpeed`. A
`Loadout` holds two guns; swapping is the other gun's equip clip. Gun cues also become decision events (4.1).

**Files:**
- Modify: `core-tests/Skyline/Skyline.Tests.csproj` (add `Runtime\Core\Combat`)
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineGun.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/SkylineGunTests.cs`

**Interfaces:**
- Consumes: `JamKit.Combat.WeaponDef` (fields `magazineSize, startingReserve, maxReserve, infiniteReserve, ammoPerShot,
  fireMode, fireInterval, burstCount, burstInterval, reloadTime, autoReloadOnEmpty`), `JamKit.Combat.WeaponState`
  (`Tick(float dt, bool held, bool pressed, List<float> shotAges)`, `StartReload()`, `CancelReload()`,
  `AmmoInMagazine`, `IsReloading`, `Def`, events `Fired, DryFired, ReloadStarted, ReloadCanceled`); Task 2's
  `HandlingClip, HandlingClipKind, HandlingEvent, HandlingPlayer`; Task 3's `CameraKickDef, AimAssistDef`; M0's
  `DecisionLog(int capacity)`, `DecisionEvent { time, kind, subject, side, context, threatBearing, ammo, position }`,
  `DecisionKind`, `DecisionContext`.
- Produces: `class GunDef { string id, displayName; WeaponDef weapon; float damage; int pellets; float spreadDeg, range,
  falloffStart, falloffEnd, falloffMinScale, weakPointMultiplier, enemyKnockback; bool reloadAfterDryFire; float
  hitStopSeconds, killHitStopSeconds, shakePerShot; CameraKickDef kick; AimAssistDef aimAssist; HandlingClip[] clips;
  const int ClipKinds = 6; HandlingClip Clip(HandlingClipKind); float FalloffScale(float distance); string Validate() }`;
  `enum GunCueKind { ClipStarted, Event, Shot }`; `struct GunCue { GunCueKind kind; HandlingClipKind clip; float
  seconds; HandlingEvent ev; float age }`; `class SkylineGun { SkylineGun(GunDef); GunDef Def; WeaponState State;
  HandlingPlayer Handling; bool Equipped, Equipping, Inspecting; int Ammo; float ReloadSpeed (read and write); float
  ReloadSeconds(bool fromEmpty); void Equip(List<GunCue>); void Holster(); bool Reload(List<GunCue>); bool
  Inspect(List<GunCue>); void Tick(float dt, bool triggerHeld, bool triggerPressed, List<GunCue> cues) }`; `class
  Loadout { Loadout(SkylineGun first, SkylineGun second, List<GunCue> cues); int ActiveIndex; SkylineGun Active;
  SkylineGun this[int]; int Count; void Swap(List<GunCue>) }`; `static class GunDecisions { int Append(DecisionLog log,
  in GunCue cue, int slot, float time, Vector3 position, DecisionContext context, float threatBearing, int ammo) }`.

- [ ] **Step 1: Add the Combat module to the Skyline test project**

`core-tests/Skyline/Skyline.Tests.csproj` (whole file):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Shared + Movement + Combat + Skyline (Skyline Repo's pure C# systems, docs/SKYLINE-DESIGN.md). BoxWorld comes from
       the Movement tests. Skyline is its own assembly in Unity (JamKit.Skyline), so it is deliberately not in All. -->
  <ItemGroup>
    <Compile Include="$(PkgRoot)Runtime\Core\Movement\**\*.cs" />
    <Compile Include="$(PkgRoot)Runtime\Core\Combat\**\*.cs" />
    <Compile Include="$(PkgRoot)Tests\Core\Movement\BoxWorld.cs" />
    <Compile Include="$(PkgRoot)Runtime\Skyline\**\*.cs" />
    <Compile Include="$(PkgRoot)Tests\Skyline\**\*.cs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/SkylineGunTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Combat;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class SkylineGunTests
    {
        const float H = 1f / 60f;

        static HandlingClip Clip(HandlingClipKind k, float s, params HandlingEvent[] ev) => new HandlingClip(k, s, ev, null);

        static GunDef TestGun() => new GunDef
        {
            id = "test",
            weapon = new WeaponDef { magazineSize = 3, fireMode = FireMode.Semi, fireInterval = 0.25f },
            clips = new[]
            {
                Clip(HandlingClipKind.Equip, 0.5f, new HandlingEvent(0f, "unfold", "test/unfold"), new HandlingEvent(1f, "lock", "test/lock")),
                Clip(HandlingClipKind.Fire, 0.2f, new HandlingEvent(0f, "shot", "test/shot")),
                Clip(HandlingClipKind.DryFire, 0.2f, new HandlingEvent(0f, "click", "test/click")),
                Clip(HandlingClipKind.ReloadPartial, 1.0f, new HandlingEvent(0.2f, "mag_out", "test/mag_out"), new HandlingEvent(0.6f, "seat", "test/seat")),
                Clip(HandlingClipKind.ReloadEmpty, 1.5f, new HandlingEvent(0.2f, "mag_out", "test/mag_out"), new HandlingEvent(0.6f, "seat", "test/seat"), new HandlingEvent(0.9f, "bolt", "test/bolt")),
                Clip(HandlingClipKind.Inspect, 2f, new HandlingEvent(0.5f, "spin", "test/spin")),
            },
        };

        /// <summary>A gun ticked at 60 fps with every cue time-stamped.</summary>
        sealed class Rig
        {
            public readonly SkylineGun gun;
            public readonly List<GunCue> cues = new List<GunCue>();
            public readonly List<float> at = new List<float>();
            public float t;

            public Rig(GunDef d, bool waitForEquip = true)
            {
                gun = new SkylineGun(d);
                gun.Equip(cues);
                Stamp();
                if (waitForEquip) Idle(0.6f);
            }

            void Stamp() { while (at.Count < cues.Count) at.Add(t); }

            public void Idle(float seconds)
            {
                int steps = (int)Math.Round(seconds / H);
                for (int i = 0; i < steps; i++) { t += H; gun.Tick(H, false, false, cues); Stamp(); }
            }

            public void Press()
            {
                t += H;
                gun.Tick(H, true, true, cues);
                Stamp();
            }

            public void Reload() { gun.Reload(cues); Stamp(); }

            public int Count(Predicate<GunCue> p) { int n = 0; foreach (var c in cues) if (p(c)) n++; return n; }

            /// <summary>The time of the first cue matching <paramref name="p"/> at or after <paramref name="from"/>; NaN if none.</summary>
            public float TimeOf(Predicate<GunCue> p, float from = 0f)
            {
                for (int i = 0; i < cues.Count; i++) if (at[i] >= from && p(cues[i])) return at[i];
                return float.NaN;
            }
        }

        static bool Shot(GunCue c) => c.kind == GunCueKind.Shot;
        static Predicate<GunCue> Started(HandlingClipKind k) => c => c.kind == GunCueKind.ClipStarted && c.clip == k;
        static Predicate<GunCue> Event(string id) => c => c.kind == GunCueKind.Event && c.ev.id == id;

        [Test]
        public void EquipBlocksFiringUntilItsClipEnds()
        {
            var r = new Rig(TestGun(), waitForEquip: false);
            Assert.That(r.Count(Started(HandlingClipKind.Equip)), Is.EqualTo(1));
            Assert.That(r.Count(Event("unfold")), Is.EqualTo(1), "the event at 0 fires with the clip");
            r.Press();
            Assert.That(r.Count(Shot), Is.EqualTo(0), "can't fire while equipping");
            r.Idle(0.5f);
            Assert.That(r.TimeOf(Event("lock")), Is.EqualTo(0.5f).Within(H + 1e-4f));
            r.Press();
            Assert.That(r.Count(Shot), Is.EqualTo(1));
            Assert.That(r.gun.Ammo, Is.EqualTo(2));
        }

        [Test]
        public void EachShotPlaysTheFireClipAndItsShotSound()
        {
            var r = new Rig(TestGun());
            float t0 = r.t;
            r.Press();
            Assert.That(r.TimeOf(Started(HandlingClipKind.Fire), t0), Is.EqualTo(r.t));
            Assert.That(r.TimeOf(Shot, t0), Is.EqualTo(r.t));
            Assert.That(r.TimeOf(Event("shot"), t0), Is.EqualTo(r.t), "the shot sound on the same tick");
        }

        [Test]
        public void AnEmptyMagazineClicksThenReloadsFromEmpty()
        {
            var r = new Rig(TestGun());
            for (int i = 0; i < 3; i++) { r.Press(); r.Idle(0.3f); }
            Assert.That(r.gun.Ammo, Is.EqualTo(0));
            Assert.That(r.gun.State.IsReloading, Is.False, "no automatic reload on the last shot");
            r.Press();
            float dry = r.t;
            Assert.That(r.TimeOf(Started(HandlingClipKind.DryFire), dry), Is.EqualTo(dry));
            Assert.That(r.TimeOf(Event("click"), dry), Is.EqualTo(dry));
            r.Idle(2f);
            float reload = r.TimeOf(Started(HandlingClipKind.ReloadEmpty), dry);
            Assert.That(reload, Is.EqualTo(dry + 0.2f).Within(H + 1e-4f), "the from-empty reload starts when the click ends");
            Assert.That(r.TimeOf(Event("bolt"), reload), Is.EqualTo(reload + 0.9f * 1.5f).Within(H + 1e-4f));
            Assert.That(r.gun.Ammo, Is.EqualTo(3));
        }

        [Test]
        public void AManualReloadWithRoundsLeftIsThePartialReload()
        {
            var r = new Rig(TestGun());
            r.Press();
            r.Idle(0.3f);
            float t0 = r.t;
            r.Reload();
            var started = r.cues.FindLast(c => c.kind == GunCueKind.ClipStarted);
            Assert.That(started.clip, Is.EqualTo(HandlingClipKind.ReloadPartial));
            Assert.That(started.seconds, Is.EqualTo(1.0f).Within(1e-5f));
            r.Idle(1.05f);
            Assert.That(r.gun.Ammo, Is.EqualTo(3));
            Assert.That(r.Count(Started(HandlingClipKind.ReloadEmpty)), Is.EqualTo(0));
            Assert.That(r.TimeOf(Event("seat"), t0), Is.EqualTo(t0 + 0.6f).Within(H + 1e-4f));
        }

        [Test]
        public void ReloadSpeedRescalesTheReloadAndItsEvents()
        {
            var r = new Rig(TestGun());
            r.gun.ReloadSpeed = 1.25f;
            Assert.That(r.gun.ReloadSeconds(false), Is.EqualTo(0.8f).Within(1e-5f));
            r.Press();
            r.Idle(0.3f);
            float t0 = r.t;
            r.Reload();
            Assert.That(r.cues.FindLast(c => c.kind == GunCueKind.ClipStarted).seconds, Is.EqualTo(0.8f).Within(1e-5f));
            r.Idle(0.8f - 2f * H);
            Assert.That(r.gun.Ammo, Is.EqualTo(2), "the ammo isn't in before the clip ends");
            r.Idle(3f * H);
            Assert.That(r.gun.Ammo, Is.EqualTo(3), "and it is in when the clip ends");
            Assert.That(r.TimeOf(Event("seat"), t0), Is.EqualTo(t0 + 0.6f * 0.8f).Within(H + 1e-4f), "the seat moved with the reload");
        }

        [Test]
        public void SwappingMidReloadCancelsItWithNoLateSounds()
        {
            var cues = new List<GunCue>();
            var a = new SkylineGun(TestGun());
            var b = new SkylineGun(TestGun());
            var loadout = new Loadout(a, b, cues);
            for (int i = 0; i < 40; i++) a.Tick(H, false, false, cues);
            a.Tick(H, true, true, cues);
            for (int i = 0; i < 20; i++) a.Tick(H, false, false, cues);
            a.Reload(cues);
            for (int i = 0; i < 6; i++) a.Tick(H, false, false, cues);   // 0.1 s: before mag_out at 0.2 s
            loadout.Swap(cues);
            int afterSwap = cues.Count;
            Assert.That(loadout.Active, Is.SameAs(b));
            Assert.That(a.State.IsReloading, Is.False);
            Assert.That(a.Equipped, Is.False);
            for (int i = 0; i < 120; i++) { a.Tick(H, false, false, cues); b.Tick(H, false, false, cues); }
            for (int i = afterSwap; i < cues.Count; i++)
                Assert.That(cues[i].clip == HandlingClipKind.ReloadPartial, Is.False, "no reload sound after the swap");
            Assert.That(a.Ammo, Is.EqualTo(2), "the cancelled reload moved no ammo");
            Assert.That(cues.FindAll(c => c.kind == GunCueKind.ClipStarted && c.clip == HandlingClipKind.Equip).Count, Is.EqualTo(2));
        }

        [Test]
        public void InspectPlaysOnlyWhenIdleAndFiringCancelsIt()
        {
            var r = new Rig(TestGun(), waitForEquip: false);
            Assert.That(r.gun.Inspect(r.cues), Is.False, "not while equipping");
            r.Idle(0.6f);
            Assert.That(r.gun.Inspect(r.cues), Is.True);
            Assert.That(r.gun.Inspecting, Is.True);
            r.Idle(0.2f);
            r.Press();
            Assert.That(r.gun.Inspecting, Is.False);
            Assert.That(r.Count(Shot), Is.EqualTo(1));
            Assert.That(r.Count(Event("spin")), Is.EqualTo(0), "the inspect's later event never fires");
        }

        [Test]
        public void AnIncompleteGunIsRefusedWithItsProblem()
        {
            var d = TestGun();
            d.clips[(int)HandlingClipKind.Inspect] = null;
            var e = Assert.Throws<ArgumentException>(() => new SkylineGun(d));
            Assert.That(e.Message, Does.Contain("Inspect"));
        }

        [Test]
        public void TheGunKeepsItsOwnCopyOfTheNumbers()
        {
            var d = TestGun();
            var g = new SkylineGun(d);
            Assert.That(d.weapon.autoReloadOnEmpty, Is.True, "the shared def is untouched");
            Assert.That(g.State.Def, Is.Not.SameAs(d.weapon));
            Assert.That(g.State.Def.infiniteReserve, Is.True);
        }

        [Test]
        public void FalloffIsFullUpCloseAndFloorsFarAway()
        {
            var d = new GunDef { falloffStart = 10f, falloffEnd = 30f, falloffMinScale = 0.4f };
            Assert.That(d.FalloffScale(5f), Is.EqualTo(1f));
            Assert.That(d.FalloffScale(20f), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(d.FalloffScale(100f), Is.EqualTo(0.4f));
            Assert.That(d.FalloffScale(float.NaN), Is.EqualTo(1f));
        }

        [Test]
        public void CuesBecomeDecisions()
        {
            var log = new DecisionLog(16);
            var shot = new GunCue { kind = GunCueKind.Shot };
            var reload = new GunCue { kind = GunCueKind.ClipStarted, clip = HandlingClipKind.ReloadEmpty };
            var equip = new GunCue { kind = GunCueKind.ClipStarted, clip = HandlingClipKind.Equip };
            var sound = new GunCue { kind = GunCueKind.Event, clip = HandlingClipKind.Fire };
            Assert.That(GunDecisions.Append(log, shot, 1, 2f, Vector3.One, DecisionContext.InCombat, 90f, 4), Is.EqualTo(1));
            Assert.That(GunDecisions.Append(log, reload, 1, 3f, Vector3.One, DecisionContext.None, 0f, 0), Is.EqualTo(1));
            Assert.That(GunDecisions.Append(log, equip, 0, 4f, Vector3.One, DecisionContext.None, 0f, 8), Is.EqualTo(1));
            Assert.That(GunDecisions.Append(log, sound, 0, 5f, Vector3.One, DecisionContext.None, 0f, 8), Is.EqualTo(0));
            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(log[0].kind, Is.EqualTo(DecisionKind.Fire));
            Assert.That(log[0].subject, Is.EqualTo(1));
            Assert.That(log[0].ammo, Is.EqualTo(4));
            Assert.That(log[1].kind, Is.EqualTo(DecisionKind.Reload));
            Assert.That(log[2].kind, Is.EqualTo(DecisionKind.Swap));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SkylineGunTests"`
Expected: build FAILS (`GunDef` not found).

- [ ] **Step 4: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineGun.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Combat;

namespace JamKit.Skyline
{
    /// <summary>One Skyline gun as data (docs/SKYLINE-DESIGN.md 2.2-2.3): its numbers, its feel and its handling set. The
    /// reload times ARE the reload clips' lengths, so balance, animation and sound share one number.</summary>
    public sealed class GunDef
    {
        public const int ClipKinds = 6;

        public string id = "";
        public string displayName = "";
        /// <summary>Magazine, fire mode and fire interval. <see cref="SkylineGun"/> copies it and sets reloadTime (from the
        /// clips), autoReloadOnEmpty off (a dry fire comes first) and infiniteReserve on (no ammo pickups in M1).</summary>
        public WeaponDef weapon = new WeaponDef();
        /// <summary>Damage per bullet (per pellet for a shotgun) before falloff and multipliers.</summary>
        public float damage = 10f;
        public int pellets = 1;
        /// <summary>Half-angle (degrees) of the cone the pellets spread over; 0 is dead accurate.</summary>
        public float spreadDeg;
        public float range = 80f;
        /// <summary>Full damage up to falloffStart (m), falling linearly to falloffMinScale at falloffEnd and beyond.</summary>
        public float falloffStart = 20f, falloffEnd = 50f, falloffMinScale = 0.5f;
        /// <summary>A hit on a bot's weak point (its lens or display: a robot's head) multiplies the damage by this.</summary>
        public float weakPointMultiplier = 1.5f;
        /// <summary>m/s a hit pushes a bot (the Breacher's identity); 0 for none.</summary>
        public float enemyKnockback;
        /// <summary>A dry fire starts the from-empty reload when its clip ends.</summary>
        public bool reloadAfterDryFire = true;
        public float hitStopSeconds = 0.03f, killHitStopSeconds = 0.08f;
        /// <summary>Screen-shake trauma added per shot fired (0..1; Combat/ScreenShake).</summary>
        public float shakePerShot = 0.05f;
        public CameraKickDef kick = new CameraKickDef();
        public AimAssistDef aimAssist = new AimAssistDef();
        /// <summary>Indexed by (int)HandlingClipKind; every kind is required.</summary>
        public HandlingClip[] clips = new HandlingClip[ClipKinds];

        public HandlingClip Clip(HandlingClipKind kind) => clips != null && (int)kind < clips.Length ? clips[(int)kind] : null;

        public float FalloffScale(float distance)
        {
            if (!(distance > falloffStart)) return 1f;
            if (distance >= falloffEnd || falloffEnd <= falloffStart) return falloffMinScale;
            float t = (distance - falloffStart) / (falloffEnd - falloffStart);
            return 1f + (falloffMinScale - 1f) * t;
        }

        /// <summary>Null when the gun is usable; otherwise what's wrong, naming the gun and the clip.</summary>
        public string Validate()
        {
            string name = string.IsNullOrEmpty(id) ? "gun" : id;
            if (weapon == null) return name + ": no weapon numbers";
            if (!(weapon.fireInterval > 0f)) return name + ": fireInterval must be > 0";
            if (weapon.magazineSize < 1) return name + ": magazineSize must be at least 1";
            if (pellets < 1) return name + ": pellets must be at least 1";
            if (!(damage > 0f)) return name + ": damage must be > 0";
            if (clips == null || clips.Length != ClipKinds) return $"{name}: clips must have one entry per HandlingClipKind ({ClipKinds})";
            for (int k = 0; k < ClipKinds; k++)
            {
                var c = clips[k];
                if (c == null) return $"{name}: missing the {(HandlingClipKind)k} clip";
                if (c.kind != (HandlingClipKind)k) return $"{name}: clip {k} is a {c.kind}, expected {(HandlingClipKind)k}";
                string bad = c.Validate();
                if (bad != null) return name + " " + bad;
            }
            return null;
        }
    }

    public enum GunCueKind { ClipStarted, Event, Shot }

    /// <summary>What the gun did this tick, for the viewmodel, the sound and the shooting code.</summary>
    public struct GunCue
    {
        public GunCueKind kind;
        /// <summary>The clip started (ClipStarted), or the clip the event belongs to (Event).</summary>
        public HandlingClipKind clip;
        /// <summary>ClipStarted: its length after the speed scale (s).</summary>
        public float seconds;
        /// <summary>Event: the event (its sound plays now).</summary>
        public HandlingEvent ev;
        /// <summary>Shot: how long before the end of this tick it was fired (s), as WeaponState reports.</summary>
        public float age;
    }

    /// <summary>
    /// A Skyline gun in hand: WeaponState's ammo, fire timing and reloads, plus the handling set (2.3). Equip blocks firing
    /// until its clip ends; each shot plays the fire clip (a new shot restarts it); an empty magazine dry fires and, when
    /// the click's clip ends, reloads from empty; a manual reload with rounds left is the partial reload; inspect plays only
    /// when idle and firing or reloading cancels it. Reload times come from the reload clips, divided by
    /// <see cref="ReloadSpeed"/>, and the clips' events rescale with them. Call <see cref="Tick"/> once per frame and read
    /// the cues it appends.
    /// </summary>
    public sealed class SkylineGun
    {
        readonly WeaponState _state;
        readonly WeaponDef _weapon;
        readonly HandlingPlayer _handling = new HandlingPlayer();
        readonly List<HandlingEvent> _fired = new List<HandlingEvent>();
        readonly List<float> _ages = new List<float>();
        List<GunCue> _cues;
        bool _equipping, _inspecting, _reloadAfterDry;
        float _reloadSpeed = 1f;

        public GunDef Def { get; }
        public WeaponState State => _state;
        public HandlingPlayer Handling => _handling;
        public bool Equipped { get; private set; }
        public bool Equipping => _equipping;
        public bool Inspecting => _inspecting;
        public int Ammo => _state.AmmoInMagazine;

        /// <summary>1 = the clips' own lengths; 1.25 reloads 25% faster (its events move with it).</summary>
        public float ReloadSpeed
        {
            get => _reloadSpeed;
            set => _reloadSpeed = value > 0.01f ? value : 0.01f;
        }

        public SkylineGun(GunDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            string bad = def.Validate();
            if (bad != null) throw new ArgumentException(bad, nameof(def));
            _weapon = Copy(def.weapon);
            _weapon.autoReloadOnEmpty = false;
            _weapon.infiniteReserve = true;
            _state = new WeaponState(_weapon);
            _state.Fired += OnFired;
            _state.DryFired += OnDryFired;
            _state.ReloadStarted += OnReloadStarted;
            _state.ReloadCanceled += OnReloadCanceled;
        }

        static WeaponDef Copy(WeaponDef w) => new WeaponDef
        {
            magazineSize = w.magazineSize, startingReserve = w.startingReserve, maxReserve = w.maxReserve,
            infiniteReserve = w.infiniteReserve, ammoPerShot = w.ammoPerShot, fireMode = w.fireMode,
            fireInterval = w.fireInterval, burstCount = w.burstCount, burstInterval = w.burstInterval,
            reloadTime = w.reloadTime, autoReloadOnEmpty = w.autoReloadOnEmpty,
        };

        /// <summary>The reload's length now: the from-empty or partial clip, divided by <see cref="ReloadSpeed"/>.</summary>
        public float ReloadSeconds(bool fromEmpty) =>
            Def.Clip(fromEmpty ? HandlingClipKind.ReloadEmpty : HandlingClipKind.ReloadPartial).seconds / _reloadSpeed;

        /// <summary>Takes the gun out: plays the equip clip; it can't fire until the clip ends.</summary>
        public void Equip(List<GunCue> cues)
        {
            _cues = cues;
            if (_state.IsReloading) _state.CancelReload();
            Equipped = true;
            _inspecting = false;
            _reloadAfterDry = false;
            _equipping = true;
            Start(HandlingClipKind.Equip, 1f);
            Flush(0f);
            _cues = null;
        }

        /// <summary>Puts it away at once (the swap's other half): a reload in progress is cancelled and no further events of
        /// the current clip fire.</summary>
        public void Holster()
        {
            if (_state.IsReloading) _state.CancelReload();
            _handling.Stop();
            Equipped = false;
            _equipping = false;
            _inspecting = false;
            _reloadAfterDry = false;
        }

        /// <summary>Manual reload. False when the magazine is full, a reload is running, or the gun is out or equipping.</summary>
        public bool Reload(List<GunCue> cues)
        {
            if (!Equipped || _equipping) return false;
            _cues = cues;
            bool ok = BeginReload();
            _cues = null;
            return ok;
        }

        /// <summary>Plays the inspect clip when the gun is idle (equipped, not equipping or reloading, no clip playing).</summary>
        public bool Inspect(List<GunCue> cues)
        {
            if (!Equipped || _equipping || _state.IsReloading || _handling.Playing) return false;
            _cues = cues;
            _inspecting = true;
            Start(HandlingClipKind.Inspect, 1f);
            Flush(0f);
            _cues = null;
            return true;
        }

        public void Tick(float dt, bool triggerHeld, bool triggerPressed, List<GunCue> cues)
        {
            if (!Equipped) return;
            if (!(dt > 0f) || float.IsInfinity(dt)) dt = 0f;
            _cues = cues;
            Flush(dt);
            if (_equipping && !_handling.Playing) _equipping = false;
            bool canFire = !_equipping;
            if (_inspecting && canFire && (triggerHeld || triggerPressed))
            {
                _inspecting = false;
                _handling.Stop();
            }
            if (_inspecting && !_handling.Playing) _inspecting = false;
            _state.Tick(dt, canFire && triggerHeld, canFire && triggerPressed, _ages);
            for (int i = 0; i < _ages.Count; i++)
                Add(new GunCue { kind = GunCueKind.Shot, clip = HandlingClipKind.Fire, age = _ages[i] });
            // After WeaponState's tick, so its reload and the reload clip start on the same clock.
            if (_reloadAfterDry && !_handling.Playing && !_state.IsReloading)
            {
                _reloadAfterDry = false;
                BeginReload();
            }
            Flush(0f);   // the events at 0 of a clip started during this tick
            _cues = null;
        }

        bool BeginReload()
        {
            _weapon.reloadTime = ReloadSeconds(_state.AmmoInMagazine == 0);
            if (!_state.StartReload()) return false;
            _inspecting = false;
            Flush(0f);
            return true;
        }

        void OnFired()
        {
            _inspecting = false;
            Start(HandlingClipKind.Fire, 1f);
        }

        void OnDryFired()
        {
            Start(HandlingClipKind.DryFire, 1f);
            if (Def.reloadAfterDryFire) _reloadAfterDry = true;
        }

        void OnReloadStarted()
        {
            Start(_state.AmmoInMagazine == 0 ? HandlingClipKind.ReloadEmpty : HandlingClipKind.ReloadPartial, _reloadSpeed);
        }

        void OnReloadCanceled()
        {
            var c = _handling.Clip;
            if (c != null && (c.kind == HandlingClipKind.ReloadEmpty || c.kind == HandlingClipKind.ReloadPartial)) _handling.Stop();
        }

        void Start(HandlingClipKind kind, float speed)
        {
            _handling.Play(Def.Clip(kind), speed);
            Add(new GunCue { kind = GunCueKind.ClipStarted, clip = kind, seconds = _handling.Duration });
        }

        void Flush(float dt)
        {
            _fired.Clear();
            _handling.Advance(dt, _fired);
            if (_fired.Count == 0) return;
            var kind = _handling.Clip.kind;
            for (int i = 0; i < _fired.Count; i++) Add(new GunCue { kind = GunCueKind.Event, clip = kind, ev = _fired[i] });
        }

        void Add(GunCue c) => _cues?.Add(c);
    }

    /// <summary>Two guns (2.1); swapping is a designed moment: the other gun's equip clip.</summary>
    public sealed class Loadout
    {
        readonly SkylineGun[] _guns;

        public int ActiveIndex { get; private set; }
        public SkylineGun Active => _guns[ActiveIndex];
        public SkylineGun this[int slot] => _guns[slot];
        public int Count => _guns.Length;

        /// <summary>Holds <paramref name="first"/> (equipped now) and <paramref name="second"/>.</summary>
        public Loadout(SkylineGun first, SkylineGun second, List<GunCue> cues)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            _guns = new[] { first, second };
            first.Equip(cues);
        }

        public void Swap(List<GunCue> cues)
        {
            Active.Holster();
            ActiveIndex = 1 - ActiveIndex;
            Active.Equip(cues);
        }
    }

    /// <summary>Gun cues as decision events (4.1, recorded from M1 on for Baja).</summary>
    public static class GunDecisions
    {
        /// <summary>A shot is Fire, a reload clip starting is Reload, an equip clip starting is Swap; other cues add nothing.
        /// <paramref name="slot"/> is the loadout slot (the decision's subject). Returns how many were added.</summary>
        public static int Append(DecisionLog log, in GunCue cue, int slot, float time, Vector3 position, DecisionContext context,
            float threatBearing, int ammo)
        {
            if (log == null) return 0;
            DecisionKind kind;
            if (cue.kind == GunCueKind.Shot) kind = DecisionKind.Fire;
            else if (cue.kind == GunCueKind.ClipStarted && (cue.clip == HandlingClipKind.ReloadPartial || cue.clip == HandlingClipKind.ReloadEmpty)) kind = DecisionKind.Reload;
            else if (cue.kind == GunCueKind.ClipStarted && cue.clip == HandlingClipKind.Equip) kind = DecisionKind.Swap;
            else return 0;
            log.Add(new DecisionEvent
            {
                time = time, kind = kind, subject = slot, context = context, threatBearing = threatBearing, ammo = ammo, position = position,
            });
            return 1;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SkylineGunTests"`
Expected: PASS (11 tests). Then the whole project: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
(M0's tests still pass).

- [ ] **Step 6: Commit**

```bash
git add -- core-tests/Skyline/Skyline.Tests.csproj Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineGun.cs Packages/com.vibejam.jamkit/Tests/Skyline/SkylineGunTests.cs
git commit -m "Skyline M1: the Skyline gun (WeaponState plus the handling set; reload times are the reload clips), loadout swap, gun decisions

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 5: The Writ and the Breacher as data, forge timelines, first-cut sounds (pure C# + tools)

The two guns' numbers and handling sets, one source of truth for the game and the sound forge: the C# data exports each
clip as a forge timeline, a test keeps the committed timelines equal to the data, and a first cut of synthesized sounds
(stand-ins the identity agents replace) is rendered into Unity's Resources so the pipeline plays and the sync check has
events to find.

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineGuns.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/SkylineGunsTests.cs`
- Create: `tools/skyline/guns/firstcut.py`, `tools/skyline/guns/render_all.py`
- Create (generated, committed): `tools/skyline/guns/writ/recipes/*.json` (12), `tools/skyline/guns/breacher/recipes/*.json`
  (12), `tools/skyline/guns/<gun>/<Clip>.timeline.json` (6 per gun),
  `Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/<gun>/<name>.wav` (24)

**Interfaces:**
- Consumes: Task 4's `GunDef`, `CameraKickDef`, `AimAssistDef`; Task 2's `HandlingClip`, `HandlingEvent`, `PoseFrame`;
  `JamKit.Combat.WeaponDef`, `FireMode`; the forge's `render_recipe(recipe, base_dir)`, `render_timeline(timeline,
  base_dir)`, `write_wav(path, audio)`, `DEFAULT_SR`.
- Produces: `static class SkylineGuns { const string WritId = "writ", BreacherId = "breacher"; GunDef Writ(); GunDef
  Breacher(); GunDef[] All(); GunDef Find(string id) }` (each call returns a new instance); `static class ForgeExport {
  string TimelineJson(HandlingClip); string FileName(HandlingClip); string Name(string sound); List<string>
  Sounds(GunDef) }`; the folder layout `tools/skyline/guns/<gun>/recipes/<name>.json` +
  `tools/skyline/guns/<gun>/<Clip>.timeline.json`; Unity clips at `Resources/SkylineAudio/<gun>/<name>`
  (`Resources.Load<AudioClip>("SkylineAudio/" + sound)`); `render_all.py` (the identity agents' render command).

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/SkylineGunsTests.cs`:
```csharp
using System.IO;
using JamKit.Combat;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class SkylineGunsTests
    {
        /// <summary>A repo path, found by walking up from the test's folder (dotnet) or the project folder (Unity); null when not reachable.</summary>
        static string RepoPath(string relative)
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                for (int i = 0; dir != null && i < 10; i++, dir = dir.Parent)
                {
                    string p = Path.Combine(dir.FullName, relative);
                    if (Directory.Exists(p) || File.Exists(p)) return p;
                }
            }
            return null;
        }

        [Test]
        public void BothGunsAreComplete()
        {
            foreach (var g in SkylineGuns.All()) Assert.That(g.Validate(), Is.Null, g.id);
        }

        [Test]
        public void EverySoundBelongsToItsGun()
        {
            foreach (var g in SkylineGuns.All())
                foreach (var s in ForgeExport.Sounds(g)) Assert.That(s, Does.StartWith(g.id + "/"));
        }

        [Test]
        public void TheWritIsAPreciseSidearmWithRoomToBreathe()
        {
            var w = SkylineGuns.Writ();
            Assert.That(w.pellets, Is.EqualTo(1));
            Assert.That(w.spreadDeg, Is.EqualTo(0f));
            Assert.That(w.weapon.fireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(60f / w.weapon.fireInterval, Is.LessThanOrEqualTo(300f), "an RPM low enough that the visuals breathe (2.3)");
            Assert.That(w.weakPointMultiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void TheBreacherIsAKnockbackShotgun()
        {
            var b = SkylineGuns.Breacher();
            var w = SkylineGuns.Writ();
            Assert.That(b.pellets, Is.GreaterThanOrEqualTo(6));
            Assert.That(b.spreadDeg, Is.GreaterThan(0f));
            Assert.That(b.enemyKnockback, Is.GreaterThan(w.enemyKnockback * 4f));
            Assert.That(b.range, Is.LessThan(w.range));
            Assert.That(b.kick.pitchDeg, Is.GreaterThan(w.kick.pitchDeg));
        }

        [Test]
        public void AReloadFromEmptyTakesLongerAndDoesMore()
        {
            foreach (var g in SkylineGuns.All())
            {
                var empty = g.Clip(HandlingClipKind.ReloadEmpty);
                var partial = g.Clip(HandlingClipKind.ReloadPartial);
                Assert.That(empty.seconds, Is.GreaterThan(partial.seconds), g.id);
                Assert.That(empty.events.Length, Is.GreaterThan(partial.events.Length), g.id + ": the bolt or pump");
            }
        }

        [Test]
        public void FindReturnsAFreshCopy()
        {
            var a = SkylineGuns.Find("writ");
            a.damage = 1f;
            Assert.That(SkylineGuns.Find("writ").damage, Is.Not.EqualTo(1f));
            Assert.That(SkylineGuns.Find("nope"), Is.Null);
        }

        [Test]
        public void AClipBecomesAForgeTimeline()
        {
            var clip = new HandlingClip(HandlingClipKind.Fire, 0.22f,
                new[] { new HandlingEvent(0f, "shot", "writ/shot"), new HandlingEvent(0.3f, "anim", null), new HandlingEvent(0.45f, "mech", "writ/shot_mech") }, null);
            Assert.That(ForgeExport.TimelineJson(clip), Is.EqualTo(
                "{\n  \"duration\": 0.22,\n  \"events\": [\n" +
                "    {\"at\": 0, \"recipe\": \"recipes/shot.json\"},\n" +
                "    {\"at\": 0.45, \"recipe\": \"recipes/shot_mech.json\"}\n  ]\n}\n"));
            Assert.That(ForgeExport.FileName(clip), Is.EqualTo("Fire.timeline.json"));
        }

        [Test]
        public void TheForgeFilesInTheRepoMatchTheGunData()
        {
            string guns = RepoPath("tools/skyline/guns");
            if (guns == null) Assert.Ignore("tools/skyline/guns isn't reachable from here");
            foreach (var g in SkylineGuns.All())
            {
                foreach (var c in g.clips)
                {
                    string path = Path.Combine(guns, g.id, ForgeExport.FileName(c));
                    Assert.That(File.Exists(path), Is.True, path + " (run WriteTheForgeTimelines)");
                    Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(ForgeExport.TimelineJson(c)), path + " is stale (run WriteTheForgeTimelines)");
                }
                foreach (var s in ForgeExport.Sounds(g))
                    Assert.That(File.Exists(Path.Combine(guns, g.id, "recipes", ForgeExport.Name(s) + ".json")), Is.True, "no recipe for " + s);
            }
        }

        [Test, Explicit("writes tools/skyline/guns/<gun>/<Clip>.timeline.json from the gun data; run after changing a clip")]
        public void WriteTheForgeTimelines()
        {
            string guns = RepoPath("tools/skyline/guns");
            Assert.That(guns, Is.Not.Null, "create tools/skyline/guns first (firstcut.py)");
            foreach (var g in SkylineGuns.All())
            {
                string dir = Path.Combine(guns, g.id);
                Directory.CreateDirectory(dir);
                foreach (var c in g.clips) File.WriteAllText(Path.Combine(dir, ForgeExport.FileName(c)), ForgeExport.TimelineJson(c));
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SkylineGunsTests"`
Expected: build FAILS (`SkylineGuns` not found).

- [ ] **Step 3: Write the gun data and the export**

`Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineGuns.cs`:
```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using JamKit.Combat;

namespace JamKit.Skyline
{
    /// <summary>The Bailiff kit's two M1 guns (docs/SKYLINE-DESIGN.md 2.2) as data. Every number is a balance number AND an
    /// animation and sound timing (2.3). The weapon identity agents (docs/SKYLINE-PLAN-M1.md Tasks 18-19) own and retune
    /// them; the tests pin the identities, not the exact numbers. Each call returns a new instance (upgrades change copies).</summary>
    public static class SkylineGuns
    {
        public const string WritId = "writ", BreacherId = "breacher";

        static readonly Vector3 O = Vector3.Zero;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static PoseFrame P(float at, Vector3 gun, Vector3 euler) => new PoseFrame(at, gun, euler, Vector3.Zero);
        static PoseFrame P(float at, Vector3 gun, Vector3 euler, Vector3 mag) => new PoseFrame(at, gun, euler, mag);
        static HandlingEvent E(float at, string id, string sound) => new HandlingEvent(at, id, sound);
        static HandlingClip C(HandlingClipKind k, float s, HandlingEvent[] ev, PoseFrame[] pose) => new HandlingClip(k, s, ev, pose);

        public static GunDef[] All() => new[] { Writ(), Breacher() };

        public static GunDef Find(string id) => id == WritId ? Writ() : id == BreacherId ? Breacher() : null;

        /// <summary>The Writ: a precise sidearm. Every weak-point hit leaves a weak spot that takes more than a headshot (2.2).
        /// 250 RPM, 8 rounds; a magazine-and-cell reload with a bolt from empty.</summary>
        public static GunDef Writ()
        {
            var tilt = V(-0.04f, -0.05f, 0f);
            var tiltEuler = V(10f, -10f, -25f);
            return new GunDef
            {
                id = WritId,
                displayName = "Writ",
                weapon = new WeaponDef { magazineSize = 8, fireMode = FireMode.Semi, fireInterval = 0.24f },
                damage = 22f,
                pellets = 1,
                spreadDeg = 0f,
                range = 80f,
                falloffStart = 25f,
                falloffEnd = 60f,
                falloffMinScale = 0.6f,
                weakPointMultiplier = 2f,
                enemyKnockback = 0.5f,
                hitStopSeconds = 0.03f,
                killHitStopSeconds = 0.08f,
                shakePerShot = 0.06f,
                kick = new CameraKickDef { pitchDeg = 2.2f, yawDeg = 0.5f, recoverRate = 9f },
                aimAssist = new AimAssistDef { slowdownConeDeg = 5f, slowdownScale = 0.5f, magnetConeDeg = 3f, magnetDegPerSec = 8f, range = 50f },
                clips = new[]
                {
                    C(HandlingClipKind.Equip, 0.55f,
                        new[] { E(0f, "unfold", "writ/equip_unfold"), E(0.7f, "lock", "writ/equip_lock") },
                        new[] { P(0f, V(0.05f, -0.3f, -0.15f), V(40f, 0f, 25f)), P(0.7f, V(0f, 0.01f, 0f), V(-3f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.Fire, 0.22f,
                        new[] { E(0f, "shot", "writ/shot"), E(0.45f, "mech", "writ/shot_mech") },
                        new[] { P(0f, O, O), P(0.15f, V(0f, 0.015f, -0.06f), V(-7f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.DryFire, 0.25f,
                        new[] { E(0f, "click", "writ/dry_click") },
                        new[] { P(0f, O, O), P(0.3f, V(0f, 0f, -0.01f), V(-1f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.ReloadPartial, 1.3f,
                        new[] { E(0.18f, "mag_out", "writ/mag_out"), E(0.52f, "cell_in", "writ/cell_in"), E(0.68f, "seat", "writ/seat") },
                        new[]
                        {
                            P(0f, O, O), P(0.15f, tilt, tiltEuler), P(0.18f, tilt, tiltEuler), P(0.3f, tilt, tiltEuler, V(0f, -0.25f, 0f)),
                            P(0.45f, tilt, tiltEuler, V(0f, -0.12f, 0f)), P(0.52f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.68f, V(-0.04f, -0.03f, 0f), V(4f, -6f, -15f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.ReloadEmpty, 1.75f,
                        new[] { E(0.13f, "mag_out", "writ/mag_out"), E(0.42f, "cell_in", "writ/cell_in"), E(0.55f, "seat", "writ/seat"), E(0.8f, "bolt", "writ/bolt") },
                        new[]
                        {
                            P(0f, O, O), P(0.11f, tilt, tiltEuler), P(0.13f, tilt, tiltEuler), P(0.25f, tilt, tiltEuler, V(0f, -0.25f, 0f)),
                            P(0.36f, tilt, tiltEuler, V(0f, -0.12f, 0f)), P(0.42f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.55f, V(-0.04f, -0.03f, 0f), V(4f, -6f, -15f)), P(0.8f, V(0.02f, -0.02f, 0f), V(0f, 8f, 12f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.Inspect, 2.4f,
                        new[] { E(0.1f, "turn", "writ/inspect_turn"), E(0.55f, "tap", "writ/inspect_tap"), E(0.85f, "settle", "writ/inspect_settle") },
                        new[]
                        {
                            P(0f, O, O), P(0.2f, V(-0.08f, 0.04f, 0.05f), V(-10f, -55f, -20f)), P(0.55f, V(-0.08f, 0.05f, 0.05f), V(-12f, -60f, -25f)),
                            P(0.6f, V(-0.08f, 0.045f, 0.05f), V(-11f, -58f, -24f)), P(0.85f, O, O), P(1f, O, O),
                        }),
                },
            };
        }

        /// <summary>The Breacher: a door-breach shotgun whose identity is knockback, including on yourself (2.2; the self-push
        /// is Breacher.cs). 80 RPM, a 4-shell cell; a cell swap, with a pump from empty.</summary>
        public static GunDef Breacher()
        {
            var tilt = V(0.03f, -0.06f, 0f);
            var tiltEuler = V(15f, 12f, 30f);
            return new GunDef
            {
                id = BreacherId,
                displayName = "Breacher",
                weapon = new WeaponDef { magazineSize = 4, fireMode = FireMode.Semi, fireInterval = 0.75f },
                damage = 9f,
                pellets = 9,
                spreadDeg = 5f,
                range = 30f,
                falloffStart = 6f,
                falloffEnd = 18f,
                falloffMinScale = 0.25f,
                weakPointMultiplier = 1.25f,
                enemyKnockback = 9f,
                hitStopSeconds = 0.05f,
                killHitStopSeconds = 0.1f,
                shakePerShot = 0.22f,
                kick = new CameraKickDef { pitchDeg = 6.5f, yawDeg = 1.5f, recoverRate = 6f },
                aimAssist = new AimAssistDef { slowdownConeDeg = 8f, slowdownScale = 0.6f, magnetConeDeg = 5f, magnetDegPerSec = 12f, range = 20f },
                clips = new[]
                {
                    C(HandlingClipKind.Equip, 0.7f,
                        new[] { E(0.15f, "rack", "breacher/equip_rack"), E(0.75f, "lock", "breacher/equip_lock") },
                        new[] { P(0f, V(0.06f, -0.35f, -0.2f), V(45f, 10f, 30f)), P(0.15f, V(0.03f, -0.15f, -0.1f), V(20f, 5f, 15f)), P(0.75f, V(0f, 0.01f, 0f), V(-4f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.Fire, 0.6f,
                        new[] { E(0f, "blast", "breacher/blast"), E(0.4f, "vent", "breacher/vent_hiss") },
                        new[] { P(0f, O, O), P(0.08f, V(0f, 0.04f, -0.14f), V(-18f, 0f, 3f)), P(0.4f, V(0f, 0.01f, -0.03f), V(-4f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.DryFire, 0.3f,
                        new[] { E(0f, "clunk", "breacher/dry_clunk") },
                        new[] { P(0f, O, O), P(0.25f, V(0f, -0.01f, -0.02f), V(2f, 0f, 0f)), P(1f, O, O) }),
                    C(HandlingClipKind.ReloadPartial, 1.6f,
                        new[] { E(0.15f, "cell_out", "breacher/cell_out"), E(0.5f, "cell_in", "breacher/cell_in"), E(0.66f, "seat", "breacher/seat") },
                        new[]
                        {
                            P(0f, O, O), P(0.12f, tilt, tiltEuler), P(0.15f, tilt, tiltEuler), P(0.28f, tilt, tiltEuler, V(0.05f, -0.3f, 0f)),
                            P(0.44f, tilt, tiltEuler, V(0.02f, -0.12f, 0f)), P(0.5f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.66f, V(0.02f, -0.04f, 0f), V(6f, 8f, 18f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.ReloadEmpty, 2.1f,
                        new[] { E(0.12f, "cell_out", "breacher/cell_out"), E(0.42f, "cell_in", "breacher/cell_in"), E(0.56f, "seat", "breacher/seat"), E(0.82f, "pump", "breacher/pump") },
                        new[]
                        {
                            P(0f, O, O), P(0.1f, tilt, tiltEuler), P(0.12f, tilt, tiltEuler), P(0.24f, tilt, tiltEuler, V(0.05f, -0.3f, 0f)),
                            P(0.37f, tilt, tiltEuler, V(0.02f, -0.12f, 0f)), P(0.42f, tilt, tiltEuler, V(0f, -0.02f, 0f)),
                            P(0.56f, V(0.02f, -0.04f, 0f), V(6f, 8f, 18f)), P(0.78f, V(0f, -0.02f, -0.08f), V(-6f, 0f, 0f)),
                            P(0.82f, V(0f, -0.02f, 0.02f), V(2f, 0f, 0f)), P(1f, O, O),
                        }),
                    C(HandlingClipKind.Inspect, 2.6f,
                        new[] { E(0.1f, "turn", "breacher/inspect_turn"), E(0.5f, "knock", "breacher/inspect_knock"), E(0.85f, "settle", "breacher/inspect_settle") },
                        new[]
                        {
                            P(0f, O, O), P(0.2f, V(-0.1f, 0.05f, 0.06f), V(-8f, -65f, -30f)), P(0.5f, V(-0.1f, 0.06f, 0.06f), V(-10f, -70f, -35f)),
                            P(0.53f, V(-0.1f, 0.055f, 0.06f), V(-9f, -68f, -33f)), P(0.85f, O, O), P(1f, O, O),
                        }),
                },
            };
        }
    }

    /// <summary>Handling clips as sound forge timelines (tools/skyline/soundforge, "timeline"), so the forge renders a gun's
    /// handling set from the same numbers the game plays.</summary>
    public static class ForgeExport
    {
        /// <summary>{"duration": s, "events": [{"at": f, "recipe": "recipes/&lt;name&gt;.json"}, ...]}, read from the gun's folder
        /// tools/skyline/guns/&lt;gun&gt;/. Animation-only events are left out.</summary>
        public static string TimelineJson(HandlingClip clip)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"duration\": ").Append(F(clip.seconds)).Append(",\n  \"events\": [");
            bool first = true;
            foreach (var e in clip.events)
            {
                if (string.IsNullOrEmpty(e.sound)) continue;
                sb.Append(first ? "\n" : ",\n").Append("    {\"at\": ").Append(F(e.at)).Append(", \"recipe\": \"recipes/")
                  .Append(Name(e.sound)).Append(".json\"}");
                first = false;
            }
            sb.Append(first ? "]\n}\n" : "\n  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>The timeline's file name in the gun's folder: "ReloadEmpty.timeline.json".</summary>
        public static string FileName(HandlingClip clip) => clip.kind + ".timeline.json";

        /// <summary>"writ/mag_out" gives "mag_out".</summary>
        public static string Name(string sound)
        {
            int slash = sound.LastIndexOf('/');
            return slash >= 0 ? sound.Substring(slash + 1) : sound;
        }

        /// <summary>Every sound a gun's clips use, once each, in order of first use.</summary>
        public static List<string> Sounds(GunDef gun)
        {
            var list = new List<string>();
            foreach (var c in gun.clips)
                foreach (var e in c.events)
                    if (!string.IsNullOrEmpty(e.sound) && !list.Contains(e.sound)) list.Add(e.sound);
            return list;
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Write the first-cut recipe generator**

`tools/skyline/guns/firstcut.py`:
```python
"""First-cut (stand-in) sounds for the Writ and the Breacher: simple synthesized layers so the pipeline plays something
and the sync check has events to find. The weapon identity agents replace every recipe (docs/SKYLINE-PLAN-M1.md Tasks
18-19); nothing here is meant to ship as it is.

    uv run --project tools/skyline/soundforge python tools/skyline/guns/firstcut.py [--force]

Writes tools/skyline/guns/<gun>/recipes/<name>.json, one per sound the gun's handling clips use (SkylineGuns.cs). It
never overwrites a recipe that exists unless --force is given, so it can't clobber an identity agent's work.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent


def click(hp=2500.0, decay=0.04, gain=-6.0, start=0.0):
    return [{"type": "noise", "highpass": hp, "start": start, "attack": 0.0005, "decay": decay, "gain_db": gain}]


def thunk(f0=180.0, f1=70.0, decay=0.12, gain=-6.0, start=0.0):
    return [
        {"type": "sine", "freq": f0, "freq_end": f1, "start": start, "attack": 0.001, "decay": decay, "gain_db": gain},
        {"type": "noise", "lowpass": 900, "start": start, "attack": 0.001, "decay": decay * 0.6, "gain_db": gain - 6},
    ]


def whir(f0=300.0, f1=900.0, length=0.25, gain=-14.0):
    return [
        {"type": "sine", "freq": f0, "freq_end": f1, "attack": 0.03, "decay": length, "gain_db": gain},
        {"type": "noise", "color": "pink", "highpass": 1500, "attack": 0.03, "decay": length, "gain_db": gain - 4},
    ]


def hiss(decay=0.4, gain=-16.0, start=0.0):
    return [{"type": "noise", "highpass": 4000, "start": start, "attack": 0.02, "decay": decay, "gain_db": gain}]


SOUNDS = {
    "writ": {
        "equip_unfold": whir(250, 700, 0.3, -14),
        "equip_lock": click(3000, 0.05, -6) + thunk(400, 200, 0.06, -10),
        "shot": [
            {"type": "noise", "highpass": 1800, "attack": 0.0005, "decay": 0.07, "gain_db": -1},
            {"type": "sine", "freq": 160, "freq_end": 50, "attack": 0.001, "decay": 0.28, "gain_db": -3},
            {"type": "noise", "color": "pink", "lowpass": 2500, "attack": 0.005, "decay": 0.7, "gain_db": -15},
        ],
        "shot_mech": click(3500, 0.03, -12),
        "dry_click": click(4000, 0.03, -8),
        "mag_out": thunk(260, 140, 0.08, -10) + click(2500, 0.03, -14),
        "cell_in": click(2000, 0.05, -8) + thunk(200, 90, 0.1, -9),
        "seat": hiss(0.25, -14) + thunk(120, 60, 0.15, -6),
        "bolt": click(3000, 0.04, -5) + thunk(300, 150, 0.08, -8),
        "inspect_turn": whir(200, 350, 0.4, -18),
        "inspect_tap": click(5000, 0.02, -12),
        "inspect_settle": thunk(220, 120, 0.1, -14),
    },
    "breacher": {
        "equip_rack": thunk(220, 110, 0.12, -6) + click(2200, 0.05, -8, start=0.06),
        "equip_lock": click(2600, 0.05, -6) + thunk(300, 150, 0.08, -9),
        "blast": [
            {"type": "noise", "color": "pink", "lowpass": 3000, "attack": 0.0008, "decay": 0.35, "gain_db": 0},
            {"type": "sine", "freq": 90, "freq_end": 38, "attack": 0.001, "decay": 0.4, "gain_db": -2},
            {"type": "noise", "highpass": 3000, "attack": 0.0005, "decay": 0.05, "gain_db": -6},
            {"type": "noise", "color": "pink", "lowpass": 1200, "attack": 0.01, "decay": 1.1, "gain_db": -16},
        ],
        "vent_hiss": hiss(0.45, -15),
        "dry_clunk": thunk(180, 120, 0.06, -8) + click(1800, 0.03, -12),
        "cell_out": thunk(200, 120, 0.1, -9) + hiss(0.15, -20),
        "cell_in": click(1800, 0.06, -7) + thunk(160, 80, 0.12, -8),
        "seat": hiss(0.3, -13) + thunk(100, 50, 0.2, -5),
        "pump": thunk(240, 120, 0.08, -5) + click(2400, 0.04, -7, start=0.09) + thunk(260, 130, 0.07, -7, start=0.09),
        "inspect_turn": whir(160, 300, 0.45, -18),
        "inspect_knock": thunk(300, 180, 0.05, -10) + click(3000, 0.02, -14),
        "inspect_settle": thunk(180, 100, 0.12, -13),
    },
}


def length(layers):
    end = max(float(l.get("start", 0.0)) + float(l.get("attack", 0.002)) + float(l.get("decay", 0.25)) for l in layers)
    return round(end + 0.05, 3)


def recipe(layers):
    return {"sample_rate": 48000, "length": length(layers), "seed": 1, "layers": layers, "master": {"limit_db": -1.0}}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--force", action="store_true", help="overwrite recipes that already exist")
    a = ap.parse_args(argv)
    written = kept = 0
    for gun, sounds in SOUNDS.items():
        folder = HERE / gun / "recipes"
        folder.mkdir(parents=True, exist_ok=True)
        for name, layers in sounds.items():
            path = folder / f"{name}.json"
            if path.exists() and not a.force:
                kept += 1
                continue
            path.write_text(json.dumps(recipe(layers), indent=2) + "\n", encoding="utf-8")
            written += 1
    print(f"wrote {written} recipes, kept {kept}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 5: Write the renderer the identity agents will use**

`tools/skyline/guns/render_all.py`:
```python
"""Renders the guns' sounds for Unity and their handling clips for the ears.

    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py [--gun writ]

- tools/skyline/guns/<gun>/recipes/<name>.json -> Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/<gun>/<name>.wav
  (16-bit PCM: what the game plays at each handling event)
- tools/skyline/guns/<gun>/<Clip>.timeline.json -> out/skyline-forge/<gun>/<Clip>.wav (a whole clip, to compare with the
  reference clips and to hear the timing; not committed)
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
import soundfile

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "skyline" / "soundforge"))
import soundforge  # noqa: E402

GUNS = ROOT / "tools" / "skyline" / "guns"
UNITY = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineAudio"
FORGE_OUT = ROOT / "out" / "skyline-forge"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--gun", help="only this gun's folder")
    a = ap.parse_args(argv)
    sounds = clips = 0
    for gun in sorted(p for p in GUNS.iterdir() if (p / "recipes").is_dir()):
        if a.gun and gun.name != a.gun:
            continue
        (UNITY / gun.name).mkdir(parents=True, exist_ok=True)
        for path in sorted((gun / "recipes").glob("*.json")):
            spec = json.loads(path.read_text(encoding="utf-8"))
            audio = soundforge.render_recipe(spec, path.parent)
            sr = int(spec.get("sample_rate", soundforge.DEFAULT_SR))
            soundfile.write(str(UNITY / gun.name / (path.stem + ".wav")), np.asarray(audio, dtype=np.float32), sr, subtype="PCM_16")
            sounds += 1
        (FORGE_OUT / gun.name).mkdir(parents=True, exist_ok=True)
        for path in sorted(gun.glob("*.timeline.json")):
            audio = soundforge.render_timeline(json.loads(path.read_text(encoding="utf-8")), gun)
            soundforge.write_wav(FORGE_OUT / gun.name / path.name.replace(".timeline.json", ".wav"), audio)
            clips += 1
    print(f"rendered {sounds} sounds into {UNITY} and {clips} clips into {FORGE_OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 6: Generate the recipes and timelines, render, and run the tests**

```bash
uv run --project tools/skyline/soundforge python tools/skyline/guns/firstcut.py
dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WriteTheForgeTimelines"
uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py
dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SkylineGunsTests"
```
Expected: "wrote 24 recipes, kept 0"; 12 `*.timeline.json` files; "rendered 24 sounds ... and 12 clips"; tests PASS (8,
the explicit writer not counted). Check one render with the ears:
`uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py events out/skyline-forge/writ/ReloadEmpty.wav --range-db 50`
should report 4 onsets near 0.23, 0.74, 0.96 and 1.40 s (the clip's 0.13 / 0.42 / 0.55 / 0.8 of 1.75 s; a dry run of
this plan measured exactly those). The Breacher's from-empty reload shows a fifth onset 0.09 s after the pump's first:
its second stroke, by design. If a quiet event is missed, raise that recipe's gain (stand-ins only need to be detectable).

- [ ] **Step 7: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/SkylineGuns.cs Packages/com.vibejam.jamkit/Tests/Skyline/SkylineGunsTests.cs tools/skyline/guns Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio
git commit -m "Skyline M1: the Writ and the Breacher as data; forge timelines exported from it; first-cut stand-in sounds rendered for Unity

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The Writ's weak spots and both upgrade paths (pure C#)

The Writ's identity (2.2): "every headshot or weak-point hit creates a weak spot on that enemy that takes more damage
than a headshot." On robots the weak point (a lens, a display) is the headshot. M1 builds the first upgrade of each gun
and writes the whole path (11.2).

Decisions made here (logged in the spec's section 14 at Task 24): a spot is **consumed** when hit; a spot hit creates no
new spot (so spots don't chain); at most 3 spots per bot, the oldest replaced; spots last 6 s; only the Writ creates and
hits spots until its capstone.

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/WritSpots.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/WritTests.cs`

**Interfaces:**
- Consumes: Task 4's `SkylineGun` (`Def.id`, `ReloadSpeed`, `ReloadSeconds`), Task 5's `SkylineGuns`.
- Produces: `enum HitZone { Body, Plate, WeakPoint }`; `struct WeakSpot { int id; Vector3 local; float createdAt }`;
  `class WritTuning { float spotMultiplier = 2.5f; int maxSpotsPerBot = 3; float spotLifetime = 6f; float spotRadius =
  0.2f }`; `class WeakSpots { int Count; IReadOnlyList<WeakSpot> All; void Expire(float now, WritTuning t); WeakSpot
  Add(Vector3 local, float now, WritTuning t); bool TryConsume(Vector3 local, float now, WritTuning t, out WeakSpot
  spot); void Clear() }`; `struct WritHit { float multiplier; bool spotCreated, spotHit; WeakSpot spot }`; `static class
  Writ { WritHit Resolve(WeakSpots spots, HitZone zone, Vector3 local, float now, bool fromWrit, float
  weakPointMultiplier, WritTuning t) }`; `enum UpgradeKind { Numbers, Augment, Capstone }`; `class UpgradeDef { string
  id, gun, name, description; UpgradeKind kind; bool built }`; `static class UpgradePaths { UpgradeDef[] Writ();
  UpgradeDef[] Breacher(); bool Apply(UpgradeDef u, SkylineGun gun, WritTuning writ) }`. Positions are in the bot's own
  space (the Unity side converts with the bot's transform), so spots ride on a moving bot.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/WritTests.cs`:
```csharp
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class WritTests
    {
        static readonly Vector3 Lens = new Vector3(0f, 0.3f, 0.4f);
        const float WritWeakPoint = 2f;

        [Test]
        public void AWritHitOnAWeakPointCreatesASpotThere()
        {
            var spots = new WeakSpots();
            var r = Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, new WritTuning());
            Assert.That(r.multiplier, Is.EqualTo(2f));
            Assert.That(r.spotCreated, Is.True);
            Assert.That(spots.Count, Is.EqualTo(1));
            Assert.That(spots.All[0].local, Is.EqualTo(Lens));
        }

        [Test]
        public void ASpotHitIsConsumedForMoreThanAHeadshot()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, t);
            var r = Writ.Resolve(spots, HitZone.Body, Lens + new Vector3(0.1f, 0f, 0f), 1.5f, true, WritWeakPoint, t);
            Assert.That(r.spotHit, Is.True);
            Assert.That(r.multiplier, Is.EqualTo(t.spotMultiplier));
            Assert.That(r.multiplier, Is.GreaterThan(WritWeakPoint), "the identity: a spot beats a headshot");
            Assert.That(r.spotCreated, Is.False, "a spot hit makes no new spot");
            Assert.That(spots.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnlyTheWritCreatesOrHitsSpots()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            var other = Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, false, 1.25f, t);
            Assert.That(other.multiplier, Is.EqualTo(1.25f), "the other gun's own headshot multiplier");
            Assert.That(spots.Count, Is.EqualTo(0));
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, t);
            var pellet = Writ.Resolve(spots, HitZone.Body, Lens, 1.2f, false, 1.25f, t);
            Assert.That(pellet.spotHit, Is.False);
            Assert.That(spots.Count, Is.EqualTo(1), "the Breacher's pellet left the spot alone (until Case Law)");
        }

        [Test]
        public void AHitAwayFromEverySpotIsAnOrdinaryHit()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            Writ.Resolve(spots, HitZone.WeakPoint, Lens, 1f, true, WritWeakPoint, t);
            var r = Writ.Resolve(spots, HitZone.Body, new Vector3(0f, -0.4f, 0f), 1.2f, true, WritWeakPoint, t);
            Assert.That(r.multiplier, Is.EqualTo(1f));
            Assert.That(r.spotHit, Is.False);
            Assert.That(spots.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpotsExpire()
        {
            var t = new WritTuning { spotLifetime = 6f };
            var spots = new WeakSpots();
            spots.Add(Lens, 0f, t);
            spots.Expire(5.9f, t);
            Assert.That(spots.Count, Is.EqualTo(1));
            Assert.That(spots.TryConsume(Lens, 6.1f, t, out _), Is.False, "gone after 6 s");
        }

        [Test]
        public void AtMostThreeSpotsTheOldestReplaced()
        {
            var t = new WritTuning();
            var spots = new WeakSpots();
            for (int i = 0; i < 4; i++) spots.Add(new Vector3(i, 0f, 0f), i * 0.1f, t);
            Assert.That(spots.Count, Is.EqualTo(3));
            Assert.That(spots.All[0].local.X, Is.EqualTo(1f), "the first spot was replaced");
        }

        [Test]
        public void TheNearestSpotWithinReachIsTheOneHit()
        {
            var t = new WritTuning { spotRadius = 0.2f };
            var spots = new WeakSpots();
            spots.Add(new Vector3(0f, 0f, 0f), 0f, t);
            spots.Add(new Vector3(0.15f, 0f, 0f), 0f, t);
            Assert.That(spots.TryConsume(new Vector3(0.12f, 0f, 0f), 0.5f, t, out var hit), Is.True);
            Assert.That(hit.local.X, Is.EqualTo(0.15f));
        }

        [Test]
        public void TheShippedWritKeepsItsIdentity()
        {
            Assert.That(new WritTuning().spotMultiplier, Is.GreaterThan(SkylineGuns.Writ().weakPointMultiplier));
        }

        [Test]
        public void EachPathIsNumbersThenAugmentThenCapstoneWithOnlyTheFirstBuilt()
        {
            foreach (var path in new[] { UpgradePaths.Writ(), UpgradePaths.Breacher() })
            {
                Assert.That(path.Length, Is.EqualTo(3));
                Assert.That(path[0].kind, Is.EqualTo(UpgradeKind.Numbers));
                Assert.That(path[1].kind, Is.EqualTo(UpgradeKind.Augment));
                Assert.That(path[2].kind, Is.EqualTo(UpgradeKind.Capstone));
                Assert.That(path[0].built, Is.True);
                Assert.That(path[1].built || path[2].built, Is.False);
                foreach (var u in path) Assert.That(u.description, Is.Not.Empty);
            }
        }

        [Test]
        public void DeeperInkRaisesTheSpotMultiplier()
        {
            var t = new WritTuning();
            var gun = new SkylineGun(SkylineGuns.Writ());
            Assert.That(UpgradePaths.Apply(UpgradePaths.Writ()[0], gun, t), Is.True);
            Assert.That(t.spotMultiplier, Is.EqualTo(3.25f));
            UpgradePaths.Apply(UpgradePaths.Writ()[0], gun, t);
            Assert.That(t.spotMultiplier, Is.EqualTo(3.25f), "applying it twice changes nothing");
        }

        [Test]
        public void QuickCellSpeedsTheBreachersReloadAndItsEvents()
        {
            var gun = new SkylineGun(SkylineGuns.Breacher());
            float before = gun.ReloadSeconds(true);
            Assert.That(UpgradePaths.Apply(UpgradePaths.Breacher()[0], gun, null), Is.True);
            Assert.That(gun.ReloadSeconds(true), Is.EqualTo(before / 1.25f).Within(1e-5f));
            UpgradePaths.Apply(UpgradePaths.Breacher()[0], gun, null);
            Assert.That(gun.ReloadSpeed, Is.EqualTo(1.25f), "applying it twice changes nothing");
        }

        [Test]
        public void UnbuiltOrForeignUpgradesDoNothing()
        {
            var t = new WritTuning();
            var writ = new SkylineGun(SkylineGuns.Writ());
            Assert.That(UpgradePaths.Apply(UpgradePaths.Writ()[1], writ, t), Is.False, "not built in M1");
            Assert.That(UpgradePaths.Apply(UpgradePaths.Breacher()[0], writ, t), Is.False, "another gun's");
            Assert.That(writ.ReloadSpeed, Is.EqualTo(1f));
            Assert.That(t.spotMultiplier, Is.EqualTo(2.5f));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WritTests"`
Expected: build FAILS (`WeakSpots` not found).

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/WritSpots.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>Where a shot landed on a bot. WeakPoint is a bot's designed weak point (its lens, its display): what a
    /// headshot is on a robot. Plate is armour that sheds (Enemies.cs).</summary>
    public enum HitZone { Body, Plate, WeakPoint }

    public struct WeakSpot
    {
        public int id;
        /// <summary>Where it sits, in the bot's own space.</summary>
        public Vector3 local;
        public float createdAt;
    }

    /// <summary>The Writ's spot numbers (docs/SKYLINE-DESIGN.md 2.2) as they stand this run; upgrades change them.</summary>
    public sealed class WritTuning
    {
        /// <summary>A spot hit's multiplier: always more than the Writ's weak-point (headshot) multiplier (the identity).</summary>
        public float spotMultiplier = 2.5f;
        public int maxSpotsPerBot = 3;
        public float spotLifetime = 6f;
        /// <summary>How close (m, bot space) a hit must land to a spot to hit it.</summary>
        public float spotRadius = 0.2f;
    }

    /// <summary>The weak spots on one bot: created by Writ hits on its weak point, consumed by Writ hits on them, expiring
    /// after <see cref="WritTuning.spotLifetime"/>, at most <see cref="WritTuning.maxSpotsPerBot"/> (the oldest replaced).</summary>
    public sealed class WeakSpots
    {
        readonly List<WeakSpot> _spots = new List<WeakSpot>();
        int _nextId = 1;

        public int Count => _spots.Count;
        public IReadOnlyList<WeakSpot> All => _spots;

        public void Expire(float now, WritTuning t)
        {
            for (int i = _spots.Count - 1; i >= 0; i--)
                if (now - _spots[i].createdAt >= t.spotLifetime) _spots.RemoveAt(i);
        }

        public WeakSpot Add(Vector3 local, float now, WritTuning t)
        {
            Expire(now, t);
            if (_spots.Count >= Math.Max(1, t.maxSpotsPerBot)) _spots.RemoveAt(0);
            var s = new WeakSpot { id = _nextId++, local = local, createdAt = now };
            _spots.Add(s);
            return s;
        }

        /// <summary>Removes and returns the spot nearest to <paramref name="local"/> within <see cref="WritTuning.spotRadius"/>.</summary>
        public bool TryConsume(Vector3 local, float now, WritTuning t, out WeakSpot spot)
        {
            Expire(now, t);
            int best = -1;
            float bestDistance = t.spotRadius;
            for (int i = 0; i < _spots.Count; i++)
            {
                float d = Vector3.Distance(_spots[i].local, local);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            if (best < 0)
            {
                spot = default;
                return false;
            }
            spot = _spots[best];
            _spots.RemoveAt(best);
            return true;
        }

        public void Clear() => _spots.Clear();
    }

    public struct WritHit
    {
        /// <summary>Multiply the hit's damage by this.</summary>
        public float multiplier;
        public bool spotCreated, spotHit;
        /// <summary>The spot created or hit.</summary>
        public WeakSpot spot;
    }

    public static class Writ
    {
        /// <summary>What a hit at <paramref name="local"/> (bot space) in <paramref name="zone"/> does. A Writ hit
        /// (<paramref name="fromWrit"/>) near a spot consumes it for <see cref="WritTuning.spotMultiplier"/>; otherwise a
        /// weak-point hit takes the gun's <paramref name="weakPointMultiplier"/>, and from the Writ leaves a spot there.</summary>
        public static WritHit Resolve(WeakSpots spots, HitZone zone, Vector3 local, float now, bool fromWrit, float weakPointMultiplier, WritTuning t)
        {
            var r = new WritHit { multiplier = 1f };
            if (spots == null || t == null) return r;
            if (fromWrit && spots.TryConsume(local, now, t, out var hit))
            {
                r.multiplier = t.spotMultiplier;
                r.spotHit = true;
                r.spot = hit;
                return r;
            }
            if (zone == HitZone.WeakPoint)
            {
                r.multiplier = weakPointMultiplier;
                if (fromWrit)
                {
                    r.spot = spots.Add(local, now, t);
                    r.spotCreated = true;
                }
            }
            return r;
        }
    }

    public enum UpgradeKind { Numbers, Augment, Capstone }

    /// <summary>One step of a gun's upgrade path (2.2: numbers and stats, then augments that deepen the identity, then a
    /// capstone that extends it). The augments and capstones are proposals for the creator, built after M1.</summary>
    public sealed class UpgradeDef
    {
        public string id = "", gun = "", name = "", description = "";
        public UpgradeKind kind;
        /// <summary>Built in M1 (the first of each path); the rest are written and built later.</summary>
        public bool built;
    }

    public static class UpgradePaths
    {
        public static UpgradeDef[] Writ() => new[]
        {
            new UpgradeDef { id = "writ-deeper-ink", gun = SkylineGuns.WritId, kind = UpgradeKind.Numbers, built = true, name = "Deeper Ink",
                description = "Spot hits deal x3.25 instead of x2.5." },
            new UpgradeDef { id = "writ-precedent", gun = SkylineGuns.WritId, kind = UpgradeKind.Augment, name = "Precedent",
                description = "After a reload, one extra spot appears on the last bot you hit for every two spot hits the last magazine landed (at most 3)." },
            new UpgradeDef { id = "writ-case-law", gun = SkylineGuns.WritId, kind = UpgradeKind.Capstone, name = "Case Law",
                description = "Any damage can hit the spots: the Breacher's pellets, explosions, a hire's attacks." },
        };

        public static UpgradeDef[] Breacher() => new[]
        {
            new UpgradeDef { id = "breacher-quick-cell", gun = SkylineGuns.BreacherId, kind = UpgradeKind.Numbers, built = true, name = "Quick Cell",
                description = "Reloads 25% faster; the reload's sounds and animation speed up with it." },
            new UpgradeDef { id = "breacher-eviction-notice", gun = SkylineGuns.BreacherId, kind = UpgradeKind.Augment, name = "Eviction Notice",
                description = "A bot knocked into a wall or another bot takes impact damage (and so does the bot it hits)." },
            new UpgradeDef { id = "breacher-forced-entry", gun = SkylineGuns.BreacherId, kind = UpgradeKind.Capstone, name = "Forced Entry",
                description = "A boost's blast knocks back and damages bots within 4 m of where you fired it." },
        };

        /// <summary>Applies a built upgrade to this run's gun (idempotent). False, changing nothing, for one that isn't built
        /// or belongs to another gun.</summary>
        public static bool Apply(UpgradeDef u, SkylineGun gun, WritTuning writ)
        {
            if (u == null || !u.built || gun == null || u.gun != gun.Def.id) return false;
            switch (u.id)
            {
                case "writ-deeper-ink":
                    if (writ == null) return false;
                    writ.spotMultiplier = 3.25f;
                    return true;
                case "breacher-quick-cell":
                    gun.ReloadSpeed = 1.25f;
                    return true;
                default:
                    return false;
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WritTests"`
Expected: PASS (12 tests).

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/WritSpots.cs Packages/com.vibejam.jamkit/Tests/Skyline/WritTests.cs
git commit -m "Skyline M1: the Writ's weak spots (created by weak-point hits, consumed for more than a headshot) and both upgrade paths (first of each built)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The Breacher's self-knockback and the one-boost-per-airtime charge (pure C#)

The Breacher pushes its shooter opposite the aim (2.2). Limits, as tunable data, use the spec's default for pending
question 3: "one boost per airtime, refreshed by landing or touching a wall; no self-damage; it doesn't refresh the
double jump; its height is about the jump upgrade's." Rules decided here: in the air, any self-push needs the charge and
spends it; on the ground a flat shot shoves you back without spending it, and a downward shot lifts you (spending it,
since it starts an airtime); a downward shot **sets** the rise speed (it cancels a fall, never adds to a faster rise),
so a straight-down shot from rest or from a fall lifts exactly `boostHeight`. The double-jump rule belongs to the engine
(Task 12).

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/Breacher.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/BreacherTests.cs`

**Interfaces:**
- Consumes: `System.Numerics.Vector3`; `JamKit.Movement.MoveDef` (test only: `doubleJumpHeight`, `gravity`).
- Produces: `class BreacherBoostDef { float selfPushSpeed = 6f, boostHeight = 1f, minLift = 0.35f; bool
  oneBoostPerAirtime = true; float selfDamage = 0f }`; `class BoostCharge { bool Ready; void Observe(bool grounded,
  bool onWall); void Spend() }`; `static class BreacherKnockback { Vector3 SelfPush(Vector3 aim, Vector3 velocity, bool
  grounded, float gravity, BoostCharge charge, BreacherBoostDef d, out bool boosted) }` (the result is a velocity change
  for `MoveSim.AddPush`, Task 12).

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/BreacherTests.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class BreacherTests
    {
        const float G = 24f;
        static readonly Vector3 Down = new Vector3(0f, -1f, 0f);

        static Vector3 Push(Vector3 aim, Vector3 vel, bool grounded, BoostCharge c, out bool boosted) =>
            BreacherKnockback.SelfPush(aim, vel, grounded, G, c, new BreacherBoostDef(), out boosted);

        [Test]
        public void AStraightDownShotFromRestLiftsTheBoostHeight()
        {
            var p = Push(Down, Vector3.Zero, true, new BoostCharge(), out bool boosted);
            Assert.That(boosted, Is.True);
            Assert.That(p.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(p.Z, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(p.Y * p.Y / (2f * G), Is.EqualTo(new BreacherBoostDef().boostHeight).Within(1e-4f), "rise = v^2 / 2g");
        }

        [Test]
        public void ItCancelsAFallButNeverAddsToAFasterRise()
        {
            float up = MathF.Sqrt(2f * G * 1f);
            Assert.That(Push(Down, new Vector3(0f, -5f, 0f), false, new BoostCharge(), out _).Y, Is.EqualTo(up + 5f).Within(1e-4f));
            var rising = Push(Down, new Vector3(0f, 9f, 0f), false, new BoostCharge(), out bool spent);
            Assert.That(rising.Y, Is.EqualTo(0f));
            Assert.That(spent, Is.True, "an air shot spends the boost even when it adds no lift");
        }

        [Test]
        public void OneBoostPerAirtimeRefreshedByLandingOrAWall()
        {
            var c = new BoostCharge();
            Assert.That(Push(Down, Vector3.Zero, false, c, out _).Y, Is.GreaterThan(0f));
            Assert.That(Push(Down, Vector3.Zero, false, c, out bool again), Is.EqualTo(Vector3.Zero));
            Assert.That(again, Is.False);
            c.Observe(false, false);
            Assert.That(c.Ready, Is.False, "still airborne");
            c.Observe(false, true);
            Assert.That(c.Ready, Is.True, "a wall refreshes it");
            Push(Down, Vector3.Zero, false, c, out _);
            c.Observe(true, false);
            Assert.That(c.Ready, Is.True, "landing refreshes it");
        }

        [Test]
        public void AFlatShotOnTheGroundShovesBackWithoutSpendingTheBoost()
        {
            var c = new BoostCharge();
            var p = Push(new Vector3(0f, 0f, 1f), Vector3.Zero, true, c, out bool boosted);
            Assert.That(p.Z, Is.EqualTo(-new BreacherBoostDef().selfPushSpeed).Within(1e-5f));
            Assert.That(p.Y, Is.EqualTo(0f));
            Assert.That(boosted, Is.False);
            Assert.That(c.Ready, Is.True);
        }

        [Test]
        public void AShotPointingUpOrBarelyDownNeverLifts()
        {
            Assert.That(Push(new Vector3(0f, 0.8f, 0.6f), Vector3.Zero, true, new BoostCharge(), out _).Y, Is.EqualTo(0f));
            Assert.That(Push(Vector3.Normalize(new Vector3(0f, -0.2f, 1f)), Vector3.Zero, true, new BoostCharge(), out _).Y, Is.EqualTo(0f));
        }

        [Test]
        public void AnAngledShotLiftsLessAndPushesBack()
        {
            var aim = Vector3.Normalize(new Vector3(0f, -1f, 1f));
            var p = Push(aim, Vector3.Zero, true, new BoostCharge(), out _);
            Assert.That(p.Y, Is.EqualTo(MathF.Sqrt(2f * G) * 0.7071f).Within(1e-3f));
            Assert.That(p.Z, Is.LessThan(0f));
        }

        [Test]
        public void ABrokenAimGivesNoPush()
        {
            var c = new BoostCharge();
            Assert.That(Push(Vector3.Zero, Vector3.Zero, false, c, out _), Is.EqualTo(Vector3.Zero));
            Assert.That(Push(new Vector3(float.NaN, 0f, 0f), Vector3.Zero, false, c, out _), Is.EqualTo(Vector3.Zero));
            Assert.That(c.Ready, Is.True, "nothing was spent");
        }

        [Test]
        public void TheLimitsAreTheSpecsDefaults()
        {
            var d = new BreacherBoostDef();
            Assert.That(d.oneBoostPerAirtime, Is.True);
            Assert.That(d.selfDamage, Is.EqualTo(0f));
            Assert.That(MathF.Abs(d.boostHeight - new MoveDef().doubleJumpHeight), Is.LessThanOrEqualTo(0.25f),
                "about the jump upgrade's height (a third jump: the double jump's)");
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~BreacherTests"`
Expected: build FAILS (`BreacherKnockback` not found).

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/Breacher.cs`:
```csharp
using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The Breacher's self-knockback (docs/SKYLINE-DESIGN.md 2.2) as tunable data. The limits are the spec's
    /// proposal, used until the creator answers (14, pending 3): one boost per airtime, refreshed by landing or touching a
    /// wall; no self-damage; no double-jump refresh (the engine's rule); about the jump upgrade's height.</summary>
    public sealed class BreacherBoostDef
    {
        /// <summary>m/s along the flat part of the shot's reverse direction.</summary>
        public float selfPushSpeed = 6f;
        /// <summary>Metres a straight-down shot lifts you, from rest or from a fall (it cancels the fall first).</summary>
        public float boostHeight = 1f;
        /// <summary>The shot must point at least this far down (the reverse direction's up part, 0..1) to lift you.</summary>
        public float minLift = 0.35f;
        public bool oneBoostPerAirtime = true;
        /// <summary>Damage the shooter takes from their own blast (none, by the spec's proposal).</summary>
        public float selfDamage = 0f;
    }

    /// <summary>The one boost per airtime: spent by a self-push in the air or one that lifts you off the ground, refreshed
    /// by landing or touching a wall. Call <see cref="Observe"/> every frame with the body's state.</summary>
    public sealed class BoostCharge
    {
        public bool Ready { get; private set; } = true;

        public void Observe(bool grounded, bool onWall)
        {
            if (grounded || onWall) Ready = true;
        }

        public void Spend() => Ready = false;
    }

    public static class BreacherKnockback
    {
        /// <summary>The velocity change a Breacher shot along <paramref name="aim"/> gives its shooter (for MoveSim.AddPush):
        /// the flat part of the reverse direction times <see cref="BreacherBoostDef.selfPushSpeed"/>, plus, when the shot
        /// points down by more than <see cref="BreacherBoostDef.minLift"/>, enough upward speed to reach
        /// sqrt(2 g boostHeight) x (how far down it points) from the current vertical speed (never negative). In the air it
        /// needs the charge (zero without it) and spends it; on the ground it spends it only when it lifts.</summary>
        public static Vector3 SelfPush(Vector3 aim, Vector3 velocity, bool grounded, float gravity, BoostCharge charge, BreacherBoostDef d, out bool boosted)
        {
            boosted = false;
            if (d == null || charge == null) return Vector3.Zero;
            float len = aim.Length();
            if (!(len > 1e-4f) || float.IsInfinity(len)) return Vector3.Zero;
            if (!grounded && d.oneBoostPerAirtime && !charge.Ready) return Vector3.Zero;
            Vector3 back = -aim / len;
            var push = new Vector3(back.X, 0f, back.Z) * d.selfPushSpeed;
            if (back.Y > d.minLift)
            {
                float boostUp = MathF.Sqrt(2f * MathF.Max(0f, gravity) * MathF.Max(0f, d.boostHeight));
                float vy = float.IsNaN(velocity.Y) ? 0f : velocity.Y;
                push.Y = MathF.Max(0f, boostUp * back.Y - vy);
            }
            if (!grounded || push.Y > 0f)
            {
                charge.Spend();
                boosted = true;
            }
            return push;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~BreacherTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/Breacher.cs Packages/com.vibejam.jamkit/Tests/Skyline/BreacherTests.cs
git commit -m "Skyline M1: the Breacher's self-knockback and the one-boost-per-airtime charge, as tunable data

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 8: Enemies as data, plates that shed, the player's plating and chassis (pure C#)

The three M1 bots with their aim and layer tags (2.5), a damage model where **plates shed where you hit them** and
overflow carries into the chassis (2.3), and the Unit's health (2.4): plating over a chassis, plating back from scrap
stripped off fresh wrecks, a chassis that never regenerates.

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/Enemies.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/EnemyTests.cs`

**Interfaces:**
- Consumes: M0's `AimModel { LeadsYou, FiresAtWhereYouAre, Area, Melee }`, `AimRules.CanShareEncounter(params AimModel[])`.
- Produces: `enum ThreatLayer { Floor, Walls, Air }`; `class PlateDef { string id; float hp; PlateDef(string, float) }`;
  `class EnemyDef { string id, displayName; AimModel aim; ThreatLayer layer; float chassisHp; PlateDef[] plates; float
  bodyRadius, alertSeconds, telegraphSeconds, fireInterval, loseSightSeconds, projectileSpeed, projectileRadius,
  attackDamage, range, beamFollowRate, beamHitRadius, beamLockBefore, sweepSeconds, sweepRadius, sweepArcDeg,
  sweepWidthDeg, ankleHeight, scrapPlating }`; `static class SkylineEnemies { const string DroneId = "mall-cop-drone",
  FloorCareId = "floor-care-unit", SpotlightId = "spotlight"; EnemyDef MallCopDrone(); EnemyDef FloorCareUnit();
  EnemyDef Spotlight(); EnemyDef[] All(); EnemyDef Find(string id) }` (new instances each call); `struct BotDamage {
  float toPlate, toChassis; float Applied; int plateShed; bool wrecked }`; `class EnemyBody { EnemyBody(EnemyDef);
  EnemyDef Def; float Chassis; bool Wrecked; int PlateCount; float PlateHp(int); bool PlateOn(int); BotDamage
  Damage(float amount, int plate) }`; `struct ArmorHit { float toPlating, toChassis; bool platingBroke, wrecked }`;
  `class Wreck { EnemyDef def; float wreckedAt; float freshSeconds = 8f; bool stripped; Wreck(EnemyDef, float); bool
  Fresh(float now) }`; `class PlayerArmor { PlayerArmor(float platingMax = 100f, float chassisMax = 100f); float
  PlatingMax, Plating, ChassisMax, Chassis; bool Wrecked; int PlatingBreaks; ArmorHit Damage(float amount); float
  Strip(Wreck w, float now, float scale = 1f) }`.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/EnemyTests.cs`:
```csharp
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class EnemyTests
    {
        static EnemyDef TwoPlates() => new EnemyDef
        {
            id = "test", chassisHp = 50f, plates = new[] { new PlateDef("left", 20f), new PlateDef("right", 20f) },
        };

        [Test]
        public void TheThreeM1BotsCarryTheSpecsTags()
        {
            var drone = SkylineEnemies.MallCopDrone();
            var floor = SkylineEnemies.FloorCareUnit();
            var spot = SkylineEnemies.Spotlight();
            Assert.That(drone.aim, Is.EqualTo(AimModel.LeadsYou));
            Assert.That(drone.layer, Is.EqualTo(ThreatLayer.Air));
            Assert.That(floor.aim, Is.EqualTo(AimModel.Area));
            Assert.That(floor.layer, Is.EqualTo(ThreatLayer.Floor));
            Assert.That(spot.aim, Is.EqualTo(AimModel.FiresAtWhereYouAre));
            Assert.That(spot.layer, Is.EqualTo(ThreatLayer.Air));
            Assert.That(spot.projectileSpeed, Is.EqualTo(0f), "a telegraphed hitscan");
            Assert.That(spot.telegraphSeconds, Is.EqualTo(0.8f), "the spec's 0.8 s telegraph");
            Assert.That(spot.beamLockBefore, Is.EqualTo(0.25f), "locks 0.25 s before the shot");
            Assert.That(AimRules.CanShareEncounter(drone.aim, spot.aim), Is.False, "never in one encounter");
        }

        [Test]
        public void EveryBotHasPlatesAndAWayToHurtYou()
        {
            foreach (var d in SkylineEnemies.All())
            {
                Assert.That(d.plates.Length, Is.GreaterThanOrEqualTo(2), d.id);
                Assert.That(d.attackDamage, Is.GreaterThan(0f), d.id);
                Assert.That(d.fireInterval, Is.GreaterThan(d.telegraphSeconds), d.id + ": the cycle holds the telegraph");
                Assert.That(SkylineEnemies.Find(d.id).id, Is.EqualTo(d.id));
            }
            Assert.That(SkylineEnemies.Find("nope"), Is.Null);
        }

        [Test]
        public void APlateAbsorbsFirstAndShedsOnce()
        {
            var b = new EnemyBody(TwoPlates());
            var first = b.Damage(15f, 0);
            Assert.That(first.toPlate, Is.EqualTo(15f));
            Assert.That(first.toChassis, Is.EqualTo(0f));
            Assert.That(first.plateShed, Is.EqualTo(-1));
            var second = b.Damage(5f, 0);
            Assert.That(second.plateShed, Is.EqualTo(0), "shed where it was hit");
            Assert.That(b.PlateOn(0), Is.False);
            Assert.That(b.PlateOn(1), Is.True, "the other plate stays");
        }

        [Test]
        public void DamagePastAPlateCarriesIntoTheChassis()
        {
            var b = new EnemyBody(TwoPlates());
            var r = b.Damage(30f, 1);
            Assert.That(r.toPlate, Is.EqualTo(20f));
            Assert.That(r.toChassis, Is.EqualTo(10f));
            Assert.That(r.Applied, Is.EqualTo(30f));
            Assert.That(r.plateShed, Is.EqualTo(1));
            Assert.That(b.Chassis, Is.EqualTo(40f));
        }

        [Test]
        public void HitsWhereAPlateWasShedOrOnNoPlateGoToTheChassis()
        {
            var b = new EnemyBody(TwoPlates());
            b.Damage(20f, 0);
            var r = b.Damage(5f, 0);
            Assert.That(r.toChassis, Is.EqualTo(5f));
            Assert.That(r.plateShed, Is.EqualTo(-1), "a plate sheds once");
            Assert.That(b.Damage(5f, -1).toChassis, Is.EqualTo(5f));
            Assert.That(b.Damage(5f, 7).toChassis, Is.EqualTo(5f), "an index out of range is the chassis");
        }

        [Test]
        public void AWreckHappensOnceAndLaterHitsDoNothing()
        {
            var b = new EnemyBody(TwoPlates());
            var r = b.Damage(500f, -1);
            Assert.That(r.wrecked, Is.True);
            Assert.That(r.toChassis, Is.EqualTo(50f), "only what was left");
            var after = b.Damage(10f, 0);
            Assert.That(after.wrecked, Is.False);
            Assert.That(after.Applied, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroNegativeNaNOrInfiniteDamageIsSafe()
        {
            var b = new EnemyBody(TwoPlates());
            Assert.That(b.Damage(0f, 0).Applied, Is.EqualTo(0f));
            Assert.That(b.Damage(-10f, 0).Applied, Is.EqualTo(0f));
            Assert.That(b.Damage(float.NaN, 0).Applied, Is.EqualTo(0f));
            Assert.That(b.Chassis, Is.EqualTo(50f));
            Assert.That(b.Damage(float.PositiveInfinity, -1).wrecked, Is.True);
            Assert.That(float.IsNaN(b.Chassis), Is.False);
        }

        [Test]
        public void PlatingAbsorbsFirstThenTheChassis()
        {
            var a = new PlayerArmor(100f, 100f);
            var r = a.Damage(130f);
            Assert.That(r.toPlating, Is.EqualTo(100f));
            Assert.That(r.toChassis, Is.EqualTo(30f));
            Assert.That(r.platingBroke, Is.True);
            Assert.That(a.PlatingBreaks, Is.EqualTo(1));
            Assert.That(a.Damage(10f).platingBroke, Is.False, "already broken");
            Assert.That(a.PlatingBreaks, Is.EqualTo(1));
        }

        [Test]
        public void ScrapFromAFreshWreckRestoresPlatingOnce()
        {
            var a = new PlayerArmor(100f, 100f);
            a.Damage(60f);
            var w = new Wreck(SkylineEnemies.MallCopDrone(), 10f);
            float got = a.Strip(w, 12f);
            Assert.That(got, Is.EqualTo(SkylineEnemies.MallCopDrone().scrapPlating));
            Assert.That(a.Plating, Is.EqualTo(40f + got));
            Assert.That(a.Strip(w, 12.5f), Is.EqualTo(0f), "a wreck is stripped once");
        }

        [Test]
        public void AStaleWreckOrFullPlatingGivesNothing()
        {
            var a = new PlayerArmor(100f, 100f);
            a.Damage(50f);
            Assert.That(a.Strip(new Wreck(SkylineEnemies.MallCopDrone(), 0f), 9f), Is.EqualTo(0f), "older than 8 s");
            var full = new PlayerArmor(100f, 100f);
            var w = new Wreck(SkylineEnemies.MallCopDrone(), 0f);
            Assert.That(full.Strip(w, 1f), Is.EqualTo(0f));
            Assert.That(w.stripped, Is.False, "left for later if you couldn't use it");
        }

        [Test]
        public void TheChassisNeverComesBackFromScrap()
        {
            var a = new PlayerArmor(100f, 100f);
            a.Damage(150f);
            a.Strip(new Wreck(SkylineEnemies.FloorCareUnit(), 0f), 1f);
            Assert.That(a.Chassis, Is.EqualTo(50f));
            Assert.That(a.Plating, Is.GreaterThan(0f));
        }

        [Test]
        public void AWreckedUnitTakesNoMoreDamage()
        {
            var a = new PlayerArmor(10f, 10f);
            Assert.That(a.Damage(25f).wrecked, Is.True);
            Assert.That(a.Damage(5f).toChassis, Is.EqualTo(0f));
            Assert.That(a.Damage(float.NaN).toPlating, Is.EqualTo(0f));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EnemyTests"`
Expected: build FAILS (`EnemyDef` not found).

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/Enemies.cs`:
```csharp
using System;

namespace JamKit.Skyline
{
    /// <summary>The layer of the player's space a bot threatens (docs/SKYLINE-DESIGN.md 2.5-2.6).</summary>
    public enum ThreatLayer { Floor, Walls, Air }

    /// <summary>A plate of armour over a bot's chassis; it absorbs hits where it sits and sheds when broken (2.3).</summary>
    public sealed class PlateDef
    {
        public string id;
        public float hp;

        public PlateDef(string id, float hp)
        {
            this.id = id;
            this.hp = hp;
        }
    }

    /// <summary>One bot as data (2.5): its tags, its body and its attack. Each bot's identity agent owns and tunes it; the
    /// dodge measurement (Task 11) tunes the aimed attacks on Normal.</summary>
    public sealed class EnemyDef
    {
        public string id = "", displayName = "";
        public AimModel aim;
        public ThreatLayer layer;
        public float chassisHp = 50f;
        public PlateDef[] plates = new PlateDef[0];
        /// <summary>The bot's size for aim assist and line-of-sight checks (m).</summary>
        public float bodyRadius = 0.5f;
        // The behaviour clock (BotBrain): alert, then a cycle of telegraph + attack + recovery.
        public float alertSeconds = 0.6f;
        public float telegraphSeconds = 0.4f;
        /// <summary>One full attack cycle (s): the telegraph plus the recovery.</summary>
        public float fireInterval = 1.4f;
        public float loseSightSeconds = 3f;
        // A shot.
        /// <summary>m/s; 0 is a telegraphed hitscan (the spotlight).</summary>
        public float projectileSpeed = 30f;
        public float projectileRadius = 0.15f;
        public float attackDamage = 10f;
        public float range = 40f;
        // The spotlight's beam (TelegraphedShot); its telegraph is telegraphSeconds.
        public float beamFollowRate = 10f, beamHitRadius = 0.6f, beamLockBefore = 0.25f;
        // The floor-care unit's acid sweep (AcidSweep).
        public float sweepSeconds = 0.8f, sweepRadius = 4.5f, sweepArcDeg = 120f, sweepWidthDeg = 24f, ankleHeight = 0.4f;
        /// <summary>Plating a fresh wreck gives back when stripped, on Normal (2.4, 7.4).</summary>
        public float scrapPlating = 20f;
    }

    /// <summary>M1's three bots (11.2). Each call returns a new instance.</summary>
    public static class SkylineEnemies
    {
        public const string DroneId = "mall-cop-drone", FloorCareId = "floor-care-unit", SpotlightId = "spotlight";

        /// <summary>Officious; shots that lead your movement (punishes straight lines). Aim: leads you. Layer: air.</summary>
        public static EnemyDef MallCopDrone() => new EnemyDef
        {
            id = DroneId, displayName = "Mall-cop drone", aim = AimModel.LeadsYou, layer = ThreatLayer.Air,
            chassisHp = 50f, plates = new[] { new PlateDef("shell", 30f), new PlateDef("lens-guard", 20f) }, bodyRadius = 0.45f,
            alertSeconds = 0.6f, telegraphSeconds = 0.35f, fireInterval = 1.2f, loseSightSeconds = 3f,
            projectileSpeed = 30f, projectileRadius = 0.15f, attackDamage = 12f, range = 45f, scrapPlating = 20f,
        };

        /// <summary>A roomba with a happy face, angry at "stains"; an ankle-height acid sweep (punishes staying on the floor).
        /// Aim: area. Layer: floor.</summary>
        public static EnemyDef FloorCareUnit() => new EnemyDef
        {
            id = FloorCareId, displayName = "Floor-care unit", aim = AimModel.Area, layer = ThreatLayer.Floor,
            chassisHp = 80f, plates = new[] { new PlateDef("lid", 40f), new PlateDef("bumper", 30f) }, bodyRadius = 0.5f,
            alertSeconds = 0.8f, telegraphSeconds = 0.6f, fireInterval = 3f, loseSightSeconds = 3f,
            projectileSpeed = 0f, attackDamage = 8f, range = 10f,
            sweepSeconds = 0.8f, sweepRadius = 4.5f, sweepArcDeg = 120f, sweepWidthDeg = 24f, ankleHeight = 0.4f, scrapPlating = 30f,
        };

        /// <summary>A searchlight with a rifle: a 0.8 s laser telegraph that follows you with a lag and locks 0.25 s before a
        /// hitscan shot (punishes lingering exposed up high). Aim: fires at where you are now. Layer: air.</summary>
        public static EnemyDef Spotlight() => new EnemyDef
        {
            id = SpotlightId, displayName = "Spotlight", aim = AimModel.FiresAtWhereYouAre, layer = ThreatLayer.Air,
            chassisHp = 70f, plates = new[] { new PlateDef("housing-left", 30f), new PlateDef("housing-right", 30f) }, bodyRadius = 0.6f,
            alertSeconds = 0.5f, telegraphSeconds = 0.8f, fireInterval = 2.2f, loseSightSeconds = 3f,
            projectileSpeed = 0f, attackDamage = 30f, range = 60f,
            beamFollowRate = 10f, beamHitRadius = 0.6f, beamLockBefore = 0.25f, scrapPlating = 25f,
        };

        public static EnemyDef[] All() => new[] { MallCopDrone(), FloorCareUnit(), Spotlight() };

        public static EnemyDef Find(string id) =>
            id == DroneId ? MallCopDrone() : id == FloorCareId ? FloorCareUnit() : id == SpotlightId ? Spotlight() : null;
    }

    public struct BotDamage
    {
        public float toPlate, toChassis;
        /// <summary>The plate this hit knocked off, or -1.</summary>
        public int plateShed;
        /// <summary>This hit wrecked the bot.</summary>
        public bool wrecked;
        public float Applied => toPlate + toChassis;
    }

    /// <summary>A bot's plates and chassis. A hit on a plate that's still on is absorbed by it first; what's left carries
    /// into the chassis. A plate sheds once, where it was hit; the bot is wrecked once.</summary>
    public sealed class EnemyBody
    {
        readonly float[] _plates;

        public EnemyDef Def { get; }
        public float Chassis { get; private set; }
        public bool Wrecked { get; private set; }
        public int PlateCount => _plates.Length;

        public EnemyBody(EnemyDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            int n = def.plates != null ? def.plates.Length : 0;
            _plates = new float[n];
            for (int i = 0; i < n; i++) _plates[i] = Math.Max(0f, def.plates[i].hp);
            Chassis = Math.Max(1f, def.chassisHp);
        }

        public float PlateHp(int i) => i >= 0 && i < _plates.Length ? _plates[i] : 0f;
        public bool PlateOn(int i) => PlateHp(i) > 0f;

        /// <summary><paramref name="amount"/> after every multiplier, at <paramref name="plate"/> (an index into the def's
        /// plates; -1 or a shed plate means the chassis). Zero, negative and NaN do nothing; huge amounts are capped.</summary>
        public BotDamage Damage(float amount, int plate)
        {
            var r = new BotDamage { plateShed = -1 };
            if (Wrecked || !(amount > 0f)) return r;
            float left = Math.Min(amount, 1e6f);
            if (PlateOn(plate))
            {
                float take = Math.Min(left, _plates[plate]);
                _plates[plate] -= take;
                left -= take;
                r.toPlate = take;
                if (_plates[plate] <= 0f)
                {
                    _plates[plate] = 0f;
                    r.plateShed = plate;
                }
            }
            if (left > 0f)
            {
                float take = Math.Min(left, Chassis);
                Chassis -= take;
                r.toChassis = take;
                if (Chassis <= 0f)
                {
                    Chassis = 0f;
                    Wrecked = true;
                    r.wrecked = true;
                }
            }
            return r;
        }
    }

    public struct ArmorHit
    {
        public float toPlating, toChassis;
        /// <summary>This hit took the plating to zero (the first time is Dot's teaching beat, 6.8).</summary>
        public bool platingBroke;
        public bool wrecked;
    }

    /// <summary>A wrecked bot's remains: scrap can be stripped once while it's fresh (2.4: "aggression heals").</summary>
    public sealed class Wreck
    {
        public EnemyDef def;
        public float wreckedAt;
        public float freshSeconds = 8f;
        public bool stripped;

        public Wreck(EnemyDef def, float wreckedAt)
        {
            this.def = def;
            this.wreckedAt = wreckedAt;
        }

        public bool Fresh(float now) => !stripped && now >= wreckedAt && now - wreckedAt <= freshSeconds;
    }

    /// <summary>The Unit's health (2.4): plating over the chassis. Plating absorbs first and comes back only from scrap
    /// stripped off fresh wrecks; the chassis never regenerates (repair rewards and vending machines come later).</summary>
    public sealed class PlayerArmor
    {
        public float PlatingMax { get; }
        public float Plating { get; private set; }
        public float ChassisMax { get; }
        public float Chassis { get; private set; }
        public bool Wrecked { get; private set; }
        /// <summary>How many times the plating has broken this run.</summary>
        public int PlatingBreaks { get; private set; }

        public PlayerArmor(float platingMax = 100f, float chassisMax = 100f)
        {
            PlatingMax = Math.Max(0f, platingMax);
            Plating = PlatingMax;
            ChassisMax = Math.Max(1f, chassisMax);
            Chassis = ChassisMax;
        }

        public ArmorHit Damage(float amount)
        {
            var r = new ArmorHit();
            if (Wrecked || !(amount > 0f)) return r;
            float left = Math.Min(amount, 1e6f);
            if (Plating > 0f)
            {
                float take = Math.Min(left, Plating);
                Plating -= take;
                left -= take;
                r.toPlating = take;
                if (Plating <= 0f)
                {
                    Plating = 0f;
                    r.platingBroke = true;
                    PlatingBreaks++;
                }
            }
            if (left > 0f)
            {
                float take = Math.Min(left, Chassis);
                Chassis -= take;
                r.toChassis = take;
                if (Chassis <= 0f)
                {
                    Chassis = 0f;
                    Wrecked = true;
                    r.wrecked = true;
                }
            }
            return r;
        }

        /// <summary>Strips a fresh wreck: plating back by its scrap (times <paramref name="scale"/>, the difficulty's), up to
        /// the max. Returns what was gained; a wreck is used up only when it gave something.</summary>
        public float Strip(Wreck w, float now, float scale = 1f)
        {
            if (Wrecked || w == null || w.def == null || !w.Fresh(now)) return 0f;
            float gain = Math.Min(Math.Max(0f, w.def.scrapPlating * (scale > 0f ? scale : 0f)), PlatingMax - Plating);
            if (!(gain > 0f)) return 0f;
            Plating += gain;
            w.stripped = true;
            return gain;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EnemyTests"`
Expected: PASS (12 tests).

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/Enemies.cs Packages/com.vibejam.jamkit/Tests/Skyline/EnemyTests.cs
git commit -m "Skyline M1: the three bots as data (aim and layer tags), plates that shed where hit, the Unit's plating and chassis with scrap from fresh wrecks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Bot brains, the acid sweep, and the spotlight's sweep lock (pure C#)

Each bot runs a behaviour clock (idle, alert, a telegraph-attack-recover cycle) that drives its face, sounds and
attacks (2.5). The floor-care unit's attack is an ankle-height acid sweep you jump over. The spotlight's beam is
changed to fix M0's deferred finding ("the spotlight's lag lets straight running dodge it"): the beam now tracks with a
critically damped follow that also matches your velocity, and **at the lock it keeps sweeping at the velocity it had**.
So running straight keeps you in the beam (hit), and changing direction in the last 0.25 s leaves it (dodge), which is
exactly the spec's "dodged by changing direction in that last 0.25 s or breaking line of sight" (2.5).

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/BotBrain.cs`
- Modify: `Packages/com.vibejam.jamkit/Runtime/Skyline/Aim.cs` (the `TelegraphedShot` class and a null guard in `AimRules`)
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/BotBrainTests.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/SpotlightBeamTests.cs`

**Interfaces:**
- Consumes: Task 8's `EnemyDef` (`alertSeconds, telegraphSeconds, fireInterval, loseSightSeconds, sweepSeconds,
  sweepRadius, sweepArcDeg, sweepWidthDeg, ankleHeight`), Task 3's `AimAssist.DeltaAngle`, `JamKit.Rng(ulong)` with
  `Range(float, float)`.
- Produces: `enum BotState { Idle, Alert, Telegraph, Recover, Wrecked }`; `enum BotFace { Happy, Alert, Angry, Hurt,
  Wrecked }`; `enum BotCueKind { State, Attack, Bark }`; `struct BotCue { BotCueKind kind; BotState state; string
  bark }` (barks: `"spotted"`, `"hurt"`, `"lost"`, `"wrecked"`); `class BotBrain { const float HurtSeconds = 0.25f;
  BotBrain(EnemyDef, ulong seed); BotState State; float StateTime; BotFace Face; void Tick(float dt, bool seesPlayer,
  List<BotCue> cues); void OnDamaged(List<BotCue> cues); void OnWrecked(List<BotCue> cues) }`; `static class AcidSweep
  { bool Touches(Vector3 unit, float unitYawDeg, float t, Vector3 feet, float floorY, EnemyDef d) }`;
  `TelegraphedShot` keeps its members (`telegraph, lockBefore, followRate, hitRadius, Active, Locked, AimPoint,
  Start(Vector3), Step(float, Vector3, out bool)`), adds `AimVelocity`, and changes defaults to `followRate = 10f`,
  `hitRadius = 0.6f`. The Unity spotlight starts a `TelegraphedShot` when its brain enters Telegraph; the brain's Attack
  cue comes when the shot fires.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/BotBrainTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class BotBrainTests
    {
        const float H = 1f / 60f;

        /// <summary>A fixed clock for the timing tests, so Task 11's retuning of the shipped bots can't move them.</summary>
        static EnemyDef Clock() => new EnemyDef
        {
            id = "clock", alertSeconds = 0.6f, telegraphSeconds = 0.35f, fireInterval = 1.2f, loseSightSeconds = 3f,
        };

        static int Attacks(BotBrain b, float seconds, float dt, bool sees = true)
        {
            var cues = new List<BotCue>();
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) b.Tick(dt, sees, cues);
            return cues.FindAll(c => c.kind == BotCueKind.Attack).Count;
        }

        [Test]
        public void AnIdleBotWakesWhenItSeesYou()
        {
            var b = new BotBrain(SkylineEnemies.MallCopDrone(), 1);
            var cues = new List<BotCue>();
            b.Tick(H, false, cues);
            Assert.That(b.State, Is.EqualTo(BotState.Idle));
            Assert.That(b.Face, Is.EqualTo(BotFace.Happy));
            Assert.That(cues.Count, Is.EqualTo(0));
            b.Tick(H, true, cues);
            Assert.That(b.State, Is.EqualTo(BotState.Alert));
            Assert.That(b.Face, Is.EqualTo(BotFace.Alert));
            Assert.That(cues.Exists(c => c.kind == BotCueKind.Bark && c.bark == "spotted"), Is.True);
        }

        [Test]
        public void ItAttacksOncePerCycleAlwaysAfterItsTelegraph()
        {
            var d = Clock();
            var b = new BotBrain(d, 1);
            var cues = new List<BotCue>();
            var attacks = new List<float>();
            float t = 0f, telegraphAt = -1f;
            for (int i = 0; i < 600; i++)
            {
                t += H;
                int before = cues.Count;
                b.Tick(H, true, cues);
                for (int k = before; k < cues.Count; k++)
                {
                    if (cues[k].kind == BotCueKind.State && cues[k].state == BotState.Telegraph) telegraphAt = t;
                    if (cues[k].kind == BotCueKind.Attack)
                    {
                        Assert.That(t - telegraphAt, Is.EqualTo(d.telegraphSeconds).Within(H + 1e-4f), "the telegraph always comes first");
                        Assert.That(b.Face, Is.EqualTo(BotFace.Angry));
                        attacks.Add(t);
                    }
                }
            }
            Assert.That(attacks.Count, Is.InRange(7, 8));
            for (int i = 1; i < attacks.Count; i++)
                Assert.That(attacks[i] - attacks[i - 1], Is.EqualTo(d.fireInterval).Within(H + 1e-4f));
        }

        [Test]
        public void TheCycleHoldsAt20And144Fps()
        {
            int slow = Attacks(new BotBrain(Clock(), 3), 30f, 1f / 20f);
            int fast = Attacks(new BotBrain(Clock(), 3), 30f, 1f / 144f);
            Assert.That(Math.Abs(slow - fast), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ItGivesUpAfterLosingSightForAWhile()
        {
            var b = new BotBrain(Clock(), 1);
            Attacks(b, 2f, H);
            var cues = new List<BotCue>();
            for (int i = 0; i < 300; i++) b.Tick(H, false, cues);
            Assert.That(b.State, Is.EqualTo(BotState.Idle));
            Assert.That(cues.Exists(c => c.kind == BotCueKind.Bark && c.bark == "lost"), Is.True);
        }

        [Test]
        public void BeingShotWakesAnIdleBotAndFlinchesItsFace()
        {
            var b = new BotBrain(SkylineEnemies.FloorCareUnit(), 1);
            var cues = new List<BotCue>();
            b.OnDamaged(cues);
            Assert.That(b.State, Is.EqualTo(BotState.Alert));
            Assert.That(b.Face, Is.EqualTo(BotFace.Hurt));
            for (int i = 0; i < 20; i++) b.Tick(H, true, cues);
            Assert.That(b.Face, Is.Not.EqualTo(BotFace.Hurt), "the flinch lasts 0.25 s");
        }

        [Test]
        public void AWreckedBotDoesNothing()
        {
            var b = new BotBrain(SkylineEnemies.MallCopDrone(), 1);
            var cues = new List<BotCue>();
            b.OnWrecked(cues);
            Assert.That(b.State, Is.EqualTo(BotState.Wrecked));
            Assert.That(b.Face, Is.EqualTo(BotFace.Wrecked));
            Assert.That(cues.Exists(c => c.kind == BotCueKind.Bark && c.bark == "wrecked"), Is.True);
            int n = cues.Count;
            for (int i = 0; i < 300; i++) b.Tick(H, true, cues);
            b.OnDamaged(cues);
            Assert.That(cues.Count, Is.EqualTo(n));
        }

        [Test]
        public void TwoBotsDontAttackInLockstep()
        {
            float First(ulong seed)
            {
                var b = new BotBrain(SkylineEnemies.MallCopDrone(), seed);
                var cues = new List<BotCue>();
                for (int i = 0; i < 300; i++)
                {
                    b.Tick(H, true, cues);
                    if (cues.Exists(c => c.kind == BotCueKind.Attack)) return i * H;
                }
                return -1f;
            }
            var firsts = new HashSet<float>();
            for (ulong seed = 1; seed <= 5; seed++) firsts.Add(First(seed));
            Assert.That(firsts.Count, Is.GreaterThan(1), "the seeded jitter spreads the first shots");
            Assert.That(firsts.Contains(-1f), Is.False);
        }

        static readonly EnemyDef Floor = SkylineEnemies.FloorCareUnit();

        static Vector3 AtBearing(float deg, float dist) =>
            new Vector3(MathF.Sin(deg * MathF.PI / 180f) * dist, 0f, MathF.Cos(deg * MathF.PI / 180f) * dist);

        [Test]
        public void TheAcidSweepCrossesItsArcAtAnkleHeight()
        {
            var left = AtBearing(-60f, 3f);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, 0f, left, 0f, Floor), Is.True, "the jet starts on the left");
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, Floor.sweepSeconds, left, 0f, Floor), Is.False, "and ends on the right");
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, Floor.sweepSeconds, AtBearing(60f, 3f), 0f, Floor), Is.True);
        }

        [Test]
        public void JumpingOverItOrStandingOutOfReachIsSafe()
        {
            var left = AtBearing(-60f, 3f);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, 0f, left + new Vector3(0f, 0.5f, 0f), 0f, Floor), Is.False, "guests are advised to jump");
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, 0f, AtBearing(-60f, 5f), 0f, Floor), Is.False);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, -0.1f, left, 0f, Floor), Is.False, "not before the sweep");
            for (float t = 0f; t <= Floor.sweepSeconds; t += 0.02f)
                Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, t, AtBearing(180f, 2f), 0f, Floor), Is.False, "never behind it");
        }

        [Test]
        public void TheSweepFollowsTheUnitsFacing()
        {
            var right = AtBearing(90f - 60f, 3f);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 90f, 0f, right, 0f, Floor), Is.True);
        }
    }
}
```

`Packages/com.vibejam.jamkit/Tests/Skyline/SpotlightBeamTests.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The spotlight (docs/SKYLINE-DESIGN.md 2.5): dodged by changing direction in the last 0.25 s, not by
    /// running straight.</summary>
    public class SpotlightBeamTests
    {
        static bool RunShot(Func<float, Vector3> targetAt, float h = 1f / 120f)
        {
            var s = new TelegraphedShot();
            float t = 0f;
            s.Start(targetAt(0f));
            while (true)
            {
                t += h;
                if (s.Step(h, targetAt(t), out bool hit)) return hit;
                if (t > 5f) Assert.Fail("the shot never fired");
            }
        }

        static Vector3 Run(float x) => new Vector3(x, 0f, 20f);

        [Test]
        public void ASteadyRunIsCaught()
        {
            Assert.That(RunShot(t => Run(7f * t)), Is.True);
        }

        [Test]
        public void TurningAfterTheLockDodges()
        {
            Assert.That(RunShot(t => Run(t < 0.6f ? 7f * t : 4.2f - 7f * (t - 0.6f))), Is.False);
        }

        [Test]
        public void TurningLongBeforeTheLockIsStillCaught()
        {
            Assert.That(RunShot(t => Run(t < 0.2f ? 7f * t : 1.4f - 7f * (t - 0.2f))), Is.True);
        }

        [Test]
        public void TheBeamHoldsAt20Fps()
        {
            Assert.That(RunShot(t => Run(7f * t), 1f / 20f), Is.True);
            Assert.That(RunShot(t => Run(t < 0.6f ? 7f * t : 4.2f - 7f * (t - 0.6f)), 1f / 20f), Is.False);
        }

        [Test]
        public void ABrokenStepChangesNothing()
        {
            var s = new TelegraphedShot();
            s.Start(Vector3.Zero);
            Assert.That(s.Step(float.NaN, Vector3.Zero, out _), Is.False);
            Assert.That(s.Locked, Is.False);
            Assert.That(float.IsNaN(s.AimPoint.X), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~BotBrainTests|FullyQualifiedName~SpotlightBeamTests"`
Expected: build FAILS (`BotBrain` not found). (Once BotBrain exists, `ASteadyRunIsCaught` fails against M0's beam: the
lag leaves the beam about 1.2 m behind a 7 m/s run.)

- [ ] **Step 3: Write the brain and the sweep**

`Packages/com.vibejam.jamkit/Runtime/Skyline/BotBrain.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    public enum BotState { Idle, Alert, Telegraph, Recover, Wrecked }

    /// <summary>What the bot's face or display shows (2.5: "every enemy has a face or display").</summary>
    public enum BotFace { Happy, Alert, Angry, Hurt, Wrecked }

    public enum BotCueKind { State, Attack, Bark }

    public struct BotCue
    {
        public BotCueKind kind;
        /// <summary>State: the state entered.</summary>
        public BotState state;
        /// <summary>Bark: "spotted", "hurt", "lost" or "wrecked" (each bot's agent writes the lines and the babble voice).</summary>
        public string bark;
    }

    /// <summary>
    /// One bot's behaviour clock (docs/SKYLINE-DESIGN.md 2.5: idle, alert and attack states). Seeing the player (or being
    /// shot) alerts it; after <see cref="EnemyDef.alertSeconds"/> plus a seeded jitter of up to 0.4 s (so bots don't fire in
    /// lockstep) it cycles: a telegraph of <see cref="EnemyDef.telegraphSeconds"/>, the attack (a cue), then recovery, one
    /// cycle every <see cref="EnemyDef.fireInterval"/>. A started telegraph always finishes; out of sight for
    /// <see cref="EnemyDef.loseSightSeconds"/> it goes back to idle. Frame-rate independent: time past a deadline carries
    /// into the next state.
    /// </summary>
    public sealed class BotBrain
    {
        public const float HurtSeconds = 0.25f;

        readonly EnemyDef _def;
        readonly Rng _rng;
        float _t, _unseen, _hurt, _alertFor;

        public BotState State { get; private set; } = BotState.Idle;
        public float StateTime => _t;
        public BotFace Face =>
            State == BotState.Wrecked ? BotFace.Wrecked :
            _hurt > 0f ? BotFace.Hurt :
            State == BotState.Idle ? BotFace.Happy :
            State == BotState.Alert ? BotFace.Alert : BotFace.Angry;

        public BotBrain(EnemyDef def, ulong seed)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _rng = new Rng(seed);
        }

        public void Tick(float dt, bool seesPlayer, List<BotCue> cues)
        {
            if (State == BotState.Wrecked || !(dt > 0f) || float.IsInfinity(dt)) return;
            _t += dt;
            _hurt = Math.Max(0f, _hurt - dt);
            _unseen = seesPlayer ? 0f : _unseen + dt;
            bool lost = _unseen >= _def.loseSightSeconds;
            float recover = Math.Max(0f, _def.fireInterval - _def.telegraphSeconds);
            switch (State)
            {
                case BotState.Idle:
                    if (seesPlayer) Alert(cues, "spotted");
                    break;
                case BotState.Alert:
                    if (lost) GiveUp(cues);
                    else if (_t >= _alertFor) Go(BotState.Telegraph, _t - _alertFor, cues);
                    break;
                case BotState.Telegraph:
                    if (_t >= _def.telegraphSeconds)
                    {
                        cues?.Add(new BotCue { kind = BotCueKind.Attack, state = BotState.Telegraph });
                        Go(BotState.Recover, _t - _def.telegraphSeconds, cues);
                    }
                    break;
                case BotState.Recover:
                    if (_t >= recover)
                    {
                        if (lost) GiveUp(cues);
                        else Go(BotState.Telegraph, _t - recover, cues);
                    }
                    break;
            }
        }

        /// <summary>A hit: the face flinches for <see cref="HurtSeconds"/>, and an idle bot wakes up.</summary>
        public void OnDamaged(List<BotCue> cues)
        {
            if (State == BotState.Wrecked) return;
            _hurt = HurtSeconds;
            _unseen = 0f;
            if (State == BotState.Idle) Alert(cues, "hurt");
        }

        public void OnWrecked(List<BotCue> cues)
        {
            if (State == BotState.Wrecked) return;
            Go(BotState.Wrecked, 0f, cues);
            Bark("wrecked", cues);
        }

        void Alert(List<BotCue> cues, string bark)
        {
            _alertFor = _def.alertSeconds + _rng.Range(0f, 0.4f);
            Go(BotState.Alert, 0f, cues);
            Bark(bark, cues);
        }

        void GiveUp(List<BotCue> cues)
        {
            Go(BotState.Idle, 0f, cues);
            Bark("lost", cues);
        }

        void Go(BotState s, float carry, List<BotCue> cues)
        {
            State = s;
            _t = Math.Max(0f, carry);
            cues?.Add(new BotCue { kind = BotCueKind.State, state = s });
        }

        static void Bark(string id, List<BotCue> cues) => cues?.Add(new BotCue { kind = BotCueKind.Bark, bark = id });
    }

    /// <summary>The floor-care unit's attack (2.5: "ankle-height acid sweep"; CONCIERGE: "Guests are advised to jump").</summary>
    public static class AcidSweep
    {
        /// <summary>Whether the acid jet, <paramref name="t"/> seconds into a sweep, touches a player whose feet are at
        /// <paramref name="feet"/>. The jet sweeps <see cref="EnemyDef.sweepArcDeg"/> across the unit's facing, left to
        /// right, over <see cref="EnemyDef.sweepSeconds"/>, out to <see cref="EnemyDef.sweepRadius"/>, and is
        /// <see cref="EnemyDef.sweepWidthDeg"/> wide. Feet more than <see cref="EnemyDef.ankleHeight"/> above the floor clear it.</summary>
        public static bool Touches(Vector3 unit, float unitYawDeg, float t, Vector3 feet, float floorY, EnemyDef d)
        {
            if (d == null || !(t >= 0f) || t > d.sweepSeconds) return false;
            if (feet.Y - floorY > d.ankleHeight) return false;
            float dx = feet.X - unit.X, dz = feet.Z - unit.Z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (!(dist <= d.sweepRadius)) return false;
            if (dist < 0.3f) return true;   // standing on the nozzle
            float jet = unitYawDeg - d.sweepArcDeg * 0.5f + d.sweepArcDeg * (d.sweepSeconds > 0f ? t / d.sweepSeconds : 1f);
            float bearing = MathF.Atan2(dx, dz) * (180f / MathF.PI);
            return MathF.Abs(AimAssist.DeltaAngle(jet, bearing)) <= d.sweepWidthDeg * 0.5f;
        }
    }
}
```

- [ ] **Step 4: Change the spotlight's beam**

In `Packages/com.vibejam.jamkit/Runtime/Skyline/Aim.cs`, add a null guard as the first line of
`AimRules.CanShareEncounter`:
```csharp
            if (models == null) return true;
```
and replace the whole `TelegraphedShot` class with:
```csharp
    /// <summary>The spotlight's shot (2.5): a telegraph of <see cref="telegraph"/> seconds whose beam follows the target with a
    /// lag, locks <see cref="lockBefore"/> seconds before the shot, then a hitscan where the beam is. The beam is a
    /// critically damped follow with the target's velocity fed forward, so it settles onto a steady run; at the lock it
    /// keeps sweeping at the velocity it had. Running straight stays in the beam; changing direction in the last
    /// <see cref="lockBefore"/> seconds leaves it. Line of sight is the caller's: a shot that fires behind cover misses.</summary>
    public sealed class TelegraphedShot
    {
        public float telegraph = 0.8f;
        public float lockBefore = 0.25f;
        /// <summary>How tightly the beam tracks (1/s): it settles onto a steady run within about 4 / followRate seconds.</summary>
        public float followRate = 10f;
        /// <summary>How far from the beam's point still counts as a hit (m).</summary>
        public float hitRadius = 0.6f;

        float _t;
        Vector3 _aim, _aimVel, _lastTarget;

        public bool Active { get; private set; }
        public bool Locked { get; private set; }
        public Vector3 AimPoint => _aim;
        /// <summary>How fast the beam's point is sweeping (m/s).</summary>
        public Vector3 AimVelocity => _aimVel;

        public void Start(Vector3 targetNow)
        {
            Active = true;
            Locked = false;
            _t = 0f;
            _aim = targetNow;
            _aimVel = Vector3.Zero;
            _lastTarget = targetNow;
        }

        /// <summary>Advances the telegraph. Returns true on the step the shot fires; <paramref name="hit"/> says whether it
        /// landed. A step that isn't a positive number changes nothing.</summary>
        public bool Step(float dt, Vector3 target, out bool hit)
        {
            hit = false;
            if (!Active || !(dt > 0f) || float.IsInfinity(dt)) return false;
            _t += dt;
            if (!Locked)
            {
                Vector3 targetVel = (target - _lastTarget) / dt;
                float w = followRate;
                int n = Math.Min(64, Math.Max(1, (int)MathF.Ceiling(w * dt / 0.1f)));   // sub-steps keep it stable at any frame rate
                float h = dt / n;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(_lastTarget, target, (i + 1f) / n);
                    Vector3 acc = w * w * (p - _aim) + 2f * w * (targetVel - _aimVel);
                    _aimVel += acc * h;
                    _aim += _aimVel * h;
                }
                if (_t >= telegraph - lockBefore) Locked = true;
            }
            else
            {
                _aim += _aimVel * dt;
            }
            _lastTarget = target;
            if (_t >= telegraph)
            {
                Active = false;
                hit = Vector3.Distance(target, _aim) <= hitRadius;
                return true;
            }
            return false;
        }
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q`
Expected: PASS, including M0's `AimTests` (`ChangingDirectionAfterTheLockDodgesTheSpotlight`, `TheSpotlightHitsAStillTarget`,
`TheSpotlightLocksBeforeItFires`) and `CombatSimTests.TheSpotlightFiresTelegraphedHitscan`, which still hold under the
new beam.

- [ ] **Step 6: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/BotBrain.cs Packages/com.vibejam.jamkit/Runtime/Skyline/Aim.cs Packages/com.vibejam.jamkit/Tests/Skyline/BotBrainTests.cs Packages/com.vibejam.jamkit/Tests/Skyline/SpotlightBeamTests.cs
git commit -m "Skyline M1: bot brains (idle, alert, telegraph-attack cycle, faces, barks), the acid sweep, and a spotlight beam that locks its sweep so straight running is caught

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Encounters, their rules, Normal difficulty, the arena's layout and teaching beats (pure C#)

The encounter rules of 2.6 as a checker; the arena (11.2) as data, so the Unity builder and the headless checks read
the same layouts; Normal's numbers (7.4); and the M1 teaching beats of 6.8 as one editable data file.

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/Encounters.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/EncounterTests.cs`

**Interfaces:**
- Consumes: Task 8's `EnemyDef`, `ThreatLayer`, `SkylineEnemies`; M0's `AimRules`, `AimModel`; Task 3's
  `AimAssist.DeltaAngle`.
- Produces: `struct Spawn { string enemy; Vector3 position; Spawn(string, Vector3) }`; `class EncounterDef { string id;
  Vector3 anchor; Spawn[] spawns; bool keepOut }`; `static class EncounterRules { const float MinBearingSpread = 90f;
  const int MaxLayers = 2; float Bearing(Vector3 from, Vector3 to); float BearingSpread(EncounterDef);
  List<ThreatLayer> OpenLayers(EncounterDef, Func<string, EnemyDef>); List<string> Check(EncounterDef, Func<string,
  EnemyDef>) }`; `class DifficultyDef { string id; float leadFactor; float scrapScale }`; `static class Difficulties {
  DifficultyDef Normal() }`; `static class GalleriaArena` (constants `HallMinX/MaxX/MinZ/MaxZ`, `MezzanineY`,
  `WarmupMinX/MaxX/MinZ/MaxZ`, `PlayerStart`, `PracticeBot`, `BoostLedgeTop`, `BoostLedgeSize`, `LedgeWallClearance`;
  `EncounterDef FoodCourt()`, `EncounterDef Skylight()`, `EncounterDef[] Encounters()`); `enum Speaker { Dot,
  Concierge, Nan }`; `enum BeatTrigger { WarmupStart, PracticeBotSeen, FirstSpotCreated, NearBoostLedge,
  BoostLedgeReached, FirstPlatingBreak, FirstSeen }`; `class TeachingBeat { string id, text, enemy; Speaker speaker;
  BeatTrigger trigger; bool draft }`; `static class TeachingBeats { const int MaxChars = 140; TeachingBeat[] M1() }`.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/EncounterTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class EncounterTests
    {
        static EncounterDef Make(params Spawn[] spawns) => new EncounterDef { id = "test", anchor = Vector3.Zero, spawns = spawns };

        static string Problems(EncounterDef e, Func<string, EnemyDef> lookup = null) =>
            string.Join(" | ", EncounterRules.Check(e, lookup ?? SkylineEnemies.Find));

        [Test]
        public void TheArenasEncountersPassEveryRule()
        {
            foreach (var e in GalleriaArena.Encounters()) Assert.That(EncounterRules.Check(e, SkylineEnemies.Find), Is.Empty, e.id);
        }

        [Test]
        public void MixingLeadingWithWhereYouAreIsCaught()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(-15f, 6f, 0f)), new Spawn(SkylineEnemies.SpotlightId, new Vector3(15f, 9f, 0f)));
            Assert.That(Problems(e), Does.Contain("mixes"));
            e.keepOut = true;
            Assert.That(Problems(e), Does.Not.Contain("mixes"), "a deliberate keep-out zone may mix (2.6)");
        }

        [Test]
        public void ThreeLayersAreTooMany()
        {
            Func<string, EnemyDef> lookup = id => id == "washer"
                ? new EnemyDef { id = "washer", aim = AimModel.Area, layer = ThreatLayer.Walls }
                : SkylineEnemies.Find(id);
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(-15f, 6f, 0f)), new Spawn(SkylineEnemies.FloorCareId, new Vector3(15f, 0f, 0f)),
                new Spawn("washer", new Vector3(0f, 4f, 15f)));
            Assert.That(Problems(e, lookup), Does.Contain("3 layers"));
        }

        [Test]
        public void SpawnsOnOneBearingAreCaught()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(0f, 6f, 15f)), new Spawn(SkylineEnemies.DroneId, new Vector3(1f, 6f, 15f)));
            Assert.That(Problems(e), Does.Contain("bearing"));
            Assert.That(EncounterRules.BearingSpread(e), Is.LessThan(10f));
        }

        [Test]
        public void ASpawnOnTheAnchorHasNoBearing()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(0.5f, 6f, 0f)), new Spawn(SkylineEnemies.DroneId, new Vector3(0f, 6f, 15f)));
            Assert.That(EncounterRules.BearingSpread(e), Is.EqualTo(0f));
        }

        [Test]
        public void BearingsWrapTheShortWay()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(-10f, 6f, -10f)), new Spawn(SkylineEnemies.DroneId, new Vector3(10f, 6f, -10f)));
            Assert.That(EncounterRules.BearingSpread(e), Is.EqualTo(90f).Within(1e-3f), "-135 and +135 are 90 apart, not 270");
        }

        [Test]
        public void UnknownEnemiesAndEmptyEncountersAreNamed()
        {
            Assert.That(Problems(Make(new Spawn("ghost", new Vector3(5f, 0f, 5f)))), Does.Contain("ghost"));
            Assert.That(Problems(Make()), Does.Contain("no spawns"));
        }

        [Test]
        public void EachArenaEncounterLeavesALayerOpen()
        {
            foreach (var e in GalleriaArena.Encounters())
                Assert.That(EncounterRules.OpenLayers(e, SkylineEnemies.Find), Is.Not.Empty, e.id);
        }

        [Test]
        public void EveryM1BotAppearsInTheArenaAndTheDroneNeverMeetsTheSpotlight()
        {
            var seen = new HashSet<string>();
            foreach (var e in GalleriaArena.Encounters())
            {
                var ids = new HashSet<string>();
                foreach (var s in e.spawns) ids.Add(s.enemy);
                Assert.That(ids.Contains(SkylineEnemies.DroneId) && ids.Contains(SkylineEnemies.SpotlightId), Is.False, e.id);
                seen.UnionWith(ids);
            }
            foreach (var d in SkylineEnemies.All()) Assert.That(seen.Contains(d.id), Is.True, d.id);
        }

        [Test]
        public void EverySpawnStandsInsideTheHall()
        {
            foreach (var e in GalleriaArena.Encounters())
                foreach (var s in e.spawns)
                {
                    Assert.That(s.position.X, Is.InRange(GalleriaArena.HallMinX, GalleriaArena.HallMaxX), e.id + " " + s.enemy);
                    Assert.That(s.position.Z, Is.InRange(GalleriaArena.HallMinZ, GalleriaArena.HallMaxZ), e.id + " " + s.enemy);
                }
        }

        [Test]
        public void TheBoostLedgeStandsClearOfTheWarmupWalls()
        {
            var c = GalleriaArena.BoostLedgeTop;
            float half = GalleriaArena.BoostLedgeSize * 0.5f;
            float clearance = Math.Min(Math.Min(c.X - half - GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX - (c.X + half)),
                                       Math.Min(c.Z - half - GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ - (c.Z + half)));
            Assert.That(clearance, Is.GreaterThanOrEqualTo(GalleriaArena.LedgeWallClearance), "no wall-run reaches it, so only a boost does (6.8)");
        }

        [Test]
        public void NormalLeadsButNotPerfectly()
        {
            var n = Difficulties.Normal();
            Assert.That(n.id, Is.EqualTo("normal"));
            Assert.That(n.leadFactor, Is.InRange(0.3f, 0.95f));
            Assert.That(n.scrapScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheTeachingBeatsCoverM1sRowsOfTheTeachingMap()
        {
            var beats = TeachingBeats.M1();
            bool Has(Speaker s, BeatTrigger t, string enemy = null) =>
                Array.Exists(beats, b => b.speaker == s && b.trigger == t && (enemy == null || b.enemy == enemy));
            Assert.That(Has(Speaker.Dot, BeatTrigger.PracticeBotSeen), Is.True, "Writ's weak spots: Dot, the practice bot");
            Assert.That(Has(Speaker.Dot, BeatTrigger.NearBoostLedge), Is.True, "Breacher's boost: Dot, the ledge");
            Assert.That(Has(Speaker.Dot, BeatTrigger.FirstPlatingBreak), Is.True, "Plating from wrecks: Dot");
            foreach (var d in SkylineEnemies.All())
                Assert.That(Has(Speaker.Concierge, BeatTrigger.FirstSeen, d.id), Is.True, "CONCIERGE's safety notice: " + d.id);
        }

        [Test]
        public void TheSpecsOwnLinesAreVerbatimAndEveryLineFits()
        {
            var beats = TeachingBeats.M1();
            var ids = new HashSet<string>();
            foreach (var b in beats)
            {
                Assert.That(ids.Add(b.id), Is.True, "unique id " + b.id);
                Assert.That(b.text.Length, Is.InRange(1, TeachingBeats.MaxChars), b.id);
            }
            Assert.That(Array.Find(beats, b => b.id == "dot-plating").text, Is.EqualTo("Strip it! Scrap's armour!"));
            Assert.That(Array.Find(beats, b => b.id == "dot-plating").draft, Is.False);
            Assert.That(Array.Find(beats, b => b.id == "concierge-floor-care").text,
                Is.EqualTo("Please note: the floor-care unit operates at ankle height. Guests are advised to jump."));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EncounterTests"`
Expected: build FAILS (`EncounterDef` not found).

- [ ] **Step 3: Write the implementation**

`Packages/com.vibejam.jamkit/Runtime/Skyline/Encounters.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    public struct Spawn
    {
        /// <summary>An <see cref="EnemyDef.id"/>.</summary>
        public string enemy;
        public Vector3 position;

        public Spawn(string enemy, Vector3 position)
        {
            this.enemy = enemy;
            this.position = position;
        }
    }

    /// <summary>One fight: where the player stands when it starts (bearings are measured from here) and who spawns where.</summary>
    public sealed class EncounterDef
    {
        public string id = "";
        public Vector3 anchor;
        public Spawn[] spawns = new Spawn[0];
        /// <summary>A deliberate keep-out zone (2.6): the aim models may mix here, on purpose.</summary>
        public bool keepOut;
    }

    /// <summary>The encounter rules of docs/SKYLINE-DESIGN.md 2.6. (The clean line is checked with the generator in M2.)</summary>
    public static class EncounterRules
    {
        public const float MinBearingSpread = 90f;
        public const int MaxLayers = 2;

        /// <summary>Degrees, 0 along +Z, 90 along +X.</summary>
        public static float Bearing(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z) * (180f / MathF.PI);

        /// <summary>The widest angle (0..180) between two spawns' bearings from the anchor. Spawns within 1 m of the anchor
        /// (on the flat) have no bearing.</summary>
        public static float BearingSpread(EncounterDef e)
        {
            var bearings = new List<float>();
            if (e?.spawns == null) return 0f;
            foreach (var s in e.spawns)
            {
                float dx = s.position.X - e.anchor.X, dz = s.position.Z - e.anchor.Z;
                if (dx * dx + dz * dz >= 1f) bearings.Add(Bearing(e.anchor, s.position));
            }
            float best = 0f;
            for (int i = 0; i < bearings.Count; i++)
                for (int j = i + 1; j < bearings.Count; j++)
                    best = MathF.Max(best, MathF.Abs(AimAssist.DeltaAngle(bearings[i], bearings[j])));
            return best;
        }

        /// <summary>The layers no spawn threatens (2.6: "reading the open layer is the skill").</summary>
        public static List<ThreatLayer> OpenLayers(EncounterDef e, Func<string, EnemyDef> lookup)
        {
            var open = new List<ThreatLayer> { ThreatLayer.Floor, ThreatLayer.Walls, ThreatLayer.Air };
            if (e?.spawns == null || lookup == null) return open;
            foreach (var s in e.spawns)
            {
                var d = lookup(s.enemy);
                if (d != null) open.Remove(d.layer);
            }
            return open;
        }

        /// <summary>Every rule the encounter breaks, in words; empty when it's fair: no mixed aim models (unless a keep-out
        /// zone), at most two layers, spawns on bearings at least 90 degrees apart, every enemy known.</summary>
        public static List<string> Check(EncounterDef e, Func<string, EnemyDef> lookup)
        {
            var problems = new List<string>();
            if (e == null)
            {
                problems.Add("no encounter");
                return problems;
            }
            string name = string.IsNullOrEmpty(e.id) ? "encounter" : e.id;
            if (e.spawns == null || e.spawns.Length == 0)
            {
                problems.Add(name + ": no spawns");
                return problems;
            }
            var aims = new List<AimModel>();
            var layers = new HashSet<ThreatLayer>();
            foreach (var s in e.spawns)
            {
                var d = lookup?.Invoke(s.enemy);
                if (d == null)
                {
                    problems.Add($"{name}: unknown enemy '{s.enemy}'");
                    continue;
                }
                aims.Add(d.aim);
                layers.Add(d.layer);
            }
            if (!e.keepOut && !AimRules.CanShareEncounter(aims.ToArray()))
                problems.Add(name + ": mixes shots that lead you with shots at where you are now (2.6)");
            if (layers.Count > MaxLayers)
                problems.Add($"{name}: threatens {layers.Count} layers; at most {MaxLayers}, so one is left open (2.6)");
            float spread = BearingSpread(e);
            if (spread < MinBearingSpread)
                problems.Add($"{name}: spawns span only {spread:0} degrees of bearing; they need at least {MinBearingSpread:0} (2.6)");
            return problems;
        }
    }

    /// <summary>A difficulty preset's numbers (7.4). M1 builds Normal; Easy and Hard come with M4.</summary>
    public sealed class DifficultyDef
    {
        public string id = "normal";
        /// <summary>How much of your velocity a leading shot accounts for (0..1; rises with difficulty).</summary>
        public float leadFactor = 0.75f;
        /// <summary>Scales the plating wrecks refund.</summary>
        public float scrapScale = 1f;
    }

    public static class Difficulties
    {
        /// <summary>Normal: tuned so the straight-line bot takes about twice the jinking bot's hits (Task 11).</summary>
        public static DifficultyDef Normal() => new DifficultyDef { id = "normal", leadFactor = 0.75f, scrapScale = 1f };
    }

    /// <summary>
    /// M1's arena (11.2) as data, read by the Unity builder and the headless checks alike. Two connected spaces: the
    /// warm-up hall (south: the practice bot and the boost ledge, 6.8) and the Galleria's main hall (a two-storey atrium,
    /// mezzanine at 5 m) with two encounters. Metres; +Z is north, the way the player faces at the start.
    /// </summary>
    public static class GalleriaArena
    {
        public const float HallMinX = -25f, HallMaxX = 25f, HallMinZ = -20f, HallMaxZ = 20f, MezzanineY = 5f;
        public const float WarmupMinX = -15f, WarmupMaxX = 15f, WarmupMinZ = -48f, WarmupMaxZ = -20f;
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -45f);
        /// <summary>The practice bot that visibly grows spots (Dot's Writ lesson); it never attacks.</summary>
        public static readonly Vector3 PracticeBot = new Vector3(-9f, 1.4f, -38f);
        /// <summary>The top of a kiosk in the warm-up hall: reachable only with a Breacher boost at the double jump's apex
        /// (jump + double jump + mantle falls short; checked in the sim in Task 12). A scrap cache waits on top.</summary>
        public static readonly Vector3 BoostLedgeTop = new Vector3(0f, 4.6f, -34f);
        public const float BoostLedgeSize = 3f;
        /// <summary>The kiosk keeps at least this far (m) from every wall, so no wall-run reaches its top.</summary>
        public const float LedgeWallClearance = 12f;

        /// <summary>Two drones (air, leading) and two floor-care units (floor): walls open.</summary>
        public static EncounterDef FoodCourt() => new EncounterDef
        {
            id = "food-court",
            anchor = Vector3.Zero,
            spawns = new[]
            {
                new Spawn(SkylineEnemies.DroneId, new Vector3(-15f, 6f, 8f)),
                new Spawn(SkylineEnemies.DroneId, new Vector3(15f, 6f, 8f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(-8f, 0f, -12f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(10f, 0f, 14f)),
            },
        };

        /// <summary>A spotlight on the north mezzanine (air, where you are now) and two floor-care units (floor): walls open.</summary>
        public static EncounterDef Skylight() => new EncounterDef
        {
            id = "skylight",
            anchor = Vector3.Zero,
            spawns = new[]
            {
                new Spawn(SkylineEnemies.SpotlightId, new Vector3(0f, 9f, 18f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(-16f, 0f, -6f)),
                new Spawn(SkylineEnemies.FloorCareId, new Vector3(16f, 0f, -6f)),
            },
        };

        public static EncounterDef[] Encounters() => new[] { FoodCourt(), Skylight() };
    }

    public enum Speaker { Dot, Concierge, Nan }

    public enum BeatTrigger { WarmupStart, PracticeBotSeen, FirstSpotCreated, NearBoostLedge, BoostLedgeReached, FirstPlatingBreak, FirstSeen }

    /// <summary>One teaching line (6.8): who says it, when, and the words. Drafts are for the creator to rewrite.</summary>
    public sealed class TeachingBeat
    {
        public string id = "", text = "";
        /// <summary>FirstSeen: the bot whose first appearance triggers it.</summary>
        public string enemy;
        public Speaker speaker;
        public BeatTrigger trigger;
        /// <summary>A draft (false only for the lines the spec quotes).</summary>
        public bool draft = true;
    }

    /// <summary>M1's teaching beats (6.8): Dot teaches the guns and plating, CONCIERGE's safety notices introduce each bot,
    /// and Nan opens the warm-up. One file, so the creator can rewrite every line in one place.</summary>
    public static class TeachingBeats
    {
        /// <summary>The longest line that fits the subtitle box at 1080p without clipping (10: no clipped text).</summary>
        public const int MaxChars = 140;

        public static TeachingBeat[] M1() => new[]
        {
            new TeachingBeat { id = "nan-warmup", speaker = Speaker.Nan, trigger = BeatTrigger.WarmupStart,
                text = "Unit's live. Warm-up hall first; the Galleria's through the far doors. Try not to dent anything we have to return." },
            new TeachingBeat { id = "dot-writ-spots", speaker = Speaker.Dot, trigger = BeatTrigger.PracticeBotSeen,
                text = "Practice bot, dead ahead! Tag its lens with the Writ and it leaves a weak spot. Hit the spot: hurts more than the lens!" },
            new TeachingBeat { id = "dot-writ-spot-made", speaker = Speaker.Dot, trigger = BeatTrigger.FirstSpotCreated,
                text = "That glowy bit? That's the spot. Shoot it!" },
            new TeachingBeat { id = "dot-breacher-boost", speaker = Speaker.Dot, trigger = BeatTrigger.NearBoostLedge,
                text = "Breacher kicks both ways. Jump, jump again, then shoot your own feet. There's scrap on top of that kiosk." },
            new TeachingBeat { id = "dot-boost-done", speaker = Speaker.Dot, trigger = BeatTrigger.BoostLedgeReached,
                text = "Ha! One boost a jump, mind. Land or touch a wall to get it back." },
            new TeachingBeat { id = "dot-plating", speaker = Speaker.Dot, trigger = BeatTrigger.FirstPlatingBreak, draft = false,
                text = "Strip it! Scrap's armour!" },
            new TeachingBeat { id = "concierge-floor-care", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.FloorCareId, draft = false,
                text = "Please note: the floor-care unit operates at ankle height. Guests are advised to jump." },
            new TeachingBeat { id = "concierge-drone", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.DroneId,
                text = "Please note: security drones anticipate where guests are going. Guests are advised to change their minds." },
            new TeachingBeat { id = "concierge-spotlight", speaker = Speaker.Concierge, trigger = BeatTrigger.FirstSeen,
                enemy = SkylineEnemies.SpotlightId,
                text = "Please note: the spotlight confirms its target before firing. Unconfirmed guests turn sharply, or stand behind something." },
        };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EncounterTests"`
Expected: PASS (14 tests).

- [ ] **Step 5: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/Encounters.cs Packages/com.vibejam.jamkit/Tests/Skyline/EncounterTests.cs
git commit -m "Skyline M1: encounter rules (aim mixing, two layers, two bearings), Normal, the arena's layout and M1's teaching beats as data

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 11: The dodge harness on the arena's encounters, and Normal's tuning (pure C#)

M0 measured one leading drone (ratio 6.0; "M1 tunes towards ~2 with real encounters (2+ bearings)"). This task runs
the dodge report on the arena's own encounters (Task 10) and tunes Normal so that on each, "the straight-line bot takes
about twice the hits of the jinking bot, and neither goes unhit" (2.6, 11.2). The numbers it tunes live in data
(`Difficulties.Normal()`, `SkylineEnemies`).

Two findings from a dry run of this plan (2026-10-13), built into it:
- **With perfectly accurate leading shots the ratio is a cliff, not a dial.** A straight-line runner is either always
  inside the hit radius (lead 0.88 and up: ratios of 8-30) or never (0.82 and down: ratios under 1). So the drone's shots
  get a small random aim error, as data (`EnemyDef.aimSpreadDeg`, 3 degrees to start), which makes the ratio change
  smoothly; with it, the plan's starting numbers measured **food court 1.89** (straight 10 hits, jinking 5.4) and
  **skylight 2.22** (straight 20, jinking 9.0). Because aimed shots are now random, the straight-line bot is averaged
  over the same seeds as the jinking bot.
- The spotlight needs a slightly wider beam than M0's default (0.9 m) and a 2.4 s cycle to land in the band with the
  sweep lock of Task 9.

The measurement covers **aimed** shots ("leads you", "fires at where you are now"). The floor-care units' acid sweeps
are area attacks dodged by jumping, which neither bot does; they are left out of the ratio (stated in the report and in
the spec's section 14 at Task 24).

**Files:**
- Modify (whole file): `Packages/com.vibejam.jamkit/Runtime/Skyline/CombatSim.cs` (shooters gain aim error and the beam's
  tuning; a seeded random per fight; both bots averaged over the seeds)
- Modify: `Packages/com.vibejam.jamkit/Runtime/Skyline/Enemies.cs` (`EnemyDef.aimSpreadDeg`; the drone's 3 degrees; the
  spotlight's beam and cycle)
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/EncounterDodge.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/EncounterDodgeTests.cs`

**Interfaces:**
- Consumes: M0's `IPlayerBot`, `StraightLineBot`, `JinkingBot(ulong seed)`; Tasks 8-10.
- Produces: `Shooter.aimSpreadDeg` (default 0), `Shooter.beamTelegraph, beamLockBefore, beamFollowRate, beamHitRadius`
  (defaults 0.8, 0.25, 10, 0.6); `CombatSim(MoveSim player, IPlayerBot bot, IEnumerable<Shooter> shooters, ulong seed
  = 1)`; `DodgeResult.straightHitsMean` (and `Ratio` = straight mean / jinking mean); `DodgeReport.Run(...)` unchanged
  in signature; `EnemyDef.aimSpreadDeg`; `static class EncounterDodge { const float Stagger = 0.3f; List<Shooter>
  Shooters(EncounterDef, Func<string, EnemyDef>, DifficultyDef); DodgeResult Run(EncounterDef, Func<IMoveWorld>,
  Func<string, EnemyDef>, DifficultyDef, float seconds = 60f, ulong seed = 7, int seeds = 5); DodgeResult
  Run(EncounterDef, Func<IMoveWorld>, DifficultyDef) }`. The fight puts the player at the origin, which is every arena
  encounter's anchor. The Unity drone (Task 15) applies the same `aimSpreadDeg` to its bolts.

- [ ] **Step 1: Write the failing tests**

`Packages/com.vibejam.jamkit/Tests/Skyline/EncounterDodgeTests.cs`:
```csharp
using System;
using System.Text;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The dodging measurement on Normal with the arena's real encounters (docs/SKYLINE-DESIGN.md 2.6, 11.2).</summary>
    public class EncounterDodgeTests
    {
        static BoxWorld Hall()
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.HallMinX, GalleriaArena.HallMaxX, GalleriaArena.HallMinZ, GalleriaArena.HallMaxZ, 0f);
            return w;
        }

        static EncounterDef ById(string id) => Array.Find(GalleriaArena.Encounters(), e => e.id == id);

        [Test]
        public void OnlyAimedShotsAreMeasured()
        {
            var food = EncounterDodge.Shooters(ById("food-court"), SkylineEnemies.Find, Difficulties.Normal());
            Assert.That(food.Count, Is.EqualTo(2), "the two drones; the floor-care units' sweeps are dodged by jumping");
            Assert.That(food[0].leadFactor, Is.EqualTo(Difficulties.Normal().leadFactor));
            Assert.That(food[0].aimSpreadDeg, Is.EqualTo(SkylineEnemies.MallCopDrone().aimSpreadDeg));
            Assert.That(food[1].firstShotAt, Is.GreaterThan(food[0].firstShotAt), "staggered, not in lockstep");
            var sky = EncounterDodge.Shooters(ById("skylight"), SkylineEnemies.Find, Difficulties.Normal());
            Assert.That(sky.Count, Is.EqualTo(1));
            Assert.That(sky[0].projectileSpeed, Is.EqualTo(0f));
            Assert.That(sky[0].leadFactor, Is.EqualTo(0f));
            Assert.That(sky[0].beamTelegraph, Is.EqualTo(SkylineEnemies.Spotlight().telegraphSeconds));
            Assert.That(sky[0].beamFollowRate, Is.EqualTo(SkylineEnemies.Spotlight().beamFollowRate));
        }

        [Test]
        public void AimErrorIsSeededSoTheReportRepeats()
        {
            var a = EncounterDodge.Run(ById("food-court"), Hall, SkylineEnemies.Find, Difficulties.Normal(), 20f, 3, 2);
            var b = EncounterDodge.Run(ById("food-court"), Hall, SkylineEnemies.Find, Difficulties.Normal(), 20f, 3, 2);
            Assert.That(a.straightHitsMean, Is.EqualTo(b.straightHitsMean));
            Assert.That(a.jinkHitsMean, Is.EqualTo(b.jinkHitsMean));
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        public void OnNormalTheStraightLineBotTakesAboutTwiceTheJinkingBotsHits(string id)
        {
            var r = EncounterDodge.Run(ById(id), Hall, Difficulties.Normal());
            TestContext.WriteLine($"Dodge on Normal, {id}: straight-line {r.straightHitsMean:0.0} hits, jinking {r.jinkHitsMean:0.0} " +
                $"(means of {r.seeds} seeds), {r.shots} shots in {r.seconds:0} s, ratio {r.Ratio:0.00} (target 1.6-2.5, neither 0)");
            Assert.That(r.straightHitsMean, Is.GreaterThan(0f), "the straight-line bot is hit");
            Assert.That(r.jinkHitsMean, Is.GreaterThan(0f), "and so is the jinking bot: nobody goes unhit");
            Assert.That(r.Ratio, Is.InRange(1.6f, 2.5f), "about twice (2.6)");
        }

        [Test, Explicit("the Normal tuning sweep: prints a table of ratios; asserts nothing")]
        public void SweepTheNormalTuning()
        {
            var sb = new StringBuilder();
            sb.AppendLine("food-court (two drones): aimSpreadDeg leadFactor fireInterval -> straight jink ratio");
            foreach (float spread in new[] { 2f, 3f, 4f })
                foreach (float lead in new[] { 0.65f, 0.7f, 0.75f, 0.8f })
                    foreach (float fi in new[] { 1.0f, 1.2f, 1.4f })
                    {
                        Func<string, EnemyDef> lookup = id =>
                        {
                            var d = SkylineEnemies.Find(id);
                            if (d != null && id == SkylineEnemies.DroneId) { d.aimSpreadDeg = spread; d.fireInterval = fi; }
                            return d;
                        };
                        var r = EncounterDodge.Run(ById("food-court"), Hall, lookup, new DifficultyDef { leadFactor = lead });
                        sb.AppendLine($"{spread:0} {lead:0.00} {fi:0.0} -> {r.straightHitsMean:0.0} {r.jinkHitsMean:0.0} {r.Ratio:0.00}");
                    }
            sb.AppendLine("skylight (spotlight): beamFollowRate beamHitRadius fireInterval -> straight jink ratio");
            foreach (float w in new[] { 8f, 10f, 12f })
                foreach (float hr in new[] { 0.7f, 0.8f, 0.9f, 1.0f })
                    foreach (float fi in new[] { 2.4f, 2.8f, 3.2f })
                    {
                        Func<string, EnemyDef> lookup = id =>
                        {
                            var d = SkylineEnemies.Find(id);
                            if (d != null && id == SkylineEnemies.SpotlightId) { d.beamFollowRate = w; d.beamHitRadius = hr; d.fireInterval = fi; }
                            return d;
                        };
                        var r = EncounterDodge.Run(ById("skylight"), Hall, lookup, Difficulties.Normal());
                        sb.AppendLine($"{w:0} {hr:0.0} {fi:0.0} -> {r.straightHitsMean:0.0} {r.jinkHitsMean:0.0} {r.Ratio:0.00}");
                    }
            TestContext.WriteLine(sb.ToString());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EncounterDodgeTests"`
Expected: build FAILS (`EncounterDodge` not found).

- [ ] **Step 3: Give shots their aim error, beams their tuning, and average both bots**

`Packages/com.vibejam.jamkit/Runtime/Skyline/CombatSim.cs` (whole file):
```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Drives the player's body in the headless sim, like a player's hands.</summary>
    public interface IPlayerBot
    {
        MoveInput Next(MoveSim sim, float time, float h);
    }

    /// <summary>A stationary enemy that fires at the player (docs/SKYLINE-DESIGN.md 2.5).</summary>
    public sealed class Shooter
    {
        public Vector3 position;
        public AimModel aim = AimModel.LeadsYou;
        /// <summary>m/s; 0 means a telegraphed hitscan (the spotlight).</summary>
        public float projectileSpeed = 30f;
        public float fireInterval = 1.2f;
        /// <summary>0..1, how much of the player's velocity a leading shot accounts for (rises with difficulty).</summary>
        public float leadFactor = 0.9f;
        public float projectileRadius = 0.15f;
        /// <summary>Seconds into the fight before the first shot.</summary>
        public float firstShotAt = 0.5f;
        /// <summary>Degrees of random aim error on a projectile (uniform over a cone); 0 is a perfect shot.</summary>
        public float aimSpreadDeg;
        /// <summary>A telegraphed hitscan's beam (projectileSpeed 0): its telegraph, lock, follow rate and hit radius.</summary>
        public float beamTelegraph = 0.8f, beamLockBefore = 0.25f, beamFollowRate = 10f, beamHitRadius = 0.6f;

        internal float nextFire = -1f;
        internal TelegraphedShot telegraphShot;
    }

    /// <summary>
    /// A headless fight: the real movement engine moving the player, stationary shooters, straight projectiles and the
    /// spotlight's telegraphed hitscan. It measures how often a way of moving gets hit (the dodging measurement, 2.6).
    /// The seed drives the shooters' aim error, so a fight repeats exactly.
    /// </summary>
    public sealed class CombatSim
    {
        struct Projectile
        {
            public Vector3 pos, vel;
            public float radius, age;
        }

        const float MaxProjectileAge = 4f;

        readonly MoveSim _player;
        readonly IPlayerBot _bot;
        readonly List<Shooter> _shooters;
        readonly List<Projectile> _shots = new List<Projectile>();
        readonly Rng _rng;

        public int Hits { get; private set; }
        public int ShotsFired { get; private set; }
        public float Time { get; private set; }

        public CombatSim(MoveSim player, IPlayerBot bot, IEnumerable<Shooter> shooters, ulong seed = 1)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _shooters = new List<Shooter>(shooters);
            _rng = new Rng(seed);
            foreach (var s in _shooters)
            {
                s.nextFire = s.firstShotAt;
                s.telegraphShot = null;   // a shooter reused across fights starts with no beam in flight
            }
        }

        public void Run(float seconds)
        {
            float h = 1f / _player.def.tickRate;
            int steps = (int)MathF.Ceiling(seconds / h);
            for (int i = 0; i < steps; i++) Step(h);
        }

        Vector3 Chest => _player.Position + new Vector3(0f, _player.Height * 0.5f, 0f);

        void Step(float h)
        {
            var input = _bot.Next(_player, Time, h);
            _player.Step(in input, h);
            Time += h;

            foreach (var s in _shooters)
            {
                if (s.telegraphShot != null && s.telegraphShot.Active)
                {
                    if (s.telegraphShot.Step(h, Chest, out bool hit) && hit) Hits++;
                    continue;
                }
                if (Time < s.nextFire) continue;
                s.nextFire = Time + s.fireInterval;
                ShotsFired++;
                if (s.projectileSpeed <= 0f)
                {
                    if (s.telegraphShot == null)
                        s.telegraphShot = new TelegraphedShot
                        {
                            telegraph = s.beamTelegraph, lockBefore = s.beamLockBefore, followRate = s.beamFollowRate, hitRadius = s.beamHitRadius,
                        };
                    s.telegraphShot.Start(Chest);
                    continue;
                }
                Vector3 aimAt = s.aim == AimModel.LeadsYou
                    ? Aim.LeadPoint(s.position, Chest, _player.Velocity, s.projectileSpeed, s.leadFactor)
                    : Chest;
                Vector3 dir = aimAt - s.position;
                float len = dir.Length();
                if (len < 1e-4f) continue;
                _shots.Add(new Projectile { pos = s.position, vel = Spread(dir / len, s.aimSpreadDeg) * s.projectileSpeed, radius = s.projectileRadius });
            }

            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var p = _shots[i];
                p.pos += p.vel * h;
                p.age += h;
                if (HitsPlayer(p.pos, p.radius)) { Hits++; _shots.RemoveAt(i); continue; }
                if (p.age > MaxProjectileAge) { _shots.RemoveAt(i); continue; }
                _shots[i] = p;
            }
        }

        /// <summary>The direction tilted by a random error uniform over a cone of <paramref name="deg"/> degrees.</summary>
        Vector3 Spread(Vector3 dir, float deg)
        {
            if (!(deg > 0f)) return dir;
            Vector3 side = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 up = Vector3.Cross(side, dir);
            float angle = _rng.Range(0f, 2f * MathF.PI);
            float r = MathF.Tan(deg * MathF.PI / 180f) * MathF.Sqrt(_rng.NextFloat());
            return Vector3.Normalize(dir + (side * MathF.Cos(angle) + up * MathF.Sin(angle)) * r);
        }

        /// <summary>The player's capsule: a vertical segment from feet+radius to feet+height-radius, thickened by the radius.</summary>
        bool HitsPlayer(Vector3 point, float radius)
        {
            float r = _player.def.radius;
            Vector3 a = _player.Position + new Vector3(0f, r, 0f);
            Vector3 b = _player.Position + new Vector3(0f, MathF.Max(r, _player.Height - r), 0f);
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / MathF.Max(1e-6f, Vector3.Dot(ab, ab));
            t = MathF.Max(0f, MathF.Min(1f, t));
            return Vector3.Distance(point, a + ab * t) <= r + radius;
        }
    }

    public struct DodgeResult
    {
        /// <summary>straightHits and jinkHits are the means below, rounded.</summary>
        public int straightHits, jinkHits, shots;
        /// <summary>Each bot's hits averaged over the seeds run.</summary>
        public float straightHitsMean, jinkHitsMean;
        public int seeds;
        public float seconds;
        public float Ratio => jinkHitsMean > 0f ? straightHitsMean / jinkHitsMean : float.PositiveInfinity;
    }

    /// <summary>The dodging measurement (2.6): the same fight run by a straight-line bot and a jinking bot. Target on
    /// Normal: the straight-line bot takes about twice the hits, and neither goes unhit.</summary>
    public static class DodgeReport
    {
        /// <summary>Both bots run with seeds <paramref name="seed"/>..<paramref name="seed"/>+<paramref name="seeds"/>-1 (the
        /// seed drives the jinking bot's reversals and the shooters' aim error) and their hits are averaged. With no aim
        /// error the straight-line bot's runs are identical.</summary>
        public static DodgeResult Run(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, float seconds, ulong seed, int seeds = 5)
        {
            if (seeds < 1) seeds = 1;
            int straightTotal = 0, jinkTotal = 0, shots = 0;
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                var straight = Fight(makeWorld(), new StraightLineBot(), makeShooters(), seconds, s);
                straightTotal += straight.Hits;
                if (i == 0) shots = straight.ShotsFired;
                jinkTotal += Fight(makeWorld(), new JinkingBot(s), makeShooters(), seconds, s).Hits;
            }
            float straightMean = (float)straightTotal / seeds, jinkMean = (float)jinkTotal / seeds;
            return new DodgeResult
            {
                straightHits = (int)MathF.Round(straightMean), straightHitsMean = straightMean,
                jinkHits = (int)MathF.Round(jinkMean), jinkHitsMean = jinkMean,
                seeds = seeds, shots = shots, seconds = seconds,
            };
        }

        static CombatSim Fight(IMoveWorld world, IPlayerBot bot, List<Shooter> shooters, float seconds, ulong seed)
        {
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            var c = new CombatSim(sim, bot, shooters, seed);
            c.Run(seconds);
            return c;
        }
    }
}
```
M0's `CombatSimTests` keep passing: with no aim error the straight-line bot's five runs are identical, so its mean is
M0's single run.

- [ ] **Step 4: The data: aim error for the drone, the spotlight's beam and cycle**

In `Packages/com.vibejam.jamkit/Runtime/Skyline/Enemies.cs`:
- In `EnemyDef`, after `projectileRadius`, add:
```csharp
        /// <summary>Degrees of random aim error on a projectile shot (a drone isn't a perfect shot; it also makes the dodge
        /// measurement a dial instead of a cliff). 0 for none.</summary>
        public float aimSpreadDeg;
```
- In `MallCopDrone()`, add `aimSpreadDeg = 3f,` after `projectileRadius = 0.15f,`.
- In `Spotlight()`, change `fireInterval = 2.2f` to `fireInterval = 2.4f`, `beamFollowRate = 10f` to `beamFollowRate = 12f`
  and `beamHitRadius = 0.6f` to `beamHitRadius = 0.9f`.

- [ ] **Step 5: Write the harness**

`Packages/com.vibejam.jamkit/Runtime/Skyline/EncounterDodge.cs`:
```csharp
using System;
using System.Collections.Generic;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>The dodging measurement (docs/SKYLINE-DESIGN.md 2.6) on an encounter's real layout: its aimed bots become
    /// stationary shooters at their spawns (with the difficulty's lead), and the straight-line and jinking bots run the
    /// fight from the anchor. Area and melee attacks are left out: they're dodged by jumping and spacing, not by changing
    /// direction.</summary>
    public static class EncounterDodge
    {
        /// <summary>Seconds between the aimed shooters' first shots, so bots of one kind don't fire in lockstep.</summary>
        public const float Stagger = 0.3f;

        public static List<Shooter> Shooters(EncounterDef e, Func<string, EnemyDef> lookup, DifficultyDef difficulty)
        {
            var list = new List<Shooter>();
            if (e?.spawns == null || lookup == null || difficulty == null) return list;
            foreach (var s in e.spawns)
            {
                var d = lookup(s.enemy);
                if (d == null || (d.aim != AimModel.LeadsYou && d.aim != AimModel.FiresAtWhereYouAre)) continue;
                bool beam = d.projectileSpeed <= 0f;
                list.Add(new Shooter
                {
                    position = s.position,
                    aim = d.aim,
                    projectileSpeed = d.projectileSpeed,
                    fireInterval = d.fireInterval,
                    leadFactor = d.aim == AimModel.LeadsYou ? difficulty.leadFactor : 0f,
                    projectileRadius = d.projectileRadius,
                    aimSpreadDeg = d.aimSpreadDeg,
                    firstShotAt = d.alertSeconds + (beam ? 0f : d.telegraphSeconds) + Stagger * list.Count,
                    beamTelegraph = d.telegraphSeconds,
                    beamLockBefore = d.beamLockBefore,
                    beamFollowRate = d.beamFollowRate,
                    beamHitRadius = d.beamHitRadius,
                });
            }
            return list;
        }

        public static DodgeResult Run(EncounterDef e, Func<IMoveWorld> makeWorld, Func<string, EnemyDef> lookup, DifficultyDef difficulty,
            float seconds = 60f, ulong seed = 7, int seeds = 5) =>
            DodgeReport.Run(makeWorld, () => Shooters(e, lookup, difficulty), seconds, seed, seeds);

        public static DodgeResult Run(EncounterDef e, Func<IMoveWorld> makeWorld, DifficultyDef difficulty) =>
            Run(e, makeWorld, SkylineEnemies.Find, difficulty);
    }
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo --filter "FullyQualifiedName~EncounterDodgeTests" --logger "console;verbosity=detailed"`
Expected: PASS (4 tests), printing about "food-court: straight-line 10.0 hits, jinking 5.4 ..., ratio 1.89" and
"skylight: straight-line 20.0 hits, jinking 9.0 ..., ratio 2.22" (the dry run's numbers; small differences are fine as
long as both stay in 1.6-2.5).

- [ ] **Step 7: Only if a band test fails (the data changed since the dry run): sweep, pick, set**

Run: `dotnet test core-tests/Skyline/Skyline.Tests.csproj --nologo --filter "FullyQualifiedName~SweepTheNormalTuning" --logger "console;verbosity=detailed"`
(about 20 s). For each failing encounter pick the row closest to 2.0 with both counts above zero **whose neighbours in
the table stay in 1.6-2.5** (a row on a cliff will break with the next data change), preferring: Normal's `leadFactor`
within 0.6-0.85 (Easy and Hard need room either side); the drone's `fireInterval` at 1.0 s or slower; the spotlight's
`beamHitRadius` at 1.0 m or less. Set the values in `Difficulties.Normal()` / `SkylineEnemies`. If no row lands in the
band, **stop** and report BLOCKED with the table: don't change the bots' rules or the band (the call is the creator's).

- [ ] **Step 8: Run everything**

Run: `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: every Skyline test (M0's included), the Movement suite and the Unity-side compile check pass.

- [ ] **Step 9: Commit**

```bash
git add -- Packages/com.vibejam.jamkit/Runtime/Skyline/CombatSim.cs Packages/com.vibejam.jamkit/Runtime/Skyline/Enemies.cs Packages/com.vibejam.jamkit/Runtime/Skyline/EncounterDodge.cs Packages/com.vibejam.jamkit/Tests/Skyline/EncounterDodgeTests.cs
git commit -m "Skyline M1: the dodge measurement on the arena's encounters (food court <ratio>, skylight <ratio> on Normal); drones get a small aim error so the ratio is a dial, not a cliff

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
(Write the two measured ratios in place of `<ratio>`.)

---

### Task 12: The engine's opt-in outside-push hook (engine; shared files)

The Breacher's self-knockback needs the movement engine to accept a push (spec 8). This is the only edit to shared
files in M1, and it is **opt-in, default off**: Visiting Hours never turns it on, so its nights are unchanged.
`MovementController` also gets an opt-in input hook, because Skyline's scenes remove Visiting Hours' `GameRoot`
(`SkylineSceneGuard`, M0) and `MovementController` otherwise reads input only through `GameRoot` (with no `GameRoot`
it doesn't move at all, not even under its autopilot).

**Shared files touched (the complete list):**
- `Packages/com.vibejam.jamkit/Runtime/Core/Movement/MoveSim.cs` (the push: a flag, a queue, applied at the start of a step)
- `Packages/com.vibejam.jamkit/Runtime/Unity/Movement/MovementController.cs` (`allowOutsidePush` + `AddPush`, and
  `InputOverride`, both default off)
- `Packages/com.vibejam.jamkit/Tests/Core/Movement/OutsidePushTests.cs` (new, in the Movement suite)

Not touched: `MoveDef.cs`, `MoveTypes.cs`, the shared tuning asset (the push is enabled per sim, not per def, so
`SharedTuningTests` is unaffected), every movement mode and every switch on the mode.

**Gate (spec 8):** the full Movement suite, plus the Dream Escape and Deep Block route times, unchanged. Before
committing, the main session tells the Visiting Hours session which shared files changed and that both hooks are off by
default.

**Files:**
- Create: `tools/skyline/engine-gate.ps1` (write `.ps1` files with the Write tool; a bash heredoc into a `.ps1` trips
  Windows Defender on this machine)
- Modify: `Packages/com.vibejam.jamkit/Runtime/Core/Movement/MoveSim.cs`
- Modify: `Packages/com.vibejam.jamkit/Runtime/Unity/Movement/MovementController.cs`
- Create: `Packages/com.vibejam.jamkit/Tests/Core/Movement/OutsidePushTests.cs`
- Modify: `core-tests/Skyline/Skyline.Tests.csproj`, `core-tests/SkylineUnity/SkylineUnity.Check.csproj` (add the
  movement test kit)
- Create: `Packages/com.vibejam.jamkit/Tests/Skyline/BreacherBoostSimTests.cs`

**Interfaces:**
- Consumes: `MoveSim` internals (`_mode`, `_vel`, `_grounded`, `_doubleJump`, `_jumpedSinceGround`, `_sinceJump`,
  `_landGrace`, `_wallCoyote`, `_crouched`, `_events`, `LeaveWall()`, `SetCrouched(bool)`, `MaxSpeed`,
  `MoveMath.CapFlat`); `MoveTestKit` (`H`, `Forward()`, `With(jump:)`, `Run`, `Flat()`, `Standing`); Task 7's
  `BreacherKnockback`, `BoostCharge`, `BreacherBoostDef`; Task 10's `GalleriaArena`.
- Produces: `MoveSim.OutsidePushEnabled` (bool, default false), `MoveSim.AddPush(System.Numerics.Vector3 deltaV)`;
  `MovementController.allowOutsidePush` (bool, default false), `MovementController.AddPush(UnityEngine.Vector3
  deltaV)`, `MovementController.InputOverride` (`Func<MoveSim, float, MoveInput>`, default null); the tool
  `tools/skyline/engine-gate.ps1 [-Record]`.

- [ ] **Step 1: Write the gate script**

`tools/skyline/engine-gate.ps1`:
```powershell
# The engine gate (docs/SKYLINE-DESIGN.md 8): "the full Movement test suite, plus the Dream Escape and Deep Block
# route-time pins, unchanged". Record the route times BEFORE an engine change, check them after.
#   powershell -NoProfile -File tools/skyline/engine-gate.ps1 -Record   # before: writes out/skyline-gate/route-times.txt
#   powershell -NoProfile -File tools/skyline/engine-gate.ps1           # after: Movement green, route times identical
# It also compile-checks the Unity layer (Visiting Hours' core-tests/UnityLayerPlayer), since MovementController.cs is
# shared. A failure there that isn't in MovementController.cs is another session's: report it, don't fix it.
param([switch]$Record)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$gateDir = Join-Path $root 'out\skyline-gate'
$baseline = Join-Path $gateDir 'route-times.txt'

function Get-RouteTimes {
    $lines = @()
    $runs = @(
        @{ Name = 'DreamEscape'; Filter = 'FullyQualifiedName~TheAutopilot' },
        @{ Name = 'DeepBlock'; Filter = 'FullyQualifiedName~TheAutopilotRunsTheRouteToTheHillGate' }
    )
    foreach ($r in $runs) {
        $proj = Join-Path $root "core-tests\$($r.Name)\$($r.Name).Tests.csproj"
        $ErrorActionPreference = 'Continue'
        $out = & dotnet test $proj --nologo --filter $r.Filter --logger 'console;verbosity=detailed' 2>&1 | ForEach-Object { "$_" }
        $code = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($code -ne 0) { throw "$($r.Name): the autopilot tests failed (exit $code)" }
        $lines += $out | Where-Object { $_ -match 'finished (True|False) in [0-9.]+ s' -or $_ -match "the doc's rough time" } |
            ForEach-Object { "$($r.Name): $($_.Trim())" }
    }
    if ($lines.Count -eq 0) { throw 'no route times in the test output: the tests changed what they print; update this gate' }
    return @($lines | Sort-Object)
}

& dotnet test (Join-Path $root 'core-tests\Movement\Movement.Tests.csproj') --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'ENGINE GATE FAILED: the Movement suite'; exit 1 }
& dotnet build (Join-Path $root 'core-tests\UnityLayerPlayer\UnityLayerPlayer.Check.csproj') --nologo -v q -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { Write-Host 'ENGINE GATE FAILED: the Unity layer does not compile (is the error in MovementController.cs?)'; exit 1 }
$times = Get-RouteTimes
New-Item -ItemType Directory -Force $gateDir | Out-Null
if ($Record) {
    Set-Content -Path $baseline -Value $times -Encoding utf8
    Write-Host "Recorded $($times.Count) route times in $baseline"
    exit 0
}
if (-not (Test-Path $baseline)) { Write-Host 'ENGINE GATE FAILED: no baseline; run with -Record before the engine change'; exit 1 }
$before = @(Get-Content $baseline)
$diff = Compare-Object $before $times
if ($diff) {
    Write-Host 'ENGINE GATE FAILED: route times changed:'
    $diff | ForEach-Object { Write-Host "  $($_.SideIndicator) $($_.InputObject)" }
    exit 1
}
Write-Host "ENGINE GATE OK: Movement green, the Unity layer compiles, $($times.Count) route times unchanged"
exit 0
```

- [ ] **Step 2: Record the baseline on the unchanged engine**

Run: `powershell -NoProfile -File tools/skyline/engine-gate.ps1 -Record`
Expected: "Recorded N route times" with N at least 5 (the dream escape's runs, the deep block's four routes). Read
`out/skyline-gate/route-times.txt` once to see the lines are route times. If the Unity layer fails to compile before
any change of ours, it's another session's state: stop and report it.

- [ ] **Step 3: Write the failing engine tests**

`Packages/com.vibejam.jamkit/Tests/Core/Movement/OutsidePushTests.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Movement
{
    /// <summary>The opt-in outside push (Skyline Repo's Breacher; docs/SKYLINE-DESIGN.md 8): off by default; applied at the
    /// start of a step; an upward part takes off and ends a wall-run; ignored in a mantle; capped at MaxSpeed; never
    /// refreshes the double jump.</summary>
    public class OutsidePushTests
    {
        static MoveSim Pushable(BoxWorld w, Vector3 at, Vector3 velocity = default)
        {
            var sim = Standing(w, at, null, 0f, velocity);
            sim.OutsidePushEnabled = true;
            return sim;
        }

        [Test]
        public void OffByDefaultAPushChangesNothing()
        {
            var a = Standing(Flat(), Vector3.Zero);
            var b = Standing(Flat(), Vector3.Zero);
            Assert.That(b.OutsidePushEnabled, Is.False);
            for (int i = 0; i < 240; i++)
            {
                if (i == 10) b.AddPush(new Vector3(3f, 9f, 2f));
                var input = Forward().With(jump: i == 30);
                a.Step(input, H);
                b.Step(input, H);
                Assert.That(b.Position, Is.EqualTo(a.Position), "step " + i);
                Assert.That(b.Velocity, Is.EqualTo(a.Velocity), "step " + i);
            }
        }

        [Test]
        public void AnUpwardPushOnTheGroundTakesOff()
        {
            var sim = Pushable(Flat(), Vector3.Zero);
            Assert.That(sim.Grounded, Is.True);
            sim.AddPush(new Vector3(0f, 7f, 0f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Grounded, Is.False);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.Events.leftGround, Is.True);
            float top = 0f;
            Run(sim, new MoveInput(), 2f, s => { top = Math.Max(top, s.Position.Y); return s.Grounded; });
            Assert.That(top, Is.EqualTo(49f / (2f * sim.def.gravity)).Within(0.08f), "7 m/s up rises v^2 / 2g");
        }

        [Test]
        public void AFlatPushOnTheGroundStaysOnTheGround()
        {
            var sim = Pushable(Flat(), Vector3.Zero);
            sim.AddPush(new Vector3(0f, 0f, -6f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Grounded, Is.True);
            Assert.That(sim.Velocity.Z, Is.LessThan(-4f));
        }

        [Test]
        public void AnUpwardPushEndsAWallrunWithNoWallJumpAfterIt()
        {
            var w = new BoxWorld();
            w.Add(new Vector3(2.5f, -10f, 30f), new Vector3(1f, 40f, 60f));
            var sim = Pushable(w, new Vector3(1.3f, 3f, 1f), new Vector3(0f, 0f, 10f));
            Run(sim, Forward(), 0.2f, s => s.Mode == MoveMode.Wallrun);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Wallrun));
            sim.AddPush(new Vector3(0f, 5f, 0f));
            sim.Step(Forward(), H);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Air));
            Assert.That(sim.Events.wallrunEnded, Is.True);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.wallJumped, Is.False, "the push used up the wall");
        }

        [Test]
        public void APushDuringAMantleIsIgnored()
        {
            var w = Flat();
            w.Floor(-10f, 10f, 5f, 30f, 1.5f, 2.5f);
            var sim = Pushable(w, Vector3.Zero, new Vector3(0f, 0f, 8f));
            Run(sim, Forward(), 2f, s => s.Mode == MoveMode.Mantle);
            Assert.That(sim.Mode, Is.EqualTo(MoveMode.Mantle));
            sim.AddPush(new Vector3(0f, 12f, 0f));
            float top = 0f;
            Run(sim, Forward(), 1.5f, s => { top = Math.Max(top, s.Position.Y); return false; });
            Assert.That(sim.Position.Y, Is.EqualTo(1.5f).Within(0.06f), "on top of the ledge");
            Assert.That(top, Is.LessThan(1.5f + 0.3f), "the push never launched it, then or later");
        }

        [Test]
        public void APushNeverTakesTheFlatSpeedPastTheCap()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f), new Vector3(0f, 0f, 20f));
            sim.AddPush(new Vector3(0f, 0f, 15f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.HorizontalSpeed, Is.LessThanOrEqualTo(sim.MaxSpeed + 1e-3f));
            Assert.That(sim.MaxSpeed, Is.EqualTo(22f));
        }

        [Test]
        public void APushNeverRefreshesTheDoubleJump()
        {
            var sim = Pushable(Flat(), Vector3.Zero);
            sim.Step(Forward().With(jump: true), H);
            Run(sim, Forward(), 0.3f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.Events.doubleJumped, Is.True);
            Assert.That(sim.DoubleJumpAvailable, Is.False, "spent");
            sim.AddPush(new Vector3(0f, 6f, 0f));
            sim.Step(Forward(), H);
            Assert.That(sim.DoubleJumpAvailable, Is.False);
        }

        [Test]
        public void PushesQueuedBeforeAStepAddUpAndApplyOnce()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f));
            sim.AddPush(new Vector3(1f, 0f, 0f));
            sim.AddPush(new Vector3(2f, 0f, 0f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(3f).Within(1e-4f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(3f).Within(1e-4f), "not applied twice");
        }

        [Test]
        public void ABrokenPushIsIgnoredAndTeleportDropsAQueuedOne()
        {
            var sim = Pushable(new BoxWorld(), new Vector3(0f, 50f, 0f));
            sim.AddPush(new Vector3(float.NaN, 0f, 0f));
            sim.AddPush(new Vector3(0f, float.PositiveInfinity, 0f));
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(0f));
            Assert.That(float.IsNaN(sim.Velocity.Y), Is.False);
            sim.AddPush(new Vector3(5f, 0f, 0f));
            sim.Teleport(new Vector3(0f, 50f, 0f), 0f, Vector3.Zero);
            sim.Step(new MoveInput(), H);
            Assert.That(sim.Velocity.X, Is.EqualTo(0f), "a respawn drops a queued push");
        }
    }
}
```

- [ ] **Step 4: Run them to verify they fail**

Run: `dotnet test core-tests/Movement/Movement.Tests.csproj --nologo -v q --filter "FullyQualifiedName~OutsidePushTests"`
Expected: build FAILS (`'MoveSim' does not contain a definition for 'OutsidePushEnabled'`).

- [ ] **Step 5: Add the push to MoveSim**

In `Packages/com.vibejam.jamkit/Runtime/Core/Movement/MoveSim.cs`:

(a) After the field `MoveEvents _events;`, add:
```csharp

        // The outside push (opt-in; Skyline Repo's Breacher, docs/SKYLINE-DESIGN.md 8)
        Vector3 _push;
        bool _pushPending, _pushEnabled;
```

(b) After the `MaxSpeed` property, add:
```csharp

        /// <summary>Opt-in (Skyline Repo): lets <see cref="AddPush"/> change the velocity. Off by default, so every game that
        /// doesn't turn it on moves exactly as before.</summary>
        public bool OutsidePushEnabled
        {
            get => _pushEnabled;
            set => _pushEnabled = value;
        }

        /// <summary>Queues a velocity change (m/s) from outside the body (the Breacher's self-knockback), applied at the start
        /// of the next step under these rules: an upward part forces leaving the ground and ends a wall-run (with no
        /// wall-coyote jump after it); it is ignored during a mantle; the horizontal speed stays within <see cref="MaxSpeed"/>
        /// (CapFlat); it never refreshes the double jump. Pushes queued before one step add up. Ignored when
        /// <see cref="OutsidePushEnabled"/> is off or the push isn't finite.</summary>
        public void AddPush(Vector3 deltaV)
        {
            if (!_pushEnabled) return;
            if (!(IsFinite(deltaV.X) && IsFinite(deltaV.Y) && IsFinite(deltaV.Z))) return;
            _push += deltaV;
            _pushPending = true;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
```

(c) In `Teleport`, after the lines `_lastKick = -999f;` and `_events = default;`, add:
```csharp
            _push = Vector3.Zero;
            _pushPending = false;
```

(d) In `Step`, change
```csharp
            if (!(h > 0f)) return;
            _events = default;
            _time += h;
```
to
```csharp
            if (!(h > 0f)) return;
            _events = default;
            _time += h;
            if (_pushPending) ApplyPush();
```

(e) Before the line `// ---------- Ground ----------`, add:
```csharp
        // ---------- The outside push ----------

        void ApplyPush()
        {
            Vector3 p = _push;
            _push = Vector3.Zero;
            _pushPending = false;
            if (_mode == MoveMode.Mantle) return;
            bool doubleJump = _doubleJump;
            if (p.Y > 0f)
            {
                if (_mode == MoveMode.Wallrun)
                {
                    LeaveWall();
                    _wallCoyote = false;
                }
                else if (_mode == MoveMode.Grounded || _mode == MoveMode.Sprint || _mode == MoveMode.Slide)
                {
                    // On the ground StepGround zeroes vertical speed, so an upward push must take off here.
                    if (_mode == MoveMode.Slide) _events.slideEnded = true;
                    _mode = MoveMode.Air;
                    _grounded = false;
                    _jumpedSinceGround = true;   // no coyote jump on top of a push
                    _sinceJump = 0f;
                    _landGrace = 0f;
                    _events.leftGround = true;
                    if (_crouched) SetCrouched(false);
                }
            }
            _vel = CapFlat(_vel + p, MaxSpeed);
            _doubleJump = doubleJump;
        }

```

- [ ] **Step 6: Run the engine tests and the gate**

Run: `dotnet test core-tests/Movement/Movement.Tests.csproj --nologo -v q --filter "FullyQualifiedName~OutsidePushTests"`
Expected: PASS (9 tests). Then: `powershell -NoProfile -File tools/skyline/engine-gate.ps1`
Expected: `ENGINE GATE OK: Movement green, the Unity layer compiles, N route times unchanged`.

- [ ] **Step 7: Add the two opt-in hooks to MovementController**

In `Packages/com.vibejam.jamkit/Runtime/Unity/Movement/MovementController.cs`:

(a) After `public bool InputEnabled = true;`, add:
```csharp
        /// <summary>Opt-in (Skyline Repo, docs/SKYLINE-DESIGN.md 8): when set, it drives the body instead of the shell's input
        /// and the autopilot, with or without GameRoot (Skyline's scenes remove it). It gets the sim and the frame's dt and
        /// returns the step's input; that input's yaw becomes the view's. Null (the default) changes nothing.</summary>
        public Func<MoveSim, float, MoveInput> InputOverride;
        /// <summary>Opt-in (Skyline Repo): lets <see cref="AddPush"/> push the body (the Breacher's self-knockback). Off by default.</summary>
        public bool allowOutsidePush;
```

(b) After the `SetLook` method, add:
```csharp

        /// <summary>Queues a velocity change (m/s, world space) for the start of the next simulation step, under MoveSim's
        /// outside-push rules (<see cref="MoveSim.AddPush"/>). Does nothing unless <see cref="allowOutsidePush"/> is on.</summary>
        public void AddPush(Vector3 deltaV)
        {
            if (!allowOutsidePush) return;
            if (_sim == null) Setup();
            _sim.OutsidePushEnabled = true;
            _sim.AddPush(ToN(deltaV));
        }
```

(c) In `ReadInput`, right after `var input = new MoveInput { yaw = _yaw };`, add:
```csharp
            if (InputOverride != null)
            {
                var o = InputOverride(_sim, dt);
                _yaw = o.yaw;
                return o;
            }
```

- [ ] **Step 8: Write the Skyline-side checks of the boost in the real engine**

Add `<Compile Include="$(PkgRoot)Tests\Core\Movement\MoveTestKit.cs" />` after the `BoxWorld.cs` line in both
`core-tests/Skyline/Skyline.Tests.csproj` and `core-tests/SkylineUnity/SkylineUnity.Check.csproj`.

`Packages/com.vibejam.jamkit/Tests/Skyline/BreacherBoostSimTests.cs`:
```csharp
using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;
using static JamKit.Tests.Movement.MoveTestKit;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Breacher's boost in the real movement engine (Task 7's rules through Task 12's hook), and the arena's
    /// boost ledge (6.8: "an optional ledge reachable only with a boost").</summary>
    public class BreacherBoostSimTests
    {
        static readonly Vector3 Down = new Vector3(0f, -1f, 0f);

        static MoveSim Body(BoxWorld w, Vector3 at)
        {
            var sim = Standing(w, at);
            sim.OutsidePushEnabled = true;
            return sim;
        }

        static bool Fire(MoveSim sim, Vector3 aim, BoostCharge charge)
        {
            sim.AddPush(BreacherKnockback.SelfPush(aim, sim.Velocity, sim.Grounded, sim.def.gravity, charge, new BreacherBoostDef(), out bool boosted));
            return boosted;
        }

        /// <summary>Steps until the body starts falling (or 5 s); returns the highest feet height.</summary>
        static float RiseToApex(MoveSim sim, MoveInput input)
        {
            float top = sim.Position.Y;
            for (int i = 0; i < 600; i++)
            {
                sim.Step(input, H);
                top = Math.Max(top, sim.Position.Y);
                if (!sim.Grounded && sim.Velocity.Y <= 0f) break;
            }
            return top;
        }

        [Test]
        public void AStraightDownShotFromTheFloorRisesTheBoostHeight()
        {
            var sim = Body(Flat(), Vector3.Zero);
            Assert.That(Fire(sim, Down, new BoostCharge()), Is.True);
            Assert.That(RiseToApex(sim, new MoveInput()), Is.EqualTo(new BreacherBoostDef().boostHeight).Within(0.08f));
        }

        [Test]
        public void OneBoostAnAirtimeAndLandingGivesItBack()
        {
            var sim = Body(Flat(), Vector3.Zero);
            var charge = new BoostCharge();
            sim.Step(Forward().With(jump: true), H);
            float jumpTop = RiseToApex(sim, Forward());
            Assert.That(Fire(sim, Down, charge), Is.True);
            float boostTop = RiseToApex(sim, Forward());
            Assert.That(boostTop - jumpTop, Is.EqualTo(new BreacherBoostDef().boostHeight).Within(0.1f));
            Assert.That(Fire(sim, Down, charge), Is.False, "the second shot this airtime gives nothing");
            Run(sim, Forward(), 3f, s => s.Grounded);
            charge.Observe(sim.Grounded, sim.Mode == MoveMode.Wallrun);
            Assert.That(charge.Ready, Is.True);
        }

        [Test]
        public void TheBoostNeverGivesBackTheDoubleJump()
        {
            var sim = Body(Flat(), Vector3.Zero);
            sim.Step(Forward().With(jump: true), H);
            Run(sim, Forward(), 0.2f);
            sim.Step(Forward().With(jump: true), H);
            Assert.That(sim.DoubleJumpAvailable, Is.False);
            Fire(sim, Down, new BoostCharge());
            sim.Step(Forward(), H);
            Assert.That(sim.DoubleJumpAvailable, Is.False);
        }

        /// <summary>The warm-up hall's kiosk, its top at the boost ledge, sides not runnable; the body 0.8 m short of its south
        /// face, facing it.</summary>
        static bool ReachesTheLedge(bool boost)
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX, GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ, 0f);
            var top = GalleriaArena.BoostLedgeTop;
            float s = GalleriaArena.BoostLedgeSize;
            w.Add(new Vector3(top.X, top.Y * 0.5f, top.Z), new Vector3(s, top.Y, s), runnable: false);
            var sim = Body(w, new Vector3(top.X, 0f, top.Z - s * 0.5f - 0.8f));
            sim.Step(Forward().With(jump: true), H);
            RiseToApex(sim, Forward());
            sim.Step(Forward().With(jump: true), H);   // the double jump at the jump's apex
            Assert.That(sim.Events.doubleJumped, Is.True, "double jumped");
            RiseToApex(sim, Forward());
            if (boost) Assert.That(Fire(sim, Down, new BoostCharge()), Is.True);
            bool mantled = false;
            Run(sim, Forward(), 3f, x => { mantled |= x.Mode == MoveMode.Mantle; return mantled && x.Grounded; });
            return mantled && sim.Position.Y > top.Y - 0.1f;
        }

        [Test]
        public void TheWarmupLedgeIsOutOfReachWithoutABoost()
        {
            Assert.That(ReachesTheLedge(false), Is.False);
        }

        [Test]
        public void TheWarmupLedgeIsReachableWithABoostAtTheDoubleJumpsApex()
        {
            Assert.That(ReachesTheLedge(true), Is.True);
        }
    }
}
```
If `TheWarmupLedgeIsReachable...` fails while the no-boost case holds, the ledge is too high for this engine's mantle
reach: lower `GalleriaArena.BoostLedgeTop.Y` in steps of 0.1 m until both pass, and keep at least 0.3 m of margin on
each side (check by also trying the top 0.3 m higher and lower by hand in a scratch test, then delete it).

- [ ] **Step 9: Run everything**

Run: `powershell -NoProfile -File tools/skyline/engine-gate.ps1` then `powershell -NoProfile -File tools/skyline/test.ps1`
Expected: `ENGINE GATE OK ...`; all Skyline tests (with `BreacherBoostSimTests`, 5), the Movement suite and the
Skyline Unity-side compile check pass.

- [ ] **Step 10: Commit (after the main session has told the Visiting Hours session)**

```bash
git add -- tools/skyline/engine-gate.ps1 Packages/com.vibejam.jamkit/Runtime/Core/Movement/MoveSim.cs Packages/com.vibejam.jamkit/Runtime/Unity/Movement/MovementController.cs Packages/com.vibejam.jamkit/Tests/Core/Movement/OutsidePushTests.cs core-tests/Skyline/Skyline.Tests.csproj core-tests/SkylineUnity/SkylineUnity.Check.csproj Packages/com.vibejam.jamkit/Tests/Skyline/BreacherBoostSimTests.cs
git commit -m "Movement engine: an opt-in outside-push hook (MoveSim) and opt-in push/input hooks on MovementController, all off by default; engine gate green (Movement suite, Dream Escape and Deep Block route times unchanged)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
## Phase 3: Unity integration (one batch, one pass)

Tasks 13-16 are written by agents that never run Unity; their safety net is `tools/skyline/test.ps1`, whose last step
compiles everything under `Assets/JamKit/Templates/SkylineRepo/` against the Unity DLLs. Each task ends with **what
the pass must show**: Task 17 (the main session) runs one `SkylinePass` for the whole batch and checks those lines.
Unity-side code is plain, readable MonoBehaviours that **play** what the pure systems decide; no tuning number lives in
a component. The interfaces between Tasks 14, 15 and 16 are fixed in Task 13's `Scripts/Common/` so they can be built
in parallel.

### Task 13: The pass for M1: three scenes, longer recordings, still cameras, the arena's frame time, the sync check (Unity + tools)

**Files:**
- Modify: `Assets/JamKit/Templates/SkylineRepo/Scripts/JamKit.SkylineRepo.asmdef`,
  `Assets/JamKit/Templates/SkylineRepo/Editor/JamKit.SkylineRepo.Editor.asmdef` (add `"JamKit.Unity"` and
  `"Unity.InputSystem"` to `references`)
- Modify: `core-tests/SkylineUnity/SkylineUnity.Check.csproj` (compile `Runtime\Unity\**` with the package DLLs, as
  `core-tests/UnityLayer/UnityLayer.props` does)
- Create: `Assets/JamKit/Templates/SkylineRepo/Scripts/Common/SkylineContracts.cs`
- Create: `Assets/JamKit/Templates/SkylineRepo/Scripts/Capture/SkylineRunArgs.cs`, `.../Scripts/Capture/SkylineStillCamera.cs`
- Modify: `Assets/JamKit/Templates/SkylineRepo/Editor/SkylineScenes.cs` (rewrite)
- Create: `Assets/JamKit/Templates/SkylineRepo/Editor/SkylineArenaBuilder.cs`, `.../Editor/SkylineRangeBuilder.cs`,
  `.../Editor/SkylineGalleryBuilder.cs` (minimal now; Tasks 14-16 fill them)
- Modify: `Assets/JamKit/Templates/SkylineRepo/Editor/SkylinePassBatch.cs`, `.../Editor/SkylineBuild.cs`,
  `.../Editor/Recording/SkylineCapture.cs`, `.../Scripts/SkylinePerfProbe.cs`
- Delete: `Assets/JamKit/Templates/SkylineRepo/Scripts/SkylineTestTone.cs` and its `.meta` (`git rm`; the range
  director's marker beep replaces it)
- Create: `tools/skyline/soundforge/synccheck.py`, `tools/skyline/soundforge/tests/test_synccheck.py`
- Modify: `tools/skyline/pass.ps1` (stale files, stage D's scene check, stage E), `docs/SKYLINE-PIPELINE.md`

**Interfaces:**
- Produces (used by Tasks 14-16):
  - `JamKit.SkylineRepo.SkylineScenes { const string Arena, FiringRange, Gallery; static readonly string[] All (arena
    first); static int RecordSeconds(string scene); static string[] BuildAll(); static Scene NewScene() }`.
  - Builders: `SkylineArenaBuilder.Build(string path)`, `SkylineRangeBuilder.Build(string path)`,
    `SkylineGalleryBuilder.Build(string path)` (each starts with `SkylineScenes.NewScene()` and saves to `path`).
  - `SkylineRunArgs { static string Value(string name); static bool Has(string name); static string OutDir; static bool
    Demo }` (`Demo` is true under `-skylineOut`, `-skyline-perf` or `-skyline-demo`: the directors run their scripted
    demos).
  - `SkylineStillCamera : MonoBehaviour { string shotName }` (a disabled Camera beside it; stage B renders each to
    `still-<scene>-<shotName>.png` at 1920x1080 after recording the scene).
  - `Scripts/Common/SkylineContracts.cs`: `interface ISkylineTarget { Vector3 Chest; Vector3 Velocity; Transform Root;
    void TakeAttack(float damage, string attackerId); }` (what bots shoot at: the player, or the gallery's dummy);
    `struct SkylineHitInfo { GunDef gun; float damage; HitZone zone; int plate; Vector3 point, normal, direction; bool
    fromWrit; float knockback; float time; }`; `struct SkylineHitResult { BotDamage damage; WritHit writ; bool hit,
    killed; }`; `interface ISkylineHittable { SkylineHitResult TakeHit(in SkylineHitInfo hit); }`; `sealed class
    SkylineHitZone : MonoBehaviour { MonoBehaviour owner (an ISkylineHittable); HitZone zone; int plate = -1 }`;
    `static class SkylineTargets { List<ISkylineAimable> Aimables }` and `interface ISkylineAimable { Vector3 Center;
    float Radius; bool Alive; }` (aim assist's candidates).
  - Stage E reads `firing-range-schedule.json` from the pass's out folder: `{"marker":"440 Hz beep","events":[{"t":
    1.234,"gun":"writ","clip":"ReloadEmpty","id":"mag_out","sound":"writ/mag_out"}, ...]}` with `t` in seconds after
    the marker beep (Task 14's range director writes it).
  - `perf.json` gains `"scene"`.

- [ ] **Step 1: Write the sync check's failing tests**

`tools/skyline/soundforge/tests/test_synccheck.py`:
```python
import synccheck as sc


def schedule(*events):
    return {"events": [{"t": t, "gun": "writ", "clip": "Fire", "id": f"e{i}", "sound": s} for i, (t, s) in enumerate(events)]}


def test_match_takes_the_nearest_unused_onset_within_tolerance():
    assert sc.match([1.0, 2.0], [0.99, 1.02, 2.5], 0.035) == [-0.01, None]
    errs = sc.match([1.0, 1.01], [1.0], 0.035)
    assert errs[0] == 0.0 and errs[1] is None, "an onset is used once"


def test_events_are_aligned_on_the_marker():
    onsets = [5.0, 6.0, 7.51]                  # the marker at 5.0, events 1.0 and 2.5 s after it
    r = sc.check(schedule((1.0, "writ/shot"), (2.5, "writ/mag_out")), onsets)
    assert r["ok"] and r["matched"] == 2 and r["worst_ms"] == 10.0


def test_a_late_event_is_missed_and_named():
    r = sc.check(schedule((1.0, "writ/shot"), (2.0, "writ/seat")), [5.0, 6.0, 7.2], min_share=0.9)
    assert not r["ok"] and r["matched"] == 1
    assert "writ/Fire/e1@2.000" in r["missed"]


def test_undetectable_sounds_are_left_out_of_the_share():
    r = sc.check(schedule((1.0, "writ/shot"), (2.0, "writ/inspect_turn")), [5.0, 6.0], detectable={"writ/shot"})
    assert r["ok"] and r["checked"] == 1 and r["undetectable"] == ["writ/inspect_turn"]


def test_silence_or_an_empty_schedule_fails_with_a_note():
    assert not sc.check(schedule((1.0, "writ/shot")), [])["ok"]
    assert "silent" in sc.check(schedule((1.0, "writ/shot")), [])["note"]
    assert not sc.check({"events": []}, [1.0, 2.0])["ok"]
```

Run: `cd tools/skyline/soundforge && uv run --group dev pytest -q tests/test_synccheck.py`
Expected: FAIL (`ModuleNotFoundError: No module named 'synccheck'`).

- [ ] **Step 2: Write the sync check**

`tools/skyline/soundforge/synccheck.py`:
```python
"""The sync check (docs/SKYLINE-DESIGN.md 2.3: "the sound events stay locked to the animation"; 9.4).

The firing range director (Unity) plays a marker beep, then both guns' handling sets, and writes
firing-range-schedule.json: each sound event's time in seconds after the marker, as the game cued it. This finds the
onsets in the recording with the ears, takes the first as the marker, and checks that every event whose sound is
detectable on its own lands on an onset within the tolerance.

    uv run --project ~/.claude/skills/ears python tools/skyline/soundforge/synccheck.py <rec.mp4> <schedule.json> \
        --sounds Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio [--tolerance-ms 35] [--min-share 0.9]

Prints one JSON object (ok, events, checked, matched, share, median_ms, worst_ms, missed, undetectable, note). The
matching (match, check) is plain Python, tested in tests/test_synccheck.py; only main() needs the ears.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def match(event_times, onsets, tolerance):
    """For each event time (recording time, in order): the error (s) to the nearest onset not used yet, within the
    tolerance, or None."""
    used = set()
    errors = []
    for t in event_times:
        best, best_err = None, None
        for i, o in enumerate(onsets):
            if i in used:
                continue
            err = round(o - t, 6)
            if abs(err) <= tolerance and (best_err is None or abs(err) < abs(best_err)):
                best, best_err = i, err
        if best is not None:
            used.add(best)
        errors.append(best_err)
    return errors


def check(schedule, onsets, detectable=None, tolerance_ms=35.0, min_share=0.9):
    events = list(schedule.get("events", []))
    skipped = [e for e in events if detectable is not None and e.get("sound") not in detectable]
    checked = [e for e in events if detectable is None or e.get("sound") in detectable]
    base = {"events": len(events), "checked": len(checked), "undetectable": sorted({str(e.get("sound")) for e in skipped})}
    if not onsets:
        return {**base, "ok": False, "matched": 0, "note": "no onsets in the recording (silent?)"}
    if not checked:
        return {**base, "ok": False, "matched": 0, "note": "the schedule has no detectable events"}
    marker = onsets[0]
    errors = match([marker + float(e["t"]) for e in checked], onsets[1:], tolerance_ms / 1000.0)
    hit = sorted(abs(x) for x in errors if x is not None)
    missed = [f'{e.get("gun")}/{e.get("clip")}/{e.get("id")}@{float(e["t"]):.3f}' for e, x in zip(checked, errors) if x is None]
    share = len(hit) / len(checked)
    out = {
        **base,
        "ok": share >= min_share,
        "matched": len(hit),
        "share": round(share, 3),
        "median_ms": round(hit[len(hit) // 2] * 1000, 1) if hit else None,
        "worst_ms": round(hit[-1] * 1000, 1) if hit else None,
        "missed": missed[:20],
        "marker_s": round(marker, 3),
    }
    out["note"] = (f'{out["matched"]}/{out["checked"]} events within {tolerance_ms:g} ms (median {out["median_ms"]} ms, '
                   f'worst {out["worst_ms"]} ms; {len(base["undetectable"])} sounds undetectable alone)')
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("recording")
    ap.add_argument("schedule")
    ap.add_argument("--sounds", required=True, help="the folder holding <gun>/<name>.wav (Unity's Resources/SkylineAudio)")
    ap.add_argument("--ears", default=str(Path.home() / ".claude" / "skills" / "ears"))
    ap.add_argument("--tolerance-ms", type=float, default=35.0)
    ap.add_argument("--min-share", type=float, default=0.9)
    ap.add_argument("--range-db", type=float, default=50.0)
    a = ap.parse_args(argv)
    try:
        sys.path.insert(0, a.ears)
        import listen  # the ears skill; run this through its uv environment

        sched = json.loads(Path(a.schedule).read_text(encoding="utf-8-sig"))
        audio, sr = listen.load(a.recording)
        onsets = listen.onsets(audio, sr, range_db=a.range_db)
        detectable = set()
        for sound in {e.get("sound") for e in sched.get("events", []) if e.get("sound")}:
            path = Path(a.sounds) / (sound + ".wav")
            if path.is_file():
                s_audio, s_sr = listen.load(str(path))
                if listen.onsets(s_audio, s_sr, range_db=a.range_db):
                    detectable.add(sound)
        result = check(sched, onsets, detectable, a.tolerance_ms, a.min_share)
    except Exception as e:  # the runner reads the JSON: never a bare traceback instead of it
        result = {"ok": False, "note": f"sync check failed: {type(e).__name__}: {e}"}
    print(json.dumps(result, indent=2))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
```

Run: `cd tools/skyline/soundforge && uv run --group dev pytest -q` -- Expected: PASS (the forge's 18 and these 5).

- [ ] **Step 3: The shared contracts and the run arguments**

`Assets/JamKit/Templates/SkylineRepo/Scripts/Common/SkylineContracts.cs` declares exactly the types listed under
Interfaces (namespace `JamKit.SkylineRepo`; `using JamKit.Skyline;` for `GunDef`, `HitZone`, `BotDamage`, `WritHit`).
`SkylineTargets.Aimables` is a plain static `List<ISkylineAimable>`; implementers add themselves in `OnEnable` and
remove themselves in `OnDisable`. `SkylineHitZone.owner` is serialized as `MonoBehaviour` and cast to
`ISkylineHittable` at hit time (a non-hittable owner is a no-op hit with `hit = false`).

`Assets/JamKit/Templates/SkylineRepo/Scripts/Capture/SkylineRunArgs.cs`:
```csharp
using System;

namespace JamKit.SkylineRepo
{
    /// <summary>The pass's command-line switches, read in the editor (stage B: -skylineOut) and the player (stage D:
    /// -skyline-perf). Under either, the scenes' directors run their scripted demos, so the recording and the frame-time
    /// probe see the same thing; -skyline-demo runs them by hand. A plain launch is the game.</summary>
    public static class SkylineRunArgs
    {
        public static string Value(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        public static bool Has(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        /// <summary>Where the pass wants files written (null outside a pass).</summary>
        public static string OutDir => Value("-skylineOut") ?? Value("-skyline-perf");

        public static bool Demo => Has("-skylineOut") || Has("-skyline-perf") || Has("-skyline-demo");
    }
}
```

`Assets/JamKit/Templates/SkylineRepo/Scripts/Capture/SkylineStillCamera.cs`:
```csharp
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>A fixed camera for the judges' stills (docs/SKYLINE-DESIGN.md 9.4): disabled, rendered by stage B to
    /// still-&lt;scene&gt;-&lt;shotName&gt;.png at 1920x1080 after the scene's recording.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SkylineStillCamera : MonoBehaviour
    {
        public string shotName = "shot";
    }
}
```

- [ ] **Step 4: Three scenes, the arena first**

`Assets/JamKit/Templates/SkylineRepo/Editor/SkylineScenes.cs` (whole file):
```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the pipeline's scenes from code each pass (generated, not hand-edited): the arena (M1's one arena,
    /// first in the build so the frame-time probe measures it), the firing range (both guns' handling sets, recorded with
    /// audio) and the enemy gallery (each bot idle, alert and attacking). Every scene carries the SkylineSceneGuard that
    /// keeps Visiting Hours' GameRoot out.</summary>
    public static class SkylineScenes
    {
        public const string Folder = "Assets/JamKit/Templates/SkylineRepo/Generated";
        public const string Arena = Folder + "/SkylineArena.unity";
        public const string FiringRange = Folder + "/SkylineFiringRange.unity";
        public const string Gallery = Folder + "/SkylineGallery.unity";
        public static readonly string[] All = { Arena, FiringRange, Gallery };

        /// <summary>Seconds stage B records each scene (its director's script fits inside).</summary>
        public static int RecordSeconds(string scene) => scene == Arena ? 60 : scene == FiringRange ? 45 : 36;

        public static string[] BuildAll()
        {
            Directory.CreateDirectory(Folder);
            SkylineArenaBuilder.Build(Arena);
            SkylineRangeBuilder.Build(FiringRange);
            SkylineGalleryBuilder.Build(Gallery);
            AssetDatabase.Refresh();
            return (string[])All.Clone();
        }

        /// <summary>A new empty scene with the guard that keeps Visiting Hours' GameRoot out (see SkylineSceneGuard).</summary>
        public static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("SkylineSceneGuard").AddComponent<JamKit.SkylineRepo.SkylineSceneGuard>();
            return scene;
        }
    }
}
```
Each builder for now: `NewScene()`, a directional light, a 40 x 40 m floor (a `Plane` scaled 4), a `MainCamera` with an
`AudioListener` at (0, 1.6, -8), then `EditorSceneManager.SaveScene(scene, path)` (M0's content, minus the test tone).

- [ ] **Step 5: The batch job, the build and the recording**

- `SkylinePassBatch`: replace `scenes.Length == 2` with `scenes.Length == SkylineScenes.All.Length`.
- `SkylineBuild`: `scenes = SkylineScenes.All` (the arena first, so the player opens on it).
- `SkylineCapture`: `static readonly string[] Scenes = SkylineScenes.All;`; the recording length per scene is
  `SkylineScenes.RecordSeconds(Scenes[index]) * 60` frames instead of `RecordFrames`; `Deadline` becomes 840 s (stage
  B's timeout is 900 s); the stale-file sweep in `RunFromCommandLine` also deletes `still-<name>-*.png`; and in
  `TickRecording`, after `controller.StopRecording()` succeeds and before leaving Play mode, call `TakeStills(dir,
  name)` (inside a try: a failure finishes the capture with `"stills for <name>: <message>"`):
```csharp
        /// <summary>Renders every SkylineStillCamera in the playing scene to still-&lt;scene&gt;-&lt;shot&gt;.png (1920x1080).</summary>
        static void TakeStills(string dir, string scene)
        {
            int n = 0;
            foreach (var shot in UnityEngine.Object.FindObjectsByType<JamKit.SkylineRepo.SkylineStillCamera>(FindObjectsSortMode.None))
            {
                var cam = shot.GetComponent<Camera>();
                var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                var previousTarget = cam.targetTexture;
                var previousActive = RenderTexture.active;
                try
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, "still-" + scene + "-" + shot.shotName + ".png"), tex.EncodeToPNG());
                    n++;
                }
                finally
                {
                    cam.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            Debug.Log("SKYLINE_CAPTURE stills " + scene + " " + n);
        }
```
- `SkylinePerfProbe`: add `,"scene":"` + `Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)` + `"` to
  perf.json.

- [ ] **Step 6: The runner**

In `tools/skyline/pass.ps1`:
- The stale list becomes: `'editor-batch.json', 'capture.json', 'build.json', 'perf.json', 'firing-range-schedule.json',
  'sync-check.json', 'build\SkylineRepo.exe'`, plus every `rec-*.mp4` and `still-*.png` in `$Out`
  (`Get-ChildItem -LiteralPath $Out -Filter 'rec-*.mp4'` and `'still-*.png'`, each removed with `-Force`).
- `Update-PerfStage`: after the resolution check add
  `if ($p.scene -and $p.scene -ne 'SkylineArena') { $problems += "measured '$($p.scene)', not SkylineArena" }` and
  append `$(if ($p.scene) { ", scene $($p.scene)" })` at the end of `$perfNote` (after the gpu part, so the self-test's
  notes keep their prefix).
- `Update-BatchStage`: after the tests note, `if ($j.Data.lint) { Add-StageNote $Stage "lint: $($j.Data.lint.count) problem(s)" }`
  (Task 16 adds the lint; it is reported, not part of stage A's ok: lint problems are the judges' blockers).
- Replace stage E with:
```powershell
    # E: the sync check without Unity (docs/SKYLINE-DESIGN.md 2.3): the firing range recording's sound onsets against the
    # schedule its director wrote, aligned on the marker beep; plus the ears' picture of the recording's audio.
    $rec = Join-Path $Out 'rec-SkylineFiringRange.mp4'
    $schedule = Join-Path $Out 'firing-range-schedule.json'
    $started = Get-Date
    $eok = $false; $enote = ''
    if ((Test-Path $rec) -and (Test-Path $schedule)) {
        try {
            $ears = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude\skills\ears'
            $sounds = Join-Path $RepoRoot 'Assets\JamKit\Templates\SkylineRepo\Resources\SkylineAudio'
            $err = Join-Path $Out 'sync-check.stderr.txt'
            # stdout (the JSON) only; uv's progress goes to stderr, which under Stop would surface as an error in PS 5.1
            $ErrorActionPreference = 'Continue'
            & uv run --project $ears python (Join-Path $ears 'listen.py') measure $rec --png (Join-Path $Out 'audio-firing-range.png') 2>$err | Out-Null
            $json = & uv run --project $ears python (Join-Path $RepoRoot 'tools\skyline\soundforge\synccheck.py') $rec $schedule --sounds $sounds 2>>$err
            $ErrorActionPreference = 'Stop'
            $text = ($json | Out-String)
            Set-Content -Path (Join-Path $Out 'sync-check.json') -Value $text -Encoding utf8
            $m = $text.Substring($text.IndexOf('{')) | ConvertFrom-Json
            $eok = [bool]$m.ok
            $enote = [string]$m.note
        } catch { $ErrorActionPreference = 'Stop'; $enote = "could not run the sync check: $($_.Exception.Message)" }
    } elseif (Test-Path $rec) { $enote = 'no firing-range-schedule.json (the range director did not write one)' }
    else { $enote = 'skipped: no firing range recording' }
    $stages += [pscustomobject]@{ name = 'E sync check'; ok = $eok; seconds = [int]((Get-Date) - $started).TotalSeconds; log = ''; note = $enote }
```
Edit `pass.ps1` with the Edit tool (never through a bash heredoc).

- [ ] **Step 7: The Unity-side compile check sees the Unity layer**

In `core-tests/SkylineUnity/SkylineUnity.Check.csproj`, inside the main `ItemGroup`, add:
```xml
    <Compile Include="$(PkgRoot)Runtime\Unity\**\*.cs" />
    <PkgDll Include="$(ScriptAssemblies)Unity.InputSystem.dll;$(ScriptAssemblies)UnityEngine.UI.dll;$(ScriptAssemblies)Ink.dll;$(ScriptAssemblies)Ink-Libraries.dll;$(ScriptAssemblies)Unity.RenderPipelines.Universal.Runtime.dll;$(ScriptAssemblies)Unity.RenderPipelines.Core.Runtime.dll;$(ScriptAssemblies)Unity.AI.Navigation.dll" />
    <Reference Include="@(PkgDll)" Private="false" />
```
If a file under `Runtime\Unity` needs something else, copy the matching line from `core-tests/UnityLayer/UnityLayer.props`
rather than excluding the file.

- [ ] **Step 8: Verify, update the how-to, commit**

Run: `powershell -NoProfile -File tools/skyline/test.ps1` and `powershell -NoProfile -File tools/skyline/pass.ps1 -SelfTest`
Expected: green; the self-test still passes (its perf and batch cases are unchanged by the additions).
In `docs/SKYLINE-PIPELINE.md`: the stage table's E row becomes "the sync check: the firing range recording's onsets
against the director's schedule (`sync-check.json`)"; outputs gain `still-<scene>-<shot>.png`, `sync-check.json`,
`firing-range-schedule.json`; D measures the arena.
```bash
git rm -- Assets/JamKit/Templates/SkylineRepo/Scripts/SkylineTestTone.cs Assets/JamKit/Templates/SkylineRepo/Scripts/SkylineTestTone.cs.meta
git add -- Assets/JamKit/Templates/SkylineRepo/Scripts/JamKit.SkylineRepo.asmdef Assets/JamKit/Templates/SkylineRepo/Editor/JamKit.SkylineRepo.Editor.asmdef Assets/JamKit/Templates/SkylineRepo/Scripts/Common Assets/JamKit/Templates/SkylineRepo/Scripts/Capture Assets/JamKit/Templates/SkylineRepo/Editor/SkylineScenes.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylineArenaBuilder.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylineRangeBuilder.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylineGalleryBuilder.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylinePassBatch.cs Assets/JamKit/Templates/SkylineRepo/Editor/SkylineBuild.cs Assets/JamKit/Templates/SkylineRepo/Editor/Recording/SkylineCapture.cs Assets/JamKit/Templates/SkylineRepo/Scripts/SkylinePerfProbe.cs core-tests/SkylineUnity/SkylineUnity.Check.csproj tools/skyline/soundforge/synccheck.py tools/skyline/soundforge/tests/test_synccheck.py tools/skyline/pass.ps1 docs/SKYLINE-PIPELINE.md
git commit -m "Skyline M1 pass: three scenes (arena first), per-scene recording lengths, still cameras, the arena's frame time, the sync check

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

**What the pass must show (checked in Task 17):** `editor-batch.json` lists 3 scenes; `capture.json` recorded
`SkylineArena;SkylineFiringRange;SkylineGallery;`; the stage B log has one `SKYLINE_CAPTURE stills <scene> <n>` line per
scene; `perf.json` has `"scene":"SkylineArena"`; the manifest's E stage has a note (with Tasks 14-16 in, an `ok`).

---

### Task 14: The player and the guns in Unity: input, firing, the Breacher's push, aim assist, the viewmodel, sound, HUD, the firing range (Unity)

**Files (all under `Assets/JamKit/Templates/SkylineRepo/`):**
- Create: `Scripts/Player/SkylineInputFrame.cs`, `Scripts/Player/SkylineDeviceInput.cs`, `Scripts/Player/SkylinePlayer.cs`,
  `Scripts/Player/SkylineShooting.cs`, `Scripts/Player/SkylineViewmodel.cs`, `Scripts/Player/SkylineTargetPlate.cs`
- Create: `Scripts/Audio/SkylineSounds.cs`, `Scripts/Fx/SkylineTracer.cs`, `Scripts/Fx/SkylineImpact.cs`, `Scripts/Hud/SkylineHud.cs`
- Create: `Scripts/Range/SkylineRangeDirector.cs`; `Editor/SkylinePlayerBuilder.cs`; fill `Editor/SkylineRangeBuilder.cs`

**Interfaces:**
- Consumes: Task 4's `SkylineGun`, `Loadout`, `GunCue`, `GunDecisions`; Task 5's `SkylineGuns`; Task 3's `AimAssist`,
  `CameraKick`; Task 6's `WritTuning`, `Writ`; Task 7's `BreacherKnockback`, `BoostCharge`, `BreacherBoostDef`; Task 8's
  `PlayerArmor`; M0's `DecisionLog`, `LedgerRun`; `JamKit.Combat.HitStop`, `ScreenShake`; Task 12's
  `MovementController.InputOverride`, `allowOutsidePush`, `AddPush`, `SetLook`, `Sim`; Task 13's contracts and
  `SkylineRunArgs`; `JamKit.Audio.ToneSynth` (the marker beep).
- Produces: `struct SkylineInputFrame { Vector2 move, look; bool lookFromPad, jumpHeld, jumpPressed, crouchHeld,
  crouchPressed, sprintHeld, fireHeld, firePressed, reloadPressed, swapPressed, inspectPressed; }`;
  `static class SkylineDeviceInput { SkylineInputFrame Read(float dt) }`; `class SkylinePlayer : MonoBehaviour,
  ISkylineTarget { Func<SkylineInputFrame> ScriptedInput; Loadout Loadout; PlayerArmor Armor; BoostCharge Boost;
  WritTuning Writ; DecisionLog Decisions; LedgerRun Ledger; MovementController Movement; Camera Camera; Vector3
  EyePosition, AimDirection; float Yaw, Pitch; event Action<GunCue, int> CueRaised; event Action<SkylineHitInfo,
  SkylineHitResult> HitLanded; void ReEquip(); void SetAim(float yaw, float pitch); }`; `static class SkylineSounds {
  AudioClip Get(string sound); void PlayViewmodel(string sound, float volume = 1f); void PlayAt(string sound, Vector3
  position, float volume = 1f) }` (clips from `Resources/SkylineAudio/<sound>`; a missing one logs `SKYLINE_SOUND
  missing <sound>` once); `SkylineHud { static SkylineHud Instance; void Say(Speaker speaker, string text); void
  Marker(SkylineHitResult r) }`; `SkylinePlayerBuilder.Build(Transform parent, Vector3 feet, float yaw) : SkylinePlayer`
  (editor); the model contract `Resources/SkylineModels/<gun>/<gun>` (an FBX with children `Mag` and `Muzzle`) and
  `Resources/SkylineModels/arms/arms` (children `HandR`, `HandL`), used when present, primitives otherwise.

Behaviour, in the order `SkylinePlayer.Update` does it (`[DefaultExecutionOrder(-50)]`, so it runs before
`MovementController`):
1. Read a frame: `ScriptedInput?.Invoke() ?? SkylineDeviceInput.Read(Time.unscaledDeltaTime)`. Keyboard and mouse:
   WASD, mouse look (0.09 deg per pixel), Space jump, Left Ctrl or C crouch, Left Shift sprint, left button fire, R
   reload, Q or the wheel swap, F inspect. Pad: left stick, right stick (190 deg/s, the same response curve as
   `MovementController`), A jump, B crouch, L3 sprint, right trigger fire (pressed past 0.5), X reload, Y swap, D-pad up
   inspect. `lookFromPad` is true when the last look input came from the pad.
2. Look: with the pad, compute `AimAssist.Compute(EyePosition, Yaw, Pitch, targets, moving || turning, dt,
   Active.Def.aimAssist)` over `SkylineTargets.Aimables` that are `Alive` and in line of sight (a `Physics.Linecast`
   from the eye to the centre hitting nothing else first); scale the stick turn by `turnScale`, add the nudges. With
   the mouse, no assist. Clamp pitch to +-85 and call `Movement.SetLook(Yaw, Pitch)`.
3. `Boost.Observe(Movement.Sim.Grounded, Movement.Mode == MoveMode.Wallrun)`.
4. Swap / reload / inspect presses go to the loadout and the active gun; then `Active.Tick(dt, fireHeld, firePressed,
   cues)`.
5. For each cue: raise `CueRaised(cue, slot)`; an `Event` with a sound plays it through `SkylineSounds.PlayViewmodel`;
   `GunDecisions.Append(...)` with the context built from the body (airborne, wall-running, sliding, in combat); a
   `Shot` calls `SkylineShooting.Fire` (pellets with `spreadDeg` sampled uniformly in the cone from a seeded `Rng`,
   `Physics.Raycast` up to `range`, a `SkylineHitZone` hit becomes a `SkylineHitInfo` with `damage x
   FalloffScale(distance)`, `fromWrit = gun.Def.id == SkylineGuns.WritId`, `knockback = enemyKnockback / pellets`),
   records `Ledger.RecordShot`, kicks the camera (`Kick.Kick(def.kick, rng in -1..1)`), adds `shakePerShot` trauma,
   requests hit stop on a hit (`hitStopSeconds`, `killHitStopSeconds` on a kill), raises `HitLanded`, shows the hit
   marker (white for a hit, yellow for a weak point, gold for a spot, red for a kill), draws a tracer
   from the muzzle, spawns an impact. A Breacher shot also calls `BreacherKnockback.SelfPush(AimDirection,
   Movement.Sim.Velocity, Movement.Sim.Grounded, Movement.Sim.def.gravity, Boost, boostDef, out boosted)` and
   `Movement.AddPush(push)`; when `boosted`, appends a `DecisionKind.Boost` decision.
6. `Kick.Tick(dt)`, the shake's tick, hit stop (`Time.timeScale = hitStop.CurrentTimeScale`, ticked on unscaled
   time); a pivot under the head takes the local rotation `Euler(Kick.Pitch + shakeSample.offset.y, Kick.Yaw +
   shakeSample.offset.x, shakeSample.roll)` (the shake's offset read as degrees), so the aim itself never moves.
7. `MovementController.InputOverride` returns a `MoveInput` built from the same frame (move, yaw, jump, crouch,
   sprint), so movement and shooting read one input per frame. Set on the controller in `Awake`:
   `allowOutsidePush = true`, `managePointerLock = false` (Skyline locks the cursor itself while playing),
   `onlyMarkedWalls = true` (every runnable wall carries a `MovementSurface`; the lint in Task 16 holds builders to it).
8. `ISkylineTarget.TakeAttack(damage, attackerId)` → `Armor.Damage`; on `platingBroke` the HUD flashes the plating
   bar; on `wrecked` the M1 arena restarts at the warm-up (no run structure yet; the creator's death flow is M4).

The viewmodel (`SkylineViewmodel`, under the camera, its own layer-free hierarchy drawn with a 60-degree FOV overlay
camera stacked on the main camera so it never clips into walls): it samples the active gun's `Handling.Pose` every frame
and applies `gunOffset` / `gunEuler` to the gun root and `magOffset` to `Mag`; with no clip playing, the rest pose plus
a small walk bob from `Movement.StridePhase`. Equip starts from the clip's first frame (the gun rises into view).

The firing range (`SkylineRangeBuilder`): a 12 x 30 m room (floor, walls, back wall), the player rig at the origin
facing +Z, three `SkylineTargetPlate`s (box colliders with `SkylineHitZone`, a plate that tips over when hit) at 8, 12
and 18 m, the HUD, a 2D `AudioSource` for the marker beep, and a `SkylineRangeDirector`. The director (only when
`SkylineRunArgs.Demo`) drives `ScriptedInput` from a fixed script: wait 2.0 s; play the 80 ms 440 Hz marker beep
(`ToneSynth`) and note `Time.time`; for each gun (the Writ, then a swap to the Breacher): re-equip, three shots 1.0 s
apart at the first plate, a partial reload, an inspect, then fire at the fire interval + 0.1 s until empty, one more
press (the dry fire; its from-empty reload follows), wait for the reload to end plus 1 s. It records every `Event` cue
with a sound as `{t: Time.time - marker, gun, clip, id, sound}` and, at the end, writes
`<OutDir>/firing-range-schedule.json` and logs `SKYLINE_RANGE done events=<n>`. It also logs
`SKYLINE_RANGE <gun> <clip>` at each clip start.

- [ ] **Step 1:** Write the files above. Keep every number in the pure data (guns, boost) except presentation constants
  (mouse sensitivity, bob size, HUD layout), which go at the top of their class as named constants.
- [ ] **Step 2:** `powershell -NoProfile -File tools/skyline/test.ps1` until green (the Unity-side compile check is the
  only check an agent can run here).
- [ ] **Step 3:** Commit with explicit paths (`git add -- Assets/JamKit/Templates/SkylineRepo/Scripts/Player
  Assets/JamKit/Templates/SkylineRepo/Scripts/Audio Assets/JamKit/Templates/SkylineRepo/Scripts/Fx
  Assets/JamKit/Templates/SkylineRepo/Scripts/Hud Assets/JamKit/Templates/SkylineRepo/Scripts/Range
  Assets/JamKit/Templates/SkylineRepo/Editor/SkylinePlayerBuilder.cs
  Assets/JamKit/Templates/SkylineRepo/Editor/SkylineRangeBuilder.cs`), message "Skyline M1: the player and both guns
  in Unity (input, firing, the Breacher's push, pad aim assist, code-animated viewmodel, sound, HUD) and the firing
  range's scripted showcase", ending with the Co-Authored-By line.

**What the pass must show:** `rec-SkylineFiringRange.mp4` shows both guns' equip, three shots, a partial reload, an
inspect, the magazine emptied, a dry fire and a from-empty reload, each gun's viewmodel moving with its clips; the stage
B log has `SKYLINE_RANGE writ Equip` ... `SKYLINE_RANGE breacher ReloadEmpty` and `SKYLINE_RANGE done events=<n>` with
n of 50 or more; `firing-range-schedule.json` exists; the manifest's `E sync check` is ok (90% or more of the detectable
events within 35 ms); no `SKYLINE_SOUND missing` line; no exceptions in the log.

---

### Task 15: The bots in Unity: bodies, faces, plates that shed, attacks, wrecks, barks; the enemy gallery (Unity)

**Files (all under `Assets/JamKit/Templates/SkylineRepo/`):**
- Create: `Scripts/Bots/SkylineBot.cs`, `Scripts/Bots/SkylineBotBodies.cs`, `Scripts/Bots/SkylineBotFace.cs`,
  `Scripts/Bots/SkylineBolt.cs`, `Scripts/Bots/SkylineBeam.cs`, `Scripts/Bots/SkylineAcid.cs`,
  `Scripts/Bots/SkylineWreck.cs`, `Scripts/Bots/SkylineSpotMarker.cs`, `Scripts/Bots/SkylineBarks.cs`,
  `Scripts/Bots/SkylineDummy.cs`, `Scripts/Bots/SkylineGalleryDirector.cs`, `Scripts/Voice/SkylineBabble.cs`
- Fill: `Editor/SkylineGalleryBuilder.cs`

**Interfaces:**
- Consumes: Task 8's `EnemyDef`, `EnemyBody`, `SkylineEnemies`, `Wreck`, `PlayerArmor.Strip`; Task 9's `BotBrain`,
  `BotCue`, `BotFace`, `AcidSweep`, `TelegraphedShot`; Task 6's `WeakSpots`, `Writ.Resolve`, `WritTuning`; Task 10's
  `Difficulties.Normal()`; M0's `Aim.LeadPoint`; Task 13's contracts; Task 14's `SkylineSounds` (if Task 14 isn't merged
  yet, write against the signature in its Interfaces; the compile check catches drift); `JamKit.Audio.ToneSynth`.
- Produces: `class SkylineBot : MonoBehaviour, ISkylineHittable, ISkylineAimable { static List<SkylineBot> Live; static
  event Action<SkylineBot> SpotCreated, FirstSeenAny, Wrecked; EnemyDef Def; EnemyBody Body; BotBrain Brain; WeakSpots
  Spots; bool practice; ISkylineTarget target; static SkylineBot Spawn(string enemyId, Vector3 position, float yaw,
  ulong seed, ISkylineTarget target, WritTuning writ, Transform parent) }`; `class SkylineWreck : MonoBehaviour { Wreck Data }` (stripped
  automatically when the player comes within 1.8 m: `Armor.Strip(Data, Time.time, Difficulties.Normal().scrapScale)`);
  `static class SkylineBabble { AudioClip Make(string voice, string text, int seed); void Speak(Speaker speaker, string
  text, AudioSource source) }` (Undertale-style beeps built with `ToneSynth`, one voice per speaker and per bot,
  spec 3.6; Dot's is band-passed like a radio); the bot model contract `Resources/SkylineModels/bots/<id>` (an FBX with
  children `Plate_<plateId>` for each of its `PlateDef`s, `WeakPoint`, `Face`, and `Muzzle` (drone, spotlight) /
  `Nozzle` (floor-care) / `Lamp` (spotlight)), faces `Resources/SkylineModels/bots/<id>_face_<BotFace>` (textures),
  sounds `Resources/SkylineAudio/<id>/<name>` with names `idle_loop, alert, telegraph, attack, hurt, plate_shed, wreck`
  (plus `lock_beep` for the spotlight and `acid_sweep` for the floor-care unit). Missing models, faces or sounds fall
  back to primitives, generated pixel faces and silence (logged once).

Behaviour:
- **Body.** Built from the model when present, else from primitives named by the same contract, in CONCIERGE white and
  gold. Each plate child gets a `SkylineHitZone { zone = Plate, plate = i }`, the weak point `zone = WeakPoint`, the rest
  `zone = Body`. A `practice` bot never attacks, regrows a shed plate after 3 s and refills its chassis instead of
  wrecking (Dot's lesson needs it to last).
- **Brain.** Each frame: `sees` = within `range` and a clear line of sight to `target.Chest`; `Brain.Tick(dt, sees,
  cues)`. State cues set the face and play `alert` / `telegraph`; the spotlight starts a `TelegraphedShot` (its
  `telegraph`, `lockBefore`, `followRate`, `hitRadius` from the def) on entering Telegraph and steps it each frame with
  `target.Chest`; `SkylineBeam` draws it (a thin red line from `Lamp` to the beam point that turns white and widens at
  `Locked`; `lock_beep` plays on the lock). Attack cues fire: the drone a `SkylineBolt` at `Aim.LeadPoint(muzzle, chest,
  target.Velocity, projectileSpeed, Difficulties.Normal().leadFactor)`, tilted by a random error within the def's
  `aimSpreadDeg` (the same aim error the dodge measurement assumes); the spotlight resolves its shot (a hit needs the
  beam's `hit` and a clear line of sight now: breaking line of sight dodges it); the floor-care unit turns to face the
  target and runs `SkylineAcid` for `sweepSeconds`, damaging once per sweep when `AcidSweep.Touches(...)` (with a
  visible jet and a puddle line along its path). Damage goes through `target.TakeAttack(def.attackDamage, def.id)`.
- **Moving.** The drone holds its spawn height and drifts in a slow 1.5 m figure-eight; the floor-care unit rolls toward
  the target at 2.5 m/s on the floor and stops 3 m short to sweep; the spotlight only turns. A Breacher hit adds
  `knockback` to a short-lived velocity (drones and floor-care units slide, the spotlight shakes).
- **Hits.** `TakeHit`: `Writ.Resolve(Spots, zone, localPoint, time, fromWrit, gun.weakPointMultiplier, writ)` (the
  `WritTuning` handed to `Spawn`: the player's, so the Writ's upgrades reach every bot; the gallery passes a new one), then
  `Body.Damage(damage x multiplier, plate)`; `Brain.OnDamaged`; a shed plate detaches (unparented, a `Rigidbody` with an
  impulse along the hit normal and up, `Physics.IgnoreCollision` with the player's controller, faded and destroyed after
  6 s) and plays `plate_shed`; a created spot spawns a `SkylineSpotMarker` (a small gold emissive disc on the surface,
  pulsing, removed when consumed or expired) and raises `SpotCreated`; a wreck bursts the remaining plates and 4-6 parts
  outward, plays `wreck`, raises `Wrecked`, and leaves a `SkylineWreck` that glows orange while fresh (8 s).
- **Barks.** On a Bark cue, `SkylineBarks.Line(def.id, bark)` (drafts for the creator, one file:
  drone "Halt! Mall policy!", "Assault on an officer!", "Suspect has left the premises.", "Code... red...";
  floor-care "Oh! A stain!", "Rude!", "All clean!", "Out of... order..."; spotlight "Target acquired. Smile.",
  "Lens damage reported.", "Scanning.", "Lights... out.") is shown in a small world-space bubble for 1.5 s and spoken with
  the bot's babble voice.
- **Gallery** (`SkylineGalleryBuilder` + `SkylineGalleryDirector`): the three bots 7 m apart on pedestals facing a
  fixed camera, each with a `SkylineDummy` (an `ISkylineTarget` mannequin that never dies) behind the camera's line
  that the director reveals in turn. Script per bot (12 s each, in the order drone, floor-care unit, spotlight): idle 2 s;
  reveal the dummy (alert, then two attack cycles); then the director's invisible shooter lands Writ hits on plate 0,
  plate 1 (both shed), the weak point (a spot appears), the spot (consumed), then the chassis until it wrecks. Logs:
  `SKYLINE_GALLERY <bot> state <State>`, `SKYLINE_GALLERY <bot> plate-shed <plateId>`, `SKYLINE_GALLERY <bot> spot
  created|hit`, `SKYLINE_GALLERY <bot> wrecked`. Four `SkylineStillCamera`s: one per bot close-up and one wide.

- [ ] **Step 1:** Write the files. Sound and model names come only from the contract above.
- [ ] **Step 2:** `powershell -NoProfile -File tools/skyline/test.ps1` until green.
- [ ] **Step 3:** Commit with explicit paths (`Scripts/Bots`, `Scripts/Voice`, `Editor/SkylineGalleryBuilder.cs`),
  message "Skyline M1: the bots in Unity (bodies, faces, plates that shed, attacks, wrecks with scrap, barks) and the
  enemy gallery", ending with the Co-Authored-By line.

**What the pass must show:** `rec-SkylineGallery.mp4` shows each bot's face change from happy to alert to angry, its
telegraph before each attack (the drone's charge, the floor-care unit's spin-up and sweep, the spotlight's red beam
turning white), plates flying off where they're hit, a gold spot appearing and being shot off, and a wreck burst; the
stage B log has, for each of the three bots, `state Alert`, `state Telegraph`, two `plate-shed` lines, `spot created`,
`spot hit` and `wrecked`; four `still-SkylineGallery-*.png`; no exceptions.

---

### Task 16: The arena, its teaching beats, its demo run, and the lint (Unity)

**Files (all under `Assets/JamKit/Templates/SkylineRepo/`):**
- Fill: `Editor/SkylineArenaBuilder.cs`
- Create: `Editor/SkylineLint.cs`; `Scripts/Arena/SkylineArenaDirector.cs`, `Scripts/Arena/SkylineEncounterZone.cs`,
  `Scripts/Arena/SkylineScrapCache.cs`, `Scripts/Arena/SkylineArenaDemo.cs`, `Scripts/Arena/SkylineNoCollider.cs`
- Modify: `Editor/SkylinePassBatch.cs` (run the lint on the arena; add `"lint":{"count":N,"problems":[...]}` to
  editor-batch.json; it does not change `ok`)

**Interfaces:**
- Consumes: Task 10's `GalleriaArena`, `EncounterDef`, `TeachingBeats`, `TeachingBeat`, `BeatTrigger`, `Speaker`;
  Task 14's `SkylinePlayer`, `SkylinePlayerBuilder`, `SkylineHud.Say`; Task 15's `SkylineBot.Spawn`, its events, and
  `SkylineBabble.Speak`; `JamKit.Movement.MovementSurface { bool runnable }`; Task 13's `SkylineStillCamera`,
  `SkylineRunArgs`.
- Produces: the arena scene; `SkylineLint.Run(string scenePath) : List<string>` (problems, one line each); log lines
  `SKYLINE_LINE <beat id>`, `SKYLINE_ARENA encounter <id> start|cleared`, `SKYLINE_ARENA boost-ledge reached`,
  `SKYLINE_DEMO <step>`.

The arena (`SkylineArenaBuilder.Build`, from `GalleriaArena`'s numbers only):
- **The warm-up hall** (x -15..15, z -48..-20): floor, walls (each with a `MovementSurface`, runnable), the player rig at
  `PlayerStart` facing +Z, the practice bot (`SkylineBot.Spawn(SkylineEnemies.DroneId, PracticeBot, ...)` with
  `practice = true`, on a gantry so it reads as a training rig), the kiosk (a 3 x 3 m box up to `BoostLedgeTop.Y`,
  `MovementSurface { runnable = false }`, perfume-counter glass sides) with a `SkylineScrapCache` on top (taking it tops
  the plating up to its maximum and fires `BoostLedgeReached`), and a doorway (6 m wide, 5 m tall) into the hall at z -20.
- **The main hall** (x -25..25, z -20..20): a two-storey atrium: the floor; shop fronts all round (runnable walls,
  marked); a mezzanine walkway 4 m deep at `MezzanineY` along the north and both sides with railings and two stairways
  from the floor; a glass skylight roof at 14 m; cover pieces in the open (two planters, a fountain, a kiosk; cover that
  never blocks every line to a spawn); spawn markers from `FoodCourt()` and `Skylight()`; two `SkylineEncounterZone`
  triggers (the food court's: the hall floor south of z 5, entered from the doorway; the skylight's: the north half,
  armed once the food court is cleared).
- **Stills:** eight `SkylineStillCamera`s: `01-warmup-start`, `02-practice-bot`, `03-kiosk-ledge`, `04-doorway`,
  `05-food-court`, `06-mezzanine`, `07-hall-wide`, `08-skylight`.
- **Light (stand-in until Task 21):** one directional sun at 50 degrees elevation and a procedural sky; plain URP Lit
  materials. The lint will flag every untextured surface; that is expected until Task 21 and is not a stage A failure.

The director (`SkylineArenaDirector`): encounters spawn when their zone is entered (`SkylineBot.Spawn` for each spawn,
seeded by index, target = the player, `WritTuning` = the player's); `LedgerRun.BeginCombat` at the first alert,
`EndCombat` when the last bot of the encounter is wrecked; teaching beats fire once each, queued so lines never overlap
(0.5 s between): `WarmupStart` 1 s after the scene starts; `PracticeBotSeen` when the practice bot is within 25 m and on
screen; `FirstSpotCreated` on `SkylineBot.SpotCreated`; `NearBoostLedge` within 6 m of the kiosk; `BoostLedgeReached`
from the cache; `FirstPlatingBreak` when `Armor.PlatingBreaks` reaches 1; `FirstSeen` per enemy id the first time a
bot of that id goes Alert. Each beat calls `SkylineHud.Say(beat.speaker, beat.text)`, speaks it with
`SkylineBabble.Speak`, and logs `SKYLINE_LINE <id>`.

The demo (`SkylineArenaDemo`, only when `SkylineRunArgs.Demo`): drives `ScriptedInput` with perfect aim computed by
`AimAssist.Angles`: walk to 10 m from the practice bot; three Writ shots at its weak point (a spot), one at the spot;
swap to the Breacher; walk to 0.8 m short of the kiosk's south face; jump, double jump at the apex, aim straight down,
fire (the boost), hold forward (the mantle); take the cache; drop off; run through the doorway; fight the food court
(strafe and reverse every 0.4-0.7 s while shooting the nearest bot with the Writ); then the skylight. Logs `SKYLINE_DEMO
<step>` at each step. It doesn't need to win; the recording (60 s) and the frame-time probe (3 s warm-up + 20 s) just
need the arena busy.

The lint (`SkylineLint.Run`), each problem one line naming the object:
1. a `MeshRenderer` with no `Collider` on it or a parent, unless under an object named `... (fx)` or `... (backdrop)` or
   carrying `SkylineNoCollider`;
2. two static colliders (no `Rigidbody`, not parent and child) whose `Physics.ComputePenetration` depth exceeds 0.02 m;
3. a wall face (a collider whose world bounds are at least 2 m tall and 3 m long, nearly vertical) with no
   `MovementSurface` on it or a parent: "unmarked wall: runnable or not?";
4. a renderer whose material has no `_BaseMap` texture, unless the material's name starts with `SK_Emissive` or
   `SK_Glass`: "untextured: <object> (<material>)".

- [ ] **Step 1:** Write the files. Every position, size and spawn comes from `GalleriaArena`; the builder adds only
  dressing-free geometry and markers.
- [ ] **Step 2:** `powershell -NoProfile -File tools/skyline/test.ps1` until green.
- [ ] **Step 3:** Commit with explicit paths (`Editor/SkylineArenaBuilder.cs`, `Editor/SkylineLint.cs`,
  `Editor/SkylinePassBatch.cs`, `Scripts/Arena`), message "Skyline M1: the arena (warm-up hall with the practice bot
  and boost ledge, the atrium with two encounters), teaching beats, the demo run, the lint", ending with the
  Co-Authored-By line.

**What the pass must show:** `rec-SkylineArena.mp4` shows the warm-up (Nan's line), the practice bot growing a spot
(Dot's lines), the boost onto the kiosk (`SKYLINE_ARENA boost-ledge reached`), the food court fight with CONCIERGE's
drone and floor-care notices, then the skylight fight with the spotlight notice; the stage B log has `SKYLINE_LINE` for
`nan-warmup`, `dot-writ-spots`, `dot-writ-spot-made`, `dot-breacher-boost`, `dot-boost-done`, `concierge-drone`,
`concierge-floor-care` (and `concierge-spotlight` if the demo reaches the skylight in 60 s); eight
`still-SkylineArena-*.png`; `editor-batch.json` has a `lint` block (problems expected until Task 21); `perf.json` from
the arena with the demo running.

---

### Task 17 (main session only): The Unity batch pass, fixes, metas

- [ ] **Step 1:** Check no `Unity.exe` has the VibeJamKit project open, then run the pass in the background, uncapped
  (the runner caps stages A and C itself): `powershell -NoProfile -File tools/skyline/pass.ps1`. It waits for the lock.
- [ ] **Step 2:** Read `manifest.json` and the stage logs. Check every "What the pass must show" list of Tasks 13-16,
  line by line. A compile error or a failed check goes back to the task's agent (by file ownership) with the log
  excerpt; re-run only after the fixes; one commit per fix.
- [ ] **Step 3:** Spot-check the recordings yourself only as far as the checks need (a frame or two via ffmpeg at the
  logged times); the full look is the judges' job (Task 23).
- [ ] **Step 4:** When the pass is green apart from the lint's untextured surfaces, commit the `.meta` files Unity
  generated (`git status --short -- Assets/JamKit/Templates/SkylineRepo Packages/com.vibejam.jamkit/Runtime/Skyline
  Packages/com.vibejam.jamkit/Tests/Skyline Packages/com.vibejam.jamkit/Tests/Core/Movement`), by explicit path.
- [ ] **Step 5:** Add a `docs/SKYLINE-WORKLOG.md` entry with the pass's numbers (tests, sync share and worst ms, frame
  times, lint count), then prune older `out/skyline/<stamp>` folders that were judged.

---
## Phase 4: Content (identity agents, one pass)

Spec 9.3: "one identity agent per weapon and per enemy: design, numbers, model, VFX, layered sound, the handling set
(for enemies: faces, states, barks), and a check that it fits the roster." These are agent briefs, not TDD code. Each
agent works in its own folders (no two agents edit one file), never runs Unity, and ends with the deliverables in place,
`tools/skyline/test.ps1` green, and a short report: what it built, the measurements, and what the pass must show. The
main session then runs one pass for all four (Task 22).

**Rules for every content agent:**
- CC0 first (Kenney, Quaternius, Poly Haven, ambientCG; licences in `docs/JAM-ASSETS-AND-PRESENTATION.md` section 1).
  Record each source (URL, licence, files) in `Assets/JamKit/Templates/SkylineRepo/CREDITS.md`. Building in Blender
  from primitives and scripts is fine (the machine's Blender is shared; short renders only, through
  `node C:/Users/User/.claude/hooks/gpu-short-run.mjs -- <command>`).
- **Tripo** (`tools/tripo/gen.mjs`) only where CC0 and modelling can't express the identity, and only after the creator
  has answered the credit question (Task 24's question list carries it; until then: no Tripo). Tripo's free outputs are
  CC BY 4.0 and need a credit.
- Models are FBX files under `Assets/JamKit/Templates/SkylineRepo/Resources/SkylineModels/` following the contracts of
  Tasks 14-15 (child names). No hand-written `.meta` files; no prefabs written by hand.
- Sounds are forge recipes rendered by `render_all.py`; shipped layers are synthesized, recorded by us, or CC0. The
  reference clips (`out/skyline-refs/`, Task 1) are measuring sticks only: never a layer, never copied into the repo.
- Numbers may be retuned in the data files (`SkylineGuns.cs`, `SkylineEnemies` in `Enemies.cs`) as long as every test
  stays green; a changed clip means re-running `WriteTheForgeTimelines` and `render_all.py`; a changed attack number
  means re-running Task 11's dodge test (and re-tuning if it leaves the band).
- Gemini (the ears' layers 3-4) is not used in M1 unless the creator has made the API key.
- Budgets (spec 10, performance): a viewmodel gun at most 15k triangles, the arms 10k, a bot 8k; textures at most 2K,
  PBR (base, normal, mask); far things cost less (LODs on anything with more than 2k triangles in the arena).

### Task 18: The Writ's identity agent (content)

**Owns:** `docs/SKYLINE-GUN-WRIT.md`, `tools/skyline/guns/writ/` (recipes; timelines only through the export),
`Resources/SkylineModels/writ/`, `Resources/SkylineModels/arms/` (the robot arms both guns share),
`Resources/SkylineAudio/writ/` (rendered), the Writ's VFX under `Assets/JamKit/Templates/SkylineRepo/Fx/Writ/`
(materials, textures, a `SkylineWritFx.cs` the player calls through the hook `SkylinePlayer.HitLanded`), and the Writ's
numbers in `SkylineGuns.Writ()`.

**Brief:** the Writ is a precise sidearm whose weak-point hits leave weak spots that take more than a headshot (2.2).
Make that identity readable by eye and by ear. The creator's bar for sound: "phenomenal", like "a well-oiled hydraulic
masterpiece"; the RPM leaves the visuals room to breathe (2.3). The viewmodel is robot arms; reloads are mechanical and
a bit fantastical, animated in code from the clips (edit the pose frames in `SkylineGuns.Writ()`).

**Deliverables:**
1. The identity sheet `docs/SKYLINE-GUN-WRIT.md`: the fantasy in a paragraph; the verbs; every number and why; the sound
   palette per event (layers and what each does); the visual language (silhouette, materials, the firm's orange with
   hazard stripes on the arms, spot creation and pop); the full upgrade path (`UpgradePaths.Writ()`: what each step
   looks and sounds like); and a "For the creator's ears" list (what to listen for, which reference each event aims at).
2. The model and the arms (CC0 base or built), with `Mag` and `Muzzle`; the arms with `HandR` / `HandL`.
3. VFX: a muzzle flash (at most 3 frames), a tracer, impact sparks and a decal, the spot's creation flare, its idle glow
   and its pop on a spot hit (distinct from a normal hit), a kill burst accent.
4. Layered sounds for all 12 events (the shot at least three layers: transient, body, tail), plus `writ/impact`,
   `writ/impact_weak` and `writ/spot_pop` (the Unity side plays them on hits when present), rendered with
   `render_all.py --gun writ`.

**Acceptance (measured; the report shows each number):**
- For every event with an approved reference (Task 1's list; until approved, the first candidate),
  `listen.py compare out/skyline-forge/writ/<Clip>.wav out/skyline-refs/writ/<event>-*.wav --png
  out/skyline-forge/writ/compare/<event>.png`: no clipping (true peak at most -1 dBTP); the shot's attack at most
  max(5 ms, 1.5 x the reference's), crest factor within 4 dB, tail within 35%, spectral centroid within 30%, every band
  share within 0.12 of the reference. Where a number is out on purpose (the identity differs), the sheet says why.
- Each rendered reload clip (`out/skyline-forge/writ/ReloadPartial.wav`, `ReloadEmpty.wav`): `listen.py events` finds an
  onset at every sound event's time (its fraction of the clip) within 15 ms; extra onsets inside one event's own sound
  are fine (a pump has two strokes). With the first-cut sounds, the dry run found the Writ's from-empty reload at 0.228,
  0.735, 0.963 and 1.400 s against 0.228, 0.735, 0.963 and 1.400 planned.
- From the pass (Task 22): stage E ok with 90% or more of the detectable events within 35 ms; in
  `rec-SkylineFiringRange.mp4` every Writ clip reads at a glance; no `SKYLINE_SOUND missing writ/...`; the viewmodel
  never clips through the camera's near plane.
- `tools/skyline/test.ps1` green (including the forge-timeline test and the dodge test).

### Task 19: The Breacher's identity agent (content)

**Owns:** `docs/SKYLINE-GUN-BREACHER.md`, `tools/skyline/guns/breacher/`, `Resources/SkylineModels/breacher/`,
`Resources/SkylineAudio/breacher/`, `Assets/JamKit/Templates/SkylineRepo/Fx/Breacher/` (with `SkylineBreacherFx.cs`,
driven from `SkylinePlayer.HitLanded` and `CueRaised`), and the Breacher's numbers in `SkylineGuns.Breacher()` and
`BreacherBoostDef` (tunable within the spec's limits: one boost per airtime, no self-damage, about the jump upgrade's
height).

**Brief:** a door-breach shotgun whose identity is knockback, including on yourself: the self-push is a movement tool
(2.2; TF2's rocket jump, Destiny 2's Micro-Missile frames). Make the knockback felt three ways: bots slide and plates
fly, the camera kicks hard, and a boost looks and sounds like a blast under your feet. Uses the arms from Task 18 (if
Task 18 hasn't delivered them yet, use the primitive arms and say so).

**Deliverables:** as Task 18's, for the Breacher: the identity sheet (with the boost's rules in plain words and its
upgrade path from `UpgradePaths.Breacher()`), the model with `Mag` (the cell) and `Muzzle`, VFX (a muzzle blast, a
shockwave ring, pellet sparks, a dust ring and a pressure puff for a boost, the cell's vent), the 12 event sounds plus
`breacher/impact` and `breacher/boost` (played on a boost), and the reference comparison table.

**Acceptance:** Task 18's measurements for the Breacher's events (the blast against its references; the reload clips'
onsets); the pass shows the Breacher's clips in the firing range, stage E ok; in `rec-SkylineArena.mp4` the demo's boost
onto the kiosk reads as a blast (judges); the boost's sim tests still pass (`BreacherBoostSimTests`).

### Task 20: The three bots' identity agents (content; three agents in parallel)

**Owns (per bot `<id>`):** a section of `docs/SKYLINE-BOTS.md` (one agent writes the file's skeleton first; each bot's
agent fills only its own section), `Resources/SkylineModels/bots/<id>*`, `tools/skyline/bots/<id>/recipes/`,
`Resources/SkylineAudio/<id>/`, the bot's barks in `Scripts/Bots/SkylineBarks.cs` (its own lines only) and its numbers
in `SkylineEnemies.<Bot>()` (plates, chassis, timings; the aimed numbers only through Task 11's band). The first agent
extends `render_all.py` to render `tools/skyline/bots/<id>/recipes` into `Resources/SkylineAudio/<id>/` too.

**Brief (2.5):** every enemy has a face or display, idle, alert and attack states, its own sounds and barks, an aim tag
and a layer tag; its identity reads at a glance and by ear. CONCIERGE's bots are white and gold. The floor-care unit is
a roomba with a happy face that turns angry at a "stain" (you) and sweeps acid at ankle height; the mall-cop drone is
officious and leads your movement; the spotlight is a searchlight with a rifle whose beam follows you, locks, and fires.
Barks are lines in a synthesized babble voice of the bot's own (3.6), drafts for the creator.

**Deliverables per bot:** the model (plates as separate children named `Plate_<plateId>` matching its `PlateDef`s, so
plates shed where hit; `WeakPoint`; `Face`; `Muzzle` / `Nozzle` / `Lamp`), the five face textures, the sounds named by
Task 15's contract (`idle_loop, alert, telegraph, attack, hurt, plate_shed, wreck`, plus `lock_beep` / `acid_sweep`),
its babble voice settings and four barks, and its section of the sheet (personality, states, what its telegraph looks
and sounds like, the habit it punishes).

**Acceptance:** from the pass (Task 22): `rec-SkylineGallery.mp4` shows each bot's state changes, telegraphs, plates
shedding where hit, a spot, a wreck; the gallery stills read at a glance (judges score enemy readability 8 or more); the
three bots' attack sounds are told apart by the ears (`listen.py measure` on each: centroids at least 30% apart pairwise,
or band shares 0.15 apart); each telegraph sound's onset leads its attack by the def's `telegraphSeconds` (checked from
the gallery recording's onsets at the logged times, within 50 ms); no missing models, faces or sounds in the log.

### Task 21: The arena's look and dressing (content)

**Owns:** `Assets/JamKit/Templates/SkylineRepo/Look/` (the Skyline afternoon rig, the post profile, the sky), the
Skyline material library under `Assets/JamKit/Templates/SkylineRepo/ThirdParty/Library/` and its catalogue
`docs/SKYLINE-LOOK.md`, props under `Assets/JamKit/Templates/SkylineRepo/ThirdParty/Props/`, and the dressing calls in
`Editor/SkylineArenaBuilder.cs` (a `Dress(...)` section; positions of gameplay pieces stay `GalleriaArena`'s).

**Brief (spec 10, items 1-6 and 12):** a bright, sunny near-future afternoon inside a glass mall atrium: crisp sun
through the skylight, glass reflections, blue sky with high cloud, haze over the far towers seen through the glass,
bounce light, bloom on highlights; interiors lit by skylights and shop signage. Stylized AAA, coherent, CONCIERGE's
white and gold; PBR with normal maps everywhere; trim, bevels, signage, interiors behind the shop glass; props at real
scale; no visible repetition. Reuse Visiting Hours' Look library by calling its API (`VhLook.ApplyGoldenHour` with
afternoon options as the rig's base, `VhLookUv`, `VhLookLibrary`, `VhLook.AddReflectionProbe`) or copying its patterns
(`docs/redesign/look.md`; add `JamKit.Look` and `JamKit.Look.Editor` to Skyline's asmdef references, and their built DLLs
from `Library/ScriptAssemblies` to the Unity-side compile check). New CC0 sets (terrazzo, brushed steel, shopfront
glass, white and gold trim, signage) from Poly Haven and ambientCG, prepared with `tools/look/prep-library.py`; a Poly
Haven HDRI prepared with `tools/look/hdri-prep.py`. The firm's orange with hazard stripes stays on the player's things.

**Acceptance:** from the pass (Task 22): the lint reports zero problems; the eight arena stills show no untextured
surface, a readable route (the doorway frames the hall, the kiosk is a landmark, the next step and the far goal visible)
and the afternoon light; `perf.json` (scene `SkylineArena`, the demo running) has its 1% low at or under 16.7 ms
(target 8.3 ms average); stage A passes under the 30% memory cap (the scene opens within 4.6 GB); judges score lighting,
materials, art direction and level design 8 or more.

### Task 22 (main session only): The content pass

- [ ] **Step 1:** When Tasks 18-21 have reported, run `powershell -NoProfile -File tools/skyline/pass.ps1`.
- [ ] **Step 2:** Check every acceptance line of Tasks 18-21 that the pass measures; send failures back to the owning
  agent with the evidence; re-run after fixes.
- [ ] **Step 3:** Commit the new `.meta` files Unity generated, by explicit path; prune judged `out/skyline/<stamp>`
  folders; add the pass's numbers to `docs/SKYLINE-WORKLOG.md`.

---

## Phase 5: Judging and the creator's play session

### Task 23 (main session dispatches): The adversarial judge loop against the Skyline bar

Spec 9.6 and 10: judges score each batch's captures against the bar until every category is 8/10 or more with no
blockers; green tests are never evidence of quality; judges keep a category "not covered by the spec" (gaps recorded and
raised, not quietly patched).

- [ ] **Step 1: Prepare the evidence once** (main session, no Unity): from the latest pass folder, the stills; frames
  from each recording at the logged times (`ffmpeg -ss <t> -i rec-<scene>.mp4 -frames:v 1 frame-<scene>-<t>.png`; about
  12 per recording); `manifest.json`, `sync-check.json`, `perf.json`, the lint block, the ears' compare sheets, the
  identity sheets. Put the list of files in `out/skyline/<stamp>/judge-pack.txt`.
- [ ] **Step 2: Five judges in parallel**, fresh agents, each with the spec's sections 2, 6.8, 10 and its own
  categories: (A) lighting and sky, materials, art direction; (B) level design, movement challenge, physical integrity;
  (C) gun-feel (looks, sounds via the ears' numbers and pictures, numbers, handling sets complete and in sync);
  (D) enemy readability, encounter fairness (clean line, aim models, counters announced), consequence; (E) presentation
  (no text pops, no clipped text, camera and audio sell each beat) and performance. Each writes
  `docs/skyline-judge/m1-r<round>-<group>.md`: defects first (each with the evidence file and frame), then scores 1-10
  per category, blockers (spec 10's list), and "not covered by the spec".
- [ ] **Step 3: Fix round.** The main session groups the defects by owning file and dispatches fixes to the owning
  agents (Tasks 14-16, 18-21), runs one pass, and re-judges only the categories under 8 or with blockers. At most three
  rounds; what remains after round 3 goes to the creator as a short list with the evidence, rather than a fourth round.
- [ ] **Step 4:** Commit the judge reports (`git add -- docs/skyline-judge`).

### Task 24 (main session): The creator plays; M1 close-out

- [ ] **Step 1:** Hand the creator the build (`out/skyline/<stamp>/build/SkylineRepo.exe`, pad or keyboard) with a
  one-page play sheet: what to try (both guns' full handling sets, the inspect, the boost onto the kiosk, both fights,
  stripping wrecks), what to listen for (each gun's sheet's "For the creator's ears"), the reference list to approve
  (`docs/SKYLINE-REFS.md`), and the questions below. The creator's hands and ears are the last word on guns and feel.
- [ ] **Step 2: Questions for the creator** (batched, spec 13): the seven pending ones of spec 14 that still stand
  (the Breacher's limits as built; the starting loadout; the ears' Gemini key), plus this plan's decisions: the
  spotlight's sweep lock; spots consumed on hit and capped at three; the Breacher's ground shove and fall-cancelling
  boost; the dodge band 1.6-2.5 and leaving area attacks out of it; wrecks stripped by touch; a wrecked Unit restarting
  the warm-up in M1; all dialogue drafts (who rewrites them, and whether Dot and Nan get recorded voices or the babble);
  Tripo credits for any model CC0 can't cover; the first upgrades (Deeper Ink, Quick Cell) and the written paths.
- [ ] **Step 3: Close-out docs:**
  - Spec section 14, a new "while building M1 (technical, decided and logged)" block listing every decision above and
    the engine hook's exact shape (two shared files, both hooks off by default; `MoveDef`, `MoveTypes` and the shared
    asset untouched; the input hook and why).
  - `docs/SKYLINE-PIPELINE.md`: the sync check, the lint, the engine gate, `render_all.py`, the model and sound
    contracts.
  - `docs/SKYLINE-WORKLOG.md`: M1's exit criteria (spec 11.2) line by line, each with its evidence (test names, the
    dodge ratios, the pass folder, perf numbers, judge scores), and what the creator said.
  - Commit with explicit paths.
- [ ] **Step 4:** Per spec 11: stop after M1 until the hackathon is over.

---

## Spec coverage (11.2, line by line)

| M1 exit criterion | Tasks |
|---|---|
| Writ and Breacher: base identity complete | 4, 5, 6, 7 (systems), 14 (in Unity), 18, 19 (identity) |
| the full handling set (equip, fire, dry fire, partial and from-empty reload, inspect) with sound and code animation in sync | 2, 4, 5 (timelines), 13 (sync check), 14 (viewmodel, range), 18, 19 (sound) |
| aim assist on pad | 3, 14 |
| the first upgrade of each built and the whole path written | 6 (built: Deeper Ink, Quick Cell; written: both paths), 18, 19 (sheets) |
| three bots with identity, faces, states, sounds, aim and layer tags, plates shed where hit | 8, 9, 15, 20 |
| one arena dressed to the bar, with the teaching beats for both guns and all three bots (6.8) | 10 (layout, beats), 16 (arena, director), 21 (dressing) |
| the outside-push hook, with the engine gate green (8) | 12 |
| the dodging measurement in the headless sim on Normal (about twice, neither unhit) | 9 (spotlight), 10, 11 |
| 1% lows at or under 16.7 ms in a build | 13 (arena measured), 21, 22 |
| judges score every category 8/10 or more with no blockers; then the creator plays | 23, 24 |

Also from the spec: decision events from the guns (4.1: Tasks 4, 14); the Ledger's combat records (4.1: Task 14's
`Ledger.RecordShot`, Task 16's combat bounds); plating from wrecks (2.4: Tasks 8, 15); CONCIERGE's safety notices and
Dot's lessons (1.3, 6.8: Tasks 10, 16); barks in babble voices (3.6: Tasks 15, 20); the spotlight's M0 lag finding and
the dodge ratio's M0 note (Tasks 9, 11).

## Decisions this plan made (to log in spec 14 at Task 24)

1. **The spotlight's beam locks its sweep, not only its point** (Task 9): a critically damped follow that matches your
   velocity; at the lock it keeps sweeping. Straight running is hit; a direction change in the last 0.25 s dodges. This
   fixes M0's "the spotlight's lag lets straight running dodge it" while keeping the spec's tell and its dodge.
2. **The Writ's spots:** a weak point (lens, display) is a robot's headshot; a spot is consumed when hit; spot hits make
   no new spots; at most three per bot (oldest replaced); six seconds each. Weak-point x2, spot x2.5 (x3.25 with Deeper
   Ink).
3. **The first upgrades:** Writ "Deeper Ink" (spot damage) and Breacher "Quick Cell" (reload 25% faster, which proves
   the timeline rescaling); augments and capstones written as proposals.
4. **The Breacher's boost rules** (within the spec's default limits): in the air any self-push needs and spends the
   boost; on the ground a flat shot shoves without spending it; a downward shot sets the rise speed (cancels a fall,
   never stacks on a faster rise); straight down lifts 1.0 m (the double jump's height).
5. **The engine hooks:** the push is enabled per sim (`MoveSim.OutsidePushEnabled`), so `MoveDef`, `MoveTypes` and the
   shared tuning asset stay untouched; a push off a wall also clears the wall-coyote jump. `MovementController` gains an
   opt-in `InputOverride` because Skyline's scenes remove `GameRoot`, through which it reads all input.
6. **The dodge measurement:** "about twice" is 1.6-2.5; it covers aimed shots only (the floor-care sweep is dodged by
   jumping, which neither bot does); drones get a 3-degree aim error (with perfect aim the ratio was a cliff: under 1 at
   lead 0.82, 8-30 from 0.88); the dry run's starting numbers give food court 1.89 and skylight 2.22 (spotlight beam
   0.9 m, cycle 2.4 s); M1's arena encounters all leave the walls open (no wall bot until M3), so "which layer is left
   open changes room to room" starts with M3's bots.
7. **Reloads:** an empty magazine dry fires first, then reloads from empty when the click ends; no automatic reload on
   the last shot; no ammo reserve in M1.
8. **Wrecks** are stripped by touch (within 1.8 m, while fresh for 8 s); a wrecked Unit restarts the arena's warm-up in
   M1 (the real fail flow is M4's).
9. **The boost ledge** is a kiosk top at 4.6 m standing 12 m or more from any wall, so no wall-run reaches it.
10. **Dialogue** in this plan is draft, except the spec's two quoted lines; the human radio voices (Dot, Nan) use a
   radio-filtered babble until the creator decides.

---

## Execution

Recommended: **subagent-driven** (superpowers:subagent-driven-development), as in M0: Tasks 2-12 are a chain of small
pure tasks with exact interfaces, each worth a fresh reviewer; one git writer at a time on `main` (shared with Visiting
Hours: explicit paths only); Task 1 in parallel from the start; Tasks 14 and 15 in parallel after 13, then 16; Tasks
18-21 as four parallel agents; Tasks 17, 22, 23 and 24 are the main session's (Unity runs only there). Work can stop at
any task boundary: phases 1 and 2 leave tested systems and a gated engine hook even if no Unity work follows. Before
starting, the main session checks the weekly budget (`node ~/.claude/usage-budget/report.mjs`) and stops at the next
clean task boundary when it nears the creator's threshold.
