# Visiting Hours: example music sketches, made in code, as starting points to react to. The real pieces are
# composed with the creator, step by step (docs/MUSIC-SKETCHES.md explains each sketch and how to change it).
#   uv run --with numpy python tools/audio/make-music-sketches.py [piece ...]
# With no arguments it renders round 4's demos (chase-run, hope, despair); "all" renders everything; or name pieces.
# Writes out/vh-music-sketches/: each looping piece as <nn>-<piece>.loop.wav (its end runs into its start
# sample-accurately: everything ringing past the end is wrapped onto the start) and <nn>-<piece>.twice.wav (the
# loop heard twice, to hear the seam); one-shots as <nn>-<piece>.wav; the chase as speed stems (base, drive, peak) in
# two variants with a slow-fast-slow run; loops.txt (loop points and sections) and index.html. About -16 LUFS.
import os
import re
import subprocess
import sys

import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "out", "vh-music-sketches")
ONLY = [a for a in sys.argv[1:]]
RNG = np.random.default_rng(7)

# ---------------------------------------------------------------- the shared material (motif families)

# The dream's chords (the movement base, MoveMusicSynth): i-VI-III-VII in D minor, add9 colour.
DREAM_CHORDS = [[50, 53, 57, 64], [46, 50, 53, 57], [53, 57, 60, 67], [48, 50, 55, 64]]

# 1. THE FAMILY MOTIF (the Lasnames: the Dream, Elias, Lena, Nadia): one bar. A rising fourth, then two steps up.
FAMILY = [(69, 1.5), (74, .5), (76, 1), (77, 1)]                       # A D E F
# The Dream's own melody ("the street's song") grows out of it, with its answer.
STREET = FAMILY + [(76, 1.5), (74, .5), (74, 2), (72, 1), (77, 1), (76, 1), (72, 1), (74, 3), (None, 1)]
ANSWER = FAMILY + [(79, 1.5), (77, .5), (76, 2), (77, 1), (76, 1), (74, 1), (72, 1), (74, 4)]

# 2. THE WAKING MOTIF (the Opening and the Ending only): a slow fall that doesn't land, then a climb.
WAKING = [(69, 2), (67, 1), (65, 1), (64, 4)]                          # A G F E (held: unresolved)
WAKING_STAY = [(62, 1), (64, 1), (65, 2), (64, 4)]                     # D E F E: still unresolved (the Opening)
WAKING_HOME = [(62, 1), (64, 1), (66, 2), (69, 4)]                     # D E F# A: home, in D major (the Ending)

# 3. DAYTIME's own melody ("daylight"), F major: no shared melody, only kinship (the same four chords from F).
DAYLIGHT = [(77, .5), (76, .5), (72, 1), (69, 1), (72, 1), (67, .5), (69, .5), (72, 1), (76, 2),
            (74, .5), (76, .5), (77, 1), (76, .5), (74, .5), (72, 1), (74, 1), (72, 1), (69, 2)]
DAYLIGHT_2 = [(77, .5), (76, .5), (72, 1), (69, 1), (72, 1), (67, .5), (69, .5), (72, 1), (79, 2),
              (77, 1), (76, .5), (74, .5), (72, 1), (69, 1), (70, 1), (69, 1), (65, 2)]

# The characters' own material (each also quotes the family motif).
NADIA = [(62, 1), (65, 1), (67, 1), (69, 1), (72, 1.5), (69, .5), (67, 2),
         (67, 1), (71, 1), (69, 1), (67, 1), (62, 3), (None, 1)]          # D dorian: the B natural is her lift
LENA = [(65, 1), (74, 2), (72, 1), (70, 1), (69, 1), (67, 2), (70, 1), (69, 3),
        (65, 1), (74, 2), (75, 1), (74, 1), (72, 1), (70, 1), (67, 1), (72, 1), (70, 3)]  # 3/4, Bb major
ELIAS = [(62, 2), (57, 1), (60, 1), (58, 2), (57, 1), (55, 1), (53, 1.5), (55, .5), (57, 2), (52, 4)]
# The argument's own line (harmonic minor: the C# bites), and the family motif upside down for its breakdown.
CONFLICT = [(74, .5), (73, .5), (74, 1), (77, 1), (76, 1), (74, .5), (72, .5), (70, 1), (69, 2),
            (70, .5), (69, .5), (67, 1), (65, 1), (64, 1), (65, .5), (64, .5), (62, 1), (61, 2)]
FAMILY_INVERTED = [(69, 1.5), (64, .5), (62, 1), (61, 1)]              # A E D C#: the family, turned over


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def seq(notes, start_beat=0.0, transpose=0, scale=1.0):
    """(midi, beats) list -> [(beat, midi, beats)] from a start beat; scale squeezes or stretches the rhythm."""
    out, b = [], start_beat
    for m, d in notes:
        if m is not None:
            out.append((b, m + transpose, d * scale))
        b += d * scale
    return out


def beats_of(notes):
    return sum(d for _, d in notes)


# ---------------------------------------------------------------- the mix

class Mix:
    """A stereo buffer on a bar grid. loop=True: the buffer is exactly the loop, and everything ringing past the end
    (note tails, reverb) wraps round onto the start, as it would when the loop repeats: the file loops seamlessly."""

    def __init__(self, bars, bpm, bpb=4, loop=True, tail=5.0):
        self.bpm, self.bpb, self.loop, self.bars = bpm, bpb, loop, bars
        self.length = bars * bpb * 60.0 / bpm
        self.n = int(round(self.length * SR)) + (0 if loop else int(tail * SR))
        self.dry = np.zeros((2, self.n))
        self.send = np.zeros((2, self.n))
        self.pumped = np.zeros((2, self.n))
        self.kicks = []
        self.sections = []

    def sec(self, beat):
        return beat * 60.0 / self.bpm

    def bar(self, b):
        return b * self.bpb

    def section(self, name, start_bar, end_bar):
        self.sections.append((name, start_bar, end_bar))

    def _put(self, bus, x, i, l, r):
        n = self.n
        if not self.loop:
            if i >= n:
                return
            x = x[: n - i]
            bus[0, i:i + len(x)] += x * l
            bus[1, i:i + len(x)] += x * r
            return
        k = 0
        while k < len(x):
            j = (i + k) % n
            take = min(len(x) - k, n - j)
            bus[0, j:j + take] += x[k:k + take] * l
            bus[1, j:j + take] += x[k:k + take] * r
            k += take

    def add(self, x, beat, pan=0.0, gain=1.0, verb=0.2, pump=False):
        if len(x) == 0 or gain == 0:
            return
        i = int(round(self.sec(beat) * SR))
        x = x * gain
        l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
        self._put(self.pumped if pump else self.dry, x, i, l, r)
        if verb > 0:
            self._put(self.send, x * verb, i, l, r)

    def kick_at(self, beat):
        self.kicks.append(int(round(self.sec(beat) * SR)))

    def render(self, rt60=2.2, pump_depth=0.55):
        g = np.ones(self.n)
        w = int(0.6 * SR)
        shape = 1 - pump_depth * np.exp(-np.arange(w) / SR / 0.11)
        for i0 in self.kicks:
            idx = i0 + np.arange(w)
            s = shape
            if self.loop:
                idx = idx % self.n
            else:
                keep = idx < self.n
                idx, s = idx[keep], shape[keep]
            g[idx] = np.minimum(g[idx], s)
        return self.dry + self.pumped * g + reverb(self.send, rt60, self.loop)


def reverb(x, rt60, loop):
    """A plate-ish reverb: convolution with decaying, darkening stereo noise; on a loop its tail wraps round."""
    n_ir = int(rt60 * SR)
    t = np.arange(n_ir) / SR
    out = np.zeros_like(x)
    n = x.shape[1]
    for ch in range(2):
        ir = RNG.standard_normal(n_ir) * np.exp(-6.9 * t / rt60)
        ir = lowpass(ir, 6000) * 0.08
        ir[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))
        size = 1 << int(np.ceil(np.log2(n + n_ir)))
        y = np.fft.irfft(np.fft.rfft(x[ch], size) * np.fft.rfft(ir, size), size)[: n + n_ir]
        out[ch] = y[:n]
        if loop:
            out[ch, : n_ir] += y[n:n + n_ir]
    return out


def tile_apply(x, fn, loop):
    """Applies an offline filter to a loop as if it repeated (tiled three times, the middle kept)."""
    if not loop:
        return fn(x)
    n = len(x)
    return fn(np.concatenate([x, x, x]))[n:2 * n]


def lowpass(x, fc, order=2):
    """Zero-phase smooth low-pass by FFT (offline)."""
    size = 1 << int(np.ceil(np.log2(len(x) + 1)))
    f = np.fft.rfftfreq(size, 1 / SR)
    h = 1 / np.sqrt(1 + (f / fc) ** (2 * order))
    return np.fft.irfft(np.fft.rfft(x, size) * h, size)[: len(x)]


def highpass(x, fc, order=2):
    size = 1 << int(np.ceil(np.log2(len(x) + 1)))
    f = np.fft.rfftfreq(size, 1 / SR)
    h = 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order))
    return np.fft.irfft(np.fft.rfft(x, size) * h, size)[: len(x)]


# ---------------------------------------------------------------- instruments (additive, so no aliasing)

def env(n, attack, hold_n, release):
    a, r = max(1, int(attack * SR)), max(1, int(release * SR))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a) if a < n else np.linspace(0, 1, n)
    if hold_n < n:
        e[hold_n:] *= np.maximum(0, 1 - np.arange(n - hold_n) / r)
    return e


def partials(f, dur, amps, decays, attack=0.005, release=0.1, vib=0.0, rate=5.5, ratios=None, cents=0.0, fmax=15000):
    n = int((dur + release) * SR)
    t = np.arange(n) / SR
    f = f * 2 ** (cents / 1200)
    # Vibrato: a frequency wobble of +-vib (relative) at `rate` Hz, as a phase term.
    wob = -np.cos(2 * np.pi * rate * t) / rate if vib else None
    x = np.zeros(n)
    for k, (a, d) in enumerate(zip(amps, decays)):
        ratio = ratios[k] if ratios is not None else k + 1
        fk = f * ratio
        if fk > fmax:
            break
        ph = 2 * np.pi * fk * t
        if vib:
            ph = ph + fk * vib * wob
        x += a * np.exp(-t * d) * np.sin(ph + RNG.uniform(0, 2 * np.pi))
    return x * env(n, attack, int(dur * SR), release)


def saw_amps(k):
    return [1.0 / i for i in range(1, k + 1)]


def pad(m, dur, bright=12, cents=8, attack=0.6, release=1.2):
    """A detuned soft saw pad (three voices)."""
    x = 0
    for c in (-cents, 0, cents):
        a = [v * np.exp(-0.18 * i) for i, v in enumerate(saw_amps(bright))]
        x = x + partials(hz(m), dur, a, [0.15] * bright, attack, release, cents=c)
    return x / 3


def warm_pad(m, dur, attack=0.5, release=1.0):
    """The movement base's pad: sine and a little 3rd harmonic, doubled slightly sharp and flat."""
    return (partials(hz(m), dur, [1, 0, .12], [0, 0, 0], attack, release, cents=3)
            + partials(hz(m), dur, [1, 0, .12], [0, 0, 0], attack, release, cents=-3)) / 2


def sub(m, dur, release=0.15):
    return partials(hz(m), dur, [1], [0], 0.01, release)


def pluck(m, dur, bright=14, decay=3.0, release=0.15):
    a = saw_amps(bright)
    d = [decay * (1 + 0.35 * i) for i in range(bright)]
    return partials(hz(m), dur, a, d, 0.003, release)


def supersaw(m, dur, decay=2.2, bright=18, release=0.2):
    """The melodic-house lead: five detuned saws with a plucked brightness."""
    x = 0
    for c in (-14, -6, 0, 6, 14):
        a = saw_amps(bright)
        d = [decay * (0.6 + 0.25 * i) for i in range(bright)]
        x = x + partials(hz(m), dur, a, d, 0.004, release, cents=c)
    return x / 3.5


