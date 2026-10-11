using System;
using System.Collections.Generic;

namespace JamKit.VisitingHours
{
    /// <summary>What a cue of the opening does (the Unity layer draws and plays it).</summary>
    public enum OpeningCueKind
    {
        /// <summary>The black over the picture: <c>text</c> "in" lifts it, "out" brings it down, over <c>seconds</c>.</summary>
        Fade,
        /// <summary>The camera moves to the pose named <c>text</c> (pillow, nightstand, away, overhead, table) over <c>seconds</c> (0 = a cut).</summary>
        Camera,
        /// <summary>The phone starts ringing and buzzing (loops until RingStop).</summary>
        Ring,
        RingStop,
        /// <summary>The phone's screen: <c>text</c> incoming, missed, playing, dim (to <c>value</c> over <c>seconds</c>) or off.</summary>
        Phone,
        /// <summary>Nadia's arm: <c>text</c> reach, grab, lift, tap, drop or hide, over <c>seconds</c>.</summary>
        Arm,
        /// <summary>A sound effect by id (<c>text</c>), at <c>value</c> volume.</summary>
        Sfx,
        /// <summary>Starts the voice track named <c>text</c> (voicemail, news): its markers count from this moment.</summary>
        Voice,
        /// <summary>The voicemail moves to the phone's small speaker (thin, a little distorted) by <c>value</c> (1 = all
        /// through the speaker, 0 = back to his clear voice).</summary>
        Speaker,
        /// <summary>Nadia's slow breathing: <c>text</c> on or off.</summary>
        Breathing,
        /// <summary>The view under the blanket: dark to <c>value</c> (1 = under it, 0 = clear) over <c>seconds</c>.</summary>
        Blanket,
        /// <summary>The bedroom's lights (the streetlight, the night) to <c>value</c> (0..1) over <c>seconds</c>.</summary>
        Lights,
        /// <summary>The music blooms in: <c>text</c> a music id ("" = the title music), over <c>seconds</c>.</summary>
        Music,
        /// <summary>A piece of the title table (<c>text</c>: table, tablet, mug, notepad, pen, badge) fades in over <c>seconds</c>.</summary>
        Show,
        /// <summary>The title takes input.</summary>
        Interactive,
        /// <summary>A subtitle line (made from a voice track's markers; <c>speaker</c> names who says it).</summary>
        Subtitle,
        ClearSubtitle,
        /// <summary>The opening is over.</summary>
        End,
        /// <summary>How loud the voicemail is (0..1 of its volume, <c>value</c>) over <c>seconds</c>: it fades as she
        /// drifts off and comes back for the view from above. (Added last: the boot asset stores kinds by number.)</summary>
        VoiceLevel,
    }

    /// <summary>
    /// One cue of the opening. Its time is either <see cref="at"/> (seconds from the start) or, when
    /// <see cref="anchor"/> is set, a voice track's marker plus <see cref="offset"/>:
    /// "voicemail:drop" is the voicemail's "drop" marker, "news:end" the end of the news report,
    /// "voicemail:start" the moment it starts, "news:40%" 40% of the way through it (paced by the recording's
    /// own length, whatever take is in). Re-timing a recording means moving its markers only: every cue
    /// anchored to them follows.
    /// </summary>
    [Serializable]
    public class OpeningCue
    {
        public float at;
        public string anchor = "";
        public float offset;
        public OpeningCueKind kind;
        public string text = "";
        public string speaker = "";
        public float seconds = 1f;
        public float value = 1f;
        public string note = "";

        public OpeningCue Copy() => new OpeningCue
        {
            at = at, anchor = anchor, offset = offset, kind = kind, text = text, speaker = speaker,
            seconds = seconds, value = value, note = note,
        };
    }

    /// <summary>A moment in a recording (seconds from its start), with the line that starts there.</summary>
    [Serializable]
    public class VoiceMarker
    {
        public string name = "";
        public float at;
        public string subtitle = "";
    }

    /// <summary>
    /// A recording and its markers. <see cref="recordedLength"/> is the length of the take the markers
    /// were timed on: if a different take drops in (another length), the markers stretch in proportion
    /// until someone re-times them (the stretch is reported).
    /// </summary>
    [Serializable]
    public class VoiceTrack
    {
        public string name = "";
        public string clip = "";
        public string speaker = "";
        public float recordedLength;
        /// <summary>Seconds of the recording skipped when it plays (a quiet lead-in). Marker times stay in the
        /// recording's own seconds; "start" is the moment playback starts (at this point in the recording).</summary>
        public float trimStart;
        public List<VoiceMarker> markers = new List<VoiceMarker>();

