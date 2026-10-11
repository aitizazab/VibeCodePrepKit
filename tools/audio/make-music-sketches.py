# Visiting Hours: example music sketches, made in code, as starting points to react to. The real pieces are
# composed with the creator, step by step (docs/MUSIC-SKETCHES.md explains each sketch and how to change it).
#   uv run --with numpy python tools/audio/make-music-sketches.py [out dir] [piece ...]
# Writes <out>/<nn>-<piece>.wav (stereo, 44.1 kHz, about -16 LUFS, true peak under -1 dBFS) and index.html.
import os
import re
import subprocess
import sys

import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].isidentifier() else os.path.join(ROOT, "out", "vh-music-sketches")
ONLY = [a for a in sys.argv[1:] if a.isidentifier()]
RNG = np.random.default_rng(7)

# ---------------------------------------------------------------- the shared material

# The chords of the dream (the movement base, MoveMusicSynth): i-VI-III-VII in D minor, add9 colour.
DREAM_CHORDS = [[50, 53, 57, 64], [46, 50, 53, 57], [53, 57, 60, 67], [48, 50, 55, 64]]
# THE LEITMOTIF ("the street's song"): 4 bars over those chords, (midi, beats); None is a rest. It climbs a fourth
# (A to D), reaches for F, leans on E over Bb (a dreamy raised 4th) and comes to rest on D over C (the add9).
MOTIF = [(69, 1.5), (74, .5), (76, 1), (77, 1), (76, 1.5), (74, .5), (74, 2),
         (72, 1), (77, 1), (76, 1), (72, 1), (74, 3), (None, 1)]
# Its answer (the second four bars): the same start, then it climbs higher and walks home.
ANSWER = [(69, 1.5), (74, .5), (76, 1), (77, 1), (79, 1.5), (77, .5), (76, 2),
          (77, 1), (76, 1), (74, 1), (72, 1), (74, 4)]


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


# ---------------------------------------------------------------- the mix

class Mix:
    def __init__(self, seconds, bpm, beats_per_bar=4):
        self.n = int(seconds * SR)
        self.bpm, self.bpb = bpm, beats_per_bar
        self.dry = np.zeros((2, self.n))
        self.send = np.zeros((2, self.n))
        self.pumped = np.zeros((2, self.n))
        self.kicks = []

    def sec(self, beat):
        return beat * 60.0 / self.bpm

    def bar(self, b):
        return b * self.bpb

    def add(self, x, beat, pan=0.0, gain=1.0, verb=0.2, pump=False, at_sec=None):
        i = int(round((self.sec(beat) if at_sec is None else at_sec) * SR))
        if i >= self.n or len(x) == 0:
            return
        x = x[: self.n - i] * gain
        l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
        bus = self.pumped if pump else self.dry
        bus[0, i:i + len(x)] += x * l
        bus[1, i:i + len(x)] += x * r
        if verb > 0:
            self.send[0, i:i + len(x)] += x * l * verb
            self.send[1, i:i + len(x)] += x * r * verb

    def render(self, rt60=2.2, pump_depth=0.55, tail=0.0):
        t = np.arange(self.n) / SR
        g = np.ones(self.n)
        for k in self.kicks:
            dt = t - k
            m = dt >= 0
            g[m] = np.minimum(g[m], 1 - pump_depth * np.exp(-dt[m] / 0.11))
        out = self.dry + self.pumped * g + reverb(self.send, rt60)
        return out


def reverb(x, rt60):
    """A plate-ish reverb: convolution with decaying, darkening stereo noise (zero-phase, offline)."""
    n_ir = int(rt60 * SR)
    t = np.arange(n_ir) / SR
    out = np.zeros_like(x)
    for ch in range(2):
        ir = RNG.standard_normal(n_ir) * np.exp(-6.9 * t / rt60)
        ir = lowpass(ir, 6000) * 0.08
        ir[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))  # a small pre-delay feel
        size = 1 << int(np.ceil(np.log2(x.shape[1] + n_ir)))
        y = np.fft.irfft(np.fft.rfft(x[ch], size) * np.fft.rfft(ir, size), size)[: x.shape[1]]
        out[ch] = y
    return out


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


