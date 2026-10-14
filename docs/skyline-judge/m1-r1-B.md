# Skyline Repo M1, round 1: judge B (level design, movement challenge, physical integrity, the demo player)

Pass: `out/skyline/20261014-0326/`. My extra frames are in `out/skyline/20261014-0326/judge-B/`:
`arena-sheet-01..04.jpg` show the whole arena recording at 1 frame a second (5 across, 6 down; sheet 1 is 0-29 s,
sheet 2 is 30-59 s, and so on). The other files are single frames or 10-20 fps strips named by their time.

The log lines have no times, so I timed the demo steps from the frames. Spawn and practice bot: 0-5 s. Kiosk boost:
5-8 s. Doorway: 9 s. Food court: 10-55 s. "Security is re-deploying": 56 s. Skylight fight: 57-72 s. Back to the entry
lane: 72-75 s. Facade fight: 75-120 s. The build's `perf.json` `worst` list agrees: mantle at 9.3 s, food court
to-hide at 12.1 s.

## Defects, most severe first

1. **The demo player still spends about 30% of the recording staring at a wall from 1 m away, and most of the fights
   in one spot.** The whole food court fight (10-55 s) happens within about 5 m of the information booth: inside its U
   from 10-39 s, at the planter beside it from 40-44 s, and on its north face from 45-55 s. The whole facade fight
   (75-120 s) happens inside that same U. The player never goes near the facade, the gondolas or the side walls. At
   1 frame a second I count about 36 of the 120 frames where the screen is mostly concrete wall or the booth's posters
   at point-blank range. Examples: frames 015, 025 and 085, sheet 3 from 77-89 s, sheet 4 from 91-101 s and 117-119 s.
   The built game shows the same thing in `buildcheck/build-shot-20s.png`.
   - Cause: in cover, `LookWhileNoTarget` (`SkylineArenaDemo.cs` around line 870) aims at the nearest live bot, which is
     behind the wall it is hiding behind. A person in cover watches the corner they will peek from.
   - The turns are capped now, not instant (`HumanAim.maxRate` = 650 deg/s). But a duck still whips about 150 degrees
     in 0.25 s to face a wall (`judge-B/turn-104.5.jpg`). The aim during a peek is smooth (`judge-B/aim-40-42.jpg`).
   - The creator's complaint, "barely moves from his space", is still true. The "flicks" complaint is half-fixed: the
     shots no longer flick, but the cover turns do.
   - Owner: demo player/capture.

2. **One cover piece wins all three fights.** The information booth is three 4.5 m walls in a U, open to the south, at
   x -2.5..2.5, z -14..-9.6. It is 4 m from the entry anchor where every fight starts. Every spawn in all three fights
   is north of z -14 or off to the east or west (`GalleriaArena` in `Encounters.cs`), so the U's walls block every
   starting bearing. The demo is even told to go there ("the food court's to the information booth's U"). The result
   is that "split the 12 drones into small groups with cover" (the creator's call) turns into "camp the U". For roughly
   85 of the 105 fight seconds the recording shows the same booth.
   - Owner: arena layout/encounters.

3. **The three fights are one room.** "Food court", "skylight" and "facade" are three sets of spawns in the same
   50 x 40 m hall, with the same 14 cover pieces and the same anchor. After each fight CONCIERGE says "Guests are asked
   to return to the food court entrance" (sheet 2 at 56 s, sheet 3 at 72-75 s), and the next wave starts from the
   lit floor marker in the entry lane.
   - The bar (10.4) asks for clear landmarks, the next step and the far goal always visible, rests and vistas. Here
     there is no route, no far goal, no exit and no change of space. The run doubles back twice and ends mid-room.
   - The "facade" is the hall's side walls. The gondolas' rails only exist once the facade fight spawns them
     (`SkylineBot.BuildRailBar`), so for the first two fights the facade isn't readable as a place.
   - The hall is mirror-symmetric on both axes, so every bearing looks alike.
   - Owner: arena layout/encounters.

4. **The one optional harder line pays nothing.** The kiosk ledge really does need the boost. The numbers: jump 1.25 m
   + double jump 1.0 m + mantle reach 1.9 m = 4.15 m, which is short of the 4.6 m top, and the boost's 1 m reaches it.
   That part is good. But the scrap cache tops plating up to full (`SkylineScrapCache.cs`) and puts nothing into
   recovered scrap. Nothing in the warm-up does damage, so on a first run the reward is always zero: the log says
   `SKYLINE_ARENA scrap-cache plating +0`, in both the recording and the build's perf log. The crate (0.4 m on a 4.6 m
   top) also can't be seen from the floor (`still-SkylineArena-03-kiosk-ledge.png`), so only Dot's line says it's
   there. The bar asks for "optional harder lines with rewards", and 6.8 asks for "a small reward".
   - Owner: arena layout/encounters.

