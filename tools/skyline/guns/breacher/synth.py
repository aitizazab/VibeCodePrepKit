"""The Breacher's synthesized source layers (Task 19): the textures the sound forge can't make on its own (a saturated
boom with a held body, a room tail with a slap-back, metal parts that ring, a cell that hisses), written as short mono
WAVs into samples/ for the recipes' "sample" layers. Every sound here is made from numbers (sines, noise, filters,
saturation); nothing is recorded or copied, and the reference clips under out/skyline-refs/ are never read.

    uv run --project tools/skyline/soundforge python tools/skyline/guns/breacher/synth.py

Deterministic (fixed seeds): re-running it rewrites the same files. Then render the recipes:

    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py --gun breacher
    uv run --project tools/skyline/soundforge python tools/skyline/impact/render.py
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
import soundfile
from scipy import signal

SR = 48000
HERE = Path(__file__).resolve().parent
OUT = HERE / "samples"


# ---------------------------------------------------------------- building blocks

def n_of(seconds: float) -> int:
    return int(round(seconds * SR))


def t_of(n: int) -> np.ndarray:
    return np.arange(n) / SR


def env(n: int, attack: float, hold: float, decay: float, sustain_drop_db: float = 0.0) -> np.ndarray:
    """Linear attack, a hold that sags by sustain_drop_db, then an exponential fall reaching -60 dB `decay` s later."""
    t = t_of(n)
    g = np.ones(n)
    if attack > 0:
        g = np.minimum(1.0, t / attack)
    in_hold = (t >= attack) & (t < attack + hold)
    if hold > 0:
        g[in_hold] *= 10 ** (-sustain_drop_db / 20 * (t[in_hold] - attack) / hold)
    after = t >= attack + hold
    level_at_end = 10 ** (-sustain_drop_db / 20)
    g[after] = level_at_end * np.exp(-(t[after] - attack - hold) * np.log(1000.0) / max(decay, 1e-4))
    return g


def noise(n: int, seed: int, color: str = "white") -> np.ndarray:
    rng = np.random.default_rng(seed)
    x = rng.standard_normal(n)
    if color == "pink":
        spec = np.fft.rfft(x)
        f = np.arange(len(spec), dtype=np.float64)
        f[0] = 1.0
        x = np.fft.irfft(spec / np.sqrt(f), n)
    elif color == "brown":
        x = np.cumsum(x)
        x = x - np.convolve(x, np.ones(2048) / 2048, mode="same")
    return x / (np.abs(x).max() or 1.0)


def band(x: np.ndarray, lo: float | None, hi: float | None, order: int = 2) -> np.ndarray:
    if lo and hi:
        sos = signal.butter(order, [lo, hi], "bandpass", fs=SR, output="sos")
    elif lo:
        sos = signal.butter(order, lo, "highpass", fs=SR, output="sos")
    elif hi:
        sos = signal.butter(order, hi, "lowpass", fs=SR, output="sos")
    else:
        return x
    return signal.sosfilt(sos, x)


def glide(n: int, f0: float, f1: float, curve: float = 4.0) -> np.ndarray:
    """A sine whose pitch falls (or rises) from f0 to f1, fast at first (exponential approach)."""
    t = t_of(n)
    dur = max(t[-1], 1e-6)
    f = f1 + (f0 - f1) * np.exp(-curve * t / dur)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def modal(n: int, modes: list[tuple[float, float, float]], start: float = 0.0) -> np.ndarray:
    """Struck metal: a sum of decaying sines (freq Hz, decay s to -60 dB, gain dB)."""
    out = np.zeros(n)
    s = n_of(start)
    t = t_of(n - s)
    for f, d, g in modes:
        out[s:] += 10 ** (g / 20) * np.sin(2 * np.pi * f * t + f * 0.001) * np.exp(-t * np.log(1000.0) / d)
    return out


def tick(n: int, at: float, seed: int, lo: float = 3000, hi: float = 12000, decay: float = 0.004, gain_db: float = 0.0) -> np.ndarray:
    """A tiny bright click (a latch tooth, a fragment)."""
    out = np.zeros(n)
    s = n_of(at)
    m = min(n - s, n_of(decay * 3 + 0.002))
    if m <= 0:
        return out
    x = band(noise(m, seed), lo, None if hi >= 12000 else hi)   # 12 kHz and up: open to the top (air)
    out[s:s + m] = x * np.exp(-t_of(m) * np.log(1000.0) / decay) * 10 ** (gain_db / 20)
    return out


def scrape(n: int, start: float, length: float, seed: int, lo: float, hi: float, rate: float, gain_db: float) -> np.ndarray:
    """Metal sliding on metal: band noise, amplitude-modulated by a jittery fast ripple."""
    out = np.zeros(n)
    s = n_of(start)
    m = min(n - s, n_of(length))
    x = band(noise(m, seed), lo, hi)
    t = t_of(m)
    rng = np.random.default_rng(seed + 99)
    ripple = 0.55 + 0.45 * np.sign(np.sin(2 * np.pi * rate * t + rng.uniform(0, 6) + 2.0 * np.sin(2 * np.pi * 7 * t)))
    ripple = np.convolve(ripple, np.ones(48) / 48, mode="same")
    shape = np.sin(np.pi * np.clip(t / length, 0, 1)) ** 0.7
    out[s:s + m] = x * ripple * shape * 10 ** (gain_db / 20)
    return out


def thump(n: int, at: float, f0: float, f1: float, decay: float, gain_db: float) -> np.ndarray:
    out = np.zeros(n)
    s = n_of(at)
    m = n - s
    out[s:] = glide(m, f0, f1, 6.0) * env(m, 0.0015, 0.0, decay) * 10 ** (gain_db / 20)
    return out


def hiss(n: int, start: float, attack: float, length: float, seed: int, lo: float, hi: float, gain_db: float,
         wobble: float = 0.0) -> np.ndarray:
    """Pressurised gas: band noise with a quick swell and a fade, optionally wobbling (a valve fluttering)."""
    out = np.zeros(n)
    s = n_of(start)
    m = min(n - s, n_of(length))
    x = band(noise(m, seed), lo, hi, 3)
    t = t_of(m)
    e = np.minimum(1.0, t / max(attack, 1e-4)) * np.exp(-np.maximum(0, t - attack) * np.log(1000.0) / max(length - attack, 1e-3))
    if wobble:
        e *= 1.0 + wobble * np.sin(2 * np.pi * 23 * t) * np.exp(-t * 4)
    out[s:s + m] = x * e * 10 ** (gain_db / 20)
    return out


def norm(x: np.ndarray, peak_db: float = -1.0) -> np.ndarray:
    p = float(np.abs(x).max()) or 1.0
    return x * (10 ** (peak_db / 20) / p)


def fade_out(x: np.ndarray, seconds: float = 0.01) -> np.ndarray:
    m = min(len(x), n_of(seconds))
    x = x.copy()
    x[-m:] *= np.linspace(1, 0, m)
    return x


def write(name: str, x: np.ndarray, peak_db: float = -1.0) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    x = fade_out(norm(x, peak_db))
    soundfile.write(str(OUT / (name + ".wav")), x.astype(np.float32), SR, subtype="PCM_16")
    print(f"  samples/{name}.wav  {len(x) / SR:.3f} s")


# ---------------------------------------------------------------- the blast

def blast_boom() -> np.ndarray:
    """The blast's held body: a saturated low tone (100 Hz falling to 66 Hz) that stays near full level for about half a
    second, like a pressure wave rolling out of a short barrel, with a rumble riding on it. High-passed at 55 Hz so the
    weight sits in the low band (the Fourth Horseman measures 76% in 60-250 Hz and only 2% below 60 Hz)."""
    n = n_of(1.6)
    tone = glide(n, 104, 66, 3.0) + 0.45 * glide(n, 156, 99, 3.0)
    rumble = band(noise(n, 11, "brown"), 60, 180, 2)
    rumble = rumble / (np.abs(rumble).max() or 1)
    jitter = 1.0 + 0.25 * band(noise(n, 12), None, 30)   # the plateau is jagged, not a smooth sine
    x = (tone * jitter + 0.55 * rumble)
    x = np.tanh(2.2 * x) / np.tanh(2.2)
    x = band(x, 55, 1800, 2)
    return x * env(n, 0.004, 0.42, 0.75, sustain_drop_db=5.0)


def blast_roar() -> np.ndarray:
    """The blast's roar: broadband pressure noise (250 Hz to 6 kHz) held with the boom for about a third of a second,
    brightest at the start, so the shot has the reference's mid and presence (15% and 5% of its energy) and not only its
    weight."""
    n = n_of(1.0)
    x = band(noise(n, 13, "pink"), 250, 6000, 2)
    bright = band(noise(n, 14), 2000, 12000, 2) * env(n, 0.001, 0.05, 0.3)
    flutter = 1.0 + 0.35 * band(noise(n, 15), None, 60) / 0.05
    flutter = np.clip(flutter, 0.3, 1.8)
    body = x * flutter
    body = np.tanh(2.5 * body / np.abs(body).max()) / np.tanh(2.5)   # pressed flat: loud without spiky peaks
    bright = np.tanh(2.0 * bright / np.abs(bright).max()) / np.tanh(2.0)
    return body * env(n, 0.002, 0.38, 0.6, sustain_drop_db=7.0) + 0.8 * bright


def blast_room(seed: int) -> np.ndarray:
    """The space answering: a dark room tail (2.4 s to -60 dB) and a soft slap-back off a far wall at 0.66 s, the way the
    reference's level bumps again near 0.75 s (about 18 dB under the blast)."""
    n = n_of(2.5)
    tail = band(noise(n, seed, "pink"), 60, 650, 2) * env(n, 0.035, 0.12, 3.4)
    tail += 0.2 * band(noise(n, seed + 7), 2000, 10000, 2) * env(n, 0.02, 0.08, 1.6)
    slap = np.zeros(n)
    s = n_of(0.66)
    m = n - s
    slap[s:] = band(noise(m, seed + 1, "pink"), 120, 2400, 2) * env(m, 0.01, 0.05, 0.42)
    slap += thump(n, 0.665, 120, 70, 0.35, -10)
    return tail + 0.16 * slap