# ---------------------------------------------------------------- helpers to write parts

def chords(mix, prog, bars, start_bar=0, fn=None, gain=0.15, beats=None, verb=0.35, pump=False, octave=0, pan_spread=0.5):
    fn = fn or (lambda m, d: pad(m, d))
    beats = beats or mix.bpb
    for b in range(bars):
        ch = prog[b % len(prog)]
        for j, m in enumerate(ch):
            pan = (j / max(1, len(ch) - 1) - 0.5) * pan_spread * 2
            mix.add(fn(m + octave, mix.sec(beats)), mix.bar(start_bar + b), pan, gain, verb, pump)


def melody(mix, notes, fn, gain=0.3, pan=0.0, verb=0.3, legato=0.95, pump=False):
    for beat, m, d in notes:
        mix.add(fn(m, mix.sec(d) * legato), beat, pan, gain, verb, pump)


def four_floor(mix, start_bar, bars, level=0.9, clap_level=0.5, hats=0.5, offhat=True):
    for b in range(start_bar * mix.bpb, (start_bar + bars) * mix.bpb):
        mix.add(kick(level), b, 0, 1, 0.02)
        mix.kicks.append(mix.sec(b))
        if clap_level and b % 2 == 1:
            mix.add(clap(clap_level), b, 0.05, 1, 0.25)
        if hats:
            mix.add(hat(hats * (1.0 if offhat else 0.6)), b + 0.5, 0.25, 1, 0.05)
            mix.add(hat(hats * 0.45), b, -0.2, 1, 0.05)


def bassline(mix, prog, start_bar, bars, fn=None, pattern=(0.5, 1.5, 2.5, 3.5), octave=-24, gain=0.35, length=0.45):
    fn = fn or (lambda m, d: pluck(m, d, 8, 6))
    for b in range(bars):
        root = prog[b % len(prog)][0]
        for p in pattern:
            mix.add(fn(root + octave + 12, mix.sec(length)), mix.bar(start_bar + b) + p, 0, gain, 0.05, False)


def subline(mix, prog, start_bar, bars, gain=0.4):
    for b in range(bars):
        mix.add(sub(prog[b % len(prog)][0] - 12, mix.sec(mix.bpb) * 0.97), mix.bar(start_bar + b), 0, gain, 0.0, True)


def arp(mix, prog, start_bar, bars, fn, gain=0.12, step=0.25, octave=24, order=(0, 1, 2, 3, 2, 3, 1, 2), pan=0.3):
    k = 0
    for b in range(bars):
        ch = prog[b % len(prog)]
        for s in range(int(mix.bpb / step)):
            m = ch[order[k % len(order)]] + octave
            mix.add(fn(m), mix.bar(start_bar + b) + s * step, pan * (1 if k % 2 else -1), gain * (1.0 if s % 4 == 0 else 0.75), 0.35)
            k += 1


def open_up(x, start_s, end_s, fc_closed=500):
    """The music opens: a muffled copy crossfades to the full one between two times."""
    muffled = np.vstack([lowpass(x[0], fc_closed), lowpass(x[1], fc_closed)])
    t = np.arange(x.shape[1]) / SR
    w = np.clip((t - start_s) / max(1e-3, end_s - start_s), 0, 1)[None, :]
    return muffled * (1 - w) + x * w


def fade(x, fin=0.5, fout=3.0):
    a, b = int(fin * SR), int(fout * SR)
    x[:, :a] *= np.linspace(0, 1, a)
    x[:, -b:] *= np.linspace(1, 0, b) ** 1.5
    return x


# ---------------------------------------------------------------- the pieces

