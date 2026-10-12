# *Visiting Hours*: the vertical slice (spec, 2026-10-09)

> **Status: the spec the first slice was built from (2026-10-09).** Superseded where it conflicts with `VISITING-HOURS.md`: the **art** (now style E: 3D waking world, billboards only in the dream, a 3D dream), the **opening and title** (`OPENING-AND-TITLE.md`), and the premise. The creator's notes in `STORY-PROSE.md` (2026-10-11) also overrule it: Walt's tutorial and the stage-strike exit are rejected as built, and the endings are the creator's (see `VISITING-HOURS.md`). Its systems (journal, deductions, argument battle, rushing check) still stand.

The slice tests whether the format works before more is built: first-person exploration with no guns, a
journal of facts and deductions, and Undertale-style argument battles. The story is in
`STORY-VISITING-HOURS.md` (draft 1), and the decisions below supersede it where they differ.

## Story decisions since draft 1 (the creator, 2026-10-09)
- **Elias is an ER doctor.** Around 3 AM, having stayed late to put off facing her, he called Nadia for a ride and
  left a voicemail; she heard the start and fell asleep as it played (the premise in `VISITING-HOURS.md`).
- **The call opens the game** (see "Opening"); it isn't hidden.
- **The deeper hidden layer:** when Nadia didn't pick up, Elias called Lena (3:06). Lena drove out with Theo
  asleep in the back, and they drove home. Nadia doesn't know yet. The police report's call log is the bomb
  under the table for attentive players: it's shown, and nobody comments on it.
- **The Dream Bridge is secret.** Mags runs it off the books after visiting hours. Forcing Elias out is selfish:
  if it's done "cleanly" he might think it was a dream. That's what Nadia believes.
- **Days are real gameplay:** short first-person hospital and real-world scenes where facts the dream can't give
  are found. Neglecting the job raises a discovery risk (later).
