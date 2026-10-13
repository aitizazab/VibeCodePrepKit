import numpy as np

import synccheck as sc


def schedule(*events):
    return {"events": [{"t": t, "gun": "writ", "clip": "Fire", "id": f"e{i}", "sound": s} for i, (t, s) in enumerate(events)]}


def test_match_takes_the_nearest_unused_onset_within_tolerance():
    assert sc.match([1.0, 2.0], [0.99, 1.02, 2.5], 0.035) == [-0.01, None]
    errs = sc.match([1.0, 1.01], [1.0], 0.035)
    assert errs[0] == 0.0 and errs[1] is None, "an onset is used once"


def test_events_are_aligned_on_the_marker():
    onsets = [5.0, 6.0, 7.51]                  # the marker at 5.0, events 1.0 and 2.5 s after it
    r = sc.check(schedule((1.0, "writ/shot"), (2.5, "writ/mag_out")), onsets)
    assert r["ok"] and r["matched"] == 2 and r["worst_ms"] == 10.0


def test_a_late_event_is_missed_and_named():
    r = sc.check(schedule((1.0, "writ/shot"), (2.0, "writ/seat")), [5.0, 6.0, 7.2], min_share=0.9)
    assert not r["ok"] and r["matched"] == 1
    assert "writ/Fire/e1@2.000" in r["missed"]


def test_undetectable_sounds_are_left_out_of_the_share():
    r = sc.check(schedule((1.0, "writ/shot"), (2.0, "writ/inspect_turn")), [5.0, 6.0], detectable={"writ/shot"})
    assert r["ok"] and r["checked"] == 1 and r["undetectable"] == ["writ/inspect_turn"]


def test_silence_or_an_empty_schedule_fails_with_a_note():
    assert not sc.check(schedule((1.0, "writ/shot")), [])["ok"]
    assert "silent" in sc.check(schedule((1.0, "writ/shot")), [])["note"]
    assert not sc.check({"events": []}, [1.0, 2.0])["ok"]


# Amendment 4: the marker is found by its 440 Hz energy, not taken as the first onset; only reload events count.

def test_a_given_marker_replaces_the_first_onset_and_its_own_onset_is_not_an_event():
    onsets = [0.5, 5.0, 6.0, 7.51]             # a stray sound at 0.5 before the beep at 5.0
    r = sc.check(schedule((1.0, "writ/shot"), (2.5, "writ/mag_out")), onsets, marker=5.0)
    assert r["ok"] and r["matched"] == 2 and r["marker_s"] == 5.0


def test_only_the_chosen_clips_count():
    s = {"events": [
        {"t": 1.0, "gun": "writ", "clip": "Fire", "id": "shot", "sound": "writ/shot"},
        {"t": 2.0, "gun": "writ", "clip": "ReloadEmpty", "id": "mag_out", "sound": "writ/mag_out"},
    ]}
    r = sc.check(s, [5.0, 7.0], clips={"ReloadEmpty", "ReloadPartial"})
    assert r["ok"] and r["checked"] == 1 and r["matched"] == 1 and r["events"] == 2


def test_the_marker_is_found_by_its_440_hz_energy():
    sr = 48000
    audio = np.zeros(sr * 4)
    rng = np.random.default_rng(3)
    audio[int(0.7 * sr):int(0.75 * sr)] += rng.normal(0, 0.5, int(0.05 * sr))      # a click before the beep
    t = np.arange(int(0.5 * sr)) / sr
    audio[int(2.0 * sr):int(2.5 * sr)] += 0.4 * np.sin(2 * np.pi * 440 * t)          # the marker beep at 2.0 s
    audio[int(3.0 * sr):int(3.05 * sr)] += rng.normal(0, 0.5, int(0.05 * sr))       # a gun sound after it
    m = sc.find_marker(audio, sr)
    assert m is not None and abs(m - 2.0) < 0.02


def test_no_beep_means_no_marker():
    sr = 48000
    audio = np.random.default_rng(1).normal(0, 0.1, sr * 2)
    assert sc.find_marker(audio, sr) is None
