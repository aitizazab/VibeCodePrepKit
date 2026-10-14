# Skyline Repo: fonts

Display faces for the HUD and the comic lettering. Loaded with `Resources.Load<Font>` (so they sit under `Resources/SkylineFonts/`),
each with its licence beside it. Unity makes the .meta files; do not write them by hand.

| Face | Used for | Licence | Source |
|---|---|---|---|
| Anton Regular | the ammo digits (the candidate the creator will judge; `SkylineHudStyle.AmmoFontDefault`) | SIL Open Font License 1.1 (`Resources/SkylineFonts/Anton/OFL.txt`) | https://github.com/google/fonts/tree/main/ofl/anton (https://fonts.google.com/specimen/Anton) |
| Bangers Regular | the alternative ammo face; comic lettering (C2) | SIL Open Font License 1.1 (`Resources/SkylineFonts/Bangers/OFL.txt`) | https://github.com/google/fonts/tree/main/ofl/bangers (https://fonts.google.com/specimen/Bangers) |

**Luckiest Guy** (comic lettering, C2) is NOT here yet. It is Apache License 2.0, not OFL: when C2 adds it, ship its
`LICENSE.txt` (Apache 2.0) beside it, from https://github.com/google/fonts/tree/main/apache/luckiestguy.

To try another ammo face in a still without editing code: launch with `-skyline-ammo-font Bangers`.
To change it for good: edit `SkylineHudStyle.AmmoFontDefault` (and add the face to `SkylineHudStyle.Faces`).