- **Force and Stay are opposite ends of a spectrum.**
  - **Force** is the player imposing their will on Nadia. It's loud, she doubts herself at every step, and it
    can be called off at each doubt (with repercussions).
  - **The Long Night** creeps up. It needs a hard-to-reach but slippery hidden despair value, and it locks in
    well before the end **through an innocuous choice**: a strange, curious one that sneaks up on the player
    (the creator, 2026-10-11, like Deltarune's Snowgrave). Which choice is undecided.
  - **The rule-break (the Sans equivalent) is broader than battles** (the creator, 2026-10-09). In the Force
    route it's **Lena as a character**: she breaks the game's rules wherever she appears, not only in her battle
    box. In the Despair route it's **the whole route after the lock-in**: the game itself stops behaving normally,
    and that's the uncanniness. Not in the slice.
- **Teaching, each by someone it makes sense for:**
  - **Mags** explains the rig: movement and the journal.
  - **Nadia's charting habit** teaches recording facts (day 1).
  - **What teaches the argument battle** is undecided: Walt's tutorial as built was rejected (2026-10-11).
  - **The dream** teaches escapes (later).
  - Withhold meaning and stakes, never mechanics.
- **A rushing check:** heading deeper with open questions in the journal makes Nadia think *"I don't know enough
  yet."* The journal lists the open questions. It's never a hard block.

## Argument battles: how deductions are made inside them
**Ideas considered:**
1. Firing deductions at statements, like Danganronpa (rejected as the main verb).
2. **Objects in the box**, like Omega Flowey's ACTs. Deductions you've made appear as objects drifting into the
   box during the statements they answer. You reach one through the bullets to say it. **Chosen.**
3. Catching glowing words: the speaker's sentence is the bullet stream, and its contradicting word glows.
4. **Position as stance**: the box's lower half is *listening*, the upper half, nearer the speaker, is
   *pressing*. **Chosen together with 2: no menus at all.**
5. Holding still in a safe pocket for a whole line to "hear" it (a variant of 4).
6. Tracing a shape with your movement (a gear, a word): possible for later bosses.

**The chosen design (2 plus 4):**
- **The layout:** the speaker's portrait sits above the box. Nadia's **composure** is a small light in the box,
  with 5 hits.
- **Their lines become the volleys:** each statement shows as a subtitle while its pattern plays. The patterns
  use the core `PatternDef` and `Emitter`, styled by emotion:
  - gentle rain for "go home";
  - slow rings for reassurance;
  - walls for denial;
  - spirals for a lesson.
- **Stance is where you are:**
  - the **lower zone (Listening)** has a soft glow;
  - the **upper zone (Pressing)** sits nearer the portrait, with denser, faster patterns.

  At the end of each statement your zone counts as a choice: Listen or Press. Pressing adds to the hidden Force
  tally.
- **Speaking with objects:**
  - During a statement, the deductions and memories you've **actually made** drift into the box as small object
    icons (the memorial card, a key...).
  - Touching the **right one during the right statement** cracks it, with a line, a flash and a softer next volley.
  - Touching a **wrong one** brings a line ("That's not what this is about") and a harsher next volley.
  - Objects you haven't found never appear: you can't say what you don't know.
- **Outcomes:**

  | Outcome | How | What it gives |
  |---|---|---|
  | **Convinced** | Every *key* statement cracked | A **truth** entry. Truths gate endings |
  | **Heard out** | Listened through every statement, never pressed, no wrong objects | You pass for tonight; they soften; a **personal** entry (an object of theirs) |
  | **Pressed** | Pressed at least half the statements | You pass; they're hurt and hostile; Force tally +1 |
  | **Composure lost** | 0 hits left | Nadia breaks down, Mags pulls her out, and the night ends |

  Mixed play falls through to Heard out or Pressed, depending on which you did most.
- **Progression never needs Convinced.** Truths only gate endings.

## The slice's content
**1. The opening** (no player input until the title screen):
- Black screen; a phone ringing. Nadia's eyes open: a first-person view of a dark ceiling, a slow fade-in.
- The phone screen: "Elias calling…", then "Missed call: Elias, 5:52 AM", then a voicemail plays with subtitles
  (the creator will rewrite and record it; placeholder text):
  > *"Hey, Nads. It's me. Could I bother you for a... (pause) ...it's five in the morning. You're asleep. And your
  > phone's probably dead again, isn't it. (small laugh) No cabs out here at this hour. Figures. (pause) Look.
  > About yesterday. I'm sorry. I shouldn't have said that. ...G'night."*
- Nadia starts to sit up. At "five in the morning" she sinks back into the pillow: *"...sleep in."* (a small
  happy sigh). Fade to black.
- **The title screen** loads its menu while a TV news report plays behind it (a still TV frame with a lower-third
  ticker reading "Route 9: two dead, driver critical after early-morning crash" and the anchor's voice), with
  sorrowful placeholder music.

**2. Day 1: St. Brigid's, six weeks later** (first person, small):
- The nurses' station; corridor; **room 14** with Elias in bed, monitors beeping, and a family photo of Elias,
  Lena and Theo on the bedside table.
- **Teaching the journal (Nadia charts):** examining the chart writes a fact, in Nadia's voice ("Charting:
  GCS 6. No sedation. Scans normal. He just won't wake up.").
- **Facts to find:**
  - `fact_chart`: no reason he shouldn't wake.
  - `fact_effects`: his effects bag, a keyring with a tag "W.A. workshop" and a folded memorial card,
    "Walter Abernathy, 1941–2015".
  - `fact_police_report`: an envelope at the station for the family, with the timeline and the phone log:
    5:52 call to Nadia, not answered; 5:58 call to Lena, 2 minutes. **No comment on it, from anyone.**
  - `fact_photo`: the family photo.
- **11 PM: the closed sleep-research wing.** **Mags** in the booth: introduction, the rig, controls, and how the
  journal is used inside the dream. She's frank about the mechanics and evasive about the 9 dives that went
  badly ("Let's not do that tonight.").

**3. Night 1: Walt's street** (first person, the dream):
- A Saturday at golden hour: lawns, sprinklers, a barbecue, a golden retriever, two neighbours (a woman watering
  roses, a man at a grill). The houses are **painted flats**, like a stage set: a dream look, and cheap to make.
- **Memories to find:**
  - `mem_walt_alive`: Walt at the barbecue, waving.
  - `mem_newspaper`: the newspaper on a porch, dated the morning of the crash.
  - `mem_shop_sign`: "Abernathy Cycles, since 1971", Walt's garage.
  - `mem_saturday`: the radio and neighbours repeating that it's Saturday.
- **Deductions** (combine two entries in the journal):
  - `fact_effects` + `mem_walt_alive` gives **`ded_walt_dead`** ("Walt died in 2015. He can't be here.").
    **The key deduction.**
  - `mem_newspaper` + `mem_saturday` gives **`ded_day_before`** ("He's living the day before the crash, over and
    over."). Optional.
- **The rushing check:** walking up to Walt with open questions (any entry that could still combine) makes
  Nadia think *"I don't know enough yet."* The open questions show in the journal. It never blocks.
- **Battle: Walt Abernathy.** He teaches the box first ("Easy. Watch the pattern first."), with one gentle
  practice volley that can't hurt. Then four statements:
  1. "Go home, Nadia." (gentle rain)
  2. "He's fine here. Look at him." (slow rings)
  3. "I've always been on this street." (walls). **Key:** `ded_walt_dead`.
  4. "You can fix anything, if you're patient enough." (a spiral, his old lesson). Optional crack:
     `ded_day_before` ("Not this. You can't fix this by doing it again.").

  **What each outcome gives:**
  - **Convinced** gives `truth_walt`: *"He brings Walt back because Walt is the one who told him you can fix
    anything."*
  - **Heard out** gives `pers_wrench`: Walt's wrench.
  - **Pressed** gives a hurt line, and Walt steps aside.
- **The end of the slice:** the street dims, Mags pulls Nadia out, and a card reads "End of the slice. Notes for
  the creator: docs/SLICE-NOTES.md".

## Systems to build
Template folder `Assets/JamKit/Templates/VisitingHours/`, asmdefs `JamKit.VisitingHours` and `.Editor`. Pure
logic goes in `Packages/com.vibejam.jamkit/Runtime/Core/VisitingHours/`, with dotnet tests.
- **First person:** reuse the DOOM-like's controller, interaction and billboards, with **no weapons and no
  combat**. Use (E) examines. Walking only, at calm speed (a short jog on Shift). Billboard people face you
  (Danganronpa-style flats). The low-res look stays off by default here, so the art reads.
- **The journal** (core plus UI Toolkit):
  - Tabs: Facts / Memories / Deductions / Truths.
  - Combine: pick two entries; a valid pair makes a deduction (data-driven pair table), and an invalid one gets
    a gentle line.
  - An "open questions" list: entries that still have an uncombined partner.
  - **Scribbled-out lines** (Nadia's own notes, legible if you look closely) are supported. The slice has one:
    *"I should have"*, scribbled out, under the police report.
  - Opens with J or Tab, and auto-opens the first time Mags explains it.
- **The argument battle** (core plus a 2D overlay): as specified above. Reuse core Patterns and Emitter and the
  bullet hell's `BulletPool`, so it works in the core without Unity. BattleDef data: statements (text, pattern,
  key deduction id, optional), outcome entries.
- **Sequences:** the opening, the title, scene transitions, Mags's lines and Walt's lines, through the shell's
  ink DialogueService with portraits.
- **Audio:** placeholder voice files from main (see "Audio") in `Assets/JamKit/Templates/VisitingHours/Audio/`,
  plus the shell's procedural sounds for the ring, the heart monitor and ambience. Music: a placeholder sad pad
  (procedural), clearly marked as placeholder; the creator composes the real score.
- **A template switch** ("JamKit/Use Template/Visiting Hours") and a smoke driver that walks the slice with
  scripted inputs: it collects the facts and memories, combines `ded_walt_dead`, wins Convinced, and reaches the end.
- **Tests:** core (deduction table, open questions, battle outcomes, stance zones, objects only when known) and
  PlayMode (each outcome, the rushing line, the journal combine). Everything else stays green.

## Art (generated with Flow; the creator judges)
Style bible:
- **Painterly, grounded realism**: soft gouache and oil textures, clean silhouettes, muted palette. No text in
  the images; any text in-game is drawn by the UI.
- **The dream:** warm late-afternoon light, slightly too saturated, faintly hazy edges.
- **Reality (the hospital and the bedroom):** cool, flat fluorescent or night light, desaturated.
- Every generation is **16:9** (stated in the prompt), then cut out or cropped.

Files go in `Assets/JamKit/Templates/VisitingHours/Art/` with these exact names (PNG):

| File | What it is |
|---|---|
| `portrait_walt_neutral/_sad/_firm.png` | Walt, waist-up, transparent background. 70s, tall and thin, flat cap, rolled sleeves, grease-stained canvas apron, a kind lined face, white moustache |
| `portrait_mags.png` | Mags, waist-up, transparent. 50s Black woman, close-cropped grey hair, reading glasses on a cord, a cardigan over scrubs, tired and amused eyes, a mug |
| `billboard_walt.png`, `billboard_neighbor_roses.png`, `billboard_neighbor_grill.png`, `billboard_dog.png` | Full-body, front-facing, transparent |
| `billboard_elias_bed.png` | Elias asleep in a hospital bed, three-quarter view from the foot, transparent |
| `prop_family_photo.png` | Framed photo: a man in his 30s, a woman and a 10-year-old boy, laughing on a beach |
| `prop_chart.png`, `prop_effects_bag.png`, `prop_keyring.png`, `prop_memorial_card.png`, `prop_police_envelope.png`, `prop_newspaper.png`, `prop_wrench.png` | Item art for the journal and in-world props, transparent |
| `flat_house_1/2/3.png`, `flat_bike_garage.png` | Painted house facades and Walt's garage, front-on, transparent sky |
| `tex_grass.png`, `tex_road.png`, `tex_sidewalk.png`, `tex_hospital_floor.png`, `tex_hospital_wall.png` | Seamless tileable textures, square |
| `sky_dream.png`, `sky_night.png` | Panoramic skies |
| `bg_ceiling_dark.png` | The opening: a dark bedroom ceiling, a faint streetlight stripe |
| `ui_phone.png` | The phone frame (the screen area plain; the UI draws the text) |
| `bg_title_news.png` | The title background: a TV in a dim room showing a night-time road with emergency lights (no readable text) |
| `fx_static_wasp.png` | A small creature made of TV static (for later) |
| `journal_page.png` | A paper page texture for the journal |

## Audio (main makes these)
Placeholder synthesised voices, clearly marked; the creator records the real ones later:
- `vo_elias_voicemail.wav`
- `vo_news_anchor.wav`
- `vo_nadia_sleep_in.wav`

## Verification (main runs the Unity steps)
The usual `tools/verify.ps1` steps, the template switch, a Windows build and the smoke run, then a playable
build in `VibeJamKit-play/Builds/Play/VisitingHours/`. Notes for the creator go in `docs/SLICE-NOTES.md`.
