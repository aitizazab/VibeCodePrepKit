# Skyline Repo: open threads (check this before switching topics; never drop a thread silently)

Started 2026-10-14 after the creator asked: "can we stop randomly dropping things we were developing?" Every thread we
start goes here with its next step; a thread leaves this list only when it's done (with the commit) or the creator drops it.

## Dropped without a reason (found 2026-10-14 evening): resume these
| Thread | Where it stopped | Next step |
|---|---|---|
| Music: the rest of the floor song | Mood plan and references agreed (`SKYLINE-MUSIC.md`); no layer after the pre-drop was started | The drop (next layer), then main A's base |
| Music: the riser's instrument | Brass / accordion / reed, pairs, with and without backing (`art/review/2026-10-14/skyline-music-step6-2.html`); never picked, never followed up | Creator picks |
| Bot redesigns from playtest 1 (`SKYLINE-PLAYTEST-1.md`) | Held all day "until a Unity pass verifies"; passes ran, never resumed | All bots larger; floor-care: face on its top, the canister the only protrusion, wind-up then molly acid, 15 s recharge unless the canister is shot; drone: much larger rotors as the weak spot, faceplate part of the face; spotlight: a larger humanoid with the spotlight as its head, charging an electric railgun before locking on |
| Damage states for every bot (`SKYLINE-BOTS.md`) | Held | Animated threshold crossings |
| The Breacher redesign | Round 2's club subwoofer (B, take 1) leads; "redo it inside the chosen style" never started | Concepts in the comic-ink style, then the model |
| The corridor-camper pincer (D22) | Decided; not built; the pinned sim test `ACamperHiddenInTheWestCorridors...` waits to flip | Build in the director (X0) or as its own task |
| The shop-S4 corner drones can't see | Noted from the sim; never checked | Check in the camper survey (DM2) |
| A per-assembly compile check | Promised after the second missing-reference failure | Make `core-tests/SkylineUnity` compile each asmdef with only its references |
| Perf: the food-court fight stutters | Re-measured in perf9 with the fixed pilot (demo completed): average 6.6 ms but 1% low 30.4 ms, worst fight 44 ms; the spikes are in the food-court fight with 15 bots, mostly waiting on the GPU (25 ms) with some CPU spikes (19-24 ms); perf8 was the same, so not tonight's work | A/B with `-skyline-no-ink` (the ink pass's cost), then profile the CPU spikes (the new HUD elements, bots) |
| The art-style verdict (gate G0) | The creator played the ink look; never asked for the verdict | Ask; dressing DR1-DR5 waits on it |

## Waiting on the creator
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
- The pre-drop is the original PopularMMOs recording: one swap to our instruments before anything goes public.
- The overnight draft lines (`SKYLINE-DRAFT-LINES-OVERNIGHT.md`): the creator's to rewrite.
