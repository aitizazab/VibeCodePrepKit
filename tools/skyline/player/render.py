"""Renders the player-side feedback sounds and CONCIERGE's PA cues (judge round 1: "taking damage barely registers"; "no
fight-start sting, no PA chime"), all synthesized with the sound forge:

    uv run --project tools/skyline/soundforge python tools/skyline/player/render.py

- recipes/hurt.json             -> Resources/SkylineAudio/player/hurt.wav
  (the Unit's shell dented: a low body thump, a short crunch and a metallic ring, with a falling servo whine)
- recipes/plating_break.json    -> Resources/SkylineAudio/player/plating_break.wav
  (the plating cracking off: a sharp crack, a heavy thud, shards tinkling away, a power-down drop)
- recipes/concierge_chime.json  -> Resources/SkylineAudio/concierge/chime.wav
  (the mall's PA "ding-dong" before each CONCIERGE notice: E5 then C5, bell-like)
- recipes/concierge_sting.json  -> Resources/SkylineAudio/concierge/sting.wav
  (a fight starting: three quick rising tones over a low pulse, a polite chime turned alarm)

SkylineDamageFeedback plays them by name through SkylineSounds.PlayViewmodel (the mix owns their levels).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import soundfile

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "skyline" / "soundforge"))
import soundforge  # noqa: E402

HERE = Path(__file__).resolve().parent
AUDIO = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineAudio"
OUT = {
    "hurt": AUDIO / "player" / "hurt.wav",
    "plating_break": AUDIO / "player" / "plating_break.wav",
    "concierge_chime": AUDIO / "concierge" / "chime.wav",
    "concierge_sting": AUDIO / "concierge" / "sting.wav",
}


def main() -> int:
    for stem, dest in OUT.items():
        path = HERE / "recipes" / (stem + ".json")
        spec = json.loads(path.read_text(encoding="utf-8"))
        audio = soundforge.render_recipe(spec, path.parent)
        sr = int(spec.get("sample_rate", soundforge.DEFAULT_SR))
        dest.parent.mkdir(parents=True, exist_ok=True)
        soundfile.write(str(dest), np.asarray(audio, dtype=np.float32), sr, subtype="PCM_16")
        print(f"{stem} -> {dest.relative_to(ROOT)} ({len(audio) / sr:.2f} s, peak {20 * np.log10(np.abs(audio).max()):.1f} dBFS)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
