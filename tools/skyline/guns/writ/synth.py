"""The Writ's synthesized sound stems (Task 18): a courtroom grown into a hand cannon, voiced as "a well-oiled hydraulic
masterpiece" (spec 2.3). Every sound is made from numbers (struck-wood and brass modes, filtered noise, saturated sines,
a synthetic stone hall); nothing is recorded or copied, and the reference clips under out/skyline-refs/ are never read.
Writes short WAV stems into samples/ (the recipes' "sample" layers) and the recipes into recipes/:

    uv run --project tools/skyline/soundforge python tools/skyline/guns/writ/synth.py
    uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py --gun writ

Deterministic (fixed seeds): re-running rewrites the same files. The palette (docs/SKYLINE-GUN-WRIT.md):
- the shot (the creator, 2026-10-14: "quite grating"; "a small thwip, like shooting a small quill"): a quill flicked
  through air, a short bright airy whoosh with a soft feather flutter, a tiny ink flick and a short dark air tail. The
  gavel keeps for the weak-spot hit (a hard dry wooden crack, impact_crack) and the reload's smack;
- the handling: oiled pistons, brass latches and a stone drum (the magazine) that the gavel smacks home and a brass click seats;
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


# ---------------------------------------------------------------- the shot: a thwip

# The creator (2026-10-14): the shot was "quite grating"; the gavel crack belongs to a weak-spot hit only, and an ordinary
# shot is "a small thwip, like shooting a small quill". So the shot is a quill flicked through air: a short bright airy
# whoosh (broad, 5-11 kHz, nothing narrow in 2-5 kHz), a soft paper/feather flutter, a tiny ink flick and a soft low-mid
# "thup" for the flick of the wrist, with a short dark air tail. The gavel is the weak-spot hit's (impact_crack) and the
# reload's (smack).

def thwip_air(seed):
    """The whoosh: broad noise sliding up from 5.5 to 11 kHz, a soft 5 ms attack, gone in about 90 ms (no hard onset)."""
    n = n_of(0.22)
    whoosh = bp(sweep_noise(n, seed, 5500, 11000, q=1.1, length=0.07), 4200, None) * decay_env(n, 0.09, attack=0.005)
    breath = noise_burst(n, 0.0, seed + 1, 800, 2800, 0.009, 0.03, gain=0.12)   # the "th": a soft breath, low and short
    return whoosh + breath


def thwip_body():
    """The wrist's flick: a soft low-mid "thup" (a sine gliding 520 to 300 Hz in 30 ms); nothing under 250 Hz."""
    n = n_of(0.08)
    return glide(n, 520, 300, curve=60) * decay_env(n, 0.03, attack=0.002) + noise_burst(n, 0.0, 301, 350, 1300, 0.002, 0.02, gain=0.25)


def thwip_flutter(seed):
    """The feather and paper: high noise chopped at about 95 Hz (a vane fluttering), 70 ms, just after the whoosh starts."""
    n = n_of(0.16)
    t = t_of(n)
    flutter = bp(rng(seed).standard_normal(n), 5000, 11000)
    am = (0.5 + 0.5 * np.sin(2 * np.pi * 95 * t + seed)) ** 2
    return flutter * am * decay_env(n, 0.07, attack=0.012, start=0.016)


def thwip_flick():
    """The ink: a tiny wet tick as the nib lets go and a droplet's "plip" (a short sine rising 1.2 to 2.1 kHz) just after."""
    n = n_of(0.1)
    tick = click(n, 0.012, 311, lo=2400, hi=7500, decay=0.0011, gain=0.5)
    t = t_of(n) - 0.03
    f = 2100 + (1200 - 2100) * np.exp(-np.maximum(t, 0) * 230)
    plip = np.where(t < 0, 0.0, np.sin(2 * np.pi * np.cumsum(f) / SR)) * decay_env(n, 0.022, attack=0.002, start=0.03)
    return tick + plip * 0.35


