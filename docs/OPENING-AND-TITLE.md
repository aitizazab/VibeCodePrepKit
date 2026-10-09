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

The fade-in order follows the report: the table and the tablet first (the report starts on it), then the mug,
then the pen and notepad, then the badge.

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
