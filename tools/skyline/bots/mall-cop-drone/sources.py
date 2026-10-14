"""The mall-cop drone's source stems (Task 20): officious and electronic. Rotor whine, chirps, a citation-printer clack,
a taser-like zap. Writes tools/skyline/bots/mall-cop-drone/sources/*.wav; the recipes mix them.

    uv run --project tools/skyline/soundforge python tools/skyline/bots/mall-cop-drone/sources.py
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import botsynth as bs  # noqa: E402

OUT = HERE / "sources"


def rotor_buzz(f: np.ndarray, rng, n: int) -> np.ndarray:
    """One small rotor: a blade-pass buzz (a band-limited pulse) with the shaft's slight wobble."""
    x = bs.osc(f, kind="pulse", duty=0.3, harmonics=18)
    shaft = 1 + 0.12 * np.sin(bs.phase(f * 0.5))
    return x * shaft


def rotor_loop(rng) -> np.ndarray:
    """2 s, seamless, SOFT (the creator, 2026-10-14: "very grating noise and beeps ... it won't shut up"): two rotors
    beating gently against each other as a low hum (a filtered blade-pass buzz, nothing above ~650 Hz), a little air under
    it, no motor whine and no "recording" blip (that was a beep once a loop). Quiet by itself: peak -20 dB."""
    L = 2.0
    n = bs.n_of(L)
    t = np.arange(n) / bs.SR
    f1, f2 = bs.loop_freq(104.0, L), bs.loop_freq(106.5, L)
    a = rotor_buzz(np.full(n, f1), rng, n)
    b = rotor_buzz(np.full(n, f2), rng, n)
    buzz = bs.loop_filter(a + 0.85 * b, 70, 420)
    chop = (0.6 + 0.4 * np.abs(np.sin(np.pi * f1 * t))) ** 2
    air = bs.loop_noise(n, rng, 120, 650) * chop
    x = 0.8 * buzz / (np.abs(buzz).max() or 1) + 0.18 * air
    return bs.norm(bs.loop_filter(x, 60, 650), -20.0)


def yelp(rng) -> np.ndarray:
    """"Halt!": a clipped two-whoop siren yelp, a radio squelch, two confirming chirps."""
    L = 0.78
    x = bs.zeros(L)
    for at in (0.0, 0.13):
        m = bs.n_of(0.12)
        f = bs.curve([(0, 720), (0.1, 1450), (0.12, 1500)], m, exp=True)
        w = bs.lowpass(bs.osc(f, kind="square", harmonics=11), 5000) * bs.envelope([(0, 0), (0.002, 1), (0.1, 0.9), (0.12, 0)], m)
        bs.place(x, w, at, 0.55)
    m = bs.n_of(0.08)
    sq = bs.bandpass(bs.noise(m, rng), 1200, 3600) * bs.envelope([(0, 0), (0.003, 1), (0.06, 0.6), (0.08, 0)], m)
    bs.place(x, sq, 0.3, 0.5)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 3000, 0.003), 0.38, 0.4)
    for at, f in ((0.44, 2200.0), (0.52, 2930.0)):
        m = bs.n_of(0.05)
        c = bs.osc(np.full(m, f)) + 0.3 * bs.osc(np.full(m, f * 2))
        bs.place(x, c * bs.envelope([(0, 0), (0.002, 1), (0.04, 0.8), (0.05, 0)], m), at, 0.35)
    return bs.norm(bs.fade(x, 0.0003, 0.02), -3.0)


def charge(rng) -> np.ndarray:
    """The telegraph (0.35 s): a sharp target chirp, then a capacitor whine climbing to the shot with a fizz building
    under it."""
    L = 0.36
    n = bs.n_of(L)
    x = np.zeros(n)
    m = bs.n_of(0.04)
    chirp = bs.osc(bs.curve([(0, 1800), (0.04, 2700)], m, exp=True)) * bs.envelope([(0, 0), (0.001, 1), (0.03, 0.7), (0.04, 0)], m)
    bs.place(x, chirp, 0.0, 0.8)
    f = bs.curve([(0, 900), (L, 4200)], n, exp=True)
    trem = 1 + 0.35 * np.sin(bs.phase(bs.curve([(0, 18), (L, 60)], n, exp=True)))
    whine = (bs.osc(f) + 0.3 * bs.osc(f * 2) + 0.12 * bs.osc(f * 3)) * trem
    x += 0.5 * whine * bs.envelope([(0, 0), (0.03, 0.15), (L * 0.97, 1.0), (L, 0.2)], n)
    x += 0.3 * bs.crackle(L, rng, 900, density=bs.envelope([(0, 0), (L, 1)], n), decay=0.001, bright=4000)
    # the capacitor clicks off just before the zap, so the attack's onset stands clear (a 12 dB jump)
    x *= bs.envelope([(0, 1), (0.29, 1), (0.325, 0.03), (L, 0.0)], n)
    bs.place(x, bs.click(bs.n_of(0.01), rng, 3000, 0.002), 0.3, 0.25)
    return bs.norm(bs.fade(x, 0.0003, 0.005), -3.0)


