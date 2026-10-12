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
3. Her hand comes out from under the blanket and takes the phone by its end as the ringing stops (it lies with its
   bottom end 4 cm over the nightstand's edge: her fingers go under that end, her thumb on its face, her palm on its
   back from the start, so it never turns over in her hand). Missed call, then the voicemail card; she brings it
   close, her thumb taps play; the voicemail plays clean.
4. At "Well, it's three in the morning..." she sets it back down (a clunk) the same way, and rolls away. She drifts
   off: his voice fades to nothing over 3 s, her eyes close in a slow blink (to black, open again, heavy) and then
   stay shut.
5. Cut: the phone from above on the nightstand, still playing, its glow on the wood; the edge of the bed, Nadia a
   shape under the blanket with her hair on the pillow, breathing slowly (the blanket rises with each breath).
   His voice comes back here, clear: the phone-speaker filter is no longer used (his lines must be understood).
   A slow push in; the screen dims.
6. "G'night": the room's light goes, the phone's screen last, then black. Silence; the music blooms.
7. The table: out of the black as the news report starts, the pieces arrive one after another, paced by the
   report's own length (cues anchored to shares of it, "news:14%"): the coffee (2%), the badge (14%), the notepad and
   its pen (27%), the tablet (41%), the table under them (54%), the room, its floor and wall (67%). On the 17.6 s
   report (a clear newsreader) that's every 2.3 s or so, the room starting at about 11.8 s and all up by about 14 s. The tablet's video drifts and crossfades its frames, the emergency lights pulse, the LIVE tag and its
   own headline scrolls along the bottom of the video. When the report ends the video pauses and its play button
   comes up: the title takes input.

