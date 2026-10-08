using System;

namespace JamKit.Narrative
{
    /// <summary>
    /// "Iris: You're late." gives speaker "Iris" and text "You're late.", when Iris is a known
    /// character (by id or display name). Anything else stays narration as written, so "Note: the
    /// door is open" isn't split unless there's a character called Note.
    /// </summary>
    public static class StoryLine
    {
        public const int MaxSpeakerLength = 24;

        public static bool TrySplitSpeaker(string line, Func<string, bool> isSpeaker, out string speaker, out string text)
        {
            speaker = "";
            text = line ?? "";
            if (string.IsNullOrEmpty(line) || isSpeaker == null) return false;
            int colon = line.IndexOf(':');
            if (colon <= 0 || colon > MaxSpeakerLength || colon + 1 >= line.Length || line[colon + 1] != ' ') return false;
            string candidate = line.Substring(0, colon).Trim();
            if (candidate.Length == 0 || candidate.IndexOfAny(new[] { '<', '>', '{', '}', '"', '(', ')' }) >= 0) return false;
            if (!isSpeaker(candidate)) return false;
            speaker = candidate;
            text = line.Substring(colon + 1).Trim();
            return text.Length > 0;
        }

        /// <summary>"Irsi: hello" looks like a speaker prefix: one capitalised word before ": ".</summary>
        public static bool LooksLikeSpeakerPrefix(string line, out string candidate)
        {
            candidate = "";
            if (string.IsNullOrEmpty(line)) return false;
            int colon = line.IndexOf(':');
            if (colon <= 0 || colon > 14 || colon + 1 >= line.Length || line[colon + 1] != ' ') return false;
            string c = line.Substring(0, colon);
            if (c.IndexOf(' ') >= 0 || !char.IsUpper(c[0])) return false;
            foreach (char ch in c) if (!char.IsLetter(ch) && ch != '_' && ch != '-') return false;
            candidate = c;
            return true;
        }
    }
}
