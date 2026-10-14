# Skyline Repo: fonts

Display faces for the HUD and the comic lettering. Loaded with `Resources.Load<Font>` (so they sit under `Resources/SkylineFonts/`),
each with its licence beside it. Unity makes the .meta files; do not write them by hand.

| Face | Used for | Licence | Source |
|---|---|---|---|
| Anton Regular | kept for a later "Condensed" UI option (digits set closer together) | SIL Open Font License 1.1 (`Resources/SkylineFonts/Anton/OFL.txt`) | https://github.com/google/fonts/tree/main/ofl/anton (https://fonts.google.com/specimen/Anton) |
| Bangers Regular | comic lettering (C2) | SIL Open Font License 1.1 (`Resources/SkylineFonts/Bangers/OFL.txt`) | https://github.com/google/fonts/tree/main/ofl/bangers (https://fonts.google.com/specimen/Bangers) |
| Luckiest Guy Regular | the ammo digits (the creator's pick, 2026-10-14; `SkylineHudStyle.AmmoFontDefault`); comic lettering (C2) | Apache License 2.0 (`Resources/SkylineFonts/LuckiestGuy/LICENSE.txt`) | https://github.com/google/fonts/tree/main/apache/luckiestguy |

To try another ammo face in a still without editing code: launch with `-skyline-ammo-font Bangers`.
To change it for good: edit `SkylineHudStyle.AmmoFontDefault` (and add the face to `SkylineHudStyle.Faces`).