def piano(m, dur, vel=1.0, release=0.4):
    f = hz(m)
    k = 14
    ratios = [i * np.sqrt(1 + 0.0004 * i * i) for i in range(1, k + 1)]
    a = [vel * (1.0 / i ** (1.3 - 0.4 * vel)) for i in range(1, k + 1)]
    d = [0.9 + 0.55 * i + f / 600 for i in range(k)]
    x = partials(f, dur, a, d, 0.002, release, ratios=ratios)
    hammer = RNG.standard_normal(int(0.012 * SR)) * np.exp(-np.arange(int(0.012 * SR)) / (0.002 * SR)) * 0.08 * vel
    x[: len(hammer)] += hammer
    return x


def felt_piano(m, dur, vel=0.8):
    return lowpass(piano(m, dur, vel * 0.8, 0.8), 1800, 1)


def music_box(m, dur=1.2, cents=0.0):
    return partials(hz(m), dur, [1, .22, .08], [4.2, 9, 20], 0.002, 0.2, ratios=[1, 2, 5.4], cents=cents)


def marimba(m, dur=0.8):
    return partials(hz(m), dur, [1, .3, .12], [6, 16, 40], 0.002, 0.1, ratios=[1, 3.93, 9.2])


def celesta(m, dur=1.4):
    return partials(hz(m), dur, [1, .4, .15, .05], [2.5, 5, 9, 14], 0.002, 0.3, ratios=[1, 2, 3, 4.1])


def strings(m, dur, attack=0.35, release=0.6, bright=14, vib=0.004):
    x = 0
    for c in (-9, 0, 9):
        a = [v * np.exp(-0.12 * i) for i, v in enumerate(saw_amps(bright))]
        x = x + partials(hz(m), dur, a, [0] * bright, attack, release, vib=vib, rate=5.2 + c / 30, cents=c)
    return lowpass(x / 3, 4500, 1)


def staccato(m, dur=0.12):
    return lowpass(strings(m, dur, 0.01, 0.08, 12, 0), 5000, 1)


def choir(m, dur, attack=0.8, release=1.0):
    """An 'ooh': a saw weighted by two formants (about 400 and 850 Hz)."""
    f = hz(m)
    k = int(5000 / f)
    a = [(np.exp(-((f * i - 400) / 180) ** 2) + 0.6 * np.exp(-((f * i - 850) / 250) ** 2) + 0.05) / i ** 0.3 for i in range(1, k + 1)]
    x = 0
    for c in (-7, 0, 7):
        x = x + partials(f, dur, a, [0] * k, attack, release, vib=0.003, rate=4.8 + c / 20, cents=c)
    return x / 3


def brass(m, dur=0.35):
    n = int((dur + 0.15) * SR)
    t = np.arange(n) / SR
    bright = 1 - np.exp(-t / 0.04)
    x = 0
    for c in (-6, 6):
        f = hz(m) * 2 ** (c / 1200)
        for i in range(1, int(8000 / f)):
            x = x + (1 / i) * np.sin(2 * np.pi * f * i * t) * np.clip(bright * 14 / i, 0, 1)
    return x * env(n, 0.01, int(dur * SR), 0.15) / 3


def cello(m, dur, attack=0.25, release=0.5):
    return lowpass(strings(m, dur, attack, release, 18, 0.005), 2500, 1) * 1.3


def kick(level=1.0, punch=1.0):
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    f = 46 + 90 * punch * np.exp(-t / 0.035)
    ph = 2 * np.pi * np.cumsum(f) / SR
    click = RNG.standard_normal(n) * np.exp(-t / 0.002) * 0.15
    return (np.sin(ph) * np.exp(-t / 0.12) + click) * level


def snare(level=1.0):
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * 185 * t) * np.exp(-t / 0.05) * 0.5
    noise = highpass(RNG.standard_normal(n), 1500) * np.exp(-t / 0.07) * 0.6
    return (body + noise) * level


def clap(level=1.0):
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for off in (0, 0.008, 0.017):
        i = int(off * SR)
        x[i:] += RNG.standard_normal(n - i) * np.exp(-t[: n - i] / 0.012)
    x += RNG.standard_normal(n) * np.exp(-t / 0.08) * 0.4
    return lowpass(highpass(x, 900), 6000) * 0.5 * level


def hat(level=1.0, open_=False):
    n = int((0.25 if open_ else 0.05) * SR)
    t = np.arange(n) / SR
    return highpass(RNG.standard_normal(n), 7000) * np.exp(-t / (0.08 if open_ else 0.012)) * 0.35 * level


def shaker(level=1.0):
    n = int(0.08 * SR)
    t = np.arange(n) / SR
    e = np.minimum(1, t / 0.02) * np.exp(-t / 0.03)
    return highpass(RNG.standard_normal(n), 5000) * e * 0.25 * level


def tom(m=45, level=1.0):
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    f = hz(m) * (1 + 0.6 * np.exp(-t / 0.05))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.22) * level


def riser(seconds, level=0.5):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    x = RNG.standard_normal(n)
    out = np.zeros(n)
    seg = int(0.05 * SR)
    for i in range(0, n, seg):  # a band rising from 400 Hz to 8 kHz
        fc = 400 * (20 ** (i / n))
        out[i:i + seg] = highpass(lowpass(x[i:i + seg + 512], fc * 1.6), fc)[: len(out[i:i + seg])]
    return out * (t / seconds) ** 2 * level


def impact(level=1.0):
    n = int(1.5 * SR)
    x = lowpass(RNG.standard_normal(n) * np.exp(-np.arange(n) / SR / 0.4), 3000) * 0.4 * level
    k = kick(level, 1.4)
    x[: len(k)] += k
    return x


def lay(*xs):
    """Sums sounds of different lengths (padding the shorter ones)."""
    n = max(len(x) for x in xs)
    out = np.zeros(n)
    for x in xs:
        out[: len(x)] += x
    return out


def clarinet(m, dur, attack=0.06, release=0.25):
    """A soft reed: odd harmonics, a little vibrato."""
    return lowpass(partials(hz(m), dur, [1, 0, .45, 0, .22, 0, .1, 0, .05], [0] * 9, attack, release, vib=0.004, rate=5.0), 3500, 1)


def bell(m, dur=2.5):
    return partials(hz(m), dur, [1, .5, .25, .12], [1.2, 2.4, 4, 7], 0.002, 0.6, ratios=[1, 2.76, 5.4, 8.9])


# ---------------------------------------------------------------- helpers to write parts (bars are absolute)

def chord_at(prog, b):
    return prog(b) if callable(prog) else prog[b % len(prog)]


def chords(mix, prog, start, bars, fn, gain=0.15, verb=0.35, pump=False, octave=0, pan_spread=0.5, ramp=None):
    for b in range(start, start + bars):
        ch = chord_at(prog, b)
        g = gain * (ramp(b - start) if ramp else 1.0)
        for j, m in enumerate(ch):
            pan = (j / max(1, len(ch) - 1) - 0.5) * pan_spread * 2
            mix.add(fn(m + octave, mix.sec(mix.bpb)), mix.bar(b), pan, g, verb, pump)


def melody(mix, notes, fn, gain=0.3, pan=0.0, verb=0.3, legato=0.95, pump=False, echo=0.0, echo_beats=0.75):
    for beat, m, d in notes:
        x = fn(m, mix.sec(d) * legato)
        mix.add(x, beat, pan, gain, verb, pump)
        if echo:
            mix.add(x, beat + echo_beats, -pan - 0.4, gain * echo, verb * 1.5, pump)
            mix.add(x, beat + 2 * echo_beats, pan + 0.4, gain * echo * echo, verb * 2, pump)


def drums(mix, start, bars, kick_level=0.85, punch=1.0, snare_fn=None, back=0.45, hats=0.5, half=False, ghost=True):
    snare_fn = snare_fn or clap
    for b in range(start, start + bars):
        for beat in range(mix.bpb):
            at = mix.bar(b) + beat
            if not half or beat in (0,):
                mix.add(kick(kick_level, punch), at, 0, 1, 0.02)
                mix.kick_at(at)
            if back and ((not half and beat % 2 == 1) or (half and beat == 2)):
                mix.add(snare_fn(back), at, 0.05, 1, 0.25)
            if hats:
                mix.add(hat(hats), at + 0.5, 0.25, 1, 0.05)
                if ghost:
                    mix.add(hat(hats * 0.45), at, -0.2, 1, 0.05)


def bassline(mix, prog, start, bars, fn, pattern=(0.5, 1.5, 2.5, 3.5), octave=-12, gain=0.3, length=0.45):
    for b in range(start, start + bars):
        root = chord_at(prog, b)[0]
        for p in pattern:
            mix.add(fn(root + octave, mix.sec(length)), mix.bar(b) + p, 0, gain, 0.05)


def subline(mix, prog, start, bars, gain=0.4, pump=True):
    for b in range(start, start + bars):
        mix.add(sub(chord_at(prog, b)[0] - 12, mix.sec(mix.bpb) * 0.97), mix.bar(b), 0, gain, 0.0, pump)


def arp(mix, prog, start, bars, fn, gain=0.12, step=0.25, octave=24, order=(0, 1, 2, 3, 2, 3, 1, 2), pan=0.3, verb=0.35):
    k = 0
    for b in range(start, start + bars):
        ch = chord_at(prog, b)
        for s in range(int(mix.bpb / step)):
            m = ch[order[k % len(order)] % len(ch)] + octave
            mix.add(fn(m), mix.bar(b) + s * step, pan * (1 if k % 2 else -1), gain * (1.0 if s % 4 == 0 else 0.75), verb)
            k += 1


def muffle(x, mix, points, fc=500):
    """The music opens and closes: points are (bar, openness 0..1), linear between (circular on a loop)."""
    t_bar = np.arange(x.shape[1]) / SR / mix.sec(mix.bpb)
    bars = [p[0] for p in points]
    vals = [p[1] for p in points]
    w = np.interp(t_bar, bars, vals)[None, :]
    lp = np.vstack([tile_apply(x[c], lambda y: lowpass(y, fc), mix.loop) for c in range(2)])
    return lp * (1 - w) + x * w


def ends(x, fin=0.0, fout=0.0):
    if fin:
        a = int(fin * SR)
        x[:, :a] *= np.linspace(0, 1, a)
    if fout:
        b = int(fout * SR)
        x[:, -b:] *= np.linspace(1, 0, b) ** 1.5
    return x


# ---------------------------------------------------------------- the pieces

