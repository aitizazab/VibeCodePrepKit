"""The floor-care unit's source stems (Task 20): domestic and wet. Brushes, squeaky wheels, a pump, a hiss of acid,
appliance chimes. Writes tools/skyline/bots/floor-care-unit/sources/*.wav; the recipes mix them.

    uv run --project tools/skyline/soundforge python tools/skyline/bots/floor-care-unit/sources.py
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import botsynth as bs  # noqa: E402

OUT = HERE / "sources"


def motor_loop(rng) -> np.ndarray:
    """3 s, seamless: a soft motor hum, the brushes swishing round, a slosh in the tank, one small wheel squeak."""
    L = 3.0
    n = bs.n_of(L)
    t = np.arange(n) / bs.SR
    f0 = bs.loop_freq(118.0, L)
    hum = bs.osc(np.full(n, f0), kind="saw", harmonics=12)
    hum = bs.loop_filter(hum, 60, 700) * (1 + 0.08 * np.sin(2 * np.pi * bs.loop_freq(0.67, L) * t))
    rot = bs.loop_freq(5.0, L)
    swish = bs.loop_noise(n, rng, 900, 5200) * (0.25 + 0.75 * (0.5 + 0.5 * np.sin(2 * np.pi * rot * t)) ** 3)
    ticks = bs.loop_noise(n, rng, 2500, 9000) * (0.5 + 0.5 * np.sin(2 * np.pi * rot * 3 * t)) ** 12
    whine = np.sin(2 * np.pi * bs.loop_freq(1180.0, L) * t) * 0.5 + np.sin(2 * np.pi * bs.loop_freq(2360.0, L) * t) * 0.2
    x = 0.55 * hum + 0.32 * swish + 0.12 * ticks + 0.035 * whine
    slosh = np.zeros(n)
    for at in (0.6, 0.66, 1.9):
        bs.place(slosh, bs.bubble(bs.n_of(0.1), rng.uniform(260, 420), 14, 0.06), at, 0.25)
    x += slosh
    sq = bs.fade(bs.squeak(0.16, 1350, 1250, rng, grit=0.15), 0.02, 0.06)
    bs.place(x, sq, 2.3, 0.07)
    return bs.norm(x, -12.0)


def alert(rng) -> np.ndarray:
    """"Oh! A stain!": a bright rising appliance chime (bi-BEEP), the wheels skidding to a stop, the tank sloshing."""
    x = bs.zeros(0.95)
    bs.place(x, bs.chime(0.5, 1318.5, 0.35), 0.0, 0.7)          # E6
    bs.place(x, bs.chime(0.7, 1975.5, 0.5), 0.09, 0.9)          # B6
    bs.place(x, bs.chime(0.7, 2637.0, 0.4, 0.1), 0.09, 0.25)    # E7 on top, a sparkle
    skid = bs.squeak(0.22, 1500, 1100, rng, wobble=0.07, rate=34, grit=0.4) * bs.envelope([(0, 0), (0.02, 1), (0.14, 0.8), (0.22, 0)], bs.n_of(0.22))
    bs.place(x, skid, 0.2, 0.32)
    for at, f in ((0.36, 300), (0.41, 380), (0.47, 330)):
        bs.place(x, bs.bubble(bs.n_of(0.1), f, 16, 0.05), at, 0.35)
    return bs.norm(x, -3.0)


def spinup(rng) -> np.ndarray:
    """The telegraph (0.6 s): a relay click and the wheels squeaking as it squares up, then the brushes spinning up to a
    whirr while the pump primes with two wet glugs and the motor whines upward."""
    L = 0.62
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 3000, 0.006), 0.0, 0.9)
    bs.place(x, bs.modal(bs.n_of(0.08), [720, 1650], [0.04, 0.02], [0.6, 0.3], rng), 0.0, 0.5)
    sq = bs.squeak(0.14, 1250, 1500, rng, wobble=0.05, grit=0.3) * bs.envelope([(0, 0), (0.01, 1), (0.1, 0.7), (0.14, 0)], bs.n_of(0.14))
    bs.place(x, sq, 0.01, 0.35)
    rate = bs.curve([(0, 6), (L, 46)], n, exp=True)
    ph = bs.phase(rate)
    whirr = bs.bandpass(bs.noise(n, rng), 1100, 6500) * (0.3 + 0.7 * (0.5 + 0.5 * np.sin(ph)) ** 3)
    x += whirr * bs.envelope([(0, 0), (0.05, 0.2), (L, 1.0)], n) * 0.9
    f = bs.curve([(0, 150), (L, 520)], n, exp=True)
    whine = bs.lowpass(bs.osc(f, kind="saw", harmonics=14), 2800) * bs.envelope([(0, 0), (0.08, 0.25), (L, 0.9)], n)
    x += whine * 0.45
    for at, f0 in ((0.17, 210), (0.36, 260)):
        bs.place(x, bs.bubble(bs.n_of(0.12), f0, 22, 0.08), at, 0.6)
        bs.place(x, bs.lowpass(bs.noise(bs.n_of(0.06), rng, "pink"), 700) * bs.ad(bs.n_of(0.06), 0.004, 0.05), at, 0.4)
    # it draws breath before it spits: the whirr cuts out in the last tenth of a second, so the attack's onset stands
    # clear of the telegraph (the onset check needs a 12 dB jump)
    x *= bs.envelope([(0, 1), (0.47, 1), (0.56, 0.04), (L, 0.0)], n)
    return bs.norm(bs.fade(x, 0.0005, 0.01), -3.0)


def pump_spit(rng) -> np.ndarray:
    """The attack: the pump thumps and the nozzle spits a wet spray (a gurgling splatter of bubbles)."""
    L = 0.5
    n = bs.n_of(L)
    x = np.zeros(n)
    # weighted low (a wet pump "glorp"): the bright part of the floor-care unit's attack is the acid hiss that plays with
    # it, and the low weight keeps the spit apart from the gondola's squeegee slap and the drone's zap by ear
    thump = bs.osc(bs.curve([(0, 120), (0.14, 48)], n, exp=True)) * bs.ad(n, 0.002, 0.24)
    x += 1.2 * thump
    chug = bs.lowpass(bs.noise(n, rng, "pink"), 420) * bs.ad(n, 0.001, 0.12)
    x += 0.9 * chug
    spray = bs.bandpass(bs.noise(n, rng), 700, 2800) * bs.ad(n, 0.012, 0.3)
    gurgle = bs.lowpass(np.abs(bs.noise(n, rng)), 35)
    gurgle = gurgle / (gurgle.max() or 1)
    x += 0.45 * spray * (0.4 + 0.6 * gurgle)
    dens = bs.envelope([(0, 0), (0.02, 1), (0.25, 0.6), (0.45, 0.05)], n)
    x += 0.6 * bs.bubbles(L, rng, 150, 280, 1100, rise=(12, 40), decay=(0.015, 0.05), density=dens)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 2000, 0.003), 0.0, 0.5)
    return bs.norm(bs.fade(x, 0.0003, 0.03), -1.0)


def acid_hiss(rng) -> np.ndarray:
    """The sweep (0.8 s, plays with the attack): acid hissing out across the floor, fizzing and spitting."""
    L = 0.95
    n = bs.n_of(L)
    hiss = bs.lowpass(bs.highpass(bs.noise(n, rng), 2800), 10500)
    fizz = bs.lowpass(bs.noise(n, rng), 28)
    fizz = 0.55 + 0.45 * fizz / (np.abs(fizz).max() or 1)
    shape = bs.envelope([(0, 0), (0.04, 1), (0.6, 0.85), (0.8, 0.6), (L, 0)], n)
    x = hiss * fizz * shape * 0.8
    x += 0.6 * bs.crackle(L, rng, 1100, density=shape, decay=0.0012, bright=2500)
    x += 0.25 * bs.bubbles(L, rng, 70, 1500, 4200, rise=(20, 60), decay=(0.006, 0.02), density=shape)
    x += 0.2 * bs.bandpass(bs.noise(n, rng), 500, 1400) * shape * fizz
    return bs.norm(bs.fade(x, 0.002, 0.04), -4.0)


def eep(rng) -> np.ndarray:
    """Hurt: a plastic knock and an indignant squeaky-toy "eep!"."""
    L = 0.36
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.modal(bs.n_of(0.15), [430, 1130, 2380], [0.06, 0.04, 0.025], [0.7, 0.45, 0.25], rng, 0.03), 0.0, 0.8)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 3500, 0.003), 0.0, 0.5)
    m = bs.n_of(0.24)
    f = bs.curve([(0, 1150), (0.07, 1800), (0.16, 1500), (0.24, 1300)], m)
    sq = bs.osc(f * (1 + 0.035 * np.sin(bs.phase(np.full(m, 31.0)))), kind="pulse", duty=0.22, harmonics=12)
    sq = 0.5 * sq + bs.peak(sq, 2400, 3) * 2.5
    sq *= bs.envelope([(0, 0), (0.015, 1), (0.15, 0.8), (0.24, 0)], m)
    bs.place(x, sq, 0.03, 0.55)
    return bs.norm(bs.fade(x, 0.0003, 0.02), -3.0)


def clatter(rng) -> np.ndarray:
    """Plate shed: a plastic panel popping off with a ring of its gold trim, bouncing and skittering on the floor."""
    L = 0.85
    n = bs.n_of(L)
    x = np.zeros(n)
    bs.place(x, bs.click(bs.n_of(0.02), rng, 2500, 0.004), 0.0, 0.8)
    bs.place(x, bs.modal(bs.n_of(0.3), [560, 1390, 2900, 4100], [0.14, 0.09, 0.05, 0.03], [0.8, 0.5, 0.3, 0.15], rng, 0.02), 0.0, 0.9)
    bs.place(x, bs.bandpass(bs.noise(bs.n_of(0.1), rng, "pink"), 300, 3000) * bs.ad(bs.n_of(0.1), 0.001, 0.06), 0.0, 0.8)
    bs.place(x, bs.modal(bs.n_of(0.5), [2150, 5300], [0.35, 0.2], [0.18, 0.08], rng), 0.0, 1.0)
    for at, g in ((0.24, 0.6), (0.39, 0.42), (0.5, 0.3), (0.57, 0.2)):
        bs.place(x, bs.modal(bs.n_of(0.12), [610, 1480, 3100], [0.04, 0.03, 0.02], [0.8, 0.5, 0.3], rng, 0.08), at, g)
        bs.place(x, bs.click(bs.n_of(0.015), rng, 2500, 0.002), at, g * 0.6)
    sk = bs.crackle(0.25, rng, 260, decay=0.002, bright=1200)
    bs.place(x, bs.lowpass(sk, 6000), 0.6, 0.5)
    # squash the first crack's peak so the bounces and the rattle carry (it measured -29 LUFS before, ten below the rest)
    x = bs.soft_clip(bs.norm(x, 0.0), 3.0)
    return bs.norm(bs.fade(x, 0.0003, 0.05), -2.0)


def powerdown(rng) -> np.ndarray:
    """Wreck: the acid tank bursts (a wet pop and a gush), the motor winds down, a sad falling chime, a few sparks."""
    L = 1.9
    n = bs.n_of(L)
    x = np.zeros(n)
    pop = bs.lowpass(bs.noise(n, rng, "pink"), 2600) * bs.ad(n, 0.001, 0.22)
    thump = bs.osc(bs.curve([(0, 95), (0.3, 38)], n, exp=True)) * bs.ad(n, 0.002, 0.35)
    x += 0.8 * pop + 0.9 * thump
    gush_shape = bs.envelope([(0, 0), (0.03, 1), (0.5, 0.5), (1.3, 0.0)], n)
    x += 0.35 * bs.lowpass(bs.highpass(bs.noise(n, rng), 2600), 9000) * gush_shape
    x += 0.5 * bs.bubbles(L, rng, 110, 250, 1200, rise=(10, 30), decay=(0.02, 0.06), density=gush_shape)
    f = bs.curve([(0, 300), (1.3, 38)], n, exp=True)
    wind = bs.lowpass(bs.osc(f, kind="saw", harmonics=14), 1500) * bs.envelope([(0, 0), (0.05, 0.5), (1.0, 0.25), (1.35, 0)], n)
    x += 0.5 * wind
    for at, fr, bend in ((0.3, 783.99, 1.0), (0.52, 659.25, 1.0), (0.74, 523.25, 0.94)):
        m = bs.n_of(0.7)
        if bend != 1.0:
            ff = bs.curve([(0, fr), (0.2, fr), (0.7, fr * bend)], m, exp=True)
            c = np.sin(bs.phase(ff)) * bs.ad(m, 0.003, 0.6) + 0.2 * np.sin(bs.phase(ff * 3.93)) * bs.ad(m, 0.003, 0.15)
        else:
            c = bs.chime(0.7, fr, 0.5)
        bs.place(x, c, at, 0.45)
    sp = bs.crackle(0.7, rng, 160, density=bs.envelope([(0, 1), (0.7, 0.1)], bs.n_of(0.7)), decay=0.002, bright=2500)
    bs.place(x, sp, 0.95, 0.7)
    return bs.norm(bs.fade(x, 0.0003, 0.08), -1.5)


STEMS = {
    "fc_motor_loop": motor_loop,
    "fc_alert_chime": alert,
    "fc_spinup": spinup,
    "fc_pump_spit": pump_spit,
    "fc_acid_hiss": acid_hiss,
    "fc_eep": eep,
    "fc_clatter": clatter,
    "fc_powerdown": powerdown,
}


def main(out: Path = OUT) -> int:
    for i, (name, fn) in enumerate(STEMS.items()):
        bs.write(out / (name + ".wav"), fn(np.random.default_rng(2000 + i)))
    print(f"wrote {len(STEMS)} stems into {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
