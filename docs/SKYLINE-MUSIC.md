# Skyline Repo: the music (how it's built and what's decided)

Composed with the creator, layer by layer, each choice explained (as with the Premonition remix); never handed over
finished. The floors run on a gradient: comical and goofy at the bottom, more serious towards the top.

## The opening of floor 1 (the creator's plan, 2026-10-14)

> "Before the 'door' into the first floor opens, we have the notorious six-step riser Subway Surfers has, then it
> transitions into pre-drop sounds from the PopularMMOs theme just before we're launched into movement, where the beat
> drops just like in that opening, and then it transitions into the main song."

In the lobby (`SKYLINE-FLOOR-REPOSSESS.md` section 9): hotel muzak of the theme's opening while the player explores; in
the lift, the six-step riser; the PopularMMOs-style pre-drop as the doors are about to open; the drop as they open and
the wave clock starts; then the floor's main song (a reimagining of the Subway Surfers theme, recognisable for its
first 10-15 s, then its own tune).
The PopularMMOs intro (the creator, 2026-10-14): the 8 s clip `pmmo-intro-8s-NX6lODP5faM.wav`; the drop is at about 3.5 s and everything before it is the pre-drop (measured about 86 BPM). The riser (confirmed): C-E-G-A-Bb walking up the C chord one per beat, landing on C at about 16.1 s of the original.
References (cut and labelled; the creator confirms the moments by ear): `FableAnimationTests/audio/sources/youtube/`
(`ss-riser-candidate-*.wav`, `pmmo-predrop-and-drop-*.wav`; page `art/review/2026-10-14/skyline-music-refs.html`).
Measured: the Subway Surfers theme is about 122.5 BPM; its opening sits on C for about 14 s and moves at about 16 s.
Rights: the themes belong to SYBO and PopularMMOs; quoting them is checked against the jam's rules before publishing.

## Adaptive music (the creator, 2026-10-14)

The layering applies to the floor's main theme and to most places: the music **picks up with speed and settles when
the player isn't moving**, **gets dangerous when many enemies are near**, and **gets more light-hearted when few are
near or the objective is simpler** (parkour between sections).
- **Vertical layering:** each piece is written as stems that always play in sync (one clock, scheduled on the audio
  DSP time); parameters fade stems in and out: a **motion** layer driven by the player's smoothed speed, a **danger**
  layer driven by a threat score (bots alerted within range, weighted by type), a **light** layer when threat is low or
  the section is traversal.
- **Horizontal re-sequencing:** sections (muzak, riser, pre-drop, drop, main loop, calm loop) change only on bar lines;
  a section loops until its trigger, so any player pace stays in sync with set-pieces (the lobby's doors).
- Changes are smoothed (seconds, not frames) so the music never flickers with a single bot.

## The pre-drop: decided (the creator, 2026-10-14 evening)
- **Base A** (the original pre-drop, unfiltered, shifted to C and stretched onto our timing after our riser) is the pre-drop:
  "Base A and B both have the melody, but it sounds better in A." The creator doesn't want to iterate on it further.
  File: `FableAnimationTests/audio/skyline-music/predrop-base-A-full-original.wav` (step3f).
- **Rights:** it is the original recording, so it is fine in private builds and playtests, but it must not ship in anything
  public (a published build, a store or itch.io page, a video) without the owner's permission. Before anything goes public,
  do ONE swap: our instruments play the melody pitch-tracked from base A, the creator compares once, and it replaces the recording.

## Riser to pre-drop: decided (the creator, 2026-10-14 evening)
- **`FableAnimationTests/audio/skyline-music/step6d-t2-3.5-harsh.wav`**: C, E, G and A as they were; then B-flat repeats
  every 3.5 sixteenths (slightly off the grid, so it floats against the pulse), each repeat with more wub; the repeats
  stay at full volume (no level-keeping) and drop harshly over the last three (about -6, -15, -26 dB). Under them the
  original pre-drop (base A) starts at the bar line 30 dB down and swells up over about 1.5 s. Script:
  `FableAnimationTests/.work/music_step6d.py` (on `music_step5.py`).
- Riser instrument candidates (brass, accordion, reed, pairs, with/without a bass and hi-hats): `skyline-music-step6-2.html`;
  not chosen yet.

## The mood references (the creator, 2026-10-14 evening; page `art/review/2026-10-14/skyline-music-refs-moods.html`)
- **Main theme:** the Subway Surfers theme's main part is **after 16 s** (not its opening). Source `audio/sources/youtube/subway-surfers-theme.wav`.
- **Dangerous:** the **first 17 s** of the Empress of Light theme (Terraria). Measured: C minor (our tonic), about 161 BPM,
  bright with little bass, 6-9 note onsets a second. Source `audio/sources/youtube/5qRGzzcw52E - Empress of Light ...wav`.
- **Quiet:** both the **Stonewood** pieces (0-30 s and 62-92 s of `fortnite-stw/stw-stonewood-Y8N9lZUtb6Y.wav`) and both
  **Plankerton** pieces (0-30 s and 50-80 s of `fortnite-stw/stw-plankerton-of-MDd6dvfc.wav`) are the music the creator
  meant; "we can take the parts we want from them". Measured: slow (about 92-96 BPM), dark-toned, sparse; Plankerton in C major.
- Plan (proposed 2026-10-14): normal is the default; danger is a parallel C-minor arrangement crossfaded on bar lines by
  threat; a light (Save the World) arrangement for traversal or very low threat; Visiting Hours' speed stems (drive, peak,
  muffled-to-open, the whistle; `docs/SPEED-MUSIC-BREAKDOWN.md`) on top of each mood. Waves every 16 bars (31.35 s).