def dream_theme():
    """104 bpm, D minor, melodic house (the 'Faded' era): the leitmotif on a plucked supersaw over pumping pads."""
    bpm, bars = 104, 18
    mix = Mix(bars * 4 * 60 / bpm + 4, bpm)
    prog = DREAM_CHORDS
    # Intro (4 bars): felt piano plays the motif over the pad, muffled, opening up.
    chords(mix, prog, 4, 0, lambda m, d: warm_pad(m, d), 0.16, verb=0.4)
    melody(mix, seq(MOTIF, 0), lambda m, d: felt_piano(m, d), 0.5, -0.1, 0.45)
    # A (8 bars): the groove; the motif and its answer on the lead; pads pump with the kick.
    chords(mix, prog, 8, 4, lambda m, d: pad(m, d, 10), 0.11, verb=0.3, pump=True)
    subline(mix, prog, 4, 8, 0.38)
    bassline(mix, prog, 4, 8, gain=0.22)
    four_floor(mix, 4, 8, 0.85, 0.45, 0.5)
    melody(mix, seq(MOTIF, mix.bar(4)) + seq(ANSWER, mix.bar(8)), lambda m, d: supersaw(m, d), 0.16, 0.0, 0.35)
    melody(mix, seq(MOTIF, mix.bar(4), -12) + seq(ANSWER, mix.bar(8), -12), lambda m, d: piano(m, d, 0.7), 0.18, -0.2, 0.3)
    # B (4 bars): the movement's peak arpeggio and the music box double the motif an octave up.
    chords(mix, prog, 4, 12, lambda m, d: pad(m, d, 12), 0.12, verb=0.35, pump=True)
    subline(mix, prog, 12, 4, 0.38)
    bassline(mix, prog, 12, 4, gain=0.22)
    four_floor(mix, 12, 4, 0.85, 0.45, 0.6)
    arp(mix, prog, 12, 4, lambda m: pluck(m, 0.25, 10, 7), 0.07)
    melody(mix, seq(ANSWER, mix.bar(12)), lambda m, d: supersaw(m, d), 0.16, 0, 0.35)
    melody(mix, seq(ANSWER, mix.bar(12), 12), lambda m, d: music_box(m), 0.16, 0.3, 0.5)
    # Outro (2 bars): the pad and one last piano D.
    chords(mix, prog[:1], 2, 16, lambda m, d: warm_pad(m, d * 1.6, release=2.5), 0.14, verb=0.5)
    mix.add(felt_piano(74, 4), mix.bar(16), 0, 0.5, 0.6)
    x = mix.render(2.4, 0.5)
    x = open_up(x, mix.sec(mix.bar(1)), mix.sec(mix.bar(4)), 450)
    return fade(x, 0.2, 3.5)


