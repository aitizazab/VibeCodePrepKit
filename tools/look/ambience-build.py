"""The golden-hour Saturday soundscape for the dream's hill (Visiting Hours, Look pass 2).

    uv run --with numpy --with scipy --with pyloudnorm python -I tools/look/ambience-build.py <previews-dir> <out-dir> [--work <tmp-dir>]

<previews-dir> holds the Freesound HQ previews named <id>.mp3 (all CC0; ids, authors and URLs are in SOURCES below and
are written to <out-dir>/SOURCES.md). Each clip is cut, cleaned (high/low-pass, a soft downward expander on the bird
recordings), pushed into the distance where it belongs (low-pass plus a synthetic diffuse tail), looped with a
crossfade at the loop point, normalised to a loudness target (pyloudnorm, ITU-R BS.1770) and encoded to Ogg Vorbis
44.1 kHz with ffmpeg. The script then decodes every .ogg and prints a check table: LUFS, sample peak, and for loops the
jump at the seam against the clip's own 99.9th-percentile sample step. Re-runnable: it writes into <work> and only
then replaces <out-dir>'s .ogg files.
"""
import os
import shutil
import subprocess
import sys

import numpy as np
import pyloudnorm
from scipy import signal

SR = 44100
RNG = np.random.default_rng(1990)

# id: (author, title, page url)
SOURCES = {
    "702475": ("RyanKingArt", "Summer Evening Ambience in the Tennessee Suburbs 1", "https://freesound.org/people/RyanKingArt/sounds/702475/"),
    "702476": ("RyanKingArt", "Summer Evening Ambience in the Tennessee Suburbs 2", "https://freesound.org/people/RyanKingArt/sounds/702476/"),
    "636070": ("coalcon", "American Robin in the Yard", "https://freesound.org/people/coalcon/sounds/636070/"),
    "512825": ("deleted_user_11205508", "American robin 1", "https://freesound.org/people/deleted_user_11205508/sounds/512825/"),
    "171463": ("kvgarlic", "SongSparrowSongMarch52012.wav", "https://freesound.org/people/kvgarlic/sounds/171463/"),
    "512805": ("deleted_user_11205508", "House sparrows", "https://freesound.org/people/deleted_user_11205508/sounds/512805/"),
    "456930": ("nathankwright", "mourning_dove _2017-06-06.wav", "https://freesound.org/people/nathankwright/sounds/456930/"),
    "399375": ("tbirch55", "Distant Straining Lawnmower", "https://freesound.org/people/tbirch55/sounds/399375/"),
    "520886": ("pimstoltz", "Rotary sprinkler", "https://freesound.org/people/pimstoltz/sounds/520886/"),
    "832435": ("felix.blume", "Dog barking", "https://freesound.org/people/felix.blume/sounds/832435/"),
    "495658": ("aunrea", "Dog Bark", "https://freesound.org/people/aunrea/sounds/495658/"),
    "277058": ("kwahmah_02", "Single Dog Bark", "https://freesound.org/people/kwahmah_02/sounds/277058/"),
    "751473": ("Borgory", "Soft Wind in the Trees - Leaves rustle", "https://freesound.org/people/Borgory/sounds/751473/"),
    "546759": ("Nox_Sound", "Ambiance_Wind_Trees_Leaves_Moderate_Loop_Stereo.wav", "https://freesound.org/people/Nox_Sound/sounds/546759/"),
    "430924": ("SteeLManure", "little league baseball game", "https://freesound.org/people/SteeLManure/sounds/430924/"),
    "860786": ("bowlingballout", "Softball/Baseball Hit", "https://freesound.org/people/bowlingballout/sounds/860786/"),
    "851452": ("hashtagsmcgee", "Bat_hit2", "https://freesound.org/people/hashtagsmcgee/sounds/851452/"),
    "625473": ("jcookvoice", "American Baseball The Umpire.wav", "https://freesound.org/people/jcookvoice/sounds/625473/"),
    "102108": ("moxobna", "072510_distant_cheer.wav", "https://freesound.org/people/moxobna/sounds/102108/"),
    "128573": ("sndadv", "Ice Cream Truck - \"The Entertainer\"", "https://freesound.org/people/sndadv/sounds/128573/"),
}
USED = {}  # output file -> list of (id, what was taken)


