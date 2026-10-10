# Speed feel: brainstorm for the audio and visual overhaul

**Status:** brainstorm, 2026-10-10. The creator picked V1, V2, V3, V5, V6 and V7 (not V4 or V8); those are built, see "Speed feel (built)" at the end.

## Why
The creator's notes (2026-10-10):
- **Music:** the speed sound needs work. The background music should get more excited the faster you go.
- **A moment for high speed:** going supersonic in Rocket League has the camera "boom" into it and a trail behind the wheels.
- **Our speed lines** only show against a dark background.

So the visual and audio layer for speed gets an overhaul.

What we have now:
- **Sound:** wind from 5 m/s and an air rush from 11 m/s (the sound pass); the field of view widens with speed.
- **Visuals:** thin, light screen-edge speed lines.
- **Music:** none in the lab; the escape has a placeholder chase loop tied to the chaser's distance.

## The principle: speed tiers with a crossing moment
Rocket League's supersonic works because it is a **threshold**: below it, nothing; cross it, and the car changes state (trail, sound, camera), and you keep that state while you hold the speed. Our research already names three tiers (RESEARCH-MOVEMENT 8.4):
- **Running:** under 8 m/s.
- **Fast:** 8–13 m/s.
- **Flying:** over 13 m/s.

The overhaul gives each tier a clear look and sound, and gives the moment of crossing into **Flying** its own "boom".

## Audio ideas
| # | Idea | What it does | Notes |
|---|---|---|---|
| A1 | **Music stems by tier** (vertical layering) | The track is split into base, drive and peak stems. Fast unmutes drive; Flying unmutes peak. They fade in quickly (about 1 s) and out slowly (about 2 s), so a stumble doesn't drop the music | The research's plan (8.3). Stems switch on the next beat or bar, so it always sounds musical |
| A2 | **The music opens up with speed** | A low-pass filter on the whole mix: muffled when slow, full and bright at Flying | Cheap and very readable. WebGL can't filter in real time, so it needs a pre-rendered "muffled" copy crossfaded instead |
| A3 | **A crossing stinger** | Crossing into Flying plays a riser that resolves into a hit on the next beat | The audio half of the "boom" |
| A4 | **The supersonic layer** | While Flying: a sustained shimmering whistle over the air rush, panned slightly away from any wall | Rocket League's supersonic hum, our way |
| A5 | **Tunnel focus** | At Flying, the world and ambience duck, so music and wind dominate | Makes Flying feel like a different state |
| A6 | **Nadia's breath** | Her breathing quickens with time spent fast, and a sharp breath-in at the crossing | Optional; ties the body to the speed |

The music itself is **composed with the creator** later. The system ships with placeholder stems so it can be judged now, and the real stems drop in.

## Visual ideas
| # | Idea | What it does | Notes |
|---|---|---|---|
| V1 | **The "boom" at Flying** | On crossing: a quick extra field-of-view punch (about +6°, settling over 0.4 s), a soft shockwave ring rippling out from the screen centre, and a one-frame chromatic pulse | The first-person version of Rocket League's camera boom. Scaled by Motion effects |
| V2 | **World-space streaks instead of screen lines** | Motes, dust or (in the dream) pastel confetti and petals in the air ahead stretch into streaks as you pass them | Reads on ANY background (light or dark), and the parallax shows real speed. The biggest fix for "you can't see the lines" |
| V3 | **Contrast-proof speed lines** | If screen lines stay, each one gets a dark core and a light rim (or a difference blend), so it shows on bright pastel and on dark scenes | A cheaper fix if V2 is too much |
| V4 | **Hand trails** (our "trail behind the wheels") | At Flying, ribbons of light stream from Nadia's hands as they pump; visible in first person, in the F4 view, on the ghost and in her shadow | The supersonic trail, on the one thing you always see: her arms |
| V5 | **Footfall sparks** | At Fast and above, each footstep kicks up a puff (dust in the lab, a pastel bloom in the dream); at Flying, glowing footprints that fade behind you | You see them when you look down or turn back |
| V6 | **Edge zoom blur** | A soft radial blur at the screen edges at Flying only; the centre stays sharp | Comfort risk: off when "reduce effects" is on |
| V7 | **The dream answers** (escape only) | At Flying, the dream brightens and saturates, flowers open along the route, and the static falls back a little | Rule 5 in RESEARCH 8.4: speed gets a visible reward |
| V8 | **A tier glyph** | A small, quiet mark at the screen edge that fills per tier (or a colour tint on the vignette) | Optional; the other cues may make it unnecessary |

