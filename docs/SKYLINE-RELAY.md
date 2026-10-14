# Skyline Repo: the Relay (a boss-fight prototype, 2026-10-14)

A test mode for the creator to feel. It is meant, if it works, as one of the boss fights at the end of a floor. The normal mode
is the default and unchanged.

## The creator's ask

> "Can we make a mode that instead requires you to get between six points on a map to do something in a random order quickly?
> Like Get to Point A, hit a button, go to Point C, grab a cable that you tug over to Point F to get a keycard that you swipe at
> point B to open a door at point E to escape/elevate before a timed-wipe, all while enemies shoot at you? It'll be one of the boss
> fights at the end of a floor, most likely, but I want to see how it'll fare."

After the first playtest (same day): "Station F doesn't need a ramp by the way, the entire point is that you need movement to get
between points fast. Any straight path point should be quite hard, and any easy pathway should be one of the longer routes to the
next point you're instructed to go to." The ramp is gone; the hall is now built around that rule (next section).

## The movement rule: the straight way is hard, the easy way is long

The hall is cut into five east-west **lanes** by four **2.2 m walls** (`RelayArena.LaneWallZ`). Each wall stops 5 m short of one end
of the hall, and the openings alternate (`OpeningEast`: west, east, east, west from the south), so walking from lane to lane means
going the long way round to an opening, while the straight line is **a sprint, a jump and a mantle** over the wall. Stations A to E
sit one per lane; **F is on the catwalk** (the mezzanine deck, 5 m up) with **no walk route at all**: it is reached by a mantle onto a
2.4 m block, a mantle onto a 4.0 m block and a mantle onto the deck's edge. No ramps, no stairs. The walls and blocks carry an
orange cap strip so a ledge reads as a ledge.

