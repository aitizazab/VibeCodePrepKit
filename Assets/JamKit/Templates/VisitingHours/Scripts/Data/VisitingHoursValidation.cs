using System.Collections.Generic;

namespace JamKit.VisitingHours
{
    /// <summary>Plain-words checks of the slice's data (menu JamKit > Visiting Hours > Validate
    /// Content, the EditMode tests): the journal's book, the argument's keys, offers and rewards,
    /// the examinables' entries, the art the data names, the materials and the ink.</summary>
    public static class VisitingHoursValidation
    {
        public static List<string> Problems(VisitingHoursConfig c)
        {
            var problems = new List<string>();
            if (c == null)
            {
                problems.Add("no VisitingHoursConfig (run JamKit > Rebuild Shell Scenes)");
                return problems;
            }
            var book = c.MakeBook();
            book.Validate(problems);
            if (c.story == null) problems.Add("the config has no ink story");
            if (c.paintedMaterial == null || c.worldMaterial == null || c.skyMaterial == null) problems.Add("a material is missing (run JamKit > Rebuild Shell Scenes)");

            var b = c.walt;
            if (b == null || b.statements == null || b.statements.Count == 0) problems.Add("Walt's argument has no statements");
            else
            {
                bool anyKey = false;
                for (int i = 0; i < b.statements.Count; i++)
                {
                    var s = b.statements[i];
                    string name = "Walt's statement " + (i + 1);
                    if (s == null) { problems.Add(name + " is empty"); continue; }
                    if (s.volleys == null || s.volleys.Count == 0) problems.Add(name + " has no volley");
                    if (!(s.duration > 0f)) problems.Add(name + " has no duration");
                    if (!string.IsNullOrEmpty(s.key))
                    {
                        if (!s.optional) anyKey = true;
                        if (book.Find(s.key) == null) problems.Add(name + "'s key \"" + s.key + "\" isn't a journal entry");
                        if (s.offers == null || !s.offers.Contains(s.key)) problems.Add(name + "'s key \"" + s.key + "\" never drifts into the box (add it to Offers)");
                    }
                    if (s.offers != null)
                        foreach (var o in s.offers)
                            if (book.Find(o) == null) problems.Add(name + " offers an unknown entry \"" + o + "\"");
                }
                if (!anyKey) problems.Add("Walt's argument has no key statement: Convinced can't happen");
                if (book.Find(b.convincedEntry) == null) problems.Add("Convinced gives an unknown entry \"" + b.convincedEntry + "\"");
                if (book.Find(b.heardOutEntry) == null) problems.Add("Heard out gives an unknown entry \"" + b.heardOutEntry + "\"");
                if (b.composure < 1) problems.Add("Walt's argument gives Nadia no composure");
            }

            foreach (var who in new[] { b != null ? b.speaker : "Walt", "Mags" })
            {
                var p = c.FindPortrait(who);
                if (p == null) problems.Add("no portrait for " + who + " in the config's Portraits (run JamKit > Rebuild Shell Scenes)");
                else if (p.FindExpression("") == null) problems.Add(who + "'s portrait has no default expression picture");
            }

            var ids = new HashSet<string>();
            foreach (var e in c.examinables)
            {
                if (e == null) continue;
                if (!ids.Add(e.id)) problems.Add("two examinables have the id \"" + e.id + "\"");
                if (string.IsNullOrEmpty(e.knot)) problems.Add("examinable \"" + e.id + "\" has no knot");
                if (!string.IsNullOrEmpty(e.records) && book.Find(e.records) == null) problems.Add("examinable \"" + e.id + "\" records an unknown entry \"" + e.records + "\"");
            }
            foreach (var e in book.Entries)
                if (!string.IsNullOrEmpty(e.icon) && c.Art(e.icon) == null) problems.Add("journal entry \"" + e.id + "\" names a picture that isn't in the config: " + e.icon);
            return problems;
        }
    }
}
