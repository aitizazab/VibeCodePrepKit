import numpy as np
import pytest

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


def beep(sr, seconds, amp=0.4, fade=0.005):
    t = np.arange(int(seconds * sr)) / sr
    env = np.minimum(1.0, t / fade) * np.minimum(1.0, (seconds - t) / 0.01)
    return amp * np.sin(2 * np.pi * 440 * t) * env


@pytest.mark.parametrize("sr", [48000, 44100])
def test_a_loud_sound_right_after_a_short_beep_does_not_move_the_marker(sr):
    audio = np.zeros(sr * 4)
    b = beep(sr, 0.08)
    audio[int(2.0 * sr):int(2.0 * sr) + len(b)] += b                                   # Task 14's 80 ms beep at 2.0 s
    burst = np.random.default_rng(5).normal(0, 0.8, int(0.05 * sr))                     # louder, 30 ms after it ends
    audio[int(2.11 * sr):int(2.11 * sr) + len(burst)] += burst
    m = sc.find_marker(audio, sr)
    assert m is not None and abs(m - 2.0) < 0.004, m


def test_the_first_beep_wins_over_a_louder_tone_later():
    sr = 48000
    audio = np.zeros(sr * 6)
    q = beep(sr, 0.08, amp=0.05)
    audio[int(2.0 * sr):int(2.0 * sr) + len(q)] += q
    loud = beep(sr, 0.5, amp=0.8)
    audio[int(4.0 * sr):int(4.0 * sr) + len(loud)] += loud
    m = sc.find_marker(audio, sr)
    assert m is not None and abs(m - 2.0) < 0.004, m


def test_a_marker_onset_farther_than_the_snap_stays_an_event():
    onsets = [5.0, 5.03, 6.0]                  # 5.03 is 30 ms from the marker: not the beep's own onset
    s = {"events": [{"t": 0.03, "gun": "writ", "clip": "Fire", "id": "a", "sound": "writ/shot"},
                    {"t": 1.0, "gun": "writ", "clip": "Fire", "id": "b", "sound": "writ/shot"}]}
    r = sc.check(s, onsets, marker=5.0)
    assert r["ok"] and r["matched"] == 2


def test_a_counted_sound_with_no_rendered_clip_fails():
    s = {"events": [{"t": 1.0, "gun": "writ", "clip": "ReloadEmpty", "id": "mag_out", "sound": "writ/mag_out"},
                    {"t": 2.0, "gun": "writ", "clip": "Fire", "id": "shot", "sound": "writ/shot"}]}
    r = sc.check(s, [5.0, 6.0], detectable={"writ/mag_out"}, clips={"ReloadEmpty"}, missing={"writ/shot"})
    assert r["ok"], "a missing clip outside the counted clips doesn't matter"
    r = sc.check(s, [5.0, 6.0], detectable=set(), clips={"ReloadEmpty"}, missing={"writ/mag_out"})
    assert not r["ok"] and r["missing_sounds"] == ["writ/mag_out"] and "no rendered clip" in r["note"]


def test_the_chance_of_a_stray_match_is_reported():
    s = schedule((1.0, "writ/shot"), (2.0, "writ/shot"))
    sparse = sc.check(s, [5.0, 6.0, 7.0])
    dense = sc.check(s, [5.0] + [6.0 + i * 0.05 for i in range(21)])
    assert sparse["chance_hit"] < 0.1 and dense["chance_hit"] > 0.5 and "dense" in dense["note"]


def test_no_beep_means_no_marker():
    sr = 48000
    audio = np.random.default_rng(1).normal(0, 0.1, sr * 2)
    assert sc.find_marker(audio, sr) is None
