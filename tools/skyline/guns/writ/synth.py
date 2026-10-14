"""The Writ's synthesized sound stems (Task 18): a courtroom grown into a hand cannon, voiced as "a well-oiled hydraulic
masterpiece" (spec 2.3). Every sound is made from numbers (struck-wood and brass modes, filtered noise, saturated sines,
a synthetic stone hall); nothing is recorded or copied, and the reference clips under out/skyline-refs/ are never read.
Writes short WAV stems into samples/ (the recipes' "sample" layers) and the recipes into recipes/:

    uv run --project tools/skyline/soundforge python tools/skyline/guns/writ/synth.py
    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py --gun writ

Deterministic (fixed seeds): re-running rewrites the same files. The palette (docs/SKYLINE-GUN-WRIT.md):
- the shot: a GAVEL STRIKE (a hardwood knock with a dry click) on a deep hand-cannon BODY (a saturated low boom and a
  thump), a HYDRAULIC CHUNK (a short pneumatic breath and a latch) inside the first 15 ms, and a STONE-HALL TAIL (the
  colonnade's slap-back, then a dark hall that settles into a hush);
- the handling: oiled pistons, brass latches and a stone drum (the magazine) that seats with a hydraulic "pssh-THUNK";
- the spot: a ceramic crack with an electric crackle and a tiny seal chime when it forms, a crunchy break-through with a
  spark sizzle when it is hit, and a seal-stamp THUNK with a brass bell for the shooter (the "gavel came down" moment);
- at rest: a hushed-court air (a quiet loop SkylineWritFx plays while the Writ is out).
"""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import soundfile
from scipy import signal

SR = 48000
HERE = Path(__file__).resolve().parent
SAMPLES = HERE / "samples"
RECIPES = HERE / "recipes"


# ---------------------------------------------------------------- building blocks

def n_of(s: float) -> int:
    return int(round(s * SR))


def t_of(n: int) -> np.ndarray:
    return np.arange(n) / SR


def rng(seed: int) -> np.random.Generator:
    return np.random.default_rng(seed)


def bp(x, lo=None, hi=None, order=2):
    if lo and hi:
        sos = signal.butter(order, [lo, hi], "bandpass", fs=SR, output="sos")
    elif lo:
        sos = signal.butter(order, lo, "highpass", fs=SR, output="sos")
    elif hi:
        sos = signal.butter(order, hi, "lowpass", fs=SR, output="sos")
    else:
        return x
    return signal.sosfilt(sos, x)


def decay_env(n, decay, attack=0.0005, start=0.0):
    """Linear attack, then -60 dB `decay` seconds later; zero before `start`."""
    t = t_of(n) - start
    g = np.where(t < 0, 0.0, np.minimum(1.0, np.maximum(t, 0) / max(attack, 1e-6)))
    return g * np.exp(-np.maximum(t - attack, 0) * 6.9078 / decay)


def modal(n, modes, start=0.0, seed=0, jitter=0.0):
    """A struck object: a sum of decaying sines (freq Hz, decay s to -60 dB, gain)."""
    t = t_of(n) - start
    out = np.zeros(n)
    r = rng(seed)
    for f, d, g in modes:
        f = f * (1 + r.uniform(-jitter, jitter))
        ph = r.uniform(0, 2 * np.pi)
        out += g * np.where(t < 0, 0.0, np.sin(2 * np.pi * f * np.maximum(t, 0) + ph) * np.exp(-np.maximum(t, 0) * 6.9078 / d))
    return out


def click(n, at, seed, lo=1500, hi=None, decay=0.0025, gain=1.0):
    x = rng(seed).standard_normal(n)
    return bp(x, lo, hi) * decay_env(n, decay, attack=0.0002, start=at) * gain


def noise_burst(n, at, seed, lo, hi, attack, decay, gain=1.0, pink=False):
    x = rng(seed).standard_normal(n)
    if pink:   # 1/f power (-3 dB an octave)
        spec = np.fft.rfft(x)
        f = np.arange(len(spec), dtype=np.float64)
        f[0] = 1.0
        x = np.fft.irfft(spec / np.sqrt(f), n)
        x /= np.std(x) + 1e-9
    return bp(x, lo, hi) * decay_env(n, decay, attack=attack, start=at) * gain