        public VoiceMarker Find(string marker)
        {
            if (markers == null) return null;
            foreach (var m in markers)
                if (m != null && m.name == marker) return m;
            return null;
        }
    }

    /// <summary>Turns the authored cues (absolute or anchored to markers) into absolute times, and re-times markers.</summary>
    public static class OpeningScript
    {
        /// <summary>How far a recording's length may drift from <see cref="VoiceTrack.recordedLength"/> before its markers stretch.</summary>
        public const float LengthTolerance = 0.25f;

        /// <summary>
        /// The cues with absolute times, sorted (stable), plus subtitle cues from the tracks' markers when
        /// <paramref name="subtitles"/> is on. <paramref name="clipLength"/> gives a clip's real length
        /// (0 or less: unknown, the track's recorded length is used). Problems (an anchor that can't be
        /// resolved, a stretched track) go to <paramref name="notes"/> when it isn't null.
        /// </summary>
        public static List<OpeningCue> Resolve(IList<OpeningCue> cues, IList<VoiceTrack> tracks, Func<string, float> clipLength,
            bool subtitles, List<string> notes)
        {
            var result = new List<OpeningCue>();
            if (cues == null) return result;
            var starts = new Dictionary<string, float>();
            var done = new bool[cues.Count];
            var times = new float[cues.Count];
            int left = 0;
            for (int i = 0; i < cues.Count; i++) if (cues[i] != null) left++;
            for (int pass = 0; pass <= cues.Count && left > 0; pass++)
            {
                int before = left;
                for (int i = 0; i < cues.Count; i++)
                {
                    var c = cues[i];
                    if (c == null || done[i]) continue;
                    if (!TryTime(c, tracks, clipLength, starts, out float t, null)) continue;
                    done[i] = true;
                    times[i] = t;
                    left--;
                    if (c.kind == OpeningCueKind.Voice && !starts.ContainsKey(c.text)) starts[c.text] = t;
                }
                if (left == before) break;
            }
            for (int i = 0; i < cues.Count; i++)
            {
                var c = cues[i];
                if (c == null) continue;
                if (!done[i])
                {
                    notes?.Add("cue " + i + " (" + c.kind + " " + c.text + ") is anchored to \"" + c.anchor + "\", which never resolves (an unknown track or marker, or a track that never starts)");
                    continue;
                }
                var copy = c.Copy();
                copy.at = Math.Max(0f, times[i]);
                result.Add(copy);
            }
            // Report stretched tracks once each.
            if (notes != null && tracks != null)
                foreach (var tr in tracks)
                {
                    if (tr == null) continue;
                    float k = Stretch(tr, clipLength);
                    if (Math.Abs(k - 1f) > 1e-4f)
                        notes.Add("the " + tr.name + " recording is " + Length(tr, clipLength).ToString("0.00") + " s, its markers were timed on a " + tr.recordedLength.ToString("0.00") + " s take: they stretch by x" + k.ToString("0.000") + " until they're re-timed");
                }
            if (subtitles && tracks != null)
                foreach (var tr in tracks)
                {
                    if (tr == null || tr.markers == null || !starts.TryGetValue(tr.name, out float s0)) continue;
                    float k = Stretch(tr, clipLength);
                    OpeningCue last = null;
                    foreach (var m in tr.markers)
                    {
                        if (m == null || string.IsNullOrEmpty(m.subtitle)) continue;
                        last = new OpeningCue { at = s0 + Math.Max(0f, m.at * k - tr.trimStart), kind = OpeningCueKind.Subtitle, text = m.subtitle, speaker = tr.speaker, seconds = 0f };
                        result.Add(last);
                    }
                    if (last != null) result.Add(new OpeningCue { at = s0 + Playing(tr, clipLength) + 0.2f, kind = OpeningCueKind.ClearSubtitle, seconds = 0f });
                }
            SortStable(result);
            return result;
        }

        static bool TryTime(OpeningCue c, IList<VoiceTrack> tracks, Func<string, float> clipLength, Dictionary<string, float> starts, out float t, List<string> notes)
        {
            t = c.at;
            if (string.IsNullOrEmpty(c.anchor)) return true;
            if (!SplitAnchor(c.anchor, out string track, out string marker)) return false;
            var tr = FindTrack(tracks, track);
            if (tr == null || !starts.TryGetValue(track, out float s0)) return false;
            if (!MarkerTime(tr, marker, clipLength, out float m)) return false;
            t = s0 + m + c.offset;
            return true;
        }

