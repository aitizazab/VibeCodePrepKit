# Skyline Repo M1, round 3 (final): judge D (enemy readability, motion, encounter fairness, consequence)

Pass: `out/skyline/20261014-0625/`. My frames and contact sheets are in `out/skyline/20261014-0625/judge-D/`:

| Sheet | What it shows |
|---|---|
| `sheet-gal` | the gallery and its wide still |
| `sheet-z` | the drone's face, zoomed |
| `sheet-arena` | the 14 pass frames |
| `sheet-a1` | arena 48-93 s |
| `sheet-a2` | arena 99-147 s |
| `sheet-a3` | arena 150-179 s |
| `sheet-w` | the "watch" moments |
| `sheet-x` | arena 42.5-45.5 s |

## Round-1 defects: status after round 3

| # | Round-1 defect | Round 3 | Evidence |
|---|---|---|---|
| 1 | Every wreck was the same grey lozenge; no error face | **Fixed** | Each bot leaves its own broken model: the drone on its side, the floor-care unit slumped in its acid, the spotlight's head sagged and dark, the gondola hanging from one stirrup (`frames/SkylineGallery-011.1s`, `024.4s`, `042.1s`, `059.8s`; `still-SkylineGallery-wide.png`). In the arena, wrecks read by kind (`judge-D/a-150.jpg`, `a-158.jpg`). |
| 2 | Floor-care units interpenetrated | **Fixed** | Three to four units close in on the player in the skylight fight, but they stay apart, each face readable (`judge-D/a-158.jpg`, `a-160.jpg`, `a-162.jpg`). |
| 3 | The arena recording doesn't show the bots doing their thing | **Partly fixed** | The skylight fight now shows it well (150-164 s): floor-care units up close with their angry faces and acid streams, the spotlight's red beam on the player (`a-152.jpg`), and wrecks by kind. The food court and the facade still don't. In 16 food-court frames from 48 to 93 s, no live drone is readable. 4 of the 16 are a wall filling the screen (`a-54`, `a-57`, `a-60`, `a-63`), and "Halt! Mall security!" cards float over the hall with no drone visible under them (`a-51`, `a-72`, `a-90`). At the "watch mall-cop-drone" step the drone is about 40 px for about half a second before the camera turns to a wall (`judge-D/x-43.jpg` to `x-45.5.jpg`), and CONCIERGE's drone notice plays over a wall (`w-40.jpg`). In the facade fight (94-147 s), 10 of 23 frames are mostly a wooden stall back, and the live gondola is never on screen: only its wreck, far off (`frames/SkylineArena-109.3s.jpg`, "Hanging... up..."). The spotlight's body never appears; only its beam does. |
| 4 | The drone's face couldn't be read | **Fixed** | The face screen now sits in front of the egg: half-lidded blue eyes, a smirk and the REC light at idle, and red when angry (`judge-D/sheet-z.jpg`, `g-5.0.jpg`). |
| 5 | Bolt, acid and debris were primitives | **Fixed** | The bolt is a gold streak (`frames/SkylineGallery-006.6s.jpg`). The acid is a wet stream with glossy puddles (`019.9s`, `024.4s`; `judge-D/a-158.jpg`). Debris is shell shards. |
| 6 | Idle behaviours were invisible | **Mostly fixed** | The spotlight's scan, the gondola's ride and the drone's hover all show (same gallery timing as round 2). The floor-care unit's idle still reads mostly as turning on the spot (`frames/SkylineGallery-015.5s.jpg`; round 2 `sheet-idle`). |
| 7 | The cache paid +0 | **Fixed** | `scrap-cache plating +0 recovered +50`; the new locker ledge pays `+0 recovered +30`. I couldn't check whether the banked amount shows on screen. |
| 8 | Damage taken barely registered | **Fixed** | The red vignette, a hurt sound and labelled bars, per round 2. |
| 9 | No wear on live bots | **Partly fixed** | There is a wear tint (not visible in frames) and no smoke at low chassis. |
| 10 | The skylight's arc and the open layer | **Fixed** | The fights run food court, then facade, then skylight; the skylight now has 3 spotlights plus floor-care units. Aim models stay unmixed (drones never share a fight with spotlights). |
| 11 | Gallery capture faults | **Mostly fixed** | The stills show live bots and the wide shot shows all four wrecks. The wall-run dummy still floats upright on the glass (`frames/SkylineGallery-050.9s.jpg`). |
| 12 | Bark cards skewed, clipped or overlapping | **Mostly fixed, one new fault** | Cards are clean and flat. "Rude! I'm cleaning!" is drawn on top of a wall that stands between it and the camera (`judge-D/x-44.5.jpg`), though the presentation report says barks hide behind walls. |