## Comfort rules (unchanged)
- Every effect scales with the Motion effects setting.
- No continuous camera shake.
- The camera never rolls from speed alone.
- Blur and chromatic effects stay off when "reduce effects" is on.
- The comfort vignette stays even when other effects are off.

## Recommended starter set
- **The tiers in sound:** A1 + A2 + A3 + A4 (stems, the opening filter, the stinger, the supersonic layer).
- **The tiers in sight:** V1 + V2 + V4 (the boom, world streaks, hand trails), plus V7 in the dream escape.
- **Later, if it's still not enough:** V5, A5, V6.

## Speed feel (built)
Built 2026-10-10: V1, V2, V3, V5, V6 and V7, in the Movement Lab and the dream escape. Not built: V4 (hand trails) and V8 (a tier glyph).

### How it hangs together
- **One set of tiers.** Everything follows the shared `SpeedTier` (running under 8 m/s, fast, flying from 13; hysteresis, a 0.6 s drop delay, quick-in slow-out blends), the same tiers the music uses.
- **The hub.** `SpeedFeel`, on the player's camera, ticks the tiers once per frame and works out every effect's level (core `SpeedFeelState`), already scaled by the comfort settings.
- **Arriving at speed isn't a crossing.** A respawn or chapter jump doesn't boom.
- **The numbers** live in the movement tuning asset, under **Speed Feel** (`SpeedFeelDef`, every field commented) and **Speed Tiers**. Colours and shapes are per scene, set by the builders.
- **Wiring.** The scene builders call `SpeedFeelSetup.Wire`. The scenes need "JamKit/Rebuild Shell Scenes" before the effects appear.

### What each effect does
| # | Effect | What you see | Main settings (tuning: Speed Feel) |
|---|---|---|---|
| V1 | **The boom** | On crossing into flying, once per crossing: the view punches out about 6° (in 0.05 s, easing back over 0.4 s, on top of the speed FOV); a soft ring ripples out from the screen centre and pushes the picture as it passes (0.45 s); for one frame, colours split red-out and blue-in at the edges | `boomFov` 6, `boomRise` 0.05, `boomSettle` 0.4; `shockTime` 0.45, `shockStrength` 0.02, `shockWidth` 0.09, `shockReach` 1.0; `chromaPulse` 0.008, `chromaFrames` 1 |
| V2 | **World-space streaks** | Lab: pale dust motes with a dark outline. Dream: pastel confetti slips and petals, outlined in plum like the doodles, drifting down a little. They hang in the air ahead and stretch into streaks as you pass, with real parallax. None while running slowly; from fast, about 45 a second; at flying, about 140 a second, and longer streaks. They spawn at least 1.5 m off your line, and near ones fade out of the central ~40% of the screen | `streakRateFast` 45, `streakRateFlying` +95; `streakStretchFast` 0.018 and `streakStretchFlying` 0.04 (seconds of camera travel: 0.04 s at 20 m/s is a 0.8 m streak); `streakClear` 1.5 m; `centerClear` 0.2, `centerClearNear` 8 m |
| V3 | **Contrast-proof speed lines** | The same edge-only streaks as before, now with a dark core and a light rim (blended, not added), so they show on bright pastel and on dark scenes. Kept a touch subtler (alpha 0.35 → 0.3) because V2 now carries speed | On `SpeedLines`: `maxAlpha` 0.3, `fromSpeed` 12, `fullSpeed` 20, `widthScale` 1.5; colours on the SpeedLines material (`_Color` rim, `_CoreColor` core) |
| V5 | **Footfall puffs and footprints** | From fast, each foot plant kicks up a puff: grey-beige dust in the lab, a little pastel flower bloom in the dream, and off the wall during a wallrun. At flying, each ground step also leaves a glowing footprint (cyan in the lab, warm gold in the dream) that fades over 1.6 s. Timed by the controller's foot plants | `puffCount` 6, `puffSpeed` 1.1, `puffLife` 0.55, `puffSize` 0.16; `footprintLife` 1.6, `footprintSize` 0.34 |
| V6 | **Edge zoom blur** | At flying only: a soft blur towards the centre, out at the edges; the middle stays sharp | `edgeBlur` 0.6 (strength), `edgeBlurSpread` 0.07, `edgeBlurInner` 0.45, `edgeBlurOuter` 1.0 |
| V7 | **The dream answers** (escape only) | At flying: the dream gets brighter (+0.07 lift) and more saturated (+25%); small flowers open on the ground beside your line, 5 to 16 m ahead, and close after about 2.6 s; the static's wall is drawn up to 3 m further back. **Visual only:** the pursuer's distance, speed and catch don't change | `dreamSaturation` 0.25, `dreamBrightness` 0.07; `flowerRate` 14, `flowerLife` 2.6, `flowerSize` 0.5; `staticRecede` 3 m |