def glide(n, f0, f1, curve=5.0, start=0.0):
    t = t_of(n) - start
    f = f1 + (f0 - f1) * np.exp(-np.maximum(t, 0) * curve)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.where(t < 0, 0.0, np.sin(ph))


def sweep_noise(n, seed, f0, f1, q=3.0, start=0.0, length=None):
    """Noise through a band-pass whose centre slides from f0 to f1 (block-wise)."""
    x = rng(seed).standard_normal(n)
    out = np.zeros(n)
    block = 512
    L = length if length else n / SR
    zi = None
    for i in range(0, n, block):
        t = max(0.0, i / SR - start)
        fc = f0 * (f1 / f0) ** min(1.0, t / L)
        bw = fc / q
        sos = signal.butter(2, [max(20, fc - bw / 2), min(SR / 2 - 100, fc + bw / 2)], "bandpass", fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        y, zi = signal.sosfilt(sos, x[i:i + block], zi=zi)
        out[i:i + block] = y
    return out


def hall(n, seed, rt60, lp_start=2600.0, lp_end=420.0, predelay=0.012):
    """A dark synthetic stone hall's impulse response tail: decaying noise whose low-pass falls over time."""
    t = t_of(n)
    x = rng(seed).standard_normal(n)
    env = np.where(t < predelay, 0.0, np.exp(-(t - predelay) * 6.9078 / rt60)) * np.minimum(1.0, np.maximum(t - predelay, 0) / 0.02)
    hi = sweep_lp(x, lp_start, lp_end, n)
    return hi * env


def sweep_lp(x, f0, f1, n):
    out = np.zeros(n)
    block = 1024
    zi = None
    for i in range(0, n, block):
        fc = f0 * (f1 / f0) ** min(1.0, i / n)
        sos = signal.butter(2, fc, "lowpass", fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        y, zi = signal.sosfilt(sos, x[i:i + block], zi=zi)
        out[i:i + block] = y
    return out


def sat(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)


def norm(x, peak_db=-1.5):
    m = np.abs(x).max()
    return x * (10 ** (peak_db / 20) / m) if m > 0 else x


def fade(x, s=0.006):
    k = min(len(x), n_of(s))
    if k > 0:
        x = x.copy()
        x[-k:] *= np.linspace(1, 0, k)
    return x


def write(name, x, peak_db=-1.5):
    SAMPLES.mkdir(parents=True, exist_ok=True)
    x = fade(norm(np.asarray(x, dtype=np.float64), peak_db))
    soundfile.write(str(SAMPLES / f"{name}.wav"), x.astype(np.float32), SR, subtype="PCM_16")
    return x


# ---------------------------------------------------------------- the shot

BODY = {"boom": 0.6, "roar": 0.95, "thump": 0.9}   # the body's three parts (tuned against the reference's band shares)
HALL_WET = 0.06

def gavel_knock(n, seed=1, pitch=1.0, bright=1.0):
    """A hardwood gavel on a sound block: a dry click, then a few fast wooden modes."""
    modes = [(540 * pitch, 0.07, 1.0), (1230 * pitch, 0.045, 0.62), (2140 * pitch, 0.03, 0.42 * bright),
             (3350 * pitch, 0.02, 0.26 * bright), (5100 * pitch, 0.012, 0.14 * bright)]
    return modal(n, modes, seed=seed, jitter=0.01) + click(n, 0.0, seed + 1, lo=1800, decay=0.0022, gain=1.4 * bright)


def shot_gavel():
    n = n_of(0.18)
    return gavel_knock(n, seed=11)


def shot_body():
    boom_gain, roar_gain, thump_gain = BODY["boom"], BODY["roar"], BODY["thump"]
    n = n_of(0.9)
    boom = glide(n, 175, 68, curve=9.0) * decay_env(n, 0.32, attack=0.0015)
    boom = sat(boom, 3.5)
    thump = noise_burst(n, 0.0, 12, 70, 900, 0.001, 0.16, pink=True)
    roar = noise_burst(n, 0.0, 14, 280, 1600, 0.001, 0.32, pink=True)
    punch = noise_burst(n, 0.0, 13, 250, 1800, 0.0008, 0.07)
    return boom * boom_gain + thump * thump_gain + roar * roar_gain + punch * 0.3


def shot_crack():
    """The snap (fix round 1: the shot had no crack above 4 kHz): a sharp broadband report, the gavel's brass band ringing
    like a struck nail, and the slug's short whiz as it leaves, so the shot cracks over its body."""
    n = n_of(0.35)
    snap = noise_burst(n, 0.0, 15, 1500, 15000, 0.0004, 0.014, gain=0.6)
    snap += noise_burst(n, 0.0015, 16, 3000, 12000, 0.0005, 0.03, gain=0.35)
    nail = modal(n, [(3610, 0.11, 0.5), (5270, 0.08, 0.38), (7930, 0.05, 0.26), (11200, 0.03, 0.15)], start=0.0005, seed=17)
    whiz = sweep_noise(n, 18, 9500, 3800, q=3.0, length=0.04) * decay_env(n, 0.05, attack=0.002)
    return snap + nail * 0.45 + whiz * 0.3


def shot_hydraulic():
    n = n_of(0.12)
    breath = noise_burst(n, 0.004, 21, 700, 3200, 0.002, 0.035, gain=0.8)
    latch = modal(n, [(2800, 0.02, 0.5), (4150, 0.014, 0.35)], start=0.011, seed=22) + click(n, 0.011, 23, lo=2500, decay=0.0015)
    return breath + latch * 0.6


def shot_hall():
    n = n_of(2.3)
    excite = np.zeros(n)
    # the colonnade's slap-back: the knock and the body coming back off stone, darker each time
    src = shot_gavel()[: n_of(0.1)] * 0.5 + shot_body()[: n_of(0.1)] * 0.7
    for d, g in ((0.017, 0.55), (0.029, 0.42), (0.043, 0.33), (0.061, 0.24)):
        k = n_of(d)
        seg = bp(src, None, 2400)[: n - k]
        excite[k:k + len(seg)] += seg * g
    tail_l = hall(n, 31, rt60=1.5, lp_start=6000.0, lp_end=700.0) + late_hall(n, 33)
    tail_r = hall(n, 32, rt60=1.55, lp_start=6000.0, lp_end=700.0) + late_hall(n, 34)
    # the hall is excited by the shot's mid-band roar and knock (the stone answers in the mids, like a real room)
    feed = bp(shot_body()[: n_of(0.25)], 200, 2400)
    knock = bp(shot_gavel(), 200, 3000)[: len(feed)]
    feed[: len(knock)] += knock * 0.5
    wet_l = signal.fftconvolve(feed, tail_l)[:n] * HALL_WET + excite
    wet_r = signal.fftconvolve(feed, tail_r)[:n] * HALL_WET + np.roll(excite, n_of(0.004))
    return wet_l, wet_r


def late_hall(n, seed):
    """The stone room's late tail: a slower second slope that holds the hall at about -20 to -35 dB from 0.5 to 2 s."""
    t = t_of(n)
    x = rng(seed).standard_normal(n)
    env = np.exp(-np.maximum(t - 0.08, 0) * 6.9078 / 3.6) * np.clip((t - 0.06) / 0.25, 0, 1)
    return sweep_lp(x, 4200.0, 1100.0, n) * env * 0.11


ROOM_RT = 30.0


SHOT_LEN = 2.8   # the shot's length (round 2: 2.3 cut the room and the air; now every layer fades out before the end)


def tail_fade(n, seconds=0.5):
    """A smooth (raised-cosine) fade over the last `seconds` of n samples, so a long tail ends in silence, not a cut."""
    g = np.ones(n)
    k = min(n, n_of(seconds))
    g[n - k:] = 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, k))
    return g


