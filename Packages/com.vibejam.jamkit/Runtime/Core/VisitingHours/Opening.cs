using System;
using System.Collections.Generic;

namespace JamKit.VisitingHours
{
    /// <summary>What a cue of the opening does (the Unity layer draws and plays it).</summary>
    public enum OpeningCueKind
    {
        /// <summary>The phone starts ringing (loops until RingStop).</summary>
        Ring,
        RingStop,
        /// <summary>Nadia's eyes open: the dark ceiling fades in over <c>seconds</c>.</summary>
        EyesOpen,
        /// <summary>The phone's screen shows <c>text</c> (shows the phone if hidden).</summary>
        PhoneText,
        PhoneHide,
        /// <summary>Plays the voice clip named <c>text</c> (vo_elias_voicemail...); silence when it's missing.</summary>
        Voice,
        /// <summary>A subtitle line; <c>speaker</c> names who says it (empty: the caller on the phone).</summary>
        Subtitle,
        ClearSubtitle,
        /// <summary>She starts to sit up (the view rises a little).</summary>
        SitUp,
        /// <summary>She sinks back into the pillow.</summary>
        SinkBack,
        /// <summary>Fade to black over <c>seconds</c>.</summary>
        FadeOut,
        /// <summary>The opening is over: the title screen comes up.</summary>
        End,
    }

    /// <summary>One timed cue of the opening (seconds from its start).</summary>
    [Serializable]
    public class OpeningCue
    {
        public float at;
        public OpeningCueKind kind;
        public string text = "";
        public string speaker = "";
        public float seconds = 1f;
    }

    /// <summary>
    /// The opening as a timeline of cues (no player input until the title). <see cref="Advance"/>
    /// moves time on and appends every cue it crossed, in order, so a long frame never drops a cue.
    /// The voicemail's subtitles can be stretched to the real recording's length with <see cref="ScaleSpan"/>.
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
                    if (c != null) _cues.Add(new OpeningCue { at = c.at, kind = c.kind, text = c.text, speaker = c.speaker, seconds = c.seconds });
            Sort();
        }

        public IReadOnlyList<OpeningCue> Cues => _cues;
        public float Time => _time;
        public bool Finished => _next >= _cues.Count;

        /// <summary>When the last cue fires (the End cue, normally).</summary>
        public float Length => _cues.Count > 0 ? _cues[_cues.Count - 1].at : 0f;

        /// <summary>Stable sort by time, so cues authored at the same moment keep their order.</summary>
        void Sort()
        {
            for (int i = 1; i < _cues.Count; i++)
            {
                var c = _cues[i];
                int j = i - 1;
                while (j >= 0 && _cues[j].at > c.at)
                {
                    _cues[j + 1] = _cues[j];
                    j--;
                }
                _cues[j + 1] = c;
            }
        }

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

        /// <summary>Jumps to the end, appending the cues still to fire that leave things in their final
        /// state (stops and hides); voices and subtitles are dropped.</summary>
        public void SkipToEnd(List<OpeningCue> fired)
        {
            for (; _next < _cues.Count; _next++)
            {
                var c = _cues[_next];
                if (c.kind == OpeningCueKind.RingStop || c.kind == OpeningCueKind.PhoneHide || c.kind == OpeningCueKind.ClearSubtitle
                    || c.kind == OpeningCueKind.FadeOut || c.kind == OpeningCueKind.End) fired.Add(c);
            }
            _time = Math.Max(_time, Length);
        }

        /// <summary>
        /// Stretches the cues from the first <see cref="OpeningCueKind.Voice"/> cue playing
        /// <paramref name="voice"/> up to (not including) the next cue of <paramref name="untilKind"/> after it so
        /// that span lasts <paramref name="newLength"/> seconds, and shifts every later cue by the difference.
        /// For fitting the placeholder subtitles to a real recording. Returns false when the voice isn't there.
        /// </summary>
        public bool ScaleSpan(string voice, OpeningCueKind untilKind, float newLength)
        {
            int start = -1;
            for (int i = 0; i < _cues.Count; i++)
                if (_cues[i].kind == OpeningCueKind.Voice && _cues[i].text == voice) { start = i; break; }
            if (start < 0 || !(newLength > 0f)) return false;
            int end = -1;
            for (int i = start + 1; i < _cues.Count; i++)
                if (_cues[i].kind == untilKind) { end = i; break; }
            if (end < 0) return false;
            float t0 = _cues[start].at, t1 = _cues[end].at;
            float old = t1 - t0;
            if (!(old > 0f)) return false;
            float k = newLength / old;
            for (int i = start + 1; i < end; i++) _cues[i].at = t0 + (_cues[i].at - t0) * k;
            float shift = newLength - old;
            for (int i = end; i < _cues.Count; i++) _cues[i].at += shift;
            return true;
        }
    }
}
