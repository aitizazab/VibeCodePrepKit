# Visiting Hours: music sketches

**What these are:** example drafts, made in code, of every piece the game needs. They're starting points to react to
("darker", "slower", "not that instrument"), not the score. The real pieces are composed with the creator, step by
step.

- **Listen:** `out/vh-music-sketches/index.html` (a player per sketch; nothing autoplays; the volume starts at 50 %).
  The files are `out/vh-music-sketches/01-dream-theme.wav` to `08-ending.wav`: stereo, 44.1 kHz, about -16 LUFS.
- **Rebuild or change one:** `uv run --with numpy python tools/audio/make-music-sketches.py [piece ...]`, for example
  `... make-music-sketches.py chase`. Every piece is one short function in that file, with its notes written out.
- **Every sound is synthesised in code.** There are no samples and no borrowed melodies; the chords are common
  progressions (see `SPEED-MUSIC-BREAKDOWN.md`, section 5).

## The shared material

**The chords of the dream:** Dm(add9), Bbmaj7, F(add9), C(add9). That's i–VI–III–VII in D minor, the movement base the
creator likes (`SPEED-MUSIC-BREAKDOWN.md`). The same four chords, started from F, are F–C–Dm–Bb: I–V–vi–IV in F major.
That turn is how the Dream and the Day share their harmony: the same chords, seen from the other side.

**The leitmotif** ("the street's song"): four bars over those chords. In note names, with beats in brackets:

| Bar | Chord | Notes |
|---|---|---|
| 1 | Dm | A (1½), D (½), E, F |
| 2 | Bb | E (1½), D (½), D (2) |
| 3 | F | C, F, E, C |
| 4 | C | D (3), rest |

It climbs a fourth (A up to D), reaches for F, leans on E over Bb (a dreamy, slightly bright clash), and comes to rest on
D over C, which is the add9 colour, so it never quite closes. Its **answer** (the next four bars) starts the same, climbs
higher (G, F, E) and walks home to D.

**Where the motif appears:**

| Piece | How |
|---|---|
| Dream theme | Whole, on felt piano, then a plucked lead, then a music box an octave up |
| Chase | Its first two bars twice as fast; the music box plays it out of tune |
| Daytime | Whole and unchanged, on marimba and celesta, over the F-major turn of the chords |
| Opening | Only its first half, very slow, stopping on the E (unresolved) |
| Argument | Upside down (where it rises, this falls), on strings and brass |
| Lena | Not quoted. Her motif shares its landing note: hers rises a sixth to the same D |
| Elias | His falling figure first; then the dream's motif answers, faint, on a music box (the dream is his) |
| Ending | Whole on piano, the answer on strings, then the minor key opens to D major |

## The sketches

### 1. The Dream theme (`01-dream-theme.wav`, 45 s)
- **Key and tempo:** D minor, 104 bpm, 4/4 (the movement base's tempo, so it can become the speed stems' parent).
- **Shape:** 4 bars of intro, muffled and opening up (felt piano plays the motif over the soft pad); 8 bars of groove
  (four-on-the-floor kick and clap, off-beat plucked bass, sub, the pads pumping with the kick, the motif and its answer
  on a plucked supersaw lead doubled by piano); 4 bars of lift (the movement's sixteenth-note arpeggio, and the music
  box an octave up on the answer); a 2-bar tail.
- **Why this way:** the melodic-house sound of the "Faded" era is a soft, sad chord loop, a pumping pad and a plucked
  lead carrying a simple, singable line. The motif is written to be singable.
- **Darker:** drop the lead an octave and swap it for felt piano; take out the clap; open the muffle later.
  **Brighter:** add the shimmer from the movement peak stem; move the lead up an octave.
  **Faster:** 112-120 bpm keeps the same feel (the pump gets tighter). To use it as the speed stems, split it: pad and
  sub = base, drums and bass = drive, lead, arpeggio and music box = peak.

### 2. The chase (`02-chase.wav`, 48 s)
- **Key and tempo:** D minor, 128 bpm. The chords become Dm–Bb–C–A (i–VI–VII–V): the A major chord at the end of each
  bar pulls hard back to Dm, which keeps it restless.
