"""Renders the guns' and the bots' sounds for Unity, and the guns' handling clips for the ears.

    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py [--gun writ] [--bot floor-care-unit]

- tools/skyline/guns/<gun>/recipes/<name>.json -> Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/<gun>/<name>.wav
  (16-bit PCM: what the game plays at each handling event)
- tools/skyline/guns/<gun>/<Clip>.timeline.json -> out/skyline-forge/<gun>/<Clip>.wav (a whole clip, to compare with the
  reference clips and to hear the timing; not committed)
- tools/skyline/bots/<id>/recipes/<name>.json -> Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/<id>/<name>.wav
  (Task 20: the sound names of Task 15's contract). A bot folder may have a `sources.py` that synthesizes the stems its
  recipes' `sample` layers read (tools/skyline/bots/<id>/sources/); it runs first, so a render always uses fresh stems.

With --gun only that gun is rendered (no bots); with --bot only that bot (no guns); with neither, everything.
"""
from __future__ import annotations

import argparse
import json
import runpy
import sys
from pathlib import Path

import numpy as np
import soundfile

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "skyline" / "soundforge"))
import soundforge  # noqa: E402

GUNS = ROOT / "tools" / "skyline" / "guns"
BOTS = ROOT / "tools" / "skyline" / "bots"
UNITY = ROOT / "Assets" / "JamKit" / "Templates" / "SkylineRepo" / "Resources" / "SkylineAudio"
FORGE_OUT = ROOT / "out" / "skyline-forge"


def render_recipes(folder: Path, out: Path) -> int:
    """Every recipes/*.json under `folder` -> out/<name>.wav (16-bit PCM)."""
    out.mkdir(parents=True, exist_ok=True)
    count = 0
    for path in sorted((folder / "recipes").glob("*.json")):
        spec = json.loads(path.read_text(encoding="utf-8"))
        audio = soundforge.render_recipe(spec, path.parent)
        sr = int(spec.get("sample_rate", soundforge.DEFAULT_SR))
        soundfile.write(str(out / (path.stem + ".wav")), np.asarray(audio, dtype=np.float32), sr, subtype="PCM_16")
        count += 1
    return count


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--gun", help="only this gun's folder")
    ap.add_argument("--bot", help="only this bot's folder")
    a = ap.parse_args(argv)
    sounds = clips = bot_sounds = 0
    if not a.bot:
        for gun in sorted(p for p in GUNS.iterdir() if (p / "recipes").is_dir()):
            if a.gun and gun.name != a.gun:
                continue
            sounds += render_recipes(gun, UNITY / gun.name)
            (FORGE_OUT / gun.name).mkdir(parents=True, exist_ok=True)
            for path in sorted(gun.glob("*.timeline.json")):
                audio = soundforge.render_timeline(json.loads(path.read_text(encoding="utf-8")), gun)
                soundforge.write_wav(FORGE_OUT / gun.name / path.name.replace(".timeline.json", ".wav"), audio)
                clips += 1
    if not a.gun and BOTS.is_dir():
        for bot in sorted(p for p in BOTS.iterdir() if (p / "recipes").is_dir()):
            if a.bot and bot.name != a.bot:
                continue
            if (bot / "sources.py").is_file():
                runpy.run_path(str(bot / "sources.py"), run_name="skyline_bot_sources")["main"]()
            bot_sounds += render_recipes(bot, UNITY / bot.name)
    print(f"rendered {sounds} gun sounds and {bot_sounds} bot sounds into {UNITY}, and {clips} clips into {FORGE_OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
