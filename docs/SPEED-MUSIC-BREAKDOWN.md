# The speed music: a breakdown of the placeholder track

**What this covers:** the music made in code for the speed overhaul. It's written by
`Packages/com.vibejam.jamkit/Runtime/Core/Movement/MoveMusicSynth.cs` (the Movement Lab) and
`Assets/JamKit/Templates/VisitingHours/Dream/Scripts/DreamSynth.cs` (the dream escape), and mixed by
`MovementMusic`/`StemMixer`. It's a placeholder: the real music is composed with the creator. **The creator likes its feel
(2026-10-10) and wants its progression kept.**

The audition is `out/speed-audio-review/00-speed-run.wav`, with the cue times in `00-speed-run-cues.txt`.

## 1. The grid
| | |
|---|---|
| Key | D minor (or F major: see section 5) |
| Tempo | 104 bpm. One beat 0.577 s, one bar 2.31 s |
| Metre | 4/4 |
| Loop | 4 bars (16 beats, 9.23 s), one chord per bar, repeating without a seam |
| Chords | **Dm(add9) → Bbmaj7 → F(add9) → C(add9)**: i–VI–III–VII in D minor |

**The voicings** (as MIDI notes):

| Bar | Chord | Notes | Top note |
|---|---|---|---|
| 1 | Dm(add9) | D3 F3 A3 E4 | E4: the added 9th rings over the minor chord |
| 2 | Bbmaj7 | Bb2 D3 F3 A3 | A3 |
| 3 | F(add9) | F3 A3 C4 G4 | G4 |
| 4 | C(add9) | C3 D3 G3 E4 | E4 |

The code's comments call bar 1 "Dm9" and bar 4 "Csus2". Strictly, bar 1 has no 7th, so it's Dm(add9). Bar 4 has its 3rd
(E), so it's C(add9), not a sus chord. The top voice moves E–A–G–E, a small arch that comes home every four bars, which
is why the loop never feels like it stops.

## 2. The layers and how speed brings them in
There are three stems, always playing in sync. Speed decides which ones you hear.

| Stem | Heard when | Instruments |
|---|---|---|
| **Base** | Always | A soft pad and a sub bass |
| **Drive** | Fast (8 m/s and up) | A plucked bass pulse, a four-on-the-floor kick, hi-hats, a clap |
| **Peak** | Flying (13 m/s and up) | A bright sixteenth-note arpeggio and a high shimmer |

On top of the stems:
- **The opening (A2):** each stem has a muffled twin (low-passed at 700 Hz) playing in step. Speed crossfades from
  muffled at a standstill to full and bright at flying.
- **The supersonic whistle (A4):** a high shimmering tone that rides on flying.
- **Tunnel focus (A5):** at flying, world sounds duck to 55%; the music, the wind and your movement don't.
- **Breath (A6):** almost inaudible (0.05), panting more with time spent fast.

