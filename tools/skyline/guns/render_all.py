"""Renders the guns' sounds for Unity and their handling clips for the ears.

    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py [--gun writ]

- tools/skyline/guns/<gun>/recipes/<name>.json -> Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/<gun>/<name>.wav
  (16-bit PCM: what the game plays at each handling event)
- tools/skyline/guns/<gun>/<Clip>.timeline.json -> out/skyline-forge/<gun>/<Clip>.wav (a whole clip, to compare with the
  reference clips and to hear the timing; not committed)
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
import soundfile

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "skyline" / "soundforge"))
import soundforge  # noqa: E402

GUNS = ROOT / "tools" / "skyline" / "guns"
UNITY = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineAudio"
FORGE_OUT = ROOT / "out" / "skyline-forge"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--gun", help="only this gun's folder")
    a = ap.parse_args(argv)
    sounds = clips = 0
    for gun in sorted(p for p in GUNS.iterdir() if (p / "recipes").is_dir()):
        if a.gun and gun.name != a.gun:
            continue
        (UNITY / gun.name).mkdir(parents=True, exist_ok=True)
        for path in sorted((gun / "recipes").glob("*.json")):
            spec = json.loads(path.read_text(encoding="utf-8"))
            audio = soundforge.render_recipe(spec, path.parent)
            sr = int(spec.get("sample_rate", soundforge.DEFAULT_SR))
            soundfile.write(str(UNITY / gun.name / (path.stem + ".wav")), np.asarray(audio, dtype=np.float32), sr, subtype="PCM_16")
            sounds += 1
        (FORGE_OUT / gun.name).mkdir(parents=True, exist_ok=True)
        for path in sorted(gun.glob("*.timeline.json")):
            audio = soundforge.render_timeline(json.loads(path.read_text(encoding="utf-8")), gun)
            soundforge.write_wav(FORGE_OUT / gun.name / path.name.replace(".timeline.json", ".wav"), audio)
            clips += 1
    print(f"rendered {sounds} sounds into {UNITY} and {clips} clips into {FORGE_OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