def dream_theme():
    """The Dream theme. D minor, 104 bpm, melodic house (the "Faded" era). Built on the family motif: the street's
    song is the family motif carried on. Loop: intro (the beat, the music opening) - A - B (the lift) - breakdown - A' -
    outro (back to the beat, the music closing)."""
    mix = Mix(32, 104)
    prog = DREAM_CHORDS
    mix.prog, mix.tonic = prog, 62
    for name, a, b in (("intro", 0, 4), ("A", 4, 12), ("B (the lift)", 12, 20), ("breakdown", 20, 24), ("A'", 24, 28), ("outro", 28, 32)):
        mix.section(name, a, b)
    # The beat that ends the outro and starts the intro: a soft kick, hats and the sub.
    drums(mix, 0, 4, 0.55, 0.8, back=0, hats=0.35)
    drums(mix, 28, 4, 0.55, 0.8, back=0, hats=0.35)
    subline(mix, prog, 0, 32, 0.34)
    chords(mix, prog, 0, 4, lambda m, d: warm_pad(m, d, 0.8, 1.2), 0.16, 0.4, ramp=lambda i: 0.5 + 0.17 * i)
    melody(mix, seq(STREET, mix.bar(0)), lambda m, d: felt_piano(m, d), 0.45, -0.1, 0.45)
    # A and A': the groove, the lead, pumping pads.
    for a, n, tune in ((4, 8, STREET + ANSWER), (24, 4, STREET)):
        drums(mix, a, n, 0.85, 1.0, back=0.45, hats=0.5)
        chords(mix, prog, a, n, lambda m, d: pad(m, d, 10), 0.11, 0.3, pump=True)
        bassline(mix, prog, a, n, lambda m, d: pluck(m, d, 8, 6), gain=0.22)
        melody(mix, seq(tune, mix.bar(a)), lambda m, d: supersaw(m, d), 0.16, 0.0, 0.35)
        melody(mix, seq(tune, mix.bar(a), -12), lambda m, d: piano(m, d, 0.7), 0.17, -0.2, 0.3)
    melody(mix, seq(STREET, mix.bar(24), 12), lambda m, d: music_box(m), 0.13, 0.3, 0.5)
    # B, the lift: the movement's arpeggio, the music box carrying the answer, the lead resting for the last half.
    drums(mix, 12, 8, 0.85, 1.0, back=0.45, hats=0.6)
    chords(mix, prog, 12, 8, lambda m, d: pad(m, d, 12), 0.12, 0.35, pump=True)
    bassline(mix, prog, 12, 8, lambda m, d: pluck(m, d, 8, 6), gain=0.22)
    arp(mix, prog, 12, 8, lambda m: pluck(m, 0.25, 10, 7), 0.07)
    melody(mix, seq(ANSWER, mix.bar(12)), lambda m, d: supersaw(m, d), 0.16, 0, 0.35)
    melody(mix, seq(ANSWER, mix.bar(12), 12) + seq(STREET, mix.bar(16), 12), lambda m, d: music_box(m), 0.15, 0.3, 0.5)
    # Breakdown: no kick; the pad and felt piano, a riser into A'.
    chords(mix, prog, 20, 4, lambda m, d: warm_pad(m, d, 0.6, 1.5), 0.16, 0.5)
    melody(mix, seq(STREET, mix.bar(20)), lambda m, d: felt_piano(m, d), 0.5, 0.0, 0.5)
    mix.add(riser(mix.sec(8), 0.3), mix.bar(22), 0, 1, 0.2)
    # Outro: the lead goes, the pad thins, the family motif once on piano, back to the beat.
    chords(mix, prog, 28, 4, lambda m, d: warm_pad(m, d, 0.5, 1.2), 0.16, 0.4, ramp=lambda i: 1.0 - 0.17 * i)
    melody(mix, seq(FAMILY, mix.bar(28)), lambda m, d: felt_piano(m, d + 1), 0.45, 0.0, 0.5)
    x = mix.render(2.4, 0.5)
    return mix, muffle(x, mix, [(0, 0.15), (4, 1.0), (28, 1.0), (32, 0.15)], 450)


def chase_prog(b):
    if b < 16 or b >= 40:
        return [[50, 53, 57, 62], [46, 50, 53, 58], [48, 52, 55, 60], [45, 49, 52, 57]][b % 4]   # Dm Bb C A
    if b < 32:
        return [[43, 46, 50, 55], [46, 50, 53, 58], [41, 45, 48, 53], [45, 49, 52, 57]][b % 4]   # Gm Bb F A
    return [[50, 53, 57, 62], [50, 53, 57, 62], [46, 50, 53, 58], [46, 50, 53, 58]][b % 4]       # Dm Dm Bb Bb


CHASE_VARIANTS = {
    # Grounded: dry and close (piano, plucked bass, a kit with a snare). Its base is the Opening's (round 3): a low
    # drone on the chord's root and fifth under soft held chords, so slow running is clearly heard.
    "grounded": dict(rt60=1.0, verb=0.6, pad=lambda m, d: warm_pad(m, d, 1.2, 2.0), drone=True, pulse=None,
                     kick=(0.95, 1.1), back=snare, back_level=0.5, hats=0.55,
                     lead=lambda m, d: lay(piano(m, d, 0.85) * 1.2, pluck(m, d, 12, 6) * 0.5), echo=0.0,
                     arp=lambda m: pluck(m, 0.2, 10, 9), top=lambda m, d: music_box(m, 1.0),
                     air=lambda m, d: strings(m, d, 0.4, 0.8, 12)),
    # Airy: space and air (choir and soft pads, bells, echoes, a soft kick, a long reverb).
    "airy": dict(rt60=4.5, verb=1.8, pad=lambda m, d: lay(choir(m, d) * 1.2, pad(m, d, 6, 14) * 0.6), drone=False,
                 pulse=lambda m, d: pluck(m, d, 3, 4), kick=(0.6, 0.6), back=clap, back_level=0.28, hats=0.3,
                 lead=lambda m, d: celesta(m, 1.4), echo=0.45,
                 arp=lambda m: bell(m, 0.8), top=lambda m, d: bell(m, 2.0),
                 air=lambda m, d: choir(m, d, 1.0, 1.5)),
    # Spacey (round 4): analog-synth space (bright detuned pads, a synth pulse, a supersaw lead and arpeggio with wide
    # echoes, a 3 s room).
    "spacey": dict(rt60=3.0, verb=1.4, pad=lambda m, d: pad(m, d, 14, 20), drone=False,
                   pulse=lambda m, d: pluck(m, d, 6, 8), kick=(0.8, 1.0), back=clap, back_level=0.38, hats=0.45,
                   lead=lambda m, d: supersaw(m, d, 3.0), echo=0.4,
                   arp=lambda m: pluck(m, 0.3, 14, 5), top=lambda m, d: lay(bell(m, 1.5), pluck(m, d, 10, 3) * 0.6),
                   air=lambda m, d: pad(m, d, 10, 25)),
}
# Each stem's loudness on its own (round 3): every tier steps up in presence, so the peak is never quieter than the
# drive. One gain is then set for all three together, so the stems still add up to exactly the mix.
STEM_LUFS = {"base": -21.0, "drive": -20.0, "peak": -19.0}


def chase_stems(variant):
    """The chase: the Dream theme running, as speed stems on one 48-bar loop (128 bpm, 90 s): base (always), drive
    (fast), peak (flying). Sections A (bars 1-16), B (17-32, new chords), breakdown (33-40, half time), A' (41-48)."""
    v = CHASE_VARIANTS[variant]
    bpm, bars = 128, 48
    mixes = {k: Mix(bars, bpm) for k in ("base", "drive", "peak")}
    for m in mixes.values():
        for name, a, b in (("A", 0, 16), ("B", 16, 32), ("breakdown", 32, 40), ("A'", 40, 48)):
            m.section(name, a, b)
    base, drive, peak = mixes["base"], mixes["drive"], mixes["peak"]
    vs = v["verb"]
    # BASE: the bed you hear even standing still.
    subline(base, chase_prog, 0, bars, 0.36, pump=False)
    if v["drone"]:
        # The Opening's drone: the root and the fifth below it, low, a long note per bar overlapping the next.
        for b in range(bars):
            r = chord_at(chase_prog, b)[0]
            d = base.sec(base.bpb)
            base.add(lay(warm_pad(r - 12, d, 0.8, 1.6), warm_pad(r - 17, d, 0.8, 1.6)), base.bar(b), 0, 0.2, 0.4 * vs)
        chords(base, chase_prog, 0, bars, v["pad"], 0.09, 0.6 * vs)
    else:
        chords(base, chase_prog, 0, bars, v["pad"], 0.07, 0.35 * vs)
    if v["pulse"]:
        bassline(base, chase_prog, 0, 32, v["pulse"], (0, 1, 2, 3), 0, 0.12, 0.4)
        bassline(base, chase_prog, 40, 8, v["pulse"], (0, 1, 2, 3), 0, 0.12, 0.4)
    # DRIVE (fast): drums, the off-beat bass, toms into each section, and the street's song at double speed.
    kl, kp = v["kick"]
    for a, n in ((0, 16), (16, 16), (40, 8)):
        drums(drive, a, n, kl, kp, v["back"], v["back_level"], v["hats"])
        bassline(drive, chase_prog, a, n, lambda m, d: pluck(m, d, 12, 9), (0.5, 1.5, 2.5, 3.5, 1.75, 3.75), -12, 0.22, 0.25)
    drums(drive, 32, 8, kl * 0.8, kp, v["back"], v["back_level"], v["hats"] * 0.6, half=True)
    for b in (15, 31, 39, 47):
        for k, m in enumerate((45, 43, 40, 38)):
            drive.add(tom(m, 0.55), drive.bar(b) + 2 + k * 0.5, -0.3 + 0.2 * k, 1, 0.2 * vs)
    fast = [(m, d * 0.5) for m, d in STREET[:7]]
    fast_answer = [(m, d * 0.5) for m, d in ANSWER[:7]]
    for k in range(8):
        melody(drive, seq(fast, drive.bar(2 * k), 0 if k < 4 else 12), v["lead"], 0.12, 0.0, 0.25 * vs, 0.8, echo=v["echo"])
        melody(drive, seq(fast_answer, drive.bar(16 + 2 * k)), v["lead"], 0.12, 0.05, 0.25 * vs, 0.8, echo=v["echo"])
    for k in range(4):
        melody(drive, seq(fast, drive.bar(40 + 2 * k)) + seq(fast, drive.bar(40 + 2 * k), -12), v["lead"], 0.1, 0.0, 0.25 * vs, 0.8, echo=v["echo"])
    # PEAK (flying): the sixteenth arpeggio (A, A'), the music box or bells on the full melody (B), air in the breakdown.
    arp(peak, chase_prog, 0, 16, v["arp"], 0.06, 0.25, 24, verb=0.35 * vs)
    arp(peak, chase_prog, 40, 8, v["arp"], 0.06, 0.25, 24, verb=0.35 * vs)
    melody(peak, seq(STREET, peak.bar(16), 12) + seq(ANSWER, peak.bar(24), 12), v["top"], 0.14, 0.3, 0.5 * vs, echo=v["echo"])
    chords(peak, chase_prog, 32, 8, v["air"], 0.05, 0.6 * vs, octave=12)
    melody(peak, seq(FAMILY, peak.bar(34), 12, 2.0), v["top"], 0.14, 0.2, 0.6 * vs, echo=v["echo"])
    peak.add(riser(peak.sec(8), 0.3), peak.bar(38), 0, 1, 0.3 * vs)
    return {k: (m, m.render(v["rt60"], 0.4 if k == "base" else 0.0)) for k, m in mixes.items()}


def daytime():
    """The Daytime theme. F major, 92 bpm. Its own melody ("daylight"), no dream motif; kin only in harmony (the
    dream's four chords started from F). Loop: intro (the groove) - A - B (new chords) - A' - outro (back to the groove)."""
    mix = Mix(32, 92)
    prog_a = [[53, 57, 60, 64], [48, 52, 55, 62], [50, 53, 57, 64], [46, 50, 53, 57]]   # Fmaj7 Cadd9 Dm(add9) Bbmaj7
    prog_b = [[46, 50, 53, 57], [48, 52, 55, 58], [45, 48, 52, 55], [50, 53, 57, 60]]   # Bbmaj7 C7 Am7 Dm7
    prog = lambda b: prog_b[b % 4] if 12 <= b < 20 else prog_a[b % 4]
    mix.prog, mix.tonic = prog, 65
    for name, a, b in (("intro", 0, 4), ("A", 4, 12), ("B", 12, 20), ("A'", 20, 28), ("outro", 28, 32)):
        mix.section(name, a, b)
    for b in range(32):
        ch = prog(b)
        soft = b < 4 or b >= 28
        for s in (0, 1.5, 2, 3.5):
            for m in ch[1:]:
                mix.add(piano(m, mix.sec(0.4), 0.45), mix.bar(b) + s, -0.25, 0.1 if soft else 0.12, 0.2)
        mix.add(piano(ch[0] - 12, mix.sec(1.8), 0.6), mix.bar(b), -0.1, 0.2, 0.15)
        mix.add(piano(ch[0] - 12, mix.sec(1.5), 0.5), mix.bar(b) + 2, -0.1, 0.16, 0.15)
        for beat in range(4):
            if beat % 2 == 0:
                mix.add(kick(0.5 if soft else 0.55, 0.7), mix.bar(b) + beat, 0, 1, 0.02)
            elif not soft:
                mix.add(clap(0.3), mix.bar(b) + beat, 0.1, 1, 0.2)
            mix.add(shaker(0.7), mix.bar(b) + beat + 0.5, 0.4, 1, 0.05)
            mix.add(shaker(0.4), mix.bar(b) + beat + 0.25, 0.4, 1, 0.05)
        mix.add(sub(ch[0] - 24, mix.sec(3.8)), mix.bar(b), 0, 0.22, 0)
    arp(mix, prog, 4, 24, lambda m: pluck(m, 0.35, 9, 5), 0.06, 0.5, 12, (0, 2, 1, 3, 2, 1, 3, 2), 0.5)
    melody(mix, seq(DAYLIGHT, mix.bar(4)) + seq(DAYLIGHT_2, mix.bar(8)), lambda m, d: marimba(m, 0.6), 0.34, 0.15, 0.3)
    # B: the melody moves to a whistle-like celesta over the new chords, strings under it.
    melody(mix, seq(DAYLIGHT, mix.bar(12), -5) + seq(DAYLIGHT_2, mix.bar(16), -5), lambda m, d: celesta(m), 0.26, 0.1, 0.35)
    chords(mix, prog, 12, 8, lambda m, d: strings(m, d, 0.5, 0.8, 10), 0.05, 0.4)
    melody(mix, seq(DAYLIGHT, mix.bar(20)) + seq(DAYLIGHT_2, mix.bar(24)), lambda m, d: marimba(m, 0.6), 0.3, 0.15, 0.3)
    melody(mix, seq(DAYLIGHT, mix.bar(20), 12) + seq(DAYLIGHT_2, mix.bar(24), 12), lambda m, d: celesta(m), 0.13, -0.2, 0.35)
    return mix, mix.render(1.4, 0.0)


