# Skyline Repo M1, round 2: judge B (level design, movement challenge, physical integrity, the demo as a capture)

Pass: `out/skyline/20261014-0520/`. My extra frames are in `out/skyline/20261014-0520/judge-B/`.
`arena-sheet-01..06.jpg` show the 170 s arena recording at 1 frame a second (5 across, 6 down; sheet 1 is 0-29 s,
sheet 2 is 30-59 s, and so on). The other files are single frames or 10 fps strips named by their time.

Timeline, from the frames and log order:

| Time | What happens |
|---|---|
| 0-4 s | Practice bot. A spot is made and shot. |
| 5-9 s | The kiosk boost. |
| 10-75 s | The food court fight. |
| 76-105 s | The facade fight, at the middle stall backs. |
| 106-109 s | The walk to the skylight marker. |
| 109-120 s | The skylight fight. |
| 120-123 s | The slide under the lift shutter. |
| 123-170 s | Pressed against the lift's flat mint panel. |

## Round-1 defects

| # | Round 1 | Now | Evidence |
|---|---|---|---|
| B1 | The demo stares at walls and fights from one booth | **Partly fixed** | Point-blank wall frames before the lift fell from about 36/120 to about 12/123: sheet 1 at 12-13, 18, 24 s; sheet 2 at 35-36 s; sheet 4 at 101-104 s. The peeks now look out past the cover's edge, for example at 85-89 s and 102 s. **But** the food court still takes 65 s around the booth (10-75 s). For about 10 of those seconds the view points backwards, through the doorway at the warm-up's espresso kiosk, 180 degrees away from the fight (32-33, 37-38, 41-42, 51-52, 57-58 s; `a-41.0.jpg`, `a-52.0.jpg`). |
| B2 | One cover piece wins every fight | **Fixed** | The facade is fought at the middle stall backs (76-104 s) and the skylight under the mezzanine (109-120 s). Neither is the booth. |
| B3 | Three fights in one room with no route | **Mostly fixed** | Anchors and markers progress north, CONCIERGE says "proceed to the marked area", and the SKY TERRACE banners in both halls point at the far goal (`still-...-01`, `-06`, `-07`). What's left: the far goal's payoff (new defect N1), and the de-escalating order (N4). |
| B4 | The cache pays +0 | **Fixed** | `scrap-cache plating +0 recovered +50`. A readout of the +50 is a presentation matter. |
| B5 | Teaching lines late, out of place, or wrong | **Partly fixed** | Both wrong lines are rewritten: "Shoot again mid-air to climb higher" matches the creator's boost rule, and Nan's line now points at a practice counter that double jump plus mantle can reach. **But** in this run: Dot's Writ instruction never plays (only the result line, at 1-3 s); `dot-breacher-boost dropped`; Nan's counter line never fires, because the demo never goes near the counter; `concierge-floor-care dropped rearmed` twice, so the floor-care notice is never heard all run. |
| B6 | The Writ lesson fails on camera | **Fixed** | `writ-spot-shot` is logged, with Dot's "That glowing crack?" at 1-3 s. |
| B7 | Nothing in the hall asks for movement | **Partly fixed** | Finishing now needs a slide (the shutter, 121.6-123 s, `slide-121.6.jpg`). The warm-up has a double-jump-and-mantle counter, and the tall cover is runnable. Still, nothing requires or rewards a wall-run, no route chains moves, and nothing makes skilled play faster. The demo shows the boost and the slide and nothing else: no counter climb, no wall-run, no hop over acid (it stands in acid at 116-119 s). |
| B8 | The mezzanine and cover tops are reachable | **Fixed** for the mezzanine | The 0.55 m upstand puts its top at 5.55 m, above a boosted mantle (about 5.2 m). The cover tops are still reachable, but they're runnable and inside the fight sim's cover, which is acceptable. |
| B9 | Bots pass through each other | **Fixed** for live units | At 116-118 s the units stay apart (`a-118.0.jpg` in `lift-118-124.jpg`). Live units still drive through fresh wrecks (`a-116.5.jpg`), because wrecks have no colliders by design. That's acceptable, but it reads as overlap. |
| B10 | Visible solids with no collider | **Mostly fixed** | The upstand and plinths are solid. The visible glass above the 0.55 m upstand, up to the handrail at about 1.05 m, still has no collider (`still-...-06-mezzanine.png`), so shots pass through the top half of a pane that looks solid. |
| B11 | Booth and stall walls not runnable | **Fixed** (by the report) | Not shown in the recording: the demo never wall-runs. |
| B12 | Fast motion unreadable | **Not fixed** | The bitrate is 6.6 Mbps. Frames at 6-7 s and 12-13 s are still blocks (sheet 1), and the boost is again the worst part. |

## New defects, most severe first