def zap(rng) -> np.ndarray:
    """The attack: a taser zap (a snap, a crackling arc and a falling buzz) and the citation printer clacking out a
    ticket behind it, torn off."""
    L = 0.44
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 5000, 0.002), 0.0, 1.0)
    m = bs.n_of(0.13)
    arc_env = bs.envelope([(0, 1), (0.08, 0.7), (0.13, 0)], m)
    arc = bs.crackle(0.13, rng, 5000, density=arc_env, decay=0.0007, bright=1500)
    bs.place(x, arc, 0.0, 0.7)
    f = bs.curve([(0, 2600), (0.1, 420)], m, exp=True)
    buzz = bs.bandpass(bs.osc(f, kind="saw", harmonics=20), 350, 6000) * arc_env
    buzz *= 0.6 + 0.4 * np.sign(np.sin(bs.phase(np.full(m, 120.0))))
    bs.place(x, buzz, 0.0, 0.9)
    # the stun buzz: a square carrier chopped by the arc's pulses ("bzzt")
    carrier = bs.lowpass(bs.osc(bs.curve([(0, 760), (0.13, 610)], m), kind="square", harmonics=9), 3500)
    pulses = (0.5 + 0.5 * np.sign(np.sin(bs.phase(np.full(m, 55.0))))) * arc_env
    bs.place(x, carrier * pulses, 0.0, 0.5)
    for k in range(9):
        at = 0.155 + k * 0.017
        tick = bs.modal(bs.n_of(0.03), [2250, 4600, 7100], [0.012, 0.008, 0.005], [0.8, 0.5, 0.25], rng, 0.05)
        bs.place(x, tick + bs.click(bs.n_of(0.03), rng, 3000, 0.0015) * 0.5, at, 0.3)
    m = bs.n_of(0.16)
    motor = bs.lowpass(bs.osc(np.full(m, 115.0), kind="square", harmonics=9), 900) * bs.envelope([(0, 0), (0.01, 1), (0.15, 1), (0.16, 0)], m)
    bs.place(x, bs.highpass(motor, 300), 0.15, 0.015)
    m = bs.n_of(0.06)
    tear = bs.sweep_bandpass(bs.noise(m, rng), bs.curve([(0, 2000), (0.06, 6500)], m, exp=True), q=3) * bs.envelope([(0, 0), (0.005, 1), (0.05, 0.6), (0.06, 0)], m)
    bs.place(x, tear, 0.32, 0.6)
    return bs.norm(bs.fade(x, 0.0002, 0.02), -1.0)


def bzzt(rng) -> np.ndarray:
    """Hurt: a metallic tink, then an offended two-tone error buzz ("uh-uh")."""
    L = 0.32
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.modal(bs.n_of(0.12), [3100, 4720, 7900], [0.07, 0.05, 0.03], [0.7, 0.4, 0.2], rng, 0.03), 0.0, 0.7)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 4000, 0.002), 0.0, 0.6)
    for at, f in ((0.04, 620.0), (0.13, 430.0)):
        m = bs.n_of(0.075)
        tone = bs.osc(np.full(m, f), kind="square", harmonics=15)
        tone = np.round(tone * 6) / 6
        bs.place(x, bs.lowpass(tone, 6000) * bs.envelope([(0, 0), (0.002, 1), (0.065, 0.9), (0.075, 0)], m), at, 0.4)
    bs.place(x, bs.crackle(0.08, rng, 1500, decay=0.0008, bright=3000), 0.01, 0.4)
    return bs.norm(bs.fade(x, 0.0002, 0.02), -3.0)


