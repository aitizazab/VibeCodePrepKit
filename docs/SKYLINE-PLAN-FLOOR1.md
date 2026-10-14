# Skyline Repo, floor 1 (the Halcyon Grand lobby and the Food Court repossession): implementation plan

> **For agentic workers:** you are a well-scoped Sonnet implementer (the creator's rule, 2026-10-14: only well-scoped
> Sonnet agents, meant to implement a plan). Implement your task and only your task. Steps use checkbox (`- [ ]`) syntax.
> You write code and run the no-Unity checks only (`powershell -NoProfile -File tools/skyline/test.ps1`). **Never start
> Unity, never take the Unity lock, never wait or poll.** When done, stop and hand back; the main session runs the pass.

**Goal:** replace M1's warm-up hall and three player-started fights with floor 1 as specced: the hotel lobby as the fixed
entrance, a lift ride synced to the music, then the Food Court arena where the objective is to repossess three assets in
tow circles while waves arrive on a 30 s clock from flanking doors, in the comic-ink style, with the minimal HUD, the
special-ammo economy, comic lettering, a demo player that completes the floor like a human and a corner-camper that fails.

**Architecture:** the same split as M1. Everything that can be pure C# is pure C# in `JamKit.Skyline`
(`Packages/com.vibejam.jamkit/Runtime/Skyline/`, flat folder, no engine references) with tests in
`Packages/com.vibejam.jamkit/Tests/Skyline/`: the arena as DATA (solids, zones, sockets, spawn doors, routes) that the
Unity builder, the layout lint and the headless sim all read; the wave clock, the spawn bearings, the tow meter, the
floor flow, the music sequencer, the ammo ledger, the lettering picker, the HUD models. The Unity side
(`Assets/JamKit/Templates/SkylineRepo/`) only plays what the pure systems decide and builds scenes from data in editor
code. A new headless `RepossessSim` runs the real `MoveSim` over the arena's data with the wave clock, a pilot that
completes the floor and a camper that must fail; the Unity demo mirrors them. The floor is built as a NEW scene
(`Generated/SkylineFloor1.unity`) beside the old arena until a final switch-over task (X1) makes it the arena.

**Specs (read in this order):** `docs/SKYLINE-PLAYTEST-1.md` (start-here page; the creator's calls, which override older
docs), `docs/SKYLINE-FLOOR-REPOSSESS.md` (the approved level, sections 1-9), `docs/SKYLINE-HUD.md`,
`docs/SKYLINE-STYLE.md`, `docs/SKYLINE-MUSIC.md`, `docs/SKYLINE-BOTS.md` (damage states, the gondola outside the glass),
`docs/SKYLINE-DESIGN.md` 2.4-2.6, 6 and 10, `docs/SKYLINE-PIPELINE.md`, `docs/SKYLINE-LOOK.md`, and the conventions of
`docs/SKYLINE-PLAN-M1.md` (its Amendments section still applies) and `docs/SKYLINE-PLAN-STYLE.md`.

**Precedence when documents disagree:** the creator's newest call > `SKYLINE-FLOOR-REPOSSESS.md` section 9 > its
sections 1-8 > the other Skyline docs > M1's plan. Section "Conflicts and decisions" below records every disagreement
found and how this plan resolves it.

---

## Global constraints (copied from the specs; exact numbers)

### The level (`SKYLINE-FLOOR-REPOSSESS.md`)
- **Arena:** about 70 x 56 m, three levels. **Pit** -4 m, about 28 x 20 m, in by ramps on all four sides and a slide chute
  from the north ring; waist-high tables and planters (crouch cover only) and a kiosk at its centre. **Ring concourse**
  0 m, about 8 m wide; its cover is architecture (the pit's rim, shopfronts, escalators' bulk), not free-standing boxes.
  **Galleries** +6 m (north, south) overhang the ring by about 3 m; joined by the **bridge** (4 m wide, glass, low
  balustrade, the fastest and most exposed crossing) and **two escalators** criss-crossing (north-west ring to north
  gallery, south-east ring to south gallery). **Shops:** five a side, two walk-in a side (front door to the ring, back
  door to the service corridor). **Service corridor:** about 3.5 m wide, a U behind the north row, past the west lift
  and behind the south row. **East façade:** a glass wall-run the length of the hall, ending at each gallery's lip
  (mantle up); the gondola rail runs along it, OUTSIDE the glass (`SKYLINE-BOTS.md`). **Skylight** +14 m: the drone vents.
- **Layout rules (measured, all four):** (1) every standable point has at least two exits that leave a 10 m radius by
  different routes; (2) no standable point sees more than 70% of the ground-level spawn doors (shutters, hatches, rail ends; roof vents excluded, D23); (3) every tow circle is at least half
  exposed from two bearings 90 degrees apart; (4) the floor is finishable with the base kit (wall-run, double jump, slide,
  mantle).
- **Objective:** three listed assets (draft set: an animatronic mascot, a massage-chair display, an arcade cabinet). Tow
  circle about 6 m across (radius 3 m). **10 s** inside takes an asset; leaving the circle **pauses** the meter, it never
  drains; being in any circle shows its meter on the HUD. Order is free. Each circle is placed so at least half of it is
  exposed from at least two bearings (no circle can be held from behind one wall). **Clamping is loud:** the moment a
  meter first starts, CONCIERGE's alarm sounds and the next wave's spawns are chosen round that asset.
- **Sockets:** three groups, one asset per group per run (one low, one inside, one high). Pit: the kiosk at the pit's
  centre, the pit's north-east corner, its south-west corner. Inside: any of the four walk-in shops. High: the middle of
  the bridge, the north gallery's east end, the south gallery's west end.
