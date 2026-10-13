"""Small synthesis kit for the bots' source stems (Task 20). Every bot's `sources.py` builds its stems with these and
writes them to `tools/skyline/bots/<id>/sources/`; the recipes in `recipes/` mix the stems (sound forge `sample` layers)
with the forge's own sine and noise layers. Everything is synthesized and seeded, so a re-run gives the same files.

Mono float arrays at SR. Times in seconds.
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
import soundfile
from scipy import signal

SR = 48000


def n_of(sec: float) -> int:
    return int(round(sec * SR))


def zeros(sec: float) -> np.ndarray:
    return np.zeros(n_of(sec))


def curve(points, n: int, exp: bool = False) -> np.ndarray:
    """A per-sample curve through (time, value) points; linear, or exponential (for frequencies) when exp."""
    ts = np.array([p[0] for p in points], dtype=float)
    vs = np.array([p[1] for p in points], dtype=float)
    t = np.arange(n) / SR
    if exp:
        return np.exp(np.interp(t, ts, np.log(vs)))
    return np.interp(t, ts, vs)


def envelope(points, n: int) -> np.ndarray:
    return curve(points, n)


def ad(n: int, attack: float, decay: float) -> np.ndarray:
    """Linear attack, then an exponential fall reaching -60 dB `decay` seconds after it."""
    t = np.arange(n) / SR
    g = np.minimum(1.0, t / attack) if attack > 0 else np.ones(n)
    return g * np.exp(-np.maximum(0.0, t - attack) * np.log(1000.0) / max(decay, 1e-4))


def phase(freq) -> np.ndarray:
    return 2 * np.pi * np.cumsum(np.asarray(freq, dtype=float)) / SR


def osc(freq, n: int | None = None, kind: str = "sine", duty: float = 0.5, harmonics: int = 24) -> np.ndarray:
    """An oscillator following a frequency (a number or a per-sample array). Square, saw and pulse are band-limited by
    summing harmonics under Nyquist."""
    if np.isscalar(freq):
        freq = np.full(n, float(freq))
    freq = np.asarray(freq, dtype=float)
    ph = phase(freq)
    if kind == "sine":
        return np.sin(ph)
    if kind == "tri":
        x = np.zeros_like(ph)
        for k in range(1, harmonics * 2, 2):
            x += np.where(freq * k < SR / 2.2, ((-1) ** ((k - 1) // 2)) * np.sin(k * ph) / (k * k), 0.0)
        return x * 8 / np.pi ** 2
    x = np.zeros_like(ph)
    for k in range(1, harmonics + 1):
        ok = freq * k < SR / 2.2
        if kind == "saw":
            x += np.where(ok, np.sin(k * ph) / k, 0.0)
        elif kind == "square":
            if k % 2:
                x += np.where(ok, np.sin(k * ph) / k, 0.0)
        elif kind == "pulse":
            x += np.where(ok, np.sin(np.pi * k * duty) * np.cos(k * ph) / k, 0.0)
    return x * (4 / np.pi if kind == "square" else 2 / np.pi)


def noise(n: int, rng: np.random.Generator, color: str = "white") -> np.ndarray:
    w = rng.standard_normal(n)
    if color == "white":
        return w / 3.0
    spec = np.fft.rfft(w)
    f = np.arange(len(spec), dtype=float)
    f[0] = 1.0
    spec = spec / (np.sqrt(f) if color == "pink" else f)
    x = np.fft.irfft(spec, n)
    return x / (np.abs(x).max() or 1.0)


def _sos(kind, f, order=2):
    return signal.butter(order, f, kind, fs=SR, output="sos")


def lowpass(x, f, order=2):
    return signal.sosfilt(_sos("lowpass", f, order), x)


def highpass(x, f, order=2):
    return signal.sosfilt(_sos("highpass", f, order), x)


def bandpass(x, lo, hi, order=2):
    return signal.sosfilt(_sos("bandpass", [lo, hi], order), x)


def peak(x, f, q):
    """A resonant band (a body, a formant): scipy's iirpeak."""
    b, a = signal.iirpeak(f, q, fs=SR)
    return signal.lfilter(b, a, x)


