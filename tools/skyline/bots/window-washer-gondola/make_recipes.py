"""Writes the window-washer gondola's sound recipes (tools/skyline/bots/window-washer-gondola/recipes/*.json).

    uv run python tools/skyline/bots/window-washer-gondola/make_recipes.py

The gondola is winch-and-cable: a geared winch motor, rope creak, cable twangs, a rubber squeegee swipe, mine beeps
and a sharp blast. The recipes are plain sound-forge JSON; this script only saves writing the many small layers
(creak pulses, ratchet teeth, beeps) by hand. Then render them (its sources.py stem first) with
`uv run --project tools/skyline/soundforge python tools/skyline/guns/render_all.py --bot window-washer-gondola`, 16-bit PCM, into
Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio/window-washer-gondola/<name>.wav.

Timing contract (Unity, SkylineBot.cs): `telegraph` plays twice over. (1) When the brain enters its telegraph, and the
squeegee swipe (`attack`) follows telegraphSeconds (0.4 s) later. (2) When a wall mine is triggered (fusing), and
`mine_blast` follows mineFuseSeconds (0.35 s) later. So `telegraph` has a hard onset at 0 and is over by 0.33 s,
before either follow-up's onset.
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


def creak(at, *, n=12, period=0.028, slow=1.25, pitch=620.0, gain=-14.0, rnd=None):
    """A rope creaking: stick-slip, a train of short resonant knocks whose spacing stretches as the rope settles."""
    rnd = rnd or random.Random(1)
    layers, t = [], at
    for i in range(n):
        g = gain - 6.0 * abs(i - n * 0.4) / n - rnd.uniform(0, 3)
        f = pitch * rnd.uniform(0.92, 1.08)
        layers.append(sine(f, start=t, attack=0.0008, decay=0.03, gain=g))
        layers.append(sine(f * 2.37, start=t, attack=0.0005, decay=0.015, gain=g - 9))
        layers.append(noise(start=t, attack=0.0004, decay=0.008, gain=g - 8, hp=900, lp=3500))
        t += period * (slow ** (i / n)) * rnd.uniform(0.85, 1.15)
    return layers


def ratchet(at, teeth=3, gap=0.045, gain=-6.0):
    """The winch's pawl clicking over its ratchet teeth."""
    layers = []
    for i in range(teeth):
        t = at + i * gap
        layers += [noise(start=t, attack=0.0003, decay=0.012, gain=gain, hp=1500, lp=6000),
                   sine(2350, start=t, attack=0.0004, decay=0.03, gain=gain - 10),
                   sine(3610, start=t, attack=0.0004, decay=0.02, gain=gain - 14)]
    return layers


def save(name, length, layers, seed=1, limit=-1.0):
    OUT.mkdir(parents=True, exist_ok=True)
    spec = {"sample_rate": SR, "length": length, "seed": seed, "layers": layers, "master": {"limit_db": limit}}
    (OUT / f"{name}.json").write_text(json.dumps(spec, indent=1) + "\n", encoding="utf-8")


def idle_loop():
    # 2.0 s, seamless: the winch's gear motor (a 60 Hz-family drone with a gear whine, beating at 3 Hz as the teeth mesh)
    # runs without attack, decay or filters, on a 0.5 Hz grid so it loops; two rope creaks sit inside the loop and die
    # out well before its end.
    layers = []
    for f, g in ((60.0, -30), (120.0, -12), (180.0, -28), (240.0, -22), (360.0, -25), (363.0, -27), (720.0, -33), (1083.0, -38)):
        layers.append(sine(f, attack=0.0, decay=0.0, gain=g))
    layers.append(noise(attack=0.0, decay=0.0, gain=-46, pink=True))
    rnd = random.Random(23)
    layers += creak(0.35, n=10, pitch=560, gain=-27, rnd=rnd)
    layers += creak(1.22, n=8, pitch=690, gain=-30, period=0.032, rnd=rnd)
    save("idle_loop", 2.0, layers, seed=23)