def cap_off(rng) -> np.ndarray:
    """Plate shed: a hollow plastic clonk and a ring of gold trim as a piece of its uniform flies off, a falling
    "whoop" of protest, two bounces."""
    L = 0.78
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 3000, 0.003), 0.0, 0.8)
    bs.place(x, bs.modal(bs.n_of(0.25), [390, 960, 2120, 3400], [0.09, 0.06, 0.04, 0.025], [0.8, 0.55, 0.3, 0.15], rng, 0.02), 0.0, 0.9)
    bs.place(x, bs.modal(bs.n_of(0.5), [1930, 4810], [0.3, 0.18], [0.16, 0.07], rng), 0.0, 1.0)
    m = bs.n_of(0.13)
    wh = bs.lowpass(bs.osc(bs.curve([(0, 1250), (0.13, 480)], m, exp=True), kind="square", harmonics=9), 4000)
    bs.place(x, wh * bs.envelope([(0, 0), (0.003, 1), (0.11, 0.7), (0.13, 0)], m), 0.07, 0.3)
    for at, g in ((0.33, 0.5), (0.47, 0.3)):
        bs.place(x, bs.modal(bs.n_of(0.12), [420, 1010, 2250], [0.05, 0.035, 0.02], [0.8, 0.5, 0.3], rng, 0.08), at, g)
    return bs.norm(bs.fade(x, 0.0003, 0.05), -2.0)


def crash(rng) -> np.ndarray:
    """Wreck: a burst of sparks, the rotors winding down, three falling error beeps, the printer spewing paper, then it
    drops and clatters on the floor."""
    L = 2.0
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 4000, 0.003), 0.0, 1.0)
    bs.place(x, bs.crackle(0.35, rng, 3500, density=bs.envelope([(0, 1), (0.35, 0)], bs.n_of(0.35)), decay=0.0008, bright=2200), 0.0, 1.6)
    m = bs.n_of(0.3)
    bang = bs.lowpass(bs.noise(m, rng, "pink"), 3000) * bs.ad(m, 0.001, 0.18)
    bang += 0.5 * bs.bandpass(bs.osc(bs.curve([(0, 2800), (0.15, 300)], m, exp=True), kind="saw", harmonics=20), 500, 8000) * bs.ad(m, 0.001, 0.2)
    bs.place(x, bang, 0.0, 1.0)
    m = bs.n_of(1.35)
    f = bs.curve([(0, 180), (1.35, 24)], m, exp=True)
    rot = bs.lowpass(rotor_buzz(f, rng, m) + 0.8 * rotor_buzz(f * 1.04, rng, m), 2200) * bs.envelope([(0, 0.9), (0.9, 0.4), (1.35, 0)], m)
    bs.place(x, rot, 0.0, 0.4)
    for at, fr in ((0.15, 1000.0), (0.3, 800.0), (0.45, 600.0)):
        mm = bs.n_of(0.09)
        tone = bs.lowpass(bs.osc(np.full(mm, fr), kind="square", harmonics=11), 5000) * bs.envelope([(0, 0), (0.002, 1), (0.08, 0.9), (0.09, 0)], mm)
        bs.place(x, tone, at, 0.32)
    k = 0
    at = 0.55
    while at < 1.15:
        tick = bs.modal(bs.n_of(0.03), [2250, 4600], [0.012, 0.008], [0.8, 0.4], rng, 0.05)
        bs.place(x, tick, at, 0.22 * (1 - (at - 0.55) / 0.8))
        at += 0.016 + 0.0009 * k
        k += 1
    m = bs.n_of(0.4)
    thud = bs.osc(bs.curve([(0, 85), (0.2, 45)], m, exp=True)) * bs.ad(m, 0.002, 0.22)
    thud += 0.6 * bs.lowpass(bs.noise(m, rng, "pink"), 1500) * bs.ad(m, 0.001, 0.1)
    thud += 0.5 * bs.modal(m, [360, 900, 2050], [0.08, 0.05, 0.03], [0.8, 0.5, 0.3], rng, 0.03)
    bs.place(x, thud, 1.35, 0.55)
    bs.place(x, bs.modal(bs.n_of(0.12), [420, 1010, 2250], [0.04, 0.03, 0.02], [0.8, 0.5, 0.3], rng, 0.08), 1.56, 0.3)
    return bs.norm(bs.fade(x, 0.0002, 0.08), -1.5)


STEMS = {
    "dr_rotor_loop": rotor_loop,
    "dr_yelp": yelp,
    "dr_charge": charge,
    "dr_zap": zap,
    "dr_bzzt": bzzt,
    "dr_cap_off": cap_off,
    "dr_crash": crash,
}


def main(out: Path = OUT) -> int:
    for i, (name, fn) in enumerate(STEMS.items()):
        bs.write(out / (name + ".wav"), fn(np.random.default_rng(3000 + i)))
    print(f"wrote {len(STEMS)} stems into {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