# ---------------------------------------------------------------- basics

def load(sid, stereo=False):
    cmd = ["ffmpeg", "-v", "quiet", "-i", os.path.join(SRC, sid + ".mp3"), "-ac", "2" if stereo else "1", "-ar", str(SR),
           "-f", "f32le", "-"]
    x = np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, np.float32).astype(np.float64)
    return x.reshape(-1, 2).T.copy() if stereo else x


def cut(x, t0, t1):
    return x[..., int(t0 * SR):int(t1 * SR)].copy()


def filt(x, kind, f, order=4):
    sos = signal.butter(order, f, btype=kind, fs=SR, output="sos")
    return signal.sosfiltfilt(sos, x, axis=-1)


def shelf_high(x, f, gain_db):
    """High shelf as a parallel low-passed path: x + (g-1) * highpass(x)."""
    g = 10 ** (gain_db / 20)
    return x + (g - 1) * filt(x, "highpass", f, 2)


def fade(x, fin=0.005, fout=0.05):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        x[..., :a] *= np.sin(np.linspace(0, np.pi / 2, a)) ** 2
    if b:
        x[..., -b:] *= np.cos(np.linspace(0, np.pi / 2, b)) ** 2
    return x


def xfade_join(a, b, t=1.0):
    n = int(t * SR)
    w = np.linspace(0, np.pi / 2, n)
    mid = a[..., -n:] * np.cos(w) + b[..., :n] * np.sin(w)
    return np.concatenate([a[..., :-n], mid, b[..., n:]], axis=-1)


def make_loop(x, xf, power=True):
    """Seamless loop: the last xf seconds are crossfaded onto the start, so out[-1] runs straight into out[0]."""
    n = int(xf * SR)
    L = x.shape[-1] - n
    y = x[..., :L].copy()
    t = np.linspace(0, 1, n)
    fin, fout = (np.sin(t * np.pi / 2), np.cos(t * np.pi / 2)) if power else (t, 1 - t)
    y[..., :n] = x[..., :n] * fin + x[..., L:L + n] * fout
    return y


def expander(x, thr_db, floor_db=-12.0, ratio=2.0, side=None):
    """Soft downward expander (pulls the background down between calls, never below floor_db)."""
    s = x if side is None else side
    s = s if s.ndim == 1 else s.mean(0)
    h = int(0.03 * SR)
    lev = np.sqrt(signal.lfilter([1.0 / h] * 1, [1, -(1 - 1.0 / h)], s ** 2) + 1e-12)
    ldb = 20 * np.log10(lev)
    gdb = np.where(ldb < thr_db, (ldb - thr_db) * (ratio - 1), 0.0)
    gdb = np.maximum(gdb, floor_db)
    # smooth: fast attack (opening), slow release
    g = 10 ** (gdb / 20)
    sm = np.empty_like(g)
    a_open, a_close = np.exp(-1 / (0.005 * SR)), np.exp(-1 / (0.25 * SR))
    v = g[0]
    for i in range(len(g)):  # one pass; fine for clips of a minute
        c = a_open if g[i] > v else a_close
        v = c * v + (1 - c) * g[i]
        sm[i] = v
    sm = np.concatenate([sm[int(0.01 * SR):], np.full(int(0.01 * SR), sm[-1])])  # 10 ms look-ahead
    return x * sm


def tail_ir(rt60=1.2, predelay=0.03, lp=3000.0, seed=0):
    r = np.random.default_rng(seed)
    n = int(rt60 * SR)
    t = np.arange(n) / SR
    ir = r.standard_normal(n) * np.exp(-6.91 * t / rt60)
    ir = filt(ir, "lowpass", lp, 2)
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    return ir / np.sqrt((ir ** 2).sum())


