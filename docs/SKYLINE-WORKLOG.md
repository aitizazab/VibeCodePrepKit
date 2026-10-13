# Skyline Repo: work log

Newest first. Every agent result and every Unity pass gets an entry (what was done, numbers, paths, decisions needed).

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