def chase():
    """128 bpm, D minor: the leitmotif in double time over a driving off-beat bass, toms and a detuned music box."""
    bpm, bars = 128, 24
    mix = Mix(bars * 4 * 60 / bpm + 3, bpm)
    prog = [[50, 53, 57, 62], [46, 50, 53, 58], [48, 52, 55, 60], [45, 49, 52, 57]]  # Dm Bb C A: i-VI-VII-V
    fast = [(m, d * 0.5) for m, d in MOTIF[:7]]  # the motif's first two bars, twice as fast
    # Intro (4): bass, hats, a riser.
    bassline(mix, prog, 0, 4, lambda m, d: pluck(m, d, 10, 9), (0.5, 1.5, 2.5, 3.5, 1.0, 3.0), gain=0.25, length=0.3)
    for b in range(16):
        mix.add(hat(0.5), b + 0.5, 0.2, 1, 0.05)
    mix.add(riser(mix.sec(8), 0.35), mix.bar(2), 0, 1, 0.2)
    # A (8): the full drive; the fast motif on a staccato lead, twice per 2 bars.
    for start in (4, 12):
        four_floor(mix, start, 8, 0.95, 0.5, 0.6)
        subline(mix, prog, start, 8, 0.36)
        bassline(mix, prog, start, 8, lambda m, d: pluck(m, d, 12, 9), (0.5, 1.5, 2.5, 3.5, 1.75, 3.75), gain=0.25, length=0.25)
        chords(mix, prog, 8, start, lambda m, d: pad(m, d, 10), 0.08, pump=True)
        for k in range(4):
            melody(mix, seq(fast, mix.bar(start + 2 * k)), lambda m, d: supersaw(m, d, 4), 0.14, 0.0, 0.25, 0.8)
        for b in range(start, start + 8):
            mix.add(tom(43, 0.5), mix.bar(b) + 3.5, -0.3, 1, 0.15)
            mix.add(tom(38, 0.5), mix.bar(b) + 3.75, 0.3, 1, 0.15)
    # B adds the arpeggio and the music box, out of tune (the static is close).
    arp(mix, prog, 12, 8, lambda m: pluck(m, 0.2, 10, 9), 0.06, 0.25)
    melody(mix, seq(MOTIF, mix.bar(12), 12) + seq(ANSWER, mix.bar(16), 12),
           lambda m, d: music_box(m, 1.0, cents=-25), 0.14, 0.35, 0.5)
    mix.add(riser(mix.sec(4), 0.3), mix.bar(19), 0, 1, 0.2)
    # End (4): an impact, the pad and the motif's first notes, alone.
    mix.add(impact(1.0), mix.bar(20), 0, 0.9, 0.5)
    chords(mix, prog[:1], 2, 20, lambda m, d: pad(m, d * 2, 12, release=2.5), 0.14, verb=0.5)
    melody(mix, seq(MOTIF[:4], mix.bar(20)), lambda m, d: music_box(m, 1.5, cents=-30), 0.2, 0.2, 0.6)
    x = mix.render(1.8, 0.45)
    return fade(x, 0.05, 3.0)


def daytime():
    """92 bpm, F major: the same leitmotif in daylight, over the same four chords turned round (F C Dm Bb)."""
    bpm, bars = 92, 16
    mix = Mix(bars * 4 * 60 / bpm + 3, bpm)
    prog = [[53, 57, 60, 64], [48, 52, 55, 62], [50, 53, 57, 64], [46, 50, 53, 57]]  # Fmaj7 Cadd9 Dm(add9) Bbmaj7
    # Piano comping in bouncy eighths, a plucked "guitar" arpeggio, a light groove.
    for b in range(bars):
        ch = prog[b % 4]
        for s in (0, 1.5, 2, 3.5):
            for m in ch[1:]:
                mix.add(piano(m, mix.sec(0.4), 0.45), mix.bar(b) + s, -0.25, 0.12, 0.2)
        mix.add(piano(ch[0] - 12, mix.sec(1.8), 0.6), mix.bar(b), -0.1, 0.2, 0.15)
        mix.add(piano(ch[0] - 12, mix.sec(1.5), 0.5), mix.bar(b) + 2, -0.1, 0.16, 0.15)
        if b >= 4:
            for beat in range(4):
                if beat % 2 == 0:
                    mix.add(kick(0.55, 0.7), mix.bar(b) + beat, 0, 1, 0.02)
                else:
                    mix.add(clap(0.3), mix.bar(b) + beat, 0.1, 1, 0.2)
                mix.add(shaker(0.7), mix.bar(b) + beat + 0.5, 0.4, 1, 0.05)
                mix.add(shaker(0.4), mix.bar(b) + beat + 0.25, 0.4, 1, 0.05)
            mix.add(sub(ch[0] - 24, mix.sec(3.8)), mix.bar(b), 0, 0.25, 0)
    arp(mix, prog, 4, 12, lambda m: pluck(m, 0.35, 9, 5), 0.07, 0.5, 12, (0, 2, 1, 3, 2, 1, 3, 2), 0.5)
    # The leitmotif on marimba, then whistled-sounding celesta for the answer, then both.
    melody(mix, seq(MOTIF, mix.bar(4)), lambda m, d: marimba(m, 0.6), 0.35, 0.15, 0.3)
    melody(mix, seq(ANSWER, mix.bar(8)), lambda m, d: celesta(m), 0.3, 0.1, 0.35)
    melody(mix, seq(MOTIF, mix.bar(12)), lambda m, d: marimba(m, 0.6), 0.3, 0.15, 0.3)
    melody(mix, seq(MOTIF, mix.bar(12), 12), lambda m, d: celesta(m), 0.16, -0.2, 0.35)
    x = mix.render(1.4, 0.0)
    return fade(x, 0.05, 3.0)