def distance(x, lp=4000.0, hp=120.0, wet=0.3, rt60=1.2, seed=0):
    """Far away: lose the top and the very bottom, and add a diffuse tail (the yards and houses in between)."""
    d = filt(filt(x, "lowpass", lp, 2), "highpass", hp, 2)
    w = signal.fftconvolve(d, tail_ir(rt60, 0.03, lp * 0.7, seed))[: len(d)]
    return np.sqrt(1 - wet) * d + np.sqrt(wet) * w


def distance_oneshot(x, tail=1.2, **kw):
    """distance() for a one-shot, with room for its tail."""
    x = np.concatenate([x, np.zeros(int(tail * SR))])
    return distance(x, **kw)


def lufs(x):
    m = pyloudnorm.Meter(SR)
    d = x.T if x.ndim == 2 else x
    pad = max(0, int(0.8 * SR) - (d.shape[0]))
    if pad:
        d = np.concatenate([d, np.zeros((pad,) + d.shape[1:])])
    return m.integrated_loudness(d)


def limit(x, ceil, max_gr_db=8.0, look=0.005):
    """Look-ahead peak limiter (gain = a widened, smoothed minimum of ceil/|x|); takes at most max_gr_db off."""
    from scipy.ndimage import minimum_filter1d, uniform_filter1d
    a = np.abs(x) if x.ndim == 1 else np.abs(x).max(0)
    need = np.clip(ceil / (a + 1e-12), 10 ** (-max_gr_db / 20), 1.0)
    n = max(3, int(look * SR))
    g = uniform_filter1d(minimum_filter1d(need, 2 * n + 1), n)
    return x * g


def norm(x, target, peak_ceiling=-1.0):
    """Loudness to target LUFS; peaks over the ceiling go through the limiter (at most 8 dB), and only if that is not
    enough does the whole clip come down (so a clip can land a little under its target)."""
    ceil = 10 ** (peak_ceiling / 20)
    y = x
    for _ in range(4):
        y = y * 10 ** ((target - lufs(y)) / 20)
        if np.abs(y).max() <= ceil * 1.001:
            break
        y = limit(y, ceil * 0.97)
    pk = np.abs(y).max()
    if pk > ceil:
        y *= ceil / pk
    return y


def write(name, x, q=3, used=()):
    path_ogg = os.path.join(WORK, name + ".ogg")
    d = (x.T if x.ndim == 2 else x[:, None]).astype(np.float32)
    raw = os.path.join(WORK, name + ".f32")
    d.tofile(raw)
    ch = d.shape[1]
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(ch), "-i", raw,
                    "-c:a", "libvorbis", "-q:a", str(q), path_ogg], check=True)
    os.remove(raw)
    USED[name] = list(used)


# ---------------------------------------------------------------- the clips

def bed():
    a = cut(load("702475", True), 46, 127)                    # birds, crickets far off, a car or a plane now and then
    b6 = load("702476", True)
    b = xfade_join(cut(b6, 0, 38), cut(b6, 62, 107), 2.0)      # skips the one close car (38-62 s)
    n = min(a.shape[1], b.shape[1])
    x = 0.8 * a[:, :n] + 0.6 * b[:, :n]
    x = filt(x, "highpass", 70, 2)                             # keep the far hum, lose wind/handling rumble
    x = shelf_high(x, 6500, -4.0)                              # the crickets sit back: an evening, not a night
    x = make_loop(x, 3.0)
    write("VH_Amb_Bed_SaturdayEvening", norm(x, -30.0), q=4,
          used=[("702475", "46-127 s"), ("702476", "0-38 s and 62-107 s")])


