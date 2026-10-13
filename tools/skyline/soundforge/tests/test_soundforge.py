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


# ---- fix round 1 ----

def test_pan_is_constant_power_with_no_jump_at_the_ends():
    l, r = sf._pan_gains(-1.0)
    assert abs(l - 1.0) < 1e-9 and abs(r) < 1e-9
    l, r = sf._pan_gains(0.0)
    assert abs(l - 0.70710678) < 1e-6 and abs(r - 0.70710678) < 1e-6
    for pan in np.linspace(-1, 1, 41):
        l, r = sf._pan_gains(pan)
        assert abs(l * l + r * r - 1.0) < 1e-9
    assert abs(sf._pan_gains(-0.99)[0] - sf._pan_gains(-1.0)[0]) < 0.01  # no ~3 dB jump near hard pan


def test_pan_centre_through_render(tmp_path):
    a = sf.render_recipe({"length": 0.2, "master": {"limit_db": 0},
                          "layers": [{"type": "sine", "freq": 200, "attack": 0.001, "decay": 50, "pan": 0}]}, tmp_path)
    assert abs(np.abs(a[:, 0]).max() - 0.7071) < 0.01
    assert abs(np.abs(a[:, 1]).max() - 0.7071) < 0.01


def test_bad_layer_past_the_end_still_raises(tmp_path):
    with pytest.raises(sf.RecipeError, match=r"layer 0.*'saw'"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "saw", "start": 5.0}]}, tmp_path)
    with pytest.raises(sf.RecipeError, match=r"layer 0.*nope\.wav"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "sample", "path": "nope.wav", "start": 5.0}]}, tmp_path)


def test_timeline_errors_name_the_event(tmp_path):
    (tmp_path / "bad.json").write_text(json.dumps({"length": 0.2, "layers": [{"type": "sine", "freq": 100}, {"type": "saw"}]}))
    (tmp_path / "good.json").write_text(json.dumps({"length": 0.05, "layers": [{"type": "noise"}]}))
    with pytest.raises(sf.RecipeError, match=r"event 1 \(bad\.json\): layer 1.*'saw'"):
        sf.render_timeline({"duration": 1.0, "events": [{"at": 0.0, "recipe": "good.json"}, {"at": 0.5, "recipe": "bad.json"}]}, tmp_path)
    with pytest.raises(sf.RecipeError, match=r"event 0.*missing 'at'"):
        sf.render_timeline({"duration": 1.0, "events": [{"recipe": "bad.json"}]}, tmp_path)
    with pytest.raises(sf.RecipeError, match=r"event 0.*missing 'recipe'"):
        sf.render_timeline({"duration": 1.0, "events": [{"at": 0.5}]}, tmp_path)


def test_bad_numbers_raise_recipe_error_naming_the_layer(tmp_path):
    def layer(**kw):
        return {"length": 0.2, "layers": [{"type": "sine", "freq": 100}, {"type": "sine", **kw}]}
    for kw, word in [({"start": -0.1}, "start"), ({"freq": 0}, "freq"), ({"freq": 100, "freq_end": -5}, "freq_end"),
                     ({"highpass": 24000}, "highpass"), ({"lowpass": 0}, "lowpass"), ({"lowpass": 30000}, "lowpass"),
                     ({"decay": "long"}, "decay")]:
        with pytest.raises(sf.RecipeError, match=rf"layer 1.*{word}"):
            sf.render_recipe(layer(**kw), tmp_path)


def test_bad_sample_files_raise_recipe_error(tmp_path):
    (tmp_path / "junk.wav").write_bytes(b"this is not audio")
    sf.write_wav(tmp_path / "empty.wav", np.zeros((0, 2), dtype=np.float32), SR)
    with pytest.raises(sf.RecipeError, match=r"layer 0.*junk\.wav"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "sample", "path": "junk.wav"}]}, tmp_path)
    with pytest.raises(sf.RecipeError, match=r"layer 0.*empty\.wav"):
        sf.render_recipe({"length": 0.2, "layers": [{"type": "sample", "path": "empty.wav"}]}, tmp_path)


def test_bad_event_at_raises(tmp_path):
    (tmp_path / "c.json").write_text(json.dumps({"length": 0.05, "layers": [{"type": "noise"}]}))
    for at in (-0.1, 1.5):
        with pytest.raises(sf.RecipeError, match=r"event 0 \(c\.json\).*'at'"):
            sf.render_timeline({"duration": 1.0, "events": [{"at": at, "recipe": "c.json"}]}, tmp_path)


def test_cli_errors_are_clean(tmp_path, capsys):
    out = str(tmp_path / "o.wav")
    (tmp_path / "broken.json").write_text("{not json")
    (tmp_path / "tl.json").write_text(json.dumps({"events": []}))
    for argv in (["render", str(tmp_path / "missing.json"), "-o", out],
                 ["render", str(tmp_path / "broken.json"), "-o", out],
                 ["timeline", str(tmp_path / "tl.json"), "-o", out]):
        assert sf.main(argv) == 2
        assert capsys.readouterr().err.startswith("soundforge: ")
    assert sf.main(["timeline", str(tmp_path / "tl.json"), "--duration", "0.1", "-o", out]) == 0


# ---- final fix wave ----

def test_editing_one_layer_does_not_reroll_the_noise_of_the_others(tmp_path):
    # Each layer has its own generator (seed + index). Layer 1's noise must be the same sample for sample whatever layer 0 is,
    # including a layer 0 that draws random numbers itself.
    noise_b = {"type": "noise", "attack": 0.001, "decay": 0.2}
    master = {"limit_db": 20}
    silent = {"type": "noise", "start": 5.0}                       # starts past the end: contributes nothing
    loud_a = {"type": "noise", "attack": 0.001, "decay": 0.2, "gain_db": 3}
    quiet_a = {"type": "noise", "attack": 0.001, "decay": 0.2, "gain_db": -9, "color": "pink"}
    base = sf.render_recipe({"length": 0.5, "master": master, "layers": [silent, noise_b]}, tmp_path)
    for a in (loud_a, quiet_a):
        both = sf.render_recipe({"length": 0.5, "master": master, "layers": [a, noise_b]}, tmp_path)
        a_alone = sf.render_recipe({"length": 0.5, "master": master, "layers": [a]}, tmp_path)
        assert np.allclose(both, a_alone + base, atol=1e-5)
    # the same recipe still renders the same every time
    again = sf.render_recipe({"length": 0.5, "master": master, "layers": [loud_a, noise_b]}, tmp_path)
    first = sf.render_recipe({"length": 0.5, "master": master, "layers": [loud_a, noise_b]}, tmp_path)
    assert np.array_equal(again, first)