def blast_hang(seed: int) -> np.ndarray:
    """The hall's long low hang after the blast: dark rumble that swells in over a quarter second, holds, and is still
    there at 2.5 s (the Fourth Horseman's tail holds at -35 to -40 dB to its end; ours fell to -60 by 2 s)."""
    n = n_of(2.5)
    x = band(noise(n, seed, "pink"), 70, 400, 2)
    return x * env(n, 0.3, 1.6, 4.0)


def blast_debris() -> np.ndarray:
    """Grit and fragments after the crack: a scatter of small ticks over the first 0.35 s (the breach charge's debris),
    getting sparser, then a few pieces landing on the floor up to 1.3 s."""
    n = n_of(1.4)
    x = np.zeros(n)
    rng = np.random.default_rng(31)
    for k in range(34):
        at = 0.02 + 0.33 * rng.random() ** 1.8
        x += tick(n, at, 400 + k, 1500, 9000, 0.003 + 0.004 * rng.random(), -rng.uniform(0, 14))
    for k in range(14):
        at = 0.4 + 0.9 * rng.random()
        # landing fragments: softer and duller than a click (judge round 1: they were broadband clicks at 0.95-1.3 s)
        x += tick(n, at, 450 + k, 1500, 8000, 0.006 + 0.006 * rng.random(), -rng.uniform(13, 23))
    return x