def shot_room(seed):
    """The courtroom's stone room after the shot (fix round 1: the tail fell to -40 dB by 1 s; a hand cannon's room holds):
    a dark wash with a slow second slope, faded out before the clip ends."""
    n = n_of(SHOT_LEN)
    t = t_of(n)
    x = rng(seed).standard_normal(n)
    env = np.clip((t - 0.02) / 0.3, 0, 1) * np.exp(-np.maximum(t - 0.32, 0) * 6.9078 / ROOM_RT)
    env *= np.where(t > 1.0, np.exp(-(t - 1.0) * 6.9078 / 4.3), 1.0)        # the second slope: about -14 dB a second
    return bp(sweep_lp(x, 420.0, 170.0, n), 70, None) * env * tail_fade(n)


def shot_air(seed):
    """The room's bright early reflections (the crack coming back off stone and brass): 3.5-13 kHz, fading over about 0.5 s (-60 dB at 1.5 s)."""
    n = n_of(SHOT_LEN)
    t = t_of(n)
    x = rng(seed).standard_normal(n)
    early = np.exp(-np.maximum(t - 0.008, 0) * 6.9078 / 1.5) * np.clip((t - 0.004) / 0.02, 0, 1)
    return sweep_lp(bp(x, 3500, None), 13000.0, 5500.0, n) * early * tail_fade(n)


