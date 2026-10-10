# The first-person movement's one-shot sounds, layered from Kenney's CC0 packs (Impact Sounds, RPG Audio,
# Sci-Fi Sounds) into round-robin variants, written as mono 16-bit 44.1 kHz WAVs into the package's
# Resources folder, where MovementAudio loads them (missing files fall back to the code-made sounds).
#
#   uv run --with numpy python tools/audio/make-movement-sfx.py <packs dir>
#
# <packs dir> holds the three zips unpacked into impact/, rpg/ and scifi/ (each with its Audio/ folder):
#   https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip
#   https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip
#   https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip
# Safe to re-run: it rewrites the WAVs and keeps existing .meta files (their GUIDs).
#
# Every layer is first normalised to a 0 dBFS peak, so the gains below are relative. Then each variant is
# matched in punch (loudest 30 ms RMS) to the others of its set, and the whole set is scaled so its loudest
# peak sits at -3 dBFS (MovementAudio sets the levels between sets; nothing goes above -1 dBFS).
import os
import subprocess
import sys
import uuid
import wave

import numpy as np

SR = 44100
PEAK = 10 ** (-3 / 20)
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Packages", "com.vibejam.jamkit", "Runtime", "Audio", "Movement", "Resources", "JamKitMovementAudio")

if len(sys.argv) < 2:
    sys.exit(__doc__ or "usage: make-movement-sfx.py <packs dir>")
PACKS = sys.argv[1]


def src(pack, name):
    return os.path.join(PACKS, pack, "Audio", name + ".ogg")


def load(pack, name, af="highpass=f=40", start=0.0, length=None):
    """Decode one source file to mono float at 44.1 kHz (ffmpeg filters in af), trimmed of lead silence."""
    args = ["ffmpeg", "-v", "error", "-ss", str(start)]
    if length:
        args += ["-t", str(length)]
    args += ["-i", src(pack, name), "-ac", "1", "-ar", str(SR), "-af", af, "-f", "f32le", "-"]
    x = np.frombuffer(subprocess.run(args, capture_output=True, check=True).stdout, np.float32).astype(np.float64)
    if len(x) == 0:
        raise SystemExit("empty decode: " + src(pack, name))
    pk = np.abs(x).max()
    on = np.nonzero(np.abs(x) > pk * 10 ** (-45 / 20))[0]
    x = x[max(0, on[0] - int(0.001 * SR)):]
    return x / np.abs(x).max()


def cut(x, seconds, fade_out=0.04):
    n = min(len(x), int(seconds * SR))
    y = x[:n].copy()
    f = min(n, int(fade_out * SR))
    y[n - f:] *= np.linspace(1, 0, f) ** 2
    return y


def pitch(x, ratio):
    """Resample (pitch and length together, like a tape)."""
    n = int(len(x) / ratio)
    return np.interp(np.arange(n) * ratio, np.arange(len(x)), x)


def puff(x, attack=0.004, tau=0.07):
    t = np.arange(len(x)) / SR
    e = np.where(t < attack, t / attack, np.exp(-(t - attack) / tau))
    return x * e


def whump(hz0=120.0, hz1=55.0, dur=0.11, tau=0.04):
    t = np.arange(int(dur * SR)) / SR
    hz = hz0 * (hz1 / hz0) ** (t / dur)
    ph = 2 * np.pi * np.cumsum(hz) / SR
    e = np.minimum(1, t / 0.002) * np.exp(-t / tau) * (1 - t / dur)
    return np.sin(ph) * e


def mix(*layers):
    """layers: (signal, offset seconds, gain dB)."""
    n = max(int(o * SR) + len(s) for s, o, _ in layers)
    y = np.zeros(n)
    for s, o, g in layers:
        i = int(o * SR)
        y[i:i + len(s)] += s * 10 ** (g / 20)
    return y


