using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JamKit.Argument
{
    /// <summary>How a battle ended (doc 3.4), plus Yielded: Nadia conceded the claim itself.</summary>
    public enum ArgumentOutcome { None, Convinced, HeardOut, Pressed, ComposureLost, Yielded }

    /// <summary>Where the opponent stands afterwards: what later battles read (Lena's blessing, or not).</summary>
    public enum ArgumentStance { None, Amenable, Unsure, Condemns }

    /// <summary>One battle's result, kept in the save so a later battle can read it.</summary>
    [Serializable]
    public class ArgumentRecord
    {
        public string id = "";
        public ArgumentOutcome outcome;
        public ArgumentStance stance;
        /// <summary>0..3: how much Nadia antagonised them.</summary>
        public int anger;
        public int presses;
        /// <summary>The truth won (Convinced) or "".</summary>
        public string truth = "";
        /// <summary>The personal entry won (Heard out) or "".</summary>
        public string personal = "";
        public int keysBroken;
        public ArgumentDifficulty difficulty;
        /// <summary>How many times it's been played to an end (a re-dive after Composure lost counts again).</summary>
        public int plays;

        public ArgumentRecord Clone() => (ArgumentRecord)MemberwiseClone();
    }

    /// <summary>
    /// Every battle's last result, by battle id. Lives in the shell save as one text entry
    /// (<see cref="SaveKey"/>, SaveData.SetEntry): one line per battle, fields split by '|'. Plain text, so the
    /// core reads and writes it without Unity and a hand-edited or damaged save just loses its bad lines.
    /// </summary>
    public sealed class ArgumentLedger
    {
        public const string SaveKey = "vh_arguments";
        const int Fields = 10;

        readonly List<ArgumentRecord> _records = new List<ArgumentRecord>();

        public IReadOnlyList<ArgumentRecord> Records => _records;

        /// <summary>The record for <paramref name="id"/>, or null if that battle hasn't ended yet.</summary>
        public ArgumentRecord Get(string id)
        {
            foreach (var r in _records)
                if (r.id == id) return r;
            return null;
        }

        /// <summary>Where they stand after their battle (None if it hasn't happened).</summary>
        public ArgumentStance StanceOf(string id)
        {
            var r = Get(id);
            return r != null ? r.stance : ArgumentStance.None;
        }

        /// <summary>True if any battle won this truth (later challenges can need it: Lena's L3'' and Walt's truth).</summary>
        public bool HasTruth(string truthId)
        {
            if (string.IsNullOrEmpty(truthId)) return false;
            foreach (var r in _records)
                if (r.truth == truthId) return true;
            return false;
        }

        /// <summary>Keeps <paramref name="record"/> as the battle's latest result (the play count carries on).</summary>
        public void Set(ArgumentRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.id)) return;
            var copy = record.Clone();
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].id != copy.id) continue;
                copy.plays = Math.Max(copy.plays, _records[i].plays + 1);
                _records[i] = copy;
                return;
            }
            copy.plays = Math.Max(1, copy.plays);
            _records.Add(copy);
        }

        public void Clear() => _records.Clear();

        public string Serialize()
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            foreach (var r in _records)
            {
                sb.Append(Clean(r.id)).Append('|')
                  .Append(r.outcome).Append('|')
                  .Append(r.stance).Append('|')
                  .Append(r.anger.ToString(inv)).Append('|')
                  .Append(r.presses.ToString(inv)).Append('|')
                  .Append(Clean(r.truth)).Append('|')
                  .Append(Clean(r.personal)).Append('|')
                  .Append(r.keysBroken.ToString(inv)).Append('|')
                  .Append(r.difficulty).Append('|')
                  .Append(r.plays.ToString(inv)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Reads <see cref="Serialize"/>'s text; lines that don't parse are skipped (null or "" = empty).</summary>
        public static ArgumentLedger Parse(string text)
        {
            var ledger = new ArgumentLedger();
            if (string.IsNullOrEmpty(text)) return ledger;
            var inv = CultureInfo.InvariantCulture;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim('\r', ' ');
                if (line.Length == 0) continue;
                var f = line.Split('|');
                if (f.Length < Fields || f[0].Length == 0) continue;
                if (!Enum.TryParse(f[1], out ArgumentOutcome outcome)) continue;
                if (!Enum.TryParse(f[2], out ArgumentStance stance)) continue;
                if (!int.TryParse(f[3], NumberStyles.Integer, inv, out int anger)) continue;
                if (!int.TryParse(f[4], NumberStyles.Integer, inv, out int presses)) continue;
                if (!int.TryParse(f[7], NumberStyles.Integer, inv, out int keys)) continue;
                if (!Enum.TryParse(f[8], out ArgumentDifficulty difficulty)) difficulty = ArgumentDifficulty.Normal;
                if (!int.TryParse(f[9], NumberStyles.Integer, inv, out int plays)) plays = 1;
                if (ledger.Get(f[0]) != null) continue;
                ledger._records.Add(new ArgumentRecord
                {
                    id = f[0], outcome = outcome, stance = stance, anger = anger, presses = presses,
                    truth = f[5], personal = f[6], keysBroken = keys, difficulty = difficulty, plays = plays,
                });
            }
            return ledger;
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
    }
}
