# The sound forge (Skyline Repo)

Renders layered sounds from JSON recipes and handling sets (equip, fire, reload...) from event timelines.
Spec: docs/SKYLINE-DESIGN.md 2.3 and 9.4. Shipped sound is synthesized, recorded or CC0; reference clips from other
games are measuring sticks only and never go in this folder.

    uv run python soundforge.py render recipes/test_tone.json -o out.wav
    uv run python soundforge.py timeline <timeline.json> --duration 1.8 -o reload.wav
    uv run --group dev pytest -q

Judge the result with the ears skill (`~/.claude/skills/ears`), against the weapon's reference clips.
