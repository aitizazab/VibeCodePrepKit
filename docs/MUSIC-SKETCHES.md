# Visiting Hours: music sketches

**What these are:** example drafts, made in code, of the pieces the game needs. They're starting points to react to
("darker", "slower", "not that instrument"), not the score. The real pieces are composed with the creator, step by
step.

- **Listen:** `out/vh-music-sketches/index.html`. There's a player per sketch; nothing autoplays and the volume starts
  at 50 %. Looping pieces play **twice** there, so you can hear the seam. The chase has its own card: a run (slow,
  fast, flying, slow), all its stems together, and each stem on its own, in two variants.
- **The files** (all in `out/vh-music-sketches/`, stereo, 44.1 kHz, about -16 LUFS):
  - `NN-piece.loop.wav` is the loop itself;
  - `NN-piece.twice.wav` is the preview;
  - `NN-piece.wav` is a piece that plays once;
  - `02-chase-<variant>.<stem>.loop.wav` are the chase's speed stems.

  `loops.txt` (round 2) and `loops-round3.txt` list every loop point and section with sample counts. Round 1's
  files are in `round1/`, and round 2's versions of what round 3 changed are in `round2/`.
- **Rebuild:** `uv run --with numpy python tools/audio/make-music-sketches.py` renders round 3's new and changed
  pieces; add `all` to render everything, or name pieces (`chase`, `opening`, `colours`, `lena`...). Every piece is one
  function in that file, with its notes written out.
- **Every sound is synthesised in code.** There are no samples and no borrowed melodies; the chords are common
  progressions (see `SPEED-MUSIC-BREAKDOWN.md`, section 5).

## Round 4: the creator's notes on round 3 (2026-10-12)

> "For the chase songs, please make the track that ramps up and down through the speed thresholds do so faster.
> Sitting through a minute and 20 seconds to listen to what should be 30 seconds is not my cup of tea. Other than the
> chase things, the opening is good, but I can't quite tell what changed. Did the main instrument swap? The new battle
> music sounds good, Elias' music now ends right, and the hope overlays sound well, though they do seem to mess with
> the volume a bit? Anyways, the Despair overlay is too obvious. It needs to be a subtler feeling, that can ramp up, not
> starting at this obvious drone over every single song."

| Note | What changed |
|---|---|
| **The chase run takes too long** | The run demo is now **30 s** for each variant. It goes running → fast at 4 s → flying at 11.5 s → fast at 19 s → running at 25 s. The stems and the game's rules (bar-line switches, 1 s in, 2 s out) are unchanged; only the demo's run is quicker. A third variant, **spacey** (analog synths, wide echoes), is new, so there are three: grounded, spacey, airy. |
| **"Did the main instrument swap?"** | No. Answer below. The music is unchanged. |
| **Hope changes the volume** | It did: each round-3 version was normalised as a whole, so when hope came in the song got louder, and the parts without hope came out quieter than the plain song. Now the song keeps exactly the level it has alone, and while hope plays the mix rides a smooth gain (a light duck), so its short-term loudness follows the plain song's. Measured before and after, below. |
| **Despair is too obvious** | Rebuilt as **three levels** (faint, growing, full) that the game crossfades between. Faint is barely felt: no drone. Only full comes near round 3's. There's a ramp demo, faint to full over 30 s. |
| **Battle, Elias, Lena, Nadia** | Approved; untouched. |
| **The Long Night** | Kept as it is. It already is despair at full (the flattened-second sigh, the pedal, the music box), so the new levels lead into it rather than replace it. If it should also share the new levels' slow pitch sag, that's a one-line change. |

### The opening: what changed from round 2 to round 3
**The main instrument didn't change.** It's the same felt piano playing the waking motif, over the same drone on D
and A, the same soft chords and the same glass notes. What changed is the **shape**, not the sound:
- **Round 2** was one 30 s piece that played once and ended: the waking motif three times, then a held chord.
- **Round 3** split it in two:
  - An **intro** (23.3 s, for the black and the news report): the piano a little quieter, sitting in the gaps of the
    news voice, the motif once.
  - A **title loop** (40 s) that repeats until Play. In it the motif comes twice, the second time an octave higher
    with **low strings** doubling it underneath (the one new sound), then four bars of just the chords and glass.