def alert():
    # "Oi!": the brake lets go (three ratchet clicks), the cable twangs as the platform jolts, and a workman's two-note
    # whistle (up, then down).
    layers = ratchet(0.0, teeth=3, gap=0.04, gain=-4)
    layers += [sine(196, start=0.13, attack=0.002, decay=0.5, gain=-10),     # cable twang
               sine(392.6, start=0.13, attack=0.002, decay=0.35, gain=-16),
               sine(589.4, start=0.13, attack=0.002, decay=0.25, gain=-21)]
    layers += [sine(1500, end=2150, start=0.16, attack=0.02, decay=0.16, gain=-11),   # whistle up
               sine(2000, end=1350, start=0.33, attack=0.02, decay=0.22, gain=-12),   # and down
               noise(start=0.16, attack=0.02, decay=0.35, gain=-34, hp=2000, lp=5000)]
    save("alert", 0.7, layers, seed=29)


def telegraph():
    # A platform warning beeper speeding up: five square-ish pips at 1.25 kHz (odd harmonics), each a hair higher, the
    # first with a relay click so the onset is hard. Over by 0.33 s.
    layers = [noise(attack=0.0003, decay=0.01, gain=-12, hp=2500)]
    for i, at in enumerate((0.0, 0.1, 0.175, 0.23, 0.27)):
        f = 1250 * (1 + 0.025 * i)
        for k, g in ((1, -3), (3, -13), (5, -19)):
            layers.append(sine(f * k, start=at, attack=0.0008, decay=0.045, gain=g))
    save("telegraph", 0.33, layers, seed=31)


def attack():
    # The squeegee swipe: the arm whooshes down, the rubber blade squeals across the glass (a bending tone with its
    # harmonics, wobbling as it chatters), and slaps off the end.
    rnd = random.Random(37)
    layers = [noise(attack=0.07, decay=0.25, gain=-14, hp=700, lp=3200)]       # whoosh
    for k, g in ((1, -6), (2, -12), (3, -17)):
        layers.append(sine(880 * k, end=1350 * k, start=0.0, attack=0.006, decay=0.5, gain=g))
        layers.append(sine(893 * k, end=1371 * k, start=0.0, attack=0.006, decay=0.5, gain=g - 3))   # chatter
    for i in range(9):   # the blade skipping
        at = 0.012 + i * 0.024 + rnd.uniform(-0.003, 0.003)
        layers.append(noise(start=at, attack=0.0005, decay=0.006, gain=-16, hp=1200, lp=4000))
    layers += [noise(start=0.24, attack=0.0008, decay=0.05, gain=-11, hp=300, lp=2500, pink=True),   # slap
               sine(210, end=120, start=0.24, attack=0.001, decay=0.06, gain=-18)]
    layers.append(noise(attack=0.0005, decay=0.02, gain=0, hp=1000, lp=5000))            # the arm's start click
    save("attack", 0.5, layers, seed=37)


def hurt():
    # Hit: the tin bucket's hollow bonk and a cable twanging under the jolt.
    layers = [
        noise(attack=0.0005, decay=0.03, gain=-6, lp=2500, pink=True),
        sine(312, attack=0.0008, decay=0.28, gain=-6),
        sine(741, attack=0.0008, decay=0.22, gain=-11),
        sine(1287, attack=0.0008, decay=0.15, gain=-15),
        sine(2034, attack=0.0008, decay=0.1, gain=-19),
        sine(174, start=0.02, attack=0.002, decay=0.4, gain=-12),
        sine(348.8, start=0.02, attack=0.002, decay=0.3, gain=-17),
        sine(523.9, start=0.02, attack=0.002, decay=0.2, gain=-22),
    ]
    save("hurt", 0.45, layers, seed=41)


def plate_shed():
    # A bucket knocked off its hook: a tin-pail clank, its handle rattling, then a second, smaller clank as it tumbles.
    rnd = random.Random(43)
    layers = [noise(attack=0.0005, decay=0.04, gain=-6, lp=3000, pink=True)]
    for f, d, g in ((523, 0.4, -6), (1213, 0.3, -10), (2031, 0.22, -14), (3170, 0.15, -18)):
        layers.append(sine(f, attack=0.0008, decay=d, gain=g))
        layers.append(sine(f * 1.07, start=0.33, attack=0.0008, decay=d * 0.6, gain=g - 9))   # the tumble
    layers.append(noise(start=0.33, attack=0.0005, decay=0.03, gain=-15, lp=3000, pink=True))
    for i in range(6):   # handle rattle
        at = 0.05 + i * 0.035 + rnd.uniform(0, 0.012)
        layers.append(sine(rnd.uniform(3800, 4600), start=at, attack=0.0004, decay=0.02, gain=-20 - rnd.uniform(0, 5)))
    save("plate_shed", 0.8, layers, seed=43)