def birds():
    r1 = filt(cut(load("636070"), 0.5, 27.3), "highpass", 1000, 4)
    r2 = filt(cut(load("512825"), 0.5, 52.0), "highpass", 1000, 4)
    r1 = expander(r1, thr_db=-50, floor_db=-10)
    r2 = expander(r2, thr_db=-58, floor_db=-10)
    r1, r2 = norm(r1, -24), norm(r2, -24)
    x = make_loop(xfade_join(r1, r2, 1.5), 1.5)
    write("VH_Amb_Birds_Robin_Loop", norm(x, -22.0), used=[("636070", "0.5-27.3 s"), ("512825", "0.5-52 s")])

    s1 = filt(cut(load("171463"), 0.0, 26.0), "highpass", 1800, 4)
    s2 = filt(cut(load("512805"), 18.0, 45.0), "highpass", 1500, 4)
    s1 = expander(s1, thr_db=-45, floor_db=-12)
    s2 = expander(s2, thr_db=-55, floor_db=-10)
    s1, s2 = norm(s1, -22), norm(s2, -27)                      # the song sparrow leads, house sparrows chatter behind
    x = make_loop(xfade_join(s1, s2, 1.5), 1.5)
    write("VH_Amb_Birds_Sparrows_Loop", norm(x, -22.0), used=[("171463", "0-26 s"), ("512805", "18-45 s")])

    d = cut(load("456930"), 0.0, 73.0)
    d = filt(filt(d, "highpass", 280, 4), "lowpass", 2500, 4)
    d = expander(d, thr_db=-66, floor_db=-14)
    d = make_loop(d, 2.0)
    write("VH_Amb_Birds_MourningDove_Loop", norm(d, -23.0), used=[("456930", "0-73 s")])


def mower():
    x = cut(load("399375"), 0.0, 56.0)                         # before the second, near mower starts at 1:00
    x = filt(x, "highpass", 90, 2)
    x = make_loop(x, 3.0)
    write("VH_Amb_Lawnmower_Loop", norm(x, -21.0), used=[("399375", "0-56 s")])


def sprinkler():
    x = cut(load("520886"), 4.0, 52.0)
    x = filt(x, "highpass", 300, 4)                            # the ticks and the spray; no garden rumble
    x = make_loop(x, 1.0)
    write("VH_Amb_Sprinkler_Loop", norm(x, -24.0), used=[("520886", "4-52 s")])


def dogs():
    a = load("832435")
    for i, (t0, t1) in enumerate([(3.23, 4.05), (5.47, 6.35), (11.27, 12.15), (14.77, 15.65), (0.57, 2.15), (8.09, 9.8)]):
        x = filt(cut(a, t0, t1), "highpass", 150, 2)
        x = fade(distance_oneshot(x, tail=0.6, lp=6000, hp=150, wet=0.15, rt60=0.7, seed=i), 0.004, 0.25)
        write(f"VH_Amb_Dog_Bark_{i + 1:02d}", norm(x, -18.0), used=[("832435", f"{t0:.2f}-{t1:.2f} s")])
    b = load("495658")
    c = load("277058")
    far = [(b, "495658", 0.03, 0.85), (b, "495658", 1.41, 2.1), (b, "495658", 3.39, 4.0), (c, "277058", 0.0, 0.4)]
    for i, (src, sid, t0, t1) in enumerate(far):
        x = filt(cut(src, t0, t1), "highpass", 200, 2)
        x = fade(distance_oneshot(x, tail=1.4, lp=2200, hp=250, wet=0.45, rt60=1.3, seed=10 + i), 0.004, 0.4)
        write(f"VH_Amb_Dog_BarkFar_{i + 1:02d}", norm(x, -22.0), used=[(sid, f"{t0:.2f}-{t1:.2f} s, distanced")])


def tree_wind():
    x = cut(load("751473"), 55.0, 128.0)
    x = filt(x, "highpass", 180, 4)
    x = make_loop(x, 3.0)
    write("VH_Amb_TreeWind_Soft_Loop", norm(x, -24.0), used=[("751473", "55-128 s")])
    y = load("546759")
    y = filt(y, "highpass", 180, 4)
    y = make_loop(y, 1.5)
    write("VH_Amb_TreeWind_Gusty_Loop", norm(y, -23.0), used=[("546759", "whole, mono")])


