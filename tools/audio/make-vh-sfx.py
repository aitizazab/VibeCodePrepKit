# The Visiting Hours opening's sounds from public CC0 recordings (Freesound), replacing the code-made placeholders:
# the sleeping breath, the phone's ring and buzz (one 3 s loop each, in step), rolling over in bed, a sheet
# rustle, the phone set down and a screen tap. Mono 16-bit 44.1 kHz WAVs at the opening's set peaks
# (ProceduralAudio's Peaks, commit 0f1c837), written where ProceduralAudio.Get finds them first:
#   Assets/JamKit/Templates/VisitingHours/Audio/Resources/JamKitSounds/<id>.wav      (shipped)
#   Assets/JamKit/Templates/VisitingHours/Audio/Takes/<id>__<take>.wav               (alternates, not shipped)
#   out/vh-opening-audio/00-opening-sounds.wav                                       (the audition)
#
#   uv run --with numpy python tools/audio/make-vh-sfx.py <downloads dir>
#
# <downloads dir> holds the Freesound HQ previews as <sound id>.mp3 (https://freesound.org/s/<id>/, every one CC0;
# the list and what each is used for: Templates/VisitingHours/ThirdParty/LICENSES.md). Safe to re-run: it
# rewrites the WAVs and keeps existing .meta files.
import os
import subprocess
import sys
import uuid
import wave

import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VH = os.path.join(ROOT, "Assets", "JamKit", "Templates", "VisitingHours", "Audio")
SHIP = os.path.join(VH, "Resources", "JamKitSounds")
TAKES = os.path.join(VH, "Takes")
REVIEW = os.path.join(ROOT, "out", "vh-opening-audio")
DL = sys.argv[1] if len(sys.argv) > 1 else sys.exit("usage: make-vh-sfx.py <downloads dir>")

# The opening's set peaks (ProceduralAudio.Peaks).
PEAK = {"vh_phone_ring": 0.75, "vh_vibrate": 0.6, "vh_breath": 0.35, "vh_rustle_big": 0.6, "vh_rustle": 0.55,
        "vh_clunk": 0.75, "vh_tap": 0.6}
RING_LOOP = 3.0   # the ring and the buzz loop together every 3 s
BREATH = 4.8      # VhStage.BreathAt: in over 0-1.7 s, out from 1.9 s, 4.8 s a breath
EXHALE_AT = 1.9


def load(sid, start=0.0, dur=None, af="highpass=f=60"):
    args = ["ffmpeg", "-v", "error", "-ss", f"{start:.3f}"]
    if dur:
        args += ["-t", f"{dur:.3f}"]
    args += ["-i", os.path.join(DL, f"{sid}.mp3"), "-ac", "1", "-ar", str(SR), "-af", af, "-f", "f32le", "-"]
    x = np.frombuffer(subprocess.run(args, capture_output=True, check=True).stdout, np.float32).astype(np.float64)
    if len(x) == 0:
        raise SystemExit(f"empty decode: {sid} at {start}")
    return x


def fade(x, fin=0.003, fout=0.03):
    y = x.copy()
    a, b = min(len(y), int(fin * SR)), min(len(y), int(fout * SR))
    if a:
        y[:a] *= np.linspace(0, 1, a)
    if b:
        y[len(y) - b:] *= np.linspace(1, 0, b) ** 2
    return y


def place(buf, x, at, wrap=False):
    i = int(round(at * SR))
    for k in range(len(x)):
        j = i + k
        if wrap:
            j %= len(buf)
        elif j >= len(buf):
            break
        buf[j] += x[k]


def norm(x, peak):
    return x * (peak / max(1e-9, np.abs(x).max()))


def one_shot(sid, start, dur, fin, fout, af="highpass=f=60"):
    return fade(load(sid, start, dur, af), fin, fout)


# ---------- the ring and the buzz: 3 s loops, sounding from 0, in step ----------

