"""Writes the spotlight's sound recipes (tools/skyline/bots/spotlight/recipes/*.json) for the sound forge.

    uv run python tools/skyline/bots/spotlight/make_recipes.py

The spotlight is heavy-electric: a mains hum and a lamp ballast's buzz, relay clunks, a rising lock tone, a hard rifle
crack. The recipes are plain sound-forge JSON (sines, noise, envelopes, filters); this script only saves writing the
many small layers (harmonics, sputters, glass pings) by hand. Then render them (its sources.py stem first) with
`uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py --bot spotlight`, 16-bit PCM, into
Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/spotlight/<name>.wav.

Timing contract (Unity, SkylineBot.cs): `telegraph` plays when the bot enters its telegraph and the shot (`attack`) lands
telegraphSeconds (0.8 s) later; `lock_beep` plays at the lock (0.25 s before the shot). So `telegraph` starts with a hard
onset (a relay clunk), rises for 0.8 s and is cut at 0.8 s, under the shot's onset.
"""
from __future__ import annotations

import json
import random
from pathlib import Path

OUT = Path(__file__).resolve().parent / "recipes"
SR = 48000


def sine(freq, *, end=None, start=0.0, attack=0.002, decay=0.25, gain=0.0, pan=0.0, hp=None, lp=None):
    d = {"type": "sine", "freq": round(freq, 3), "start": round(start, 4), "attack": attack, "decay": decay, "gain_db": round(gain, 2)}
    if end is not None:
        d["freq_end"] = round(end, 3)
    if pan:
        d["pan"] = pan
    if hp:
        d["highpass"] = hp
    if lp:
        d["lowpass"] = lp
    return d


def noise(*, start=0.0, attack=0.002, decay=0.25, gain=0.0, hp=None, lp=None, pink=False, pan=0.0):
    d = {"type": "noise", "start": round(start, 4), "attack": attack, "decay": decay, "gain_db": round(gain, 2)}
    if pink:
        d["color"] = "pink"
    if hp:
        d["highpass"] = hp
    if lp:
        d["lowpass"] = lp
    if pan:
        d["pan"] = pan
    return d


def relay_clunk(at, gain=0.0):
    """A heavy contactor slamming shut: a low knock, the steel frame's ring and the contact's click."""
    return [
        sine(95, end=48, start=at, attack=0.0008, decay=0.09, gain=gain - 2),
        noise(start=at, attack=0.0005, decay=0.035, gain=gain - 6, lp=1400, pink=True),
        sine(1180, start=at, attack=0.0005, decay=0.05, gain=gain - 18),
        sine(1730, start=at, attack=0.0005, decay=0.04, gain=gain - 21),
        noise(start=at + 0.004, attack=0.0003, decay=0.012, gain=gain - 10, hp=3500),
    ]


def hum(gain, *, start=0.0, attack=0.0, decay=0.0, base=50.0, harmonics=8, buzz=True):
    """Mains hum: the 50 Hz family, the even (100 Hz) ones strongest, as a magnetic ballast buzzes."""
    layers = []
    for k in range(1, harmonics + 1):
        f = base * k
        g = gain - (20 if k == 1 else 0 if k == 2 else 4 + 2.2 * k)   # the 50 Hz itself low: magnetostriction hums at 100 Hz
        if k % 2 == 1 and k > 1:
            g -= 9
        layers.append(sine(f, start=start, attack=attack, decay=decay, gain=g))
    if buzz:  # the ballast's rasp: the high 100 Hz harmonics
        for f, g in ((1100, -30), (1300, -31), (1500, -33), (2300, -36)):
            layers.append(sine(f, start=start, attack=attack, decay=decay, gain=gain + g))
    return layers


def save(name, length, layers, seed=1, limit=-1.0):
    OUT.mkdir(parents=True, exist_ok=True)
    spec = {"sample_rate": SR, "length": length, "seed": seed, "layers": layers, "master": {"limit_db": limit}}
    (OUT / f"{name}.json").write_text(json.dumps(spec, indent=1) + "\n", encoding="utf-8")


def idle_loop():
    # 2.0 s, seamless: every sine completes whole cycles in 2 s (frequencies on a 0.5 Hz grid), no attack, no decay, no
    # filters (a filter's start-up transient would click at the loop point). The two 100 Hz partials half a hertz apart
    # make the hum swell and sag once a loop (a ballast breathing under load). Pink noise from the forge is made with an
    # FFT over the whole clip, so it is periodic too.
    layers = hum(-14.0, attack=0.0, decay=0.0)
    layers.append(sine(100.5, attack=0.0, decay=0.0, gain=-19))
    layers.append(sine(4000.0, attack=0.0, decay=0.0, gain=-50))   # the lamp's faint whistle
    layers.append(noise(attack=0.0, decay=0.0, gain=-44, pink=True))
    save("idle_loop", 2.0, layers)