# ---------------------------------------------------------------- the cell, the pump, handling

def vent() -> np.ndarray:
    """The cell venting after a shot: a pressurised hiss with a valve flutter and a falling whistle, the Breacher's
    afterthought."""
    n = n_of(0.62)
    x = hiss(n, 0.0, 0.012, 0.6, 51, 2500, 9500, 0, wobble=0.6)
    x += hiss(n, 0.0, 0.004, 0.18, 52, 900, 3000, -6)
    t = t_of(n)
    whistle = np.sin(2 * np.pi * np.cumsum(2300 - 700 * np.minimum(1, t / 0.45)) / SR) * env(n, 0.02, 0.05, 0.4)
    x += 0.12 * whistle
    x += tick(n, 0.0, 53, 2000, 9000, 0.004, -2)   # the valve's pop: the vent's onset
    return x


def latch(seed: int, pitch: float = 1.0) -> np.ndarray:
    """A heavy latch snapping: a bright tick, then a short steel ring."""
    n = n_of(0.16)
    x = tick(n, 0.0, seed, 2500, 14000, 0.0035, 0)
    x += 0.35 * modal(n, [(2650 * pitch, 0.06, 0), (4300 * pitch, 0.045, -3), (6900 * pitch, 0.03, -6), (9800 * pitch, 0.02, -9)])
    x += thump(n, 0.0, 420 * pitch, 260 * pitch, 0.05, -18)
    return x


