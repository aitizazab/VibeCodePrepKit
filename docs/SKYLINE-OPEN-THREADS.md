# Skyline Repo: open threads (check this before switching topics; never drop a thread silently)

## THE PLAN (top of the list; the creator, 2026-10-15: "name at the top everything that needs to be done next for the full plan of the vertical slice, and then beyond that")

**The vertical slice** (`SKYLINE-DESIGN.md`): the Galleria, built to the full quality bar on data-driven systems: its
floors, the atrium climb, the boss LOSS PREVENTION, and the sky-bridge to the offices tower as the closing shot.

**Paused 2026-10-15 night (state and next steps: `SKYLINE-PLAYTEST-1.md`, "Tonight first"):** the riser into the pre-drop
and the PopularMMOs intro (next: the a cappella rebuild, `audio/skyline-music/research/ACAPELLA-METHOD.md`); the creator's
notes on the layered speed page (`SKYLINE-MUSIC.md`, last section: Sanctuary less intense at every speed, above all with
Rapture; Judgement's still layer more dangerous, less muddled, more ominous); the perf A/B windowed runs; the floor-care redesign (RESUME.md
in the `VibeJamKit-floorcare` worktree); the spotlight humanoid (not started).

**Stage 1: floor 1 (the Food Court repossession) playable end to end**
1. **Music, now:** the riser into the pre-drop redone from scratch (the creator's sources: the PopularMMOs theme, the Subway
   Surfers six-step riser, the creator's two imitation recordings): a "perfect" transition and a step-by-step one. Then the
   named variants of the approved theme **"Heaven"** (the normal layer, v3): **"Judgement"** (danger; the Empress of Light's
   first 17 s), **"Sanctuary"** (quiet; Stonewood and Plankerton), and the speed stems **"Updraft"** (drive, fast) and
   **"Rapture"** (peak, flying). Then **M2** (the music director in the game) and the lift-ride opening.
2. **X0, the floor director:** wires the waves, repossession, spawn doors, gondola, HUD, Dot's beats, the wreck sequence
   and restart into one playable floor; **L2** the lobby runtime (desk, practice drone, lift ride). Then **X1**: floor 1
   replaces the M1 arena as the game.
3. **Bots:** the remaining playtest-1 redesigns (floor-care: face on its top, the canister its only protrusion, wind-up
   then molly acid, 15 s recharge unless the canister is shot; spotlight: a larger humanoid with a charging railgun);
   verify the new sizes and damage states in stills; **enemy cries** to replace the removed hurt sounds.
4. **Balance with the creator in play:** the drone pressure gap (lagged aim), wave tables, Breacher ammo, the Relay clock.
5. **Content and feel:** dressing DR1-DR5 (the look passed), the Writ's reload sounds, the Breacher redesign (club
   subwoofer in the comic-ink style), line weight checked in play.
6. **Quality:** the remaining CPU spikes (perf), P1 prewarm, DM1-DM3 (the demo pilot and the camper on floor 1), J1
   captures and judges.

**Stage 2: the rest of the Galleria (the slice)**
7. Floor generation (CONCIERGE's remodel: rooms as chunks, a first set of the 16 normal and 8 special rooms, two exits
   with rewards); objectives beyond repossession (the Relay as an end-of-floor boss objective).