        /// <summary>"40%" -> 0.4 (clamped to 0..1); false for anything else.</summary>
        public static bool Percent(string marker, out float share)
        {
            share = 0f;
            if (string.IsNullOrEmpty(marker) || marker[marker.Length - 1] != '%') return false;
            if (!float.TryParse(marker.Substring(0, marker.Length - 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float p)) return false;
            share = Math.Max(0f, Math.Min(1f, p / 100f));
            return true;
        }

        /// <summary>"voicemail:drop" -> ("voicemail", "drop").</summary>
        public static bool SplitAnchor(string anchor, out string track, out string marker)
        {
            track = marker = "";
            if (string.IsNullOrEmpty(anchor)) return false;
            int colon = anchor.IndexOf(':');
            if (colon <= 0 || colon == anchor.Length - 1) return false;
            track = anchor.Substring(0, colon).Trim();
            marker = anchor.Substring(colon + 1).Trim();
            return track.Length > 0 && marker.Length > 0;
        }

        public static VoiceTrack FindTrack(IList<VoiceTrack> tracks, string name)
        {
            if (tracks == null) return null;
            foreach (var t in tracks)
                if (t != null && t.name == name) return t;
            return null;
        }

        /// <summary>The recording's real length (or the recorded length when it's unknown).</summary>
        public static float Length(VoiceTrack track, Func<string, float> clipLength)
        {
            float real = clipLength != null ? clipLength(track.clip) : 0f;
            return real > 0f ? real : track.recordedLength;
        }

        /// <summary>How long the recording plays (its length less <see cref="VoiceTrack.trimStart"/>).</summary>
        public static float Playing(VoiceTrack track, Func<string, float> clipLength) => Math.Max(0f, Length(track, clipLength) - Math.Max(0f, track.trimStart));

        /// <summary>How much the markers stretch: 1 unless the recording's length differs from the take they were timed on.</summary>
        public static float Stretch(VoiceTrack track, Func<string, float> clipLength)
        {
            float real = clipLength != null ? clipLength(track.clip) : 0f;
            if (!(real > 0f) || !(track.recordedLength > 0f)) return 1f;
            if (Math.Abs(real - track.recordedLength) <= LengthTolerance) return 1f;
            return real / track.recordedLength;
        }

        /// <summary>A marker's time from the moment the track starts playing ("start" = 0, "end" = when it finishes,
        /// "40%" that share of how long it plays; a named marker's own time less the trimmed lead-in).</summary>
        public static bool MarkerTime(VoiceTrack track, string marker, Func<string, float> clipLength, out float seconds)
        {
            seconds = 0f;
            if (marker == "start") return true;
            if (marker == "end")
            {
                seconds = Playing(track, clipLength);
                return true;
            }
            if (Percent(marker, out float share))
            {
                seconds = Playing(track, clipLength) * share;
                return true;
            }
            var m = track.Find(marker);
            if (m == null) return false;
            seconds = Math.Max(0f, m.at * Stretch(track, clipLength) - Math.Max(0f, track.trimStart));
            return true;
        }

        /// <summary>
        /// Re-times a track to a new take: <paramref name="taps"/> are the moments (seconds from when the
        /// track started playing) someone marked, one per marker in order; the markers keep the recording's
        /// own seconds (the tap plus the trimmed lead-in). Sets the recorded length too.
        /// False (nothing changed) when the counts differ or the taps go backwards.
        /// </summary>
        public static bool Retime(VoiceTrack track, IList<float> taps, float newLength)
        {
            if (track == null || track.markers == null || taps == null || taps.Count != track.markers.Count) return false;
            for (int i = 0; i < taps.Count; i++)
            {
                if (taps[i] < 0f) return false;
                if (i > 0 && taps[i] < taps[i - 1]) return false;
            }
            for (int i = 0; i < taps.Count; i++) track.markers[i].at = taps[i] + Math.Max(0f, track.trimStart);
            if (newLength > 0f) track.recordedLength = newLength;
            return true;
        }

        /// <summary>
        /// Data problems (for the editor's checks and tests): anchors that never resolve, markers out of
        /// order or past the end of their recording, no End or no Interactive cue, an End that comes before
        /// a voice finishes.
        /// </summary>
        public static List<string> Problems(IList<OpeningCue> cues, IList<VoiceTrack> tracks, Func<string, float> clipLength)
        {
            var problems = new List<string>();
            var resolved = Resolve(cues, tracks, clipLength, false, problems);
            // Stretching is a note, not a problem.
            problems.RemoveAll(p => p.Contains("stretch by"));
            if (tracks != null)
                foreach (var tr in tracks)
                {
                    if (tr == null) continue;
                    if (string.IsNullOrEmpty(tr.name)) problems.Add("a voice track has no name");
                    float len = Length(tr, clipLength);
                    float k = Stretch(tr, clipLength);
                    float prev = -1f;
                    if (tr.markers == null) continue;
                    foreach (var m in tr.markers)
                    {
                        if (m == null) continue;
                        if (m.name == "start" || m.name == "end") problems.Add(tr.name + ": \"" + m.name + "\" is a reserved marker name");
                        if (m.at * k < tr.trimStart) problems.Add(tr.name + ": marker \"" + m.name + "\" is in the trimmed lead-in (before " + tr.trimStart.ToString("0.00") + " s)");
                        if (m.at < prev) problems.Add(tr.name + ": marker \"" + m.name + "\" comes before the one above it");
                        if (len > 0f && m.at * k > len + 0.01f) problems.Add(tr.name + ": marker \"" + m.name + "\" (" + m.at.ToString("0.00") + " s) is past the end of the recording (" + len.ToString("0.00") + " s)");
                        prev = m.at;
                    }
                }
            bool end = false, interactive = false;
            float endAt = 0f;
            foreach (var c in resolved)
            {
                if (c.kind == OpeningCueKind.End) { end = true; endAt = Math.Max(endAt, c.at); }
                if (c.kind == OpeningCueKind.Interactive) interactive = true;
            }
            if (!end) problems.Add("no End cue");
            if (!interactive) problems.Add("no Interactive cue (the title would never take input)");
            foreach (var c in resolved)
            {
                if (c.kind != OpeningCueKind.Voice) continue;
                var tr = FindTrack(tracks, c.text);
                if (tr == null) { problems.Add("a Voice cue plays \"" + c.text + "\", which isn't a track"); continue; }
                if (end && c.at + Playing(tr, clipLength) > endAt + 0.5f)
                    problems.Add("the " + tr.name + " recording runs past the End cue");
            }
            return problems;
        }

        /// <summary>Stable insertion sort by time, so cues authored at the same moment keep their order.</summary>
        public static void SortStable(List<OpeningCue> cues)
        {
            for (int i = 1; i < cues.Count; i++)
            {
                var c = cues[i];
                int j = i - 1;
                while (j >= 0 && cues[j].at > c.at)
                {
                    cues[j + 1] = cues[j];
                    j--;
                }
                cues[j + 1] = c;
            }
        }
    }

