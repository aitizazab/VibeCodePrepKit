using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Presenting
{
    public enum AttractStepKind { Jump, Drive, Wait }

    /// <summary>One step of an attract script: jump to a chapter (then let it settle for
    /// <see cref="seconds"/>), drive a named gameplay segment for <see cref="seconds"/>, or just wait.</summary>
    [Serializable]
    public sealed class AttractStep
    {
        public AttractStepKind kind;
        /// <summary>The chapter (Jump) or the template's driver segment (Drive).</summary>
        public string name = "";
        public float seconds;
    }

    /// <summary>
    /// The template's best moments, played by itself and looped (attract mode, for b-roll): a list of
    /// chapter jumps and driver segments. Plain data; build one with the fluent helpers and check it
    /// against the template's chapters and segments with <see cref="Validate"/>.
    /// </summary>
    [Serializable]
    public sealed class AttractScript
    {
        /// <summary>The game scene whose chapters the jumps name.</summary>
        public string scene = "";
        public List<AttractStep> steps = new List<AttractStep>();

        public AttractScript() { }

        public AttractScript(string scene)
        {
            this.scene = scene ?? "";
        }

        /// <summary>Jump to <paramref name="chapter"/>, then wait <paramref name="settleSeconds"/> (a title card, a fade).</summary>
        public AttractScript Jump(string chapter, float settleSeconds = 1.5f)
        {
            steps.Add(new AttractStep { kind = AttractStepKind.Jump, name = chapter ?? "", seconds = settleSeconds });
            return this;
        }

        public AttractScript Drive(string segment, float seconds)
        {
            steps.Add(new AttractStep { kind = AttractStepKind.Drive, name = segment ?? "", seconds = seconds });
            return this;
        }

        public AttractScript Wait(float seconds)
        {
            steps.Add(new AttractStep { kind = AttractStepKind.Wait, seconds = seconds });
            return this;
        }

        /// <summary>One pass, in seconds (jumps count their settle time).</summary>
        public float TotalSeconds
        {
            get
            {
                float t = 0f;
                for (int i = 0; i < steps.Count; i++) t += Math.Max(0f, steps[i].seconds);
                return t;
            }
        }

        /// <summary>What's wrong with the script ("" lines none): no steps, not starting with a jump
        /// (a loop must start from a known place), a jump to an unknown chapter, an unknown segment,
        /// a segment or wait with no time.</summary>
        public List<string> Validate(ICollection<string> chapters, ICollection<string> segments)
        {
            var problems = new List<string>();
            if (steps == null || steps.Count == 0)
            {
                problems.Add("the attract script for \"" + scene + "\" has no steps");
                return problems;
            }
            if (steps[0].kind != AttractStepKind.Jump) problems.Add("the attract script must start with a chapter jump (it loops from there)");
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                string at = "step " + (i + 1) + " (" + s.kind + " \"" + s.name + "\")";
                switch (s.kind)
                {
                    case AttractStepKind.Jump:
                        if (chapters == null || !chapters.Contains(s.name)) problems.Add(at + ": no chapter by that name");
                        if (s.seconds < 0f) problems.Add(at + ": negative settle time");
                        break;
                    case AttractStepKind.Drive:
                        if (segments == null || !segments.Contains(s.name)) problems.Add(at + ": no driver segment by that name");
                        if (!(s.seconds > 0f)) problems.Add(at + ": needs a duration");
                        break;
                    case AttractStepKind.Wait:
                        if (!(s.seconds > 0f)) problems.Add(at + ": needs a duration");
                        break;
                }
            }
            return problems;
        }
    }

    /// <summary>Walks an attract script step by step, looping back to the start after the last one.</summary>
    public sealed class AttractSequencer
    {
        readonly AttractScript _script;
        int _index = -1;
        int _loops;

        public AttractSequencer(AttractScript script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
        }

        public AttractScript Script => _script;
        /// <summary>The current step's index (-1 before the first <see cref="Next"/>).</summary>
        public int Index => _index;
        /// <summary>Completed passes through the script.</summary>
        public int Loops => _loops;
        public AttractStep Current => _index >= 0 && _index < _script.steps.Count ? _script.steps[_index] : null;

        /// <summary>Moves to the next step (wrapping to the first and counting a loop). Null if the script is empty.</summary>
        public AttractStep Next()
        {
            if (_script.steps.Count == 0) return null;
            _index++;
            if (_index >= _script.steps.Count)
            {
                _index = 0;
                _loops++;
            }
            return _script.steps[_index];
        }

        public void Restart()
        {
            _index = -1;
            _loops = 0;
        }
    }

    /// <summary>Stick input that moves like a hand on a pad: towards the target at a limited rate.</summary>
    public static class NaturalStick
    {
        /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> by at most
        /// <paramref name="ratePerSecond"/> x dt, and keeps it inside the unit circle.</summary>
        public static Vector2 Step(Vector2 current, Vector2 target, float ratePerSecond, float dt)
        {
            float tl = target.Length();
            if (tl > 1f) target /= tl;
            Vector2 d = target - current;
            float len = d.Length();
            float max = Math.Max(0f, ratePerSecond) * Math.Max(0f, dt);
            Vector2 next = len <= max || len < 1e-6f ? target : current + d * (max / len);
            float nl = next.Length();
            return nl > 1f ? next / nl : next;
        }

        /// <summary>The turn input (-1..1) that steers a heading towards a target angle (degrees), easing
        /// off near it so the camera settles instead of snapping. <paramref name="fullTurnAt"/> is the angle
        /// gap that gets full input.</summary>
        public static float Turn(float headingDegrees, float targetDegrees, float fullTurnAt = 45f)
        {
            float gap = DeltaAngle(headingDegrees, targetDegrees);
            float k = fullTurnAt > 0f ? gap / fullTurnAt : Math.Sign(gap);
            return k < -1f ? -1f : k > 1f ? 1f : k;
        }

        /// <summary>The shortest signed difference from <paramref name="from"/> to <paramref name="to"/>, in degrees (-180..180].</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            else if (d <= -180f) d += 360f;
            return d;
        }
    }

    /// <summary>File names for presenter photos: Pictures/JamKit/&lt;game&gt;_&lt;timestamp&gt;.png.</summary>
    public static class PhotoNames
    {
        /// <summary>The game title as a file-name part: letters and digits kept, runs of anything else
        /// become one '-'; "JamKit" when nothing is left.</summary>
        public static string Slug(string title)
        {
            var sb = new System.Text.StringBuilder();
            bool dash = false;
            if (title != null)
                foreach (char c in title)
                {
                    if (char.IsLetterOrDigit(c) && c < 128)
                    {
                        if (dash && sb.Length > 0) sb.Append('-');
                        sb.Append(c);
                        dash = false;
                    }
                    else dash = true;
                }
            return sb.Length > 0 ? sb.ToString() : "JamKit";
        }

        public static string FileName(string title, DateTime when) =>
            Slug(title) + "_" + when.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".png";

        /// <summary><paramref name="fileName"/>, or name-2.png, name-3.png... when it's taken.</summary>
        public static string Unique(string fileName, Func<string, bool> exists)
        {
            if (exists == null || !exists(fileName)) return fileName;
            string stem = fileName, ext = "";
            int dot = fileName.LastIndexOf('.');
            if (dot > 0)
            {
                stem = fileName.Substring(0, dot);
                ext = fileName.Substring(dot);
            }
            for (int i = 2; i < 1000; i++)
            {
                string candidate = stem + "-" + i + ext;
                if (!exists(candidate)) return candidate;
            }
            return stem + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ext;
        }
    }
}
