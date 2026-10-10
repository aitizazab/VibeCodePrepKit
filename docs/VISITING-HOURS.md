# *Visiting Hours*: the current state (the working title; a better one is coming)

**The single source of truth for the game.** Where an older doc disagrees, this one wins. Updated 2026-10-10.

## The premise (the creator's, as of 2026-10-10)
- **The world:** a near-future city sliding into dystopia, Cyberpunk without the cybernetics. Corporations run
  the streets and still pretend to be decent. Two of them, **LeBaget** and a second fictional one (name to be
  chosen; not a real company), fight a quiet war that spills into the streets.
- **Elias Lasname** (the surname is a placeholder) is an ER doctor who **founded Saint Brigid's** for the local
  community. He comes from money; where it came from is never said. A good man with an abrasive personality.
- **Nadia**, his younger sister, is a nurse at Saint Brigid's and **usually picks him up from work.**
- **The day before:** Elias implied Nadia only got her job because she's his sister. It hurt because it's half
  true: she was qualified, but the other candidate was just as good.
- **The night:** Elias stays late at work, **putting off facing her**. Around 3 AM he leaves a voicemail asking
  whether she's still picking him up, guesses she's asleep with a dead phone, apologises ("It's not true, and it
  was rude of me to even imply it"), and says he'll get a cab or call Lena. Driving home, **his car is caught in
  the crossfire** between the two corporations (the cause is external, which leaves room for more later).
- **Lena**, his wife, is **missing**: her body was never found (for later). **Theo** (10) was killed.
- **Elias is in a coma** at his own hospital. **Mags** runs a **secret, off-the-books** Dream Bridge; Nadia
  goes into his dream.
- The design story (layers, the dilemma, endings, the Force and Long Night spectrum) is in
  `STORY-VISITING-HOURS.md`, with decisions in `SLICE-VISITING-HOURS.md`. The premise above supersedes
  their details: the call time, the cause of the crash, and Elias's background.

## Structure and endings (the creator, 2026-10-10 night)
- **Five nights, not all the same length.** Each player's time in a night can differ from another's,
  depending on what they choose to do or skip.
- **The open threads** (Lena missing, the corporate crossfire, Elias's money) are **hinted, not resolved**:
  clues for the attentive player, perhaps one reframing reveal near the end, no full answers.
- **Endings:**
  - **The Long Night stays** (much of the premise was built for it).
  - **Morning** absorbs "His Choice" **if that ending's outcome is shown in the game**, giving one ending with
    two paths:
    - **forced:** Nadia breaks the dream;
    - **chosen:** she tells him the truth and he decides to wake.
  - **Evening:** she lets him stay.
  - The design docs propose the details for the creator to approve.
- **The argument battles get rebuilt from the ground up:**
  - **Unique art per battle**, stylised battle icons and **arenas**: no dodging rectangles in the green or red
    half of a plain box.
  - **The argument must make sense** as an argument (claims, evidence, rebuttals that follow logically), not a
    loose association of concepts.

## Characters
`CHARACTERS.md`: Nadia, Elias, Lena, Theo, Mags, Walt, with looks for each register.

## Art: style E (the split), chosen on 2026-10-10
- **The waking world:** grounded, muted, stylised but adult, in **full 3D models**.
- **The dream:** cutesy pastel doodle with dread underneath; **billboards exist only in the dream**, and the
  dream itself is **real 3D** (so escape sequences can feel good).
- Each character's **introduction pose says who they are**; scene-specific poses are made per scene.
- Character sheets: being made (turnarounds for 3D, dream billboards, expressions).
- The style exploration: `FableAnimationTests/art/review/2026-10-10/visiting-hours-styles.html`. The first
  painterly set was rejected (hyper-real portraits don't work as billboards).

## The opening and title (the creator's direction)
`OPENING-AND-TITLE.md` has the shot list and the diegetic title: a table with a tablet (its video's play button
is Play), a coffee mug whose swirl spells "Quit", **the ID badge (Settings)** and **the notepad (Credits)**.
Both are confirmed, as are the timings. No title yet; no How to Play.
- **With the new script, Nadia drops the phone at** "Well, it's three in the morning. You're probably asleep...".
- The editable script is `script/opening-script.md` (the creator's file: don't edit it while it's open in their editor).

## Audio
- **Elias's voicemail:** the creator's own recording (an earlier take, cleaned and in the game). A re-record to
  match the final script is due.
- **The news report:** Kokoro's **af_nicole** (the creator's pick) through a tablet-speaker effect, generated
  from the script by `tools/audio/make-vh-news.py`. **Pending:** the script's "his condolences" becomes "her"
  (the reporter is a woman). The creator makes that edit; then re-run.
- **Music:** the placeholder sad pad stays for now. The real score is composed with the creator.

## Movement (the dream)
Titanfall-2-style: double jump and wallrun are core, and **momentum carries between moves**. The research, with
measured numbers (wallrun goal 8.6 m/s, 1.75 s; wall jump 5.2 m/s off the wall + up to 1.9 along it + up to 4.4
inward; wall kick within 83 ms), is in `RESEARCH-MOVEMENT.md`. The build is in progress: the Movement Lab
course, then `MOVEMENT.md`.

## Status
| Piece | State |
|---|---|
| The slice (opening, Day 1, booth, street, journal, Walt's argument) | Built and verified in Unity, in the superseded painterly art and old opening |
| The movement module and Movement Lab | Being built |
| Style-E character sheets | Being made |
| The opening and title rebuild (style E, new script) | Next, after the sheets and the 3D models |
| The dream street in 3D, an escape sequence | After the opening |
| Music | With the creator, later |

## Open questions for the creator
1. **The second corporation's name** (fictional, obviously corporate). Proposals: *Northline Consolidated*,
   *Vantor & Pryce*, *Grayhaven Protective Services*, *Solenne Dynamics*, *Cordell-Ashby Group*,
   *Obelisk Civic Solutions*. Search the pick once to be sure it isn't real.
2. **Tripo credits** for the waking-world 3D models (about 80 per character; Nadia first).
3. **A re-record** of the voicemail to the final script.
4. **The title** (later).