def ring(sid, start, dur):
    buf = np.zeros(int(RING_LOOP * SR))
    place(buf, fade(load(sid, start, dur, "highpass=f=150"), 0.002, 0.06), 0.0)
    return buf


def buzz(sid, start, dur, af="highpass=f=40"):
    buf = np.zeros(int(RING_LOOP * SR))
    place(buf, fade(load(sid, start, dur, af), 0.004, 0.05), 0.0)
    return buf


# ---------- the breath: real breaths laid on VhStage's 4.8 s grid, so her chest moves with the sound ----------

def onset(x, t, window=0.35, rise_db=8.0):
    """The first 20 ms frame within +-window of t where the (band-passed) level rises rise_db over the quiet before."""
    w = int(0.02 * SR)
    i0, i1 = max(0, int((t - window) * SR)), min(len(x), int((t + window) * SR))
    seg = x[i0:i1]
    n = len(seg) // w
    db = 20 * np.log10(np.sqrt((seg[:n * w].reshape(n, w) ** 2).mean(1)) + 1e-9)
    floor = np.percentile(db, 10)
    for k in range(n):
        if db[k] > floor + rise_db:
            return (i0 + k * w) / SR
    return t


def breath_loop(sid, cycles, pitch=1.0, stretch=1.0, af="highpass=f=120,lowpass=f=9000"):
    """cycles: (inhale start, exhale start, next inhale start) in the source, roughly; refined to the onsets."""
    src = load(sid, 0, None, af)
    band = load(sid, 0, None, "highpass=f=200,lowpass=f=6000")
    if pitch != 1.0:
        # Up a little in pitch and formant (lighter), the same length: resample, then stretch back.
        n = int(len(src) / pitch)
        src = np.interp(np.arange(n) * pitch, np.arange(len(src)), src)
        band = np.interp(np.arange(n) * pitch, np.arange(len(band)), band)
        cycles = [tuple(t / pitch for t in c) for c in cycles]
    loop = np.zeros(int(BREATH * len(cycles) * SR))
    pre = 0.08
    for k, (inh, exh, nxt) in enumerate(cycles):
        inh, exh = onset(band, inh), onset(band, exh)
        base = k * BREATH
        a = src[int((inh - pre) * SR):int((exh - pre) * SR)]
        b = src[int((exh - pre) * SR):int((nxt - pre) * SR)]
        if stretch != 1.0:
            a, b = tempo(a, 1 / stretch), tempo(b, 1 / stretch)
        place(loop, fade(a, 0.04, 0.08), base - pre * stretch, wrap=True)
        place(loop, fade(b, 0.04, 0.25), base + EXHALE_AT - pre * stretch, wrap=True)
    return loop


