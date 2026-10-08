using System;
using System.Collections.Generic;
using System.Globalization;

namespace JamKit.Flow
{
    /// <summary>One "label: value" line on the game-over and victory screens ("Coins: 2 / 3").</summary>
    [Serializable]
    public struct RunStat
    {
        public string label;
        public string value;

        public RunStat(string label, string value)
        {
            this.label = label;
            this.value = value;
        }
    }

    /// <summary>
    /// The numbers of the current run: play time (advanced only while Playing), score, and any extra
    /// lines the game wants on the end screens. Reset on every run start.
    /// </summary>
    public sealed class RunStats
    {
        public float time;
        public int score;
        readonly List<RunStat> _extra = new List<RunStat>();

        public IReadOnlyList<RunStat> Extra => _extra;

        public void Reset()
        {
            time = 0f;
            score = 0;
            _extra.Clear();
        }

        public void Tick(float dt)
        {
            if (dt > 0f) time += dt;
        }

        /// <summary>Adds a line, or replaces the value of the line with the same label.</summary>
        public void Set(string label, string value)
        {
            for (int i = 0; i < _extra.Count; i++)
            {
                if (_extra[i].label != label) continue;
                _extra[i] = new RunStat(label, value);
                return;
            }
            _extra.Add(new RunStat(label, value));
        }

        /// <summary>"0:07", "1:05", "1:02:03". Rounds down to whole seconds.</summary>
        public static string FormatTime(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) seconds = 0f;
            int total = (int)Math.Floor(seconds);
            int h = total / 3600, m = (total / 60) % 60, s = total % 60;
            return h > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", h, m, s)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", m, s);
        }

        /// <summary>"1:05.3": for best times and speedrun-style victory screens.</summary>
        public static string FormatTimePrecise(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) seconds = 0f;
            int tenths = (int)Math.Floor(seconds * 10f) % 10;
            return FormatTime(seconds) + "." + tenths.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Player-facing words for the difficulty options.</summary>
    public static class DifficultyText
    {
        public static string Name(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Story: return "Story";
                case Difficulty.Hard: return "Hard";
                default: return "Normal";
            }
        }

        /// <summary>One line on what changes, shown under the option.</summary>
        public static string Describe(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Story: return "Enemies are slower and fewer, you take half damage and get extra lives.";
                case Difficulty.Hard: return "Faster, denser enemies, more damage taken and no extra lives.";
                default: return "The game as designed.";
            }
        }

        public static Difficulty Next(Difficulty d, int step)
        {
            int count = 3;
            int i = (((int)d + step) % count + count) % count;
            return (Difficulty)i;
        }
    }
}
