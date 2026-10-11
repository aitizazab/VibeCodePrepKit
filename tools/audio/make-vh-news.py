# The title's news report, read by a Kokoro TTS voice (built-in voices; no real person), taken straight from the
# creator's script (script/opening-script.md, the "## The news report" section), so re-running picks up their
# edits. The report is trimmed by CUTS (whole clauses taken out, nothing rewritten) so the title's fade-in doesn't
# sit in long pauses, then given the tablet-speaker treatment and levelled for the game.
#   <seed-vc venv python> tools/audio/make-vh-news.py <script.md> <out.wav> [voice=af_heart] [speed=0.95]
#       [pause=0.35] [nocuts] [dry=<path>] [OLD=NEW ...]
# Runs on the CPU (it's small), one voice per run.
import os
import re
import subprocess
import sys

import numpy as np
import soundfile as sf
from kokoro import KPipeline

SCRIPT, OUT = sys.argv[1], sys.argv[2]
KEYS = ("voice", "speed", "pause", "dry")
opts = dict(a.split("=", 1) for a in sys.argv[3:] if "=" in a and a.split("=", 1)[0] in KEYS)
VOICE = opts.get("voice", "af_heart")
SPEED = float(opts.get("speed", "0.95"))
PAUSE = float(opts.get("pause", "0.35"))
DRY = opts.get("dry", "")
# Optional word swaps (OLD=NEW), for fixes decided before the script file itself is updated.
SWAPS = [a.split("=", 1) for a in sys.argv[3:] if "=" in a and a.split("=", 1)[0] not in KEYS]
# The cut (2026-10-11, the creator: "make it shorter", about half): whole clauses out, the rest word for word.
CUTS = [] if "nocuts" in sys.argv[3:] else [
    ", as he drove back from his workplace",
    ", being treated by the best at his own hospital, Saint Brigid",
    ", and the town eagerly awaits the day he wakes from his coma",
]
SR = 24000
OUT_SR = 48000
LUFS = -18.0  # dialogue loudness for the tablet (the voicemail is -16; commit 0f1c837)
# The tablet's speaker: no deep lows, a little presence, softened top, light compression. No distortion, so the
# words stay clear.
TABLET = ("highpass=f=220,lowpass=f=7500,equalizer=f=180:t=q:w=1:g=-3,equalizer=f=2600:t=q:w=1.2:g=3,"
          "acompressor=threshold=-24dB:ratio=3:attack=4:release=90:makeup=4,alimiter=limit=0.5:attack=1.5:release=40:level=false")

text = open(SCRIPT, encoding="utf-8").read()
m = re.search(r"^## The news report[^\n]*\n(.*?)(?=^## |\Z)", text, re.S | re.M)
if not m:
    sys.exit("no '## The news report' section found")
report = " ".join(line.strip() for line in m.group(1).splitlines() if line.strip())
for old, new in SWAPS:
    report = report.replace(old, new)
for cut in CUTS:
    if cut not in report:
        print("note: cut not found in the script (already edited?):", cut)
    report = report.replace(cut, "")
sentences = [s.strip() for s in re.split(r"(?<=[.!?])\s+", report) if s.strip()]

pipe = KPipeline(lang_code=VOICE[0], device="cpu")  # the CPU: a short job, and the GPU stays free
parts = [np.zeros(int(SR * 0.3), dtype=np.float32)]
for s in sentences:
    chunks = [a for _, _, a in pipe(s, voice=VOICE, speed=SPEED)]
    parts.append(np.concatenate([np.asarray(c, dtype=np.float32) for c in chunks]))
    parts.append(np.zeros(int(SR * PAUSE), dtype=np.float32))
wav = np.concatenate(parts)
dry = DRY or OUT + ".dry.wav"
sf.write(dry, wav, SR)


def run(args):
    return subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")


# Tablet treatment, then measure and set the loudness (true peak kept under -1.5 dBFS).
tmp = OUT + ".tmp.wav"
run(["ffmpeg", "-y", "-v", "error", "-i", dry, "-af", TABLET + f",aresample={OUT_SR}", "-ac", "1", tmp])
meas = run(["ffmpeg", "-v", "info", "-i", tmp, "-af", "ebur128=peak=true", "-f", "null", "-"]).stderr
lufs = float(re.findall(r"I:\s+(-?[\d.]+) LUFS", meas)[-1])
peak = float(re.findall(r"Peak:\s+(-?[\d.]+) dBFS", meas)[-1])
gain = min(LUFS - lufs, -1.5 - peak)
run(["ffmpeg", "-y", "-v", "error", "-i", tmp, "-af", f"volume={gain:.2f}dB", "-c:a", "pcm_s16le", OUT])
os.remove(tmp)
if not DRY:
    os.remove(dry)
print(f"{OUT}  {len(wav) / SR:.2f}s, {len(sentences)} sentences, voice {VOICE} x{SPEED}, "
      f"{lufs + gain:.1f} LUFS, true peak {peak + gain:.1f} dBFS")
for s in sentences:
    print("  |", s)