### Comfort
- **Motion effects** (Comfort and camera) multiplies every effect: the punch, the ring, the pulse, the streaks' density and stretch, the lines, the puff count, the footprints, the blur and the dream's answer. At 0, all of them are off.
- **Reduce effects**, a new toggle on the same screen (off by default):
  - turns off the edge blur (V6) and the chromatic pulse;
  - keeps the streaks and the speed lines at 40% (`reduceShare`);
  - the FOV punch and the ring stay, scaled by Motion effects.
- **No continuous shake and no roll from speed.** The boom changes only the field of view.
- **There is no speed vignette yet** in the movement scenes. The comfort rule "the vignette stays even when other effects are off" is for when one is added; this work doesn't add one.

### Under the hood
**Files:**
- Core: `Runtime/Core/Movement/SpeedFeelState.cs` (`SpeedFeelDef`, `SpeedFeelLevels`, `SpeedFeelState`).
- Components in `Runtime/Unity/Movement`: `SpeedFeel`, `SpeedScreenFx`, `SpeedStreaks`, `SpeedFootfalls`, plus the changes to `MovementCamera` and `SpeedLines`.
- The dream: `Dream/Scripts/DreamSpeedAnswer.cs`.
- The builders' helper: `Editor/SpeedFeelSetup.cs`.

**The shaders, all in `Runtime/Unity/Shaders`:**
- `JamKit/Movement/SpeedScreen` (`MovementSpeedScreen.shader`): one full-screen pass for the ring, the chromatic pulse, the edge blur and the dream's grade. URP 17 render graph, `Blit.hlsl`.
- `JamKit/Movement/SpeedMotes` (`MovementSpeedMotes.shader`): every particle. Its shapes are drawn from the UV with no texture: mote, confetti, petal, flower, shoe print and puff. Each has a light core and a dark rim, near ones fade out of the screen centre, and it can blend normally or add (the footprints).
- `JamKit/Movement/SpeedLines` (reworked): the dark core and light rim.

**Cost:**
- The full-screen pass is put into the camera's renderer from script, only on frames where something shows. No renderer asset changes, and a slow frame costs nothing.
- The particles are hand-emitted, recycled and in world space. There's no allocation per frame (a play test checks this).

### What the creator should look at
1. **The Gauntlet at full tilt** (lab): does the boom read as a moment, without being too much? Are the motes visible on the light grey grid and the dark folds, and do they read as speed?
2. **The dream escape:** confetti and petals against the golden sky, the lawns and the houses. Do they fit the doodle style? Is the dream's brightening at flying a reward you notice?
3. **The footprints:** look down or turn back at flying. If they point sideways, set the `footprintYawOffset` field on `SpeedFootfalls` to 90.
4. **The streak length:** if motes don't stretch into streaks, raise `streakStretchFlying`. Unity's camera-velocity stretch scale is set from my best reading of its units, untested on screen.
5. **The edge blur:** comfortable, or too strong? Lower `edgeBlur`, or set it to 0.
6. **The speed lines:** with V2 in, are they still needed? Set `maxAlpha` to 0 to compare.
7. **Comfort:** try Motion effects at 50% and 0%, and Reduce effects on.