### Where it kicks in, in the audition
| Time | Event | What the music does |
|---|---|---|
| 0–6 s | Standing, walking | The base alone, muffled: the pad and sub as if heard through a wall |
| 6.14 s | You reach fast (8.1 m/s) | Nothing yet: the drive waits for the next bar line |
| **6.93 s** | Bar 4 begins | **The drive comes in**, fading in over 1 s, and the music starts opening up |
| 10.84 s | You reach flying (13.0 m/s) | The whistle starts rising at once (it isn't on the bar grid) |
| **11.54 s** | Bar 6 begins | **The peak comes in**: the arpeggio and shimmer; the music is now fully open and the world ducks |
| 20.18 s | The stumble: 0.6 s at 3 m/s drops the tier to running | — |
| **20.77 s** | Bar 10 | **The drive and peak leave together**, fading out over 2 s |
| 22.63 s | Flying again | — |
| **23.08 s** | Bar 11 | **The drive and peak return together** |
| 32.10 s | Running | — |
| **32.31 s** | Bar 15 | Both leave, and the music closes back to muffled |

### Why those points
1. **On the bar line, never mid-bar.** Every bar is a new chord, so a stem that enters on a bar line arrives *with* a
   chord change and sounds like the arranger meant it. Mid-bar entries sound like a mistake. The cost is a wait of at
   most one bar (2.3 s); in the audition the waits are 0.7–0.8 s.
2. **Quick in (1 s), slow out (2 s).** Gaining speed is a reward, so the music answers quickly, well inside a bar.
   Losing speed should be *felt, not punished* (RESEARCH-MOVEMENT 8.4, rule 3), so it ebbs instead of cutting out.
3. **A 0.6 s grace before a tier drops** (the shared `SpeedTier`), so a mantle or a quick bump doesn't strip the music.
   The audition's stumble lasted 1.5 s, longer than the grace, so the layers left. A music-only hold could make that
   more forgiving if the creator wants.
4. **The whistle isn't quantised.** It's a sound effect of speed, not part of the arrangement, so it rises the moment
   you cross into flying (with the visual boom), and the music confirms it on the next bar.
5. **Muffled-to-open is continuous with speed** rather than stepped, so even inside one tier, faster sounds brighter.
   It's the music's own speedometer.

## 3. The instruments and what each one is for
### The base stem (always): "you're in the world"
- **The pad.**
  - **The sound:** each chord note is played twice, 0.18% sharp and 0.18% flat, so the two copies beat slowly against
    each other (a gentle natural chorus, no effect added). Each is a sine with a little third harmonic: soft, slightly
    hollow, like a muted organ or a felt synth.
  - **The envelope:** a 0.5 s fade-in on each chord, and a 1 s release that spills into the next bar, so the chords
    blur into each other legato with no gaps.
  - **Its job:** the harmony and the mood, and the bed everything else sits on. Its quiet, slow-moving quality is what
    makes standing still sound reflective rather than empty.
- **The sub bass.** The chord's root two octaves down (D down to about 37 Hz) as a pure sine, held a whole bar.
  - **Its job:** the weight you feel more than hear. It anchors each chord change, so the progression lands even when
    muffled.

### The drive stem (fast): "you're moving"
- **The bass pulse.** The root one octave below the chord, on every eighth note: a saw-like tone (four harmonics) with a
  quick pluck envelope (5 ms attack, decaying) through a simple low-pass that rounds it off.
  - **Its job:** momentum. Eighth notes at 104 bpm are about 3.5 per second, close to a sprinting footfall rate, so the
    music runs *with* you.
- **The kick.** On every beat (four on the floor): a sine that sweeps from about 128 Hz down to 48 Hz in a few
  hundredths of a second, decaying fast.
  - **Its job:** the pulse you lock onto. Four on the floor reads as "go" in almost every genre.
- **The hi-hats.** High-passed noise on every eighth, the off-beats twice as loud.
  - **Its job:** forward lean. Accented off-beats give a skipping, galloping push between the kicks.
- **The clap.** Band-passed noise on beats 2 and 4.
  - **Its job:** the backbeat. It turns a pulse into a groove, which is a lot of why the track feels "vibey" rather than
    tense.

### The peak stem (flying): "you're flying"
- **The arpeggio.** The chord's four notes two octaves up, in sixteenth notes, in a fixed sixteen-step order (the same
  shape each bar, following the chord). A plucked, bell-like tone: the fundamental plus a 2nd and 3rd harmonic that die
  away faster than it, so each note starts bright and settles round. Accents on each beat (full, 80%, 65%).
  - **Its job:** sparkle and speed. Sixteenths double the rhythmic rate again, so flying is literally busier, and the
    high register lifts the whole mix.
- **The shimmer.** The chord three octaves up (about 2–3 kHz), each bar swelling in and out as a single arc, with a
  tremolo of eight pulses a bar.
  - **Its job:** air. It's barely there as notes, but it fills the top of the spectrum, the "wind in your ears" of the
    arrangement.