def cell_out() -> np.ndarray:
    """The cell unlatches and drops: a heavy latch clack (steel ring plus a solid mid knock), a short pressure release
    (pssht), the cell scraping out of its rails, and a knock as it leaves. Round 2 (N5): more body, less hiss."""
    n = n_of(0.34)
    x = np.pad(latch(61, 0.8), (0, n - n_of(0.16)))
    x += 0.7 * band(noise(n, 64, "pink"), 500, 2200, 2) * env(n, 0.0008, 0.008, 0.06)   # the latch's body
    x += thump(n, 0.0, 220, 120, 0.08, -14)
    x += hiss(n, 0.006, 0.004, 0.12, 62, 3000, 12000, -15)
    x += scrape(n, 0.05, 0.16, 63, 1800, 9000, 90, -14)
    x += thump(n, 0.2, 260, 150, 0.08, -16)
    return x


def cell_in() -> np.ndarray:
    """A fresh cell driven into the rails: the clack of it bottoming comes first (the event's moment, so the sync check
    finds it): a hard tick, a solid mid knock of a heavy part (500-2500 Hz), steel modes and a low thunk. Then the rails'
    ratchet and a short scrape as it settles, and a small second tick. Round 2 (N5): the knock and the thunk carry it; the
    scrape and ratchet sit under them."""
    n = n_of(0.3)
    x = tick(n, 0.0, 74, 900, 13000, 0.006, 0)
    x += 0.9 * band(noise(n, 77, "pink"), 500, 2500, 2) * env(n, 0.0008, 0.01, 0.07)
    x += 0.5 * modal(n, [(1350, 0.07, 0), (2380, 0.06, -2), (4120, 0.045, -4), (6350, 0.03, -8)])
    x += thump(n, 0.0, 190, 95, 0.12, -11)
    rng = np.random.default_rng(75)
    for k in range(4):
        x += tick(n, 0.035 + 0.016 * k + 0.004 * rng.random(), 76 + k, 2000, 9000, 0.004, -11 - 2 * k)
    x += scrape(n, 0.04, 0.12, 73, 1500, 7000, 120, -13)
    x += tick(n, 0.16, 71, 1500, 9000, 0.004, -10)
    return x


