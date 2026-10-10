# Visiting Hours: the opening and the title screen (the creator's direction, 2026-10-09)

> **Confirmed by the creator on 2026-10-10:**
> - The **ID badge (Settings)** and the **notepad (Credits)**, and these **timings**, as long as the result
>   looks good.
> - The **anchor is af_nicole** (a woman, so "her condolences").
> - The **script changed** (`script/opening-script.md` is the source): the voicemail now asks "You still good
>   for picking me up?", it's **three** in the morning, it adds "It's not true, and it was rude of me to even
>   imply it", and ends with "I'll see if I can get a cab back home, or maybe call Lena... G'night." **Nadia drops
>   the phone at "Well, it's three in the morning. You're probably asleep..."**
> - **The report names Elias** and the crossfire.
> - **Art is now style E** (the waking world in 3D), so the bedroom and the table are 3D scenes.

Replaces the slice's first opening (a ceiling with a phone overlay) and its TV-room title. To be built after the
art style is chosen and presenter mode is verified, in the chosen style. Timings are a first proposal for the
creator to adjust.

## Shot list
| Time | Picture | Sound |
|---|---|---|
| 0:00 | Black | Silence, then the phone ringing (two rings) |
| 0:03 | Fade up: first person from Nadia's pillow, a dark bedroom, streetlight on the ceiling. She turns towards the nightstand; the phone glows on it: "Elias" | Ringing, sheets rustling |
| 0:06 | Her hand reaches out and **grabs the phone just as the ringing stops**. The screen reads: missed call, Elias, 5:52 AM. Her thumb plays the voicemail | The ringing cuts off, a tap |
| 0:08 | She holds the phone near. *"Hey, Nads. It's me. Could I bother you for a..."* | Elias, close and clear |
| ~0:12 | As Elias **drops** the request (*"...it's five in the morning. You're asleep."*), **she drops the phone back onto the nightstand**, rolls away and throws the blanket over herself. The view goes dark under the blanket | A soft clunk, a big rustle |
| ~0:14 | **Cut: the phone from above**, face up on the nightstand, still playing on speaker. The edge of the bed, Nadia a shape under the blanket. She's already asleep | The rest of the voicemail through the phone's small speaker (thin, a little distorted); her slow breathing |
| ~0:14–0:36 | Hold on the overhead shot: *"...your phone's probably dead again... No cabs out here... Look. About yesterday. I'm sorry... G'night."* The screen slowly dims | |
| ~0:36 | **Fade to black on "G'night".** The phone's screen goes out last | |
| ~0:36–0:39 | Black, held | Silence |
| ~0:39 | | **The sad music blooms in** (the current placeholder pad stays for now) |
| ~0:42 | The title scene's pieces **fade in one at a time** (see below) | **The news report starts**, from the tablet's speaker. It plays **once**, never looping |
| End of report | **The title becomes interactive** | The music carries on |

**No "...sleep in" line**: Nadia doesn't speak in this opening. Holding Esc skips to the title.

## The title screen: a scene, not a menu
A **close-up of a table**: a tablet, a **half-drunk mug of coffee**, a pen. **No title for now** (it waits for
a better one than *Visiting Hours*). No red bar, no overlaid buttons, no How to Play.

| Object | What it does |
|---|---|
| **The tablet** | Plays the news report: the night road, emergency lights, the anchor. **Its own scrolling headline** runs along the bottom of the tablet's video, not across the screen. When the report ends, the video's **play button is the Play button** |
| **The coffee mug** | Its swirl of milk **spells "Quit"**. Selecting it quits. Hidden or inert in the web build |
| **Settings** | Another object on the table. Proposal: **Nadia's hospital ID badge on its lanyard** (her identity, her preferences) |
| **Credits** | Proposal: **a notepad under the pen**, the credits "handwritten" on it |

The fade-in order (the creator, 2026-10-11): **the coffee, then the badge, then the notepad, then the tablet, then
the table, then the room**, each arriving one after another out of the black while the report plays. The tablet
**takes up most of the frame**; the badge carries **a gear icon and the label "Settings"**; the notepad says
**"Credits"** at the top of its page.

**Interaction:**
- Hovering highlights an object (a soft glow, a small lift).
- Mouse click, or arrow keys and a gamepad to cycle through the objects, with Enter or A to select. Keyboard
  and gamepad players need the same access as mouse players.
- Presenter mode's chapter select still works from here.

## Voices
- **Elias's voicemail: recorded by the creator.** AI voices don't carry it. Direction and how to send it are below.
  It gets a **phone-speaker treatment** after the drop (the existing `reyna_phone` chain in FableAnimationTests'
  audio tools does exactly this) and stays clean while Nadia holds the phone close.
- **Nadia** has no lines here: only breathing and rustles (sound effects).
- **The news anchor:** a different synthetic voice through a "tablet speaker" effect. Three options are in
  `docs/audio/anchor-options/` (`*_tablet.wav` with the effect, `*_dry.wav` without):
  - `am_adam`: an American man;
  - `bm_george`: a British man;
  - `af_nicole`: an American woman.

  Or the creator records it, with the same effect.