- So the first 20 s sound much the same. The difference is that it now carries on and loops instead of stopping.

### Hope without the volume change: before and after
Measured with EBU R128 short-term loudness (a 3 s window, every 0.1 s), comparing each hope version with the plain
song, loop-aware. "Typical" is the median difference; "95 %" means 95 % of the time the difference is smaller than this.

| Song | | Whole song | While hope plays: typical / 95 % | Where hope isn't: typical / 95 % |
|---|---|---|---|---|
| Dream theme | round 3 | 0.0 LU | +1.3 / 2.9 LU | **-2.2** / 3.8 LU |
| | **round 4** | 0.0 LU | **+0.1** / 1.0 LU | **0.0** / 0.2 LU |
| Daytime | round 3 | 0.0 LU | -0.5 / 2.7 LU | **-6.9** / 7.2 LU |
| | **round 4** | +0.1 LU | **+0.2** / 2.5 LU | **0.0** / 0.8 LU |
| Lena | round 3 | 0.0 LU | +0.1 / 1.7 LU | **-1.7** / 3.1 LU |
| | **round 4** | +0.1 LU | **+0.1** / 1.3 LU | **0.0** / 2.0 LU |

- **What was wrong:** round 3 normalised each hope version as a whole, so the parts without hope came out 1.7–6.9 LU
  quieter than the plain song. That was the "volume messing".