def seat() -> np.ndarray:
    """The cell seated and pressurised: the lock's hard clack with a heavy mid knock, a low hydraulic thump (140 to
    62 Hz, the weight), and a short hiss of the cell charging the chamber, cut off by the valve. Round 2 (N5): the thump
    and the knock lead; the hiss is 9 dB lower than before."""
    n = n_of(0.45)
    x = tick(n, 0.0, 81, 1200, 14000, 0.006, 0)
    x += 0.9 * band(noise(n, 84, "pink"), 400, 2000, 2) * env(n, 0.0008, 0.012, 0.08)
    x += 0.5 * modal(n, [(980, 0.1, 0), (1650, 0.09, -1), (3300, 0.07, -4), (5200, 0.05, -8)])
    x += thump(n, 0.0, 140, 62, 0.25, -9)
    # the hydraulic press: a low rumble and a mid groan held while the cell charges (the weight, under the hiss)
    x += 0.12 * band(noise(n, 85, "brown"), 70, 400, 2) / 0.2 * env(n, 0.02, 0.22, 0.15)
    x += 0.1 * glide(n, 420, 300, 2.0) * env(n, 0.03, 0.2, 0.15)
    h = hiss(n, 0.05, 0.1, 0.3, 82, 4000, 14000, -15)
    t = t_of(n)
    h *= np.where(t < 0.33, 1.0, np.exp(-(t - 0.33) * 60))   # cut off when the chamber is full
    x += h
    x += tick(n, 0.33, 83, 2000, 9000, 0.003, -12)     # the valve shutting
    return x


def pump() -> np.ndarray:
    """From empty: the pump racked back and slammed forward. Two strokes, the second the harder, both with steel ring."""
    n = n_of(0.32)
    x = tick(n, 0.0, 91, 2000, 13000, 0.004, -1)
    x += 0.45 * modal(n, [(2100, 0.05, 0), (3900, 0.04, -3), (6200, 0.03, -6)])
    x += scrape(n, 0.012, 0.07, 92, 2500, 12000, 140, -10)
    x += thump(n, 0.0, 300, 170, 0.06, -17)
    x += tick(n, 0.105, 93, 1200, 14000, 0.006, 2)
    x += 0.42 * modal(n, [(1900, 0.08, 0), (3600, 0.06, -2), (5600, 0.045, -4), (8300, 0.03, -8)], start=0.105)
    x += thump(n, 0.105, 220, 110, 0.12, -13)
    return x


def equip_rack() -> np.ndarray:
    """Taken out: the gun's weight caught by the hand (a cloth-and-strap rustle under a grip tick) and the pump's loose
    rattle."""
    n = n_of(0.3)
    x = tick(n, 0.0, 101, 1500, 9000, 0.005, -4)
    x += thump(n, 0.0, 180, 110, 0.08, -24)
    x += 0.9 * band(noise(n, 102, "pink"), 500, 2200, 3) * env(n, 0.01, 0.03, 0.18)
    rng = np.random.default_rng(103)
    for k in range(6):
        x += tick(n, 0.06 + 0.025 * k + 0.008 * rng.random(), 104 + k, 1200, 6000, 0.003, -12 - 2 * k)
    return x


def equip_lock() -> np.ndarray:
    """Ready: a firm lock clack and the cell arming, a short rising charge whine under it."""
    n = n_of(0.36)
    x = 0.35 * latch(111, 0.9)
    x = np.pad(x, (0, n - len(x)))
    x += 1.0 * band(noise(n, 113, "pink"), 380, 1800, 3) * env(n, 0.0008, 0.01, 0.06)   # the receiver's body
    for k in range(5):   # the sling and the pump's play settling
        x += tick(n, 0.03 + 0.03 * k, 114 + k, 450, 2200, 0.006, -6 - 2 * k)
    x += thump(n, 0.0, 160, 80, 0.12, -22)
    t = t_of(n)
    f = 420 + 900 * np.minimum(1, t / 0.22)
    whine = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(n, 0.05, 0.12, 0.16)
    x += 0.07 * whine
    x += hiss(n, 0.02, 0.05, 0.25, 112, 4000, 11000, -22)
    return x


def dry_clunk() -> np.ndarray:
    """Pulled with an empty cell: a dull hammer clunk with no ring, and a tired fizzle where the blast should be."""
    n = n_of(0.3)
    x = tick(n, 0.0, 121, 700, 5000, 0.006, 0)
    x += thump(n, 0.0, 240, 140, 0.07, -4)
    x += 0.2 * modal(n, [(760, 0.03, 0), (1530, 0.02, -4)])
    x += hiss(n, 0.04, 0.02, 0.22, 122, 1500, 5000, -18)
    return x


