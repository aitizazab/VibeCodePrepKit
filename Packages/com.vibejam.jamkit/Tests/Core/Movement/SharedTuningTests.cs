using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The shared movement tuning (`Runtime/Unity/Movement/Resources/JamKitMovementTuning.asset`, every game's
    /// since 2026-10-11) holds exactly MoveDef's defaults, so every course test and autopilot pin the dotnet sim runs
    /// (the Gauntlet, the dream tool, the dream escape, the deep block's four routes) is what the scenes play. Read from
    /// the asset's text.</summary>
    public class SharedTuningTests
    {
        const string Relative = "Packages/com.vibejam.jamkit/Runtime/Unity/Movement/Resources/JamKitMovementTuning.asset";

        static string FindAsset()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                for (int i = 0; dir != null && i < 10; i++, dir = dir.Parent)
                {
                    string p = Path.Combine(dir.FullName, Relative);
                    if (File.Exists(p)) return p;
                }
            }
            return null;
        }

        /// <summary>The <c>def:</c> block's simple fields (name, value).</summary>
        static Dictionary<string, string> DefFields(string text)
        {
            var d = new Dictionary<string, string>();
            bool inDef = false;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line == "  def:") { inDef = true; continue; }
                if (!inDef) continue;
                if (!line.StartsWith("    ")) break;
                if (line.StartsWith("     ")) continue;
                int c = line.IndexOf(':');
                if (c < 0) continue;
                d[line.Substring(4, c - 4).Trim()] = line.Substring(c + 1).Trim();
            }
            return d;
        }

        [Test]
        public void TheSharedTuningIsMoveDefsDefaults()
        {
            string path = FindAsset();
            if (path == null) Assert.Ignore("the shared tuning asset isn't reachable from here");
            var fields = DefFields(File.ReadAllText(path));
            Assert.That(fields.Count, Is.GreaterThan(40), "read the def block");
            var defaults = new MoveDef();
            var differ = new List<string>();
            foreach (var f in typeof(MoveDef).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!fields.TryGetValue(f.Name, out var v)) continue; // not in the asset: Unity uses the default
                object d = f.GetValue(defaults);
                if (d is float fd)
                {
                    float fv = float.Parse(v, CultureInfo.InvariantCulture);
                    if (Math.Abs(fv - fd) > 1e-5f) differ.Add($"{f.Name}: asset {fv}, default {fd}");
                }
                else if (d is bool bd)
                {
                    bool bv = v == "1" || v == "true";
                    if (bv != bd) differ.Add($"{f.Name}: asset {bv}, default {bd}");
                }
                else if (d is int id)
                {
                    if (int.Parse(v, CultureInfo.InvariantCulture) != id) differ.Add($"{f.Name}: asset {v}, default {id}");
                }
            }
            Assert.That(differ, Is.Empty, "the shared asset matches the code (or the sims test something else): " + string.Join("; ", differ));
            Assert.That(File.ReadAllText(path), Does.Contain("defaultsVersion: 2"), "it has taken the 2026-10-11 upgrade");
        }
    }
}