def opening_title():
    """The opening and the title screen. D minor, 72 bpm. An intro of 7 bars (23.3 s) for the black and the news report
    (the music starts 3 s before the report, which runs 18.45 s), then the title screen's loop (12 bars, 40 s), entered
    at the intro's end. The waking motif, left unresolved: drone, felt piano, glass."""
    bpm = 72
    intro = Mix(7, bpm, loop=False, tail=6.0)
    body = Mix(12, bpm)
    intro.section("intro: the black, then the news report", 0, 7)
    body.section("the title loop", 0, 12)
    ip = [DREAM_CHORDS[0], DREAM_CHORDS[1], DREAM_CHORDS[0], DREAM_CHORDS[1], DREAM_CHORDS[2], DREAM_CHORDS[3], DREAM_CHORDS[3]]
    d_all = intro.sec(intro.bpb * 7)
    intro.add(lay(warm_pad(38, d_all, 3, 3), warm_pad(45, d_all, 3, 3)), 0, 0, 0.18, 0.4)
    chords(intro, ip, 0, 7, lambda m, d: warm_pad(m, d, 1.2, 2.0), 0.09, 0.6)
    # Under the news the piano is sparse and a little quieter, in the gaps of the voice.
    melody(intro, seq(WAKING, intro.bar(1)) + seq(WAKING_STAY, intro.bar(3)), lambda m, d: felt_piano(m, d + 2, 0.55), 0.48, 0.0, 0.7, 1.0)
    intro.add(partials(hz(88), 5, [1, .1], [0.5, 1.5], 1.0, 2.0), intro.bar(5.5), 0.4, 0.05, 0.8)
    # The title loop: the drone held right round the loop, the dream's chords, the waking motif twice (the second time
    # an octave up with low strings), then four bars of just the chords and glass, back to where it started.
    d_loop = body.sec(body.bpb * 12)
    body.add(lay(warm_pad(38, d_loop, 3, 3), warm_pad(45, d_loop, 3, 3)), 0, 0, 0.18, 0.4)
    chords(body, DREAM_CHORDS, 0, 12, lambda m, d: warm_pad(m, d, 1.2, 2.0), 0.09, 0.6)
    melody(body, seq(WAKING, body.bar(0)) + seq(WAKING_STAY, body.bar(2)), lambda m, d: felt_piano(m, d + 2, 0.6), 0.55, 0.0, 0.7, 1.0)
    melody(body, seq(WAKING, body.bar(4), 12) + seq(WAKING_STAY, body.bar(6), 12), lambda m, d: felt_piano(m, d + 2, 0.5), 0.36, 0.15, 0.7, 1.0)
    melody(body, seq(WAKING, body.bar(4), -12) + seq(WAKING_STAY, body.bar(6), -12), lambda m, d: strings(m, d, 0.6, 1.5, 10), 0.1, -0.2, 0.5)
    for b, m in ((8.5, 88), (10.5, 86)):
        body.add(partials(hz(m), 5, [1, .1], [0.5, 1.5], 1.0, 2.0), body.bar(b), 0.4, 0.05, 0.8)
    xi = muffle(intro.render(3.5, 0.0), intro, [(0, 0.1), (2, 1.0), (13, 1.0)], 700)
    return intro, ends(xi, 1.5, 0.0), body, body.render(3.5, 0.0)


def argument():
    """An argument battle. D minor, 138 bpm, a 97 s loop. Its own line ("conflict"), and the family motif upside down
    in the build (the arguments in this game are inside one family). Loop: intro (the ostinato and toms) - A - B (new
    chords) - C, the duel (strings and brass trade the line over a march) - the build (4 bars, half time) - A' - outro
    (back to the ostinato and toms)."""
    mix = Mix(56, 138)
    pa = [[50, 53, 57], [46, 50, 53], [43, 46, 50], [45, 49, 52]]      # Dm Bb Gm A
    pb = [[46, 50, 53], [48, 52, 55], [43, 46, 50], [45, 49, 52]]      # Bb C Gm A
    pc = [[41, 45, 48], [48, 52, 55], [50, 53, 57], [45, 49, 52]]      # F C Dm A (the duel)
    pd = [[50, 53, 57], [50, 53, 57], [46, 50, 53], [45, 49, 52]]      # Dm Dm Bb A (the build)
    sec = [("intro", 0, 4), ("A", 4, 16), ("B", 16, 28), ("C, the duel", 28, 36), ("the build", 36, 40), ("A'", 40, 52), ("outro", 52, 56)]
    for name, a, b in sec:
        mix.section(name, a, b)

    def part(b):
        return next(name for name, a, e in sec if a <= b < e)

    prog = lambda b: {"B": pb, "C, the duel": pc, "the build": pd}.get(part(b), pa)[b % 4]
    for b in range(56):
        p = part(b)
        r = prog(b)[0]
        ch = prog(b)
        if p == "C, the duel":
            # The duel: a march (kick on 1 and 3, a snare roll building through each pair of bars), pizzicato chords.
            for beat in range(4):
                if beat % 2 == 0:
                    mix.add(kick(0.7, 0.9), mix.bar(b) + beat, 0, 1, 0.1)
                for s in range(4):
                    acc = 1.0 if s == 0 else 0.45
                    mix.add(snare(0.18 * acc * (1 + 0.5 * (b % 2))), mix.bar(b) + beat + s * 0.25, 0.05, 1, 0.2)
            for s, j in enumerate((0, 1, 2, 1, 0, 2, 1, 2)):
                mix.add(staccato(ch[j] + 12, 0.07), mix.bar(b) + s * 0.5, -0.3 if s % 2 else 0.3, 0.11, 0.2)
            mix.add(sub(r - 12, mix.sec(3.9)), mix.bar(b), 0, 0.3, 0)
            continue
        quiet = p == "the build"
        pat = [r, r + 12, r + 7, r + 12] * 4 if p != "B" else [r, r + 7, r + 12, r + 7, r + 10, r + 12, r + 7, r + 12] * 2
        for s, m in enumerate(pat):
            if quiet and s % 2:
                continue
            g = (0.16 if not quiet else 0.08) * (1.0 if s % 4 == 0 else 0.7)
            mix.add(staccato(m, 0.09), mix.bar(b) + s * 0.25, -0.35 if s % 2 else 0.35, g, 0.15)
        edge = p in ("intro", "outro")
        if not quiet:
            for beat in range(4):
                if not edge or b in (2, 3, 52, 53):
                    mix.add(tom(38 if beat % 2 == 0 else 43, 0.7), mix.bar(b) + beat, 0, 1, 0.2)
        if not edge and not quiet:
            mix.add(snare(0.5), mix.bar(b) + 1, 0, 1, 0.3)
            mix.add(snare(0.5), mix.bar(b) + 3, 0, 1, 0.3)
            mix.add(sub(r - 12, mix.sec(3.9)), mix.bar(b), 0, 0.33, 0)
            if b % 2 == 0:
                for m in ch:
                    mix.add(brass(m + 12, mix.sec(0.6)), mix.bar(b), 0, 0.13, 0.3)
                    mix.add(brass(m + 12, mix.sec(0.3)), mix.bar(b) + 2.5, 0, 0.11, 0.3)
            if p == "B":
                mix.add(hat(0.4), mix.bar(b) + 0.5, 0.2, 1, 0.05)
                mix.add(hat(0.4), mix.bar(b) + 2.5, 0.2, 1, 0.05)
        if quiet:
            mix.add(sub(r - 12, mix.sec(3.9)), mix.bar(b), 0, 0.3, 0)
            mix.add(snare(0.35), mix.bar(b) + 2, 0, 1, 0.5)
    strings_line = lambda m, d: strings(m, d, 0.05, 0.3, 14)
    melody(mix, seq(CONFLICT, mix.bar(8), 0) + seq(CONFLICT, mix.bar(12), 12), strings_line, 0.2, 0.1, 0.35)
    melody(mix, seq(CONFLICT, mix.bar(20), -12), lambda m, d: brass(m, d), 0.14, -0.1, 0.3)
    melody(mix, seq(CONFLICT, mix.bar(24), 12), strings_line, 0.2, 0.1, 0.35)
    # The duel: strings call, brass answers an octave down, strings again higher, then both together.
    half = CONFLICT[:9]
    melody(mix, seq(half, mix.bar(28), 12), strings_line, 0.22, 0.3, 0.35)
    melody(mix, seq(half, mix.bar(30), -12), lambda m, d: brass(m, d), 0.16, -0.3, 0.3)
    melody(mix, seq(CONFLICT[9:], mix.bar(32), 12), strings_line, 0.22, 0.3, 0.35)
    melody(mix, seq(CONFLICT[9:], mix.bar(34), 0), lambda m, d: brass(m, d), 0.15, -0.3, 0.3)
    melody(mix, seq(CONFLICT[9:], mix.bar(34), 12), strings_line, 0.18, 0.3, 0.35)
    # The build: half time, low strings, the family motif upside down on solo cello, a riser.
    chords(mix, prog, 36, 4, lambda m, d: strings(m - 12, d, 0.8, 1.0, 10), 0.06, 0.4)
    melody(mix, seq(FAMILY_INVERTED, mix.bar(36), -12, 2.0), lambda m, d: cello(m, d), 0.4, -0.1, 0.4)
    mix.add(riser(mix.sec(8), 0.3), mix.bar(38), 0, 1, 0.2)
    melody(mix, seq(CONFLICT, mix.bar(40), 12) + seq(CONFLICT, mix.bar(44), 12), strings_line, 0.2, 0.1, 0.35)
    melody(mix, seq(CONFLICT, mix.bar(44), 0), lambda m, d: brass(m, d), 0.12, -0.1, 0.3)
    melody(mix, seq(FAMILY_INVERTED, mix.bar(48), 12, 1.0), lambda m, d: brass(m, d), 0.12, 0.1, 0.3)
    return mix, mix.render(1.6, 0.0)