def inspect_turn() -> np.ndarray:
    """The gun turned over in the hand: a slow creak of the grip and a soft slide of the pump."""
    n = n_of(0.55)
    x = 0.6 * band(noise(n, 131, "pink"), 300, 2500) * env(n, 0.06, 0.15, 0.3)
    x += scrape(n, 0.12, 0.22, 132, 900, 4000, 35, -6)
    x += tick(n, 0.0, 133, 1200, 6000, 0.004, -4)
    return x


def inspect_knock() -> np.ndarray:
    """A knuckle knocked on the cell: a hollow gas-cylinder tonk, twice."""
    n = n_of(0.3)
    x = tick(n, 0.0, 141, 600, 6000, 0.003, -4)
    x += 0.7 * modal(n, [(410, 0.16, 0), (1030, 0.1, -4), (1870, 0.06, -9)])
    x += tick(n, 0.12, 142, 600, 6000, 0.003, -9)
    x += 0.4 * modal(n, [(410, 0.14, 0), (1030, 0.08, -4)], start=0.12)
    return x


def inspect_settle() -> np.ndarray:
    """Back at rest: a light clack and the pump settling."""
    n = n_of(0.22)
    x = tick(n, 0.0, 151, 1500, 10000, 0.004, 0)
    x += thump(n, 0.0, 190, 110, 0.08, -12)
    x += 0.2 * modal(n, [(1600, 0.04, 0), (2950, 0.03, -4)])
    return x


# ---------------------------------------------------------------- hits and the boost

def breacher_hit() -> np.ndarray:
    """The Breacher's pellets landing on a bot, built to cut through its own blast (judge round 2, N2: the old one was a
    103 Hz thump the blast's body masked). The blast lives in 60-250 Hz; this lives in 1-6 kHz:
    a saturated crunch of the casing giving (1.2-5 kHz, 0.1 s), nine pellets rattling on the plating over 45 ms (each a
    hard tick and a short steel ring at 1.9-4.6 kHz), the plating's loose rattle settling to 0.25 s, and only a small
    body knock underneath."""
    n = n_of(0.4)
    x = np.zeros(n)
    crunch = band(noise(n, 175), 1200, 5000, 2)
    crunch = np.tanh(3.0 * crunch / (np.abs(crunch).max() or 1.0))
    x += 0.9 * crunch * env(n, 0.001, 0.02, 0.1)
    rng = np.random.default_rng(161)
    for k in range(9):
        at = 0.045 * rng.random() ** 1.3
        x += tick(n, at, 162 + k, 2000, 14000, 0.005, -rng.uniform(0, 5))
        x += 0.4 * modal(n, [(1900 + 1200 * rng.random(), 0.13, 0), (3300 + 1300 * rng.random(), 0.09, -3)], start=at)
    for k in range(6):
        at = 0.07 + 0.03 * k + 0.012 * rng.random()
        x += tick(n, at, 180 + k, 1500, 7000, 0.004, -10 - 2.5 * k)
        x += 0.1 * modal(n, [(2200 + 900 * rng.random(), 0.05, 0)], start=at)
    x += thump(n, 0.0, 170, 80, 0.12, -9)
    # lightly rounded only (round 3: the heavy press made it louder than the blast); it cuts through by its band, not
    # its level: the recipe sets it about 7 LU under the blast
    return np.tanh(1.1 * x / (np.abs(x).max() or 1.0))


def boost_whump() -> np.ndarray:
    """A boost: the blast's pressure caught under your feet (a low whump), then the rush of air as you rise."""
    n = n_of(0.75)
    x = thump(n, 0.0, 112, 58, 0.42, 0)
    x += 0.5 * band(noise(n, 181, "brown"), 60, 220) * env(n, 0.006, 0.08, 0.35)
    t = t_of(n)
    rush = noise(n, 182, "pink")
    # an upward sweep: a low-pass opening from 500 Hz to 4.5 kHz over 0.4 s (block-wise, a simple time-varying filter)
    out = np.zeros(n)
    blk = 480
    zi = None
    for s in range(0, n, blk):
        fc = 500 + 4000 * min(1.0, (s / SR) / 0.4)
        sos = signal.butter(2, fc, "lowpass", fs=SR, output="sos")
        if zi is None:
            zi = signal.sosfilt_zi(sos) * 0.0
        out[s:s + blk], zi = signal.sosfilt(sos, rush[s:s + blk], zi=zi)
    x += 0.5 * out * env(n, 0.06, 0.15, 0.45)
    return x


