using System;
using System.Collections.Generic;

namespace JamKit.Patterns
{
    [Serializable]
    public class PatternStep
    {
        /// <summary>Seconds after the sequence (or this loop pass) starts.</summary>
        public float time;
        public PatternDef pattern = new PatternDef();
    }

    /// <summary>
    /// A timed script of patterns: a boss phase or an enemy's attack routine. Steps may overlap.
    /// When looping, the next pass starts loopDelay after the last volley of the current one.
    /// </summary>
    [Serializable]
    public class PatternSequence
    {
        public List<PatternStep> steps = new List<PatternStep>();
        /// <summary>Repeat forever. Ignored if any step fires forever (it would never end a pass).</summary>
        public bool loop;
        public float loopDelay = 0.5f;

        /// <summary>For building sequences in code: <c>new PatternSequence().Add(0, a).Add(1.5f, b)</c>.</summary>
        public PatternSequence Add(float time, PatternDef pattern)
        {
            steps.Add(new PatternStep { time = time, pattern = pattern });
            return this;
        }

        /// <summary>Time of the last volley of one pass; infinity if any step fires forever.</summary>
        public float Duration
        {
            get
            {
                float end = 0f;
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step == null || step.pattern == null) continue;
                    end = Math.Max(end, Math.Max(0f, step.time) + step.pattern.Duration);
                }
                return end;
            }
        }

        public static PatternSequence Single(PatternDef pattern) => new PatternSequence().Add(0f, pattern);
    }
}
