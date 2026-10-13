"""First-cut (stand-in) sounds for the Writ and the Breacher: simple synthesized layers so the pipeline plays something
and the sync check has events to find. The weapon identity agents replace every recipe (docs/SKYLINE-PLAN-M1.md Tasks
18-19); nothing here is meant to ship as it is.

    uv run --project tools/skyline/soundforge python tools/skyline/guns/firstcut.py [--force]

Writes tools/skyline/guns/<gun>/recipes/<name>.json, one per sound the gun's handling clips use (SkylineGuns.cs). It
never overwrites a recipe that exists unless --force is given, so it can't clobber an identity agent's work.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent


def click(hp=2500.0, decay=0.04, gain=-6.0, start=0.0):
    return [{"type": "noise", "highpass": hp, "start": start, "attack": 0.0005, "decay": decay, "gain_db": gain}]


def thunk(f0=180.0, f1=70.0, decay=0.12, gain=-6.0, start=0.0):
    return [
        {"type": "sine", "freq": f0, "freq_end": f1, "start": start, "attack": 0.001, "decay": decay, "gain_db": gain},
        {"type": "noise", "lowpass": 900, "start": start, "attack": 0.001, "decay": decay * 0.6, "gain_db": gain - 6},
    ]


def whir(f0=300.0, f1=900.0, length=0.25, gain=-14.0):
    return [
        {"type": "sine", "freq": f0, "freq_end": f1, "attack": 0.03, "decay": length, "gain_db": gain},
        {"type": "noise", "color": "pink", "highpass": 1500, "attack": 0.03, "decay": length, "gain_db": gain - 4},
    ]


def hiss(decay=0.4, gain=-16.0, start=0.0):
    return [{"type": "noise", "highpass": 4000, "start": start, "attack": 0.02, "decay": decay, "gain_db": gain}]


SOUNDS = {
    "writ": {
        "equip_unfold": whir(250, 700, 0.3, -14),
        "equip_lock": click(3000, 0.05, -6) + thunk(400, 200, 0.06, -10),
        "shot": [
            {"type": "noise", "highpass": 1800, "attack": 0.0005, "decay": 0.07, "gain_db": -1},
            {"type": "sine", "freq": 160, "freq_end": 50, "attack": 0.001, "decay": 0.28, "gain_db": -3},
            {"type": "noise", "color": "pink", "lowpass": 2500, "attack": 0.005, "decay": 0.7, "gain_db": -15},
        ],
        "shot_mech": click(3500, 0.03, -12),
        "dry_click": click(4000, 0.03, -8),
        "mag_out": thunk(260, 140, 0.08, -10) + click(2500, 0.03, -14),
        "cell_in": click(2000, 0.05, -8) + thunk(200, 90, 0.1, -9),
        "seat": hiss(0.25, -14) + thunk(120, 60, 0.15, -6),
        "bolt": click(3000, 0.04, -5) + thunk(300, 150, 0.08, -8),
        "inspect_turn": whir(200, 350, 0.4, -18),
        "inspect_tap": click(5000, 0.02, -12),
        "inspect_settle": thunk(220, 120, 0.1, -14),
    },
    "breacher": {
        "equip_rack": thunk(220, 110, 0.12, -6) + click(2200, 0.05, -8, start=0.06),
        "equip_lock": click(2600, 0.05, -6) + thunk(300, 150, 0.08, -9),
        "blast": [
            {"type": "noise", "color": "pink", "lowpass": 3000, "attack": 0.0008, "decay": 0.35, "gain_db": 0},
            {"type": "sine", "freq": 90, "freq_end": 38, "attack": 0.001, "decay": 0.4, "gain_db": -2},
            {"type": "noise", "highpass": 3000, "attack": 0.0005, "decay": 0.05, "gain_db": -6},
            {"type": "noise", "color": "pink", "lowpass": 1200, "attack": 0.01, "decay": 1.1, "gain_db": -16},
        ],
        "vent_hiss": hiss(0.45, -15),
        "dry_clunk": thunk(180, 120, 0.06, -8) + click(1800, 0.03, -12),
        "cell_out": thunk(200, 120, 0.1, -9) + hiss(0.15, -20),
        "cell_in": click(1800, 0.06, -7) + thunk(160, 80, 0.12, -8),
        "seat": hiss(0.3, -13) + thunk(100, 50, 0.2, -5),
        "pump": thunk(240, 120, 0.08, -5) + click(2400, 0.04, -7, start=0.09) + thunk(260, 130, 0.07, -7, start=0.09),
        "inspect_turn": whir(160, 300, 0.45, -18),
        "inspect_knock": thunk(300, 180, 0.05, -10) + click(3000, 0.02, -14),
        "inspect_settle": thunk(180, 100, 0.12, -13),
    },
}


def length(layers):
    end = max(float(l.get("start", 0.0)) + float(l.get("attack", 0.002)) + float(l.get("decay", 0.25)) for l in layers)
    return round(end + 0.05, 3)


def recipe(layers):
    return {"sample_rate": 48000, "length": length(layers), "seed": 1, "layers": layers, "master": {"limit_db": -1.0}}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--force", action="store_true", help="overwrite recipes that already exist")
    a = ap.parse_args(argv)
    written = kept = 0
    for gun, sounds in SOUNDS.items():
        folder = HERE / gun / "recipes"
        folder.mkdir(parents=True, exist_ok=True)
        for name, layers in sounds.items():
            path = folder / f"{name}.json"
            if path.exists() and not a.force:
                kept += 1
                continue
            path.write_text(json.dumps(recipe(layers), indent=2) + "\n", encoding="utf-8")
            written += 1
    print(f"wrote {written} recipes, kept {kept}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