def league():
    g = load("430924")
    far = dict(lp=2400, hp=220, wet=0.5, rt60=1.6)
    m = distance(cut(g, 78.0, 128.0), seed=21, **far)
    m = make_loop(m, 3.0)
    write("VH_Amb_LittleLeague_Crowd_Loop", norm(m, -23.0), used=[("430924", "78-128 s, distanced")])

    hits = [("860786", 0.47, 1.4), ("860786", 2.21, 3.15), ("851452", 0.0, 0.5)]
    cheers = [(29.5, 35.5), (124.5, 130.5), (45.5, 51.5)]  # loud crowd voices, not the rhythmic team chant at 0-18 s
    for i, ((sid, t0, t1), (c0, c1)) in enumerate(zip(hits, cheers)):
        bat = filt(cut(load(sid), t0, t1), "highpass", 300, 2)
        bat = norm(bat, -16)
        ch = fade(cut(g, c0, c1), 0.4, 1.5)
        ch = norm(ch, -22)
        n = int((0.55 + (c1 - c0)) * SR)
        x = np.zeros(n)
        x[: len(bat)] += bat
        off = int(0.55 * SR)
        x[off:off + len(ch)] += ch[: n - off]
        x = fade(distance_oneshot(x, tail=1.6, seed=30 + i, **far), 0.002, 0.8)
        write(f"VH_Amb_LittleLeague_Hit_{i + 1:02d}", norm(x, -20.0),
              used=[(sid, f"{t0:.2f}-{t1:.2f} s (the bat)"), ("430924", f"{c0}-{c1} s (the cheer)")])
    ch = fade(cut(load("102108"), 8.8, 14.0), 0.3, 1.0)
    ch = fade(distance_oneshot(ch, tail=1.2, lp=3000, hp=220, wet=0.3, rt60=1.4, seed=40), 0.01, 0.8)
    write("VH_Amb_LittleLeague_Cheer_01", norm(ch, -21.0), used=[("102108", "8.8-14 s")])
    u = load("625473")
    for i, (t0, t1) in enumerate([(1.62, 3.55), (4.76, 7.0), (8.32, 10.05), (11.28, 12.25)]):
        x = filt(cut(u, t0, t1), "bandpass", [350, 3200], 2)
        x = fade(distance_oneshot(x, tail=1.6, seed=50 + i, **far), 0.01, 0.6)
        write(f"VH_Amb_LittleLeague_Umpire_{i + 1:02d}", norm(x, -21.0), used=[("625473", f"{t0:.2f}-{t1:.2f} s, distanced")])


def ice_cream():
    x = load("128573")
    t0, approx = 2.0, 26.07
    # refine the tune's period to the sample: correlate 3 s at t0 against 3 s one period later
    a = x[int(t0 * SR):int((t0 + 3) * SR)]
    best, lag = -2, 0
    for L in range(int((approx - 0.06) * SR), int((approx + 0.06) * SR)):
        b = x[int(t0 * SR) + L:int(t0 * SR) + L + len(a)]
        c = np.dot(a, b) / np.sqrt(np.dot(a, a) * np.dot(b, b))
        if c > best:
            best, lag = c, L
    PERIOD = lag / SR
    print(f"  ice cream: period {PERIOD:.4f} s, waveform corr {best:.3f}")
    seg = x[int(t0 * SR):int(t0 * SR) + lag + int(0.25 * SR)].copy()
    # the truck was drifting off: level the one period out so the loop seam doesn't step
    w = int(2.0 * SR)
    full = x[int(t0 * SR):int(t0 * SR) + lag + w]
    r = np.sqrt((full[:w] ** 2).mean() / ((full[lag:lag + w] ** 2).mean() + 1e-12))
    ramp = 1 + (r - 1) * np.arange(len(seg)) / lag
    seg *= ramp
    seg = filt(filt(seg, "highpass", 380, 2), "lowpass", 5500, 2)        # tinny, from a street away
    seg = distance(np.concatenate([seg[-int(1.6 * SR):], seg]), lp=5000, hp=380, wet=0.25, rt60=1.0, seed=60)[int(1.6 * SR):]
    y = make_loop(seg, 0.25, power=False)                                 # same tune both sides: an equal-gain crossfade
    write("VH_Amb_IceCreamTruck_Loop", norm(y, -22.0), used=[("128573", f"{t0:.2f}-{t0 + PERIOD:.2f} s (one pass of the tune), levelled")])