## The drop and the normal layer (2026-10-15)
- **The drop** (the creator): "The drop should start off with the original drop ... and then continue into the rest of
  the song." The earlier render had noise in the drop's second half (librosa's stretch smearing it) and friction into the
  stand-in main part (the drop's final hit ringing into a different groove). Now: the original drop (3.31 s to just before
  its final hit at 6.77 s of the PopularMMOs intro), stretched with Rubber Band (crisp transients), down a whole step;
  our song starts on a clean downbeat exactly where that final hit would be.
- **The normal layer, drafted for the creator's judgement** (the creator: "come up with and make the entire rest of the
  normal layer ... making sure it can loop"): 122.5 BPM, A minor / C major, 32 bars looping from the end of A' to A bar 1:
  A theme (Am F C G x2; a steel-pan hook whose first bar uses the riser's rhythm), B answer (F G Em Am, F G Am Am; the
  riser's instrument walks up each chord), C breakdown (kick and hats, held bass, pad, arpeggio, the hook echoed; a snare
  roll and the riser figure on G lead back), A' return (theme with the arpeggio and a slow brass line). Waves every 16
  bars land on A and C. Stems: drums, bass, chords, lead, brass, arp.
- Files: `FableAnimationTests/audio/skyline-music/song-normal/` (full, loop, loop-check, stems, `song-normal.json`);
  scripts in `audio/skyline-music/scripts/`; page `art/review/2026-10-15/skyline-music-song-normal.html`.

## The normal layer, version 2 (2026-10-15)
- The creator on v1: "it doesn't connect from the opening cleanly at all, and the main instrument alone sounds too retro
  or digital ... Try layering it, and using our music analysis skill or script ... Try continuing the end of the opening
  sound into something that settles, and starting there. It absolutely needs to be clean." v1 was synthesised in numpy
  and never measured against a reference; v2 fixes both.
- **Tools (use these for every music step):** compose as MIDI (mido) and render with sampled instruments: the
  GeneralUser GS SoundFont (`C:/Users/User/tools/soundfonts/GeneralUser-GS.sf2`; also SGM-V2.01 and THInst in
  `.../soundfonts/toby/`) through FluidSynth 2.6 (`C:/Users/User/tools/fluidsynth/.../fluidsynth.exe -ni -F out.wav -r 48000 sf2 mid`;
  the project skill `.claude/skills/music-gen`); measure every render with the ears skill
  (`uv run --project ~/.claude/skills/ears python ~/.claude/skills/ears/listen.py compare take.wav ref.wav`) against the
  reference; `audio-tools/sf2-presets.py` lists a SoundFont's instruments.
- **The settle:** the drop's own final hit is kept as the song's first downbeat; its sound is spectrally frozen and darkens
  over two bars while a warm pad, strings and a held bass take over on F (the drop ends on F, measured), then electric
  piano, hats, kick and a snare pickup over 4 bars into A. Join measured clean (level -13.5 dB before, -12.8 after; the
  largest sample jump is inside the original drop). Settle 5-8 dB under the drop; A about 1 dB under it.
- **Band:** GM drum kit + congas + tambourine, finger bass + a sine sub, muted-guitar off-beats, electric piano, warm
  pad, slow strings, brass section (B's answers, the C callback, A's counter line), vibraphone arpeggio. **Hook:** steel
  drums + marimba (+ vibraphone an octave up in A'); four other layerings rendered for the creator to pick.
- **Balance vs the Subway Surfers main part (ears):** first v2 render 35% low+sub / 60% mid (reference 95% / 4%); now
  about 60% low+sub / 31% mid, with congas and tambourine for density (30 events vs the reference's 124 in 16 s by the
  ears event detector). Still lighter and busier in the mids than the reference: the hook and comping live there.
- Files: `FableAnimationTests/audio/skyline-music/song-normal-v2/` (full, loop, loop-check, drop-to-settle, stems,
  hook-options, page copies); script `audio/skyline-music/scripts/music_song_normal_v2.py`; page
  `art/review/2026-10-15/skyline-music-song-normal-v2.html`.

## What's next with the music (in order)
1. The creator's verdict on v2 and the hook layering pick (and the still-open riser instrument pick).
2. **The danger layer** (Empress of Light's first 17 s): a parallel arrangement of the same 32 bars on the same clock,
   crossfaded on bar lines by threat: the minor colour pushed further (harmonic minor's G#, the pre-drop's A-flat/E-flat
   colour), a driving low string ostinato, choir pad, fast high bell runs in sixteenths, heavier drums. Built against v2's stems.
3. **The quiet layer** (Stonewood and Plankerton): the same chords stripped to soft keys, plucked guitar and strings,
   no drums, slower-feeling; for traversal and low threat.
4. **The speed stems** (Visiting Hours' system): drive (eighth bass pulse, four-on-the-floor, hats, clap) and peak
   (sixteenth arpeggio, shimmer), muffled-to-open with speed, per mood.
5. **In the game:** M2 (the music director: the sequencer exists, `MusicSequencer.cs`; stems scheduled on DSP time,
   bar-line changes) and the lift-ride opening.
6. Before anything is public: swap the original pre-drop and drop recordings for our instruments (one comparison).