def impact_hit() -> np.ndarray:
    """Any bullet striking a bot's plating: a hard metallic tick with a short ring."""
    n = n_of(0.26)
    x = tick(n, 0.0, 191, 2500, 16000, 0.0025, 0)
    x += 0.5 * modal(n, [(2380, 0.11, 0), (3910, 0.08, -2), (5630, 0.06, -5), (7420, 0.04, -9)])
    x += thump(n, 0.0, 360, 220, 0.04, -9)
    return x


def impact_world() -> np.ndarray:
    """A strike on the mall (concrete, tile, glass): a dull short crack, a low knock and a little falling grit; no ring."""
    n = n_of(0.28)
    x = tick(n, 0.0, 201, 700, 5000, 0.008, 0)
    x += 0.6 * band(noise(n, 202, "pink"), 500, 3500) * env(n, 0.0008, 0.004, 0.05)
    x += thump(n, 0.0, 170, 90, 0.05, -6)
    rng = np.random.default_rng(203)
    for k in range(8):
        x += tick(n, 0.03 + 0.12 * rng.random(), 204 + k, 800, 3500, 0.003, -14 - 6 * rng.random())
    x += tick(n, 0.004, 215, 4500, 11000, 0.006, -19)   # a faint glassy edge
    return x


# ---------------------------------------------------------------- the recipes (what the forge renders for Unity)

def S(name: str, gain: float = 0.0, pan: float = 0.0, start: float = 0.0, **kw) -> dict:
    """A sample layer from samples/ (the forge mixes it to mono, then pans it). decay is long so the sample's own shape
    stands."""
    d = {"type": "sample", "path": f"../samples/{name}.wav", "start": start, "attack": 0.0, "decay": 30.0, "gain_db": gain, "pan": pan}
    d.update(kw)
    return d


def N(gain: float, decay: float, attack: float = 0.0005, hp: float | None = None, lp: float | None = None, pink: bool = False,
      pan: float = 0.0, start: float = 0.0) -> dict:
    d = {"type": "noise", "start": start, "attack": attack, "decay": decay, "gain_db": gain, "pan": pan}
    if pink:
        d["color"] = "pink"
    if hp:
        d["highpass"] = hp
    if lp:
        d["lowpass"] = lp
    return d


def W(gain: float, f0: float, f1: float, decay: float, attack: float = 0.001, start: float = 0.0, pan: float = 0.0) -> dict:
    return {"type": "sine", "freq": f0, "freq_end": f1, "start": start, "attack": attack, "decay": decay, "gain_db": gain, "pan": pan}


def R(length: float, layers: list[dict], limit: float = -1.5, seed: int = 19) -> dict:
    return {"sample_rate": SR, "length": length, "seed": seed, "layers": layers, "master": {"limit_db": limit}}


HANG = -22.0   # the hang level in the blast (dB)

IMPACT_DB = -10.0   # breacher/impact's level: about 7 LU under the blast (judge round 3)