The movement used is **sprint, jump and mantle** only (the base kit's, all of it proven by the sim below). Not used: slide,
double jump, wall-run, the Breacher's boost (the sim has no pilot for the last two; the first two would only add variety).
M1's own hall cover and the mezzanine's glass upstand are turned off in the Relay (the upstand's face would stop the mantle onto the
deck, the cover would sit in the lanes).

### Where the stations are

| | Place | Position (x, y, z) |
|---|---|---|
| A | lane 1 (south), east | (16, 0, -16) |
| B | lane 2, middle | (-2, 0, -8) |
| C | lane 3, west end | (-20, 0, 0) |
| D | lane 4, middle | (-4, 0, 8) |
| E | lane 5 (under the mezzanine), east | (16, 0, 16) |
| F | the catwalk (mezzanine deck), north-west | (-18, 5, 17) |

The start is (0, 0, -17) in lane 1. Every pair is at least 15 m apart (3D). The climbing blocks stand in lane 4 at x -14.5..-11.5 against
the deck's south edge (z 7..10 and 10..12).

### The check (the sim)

`RelaySimTests` runs the real `MoveSim` over the hall's solids for **every ordered pair** of stations, twice: the **skilled** route
(`RelayRoutes.Skilled`: straight line, a jump and a mantle onto every wall it crosses, the blocks for the catwalk) and the
**walk-only** route (the shortest path on the walkable graph, still sprinting, no jump and no mantle: the conservative reading, since
walking at 5 m/s would double every ratio). Asserted: every skilled route is achieved with `MoveSim`'s own movement, every route
that has a walk takes at least 1.6x the skilled time, and no walker can stand on the blocks or the deck. Result (seconds; ratio = walk / skilled):

| leg | wall climbs | skilled | walk-only | ratio |
|---|---|---|---|---|
| A-B / B-A | 1 | 2.1 | 6.1 | 2.88 |
| A-C / C-A | 2 | 4.5 | 13.3 / 13.4 | 2.97 / 2.99 |
| A-D / D-A | 3 | 3.5 | 12.5 | 3.57 / 3.59 |
| A-E / E-A | 4 | 3.7 | 18.6 | 5.06 |
| B-C / C-B | 1 | 2.1 | 7.0 | 3.28 / 3.27 |
| B-D / D-B | 2 | 1.8 | 6.1 | 3.48 / 3.46 |
| B-E / E-B | 3 | 3.3 / 3.4 | 12.3 | 3.68 / 3.66 |
| C-D / D-C | 1 | 1.9 | 7.2 | 3.71 / 3.70 |
| C-E / E-C | 2 | 4.5 | 13.3 | 2.97 / 2.98 |
| D-E / E-D | 1 | 2.3 | 6.1 / 6.0 | 2.62 / 2.59 |
| A-F | 6 (3 walls + 3 blocks) | 5.8 | no walk | - |
| B-F | 5 | 3.8 | no walk | - |
| C-F | 4 | 2.7 | no walk | - |
| D-F | 3 | 2.5 | no walk | - |
| E-F | 4 | 5.1 | no walk | - |
| F-A | 3 | 5.2 | no walk | - |
| F-B | 2 | 3.3 | no walk | - |
| F-C | 1 | 1.8 | no walk | - |
| F-D | 0 | 1.5 | no walk | - |
| F-E | 1 | 5.2 | no walk | - |

The smallest ratio is **2.59** (E-D). Leaving the catwalk is a drop off its edge (needs no skill, and there is no walk back down, so
"walk-only" doesn't exist for those legs; F-D crosses no wall). Everything else needs the kit and is 2.6 to 5.1 times faster than the
walk. Not simulated: enemies, the wall-run and the Breacher's boost.

## The design (unchanged from the first build)

It runs in the current playable arena: M1's main hall (50 x 40 m, `SkylineArena` scene), with `SkylineArenaDirector` left off
(no encounters, practice bot or teaching lines) and `SkylineRelayDirector` in charge.

**The six stations.** Each run shuffles which station plays which role:

| # | Step | Prop (primitives, flat ink colours, a 4.5 m beacon post) | What you do |
|---|------|------|------|
| 1 | Button | a pedestal with a big orange dome | hold interact 0.5 s (a bar fills under the crosshair) |
| 2 | Cable | a cable reel on a stand, its end hanging in front | interact to take the end |
| 3 | Plug | a panel with a round socket and a card slot | interact while carrying the cable: the cable plugs in and a keycard pops out |
| 4 | Swipe | a card reader on a post | stand at the socket until the card is picked up (automatic), then interact here: a loud clunk, the door and the lift's doors slide open |
| 5 | Door | a shutter door in a frame | pass it once it is open |
| 6 | Lift | a lift cab with sliding doors | enter it: the run is won |

The steps are always in that order; only the stations (A to F) shuffle. The Escape step needs the door passed (the fifth station's
zone entered while it is open) and then the lift (the sixth).

* **Interact** is **E** (keyboard) or **B / Circle** (pad, "buttonEast"). It is read directly in `SkylineRelayDirector`, not in
  the bindings table, so it isn't rebindable yet.
* **The cable.** Carrying it costs the sprint and slows you to **75% of the walk** (3.75 m/s). (`SkylinePlayer.MoveScale` and
  `SprintBlocked`. The engine reads a stick as full from `MoveDef.stickFullAt` = 0.7 up, so a move vector of 0.75 changes
  nothing; the player now scales it by 0.75 x stickFullAt. The first build missed this and the cable only cost the sprint. The ADS
  move scale of 0.8 has the same flaw in the normal mode: it is above 0.7, so aiming slows nothing; not touched here.) It
  stays tethered to the reel, drawn from the reel to your hand (box segments with a sag). Its length is the shortest route from the
  reel to the socket + 20% (the walk where there is one, else the fast route: `RelayStationMap.PathLength`). At that length it pulls
  taut and stops you (the outward speed is removed, and you are pulled back onto the length); it never breaks. Once plugged it stays
  drawn from the reel to the socket and no longer holds you.
* **The wipe.** The clock starts with the run: **60 s** (`RelayRun.DefaultSeconds`). At 0 the floor is wiped: the Unit is
  wrecked through its own flow (`SkylinePlayer.TakeAttack`, then the `Wrecked` event) and a new run starts with a new random
  order. Being wrecked by the bots does the same (a new order too). Winning shows the time left in green for 4.5 s, then
  starts a new run.
* **The timer** is digits only, top centre, in the HUD's display font with the ink keyline (`SkylineRelayHud`): white, the last
  ten seconds with a decimal, hostile red `#FF2E63` flashing under 15 s. No label text anywhere.
* **Showing the way.** Only the current target gets the objective violet outline `#B36BFF` through walls (`SkylineInkRoles.Objective`)
  and a bright violet letter over it. Every station's letter (A to F, display font) is always on screen, faint. The target is the
  current step's station, except that the Escape heads for the door first, then the lift. A step finished gives a soft violet
  screen pulse, a station pulse, a chime and a green lamp on its beacon; the card in hand shows as a small card glyph under the timer.
* **Enemies.** Waves the whole time (`RelayWaves`, on the floor's `WaveClock`): one at the start, then every **20 s**, at most **8**
  bots alive (a wave that doesn't fit waits for room), the floor's mixes (3 drones; 3 drones + 1 floor-care; 2 + 2; ...; the
  gondola, which needs a rail, is swapped for a drone). They spawn awake from points on the side **away from the current
  target** (`RelayArena.AirSpawns` / `FloorSpawns`; never closer than 14 m to you) and shoot normally. The floor-care units drive
  and cannot climb the lane walls, so they are slowed by the layout; the drones fly over.
* **No new dialogue.** Existing sounds only: the Concierge chime and sting, and the Breacher's clunk and lock for the door.

## How to start it

* In the game: Escape, then **Relay (test)**. It restarts the arena scene in the Relay. In the Relay the menu also has "Relay (test): new run"
  and "Back to the normal mode".
* From the command line: `-skyline-mode relay`. `-skyline-relay-seed N` fixes the first run's seed (the order for a seed is
  always the same); later runs pick new seeds.

## Logs (for the automated pass)

```
SKYLINE_RELAY start order=<letters> seconds=<n>        order = the stations in step order: button, reel, socket, reader, door, lift
SKYLINE_RELAY step <1..5> <button|cable|plug|swipe|escape> done at <t>
SKYLINE_RELAY won at <t>
SKYLINE_RELAY wiped at <t> cause=timer|wrecked
SKYLINE_RELAY wave <n> bots=<made>/<asked> alive=<n> at <t>
SKYLINE_RELAY mode relay|normal (scene restart)
```
`<t>` is run seconds (the clock pauses with the pause menu).

## The clock: 60 s

The skilled pilot (the sim: sprint, jump, mantle, 0.4 s per press, the keycard's 0.6 s, the cable leg at 3.75 m/s) takes, over 60
seeded chains (start, then the six stations in order): **min 18.3 s, mean 26.1 s, p95 30.4 s, max 33.3 s**; the cable leg averages
7.4 s (worst 12.5 s), the longest leg 12.5 s. Stretching the walking time by what fire costs (corners, strafing, shooting back; bots are not
simulated): x1.3 worst 42 s, x1.6 worst 51 s, x2.0 worst 63 s (1 of 60 chains over 60 s). The creator found 100 s generous (that was
the first build, whose perfect pilot took 23 to 40 s); **60 s** is the default now: about 2x the skilled mean, so a clean run has room for
a mistake or two and a player who is pinned down for a third of the clock loses. The honest limit: the sim has no bots in it, so how
much fire stretches the movement is for the creator's feel; 45 s is the next step if 60 is still easy.

## Tunables

| What | Where |
|---|---|
| Clock (60 s) | `RelayRun.DefaultSeconds` |
| Button hold (0.5 s), carry speed (0.75 of the walk), cable slack (1.2) | `RelayRun.ButtonHold`, `CarryScale`; `RelayTether.Slack` |
| Wave interval (20 s), cap (8), spawn distance (14 m) | `RelayWaves.Interval`, `Cap`, `MinPlayerDistance`; the mixes are `WavePlans` |
| Minimum hop (15 m) | `RelayChain.MinHop` |
| Station places, wall heights and openings, the blocks, spawn points | `RelayArena` |
| Warn colour/flash (15 s) | `SkylineRelayHud.WarnSeconds` |

## Code

* Pure (`Packages/com.vibejam.jamkit/Runtime/Skyline/`): `RelayArena`, `RelayChain` (seeded shuffle, hop checks, `RelayStationMap` over
  `ArenaNav`), `RelayRoutes` (the fast way between stations), `RelayRun` (the step machine, clock, wipe, cable rules), `RelayWaves`.
  Tests: `Tests/Skyline/Relay*Tests.cs` (`RelaySimTests` is the sim above).
* Unity (`Assets/JamKit/Templates/SkylineRepo/Scripts/Relay/`): `SkylineRelayMode`, `SkylineRelayDirector`, `SkylineRelayProp`,
  `SkylineRelayStage` (walls, blocks, turns M1's cover and glass upstand off), `SkylineRelayTether`, `SkylineRelayHud`.
* Hooks into existing files: `SkylineArenaDirector.Start` (stays off in the Relay), `SkylinePlayer` (`MoveScale`, `SprintBlocked`),
  `SkylinePauseMenu` (the entry).

## The creator's note on the skill check (2026-10-14, before playing it)
"Sprinting, jumping and mantling is way too easy, you'd need going at least 18 m/s for just that to be worth being called
a skill check. Maybe the space is too small for that, I'll judge it when I see it." The fast routes should demand chained
movement (wall-runs, slide-hops, the Breacher boost) at 18 m/s or more; the hall may be too small. Waits on the creator's
playtest of build `out/skyline/20261014-perf8/build/SkylineRepo.exe`.
