import json
from pathlib import Path

import numpy as np
import pytest

import soundforge as sf

SR = 48000


def peak_freq(mono, sr=SR):
    spec = np.abs(np.fft.rfft(mono * np.hanning(len(mono))))
    return np.fft.rfftfreq(len(mono), 1 / sr)[np.argmax(spec)]


def onset(mono, sr=SR, frac=0.1):
    level = np.abs(mono)
    return np.argmax(level >= level.max() * frac) / sr


def test_sine_has_its_frequency(tmp_path):
    a = sf.render_recipe({"length": 1.0, "layers": [{"type": "sine", "freq": 440, "attack": 0.01, "decay": 5.0}]}, tmp_path)
    assert a.shape == (SR, 2)
    assert abs(peak_freq(a[:, 0]) - 440) < 2


def test_decay_reaches_minus_60_db(tmp_path):
    a = sf.render_recipe({"length": 1.0, "layers": [{"type": "sine", "freq": 1000, "attack": 0.001, "decay": 0.3}]}, tmp_path)
    m = a[:, 0]
    peak = np.abs(m).max()
    late = np.abs(m[int(0.31 * SR):int(0.33 * SR)]).max()
    assert 20 * np.log10(late / peak) < -55


def test_gain_and_pan(tmp_path):
    a = sf.render_recipe({"length": 0.5, "master": {"limit_db": 0},
                          "layers": [{"type": "sine", "freq": 200, "attack": 0.001, "decay": 50, "gain_db": -6, "pan": -1}]}, tmp_path)
    assert abs(np.abs(a[:, 0]).max() - 10 ** (-6 / 20)) < 0.02
    assert np.abs(a[:, 1]).max() < 1e-6


def test_limiter_caps_the_peak(tmp_path):
    loud = {"type": "sine", "freq": 100, "attack": 0.001, "decay": 50, "gain_db": 0}
    a = sf.render_recipe({"length": 0.3, "layers": [loud, dict(loud)], "master": {"limit_db": -1.0}}, tmp_path)
    assert np.abs(a).max() <= 10 ** (-1 / 20) + 1e-4


def test_timeline_events_sit_at_fractions_and_rescale(tmp_path):
    click = {"length": 0.05, "layers": [{"type": "noise", "attack": 0.0005, "decay": 0.03}]}
    (tmp_path / "click.json").write_text(json.dumps(click))
    tl = {"duration": 2.0, "events": [{"at": 0.5, "recipe": "click.json"}]}
    a = sf.render_timeline(tl, tmp_path)
    assert abs(onset(a[:, 0]) - 1.0) < 0.005
    b = sf.render_timeline(tl, tmp_path, duration=1.0)
    assert abs(onset(b[:, 0]) - 0.5) < 0.005


def test_sample_layer_pitch_shift(tmp_path):
    t = np.arange(SR) / SR
    sf.write_wav(tmp_path / "a440.wav", np.stack([np.sin(2 * np.pi * 440 * t)] * 2, axis=1).astype(np.float32), SR)
    a = sf.render_recipe({"length": 1.0, "master": {"limit_db": 0},
                          "layers": [{"type": "sample", "path": "a440.wav", "pitch_semitones": 12, "attack": 0.001, "decay": 50}]}, tmp_path)
    assert abs(peak_freq(a[: SR // 2, 0]) - 880) < 4


def test_missing_sample_names_the_layer(tmp_path):
    with pytest.raises(sf.RecipeError, match=r"layer 1 .*nope\.wav"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "sine", "freq": 100}, {"type": "sample", "path": "nope.wav"}]}, tmp_path)


def test_unknown_layer_type_names_the_layer(tmp_path):
    with pytest.raises(sf.RecipeError, match=r"layer 0.*'saw'"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "saw"}]}, tmp_path)


def test_test_tone_recipe_renders():
    here = Path(__file__).resolve().parent.parent / "recipes"
    a = sf.render_recipe(json.loads((here / "test_tone.json").read_text()), here)
    assert abs(peak_freq(a[:, 0]) - 440) < 3
