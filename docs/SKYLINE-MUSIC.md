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