def thwip_room(seed):
    """A short dark air tail (about 0.2 s): the whoosh in the far corner of the stone hall."""
    n = n_of(0.35)
    return bp(sweep_lp(rng(seed).standard_normal(n), 5200.0, 1800.0, n), 900, None) * decay_env(n, 0.17, attack=0.03, start=0.008)


def gavel_knock(n, seed=1, pitch=1.0, bright=1.0):
    """A hardwood gavel on a sound block: a dry click, then a few fast wooden modes."""
    modes = [(540 * pitch, 0.07, 1.0), (1230 * pitch, 0.045, 0.62), (2140 * pitch, 0.03, 0.42 * bright),
             (3350 * pitch, 0.02, 0.26 * bright), (5100 * pitch, 0.012, 0.14 * bright)]
    return modal(n, modes, seed=seed, jitter=0.01) + click(n, 0.0, seed + 1, lo=1800, decay=0.0022, gain=1.4 * bright)


def wood_hit(n, pitch, bright, seed, body_hz, body_gain):
    """Hardwood on hardwood: the gavel's modes (pitch scales them, bright scales the upper ones), a dry click and a body thud."""
    knock = gavel_knock(n, seed=seed, pitch=pitch, bright=bright)
    thud = glide(n, body_hz * 1.7, body_hz, curve=45) * decay_env(n, 0.08, attack=0.001) * body_gain
    return knock + thud


def short_room(src, n, seed, ring):
    """A short stone-room ring: four early reflections (darker each time) and a dark wash of `ring` seconds."""
    out = np.zeros(n)
    for d, g in ((0.011, 0.5), (0.019, 0.38), (0.031, 0.28), (0.047, 0.2)):
        k = n_of(d)
        seg = bp(src, None, 3200)[: n - k]
        out[k:k + len(seg)] += seg * g
    wash = bp(sweep_lp(rng(seed).standard_normal(n), 2600.0, 500.0, n), 220, None) * decay_env(n, ring, attack=0.012, start=0.012) * 0.16
    return out + wash


def impact_crack():
    """The gavel crack, on a weak-spot hit only: a hard dry crack of hardwood (a bright pitch, a thud) with a short room ring."""
    n = n_of(0.45)
    hit = wood_hit(n, pitch=1.12, bright=0.55, seed=321, body_hz=125, body_gain=0.55)
    crack = noise_burst(n, 0.0, 322, 1600, 7500, 0.0003, 0.012, gain=1.4)
    dry = bp(hit + crack, None, 8500)
    return dry + short_room(dry[: n_of(0.12)], n, 323, ring=0.2) * 0.9


def smack():
    """The reload's gavel smack, wood on wood on the gun's roof: a heavier, lower hit than the crack (the gavel is big against
    the court's roof), a hard dry crack with a short room ring."""
    n = n_of(0.55)
    hit = wood_hit(n, pitch=0.82, bright=0.5, seed=331, body_hz=95, body_gain=0.8)
    crack = noise_burst(n, 0.0, 332, 1400, 6500, 0.0003, 0.014, gain=1.2)
    dry = bp(hit + crack, None, 8000)
    return dry + short_room(dry[: n_of(0.14)], n, 333, ring=0.26) * 1.0


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
    """The charge seats (the reload's last beat, a moment after the smack drove it home): a brass click, a small bright
    "tink" of the latch, a second tick as it locks, and a soft low "tk" under them. Light and short: the smack is the weight."""
    n = n_of(0.3)
    tink = (modal(n, [(2350, 0.05, 0.45), (3150, 0.045, 0.4), (3720, 0.045, 0.5), (4600, 0.035, 0.35), (5640, 0.03, 0.35), (7900, 0.015, 0.2)], start=0.0, seed=101, jitter=0.03)
            + click(n, 0.0, 102, lo=2500, decay=0.001, gain=0.9))
    lock = (modal(n, [(1480, 0.03, 0.5), (3050, 0.025, 0.4)], start=0.034, seed=103)
            + click(n, 0.034, 104, lo=2000, decay=0.0012, gain=0.7))
    low = glide(n, 330, 190, curve=40, start=0.001) * decay_env(n, 0.04, attack=0.001, start=0.001) * HANDLING_LOW
    breath = noise_burst(n, 0.004, 105, 3500, 9000, 0.004, 0.03, gain=0.12)
    return tink + lock * 0.8 + low * 0.7 + breath


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
    "thwip_air_l": lambda: thwip_air(301), "thwip_air_r": lambda: thwip_air(305), "thwip_body": thwip_body,
    "thwip_flutter_l": lambda: thwip_flutter(311), "thwip_flutter_r": lambda: thwip_flutter(315), "thwip_flick": thwip_flick,
    "thwip_room_l": lambda: thwip_room(321), "thwip_room_r": lambda: thwip_room(325),
    "dry_click": dry_click, "equip_unfold": equip_unfold, "equip_lock": equip_lock, "mag_out": mag_out, "cell_in": cell_in,
    "smack": smack, "seat": seat, "bolt": bolt, "inspect_turn": inspect_turn, "inspect_tap": inspect_tap, "inspect_settle": inspect_settle,
    "spot_form": spot_form, "spot_break": spot_break, "impact": impact, "impact_weak": impact_weak, "impact_crack": impact_crack,
    "spot_pop": spot_pop, "hush": hush,
}