def lena():
    """Lena's motif. Bb major, a 3/4 waltz at 84 bpm, 69 s loop. Her own rising-sixth melody, and the family motif in
    her key in the B section. Loop: intro (the waltz beat) - A - B (the family motif) - A' - outro (back to the beat)."""
    mix = Mix(32, 84, 3)
    pa = [[46, 50, 53, 57], [43, 46, 50, 53], [39, 43, 46, 50], [41, 45, 48, 53]]   # Bbmaj7 Gm7 Ebmaj7 F
    pb = [[39, 43, 46, 50], [41, 45, 48, 51], [38, 41, 45, 48], [43, 46, 50, 53]]   # Ebmaj7 F7 Dm7 Gm7
    prog = lambda b: pb[b % 4] if 12 <= b < 20 else pa[b % 4]
    mix.prog, mix.tonic = prog, 58
    for name, a, b in (("intro", 0, 4), ("A", 4, 12), ("B (the family motif)", 12, 20), ("A'", 20, 28), ("outro", 28, 32)):
        mix.section(name, a, b)
    chords(mix, prog, 0, 32, lambda m, d: warm_pad(m, d, 0.6, 1.0), 0.09, 0.4)
    for b in range(32):
        ch = prog(b)
        mix.add(pluck(ch[0] - 12, mix.sec(2.5), 8, 2.5), mix.bar(b), -0.2, 0.25, 0.2)
        for beat in (1, 2):
            for m in ch[1:3]:
                mix.add(pluck(m + 12, mix.sec(0.8), 9, 3.5), mix.bar(b) + beat, 0.25, 0.09, 0.25)
    family_bb = [(65, 1.5), (70, .5), (72, 1), (74, 3)]            # F Bb C D: the family motif in her key
    melody(mix, seq(LENA, mix.bar(4)), lambda m, d: celesta(m, 1.6), 0.4, 0.1, 0.45)
    melody(mix, seq(family_bb, mix.bar(12)) + seq(family_bb, mix.bar(16), 0, 1.0), lambda m, d: strings(m, d, 0.3, 0.8, 12), 0.2, -0.1, 0.45)
    melody(mix, seq(LENA[8:], mix.bar(14), 0) + seq(LENA[8:], mix.bar(18), 0), lambda m, d: celesta(m, 1.6), 0.3, 0.15, 0.45)
    melody(mix, seq(LENA, mix.bar(20)), lambda m, d: celesta(m, 1.6), 0.36, 0.1, 0.45)
    melody(mix, seq(LENA, mix.bar(20), -12), lambda m, d: strings(m, d, 0.3, 0.6, 10), 0.12, -0.25, 0.4)
    return mix, mix.render(2.2, 0.0)


def elias():
    """Elias's motif. D minor, 66 bpm, 73 s loop. His own falling figure, stopping on an unresolved E; then the family
    motif, slow and high (the dream's material is his). Under it all, a gentle broken-chord pulse on felt piano, so the
    loop never goes quiet. Loop: intro (the pulse, piano bass and strings) - A (his figure, twice) - B (the family motif)
    - outro (his figure's opening, softly, falling back to the pulse)."""
    mix = Mix(20, 66)
    prog = [[50, 53, 57], [46, 50, 53], [43, 46, 50], [45, 49, 52, 55]]   # Dm Bb Gm A7
    for name, a, b in (("intro", 0, 2), ("A (his figure)", 2, 10), ("B (the family motif)", 10, 16), ("outro", 16, 20)):
        mix.section(name, a, b)
    chords(mix, prog, 0, 20, lambda m, d: strings(m - 12, d, 0.8, 1.0, 10), 0.08, 0.4)
    for b in range(20):
        ch = prog[b % 4]
        mix.add(piano(ch[0] - 12, mix.sec(3.5), 0.5), mix.bar(b), -0.2, 0.22, 0.4)
        edge = b < 2 or b >= 16
        for s, j in enumerate((0, 1, 2, 1, 0, 2, 1, 2)):
            mix.add(felt_piano(ch[j % len(ch)] + 12, mix.sec(1.2), 0.45), mix.bar(b) + s * 0.5, 0.25 if s % 2 else -0.05,
                    0.16 if edge else 0.1, 0.45)
    melody(mix, seq(ELIAS, mix.bar(2), -12), lambda m, d: cello(m, d), 0.4, -0.15, 0.35)
    melody(mix, seq(ELIAS, mix.bar(6), -12), lambda m, d: cello(m, d), 0.4, -0.15, 0.35)
    melody(mix, seq(ELIAS, mix.bar(6), 0), lambda m, d: piano(m, d, 0.5), 0.2, 0.2, 0.45)
    melody(mix, seq(FAMILY, mix.bar(10), 0, 2.0), lambda m, d: strings(m, d, 0.5, 1.0, 12), 0.2, 0.1, 0.5)
    melody(mix, seq(STREET[:7], mix.bar(13), 12), lambda m, d: music_box(m, 1.6), 0.12, 0.35, 0.7)
    melody(mix, seq(ELIAS[:6], mix.bar(12), -12), lambda m, d: cello(m, d), 0.3, -0.15, 0.4)
    # Outro: his figure's opening on piano, softly, and a short cello answer that leads back round to the intro.
    melody(mix, seq(ELIAS[:4], mix.bar(16)), lambda m, d: piano(m, d, 0.45), 0.2, 0.2, 0.45)
    melody(mix, seq([(57, 2), (55, 1), (53, 1), (52, 3)], mix.bar(18), -12), lambda m, d: cello(m, d), 0.26, -0.15, 0.4)
    return mix, mix.render(3.0, 0.0)


def nadia():
    """Nadia's motif (new). D dorian (D minor with a B natural: her lift), 96 bpm, 80 s loop. Her own stepwise,
    determined climb on a soft reed, a plucked ostinato, brushed drums; the family motif on piano in the B section.
    Loop: intro (the ostinato and brushes) - A - B (the family motif) - A' - outro (back to the ostinato)."""
    mix = Mix(32, 96)
    pa = [[50, 53, 57, 60], [43, 47, 50, 55], [50, 53, 57, 60], [48, 52, 55, 60]]   # Dm7 G Dm7 C
    pb = [[46, 50, 53, 57], [48, 52, 55, 60], [50, 53, 57, 62], [43, 47, 50, 55]]   # Bbmaj7 C Dm G
    prog = lambda b: pb[b % 4] if 12 <= b < 20 else pa[b % 4]
    for name, a, b in (("intro", 0, 4), ("A", 4, 12), ("B (the family motif)", 12, 20), ("A'", 20, 28), ("outro", 28, 32)):
        mix.section(name, a, b)
    for b in range(32):
        ch = prog(b)
        order = (0, 2, 1, 3, 2, 1, 3, 2)
        for s in range(8):
            m = ch[order[s]] + 12
            mix.add(pluck(m, mix.sec(0.6), 8, 4), mix.bar(b) + s * 0.5, 0.3 if s % 2 else -0.3, 0.08, 0.25)
        mix.add(sub(ch[0] - 12, mix.sec(3.8)), mix.bar(b), 0, 0.22, 0)
        for beat in range(4):
            if beat in (0, 2):
                mix.add(kick(0.45, 0.6), mix.bar(b) + beat, 0, 1, 0.05)
            else:
                mix.add(lowpass(snare(0.35), 3000), mix.bar(b) + beat, 0.05, 1, 0.3)
            mix.add(shaker(0.5), mix.bar(b) + beat + 0.5, -0.3, 1, 0.1)
    chords(mix, prog, 4, 24, lambda m, d: warm_pad(m, d, 0.6, 1.0), 0.07, 0.4)
    melody(mix, seq(NADIA, mix.bar(4)) + seq(NADIA, mix.bar(8)), lambda m, d: clarinet(m, d), 0.3, -0.05, 0.35)
    melody(mix, seq(FAMILY, mix.bar(12), -12) + seq(FAMILY, mix.bar(16), -12), lambda m, d: piano(m, d + 0.5, 0.7), 0.3, 0.1, 0.4)
    melody(mix, seq(NADIA[7:], mix.bar(14)) + seq(NADIA[7:], mix.bar(18)), lambda m, d: clarinet(m, d), 0.26, -0.05, 0.35)
    melody(mix, seq(NADIA, mix.bar(20)) + seq(NADIA, mix.bar(24)), lambda m, d: clarinet(m, d), 0.3, -0.05, 0.35)
    melody(mix, seq(NADIA, mix.bar(24), -12), lambda m, d: strings(m, d, 0.3, 0.6, 10), 0.1, 0.25, 0.4)
    return mix, mix.render(1.8, 0.0)


def ending():
    """The ending reprise. D minor turning to D major, 80 bpm, about 66 s, plays once (no loop). The waking motif from
    the opening, this time finishing: D E F# A, home. The family motif once, as a farewell."""
    mix = Mix(20, 80, loop=False, tail=6.0)
    prog = DREAM_CHORDS
    for name, a, b in (("the waking motif, as in the opening", 0, 4), ("strings take it", 4, 8), ("the family motif, a farewell", 8, 12),
                       ("the build", 12, 16), ("D major", 16, 20)):
        mix.section(name, a, b)
    chords(mix, prog, 0, 16, lambda m, d: strings(m, d, 0.8, 1.2, 12), 0.07, 0.5)
    chords(mix, prog, 8, 8, lambda m, d: choir(m + 12, d), 0.035, 0.6)
    for b in range(4, 16):
        ch = prog[b % 4]
        for s, j in zip((0, 1, 2, 3), (0, 2, 1, 3)):
            mix.add(piano(ch[j], mix.sec(1.5), 0.45), mix.bar(b) + s, -0.25, 0.11, 0.4)
        mix.add(sub(ch[0] - 12, mix.sec(3.9)), mix.bar(b), 0, 0.2, 0)
    melody(mix, seq(WAKING, mix.bar(0)) + seq(WAKING_STAY, mix.bar(2)), lambda m, d: felt_piano(m, d + 1.5, 0.6), 0.55, 0.0, 0.6)
    melody(mix, seq(WAKING, mix.bar(4)) + seq(WAKING_STAY, mix.bar(6)), lambda m, d: strings(m, d, 0.3, 0.8, 14), 0.2, 0.0, 0.45)
    melody(mix, seq(FAMILY, mix.bar(8)) + seq(FAMILY, mix.bar(10)), lambda m, d: music_box(m, 1.6), 0.14, 0.3, 0.6)
    melody(mix, seq(FAMILY, mix.bar(8), -12), lambda m, d: piano(m, d, 0.7), 0.25, 0.1, 0.45)
    melody(mix, seq(WAKING, mix.bar(12), 12) , lambda m, d: strings(m, d, 0.3, 0.8, 14), 0.18, 0.0, 0.45)
    # The cadence: Bb, C, then D major, and the waking motif finishes: D E F# A.
    for b, ch in ((14, [46, 53, 58, 62, 65]), (15, [48, 55, 60, 64, 67]), (16, [38, 50, 57, 62, 66, 69])):
        for m in ch:
            mix.add(strings(m, mix.sec(4 if b < 16 else 14), 0.3, 3.0, 12), mix.bar(b), 0, 0.08, 0.5)
            mix.add(piano(m, mix.sec(4 if b < 16 else 8), 0.5), mix.bar(b), -0.1, 0.09, 0.5)
    melody(mix, seq(WAKING_HOME, mix.bar(14), 12), lambda m, d: strings(m, d, 0.2, 1.5, 14), 0.2, 0.0, 0.5)
    melody(mix, seq(WAKING_HOME, mix.bar(14)), lambda m, d: piano(m, d, 0.7), 0.28, 0.1, 0.5)
    mix.add(choir(74, mix.sec(14)) + choir(78, mix.sec(14)), mix.bar(16), 0, 0.05, 0.6)
    melody(mix, seq([(81, 2), (79, 1), (78, 1), (76, 4), (74, 1), (76, 1), (78, 2), (81, 4)], mix.bar(16) + 1), lambda m, d: music_box(m, 2.0), 0.13, 0.3, 0.7)
    x = mix.render(3.2, 0.0)
    return mix, ends(x, 0.3, 6.0)


# ---------------------------------------------------------------- round 3: hope and despair, overlaid on any piece

def into(m, lo, hi):
    """The pitch class of m moved by octaves into lo..hi."""
    while m < lo:
        m += 12
    while m > hi:
        m -= 12
    return m


