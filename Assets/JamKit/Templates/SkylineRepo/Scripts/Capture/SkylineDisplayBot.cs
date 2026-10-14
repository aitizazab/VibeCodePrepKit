using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// A display bot for one still (the comic-ink look test's <c>drone-idle</c> shot): a real bot, asleep and set as the
    /// practice bot (it never attacks and never targets), made at <see cref="position"/> facing <see cref="yaw"/> only for the
    /// instant that still renders, then removed again. It sits on the still camera's own object. <see cref="SkylineStillCamera"/>
    /// calls <see cref="Show"/> from <c>BeginShot</c> and <see cref="Hide"/> from <c>EndShot</c>, which stage B's capture wraps
    /// round each still's render in one editor tick, so no frame (no director, no fight, no lint) ever sees the bot: it is
    /// removed with DestroyImmediate, which also takes it out of <c>SkylineBot.Live</c>.
    /// </summary>
    public sealed class SkylineDisplayBot : MonoBehaviour
    {
        public string enemyId = SkylineEnemies.DroneId;
        public Vector3 position;
        public float yaw;
        public ulong seed = 31UL;

        SkylineBot _bot;

        /// <summary>The bot in the scene right now (null outside a shot).</summary>
        public SkylineBot Bot => _bot;

        public void Show()
        {
            Hide();
            var player = FindFirstObjectByType<SkylinePlayer>();   // a bot needs a target, though this one never uses it
            if (player == null)
            {
                Debug.LogWarning("SKYLINE_STILL display bot: no player in the scene; the shot has no bot");
                return;
            }
            _bot = SkylineBot.Spawn(enemyId, position, yaw, seed, player, player.Writ, null);
            if (_bot == null) return;
            _bot.practice = true;
            _bot.gameObject.name = "Display " + _bot.gameObject.name;
        }

        public void Hide()
        {
            if (_bot != null) DestroyImmediate(_bot.gameObject);
            _bot = null;
        }
    }
}