def shot_mech():
    """The gavel's hydraulic return (the fire clip's "mech" event, 99 ms after the shot), the well-oiled machine you can
    hear: a pneumatic "psshh" in 2-8 kHz (where the shot's body has nothing, so it reads over it), a piston's brass
    ka-chunk, and a latch tick as the gavel locks back up over the bench."""
    n = n_of(0.26)
    t = t_of(n)
    held = np.clip(t / 0.006, 0, 1) * np.where(t < 0.075, 1.0, np.exp(-(t - 0.075) * 6.9078 / 0.12))   # a held breath
    psh = sweep_noise(n, 41, 7200, 2400, q=1.4, length=0.11) * held
    psh += noise_burst(n, 0.004, 43, 2500, 9000, 0.004, 0.08, gain=0.35)
    piston = (modal(n, [(1820, 0.05, 0.6), (2930, 0.04, 0.5), (4350, 0.03, 0.35)], start=0.05, seed=44)
              + click(n, 0.05, 45, lo=1500, decay=0.002, gain=1.0)
              + glide(n, 260, 160, curve=30, start=0.05) * decay_env(n, 0.06, attack=0.001, start=0.05) * 0.35)
    tick = modal(n, [(4100, 0.02, 0.4)], start=0.085, seed=46) + click(n, 0.085, 47, lo=3000, decay=0.001, gain=0.5)
    return psh * 0.9 + piston * 0.8 + tick * 0.5


# ---------------------------------------------------------------- handling

HANDLING_LOW = 0.36  # how much hydraulic low end the drum and the slide carry (the references are brighter: see the sheet)

def dry_click():
    n = n_of(0.3)
    tock = modal(n, [(880, 0.06, 1.0), (2080, 0.035, 0.5), (3900, 0.015, 0.25)], seed=51) + click(n, 0.0, 52, lo=2000, decay=0.0015)
    latch = modal(n, [(3100, 0.02, 0.4)], start=0.03, seed=53) + click(n, 0.03, 54, lo=3000, decay=0.001, gain=0.5)
    return tock + latch * 0.5


def equip_unfold():
    n = n_of(0.45)
    glide_ = sweep_noise(n, 61, 380, 2600, q=2.0, length=0.28) * decay_env(n, 0.38, attack=0.18)
    servo = (np.sin(2 * np.pi * np.cumsum(180 + 140 * np.minimum(t_of(n) / 0.3, 1)) / SR)
             + 0.4 * np.sin(2 * np.pi * np.cumsum(2 * (180 + 140 * np.minimum(t_of(n) / 0.3, 1))) / SR))
    servo = servo * decay_env(n, 0.42, attack=0.15) * 0.18
    clicks = sum(modal(n, [(1600 + 400 * k, 0.03, 0.5), (3400 + 300 * k, 0.015, 0.3)], start=0.07 + 0.08 * k, seed=62 + k)
                 for k in range(3))
    return glide_ * 0.6 + servo + clicks * 0.5


