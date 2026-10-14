# Skyline Repo M1, round 3 (last): judge B (level design, movement challenge, physical integrity, the demo as a capture)

Pass: `out/skyline/20261014-0625/`. My extra frames are in `out/skyline/20261014-0625/judge-B/`:
- `arena-sheet-01..06.jpg` show the 180 s arena recording at 1 frame a second (5 across, 6 down; sheet 1 is 0-29 s,
  sheet 2 is 30-59 s, and so on).
- `counter-10-20.jpg` (2 fps) and `wallrun-30-36.jpg` (5 fps) are strips of the counter climb and the wall-run.
- `stills-lift-warmup.jpg` puts the lift and warm-up stills side by side.

Timeline, from the frames and log order:

| Time | What happens |
|---|---|
| 0-9 s | Practice bot. Dot's lesson plays first, then the spot. |
| 10-19 s | Practice counter. |
| 20-30 s | Boost kiosk. |
| 30-36 s | Wall-run lockers. |
| 37-38 s | The doorway. |
| 38-83 s | The food court fight, at the booth. |
| 84-150 s | The facade fight, at the middle stall backs. |
| 151-167 s | The skylight fight: three beams, then floor-care units. |
| 168-176 s | Slide under the shutter to the lift doors. |
| 177-180 s | The vista back over the hall. |

The editor batch is green with an empty lint (`editor-batch.json`: 327 passed, 0 failed, lint count 0). The recording is
22.8 Mbit/s.

## Round-1 defects: final state

| # | Round 1 | Round 3 | Evidence |
|---|---|---|---|
| B1 | The demo stares at walls and fights from one spot | **Partly fixed, and worse in the warm-up** | See N1. About 35 of 180 frames are a surface at point-blank range (r1: 36/120; r2: 12/123 plus 47 s of mint). The food court is now fought looking out into the hall (64-83 s). The facade is fought from the same two stall backs for about 66 s (84-150 s, sheets 3-5). |
| B2 | One cover piece wins every fight | **Fixed** | Booth, then stall backs, then the mezzanine's underside. |
| B3 | One room with no route | **Fixed** | A south-to-north route, markers, SKY TERRACE banners, and a dressed lift with steel doors and a vista back over the hall (sheet 6, 175-179 s; `stills-lift-warmup.jpg`). |
| B4 | The cache pays nothing | **Fixed** | `+50` at the kiosk and `+30` at the lockers, banked as recovered scrap. |
| B5 | Teaching lines late or wrong | **Mostly fixed** | Dot's Writ lesson now plays before the shots. The counter and kiosk lessons play at their places, and the demo waits for them. The spotlight notice plays on first sight (151 s). What's left is in N2. |
| B6 | The Writ lesson fails | **Fixed** | `writ-spot-shot`, with Dot's reaction. |
| B7 | Nothing asks for movement | **Mostly fixed** | Every verb now has a gated ledge with a reward: the counter (double jump and mantle), the kiosk (the boost), the lockers (a wall-run, a kick and a double jump; `wallrun-30-36.jpg`, readable now), and the shutter (the slide). What's left is in N3. |
| B8 | Unplanned reachable spaces | **Fixed** | |
| B9 | Bots pass through each other | **Fixed** | Floor-care units stay apart in the skylight (sheet 6, 155-163 s). |
| B10 | Visible solids with no collider | **Mostly fixed** | Only the balustrade glass above the 0.55 m upstand (up to about 1.05 m) is still non-solid to shots. |
| B11 | Cover not runnable | **Fixed** | Per the fix report. The demo only wall-runs in the warm-up. |
| B12 | Fast motion unreadable | **Fixed** | 24 Mbit/s. The boost (24-30 s) and the wall-run (30-36 s) are clean. |
| r2 N1 | Untextured lift, 47 s of mint | **Fixed** | The lobby is plastered, the shutter is a grey-blue roll-up, and there are steel doors with SKY TERRACE. The fascia's lit panel is still a blank pale-yellow rectangle with no words (`still-SkylineArena-09-lift.png`). |
| r2 N2 | The demo skips lessons | **Fixed** | Counter, kiosk and lockers are all in the log and on screen. |
| r2 N3 | The demo looks backwards in the food court | **Fixed** | 64-83 s look out into the hall. |
| r2 N4 | The fights de-escalate | **Partly fixed** | The threat now escalates: drones, then drones plus mines, then three converging beams plus acid. Length doesn't: food court 45 s, facade 66 s, skylight 16 s in this run. The final fight is over almost as soon as it starts. The level report names the lever (where the creator's "at least 12 drones" goes) as the creator's call. |
| r2 N5 | Practice counter untextured, out of view | **Fixed** | It's marble now. |

## Remaining and new defects, most severe first

**N1. The demo faces a wall while it waits for a line.**
- 12-18 s: 7 s pressed against the practice counter's marble face while Nan speaks (`counter-10-20.jpg`).
- 25-28 s: 4 s against the kiosk's glass while Dot speaks (sheet 1). The fix report says it looks up at the top; on
  screen it doesn't.
