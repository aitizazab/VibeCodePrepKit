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

## Kenney: Impact Sounds, RPG Audio, Sci-Fi Sounds (the dream escape's movement sounds)
- **Licence:** CC0 1.0. They ship with JamKit's movement, so they are recorded with it:
  `Packages/com.vibejam.jamkit/Runtime/Audio/ThirdParty/LICENSES.md` (sources, files, licence texts).

## Freesound (CC0): the opening's sounds
- **Licence:** every file below is Creative Commons Zero (CC0 1.0), public domain, checked on its Freesound page
  (http://creativecommons.org/publicdomain/zero/1.0/). Credit is appreciated, not required; the credits name them.
- **Downloaded 2026-10-11** as Freesound's HQ previews (128 kbps MP3, no login), from `https://freesound.org/s/<id>/`.
- **How they were used:** `tools/audio/make-vh-sfx.py` cuts, fades, loops and levels them (mono, 44.1 kHz, at the
  opening's set peaks) into `Audio/Resources/JamKitSounds/<id>.wav`, which `ProceduralAudio.Get` plays instead of
  the generated sounds. Alternate takes (not shipped) are in `Audio/Takes/<id>__<take>.wav`.

| Opening sound (id) | Shipped take | Source (Freesound id, author, title) |
|---|---|---|
| Her sleeping breath (`vh_breath`, a 19.2 s loop of four 4.8 s breaths on VhStage's breath, a little lighter in pitch) | sleeper-lighter | 207116, harleto, "man_breathing_asleep.wav" |
| The phone's ring (`vh_phone_ring`, a 3 s loop) | synth-ring | 383439, Ferrettomato, "Phone Ringing.wav" (made in a synthesizer: not a stock ringtone) |
| The buzz on the wooden nightstand (`vh_vibrate`, a 3 s loop in step with the ring) | on-wooden-desk | 77392, Splash.Yang, "mobile phone vibration.aif" (a phone on a wooden desk) |
| Rolling over and pulling the blanket (`vh_rustle_big`) | rolling-in-bed | 108515, bsmacbride, "Bed Foley.wav" (a person moving while sleeping) |
| The sheets (`vh_rustle`) | sheets-by-hand | 644490, Froey_, "Rustling bed sheets" |
| The phone set down (`vh_clunk`) | phone-on-wooden-table | 859962, Vladislav_Sazonov, "Take the phone and put it on the wooden table" |
| The screen tap (`vh_tap`) | finger-on-phone | 704862, jentlemen, "Finger Tapping Phone" |

Alternate takes come from the same files and from: 112556 nickrave "BreathingWomNose.aiff" (breath), 60901 Nabito
"phone.wav" and 108738 BranRainey "phonering.WAV" (ring), 69585 JamesOC "CellPhoneVibrate.aif" (buzz), 388454 vovere
"Movement in bed, blanket.wav" (roll-over), 444405 MootMcnoodles "Sheets Rustling.wav" (sheets), 637747 kyles "phone
cordless plastic put down on table.flac" (set-down). Downloaded and not used: 265040 therisingorder, 722717 tsjhwt
(breathing), 587036 AntoineRomo (sheets), 371027 theshuggie, 777044 RinshiFox (taps); all CC0.

## Made for the project (not third-party downloads)
- **Nadia's 3D model** (`Models/Nadia/`): generated with Tripo Studio from the project's own style-E character
  sheet, on the creator's account (see `Models/Nadia/README.md`).
- **Elias's voicemail** (`Audio/vo_elias_voicemail.wav`): the creator's own recording.
- **The news report** (`Audio/vo_news_anchor.wav`): the creator's script read by the Kokoro TTS voice af_nicole
  (Kokoro-82M, Apache 2.0), made by `tools/audio/make-vh-news.py`.
- **The room, the bed and blanket, the phone, the tablet, the mug, the notepad, the badge, and every stand-in
  picture** are made in code (`Scripts/Waking/`, `Scripts/Opening/VhStageBuilder.cs`).
- **Sounds:** the opening's breath, ring, buzz, rustles, set-down and tap are the CC0 recordings above; the other
  Visiting Hours sounds (the monitor, the ambience beds, the score's placeholder pad) are generated in code by
  JamKit's `ProceduralAudio`.