### The dream escape's own set (`DreamSynth`)
The same chords, tempo and loop length, so the two sets can mix. The pad is the same idea; the pulse has a softer kick
and a breath of noise on the off-beats, but no clap.

Instead of a hi-hat groove it has a **music box:** the chord tones two octaves up, a toy-bell tone (a slightly
inharmonic partial at 5.4 × the fundamental, which is what makes it sound like a music box), with a gap on certain
steps every other bar so it "breathes".
- **Its job:** childhood, and the dream's sweetness. When the static closes in, the music box gets louder and goes out
  of tune (detuned by `DreamChaseMusic`) and the pulse is pushed in: the lullaby going wrong.

## 4. The effects on each layer
| Layer | Effects |
|---|---|
| Pad | Detuned double (a natural chorus), slow attack and release; muffled twin |
| Sub | None (a pure sine); muffled twin (mostly unchanged: it's already below 700 Hz) |
| Bass pulse | Pluck envelope, a one-pole low-pass; muffled twin |
| Kick | Pitch sweep and fast decay; muffled twin |
| Hats | High-pass, very short decay, accents; muffled twin (almost silent when muffled, deliberately: the groove arrives as you open up) |
| Clap | Band-pass, short decay; muffled twin |
| Arpeggio | Decaying upper harmonics (a pluck or bell); muffled twin |
| Shimmer | A bar-long swell and an eighth-note tremolo; muffled twin |
| Whistle | Pitch rises with speed; panned away from a wallrun wall |
| Everything | Quantised entries and exits, a 1 s fade-in and a 2 s fade-out, the music volume (0.45), no reverb or delay (yet) |

**Notably missing:** reverb and delay. The track is dry. Real stems would usually carry a little room. In the dream, a
long soft reverb on the pad and music box would add to the "memory" feel.

## 5. "Did we steal a progression?"
**Short answer:** the familiarity is real, but nothing was copied, and nothing here needs a credit or is blocked.

- **Why it sounds familiar:** i–VI–III–VII (Dm–Bb–F–C) is one of the most-used progressions in modern music. It's the
  same four chords as vi–IV–I–V in F major, which is a rotation of the famous I–V–vi–IV.
  - **Songs built on these four chords in this order** (in their own keys) include The Cranberries' "Zombie"
    (Em–C–G–D), OneRepublic's "Apologize", Lady Gaga's "Poker Face" and "Despacito".
  - **So:** you've heard this exact harmonic motion hundreds of times, which is the jamais vu.
- **Why it feels like jamais vu rather than déjà vu:** the progression is ambiguous between two keys. Starting on Dm
  says "D minor, sad". The same chords read as F major, "hopeful", once F arrives in bar 3. The added 9ths (E over Dm,
  G over F) and the open voicings blur it further. The ear recognises the motion but can't settle on the mood, so it's
  familiar and strange at once. That fits a dream built from a man's memories.
- **The law, in plain words:** chord progressions aren't protected by copyright. What's protected is a specific melody,
  lyrics, and a specific recording. Our track has no borrowed melody: the "tunes" (the arpeggio and the music box) are
  generated from the chord tones in a fixed pattern, not taken from anything, and every sound is synthesised in code.
- **Credit:** not required. If you'd like to acknowledge it anyway, a credits line such as "chord progression:
  i–VI–III–VII, a common progression" is harmless but unusual.
- **The one real check left:** if a specific song keeps coming to mind, name it. Then we compare its *melody* (not the
  chords) against our arpeggio and music-box patterns. That's the only place a real overlap could exist.

## 6. Notes for the real composition (with the creator)
- **The chords:** keep i–VI–III–VII and the add9 colour; that's where the feel lives.
- **The layering:** keep the three-layer rule (bed / groove / sparkle), and keep the muffled twins.
- **The tension stem:** the dream wants a fourth, "tension" stem driven by the chaser (the music box idea).
- **Room:** consider a little reverb on the pad and the top layers, rendered into the stems so it works on WebGL.
- **The drop-in:** the folder and naming convention are in `docs/MOVEMENT.md`, "Speed music".