def opening():
    """72 bpm, D minor: night, the phone. Felt piano, a drone and glass; the motif's first half, left unresolved."""
    bpm, bars = 72, 12
    mix = Mix(bars * 4 * 60 / bpm + 5, bpm)
    prog = [DREAM_CHORDS[0], DREAM_CHORDS[1], DREAM_CHORDS[0], DREAM_CHORDS[1], DREAM_CHORDS[2], DREAM_CHORDS[3]]
    # A drone (D and A) all through, and soft held chords.
    mix.add(warm_pad(38, mix.sec(bars * 4), 3, 4) + warm_pad(45, mix.sec(bars * 4), 3, 4), 0, 0, 0.18, 0.4)
    chords(mix, prog, 12, 0, lambda m, d: warm_pad(m, d, 1.2, 2.0), 0.09, verb=0.6)
    # The motif's first half, very slow, a note at a time, with space; the second time it stops on E.
    first = seq(MOTIF[:4], mix.bar(2), 0, 1.5)
    second = seq(MOTIF[:5], mix.bar(6), 0, 1.5)
    melody(mix, first + second, lambda m, d: felt_piano(m, d + 2, 0.6), 0.6, 0.0, 0.7, 1.0)
    # Glass: high, quiet, slow (two notes, far apart).
    for b, m in ((4, 88), (9, 86)):
        mix.add(partials(hz(m), 5, [1, .1], [0.5, 1.5], 1.0, 2.0), mix.bar(b), 0.4, 0.05, 0.8)
    # The last chord holds, no resolution.
    mix.add(felt_piano(64, 6, 0.4), mix.bar(11), -0.2, 0.35, 0.7)
    x = mix.render(3.5, 0.0)
    x = open_up(x, 0, mix.sec(mix.bar(4)), 700)
    return fade(x, 2.0, 4.0)


def argument():
    """138 bpm, D minor: an argument as a battle. Staccato strings, toms, brass stabs; the motif turned upside down."""
    bpm, bars = 138, 24
    mix = Mix(bars * 4 * 60 / bpm + 3, bpm)
    prog = [[50, 53, 57], [46, 50, 53], [43, 46, 50], [45, 49, 52]]  # Dm Bb Gm A: i-VI-iv-V
    # The ostinato: sixteenths on the root, octave and fifth.
    for b in range(bars):
        r = prog[b % 4][0]
        pat = [r, r + 12, r + 7, r + 12, r, r + 12, r + 7, r + 10, r, r + 12, r + 7, r + 12, r, r + 7, r + 12, r + 7]
        for s, m in enumerate(pat):
            mix.add(staccato(m, 0.09), mix.bar(b) + s * 0.25, -0.35 if s % 2 else 0.35, 0.16 * (1.0 if s % 4 == 0 else 0.7), 0.15)
        if b >= 4:
            for beat in range(4):
                mix.add(tom(38 if beat % 2 == 0 else 43, 0.7), mix.bar(b) + beat, 0, 1, 0.2)
            mix.add(snare(0.5), mix.bar(b) + 1, 0, 1, 0.3)
            mix.add(snare(0.5), mix.bar(b) + 3, 0, 1, 0.3)
            mix.add(sub(r - 12, mix.sec(3.9)), mix.bar(b), 0, 0.35, 0)
        if b >= 8 and b % 2 == 0:
            for m in prog[b % 4]:
                mix.add(brass(m + 12, mix.sec(0.6)), mix.bar(b), 0, 0.14, 0.3)
                mix.add(brass(m + 12, mix.sec(0.3)), mix.bar(b) + 2.5, 0, 0.12, 0.3)
    # The motif inverted (it falls where the dream's rises), on strings, high and urgent.
    inv = [(74 - (m - 69), d) if m is not None else (None, d) for m, d in MOTIF]
    melody(mix, seq(inv, mix.bar(8), 12), lambda m, d: strings(m, d, 0.05, 0.3, 14), 0.2, 0.1, 0.35)
    melody(mix, seq(inv, mix.bar(16), 12) , lambda m, d: strings(m, d, 0.05, 0.3, 14), 0.2, 0.1, 0.35)
    melody(mix, seq(inv, mix.bar(16), 0), lambda m, d: brass(m, d), 0.12, -0.1, 0.3)
    mix.add(impact(1.0), mix.bar(bars - 1) + 3.5, 0, 0.8, 0.5)
    x = mix.render(1.6, 0.0)
    return fade(x, 0.05, 2.5)


