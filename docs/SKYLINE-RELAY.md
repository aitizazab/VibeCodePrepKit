# Skyline Repo: the Relay (a boss-fight prototype, 2026-10-14)

A test mode for the creator to feel. It is meant, if it works, as one of the boss fights at the end of a floor. The normal mode
is the default and unchanged.

## The creator's ask

> "Can we make a mode that instead requires you to get between six points on a map to do something in a random order quickly?
> Like Get to Point A, hit a button, go to Point C, grab a cable that you tug over to Point F to get a keycard that you swipe at
> point B to open a door at point E to escape/elevate before a timed-wipe, all while enemies shoot at you? It'll be one of the boss
> fights at the end of a floor, most likely, but I want to see how it'll fare."

## The design (built)

It runs in the current playable arena: M1's main hall (50 x 40 m, `SkylineArena` scene), with `SkylineArenaDirector` left off
(no encounters, practice bot or teaching lines) and `SkylineRelayDirector` in charge.

**The six stations** are fixed places (data: `RelayArena.Stations`). Each run shuffles which station plays which role:

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
* **The cable.** Carrying it costs 25% of your speed (move vector x 0.75) and the sprint (`SkylinePlayer.MoveScale` and
  `SprintBlocked`). It stays tethered to the reel, drawn from the reel to your hand (box segments with a sag). Its length is the
  shortest walk from the reel to the socket (A* on the hall's walkable graph) + 20%. At that length it pulls taut and stops
  you (the outward speed is removed, and you are pulled back onto the length); it never breaks. Once plugged it stays drawn
  from the reel to the socket and no longer holds you.
* **The wipe.** The clock starts with the run: **100 s** (`RelayRun.DefaultSeconds`). At 0 the floor is wiped: the Unit is
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
  target** (`RelayArena.AirSpawns` / `FloorSpawns`; never closer than 14 m to you) and shoot normally.
* **The catwalk.** M1's mezzanine has no way up for a player (the deck is reached only by movement tricks), so the Relay builds a
  **ramp up the west wall** (`RelayArena.RampSolids`, built at run time by `SkylineRelayStage`): 24 degrees up to a landing 6.4 m
  up (over the glass upstand), then down onto the deck. Station F is on the deck.
* **No new dialogue.** Existing sounds only: the Concierge chime and sting, and the Breacher's clunk and lock for the door.

### Where the stations are

| | Place | Position (x, y, z) |
|---|---|---|
| A | south-west corner | (-21, 0, -14) |
| B | south-east corner | (21, 0, -14) |
| C | the middle of the hall, north of the stall backs | (0, 0, 3.5) |
| D | north-west floor, in front of the mezzanine's edge | (-15, 0, 6) |
| E | north-east floor, the far corner | (21, 0, 8) |
| F | the catwalk (mezzanine deck), north | (0, 5, 18) |

Every pair of stations is at least 15 m apart (3D), so no order has two consecutive stations close together; the generator also
checks every hop is walkable (the ramp makes C-F and D-F long walks).

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

## Tunables

| What | Where |
|---|---|
| Clock (100 s) | `RelayRun.DefaultSeconds` |
| Button hold (0.5 s), carry speed (0.75), cable slack (1.2) | `RelayRun.ButtonHold`, `CarryScale`; `RelayTether.Slack` |
| Wave interval (20 s), cap (8), spawn distance (14 m) | `RelayWaves.Interval`, `Cap`, `MinPlayerDistance`; the mixes are `WavePlans` |
| Minimum hop (15 m) | `RelayChain.MinHop` |
| Station places, spawn points, the ramp | `RelayArena` |
| Warn colour/flash (15 s) | `SkylineRelayHud.WarnSeconds` |

## Code

* Pure (`Packages/com.vibejam.jamkit/Runtime/Skyline/`): `RelayArena`, `RelayChain` (seeded shuffle, hop checks, `RelayStationMap` over
  `ArenaNav`), `RelayRun` (the step machine, clock, wipe, cable rules), `RelayWaves`. Tests: `Tests/Skyline/Relay*Tests.cs`.
* Unity (`Assets/JamKit/Templates/SkylineRepo/Scripts/Relay/`): `SkylineRelayMode`, `SkylineRelayDirector`, `SkylineRelayProp`,
  `SkylineRelayStage`, `SkylineRelayTether`, `SkylineRelayHud`.
* Hooks into existing files: `SkylineArenaDirector.Start` (stays off in the Relay), `SkylinePlayer` (`MoveScale`, `SprintBlocked`),
  `SkylinePauseMenu` (the entry).

## The simulated player (`RelaySimTests`)

The real `MoveSim` walks the hall's solids plus the ramp along the walkable graph through 60 seeded chains (start, then the six
stations in order; the cable leg at 75% speed without sprint; the press and keycard times added). No bots are simulated: the
"movement x" rows stretch the walking time to stand for what fire costs (corners, strafing, shooting back).

| Pilot | Total for a chain |
|---|---|
| perfect, sprinting | min 23.0 s, mean 31.5 s, p95 39.6 s, max 40.4 s |
| x1.5 movement | worst 59 s |
| x2.0 movement | worst 78 s |
| x2.5 movement | worst 96 s |

The cable leg averages 8.1 s (worst 15.9 s). **None of the 60 chains misses 100 s even at x2.5**, so 100 s is generous: it only bites
if the player is pinned down for more than half the clock. To make the clock a threat, 60 to 70 s is where a perfect pilot has
about 1.6x room; that is the creator's feel to judge. (An order with the catwalk late and the reel far from the socket is the
worst case.)
