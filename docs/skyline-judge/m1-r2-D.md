# Skyline Repo M1, round 2: judge D (enemy readability, motion, encounter fairness, consequence)

Pass: `out/skyline/20261014-0520/`. My frames and contact sheets are in `out/skyline/20261014-0520/judge-D/`:
`sheet-stills`, `sheet-galframes`, `sheet-idle`, `sheet-dc` and `sheet-dzoom` (the drone's face), `sheet-arena`,
`sheet-a1` and `sheet-a2` (arena 20-126 s, about every 3.5 s), `sheet-sky` (108-122 s), `sheet-c` (crops), and
`gallery-events.json` (the ears). Gallery recording t = game t - 0.831.

I checked sound sync with the ears. Every telegraph and attack in the `SKYLINE_GALLERY` log has an audio onset within
0.03 s:

| Bot | Logged (recording time) | Onsets found |
|---|---|---|
| Drone | telegraph 4.683, attack 5.033 | 4.68, 5.00 |
| Floor-care unit | telegraph 18.600, attack 19.200 | 18.56, 19.21 |
| Spotlight | lock 33.016, shot 33.266 | 33.00, 33.25 |
| Gondola | squeegee 50.866 | 50.86 |

## Round-1 defects

| # | Round-1 defect | Now | Evidence |
|---|---|---|---|
| 1 | Every wreck was the same grey lozenge; the error face was never seen | **Fixed** | Each wreck keeps its own model and falls its own way. The drone lies on its side by the pillar (`frames/SkylineGallery-011.1s.jpg`). The floor-care unit slumps with acid round it (`024.4s`). The spotlight's head sags, dark (`judge-D/g-41.5.jpg`, `g-42.8.jpg`). The gondola hangs from one stirrup (`059.8s`, `still-SkylineGallery-gondola.png`). `still-SkylineGallery-wide.png` shows all four wrecks side by side, each still recognisable. In the arena, drone wrecks read as drones (`judge-D/a-93.5.jpg`, `a-97.jpg`), and so do the floor-care wrecks (`a-118.jpg`) and the gondola's (`a-107.5.jpg`). |
| 2 | Floor-care units piled into each other | **Fixed** | In the skylight fight the units stay apart, each face readable (`judge-D/s-115.5.jpg`, `s-117.jpg`, `s-119.jpg`, `a-118.jpg`). They come close side by side, but never overlap. |
| 3 | The arena recording almost never shows a bot | **Partly fixed** | The floor-care units are now shown up close with readable angry faces and barks (`frames/SkylineArena-115.3s.jpg`, `judge-D/s-119.jpg`), and the spotlight's red beam reaches the camera (`s-110.5.jpg`, `s-113.jpg`). But in 16 food-court frames (20-72.5 s, `sheet-a1.jpg`) I find no live drone large enough to read: 2 of the 16 are walls or cover, the rest are empty corridors, and a lone "Halt! Mall security!" card floats over a dot (`judge-D/c-65.jpg`). In the facade fight, the drones show only as their barks and wrecks (`c-83.jpg`, `a-93.5.jpg`). The live gondola is never on screen (79-107 s); only its wreck is (`a-107.5.jpg`). The spotlight's body is never on screen; only its beam is. The camera faces a wall at 18.2 s, 23.5 s and 104 s (`a-104.jpg`: a full-frame wooden wall). |
| 4 | The drone's face was hidden under its cap | **Not fixed (new cause)** | The brim is now clear, but the egg's white front pushes **through** the face screen. A white disc covers the middle of the screen, leaving only the outer corners of the eyes: blue at idle, red when angry (`judge-D/sheet-dzoom.jpg`, 1.2 s and 5.0 s; `sheet-dc.jpg` across 1.2-6.6 s). The smirk, the "O" and the crosshair eyes still can't be read. |
| 5 | The bolt, the acid and the debris were primitives | **Fixed** | The bolt is a white-and-gold streak (`judge-D/dc-5.1.jpg`). The acid is a curved stream with a bright core, and the puddles are irregular blobs (`frames/SkylineGallery-019.9s.jpg`, `024.4s`). Debris is shell shards in the bots' own colours (`g-41.5.jpg`). |
| 6 | Idle behaviours were invisible | **Mostly fixed** | The spotlight's scan now visibly sweeps through about ±35° (`judge-D/g-27.2`, `g-28.4`, `g-29.6`, `g-30.8`). The gondola rides about 1 m along its rail through 44-47.6 s (`sheet-idle.jpg`). The drone's hover drifts and banks (`sheet-dc.jpg`). The floor-care unit still mostly turns on the spot during its 4.5 s idle (`g-13.5` to `g-16.5`: it shifts about 10 px and turns), so its "short straight runs" don't read. |
| 7 | The scrap cache paid +0 | **Fixed in the log** | `SKYLINE_ARENA scrap-cache plating +0 recovered +50`. I couldn't see whether the banked 50 shows anywhere on screen. |
| 8 | Damage taken barely registered | **Fixed** | A red edge vignette flares when the player is hit (`judge-D/s-119.jpg`), and the plating and chassis bars are labelled. |
| 9 | No wear on a live bot | **Partly fixed** | Per the fix report, the body now dulls as its chassis drops. I can't tell it apart in the frames. There is no smoke at low chassis (the report says this is deliberate). |
| 10 | Skylight spotlights in one forward arc; walls left open twice running | **Fixed by the route** | The fight order is now food court, facade, skylight, so the open layer changes each fight (walls, then floor, then walls). The level report says the bearing test passes at 110° or more. |
| 11 | Gallery capture faults | **Mostly fixed** | The stills now show live bots, the default still shows the drone, and the wide shot shows all four wrecks. The gallery's audio events now run to 59.2 s. The "wall-run" dummy still floats upright against the glass (`frames/SkylineGallery-050.9s.jpg`). |
| 12 | Bark cards skewed and clipped | **Mostly fixed** | Cards are flat, bold and framed (`judge-D/s-117.jpg`, `c-65.jpg`). Two drone barks still overlap each other ("Halt! Mall security!" with a second "...ecurity!" behind it, `c-83.jpg`). |

## New or remaining defects, most severe first

1. **The drone's face is blocked by its own body** (round-1 #4, new cause). The front of the white egg pushes through the
   face screen, so the screen shows only its outer corners (`judge-D/sheet-dzoom.jpg`). This is the most common bot in M1
   (20 of them across two fights), and its expressions (smug, outraged, aiming) can't be read even up close in the
   gallery. Owner: the mall-cop drone (the model: the screen needs to sit in front of the egg's surface, or the egg
   needs a recess).
2. **The arena recording still doesn't show the drones or the gondola doing their thing** (round-1 #3). The food court
   (about 20-75 s) has 12 drones, and none is readable in 16 sampled frames. The live gondola appears nowhere. Both
   spotlights die off-screen within about 10 s of the skylight fight starting ("Lights... out..." at 114.5 s). The
   creator's complaint that the drones don't move will survive this recording: the drones exist on screen as bark cards
   and wrecks. Owner: demo player/capture (point "the eyes" at a live drone during each telegraph, and at the gondola
   during the facade fight).
3. **The floor-care unit's safety notice never plays.** The log has `SKYLINE_LINE concierge-floor-care dropped rearmed`
   twice (lines 1386 and 2809) and never the line itself. Spec 6.8 requires each enemy's CONCIERGE notice on its first
   appearance. Also, the spotlight's notice reaches the screen after its beam is already on the player (beam at
   110.5 s, notice from about 113 s, `judge-D/s-110.5.jpg`, `s-113.jpg`), and it stays on screen after "Lights...
   out..." (`a-114.5.jpg`, `s-119.jpg`). Owner: HUD/presentation (teaching timing).