def tempo(x, factor):
    """Time-stretch by ffmpeg's atempo (factor < 1 slows it down)."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-af",
                          f"atempo={factor:.4f}", "-f", "f32le", "-"], input=x.astype(np.float32).tobytes(),
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


# ---------- writing ----------

AUDIO_META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""
FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def meta(path, template):
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", newline="\n") as f:
            f.write(template.format(guid=uuid.uuid4().hex))


def write(path, x, with_meta=True):
    pcm = np.clip(np.round(x * 32767), -32767, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    if with_meta:
        meta(path, AUDIO_META)


def main():
    for d in (SHIP, TAKES, REVIEW):
        os.makedirs(d, exist_ok=True)
    for d in (os.path.join(VH, "Resources"), SHIP, TAKES):
        meta(d, FOLDER_META)

    man = [(4.40, 6.40, 9.40), (9.40, 11.35, 14.10), (14.10, 16.45, 19.70), (19.70, 21.70, 24.40)]
    woman = [(1.50, 2.95, 4.35), (4.35, 5.30, 6.50), (6.50, 7.75, 9.50), (9.50, 10.95, 12.50)]
    takes = {
        # id: [(take name, samples), ...]; the first is shipped.
        "vh_breath": [
            ("sleeper-lighter", breath_loop(207116, man, pitch=1.12)),
            ("sleeper-as-recorded", breath_loop(207116, man)),
            ("woman-nose-slowed", breath_loop(112556, woman, stretch=1.45, af="highpass=f=150,lowpass=f=8000")),
        ],
        "vh_phone_ring": [
            ("synth-ring", ring(383439, 0.0, 1.56)),
            ("arpeggio-ring", ring(60901, 0.0, 2.2)),
            ("cordless-ring", ring(108738, 0.45, 1.6)),
        ],
        "vh_vibrate": [
            ("on-wooden-desk", buzz(77392, 0.30, 1.14)),
            ("on-wooden-desk-short", buzz(77392, 4.30, 0.62)),
            ("on-hard-surface", buzz(69585, 0.02, 0.58)),
        ],
        "vh_rustle_big": [
            ("rolling-in-bed", one_shot(108515, 8.85, 1.75, 0.03, 0.25)),
            ("rolling-in-bed-longer", one_shot(108515, 6.55, 2.1, 0.03, 0.3)),
            ("blanket-move", one_shot(388454, 0.0, 2.25, 0.03, 0.3)),
        ],
        "vh_rustle": [
            ("sheets-by-hand", one_shot(644490, 0.0, 0.8, 0.02, 0.25)),
            ("sheets-on-bed", one_shot(444405, 0.82, 0.64, 0.02, 0.2)),
            ("moving-in-sleep", one_shot(108515, 5.65, 0.65, 0.02, 0.2)),
        ],
        "vh_clunk": [
            ("phone-on-wooden-table", one_shot(859962, 10.38, 0.42, 0.002, 0.12)),
            ("phone-on-wooden-table-2", one_shot(859962, 1.76, 0.34, 0.002, 0.1)),
            ("cordless-on-table", one_shot(637747, 0.44, 0.40, 0.002, 0.12)),
        ],
        "vh_tap": [
            ("finger-on-phone", one_shot(704862, 1.15, 0.17, 0.001, 0.06, "highpass=f=150")),
            ("finger-on-phone-2", one_shot(704862, 0.57, 0.17, 0.001, 0.06, "highpass=f=150")),
            ("finger-on-phone-3", one_shot(704862, 1.77, 0.17, 0.001, 0.06, "highpass=f=150")),
        ],
    }
    shipped = {}
    for sid, versions in takes.items():
        for i, (name, x) in enumerate(versions):
            x = norm(x, PEAK[sid])
            if i == 0:
                write(os.path.join(SHIP, sid + ".wav"), x)
                shipped[sid] = x
            write(os.path.join(TAKES, f"{sid}__{name}.wav"), x)
            print(f"{'SHIP ' if i == 0 else 'take '} {sid:14s} {name:24s} {len(x)/SR:6.2f} s  peak {20*np.log10(np.abs(x).max()):5.1f} dBFS")

    # The audition, in the opening's order, at the levels the opening plays them (ring 0.55, buzz 0.8, breath 0.5).
    t = 22.0
    mix = np.zeros(int(t * SR))
    for k in range(3):
        place(mix, shipped["vh_phone_ring"] * 0.55, 0.3 + k * RING_LOOP)
        place(mix, shipped["vh_vibrate"] * 0.8, 0.3 + k * RING_LOOP)
    place(mix, shipped["vh_rustle"] * 0.6, 9.6)
    place(mix, shipped["vh_tap"] * 0.7, 10.6)
    place(mix, shipped["vh_clunk"] * 0.9, 11.4)
    place(mix, shipped["vh_rustle_big"], 11.7)
    breath = np.tile(shipped["vh_breath"], 2) * 0.5
    place(mix, breath[: len(mix) - int(13.8 * SR)], 13.8)
    write(os.path.join(REVIEW, "00-opening-sounds.wav"), mix, with_meta=False)
    print("audition", os.path.join(REVIEW, "00-opening-sounds.wav"), f"peak {20*np.log10(np.abs(mix).max()):.1f} dBFS")


if __name__ == "__main__":
    main()