def alert():
    # "Light on you": the contactor slams, the arc strikes (a bright fizz), the hum surges up.
    layers = relay_clunk(0.0, gain=0.0)
    layers += [noise(start=0.012, attack=0.004, decay=0.16, gain=-9, hp=2400, lp=9000),     # arc strike
               noise(start=0.012, attack=0.01, decay=0.3, gain=-17, hp=600, lp=2500)]
    layers += hum(-8.0, start=0.02, attack=0.06, decay=0.55)
    layers += [sine(200, end=420, start=0.03, attack=0.05, decay=0.5, gain=-16)]   # the surge
    save("alert", 0.65, layers, seed=3)


def telegraph():
    # 0.8 s: a contactor clunk (the hard onset), then the lock tone, a buzzing saw-like stack, climbing two octaves and
    # swelling while the beam follows (0.02-0.55 s); at the lock (0.55 s, where lock_beep plays) it stops climbing and
    # holds one hard pitch, fading, until the shot: the lock is heard as well as seen. Cut at 0.8 s, under the shot.
    L = 0.8
    lock = 0.55
    layers = relay_clunk(0.0, gain=-2.0)
    # the exponential sweep runs over the whole layer (0.78 s); this end puts it at 1000 Hz at its peak
    end = 260 * (1000 / 260) ** (0.78 / (lock - 0.04))
    follow = dict(start=0.02, attack=lock - 0.04, decay=0.02)   # peaks 20 ms before the lock, then a gap the lock_beep clicks into
    hold = dict(start=lock, attack=0.004, decay=0.5)   # fades ~30 dB by the shot: a breath of quiet before it
    for k, g in ((1, -10), (2, -15), (3, -19), (4, -24), (5, -27)):
        layers.append(sine(260 * k, end=end * k, gain=g, **follow))
        layers.append(sine(1000 * k, gain=g - 3, **hold))
    layers.append(sine(1006, gain=-16, **hold))   # a slight beat: the held tone throbs
    layers += hum(-14.0, start=0.0, attack=0.02, decay=1.2, buzz=False, harmonics=4)
    layers.append(noise(start=0.3, attack=0.46, decay=0.035, gain=-30, hp=3000, lp=8000))   # the arc sizzling hotter
    save("telegraph", L, layers, seed=5)


def lock_beep():
    # The lock: a hard square-ish double pip, 1.6 kHz with its odd harmonics, over a relay tick.
    layers = [noise(attack=0.0003, decay=0.01, gain=-10, hp=3000)]
    for at in (0.0, 0.065):
        for k, g in ((1, 0), (3, -10), (5, -16), (7, -21)):
            layers.append(sine(1600 * k, start=at, attack=0.001, decay=0.06, gain=g))
    save("lock_beep", 0.2, layers, seed=7)


RIFLE_SECONDS = 0.55


def rifle_layers():
    """The rifle shot's design, rendered into the stem sources/sl_rifle.wav by sources.py (which drives it into a soft
    clip so it sits as loud as the other bots' attacks): a hard supersonic crack, the report's body, a low thump, the
    electric discharge's falling zap. The crack's slapback off the atrium's glass and the discharge's crackle carry the
    brightness through the clip, so the rifle reads as a hard crack, not a thump."""
    rnd = random.Random(11)
    layers = [
        noise(attack=0.0003, decay=0.1, gain=0, hp=1800, lp=6000),
        noise(attack=0.0005, decay=0.22, gain=2, hp=600, lp=4500),
        sine(130, end=48, attack=0.0008, decay=0.16, gain=-6),
        sine(6200, end=2400, attack=0.0005, decay=0.16, gain=-14),
        sine(3700, end=1500, attack=0.0005, decay=0.2, gain=-17),
        noise(start=0.006, attack=0.003, decay=0.5, gain=-14, hp=900, lp=6000),
    ]
    for at, g in ((0.075, -3), (0.16, -8), (0.26, -13)):   # slapback
        layers.append(noise(start=at, attack=0.0008, decay=0.09, gain=g, hp=1500, lp=5000))
    for i in range(14):   # discharge crackle
        at = 0.02 + rnd.uniform(0, 0.32)
        layers.append(noise(start=at, attack=0.0003, decay=0.01, gain=-14 - rnd.uniform(0, 8), hp=2500, lp=8000))
    return layers


ATTACK_GAIN_DB = 3.8   # the muffled rifle plus its crackle, level-matched to sit where the old crack did (rms -21 dBFS)
HURT_GAIN_DB = -3.3    # the dying bulb, level-matched to the old tink (rms -27 dBFS)


