# Three news-anchor voice options for the Visiting Hours title (Kokoro TTS built-in voices; no real person).
#   <seed-vc venv python> tools/audio/make-vh-anchor-options.py <outDir>
# Each reads the report once (no repeats), with short pauses between sentences. A separate ffmpeg pass adds
# the "tablet speaker" sound.
import os
import sys

import numpy as np
import soundfile as sf
from kokoro import KPipeline

OUT = sys.argv[1]
SR = 24000
os.makedirs(OUT, exist_ok=True)

REPORT = [
    "Two people have died and a third is in critical condition after an early-morning crash on Route Nine.",
    "Police say a car crossed the median shortly after six a.m. on Saturday.",
    "The driver, a thirty-four-year-old emergency physician, was taken to Saint Brigid's.",
    "The names of the victims, a woman and a ten-year-old boy, have not been released.",
]
VOICES = [("a", "am_adam", 0.98), ("b", "bm_george", 0.96), ("a", "af_nicole", 1.0)]
# One voice per process (argv[2]) keeps memory low: two language pipelines in one process exceed the cap.
if len(sys.argv) > 2:
    VOICES = [v for v in VOICES if v[1] == sys.argv[2]]

pipes = {}
for lang, voice, speed in VOICES:
    pipe = pipes.setdefault(lang, KPipeline(lang_code=lang))
    parts = [np.zeros(int(SR * 0.3), dtype=np.float32)]
    for line in REPORT:
        chunks = [a for _, _, a in pipe(line, voice=voice, speed=speed)]
        parts.append(np.concatenate([np.asarray(c, dtype=np.float32) for c in chunks]))
        parts.append(np.zeros(int(SR * 0.45), dtype=np.float32))
    wav = np.concatenate(parts)
    path = os.path.join(OUT, f"anchor_{voice}_dry.wav")
    sf.write(path, wav, SR)
    print(f"{path}  {len(wav) / SR:.1f}s")
