# Visiting Hours: third-party assets and their licences

Everything downloaded for Visiting Hours is listed here with its source and licence. The game's credits
(`Templates/VisitingHours/Credits.txt`, shown on the credits screen while Visiting Hours is the template) name them too.

## Kenney: Furniture Kit (2.0)
- **Licence:** Creative Commons Zero (CC0 1.0), public domain. Credit is appreciated, not required.
  The kit's own licence file is next to the models: `Kenney/FurnitureKit/License.txt`.
- **Source:** https://kenney.nl/assets/furniture-kit
  (zip: `https://kenney.nl/media/pages/assets/furniture-kit/440e0608a4-1677580847/kenney_furniture-kit.zip`,
  5,130,729 bytes, downloaded 2026-10-10).
- **Files used** (FBX, unchanged; restyled in code with the style-E material, `VhWakingStyle.Restyle`):

| File | Used as |
|---|---|
| `Kenney/FurnitureKit/cabinetBedDrawerTable.fbx` | the nightstand |
| `Kenney/FurnitureKit/lampRoundTable.fbx` | the bedside lamp |
| `Kenney/FurnitureKit/chair.fbx` | the chair with her scrubs |
| `Kenney/FurnitureKit/rugRectangle.fbx` | the rug |
| `Kenney/FurnitureKit/bookcaseOpenLow.fbx` | the low bookcase |
| `Kenney/FurnitureKit/books.fbx` | the books on it |
| `Kenney/FurnitureKit/pottedPlant.fbx` | the plant |

## Made for the project (not third-party downloads)
- **Nadia's 3D model** (`Models/Nadia/`): generated with Tripo Studio from the project's own style-E character
  sheet, on the creator's account (see `Models/Nadia/README.md`).
- **Elias's voicemail** (`Audio/vo_elias_voicemail.wav`): the creator's own recording.
- **The news report** (`Audio/vo_news_anchor.wav`): the creator's script read by the Kokoro TTS voice af_nicole
  (Kokoro-82M, Apache 2.0), made by `tools/audio/make-vh-news.py`.
- **The room, the bed and blanket, the phone, the tablet, the mug, the notepad, the badge, and every stand-in
  picture** are made in code (`Scripts/Waking/`, `Scripts/Opening/VhStageBuilder.cs`).
- **Sounds** (the phone's ring and buzz, the sheets, the phone set down, the tap, her breathing): placeholders
  generated in code by JamKit's `ProceduralAudio`.