4. **The last 46 s of the arena recording (124-170 s) are a flat mint-green screen inside the service lift**
   (`frames/SkylineArena-127.5s` to `163.9s`). This isn't my category and is known from the lint (untextured lift walls),
   but it is 27% of the recording the creator will watch, and it is a spec-10 blocker ("an untextured placeholder in
   view"). Owner: arena look.
5. **The floor-care unit's idle cleaning reads as turning on the spot** (round-1 #6c, `judge-D/sheet-idle.jpg`
   13.5-16.5 s). Owner: the floor-care unit (`IdleWork`).
6. **The gallery's wall-run dummy floats upright against the glass** (`frames/SkylineGallery-050.9s.jpg`), so the
   gondola's whole lesson (a mine on the wall-run line) is shown on a figure that isn't wall-running. Owner: demo
   player/capture.
7. **Smoke at low chassis is still missing** (the creator's call, spec section 14). The wear tint isn't visible in these
   frames. Owner: all bots (an FX pass).

## What works now (checked)

- **Telegraphs.** Timings match the identity sheets, and each sound lands within 0.03 s of its logged event (table
  above).
- **Wrecks.** Each bot leaves its own wreck: sparks when fresh, dulled when stale (the spotlight's grey head at
  `g-42.8.jpg`).
- **Separation and plates.** The floor-care units keep apart, and plates still fly off where hit (`frames/SkylineGallery-
  011.1s`, `024.4s`, `042.1s`).
- **Aim models never mix:** the food court is drones plus floor-care units, the facade is drones plus gondolas, and the
  skylight is spotlights plus floor-care units (counted from `Encounters.cs`).
- **The spotlight's beam reads in the arena:** a red beam reaches the camera from above (`s-110.5.jpg`), and the red
  damage vignette follows (`s-119.jpg`).

## Scores (8 is the bar)

| Category | Round 1 | Round 2 | Why |
|---|---|---|---|
| Enemy readability | 4 | 6 | The wrecks, attacks and floor-care faces now read. The drone's face is still blocked, and in the arena the drones, the gondola and the spotlight's body are never readable on screen. |
| Motion | 4 | 7 | The scan, the ride, the hover and the separation all show. The floor-care unit's idle still mostly turns, and the gallery dummy floats. |
| Encounter fairness | 6 | 7 | Aim models unmixed, telegraphs timed and synced, the open layer rotates, no pile-ups. The floor-care notice never plays and the spotlight's notice comes late. The food-court line at 26/30 is known. |
| Consequence | 5 | 7 | The cache pays (banked), damage now reads through the vignette and labelled bars, and the wrecks are distinct. No smoke, and the banked scrap isn't shown. |

## Spec 10 blockers I see

1. **An untextured placeholder in view:** the service lift's mint-green walls fill the screen for 46 s
   (`frames/SkylineArena-127.5s.jpg` to `163.9s.jpg`). This is known and listed in the lint; it is not in my categories.

There is no blocker in my own categories this round. The round-1 primitives blocker (husk, cubes, capsule bolt,
cube jet) is fixed.

Blocker count: 1.

## Not covered by the spec (gaps to raise)

- There is no rule that a bot's face must be unobstructed from the angles the player sees it (the drone's screen is
  covered by its own body).
- There is no rule that a safety notice must play **before** the bot's first attack, or must not be dropped (the
  floor-care notice was dropped twice and never re-fired).
- There is no rule that the demo capture must show each bot alive and attacking, nor a minimum on-screen size for a
  telegraph at fight range.
- There is no rule on whether banked "recovered scrap" must be visible when it is earned.
- (Carried over from round 1) There is no rule on how long a gallery idle must be, or that the wall-run demonstration
  must actually wall-run.