def hope_stem(like, prog, start, end):
    """HOPE, as an extra stem for any piece (its key, tempo and metre come from the piece's own chords): every two bars
    a rising figure, high, built from that bar's chord (its fifth, the root above, the ninth; then the third above,
    held), over a soft halo (root, fifth, ninth) on high strings. Built from the chords, so it always fits."""
    mix = Mix(like.bars, like.bpm, like.bpb)
    bpb = like.bpb
    for b in range(start, end, 2):
        ch, ch2 = chord_at(prog, b), chord_at(prog, b + 1)
        r, third, fifth = ch[0], ch[1], ch[2]
        k = 1.0 if b > start else 0.6
        notes = [(into(fifth, 72, 84), 0, 1), (into(r, 74, 86), 1, 1), (into(r + 14, 76, 88), 2, bpb - 2),
                 (into(ch2[1], 77, 89), bpb, bpb)]
        for m, at, d in notes:
            mix.add(celesta(m, mix.sec(d) + 0.8), mix.bar(b) + at, 0.2, 0.3 * k, 0.5)
            mix.add(strings(m, mix.sec(d), 0.15, 0.8, 10), mix.bar(b) + at, -0.2, 0.08 * k, 0.5)
    for b in range(start, end):
        ch = chord_at(prog, b)
        for m in (into(ch[0], 79, 90), into(ch[2], 79, 91), into(ch[0] + 14, 80, 92)):
            mix.add(strings(m, mix.sec(bpb), 0.8, 1.2, 8), mix.bar(b), 0.0, 0.03, 0.6)
    return mix, mix.render(2.6, 0.0)


def despair_stem(like, prog, tonic, start, end):
    """DESPAIR, as an extra stem for any piece: a pedal on the piece's own tonic (low, dark, slowly beating), and every
    two bars a falling sigh in the cellos: a half step above the chord's fifth sinking onto it, then the flattened
    second sinking onto the root. Every four bars a music box, out of tune, plays that flattened second high."""
    mix = Mix(like.bars, like.bpm, like.bpb)
    bpb = like.bpb
    for b in range(start, end):
        d = mix.sec(bpb)
        k = 0.6 if b == start else 1.0
        mix.add(lowpass(lay(warm_pad(into(tonic, 26, 37), d, 1.0, 1.6), partials(hz(into(tonic, 26, 37)), d, [1], [0], 1.0, 1.6, cents=7)), 300),
                mix.bar(b), 0, 0.5 * k, 0.3)
    for b in range(start, end, 2):
        ch = chord_at(prog, b + 1)
        r, fifth = into(chord_at(prog, b)[0], 45, 56), into(chord_at(prog, b)[2], 45, 57)
        r2 = into(ch[0], 45, 56)
        sigh = [(fifth + 1, 0, bpb / 2), (fifth, bpb / 2, bpb / 2), (r2 + 1, bpb, bpb / 2), (r2, bpb * 1.5, bpb / 2)]
        for m, at, dd in sigh:
            x = lowpass(strings(m, mix.sec(dd) * 1.05, 0.25, 0.9, 12, vib=0.008), 1800, 1)
            mix.add(x, mix.bar(b) + at, -0.15, 0.22, 0.5)
    for b in range(start + 2, end, 4):
        mix.add(music_box(into(tonic + 1, 74, 85), 2.0, cents=-35), mix.bar(b) + 1, 0.35, 0.1, 0.8)
    return mix, mix.render(3.4, 0.0)


DESPAIR_LEVELS = {
    # Round 4: despair as a feeling that grows. Each level is a stem the game can crossfade into the next; the host's
    # own changes (the sag and the darkening) scale with the level. Only "full" comes near round 3's despair.
    #   under: how many LU the level's stem sits under the song (faint is far under); sag: cents of slow pitch sag and recovery (a tape wow) on the host; open: how much of the host's brightness is
    #   left (the muffled copy does the rest); and what the stem itself plays.
    "faint": dict(under=17, sag=6, open=0.85, low_every=4, sigh_every=0, box_every=0, pedal=0.0, breath=0.035),
    "growing": dict(under=11, sag=12, open=0.7, low_every=2, sigh_every=4, box_every=8, pedal=0.0, breath=0.05),
    "full": dict(under=6, sag=20, open=0.55, low_every=2, sigh_every=2, box_every=4, pedal=0.35, breath=0.06),
}


def despair_level_stem(like, prog, tonic, start, end, level):
    """DESPAIR at one intensity, as a stem for any piece (its key, tempo and metre come from the piece's own chords).
    Faint: sparse low tones on the root (a cello, far off) and filtered breath. Growing: the low tones every other
    bar, and every four bars a falling half-step sigh onto the chord's root. Full: the sighs every two bars, a
    detuned music box on the flattened second, and a low pedal on the tonic. The host's sag and darkening come with it
    (sag_and_darken)."""
    p = DESPAIR_LEVELS[level]
    mix = Mix(like.bars, like.bpm, like.bpb)
    bpb = like.bpb
    bar_s = mix.sec(bpb)
    for b in range(start, end):
        k = min(1.0, (b - start + 1) / 2.0) * min(1.0, (end - b) / 2.0)   # in and out over two bars
        ch = chord_at(prog, b)
        if (b - start) % p["low_every"] == 0:
            m = into(ch[0], 38, 49)
            mix.add(lowpass(cello(m, bar_s * 1.6, 1.2, 1.5), 900, 1), mix.bar(b), -0.1, 0.22 * k, 0.6)
        if p["sigh_every"] and (b - start) % p["sigh_every"] == 0:
            r = into(ch[0], 45, 56)
            for m, at, dd in ((r + 1, 0, bpb / 2), (r, bpb / 2, bpb * 0.75)):
                mix.add(lowpass(strings(m, mix.sec(dd) * 1.05, 0.35, 1.0, 12, vib=0.008), 1600, 1), mix.bar(b) + at, -0.15, 0.18 * k, 0.55)
        if p["box_every"] and (b - start) % p["box_every"] == p["box_every"] // 2:
            mix.add(music_box(into(tonic + 1, 74, 85), 2.0, cents=-35), mix.bar(b) + 1, 0.35, 0.08 * k, 0.8)
        if p["pedal"]:
            mix.add(lowpass(lay(warm_pad(into(tonic, 26, 37), bar_s, 1.0, 1.6),
                                partials(hz(into(tonic, 26, 37)), bar_s, [1], [0], 1.0, 1.6, cents=7)), 300), mix.bar(b), 0, p["pedal"] * k, 0.3)
        if p["breath"] and (b - start) % 2 == 1:
            n = int(bar_s * 1.5 * SR)
            e = np.sin(np.pi * np.arange(n) / n) ** 2
            mix.add(lowpass(highpass(RNG.standard_normal(n), 250), 900) * e * p["breath"] * k, mix.bar(b), 0.3 - 0.6 * (b % 4 == 1), 1, 0.5)
    return mix, mix.render(3.4, 0.0)


def sag_and_darken(x, mix, depth_cents, open_, start, end, ramp=None):
    """The host under despair: a slow pitch sag and recovery (a variable delay, so the length and the loop don't
    change) and a darkening (crossfade to a low-passed copy), both easing in over two bars from `start` and out by
    `end`. ramp(t_seconds) -> 0..1 overrides that with an intensity curve (the ramp demo)."""
    n = x.shape[1]
    t = np.arange(n) / SR
    bar_s = mix.sec(mix.bpb)
    if ramp is None:
        w = np.clip(np.minimum((t / bar_s - start) / 2.0, (end - t / bar_s) / 2.0), 0, 1)
    else:
        w = ramp(t)
    period = mix.length / max(1, round(mix.length / 16.0))          # about 16 s a sag, whole cycles per loop
    amp = depth_cents * np.log(2) / 1200 * period / (2 * np.pi)
    d = (amp * w) * np.cos(2 * np.pi * t / period) * SR
    idx = (np.arange(n) - d) % n
    y = np.vstack([np.interp(idx, np.arange(n + 1), np.concatenate([x[c], x[c, :1]])) for c in range(2)])
    lp = np.vstack([tile_apply(y[c], lambda z: lowpass(z, 1100), mix.loop) for c in range(2)])
    o = 1 - (1 - open_) * w
    return lp * (1 - o) + y * o


def short_term(x):
    """EBU R128 short-term loudness (3 s window) every 0.1 s, from ffmpeg's ebur128 frame log."""
    p = subprocess.run(["ffmpeg", "-v", "verbose", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-af", "ebur128",
                        "-f", "null", "-"], input=x.T.astype(np.float32).tobytes(), capture_output=True)
    vals = re.findall(r"t:\s*[\d.]+\s+TARGET:\S+ LUFS\s+M:\s*(-?[\d.]+)\s+S:\s*(-?[\d.]+)", p.stderr.decode("utf-8", "replace"))
    return np.array([float(s) for _, s in vals])


def tiled_short_term(x):
    s = short_term(np.concatenate([x, x, x], axis=1))
    k = len(s) // 3
    return s[k:2 * k]


def compare(host, other, region):
    """Integrated difference and the short-term difference (median and worst, LU) inside and outside a region
    (start, end in seconds), loop-aware."""
    sh, so = tiled_short_term(host), tiled_short_term(other)
    n = min(len(sh), len(so))
    sh, so = sh[:n], so[:n]
    t = np.arange(n) / 10.0
    inside = (t >= region[0]) & (t < region[1])
    d = so - sh
    ih, io = tiled_lufs(host), tiled_lufs(other)
    return dict(integrated=io - ih, in_med=float(np.median(d[inside])), in_max=float(np.max(np.abs(d[inside]))),
                in_p95=float(np.percentile(np.abs(d[inside]), 95)),
                out_med=float(np.median(d[~inside])), out_max=float(np.max(np.abs(d[~inside]))),
                out_p95=float(np.percentile(np.abs(d[~inside]), 95)))


def neutral_hope(fn):
    """HOPE laid over a piece without changing its loudness (round 4): the host gets the same gain as when it plays
    alone, and the whole mix rides a smooth gain (a light duck) so its short-term loudness follows the host's."""
    mix, x = fn()
    start, end = 4, mix.bars - 4
    _, o = hope_stem(mix, mix.prog, start, end)
    host, _, _, g = master(x, True)
    k = 10 ** (g / 20)
    h_raw, o_raw = x * k, o * k
    sh = tiled_short_term(host)
    env = np.ones(x.shape[1])
    t10 = np.arange(len(sh)) / 10.0
    t = np.arange(x.shape[1]) / SR
    # Ride only where hope plays (plus a second either side for the 3 s loudness window), damped, smoothed over 1.5 s.
    a, b = mix.sec(mix.bar(start)) - 1.0, mix.sec(mix.bar(end)) + 1.0
    zone = ((t10 >= a) & (t10 < b)).astype(float)
    for _ in range(5):
        m = h_raw * env + o_raw * env
        mm = ffmpeg_pipe(np.concatenate([m, m, m], axis=1), LIMIT)[:, m.shape[1]:2 * m.shape[1]]
        sm = tiled_short_term(mm)
        n = min(len(sh), len(sm))
        dd = np.zeros(len(sh))
        dd[:n] = (sm[:n] - sh[:n]) * zone[:n] * 0.7
        kern = np.ones(15) / 15
        dd = np.convolve(np.concatenate([dd[-7:], dd, dd[:7]]), kern, "valid")
        env = env * 10 ** (-np.interp(t, t10, dd[: len(t10)]) / 20)
    m = h_raw * env + o_raw * env
    y = ffmpeg_pipe(np.concatenate([m, m, m], axis=1), LIMIT)[:, m.shape[1]:2 * m.shape[1]]
    region = (mix.sec(mix.bar(start)), mix.sec(mix.bar(end)))
    return mix, host, y, region


def under(o, host, lu):
    """Scales a stem so its own loudness sits `lu` below its host's (loop-aware)."""
    return o * 10 ** ((tiled_lufs(host) - lu - tiled_lufs(o)) / 20)