def bell(n, f, decay, start=0.0, seed=0):
    """A small brass hand bell: inharmonic partials, a strike click."""
    modes = [(f, decay, 1.0), (f * 2.32, decay * 0.6, 0.5), (f * 4.25, decay * 0.35, 0.3), (f * 5.41, decay * 0.25, 0.18)]
    return modal(n, modes, start=start, seed=seed) + click(n, start, seed + 7, lo=3000, decay=0.001, gain=0.3)


def equip_lock():
    n = n_of(0.7)
    clunk = (glide(n, 150, 90, curve=30) * decay_env(n, 0.09, attack=0.001)
             + modal(n, [(620, 0.05, 0.6), (1450, 0.03, 0.4)], seed=71) + click(n, 0.0, 72, lo=1500, decay=0.002))
    ding = bell(n, 1850, 0.55, start=0.012, seed=73)
    return clunk + ding * 0.22


def mag_out():
    n = n_of(0.4)
    latch = modal(n, [(2400, 0.02, 0.7), (3700, 0.014, 0.4)], seed=81) + click(n, 0.0, 82, lo=2500, decay=0.0012)
    hiss = noise_burst(n, 0.006, 83, 2000, 7000, 0.004, 0.16, gain=0.4)
    r = rng(84)
    am = np.abs(bp(r.standard_normal(n), 20, 90)) * 6
    scrape = bp(r.standard_normal(n), 500, 2600) * am * decay_env(n, 0.2, attack=0.04, start=0.04) * 0.45
    return latch + hiss + scrape


def cell_in():
    n = n_of(0.35)
    r = rng(91)
    # the drum lands at the event's instant (a hard thunk), then slides the last millimetres home
    slide = bp(r.standard_normal(n), 500, 2400) * decay_env(n, 0.06, attack=0.012, start=0.012) * 0.4
    thunk = (glide(n, 190, 120, curve=25, start=0.001) * decay_env(n, 0.1, attack=0.001, start=0.001) * HANDLING_LOW
             + modal(n, [(720, 0.05, 0.5), (1620, 0.04, 0.6), (2900, 0.03, 0.55), (4300, 0.02, 0.35)], start=0.001, seed=92)
             + click(n, 0.0005, 93, lo=1500, decay=0.0025, gain=1.3))
    touch = click(n, 0.0, 94, lo=1500, decay=0.0015, gain=0.7) + modal(n, [(2600, 0.012, 0.35)], seed=95)   # the drum meets the column
    return slide + thunk + touch


def seat():
    """The hydraulic seat: a pressurised breath, then a heavy lock, then the cell lighting up with a low hum."""
    n = n_of(0.6)
    pssh = sweep_noise(n, 101, 7600, 2600, q=1.6, length=0.08) * decay_env(n, 0.11, attack=0.006)
    pssh += noise_burst(n, 0.03, 104, 3000, 10000, 0.01, 0.16, gain=0.3)          # the bleed after the lock
    lock = (glide(n, 140, 72, curve=18, start=0.022) * decay_env(n, 0.16, attack=0.001, start=0.022) * HANDLING_LOW * 1.3
            + modal(n, [(900, 0.06, 0.6), (2300, 0.09, 0.7), (3700, 0.07, 0.55), (5600, 0.04, 0.35)], start=0.022, seed=102)
            + click(n, 0.022, 103, lo=1800, decay=0.003, gain=1.5))
    t = t_of(n)
    hum = (np.sin(2 * np.pi * 120 * t) + 0.5 * np.sin(2 * np.pi * 240 * t)) * decay_env(n, 0.4, attack=0.05, start=0.06) * 0.12
    return pssh * 1.1 + lock + hum * 0.25