5. **The teaching lines arrive long after, and away from, what they teach.** The lines play one after another from a
   queue, and the lessons don't wait for them:
   - Nan's "Tap jump twice ..." plays during the boost (6-9 s).
   - Dot's "Practice bot, dead ahead! Tag its lens ..." plays at 10-13 s, after the player has left the warm-up and is
     in the food court (sheet 1).
   - Dot's "Breacher kicks both ways ... There's scrap on top of that kiosk" plays at 24-26 s in the middle of the food
     court fight (frame 025).
   - "Ha! One boost a jump" plays at 28-30 s.

   A real player who goes straight to the practice bot still hears Dot about 12 s late, because both of Nan's lines
   trigger on WarmupStart and play first. Two of the lines also teach the wrong thing:
   - **Dot's "One boost a jump, mind. Land or touch a wall to get it back"** describes `oneBoostPerAirtime`, which is
     off. The creator's call (2.2, section 14) is that the boost is limited only by fire rate, so the game teaches a
     rule it doesn't have.
   - **Nan's "Every kiosk in here is built for practice"**, right after teaching double jump and mantle: the warm-up's
     only kiosk can't be reached with double jump and mantle. A player who follows Nan fails.

   Owner: arena layout/encounters (teaching beats, `TeachingBeats.M1`).

6. **The Writ lesson fails on camera.** After three shots at the practice bot's weak point the log says
   `SKYLINE_DEMO writ-no-spot`, in both the editor capture and the build (`stage-d-perf.log` line 62). The crops at
   1.6-4.2 s (`judge-B/practice-bot-quad.jpg`) show a hit marker but no glowing seam. The 6.8 beat "a practice bot that
   visibly grows spots" isn't shown, and Dot's "That glowy bit? That's the spot" only arrives at 65 s, in the skylight
   fight. Owner: Writ / arena encounters (practice bot).

7. **Nothing in the main hall asks for movement.**
   - There are no slide floors, no gap, no climb and no height story (6.5).
   - The mezzanine has no stairs, so it's a backdrop.
   - The only wall-run line is the side walls, and it leads into the drone groups (x ±17-21) and the gondolas' mines,
     not between cover: the nearest cover to either side wall is the food stalls at x ±13. CONCIERGE then tells the
     player to "keep their feet on the floor".
   - Fights are clear-everything, so there's nothing for skilled movement to be faster at (bar 10.5: "skilled play is
     visibly faster").
   - The demo uses jump, double jump, boost and mantle once each in 3 s at the kiosk. It doesn't visibly slide or
     wall-run anywhere, and it stands in a carpet of acid in the skylight fight (63-68 s) without hopping it.

   Owner: arena layout/encounters.

8. **Spaces nobody planned are reachable.** One boost and a mantle reach:
   - the mezzanine's open edge (feet 3.25 m + 1.9 m reach, against a 5.0 m deck),
   - the tops of the food stalls (4.8 m) and the booth's walls (4.5 m);
   - the 2.5 m hall kiosks need only a jump and a mantle.

   The mezzanine's back (z 16-20) is hidden from every drone by the deck and out of the floor-care units' reach. It
   sits right beside the spotlights' perches. FightSim models only the floor, with the deck as cover, so the measured
   "clean line" and dodge balance don't cover a player up there. Nothing signposts it or rewards it, and nothing stops
   it. Owner: arena layout/encounters.

9. **Bots pass through each other.** In the skylight fight, floor-care units converge and merge into one pile, with
   three bodies and displays inside each other (`judge-B/arena-64.5.jpg`, also 61.5 and 63.5 s and sheet 3 at 62-68 s).
   There's no separation between bots. In the gallery the acid line passes through the mannequin
   (`frames/SkylineGallery-018.1s.jpg`). Owner: floor-care unit.

10. **Visible solids with no collider.**
    - The mezzanine's glass balustrade (builder line 141, "no collider, so it changes no sightline"): a player on the
      deck can walk off through visible glass, and shots pass through it.
    - The spotlights' plinths (line 147).
    - The kiosk's counter glass skins, which are harmless because they sit flush on the kiosk's collider.

    Bar 10.6 says every visible solid has a collider. Owner: arena layout.