RECIPES = {
    # The blast: crack, a mid blast of air, the punch, the held boom, debris, and the room (wide).
    "blast": R(2.5, [
        N(-7, 0.045, 0.0003, hp=2500, lp=14000),              # crack: the charge going off
        N(-9, 0.22, 0.0006, hp=350, lp=4000, pink=True),       # the blast of air out of the barrel
        W(-10, 170, 72, 0.22),                                 # the punch in the chest
        S("blast_boom", -2.0),                                 # the held pressure-wave body
        S("blast_roar", -1.0),                                 # its roar, held with it
        S("blast_debris", 3, 0.25),                            # grit and fragments
        S("blast_room_l", -12, -0.75),                         # the mall answering, left
        S("blast_room_r", -12, 0.75), 
        S("blast_hang_l", HANG, -0.6),                         # the hall's long hang, left
        S("blast_hang_r", HANG, 0.6),                          # and right                         # and right
        N(-8, 0.12, 0.0003, hp=8000),                          # air on the crack
    ], limit=-3.0),   # mastered 3 dB under full scale: headroom for the hit layers and bots on top (judge C2)
    "vent_hiss": R(0.65, [S("vent", -3, 0.35), N(-24, 0.3, 0.02, hp=6000, pan=-0.3)]),
    "dry_clunk": R(0.3, [S("dry_clunk", -4)]),
    "equip_rack": R(0.3, [S("equip_rack", -3, -0.15)]),
    "equip_lock": R(0.36, [S("equip_lock", -2), N(-20, 0.02, 0.0003, hp=5000)]),
    "cell_out": R(0.34, [S("cell_out", -2, 0.2)]),
    "cell_in": R(0.3, [S("cell_in", -1, 0.15)]),
    "seat": R(0.45, [S("seat", 0)]),
    "pump": R(0.32, [S("pump", 0, -0.1)]),
    "inspect_turn": R(0.55, [S("inspect_turn", -10, -0.2)]),
    "inspect_knock": R(0.3, [S("inspect_knock", -5, 0.1)]),
    "inspect_settle": R(0.22, [S("inspect_settle", -8)]),
    # Played by SkylineBreacherFx: once per shot that lands on a bot (at the hit), and on a boost (under your feet).
    # starts 40 ms in: the pellets' flight and the hit's sound coming back (about 30-40 ms at 6-15 m), which also lands
    # the crunch after the blast's crack has fallen, so it is heard by its band rather than by being loud
    "impact": R(0.44, [S("breacher_hit", IMPACT_DB, start=0.04)]),
    "boost": R(0.75, [S("boost_whump", -1), N(-18, 0.05, 0.0003, hp=3000, lp=9000)]),
}

# The generic impacts (Resources/SkylineAudio/impact/, played by SkylineShooting for any gun): tools/skyline/impact/.
IMPACT_RECIPES = {
    "hit": R(0.26, [S("impact_hit", -2)]),
    "world": R(0.28, [S("impact_world", -3)]),
}


def write_recipes() -> None:
    import json
    rec = HERE / "recipes"
    for name, spec in RECIPES.items():
        (rec / (name + ".json")).write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
    imp = HERE.parents[1] / "impact" / "recipes"
    imp.mkdir(parents=True, exist_ok=True)
    for name, spec in IMPACT_RECIPES.items():
        spec = json.loads(json.dumps(spec).replace("../samples/", "../../guns/breacher/samples/"))
        (imp / (name + ".json")).write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
    print(f"  {len(RECIPES)} recipes in {rec}, {len(IMPACT_RECIPES)} in {imp}")


def main() -> int:
    print("Breacher source layers:")
    write("blast_boom", blast_boom())
    write("blast_roar", blast_roar())
    write("blast_room_l", blast_room(21))
    write("blast_room_r", blast_room(23))
    write("blast_debris", blast_debris())
    write("blast_hang_l", blast_hang(41))
    write("blast_hang_r", blast_hang(43))
    write("vent", vent())
    write("cell_out", cell_out())
    write("cell_in", cell_in())
    write("seat", seat())
    write("pump", pump())
    write("equip_rack", equip_rack())
    write("equip_lock", equip_lock())
    write("dry_clunk", dry_clunk())
    write("inspect_turn", inspect_turn())
    write("inspect_knock", inspect_knock())
    write("inspect_settle", inspect_settle())
    write("breacher_hit", breacher_hit())
    write("boost_whump", boost_whump())
    write("impact_hit", impact_hit())
    write("impact_world", impact_world())
    write_recipes()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