- **Shape:** 4 bars of bass, hats and a riser; 16 bars of drive (kick, clap, a galloping off-beat bass, toms at the end of
  every bar, the motif's first two bars twice as fast on the lead, every two bars); the second half adds the arpeggio and
  the music box playing the full motif a quarter of a semitone flat (the static is close); an impact and the motif's first
  notes alone on a detuned music box.
- **Darker:** detune the music box further and let it drift; replace the lead with low brass. **Brighter:** use the
  dream's own III chord (F) instead of C. **Faster:** 140 bpm; keep the motif at double time.

### 3. The Daytime theme (`03-daytime.wav`, 45 s)
- **Key and tempo:** F major, 92 bpm (a walking pace), 4/4. Chords Fmaj7–Cadd9–Dm(add9)–Bbmaj7: the dream's four chords
  started from F.
- **Instruments:** piano comping in light eighths, a plucked guitar-like arpeggio, a soft kick and clap, shakers, a sub.
  The **leitmotif** comes in unchanged on marimba, the answer on celesta, then both together.
- **The link:** the same melody, note for note, as the Dream theme. Over F major it sounds hopeful; over D minor it
  sounds like longing. A player who hears both should feel the day and the dream are the same street.
- **Darker:** start the loop on Dm instead of F (then it's the dream again). **Brighter:** add a whistled or glockenspiel
  doubling an octave up. **Faster:** 100 bpm, with the shaker in sixteenths.

### 4. The opening (`04-opening.wav`, 45 s)
- **Key and tempo:** D minor, 72 bpm. Chords Dm–Bb–Dm–Bb–F–C, then held.
- **Instruments:** a D and A drone, soft held chords, felt piano with a long reverb, two high glass notes far apart.
- **The motif:** only its first half, one slow note at a time. The second time it stops on the E and doesn't go home:
  the night is interrupted. The whole thing starts muffled (as if through a wall) and opens over four bars.
- **Darker:** drop the glass; replace Bb with Gm. **Brighter:** let the second phrase finish on D. **Faster:** not
  advised: it lives under the phone call; 80 bpm at most.

### 5. An argument battle (`05-argument-battle.wav`, 45 s)
- **Key and tempo:** D minor, 138 bpm. Chords Dm–Bb–Gm–A (i–VI–iv–V): the A major chord's C# is the sharp edge.
- **Instruments:** a sixteenth-note staccato string ostinato (root, octave, fifth) in both ears, toms on every beat and a
  snare on 2 and 4, a low sub, brass stabs on the chord changes. From bar 9 the strings play the **motif upside down**
  (the dream's rising line made to fall), with brass under it in the second half.
- **Generic on purpose:** it fits any argument. A specific argument could swap the inverted motif for that character's
  motif.
- **Darker:** take out the brass, add a low drone on D. **Brighter:** major-key stabs (Bb and F) on bars 1 and 3.
  **Faster:** 150 bpm; the ostinato carries it.

### 6. Lena's motif (`06-lena.wav`, 38 s)
- **Key and tempo:** Bb major (the dream's warm VI chord), 84 bpm, **3/4**: a slow waltz, the only piece not in four.
- **Instruments:** celesta for the melody, nylon-like plucks on beats 2 and 3, a soft pad; low strings join on the
  second time through.
- **The motif:** F up to D (a rising sixth, landing on the dream's home note), then a gentle walk down: D, C, Bb, A,
  G... It resolves home to Bb at the end of the second phrase: she's the one who resolves.
- **Darker:** move it to G minor (the same notes, started from Gm). **Brighter:** a music box doubling, up an octave.
  **Faster:** 96 bpm makes it a lullaby that moves; slower than 76 starts to sound like grief.

### 7. Elias's motif (`07-elias.wav`, 49 s)
- **Key and tempo:** D minor, 66 bpm. Chords Dm–Bb–Gm–A7.
- **Instruments:** low strings (a cello-like line), piano bass notes, soft high strings; later a faint music box.
- **The motif:** D, A, C, Bb, A, G, F, G, A... then a long low **E**: it falls and stops on the note that doesn't
  resolve (the apology nobody heard). Then the dream's leitmotif answers faintly on a music box: the dream is his.
- **Darker:** end on the low C# instead of E (more tense). **Brighter:** let the last phrase rise to F. **Faster:** 76
  bpm still works; any faster loses the weight.

### 8. The ending reprise (`08-ending.wav`, 48 s)
- **Key and tempo:** D minor turning to **D major**, 80 bpm.
- **Instruments:** piano (the motif and a broken-chord accompaniment), strings and a soft choir pad, a sub; the music box
  in tune on the third phrase.
- **The shape:** the whole motif, its answer on strings, the motif again with the music box, then a last cadence Bb – C –
  **D major** (F becomes F#): the progression that has looped all game finally lands, and lands bright.
- **Darker (a sadder ending):** end on D minor, or on the unresolved C(add9). **Brighter:** add a rising fourth in the
  choir on the last chord. **Faster:** 88 bpm; keep the last chord long.

## What to tell me when you react
Whatever comes to mind is useful, but these help most: which instrument carries the motif best; whether the motif itself
works or should change (we can change notes one at a time); tempo; how dark; and, for each piece, which moment in the
game it plays under.
