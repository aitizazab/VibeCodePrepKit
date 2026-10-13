"""The sync check (docs/SKYLINE-DESIGN.md 2.3: "the sound events stay locked to the animation"; 9.4).

The firing range director (Unity) plays a marker beep (440 Hz), then both guns' handling sets, and writes
firing-range-schedule.json: each sound event's time in seconds after the marker, as the game cued it. This finds the
onsets in the recording with the ears, finds the marker by its 440 Hz energy (not the first onset: a stray sound
before the beep must not shift every event), and checks that every reload event whose sound is detectable on its own
(judged from the forge-rendered clip in --sounds) lands on an onset within the tolerance.

    uv run --project ~/.claude/skills/ears python tools/skyline/soundforge/synccheck.py <rec.mp4> <schedule.json> \
        --sounds Assets/JamKit/Templates/SkylineRepo/Resources/SkylineAudio [--tolerance-ms 35] [--min-share 0.9] \
        [--clips ReloadPartial,ReloadEmpty]

Prints one JSON object (ok, events, checked, matched, share, median_ms, worst_ms, missed, undetectable, note). The
matching (match, check) is plain Python and the marker search (find_marker) needs only numpy, both tested in
tests/test_synccheck.py; only main() needs the ears.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

RELOAD_CLIPS = ("ReloadPartial", "ReloadEmpty")


def match(event_times, onsets, tolerance):
    """For each event time (recording time, in order): the error (s) to the nearest onset not used yet, within the
    tolerance, or None."""
    used = set()
    errors = []
    for t in event_times:
        best, best_err = None, None
        for i, o in enumerate(onsets):
            if i in used:
                continue
            err = round(o - t, 6)
            if abs(err) <= tolerance and (best_err is None or abs(err) < abs(best_err)):
                best, best_err = i, err
        if best is not None:
            used.add(best)
        errors.append(best_err)
    return errors


def check(schedule, onsets, detectable=None, tolerance_ms=35.0, min_share=0.9, marker=None, clips=None):
    """marker: the marker beep's time in the recording (None: the first onset). clips: the handling clips whose
    events count (None: all)."""
    events = list(schedule.get("events", []))
    chosen = [e for e in events if clips is None or e.get("clip") in clips]
    skipped = [e for e in chosen if detectable is not None and e.get("sound") not in detectable]
    checked = [e for e in chosen if detectable is None or e.get("sound") in detectable]
    base = {"events": len(events), "checked": len(checked), "undetectable": sorted({str(e.get("sound")) for e in skipped})}
    if not onsets:
        return {**base, "ok": False, "matched": 0, "note": "no onsets in the recording (silent?)"}
    if not checked:
        return {**base, "ok": False, "matched": 0, "note": "the schedule has no detectable events"}
    tol = tolerance_ms / 1000.0
    if marker is None:
        marker, rest = onsets[0], list(onsets[1:])
    else:
        rest = list(onsets)
        nearest = min(range(len(rest)), key=lambda i: abs(rest[i] - marker))
        if abs(rest[nearest] - marker) <= tol:     # the beep's own onset is not an event's
            del rest[nearest]
    errors = match([marker + float(e["t"]) for e in checked], rest, tol)
    hit = sorted(abs(x) for x in errors if x is not None)
    missed = [f'{e.get("gun")}/{e.get("clip")}/{e.get("id")}@{float(e["t"]):.3f}' for e, x in zip(checked, errors) if x is None]
    share = len(hit) / len(checked)
    out = {
        **base,
        "ok": share >= min_share,
        "matched": len(hit),
        "share": round(share, 3),
        "median_ms": round(hit[len(hit) // 2] * 1000, 1) if hit else None,
        "worst_ms": round(hit[-1] * 1000, 1) if hit else None,
        "missed": missed[:20],
        "marker_s": round(marker, 3),
    }
    out["note"] = (f'{out["matched"]}/{out["checked"]} events within {tolerance_ms:g} ms (median {out["median_ms"]} ms, '
                   f'worst {out["worst_ms"]} ms; {len(base["undetectable"])} sounds undetectable alone)')
    return out


def find_marker(audio, sr, freq=440.0, frame_ms=40.0, hop_ms=5.0, band_hz=30.0, min_ratio=0.5, min_ms=30.0):
    """The start (s) of the first stretch of at least min_ms where the sound is mostly a tone at freq (the marker
    beep), or None. A frame counts when the energy within band_hz of freq is at least min_ratio of the frame's
    energy and at least a quarter of the loudest such frame's; the time is where the waveform first reaches a quarter
    of the beep's peak (the ears' onset rule), so it lines up with the onsets the events are matched against."""
    import numpy as np

    x = np.asarray(audio, dtype=np.float64)
    if x.ndim == 2:
        x = x.mean(axis=1)
    win = max(16, int(sr * frame_ms / 1000))
    hop = max(1, int(sr * hop_ms / 1000))
    if len(x) < win:
        return None
    starts = np.arange(0, len(x) - win + 1, hop)
    frames = np.lib.stride_tricks.sliding_window_view(x, win)[starts] * np.hanning(win)
    spec = np.abs(np.fft.rfft(frames, axis=1)) ** 2
    freqs = np.fft.rfftfreq(win, 1.0 / sr)
    band = (freqs >= freq - band_hz) & (freqs <= freq + band_hz)
    total = spec.sum(axis=1)
    narrow = spec[:, band].sum(axis=1)
    tonal = (total > 1e-12) & (narrow >= min_ratio * total)
    if not tonal.any():
        return None
    strong = tonal & (narrow >= 0.25 * narrow[tonal].max())
    need = max(1, int(round(min_ms / hop_ms)))
    run = 0
    for k in range(len(strong)):
        run = run + 1 if strong[k] else 0
        if run >= need:
            first = k - run + 1
            s0 = int(starts[first])
            seg = np.abs(x[s0:s0 + win + int(0.1 * sr)])
            at = s0 + int(np.argmax(seg >= 0.25 * seg.max()))
            return at / sr
    return None


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("recording")
    ap.add_argument("schedule")
    ap.add_argument("--sounds", required=True, help="the folder holding <gun>/<name>.wav (Unity's Resources/SkylineAudio)")
    ap.add_argument("--ears", default=str(Path.home() / ".claude" / "skills" / "ears"))
    ap.add_argument("--tolerance-ms", type=float, default=35.0)
    ap.add_argument("--min-share", type=float, default=0.9)
    ap.add_argument("--range-db", type=float, default=50.0)
    ap.add_argument("--clips", default=",".join(RELOAD_CLIPS), help="the handling clips whose events count ('' = all)")
    a = ap.parse_args(argv)
    try:
        sys.path.insert(0, a.ears)
        import listen  # the ears skill; run this through its uv environment

        sched = json.loads(Path(a.schedule).read_text(encoding="utf-8-sig"))
        audio, sr = listen.load(a.recording)
        onsets = listen.onsets(audio, sr, range_db=a.range_db)
        marker = find_marker(audio, sr)
        if marker is None:
            result = {"ok": False, "note": "no 440 Hz marker beep found in the recording", "onsets": len(onsets)}
        else:
            near = [o for o in onsets if abs(o - marker) <= 0.05]   # measure the marker as the events are measured
            if near:
                marker = min(near, key=lambda o: abs(o - marker))
            detectable = set()
            for sound in {e.get("sound") for e in sched.get("events", []) if e.get("sound")}:
                path = Path(a.sounds) / (sound + ".wav")
                if path.is_file():
                    s_audio, s_sr = listen.load(str(path))
                    if listen.onsets(s_audio, s_sr, range_db=a.range_db):
                        detectable.add(sound)
            clips = set(c for c in a.clips.split(",") if c) or None
            result = check(sched, onsets, detectable, a.tolerance_ms, a.min_share, marker=marker, clips=clips)
    except Exception as e:  # the runner reads the JSON: never a bare traceback instead of it
        result = {"ok": False, "note": f"sync check failed: {type(e).__name__}: {e}"}
    print(json.dumps(result, indent=2))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