### Recording Elias's voicemail (for the creator)
- **Script** (rewrite it freely):
  > *Hey, Nads. It's me. Could I bother you for a... (stops; realises the time) ...it's five in the morning.
  > You're asleep. And your phone's probably dead again, isn't it. (a tired half-laugh) No cabs out here at this
  > hour. Figures. (a long pause, deciding to say it) Look. About yesterday. I'm sorry. I shouldn't have said
  > that. (pause) ...G'night.*
- **Who he is in this moment:** thirty hours awake after an ER shift, standing in a cold car park at dawn. Warm,
  embarrassed to ask, proud, and he means the apology. He isn't sad: he has no idea what's coming. That's what
  makes it hurt.
- **How to record:** a quiet room (a wardrobe full of clothes is a great booth), the phone 15–20 cm from your
  mouth, slightly off to the side. Three full takes, plus one with extra-long pauses. A voice memo is fine.
- **Where to put it:** `FableAnimationTests/audio/visiting-hours/raw/`. It gets cleaned, the pauses are timed to
  the shots, and the phone-speaker treatment is added.

## As built (2026-10-10): the 3D opening and the title table, in style E
**Where:** `Assets/JamKit/Templates/VisitingHours/Scripts/Opening/` (the sets, the opening, the title) and
`Scripts/Waking/` (the reusable waking-world look); the shader `Shaders/VisitingHoursWaking.shader`; the data in
`Resources/JamKit/VisitingHoursBoot.asset`; the editor side in `Editor/VisitingHoursOpeningContent.cs`.

**How it hangs together.** With Visiting Hours as the template, the title scene gets two 3D sets built in code
(`VhStageBuilder`): Nadia's bedroom at night and, 40 m away, the title table in the morning. One camera moves between
named poses (pillow, nightstand, away, overhead, table). The opening (`VhOpening`) plays the boot asset's cue list on
them; at its end the table (`VhTitleTable`) is the menu. The shell's own title buttons stay wired underneath (the
table presses them), so Play, Settings, Credits and Quit behave exactly as before; their menu is hidden.

**The shots, as built:**
1. Black; the phone rings and buzzes twice.
2. Fade up from her pillow: the ceiling, the streetlight's stripes through the blinds (a spot light below the
   window, the blinds' slats casting the stripes), cold moonlight. She turns to the nightstand; the phone glows,
   "Elias", the call rings pulsing.
3. Her hand comes out from under the blanket, takes the phone as the ringing stops (missed call, then the voicemail
   card), brings it close, her thumb taps play; the voicemail plays clean.