def sweep_bandpass(x, centers, q: float = 4.0, block: int = 256) -> np.ndarray:
    """A band-pass whose centre follows a per-sample curve (block-wise state-variable filter)."""
    y = np.zeros_like(x)
    low = band = 0.0
    for s in range(0, len(x), block):
        fc = float(centers[min(s, len(centers) - 1)])
        f = 2 * np.sin(np.pi * min(fc, SR / 6) / SR)
        damp = 1.0 / q
        for i in range(s, min(s + block, len(x))):
            high = x[i] - low - damp * band
            band += f * high
            low += f * band
            y[i] = band
    return y


def modal(n: int, freqs, decays, amps, rng: np.random.Generator | None = None, jitter: float = 0.0) -> np.ndarray:
    """A struck body: damped sines (plastic, metal, glass)."""
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f, d, a in zip(freqs, decays, amps):
        if rng is not None and jitter:
            f *= 1 + rng.uniform(-jitter, jitter)
        x += a * np.sin(2 * np.pi * f * t + (rng.uniform(0, 6.28) if rng is not None else 0.0)) * np.exp(-t * np.log(1000) / d)
    return x


def click(n: int, rng: np.random.Generator, bright: float = 4000.0, decay: float = 0.004) -> np.ndarray:
    x = noise(n, rng) * ad(n, 0.0002, decay)
    return highpass(x, bright * 0.4) if bright else x


def bubble(n: int, f0: float, rise: float, decay: float) -> np.ndarray:
    """A bubble's ring: a sine whose pitch rises as it decays (Minnaert, with the rise that makes it read as liquid)."""
    t = np.arange(n) / SR
    f = f0 * (1 + rise * t)
    return np.sin(phase(f)) * np.exp(-t * np.log(1000) / decay) * np.minimum(1, t / 0.0015)


def bubbles(sec: float, rng, rate: float, f_lo: float, f_hi: float, rise=(8, 30), decay=(0.015, 0.05),
            density=None) -> np.ndarray:
    """Random bubbles at `rate` per second (scaled by an optional per-sample density curve)."""
    n = n_of(sec)
    out = np.zeros(n)
    t = 0.0
    while t < sec:
        t += rng.exponential(1.0 / rate)
        i = n_of(t)
        if i >= n:
            break
        if density is not None and rng.uniform() > density[i]:
            continue
        m = n_of(0.12)
        b = bubble(m, np.exp(rng.uniform(np.log(f_lo), np.log(f_hi))), rng.uniform(*rise), rng.uniform(*decay))
        place(out, b * rng.uniform(0.4, 1.0), t)
    return out


def crackle(sec: float, rng, rate: float, density=None, decay: float = 0.0015, bright: float = 3000.0) -> np.ndarray:
    """Sparse crackle: random tiny clicks at `rate` per second with random levels."""
    n = n_of(sec)
    out = np.zeros(n)
    m = n_of(0.01)
    k = click(m, rng, 0, decay)
    count = rng.poisson(rate * sec)
    for _ in range(count):
        i = int(rng.uniform(0, n))
        if density is not None and rng.uniform() > density[i]:
            continue
        a = rng.uniform(0.2, 1.0) ** 2 * (1 if rng.uniform() > 0.5 else -1)
        j = min(n, i + m)
        out[i:j] += a * k[: j - i]
    return highpass(out, bright) if bright else out