def despair_version(fn, level=None, ramp=None, gain=None):
    """A piece with despair at one level (bars 5 to four before the end), or ramping by intensity over time."""
    mix, x = fn()
    start, end = 4, mix.bars - 4
    if ramp is None:
        p = DESPAIR_LEVELS[level]
        _, o = despair_level_stem(mix, mix.prog, mix.tonic, start, end, level)
        o = under(o, x, p["under"])
        xh = sag_and_darken(x, mix, p["sag"], p["open"], start, end)
        return mix, x, xh + o
    stems = {lv: under(despair_level_stem(mix, mix.prog, mix.tonic, start, mix.bars, lv)[1], x, DESPAIR_LEVELS[lv]["under"])
             for lv in DESPAIR_LEVELS}
    t = np.arange(x.shape[1]) / SR
    i = ramp(t)                                                         # 0 faint .. 1 full
    wf = np.clip(1 - np.abs(i - 0.0) / 0.5, 0, 1)
    wg = np.clip(1 - np.abs(i - 0.5) / 0.5, 0, 1)
    wu = np.clip((i - 0.5) / 0.5, 0, 1)
    on = np.clip(t - mix.sec(mix.bar(start)), 0, 1)
    o = (stems["faint"] * wf + stems["growing"] * wg + stems["full"] * wu) * on
    xh = sag_and_darken(x, mix, 20 * 1.0, 0.55, start, mix.bars, ramp=lambda tt: np.clip(ramp(tt), 0, 1) * np.clip(tt - mix.sec(mix.bar(start)), 0, 1))
    return mix, x, xh + o


def long_night():
    """DESPAIR, a first sketch for the Long Night ending: unsettling eeriness and despair. D phrygian, 54 bpm, a 71 s
    loop. A pedal of slowly beating low sines on D; dark chords where the second is flattened (Eb over the D pedal,
    then Bb minor); the despair sigh in the cellos (Eb sinking to D, Bb to A); the family motif played backwards on a
    music box that drifts further out of tune each time (a memory going wrong); reversed swells that pull in and stop
    dead; a slow heartbeat that sometimes skips; a high G# (the tritone) that never resolves; breath-like air."""
    mix = Mix(16, 54)
    for name, a, b in (("the pedal and the sighs", 0, 8), ("the memory goes wrong", 8, 16)):
        mix.section(name, a, b)
    bar_s = mix.sec(4)
    d_loop = mix.sec(64)
    pedal = lay(partials(hz(26), d_loop, [1, .35, .12], [0, 0, 0], 4, 4, cents=4),
                partials(hz(26), d_loop, [1, .35, .12], [0, 0, 0], 4, 4, cents=-4),
                partials(hz(38), d_loop, [1, .2], [0, 0], 4, 4, cents=2))
    mix.add(lowpass(pedal, 400), 0, 0, 0.5, 0.3)
    prog = [[50, 53, 57], [51, 55, 58], [50, 53, 57], [46, 49, 53]]   # Dm, Eb (over the D pedal), Dm, Bbm
    chords(mix, prog, 0, 16, lambda m, d: lowpass(strings(m - 12, d * 1.1, 1.5, 2.0, 10, vib=0.006), 1500, 1), 0.07, 0.6)
    # The despair sigh, in the cellos, every two bars.
    for b in range(0, 16, 2):
        pair = [(63, 2), (62, 2), (58, 2), (57, 2)] if b % 4 == 0 else [(63, 2), (62, 2), (61, 2), (62, 2)]
        melody(mix, seq(pair, mix.bar(b), -12), lambda m, d: lowpass(strings(m, d * 1.05, 0.4, 1.2, 12, vib=0.01), 1600, 1), 0.3, -0.15, 0.5)
    # The family motif backwards, on a music box drifting out of tune.
    backwards = [(77, 1), (76, 1), (74, .5), (69, 2.5)]
    for k, b in enumerate((2, 6, 10, 14)):
        for beat, m, d in seq(backwards, mix.bar(b), 12):
            mix.add(music_box(m, 2.5, cents=-15 - 20 * k + 6 * np.sin(beat)), beat, 0.35, 0.12, 0.9)
    # Reversed swells: a cluster (D, Eb, A) played backwards, ending dead on a bar line.
    for b in (4, 8, 12, 16):
        sw = lay(pad(62, 2.5, 8, 10, 0.05, 2.0), pad(63, 2.5, 8, 10, 0.05, 2.0), pad(69, 2.5, 8, 10, 0.05, 2.0))[::-1]
        mix.add(sw, mix.bar(b) - mix.bpm / 60 * len(sw) / SR, 0, 0.12, 0.2)
    # A slow heartbeat (lub-dub on each beat), skipping now and then.
    for beat in range(64):
        if beat in (27, 46, 47):
            continue
        for off, lvl in ((0, 0.5), (0.22, 0.32)):
            n = int(0.25 * SR)
            t = np.arange(n) / SR
            thump = np.sin(2 * np.pi * np.cumsum(48 + 20 * np.exp(-t / 0.03)) / SR) * np.exp(-t / 0.07) * lvl
            mix.add(lowpass(thump, 200), beat + off, 0, 0.5, 0.05)
    # The tritone: a high G#, thin, trembling, never resolving.
    for b in (5, 13):
        n = int(bar_s * 2 * SR)
        t = np.arange(n) / SR
        g = partials(hz(80), bar_s * 2, [1], [0], 1.5, 1.5)
        g = g * (0.6 + 0.4 * np.sin(2 * np.pi * 5.5 * np.arange(len(g)) / SR))
        mix.add(g, mix.bar(b), 0.4, 0.02, 0.7)
    # Air: dark breath-like noise swells.
    for b in range(0, 16, 2):
        n = int(bar_s * 2 * SR)
        e = np.sin(np.pi * np.arange(n) / n) ** 2
        mix.add(lowpass(highpass(RNG.standard_normal(n), 300), 1200) * e * 0.06, mix.bar(b) + 1, -0.4 + 0.2 * (b % 4), 1, 0.6)
    return mix, mix.render(5.0, 0.0)


LOOPS = [
    ("01", "dream-theme", "The Dream theme", dream_theme),
    ("03", "daytime", "The Daytime theme", daytime),
    ("05", "argument-battle", "An argument battle", argument),
    ("06", "lena", "Lena's motif", lena),
    ("07", "elias", "Elias's motif", elias),
    ("09", "nadia", "Nadia's motif", nadia),
    ("10", "long-night", "The Long Night (despair, a first sketch)", long_night),
]
ONESHOTS = [
    ("08", "ending", "The ending reprise", ending),
]
# Round 3's hope and despair demos: (number, slug, title, piece).
COLOURED = [("01", "dream-theme", "The Dream theme", dream_theme), ("03", "daytime", "The Daytime theme", daytime),
            ("06", "lena", "Lena's motif (3/4, Bb major)", lena)]
ROUND4 = {"chase-run", "hope", "despair"}


# ---------------------------------------------------------------- mastering, files, the page

def ffmpeg_pipe(x, af):
    p = subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-af", af,
                        "-f", "f32le", "-"], input=x.T.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, np.float32).reshape(-1, 2).T.astype(np.float64)


def measure(x):
    p = subprocess.run(["ffmpeg", "-v", "info", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-af",
                        "ebur128=peak=true", "-f", "null", "-"], input=x.T.astype(np.float32).tobytes(), capture_output=True)
    err = p.stderr.decode("utf-8", "replace")
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", err)[-1]), float(re.findall(r"Peak:\s+(-?[\d.]+) dBFS", err)[-1])


LIMIT = "alimiter=limit=0.84:attack=3:release=60:level=false"


def master(x, loop, lufs=-16.0, gain=None):
    """Gain to -16 LUFS and a limiter; a loop is processed tiled three times and the middle kept, so the limiter's
    state at the start is what the end leaves (the seam stays seamless). gain= reuses another file's gain."""
    n = x.shape[1]
    work = np.concatenate([x, x, x], axis=1) if loop else x
    if gain is None:
        gain = 0.0
        for _ in range(3):
            got, peak = measure(ffmpeg_pipe(work * 10 ** (gain / 20), LIMIT))
            gain += lufs - got
    y = ffmpeg_pipe(work * 10 ** (gain / 20), LIMIT)
    if loop:
        y = y[:, n:2 * n]
    got, peak = measure(np.concatenate([y, y], axis=1) if loop else y)
    return y, got, peak, gain


def seam(y):
    """How big the loop's wrap step (last sample into the first) is against a typical step: under ~4 is seamless."""
    step = np.sqrt(np.mean(np.diff(y, axis=1) ** 2))
    return float(np.max(np.abs(y[:, 0] - y[:, -1])) / max(step, 1e-9))


