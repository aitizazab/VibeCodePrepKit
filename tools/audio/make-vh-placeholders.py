# Placeholder voice lines for the Visiting Hours slice, synthesised with Kokoro TTS (built-in voices,
# Apache-2.0; no real person's voice). The creator records the real lines later.
#   <seed-vc venv python> tools/audio/make-vh-placeholders.py <outDir>
# Each line is built from segments with real silences between them, so the pauses land.
import os
import sys

import numpy as np
import soundfile as sf
from kokoro import KPipeline

OUT = sys.argv[1]
SR = 24000
os.makedirs(OUT, exist_ok=True)
pipe = KPipeline(lang_code="a")


def say(text, voice, speed):
    chunks = [a for _, _, a in pipe(text, voice=voice, speed=speed)]
    return np.concatenate([np.asarray(c, dtype=np.float32) for c in chunks])


def gap(seconds):
    return np.zeros(int(SR * seconds), dtype=np.float32)


LINES = {
    # (segment text or a pause in seconds)
    "vo_elias_voicemail": ("am_michael", 0.92, [
        "Hey, Nads. It's me.", 0.5,
        "Could I bother you for a...", 1.4,
        "...it's five in the morning. You're asleep.", 0.6,
        "And your phone's probably dead again, isn't it.", 1.0,
        "No cabs out here at this hour. Figures.", 1.6,
        "Look. About yesterday. I'm sorry. I shouldn't have said that.", 1.2,
        "G'night.", 0.4,
    ]),
    "vo_news_anchor": ("af_sarah", 1.0, [
        "Two people have died and a third is in critical condition after an early-morning crash on Route Nine.", 0.4,
        "Police say a car crossed the median shortly after six a.m. on Saturday.", 0.4,
        "The driver, a thirty-four-year-old emergency physician, was taken to Saint Brigid's.", 0.4,
        "The names of the victims, a woman and a ten-year-old boy, have not been released.", 0.5,
    ]),
    "vo_nadia_sleep_in": ("af_heart", 0.85, [
        "...sleep in.", 0.3,
    ]),
}

for name, (voice, speed, parts) in LINES.items():
    audio = [gap(0.3)]
    for p in parts:
        audio.append(gap(p) if isinstance(p, (int, float)) else say(p, voice, speed))
    wav = np.concatenate(audio)
    path = os.path.join(OUT, name + ".wav")
    sf.write(path, wav, SR)
    print(f"{path}  {len(wav) / SR:.1f}s  ({voice})")