def squeak(sec: float, f0: float, f1: float, rng, wobble: float = 0.04, rate: float = 28.0, formant: float = 2600.0,
           grit: float = 0.3) -> np.ndarray:
    """A rubber squeak (a wheel, a toy): stick-slip pulses with a wobbling pitch through a formant."""
    n = n_of(sec)
    f = curve([(0, f0), (sec, f1)], n, exp=True)
    f = f * (1 + wobble * np.sin(phase(np.full(n, rate))) + 0.01 * lowpass(rng.standard_normal(n), 40) * 10)
    x = osc(f, kind="pulse", duty=0.18, harmonics=16)
    x = 0.6 * x + 0.4 * peak(x, formant, 3.0) * 3
    x += grit * bandpass(noise(n, rng), 1500, 6000) * (0.5 + 0.5 * np.sin(phase(f)))
    return x


def chime(sec: float, freq: float, decay: float = 0.4, bright: float = 0.25, kind: str = "marimba") -> np.ndarray:
    """A soft appliance chime: a struck bar (marimba partials) or a bell."""
    n = n_of(sec)
    if kind == "marimba":
        parts = [(1.0, 1.0, decay), (3.93, bright, decay * 0.35), (9.4, bright * 0.25, decay * 0.12)]
    else:
        parts = [(1.0, 1.0, decay), (2.0, bright, decay * 0.6), (2.76, bright * 0.6, decay * 0.4), (5.4, bright * 0.3, decay * 0.2)]
    t = np.arange(n) / SR
    x = np.zeros(n)
    for r, a, d in parts:
        if freq * r < SR / 2.2:
            x += a * np.sin(2 * np.pi * freq * r * t) * np.exp(-t * np.log(1000) / d)
    return x * np.minimum(1, t / 0.003)


def place(dst: np.ndarray, src: np.ndarray, at: float, gain: float = 1.0) -> np.ndarray:
    i = n_of(at)
    if i >= len(dst):
        return dst
    j = min(len(dst), i + len(src))
    dst[i:j] += gain * src[: j - i]
    return dst


def fade(x: np.ndarray, fin: float = 0.002, fout: float = 0.01) -> np.ndarray:
    n = len(x)
    g = np.ones(n)
    a, b = n_of(fin), n_of(fout)
    if a:
        g[:a] = np.linspace(0, 1, a)
    if b:
        g[-b:] *= np.linspace(1, 0, b)
    return x * g


def soft_clip(x: np.ndarray, drive: float = 1.0) -> np.ndarray:
    return np.tanh(x * drive) / np.tanh(drive)


def norm(x: np.ndarray, peak_db: float = -1.0) -> np.ndarray:
    p = np.abs(x).max()
    return x * (10 ** (peak_db / 20) / p) if p > 0 else x


def db(x: float) -> float:
    return 10 ** (x / 20)


# ------------------------------------------------------------------ loops (circular, so they repeat without a seam)

def loop_noise(n: int, rng, lo: float, hi: float) -> np.ndarray:
    """Band-limited noise that is periodic over n samples (shaped in the frequency domain)."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    mask = 1.0 / np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** 4) / np.sqrt(1 + (f / hi) ** 4)
    x = np.fft.irfft(spec * mask, n)
    return x / (np.abs(x).max() or 1)


def loop_filter(x: np.ndarray, lo: float | None = None, hi: float | None = None) -> np.ndarray:
    """A circular filter (no start-up transient, so a loop stays seamless)."""
    n = len(x)
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    mask = np.ones_like(f)
    if lo:
        mask /= np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** 4)
    if hi:
        mask /= np.sqrt(1 + (f / hi) ** 4)
    return np.fft.irfft(spec * mask, n)


def loop_freq(f: float, sec: float) -> float:
    """The nearest frequency that fits a whole number of cycles in sec (so a tone loops cleanly)."""
    return max(1, round(f * sec)) / sec


def write(path: Path, x: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    x = np.asarray(x, dtype=np.float64)
    if np.abs(x).max() > 1.0:
        x = x / np.abs(x).max()
    soundfile.write(str(path), x.astype(np.float32), SR, subtype="PCM_24")