11. **Walls you'd expect to run on aren't runnable.** The cover boxes are built with `runnable: false`
    (`SkylineArenaBuilder.cs`, the cover loop). That includes the booth's three 4 m concrete walls and the 4.8 m food
    stall backs, which look exactly like the hall's walls. A player who tries a wall-run off the booth, the most-visited
    wall in the arena, drops. Bar 10.6: "every wall the player would expect to run on is wall-runnable". (The kiosk is
    left unrunnable on purpose, for the boost lesson, and that's fine.) Owner: arena layout.

12. **The recording can't be read during fast moves.** Severe macroblocking during the only movement set-piece: frames
    6-11 s are unreadable mush (`judge-B/arena-6.5.jpg`, `arena-7.3.jpg`, `arena-8.2.jpg`; sheet 1, second row). The
    boost, the one movement beat a judge or the creator needs to see, can't be judged from the video. Owner: demo
    player/capture (encoder settings).

13. **(Outside my categories, flagged.)**
    - `buildcheck/build-shot-08s.png`: the Writ's tracer and the practice bot's speech bubble render solid magenta in
      the build, which is Unity's error shader. In the editor recording they are orange and white. This is a likely
      blocker ("untextured placeholder in view") for the look and presentation judges to confirm.
    - The same build shots cut off the subtitle at the bottom edge (the "Dot" label at y 860 of 864). This is probably
      the screenshot cropping the window's title bar, but it should be checked as a possible clipped-text blocker.
    - `still-SkylineArena-06-mezzanine.png` (taken after the demo) shows the north drone group's four wrecks in a row at
      their spawn points (x -7, -4, 4, 7, z ≈ 9). Those drones died where they spawned, which supports "the drones don't
      even seem to move".
    - `editor-batch.json` is `ok: false` (`SweepTheFightTuning` timed out).

## Scores (8 is the bar)

| Category | Score | Why |
|---|---|---|
| Level design | 4 | Strong landmarks (the espresso kiosk, the GALLERIA portal) and a readable hall. But all three fights are one symmetric room, one booth wins them all, there's no route or far goal, and the one optional line has a zero reward. |
| Movement challenge | 3 | One real movement test (the kiosk boost, correctly gated). Nothing in the hall asks for chained moves; wall-run and slide have no use and aren't taught; a teaching line contradicts the creator's boost rule. |
| Physical integrity | 6 | No phasing, falling through, stuck spots or floating in the frames I checked. But bots pass through each other, the glass balustrade and the plinths have no colliders, the booth and stall walls aren't runnable, and unplanned perches are reachable. |
| Demo player as a capture | 4 | The aim during peeks is now smooth and capped. But about 30% of frames show a wall, it fights from one booth, whips 150 degrees to face walls, skips past every lesson in 3 s, and never shows the facade, the mezzanine, a slide or a wall-run. |

## Spec 10 blockers I see

- In my categories: none confirmed. No phasing, no soft-lock. Whether each encounter has a clean line is judge C's
  call, but I note that the line the layout offers is "camp the booth".
- Flagged for other judges: 1 likely (the magenta error shader in the build, defect 13) and 1 to check (the subtitle cut
  off in the build shots).

## Not covered by the spec (to raise, not to patch)

- **Demo or capture standards.** Nothing says what a judging recording must show: each movement verb at least once, each
  fight's space, a limit on time spent facing a wall, and an encoder quality that keeps fast motion readable.
- **Teaching-line timing.** No rule says a lesson waits for its line, or how late a line may play before it's dropped.
  Lines are queued forever now.
- **Playable heights.** The spec doesn't say whether cover tops and the mezzanine are meant to be standable. If they
  are, FightSim and the clean-line checks must model them. If not, they need blocking that reads as blocking.
- **Glass.** Bar 10.6 wants a collider on every visible solid, but the balustrade is left out on purpose for
  sightlines. The spec should decide whether glass stops players, shots and sight.
- **Bot-to-bot separation.** No bot's identity sheet says bots keep apart, so floor-care units stacking is unowned.
- **Rewards at full plating.** The banking rule covers wrecks, not caches or other top-ups. A heal-type reward taken at
  full health is worthless.
- **The M1 arena's ending.** Exits and rewards are M2 (6.8), so the M1 arena has no far goal, which bar 10.4 asks for.
  Decide whether M1 is judged on that line.
