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
MARKER_SNAP = 0.012   # s: the marker snaps to an ears onset only this close (the beep's own)


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


def check(schedule, onsets, detectable=None, tolerance_ms=35.0, min_share=0.9, marker=None, clips=None, missing=None):
    """marker: the marker beep's time in the recording (None: the first onset). clips: the handling clips whose
    events count (None: all). missing: sounds with no rendered clip on disk; one among the counted events fails the
    check (a wrong --sounds folder must not quietly shrink the share)."""
    events = list(schedule.get("events", []))
    chosen = [e for e in events if clips is None or e.get("clip") in clips]
    missing = set(missing or ())
    absent = sorted({str(e.get("sound")) for e in chosen if e.get("sound") in missing})
    skipped = [e for e in chosen if e.get("sound") not in missing and detectable is not None and e.get("sound") not in detectable]
    checked = [e for e in chosen if e.get("sound") not in missing and (detectable is None or e.get("sound") in detectable)]
    base = {"events": len(events), "checked": len(checked), "undetectable": sorted({str(e.get("sound")) for e in skipped}),
            "missing_sounds": absent}
    if absent:
        return {**base, "ok": False, "matched": 0, "note": "no rendered clip for: " + ", ".join(absent[:10])}
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
        if abs(rest[nearest] - marker) <= MARKER_SNAP:     # the beep's own onset is not an event's
            del rest[nearest]
    times = [marker + float(e["t"]) for e in checked]
    errors = match(times, rest, tol)
    hit = sorted(abs(x) for x in errors if x is not None)
    missed = [f'{e.get("gun")}/{e.get("clip")}/{e.get("id")}@{float(e["t"]):.3f}' for e, x in zip(checked, errors) if x is None]
    share = len(hit) / len(checked)
    # The chance that an event lands on an unrelated onset: the onsets' density over the schedule's span times the
    # matching window. High means dense onsets could hide a desync.
    lo, hi = min(times) - tol, max(times) + tol
    inside = [o for o in rest if lo <= o <= hi]
    chance = min(1.0, max(0, len(inside) - len(hit)) / (hi - lo) * 2 * tol)   # the onsets no event took
    out = {
        **base,
        "ok": share >= min_share,
        "matched": len(hit),
        "share": round(share, 3),
        "median_ms": round(hit[len(hit) // 2] * 1000, 1) if hit else None,
        "worst_ms": round(hit[-1] * 1000, 1) if hit else None,
        "missed": missed[:20],
        "marker_s": round(marker, 3),
        "chance_hit": round(chance, 3),
    }
    out["note"] = (f'{out["matched"]}/{out["checked"]} events within {tolerance_ms:g} ms (median {out["median_ms"]} ms, '
                   f'worst {out["worst_ms"]} ms; {len(base["undetectable"])} sounds undetectable alone; '
                   f'chance hit {chance:.0%})')
    if chance > 0.2:
        out["note"] += "; onsets are dense: a chance match could hide a desync"
    return out


def _band_envelope(seg, sr, freq, half_width=150.0, taper=50.0):
    """|seg| after a smooth band-pass around freq (an FFT mask with raised-cosine edges, so the filter's own ringing
    stays a few ms long)."""
    import numpy as np

    spec = np.fft.rfft(seg)
    f = np.fft.rfftfreq(len(seg), 1.0 / sr)
    d = np.abs(f - freq)
    mask = np.where(d <= half_width, 1.0,
                    np.where(d <= half_width + taper, 0.5 * (1 + np.cos(np.pi * (d - half_width) / taper)), 0.0))
    return np.abs(np.fft.irfft(spec * mask, len(seg)))


def find_marker(audio, sr, freq=440.0, frame_ms=40.0, hop_ms=5.0, band_hz=30.0, min_ratio=0.5, min_ms=30.0,
                floor_db=-50.0, chunk_frames=1000):
    """The start (s) of the first stretch of at least min_ms where the sound is mostly a tone at freq (the marker
    beep), or None. A frame counts when the energy within band_hz of freq is at least min_ratio of the frame's energy
    and the tone's level is above floor_db (dBFS RMS); the first such run wins, so a louder 440 Hz sound later can't
    displace it. The start is refined on a band-passed copy of that run alone (the first sample at a quarter of the
    run's tonal peak, the ears' onset rule), so a loud sound right after the beep can't move it. The scan goes in
    chunks and stops at the first run, so a long recording is never held as one frame matrix."""
    import numpy as np

    x = np.asarray(audio, dtype=np.float64)
    if x.ndim == 2:
        x = x.mean(axis=1)
    win = max(16, int(sr * frame_ms / 1000))
    hop = max(1, int(sr * hop_ms / 1000))
    if len(x) < win:
        return None
    starts = np.arange(0, len(x) - win + 1, hop)
    window = np.hanning(win)
    freqs = np.fft.rfftfreq(win, 1.0 / sr)
    band = (freqs >= freq - band_hz) & (freqs <= freq + band_hz)
    floor = 10 ** (floor_db / 20)
    need = max(1, int(round(min_ms / hop_ms)))
    offs = np.arange(win)
    run, first, last, done = 0, -1, -1, False
    for c in range(0, len(starts), chunk_frames):
        idx = starts[c:c + chunk_frames]
        raw = x[idx[:, None] + offs]
        spec = np.abs(np.fft.rfft(raw * window, axis=1)) ** 2
        total = spec.sum(axis=1)
        narrow = spec[:, band].sum(axis=1)
        rms = np.sqrt((raw ** 2).mean(axis=1))
        ratio = np.divide(narrow, total, out=np.zeros_like(total), where=total > 1e-20)
        good = (ratio >= min_ratio) & (np.sqrt(ratio) * rms >= floor)
        for k in range(len(idx)):
            if good[k]:
                run += 1
                if run == 1:
                    first = c + k
                last = c + k
            elif run >= need:
                done = True
                break
            else:
                run = 0
        if done:
            break
    if run < need:
        return None
    s0 = int(starts[first])
    s1 = int(starts[last]) + win
    pad = int(0.05 * sr)
    a = max(0, s0 - pad)
    env = _band_envelope(x[a:s1 + pad], sr, freq)
    tone = env[s0 - a:s1 - a]
    at = s0 + int(np.argmax(tone >= 0.25 * tone.max()))
    return at / sr


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
            near = [o for o in onsets if abs(o - marker) <= MARKER_SNAP]   # measure the marker as the events are measured
            if near:
                marker = min(near, key=lambda o: abs(o - marker))
            clips = set(c for c in a.clips.split(",") if c) or None
            detectable, missing = set(), set()
            for sound in {e.get("sound") for e in sched.get("events", []) if e.get("sound")}:
                path = Path(a.sounds) / (sound + ".wav")
                if not path.is_file():
                    missing.add(sound)
                    continue
                s_audio, s_sr = listen.load(str(path))
                if listen.onsets(s_audio, s_sr, range_db=a.range_db):
                    detectable.add(sound)
            result = check(sched, onsets, detectable, a.tolerance_ms, a.min_share, marker=marker, clips=clips,
                           missing=missing)
    except Exception as e:  # the runner reads the JSON: never a bare traceback instead of it
        result = {"ok": False, "note": f"sync check failed: {type(e).__name__}: {e}"}
    print(json.dumps(result, indent=2))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
