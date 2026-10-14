using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The bots' barks (2.5), one per BotBrain bark id: "spotted", "hurt", "lost", "wrecked". DRAFTS for the creator to
    /// rewrite (one file, as the plan asks).</summary>
    public static class SkylineBarks
    {
        public static string Line(string botId, string bark)
        {
            switch (botId)
            {
                case SkylineEnemies.DroneId:
                    return Pick(bark, "Halt! Mall security!", "That's a citation!", "Suspect fled. Noted.", "Officer... down...");
                case SkylineEnemies.FloorCareId:
                    return Pick(bark, "Ooh! A stain!", "Rude! I'm cleaning!", "Spotless again!", "Out of... order...");
                case SkylineEnemies.SpotlightId:
                    return Pick(bark, "Lights on you. Smile.", "My lens! My LENS!", "Searching the dark.", "Lights... out...");
                case SkylineEnemies.GondolaId:
                    return Pick(bark, "Oi! Off my glass!", "Watch the bucket!", "Back to the panes.", "Hanging... up...");
                default:
                    return null;
            }
        }

        static string Pick(string bark, string spotted, string hurt, string lost, string wrecked)
        {
            switch (bark)
            {
                case "spotted": return spotted;
                case "hurt": return hurt;
                case "lost": return lost;
                case "wrecked": return wrecked;
                default: return null;
            }
        }

        /// <summary>The bark ids (BotBrain's).</summary>
        public static readonly string[] Ids = { "spotted", "hurt", "lost", "wrecked" };

        /// <summary>Synthesizes (and caches) the babble of one bark of the bot <paramref name="botId"/> seeded
        /// <paramref name="seed"/> (the bot's own seed, as <see cref="SkylineBot"/> barks with), so the bark itself costs
        /// nothing mid-fight. Returns false when there is no such line.</summary>
        public static bool Prepare(string botId, string bark, int seed)
        {
            string line = Line(botId, bark);
            if (string.IsNullOrEmpty(line)) return false;
            SkylineBabble.Make(botId, line, seed);
            return true;
        }

        /// <summary>Speaks <paramref name="text"/> in <paramref name="voice"/>'s babble from <paramref name="source"/> (the mix
        /// decides whether it is heard: one bark at a time, never over a teaching line) and, when it is heard and the bot is
        /// within <see cref="SkylineHudSubtitles.BarkRange"/> of the player's camera, subtitles it as <paramref name="botName"/>
        /// (the "Dialogue + bot barks" setting decides whether that shows). There is no bubble above the bot any more: enemy
        /// clarity is the hued outlines now.</summary>
        public static void Say(Transform anchor, string text, string voice, int seed, AudioSource source, string botName)
        {
            if (string.IsNullOrEmpty(text) || anchor == null || source == null) return;
            var clip = SkylineBabble.Make(voice, text, seed);
            if (clip == null) return;
            if (!SkylineSounds.PlayBark(source, clip, SkylineMix.UserGain(SkylineSoundCategory.Voices))) return;
            var hud = SkylineHud.Instance;
            if (hud == null) return;
            var cam = hud.player != null && hud.player.Camera != null ? hud.player.Camera : Camera.main;
            if (cam != null && (cam.transform.position - anchor.position).sqrMagnitude > SkylineHudSubtitles.BarkRange * SkylineHudSubtitles.BarkRange) return;
            hud.SayBark(botName, text);
        }
    }
}
