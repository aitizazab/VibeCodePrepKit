# The title's news report, read by Kokoro's af_nicole (the creator's pick), taken straight from the creator's
# script (script/opening-script.md, the "## The news report" section), so re-running picks up their edits.
#   <seed-vc venv python> tools/audio/make-vh-news.py <script.md> <out.wav>
# One sentence at a time, with short pauses; the tablet-speaker effect is a separate ffmpeg pass.
import re
import sys

import numpy as np
import soundfile as sf
from kokoro import KPipeline

SCRIPT, OUT = sys.argv[1], sys.argv[2]
# Optional word swaps (OLD=NEW), for fixes decided before the script file itself is updated.
SWAPS = [a.split("=", 1) for a in sys.argv[3:] if "=" in a]
SR = 24000
text = open(SCRIPT, encoding="utf-8").read()
m = re.search(r"^## The news report[^\n]*\n(.*?)(?=^## |\Z)", text, re.S | re.M)
if not m:
    sys.exit("no '## The news report' section found")
report = " ".join(line.strip() for line in m.group(1).splitlines() if line.strip())
for old, new in SWAPS:
    report = report.replace(old, new)
sentences = [s.strip() for s in re.split(r"(?<=[.!?])\s+", report) if s.strip()]

pipe = KPipeline(lang_code="a")
parts = [np.zeros(int(SR * 0.3), dtype=np.float32)]
for s in sentences:
    chunks = [a for _, _, a in pipe(s, voice="af_nicole", speed=1.0)]
    parts.append(np.concatenate([np.asarray(c, dtype=np.float32) for c in chunks]))
    parts.append(np.zeros(int(SR * 0.45), dtype=np.float32))
wav = np.concatenate(parts)
sf.write(OUT, wav, SR)
print(f"{OUT}  {len(wav) / SR:.1f}s, {len(sentences)} sentences")
