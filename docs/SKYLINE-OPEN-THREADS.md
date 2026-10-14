# Skyline Repo: open threads (check this before switching topics; never drop a thread silently)

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
| A per-assembly compile check | Promised after the second missing-reference failure | Make `core-tests/SkylineUnity` compile each asmdef with only its references |
| Perf: the food-court fight stutters (ink part FIXED in perf10: 1% low 16.3 ms, worst fight 20.9 ms) | Re-measured in perf9 with the fixed pilot (demo completed): average 6.6 ms but 1% low 30.4 ms, worst fight 44 ms; the spikes are in the food-court fight with 15 bots, mostly waiting on the GPU (25 ms) with some CPU spikes (19-24 ms); perf8 was the same, so not tonight's work | The remaining spikes are CPU (no-ink worst fight 15.3 ms): profile them (the new HUD elements, bots); depth creases gave nothing (off) |
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
