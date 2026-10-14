# Skyline Repo: the Food Court floor, a repossession on a clock (spec)

Approved in chat by the creator, 2026-10-14: objective A (repossess), the 30-second wave clock and the cap of about 10
living bots as starting numbers. Replaces M1's main hall and its three player-started encounters. **The look is not
decided here:** the art style is being chosen first (`docs/SKYLINE-PLAYTEST-1.md`, "Art style"), and this floor is built
in it. Nothing here is built until the creator has the style.

## 1. The problem it fixes (the creator's playtest)

"It basically just forced jiggle peeking from behind a wall to pick off the drones one by one, with no other viable
strategy." Three causes, fixed together:

| Cause | Fix |
|---|---|
| Wrecking every bot was the goal, so wearing them down from one safe spot was optimal | the goal is three assets in three places; bots are in the way, not the goal |
| The player started each fight by stepping into a zone | waves arrive on a clock |
| One box hall with free-standing cover: bots came from one side | a three-level arena with loops; each wave flanks |

## 2. The objective: repossess three listed assets

- The firm's **job sheet** lists three assets (draft set: an animatronic mascot, a massage-chair display, an arcade
  cabinet). The HUD shows the sheet with each asset's direction and distance; each asset has a world-space marker
  visible through walls.
- **Tow circle:** a lit ring about 6 m across round each asset. Standing in it fills the asset's tow meter; **10 s**
  in total takes it (Dot's tow drone clamps it and winches it out through the skylight). Leaving the circle **pauses**
  the meter; it never drains. Being in any circle shows its meter on the HUD.
- **Order is free.** Each circle is placed so that at least half of it is exposed from at least two bearings: no
  circle can be held from behind one wall.
- **Clamping is loud:** the moment a meter first starts, CONCIERGE's alarm sounds and the next wave's spawns are chosen
  round that asset (section 3).
- **Sockets:** three socket groups, one asset per group per run, so every run puts one asset low, one inside and one
  high:
  - **Pit:** the kiosk at the pit's centre; the pit's north-east corner; its south-west corner.
  - **Inside:** any of the four walk-in shops.
  - **High:** the middle of the bridge; the north gallery's east end; the south gallery's west end.
- **The end:** when the third asset is towed, the waves stop, CONCIERGE concedes ("Asset transfer... acknowledged.
  Under protest."), and the **service lift on the west side** rolls its shutter up. Living bots keep fighting; the
  floor ends when the Unit's feet reach the lift. The run for the lift is the finale.
- **Wrecked:** the arena restarts at the lobby (section 9); assets go back on their sockets (same sockets this run), the
  clock resets.
- Data shape for the generator later (design 6.2): the floor declares its socket groups; the Repossess card needs three
  sockets on at least two levels.

## 3. The waves: on a clock

- **The clock starts at the drop (section 9):** the moment the lift doors open onto the food court. Wave 1 arrives then, and **a new wave
  every 31.35 s (16 bars of the floor song at 122.5 BPM; stretched from 30 s, 2026-10-14) (Normal)** until the third asset is towed. Nothing the player does delays the clock.
- **Cap:** at most **10 living bots**. A wave that would pass the cap waits until there is room (one waiting wave at
  most; the clock keeps running for the next).
- **Composition (Normal, starting numbers for balancing together):**

| Wave | Time | Bots |
|---|---|---|
| 1 | 0 s | 3 drones |
| 2 | 30 s | 3 drones, 1 floor-care |
| 3 | 60 s | 2 drones, 2 floor-care |
| 4 | 90 s | the gondola on the façade, 2 drones |
| 5+ | every 30 s | alternate 3 drones + 1 floor-care / 2 drones + 2 floor-care; a gondola only if none is alive |

- **Bots on this floor:** the drone (leads you), floor-care (area), gondola (area). The spotlight is excluded: its
  hitscan (fires at where you are now) may not share an encounter with the drone's leading shots (design 2.6). Its
  humanoid redesign headlines another floor.
- **Spawn points are in-world doors**, each with a **2 s telegraph** (a sound from its direction, a light, the door
  moving): shop shutters on the ring (floor-care, drones), skylight vents (drones), service hatches (floor-care), the
  façade rail's ends (the gondola).
- **Bearings:** every wave spawns from **two points at least 90 degrees apart** as seen from the player. The first is at
  least 120 degrees from the previous wave's mean bearing, so the corner that faced the last wave gets taken from the side.
  After an alarm (section 2), both points are the ones nearest that asset that still obey these rules.
  Never closer than 15 m to the player; never a point the player is looking at within 25 m.
- Wrecks still give plating (design 2.4): aggression is still how you heal, now on the way between assets.

## 4. The arena: three levels and loops (about 70 x 56 m; M1's hall was 50 x 40)

```
                          NORTH
   ░░░░░░░░░░░ service corridor (0 m, behind the shops) ░░░░░░░░░░░
   [shop][SHOP][shop][SHOP][shop]     north gallery (+6 m) above
   ───────────── ring concourse (0 m) ─────────────────    ║ glass
  esc ⇅      ┌─────────────────────────┐                   ║ façade:
             │    FOOD-COURT PIT (-4 m)│   glass bridge    ║ wall-run
 WEST lift   │  tables · ◯ kiosk · ramps│  N-S at +6 m     ║ + gondola
  (exit)     └─────────────────────────┘          esc ⇅    ║ rail
   ───────────── ring concourse (0 m) ─────────────────    ║
   [shop][SHOP][shop][SHOP][shop]     south gallery (+6 m) above
   ░░░░░░░░░░░ service corridor ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
                  SOUTH: arrival alcove, the lift cab from the lobby (section 9)
```
SHOP = walk-in (front door to the ring, back door to the service corridor).

- **The pit (-4 m, about 28 x 20 m):** waist-high tables and planters (crouch cover only), the kiosk at the centre; in by
  ramps on all four sides and a slide chute from the north ring. The exposed heart: the bridge and the galleries look
  into it.
- **The ring concourse (0 m, about 8 m wide):** its cover is architecture (the pit's rim, the shopfronts, the escalators'
  bulk). The galleries overhang it by about 3 m, a roof against the skylight drones.
- **Shops:** five a side, two walk-in a side; short sightlines inside; the back door is the flank.
- **The service corridor (about 3.5 m wide):** a U behind the north row, the west end (past the lift) and the south
  row. A flanking route for the player and for the floor-care units.
- **The galleries (+6 m):** north and south, joined by the **bridge** (4 m wide, glass, low balustrade: the fastest and
  most exposed crossing) and by **two escalators** criss-crossing (north-west ring to north gallery, south-east ring to
  south gallery).
- **The east façade:** a glass wall-run the length of the hall, ending at each gallery's lip (mantle up); the gondola's
  rail runs along it.
- **The skylight** (+14 m): the drone vents; hook anchors when the hook exists (design 6.5).
- **The warm-up hall is replaced by the lobby (section 9)**; the practice drone and the boost ledge move into it. The lobby meets the arena at the **arrival alcove**: the south shop row's third slot is the lift cab's mouth, so the south row has four shops and the alcove (the north row keeps five). The cab sits in the south service corridor's line, so that corridor is cut in two: its west part joins the west leg, its east part is a dead end with a hatch (docs/SKYLINE-PLAN-FLOOR1.md, D2).
- **Rules the layout must pass (measured):**
  - every standable point has at least two exits that leave a 10 m radius by different routes (props 1.1 m tall or less,
    the pit's tables and planters, do not block an exit: you hop them);
  - no standable point sees more than 70% of the ground-level spawn doors (shop shutters, service hatches, rail ends); the
    roof vents are not counted (overhead and seen from everywhere, their drones fly to engage anyway), and the headless
    camper/holder survey is the real "no safe corner" gate (docs/SKYLINE-PLAN-FLOOR1.md, D23);
  - every tow circle is at least half exposed from two bearings 90 degrees apart;
  - the floor is finishable with the base kit (wall-run, double jump, slide, mantle).

## 5. Teaching and voice

- Nan teaches the objective at the floor's start (design 6.8: objectives belong to Nan): the job sheet, the tow circle,
  "the clock's running, love". Dot calls each tow as it ends ("Clamped! Get Moving!" on the first, the creator's line, 2026-10-14). CONCIERGE announces waves and alarms.
- Every line is a draft for the creator to rewrite; they go into `docs/SKYLINE-DRAFT-LINES.md` under this floor.

## 6. HUD additions

The job sheet (three assets, ticked when towed), a direction marker per asset, the tow meter while in a circle, a small
"next wave" clock. No bot count (the bots must read on their own: enemy clarity, `SKYLINE-PLAYTEST-1.md`).

## 7. How it's checked before the creator plays

- Core tests (outside Unity): the wave clock, the cap and the waiting wave; bearing choice (90 degrees apart, 120 from the
  last wave, distance and view rules); the tow meter (pause, never drain, reset on a wreck); socket choice per group.
- Layout lint over the arena data: the four measured rules in section 4.
- **The demo player** completes the floor like a human (strafes, moves between cover, turns at a human rate; tows all
  three; runs for the lift). **A corner-camper bot** must fail: no asset towed, flanked by wave 3, wrecked.
- Perf: 120 fps at 1080p on the RTX 3050 with the full cap alive, floor 60 fps, in a build.
- An adversarial judge reviews captures (stills, the demo run, the camper run) against the Skyline bar before the
  creator sees it. Not handed over as a grey box: it ships in the chosen art style.

## 8. Out of scope here

The bot redesigns (sizes, rotors as the weak spot, floor-care's top screen and molly acid, spotlight humanoids), the
art style itself, the chase objective (the next floor type), difficulty variants beyond Normal.

## 9. The lobby: floor 1's fixed entrance (approved by the creator, 2026-10-14)

The tower above the Galleria is the **Halcyon Grand** hotel. Floor 1 always opens in its lobby: hand-made, never
generated, so the music can be synced to it. **It replaces the warm-up hall** (the practice drone and the boost ledge
move in). A 2D stylised start menu comes before it later.
1. **The staff hallway (spawn):** Dot's tow drone drops the Unit off; dim, the lobby glowing at the end. The music's
   intro plays as tinny hotel muzak from ceiling speakers.
2. **The lobby:** two storeys; CONCIERGE's front-desk screen gives its first greeting; luggage carts; a chandelier (the
   light that takes the room over, style rule 3); a mezzanine lounge. The practice drone works as the bellhop demo (the
   Writ's spots); a luggage stack is the boost ledge. The music loops while the player is here.
3. **The lift to the Galleria:** stepping in starts the opening sequence on the next bar (`docs/SKYLINE-MUSIC.md`): the
   riser while the lift rises, the pre-drop as the doors are about to open, **the drop as the doors open onto the food
   court and the wave clock starts**.
The creator: "the hotel needs a lobby for the first floor" and "it doesn't immediately drop us into the action upon
loading in".

## 10. The creator's calls on the plan's questions (2026-10-14, evening)

- **Glass is never free to shoot through.** The east façade's wall-run is made of individual glass panes (wall
  tiles). Shooting through a pane breaks it: that tile is gone from the wall-run (a gap) and its shards and the open
  edge add environmental danger. The bridge's glass balustrade blocks shots.
- **The gondola never shoots.** It rides outside the glass and only lays mines on the wall-run tiles. Shooting it means
  shooting through glass, so it costs panes. "A skilled player can give up a bit of wall tiling here and there to
  eliminate all the Gondolas to safeguard the rest of it from mines, or completely ignore the mines, pathing around,
  below or above them."
- **Fonts:** the comic lettering uses **Luckiest Guy and Bangers, interchangeably**. The ammo font is **Luckiest Guy** (the creator,
  2026-10-14); a later "Condensed" UI option may use **Anton** with the digits set closer together. **No label text on the HUD**
  (no "PLATING", no captions): bad comic lettering.
- **Being wrecked** (replaces the instant restart): the Unit **staggers, clutches at itself as it falls, then lands with
  its head turned to the side and an arm stretched out**; the screen fades to black; **a quote** shows for a moment
  ("to ragebait the player"; CONCIERGE's passive-aggressive voice fits), **held for a time that scales with its
  length**; then it fades back into **the lift ride** (the run restarts at the lobby's lift, so the music sync holds).
  Quotes are drafts for the creator to rewrite (`docs/SKYLINE-DRAFT-LINES.md`).