def wreck():
    # A cable snaps (a crack and a whip falling in pitch), the winch motor runs down, the platform drops a notch on the
    # other cable and clangs, the rope creaks under the dead weight.
    rnd = random.Random(47)
    layers = [
        noise(attack=0.0003, decay=0.05, gain=0, hp=1500),
        sine(1800, end=140, attack=0.0005, decay=0.35, gain=-6),      # the whip
        sine(3600, end=280, attack=0.0005, decay=0.2, gain=-14),
    ]
    for f, g in ((120, -9), (240, -13), (360, -18), (60, -14)):       # winch runs down
        layers.append(sine(f, end=f * 0.22, start=0.05, attack=0.01, decay=1.4, gain=g))
    for f, d, g in ((287, 0.6, -5), (811, 0.45, -9), (1490, 0.3, -13), (2330, 0.2, -17)):   # the platform's clang
        layers.append(sine(f, start=0.42, attack=0.0008, decay=d, gain=g))
    layers.append(noise(start=0.42, attack=0.0005, decay=0.12, gain=-4, lp=2200, pink=True))
    layers.append(sine(110, end=60, start=0.42, attack=0.001, decay=0.25, gain=-6))
    layers += creak(0.9, n=14, pitch=480, gain=-16, period=0.04, slow=1.6, rnd=rnd)
    save("wreck", 2.0, layers, seed=47)


def mine_lay():
    # A disc mine slapped onto the glass: a magnetic clamp's thunk and its latch clicking shut.
    layers = [
        sine(150, end=90, attack=0.0008, decay=0.08, gain=-4),
        noise(attack=0.0005, decay=0.025, gain=-8, lp=1500, pink=True),
        noise(start=0.045, attack=0.0003, decay=0.008, gain=-10, hp=2500),
        sine(3150, start=0.045, attack=0.0004, decay=0.03, gain=-16),
    ]
    save("mine_lay", 0.3, layers, seed=53)


def mine_arm():
    # Armed: a rising two-note chirp, the same square-ish voice as the warning beeper.
    layers = []
    for at, f in ((0.0, 1000), (0.075, 1500)):
        for k, g in ((1, -6), (3, -16), (5, -22)):
            layers.append(sine(f * k, start=at, attack=0.001, decay=0.06, gain=g))
    save("mine_arm", 0.25, layers, seed=59)


BLAST_SECONDS = 0.75


def blast_layers():
    """The mine blast's design, rendered into the stem sources/gw_mine_blast.wav by sources.py (which drives it into a
    soft clip so it sits as loud as the other bots' attacks): a shaped charge on glass, a sharp crack, a short punchy
    body (not a boom: it's a small mine), glass and debris pinging out, a short tail."""
    rnd = random.Random(61)
    layers = [
        noise(attack=0.0003, decay=0.08, gain=0, hp=1500, lp=8000),
        noise(attack=0.0005, decay=0.45, gain=4, hp=300, lp=3000, pink=True),
        noise(attack=0.001, decay=0.3, gain=-2, lp=600, pink=True),
        sine(75, end=34, attack=0.001, decay=0.3, gain=-3),
        sine(160, end=60, attack=0.0008, decay=0.15, gain=-3),
        noise(start=0.01, attack=0.004, decay=0.6, gain=-8, hp=200, lp=1800, pink=True),
    ]
    for i in range(8):   # glass and shrapnel
        at = 0.03 + rnd.uniform(0, 0.3)
        layers.append(sine(rnd.uniform(2500, 6000), start=at, attack=0.0004, decay=rnd.uniform(0.03, 0.06),
                           gain=-21 - rnd.uniform(0, 8), pan=round(rnd.uniform(-0.5, 0.5), 2)))
    return layers


def mine_blast():
    # The driven stem (sources.py), +3 dB for the centre pan; the limit keeps the true peak under -1 dBTP.
    layer = {"type": "sample", "path": "../sources/gw_mine_blast.wav", "start": 0.0, "attack": 0.0, "decay": 0.0, "gain_db": 3.0}
    save("mine_blast", BLAST_SECONDS, [layer], seed=61, limit=-1.8)


def main():
    for f in (idle_loop, alert, telegraph, attack, hurt, plate_shed, wreck, mine_lay, mine_arm, mine_blast):
        f()
    print("wrote", len(list(OUT.glob("*.json"))), "recipes into", OUT)


if __name__ == "__main__":
    main()