- **Now:** the song keeps its own level (0.0 LU where hope isn't). While hope plays, the typical level is within 0.2 LU
  of the plain song, and the whole song within 0.1 LU.
- **What's left:** brief swings of 1–2.5 LU (the 95 % column) right as hope's rising figures land. They're short
  (under a second or two), and the ride smooths over 1.5 s so it never pumps.
- **In the game:** the same idea is a fixed duck of the music by the hope stem's level, plus the stem itself mixed a
  set distance under the song.

### Despair as a feeling that grows
Despair now has **three levels**, each a stem in the piece's own key, tempo and metre. The game crossfades between them
and scales the song's own changes with the level. Each level's stem is set a fixed distance under its song (faint 17 LU under, growing 11,
full 6), so despair never makes a song louder: measured over the three songs, faint and growing stay within 0.1 LU
of the plain song, and full within 0.7 LU.

| Level | The song itself | What the stem adds |
|---|---|---|
| **Faint** (barely felt) | A slow pitch sag and recovery of 6 cents, about one every 16 s; slightly darker (85 % of its brightness) | A far-off low cello on the chord's root every four bars; filtered breath now and then |
| **Growing** | A 12-cent sag; 70 % of its brightness | The low tones every other bar; every four bars a falling half-step sigh onto the root; now and then a detuned music box on the flattened second |
| **Full** (near round 3's) | A 20-cent sag; 55 % of its brightness | Sighs every two bars, the music box every four, and a low pedal on the tonic (lighter than round 3's) |

- **The sag** is a slow wow, like a tape slowing and recovering. It's done with a moving delay, so the loop's length
  and seam don't change. In the game the same thing is a slow pitch wobble on the music's sources.
- **The darkening** is the speed music's muffled copy, already in the game.
- **Ramping:** the demo (`01-dream-theme.despair-ramp.wav`) goes from faint to full over 30 s. The game would do
  the same: crossfade the level stems by an intensity value and scale the sag and darkening with it.
- Each level is on the page over three songs (the Dream theme, Daytime, Lena), against the plain song.

## Round 3: the creator's notes on round 2 (2026-10-11)

> "The running theme's grounded variation's base stems are too unnoticeable. They should mimic the base and drone for
> the Opening sequence, since those work pretty well. And the peak stem is also kind of muted compared to the drive
> stem, just by volume. Fix that. The opening music sounds fine, but doesn't it play during the news report, and loops
> for the title screen? It doesn't just play once. Elias' Theme needs its outro reworked, because it just drones
> unpleasantly, not pleasantly, and then almost completely quiets for about 9 seconds before the tune starts up again.
> Otherwise, Lena's Theme is perfect, and Nadia's Theme is pretty good too. The argument battle sound is good too, but
> it feels like it drags on a bit, like the build-up section could happen in half the bars it does, and then the empty
> space is filled by another section entirely. [...] Now, a few last things I want to add; the hope motif, and the
> Despair motif, that can be overlaid on any of these songs and fit, making each of them lighter or drearier in turn.
> [...] the Despair motif will be especially important for the Long Night Ending, and it's going to need its own track
> later as well. If you happen to have an idea of unsettling eerieness and despair, you can try it out for me to hear."

| Note | What changed |
|---|---|
| **The chase's grounded base** | Rebuilt on the Opening's bed: a low drone on each chord's root and the fifth below it, under the Opening's soft held chords (and the sub). It sits low, so it stays heard even muffled at a standstill. |
| **Peak quieter than drive** | Every stem is now balanced on its own before mixing: base -21, drive -20, peak -19 LUFS, so each tier steps **up** in presence. One gain is then set for the three together, so they still add up to exactly the mix. Both variants (grounded and airy) are balanced this way; there are two variants, not three. |
| **The opening loops for the title** | It's now an **intro** for the black and the news report, then the **title loop**. Details in "4. The opening and the title screen" below. |
| **Elias's outro** | The low held cello E is gone. A soft broken-chord pulse on felt piano now runs right round the loop, stronger in the intro and outro, so it never goes quiet. The outro plays his figure's opening softly on piano, and a short cello answer leads back to the intro. |
| **The argument drags** | The build is halved (8 bars to 4). In the freed space there's a new section, **C, the duel**: a march (kick on 1 and 3, rolling snares) where the strings and brass trade the conflict line phrase by phrase over new chords (F C Dm A). A and A' also tightened from 16 to 12 bars. It's now a 97 s loop (was 111 s). |
| **Lena, Nadia** | Untouched. |
| **Hope and despair** | Two overlay motifs, designed to sit over any piece (the integration is below), with demos over three pieces. |
| **The Long Night** | A first despair track (below). |

### Hope and despair: how they can sit over any piece

**The problem.** The pieces are in different keys (D minor, F major, Bb major, D dorian), metres (4/4 and Lena's
3/4) and tempos (66 to 138 bpm). A fixed melody laid over all of them would clash somewhere.

**The options weighed:**

| Option | How | Good | Bad |
|---|---|---|---|
| 1. One fixed recording, transposed by the game | The game pitch-shifts one hope clip to each piece's key | One asset | Pitch-shifting changes the tempo and the timbre; it can't follow a piece's chords, so it clashes on chord changes; nothing fits 3/4 |
| 2. **A motif defined by its shape, rendered per piece as an extra stem** (chosen) | The motif is a rhythm and a contour (where it goes) whose notes are taken from **each bar's own chord**. Each piece gets its own hope and despair stem, in its key, tempo and metre, the same length as its loop | Always fits, because every note is a chord tone or resolves to one. It loops with the piece, and it uses the stem system we already have | One more stem per piece (small) |
| 3. Changing the mix only | Filters, detune, ducking, no new line | Free | Lighter or darker, but no motif to recognise |

**So it's option 2, plus a little of option 3 for despair:**
- **Hope:** every two bars, a rising figure high up, built from that bar's chord: its fifth, the root above, the ninth,
  then the next chord's third, held.
  - A soft halo (root, fifth, ninth) sits over each bar on high strings.
  - The figure is on celesta doubled by strings: bright and rising, an open "add9" sound that lifts any chord without
    changing it.
- **Despair:**
  - A **pedal** on the piece's own tonic, low and dark, slowly beating (two tones a few cents apart).
  - Every two bars, a **falling sigh** in the cellos: a half step above the chord's fifth sinking onto it, then the
    **flattened second** sinking onto the root (the oldest sound of grief there is).
  - Every four bars, a music box **out of tune** plays the flattened second high.
  - It also changes the piece itself: while despair is there the piece is **half muffled**, so its brightness goes.
    The game can also lower the peak stem.
- **Recognisable everywhere:** the two motifs keep their shapes in every piece, a rising lift and a falling sigh. Only
  the notes change with the chords, the way a leitmotif sounds in a new key.
- **How the game would bring them in** (the stem system already does this: `MovementMusic`'s tension slot works the
  same way):
  - A fifth slot, the **colour stem**, per piece: `<piece>.hope` and `<piece>.despair`, the same length as the loop.
  - It comes in **on the next bar line** and fades in over 2 s, out over 3 s.
  - For despair, the muffle and a slight drop of the peak stem come in with it.
- **When** (for the creator to decide; suggestions):
  - Hope: Nadia wins a truth in an argument, a kind memory, the ending's good routes.
  - Despair: the Despair route, Nadia's anger, a lost argument, the Long Night.
  - Both can rise and fall with a value (for example the argument's balance) instead of switching on and off.
- **The demos** lay hope, then despair, over three pieces (from bar 5 to four bars before the end, so the intro and
  outro stay plain): the Dream theme (D minor, 4/4, 104 bpm), Daytime (F major, 92 bpm) and Lena (Bb major, **3/4**,
  84 bpm: the figure fits the waltz because it takes its rhythm from the bar). Listen to each against the plain piece
  on the page. Lena's own loop is unchanged; only the demo has the overlay.

### The Long Night: a first despair track (`10-long-night.loop.wav`, 71 s loop, 54 bpm)
**What I went for:** something that feels wrong slowly rather than scary all at once. Grief that has stopped
expecting anything.
- **The ground:** a pedal of low sines on D, a few cents apart, so they beat slowly like a held breath. Dark chords
  where the second is flattened: Dm, then Eb over the D pedal (the flattened second, the despair motif's own note),
  then Dm, then Bb minor.
- **The despair motif** in the cellos every two bars: Eb sinking to D, Bb to A; the second time round, Eb to D, D to C#
  and back to D (turning in place).
- **The memory going wrong:**
  - The **family motif played backwards** (F E D A) on a music box. Each time it drifts further out of tune (15, 35, 55,
    75 cents flat): the family's tune, unwinding.
  - **Reversed swells** (a D, Eb, A cluster played backwards) rise and stop dead on the bar lines.
  - A slow **heartbeat** under it all skips three times.
  - A thin high **G#** (the tritone against D) trembles twice and never resolves.
  - Dark, breath-like air drifts across.
- **To make it worse:** take the heartbeat away near the end of the loop; detune the pedal further; let the music box
  stop mid-phrase. **To make it bearable:** drop the G#, and let the chords end on D major once (a grey sliver of hope).

## Round 2: the creator's notes (2026-10-11) and what changed

> "They're not half-bad, but weren't built with the intention of looping like they must in a video game setting. For
> most songs, that looks like an opening portion, that rises into the song, the main song, and then an ending portion,
> that dies into a beat where the opening portion can pick it back up without fully dying, like a new track is playing.
> The songs are also all 45 seconds, which doesn't fit, some (like the Opening) will likely be a bit shorter, while
> others (like battle themes) could extend all the way up to 2 minutes, so looping them doesn't feel repetitive. And all
> the songs shouldn't all share one motif, or at least share more than just that motif. Firstly, Daytime shouldn't share
> the Dream motif with everyone else. Then, Elias, the Dream, Lena, and Nadia need to share a motif, and the Opening and
> Ending need to share a motif. The chase music should be built in mind with the speed thresholds of going slow/fast,
> not just as a track, and we should be ready to make more grounded, space-y, airy, or other different variants
> depending on what the scene is. Otherwise, most of the tracks you made are good bases to work off of, but they should
> extend naturally."

| Note | What changed |
|---|---|
| **Built to loop** | Every looping piece has an **intro** that rises out of a beat, the **body** (A, B, sometimes a breakdown and A'), and an **outro** that falls back to that same beat. The intro picks it up, so the loop never stops dead or restarts. The files loop **sample-accurately**: whatever rings past the end (note tails, reverb) is wrapped onto the start, exactly as it sounds when the loop repeats. The limiter runs on the loop played three times, keeping the middle, so it can't make a seam. Every seam was measured: the step across it is no bigger than an ordinary step inside the music. |
| **Lengths by role** | The Opening is 30 s and plays once (under the phone call). The character pieces and Daytime are 69–83 s loops. The argument is a 111 s loop and the chase a 90 s loop, each with real sections (A, B, a breakdown, A'). The Ending is 60 s and plays once. |
| **Motif families** | There are now three kinds of material instead of one shared motif (table below). Daytime has its own melody. |
| **Chase as speed stems** | The chase is three stems (base, drive, peak) on one 90 s loop, keyed to the movement's own speed tiers. The two variants are **grounded** and **airy**. |
| **Extend, don't replace** | The Dream theme, Daytime, Elias, Lena, the argument and the ending keep their round-1 bases (keys, tempos, instruments, chords) and grow from there. |

## The motif families

| Motif | Notes | Shared by | Why |
|---|---|---|---|
| **The family motif** | A D E F (a rising fourth, then two steps up); one bar | The Dream, Elias, Lena, Nadia | The Lasnames. The Dream's melody ("the street's song") is the family motif carried on; Elias's piece answers his own figure with it; Lena plays it in her key (F Bb C D); Nadia's B section has it on piano |
| **The waking motif** | A G F E (a slow fall that doesn't land), then D E F E (still not home) | The Opening and the Ending only | Waking up. The Opening leaves it hanging twice; the Ending finishes it, **D E F# A**, in D major |
| **Daylight** | F E C A C, G A C E... (bouncy, falling-then-rising) | Daytime only | The waking world has its own tune. It's kin only in harmony: the dream's four chords, started from F |
| Each character's own | Nadia: D F G A C (a determined climb; the B natural is her lift). Lena: F up to D (a rising sixth), then walking down. Elias: D A C Bb A G F G A, then a long low E (unresolved) | Their own piece | Each keeps their own voice; the family motif is what ties them |
| **The conflict line** | D C# D F E D C Bb A... (harmonic minor: the C# bites) | The argument | See below |

**Which motif the chase and the argument use, and why:**
- **The chase uses the Dream's material:** the street's song (family motif included) at double speed, and the family
  motif slowed down in its breakdown. The chase is the dream escape inside Elias's dream, so it's the Dream theme
  running.
- **The argument uses its own conflict line,** plus the **family motif turned upside down** (A E D C#) in its
  breakdown.
  - Its own line keeps it generic, so it works for any argument.
  - The upside-down family motif is there because the arguments in this game happen inside one family: the motif heard
    the wrong way up is the family at odds.
  - If one argument needs to be about one person, that person's motif can replace the conflict line in the B section.

## The pieces

Bars are counted from 1. Times are from the start of the loop file.

### 1. The Dream theme: `01-dream-theme.loop.wav`, 73.8 s loop (104 bpm, 32 bars)
- **Sections:**

  | Section | Bars | Time | What happens |
  |---|---|---|---|
  | Intro | 1–4 | 0–9.2 s | A soft kick, hats and sub; the music opens from muffled; felt piano plays the street's song |
  | A | 5–12 | 9.2–27.7 s | The groove, with the lead on the song and its answer |
  | B, the lift | 13–20 | 27.7–46.2 s | The movement's arpeggio; the music box carries the melody |
  | Breakdown | 21–24 | 46.2–55.4 s | No kick; pad and felt piano; a riser |
  | A' | 25–28 | 55.4–64.6 s | The groove back |
  | Outro | 29–32 | 64.6–73.8 s | The lead goes, the pad thins, the family motif once on piano; back to the soft beat as the music closes |
- **Key and sound:** D minor, the dream's chords, melodic house of the "Faded" era (pumping pads, a plucked supersaw lead).
- **Darker:** felt piano instead of the supersaw; no clap. **Brighter:** the shimmer from the movement peak stem; lead
  up an octave. **Faster:** 112–120 bpm. As speed stems: pad and sub = base, drums and bass = drive, lead and music
  box = peak (the chase shows the layout).

### 2. The chase, as speed stems: `02-chase-<variant>.base|drive|peak.loop.wav`, 90 s loop (128 bpm, 48 bars)
- **The stems match the movement's speed tiers** (`SpeedTier`: running, **fast** from 8 m/s, **flying** from 13 m/s;
  the same rules as `MovementMusic`/`StemMixer`):

  | Stem | Heard when | What's in it |
  |---|---|---|
  | **base** | always (muffled when slow, opening with speed) | sub and the chords; grounded: the Opening's drone (root and the fifth below) under its soft held chords; airy: choir and pad with a soft pulse |
  | **drive** | fast and up | drums, the galloping off-beat bass, toms into each section, the street's song at double speed |
  | **peak** | flying | the sixteenth arpeggio; the music box or bells on the full melody in B; air and the family motif in the breakdown |

  All three stems are the same length and loop together, and they add up to exactly the mix: one gain for all of
  them, no per-stem limiter. Dropped into the game's stem folder they'd work as they are
  (`MOVEMENT.md`, "Speed music"); the game's `MovementMusic` would need `bpm = 128`.
- **Sections:** A, bars 1–16 (Dm Bb C A); B, bars 17–32 (new chords Gm Bb F A, the melody's answer); breakdown, bars
  33–40 (half time, Dm and Bb); A', bars 41–48 (the melody in octaves). A tom fill leads into every section and back
  round to A.
- **The run preview** (`.run.wav`, 30 s since round 4) mixes the stems by the game's rules:
  - It goes running → fast at 4 s → flying at 11.5 s → fast at 19 s → running at 25 s.
  - Stems switch only on bar lines, fade in over 1 s and out over 2 s, and the base opens from muffled with speed.
- **The variants** use the same stems, the same notes and the same structure; only the instruments and the space differ:
  - **grounded:** dry and close (a 1 s room). The Opening's drone and soft chords as the base, a plucked bass, a kit with a real snare, a piano-and-pluck lead, plucked arpeggio, music box. For chases on the ground (the street, the hospital).
  - **airy:** wide and spacious (a 4.5 s room, echoes). A choir and soft pad, a soft kick and clap, a celesta lead with echoes, bell arpeggios. For chases in the air, at night, or anywhere dreamlike.
  - **spacey** (round 4): analog-synth space (a 3 s room). Bright detuned synth pads, a synth pulse, a supersaw lead and arpeggio with wide echoes, bells. For chases that should feel like flying through the dark.
  - **A new variant** (underwater, music-box-only and so on) is one more entry in `CHASE_VARIANTS` in the script.
- **Level (round 3):** each stem is balanced on its own first (base -21, drive -20, peak -19 LUFS), so every tier
  steps up; then one gain for all three. Before balancing, the peak was 11 dB quieter than the drive in the grounded set (and the base 1 dB above it); now the grounded stems alone are base -24.4, drive -23.4, peak -22.4 LUFS, and all together -19.0 LUFS (its punchy kick and snare reach the peak ceiling first, so it's quieter as a whole); the airy stems are -21.4, -20.4, -19.4, and -16.0 together. The game sets the music level anyway.

### 3. The Daytime theme: `03-daytime.loop.wav`, 83.5 s loop (92 bpm, 32 bars)
- **Sections:** intro, bars 1–4 (the groove: piano comping, shakers, a soft kick); A, bars 5–12 (the daylight melody on
  marimba); B, bars 13–20 (new chords Bb C7 Am7 Dm7, the melody on celesta, strings); A', bars 21–28 (marimba and
  celesta an octave apart); outro, bars 29–32 (back to the groove).
- **Its own melody,** no dream motif. The kinship is only in the chords: Fmaj7–Cadd9–Dm–Bbmaj7, the dream's four chords
  started from F, so the day feels related to the dream without quoting it.
- **Darker:** start the loop on Dm. **Brighter:** a glockenspiel an octave up. **Faster:** 100 bpm with the shaker in
  sixteenths.

### 4. The opening and the title screen: `04-opening.intro.wav` then `04-opening.loop.wav` (72 bpm)
- **Where it plays:** the music cue comes 3 s after the voicemail ends (`SliceContent`: `voicemail:end` + 3 s, with a
  5 s fade-in). The news report starts 3 s later and runs 17.6 s. Then the title screen waits on the tablet's Play.
- **So it's in two parts:**

  | Part | File | Length | Plays |
  |---|---|---|---|
  | **Intro** | `04-opening.intro.wav` | 7 bars, 23.3 s (1,029,000 samples), then a 6 s ring-out | Once: the black, then under the news report (0–21.45 s) |
  | **Title loop** | `04-opening.loop.wav` | 12 bars, 40 s (1,764,000 samples) | From the intro's end, round and round until Play |

  `04-opening.wav` is the two together, as the game would play them: the intro, then the title loop twice.
- **How the game should enter the loop:** play the intro, then start the title loop exactly **23.333 s** (1,029,000
  samples at 44.1 kHz) after the intro started, on its bar line. Mix the intro's ring-out over the loop's first
  seconds: let the intro clip play on while the loop starts. Then loop the title file whole (sample 0 to its end).
  - In Unity: two sources with `PlayScheduled`, the second at the first's start + 23.333 s.
  - Or one clip with loop points, if the music system gets them.
- **The intro:** the drone on D and A, the soft chords (Dm Bb Dm Bb F C C), the waking motif once (A G F E, then
  D E F E, unresolved) on felt piano, a little quieter and in the gaps of the news voice, and one glass note. It
  opens from muffled over the first two bars.
- **The title loop:** the drone held right round the loop, the dream's chords three times. The waking motif twice
  (the second time an octave up, with low strings under it), then four bars of just the chords and two glass notes,
  back to where it began.
- **Darker:** no glass. **Brighter:** let the last phrase land on D. **If the news timing changes:** the intro's length
  is one number (7 bars); a bar is 3.33 s.

### 5. An argument battle: `05-argument-battle.loop.wav`, 97.4 s loop (138 bpm, 56 bars; round 3)
- **Sections:**

  | Section | Bars | Time | What happens |
  |---|---|---|---|
  | Intro | 1–4 | 0–7.0 s | The staccato ostinato, toms joining |
  | A | 5–16 | 7.0–27.8 s | Full: toms, snare, sub, brass stabs, the conflict line on strings |
  | B | 17–28 | 27.8–48.7 s | New chords Bb C Gm A, a new ostinato rhythm, hats, the line on brass then strings |
  | **C, the duel** (new) | 29–36 | 48.7–62.6 s | A march (kick on 1 and 3, rolling snares, pizzicato chords) over F C Dm A. The strings call and the brass answers an octave down, then both together |
  | The build (halved) | 37–40 | 62.6–69.6 s | Half time, low strings, the family motif upside down on solo cello, a riser |
  | A' | 41–52 | 69.6–90.4 s | Full again, brass under the strings, the inverted family motif on brass |
  | Outro | 53–56 | 90.4–97.4 s | Back to the ostinato and toms |
- **Darker:** no brass, a low drone on D. **Brighter:** major-key stabs. **Faster:** 150 bpm. **Shorter still:** A and
  A' can drop to 8 bars each (83 s).

### 6. Lena's motif: `06-lena.loop.wav`, 68.6 s loop (84 bpm, 3/4, 32 bars)
- **Sections:** intro, bars 1–4 (the waltz plucks and pad); A, bars 5–12 (her melody on celesta); B, bars 13–20 (new
  chords Eb F7 Dm Gm, the **family motif in Bb** on strings, her melody's second half answering); A', bars 21–28
  (celesta with low strings); outro, bars 29–32 (back to the plucks).
- **Darker:** G minor (the same notes from Gm). **Brighter:** a music box doubling. **Faster:** 96 bpm.

### 7. Elias's motif: `07-elias.loop.wav`, 72.7 s loop (66 bpm, 20 bars)
- **Sections:** intro, bars 1–2 (the broken-chord pulse, piano bass and strings); A, bars 3–10 (his falling figure on
  cello, twice, the second time with piano); B, bars 11–16 (the **family motif**, slow and high on strings, the dream's
  melody faint on a music box, his figure answering low); outro, bars 17–20 (round 3: his figure's opening, softly on
  piano, and a short cello answer, A G F E, leading back round; the pulse carries it).
- **The pulse (round 3):** a felt piano playing each chord in broken eighths, right round the loop (louder in the intro
  and outro), so the loop never goes quiet before the tune comes back.
- **Darker:** end on C# instead of E. **Brighter:** let his figure rise to F at the end. **Faster:** 76 bpm at most.

### 8. The ending reprise: `08-ending.wav`, 60 s and a 6 s ring-out, plays once (80 bpm)
- **Sections:**

  | Bars | What happens |
  |---|---|
  | 1–4 | The waking motif exactly as in the opening, so it's recognised |
  | 5–8 | Strings take it |
  | 9–12 | The family motif, once, on music box and piano: a farewell |
  | 13–16 | The build |
  | 17–20 | The cadence Bb, C, **D major**. The waking motif finally finishes, **D E F# A**, and the music box plays it in D major |
- **Sadder:** end on D minor, or on the unresolved C. **Brighter:** a rising fourth in the choir at the end.

### 9. Nadia's motif (new): `09-nadia.loop.wav`, 80 s loop (96 bpm, 32 bars)
- **Key:** D dorian (D minor with a B natural: a lift that won't be sad).
- **Sections:** intro, bars 1–4 (a plucked eighth-note ostinato, brushed drums, shaker); A, bars 5–12 (**her motif**, D F
  G A C, a determined stepwise climb, on a soft reed); B, bars 13–20 (new chords Bb C Dm G, the **family motif** on
  piano, her motif's end answering); A', bars 21–28 (the reed, strings under it); outro, bars 29–32 (back to the
  ostinato).
- **Instruments:** a clarinet-like reed for her, the guitar-like ostinato for the walking pace.
- **Darker:** plain D minor (B flat). **Brighter:** F major, or the reed up an octave. **Faster:** 104 bpm (the dream's
  tempo, if her piece ever joins the movement).

## For the creator to decide
**Round 4:**
- **Despair:** is faint subtle enough to sit under a normal scene? Is the ramp's pace right, and what drives it (the
  Despair route's progress, anger, the Long Night)?
- **The Long Night:** keep it as the full-despair track, or rebuild it on the new levels' sag and breath?
- **Hope:** with the volume fixed, is it the right size? It could be quieter still, or come with a duck of the drive
  stem.
- **The spacey chase:** keep it as a third variant? Which scenes get which variant?

**Round 3:**
- **Hope and despair:** do the overlays fit and change the mood enough? Should despair also slow or detune the piece,
  or is the half-muffle enough?
- **When they come in:** which events bring hope, and which bring despair? Switched on and off, or rising and falling
  with a value (an argument's balance)?
- **The Long Night:** is this the right kind of despair (slow and wrong) or should it be harsher? It's the seed of
  its own track.
- **The title loop:** 40 s before it repeats on the title screen; long enough?
- **The duel section:** keep it as the argument's new middle, or make it a separate "turning point" cue?

**From round 2:**
- **The family motif itself** (A D E F): keep it, or change notes one at a time? It now carries four pieces.
- **The waking motif** (A G F E, then D E F#... A at the end): does it feel like waking?
- **Nadia's colour:** dorian (hopeful-stubborn), or plain minor?
- **The chase variants:** which scenes are grounded, which are airy, and what other variants we need (spacey, the
  hospital, the road).
- **The argument's motif:** is a generic conflict line with the family motif inverted right, or should each guardian
  get their own?
- **Lengths:** do the loops feel long enough before they repeat?