def lena():
    """84 bpm in 3/4, Bb major (the dream's warm VI chord): a waltz on celesta, nylon plucks and a soft pad."""
    bpm, bars = 84, 16
    mix = Mix(bars * 3 * 60 / bpm + 4, bpm, 3)
    prog = [[46, 50, 53, 57], [43, 46, 50, 53], [39, 43, 46, 50], [41, 45, 48, 53]]  # Bbmaj7 Gm7 Ebmaj7 F
    # Her motif: a rising sixth (F to D, landing on the dream's home note), then a gentle walk down.
    motif = [(65, 1), (74, 2), (72, 1), (70, 1), (69, 1), (67, 2), (70, 1), (69, 3),
             (65, 1), (74, 2), (75, 1), (74, 1), (72, 1), (70, 1), (67, 1), (72, 1), (70, 3)]
    chords(mix, prog, bars, 0, lambda m, d: warm_pad(m, d, 0.6, 1.0), 0.09, verb=0.4)
    for b in range(bars):
        ch = prog[b % 4]
        mix.add(pluck(ch[0] - 12, mix.sec(2.5), 8, 2.5), mix.bar(b), -0.2, 0.25, 0.2)
        for beat in (1, 2):
            for m in ch[1:3]:
                mix.add(pluck(m + 12, mix.sec(0.8), 9, 3.5), mix.bar(b) + beat, 0.25, 0.09, 0.25)
    melody(mix, seq(motif, mix.bar(2)), lambda m, d: celesta(m, 1.6), 0.4, 0.1, 0.45)
    melody(mix, seq(motif, mix.bar(10)), lambda m, d: celesta(m, 1.6), 0.35, 0.1, 0.45)
    melody(mix, seq(motif, mix.bar(10), -12), lambda m, d: strings(m, d, 0.3, 0.6, 10), 0.12, -0.25, 0.4)
    x = mix.render(2.2, 0.0)
    return fade(x, 0.3, 3.5)


