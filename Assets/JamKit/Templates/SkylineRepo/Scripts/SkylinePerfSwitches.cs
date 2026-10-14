using JamKit.Skyline;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Measurement-only player switches for the perf A/B runs (pass.ps1 <c>-Only D -Out &lt;folder&gt; -PerfArgs '&lt;switch&gt;'</c>), each one
    /// taking away something added between pass perf10 and perf11, to price it. Like <c>-skyline-no-ink</c> they are never set in play,
    /// and the perf probe records each in perf.json. Read from the command line on first use (so a bot built before the probe wakes
    /// still sees them); a test sets them with <see cref="SetForTest"/>.
    /// </summary>
    public static class SkylinePerfSwitches
    {
        public const string NoLineWeightSwitch = "-skyline-no-lineweight";
        public const string NoDamageFxSwitch = "-skyline-no-damage-fx";
        public const string OldBotSizesSwitch = "-skyline-old-bot-sizes";

        static bool _read, _noLineWeight, _noDamageFx, _oldBotSizes;

        /// <summary>The ink style's line-weight variation is forced to 0 (the composite pass is otherwise the same).</summary>
        public static bool NoLineWeight { get { ReadOnce(); return _noLineWeight; } }

        /// <summary>The bots' damage states never start: no sparks, smoke, shards, scorch, bent ducts, limp or face glitch.</summary>
        public static bool NoDamageFx { get { ReadOnce(); return _noDamageFx; } }

        /// <summary>The bots are built at their pre-perf11 size: body scale 1 and weak-part scale 1 (the sim radii are not changed).</summary>
        public static bool OldBotSizes { get { ReadOnce(); return _oldBotSizes; } }

        /// <summary>Reads the three switches through <paramref name="has"/> (the command line in play, a fake in the tests).</summary>
        public static void Apply(System.Func<string, bool> has)
        {
            _noLineWeight = has(NoLineWeightSwitch);
            _noDamageFx = has(NoDamageFxSwitch);
            _oldBotSizes = has(OldBotSizesSwitch);
            _read = true;
        }

        static void ReadOnce()
        {
            if (!_read) Apply(SkylineRunArgs.Has);
        }

        public static void SetForTest(bool noLineWeight, bool noDamageFx, bool oldBotSizes)
        {
            _noLineWeight = noLineWeight;
            _noDamageFx = noDamageFx;
            _oldBotSizes = oldBotSizes;
            _read = true;
        }

        public static void ResetForTest() { _read = false; _noLineWeight = _noDamageFx = _oldBotSizes = false; }

        /// <summary>The scale the Unity body builds a bot with: the def's, or 1 under <see cref="OldBotSizes"/>.</summary>
        public static float BodyScale(EnemyDef d) => OldBotSizes ? 1f : d.bodyScale;

        /// <summary>The weak part's extra scale: the def's, or 1 under <see cref="OldBotSizes"/>.</summary>
        public static float WeakPointScale(EnemyDef d) => OldBotSizes ? 1f : d.weakPointScale;

        /// <summary>The three perf.json fields (no leading comma).</summary>
        public static string Json() =>
            "\"noLineWeight\":" + (NoLineWeight ? "true" : "false") + ",\"noDamageFx\":" + (NoDamageFx ? "true" : "false") +
            ",\"oldBotSizes\":" + (OldBotSizes ? "true" : "false");
    }
}