def bolt():
    """The chair (the slide) racks back and slams home, re-cocking the gavel: two strokes."""
    n = n_of(0.45)
    back = (click(n, 0.0, 111, lo=2200, decay=0.0015) + modal(n, [(1900, 0.025, 0.6), (3300, 0.015, 0.35)], seed=112)
            + noise_burst(n, 0.004, 113, 900, 4000, 0.004, 0.05, gain=0.3))
    home = (glide(n, 170, 95, curve=22, start=0.105) * decay_env(n, 0.14, attack=0.001, start=0.105) * 0.8
            + noise_burst(n, 0.105, 118, 150, 1500, 0.001, 0.09, gain=0.5, pink=True)
            + modal(n, [(780, 0.05, 0.6), (1700, 0.045, 0.6), (2950, 0.03, 0.45), (4400, 0.02, 0.3)], start=0.105, seed=114)
            + click(n, 0.105, 115, lo=1500, decay=0.003, gain=1.4))
    cock = modal(n, [(3400, 0.012, 0.35)], start=0.16, seed=116) + click(n, 0.16, 117, lo=3000, decay=0.001, gain=0.4)
    return back * 0.9 + home + cock * 0.6


def inspect_turn():
    n = n_of(0.5)
    swish = noise_burst(n, 0.0, 121, 300, 2200, 0.12, 0.35, gain=0.3, pink=True)
    shift = modal(n, [(1400, 0.03, 0.4), (2600, 0.02, 0.25)], start=0.02, seed=122) + click(n, 0.02, 123, lo=2000, decay=0.001, gain=0.3)
    return swish + shift * 0.6


def inspect_tap():
    n = n_of(0.45)
    tink = lambda at, s: (modal(n, [(3150, 0.09, 0.6), (4720, 0.06, 0.4), (7230, 0.03, 0.22)], start=at, seed=s)  # noqa: E731
                          + click(n, at, s + 1, lo=3000, decay=0.0008, gain=0.4))
    return tink(0.0, 131) + tink(0.12, 133) * 0.75 + modal(n, [(700, 0.04, 0.3)], start=0.0, seed=135)


def inspect_settle():
    n = n_of(0.4)
    clunk = glide(n, 165, 110, curve=20) * decay_env(n, 0.1, attack=0.002) + modal(n, [(640, 0.05, 0.5), (1500, 0.03, 0.3)], seed=141)
    rattle = sum(modal(n, [(2600 + 200 * k, 0.012, 0.25)], start=0.03 + 0.017 * k, seed=142 + k) for k in range(3))
    return clunk + rattle * 0.5


# ---------------------------------------------------------------- spots and hits

def crackle(n, seed, start, length, rate, lo=2000, hi=9000):
    """An electric crackle: random sharp impulses, thinning out."""
    r = rng(seed)
    out = np.zeros(n)
    t = start
    while t < start + length:
        g = np.exp(-(t - start) / (length * 0.45)) * r.uniform(0.3, 1.0)
        k = n_of(t)
        if k < n:
            m = min(n - k, n_of(0.003))
            out[k:k + m] += r.standard_normal(m) * np.exp(-np.arange(m) / (SR * 0.0006)) * g
        t += r.exponential(1.0 / rate)
    return bp(out, lo, hi)


def spot_form():
    """A ceramic crack with an electric crackle, and a tiny seal chime: a writ is issued."""
    n = n_of(0.7)
    crack = noise_burst(n, 0.0, 151, 1200, 11000, 0.0002, 0.012, gain=1.4)
    shards = modal(n, [(3300, 0.05, 0.5), (5100, 0.035, 0.4), (7600, 0.025, 0.3), (9400, 0.02, 0.2)], seed=152, jitter=0.02)
    subcracks = sum(noise_burst(n, at, 153 + i, 1800, 9000, 0.0002, 0.008, gain=0.6 - 0.12 * i)
                    for i, at in enumerate((0.016, 0.034, 0.052)))
    elec = crackle(n, 157, 0.01, 0.36, rate=70) * 0.55
    chime = bell(n, 2620, 0.42, start=0.03, seed=158) * 0.12
    body = glide(n, 260, 160, curve=30) * decay_env(n, 0.06, attack=0.001) * 0.4
    return crack + shards * 0.5 + subcracks + elec + chime + body


