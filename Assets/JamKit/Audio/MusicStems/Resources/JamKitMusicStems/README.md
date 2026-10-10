# Speed music stems: drop the real music here

Each folder here is a **stem set**, named by the game that plays it:

| Folder | Played by |
|---|---|
| `lab/` | the Movement Lab |
| `dream/` | the Visiting Hours dream escape |

Put up to four stems in a set's folder, named exactly:

| File | What it is | When it plays |
|---|---|---|
| `base.wav` (or .ogg) | the bed: pads, bass | always |
| `drive.wav` | rhythm and energy | from the fast tier (8 m/s) |
| `peak.wav` | the excited top: lead, arpeggio, shimmer | while flying (13 m/s) |
| `tension.wav` | optional; the escape only: the music box that detunes as the static closes in | when the game says (the escape's gap) |

Rules:
- **Every stem the same length and tempo**, exported from the same session start to end, so they loop together.
  Loop points on bar lines. Tell the game the tempo: `MovementMusic.bpm` and `beatsPerBar` (the placeholders are
  104 bpm, 4/4).
- Import settings: **Load Type: Decompress On Load** (the game makes the muffled copies from the samples), no
  Normalize, peaks under -1 dBFS.
- Optional: your own muffled copies beside them, `base_muffled.wav` and so on. Without them the game low-passes
  the stems itself at load (on WebGL it can't, so there the music doesn't muffle unless you supply them).
- A missing stem falls back to the placeholder made in code, so you can drop them in one at a time.

Details: `docs/MOVEMENT.md`, "Speed music".