    /// <summary>
    /// The opening as a timeline of absolute cues (see <see cref="OpeningScript.Resolve"/>).
    /// <see cref="Advance"/> moves time on and appends every cue it crossed, in order, so a long frame
    /// never drops a cue.
    /// </summary>
    public sealed class OpeningTimeline
    {
        readonly List<OpeningCue> _cues = new List<OpeningCue>();
        int _next;
        float _time;

        public OpeningTimeline(IEnumerable<OpeningCue> cues)
        {
            if (cues != null)
                foreach (var c in cues)
                    if (c != null) _cues.Add(c.Copy());
            OpeningScript.SortStable(_cues);
        }

        public IReadOnlyList<OpeningCue> Cues => _cues;
        public float Time => _time;
        public bool Finished => _next >= _cues.Count;

        /// <summary>When the last cue fires (the End cue, normally).</summary>
        public float Length => _cues.Count > 0 ? _cues[_cues.Count - 1].at : 0f;

        /// <summary>Moves time on by <paramref name="dt"/>; appends the cues that came due. Returns how many.</summary>
        public int Advance(float dt, List<OpeningCue> fired)
        {
            if (dt > 0f) _time += dt;
            int n = 0;
            while (_next < _cues.Count && _cues[_next].at <= _time)
            {
                fired.Add(_cues[_next]);
                _next++;
                n++;
            }
            return n;
        }

        /// <summary>The first cue of <paramref name="kind"/> (and <paramref name="text"/>, if given), or null.</summary>
        public OpeningCue First(OpeningCueKind kind, string text = null)
        {
            foreach (var c in _cues)
                if (c.kind == kind && (text == null || c.text == text)) return c;
            return null;
        }

        /// <summary>Jumps to the end, appending the cues still to fire that leave the title in its final
        /// state (the ring stops, every table piece shows, the music, Interactive, End); voices, sounds,
        /// subtitles and the bedroom's moves are dropped.</summary>
        public void SkipToEnd(List<OpeningCue> fired)
        {
            for (; _next < _cues.Count; _next++)
            {
                var c = _cues[_next];
                if (c.kind == OpeningCueKind.RingStop || c.kind == OpeningCueKind.Show || c.kind == OpeningCueKind.Music
                    || c.kind == OpeningCueKind.Interactive || c.kind == OpeningCueKind.ClearSubtitle || c.kind == OpeningCueKind.End) fired.Add(c);
            }
            _time = Math.Max(_time, Length);
        }
    }
}