def elias():
    """66 bpm, D minor: low strings and piano. His figure falls and stops on E, the unresolved note (the apology
    nobody heard); the dream's leitmotif answers faintly on a music box (the dream is his)."""
    bpm, bars = 66, 12
    mix = Mix(bars * 4 * 60 / bpm + 5, bpm)
    prog = [[50, 53, 57], [46, 50, 53], [43, 46, 50], [45, 49, 52, 55]]  # Dm Bb Gm A7
    motif = [(62, 2), (57, 1), (60, 1), (58, 2), (57, 1), (55, 1), (53, 1.5), (55, .5), (57, 2), (52, 4)]
    chords(mix, prog, bars, 0, lambda m, d: strings(m - 12, d, 0.8, 1.0, 10), 0.07, verb=0.4)
    for b in range(bars):
        mix.add(piano(prog[b % 4][0] - 12, mix.sec(3.5), 0.5), mix.bar(b), -0.2, 0.22, 0.4)
    melody(mix, seq(motif, mix.bar(0), -12), lambda m, d: cello(m, d), 0.4, -0.15, 0.35)
    melody(mix, seq(motif, mix.bar(4), -12), lambda m, d: cello(m, d), 0.4, -0.15, 0.35)
    melody(mix, seq(motif, mix.bar(4), 0), lambda m, d: piano(m, d, 0.5), 0.2, 0.2, 0.45)
    # The answer: the dream's motif, first half, faint and high.
    melody(mix, seq(MOTIF[:7], mix.bar(8), 12), lambda m, d: music_box(m, 1.6), 0.13, 0.35, 0.7)
    melody(mix, seq(motif[:6], mix.bar(8), -12), lambda m, d: cello(m, d), 0.3, -0.15, 0.4)
    mix.add(cello(40, mix.sec(8)), mix.bar(10), -0.1, 0.35, 0.4)
    x = mix.render(3.0, 0.0)
    return fade(x, 1.0, 4.5)


def ending():
    """80 bpm, D minor turning to D major: the leitmotif in full on piano and strings, the music box in tune, and a
    last cadence Bb - C - D major (the minor key finally opens)."""
    bpm, bars = 80, 14
    mix = Mix(bars * 4 * 60 / bpm + 6, bpm)
    prog = DREAM_CHORDS
    chords(mix, prog, 12, 0, lambda m, d: strings(m, d, 0.8, 1.2, 12), 0.08, verb=0.5)
    chords(mix, prog, 12, 0, lambda m, d: choir(m + 12, d), 0.035, verb=0.6)
    for b in range(12):
        ch = prog[b % 4]
        for s, j in zip((0, 1, 2, 3), (0, 2, 1, 3)):
            mix.add(piano(ch[j], mix.sec(1.5), 0.45), mix.bar(b) + s, -0.25, 0.12, 0.4)
        mix.add(sub(ch[0] - 12, mix.sec(3.9)), mix.bar(b), 0, 0.22, 0)
    melody(mix, seq(MOTIF, mix.bar(0)), lambda m, d: piano(m, d, 0.75), 0.32, 0.1, 0.45)
    melody(mix, seq(ANSWER, mix.bar(4)), lambda m, d: strings(m, d, 0.2, 0.6, 14), 0.2, 0.0, 0.4)
    melody(mix, seq(MOTIF, mix.bar(8)), lambda m, d: piano(m, d, 0.75), 0.3, 0.1, 0.45)
    melody(mix, seq(MOTIF, mix.bar(8), 12), lambda m, d: music_box(m, 1.6), 0.13, 0.3, 0.6)
    # The cadence: Bb, C, then D major (F# instead of F), held.
    for b, ch in ((12, [46, 53, 58, 62, 65]), (12.5, [48, 55, 60, 64, 67]), (13, [38, 50, 57, 62, 66, 69])):
        for m in ch:
            mix.add(strings(m, mix.sec(2 if b < 13 else 8), 0.3, 2.5, 12), mix.bar(b), 0, 0.09, 0.5)
            mix.add(piano(m, mix.sec(2 if b < 13 else 6), 0.5), mix.bar(b), -0.1, 0.1, 0.5)
    mix.add(choir(74, mix.sec(8)) + choir(78, mix.sec(8)), mix.bar(13), 0, 0.05, 0.6)
    mix.add(music_box(78, 3), mix.bar(13) + 1, 0.3, 0.15, 0.7)
    x = mix.render(3.2, 0.0)
    return fade(x, 0.5, 5.0)