8. The Ledger (CONCIERGE learns your habits and counters them).
9. The hires (the Galleria's recruits, slots, bonds).
10. Rewards and progress: repossessed in-run rewards, commission (permanent), difficulty.
11. New enemies (the Valet, planned), LOSS PREVENTION (the boss mech), the atrium climbing set-piece, the sky-bridge.
12. Story: a subplot per floor, the creator's dialogue; movement upgrades in the slice (anchor hook, jump upgrade).

**Beyond the slice**
- Districts 2-6 as content on the same systems (the offices, the R&D labs, the hydroponic gardens, the casino, the
  penthouses), getting more serious as you climb; each district's music.
- Baja, the secret boss (data recorded from the start); the glider and the true grapple; more weapons.
- Before anything is public: swap the borrowed recordings (the PopularMMOs pre-drop and drop, any Subway Surfers material)
  for our own instruments, and check the event's or store's rules.

Started 2026-10-14 after the creator asked: "can we stop randomly dropping things we were developing?" Every thread we
start goes here with its next step; a thread leaves this list only when it's done (with the commit) or the creator drops it.

## Dropped without a reason (found 2026-10-14 evening): resume these
| Thread | Where it stopped | Next step |
|---|---|---|
| Music: the rest of the floor song | v3 "heaven" (remade from the settle, at the creator's call) + the riser hand-over pass 2, page `art/review/2026-10-15/skyline-music-heaven-v3.html`; next steps at the end of `SKYLINE-MUSIC.md` | Creator's verdict on v3 and the hand-over pick; then danger, quiet, speed layers against v3's stems; then M2 in the game |
| Music: the riser's instrument | Brass / accordion / reed, pairs, with and without backing (`art/review/2026-10-14/skyline-music-step6-2.html`); never picked, never followed up | Creator picks |
| Bot redesigns from playtest 1 (`SKYLINE-PLAYTEST-1.md`) | Held all day; 2026-10-15 the SIZES and weak points went to the drone agent (priority: "impossible to land a headshot on a drone"); the shape redesigns below still wait | All bots larger; floor-care: face on its top, the canister the only protrusion, wind-up then molly acid, 15 s recharge unless the canister is shot; drone: much larger rotors as the weak spot, faceplate part of the face; spotlight: a larger humanoid with the spotlight as its head, charging an electric railgun before locking on |
| Damage states for every bot (`SKYLINE-BOTS.md`) | Assigned 2026-10-15 to the drone agent (after aim, with the sizes) | Animated threshold crossings; Unity stills of each state |
| The Breacher redesign | Round 2's club subwoofer (B, take 1) leads; "redo it inside the chosen style" never started | Concepts in the comic-ink style, then the model |
| The corridor-camper pincer (D22) | Decided; not built; the pinned sim test `ACamperHiddenInTheWestCorridors...` waits to flip | Build in the director (X0) or as its own task |
| The shop-S4 corner drones can't see | Noted from the sim; never checked | Check in the camper survey (DM2) |
| A per-assembly compile check | Built (branch skyline-perf-ab): `core-tests/SkylineUnity/check-asmdefs.ps1`, run by `tools/skyline/test.ps1` | Done once merged |
| Perf: the food-court fight stutters (ink part fixed in perf10: 16.3 ms; REGRESSED in perf11 to 22.3 ms, worst fight 28.2 ms, GPU 15 ms wait + CPU 12.8 ms) | Re-measured in perf9 with the fixed pilot (demo completed): average 6.6 ms but 1% low 30.4 ms, worst fight 44 ms; the spikes are in the food-court fight with 15 bots, mostly waiting on the GPU (25 ms) with some CPU spikes (19-24 ms); perf8 was the same, so not tonight's work | A/B perf11's regression: line weight off, damage-state effects off, the old bot sizes; then the CPU spikes (HUD elements, bots) |
| The art style (gate G0) | PASSED 2026-10-15. Of the five stylization options the creator wants only **line weight** ("I can't really tell the change wrought on by many of the variations"): on by default from commit 2166f74f | Dressing DR1-DR5 is unblocked |

## New from the Writ rework (merged 2026-10-15, not yet seen in Unity)
| Thread | Where it stopped | Next step |
|---|---|---|
| Writ reload sounds | The reload is silent except the seat click (the old drum/gavel recipes no longer fit) | Options for the quill leaving the ink, scratching on paper, and dipping back (identity first); creator picks |

## Waiting on the creator
- **Balance after the lagged drone aim:** drones now miss a strafing Unit by design, so the skilled line takes too little
  pressure in the food court and the façade (median lowest plating 76 vs the gate's 60; both tests parked with `[Ignore]`
  and their numbers). Balance in play together: more drones, other bots (the Valet), or a new gate.
- The music: the v3 heavenly theme, and which riser hand-over (`art/review/2026-10-15/skyline-music-heaven-v3.html`).
- The Relay: is the space big enough for an 18 m/s skill check (`SKYLINE-RELAY.md`)?
- The HUD: does "- RELOADING" under the ammo count go (the gun's name stays)?
- The Valet (`SKYLINE-BOTS.md`): name, the "ding" through walls, 25 damage, lunging at a wall-runner.

## In progress
- The Writ rework (agent, branch `skyline-writ`): no chair or gavel, bigger quill curling clear in ADS, reload writes on
  paper while the ink refills, hands still and the gun tilted up, the indigo gleam back, a clean ADS picture, weaker
  hip-fire aim assist.
- Drone aim (agent, branch `skyline-drone`): fires at where you were 0.1-0.2 s ago; bots stop seeing/shooting through
  rails and balustrades (D21).
- Unity pass perf9 passed (1074 EditMode tests, 0 failed; lint 0; build OK; sync 12/12); perf failed (above). Next build after the Writ and drone branches merge.

## Queued in order (the floor-1 plan, `SKYLINE-PLAN-FLOOR1.md`)
X0 the floor director, L2 the lobby runtime, M2 the music director, DM1-DM3 the pilot and camper, DR1-DR5 dressing (gate
G0), P1 perf, J1 captures, X1 the switch-over. The Valet (planned in `SKYLINE-BOTS.md`) after X0.

## Deliberately deferred (with a reason)
- Bot hurt sounds were removed on hits (2026-10-15: they overrode the gun's hit sound); **an enemy cry** replaces them later.
- The pre-drop is the original PopularMMOs recording: one swap to our instruments before anything goes public.
- The overnight draft lines (`SKYLINE-DRAFT-LINES-OVERNIGHT.md`): the creator's to rewrite.

## Housekeeping
- Disk ran low (4.2 GB free) on 2026-10-15: old pass folders' builds, recordings and stills deleted (16 GB; logs and
  results kept; only the latest pass keeps its build), old music renders removed, and XXL-McDonalds' old takes (842 MB,
  see its HANDOFF.md). Not deleted (the creator's call): XXL's voice-conversion toolchain (~7 GB: `.venv-applio`, the
  training-resume checkpoints, the separation models), needed only to resume or extend the paused voice work.
  **Keep doing:** after each pass, delete the previous pass folders' build/, *.mp4 and stills.