# ---------------------------------------------------------------- checks + publish

def check(path):
    x = np.frombuffer(subprocess.run(["ffmpeg", "-v", "quiet", "-i", path, "-f", "f32le", "-"], capture_output=True,
                                     check=True).stdout, np.float32).astype(np.float64)
    ch = int(subprocess.run(["ffprobe", "-v", "quiet", "-show_entries", "stream=channels", "-of", "csv=p=0", path],
                            capture_output=True, text=True).stdout.strip().split(",")[0])
    x = x.reshape(-1, ch).T
    dur = x.shape[1] / SR
    pk = 20 * np.log10(np.abs(x).max() + 1e-12)
    L = lufs(x if ch == 2 else x[0])
    steps = np.abs(np.diff(x, axis=1))
    p999 = np.percentile(steps, 99.9)
    seam = np.abs(x[:, 0] - x[:, -1]).max()
    return dur, ch, L, pk, seam / (p999 + 1e-12)


def sources_md():
    lines = ["# Ambience sources (Visiting Hours, the hill's golden-hour Saturday)", "",
             "Every source is **CC0 1.0** (Creative Commons Zero, public domain dedication), checked on its Freesound page on",
             "2026-10-12. Taken from the public HQ previews (`https://cdn.freesound.org/previews/...-hq.mp3`), cut, filtered,",
             "distanced, looped and normalised by `tools/look/ambience-build.py` (re-runnable; its header has the command).",
             "Nothing here is synthesized.", "",
             "Levels in the files (integrated LUFS, before Unity's distance attenuation; sample peaks at or under -1 dBFS):",
             "the stereo bed -30; positional loops -21 to -24 (sprinkler, wind and dove at the quiet end); one-shots: the near",
             "dog -18, the far dog -22, the ball game's hits -20, umpire calls and the cheer -21.", "",
             "| Freesound id | Author | Title | URL | Licence |", "|---|---|---|---|---|"]
    for sid, (a, t, u) in SOURCES.items():
        lines.append(f"| {sid} | {a} | {t} | {u} | CC0 1.0 |")
    lines += ["", "## What each file is made of", "", "| File | From |", "|---|---|"]
    for name, used in sorted(USED.items()):
        lines.append(f"| {name}.ogg | " + "; ".join(f"{sid} ({w})" for sid, w in used) + " |")
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    args = sys.argv[1:]
    WORK = None
    if "--work" in args:
        i = args.index("--work")
        WORK = args[i + 1]
        del args[i:i + 2]
    SRC, OUT = args
    WORK = WORK or os.path.join(OUT + "~work")
    if os.path.isdir(WORK):
        shutil.rmtree(WORK)
    os.makedirs(WORK)
    for step in (bed, birds, mower, sprinkler, dogs, tree_wind, league, ice_cream):
        print("building", step.__name__)
        step()
    with open(os.path.join(WORK, "SOURCES.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write(sources_md())
    print(f"{'file':44s} {'sec':>6s} ch {'LUFS':>6s} {'peak':>6s} seam/p99.9")
    total = 0
    for fn in sorted(os.listdir(WORK)):
        if fn.endswith(".ogg"):
            d, ch, L, pk, seam = check(os.path.join(WORK, fn))
            total += os.path.getsize(os.path.join(WORK, fn))
            print(f"{fn:44s} {d:6.1f} {ch:2d} {L:6.1f} {pk:6.1f} {seam:6.2f}")
    print(f"total {total / 1e6:.2f} MB")
    os.makedirs(OUT, exist_ok=True)
    for fn in os.listdir(OUT):
        if fn.endswith(".ogg") and not os.path.exists(os.path.join(WORK, fn)):
            os.remove(os.path.join(OUT, fn))
    for fn in os.listdir(WORK):
        shutil.copy2(os.path.join(WORK, fn), os.path.join(OUT, fn))
    shutil.rmtree(WORK)