PIECES = [
    ("dream-theme", "The Dream theme", dream_theme),
    ("chase", "The chase (the Dream theme, running)", chase),
    ("daytime", "The Daytime theme", daytime),
    ("opening", "The opening", opening),
    ("argument-battle", "An argument battle", argument),
    ("lena", "Lena's motif", lena),
    ("elias", "Elias's motif", elias),
    ("ending", "The ending reprise", ending),
]


# ---------------------------------------------------------------- mastering and the page

def master(x, path, lufs=-16.0):
    tmp = path + ".raw.f32"
    x.T.astype(np.float32).tofile(tmp)
    base = ["ffmpeg", "-v", "info", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", tmp]

    def measure(gain_db):
        r = subprocess.run(base + ["-af", f"volume={gain_db:.2f}dB,alimiter=limit=0.84:attack=3:release=60:level=false,ebur128=peak=true",
                                   "-f", "null", "-"], capture_output=True, text=True, errors="replace").stderr
        return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", r)[-1]), float(re.findall(r"Peak:\s+(-?[\d.]+) dBFS", r)[-1])

    gain = 0.0
    for _ in range(3):
        got, peak = measure(gain)
        gain += lufs - got
    got, peak = measure(gain)
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", tmp, "-af",
                    f"volume={gain:.2f}dB,alimiter=limit=0.84:attack=3:release=60:level=false", "-c:a", "pcm_s16le", path], check=True)
    os.remove(tmp)
    return got, peak


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Music sketches</title>
<style>
:root {{ --bg:#f6f4ef; --fg:#222; --muted:#666; --card:#fff; --line:#e3ded3; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#17181b; --fg:#e8e6e1; --muted:#9a978f; --card:#212227; --line:#33343a; }} }}
body {{ background:var(--bg); color:var(--fg); font:16px/1.5 system-ui, sans-serif; margin:0; padding:24px 16px; }}
main {{ max-width:760px; margin:0 auto; }}
h1 {{ font-size:1.5rem; margin:0 0 4px; }} p.lede {{ color:var(--muted); margin:0 0 20px; }}
.card {{ background:var(--card); border:1px solid var(--line); border-radius:10px; padding:14px 16px; margin:0 0 14px; }}
.card h2 {{ font-size:1.1rem; margin:0 0 2px; }} .card .meta {{ color:var(--muted); font-size:.9rem; margin:0 0 8px; }}
audio {{ width:100%; }}
</style></head><body><main>
<h1>Visiting Hours: music sketches</h1>
<p class="lede">Example drafts made in code, starting points to react to. The real pieces are composed together, step by
step. Notes on each (key, tempo, instruments, the shared motif, what to change): docs/MUSIC-SKETCHES.md.</p>
{cards}
</main>
<script>
for (const a of document.querySelectorAll("audio")) {{ a.volume = 0.5; a.autoplay = false; }}
</script>
</body></html>
"""


def main():
    os.makedirs(OUT, exist_ok=True)
    cards = []
    for i, (slug, title, fn) in enumerate(PIECES, 1):
        name = f"{i:02d}-{slug}.wav"
        path = os.path.join(OUT, name)
        if not ONLY or slug.replace("-", "_") in ONLY or fn.__name__ in ONLY:
            x = fn()
            got, peak = master(x, path)
            print(f"{name:28s} {x.shape[1] / SR:5.1f} s  {got:.1f} LUFS  true peak {peak:.1f} dBFS", flush=True)
        if os.path.exists(path):
            secs = os.path.getsize(path) / (SR * 4)
            doc = (fn.__doc__ or "").strip().replace("\n", " ")
            doc = re.sub(r"\s+", " ", doc)
            cards.append(f'<section class="card"><h2>{i}. {title}</h2><p class="meta">{doc} ({secs:.0f} s)</p>'
                         f'<audio controls preload="none" src="{name}"></audio></section>')
    with open(os.path.join(OUT, "index.html"), "w", encoding="utf-8") as f:
        f.write(PAGE.format(cards="\n".join(cards)))
    print("page", os.path.join(OUT, "index.html"))


if __name__ == "__main__":
    main()
