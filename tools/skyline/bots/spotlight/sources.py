"""The spotlight's source stems (Task 20b fix round): the rifle shot, rendered from its forge layers
(make_recipes.rifle_layers) and driven into botsynth's soft clip, so the crack's peak no longer sets its level and it
sits as loud as the other bots' attacks (about -15 LUFS). Writes tools/skyline/bots/spotlight/sources/sl_rifle.wav;
recipes/attack.json plays it. render_all.py runs this first (the shared toolchain: botsynth, sources/, render_all).

    uv run --project tools/skyline/soundforge python tools/skyline/bots/spotlight/sources.py
"""
from __future__ import annotations

import runpy
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE.parents[1] / "soundforge"))
import botsynth as bs  # noqa: E402
import soundforge  # noqa: E402

OUT = HERE / "sources"
DRIVE = 2.8


def main():
    rec = runpy.run_path(str(HERE / "make_recipes.py"), run_name="spotlight_recipes")
    spec = {"sample_rate": bs.SR, "length": rec["RIFLE_SECONDS"], "seed": 11, "layers": rec["rifle_layers"](),
            "master": {"limit_db": 0.0}}
    x = soundforge.render_recipe(spec, HERE).mean(axis=1).astype("float64")
    x = bs.norm(bs.soft_clip(bs.norm(x, 0.0), DRIVE), -1.0)
    bs.write(OUT / "sl_rifle.wav", bs.fade(x, 0.0, 0.01))
    print("wrote", OUT / "sl_rifle.wav")


if __name__ == "__main__":
    main()