- In the fights:
  - 50-51 s and 56-63 s against the booth;
  - 91, 107, 112-113, 117, 120, 127, 129, 134 and 147-148 s full-frame on a stall back;
  - 176 s against the lift doors.
- That's about 35 of 180 frames. Eleven of them come in the first 30 s, the first thing the creator watches.
- A person waiting for instructions steps back and looks at the thing being described.
- Owner: demo player/capture. This is the recording, not the game.

**N2. Lines a player hears in the first five minutes that are off.**
- Nan's opener ("Unit's live. Warm up here...") restarts about five times in the warm-up: `nan-warmup` is logged 5
  times and cut 4. Its subtitle reappears at about 9, 17, 21, 29 and 36 s.
- Dot's "That glowing crack? That's the spot" is said again in the food court at about 43-45 s, long after the lesson.
- The floor-care notice is dropped in the food court (`concierge-floor-care dropped rearmed`), where the player first
  meets and fights the units at 46-49 s. It finally plays in the skylight at about 160 s, two minutes late.
- Owner: presentation (teaching queue). It's in my brief through 6.8.

**N3. The main hall itself asks for no chained movement.**
- All four movement lessons are warm-up ledges.
- In the hall: no route needs a wall-run, no line chains moves, and nothing makes skilled play visibly faster. All three
  fights are clear-everything from a pocket of cover.
- The level report says so too ("the main hall itself still has no wall-run ask").
- Bar 10.5 ("routes demand chained moves with timing; skilled play is visibly faster") is met only in the warm-up.
- Owner: arena layout.

**N4. The facade is fought from one spot.**
- The demo spends about 66 s at the two middle stall backs. The fight gives no reason to leave them: the gondolas'
  mines guard the walls, and the drones come to the pocket.
- This is legal under the route test (no piece is the favourite of two fights), but it's the "barely moves" look again,
  in the middle of the recording.
- Owner: arena encounters, partly the demo player.

**N5. The skylight climax is the shortest fight (16 s).**
- With three spotlights it's the hardest mix, but it's over before it builds.
- The run peaks in the facade's middle and then deflates. See r2 N4.
- Owner: arena encounters (the creator's call on where the drone count goes).

**N6. Small integrity and readability points.**
- The lift's fascia sign is blank (a lit yellow panel).
- The balustrade glass above 0.55 m lets shots through.
- Live floor-care units drive through fresh wrecks, which have no colliders by design.
- The wall-run lockers read as a plain teal box rather than lockers.
- Owner: arena look and layout.

## Scores (round 1 -> round 2 -> round 3; 8 is the bar)

| Category | r1 | r2 | r3 | Why |
|---|---|---|---|---|
| Level design | 4 | 6 | 7 | A full route, landmarks, a dressed far goal with a vista, three optional ledges that each pay, and an escalating threat mix. Short of 8: fight length deflates to a 16 s finale, the facade is a 66 s pocket, and the lift sign is blank. |
| Movement challenge | 3 | 4 | 6 | Every base verb is taught at a gated, rewarded ledge, and the slide is required to finish. Short of 8: the main hall never asks for a chain of moves, and skill isn't faster there. |
| Physical integrity | 6 | 7 | 8 | No clipping, stuck spots, floating or phasing in the frames I checked. The lint is empty, separation works, and the colliders and runnable surfaces are fixed. Only small points remain (N6). |
| Demo as a capture | 4 | 5 | 6 | It shows every verb, every bot kind watched, the Breacher in use and a real ending, at clean bitrate. Short of 8: about 19% of frames are point-blank surfaces, most visibly 11 s against the counter and kiosk in the first half-minute. |

## Spec 10 blockers

None in my categories. The untextured lift is fixed, the lint is empty, and the food court is at 29/30 per the
addendum. Perf (1% low 18.0 ms) is judge E's blocker, not mine.

## For the creator: what a player notices in the first five minutes (my categories)

1. Nan's opening line keeps restarting through the warm-up, and the floor-care warning never plays in the first fight
   where you meet the units (N2).
2. The warm-up has a good set of movement ledges. The main hall then asks for none of it: the fights are won by picking
   a cover pocket and staying there (N3, N4).
3. The last fight, the three-spotlight skylight, is over in seconds, so the floor ends on a whimper before the lift
   (N5).
4. (The recording, not the game:) the demo stares at the counter's and kiosk's walls for about 11 s in its first half
   minute (N1).

## Not covered by the spec

- **How a demo waits.** Facing the subject from a readable distance during a line has no rule, so it presses against
  walls.
- **Main-hall movement.** No rule says the main hall (not just a warm-up) must contain a movement ask. Bar 10.5 doesn't
  say where.
- **Fight-length escalation.** There's no rule on fight length or pacing within a floor. The creator's "at least 12
  drones" in the food court fixes the biggest fight first.
- **Repeated lines.** A cut line that restarts from the top ("Unit's live...") has no rule on how often it may repeat.
- **Glass.** The spec still doesn't say whether glass above a collider may pass shots.