def write(path, y):
    import wave
    pcm = np.clip(np.round(y.T * 32767), -32767, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def timeline(mix):
    bar_s = mix.sec(mix.bpb)
    return "; ".join(f"{name} bars {a + 1}-{b} ({a * bar_s:.1f}-{b * bar_s:.1f} s)" for name, a, b in mix.sections)


def tiled_lufs(x):
    return measure(np.concatenate([x, x, x], axis=1))[0]


def run_preview(stems, mix, muffled_base, seconds=96.0, keys=None):
    """A run going slow -> fast -> flying -> fast -> slow, mixed by the game's rules (StemMixer): drive from fast (8 m/s),
    peak from flying (13 m/s), switched only on bar lines, 1 s in and 2 s out, the base muffled when slow."""
    keys = keys or [(0, 2), (6, 5), (20, 5), (22, 10), (40, 10), (42, 15), (64, 15), (66, 10), (78, 10), (80, 4), (92, 3), (96, 0)]
    n = int(seconds * SR)
    L = stems["base"].shape[1]
    out = np.zeros((2, n))
    blk = int(0.01 * SR)
    bar_s = mix.sec(mix.bpb)
    on = {"drive": False, "peak": False}
    g = {"drive": 0.0, "peak": 0.0}
    tier, below, opened, fly = 0, 0.0, 0.0, 0.0
    cues = []
    for i0 in range(0, n, blk):
        t = i0 / SR
        v = float(np.interp(t, [k[0] for k in keys], [k[1] for k in keys]))
        target = 2 if v >= 13 else 1 if v >= 8 else 0
        if target > tier:
            tier, below = target, 0.0
            cues.append(f"{t:5.1f} s: {['running', 'fast', 'flying'][tier]}")
        elif tier > 0 and v < (13 if tier == 2 else 8) - 1.5:
            below += blk / SR
            if below >= 0.6:
                tier, below = tier - 1, 0.0
                cues.append(f"{t:5.1f} s: {['running', 'fast', 'flying'][tier]}")
        else:
            below = 0.0
        if int(t / bar_s) != int(max(0, t - blk / SR) / bar_s):
            on["drive"], on["peak"] = tier >= 1, tier >= 2
        fly = min(1, fly + 4 * blk / SR) if tier == 2 else max(0, fly - 0.8 * blk / SR)
        u = min(1.0, max(0.0, v / 13.0))
        target_open = max(u * u * (3 - 2 * u), fly)
        opened = min(target_open, opened + 1.2 * blk / SR) if target_open > opened else max(target_open, opened - 0.5 * blk / SR)
        idx = (i0 + np.arange(min(blk, n - i0))) % L
        o = out[:, i0:i0 + len(idx)]
        o += stems["base"][:, idx] * opened + muffled_base[:, idx] * (1 - opened) * 1.25
        for k in ("drive", "peak"):
            g0 = g[k]
            tgt = 1.0 if on[k] else 0.0
            g[k] = min(tgt, g0 + blk / SR / 1.0) if tgt > g0 else max(tgt, g0 - blk / SR / 2.0)
            o += stems[k][:, idx] * np.linspace(g0, g[k], len(idx))[None, :]
    return ends(out, 0.0, 3.0), cues


def note(lines, text):
    print(text, flush=True)
    lines.append(text)


def read_wav(path):
    import wave
    with wave.open(path) as w:
        x = np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(np.float64) / 32767
        return x.reshape(-1, w.getnchannels()).T


# The run demo (round 4): through every threshold and back in 30 s (it took 96 s): running, fast at 4 s, flying at
# 11.5 s, back to fast at 19 s, running at 25 s. The game's rules are unchanged; the demo's run is just quicker.
RUN_KEYS = [(0, 3), (3.5, 3), (4, 10), (11, 10), (11.5, 15), (18.5, 15), (19, 10), (24, 10), (24.5, 4), (30, 2)]


def chase_files(variant, lines):
    """Renders, balances and writes a chase variant's stems (see STEM_LUFS)."""
    stems = chase_stems(variant)
    mix = stems["base"][0]
    raw = {k: v[1] for k, v in stems.items()}
    before = {k: tiled_lufs(v) for k, v in raw.items()}
    for k in raw:
        raw[k] = raw[k] * 10 ** ((STEM_LUFS[k] - before[k]) / 20)
    full = raw["base"] + raw["drive"] + raw["peak"]
    got, peak = measure(np.concatenate([full, full, full], axis=1))
    gain = min(-16.0 - got, -1.5 - peak)
    k = 10 ** (gain / 20)
    for name in raw:
        raw[name] = raw[name] * k
        write(os.path.join(OUT, f"02-chase-{variant}.{name}.loop.wav"), raw[name])
    write(os.path.join(OUT, f"02-chase-{variant}.all.loop.wav"), full * k)
    note(lines, f"02-chase-{variant}: stems written; each alone " + ", ".join(f"{n} {STEM_LUFS[n] + gain:.1f} LUFS" for n in raw)
         + f"; all together {got + gain:.1f} LUFS, true peak {peak + gain:.1f} dBFS, seam {seam(full * k):.2f}")
    return mix, raw


def main():
    os.makedirs(OUT, exist_ok=True)
    lines = []
    want = lambda slug: (not ONLY and slug in ROUND4) or slug in ONLY or "all" in ONLY
    for num, slug, title, fn in LOOPS:
        if slug in ONLY or "all" in ONLY:
            mix, x = fn()
            y, got, peak, _ = master(x, True)
            write(os.path.join(OUT, f"{num}-{slug}.loop.wav"), y)
            write(os.path.join(OUT, f"{num}-{slug}.twice.wav"), np.concatenate([y, y], axis=1))
            note(lines, f"{num}-{slug}.loop.wav: {y.shape[1] / SR:.2f} s, {mix.bpm} bpm; {got:.1f} LUFS, true peak {peak:.1f} dBFS, "
                        f"seam {seam(y):.2f} | {timeline(mix)}")
    if want("chase-run"):
        for variant in CHASE_VARIANTS:
            files = {k: os.path.join(OUT, f"02-chase-{variant}.{k}.loop.wav") for k in ("base", "drive", "peak")}
            if variant in ONLY or not all(os.path.exists(f) for f in files.values()):
                mix, raw = chase_files(variant, lines)
            else:
                mix = Mix(48, 128)
                raw = {k: read_wav(f) for k, f in files.items()}
            muffled = np.vstack([tile_apply(raw["base"][c], lambda z: lowpass(z, 700), True) for c in range(2)])
            run, cues = run_preview(raw, mix, muffled, 30.0, RUN_KEYS)
            write(os.path.join(OUT, f"02-chase-{variant}.run.wav"), run)
            note(lines, f"02-chase-{variant}.run.wav: 30 s run; " + ", ".join(cues))
    if want("hope"):
        for num, slug, title, fn in COLOURED:
            mix, host, y, region = neutral_hope(fn)
            write(os.path.join(OUT, f"{num}-{slug}.hope.loop.wav"), y)
            after = compare(host, y, region)
            old_plain, old_hope = os.path.join(OUT, f"{num}-{slug}.loop.wav"), os.path.join(OUT, "round3", f"{num}-{slug}.hope.loop.wav")
            before = compare(read_wav(old_plain), read_wav(old_hope), region) if os.path.exists(old_hope) else None
            fmt = lambda c: (f"integrated {c['integrated']:+.1f} LU; while hope plays {c['in_med']:+.1f} LU typical, {c['in_p95']:.1f} at the 95th "
                             f"percentile, {c['in_max']:.1f} worst; elsewhere {c['out_med']:+.1f} typical, {c['out_p95']:.1f} p95, {c['out_max']:.1f} worst")
            note(lines, f"{num}-{slug}.hope.loop.wav: against the plain song, BEFORE (round 3): {fmt(before) if before else 'n/a'} | "
                        f"AFTER (round 4): {fmt(after)}")
    if want("despair"):
        for num, slug, title, fn in COLOURED:
            for level in DESPAIR_LEVELS:
                mix, x, xd = despair_version(fn, level)
                _, _, _, g = master(x, True)
                y, got, peak, _ = master(xd, True, gain=g)
                write(os.path.join(OUT, f"{num}-{slug}.despair-{level}.loop.wav"), y)
                plain, _, _, _ = master(x, True)
                c = compare(plain, y, (mix.sec(mix.bar(4)), mix.sec(mix.bar(mix.bars - 4))))
                note(lines, f"{num}-{slug}.despair-{level}.loop.wav: {level}, bars 5 to {mix.bars - 4}, at the plain song's gain; "
                            f"{got:.1f} LUFS (the plain song -16.0), {c['in_med']:+.1f} LU while despair plays, true peak {peak:.1f} dBFS, seam {seam(y):.2f}")
        mix0, _ = dream_theme()
        t0 = mix0.sec(mix0.bar(4))
        ramp = lambda t: np.clip((t - t0) / 30.0, 0, 1)
        mix, x, xd = despair_version(dream_theme, ramp=ramp)
        _, _, _, g = master(x, True)
        y, _, peak, _ = master(xd, True, gain=g)
        a, b = int((t0 - 5) * SR), int((t0 + 35) * SR)
        write(os.path.join(OUT, "01-dream-theme.despair-ramp.wav"), ends(y[:, a:b].copy(), 0.5, 2.0))
        note(lines, f"01-dream-theme.despair-ramp.wav: 40 s, plain for 5 s, then faint to full over 30 s ({t0 - 5:.1f}-{t0 + 35:.1f} s of the loop)")
    if lines:
        with open(os.path.join(OUT, "loops-round4.txt"), "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    page()


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Music sketches</title>
<style>
:root {{ --bg:#f6f4ef; --fg:#222; --muted:#666; --card:#fff; --line:#e3ded3; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#17181b; --fg:#e8e6e1; --muted:#9a978f; --card:#212227; --line:#33343a; }} }}
body {{ background:var(--bg); color:var(--fg); font:16px/1.5 system-ui, sans-serif; margin:0; padding:24px 16px; }}
main {{ max-width:780px; margin:0 auto; }}
h1 {{ font-size:1.5rem; margin:0 0 4px; }} h2.round {{ font-size:1.2rem; margin:28px 0 10px; }}
p.lede {{ color:var(--muted); margin:0 0 20px; }}
.card {{ background:var(--card); border:1px solid var(--line); border-radius:10px; padding:14px 16px; margin:0 0 14px; }}
.card h3 {{ font-size:1.05rem; margin:0 0 2px; }} .card .meta {{ color:var(--muted); font-size:.9rem; margin:0 0 8px; }}
.row {{ display:grid; grid-template-columns: 9.5em 1fr; gap:8px; align-items:center; margin:4px 0; font-size:.9rem; }}
@media (max-width: 520px) {{ .row {{ grid-template-columns: 1fr; gap:2px; }} }}
audio {{ width:100%; }} a {{ color:inherit; }}
</style></head><body><main>
<h1>Visiting Hours: music sketches</h1>
<p class="lede">Example drafts made in code, to react to; the real pieces are composed together, step by step. Looping
pieces play twice so you can hear the seam. Notes: docs/MUSIC-SKETCHES.md. Round 4 first, then everything else.</p>
<h2 class="round">Round 4</h2>
{round4}
<h2 class="round">The pieces (approved or unchanged)</h2>
{same}
<h2 class="round">Earlier versions (to compare)</h2>
{old}
</main>
<script>
for (const a of document.querySelectorAll("audio")) {{ a.volume = 0.5; a.autoplay = false; }}
</script>
</body></html>
"""


def row(label, src):
    return f'<div class="row"><span>{label}</span><audio controls preload="none" src="{src}"></audio></div>'


def card(title, meta, rows):
    return f'<section class="card"><h3>{title}</h3><p class="meta">{meta}</p>{rows}</section>'


def page():
    have = lambda f: os.path.exists(os.path.join(OUT, f))
    doc = lambda fn: re.sub(r"\s+", " ", (fn.__doc__ or "").strip())
    r4 = []
    rows = ""
    for variant in CHASE_VARIANTS:
        if have(f"02-chase-{variant}.run.wav"):
            rows += row(f"{variant}: a 30 s run", f"02-chase-{variant}.run.wav")
    if rows:
        r4.append(card("The chase: a quick run through the speed thresholds",
                       "30 s each: running, fast at 4 s (the drive comes in on the next bar), flying at 11.5 s (the peak), "
                       "back to fast at 19 s, running at 25 s. The stems and the game's rules are unchanged; only the "
                       "demo's run is quicker. Spacey is new (analog synths, wide echoes).", rows))
    rows = ""
    for num, slug, title, fn in COLOURED:
        if have(f"{num}-{slug}.hope.loop.wav"):
            rows += f"<p class=\"meta\"><b>{title}</b></p>" + row("plain", f"{num}-{slug}.loop.wav") + row("with hope", f"{num}-{slug}.hope.loop.wav")
    if rows:
        r4.append(card("Hope, without changing the loudness",
                       "The song keeps the level it has alone; while hope plays, the mix rides a smooth gain so it's no "
                       "louder than the plain song (within about 1 LU; the numbers are in the doc).", rows))
    rows = ""
    if have("01-dream-theme.despair-ramp.wav"):
        rows += row("the ramp: faint to full in 30 s", "01-dream-theme.despair-ramp.wav")
    for num, slug, title, fn in COLOURED:
        if have(f"{num}-{slug}.despair-faint.loop.wav"):
            rows += f"<p class=\"meta\"><b>{title}</b></p>" + row("plain", f"{num}-{slug}.loop.wav")
            rows += "".join(row(lv, f"{num}-{slug}.despair-{lv}.loop.wav") for lv in DESPAIR_LEVELS)
    if rows:
        r4.append(card("Despair, as a feeling that grows (three levels)",
                       "Faint: the song sags slowly in pitch and darkens a little; far-off low tones; breath. Growing: the "
                       "low tones closer, a sigh every four bars, a detuned music box now and then. Full: sighs every two "
                       "bars and a low pedal (close to round 3's). The game crossfades between the levels. " + doc(despair_level_stem), rows))
    same = []
    if have("04-opening.wav"):
        same.append(card("The opening and the title screen", doc(opening_title) + " Unchanged since round 3; what changed from round 2 is in the doc.",
                         row("as the game plays it", "04-opening.wav") + row("the title loop alone", "04-opening.loop.wav")))
    for num, slug, title, fn in sorted(LOOPS + ONESHOTS):
        f = f"{num}-{slug}.twice.wav" if (num, slug, title, fn) in LOOPS else f"{num}-{slug}.wav"
        if have(f):
            same.append(card(title, doc(fn), row("heard twice" if f.endswith("twice.wav") else "plays once", f)))
    rows = ""
    for variant in CHASE_VARIANTS:
        if have(f"02-chase-{variant}.base.loop.wav"):
            rows += f"<p class=\"meta\"><b>{variant}</b></p>" + row("all stems", f"02-chase-{variant}.all.loop.wav")
            rows += "".join(row(f"{k} stem", f"02-chase-{variant}.{k}.loop.wav") for k in ("base", "drive", "peak"))
    if rows:
        same.insert(0, card("The chase's stems", doc(chase_stems), rows))
    old = []
    for label, f in (("Round 3: the chase run, grounded (96 s)", "round3/02-chase-grounded.run.wav"),
                     ("Round 3: despair over the Dream theme", "round3/01-dream-theme.despair.loop.wav"),
                     ("Round 3: hope over the Dream theme", "round3/01-dream-theme.hope.loop.wav"),
                     ("Round 2: the opening (played once)", "round2/04-opening.wav"), ("Round 2: Elias's motif", "round2/07-elias.twice.wav"),
                     ("Round 2: an argument battle (111 s)", "round2/05-argument-battle.twice.wav")):
        if have(f):
            old.append(card(label, "", row("listen", f)))
    with open(os.path.join(OUT, "index.html"), "w", encoding="utf-8") as fh:
        fh.write(PAGE.format(round4="\n".join(r4), same="\n".join(same), old="\n".join(old)))
    print("page", os.path.join(OUT, "index.html"))


if __name__ == "__main__":
    main()