4. At "Well, it's three in the morning..." she sets it back down (a clunk), the voice moves to the phone's small
   speaker (Unity's high-pass, low-pass and distortion filters, crossfaded), she rolls away and the view goes dark
   under the blanket.
5. Cut: the phone from above on the nightstand, still playing, its glow on the wood; the edge of the bed, Nadia a
   shape under the blanket with her hair on the pillow, breathing slowly (the blanket rises with each breath).
   A slow push in; the screen dims.
6. "G'night": the room's light goes, the phone's screen last, then black. Silence; the music blooms.
7. The table: out of the black as the news report starts, the pieces arrive one after another: the coffee
   (+0.5 s), the badge (+6 s), the notepad and its pen (+11.5 s), the tablet (+17 s), the table under them (+23 s),
   the room, its floor and wall (+29 s); the report runs 38.9 s. The tablet's video drifts and crossfades its frames, the emergency lights pulse, the LIVE tag and its
   own headline scrolls along the bottom of the video. When the report ends the video pauses and its play button
   comes up: the title takes input.

**The title table:** seen from a seat at its near edge (the camera at 1.10 m, 0.40 m in front of the tablet's
foot, looking down at it, 50 degrees field of view). A big tablet (46 x 30 cm, a 16:10 screen in a thin bezel, a
front camera) stands on its folio in the middle and fills most of the frame (about half its width, 60% of its
height); the mug stands at the front right with the badge beside it (its lanyard looped towards the middle); the
notepad lies at the front left with the pen across it. The badge's card has a gear and "Settings" on its lower
band, the notepad's page "Credits" (underlined) with handwritten names under it: both are written onto the
pictures in code (`VhTextures.BadgeSettings`, `NotepadCredits`; the builder imports `op-badge` and `op-notepad`
readable for it). The tablet is Play, the mug Quit (inert and never highlighted in the web build), the ID badge
Settings, the notepad and pen Credits. The mouse highlights what it's over (a warm glow and a small lift) and a click
chooses it; the arrows, the d-pad or the stick move to the nearest thing in that direction (the first press
highlights the tablet), Enter or (A) chooses. It pauses while Settings, the credits or the presenter panel are up.

**Re-timing a new recording.** Every cue after the voicemail starts hangs off its markers (`voicemail:drop`,
`voicemail:goodnight`, `voicemail:end`, `news:start`, `news:end`). Three ways:
- **Tap along:** press Play, then *JamKit > Visiting Hours > Re-time the Voicemail*. The opening plays with a
  prompt at the top; tap Space as Elias starts each line shown. When the voicemail ends, the taps are saved as the
  markers (Undo works).
- **By hand:** `VisitingHoursBoot.asset > Tracks > voicemail > Markers`: seconds into the recording (`drop` is
  where she drops the phone, `goodnight` the "G'night"). `Recorded Length` is the take they were timed on.
- **Do nothing:** a take of another length stretches the markers in proportion (logged) until they're re-timed.
Any other beat: `Cues` (an `At` time, or an `Anchor` plus an `Offset`). *JamKit > Rebuild Shell Scenes* checks them
(an anchor that never resolves, a marker past the end of its recording) and the EditMode tests do too.
The timing is set on the creator's first take (32.3 s), from its pauses and a Whisper transcript. That take mixes
the scripts ("Could I bother you for a...", "Well, it's **five** in the morning...", no "It's not true..." line), so
the markers' lines are what he says in it. Playback skips its 3.9 s quiet lead-in (`Trim Start`), so "Hey, Nads"
comes about a second after her thumb; "drop" is at 10.25 s and "goodnight" at 30.2 s of the recording.
**Subtitles are off** (`Show Subtitles`): the shot list has none. Turn them on and they follow the markers.

**The look (style E, reusable).** `VhWakingStyle` makes one material per palette colour from `Materials/VH_Waking.mat`
(shader *JamKit/VisitingHours/Waking*): URP's real lights and soft shadows, shaded in soft steps (a lit side and a
cool shade side), a gentle rim, a hemisphere ambient the scene sets, a thin dark outline, a dithered fade and a
hover glow. `style.Restyle(model)` pulls imported models (Kenney's are named wood, carpet, metal...) into the
palette. Any waking-world scene can use it the same way.

**Models.** Kenney's Furniture Kit (CC0; `ThirdParty/LICENSES.md`): the nightstand, the lamp, the chair (her scrubs
over it), the rug, a low bookcase with books, a plant. Made in code: the room, the window and its blinds, the
streetlight and the buildings across the street, the bed, pillows and blanket, the framed photo, the phone, and the
whole title table. The phone is built as a device: a rounded dark-metal frame with softened edges, black glass,
the screen set into the glass inside a thin bezel with rounded corners, a camera bump with two lenses and a flash,
the power and volume buttons; the tablet the same way. Everything on the nightstand is placed on the model's real
top: the lamp at the back by the wall, the phone on the bed's side within her reach, the framed photo at the front
corner, leaning back on its strut, turned towards her pillow. **Nadia** is her Tripo model (`Models/Nadia/`,
imported at scale 1.69 by `VhNadiaImport`): lying on her side, head hidden in first person, her right arm posed
by two-bone IK to the phone, her fingers bent round its edges (`SetGrip`: from above while it lies on the
nightstand, then cradled from behind once it's up, the heel of her hand at its lower edge, the fingers across its
back, the thumb free over the screen to tap); turned away for the overhead shot with her hair on the pillow. The
blanket is shaped over her real body: the builder poses her both ways, bakes her skinned mesh and drapes the
blanket over the highest points (widened, smoothed, plus its thickness), so nothing of her pokes through and
there's no ridge round her outline. Without the model (or without Mixamo arm bones) a stand-in arm and
hair are built instead (the blanket then follows a drawn shape of her): the seam is `VhNadiaRig` (`SetMode`, `SetHand`, `SetGrip`,
`SetThumb`, `SetBreath`, `ShowArm`), so a
better body is one more subclass.

**Pictures.** The builder looks in `Art/` (any subfolder; `-` and `_` alike, a `-takeN` suffix ignored) for:
`op-coffee` (the coffee with "Quit"), `op-notepad`, `op-badge` (`op-badge-photo` goes into the drawn badge),
`op-phone-wallpaper` (the lock screen, and the framed photo), `op-news-*` (the tablet's frames), `tex-wallpaper`,
`tex-woodfloor`, `tex-tablewood`, `tex-knit` (the blanket), `tex-bedsheet`. Anything missing is drawn in code in
the same palette (the coffee writes "Quit" in milk, the notepad "Credits", the badge "Settings") and logged as `JAMKIT_VH art missing`.
16:9 pictures are cropped to their surface (a round coffee, a tall page, a card). Run *Rebuild Shell Scenes* after
new art lands.

**Sounds.** The ring, the buzz, the sheets, the phone set down, the tap and her breathing are placeholders made by
`ProceduralAudio` (`vh_phone_ring`, `vh_vibrate`, `vh_rustle`, `vh_rustle_big`, `vh_clunk`, `vh_tap`, `vh_breath`):
give an id a clip in the Audio Registry to replace it. The music is still the placeholder pad.

**Skipping and testing.** Hold Escape (or B) to skip to the title. Tests skip the opening (batch mode) and play it
fast on purpose; the smoke run plays it at 30x and waits for the table before pressing Play (`SmokeRunner.TitleGate`).