def finish(y):
    pk = np.abs(y).max()
    idx = np.nonzero(np.abs(y) > pk * 10 ** (-55 / 20))[0]
    y = y[:idx[-1] + int(0.01 * SR)]
    y = y - y.mean()
    fi, fo = int(0.0015 * SR), min(len(y) // 3, int(0.02 * SR))
    y[:fi] *= np.linspace(0, 1, fi)
    y[len(y) - fo:] *= np.linspace(1, 0, fo)
    return y


def punch(y):
    w = int(0.03 * SR)
    if len(y) <= w:
        return np.sqrt((y ** 2).mean())
    c = np.cumsum(np.concatenate([[0], y ** 2]))
    return np.sqrt(((c[w:] - c[:-w]) / w).max())


def write_set(name, variants):
    variants = [finish(v) for v in variants]
    target = np.median([punch(v) for v in variants])
    variants = [v * target / punch(v) for v in variants]
    k = PEAK / max(np.abs(v).max() for v in variants)
    for i, v in enumerate(variants):
        path = os.path.join(OUT, f"{name}_{i}.wav")
        pcm = np.clip(np.round(v * k * 32767), -32767, 32767).astype("<i2")
        with wave.open(path, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(pcm.tobytes())
        write_meta(path, AUDIO_META)
        print(f"{name}_{i}.wav  {len(v)/SR:5.2f} s  peak {20*np.log10(np.abs(v*k).max()):5.1f} dBFS")


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


def write_meta(path, template):
    meta = path + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", newline="\n") as f:
            f.write(template.format(guid=uuid.uuid4().hex))


def main():
    os.makedirs(OUT, exist_ok=True)
    d = OUT
    while os.path.basename(d) != "Runtime":
        write_meta(d, FOLDER_META)
        d = os.path.dirname(d)

    I, R, S = "impact", "rpg", "scifi"
    concrete = [load(I, f"footstep_concrete_00{i}", "highpass=f=70") for i in range(5)]
    soft = [load(I, f"impactSoft_medium_00{i}", "highpass=f=35") for i in range(5)]
    heavy = [load(I, f"impactSoft_heavy_00{i}", "highpass=f=30") for i in range(5)]
    light = [load(I, f"impactGeneric_light_00{i}", "highpass=f=120") for i in range(5)]
    cloth = [load(R, f"cloth{i}", "highpass=f=150") for i in range(1, 5)]
    dirt = [load(R, f"footstep0{i}", "highpass=f=90") for i in range(10)]
    grip = [load(R, "handleSmallLeather", "highpass=f=300"), load(R, "handleSmallLeather2", "highpass=f=300")]

    # Footsteps (walk and sprint: MovementAudio sets weight and pitch by speed).
    write_set("mv_step", concrete)
    # The low body under a sprinting step.
    write_set("mv_step_heavy", [cut(load(I, f"impactSoft_medium_00{i}", "lowpass=f=260,highpass=f=35"), 0.16, 0.06) for i in range(5)])
    # The jump: a shoe scuff (push-off) under a cloth swish.
    write_set("mv_jump", [mix((cut(dirt[a], 0.12), 0, -6), (cut(cloth[c], 0.32, 0.12), 0.01, 0))
                          for a, c in [(0, 0), (1, 1), (3, 2), (7, 0)]])
    # The double jump: a jump-kit puff (a short burst of thruster hiss) over a low whump. Clearly not the jump.
    write_set("mv_djump", [mix((puff(load(S, f"thrusterFire_00{i}", "highpass=f=500", 1.0 + 0.4 * i, 0.32)), 0, 0),
                               (whump(125 - 6 * i, 55), 0, -7)) for i in range(4)])
    # The wall kick: a sharp snap (hard shoe on the wall) with a little whoosh as you leave.
    write_set("mv_walljump", [mix((light[i], 0, 0), (pitch(concrete[(i + 1) % 5], 1.12), 0, -3),
                                  (cut(load(R, "cloth4", "highpass=f=600"), 0.2, 0.1), 0.015, -6)) for i in range(4)])
    # The wallrun catch: the hand planting on the wall, then the swish of leaning in.
    write_set("mv_catch", [mix((cut(load(I, f"impactSoft_medium_00{(i + 1) % 5}", "lowpass=f=420,highpass=f=40"), 0.14), 0, 0),
                               (light[(i * 2) % 5], 0, -8), (cut(cloth[(i + 1) % 4], 0.3, 0.12), 0.03, -4)) for i in range(4)])
    # Landings, by fall speed: two quick feet (soft), a thud (normal), a heavy thud with a body crumple (hard).
    write_set("mv_land_soft", [mix((concrete[a], 0, 0), (concrete[b], 0.032 + 0.003 * a, -3))
                               for a, b in [(0, 3), (1, 4), (2, 0), (3, 1)]])
    write_set("mv_land", [mix((soft[i], 0, 0), (concrete[(i + 2) % 5], 0.003, -4)) for i in range(5)])
    write_set("mv_land_hard", [mix((heavy[i], 0, 0), (soft[(i + 3) % 5], 0, -3), (concrete[i], 0.004, -5),
                                   (cut(cloth[i % 4], 0.3, 0.12), 0.02, -10)) for i in range(5)])
    # The mantle: hands gripping the ledge, the pull up (cloth), a foot landing on top.
    write_set("mv_mantle", [mix((grip[i % 2], 0, -6), (cut(cloth[i], 0.35, 0.12), 0.04, 0), (dirt[2 + 2 * i], 0.26, -4))
                            for i in range(4)])
    # The sprint kick: a hard, lower footstep and a body thud with a short whoosh.
    write_set("mv_kick", [mix((pitch(concrete[i], 0.9), 0, 0), (soft[(i + 2) % 5], 0, -4),
                              (cut(load(R, "cloth2", "highpass=f=500"), 0.2, 0.1), 0.02, -9)) for i in range(4)])
    # Dropping into a slide: a body thud, a gritty scuff and cloth.
    write_set("mv_slidein", [mix((soft[(i + 4) % 5], 0, -2), (dirt[d], 0, -3), (cut(cloth[(i + 2) % 4], 0.3, 0.12), 0.01, -6))
                             for i, d in enumerate([5, 8, 9, 3])])


if __name__ == "__main__":
    main()