def spot_break():
    """The spot is shot: a crunchy break-through (a cluster of cracks), a spark sizzle, a low thump."""
    n = n_of(0.7)
    r = rng(161)
    crunch = np.zeros(n)
    for i in range(11):
        at = r.uniform(0.0, 0.07)
        crunch += noise_burst(n, at, 162 + i, 600, 7000, 0.0002, r.uniform(0.006, 0.02), gain=r.uniform(0.4, 1.0))
    sizzle = crackle(n, 180, 0.02, 0.32, rate=160, lo=3500, hi=12000) * 0.6
    sizzle += noise_burst(n, 0.015, 181, 4500, 12000, 0.004, 0.22, gain=0.18)
    thump = glide(n, 120, 70, curve=20) * decay_env(n, 0.16, attack=0.001) * 0.9
    return crunch + sizzle + thump


def impact():
    """An ordinary Writ hit on a bot: a dull dark thock (ordinary damage is dark), no ring."""
    n = n_of(0.22)
    thock = modal(n, [(330, 0.06, 1.0), (760, 0.04, 0.5), (1350, 0.02, 0.25)], seed=191) + click(n, 0.0, 192, lo=800, hi=4000, decay=0.002, gain=0.8)
    body = noise_burst(n, 0.0, 193, 120, 900, 0.0008, 0.06, gain=0.6, pink=True)
    return thock + body


def impact_weak():
    """A weak-point hit: the same thock with a crisp crack and a short brass ring on top."""
    n = n_of(0.35)
    base = np.r_[impact(), np.zeros(n - n_of(0.22))]
    crack = noise_burst(n, 0.0, 201, 2500, 10000, 0.0002, 0.006, gain=1.1)
    ring = modal(n, [(2240, 0.16, 0.35), (3390, 0.1, 0.2)], start=0.002, seed=202)
    return base * 0.85 + crack + ring


def spot_pop():
    """The shooter's confirmation for a spot hit: a seal-stamp THUNK on a desk and a brass bell (the gavel came down)."""
    n = n_of(1.3)
    stamp = (glide(n, 105, 62, curve=14) * decay_env(n, 0.22, attack=0.0015) * 1.1
             + modal(n, [(420, 0.08, 0.7), (980, 0.05, 0.35)], seed=211)
             + noise_burst(n, 0.0, 212, 150, 1500, 0.001, 0.05, gain=0.6, pink=True)
             + click(n, 0.0, 213, lo=1000, hi=6000, decay=0.002, gain=0.7))
    ring = bell(n, 1240, 0.95, start=0.006, seed=214) * 0.2
    return sat(stamp * 1.3, 1.6) + ring


def hush():
    """A hushed court at rest: a soft dark air with slow swells, a distant murmur, a gaslamp's flutter. 6 s, loopable."""
    L = 6.0
    n = n_of(L)
    t = t_of(n)
    r = rng(221)
    air = bp(r.standard_normal(n), 140, 1300) + 0.5 * bp(r.standard_normal(n), 60, 200)
    air /= np.abs(air).max()
    air *= 0.75 + 0.25 * np.sin(2 * np.pi * t / L * 2 + 0.7)
    murmur = bp(r.standard_normal(n), 280, 850) * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * t * 3.1) * np.sin(2 * np.pi * t * 0.47 + 1)))
    murmur *= 0.55 + 0.45 * np.sin(2 * np.pi * t / L + 2.0)
    flutter = bp(r.standard_normal(n), 1500, 5000) * (0.5 + 0.5 * np.sin(2 * np.pi * t * 11.0)) * 0.05
    x = air * 0.8 + murmur * 0.12 + flutter
    # loop: crossfade the last 0.5 s into the first
    k = n_of(0.5)
    w = np.linspace(0, 1, k)
    x[:k] = x[:k] * w + x[-k:] * (1 - w)
    return x[:-k]


# ---------------------------------------------------------------- recipes

def S(name, gain=0.0, pan=0.0, start=0.0, **kw):
    d = {"type": "sample", "path": f"../samples/{name}.wav", "start": start, "attack": 0.0, "decay": 30.0, "gain_db": gain, "pan": pan}
    d.update(kw)
    return d


