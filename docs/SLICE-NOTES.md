# *Visiting Hours*: notes on the vertical slice (for the creator)

> **Status: notes on the first slice build (2026-10-09)**, in its superseded painterly art and old opening. The questions about the battle, journal and teaching still apply. Current state: `VISITING-HOURS.md`.

The slice is built to the spec in `SLICE-VISITING-HOURS.md`. It's about 8 to 12 minutes long. This page says what
to judge in each part, what's missing, and the questions I'd like answered before more gets built.

## Playing it
- **JamKit > Use Template > Visiting Hours**, then Play. The opening plays before the title the first time per
  launch (hold **Esc** to skip it). **Use Template > Story** switches back.
- **Controls:** WASD or the stick to walk, the mouse or the right stick to look, **Shift** to hurry (a jog),
  **E** to look at something or talk to someone, **J / Tab / View** for the journal. In the argument, WASD or the
  stick moves the light.
- **Debug menu** (` or F1) > **Next scene**: Day 1 > the booth > the street > the argument (everything found) >
  Convinced > Victory. **God mode** makes the box harmless.
- **To try each outcome:** at the end card, **Argue with Walt again** puts you back in front of him with the journal
  as it was. Here's how to get each one:
  - **Convinced:** touch "Walt can't be here" during "I've always been on this street".
  - **Heard out:** stay in the lower half the whole time.
  - **Pressed:** end at least two statements in the upper half.
  - **Composure lost:** take five hits.

## What to judge, part by part

**1. The opening** (the phone, the voicemail, "...sleep in.", the news).
- **Timing:** does the black screen with the ring last long enough before her eyes open (1.8 s)? Is the missed-call
  screen held long enough to read? The voicemail's subtitles are timed for the placeholder text. When a recording
  exists in `Audio/vo_elias_voicemail.wav`, they stretch to its length (in proportion, so lines may drift). For exact
  timing, edit the cue times in `Resources/JamKit/VisitingHoursBoot.asset > Opening`.
- **Mood:** she sits up at "Could I bother you for a..." and sinks back at "...it's five in the morning". Does the
  sinking back read as a choice? Her "...sleep in." comes **after** the voicemail ends, not over it, so two voices
  never overlap (see question 1).
- **The title:** the TV picture, the red ticker and the anchor's voice (looping, quiet) under the menu, with the
  sad pad. **The pad is a placeholder** (`music_vh_sad_pad`, procedural): replace it in the Audio Registry.

**2. Day 1** (St. Brigid's, cool and flat).
- **Teaching:** Nadia charts. Examining the chart writes "Charting: GCS 6...", and a toast says "Charted: His chart,
  J journal". The stairwell won't open until she has charted him ("I haven't even charted him"): that's the one
  soft gate. A controls line shows for the first few metres.
- **Room 14:** do the bed, the chart, the bag and the photo read at their sizes (from 0.3 m props up to a 1.5 m bed)?
- **The police report:** it's at the nurses' station. The call log is in the entry. Nobody comments on it, the
  knot included (a test checks this). The scribbled-out "I should have" sits under it in the journal: faint and
  struck through, clearer when the mouse is over it.
- **11 PM, the booth:** Mags is only a portrait (there's no Mags billboard in the art list). She explains movement,
  E and the journal, the journal **opens by itself** with a one-line tutorial, and closing it continues her lines
  ("Nine. ...Let's not do that tonight.").

**3. The journal.**
- Tabs (Facts, Memories, Deductions, Truths; the wrench sits under Truths). Click an entry to read **and** pick it.
  Two picks and **Combine**. A wrong pair gets a gentle line, in turn.
- Is "click to read, and to pick" one verb too many? The alternative is a separate "pick" button per entry.
- **Open questions** are listed on the right: pairs where she has both halves but hasn't combined them. The HUD hint
  says "(1 open question)" too. Is that too much of a nudge?

**4. The street** (golden hour, painted flats like a stage set).
- Do the flats read as a stage set or as cardboard? They're 13 m wide and don't turn. The people turn to face you.
- **Memories:**
  - the newspaper on a porch (lying flat, south side);
  - the garage ("the garage" prompt, from up to about 7 m away);
  - Mrs. Lindqvist with her roses, or Mr. Okafor at the grill, for "Saturday" (either one gives it).
  - Walt waves when you first see him within about 15 m. That writes "Walt at the grill".
- **The rushing check:** going up to Walt with an open question plays *"I don't know enough yet."* with two
  choices, "Go up to him anyway" or "Not yet". "Not yet" opens the journal. It never blocks. Note that walking
  straight up to him writes "Walt at the grill" first, so if she has the effects bag, the check always fires the
  first time. Is that right, or too heavy-handed?

**5. Walt's argument.**
- He says "Easy. Watch the pattern first." (dialogue box, with his portrait), then plays one gentle practice volley
  that can't hurt. After that come the four statements:
  - rain for "Go home, Nadia";
  - slow rings for "He's fine here";
  - walls with a gap for "I've always been on this street";
  - a spiral for "You can fix anything".
- **Does position-as-stance read without explanation?** The box's halves are tinted: warm above (nearer the
  portrait), cool below. The half you're in glows. Bullets come faster and denser while you're in the upper half.
  In the last 2 seconds of each statement the faint words "press" and "listen" pulse, because that's when your
  half counts. Nothing else explains it. **The two words are the one concession.** Turn them off with
  `VisitingHoursConfig > Show Zone Words` to test whether the halves read on their own.
- **Do objects in the box feel like speaking?**
  - Things she has worked out drift in from the sides, a picture and a title on a soft disc.
  - Touching one shows Nadia's line in italics, then Walt's reply under it.
  - The right one cracks the statement: a white flash, his face changes, the statement ends at once, and the next
    volley is softer.
  - A wrong one gets "That's not what this is about." and a harsher next volley.
  - Entries she hasn't found never appear.
- **Each outcome's feel:**
  - **Convinced** writes the truth ("He brings Walt back because...") and gets his apology.
  - **Heard out** gives the wrench: "For the next thing that needs fixing."
  - **Pressed** gets "That's how you strip a thread". He steps aside, and Force goes up by 1, plus 1 per pressed
    statement.
  - **Composure lost:** Nadia can't breathe, Mags pulls her out, and "The night ends."
  - After the first three outcomes the street dims and Mags brings her up ("There you are. Drink this.").
- **Difficulty:** Settings > Difficulty scales the bullets' speed and density, as everywhere in JamKit.

**6. The end card:** "End of the slice", the outcome, a pointer to this file, **Finish** (Victory and credits) and
**Argue with Walt again**.

## Known gaps
- **The art:**
  - Until each painting exists in `Templates/VisitingHours/Art/`, the slice shows neutral stand-ins (silhouettes,
    flat houses, noise textures) and the build logs `JAMKIT_VH art missing: ...`. Run **Rebuild Shell Scenes**
    after new art lands, and the real files replace the stand-ins.
  - The builder sets each file's import settings: smooth, no pixel look, cut-outs keep their alpha, tiles repeat.
  - Sizes and positions are placed for the spec's descriptions and will need a pass with the real paintings
    (Walt 1.86 m, the bed 1.5 m, props 0.3 to 0.55 m, flats 13 m).
- **The voices:**
  - Until `vo_elias_voicemail.wav`, `vo_nadia_sleep_in.wav` and `vo_news_anchor.wav` are in
    `Templates/VisitingHours/Audio/`, the opening runs on subtitles and silence (`JAMKIT_VH audio missing`).
  - Run **Rebuild Shell Scenes** after adding them: the title's boot asset picks them up.
  - The anchor's voice has no subtitles: the ticker carries the news.
- **No Mags billboard:** she's a portrait in the booth's dialogue. The booth is a small dim room with a window and
  the observation glass. There is no rig or chair to see.
- **No sprinklers or particles:** the summer sound has a sprinkler hiss and birds, but nothing moves on screen
  except the dog.
- **The heart monitor:** it beeps in room 14 (3D sound), but nothing on screen shows the vitals.
- **Not in the slice:**
  - escapes;
  - the static wasp (`fx_static_wasp` is imported but not used);
  - the Long Night's despair value (a field exists, nothing feeds it);
  - discovery risk;
  - saving mid-slice (a new run starts from Day 1).
- **Strict Heard out:** the outcome rules follow the spec. Mixed play falls through to Pressed when at least half
  the statements ended pressed, and to Heard out otherwise. The strict Heard out (never pressed, no wrong object)
  is tracked (`HeardOutClean`) but gives nothing extra yet.
- **Placeholder writing:** all lines outside the spec's quotes are placeholder in the spec's direction:
  - in `Dialogue/VisitingHours.ink`;
  - the journal text, in the config;
  - the statements' replies.
  Names I made up: Mrs. Lindqvist, Mr. Okafor, Larch Street, Biscuit the dog, Port Ellis, the date
  (Sunday 23 August 2026).

## Questions for you
1. **"...sleep in." timing:** after the voicemail (as built), or over it at "five in the morning" (two voices at
   once, more painful)?
2. **The rushing check:** fire every time she goes up to Walt with an open question (as built), or only once?
3. **Zone words:** keep the faint "press" and "listen" in the box, or trust the tint and the bullets alone?
4. **Cracking:** should a crack end the statement at once (as built)? The other option keeps the volley running,
   but softer.
5. **Wrong objects:** right now every statement offers distractors (the photo, the chart, the shop sign). Should
   statements 1 and 2 offer nothing, so the box is quiet until there's something to say?
6. **Pressing's reward:** pressing passes the night faster in feel only. Should pressing also shorten the
   statement, to make it tempting?
7. **Mixed Heard out:** should it still give the wrench, or only the clean version?
8. **Day 1's gate:** keep "chart him first" as the only gate, or let her leave without charting (and miss the
   teaching)?
9. **Mags on screen:** do you want a booth billboard of Mags (one more painting), or is the portrait enough?

## Where things are
- **Data** (edit in the Inspector): `Assets/JamKit/Templates/VisitingHours/Data/VisitingHoursConfig.asset`. It holds:
  - the journal's entries and pairs, and the gentle lines;
  - Walt's argument (its patterns, keys, offers, replies, timings, the press and soften scales);
  - the examinables;
  - the walking tuning;
  - each place's tint and haze;
  - the art by name.
- **The opening and the title:** rebuilt in 3D (style E) on 2026-10-10: `docs/OPENING-AND-TITLE.md`, "As built".
  Data in `Resources/JamKit/VisitingHoursBoot.asset` (the cues, the voicemail's markers, the voices, the headline,
  the pictures and models, the hold-to-skip time).
- **The lines:** `Dialogue/VisitingHours.ink` (playable in Inky).
- **The look:** `UI/VisitingHours.uss` (journal, box, cards, opening, title); `Shaders/VisitingHoursPainted.shader`.
- **The rules, without Unity:**
  - in `Packages/com.vibejam.jamkit/Runtime/Core/VisitingHours/`: `Journal`, `ArgumentBattle` with `BattlePilot`,
    `OpeningTimeline`, and `SliceContent` (the defaults);
  - tests: `dotnet test core-tests/VisitingHours/VisitingHours.Tests.csproj` (it also compiles and plays the ink).
- **JamKit > Visiting Hours > Validate Content** lists data problems. **Reset Content to Defaults** puts the
  config back.
