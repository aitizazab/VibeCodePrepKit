"""The sound forge: renders layered sounds from JSON recipes, and handling sets from event timelines.

docs/SKYLINE-DESIGN.md 2.3 and 9.4. Recipes stack layers (sines with sweeps, noise, recorded or CC0 samples), each with
an envelope, gain, pan and filters. Timelines place recipes at fractions of a duration, so a faster reload rescales its
sound events with its animation.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
import soundfile
from scipy import signal

DEFAULT_SR = 48000
LN1000 = np.log(1000.0)  # -60 dB


class RecipeError(ValueError):
    """A recipe that can't be rendered; the message names the layer."""


def write_wav(path, audio: np.ndarray, sr: int = DEFAULT_SR) -> None:
    soundfile.write(str(path), np.asarray(audio, dtype=np.float32), sr, subtype="FLOAT")


def _shape(n: int, sr: int, attack: float, decay: float) -> np.ndarray:
    """Linear attack, then an exponential decay that reaches -60 dB `decay` seconds after the attack."""
    t = np.arange(n) / sr
    gain = np.ones(n, dtype=np.float64)
    if attack > 0:
        gain = np.minimum(1.0, t / attack)
    after = np.maximum(0.0, t - attack)
    if decay > 0:
        gain = gain * np.exp(-after * LN1000 / decay)
    return gain


def _filter(x: np.ndarray, sr: int, layer: dict) -> np.ndarray:
    if layer.get("highpass"):
        x = signal.sosfilt(signal.butter(2, layer["highpass"], "highpass", fs=sr, output="sos"), x)
    if layer.get("lowpass"):
        x = signal.sosfilt(signal.butter(2, layer["lowpass"], "lowpass", fs=sr, output="sos"), x)
    return x


def _pink(n: int, rng: np.random.Generator) -> np.ndarray:
    white = rng.standard_normal(n)
    spec = np.fft.rfft(white)
    f = np.arange(len(spec), dtype=np.float64)
    f[0] = 1.0
    pink = np.fft.irfft(spec / np.sqrt(f), n)
    return pink / (np.abs(pink).max() or 1.0)


def _load_sample(path: Path, sr: int, semitones: float) -> np.ndarray:
    data, file_sr = soundfile.read(str(path), dtype="float64", always_2d=True)
    mono = data.mean(axis=1)
    step = (file_sr / sr) * 2.0 ** (semitones / 12.0)  # source samples consumed per output sample
    n = int(len(mono) / step)
    return np.interp(np.arange(n) * step, np.arange(len(mono)), mono)


def _pan_gains(pan: float) -> tuple[float, float]:
    """Hard left/right at the ends, full level on both sides at the centre, a constant-power curve between."""
    pan = float(np.clip(pan, -1.0, 1.0))
    if pan == 0.0:
        return 1.0, 1.0
    if pan <= -1.0:
        return 1.0, 0.0
    if pan >= 1.0:
        return 0.0, 1.0
    angle = (pan + 1.0) * np.pi / 4.0
    return float(np.cos(angle) * np.sqrt(2)), float(np.sin(angle) * np.sqrt(2))


def _layer(layer: dict, index: int, n: int, sr: int, base_dir: Path, rng: np.random.Generator) -> np.ndarray:
    kind = layer.get("type")
    start = int(round(float(layer.get("start", 0.0)) * sr))
    if start >= n:
        return np.zeros((n, 2))
    m = n - start
    if kind == "sine":
        t = np.arange(m) / sr
        f0 = float(layer.get("freq", 440.0))
        f1 = float(layer.get("freq_end", f0))
        if f1 == f0:
            phase = 2 * np.pi * f0 * t
        else:  # exponential sweep from f0 to f1 over the layer
            k = np.log(f1 / f0) / (m / sr)
            phase = 2 * np.pi * f0 * (np.exp(k * t) - 1) / k
        x = np.sin(phase)
    elif kind == "noise":
        x = _pink(m, rng) if layer.get("color") == "pink" else rng.uniform(-1.0, 1.0, m)
    elif kind == "sample":
        path = base_dir / str(layer.get("path", ""))
        if not path.is_file():
            raise RecipeError(f"layer {index} (sample): file not found: {path}")
        x = _load_sample(path, sr, float(layer.get("pitch_semitones", 0.0)))[:m]
        x = np.pad(x, (0, m - len(x)))
    else:
        raise RecipeError(f"layer {index}: unknown type {kind!r} (use 'sine', 'noise' or 'sample')")
    x = _filter(x, sr, layer)
    x = x * _shape(m, sr, float(layer.get("attack", 0.002)), float(layer.get("decay", 0.25)))
    x = x * 10 ** (float(layer.get("gain_db", 0.0)) / 20.0)
    left, right = _pan_gains(layer.get("pan", 0.0))
    out = np.zeros((n, 2))
    out[start:, 0] = x * left
    out[start:, 1] = x * right
    return out


def _master(audio: np.ndarray, master: dict) -> np.ndarray:
    limit = 10 ** (float(master.get("limit_db", -1.0)) / 20.0)
    peak = np.abs(audio).max() if audio.size else 0.0
    if peak > limit:
        audio = audio * (limit / peak)
    return audio


def render_recipe(recipe: dict, base_dir: Path, sr: int | None = None) -> np.ndarray:
    sr = int(sr or recipe.get("sample_rate", DEFAULT_SR))
    n = int(round(float(recipe.get("length", 1.0)) * sr))
    rng = np.random.default_rng(int(recipe.get("seed", 1)))
    out = np.zeros((n, 2))
    for i, layer in enumerate(recipe.get("layers", [])):
        out += _layer(layer, i, n, sr, Path(base_dir), rng)
    return _master(out, recipe.get("master", {})).astype(np.float32)


def render_timeline(timeline: dict, base_dir: Path, duration: float | None = None) -> np.ndarray:
    base_dir = Path(base_dir)
    sr = int(timeline.get("sample_rate", DEFAULT_SR))
    dur = float(duration if duration is not None else timeline["duration"])
    parts = []
    for i, ev in enumerate(timeline.get("events", [])):
        path = base_dir / ev["recipe"]
        if not path.is_file():
            raise RecipeError(f"event {i}: recipe not found: {path}")
        part = render_recipe(json.loads(path.read_text()), path.parent, sr) * 10 ** (float(ev.get("gain_db", 0.0)) / 20.0)
        parts.append((int(round(float(ev["at"]) * dur * sr)), part))
    n = max([int(round(dur * sr))] + [s + len(p) for s, p in parts])
    out = np.zeros((n, 2), dtype=np.float64)
    for s, p in parts:
        out[s:s + len(p)] += p
    return _master(out, timeline.get("master", {})).astype(np.float32)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("render", help="render one recipe")
    r.add_argument("recipe")
    r.add_argument("-o", "--out", required=True)
    t = sub.add_parser("timeline", help="render a handling-set timeline")
    t.add_argument("timeline")
    t.add_argument("--duration", type=float)
    t.add_argument("-o", "--out", required=True)
    a = ap.parse_args(argv)
    path = Path(a.recipe if a.cmd == "render" else a.timeline)
    spec = json.loads(path.read_text())
    try:
        audio = render_recipe(spec, path.parent) if a.cmd == "render" else render_timeline(spec, path.parent, a.duration)
    except RecipeError as e:
        print(f"soundforge: {e}", file=sys.stderr)
        return 2
    sr = int(spec.get("sample_rate", DEFAULT_SR))
    write_wav(a.out, audio, sr)
    print(f"wrote {a.out} ({len(audio) / sr:.3f} s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
