"""Renders the generic impact sounds every gun's shots play (Task 19's ruling: Task 14 asks for them, nobody made them):

    uv run --project tools/skyline/soundforge python tools/skyline/impact/render.py

- recipes/hit.json   -> Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/impact/hit.wav
  (a bullet striking a bot's plating: a hard metallic tick with a short ring)
- recipes/world.json -> .../SkylineAudio/impact/world.wav
  (a strike on the mall: concrete, tile, glass; short and duller)

SkylineShooting plays them with SkylineSounds.PlayAt at the hit point ("impact/hit" at 0.8, "impact/world" at 0.5). The
recipes are written by tools/skyline/guns/breacher/synth.py (their source layers live in that folder's samples/).
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
UNITY = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineAudio" / "impact"


def main() -> int:
    UNITY.mkdir(parents=True, exist_ok=True)
    count = 0
    for path in sorted((HERE / "recipes").glob("*.json")):
        spec = json.loads(path.read_text(encoding="utf-8"))
        audio = soundforge.render_recipe(spec, path.parent)
        sr = int(spec.get("sample_rate", soundforge.DEFAULT_SR))
        soundfile.write(str(UNITY / (path.stem + ".wav")), np.asarray(audio, dtype=np.float32), sr, subtype="PCM_16")
        count += 1
    print(f"rendered {count} impact sounds into {UNITY}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
