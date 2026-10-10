# JamKit shared audio: third-party sources and their licences

Every downloaded sound under `Runtime/Audio/` is listed here with its source and licence. Games that use the
movement (the Movement Lab, the Visiting Hours dream escape) credit them in their `Credits.txt`.

## Kenney: Impact Sounds (1.0), RPG Audio, Sci-Fi Sounds (1.0)
- **Licence:** Creative Commons Zero (CC0 1.0), public domain. Credit is appreciated, not required.
  The packs' own licence files are next to this page: `Kenney/ImpactSounds-License.txt`,
  `Kenney/RPGAudio-License.txt`, `Kenney/SciFiSounds-License.txt`.
- **Source** (downloaded 2026-10-10):

| Pack | Page | Zip | Bytes | SHA-256 |
|---|---|---|---|---|
| Impact Sounds | https://kenney.nl/assets/impact-sounds | `https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip` | 800,850 | `029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8` |
| RPG Audio | https://kenney.nl/assets/rpg-audio | `https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip` | 964,837 | `6dbeaf8544da958d8f2adcb4a4a4b76c1ade34a05f8ab9edccd327da7375f38b` |
| Sci-Fi Sounds | https://kenney.nl/assets/sci-fi-sounds | `https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip` | 5,875,104 | `119340f351a5098ad814f78719438c0da355a9ce8a4c8a3af6a8d48aa3d49e04` |

- **How they were used:** `tools/audio/make-movement-sfx.py` layers, trims, pitches, fades and normalises them
  (mono, 44.1 kHz, peaks at -3 dBFS) into the round-robin variants in
  `Movement/Resources/JamKitMovementAudio/` (`<id>_<n>.wav`). Re-run it to rebuild them.

| Movement sound (id) | Made from (pack: files) |
|---|---|
| Footstep (`mv_step`, 5) | Impact: `footstep_concrete_000`-`004` |
| Sprint weight under the step (`mv_step_heavy`, 5) | Impact: `impactSoft_medium_000`-`004`, low-passed at 260 Hz |
| Jump (`mv_jump`, 4) | RPG: `footstep00`, `01`, `03`, `07` (the push-off scuff) under `cloth1`-`3` (the swish) |
| Double jump puff (`mv_djump`, 4) | Sci-Fi: 0.32 s bursts of `thrusterFire_000`-`003` with a fast attack and decay, over a low whump made in code |
| Wall kick snap (`mv_walljump`, 4) | Impact: `impactGeneric_light_000`-`003` and `footstep_concrete` (pitched up); RPG: `cloth4` (the whoosh) |
| Wallrun catch (`mv_catch`, 4) | Impact: `impactSoft_medium` (low-passed: the hand plant), `impactGeneric_light` (the slap); RPG: `cloth1`-`4` (the lean-in) |
| Soft landing (`mv_land_soft`, 4) | Impact: two `footstep_concrete` steps 32-41 ms apart |
| Landing (`mv_land`, 5) | Impact: `impactSoft_medium_000`-`004` with a `footstep_concrete` |
| Hard landing (`mv_land_hard`, 5) | Impact: `impactSoft_heavy_000`-`004`, `impactSoft_medium`, `footstep_concrete`; RPG: `cloth` |
| Mantle (`mv_mantle`, 4) | RPG: `handleSmallLeather`, `handleSmallLeather2` (the grip), `cloth1`-`4` (the pull), `footstep02`, `04`, `06`, `08` (the foot on top) |
| Sprint kick push-off (`mv_kick`, 4) | Impact: `footstep_concrete` (pitched down) and `impactSoft_medium`; RPG: `cloth2` |
| Slide drop (`mv_slidein`, 4) | Impact: `impactSoft_medium`; RPG: `footstep05`, `08`, `09`, `03` (dirt scuff) and `cloth` |

## Made in code (not downloads)
The wind and air rush, the slide scrape, the wallrun rub and the slide-boost whoosh, plus a generated fallback
for every sound above, are synthesised by JamKit (`Runtime/Core/Movement/MoveSounds.cs`).