## Remaining defects, most severe first

**[5 min]** marks what a **player** would notice in the first five minutes. **[rec]** marks what the creator will see
**in the recording** but a player at the keys would not, since a player chooses where to look.

1. **[rec] The recording still doesn't show a live drone or a live gondola.** The creator's "the drones don't even seem
   to move" will come back from the food court and facade fights. Evidence: row 3 above (`judge-D/sheet-a1.jpg`,
   `sheet-a2.jpg`, `sheet-w.jpg`, `sheet-x.jpg`). The demo's "watch" steps point at each bot for about half a second,
   then return to hugging cover. Fix: hold the watch for a full telegraph-and-attack cycle (drone 1.15 s: 0.35 s
   telegraph plus 0.8 s cadence; gondola: until a mine is laid) at a range where the bot is at least 150 px. Owner:
   demo player/capture.
2. **[5 min] The floor-care unit's safety notice doesn't play when you first meet it.** In the food court it is
   `concierge-floor-care dropped rearmed` (log line 1752). It plays only in the third fight (line 3489, seen at
   `frames/SkylineArena-160.7s.jpg`), and is then cut (`... cut`, after line 3578). By then the player has fought two
   of them and eaten their acid. Spec 6.8: each enemy's notice on its first appearance. Owner: HUD/presentation
   (teaching queue).
3. **[5 min] Bark cards show through walls** (`judge-D/x-44.5.jpg`). They give away bots the player can't see, and
   read as a bug. Owner: HUD/presentation.
4. **[rec] The spotlight's body is never on screen in the arena.** Only its red beam and its "Lights... out..." bark
   appear (`a-152.jpg`, `a-170.jpg`). The creator never sees the searchlight with the rifle in the arena. Owner: demo
   player/capture.
5. **The floor-care unit's idle reads as turning on the spot, not cleaning in runs** (gallery 13-17 s). Owner: the
   floor-care unit (`IdleWork`).
6. **The gallery's wall-run dummy floats upright on the glass** (`frames/SkylineGallery-050.9s.jpg`). Owner: demo
   player/capture.
7. **No smoke at low chassis** (the creator's call, spec section 14). Owner: all bots (an FX pass).

## Scores (8 is the bar)

| Category | Round 1 | Round 2 | Round 3 | Why |
|---|---|---|---|---|
| Enemy readability | 4 | 6 | 7 | All four bots read by face, body, attack and wreck in the gallery and in the skylight fight. The recording still never shows a readable live drone, gondola or spotlight body in the arena. |
| Motion | 4 | 7 | 7 | The hover, scan, ride and separation all work. The floor-care unit's idle spins, and the gallery dummy floats. |
| Encounter fairness | 6 | 7 | 7 | Aim models unmixed, telegraphs timed, the food court at 29/30, and no pile-ups. The floor-care notice comes two fights late. |
| Consequence | 5 | 7 | 7 | Both caches pay (banked), damage reads, wrecks are distinct. No smoke, and banked scrap isn't shown on screen. |

## Spec 10 blockers I see

**None in my categories**, and none in my evidence: the mint lift interior from round 2 is now dressed
(`frames/SkylineArena-173.6s.jpg`, `judge-D/a-179.jpg`). The perf failure (1% lows at 18.0 ms) is judge E's.

Blocker count: 0.

## Not covered by the spec (gaps to raise)

- There is no rule that a teaching notice which is dropped must re-fire at the bot's **next first-sight**. Today it
  re-arms and plays only when that bot kind next starts a fight, two fights later.
- There is no rule that the demo capture must hold each bot kind on screen through one full telegraph and attack.
- There is no rule that banked "recovered scrap" must be shown when it is earned.
- There is no rule on bark visibility through walls (the presentation agent implemented one, but the spec has none).
- (Carried over) The gallery's wall-run demonstration doesn't have to actually wall-run.
