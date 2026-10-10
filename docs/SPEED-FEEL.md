# Speed feel: brainstorm for the audio and visual overhaul

**Status:** brainstorm, 2026-10-10. Nothing here is built yet; the creator picks.

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
