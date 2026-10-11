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

  `loops.txt` lists every loop point and section with sample counts. Round 1's files are in `round1/`.
- **Rebuild or change one:** `uv run --with numpy python tools/audio/make-music-sketches.py [piece ...]` (for example
  `... make-music-sketches.py chase lena`). Every piece is one function in that file, with its notes written out.
- **Every sound is synthesised in code.** There are no samples and no borrowed melodies; the chords are common
  progressions (see `SPEED-MUSIC-BREAKDOWN.md`, section 5).

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
  | **base** | always (muffled when slow, opening with speed) | sub, the chords, a soft pulse |
  | **drive** | fast and up | drums, the galloping off-beat bass, toms into each section, the street's song at double speed |
  | **peak** | flying | the sixteenth arpeggio; the music box or bells on the full melody in B; air and the family motif in the breakdown |

  All three stems are the same length and loop together, and they add up to exactly the mix: one gain for all of
  them, no per-stem limiter. Dropped into the game's stem folder they'd work as they are
  (`MOVEMENT.md`, "Speed music"); the game's `MovementMusic` would need `bpm = 128`.
- **Sections:** A, bars 1–16 (Dm Bb C A); B, bars 17–32 (new chords Gm Bb F A, the melody's answer); breakdown, bars
  33–40 (half time, Dm and Bb); A', bars 41–48 (the melody in octaves). A tom fill leads into every section and back
  round to A.
- **The run preview** (`.run.wav`, 96 s) mixes the stems by the game's rules:
  - It goes running → fast at 21 s → flying at 41 s → fast at 66 s → running at 80 s.
  - Stems switch only on bar lines, fade in over 1 s and out over 2 s, and the base opens from muffled with speed.
- **The variants** use the same stems, the same notes and the same structure; only the instruments and the space differ:
  - **grounded:** dry and close (a 1 s room). Felt piano chords, a plucked bass, a kit with a real snare, a piano-and-pluck lead, plucked arpeggio, music box. For chases on the ground (the street, the hospital).
  - **airy:** wide and spacious (a 4.5 s room, echoes). A choir and soft pad, a soft kick and clap, a celesta lead with echoes, bell arpeggios. For chases in the air, at night, or anywhere dreamlike.
  - **A new variant** (spacey, underwater, music-box-only and so on) is one more entry in `CHASE_VARIANTS` in the script.
- **Level:** the airy set reaches -16 LUFS. The grounded set peaks first (its kick and snare are punchy) and sits at
  -18 LUFS. The game sets the music level anyway.

### 3. The Daytime theme: `03-daytime.loop.wav`, 83.5 s loop (92 bpm, 32 bars)
- **Sections:** intro, bars 1–4 (the groove: piano comping, shakers, a soft kick); A, bars 5–12 (the daylight melody on
  marimba); B, bars 13–20 (new chords Bb C7 Am7 Dm7, the melody on celesta, strings); A', bars 21–28 (marimba and
  celesta an octave apart); outro, bars 29–32 (back to the groove).
- **Its own melody,** no dream motif. The kinship is only in the chords: Fmaj7–Cadd9–Dm–Bbmaj7, the dream's four chords
  started from F, so the day feels related to the dream without quoting it.
- **Darker:** start the loop on Dm. **Brighter:** a glockenspiel an octave up. **Faster:** 100 bpm with the shaker in
  sixteenths.

### 4. The opening: `04-opening.wav`, 30 s and a 4 s ring-out, plays once (72 bpm)
- A drone on D and A, soft chords (Dm Bb Dm Bb F C, then held), felt piano and two high glass notes. It starts
  muffled and opens.
- **The waking motif** (A G F E), then D E F E: both stop on E and don't go home, and the motif comes back once more.
  It's built to sit under the ring and the voicemail.
- **Darker:** no glass. **Brighter:** let the last phrase land on D. **Longer or shorter:** add or remove a bar of the
  held chord at the end.

### 5. An argument battle: `05-argument-battle.loop.wav`, 111 s loop (138 bpm, 64 bars)
- **Sections:**

  | Section | Bars | Time | What happens |
  |---|---|---|---|
  | Intro | 1–4 | 0–7 s | The staccato ostinato, toms joining |
  | A | 5–20 | 7–34.8 s | Full: toms, snare, sub, brass stabs, the conflict line on strings |
  | B | 21–36 | 34.8–62.6 s | New chords Bb C Gm A, a new ostinato rhythm, hats, the conflict line on brass and strings |
  | Breakdown | 37–44 | 62.6–76.5 s | Half time, low strings, the family motif upside down on solo cello, a riser |
  | A' | 45–60 | 76.5–104.3 s | Full again, with brass under the strings and the inverted family motif on brass |
  | Outro | 61–64 | 104.3–111.3 s | Back to the ostinato and toms |
- **Darker:** no brass, a low drone on D. **Brighter:** major-key stabs. **Faster:** 150 bpm.

### 6. Lena's motif: `06-lena.loop.wav`, 68.6 s loop (84 bpm, 3/4, 32 bars)
- **Sections:** intro, bars 1–4 (the waltz plucks and pad); A, bars 5–12 (her melody on celesta); B, bars 13–20 (new
  chords Eb F7 Dm Gm, the **family motif in Bb** on strings, her melody's second half answering); A', bars 21–28
  (celesta with low strings); outro, bars 29–32 (back to the plucks).
- **Darker:** G minor (the same notes from Gm). **Brighter:** a music box doubling. **Faster:** 96 bpm.

### 7. Elias's motif: `07-elias.loop.wav`, 72.7 s loop (66 bpm, 20 bars)
- **Sections:** intro, bars 1–2 (piano bass and strings); A, bars 3–10 (his falling figure on cello, twice, the second
  time with piano); B, bars 11–16 (the **family motif**, slow and high on strings, the dream's melody faint on a music
  box, his figure answering low); outro, bars 17–20 (a low E held, back to the piano bass).
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
- **The family motif itself** (A D E F): keep it, or change notes one at a time? It now carries four pieces.
- **The waking motif** (A G F E, then D E F#... A at the end): does it feel like waking?
- **Nadia's colour:** dorian (hopeful-stubborn), or plain minor?
- **The chase variants:** which scenes are grounded, which are airy, and what other variants we need (spacey, the
  hospital, the road).
- **The argument's motif:** is a generic conflict line with the family motif inverted right, or should each guardian
  get their own?
- **Lengths:** do the loops feel long enough before they repeat?