**The title table** (laid out to the creator's sketch, 2026-10-12: `FableAnimationTests/art/review/2026-10-12/title-screen/creator-sketch.png`):
seen from straight above (the camera 0.37 m over the table, 50 degrees field of view), at night under one warm lamp off
the frame's upper right, with a faint cold moonlight fill; graded like the bedroom's night. The big tablet (46 x 30 cm)
lies flat, screen up, turned 11.5 degrees, filling the upper left and running off the left edge (its right side about
70% across). The quit piece is upper right: the mug, its handle cut by the right edge and "Quit" in its milk reading
straight up. The notepad is at the bottom, cut by it, with the pen across it. The badge (1.4 times life size, so
"Settings" reads) is bottom right, cut by the bottom edge, its lanyard arching from the clip and curling left.
**The quit piece** is one setting (`VhStageBuilder.QuitPiece`, now "mug"; the creator is choosing a thematic object). On
the **Despair route** (the save's `vh_despair`) it's an amber pill bottle lying on its side, its label reading "Give Up".
Its piece id stays "mug" (the quit slot), so the cues and the editor's "title-mug" cover both.
**Checking it from a build:** `VisitingHours.exe -title-shot "<path>.png" [-title-variant normal|despair]` skips the
opening, waits for the table, saves a screenshot and quits (`VhTitleShot`). The badge's card has a gear and "Settings" on its lower
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
by two-bone IK to the phone, her palm always on the phone's back and kept off it, the fingers nearly straight and stopped where they touch it (`SetGrip`: by its end while it lies on the
nightstand, fingers under it and thumb on its face, then cradled from behind once it's up, her wrist under its lower edge, the fingers along its
back to just past its far edge, the thumb beside its near edge and onto the screen only to tap); turned away for the overhead shot with her hair on the pillow. The
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
fast on purpose; the smoke run plays the bedroom part at 30x and, once the opening reaches the table, ends it there
(it doesn't sit through the news report) before pressing Play (`SmokeRunner.TitleGate`, `VhBoot.SmokeGate`).

## Lighting (built 2026-10-11)
The creator asked for a severe overhaul: "it's not at all cinematic", and the title is the first thing every player sees.

**Where:**
- `Scripts/Opening/VhLighting.cs` builds the rig and the grade once both sets exist. `VhStageBuilder` calls it; the staging isn't touched.
- `Scripts/Opening/VhLightingRig.cs` handles the moment-to-moment changes.
- Every number is a commented constant at the top of `VhLighting`.

### The bedroom at night
- **The key light** is the warm sodium streetlight coming up through the blinds. It lays amber stripes across the ceiling and the wallpaper above the bed.
  - The stripes come from a **light cookie traced from the real window and slats**, on every platform. The spot light's own soft shadows (PC) thicken the 4 mm slats until the gaps close, and WebGL has no spot-light shadows at all.
- **The moon is the main light:** cold blue through the window. It casts the room's shadows, including the blinds' stripes across the bed.
- **A faint cool spill** comes in through the window. The general fill is low and cool, so the room is dark but readable.
- **The phone is the only light near her:**
  - face up, a soft cool pool on the nightstand;
  - in her hand, a second light from the screen onto her fingers and the blanket.

  Both sit 12–14 cm off the glass at small intensities: URP's lights fall off with the square of the distance, so a light a few centimetres from a surface blows it out.
- **The streetlamp and the lit windows** across the street glow and bloom.
- **The night grade:**
  - ACES tonemapping;
  - teal shadows and warm highlights;
  - blacks just above black;
  - bloom, a vignette and light film grain;
  - the background falls out of focus behind the phone in the phone shots;
  - at "G'night" the exposure falls with the room's light.

### The title table in the morning
- **The sun** is warm, about 35° up, from the camera's left. Its **window-pane cookie** lays a sun patch about 0.85 m across on the table, with the cross of the window bars falling between the pieces. Everything outside the patch is in cool shade, and the shadows are long and soft.
- **A cool sky fill** comes from the room side.
- **A soft warm glow** of morning falls on the wallpaper.
- **The tablet's screen** throws a faint glow on the table. While the report plays, it alternates gently between red and blue with the emergency lights.
- **The morning grade:** ACES, warm and quiet, slightly lifted blacks, soft bloom, a gentle vignette, the wallpaper out of focus behind the tablet.

### Shadows, SSAO and WebGL
- **Sharp shadows:** while the sets are up, the pipeline's shadow distance is 7 m instead of 50, so the shadow map covers just the rooms. It's given back when they go.
- **Contact shading on PC:** the waking shader (`VisitingHoursWaking.shader`) now reads URP's SSAO, and has a DepthNormals pass to feed it.
- **WebGL** (the Mobile quality level) gets the same picture: cookies, Gaussian depth of field, bloom, tonemapping, SMAA. It has no SSAO and harder main-light shadows.
- **Safety:** the camera stops NaNs, and bloom is clamped at 20, so one blown pixel can't flood the frame.

### Cost (the Perf agent's list, docs/redesign/perf.md; the Day agent, 2026-10-13)
- **Nadia and the blanket** skin only while on screen (`updateWhenOffscreen` off, inside their mesh bounds plus 0.8 m, so
  her arm reaching for the phone never pops out); play mode only, the editor's captures still re-skin every render.
- **A light dimmed to nothing is switched off** (the title's lights were left drawing at intensity 0).
- **The phone's and the tablet's UI panels** stop redrawing into their textures while their screens are out of view
  (`VhScreenPanel.SetDrawing`; the picture stays, the UI is put back when they're seen).
- **The stand-in pictures** (`VhTextures`) are compressed and drop their CPU copy once the set is built (`Seal`).
- All in `Scripts/Opening/VhTitlePerf.cs`, settled by `VhStageBuilder.Build`. Unmeasured until the Perf agent's probe runs.

### Iterating
- `tools/vh-lighting-capture.ps1` (or *JamKit > Visiting Hours > Capture the Lighting* in the editor) renders seven moments at 1920x1080 to `out/vh-lighting/<time>/`, on PC and on WebGL quality:
  1. the dark room
  2. the phone lit on the nightstand
  3. the phone held close
  4. the drop, seen from overhead
  5. towards "G'night"
  6. the title while the report plays
  7. the title at rest
- Unity runs in batch mode with graphics. Rebuild the scenes first, so the boot asset carries the art and Nadia's model; otherwise the stand-ins show.
- It took five rounds:
  1. blown-out lights near surfaces and a red frame;
  2. the distances fixed;
  3. the stripes fixed on PC, and the title's sun made to read as sun;
  4. thinner window bars and a warm wall;
  5. a touch less sun and more fill for the real, darker wood.

### What the creator should look at
1. The streetlight's stripes as the opening fades up.
2. The phone's light on her hand.
3. Whether the title's sun patch reads as morning and is the mood you want. The alternative is lighting from behind the tablet, which is moodier, with the shadows falling towards you.
4. Whether "Quit" in the coffee reads in the sun.