**N1. The far goal is an untextured box with a flat mint panel, and the recording spends 47 s pressed against it.**
- After the slide (123 s), the screen is one flat mint colour plus the gun and HUD until the end (`arena-sheet-05/06`,
  `slide-121.6.jpg`, `lift-118-124.jpg`). That's 28% of the recording.
- The lobby is a plain grey box with a blank pale-yellow rectangle for a sign (`still-SkylineArena-09-lift.png`).
  `editor-batch.json`'s lint lists the lift's walls, fascia and shutter as untextured, and the practice counter too.
- The camera ends flush with the lift doors' mint face. Either it has clipped into the panel or it stands nose against
  it. Either way, the run's payoff, the far goal, reads as a placeholder.
- This is a spec 10 blocker (an untextured placeholder in view), and it fills the screen.
- Owners: arena layout/look (the lift), and demo player/capture (the recording should stop at `SKYLINE_DEMO done`, or
  turn round to face the hall).

**N2. The demo skips the warm-up's new lessons.**
- It never goes to the practice counter (no step for it in the log). The counter is the one place that teaches the
  double jump and mantle, and its line never plays.
- The kiosk sequence still takes about 4 s, so Dot's boost line is dropped.
- A judge or the creator watching never sees the counter or hears two of the lessons.
- Owner: demo player/capture.

**N3. The demo still looks the wrong way in the food court.**
- In the booth, the "corner it will peek from" includes the U's open south side. The view rests on the warm-up hall
  behind the player for seconds at a time (`a-41.0.jpg`, `a-52.0.jpg`).
- A player in cover watches the side the threat comes from.
- Owner: demo player/capture.

**N4. The fights de-escalate.**
- Order and size: food court 14 bots and 65 s, then facade 10 bots and 29 s, then skylight 6 bots (with 2 floor-care
  units per spotlight now) and 11 s.
- The hardest, longest fight comes first, right after the warm-up. The last fight before the far goal is the shortest
  and easiest.
- Bar 10.4 asks for escalation after teaching.
- Owner: arena encounters. (The creator's "at least 12 drones" binds the food court's size, not its place in the
  order.)

**N5. The practice counter is untextured and sits out of the start's view.**
- The counter at (12.5, -45.5) is in the lint as untextured.
- It doesn't appear in `still-SkylineArena-01-warmup-start.png` or in the recording's opening.
- A player who hears Nan's line has to turn round to find it.
- Owner: arena layout/look.

**N6. (Flagged for judge C.)** The food court's skilled line fails its gate in Unity's own tests: 26/30 against a gate
of 27 (`editor-batch.json`). That's the "encounter without a clean line" blocker, if C confirms it.

## Scores (round 1 -> round 2; 8 is the bar)

| Category | r1 | r2 | Why |
|---|---|---|---|
| Level design | 4 | 6 | A real south-to-north route with distinct pockets, landmarks and SKY TERRACE banners pointing at the far goal, and a cache that pays. But the far goal is a grey box ending in a mint panel, the fights de-escalate, and the food court is still the booth's fight. |
| Movement challenge | 3 | 4 | A required slide, a double-jump counter and runnable cover, and the boost line is now correct. But nothing asks for a wall-run or chained moves, and skilled play still isn't faster. |
| Physical integrity | 6 | 7 | Separation, colliders, runnable cover and mezzanine reach are all fixed. New: untextured lift pieces, the camera flush with the lift panel for 47 s, the open upper glass, and live units driving through wrecks. |
| Demo as a capture | 4 | 5 | It walks the whole route and peeks outward, and wall-staring is down to about 10%. But it ends with 47 s of flat colour, faces backwards in the food court, skips the counter and two lines, and is still blocky in the boost. |

## Spec 10 blockers

1. **An untextured placeholder in view.** The service lift's box, shutter and mint panel fill the screen from 123-170 s
   (N1). The lint lists 5 untextured pieces.
2. **Flagged for C:** the food court's clean line is at 26/30 against a gate of 27 (N6).

## Not covered by the spec

- **When a recording ends.** A demo recording should end at the demo's last step (`SKYLINE_DEMO done`) or hold a
  composed shot. Nothing says so, so 47 s of a flat panel ships.
- **Demo coverage.** No rule says the demo must visit every teaching beat (the practice counter) or let each line
  finish.
- **Dropped teaching lines.** Nothing says a teaching beat that drops (the floor-care notice dropped twice) must replay.
  6.8 assumes each bot's notice is heard.
- **Fight order.** Encounter order and escalation within a floor have no rule beyond 10.4's one line.
- **Glass.** The spec doesn't decide whether glass above a collider (the balustrade's top half) may let shots through.
- **The far goal's interior.** What happens inside the lift (a cut, a fade, a ride) isn't designed. Without it, walking
  into the exit has no beat.