- **The end:** when the third asset is towed the waves stop, CONCIERGE concedes ("Asset transfer... acknowledged. Under
  protest."), the **service lift on the west side** rolls its shutter up; living bots keep fighting; the floor ends when
  the Unit's feet reach the lift. **Wrecked:** the arena restarts (now at the lobby), assets go back on their sockets
  (same sockets this run), the clock resets.
- **Waves (Normal, starting numbers, balanced with the creator in play):** the clock starts at the drop (section 9: the
  lift doors open; section 3's "feet cross from the warm-up" is superseded). Wave 1 at 0 s, then **a new wave every 31.35 s (16 bars of the floor song at 122.5 BPM; stretched from 30 s, 2026-10-14)**
  until the third asset is towed. Nothing the player does delays the clock. **Cap: at most 10 living bots**; a wave that
  would pass the cap waits until there is room (**one waiting wave at most**; the clock keeps running for the next).

  | Wave | Time | Bots |
  |---|---|---|
  | 1 | 0 s | 3 drones |
  | 2 | 30 s | 3 drones, 1 floor-care |
  | 3 | 60 s | 2 drones, 2 floor-care |
  | 4 | 90 s | the gondola on the façade, 2 drones |
  | 5+ | every 30 s | alternate 3 drones + 1 floor-care / 2 drones + 2 floor-care; a gondola only if none is alive |

- **Bots on this floor:** drone (leads you), floor-care (area), gondola (area). **The spotlight is excluded** (its
  hitscan at where you are now may not share an encounter with the drone's leading shots, design 2.6).
- **Spawn doors** are in-world, each with a **2 s telegraph** (a sound from its direction, a light, the door moving):
  shop shutters on the ring (floor-care, drones), skylight vents (drones), service hatches (floor-care), the façade
  rail's ends (the gondola). **Bearings:** every wave spawns from **two points at least 90 degrees apart** as seen from
  the player; the first is at least **120 degrees** from the previous wave's mean bearing; after an alarm both points are
  the ones nearest that asset that still obey these rules; never closer than **15 m** to the player; never a point the
  player is looking at within **25 m**.
- **Wrecks** still give plating (design 2.4): strip once by touch while fresh (`Wreck.freshSeconds` 8 s).
- **Teaching:** Nan teaches the objective (the job sheet, the tow circle, "the clock's running, love"); Dot calls each
  tow ("Clamped! Hold still-ish."); CONCIERGE announces waves and alarms. Every line is a draft for the creator to
  rewrite, kept in `docs/SKYLINE-DRAFT-LINES.md` under this floor; the exceptions quoted verbatim in the specs stay.

### The lobby (`SKYLINE-FLOOR-REPOSSESS.md` section 9, `SKYLINE-MUSIC.md`)
- The tower above the Galleria is the **Halcyon Grand** hotel. Floor 1 always opens in its lobby: hand-made, never
  generated. **It replaces the warm-up hall** (the practice drone and the boost ledge move in). A 2D start menu comes
  later.
- (1) The staff hallway (spawn): dim, the lobby glowing at the end; the music's intro plays as tinny hotel muzak from
  ceiling speakers. (2) The lobby: two storeys; CONCIERGE's front-desk screen gives its first greeting; luggage carts; a
  chandelier (the light that takes the room over); a mezzanine lounge; the practice drone works as the bellhop demo (the
  Writ's spots); a luggage stack is the boost ledge; the music loops while the player is here. (3) The lift to the
  Galleria: stepping in starts the opening sequence **on the next bar**: the six-step riser while the lift rises, the
  pre-drop as the doors are about to open, **the drop as the doors open onto the food court and the wave clock starts**.
- **Music** (`SKYLINE-MUSIC.md`): sections (muzak, riser, pre-drop, drop, main loop, calm loop) **change only on bar
  lines**; a section loops until its trigger; one clock, scheduled on the audio DSP time; changes smoothed over seconds.
  Riser: C-E-G-A-Bb walking up the C chord one per beat, landing on C (about 16.1 s in the reference). Pre-drop clip
  measured about 86 BPM, its drop at about 3.5 s; the Subway Surfers theme measured about 122.5 BPM. Composed WITH the
  creator, layer by layer, never handed over finished: this plan builds only the director, the sequencer and PLACEHOLDER
  stems.

### The HUD (`SKYLINE-HUD.md`)
- Primary weapons (the Writ): **no reserve**. Special weapons (the Breacher and later): an economy. A wreck from a kill
  **by a primary weapon gives 1 special shot**; kills **by special weapons give 1 shot per 3 kills** (starting values).
- Minimal HUD: ammo (magazine count by the gun's small icon; a special weapon's reserve beside it); health and shields in
  **one bar** (chassis is the bar, plating a brighter layer over it, shatters off when it breaks; low chassis pulses the
  bar and tints the screen edges red); crosshair and hit markers kept (white a hit, yellow a weak point, gold a Writ spot,
  red a kill); damage direction kept (a wedge on a ring round the crosshair; also flashes for an off-screen attack about to
  land); the objective (the job sheet's three asset icons along the top edge, ticked when towed; an edge-of-screen
  direction marker per untaken asset, with its distance, and a world-space marker visible through walls; the tow meter as
  a ring round the crosshair while in a circle); a small "next wave" countdown; **no bot count**; subtitles movie-style:
  bottom centre, white text with a dark outline, no box, at most two lines, the speaker's name as a coloured prefix
  ("DOT:"), CONCIERGE's PA lines in italics; setting Off / Dialogue / Dialogue + bot barks, and a size.
- **Removed:** the text bubbles above bots and the boxed speaker cards.
- **Comic lettering** replaces the bubbles: comic sound-effect lettering that writes itself out beside the bot and fades,
  on kills, weak-spot hits, Writ spot breaks and big Breacher shoves (not every hit); each bot type and gun has its own
  words (drafts: the drone "KZZT!", floor-care "SPLORCH!", a Writ spot break "OBJECTION!" / "SUSTAINED!", the Breacher's
  shove "WHUMP!"); hostile red-magenta lettering with a dark ink keyline; never covering the crosshair; **at most two on
  screen**.
- **Flashes and styling:** plating gained from stripping a wreck: the screen edges flash light blue, briefly. Special ammo
  picked up while a special weapon is out: the ammo counter flashes green. Strippable wrecks are outlined light blue,
  thinning as the strip window closes and flashing out over its last **0.4 s**. The health bar is a **layered
  parallelogram** (plating a slanted layer over the slanted chassis bar, ink keylines, hard edges); the ammo counter uses
  a **display font with character** (a bold comic/condensed face, never the UI default), big magazine digits, a small
  reserve; the whole HUD shares the style sheet's ink keyline and palette. Never "generic rounded boxes and text".

### The style (`SKYLINE-STYLE.md`)
- Comic ink: scenery ink about **1.5 px** at 1080p, lines fade with distance; three light bands (lit, half, shade) with
  light world-space hatching in the deepest shade; flat painted colour with a vertical gradient; **walls cool blue-white
  (plaster about `#E9EDF2`) and floors warm cream (about `#CDBB9C`)**; painted-gradient sky; neutral tonemapping, bloom for
  lights, slight vignette.
- **Thick outlines (about 4 px at 1080p, constant on screen), by role:** hostile bots, mines, acid, bolts `#FF2E63`
  (no scenery ever uses this hue); objectives (tow assets, the lift when it opens) violet `#B36BFF`; strippable wrecks
  light blue `#8FD3FF`; weak spots (the Writ's) yellow `#FFD60A`, drawn over the hostile hue where they overlap; a bot's own
  weak-point parts `lerp(#FF2E63, #FFD60A, t)` with t pulsing 0.35 to 0.6 at about 1.2 Hz; the viewmodel's ink colour at
  2.5 px.
- **Light takes over (Deep Rock Galactic):** low ambient, strong local lights, banded like the sun with hard-edged pools; a
  room's look is its brightest light (a shop's neon, the corridor's strip lights, a warning beacon, the tow drone's beam,
  a muzzle flash). The sunlit hall stays the bright, warm hero space; interiors are dim until their own light takes them
  over. **Colour-free zones:** the **service corridor** and the **walk-in shop interiors** go to greys, only the lights
  carry hue; the hued outlines keep their colours there. **TF2 level of detail:** not cluttered, not barren; big readable
  shapes first, detail on edges and the skyline (rooflines, cornices, trims, signs, awnings, props clustered against walls);
  floors and routes stay clean for movement; layered depth (near, mid, far, each lighter and lower in contrast); scenery
  lowest in contrast and saturation, gameplay objects highest.
- Budget: mask pass plus composite at most **0.6 ms** at 1080p on the RTX 3050.

### The bots on this floor (`SKYLINE-BOTS.md`, `SKYLINE-DESIGN.md` 2.5-2.6)
- Aim models never mix in one encounter (drone leads, spotlight fires at where you are now). Every encounter spawns on at
  least two bearings 90 degrees or more apart; an encounter covers at most two of floor / walls / air. Every encounter
  has a **clean line** (a route a skilled player can take without paying resources).
- The gondola rides OUTSIDE the glass, looking in, and lays its mines on the inside of the wall-run through the
  façade's service hatches. Its numbers: rail speed 3 m/s, a mine every 4 m of rail, at most 6 live, each lasting 12 s,
  trigger radius 1.5 m, fuse 0.35 s, blast radius 2.5 m, 25 damage.
- Damage states (70% and 30% starting set) and the redesigns are LATER (the last section); the floor must not wait on them.

### Performance, quality, process
- **120 fps target at 1080p, 60 fps floor** on the RTX 3050 laptop with the full cap (10 bots) alive; 1% lows at or under
  **16.7 ms in a build** (a blocker), target 8.3 ms; a scene must open within the 30% memory cap (**4.6 GB**) (blocker).
  First-use pipeline states cost 60-220 ms on D3D12: every new material, mesh layout and InkLit pass is drawn once in
  the start-up warm-up behind the start cover (`SkylinePrewarm`).
- Any keyword a material turns on at run time must be saved on a build-included material (the invisible-bots lesson).
- Never hand-write Unity `.meta` files. C# 9, NUnit 3.5 APIs only (`Assert.That`, `Is.*`, `Does.*`, `Assert.Throws`,
  `[TestCase]`, `[Explicit]`); no file-scoped namespaces, records, nullable annotations.
- Pure code only in `Packages/com.vibejam.jamkit/Runtime/Skyline/`; tests in `.../Tests/Skyline/`; Unity code only in
  `Assets/JamKit/Templates/SkylineRepo/`. Never edit Visiting Hours' files. The `core-tests` csprojs glob both Skyline
  folders and the whole SkylineRepo folder, so new files need no project edits. Tuning numbers live in data, never in
  components.
- **Git:** work on `main`; stage and commit explicit paths only: `git add -- <paths>` then
  `git commit -m "..." -- <the same paths>`; never `git add -A`; never the already-modified `.unity` scenes outside
  SkylineRepo. Another session works on Visiting Hours in this repo: never touch its files. Commit messages end with a
  blank line and `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Content tasks (dressing, sound, art) do not
  commit: they hand the file list back and the main session commits.
- **No grey-box judging:** the creator is handed content-complete, agent-judged builds only; grey-box scenes exist for the
  agents' own checks and for the lint. Green tests are never evidence of quality.
- Lines of dialogue are drafts in one data file (`FloorBeats` here); the creator rewrites them.
- Disk is tight: captures go to `out/skyline/<stamp>/` and are pruned after judging. Python through `uv run python`;
  heavy jobs through the capped-run wrapper; GPU jobs one at a time (the main session's business).

---

## Conflicts and decisions (spec versus spec versus code)

| # | Found | Decision in this plan |
|---|---|---|
| D1 | `FLOOR-REPOSSESS` section 4 keeps the warm-up hall and "its doorway moves to the new south wall"; section 9 (later) replaces it with the lobby. Section 3 starts the clock when the feet cross out of the warm-up; section 9 starts it at the drop. | Section 9 wins: no warm-up hall; the clock starts at the drop. Section 4's text gets a one-line correction in F0. |
| D2 | The lobby must meet the arena. Section 4 draws a doorway in the south wall; the south shop row has "five" shops. | The south row's third slot is the **arrival alcove** (the lift cab's mouth); the south row therefore has 4 shops + the alcove, the north row 5 shops. The cab sits in the south service corridor's line, so that corridor is cut in two (west part joins the west leg; the east part is a dead end with a hatch). Raised as a question. |
| D3 | The ink shader's roles (`InkLit.shader`): objective cyan `#2EE6FF`, scrap green `#5CFF8A`; `SKYLINE-PLAN-STYLE.md` and `SKYLINE-WORKLOG.md` repeat them. `SKYLINE-STYLE.md` (newer): objective violet `#B36BFF`, strippable wrecks light blue `#8FD3FF` that thin and flash out. `SkylineWreck` still sets `SkylineInkRoles.Scrap`. | The style sheet wins. Task S0 changes the role hues, thins and flashes the wreck outline. |
| D4 | The objective must be visible through walls (`FLOOR-REPOSSESS` section 2), but the ink mask pass depth-tests (`ZTest LEqual`), so nothing is outlined through a wall. | S0 adds an x-ray draw of the objective role (dimmer, `ZTest Greater`). |
| D5 | `FLOOR-REPOSSESS` section 2 says the HUD shows "each asset's direction and distance"; `SKYLINE-HUD.md` lists only an edge marker. | Edge marker **with** the distance in metres, plus the through-wall world marker. |
| D6 | The whole pure sim (`FightSim`, `FightBots.TacticalBot`, `IdleWork`, `SkylineBot.SearchArea`, `SkylinePrewarm`, `EncounterDodge`) hard-codes `GalleriaArena.Hall*` and the flat Y=3.8 drone altitude; fights start from a fixed spawn list. | W3 adds a `FightArea` (default = the old hall, so old tests stay green) and `FightSim.AddEnemy`. W4 gives door-spawned bots an arrival and engagement behaviour (drones must travel from a vent 13.6 m up to a hover point at the player's level; today they "hold their post"). |
| D7 | The bots' `bodyRadius`, size and altitude change in the later redesigns (larger, rotors as weak spot). | Floor data and lint read `EnemyDef.bodyRadius`; nothing hard-codes 0.45 / 0.5 / 0.8. |
| D8 | `SkylineHud` has boxed bars, a boxed speaker card, a CONCIERGE banner and bark bubbles; the HUD spec removes the boxes and bubbles. `SKYLINE-HUD.md` and `SKYLINE-BOTS.md` each repeat a section twice. | H2/H4 remove them; the banner becomes an italic PA subtitle. Docs hygiene: F0 deletes the duplicated sections. |
| D9 | "A wave that would pass the cap waits ... one waiting wave at most; the clock keeps running" does not say what happens when a second interval arrives while one waits. "5+ ... a gondola only if none is alive" does not say how often. | Starting rule (W1, data): the new interval's wave is **skipped** (counted, logged); a gondola is added to **every fourth wave from wave 8** when none is alive. Raised as questions. |
| D10 | The old exit lift is a knee-high slide-under shutter (a joke line in `TeachingBeats`); the spec only says the shutter "rolls up". | It rolls fully up (the finale is a run); `ShutterGap` is retired. Raised as a question. |
| D11 | `SKYLINE-PLAN-STYLE.md` still gives the objective hue as cyan and the wreck/scrap hue as green; the creator's later call moved both. | That plan is history; this plan and S0 follow the style sheet. |
| D12 | Every Skyline directory test, pass script and the frame-time probe assume one scene named `SkylineArena` (first in the build, 180 s recording). | The new floor is built as `SkylineFloor1.unity` outside `SkylineScenes.All` until X1 swaps it in as `Arena`. |
| D13 | `SKYLINE-PLAYTEST-1.md`: the level "is not built until the art style is settled"; the style is decided but the creator "will wait for the scenes from the game to judge" the look test. | Gate G0: the pure tasks and the grey-box builder may start now; the dressing tasks (DR*) start only after the creator has judged the look-test stills. |
| D14 | The last recorded perf (`SKYLINE-LOOK.md` round 5) was 1% low 28.9 ms after the ink look (target 16.7 ms), with a fix "expected, not verified". | P0 (main session) re-measures the CURRENT arena first. If still over, a perf fix outranks new content. |
| D15 | (Creator's defaults applied by F0, 2026-10-14.) The lobby replaces the warm-up hall: `FLOOR-REPOSSESS` section 9 wins over sections 3-4. | Done in F0: sections 2-4 of that spec now say the clock starts at the drop, a wreck restarts at the lobby, the warm-up hall is replaced by the lobby, and the south row's third slot is the arrival alcove (D2). |
| D16 | Hues: the objective is violet `#B36BFF`, a strippable wreck is light blue `#8FD3FF` (`SKYLINE-STYLE.md`); the code still has cyan `#2EE6FF` and green `#5CFF8A`. | The style sheet is final (see D3, D11). The code fix is task S0, not F0. Nothing before S0 may bake the old hues into new data or tests. |
| D17 | `SKYLINE-HUD.md` and `SKYLINE-BOTS.md` each held a section twice ("Feedback flashes and styling"; "Damage states"). | Done in F0: one copy of each kept, the most complete one (the HUD's first; the bots' first, which has the extra "limp" and "stack with Writ spots" lines). |
| D18 | Every open question at the end of this plan has a default. | The creator's defaults are in force (questions 1-12 below stay as the record; change one only on a new call from the creator). |
| D19 | F0's interface block left a few details open. | Chosen in F0, now fixed for every later task: (a) `FloorRules.DoorAllows` takes the `EnemyDef.id` strings (`SkylineEnemies.DroneId` "mall-cop-drone", `FloorCareId` "floor-care-unit", `GondolaId` "window-washer-gondola", `SpotlightId`); an unknown or null id is allowed nowhere. (b) `FloorSolid.TopAt(x, z)` is `box.max.Y` for a flat solid; for a ramp it interpolates linearly from `rampTopStart` at the box's MINIMUM edge on the slope axis (x when `rampAlongX`, else z) to `rampTopEnd` at its MAXIMUM edge, clamped to the box (so a ramp whose high end is at +Z has `rampTopStart` < `rampTopEnd`). A ramp's `box` spans its full footprint, with `box.min.Y` at or below the lower end and `box.max.Y` at the higher end. (c) `SpawnDoor.yawDeg`: degrees, 0 = +Z (north), clockwise seen from above like the player's yaw; it is the way the bot faces on arrival. (d) `FloorPhase`: `Lobby` from the start in the staff hallway until the player steps into the lift; `LiftRide` until the drop (doors open, wave clock starts); `Floor` until the third asset is towed; `Finale` until the Unit's feet reach the west lift; `Done` after. A wreck sends the run back to `Lobby`. (e) `FloorSolid.role` stays a free string; the starting values are floor wall rim shopfront column pilaster escalator ramp deck balustrade table planter lift facade, and F1/F2/L1 may add roles (document them in their own file). (f) All new types are in `FloorTypes.cs`, namespace `JamKit.Skyline`, `System.Numerics.Vector3`; `FloorZone` has no behaviour. (g) The twelve new `BeatTrigger` values are appended after `LiftOpened`, in the order the plan lists them; any value not listed in `UrgencyOf` falls to `Hint`. |

---

## The layout sheet (starting numbers; F1, F2 and L1 may move things inside the envelopes so the lint passes, and record why)

Metres. +Z is north, +X is east. Hall interior x -35..35, z -28..28; walls 0.4 m outside that. Roof 16 m, skylight glazing
at y 14 over x -30..30, z -22..22. Walkable tops: pit -4, ring and corridor 0, galleries and bridge 6 (slab 5.6..6.0).

| Part | Starting box(es) |
|---|---|
| Pit | x -16..12, z -10..10, floor -4; retaining walls -4..0 all round (runnable). Ramps (6 m long, 4 m wide, top 0 to -4, slope 34 degrees): west x -16..-10, z -2..2; east x 6..12, z -2..2; north x -4..0, z 4..10 (descends south); south x -4..0, z -10..-4 (descends north). Slide chute from the north ring: x -11..-8, z 5..10, top 0 to -4 (moved 1 m east on 2026-10-14, D23, so the pit's north-west corner has a second way out). |
| Pit contents | kiosk x -3.5..-0.5, z -1.5..1.5, top -1.5 (solid, not runnable); 8 tables 1.6 x 0.9 x 1.0 and 4 planters 2.0 x 0.8 x 1.0 high, placed so the pit has crouch cover only, none within 3 m of a socket's centre line to a ramp foot; sockets: kiosk top (-2, -1.5, 0), NE corner (9, -4, 7), SW corner (-13, -4, -7). |
| Ring | floor 0 round the pit (and out to the east façade): north ring z 10..18, south ring z -18..-10, west ring x -31.5..-16, east ring and promenade x 12..35. Cover is architecture: the pit rim, shopfronts and their pilasters (0.6 x 0.6 columns every shop joint), the escalators' bulk, the gallery columns (0.8 x 0.8, y 0..5.6, at z +-16.5 and x = -14, -5.2, 5.2, 14, 26 in both rows; moved 2026-10-14 from -20, -10, 0, 10, 20, 30 so that no shop shutter (8 m wide: x -24.8..-16.8, -4..4, 16.8..24.8) has a column in its 2.5 m of floor: the old x 0, +-20 columns stood 1.1 m in front of the N3, N1, N5 shutters; the deck is never more than 12 m from a column along its front edge, and none stands on an escalator landing; `RepossessArena.ColumnXs`, tests `EveryShopShutterHasTwoAndAHalfMetresClear...` and `TheGalleryColumnsKeepTheDecksSupported...`). No free-standing boxes taller than 1.2 m in the ring or on the galleries. |
| Shops | north row z 18..24 (6 m deep): N1 x -26..-15.6, N2 -15.6..-5.2, N3 -5.2..5.2, N4 5.2..15.6, N5 15.6..26; interior ceiling 4.6, roof slab to 5.6 (the north gallery deck). South row z -24..-18: S1, S2, [arrival alcove, x -5.2..5.2], S4, S5 mirrored. Walk-in: N2, N4, S2, S4 (front doorway 4 m wide at the shop centre (was 3 m; D23), back doorway 2.4 m wide into the corridor). The other shops are closed shopfronts with a roll-up shutter 8 m wide (the spawn door). Shop sockets: N2 (-10.4, 0, 21), N4 (10.4, 0, 21), S2 (-10.4, 0, -21), S4 (10.4, 0, -21). |
| Service corridor | north z 24..27.5, x -35..26; south z -27.5..-24, x -35..26 (cut at x -2.5..2.5 by the lift cab); west leg x -35..-31.5, z -27.5..27.5. Doors from the west ring into the leg at z = +-12 (2.4 m wide). East ends open to the promenade through a roll-up hatch (floor-care spawn). |
| Exit lift (west) | lobby x -31.5..-24, z -4..4, 5 m tall; doorway in its east face (x -24, z -2..2, 3.2 m high) with the shutter; exit zone (feet) x -31..-27, z -2..2. |
| Galleries | north deck x -26..35, z 15..24, top 6; south deck x -26..35, z -24..-15, top 6; rail 1.0 m along z 15 and z -15 edges (glass, bullet-transparent, blocks movement). Sockets: north east end (30, 6, 20); south west end (-22, 6, -20). |
| Bridge | x 6..10, z -15..15, top 6, glass floor, glass balustrade 1.0 m each side. Socket: middle (8, 6, 0). |
| Escalators (static ramps with moving-step visuals) | north-west x -22..-19.5, from (z 4, y 0) to (z 15, y 6); south-east x 18..20.5, from (z -4, y 0) to (z -15, y 6). Slope about 28 degrees. |
| Façade | the east wall x = 35 is glass, runnable, y 0..16 (the wall-run runs z -15..15, 30 m, longer than a 1.75 s run's 15 m, so it takes two runs or a kick); outside it the rail at x = 36.5, y = 3.0, z -27..27; 12 service hatches on the inside face every 4 m, z -22..22, y 2.0..3.0; the rail ends are the gondola spawn doors. Against it, at the north gallery: the solid `facade-landing` (role deck, x 30.5..35, z 9..14.8, top 2.8; D23), the ledge a jump and a mantle climb, from which a jump and a double jump put the deck lip at z 15 within the longest mantle (a wall-run alone tops out near 3.9 m, under the deck). |
| Skylight | glazing y 14; 6 drone vents on the frame at y 13.6 (8 more roof vents are in the Spawn doors row): (-26, 12), (-26, -12), (26, 12), (26, -12), (-4, 22), (-4, -22) (x, z); none above a socket. |
| Arrival | the lift cab x -2.5..2.5, z -28..-24 (inside the south corridor's line, south doors to the lobby at z -28.4); the alcove x -5.2..5.2, z -24..-18, open to the south ring; `ArrivalPoint` (0, 0, -21). No back walls beside the cab (`alcove-back-w/e` removed, D23): the corridor stubs open onto the alcove. |
| Spawn doors (27) | shop shutters (5): N1 (-20.8, 0, 18), N3 (0, 0, 18), N5 (20.8, 0, 18), S1 (-20.8, 0, -18), S5 (20.8, 0, -18). Skylight/roof vents (14, y 13.6): the six of the Skylight row, plus eight added by F2 (2026-10-14) so that every spot of the hall has a legal spawn pair for W2's `SpawnBearings.Choose` (15 m, 90 degrees; drone, floor-care and gondola waves): (10, 27.05) and (10, -27.05) above the corridors, (-34.55, 10) and (-34.55, -10) above the west leg, (34.55, 10) and (34.55, -10) above the east promenade (the end vents and the corridor hatches sit as near the wall as their bodies allow; D23), (-30, 18) and (-30, -18) on the skylight frame's west edge. Service hatches (6): north-east end (26, 0, 26), south-east end (26, 0, -26), west leg (-33, 0, 14), (-33, 0, -14), plus corridor hatches (-10, 0, 27), (-10, 0, -27). Rail ends (2): (36.5, 3, -27), (36.5, 3, 27). Without the ten added doors 11% of the grid had no legal pair (the end bands). |
| Lobby | interior x -22..22, z -60..-28.4 (its north wall is the hall's south wall), 9 m tall; mezzanine lounge on the east side x 14..22, z -52..-34, top 4.5, stairs at its south end; front desk x -14..-6, z -33.5..-31 with CONCIERGE's screen above; chandelier at (0, 7, -44); the lift's lobby doors x -2.5..2.5 in the north wall; luggage carts as low cover; boost stack (luggage stack, 3 x 3, top 4.6) at (-4, 4.6, -46) (>= 12 m from every wall: `GalleriaArena.LedgeWallClearance`); practice counter (3 x 3, top 3.5) at (14, 3.5, -56); wall-run lockers on the west wall (x -20.5..-18, z -36..-34, top 4.6); practice bot at (-12, 1.4, -50) on a bellhop rig. Staff hallway x -1.5..1.5, z -78..-60, dim; player start (0, 0, -76), yaw 0. The 12 m separation between the boost stack and the practice counter and the wall-clearance tests of M1 (`LedgeWallClearance`, `PracticeLedgeSize`) carry over. |

---

## Review focus (what a reviewer, and the main session, check first)

1. **A long frame in the wave clock or the tow meter** (a 0.5 s hitch, a 20 fps machine): every crossed wave is released
   once, in order; the meter integrates the same seconds however the frame is cut; nothing double-fires at the cap.
   Tests: W1, R1.
2. **Unsatisfiable spawn bearings.** The player standing in the arrival alcove, inside a shop, in a pit corner, on the
   bridge: the fallback ladder never spawns a bot closer than 15 m or in the player's view within 25 m, and the chosen
   doors still span 90 degrees or the wave waits. Tested with the player at every standable point of the arena. Test: W2.
3. **The aim-model rule per wave**, including the gondola wave, the alarm wave and every wave at the cap: no wave pairs
   "leads you" with "fires at where you are now"; no encounter covers all three layers at once (wave 4 is drone = air,
   gondola = walls: floor stays open; waves with floor-care + drones leave walls open). Test: W1.
4. **Wreck mid-anything:** mid-tow, mid-telegraph, mid-lift-ride, mid-wave, with a waiting wave. All bots, pending
   telegraphs, bolts, mines, meters and the music state reset; the socket assignment and the beats that played stay.
   Tests: R1, W3, L2.
5. **Lift and music sync.** The drop must land with the doors, measured in the recording with the ears; if the audio
   device starts late or fails, the doors follow a fallback timer and the wave clock still starts at the doors. Tests:
   M1 (pure), L2 (pass).
6. **Glass.** The façade and the bridge are glass that blocks movement, is runnable where the spec says, and is
   bullet- and sight-transparent for guns and bots (so the gondola outside can be shot and can see the Unit). A shot
   through glass must not skip a collider behind it. Lint rule: no wall-run surface is silently non-runnable.
7. **Cover is architecture.** The layout lint's rule 1 and 2 can be satisfied by cheating with free-standing boxes; the
   solids' `role` field and a test forbid tall free-standing cover in the ring and on the galleries.
8. **Special ammo is not exploitable:** the kill is credited to the gun of the killing blow; a wreck gives its ammo once;
   a restart resets the economy; the Writ never needs reserve (`infiniteReserve` stays on for primaries).
9. **Perf first-use:** every new material, InkLit pass and mesh layout (the glass bridge, the shutters, the lettering
   quads, the HUD's vector meshes) is in the start-up warm-up; light counts are inside the budget table of DR*; the
   run-time keyword rule holds; the scene opens inside 4.6 GB.
10. **HUD at the edges:** the subtitles, markers and bars at 16:9, 21:9, 4:3, 4K and in the safe area; the strings never
    clip (design 10 blocker "clipped text"); the display font loads or falls back with a log line.
11. **The hostile hue is reserved:** no scenery material, neon sign or light colour within the hostile red-magenta band
    (an editor lint, S0); the shop neons and corridor strip lights are tested against the outlines in the stills.
12. **The demo is a human, the camper fails for the right reasons:** the demo's aim spring, turn-rate cap and reaction
    beat stay (`HumanAim`); the camper is not killed by a bug (stuck, out of bounds) but by flanks.
13. **Data equals scene:** every `FloorSolid` has exactly one matching collider in the built scene and vice versa; ramps
    and escalators walk in the real `MovementController` as in `BoxWorld`.

---

## Task map

Sizes: S about 2 h, M 3-4 h, L 5-6 h (split any task that grows past that). **Gate G0:** the dressing tasks DR1-DR5 and
the lobby dressing wait for the creator to judge the look-test stills (D13). **Concurrency advice:** the usage budget is
tight (the week is in the red); run at most 3-4 agents at once, cheapest and most certain first (pure tasks with tests).

| ID | Task | Kind | Size | Owns (create / modify) | Needs |
|---|---|---|---|---|---|
| F0 | Floor contracts, beat triggers, doc fixes | pure | S | `FloorTypes.cs`; `Encounters.cs` (enum values only), `TeachingQueue.cs` (`UrgencyOf` only); 3 docs | none |
| F1 | Arena geometry data + test world (DONE 2026-10-14) | pure | L | `RepossessArena.cs`, `Tests/.../FloorWorld.cs`, `RepossessArenaTests.cs` | F0 |
| F2 | Sockets, spawn doors, circles, assets, routes (DONE 2026-10-14) | pure | M | `RepossessSites.cs`, `RepossessSitesTests.cs` | F0 (F1 for the final check) |
| W1 | Wave clock, composition, cap (DONE 2026-10-14) | pure | M | `WaveClock.cs`, `WaveClockTests.cs` | F0 |
| W2 | Spawn bearings, door choice, telegraphs (DONE 2026-10-14) | pure | M | `SpawnBearings.cs`, `SpawnBearingsTests.cs` | F0 |
| R1 | Tow meter, repossession, floor flow (DONE 2026-10-14) | pure | M | `Repossession.cs`, `FloorFlow.cs`, tests | F0 |
| L1 | Lobby data and the floor's teaching beats (DONE 2026-10-14) | pure | M | `HotelLobby.cs`, `FloorBeats.cs`, tests | F0 |
| M1 | Music sequencer, lift-ride plan, layers (DONE 2026-10-14) | pure | M | `MusicSequencer.cs`, `MusicLayers.cs`, tests | F0 |
| A1 | Special-ammo ledger and the finite reserve hook (DONE 2026-10-14) | pure | S | `SpecialAmmo.cs`, `SkylineGun.cs` (additive), `SkylineGuns.cs` (one line), tests | F0 |
| C1 | Comic lettering core | pure | S | `ComicLettering.cs`, tests | F0 |
| V1 | HUD models (health, edge marker, subtitles, wave text, wreck sequence) (DONE 2026-10-14) | pure | M | `HudModels.cs`, tests | F0 |
| S0 | Ink roles: hues, wreck thinning, x-ray objective, hostile-hue lint (DONE 2026-10-14) | Unity | M | `InkLit.shader`, `SkylineInk*.cs`, `SkylineInkRoles.cs`, `SkylineWreck.cs`, a new editor lint | none |
| D0 | Materials, palette and the colour-free zone helper | Unity | M | `SkylineLookLibrary.cs`, `SkylineInkPalette.cs`, `SkylineInkConvert.cs`, `make-textures.py` | none |
| F3 | Layout lint + navigation graph + base-kit routes (DONE 2026-10-14; the arena does NOT pass the lint yet: the findings and their verified nudges are pinned in `ArenaLintTests.Known`) | pure | L | `ArenaNav.cs`, `ArenaLint.cs`, tests | F1, F2 |
| W3 | `RepossessSim`: headless floor, `FightArea`, `AddEnemy` (DONE 2026-10-14; the constructor also takes the `IMoveWorld`; W4 joined the same day: one `BotArrival` per door-spawned drone and floor-care unit, stepped before `Fight.Tick()`, `FightEnemy.Arriving` keeps an arriving bot from seeing or attacking, `FloorOptions.arrivals`, `RepossessSim.Nav`) | pure | L | `RepossessSim.cs`, `FightSim.cs`, `FightBots.cs`, `IdleWork.cs`, tests | F1, F2, W1, W2, R1 |
| W4 | Door-spawned bots: arrival and engagement (DONE 2026-10-14) | pure + Unity hook | M | `BotArrival.cs`, `HoverPatrol.cs`, `SkylineBot.cs`, tests | F0 (W3 for the sim) |
| B1 | Floor builder: the scene from data (grey-box) (DONE 2026-10-14; extension points are `static partial void` methods declared in `SkylineArenaBuilder.Floor.cs`: B2 implements `BuildLobbyShell(Transform arena)`, X0 `WireDirector(arena, player, hud)`, DR1-DR5 `FloorDressShops/Pit/Corridor/Upper/Lobby(Transform arena)`; helpers `FloorSolidObject`, `FloorMaterialFor`, `FloorObject(id)`; also adds pure `FloorBuildPlan.cs` (ramp wedge, FloorDataMatch) with tests; `SkylineScenes.BuildFloor1FromCommandLine` is the one-off job; glass sits on layer `SkylineGlass` (made in TagManager.asset by the first build); bot sight skips it (W4), `SkylineShooting.Mask` must too (A2)) | Unity | M | `SkylineArenaBuilder.Floor.cs`, `SkylineScenes.cs`, `SkylineLint.cs` | F1, F2 |
| B2 | Lobby builder (grey-box) | Unity | M | `SkylineArenaBuilder.Lobby.cs` | L1, B1 |
| H2 | HUD base: health bar, ammo, font, old boxes removed (DONE 2026-10-14) | Unity | M | `SkylineHud.cs`, `SkylineHudHealth.cs`, `SkylineHudAmmo.cs`, `ThirdParty/Fonts` | V1 |
| H3 | HUD objective: job strip, markers, tow ring, wave clock (DONE 2026-10-14; `Scripts/Hud/SkylineHudObjective.cs`, hooked into `SkylineHud` as `Hud.Objective`; the director (X0) calls `Hud.Objective.Bind(repossession, waveClock)` at the drop and after a wreck. No label text: the clock shows "0:21" beside a drawn wave icon (the word NEXT is dropped), the ring keeps the crosshair free so the asset glyph sits on the ring at twelve o'clock instead of in its middle; the clock reads `WaveClock.SecondsToNext`, i.e. `FloorRules.WaveSeconds` 31.35 s) | Unity | M | `SkylineHudObjective.cs` | V1, R1, H2 |
| H4 | Subtitles, settings, bubbles out (DONE 2026-10-14; the two keys live in the pure `SkylineOptions` (`subtitleMode`, `subtitleSize`) and `SkylineSettings.SubtitleMode/SubtitleSize` wrap them; a Subtitles page in the pause menu; barks are subtitled from `SkylineBarks.Say` only when the mix actually played them and the bot is within 40 m, not from the `Barked` event; `SkylineBarkBubble` and `SkylineHud.BarkTag/ShowBark/PlaceBark/HideBark` are gone) | Unity | M | `SkylineHudSubtitles.cs`, `SkylineSettings.cs`, `SkylinePauseMenu.cs` | V1, H2, W4 |
| H5 | Flashes: plating, ammo, wreck outline timing (DONE 2026-10-14; the wreck outline is S0's. `SkylineHudFlashes` is a plain class owned by `SkylineHud` (`hud.Flashes`; `Build` under the root before the safe area, `Tick`, `Bind`/`Unbind` the two static events): plating gained (`SkylineWreck.Stripped`, gain > 0) flashes a hard-edged chamfered frame in `#8FD3FF` with an ink keyline on its inner edge, 0.35 s, peak 35%, one pooled element; special ammo gained (`SkylineAmmo.SpecialGained`) calls `Ammo.Flash(Green)` only while a special gun is out. Curves and the rule are pure statics in `SkylineHudFlashCurves`; no text anywhere; logs `SKYLINE_HUD flash plating` / `SKYLINE_HUD flash ammo`. `SkylineHud.cs` got six one-line hooks) | Unity | S | `SkylineHudFlashes.cs` | H2, A2, S0 |
| A2 | Special ammo in play (wreck pickups, Breacher reserve, HUD) (DONE 2026-10-14; `SkylineAmmo` (made by `SkylineAmmo.Ensure(player)` from the HUD's Start) puts a claim on the dead bot's wreck at `SkylineBot.Killed` and pays it into the ledger when the wreck is stripped (`Wreck.stripped`, also at full plating), so one pickup per wreck and none after the strip window; it re-binds the ledger to the player's current `Loadout` each frame (a restart makes a new Breacher); the director calls `SkylineAmmo.Instance.ResetRun()` (and G1's `SkylineFacade.Mend()`) on a wreck; logs `SKYLINE_AMMO +n (kill by <gun>) reserve m`. Shots (D21): `Mask` now skips the `SkylineGlass` layer (sight, aim and effect rays) and `ShotMask` keeps it; `Fire` breaks every façade pane it crosses through G1's `SkylineFacade.TryBreak` (up to 4 a pellet; the ray carries on past a pane just broken) and is stopped by every other glass, and by a pane when the scene has no `SkylineFacade`. Bolts and the demo's line-of-fire checks are not changed here) | Unity | M | `SkylineAmmo.cs`, `SkylineShooting.cs` | A1, S0, W4, H2 |
| C2 | Comic lettering in play (DONE 2026-10-14; `Scripts/Fx/SkylineLettering.cs`, hooked into `SkylineHud` as `Hud.Lettering`; kills come from `SkylineBot.Killed`, spot breaks, big Breacher shoves and weak-point hits from `SkylinePlayer.HitLanded` held one frame so a kill in the same shot wins; the pure write-out timing and placement are `LetterLayout.cs` (`LetterWipe`, `LetterPlacement`) with `LetterLayoutTests`; the wipe is one clip over the word opened by the glyph timing, a true stroke order stays a later option) | Unity | M | `SkylineLettering.cs` | C1, H2, W4 |
| M2 | Music director + placeholder stems | Unity | M | `SkylineMusicDirector.cs`, `tools/skyline/music/`, `Resources/SkylineMusic/` | M1 |
| R2 | Repossess runtime: circles, alarm, end, lift run (DONE 2026-10-14; component `SkylineRepossess` (`Setup(arena, feet, seed)`, `Begin()` at the drop, `Step(dt, feet)`, `ResetRun()` on a wreck) wraps R1's `Repossession` through the new pure `TowWatch.cs`; events for X0: `MeterStarted`, `AlarmSounded(entry, pos)` + `ConsumeAlarmAsset()` (feeds `SpawnQuery.alarmAsset`), `Towed` (`TowedCount == 1` is the first tow: Dot's "Clamped! Get Moving!"), `LeftCircleMidTow` (fires on every leave, the beat plays once), `AllTowed` (call `FloorFlow.AllTowed()`), `ExitReached` (`FloorFlow.ReachedExit()`); plain classes drive the pieces: `SkylineTowCircle` (ring + clockwise fill + pool light), `SkylineRepossessAsset` (primitive placeholders in the objective role, clamp, 1.2 s winch), `SkylineRepossessDrone` (Dot's tow drone, striped violet beam), `SkylineRepossessAlarm` (sting + amber beacon on the nearest wall), `SkylineRepossessExit` (the `lift-shutter` rolls up, violet frame and arrow, `InLift`); the sound names are placeholders in `SkylineRepossessAlarm`/`Exit`, sounds go through `SkylineRepossessFx.SoundSink`; Unity EditMode tests in `Editor/Tests/SkylineRepossessTests.cs`, not yet run) | Unity | L | `Scripts/Floor/SkylineRepossess*.cs`, `SkylineTowCircle.cs` | R1, B1, S0 |
| SD1 | Spawn doors and their 2 s telegraphs (DONE 2026-10-14; `SkylineSpawnDoor` is one component per door on the builder's `Door <id>` marker (its grey-box block is hidden), `SkylineSpawnDoors.Attach(arena)` makes them all; X0's loop: `WaveSpawner.Tick` -> for each `telegraphStarted` `doors.Telegraph(a)`, for each `due` `doors.Release(a, seed, target, writ, parent)` (= `SkylineBot.Spawn` door overload), a wreck `doors.CancelAll()`; the per-kind sound, light, colours, travel and blink rates are the pure table `DoorTelegraphPlan.cs` (sounds are existing clips as PLACEHOLDERS: lock_beep for the vent/hatch beacons, mine_lay/mine_arm for the shutter and the rail); logs `SKYLINE_DOOR telegraph|spawn|cancel <id>`; Unity EditMode tests in `Editor/Tests/SkylineSpawnDoorTests.cs`, not yet run) | Unity | M | `SkylineSpawnDoor.cs` | F2, W2, B1 |
| G1 | The gondola outside the glass (DONE 2026-10-14; pure in `Gondola.cs`: `FacadeHatches` (12 hatches z -22..22, `HatchSnap`, mine point x 34.9 y 2.5), `MineLayer` snaps to a free hatch on any façade rail by itself (`placement` Auto, `mineOffset` for other rails; M1 rails unchanged, the sim's inside copy lands on the same hatches), `FacadeGlass` (pane ids by wall point, `PaneCrossed`, broken set, open-edge cut 8 per 0.6 s, fall line y -12 = the sim's); Unity: `SkylineFacade` (`TryBreak(collider, point, dir)` for the shooting mask, `Break`, `Mend`, hatches slide on `MineEvent.Laid`, outside rail + trolley, `SpawnGondola(north, ...)`, edge cuts and fall wreck on the player), `SkylineMineView` bigger and hue `#FF2E63` for hatch mines with the trigger ring up while armed; needs the `facade/pane_break` sound recipe; X0 adds the component, A2 calls `TryBreak`) | pure + Unity | M | `Gondola.cs`, `SkylineMines.cs`, `Scripts/Floor/SkylineFacade.cs` | B1, F2 |
| L2 | Lobby runtime: desk, practice drone, lift ride | Unity | L | `Scripts/Floor/SkylineLobby*.cs` | L1, B2, M2 |
| X0 | `SkylineFloorDirector`: phases, waves, restart, beats, scene wiring | Unity | L | `Scripts/Floor/SkylineFloorDirector.cs`, `SkylineArenaBuilder.Floor.Director.cs` | W1, W2, W4, R2, SD1, G1, L2, H3, H4 |
| DR1 | Dressing: ring, shopfronts, walk-in shop interiors | content | M | `SkylineArenaBuilder.Floor.Shops.cs` | B1, D0, S0, G0 |
| DR2 | Dressing: the pit and the food court props | content | M | `SkylineArenaBuilder.Floor.Pit.cs` | B1, D0, S0, G0 |
| DR3 | Dressing: the service corridor (colour-free) | content | M | `SkylineArenaBuilder.Floor.Corridor.cs` | B1, D0, S0, G0 |
| DR4 | Dressing: galleries, bridge, escalators, façade, skylight, city | content | L | `SkylineArenaBuilder.Floor.Upper.cs` | B1, D0, S0, G0 |
| DR5 | Dressing: the lobby and staff hallway | content | M | `SkylineArenaBuilder.Floor.LobbyDress.cs` | B2, D0, S0, G0 |
| DM1 | The pilot: pure floor pilot | pure | L | `FloorPilot.cs`, tests | W3, F3 |
| DM2 | The camper: pure camper + the sim assertions | pure | M | `CamperBot.cs`, tests | W3, F3 |
| DM3 | The Unity demo and camper run | Unity | L | `SkylineFloorDemo*.cs`, `SkylineRunArgs.cs` | DM1, DM2, X0 |
| P1 | Perf and prewarm | Unity | M | `SkylinePrewarm.cs`, `SkylinePerfProbe.cs` | X0, DR* |
| J1 | Captures, stills, pass wiring, the judge brief | Unity + docs | M | `SkylineArenaBuilder.Floor.Stills.cs`, `tools/skyline/pass.ps1` (floor additions), `docs/skyline-judge/FLOOR1-BRIEF.md` | X0, DM3 |
| X1 | Switch-over (main session) | main | S | `SkylineScenes.cs`, deletions of M1 arena code | all |

### Dependency waves (tasks in one wave can run together; a task waits for every task in its Needs column)

- **Wave 0:** F0.
- **Wave 1 (all independent of each other, after F0):** F1, F2 (F2 can start beside F1), W1, W2, R1, L1, M1, A1, C1, V1;
  and, needing nothing, S0, D0.
- **Wave 2:** F3 (F1, F2), W3 (F1, F2, W1, W2, R1), W4, B1 (F1, F2), B2 (L1, B1 core: after B1 commits), H2 (V1), M2 (M1).
- **Wave 3:** H3, H4, A2, C2, R2, SD1, G1, L2.
- **Wave 4:** X0 (the director); H5 (needs A2); DM1, DM2 (they need only W3 and F3, so they can start in wave 3).
- **Wave 5:** DR1-DR5 (after G0), DM3, then P1, J1.
- **Main session:** P0 (re-measure perf) before wave 1, the pass after each wave, X1 last.

File-ownership rule for the shared partial class: the builder is `static partial class SkylineArenaBuilder`; every new
builder file is another partial named `SkylineArenaBuilder.Floor.<Part>.cs`, owned by exactly one task. The old
`SkylineArenaBuilder.cs` and `SkylineArenaDress.cs` are read-only for everybody until X1; helpers they define
(`Box`, `Group`, `Lit`, `Glass`, `Emissive`, `NoShadow`, `Mats`, `LoadMats`, `City`, `Tower`, `Still`) may be CALLED, not
changed. If a task needs a changed helper it writes its own copy in its own partial.

---

# Wave 0

### Task F0: Floor contracts, beat triggers, doc fixes (pure)

The shared vocabulary, so that every later task codes against names that already exist. Nothing here has behaviour beyond
small pure helpers.

**Files:**
- Create: `Packages/com.vibejam.jamkit/Runtime/Skyline/FloorTypes.cs`; test `Tests/Skyline/FloorTypesTests.cs`
- Modify: `Runtime/Skyline/Encounters.cs` (**only** the `BeatTrigger` enum: append `LobbyArrive, LobbyDesk, LobbyLiftNear,
  LiftRiding, FloorStart, CircleNear, MeterStarted, AssetTowed, WaveAnnounce, AlarmRaised, AllTowed, FirstSpecialAmmo`);
  `Runtime/Skyline/TeachingQueue.cs` (**only** `UrgencyOf`: `AssetTowed`, `MeterStarted`, `FirstSpecialAmmo` are
  `Reaction`; `WaveAnnounce`, `AlarmRaised`, `AllTowed`, `FloorStart` are `Notice`; `CircleNear`, `LobbyDesk`,
  `LobbyLiftNear` are `Lesson`; `LobbyArrive`, `LiftRiding` are `Hint`)
- Modify docs: `docs/SKYLINE-FLOOR-REPOSSESS.md` (section 3, "The clock starts when ... crosses from the warm-up": replace
  by "at the drop (section 9)"; section 4, "The warm-up hall stays": replace by "replaced by the lobby (section 9)"; add
  the D2 note on the south row); `docs/SKYLINE-HUD.md` and `docs/SKYLINE-BOTS.md` (delete the second copy of the
  duplicated sections "Feedback flashes and styling" and "Damage states")

**Interfaces (produces; namespace `JamKit.Skyline`, `System.Numerics.Vector3`):**
```csharp
public enum DoorKind { ShopShutter, SkylightVent, ServiceHatch, RailEnd }
public enum SocketGroup { Pit, Inside, High }
public enum AssetKind { Mascot, MassageChair, ArcadeCabinet }
public enum FloorPhase { Lobby, LiftRide, Floor, Finale, Done }
public enum SurfaceLevel { Pit, Ring, Gallery, Corridor, ShopInterior, Lobby }
public sealed class FloorSolid { public string id, role; public CoverBox box; public bool runnable, glass, ramp, rampAlongX;
    public float rampTopStart, rampTopEnd; public float TopAt(float x, float z); }   // role: floor wall rim shopfront column
                                                                                      // pilaster escalator ramp deck balustrade table planter lift facade ...
public sealed class SocketDef { public string id; public SocketGroup group; public Vector3 position; }
public sealed class SpawnDoor { public string id; public DoorKind kind; public Vector3 position; public float yawDeg; }
public sealed class AssetDef { public AssetKind kind; public string id, jobSheetName, description; }   // draft text
public sealed class FloorZone { public string id; public SurfaceLevel level; public float minX, maxX, minZ, maxZ, y; }
public static class FloorRules {
    public const float TowSeconds = 10f, CircleRadius = 3f, WaveSeconds = 30f, DoorTelegraphSeconds = 2f;
    public const int WaveCap = 10;
    public const float MinBearingSpread = 90f, MinFromLastWave = 120f, MinSpawnDistance = 15f, ViewExclusionRange = 25f;
    public const int MinExits = 2;
    public const float ExitRadius = 10f, MaxSeenShare = 0.6f, MinCircleExposure = 0.5f, ExposureBearingSpread = 90f;
    public static bool DoorAllows(DoorKind k, string enemyId);   // drone: shutter, vent; floor-care: shutter, hatch; gondola: rail end; spotlight: none
}
```

- [ ] **Step 1:** Tests `FloorTypesTests`: every `FloorRules` constant equals the number in this plan's Global
  constraints (a table test, so a later edit that drifts from the spec fails); `DoorAllows` for all 4 doors x 4 enemy ids;
  `FloorSolid.TopAt` on a flat box returns `box.max.Y`, on a ramp interpolates `rampTopStart..rampTopEnd` along z (or x when
  `rampAlongX`); `UrgencyOf` returns the classes above for every new value (and never throws for any enum value).
- [ ] **Step 2:** Write the file, extend the enum and `UrgencyOf`, edit the docs. Run `tools/skyline/test.ps1`. Commit
  (explicit paths).

**Acceptance:** green tests; no behaviour change to M1 (every existing test passes); the three docs read cleanly.
**Pass check:** none (pure).

**Status: done (2026-10-14).** The names above are exactly as built; the details the block left open are fixed in decision D19. The doc fixes are decisions D15 and D17.

---

# Wave 1 (all independent; start together)

### Task F1: The arena as data (pure)

The geometry of the Food Court from the layout sheet, as `FloorSolid`s, plus a test-only world built from the same data.
Everything else (the Unity builder, the lint, the sim, the pilot) reads this one source.

**Files:**
- Create: `Runtime/Skyline/RepossessArena.cs`; `Tests/Skyline/FloorWorld.cs` (test-only: `BoxWorld` from the solids);
  `Tests/Skyline/RepossessArenaTests.cs`

**Interfaces (produces):**
```csharp
public static class RepossessArena {
    public const float MinX = -35f, MaxX = 35f, MinZ = -28f, MaxZ = 28f, PitFloorY = -4f, RingY = 0f, GalleryY = 6f,
        SkylightY = 14f, RoofY = 16f, WallThick = 0.4f;
    public static readonly Vector3 ArrivalPoint;                    // (0, 0, -21): the alcove, where the drop lands
    public static FloorSolid[] Solids();                           // every collider, once; floors, walls, pit, ramps, shops, corridor,
                                                                    // galleries, bridge, escalators, façade, lift lobby, cab, alcove, tables...
    public static CoverBox[] SightBlockers();                      // non-glass, non-ramp solids (+ each ramp as its LOWER box); for CoverGeometry
    public static FloorZone[] Zones();                             // named areas with level and y (pit, ring-n/s/w/e, gallery-n/s, bridge,
                                                                    // shop-n1..s5, corridor-n/s/w, exit-lift, alcove, cab)
    public static CoverBox ExitLiftZone;                           // the Unit's feet here end the floor
    public static bool TryGroundY(float x, float z, float fromY, out float y);   // highest walkable top at or below fromY (ramps interpolated)
}
public static class FloorWorld { public static BoxWorld Build(); }   // in Tests/: maps each solid (ramps through BoxWorld.Ramp/RampX)
```
The numbers are the layout sheet above. **Hard rules for the data** (each is a test):

- [ ] **Step 1: tests (`RepossessArenaTests`)**
  - overall: interior 70 x 56; pit 28 x 20 at -4; galleries' tops 6 with a 3 m overhang over the ring (north deck from
    z 15, shops start at 18); bridge 4 m wide, glass, top 6; corridor 3.5 m wide; skylight 14; shops: north row 5, south
    row 4 + the alcove; exactly 2 walk-in per row with doorways >= 2.4 m;
  - no two solids penetrate by more than 0.02 m (the Unity lint's rule 2); every wall (>= 2 m tall, >= 3 m long) has a
    deliberate `runnable` value; the façade and the pit's retaining walls are runnable;
  - **cover is architecture:** in the ring and gallery zones no `role` of `table` or `planter`; no solid taller than 1.2 m
    that is not one of `rim shopfront column pilaster escalator wall lift facade deck`; tables and planters exist only in
    the pit and are <= 1.0 m high;
  - walking (real `MoveSim` over `FloorWorld.Build()`, a scripted walk, `MoveTestKit`): from the arrival point to each
    of the 4 pit ramps' feet and down; up each escalator to its gallery; across the bridge; through each walk-in shop
    (front door to back door); along the whole corridor U from the north-east hatch to the south-west part; every ramp's
    slope <= 40 degrees; the pit's chute can be slid down (a slide input from the ring);
  - `TryGroundY` agrees with `MoveSim`'s standing height at a 2 m grid of 40 sampled points;
  - `SightBlockers()` contains no glass and no ramp-as-wedge; a segment from a bridge point to the pit floor is clear.
- [ ] **Step 2:** write the data. If a walking test shows the sheet's numbers do not work, move numbers inside the
  envelope (never smaller than the spec's sizes) and list every change in the commit message.
- [ ] **Step 3:** `tools/skyline/test.ps1`; commit (explicit paths).

**Acceptance:** all above green. **Pass check:** the data feeds B1; nothing yet.

### Task F2: Sockets, spawn doors, circles, assets, routes (pure)

**Files:** Create `Runtime/Skyline/RepossessSites.cs`; `Tests/Skyline/RepossessSitesTests.cs`.

**Interfaces (produces):**
```csharp
public static class RepossessSites {
    public static SocketDef[] Sockets();                 // 10: pit-kiosk, pit-ne, pit-sw; inside-n2, inside-n4, inside-s2, inside-s4;
                                                         //     high-bridge, high-north-east, high-south-west
    public static SocketDef[] InGroup(SocketGroup g);
    public static Vector3 CircleCenter(SocketDef s);     // the socket's floor point (the asset stands above it)
    public static SpawnDoor[] Doors();                   // the 17 doors of the layout sheet
    public static SpawnDoor[] DoorsFor(string enemyId);  // by FloorRules.DoorAllows
    public static AssetDef[] Assets();                   // three drafts: mascot, massage-chair display, arcade cabinet
    public static RouteDef[] Routes();                   // clean-line routes (below)
}
public sealed class RouteDef { public string id; public Vector3[] waypoints; public MoveHint[] hints; }
public struct MoveHint { public int atWaypoint; public MoveHintKind kind; }   // Walk Slide Mantle WallRun DoubleJump
```
Routes: one clean line from the arrival point to each socket group's deepest socket (pit-sw, inside-n4, high-bridge,
high-south-west and high-north-east), the lift run from the pit to the exit lift, and the façade wall-run route (east ring,
wall-run north along x 35, mantle onto the north gallery's lip at z 15). They are lists of waypoints with hints, used by F3
and by the pilot.

- [ ] **Step 1: tests:** 10 sockets, 3 groups (3, 4, 3); every circle fits (>= 90% of 24 sample points on the circle's
  disc are standable per `RepossessArena.TryGroundY` at the socket's height); no door within 6 m of any socket circle
  centre or inside a walk-in shop; vent heights 13..14, hatches at floor 0, rail ends outside the façade (x > 35);
  `DoorsFor(drone)` has 5 + 14 doors, `DoorsFor(floor-care)` 5 + 6, `DoorsFor(gondola)` 2, `DoorsFor(spotlight)` none;
  every door's `yawDeg` faces into the hall; ids unique; every route's waypoints are standable and consecutive
  waypoints are within 12 m unless the hint says `WallRun`/`DoubleJump`.
- [ ] **Step 2:** write the data (needs F1 only for the final standable checks; until F1 lands those tests are
  `[Ignore]`d with the reason, and un-ignored by the task that lands F1). Commit.

**Acceptance:** green. **Pass check:** none.

### Task W1: The wave clock, composition and cap (pure)

**Files:** Create `Runtime/Skyline/WaveClock.cs`; `Tests/Skyline/WaveClockTests.cs`.

**Interfaces (produces):**
```csharp
public sealed class WaveDef { public int number; public float time; public string[] enemies; }
public static class WavePlans {
    public static WaveDef For(int number, bool gondolaAlive);   // Normal: the table of the Global constraints; 5+ alternates A/B;
                                                                //   a gondola on every 4th wave from 8 when none alive (D9)
    public static string[] OpenLayersCheck(WaveDef w);          // null when the wave obeys the aim rule + at most two layers
}
public sealed class WaveClock {
    public WaveClock(int cap = FloorRules.WaveCap, float interval = FloorRules.WaveSeconds);
    public bool Running { get; } public int Released { get; } public int Skipped { get; } public WaveDef Waiting { get; }
    public float SecondsToNext { get; }
    public void Start(); public void Stop(); public void Reset();
    public void Tick(float dt, int living, bool gondolaAlive, List<WaveDef> released);   // appends every wave released this call, in order
}
```
Rules: `Start` makes wave 1 due at once; wave n is due at (n-1) x 30 s of running time; the clock is never delayed by
anything; `Tick` with a large `dt` releases every due wave (a wave is "due" only once; hitch-safe); a due wave whose size
would take `living` past the cap becomes `Waiting` (at most one); each later due wave while one waits is skipped (D9);
the waiting wave is released the first tick `living + size <= cap`; `Stop` (third tow) cancels the waiting wave; `Reset` (a
wreck) clears everything.

- [ ] **Step 1: tests:** the table (waves 1-4 compositions, 5+ alternation, gondola cadence and "none alive"); due times
  with dt = 0.016, 0.5 and 31 (identical release sequence); the cap (living 9, wave of 4 waits, released at living 6;
  the next interval while waiting is skipped and `Skipped` counts it); the clock keeps its own time while waiting;
  `Stop`/`Reset`; **every plan wave obeys the aim rule and covers at most two layers** (`EncounterRules`-style over
  `SkylineEnemies.Find`); `Tick(0)`, negative, NaN, infinity dt do nothing.
- [ ] **Step 2:** write. Commit.

**Acceptance:** green. **Pass check:** none (X0 wires it).

### Task W2: Spawn bearings, door choice and telegraphs (pure)

**Files:** Create `Runtime/Skyline/SpawnBearings.cs`; `Tests/Skyline/SpawnBearingsTests.cs`.

**Interfaces (produces):**
```csharp
public struct SpawnQuery { public Vector3 player; public float viewYawDeg, viewHalfFovDeg;   // the camera's horizontal half-fov (default 55)
    public float? lastMeanBearing; public Vector3? alarmAsset; public WaveDef wave; public ulong seed;
    public IReadOnlyList<string> busyDoors; }                                                  // doors with a telegraph running
public sealed class SpawnAssignment { public string enemy; public SpawnDoor door; }
public sealed class SpawnChoice { public List<SpawnAssignment> assignments; public float meanBearing; public int relaxed;  // 0 none, 1 dropped 120, 2 dropped view
    public bool ok; }                                                                           // !ok: wait (no legal choice)
public static class SpawnBearings {
    public static float Bearing(Vector3 from, Vector3 to);          // degrees, 0 along +Z (as EncounterRules.Bearing)
    public static SpawnChoice Choose(SpawnQuery q, IReadOnlyList<SpawnDoor> doors);
}
public sealed class WaveSpawner {                                    // the 2 s telegraph
    public void Order(SpawnChoice c, float now);
    public void Tick(float now, List<SpawnAssignment> due, List<SpawnAssignment> telegraphStarted);   // telegraph starts at Order, bots due at +2.0 s
    public bool Busy(string doorId); public void Cancel();                                       // Cancel: a wreck
}
```
Rules: bots of the wave are split between **two doors** whose bearings (from the player) differ by **>= 90 degrees**, every
bot on a door that `DoorAllows` it; the first door's bearing is **>= 120 degrees** from `lastMeanBearing` (when set);
every door is **>= 15 m** from the player and, if within **25 m**, outside the view cone; a gondola wave's rail end counts
as a bearing point; with `alarmAsset` set, the candidate pairs are ranked by distance to that asset (nearest first) among
those that obey every rule; ties break on the seeded choice. Fallback ladder when no pair obeys everything: drop the 120
(relaxed 1), then the view rule within 25 m (relaxed 2); the 15 m rule and the 90-degree spread are never dropped
(`ok = false`: the wave waits and the caller retries next frame, for at most 5 s, then X0 logs `SKYLINE_WAVE stuck`).
`meanBearing` is the circular mean of the used doors' bearings.

- [ ] **Step 1: tests:** a table of hand-made cases (player in the middle of the ring; each rule alone); the property
  test: for the player at every standable point of a 2 m grid over `RepossessArena` (F1) and 5 view yaws, `Choose`
  returns either `ok` with all rules obeyed, or `!ok` — never an `ok` that breaks 15 m or 90 degrees; `relaxed` is
  recorded; alarm: the chosen doors are the nearest legal ones to the asset; two successive waves differ by >= 120
  degrees when possible; seeded determinism; the 2 s telegraph (`Order` at t, `due` at t + 2.0 within a frame, a
  `Cancel` drops pending orders); `busyDoors` are skipped. (Until F1 lands, use a small hand-made door list; the grid
  test is added when F1 and F2 exist.)
- [ ] **Step 2:** write. Commit.

**Acceptance:** green. **Pass check:** none.

### Task R1: Tow meter, repossession objective, floor flow (pure)

**Files:** Create `Runtime/Skyline/Repossession.cs`, `Runtime/Skyline/FloorFlow.cs`; tests `RepossessionTests.cs`,
`FloorFlowTests.cs`.

**Interfaces (produces):**
```csharp
public sealed class TowMeter { public float Progress {get;} /*0..1*/ public bool Started {get;} public bool Done {get;}
    public void Tick(float dt, bool inCircle); public void Reset(); }          // fills 1/TowSeconds per second inside; pauses outside; never drains
public sealed class JobEntry { public AssetDef asset; public SocketDef socket; public TowMeter meter; public bool Towed => meter.Done; }
public sealed class Repossession {
    public Repossession(IReadOnlyList<AssetDef> assets, ulong seed);            // picks one socket per group, assets shuffled across groups
    public IReadOnlyList<JobEntry> Sheet { get; }
    public int TowedCount { get; } public bool AllTowed { get; }
    public int ActiveCircle { get; }                                            // the entry whose circle holds the feet, else -1
    public event Action<JobEntry> MeterStarted, Towed; public event Action AllTowedEvent;
    public void Tick(float dt, Vector3 feet);                                   // 3D: horizontal distance <= CircleRadius and |dy| <= 2.5
    public void Reset();                                                        // a wreck: meters to 0, assets back, SAME sockets
    public static SocketDef[] Pick(ulong seed);                                 // one per group
}
public sealed class FloorFlow {                                                 // lobby -> lift -> floor -> finale -> done
    public FloorPhase Phase { get; }
    public event Action<FloorPhase> Entered;
    public void SteppedIntoLift(); public void DoorsOpened(); public void AllTowed(); public void ReachedExit(); public void Wrecked();
}
```
`FloorFlow` rules: `SteppedIntoLift` only from Lobby; `DoorsOpened` only from LiftRide (starts Floor: the wave clock's
owner reacts); `AllTowed` only in Floor (Finale: waves stop, lift opens); `ReachedExit` only in Finale (Done); `Wrecked`
in LiftRide, Floor or Finale returns to Lobby and raises an event the director uses to reset everything; out-of-order calls
are ignored and logged-by-return (`bool`).

- [ ] **Step 1: tests:** meter: 10.0 s inside fills it however dt is cut; leave at 4 s and return: resumes at 4 s;
  standing on the edge alternately in/out accumulates only inside time; never drains; `Done` is sticky; `Reset`.
  Repossession: `Pick(seed)` returns one socket per group, deterministic, and over 500 seeds covers every socket of every
  group and every asset-to-group permutation; `MeterStarted` fires once per asset the first tick its meter moves
  (that is the alarm); `Towed` once; `AllTowedEvent` once when the third completes; the active circle is the one
  containing the feet and a player standing where two circles overlap (none do; test uses a synthetic overlap) fills the
  nearer; a far-above player (the pit kiosk circle seen from the bridge, dy 6) does not fill; `Reset` keeps the sockets.
  FloorFlow: the legal sequence; every illegal call ignored; `Wrecked` from each live phase; restart path ends in Lobby.
- [ ] **Step 2:** write. Commit.

**Acceptance:** green. **Pass check:** none.

### Task L1: Lobby data and the floor's teaching beats (pure)

**Files:** Create `Runtime/Skyline/HotelLobby.cs`, `Runtime/Skyline/FloorBeats.cs`; tests `HotelLobbyTests.cs`,
`FloorBeatsTests.cs`.

**Interfaces (produces):**
```csharp
public static class HotelLobby {
    public static readonly Vector3 PlayerStart;                 // (0, 0, -76), facing +Z: the staff hallway
    public static readonly Vector3 PracticeBot, BoostStackTop, PracticeCounterTop, DeskCenter, ChandelierPosition;
    public static readonly CoverBox WallRunLockers; public static readonly Vector3 WallRunStart;
    public static readonly CoverBox LiftCab;                    // the Unit's feet here = SteppedIntoLift (after the doors have opened)
    public static FloorSolid[] Solids();                        // hallway, lobby shell, mezzanine + stairs, desk, carts, stack, counter, lockers, cab
    public static FloorZone[] Zones();                          // hallway, lobby, mezzanine, cab
}
public static class FloorBeats {
    public static TeachingBeat[] Floor1();                      // the beats below, as draft lines (<= TeachingBeats.MaxChars = 140 each)
}
```
Beats (ids, speaker, trigger; every text a draft except the quoted ones): `concierge-greeting` (Concierge,
`LobbyDesk`, on arriving within 8 m of the desk in sight), `nan-lobby` (Nan, `LobbyArrive`, "Unit's live..." the hallway
opener), `dot-writ-spots` / `dot-writ-spot-made` / `dot-breacher-boost` / `dot-boost-done` / `nan-double-jump-mantle` (the
M1 lessons, kept, moved to the lobby: same triggers `PracticeBotSeen`, `FirstSpotCreated`, `NearBoostLedge`,
`BoostLedgeReached`, `NearPracticeLedge`), `nan-job-sheet` (Nan, `LiftRiding`: the job sheet, the tow circle, "the clock's
running, love"), `concierge-first-wave` (`FloorStart`), `dot-clamped` (Dot, `AssetTowed`, the FIRST tow only, when the tow ends (the creator, 2026-10-14): "Clamped! Get Moving!"), `dot-stay-near` (Dot, `LeftCircleMidTow`: X0 fires it once per floor, the first time the Unit leaves an asset's circle while that tow's meter is above 0 and not done: "Don't go too far, you need to stay nearby to transmat!"),
`concierge-alarm` (`AlarmRaised`), `dot-towed` (Dot, `AssetTowed`, repeats; X0 skips it on the first tow, which `dot-clamped` answers), `concierge-wave` (`WaveAnnounce`, repeats,
at most every 3rd wave), `concierge-concede` (`AllTowed`: "Asset transfer... acknowledged. Under protest." verbatim),
the three bot notices (drone, floor-care verbatim as M1; gondola re-worded for "outside the glass"), `dot-plating`
(verbatim "Strip it! Scrap's armour!"), `dot-special-ammo` (`FirstSpecialAmmo`).

- [ ] **Step 1: tests (`HotelLobbyTests`):** the lobby's interior is >= 12 m from the boost stack's footprint to every
  wall (carries `GalleriaArena.LedgeWallClearance`); the stack and the practice counter are >= 12 m apart edge to edge;
  the lockers' top (4.6) is out of a jump + double jump + mantle (4.15) and the counter's (3.5) is within a double-jump
  mantle (reuse M1's `WallRunLedgeTests` and `ArenaDemoBoostTests` pattern against the lobby coordinates, with `BoxWorld`
  built from `Solids()`); the practice bot's gantry clears the ceiling; the hallway is 3 m wide and 18 m long; the cab is
  4 x 4 and its north face is the hall's south wall line; no overlapping solids (> 0.02 m).
- [ ] **Step 2: tests (`FloorBeatsTests`):** unique ids; every text <= 140 chars; the verbatim lines are exact and
  `draft == false`; every trigger is mapped by `TeachingQueue.UrgencyOf`; Nan's job-sheet line fits inside the riser's
  length (the lift ride's riser + pre-drop is >= `TeachingQueue.LineSeconds(text) + 1`, read from M1's plan once M1 lands).
- [ ] **Step 3:** write; add the new floor section with the draft lines to `docs/SKYLINE-DRAFT-LINES.md`. Commit.

**Acceptance:** green. **Pass check:** none.

### Task M1: The music sequencer, the lift-ride plan, the layers (pure)

**Files:** Create `Runtime/Skyline/MusicSequencer.cs`, `Runtime/Skyline/MusicLayers.cs`; tests `MusicSequencerTests.cs`,
`MusicLayersTests.cs`.

**Interfaces (produces):**
```csharp
public sealed class MusicSectionDef { public string id; public double bpm; public int beatsPerBar = 4, bars; public bool loops;
    public double BarSeconds => beatsPerBar * 60.0 / bpm; public double Seconds => bars * BarSeconds; }
public static class MusicSections {
    public const string Muzak = "muzak", Riser = "riser", PreDrop = "pre-drop", Drop = "drop", MainLoop = "main", CalmLoop = "calm";
    public static MusicSectionDef[] Placeholders();   // draft numbers: muzak 86 bpm 8 bars loops; riser 2 bars; pre-drop 2 bars; drop 1 bar -> main;
                                                      // main 122.5 bpm 16 bars loops; calm loops. Replaced by the composed stems' real values.
}
public struct LiftRidePlan { public double riserAt, preDropAt, dropAt, doorsStartOpeningAt, doorsClosedBy; }
public struct MusicChange { public string from, to; public double at; }
public sealed class MusicSequencer {
    public MusicSequencer(IReadOnlyList<MusicSectionDef> defs);
    public string Current { get; } public double CurrentStart { get; } public string Pending { get; } public double PendingAt { get; }
    public void Start(string id, double dspTime);
    public double Request(string id, double now);                    // quantised to the CURRENT section's next bar line (>= now + MinLead); returns the DSP time
    public bool Tick(double now, out MusicChange change);            // true when a scheduled change has started or a non-looping section ended into its Next
    public LiftRidePlan PlanLiftRide(double now);                    // riser on the muzak's next bar line; drop = riserAt + riser + pre-drop;
                                                                     //   the doors START opening DoorLead (1.2 s) before the drop so they are open ON it
    public double BarIndex(double now);
    public const double MinLead = 0.25, DoorLead = 1.2;
}
public sealed class ThreatScore { public static float Compute(IReadOnlyList<ThreatSample> bots); }   // alerted bots in range, weighted by type
public sealed class LayerSmoother { public float Value {get;} public void Tick(float target, float dt, float seconds); }  // critically damped, seconds not frames
public sealed class MusicLayers { public float motion, danger, light; public void Tick(float speed, float threat, bool traversal, float dt); }
```

- [ ] **Step 1: tests:** a request mid-bar returns the next bar line, one exactly on a bar line goes to the NEXT one only
  when inside `MinLead`; after 100 loops of the 8-bar muzak the section start is `start + 100 x length` to 1e-9 s (no
  drift; DSP times in the hours); tempo differs per section (86 then 122.5): a request quantises to the section it is
  made in; the lift plan for 50 trigger times across a bar: `dropAt` is `riserAt + riser.Seconds + preDrop.Seconds`
  exactly, `doorsStartOpeningAt = dropAt - DoorLead`, and the wait from stepping in to `riserAt` is < one muzak bar;
  a second request replaces a pending one (the later wins); `Tick` is monotonic and reports each change once.
  Layers: danger rises with alerted bots near, light rises when the threat is low or `traversal`, the smoother reaches 63%
  in `seconds` regardless of dt; no layer jumps by more than the smoothing allows on a single-bot flicker.
- [ ] **Step 2:** write. Commit.

**Acceptance:** green. **Pass check:** none (M2 plays it).

### Task A1: Special-ammo ledger and the finite reserve hook (pure)

**Files:** Create `Runtime/Skyline/SpecialAmmo.cs`, `Tests/Skyline/SpecialAmmoTests.cs`. Modify `Runtime/Skyline/SkylineGun.cs`
(additive: `GunDef.special` bool; in the constructor `_weapon.infiniteReserve = !def.special`) and
`Runtime/Skyline/SkylineGuns.cs` (`Breacher()` sets `special = true`, nothing else). **Do not touch the Breacher's other
numbers** (the creator is redesigning it).

**Interfaces (produces):**
```csharp
public sealed class SpecialAmmoDef { public int perPrimaryKill = 1, killsPerShot = 3, start = 2, max = 8; }   // draft numbers, balanced in play
public sealed class SpecialAmmoLedger {
    public SpecialAmmoLedger(SpecialAmmoDef def = null);
    public int Shots { get; }
    public int Credit(bool killedWithSpecial);         // returns shots added by this kill (0 or more); a special kill adds 1 per 3 kills
    public bool TryConsume(); public void Reset();
    public event Action<int> Gained;
}
```
The ledger's `Shots` is the Breacher's reserve: a small adapter (`SpecialAmmoLedger.Bind(SkylineGun)`) calls
`WeaponState.SetReserve`/`AddReserve`.

- [ ] **Step 1: tests:** 1 shot per primary kill; a special kill counter: kills 1, 2 give 0, the 3rd gives 1 (and the
  counter persists across wrecks of the same run); `max` caps and the excess is ignored (not banked); `Reset` restores
  `start` and zero the counter; the Writ (not `special`) still has `infiniteReserve`; the Breacher with 0 reserve
  dry-fires; `AddReserve` on an `infiniteReserve` gun does nothing; all M1 gun tests still pass.
- [ ] **Step 2:** write. Commit.

**Acceptance:** green. **Pass check:** none (A2 wires it).

### Task C1: Comic lettering core (pure)

**Files:** Create `Runtime/Skyline/ComicLettering.cs`, `Tests/Skyline/ComicLetteringTests.cs`.

**Interfaces (produces):**
```csharp
public enum LetterEvent { Kill, WeakPoint, WritSpotBreak, BreacherShove }
public sealed class LetterRequest { public string text; public string botId, gunId; public float seconds; public int size; }  // size 1..3
public sealed class ComicLettering {
    public const int MaxOnScreen = 2; public const float MinGap = 0.35f;
    public ComicLettering(ulong seed);
    public LetterRequest Pick(LetterEvent e, string botId, string gunId, float now, float onScreenCount);   // null when over the cap or inside the gap
    public static string[] Words(string botId, string gunId, LetterEvent e);                                // the draft word sets
}
```
Draft words (the creator rewrites): drone kill "KZZT!" / "BZZT-CRK!" / "ZAP!"; floor-care kill "SPLORCH!" / "GLORP!" /
"SPLAT!"; gondola kill "CLANG!" / "KA-SPLASH!"; Writ spot break "OBJECTION!" / "SUSTAINED!" / "OVERRULED!"; a weak-point hit
(not a kill) "CRACK!" / "TINK!"; Breacher shove "WHUMP!" / "BWOOM!". Hits that are not kills, weak points, spot breaks
or big shoves ask for nothing. A "big shove" is `knockback >= 8 m/s` (data).

- [ ] **Step 1: tests:** words come from the right sets; the same word is not picked twice in a row for one bot type;
  never more than 2 at once; a request inside 0.35 s of the previous is refused; seeded determinism; unknown bot/gun ids
  fall back to a generic word set; the sizes (kill 3, spot break 3, shove 2, weak point 1).
- [ ] **Step 2:** write. Commit.

**Acceptance:** green. **Pass check:** none.

### Task V1: HUD models (pure)

**Files:** Create `Runtime/Skyline/HudModels.cs`, `Tests/Skyline/HudModelsTests.cs`.

**Interfaces (produces):**
```csharp
public sealed class HealthBarModel {                         // chassis = the bar, plating = the layer over it
    public HealthBarModel(float chassisMax, float platingMax);
    public float ChassisFrac {get;} public float PlatingFrac {get;} public float LowPulse {get;} /*0..1 sine 1.6 Hz when chassis < 0.35*/
    public float EdgeTint {get;}                              // screen-edge red 0..1, follows LowPulse
    public int ShatterEvents {get;}                           // increments when plating hits 0 from > 0
    public void Set(float chassis, float plating); public void Tick(float dt);
}
public struct EdgeMarkerResult { public bool onScreen, behind; public float x01, y01, angleRad, distance; }
public static class EdgeMarker { public static EdgeMarkerResult Compute(Vector3 cam, Vector3 forward, Vector3 up, float vFovDeg, float aspect,
    Vector3 world, float margin01 = 0.06f); }                 // on screen: its own place; off screen or behind: clamped to the margin ring, angle pointing at it
public enum SubtitleMode { Off, Dialogue, DialogueAndBarks } public enum SubtitleSize { Small, Medium, Large }
public sealed class SubtitleLine { public string speaker, text; public bool pa; public float expires; }
public sealed class SubtitleBuffer {                          // at most two lines; bark lines only in DialogueAndBarks
    public void Say(string speaker, string text, bool pa, bool bark, float now, float seconds, SubtitleMode mode);
    public IReadOnlyList<SubtitleLine> Visible(float now);
    public static float FontPx(SubtitleSize s);              // 28 / 34 / 42 at the 1080p reference
}
public static class HudText { public static string WaveClock(float seconds); /* "NEXT 0:21" */ public static string Distance(float metres); /* "34 m" */ }
public static class SpeakerColours { public static (float r, float g, float b) Of(string speaker); }   // DOT, NAN, CONCIERGE; not the hostile hue
```

- [ ] **Step 1: tests:** health model: pulse only below 0.35 chassis, shatter fires once per break and again after the
  plating refills and breaks; `Set` out of range / NaN clamps; edge marker: a point dead ahead is on screen at the
  centre, one to the right off screen at the right margin with angle 0, one behind clamps to the ring with `behind`,
  distance is 3D metres; subtitle buffer: a third line pushes out the oldest, expired lines vanish, a bark under
  `Dialogue` is dropped, `Off` shows nothing, PA lines flagged italic; formats ("NEXT 0:05", "NEXT 0:00", "34 m", "1.2 km"
  never needed); no speaker colour lies in the hostile red-magenta band (hue within 20 degrees of `#FF2E63`).
- [ ] **Step 2:** write. Commit.

**Added to V1 (section 10 of `SKYLINE-FLOOR-REPOSSESS.md`, the wreck death sequence):** a pure `WreckSequence` timing model in the same file: `WreckSequence(string quote | int length)`, `Sample(t) -> WreckFrame` (phase Stagger 0.7 s, Fall 0.9 s, Lie 0.9 s, FadeOut 0.8 s, Quote, FadeIn 1.2 s, Done; `stagger01`, `fall01`, `blackAlpha`, `quoteAlpha`, `worldMayReset` (from full black), `liftRideRunning` (from the start of the fade-in)), `QuoteHold(length) = clamp(1.5 + 0.06 * length, 2, 8)` s, `TotalSeconds`. The director plays the pose from `stagger01`/`fall01`, draws the black and the quote, resets the world at `worldMayReset` and starts the lift ride (music sync) at `liftRideRunning`.

**Acceptance:** green. **Pass check:** none (H2-H5 draw them).

### Task S0: Ink roles: the style sheet's hues, wreck outline timing, x-ray objective, hostile-hue lint (Unity)

Resolves D3, D4 (and D11). Tests live in `Editor/Tests/` (the `JamKit.SkylineRepo.Tests` asmdef).

**Files:**
- Modify: `Look/Ink/InkLit.shader` (mask pass hue table and an x-ray draw), `Look/Ink/InkComposite.shader` (composite of
  the x-ray mask at lower strength), `Scripts/Ink/SkylineInkRoles.cs`, `Scripts/Ink/SkylineInkFeature.cs`,
  `Scripts/Bots/SkylineWreck.cs`, `Editor/Look/SkylineInkConvert.cs` (only the hue lint hook),
  `Editor/Tests/SkylineInkShaderTests.cs`, `Editor/Tests/SkylineInkFeatureTests.cs`
- Create: `Editor/Look/SkylineHueLint.cs`, `Editor/Tests/SkylineHueLintTests.cs`

**Interfaces:**
- Roles (`SkylineInkRoles`): keep `None 0, Hostile 1, Objective 2, Scrap 3 (now the strippable wreck), Viewmodel 4, WeakSpot 5,
  BotWeakPoint 6`; add `ObjectiveXray 7`. Hues: Hostile `#FF2E63`, Objective `#B36BFF`, Scrap `#8FD3FF`, WeakSpot `#FFD60A`.
  `public const string ObjectiveXrayPass`.
- `SkylineWreck`: `public static event Action<SkylineWreck, float> Stripped` (plating gained); the outline strength
  `OutlineStrength(float freshLeft)` (full while more than 3 s of the 8 s window are left, thinning linearly to 40% at 0.4 s,
  a flash-out in the last 0.4 s, then 0) as a **pure static** function (`SkylineWreckOutline.Strength(left, window)`
  in `Runtime/Skyline/` is better: pure and testable; the wreck calls it each frame through a material property block).
- `SkylineHueLint.Run(Scene) : List<string>`: any scenery material (not in `Resources/SkylineFx` role materials, not a bot)
  whose colour or emission hue is within 20 degrees of `#FF2E63` and saturation > 0.4, or a light of such a hue, is a
  problem line.

- [ ] **Step 1: tests:** the pure outline function (strength 1 until the window's last 3 s, monotonic down, zero after
  the window, a flash pattern in the last 0.4 s, `freshLeft` NaN gives 0); shader tests updated for the new hues and the
  new role (`Material.FindPass`, property presence); `ObjectiveXray` is not drawn with the depth test (a material
  keyword/pass check); hue lint flags a synthetic `#FF2E63` sign and passes the existing look library (run on the old
  arena build in the test via `SkylineLookLibrary.Get` only).
- [ ] **Step 2:** change the shader tables and roles; add the x-ray draw (ZTest Greater against the depth, 45% alpha,
  objective hue only); wire the wreck; keep the build rule (a role keyword is saved on a build-included material:
  `SkylineInkConvert.EnsureResourceMaterials` covers the new role).
- [ ] **Step 3:** `tools/skyline/test.ps1` (compile check only; shaders are checked in the pass).

**Acceptance:** compile + pure tests green. **Pass check (main session):** the pass compiles the shader; a still shows a
violet objective outline through a wall at 45%, a light-blue wreck thinning over 8 s, red-magenta hostile unchanged;
`-skyline-render-diag` lists the new role material with its keywords.

### Task D0: Materials, palette and the colour-free zone helper (Unity editor)

**Files:** Modify `Editor/Look/SkylineLookLibrary.cs`, `Editor/Look/SkylineInkPalette.cs`, `Editor/Look/SkylineInkConvert.cs`
(only a `MakeGrey(Transform, params string[] excludeNames)` generalisation and a `GreyWithLight` helper, no behaviour change
to existing calls), `tools/skyline/look/make-textures.py`; tests `Editor/Tests/SkylineInkConvertTests.cs` (extend).

**What:** the materials the floor's dressing needs, each with a palette row and a tiling (named so the DR tasks only call
`SkylineLookLibrary.Get(name)`): `Neon_<Hue>` glow materials for shop signs (Teal, Amber, Lime, Orange, Blue, Pink-not-red:
none within 20 degrees of the hostile hue), `Glass_Bridge` (walkable glass with a faint frit), `Glass_Facade`, `Shutter_Steel`
(grey roll-up), `Conduit`, `Pipe_Grey`, `Tile_Kitchen`, `Carpet_Hotel`, `Marble_Lobby`, `Wood_Panel_Lobby`, `Luggage` (props
atlas), `Chandelier_Gold`, `Hazard_Grey` (the corridor's hazard stripes in greys), signage atlas rows for the new shop and
lobby signs. Tile sizes in metres as for the existing catalogue. Procedural where possible (`make-textures.py`), else Poly
Haven CC0 (log in `CREDITS.md`). Every material has a `__Grey` twin path through `MakeGrey`.

- [ ] **Step 1:** tests: each new name resolves; each has a palette row; `MakeGrey` on a sample root leaves emissive
  materials coloured and every other twin `_Desaturate == 1`; no new material violates the hue lint (S0's lint).
- [ ] **Step 2:** write; run `tools/skyline/test.ps1`.

**Acceptance:** green. **Pass check:** the pass's texture/material rebuild runs; the catalogue page in `SKYLINE-LOOK.md`
gets the new rows (the task adds them).

---

# Wave 2

### Task F3: Layout lint, navigation graph, base-kit routes (pure)

The four measured rules, run over the arena data. This is the gate that tells the layout is a level and not a room.

**Files:** Create `Runtime/Skyline/ArenaNav.cs`, `Runtime/Skyline/ArenaLint.cs`; tests `ArenaNavTests.cs`, `ArenaLintTests.cs`,
`BaseKitRouteTests.cs`.

**Interfaces (produces):**
```csharp
public sealed class ArenaNav {                                        // a walkable graph over the arena's tops
    public static ArenaNav Build(IReadOnlyList<FloorSolid> solids, float cell = 2f);
    public int Count { get; } public Vector3 PositionOf(int node); public int NodeAt(Vector3 p);
    public IReadOnlyList<int> Neighbours(int node);                   // 8-neighbour steps up to 0.35 m, ramps by slope <= 45 degrees
    public List<int> Path(int from, int to);                          // A*, shortest, null when unreachable
}
public sealed class LintResult { public List<string> problems; public int standable, failedExits, failedSight, failedCircles; }
public static class ArenaLint {
    public static LintResult Run(IReadOnlyList<FloorSolid> solids, IReadOnlyList<SpawnDoor> doors, IReadOnlyList<SocketDef> sockets);
    public static int Exits(ArenaNav nav, int node, float radius);                        // vertex-disjoint routes out of the radius (max-flow, capped at 2)
    public static float SeenShare(IReadOnlyList<CoverBox> blockers, Vector3 eyePoint, IReadOnlyList<SpawnDoor> doors, float range = 60f);
    public static float CircleExposure(IReadOnlyList<CoverBox> blockers, ArenaNav nav, SocketDef s, out int exposedSectors);
}
```
Rule operationalisations (a plan decision, logged here for the creator): (1) **exits:** from each standable node, remove
the node itself, then find at most 2 vertex-disjoint paths to nodes >= 10 m (3D distance) away; the node passes with 2.
(2) **seen share:** from eye height (feet + 1.6 m) to each door's centre (+1.5 m), with `CoverGeometry.Blocked` over
`SightBlockers()`; the node passes when it sees at most 70% of the ground-level doors (shutters, hatches, rail ends: 13 of the 27; the roof vents are excluded by design, D23) ("sees" = an unblocked line within 60 m). (3) **circle exposure:** for a socket, 8 sectors of 45 degrees around the
circle's centre; a sector "exposes" the circle when some standable node 8-30 m from the centre inside the sector sees
>= 50% of 25 sample points on the disc (24 on the rim + centre) at height 1 m; the circle passes when two exposing sectors
are >= 90 degrees apart. (4) **finishable with the base kit:** `BaseKitRouteTests` (below).

- [ ] **Step 1: tests:** nav graph: the arrival, every socket and the exit lift are mutually reachable on foot; the pit is
  reachable by 4 ramps and the chute (chute downward only); each gallery reachable by an escalator and the bridge; no
  island nodes > 4 (`standable` minus the largest component). Lint on synthetic mini-levels that fail each rule (a dead-end
  closet fails exits; an open room fails seen-share; a circle behind one wall fails exposure) and pass; then the real arena:
  `ArenaLint.Run(...)` has **zero** problems, or the task records the failing points as a documented list in the test
  (`Assert.That(problems, Is.Empty)` is the target; if after three layout tries (F1 numbers moved inside the envelope)
  a handful remain, the test fails with the list and the task hands back for the main session to ask the creator).
  `BaseKitRouteTests`: with the real `MoveSim`/`FloorWorld`, a scripted bot (the `ScriptedRouteBot` pattern in
  `EncounterDodge.cs`, extended with the hints) runs (a) arrival -> every socket on foot, (b) the façade wall-run route
  onto the north gallery (a wall-run + a kick + a mantle) , (c) the slide chute, (d) the double-jump mantle onto a gallery
  from the ring where a gallery lip is within reach; each ends within 1.5 m of its goal inside a time limit.
- [ ] **Step 2:** write. Performance: the lint over the whole arena at a 2 m grid completes in < 10 s on the test machine
  (tune the grid, cap flows at 2 augmentations, cache door visibility).
- [ ] **Step 3:** commit. Optionally add `Editor/SkylineLint.cs` hook (owned by B1) to print the same result in the pass.

**Acceptance:** green with zero lint problems on the real arena. **Pass check:** `editor-batch.json` `lint` block lists
these problems (B1 wires it) and is empty.

### Task W3: The headless floor: `RepossessSim`, `FightArea`, `AddEnemy` (pure)

The refactor that makes the pure sim multi-wave and multi-level. This is the riskiest pure task: `FightSim.cs` is the
M1 fight's reference and 1000 lines long.

**Files:** Create `Runtime/Skyline/RepossessSim.cs`; tests `RepossessSimTests.cs`. Modify (additive, defaults preserve
M1 behaviour): `Runtime/Skyline/FightSim.cs` (a `FightArea` struct passed to the constructor with a default equal to the old
hall: `Hall` + `SearchMaxZ`; a public `FightEnemy AddEnemy(Spawn s, int index)` extracted from the constructor loop; `Tick`
must not end the fight when it has no enemies yet), `Runtime/Skyline/FightBots.cs` (`TacticalBot`'s grid at lines
479-480 reads `fight.Area`), `Runtime/Skyline/IdleWork.cs` (line 202: `HoverPatrol.LeavesArea` with the area).

**Interfaces (produces):**
```csharp
public struct FightArea { public float minX, maxX, minZ, maxZ, searchMaxZ; public static FightArea Galleria; public static FightArea Floor1; }
public interface IFloorBot { FightInput Next(RepossessSim sim, float h); }
public struct FloorResult { public int towed; public float[] towSeconds; public bool wrecked, reachedExit; public int wavesReleased, wavesSkipped, hits;
    public float minPlating, secondsRun; public float firstFlankedWave /*-1*/; public float timeToThird; }
public sealed class RepossessSim {
    public RepossessSim(IFloorBot bot, ulong seed, FloorOptions options = null);
    public FightSim Fight { get; } public WaveClock Clock { get; } public Repossession Objective { get; } public FloorFlow Flow { get; }
    public IReadOnlyList<CoverBox> Cover { get; }                 // RepossessArena.SightBlockers()
    public FloorResult Run(float maxSeconds);
    public void Tick();                                           // one 1/60 s step: movement, fight, clock, waves (W2), tow meters, end/lift
}
```
Behaviour: builds `FloorWorld`-equivalent data without test code: since `BoxWorld` is test-only, `RepossessSim` takes the
`IMoveWorld` in its constructor (tests pass `FloorWorld.Build()`); everything else is pure data. On each wave release
`SpawnBearings.Choose` picks doors, `WaveSpawner` waits 2 s, then `Fight.AddEnemy` spawns the bots at the doors
(drones leave a vent or a shutter and are handed to W4's arrival; until W4 lands they start at the door and hold). A bot's
`Group` = wave number x 10 + 0/1 (the two doors), woken when spawned (they arrive hunting). Plating/scrap/strip are the
existing `FightSim`'s. Third tow: `Clock.Stop`, `Flow.AllTowed`, the exit zone opens; reaching `ExitLiftZone` ends the run.

- [ ] **Step 1: tests (old):** every M1 `FightTests`/`CoverTests`/`RouteTests`/`EncounterDodgeTests` still passes unchanged
  (the default `FightArea.Galleria`); an empty-spawn `FightSim` does not end on its first tick; `AddEnemy` mid-fight
  produces the same enemy a constructor spawn would (same seeds, same patrol seeds).
- [ ] **Step 2: tests (new):** a do-nothing bot in the arrival alcove: wave 1 appears after 0 s + 2.0 s telegraph from two
  doors >= 90 degrees apart and >= 15 m away; waves at 30 s intervals; the cap holds at 10 (spawn 20 waves with a bot
  that never kills); a scripted tow (walk to the nearest socket circle and stand): the meter fills in 10 s and the
  alarm event fires once; a wreck (Invulnerable off, bot stands in the open) restarts per R1 (`Reset`) and `FloorResult`
  says wrecked; determinism (same seed, same `FloorResult`); a hitch (the sim stepped as `Tick()` x 60 vs x 1 with
  different external dt) is not applicable (fixed step) but `Run` is bounded by `maxSeconds`.
- [ ] **Step 3:** `tools/skyline/test.ps1`. Commit with a message listing the edited shared sim files.

**Acceptance:** all old and new tests green. **Pass check:** none (DM1/DM2 use it).

### Task W4: Door-spawned bots: arrival and engagement (pure + the Unity hooks)

Resolves the D6 behaviour gap. A wave bot is not placed at a post; it comes through a door and goes to work.

**Files:** Create `Runtime/Skyline/BotArrival.cs`, tests `BotArrivalTests.cs`. Modify `Runtime/Skyline/HoverPatrol.cs`
(only to accept an engagement point), `Scripts/Bots/SkylineBot.cs` (**owner of this file in wave 2**: the hooks below).

**Interfaces (produces):**
```csharp
public sealed class BotArrival {                                 // per bot, from the moment it leaves its door
    public BotArrival(EnemyDef def, SpawnDoor door, ulong seed);
    public Vector3 Step(Vector3 pos, Vector3 player, IReadOnlyList<CoverBox> blockers, float dt, FightArea area);   // new position
    public bool Engaged { get; }                                 // reached a hover/attack point and handed over to the normal brain
    public Vector3 Target { get; }
}
public static class EngagePoints {
    // drone: a point 12..28 m from the player, a clear line to the player's chest, at (player ground y + 3.8), re-picked every 6..9 s
    // floor-care: drives straight at the player along walkable nav (moveSpeed 5), falls back to the nearest cover-free approach
    public static Vector3 For(EnemyDef def, Vector3 player, IReadOnlyList<CoverBox> blockers, float seed01, float now);
}
```
`SkylineBot` hooks (all additive): `public static SkylineBot Spawn(..., SpawnDoor door)` overload (starts in `Arriving`);
`public GunDef KilledBy { get; }` and `public static event Action<SkylineBot, GunDef> Killed` (from `TakeHit` when `killed`,
carrying `SkylineHitInfo.gun`); `public static Rect SearchArea` is set by the floor director from `FightArea.Floor1`; a bot
counts toward the cap through `SkylineBot.Live`; the bark path stays (H4 subscribes to `Barked`).

- [ ] **Step 1: tests:** a drone from each skylight vent reaches an engage point within 40 m, 6 s, never entering a
  `SightBlockers` box, and ends 12..28 m from the player with a clear line; a floor-care unit from each shutter and hatch
  reaches 4 m of a player standing at 10 sampled standable points within 20 s on `ArenaNav` paths; engage points re-pick
  but never jump (move <= speed x dt); the gondola does not use `BotArrival` (rail); killing credit: `FightSim` already
  tracks the Writ; add a pure `KillCredit` check in A1's tests later.
- [ ] **Step 2:** write the pure part; add the `SkylineBot` hooks (compile check only here).
- [ ] **Step 3:** `tools/skyline/test.ps1`. Commit.

**Acceptance:** green; the Writ-only fight sim tests are unchanged. **Pass check:** the pass's stage A compiles; the
gallery scene still builds (`SkylineGalleryBuilder` spawns bots at posts).

### Task B1: The floor builder: the scene from data (Unity editor, grey-box)

**Files:** Create `Editor/SkylineArenaBuilder.Floor.cs` (partial class; the entry `BuildFloor1(string path)` plus shared
floor helpers `FloorBox`, `FloorRamp`, `FloorMarker` for the other floor partials); modify `Editor/SkylineScenes.cs`
(add `Floor1 = Folder + "/SkylineFloor1.unity"` and `BuildFloor1()`; **not** in `All`), `Editor/SkylineLint.cs` (a
`FloorDataMatch` rule: every `FloorSolid` has one collider with equal bounds and vice versa; the old four rules stay).

**What it builds (all from `RepossessArena`, `RepossessSites`, `HotelLobby`):** a box per `FloorSolid` (ramps as meshes
with matching colliders), `MovementSurface.runnable` per solid, glass solids on a `SkylineGlass` layer (collides with
movement, ignored by gun rays and bot sight: `SkylineBot.IgnoredBySight`), one marker object per socket (circle decal root,
no art yet), one per door, the exit lift's shutter and zone, the player at `HotelLobby.PlayerStart`, the HUD GameObject,
a placeholder `SkylineArenaDirector` stand-in is NOT added (X0 adds `SkylineFloorDirector`), the ink rig as the old
builder (`SkylineLook.ApplyInk`), the light rig, reflection probes (three: lobby, food court pit, food court ring), stills
(J1 adds the list). The lobby is B2's partial; B1 calls `BuildLobbyShell(...)` if present (a stub that B2 fills).

- [ ] **Step 1:** write. Reuse the old builder's helpers by calling them (`Box`, `Group`, `Lit`, `Glass`, `Emissive`, `Template`).
- [ ] **Step 2:** `tools/skyline/test.ps1` (compile check). Commit.

**Acceptance:** compiles; `SkylineScenes.BuildFloor1()` is callable. **Pass check (main session):** a pass builds
`SkylineFloor1.unity` (a one-off editor job, outside `All`); `SkylineLint.FloorDataMatch` reports 0; a still from
`FloorMarker`'s cameras shows the three levels; the player can walk the route from the lobby start to each socket in play
(done by the pilot later); the scene opens under the 4.6 GB cap.

### Task B2: The lobby builder (Unity editor, grey-box)

**Files:** Create `Editor/SkylineArenaBuilder.Floor.Lobby.cs` (`BuildLobbyShell`: the hallway, the lobby, the mezzanine and
stairs, the desk, the cab, the practice rig, the boost stack, the counter, the lockers, scrap caches on the stack and the
lockers as in M1 with `SkylineScrapCache`, the cab's door objects with `SkylineLiftDoor` placeholders for L2).

- [ ] Same method as B1: from `HotelLobby.Solids()` only. Commit.

**Acceptance:** compiles. **Pass check:** the lobby builds and the practice lessons' geometry matches the M1 lessons'
numbers (the M1 demo's lesson steps run against it in L2).

### Task H2: HUD base: health bar, ammo, font, the old boxes out (Unity)

**Files:** Modify `Scripts/Hud/SkylineHud.cs` (remove the plating/chassis boxed bars, the gun label box, the boxed speaker
card and the notice banner; keep the crosshair, hit markers, damage wedge, hurt vignette, and expose `Root` and `Safe`
`VisualElement`s plus `Instance`); create `Scripts/Hud/SkylineHudHealth.cs`, `Scripts/Hud/SkylineHudAmmo.cs`,
`Scripts/Hud/SkylineHudIcons.cs`, `Scripts/Hud/SkylineHudStyle.cs` (ink keyline 3 px `#16141F`, palette, the font loader);
create `Assets/.../ThirdParty/Fonts/` with the chosen OFL face and its licence, a `CREDITS.md` line.

**Interfaces:** consumes V1's `HealthBarModel`; produces `SkylineHudHealth.Bind(SkylinePlayer)`, `SkylineHudAmmo.Bind(SkylinePlayer)`.

- Health bar: a **layered parallelogram** drawn with `Painter2D` in a `generateVisualContent` callback: the chassis bar
  (a slanted 300 x 22 px parallelogram at the 1920 x 1080 reference, skew 14 px, orange `#FF9E3F` fill), the plating layer
  over it (a slightly taller slanted layer, light blue `#8FD3FF`, offset up-left 6 px) with hard edges and 3 px ink
  keylines; plating breaking: 8 shard polygons fall for 0.5 s then it is gone; low chassis (< 35%) pulses the bar and
  tints the screen edges red (`HealthBarModel.EdgeTint`).
- Ammo: the magazine count in the display font at 96 px reference size (digits), the reserve at 36 px beside it (special
  weapons only), a small gun icon (about 64 px, a silhouette path drawn with `Painter2D`), all with the same keyline.
- Font: **Luckiest Guy** (Apache 2.0) for the digits (the creator, 2026-10-14); Anton kept for a later "Condensed" UI option. Loaded as `FontDefinition.FromFont`; a missing font logs `SKYLINE_HUD font missing`
  and falls back to the project font.

- [ ] **Step 1:** compile-check tests are not enough: add EditMode tests that instantiate the hud on a fake panel and assert
  the elements exist and no element is a `Box`-style rounded-corner container (`borderTopLeftRadius == 0` everywhere).
- [ ] **Step 2:** write; `tools/skyline/test.ps1`. Commit.

**Acceptance:** compiles; EditMode tests green. **Pass check:** HUD stills at 16:9, 21:9, 4:3 (J1 renders them); the bar and
digits are slanted/displayed, not boxes; no clipped text; the safe area holds.

---

# Wave 3

### Task H3: HUD objective: job strip, edge markers, tow ring, wave clock (Unity)

**Files:** Create `Scripts/Hud/SkylineHudObjective.cs` (and nothing else; it adds its own `VisualElement`s under
`SkylineHud.Safe`).

- The job sheet: three asset icons along the top edge (drawn glyphs: a mascot head, a chair, a cabinet; draft shapes),
  ticked in the objective violet when towed. An edge marker per untaken asset (V1 `EdgeMarker` projection; a violet
  chevron with the distance label `HudText.Distance`), the world-space through-wall marker (a violet diamond billboard
  placed at the asset, drawn by the S0 x-ray role as a quad outline), the **tow ring** round the crosshair (a `Painter2D` arc
  filling with the meter, shown only inside a circle, the asset's icon in its middle), and the wave clock at top right
  ("NEXT 0:21", dims while a wave waits for room, flashes in the last 3 s). No bot count.
- Consumes `Repossession.Sheet`, `ActiveCircle`, `WaveClock.SecondsToNext`; produces `SkylineHudObjective.Bind(Repossession, WaveClock)`.

- [ ] Tests: EditMode construction test as H2. Commit.

**Acceptance / pass check:** a still inside a circle shows the ring at 40%; a still facing away shows the edge markers and
distances; the sheet ticks after a tow in the demo recording.

### Task H4: Subtitles, settings, bubbles out (Unity)

**Files:** Create `Scripts/Hud/SkylineHudSubtitles.cs`; modify `Scripts/Player/SkylineSettings.cs` (two keys:
`SubtitleMode`, `SubtitleSize`, persisted in PlayerPrefs like the rest), `Scripts/Hud/SkylinePauseMenu.cs` (a "Subtitles"
row: mode and size), `Scripts/Hud/SkylineHud.cs` (**only** to remove `ShowBark`/`PlaceBark`/`HideBark` and `BarkTag`; H2 has left
that file stable by now). Consumes `SubtitleBuffer`, `SkylineBot.Barked`, the director's teaching lines (`Say`); the speaking
babble sound stays (audio is unchanged).

- Bottom centre, white text, dark outline, **no box**, at most two lines, `SPEAKER:` prefix in the speaker colour, CONCIERGE's
  PA lines in italics, size from the setting (28 / 34 / 42 px at the 1080p reference), fade in/out, never clipped (wrap at
  70% of the screen width). Barks only in `DialogueAndBarks`. Default `Dialogue`.
- [ ] Tests: settings round trip; the mode filter; no `BarkTag` left (compile).
- [ ] Commit. **Pass check:** stills with a Nan line, a PA line and a bark at each size; no box; no bubble above any bot.

### Task H5: Flashes (Unity)

**Files:** Create `Scripts/Hud/SkylineHudFlashes.cs`.
- Plating gained (`SkylineWreck.Stripped` from S0, gain > 0): the screen edges flash light blue `#8FD3FF` for 0.35 s at 35%
  alpha, a hard-edged vignette; special ammo gained (A2's event) while a special weapon is out: the ammo counter flashes green
  (`#5CFF8A`) for 0.4 s; the strippable-wreck outline is S0's. One pooled overlay, no allocation per flash.
- [ ] Tests: the flash curves as pure statics in `HudModels.cs`? (owned by V1; H5 keeps its curve code in its own file with an
  EditMode test). Commit. **Pass check:** a recorded strip shows the edge flash.

### Task A2: Special ammo in play (Unity)

**Files:** Create `Scripts/Player/SkylineAmmo.cs`; modify `Scripts/Player/SkylineShooting.cs` (the Breacher's reserve and its
dry-fire), `Scripts/Hud/SkylineHudAmmo.cs` (the reserve display and the green flash hook; H2 left it stable).
- On `SkylineBot.Killed(bot, gun)` (W4): `SpecialAmmoLedger.Credit(gun.special)` and spawn the "special ammo" stripped with
  the wreck's plating (the same touch that strips plating gives the ammo; one pickup per wreck, tied to `Wreck.freshSeconds`);
  a recorded `SKYLINE_AMMO +n (kill by <gun>)`. The ledger is reset by the director on a wreck.
- [ ] Tests: EditMode: a fake kill by the Writ adds 1, by the Breacher x3 adds 1, a stripped wreck cannot give twice.
- [ ] Commit. **Pass check:** the Breacher counter in the demo recording rises after Writ kills and the wreck-touch.

### Task C2: Comic lettering in play (Unity)

**Files:** Create `Scripts/Fx/SkylineLettering.cs` (+ a `SkylineLetteringPool`).
- On `LetterEvent`s (bot kill, weak-point hit, `SpotCreated`/`SpotHit` break, Breacher shove) ask `ComicLettering.Pick`; draw
  the word as world-anchored UI Toolkit text (an overlay on `SkylineHud.Safe` placed beside the bot, never within 120 px of
  the crosshair), hostile red-magenta `#FF2E63` fill with a 3 px dark ink keyline, a random tilt of +-8 degrees; **it writes
  itself out**: each glyph's width wipes open left to right (one glyph about 40 ms, overlapped) then holds 0.5 s and fades
  over 0.3 s. (Starting version: a wipe per glyph. A true stroke order per word is a later option for the creator.)
- [ ] Tests: EditMode: at most two lettering elements alive; one never overlaps the crosshair rect; pooled (no allocation per
  word after warm-up, checked with a counter). Commit. **Pass check:** stills of a kill and a spot break.

### Task M2: Music director and placeholder stems (Unity + tools)

**Files:** Create `Scripts/Audio/SkylineMusicDirector.cs`; `tools/skyline/music/make-placeholders.py` (run with
`uv run python`; writes short synthesized loops: no references, no copyrighted material); the output
`Resources/SkylineMusic/placeholder/*.wav` (committed by the main session after the run; heavy jobs go through the
capped-run wrapper).
- Two `AudioSource`s per layer (ping-pong) and `AudioSettings.dspTime`/`PlayScheduled`/`SetScheduledEndTime`; stems for
  each section from `MusicSections.Placeholders()`; vertical layers (base, motion, danger, light) fading by
  `MusicLayers`; the API the director calls: `StartMuzak()`, `StepIntoLift() : LiftRidePlan`, `OnDropReached` event (raised on
  the DSP time of the drop), `SetFloorMain()`, `SetCalm(bool)`, `Stop()`. If `AudioSettings` reports no device or a failed
  schedule, a fallback timer raises `OnDropReached` at the plan's drop time and logs `SKYLINE_MUSIC fallback`.
- The placeholder stems are labelled as placeholders in the file names and in a `README.txt`; the composed music replaces
  them (never by an agent).
- [ ] Tests: EditMode: the director reproduces `PlanLiftRide` times with a fake DSP clock; the fallback path fires once.
- [ ] Commit (code and the tool; the wavs by the main session). **Pass check:** the lobby recording has muzak looping; the
  ears measure the riser-to-drop onsets against `LiftRidePlan` within 35 ms.

### Task R2: Repossess runtime (Unity)

**Files:** Create `Scripts/Floor/SkylineRepossess.cs` (the component wrapping R1), `Scripts/Floor/SkylineTowCircle.cs`,
`Scripts/Floor/SkylineAsset.cs`, `Scripts/Floor/SkylineTowDrone.cs`.
- Builds the three assets at the picked sockets (placeholder meshes from primitives until the art exists; the objective role
  outline, violet), the lit ring decal on the surface (banded violet light, 6 m across; hidden under a bridge's glass
  balustrade no), the meter feedback (the ring brightens, the tow drone's beam in violet stripes: a banded spot light from
  above and a clamp animation), the **alarm** (CONCIERGE's alarm sound and a spinning amber beacon at the nearest wall, only
  on `MeterStarted`), the tow completing (the asset winched up through the skylight in 1.2 s; `Towed`), the third tow
  (`AllTowed`: waves stop, CONCIERGE's concession, the exit lift's shutter rolls fully up with the violet outline and a
  glowing arrow), the lift run (`ExitLiftZone` ends the floor: `SKYLINE_FLOOR exit reached`), and the restart (`Reset`).
- Logs `SKYLINE_FLOOR socket <asset>=<id>`, `... meter-start <asset>`, `... towed <asset> at <s>`, `... all-towed`, `... exit`.
- [ ] Tests: EditMode: with a fake player, the meter fills at the wrapper's feet check (3D distance, dy rule) and the alarm
  fires once per asset; a restart keeps the sockets and re-places the assets. Commit.
- **Pass check:** the demo recording shows three tows with their sockets in three groups, the alarm at each first meter
  start, the shutter rolling up after the third, `SKYLINE_FLOOR exit` in the log.

### Task SD1: Spawn doors and their 2 s telegraphs (Unity)

**Files:** Create `Scripts/Floor/SkylineSpawnDoor.cs`.
- Per door kind: shop shutter (rattles and rises 2.5 m in 2 s, a warm light behind it), skylight vent (a hatch slides, a
  white beacon blinks, a servo whine from above), service hatch (a roll-up hatch with a hazard beacon), rail end (the
  gondola's lamp lights, the cable twangs). Each plays a sound from its direction on a world `AudioSource`
  (`SkylineSounds`, through the voice gate's `minor` kind) at telegraph start; at +2.0 s the bots spawn (`SkylineBot.Spawn` with
  the door overload from W4). A door is `Busy` during its telegraph (W2). A wreck calls `Cancel`.
- [ ] Tests: EditMode: telegraph lasts 2.0 s (+- one frame at 60 fps), then fires once; `Cancel` stops it; every kind has a
  light, a sound and a motion. Commit.
- **Pass check:** a still mid-telegraph at a shutter and at a vent; the log has `SKYLINE_WAVE <n> doors <a>,<b> bearings ...`.

### Task G1: The gondola outside the glass (pure + Unity)

**Files:** Modify `Runtime/Skyline/Gondola.cs` (`MineLayer` gains a mine offset: the mine is placed on the inside of the
glass at the nearest hatch, `mineOffset` and `HatchSnap(z)`; defaults keep M1 behaviour), `Scripts/Bots/SkylineMines.cs`
(placing and blasting at the inside face; the hatch slides open for the toss), create `Scripts/Floor/SkylineFacade.cs`
(the 12 hatches, the outside rail with its trolley, the gondola's spawn on the rail end), tests `GondolaTests.cs` (extend).
- The gondola rides x = 36.5 outside the glass, z -27..27 (rail speed 3 m/s, ping-pong); mines snap to the nearest of the
  12 hatches (z -22..22 every 4 m), at most 6 live, 12 s each; a mine blasts inside the hall (trigger 1.5 m, blast 2.5 m,
  25 damage); the glass is bullet- and sight-transparent so it can be shot and shoots back; the squeegee attack is cosmetic
  against the glass.
- [ ] Tests: mines land only on hatch z values; spacing/lifetime/cap unchanged; the gondola's sight line to a player on the
  wall-run is clear through the glass (`SightBlockers` has no glass); the M1 gondola tests pass.
- [ ] Commit. **Pass check:** a still from the wall-run shows the gondola outside the glass and a mine on the inside.

### Task L2: Lobby runtime: desk, practice drone, lift ride (Unity)

**Files:** Create `Scripts/Floor/SkylineLobby.cs`, `Scripts/Floor/SkylineLiftRide.cs`, `Scripts/Floor/SkylineLiftDoor.cs`,
`Scripts/Floor/SkylineConciergeDesk.cs`.
- The staff hallway start (dim, a sound of muzak from ceiling speakers via `SkylineMusicDirector.StartMuzak()`); the practice
  bot (`SkylineBot.Spawn(DroneId, HotelLobby.PracticeBot, ..., practice = true)`), the boost stack and its cache, the counter,
  the lockers and their caches (the M1 lessons, same numbers); CONCIERGE's desk screen shows its greeting (a texture swap) when
  the `LobbyDesk` beat fires.
- The lift ride: the cab's doors open to the lobby when the player is within 4 m; the player steps in and stays in the cab
  1 s (`SteppedIntoLift`): the doors close, the director asks `StepIntoLift()` for the plan, the cab hums and shakes (a camera
  shake with a decaying amplitude, the screen of the floor counter ticks up), the riser plays, then the pre-drop; at
  `doorsStartOpeningAt` the north doors begin opening onto the alcove and the food court; `DoorsOpened` fires on `dropAt`; the
  Unit may leave the cab only after the doors are open (a collider holds the doors shut until then). Leaving mid-ride is
  impossible; a wreck mid-ride restarts at the hallway.
- [ ] Tests: EditMode: with a fake music director, the doors are open exactly at `dropAt` within a frame; the door collider
  holds until then; a wreck resets.
- [ ] Commit. **Pass check:** the lobby recording (J1) shows the practice lesson lines, the greeting, the ride and the drop;
  the ears confirm the onset of the drop within 35 ms of the doors' full opening frame.

---

# Wave 4

### Task X0: `SkylineFloorDirector`: phases, waves, restart, beats (Unity)

The integration. It replaces `SkylineArenaDirector` for the new scene and reuses its tested ideas (the teaching queue, the
wreck restart, prewarm), written fresh around `FloorFlow`.

**Files:** Create `Scripts/Floor/SkylineFloorDirector.cs` and `Editor/SkylineArenaBuilder.Floor.Director.cs`. B1 declares
`static partial void WireDirector(Transform arena, SkylinePlayer player, SkylineHud hud)` in its file; X0 implements it in
its own partial, and so adds every runtime component of wave 3 to the scene in one place (`SkylineRepossess`, the
`SkylineSpawnDoor`s, `SkylineLobby`/`SkylineLiftRide`, `SkylineFacade`, `SkylineMusicDirector`, the HUD parts
`SkylineHudObjective`/`Subtitles`/`Flashes`/`Health`/`Ammo`, `SkylineLettering`, `SkylineAmmo`). The wave-3 tasks therefore
do NOT edit the builder.
- Phases: Lobby (L2), LiftRide (L2/M2), Floor (waves: each frame `WaveClock.Tick(dt, SkylineBot.Live alive count,
  gondolaAlive)`, each released wave `SpawnBearings.Choose` -> `WaveSpawner.Order` -> SD1 doors -> `SkylineBot.Spawn`), Finale
  (R2), Done. `Start`: set `SkylineBot.SearchArea` to `FightArea.Floor1`, prewarm (`SkylinePrewarm`), beats from
  `FloorBeats.Floor1()` through `TeachingQueue`, the ledger (`LedgerRun.BeginCombat` at the first wave's first alert,
  `EndCombat` when the last bot of a wave is wrecked), `SkylineHud.Say` replaced by `SkylineHudSubtitles`.
- Waves: announce (CONCIERGE, `WaveAnnounce`, rate-limited) at release, telegraph, spawn; `SKYLINE_WAVE <n> released <living>/<cap>`,
  `... waiting`, `... skipped`.
- Restart on a wreck: `FloorFlow.Wrecked`; every bot, telegraph, bolt, mine, wreck, tow state removed; assets back; the clock
  reset; the ledger and the ammo economy reset; the player placed at `HotelLobby.PlayerStart`; played beats stay played.
- Exposes the same events the demo needs: `WaveReleased`, `Restarted`, `PhaseEntered`.
- [ ] Tests: EditMode on fakes: a full headless run of the director against a `FakeSpawner` (waves released at 0 and 30 s,
  cap holds, a wreck resets, the third tow ends the waves).
- [ ] Commit. **Pass check:** a complete run in the demo recording.

---

# Wave 5

### Task DR1-DR5: Dressing (content; **gate G0**)

One dressing task per area, each a partial of `SkylineArenaBuilder` that ADDS visual-only geometry under a `SkylineNoCollider`
root (the solids stay exactly the data's), merged per material by `SkylineKit`, with the lights listed. Style rules apply
(`SKYLINE-STYLE.md`): thin ink on silhouettes and creases, three bands, flat painted colour with a vertical gradient, walls
cool blue-white (`#E9EDF2`), floors warm cream (`#CDBB9C`), TF2 detail on the edges and skyline, clean routes, no scenery in
the hostile hue, layered depth, scenery lowest contrast. All hues of neon signs avoid red-magenta, the objective violet, the
wreck light-blue and weak-spot yellow bands (S0's hue lint). Each task ends with `tools/skyline/test.ps1`; it does not
commit (content): it hands back the file list; the main session commits.

**Light budget (all areas together):** at most 64 live punctual lights in the scene, at most 24 overlapping any one view,
none of them shadow-casting (only the sun and, in the lobby, the chandelier's one spot shadow); every local light is a
banded hard-edged pool (the ink shader's additional-light bands) with range <= 11 m; no light colour in the hostile band.

- **DR1 Ring, shopfronts, walk-in shop interiors** (`SkylineArenaBuilder.Floor.Shops.cs`). The sunlit hero space: shopfront
  glass and signs with neon in each shop's own hue (about 18 brands from `SKYLINE-LOOK.md`'s sign words, plus the four
  walk-in shops' signs), awnings, cornices, planters against pilasters, the escalators' glass sides, the pit's rim railing, floor
  inlay, column capitals. The four **walk-in shop interiors are colour-free** (`SkylineInkConvert.MakeGrey`) lit by ONE
  neon each (a banded coloured point light plus an emissive strip), with shelving, a counter and the hung products in greys;
  the asset's tow circle is violet and reads best here. Budget: about 220 batches, 60k vertices.
- **DR2 The pit and the food court props** (`...Floor.Pit.cs`). Tables (8) and planters (4) of the data in painted wood and
  terracotta, menu boards and a food stall façade on the pit walls, the kiosk (the mascot's stand: a stage with lights and a
  banner), the ramps' handrails, the chute's rails and a hazard stripe, warm string lights (emissive only, no lights), the
  skylight's sun bands falling across the floor. Budget: about 90 batches.
- **DR3 The service corridor** (`...Floor.Corridor.cs`), **colour-free**: grey painted concrete, conduit and pipe runs, grey
  hazard stripes, numbered doors, cable trays, floor-care charging bays with hatches, a strip-light row (cool white, banded
  pools) and one amber warning beacon at each end; the hatches' roll-up doors; the leg behind the exit lift. Budget: about 60
  batches, 12 lights.
- **DR4 Galleries, bridge, escalators, façade, skylight, city** (`...Floor.Upper.cs`). Gallery fascias and railings, hanging
  banners, the glass bridge's under-glow and frit, escalator steps with moving-step texture scroll, the façade's mullions and
  hatches, the outside rail with its trolley track, the skylight beams and the six vents' housings, and the city backdrop
  (reuse `City`/`Tower` of the old dress file; re-grade for the new east view). The cool-blue ambient and the sun bands.
  Budget: about 150 batches (the city Unlit batches included).
- **DR5 The lobby and staff hallway** (`...Floor.LobbyDress.cs`). Hotel lobby: marble and carpet, the chandelier (the room's
  taking-over light, a warm gold point light plus an emissive crown; one shadowed spot), a reception desk with CONCIERGE's
  screen, brass luggage carts and a luggage stack, plants, a mezzanine lounge, ceiling speakers (the muzak's visible source),
  the lift's doors and their indicator; the staff hallway is dim and **colour-free** with one cool strip light and the lobby's
  glow at its end. Budget: about 130 batches, 10 lights.
- [ ] Each: build the area, run the lint (`FloorDataMatch`, hue lint, untextured rule), hand back with the measured batch and
  vertex counts. **Pass check:** the stills of J1 (below) and the perf probe.

### Task DM1: The pilot (pure)

**Files:** Create `Runtime/Skyline/FloorPilot.cs`, `Tests/Skyline/FloorPilotTests.cs`.
- `FloorPilot : IFloorBot`: behaves like a person: picks the nearest unclaimed asset by path length from `ArenaNav`, walks
  there along `Path` with sprint, slides down the chute when it is on the route, takes cover (`CoverPath`) when plating < 40%,
  strips fresh wrecks within 8 m when the plating has 10 of room, stands in the circle until the meter is full while
  shooting threats, retreats to the nearest cover when a floor-care unit telegraphs, jumps its sweep, then the next asset,
  then runs to the exit lift. Turn rate and reaction beat from `HumanAim` (frequency 20, damping 0.65, max 650 deg/s);
  `FightPlayerDef` human numbers (hit chance 0.85 near / 0.5 far, retarget 0.08 + 0.15 per 90 degrees).
- [ ] Tests: over 20 seeds on Normal the pilot towed all three, reached the exit and was not wrecked in at least 80%
  (starting numbers; tuning is done WITH the creator); its path never leaves `ArenaNav`; no teleports (position delta per
  step <= 1.1 x sprint speed x dt); the three assets are taken from three different groups every seed.
- [ ] Commit. **Pass check:** none (DM3 mirrors it).

### Task DM2: The corner-camper (pure)

**Files:** Create `Runtime/Skyline/CamperBot.cs`, `Tests/Skyline/CamperTests.cs`.
- `CamperBot : IFloorBot`: picks the standable point with the lowest `SeenShare` that has crouch cover adjacent (an
  automatic "best camping spot": computed once with `ArenaLint.SeenShare`), goes there and jiggle-peeks (the
  `TacticalBot` Hide/Peek/Strip cycle) forever; never goes into a tow circle.
- [ ] Tests (over 20 seeds): **no asset is towed**; **the camper is flanked by wave 3**: at some time after wave 3's release,
  at least two live shooters have clear lines to the camper from bearings >= 90 degrees apart (measured by
  `RepossessSim`, `firstFlankedWave <= 3`); **the camper is wrecked** within 240 s in at least 90% of seeds; and the camper is
  not killed by a bug (never outside the arena, never stuck for more than 3 s except while ducking).
- [ ] Commit. **Pass check:** none (DM3).

### Task DM3: The Unity demo and the camper run (Unity)

**Files:** Create `Scripts/Arena/SkylineFloorDemo.cs` (+ partials `SkylineFloorDemo.Fight.cs`, `SkylineFloorDemo.Lobby.cs`);
modify `Scripts/Capture/SkylineRunArgs.cs` (`-skyline-camper`, `-skyline-floor1`).
- The demo drives `SkylinePlayer.ScriptedInput` from `FloorPilot`'s decisions in the real scene: the lobby (the M1 lessons:
  practice bot spot, the stack boost, the counter, the lockers wall-run), the lift ride (it waits like a person), then the floor.
  The aim goes through `HumanAim`; fire only while the crosshair's own ray lands on the target; it never snaps; it uses the
  M1 demo's cover-cycle ideas (copy what you need from `SkylineArenaDemo.cs` lines 712-1330; do not edit that file). A watchdog
  (`SKYLINE_DEMO watchdog`) for stuck states. `-skyline-camper` replaces the pilot by `CamperBot`: it must end wrecked and
  restart (logging `SKYLINE_DEMO camper wrecked`).
- [ ] Tests: EditMode: the demo's step list is built; the camper mode picks the same spot as `CamperBot`. Commit.
- **Pass check:** the demo recording completes the floor like a human in the time budget (< 6 min); the camper recording
  ends wrecked with no asset towed; both with audio.

### Task P1: Perf and prewarm (Unity)

**Files:** Modify `Scripts/Fx/SkylinePrewarm.cs` (its loop over `GalleriaArena.Encounters()` at line 86 must go: it draws the
floor's bots by `RepossessSites.DoorsFor` enemy ids; add every new material, the glass, the shutters, the lettering and the
HUD vector meshes), `Scripts/SkylinePerfProbe.cs` (a measuring route for the floor: arrival, the pit, the bridge, the
gallery, the full cap alive).
- [ ] Budget table in the commit message. **Pass check:** stage D on the built player: 1% low <= 16.7 ms with 10 bots alive
  on the floor, average <= 8.3 ms; `spikes` follow no first-use event; `inkOff` A/B run recorded in `SKYLINE-LOOK.md`.

### Task J1: Captures, stills and the judge brief

**Files:** Create `Editor/SkylineArenaBuilder.Floor.Stills.cs`; modify `tools/skyline/pass.ps1` (additions only: build
`SkylineFloor1`, record the demo run, the camper run, and the lobby/lift run; stills); create
`docs/skyline-judge/FLOOR1-BRIEF.md`.
- **Stills** (from `SkylineStillCamera`s): `01-staff-hallway`, `02-lobby-wide`, `03-lobby-desk-greeting`, `04-lift-doors-opening`,
  `05-arrival-foodcourt-wide`, `06-pit-kiosk-asset`, `07-ring-shopfronts-sun`, `08-shop-interior-grey-neon`,
  `09-service-corridor-grey`, `10-bridge-looking-down-the-pit`, `11-north-gallery-east-end-circle`,
  `12-facade-wallrun-gondola-outside`, `13-skylight-vent-telegraph`, `14-tow-circle-meter-ring`, `15-wave-flank-two-bearings`,
  `16-exit-lift-open-violet`, `17-hud-minimal-full`, `18-subtitles-sizes`, `19-lettering-kill`, `20-wreck-strip-flash`.
- **Recordings** (with audio): the complete demo (lobby to exit), the camper (fails), the lobby + lift (the drop sync).
- **The judge brief** lists the Skyline bar's categories (design 10) plus the floor-specific checks: the four layout rules
  re-measured; the camper fails and the demo passes in the recordings; aim-model rule per wave in the log; the hostile hue
  reserved; colour-free zones really colour-free (a still measure of saturation); outlines readable on every area palette;
  clipped text; the drop onset against the doors; the first-use hitches; the "not covered by the spec" category.
  Adversarial judge agents (several in parallel, as in M1) score; every category >= 8/10 with no blockers before the
  creator plays.
- [ ] Commit. **Pass check:** all of the above produced in one `pass.ps1` run.

### Task X1 (main session only): the switch-over

`SkylineScenes.Arena` becomes the floor (`BuildFloor1` replaces `SkylineArenaBuilder.Build` in `BuildAll`); the old arena
code (`SkylineArenaBuilder.cs`, `SkylineArenaDress.cs`, `SkylineArenaDirector.cs`, `SkylineArenaDemo.cs`, `GalleriaArena`'s
hall, `TeachingBeats.M1` use) is deleted or reduced to what the gallery scene and the tests still need (`GalleriaArena`
stays as the headless M1 fixtures until a later cleanup); `RecordSeconds(Arena)` grows to the demo's length; the build and
stage D measure the new first scene; the creator is handed a build only after the judges.

---

# Later (not blocking the floor): the bot redesigns and damage states

Planned after the creator has judged the floor's structure ("the creator will judge them properly once the level design
exists"). Each is its own bot-identity task (design, numbers, model, faces, sounds), Sonnet implementers, CC0 bases first
(`FableAnimationTests/art/review/2026-10-14/cc0-bots.html`; downloads in `out/skyline-refs/cc0-bots/`), the creator choosing art.
They touch `Enemies.cs`, `BotBrain.cs`, `FightSim.cs`, `SkylineBotBodies.cs` and `tools/skyline/bots/*`; none of them is
needed by the floor tasks above, because the floor reads `EnemyDef.bodyRadius` and layer/aim tags only (D7).

1. **Sizes:** all three main bots larger (the drone "tiny: I could barely see it at the ranges I was fighting it"):
   re-measure `bodyRadius`, hit zones, door clearances (SD1), the lint's sight blockers and the demo's aim.
2. **Mall-cop drone:** much larger rotors, which become the weak spot (BotWeakPoint outline, a hit-zone per rotor); the
   faceplate is not a separate screen from its face's surface.
3. **Floor-care unit:** no face protrusion, the face display on the top face of the roomba; the only protrusion is the acid
   canister; on reaching you it winds up and deploys the acid like a molly (a lobbed puddle where it burst) instead of the
   ankle sweep; it must be shot (the canister) or it recharges in **15 s** and goes again. New attack in `BotBrain`/`FightSim`
   and the sweep's tests replaced; the floor's aim-model table is unchanged (area, floor).
4. **Spotlight:** larger humanoid robots with the spotlight as their head, charging electric railguns before locking on and
   firing; they headline another floor (excluded here).
5. **Gondola:** the creator has not seen it yet; notes later. Its mines must read clearly at range (the hostile outline).
6. **Damage states (all bots):** visible stages at health thresholds (70% and 30% starting set), each crossing PLAYS the
   damage (a panel snapping off and tumbling, a rotor guard bending, sparks), one hit that crosses several thresholds plays
   each in quick succession; separate from and stacking with the Writ's spots. Pure part: a `DamageStages` threshold crossing
   detector (`Runtime/Skyline/DamageStages.cs`, tests: a hit from 95% to 20% reports 70 then 30, in order, once); Unity part: per
   bot model animations.
7. **Wave composition rebalancing** after the redesigns, with the creator ("balancing hands-on together").

---

# What the main session checks in each pass

1. **After wave 1:** `tools/skyline/test.ps1` (all pure tests), stage A alone (S0's shader compiles, `JamKit.SkylineRepo.Tests`).
2. **After wave 2:** stage A with `BuildFloor1` (the scene builds; `lint.count == 0`; `FloorDataMatch` 0; the scene opens under
   4.6 GB); the headless `RepossessSim` numbers (pilot completes, camper fails) printed by an `[Explicit]` report test.
3. **After wave 3:** stage B recording of a lobby + ride + first minute: the drop lands on the doors (ears), the spawns come
   from two bearings >= 90 degrees (the `SKYLINE_WAVE` lines), the HUD is the minimal HUD (no boxes, no bubbles).
4. **After wave 4:** a complete demo run and a camper run; stage D perf; the keyword rule (`-skyline-render-diag`).
5. **Before the creator:** DR* done, P1 green, J1 stills judged by the adversarial judges (>= 8/10, no blockers), the
   look of every area compared with the style sheet, the hostile hue reserved.

---

# Questions for the creator (the defaults are in force, decision D18; a new call from the creator overrides one)

1. **Lobby lessons:** the practice counter and the wall-run lockers (M1's judge-round-2 additions) move into the lobby with the
   practice drone and the boost stack, though section 9 names only the last two. Keep them? (default: keep)
2. **The south row:** its middle slot is the arrival alcove, so the south row has four shops, not five. OK? (default: yes)
3. **Waves 5+:** when a wave is still waiting for room as the next interval arrives, is the new one skipped (default) or
   queued? And how often does a gondola join (default: every fourth wave from wave 8, when none is alive)?
4. **The exit lift:** the shutter rolls fully up (default) or stays at knee height for a final slide, as in the joke line of M1?
5. **Font:** Bangers (default) or Anton for the ammo digits and the comic lettering? (stills of both in J1.)
6. **Lettering:** is a left-to-right wipe per glyph enough for "writes itself out", or do you want a stroke order authored per
   word? (default: the wipe)
7. **Restart:** after a wreck the run restarts in the staff hallway and replays the lift ride (default). Skip the ride the second
   time?
8. **Glass:** the façade and the bridge's balustrade let bullets and sight through, so the gondola outside can be shot and shoots
   back. OK? (default: yes)
9. **Asset names:** the draft job sheet entries (a mascot "Sunny the Sun", a massage chair "RelaxoMax 3000", an arcade cabinet
   "STAR GUNNER") for you to rewrite.
10. **Tow-circle fairness measure:** "at least half exposed from two bearings 90 degrees apart" is implemented as in F3 (eight
    sectors, 25 sample points). Does that match your intent, or should a bearing mean the bots' spawn directions?
11. **Special ammo numbers** (start 2, max 8, 1 per primary kill, 1 per 3 special kills) are placeholders for the balancing session.
12. **Bridge floor:** glass you can see the pit through (default) or opaque (cheaper, less vertigo)?

### D20 (main session, 2026-10-14): Nan's job sheet is briefed in the lobby, not in the lift
M1 measured the lift ride (stepping in to the drop) at 5.1-7.1 s; `nan-job-sheet` (~120 characters, ~8.5 s) cannot
fit. So `nan-job-sheet` fires in the lobby (on reaching the desk or the lift lobby, before `SteppedIntoLift`), and the
lift gets one short line (at most ~50 characters, e.g. Dot: "Doors open, clock starts. Go!") that ends before
`dropAt`. Whoever next owns `FloorBeats.cs` (L2 or X0) moves the trigger, adds the short lift line as a draft in
`docs/SKYLINE-DRAFT-LINES.md`, and updates `FloorBeatsTests.NansJobSheetFitsInsideTheLiftRide` to read
`MusicSequencer.PlanLiftRide` (the line must END before `dropAt`).

### D21 (main session, 2026-10-14): what glass does to shots (from the creator's call, SKYLINE-FLOOR-REPOSSESS.md section 10)
All glass is see-through for sight (bots and player), on the `SkylineGlass` layer. For SHOTS it depends on the role:
**façade panes** (`facade-pane-*`): a shot passes through and BREAKS the pane (the tile leaves the wall-run; shards and
the open edge are a hazard); **balustrades / glass rails** (bridge and galleries): a shot STOPS there (no free shooting
through); the **skylight** and the **bridge floor**: a shot stops. So `SkylineShooting` (A2) and the bots' bolts must
decide by the solid's role, not by the layer alone; W4's "bolts pass through glass" is wrong for balustrades and must
follow this table. `RepossessArena.ShotBlockers()` (balustrades in, panes out) is already right.

### D22 (main session, 2026-10-14): flushing a corridor camper (closes the exploit the sim found)
The W3+W4 sim (commit e7dd86ed) showed the hidden west ends of both service corridors are a safe camp: 6/6 seeds
survive 240 s; drones never get a line in; floor-care units arrive one at a time. Rule, for the director (X0) and the
spawner (W2's caller): **when the Unit has been in a service-corridor zone for 8 s, the next waves swap their drones
for floor-care units spawned from the corridor's hatches at BOTH ends (a pincer), and the drones that can't fit hold at
the corridor's exits (waiting outside, so leaving is dangerous too)**. The floor-care redesign's acid molly makes a
narrow corridor untenable. CONCIERGE gets a draft line for it ("Pest control to the service corridor."). The 15 m
minimum spawn distance still holds (the hatch at (26, +-26) about 4 m from corridor positions must not be used when the
Unit is that close: check W2's distance rule is applied in the sim's spawn path too). Flip the sim's pinned
`ACamperHiddenInTheWestCorridors...` test when this lands; the camper must be wrecked or forced out within ~60 s.

### D23 (main session, 2026-10-14): the real arena passes the layout lint (F3's findings, closed)
F3's lint (commit f409ee3d) found the real arena failing three of the four rules. The main session's calls, applied in the data
(`RepossessArena`, `RepossessSites`) and in the lint (`ArenaLint`, `ArenaNav`); the lint now reports **zero problems** and
`ArenaLintTests.TheRealArenaPassesTheLintWithNoProblems` asserts it (the pinning test is gone).
1. **Door bodies (F2):** the vents and hatches that stood 0.1 m off a wall move to just clear it. Decided: vent-corridor-n/s z
   +-27.4 -> +-26.8, vent-leg-n/s x -34.9 -> -34.3, vent-promenade-n/s x 34.9 -> 34.3, hatch-corridor-n/s z +-27.4 -> +-26.0.
   **Adjusted by the implementing agent:** F2's full-hall spawn-coverage test (`EveryPointOfTheHallGetsALegalSpawnChoiceForEveryWaveType`)
   refused 110 point/wave pairs at the four corners with those numbers (the corner points are clamped to x +-34.5, z +-27, and a
   door has to sit just OUTSIDE that box so the two bearings keep their 90 degrees). The smallest values that keep both the lint's
   door-body check (0.45 m for vents, 0.5 m for hatches) and zero refusals are: vent-corridor-n/s z +-27.05, vent-leg-n/s x -34.55,
   vent-promenade-n/s x 34.55, hatch-corridor-n/s z +-27.0 (x -10 kept: 6.03 m from the shop circle, F2's 6 m rule). Yaws unchanged.
2. **F1:** the walk-in shops' `FrontDoorWidth` 3 -> 4 m (the shop circles are exposed from two bearings); `alcove-back-w` and
   `alcove-back-e` deleted (the corridor stubs beside the cab join the alcove); a solid `facade-landing` (role `deck`, x 30.5..35,
   z 9..14.8, top 2.8) against the east facade, the ledge that makes the wall-run -> north gallery route work (a wall-run alone
   tops out under the deck, F3's finding). F2's `facade-wall-run` route now runs the glass, mantles onto the landing and double
   jumps to the deck lip; `BaseKitRouteTests.TheFacadeWallRunRouteOfTheSitesEndsOnTheNorthGalleryWithTheLanding` drives it.
3. **Rule 1 (exits):** props at or under 1.1 m tall (`ArenaNav.HopHeight`; the pit's tables and planters) do not block the
   exit walks (you hop them; `LintOptions.hopHeight`). The one real pocket left, the pit's north-west corner west of the chute
   (a 4 m wide strip between the west wall and the chute), got a second way out by moving the **chute 1 m east** (x -11..-8,
   with its wall gap and floor mouth; `RepossessArena.ChuteMinX/ChuteMaxX`).
4. **Rule 2 (seen share):** counts only the ground-level doors (5 shop shutters, 6 service hatches, 2 rail ends = 13); the
   14 roof vents are excluded by design (overhead, seen from everywhere; their drones fly to engage anyway), and the limit is 70%
   (was 60%). `LintOptions.ventsCount` is false by default. The sim camper/holder survey (`RepossessSimTests`) remains the real
   "no safe corner" gate.
Numbers (before -> after): doors failing 8 -> 0; tow circles failing 4 -> 0; rule 1 points failing 11 -> 0 (4 corridor-stub, 7 pit);
rule 2 with all 27 doors at 60%: 290 points, worst 78%; with the 13 ground doors at 60%: 42 points, worst 69%; as decided (13 ground
doors, 70%): 0 points, worst 62% (8 of 13 doors, at the west ring (-18, 0)).

### D21 follow-up (drone-aim agent, 2026-10-15): the bots' bolts follow the glass table
`SkylineBolt` now stops at every glass that is not a facade pane (balustrades, the skylight, the bridge floor) and breaks a live
pane through `SkylineFacade.TryBreak` and flies on (`SkylineBot.IgnoredByBolt`, which unlike `IgnoredBySight` does not skip the
glass layer). Bot SIGHT is unchanged: it still sees through all glass, as D21's first sentence says and as `RepossessSim` and the
player's `SkylineShooting.Mask` do; so a bot behind a rail sees you, telegraphs, and its bolt hits the rail. If the creator wants
rails to block sight too, it is a one-line change in `IgnoredBySight` plus `SightBlockers` in the pure sim.