# the mix of the thwip's layers (dB, relative to each stem's -1.5 dBFS peak): tuned against the measurements (a small, quiet
# shot: about 14 dB under the old one's peak, nothing under 250 Hz, no narrow peak in 2-5 kHz)
THWIP_MIX = {"air": -9.0, "body": -15.0, "flutter": -19.0, "flick": -17.0, "room": -27.0}


def recipes():
    m = THWIP_MIX
    one = lambda name, length, gain=0.0: R(length, [S(name, gain)])  # noqa: E731
    return {
        "shot": R(0.4, [S("thwip_air_l", m["air"], pan=-0.4), S("thwip_air_r", m["air"], pan=0.4), S("thwip_body", m["body"]),
                        S("thwip_flutter_l", m["flutter"], pan=-0.5), S("thwip_flutter_r", m["flutter"], pan=0.5),
                        S("thwip_flick", m["flick"], pan=0.15),
                        S("thwip_room_l", m["room"], pan=-0.7), S("thwip_room_r", m["room"], pan=0.7)]),
        "dry_click": one("dry_click", 0.3, 0.5),
        "equip_unfold": one("equip_unfold", 0.45, -10.0),
        "equip_lock": one("equip_lock", 0.7, -1.5),
        "mag_out": one("mag_out", 0.4, 3.0),
        "cell_in": one("cell_in", 0.35, 3.0),
        "smack": one("smack", 0.55, 3.0),
        "seat": one("seat", 0.3, 1.0),
        "bolt": one("bolt", 0.45, 3.0),
        "inspect_turn": one("inspect_turn", 0.5, -10.5),
        "inspect_tap": one("inspect_tap", 0.45, -2.0),
        "inspect_settle": one("inspect_settle", 0.4, -5.0),
        "spot_form": one("spot_form", 0.7, -3.0),
        "spot_break": one("spot_break", 0.7, -2.0),
        "impact": one("impact", 0.22, -5.0),
        "impact_weak": one("impact_weak", 0.35, -4.0),
        "impact_crack": one("impact_crack", 0.45, 0.0),
        "spot_pop": one("spot_pop", 1.3, -3.0),
        "hush": one("hush", 5.5, -18.0),
    }


def main() -> int:
    for name, fn in STEMS.items():
        write(name, fn(), peak_db=-3.0 if name == "hush" else -1.5)
    RECIPES.mkdir(parents=True, exist_ok=True)
    for name, spec in recipes().items():
        (RECIPES / f"{name}.json").write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
    print(f"WRIT_SYNTH {len(STEMS)} stems into {SAMPLES}, {len(recipes())} recipes into {RECIPES}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
