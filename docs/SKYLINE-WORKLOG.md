# Skyline Repo: work log

Newest first. Every agent result and every Unity pass gets an entry (what was done, numbers, paths, decisions needed).

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
- **Open:** the creator reviews the spec; then the implementation plan.
