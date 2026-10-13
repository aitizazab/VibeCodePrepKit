using System;

namespace JamKit.SkylineRepo
{
    /// <summary>The pass's command-line switches, read in the editor (stage B: -skylineOut) and the player (stage D:
    /// -skyline-perf). Under either, the scenes' directors run their scripted demos, so the recording and the frame-time
    /// probe see the same thing; -skyline-demo runs them by hand. A plain launch is the game.</summary>
    public static class SkylineRunArgs
    {
        public static string Value(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        public static bool Has(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        /// <summary>Where the pass wants files written (null outside a pass).</summary>
        public static string OutDir => Value("-skylineOut") ?? Value("-skyline-perf");

        public static bool Demo => Has("-skylineOut") || Has("-skyline-perf") || Has("-skyline-demo");
    }
}