def R(length, layers, limit=-1.6, seed=1):
    return {"sample_rate": SR, "length": length, "seed": seed, "layers": layers, "master": {"limit_db": limit}}


STEMS = {
    "shot_gavel": shot_gavel, "shot_body": shot_body, "shot_crack": shot_crack,
    "shot_room_l": lambda: shot_room(35), "shot_room_r": lambda: shot_room(36),
    "shot_air_l": lambda: shot_air(37), "shot_air_r": lambda: shot_air(38), "shot_hydraulic": shot_hydraulic, "shot_mech": shot_mech,
    "dry_click": dry_click, "equip_unfold": equip_unfold, "equip_lock": equip_lock, "mag_out": mag_out, "cell_in": cell_in,
    "seat": seat, "bolt": bolt, "inspect_turn": inspect_turn, "inspect_tap": inspect_tap, "inspect_settle": inspect_settle,
    "spot_form": spot_form, "spot_break": spot_break, "impact": impact, "impact_weak": impact_weak, "spot_pop": spot_pop,
    "hush": hush,
}

# the mix of the shot's layers (dB, relative to each stem's -1.5 dBFS peak): tuned against the measurements
SHOT_MIX = {"gavel": -8.0, "body": -2.0, "crack": 3.0, "hydraulic": -9.0, "hall": -3.0, "room": -7.0, "air": 1.0}


def recipes():
    m = SHOT_MIX
    one = lambda name, length, gain=0.0: R(length, [S(name, gain)])  # noqa: E731
    return {
        "shot": R(SHOT_LEN, [S("shot_gavel", m["gavel"]), S("shot_body", m["body"]), S("shot_crack", m["crack"]),
                        S("shot_hydraulic", m["hydraulic"], pan=0.1),
                        S("shot_room_l", m["room"], pan=-0.9), S("shot_room_r", m["room"], pan=0.9),
                        S("shot_air_l", m["air"], pan=-0.7), S("shot_air_r", m["air"], pan=0.7),
                        S("shot_hall_l", m["hall"], pan=-0.85), S("shot_hall_r", m["hall"], pan=0.85)]),
        "shot_mech": one("shot_mech", 0.26, 3.0),
        "dry_click": one("dry_click", 0.3, 0.5),
        "equip_unfold": one("equip_unfold", 0.45, -10.0),
        "equip_lock": one("equip_lock", 0.7, -1.5),
        "mag_out": one("mag_out", 0.4, 3.0),
        "cell_in": one("cell_in", 0.35, 3.0),
        "seat": one("seat", 0.6, 3.0),
        "bolt": one("bolt", 0.45, 3.0),
        "inspect_turn": one("inspect_turn", 0.5, -10.5),
        "inspect_tap": one("inspect_tap", 0.45, -2.0),
        "inspect_settle": one("inspect_settle", 0.4, -5.0),
        "spot_form": one("spot_form", 0.7, -3.0),
        "spot_break": one("spot_break", 0.7, -2.0),
        "impact": one("impact", 0.22, -5.0),
        "impact_weak": one("impact_weak", 0.35, -4.0),
        "spot_pop": one("spot_pop", 1.3, -3.0),
        "hush": one("hush", 5.5, -18.0),
    }


def main() -> int:
    for name, fn in STEMS.items():
        if name == "hush":
            write(name, fn(), peak_db=-3.0)
        else:
            write(name, fn())
    hl, hr = shot_hall()
    peak = max(np.abs(hl).max(), np.abs(hr).max())
    for side, x in (("l", hl), ("r", hr)):
        write(f"shot_hall_{side}", x / peak, peak_db=-1.5 + 20 * np.log10(np.abs(x).max() / peak))
    RECIPES.mkdir(parents=True, exist_ok=True)
    for name, spec in recipes().items():
        (RECIPES / f"{name}.json").write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
    print(f"WRIT_SYNTH {len(STEMS) + 2} stems into {SAMPLES}, {len(recipes())} recipes into {RECIPES}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