def attack():
    # The creator's pick (2026-10-14, from audio/skyline-sfx-options/spotlight-attack, option c, "a bit more muffling and static,
    # like electricity crackling"): the rifle stem (sources.py) low-passed at 1.5 kHz as through a wall, a low body, a brief hiss of
    # static under 3.5 kHz and sparse crackle clicks. Almost nothing in the 2-5 kHz harsh band.
    rnd = random.Random(23)
    g = ATTACK_GAIN_DB
    layers = [
        {"type": "sample", "path": "../sources/sl_rifle.wav", "start": 0.0, "attack": 0.0, "decay": 0.0, "gain_db": g, "lowpass": 1500},
        sine(110, end=60, attack=0.002, decay=0.16, gain=g - 9),
        noise(start=0.0, attack=0.01, decay=0.22, gain=g - 17, hp=500, lp=3500),   # the static
    ]
    for i in range(12):   # the crackle: sparse random clicks, band-limited, panned a little
        at = 0.012 + rnd.uniform(0, 0.4) ** 1.3
        layers.append(noise(start=at, attack=0.0003, decay=rnd.uniform(0.004, 0.009), gain=g - 9 - rnd.uniform(0, 7), hp=700, lp=3800,
                            pan=round(rnd.uniform(-0.4, 0.4), 2)))
    save("attack", RIFLE_SECONDS, layers, seed=23, limit=-1.0)


def hurt():
    # The creator's pick (spotlight-hurt option b, the dying bulb): a falling filament whine, a soft crackle of band-limited noise
    # and a final hum-drop, like a lamp shorting out. Nothing above 2.4 kHz.
    g = HURT_GAIN_DB
    layers = [
        sine(1400, end=520, attack=0.004, decay=0.22, gain=-7 + g, lp=2400),
        noise(start=0.0, attack=0.01, decay=0.16, gain=-13 + g, hp=500, lp=2400),
        noise(start=0.14, attack=0.01, decay=0.12, gain=-18 + g, hp=400, lp=2000),
        sine(120, end=60, start=0.1, attack=0.01, decay=0.25, gain=-7 + g),
    ]
    save("hurt", 0.45, layers, seed=42)


def plate_shed():
    # A heavy gold housing panel tearing off: a thick metal clang (inharmonic plate modes), bolts pinging out, sparks.
    rnd = random.Random(17)
    layers = [
        sine(150, end=95, attack=0.001, decay=0.12, gain=-4),
        noise(attack=0.0005, decay=0.05, gain=-6, lp=3000, pink=True),
    ]
    for f, d, g in ((412, 0.55, -8), (1093, 0.45, -11), (1871, 0.35, -14), (2687, 0.28, -17), (3540, 0.2, -20)):
        layers.append(sine(f, attack=0.0008, decay=d, gain=g))
    for i in range(3):  # bolts
        at = 0.04 + 0.05 * i + rnd.uniform(0, 0.02)
        layers.append(sine(rnd.uniform(4800, 6400), start=at, attack=0.0005, decay=0.05, gain=-22))
    for i in range(7):  # sparks
        at = 0.02 + rnd.uniform(0, 0.3)
        layers.append(noise(start=at, attack=0.0005, decay=0.012, gain=-20 - rnd.uniform(0, 6), hp=4000))
    save("plate_shed", 0.75, layers, seed=17)


def wreck():
    # The lamp blows: a pop and a boom, the big lens shattering, an arc sizzle, and the hum diving to nothing.
    rnd = random.Random(19)
    layers = [
        noise(attack=0.0004, decay=0.09, gain=0, hp=900),
        noise(attack=0.001, decay=0.5, gain=-6, lp=1800, pink=True),
        sine(85, end=30, attack=0.001, decay=0.6, gain=-3),
        noise(start=0.02, attack=0.01, decay=0.7, gain=-20, hp=3500),   # arc sizzle
    ]
    for i in range(16):  # glass
        at = 0.03 + rnd.uniform(0, 0.55) ** 1.4
        layers.append(sine(rnd.uniform(3000, 9500), start=at, attack=0.0005, decay=rnd.uniform(0.04, 0.12),
                           gain=-15 - rnd.uniform(0, 10), pan=round(rnd.uniform(-0.4, 0.4), 2)))
    for k, g in ((1, -10), (2, -14), (3, -20)):   # power-down: the 100 Hz hum dives
        layers.append(sine(100 * k, end=22 * k, start=0.05, attack=0.01, decay=1.4, gain=g))
    layers += relay_clunk(1.25, gain=-12)   # the breaker drops out
    save("wreck", 1.8, layers, seed=19)


def main():
    for f in (idle_loop, alert, telegraph, lock_beep, attack, hurt, plate_shed, wreck):
        f()
    print("wrote", len(list(OUT.glob("*.json"))), "recipes into", OUT)


if __name__ == "__main__":
    main()
