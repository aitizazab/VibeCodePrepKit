using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// A display bot for one still (the comic-ink look test's <c>drone-idle</c> shot, and each bot's gallery still): a real bot,
    /// asleep and set as the practice bot (it never attacks and never targets), made at <see cref="position"/> facing
    /// <see cref="yaw"/> only for the instant that still renders, then removed again. It sits on the still camera's own object.
    /// <see cref="SkylineStillCamera"/> calls <see cref="Show"/> from <c>BeginShot</c> and <see cref="Hide"/> from <c>EndShot</c>,
    /// which stage B's capture wraps round each still's render in one editor tick, so no frame (no director, no fight, no lint)
    /// ever sees the bot: it is removed with DestroyImmediate, which also takes it out of <c>SkylineBot.Live</c>.
    /// <para>While the shot renders every other bot, wreck and bolt in the scene is hidden (their renderers switched off and
    /// back on), so the display bot is the only one in frame: the demo's wrecks lie about the hall by the end of a recording.</para>
    /// <para>Display only: <see cref="bodyScale"/> and <see cref="rotorScale"/> enlarge the body and the drone's rotors for the
    /// shot (a preview of the larger-rotor redesign); the gameplay bots are never changed.</para>
    /// </summary>
    public sealed class SkylineDisplayBot : MonoBehaviour
    {
        public string enemyId = SkylineEnemies.DroneId;
        public Vector3 position;
        public float yaw;
        public ulong seed = 31UL;
        [Tooltip("Display only: the body (the whole rig) is scaled by this.")]
        public float bodyScale = 1f;
        [Tooltip("Display only: each rotor (the children of the drone's Rotors) is scaled by this about its own centre.")]
        public float rotorScale = 1f;
        [Tooltip("The bot is shown as it looks at this damage stage (0 undamaged, 1 at 70% health, 2 at 30%), with no animation.")]
        public int damageStage;

        SkylineBot _bot;
        readonly List<Renderer> _hidden = new List<Renderer>();

        /// <summary>The bot in the scene right now (null outside a shot).</summary>
        public SkylineBot Bot => _bot;

        public void Show()
        {
            Hide();
            HideOthers();
            // a bot needs a target, though this one never uses it: the player, else the gallery's dummy
            ISkylineTarget target = FindFirstObjectByType<SkylinePlayer>();
            if (target == null) target = FindFirstObjectByType<SkylineDummy>(FindObjectsInactive.Include);   // the gallery hides its dummies until their segment
            if (target == null)
            {
                Debug.LogWarning("SKYLINE_STILL display bot: no player or dummy in the scene; the shot has no bot");
                return;
            }
            var player = target as SkylinePlayer;
            _bot = SkylineBot.Spawn(enemyId, position, yaw, seed, target, player != null ? player.Writ : null, null);
            if (_bot == null) { Debug.LogWarning("SKYLINE_STILL display bot " + enemyId + " could not be spawned"); return; }
            _bot.practice = true;
            _bot.gameObject.name = "Display " + _bot.gameObject.name;
            if (!Mathf.Approximately(bodyScale, 1f) && _bot.Rig != null) _bot.Rig.localScale *= bodyScale;
            if (!Mathf.Approximately(rotorScale, 1f) && _bot.Parts != null && _bot.Parts.spinner != null && _bot.Parts.spinner.name == "Rotors")
                foreach (Transform rotor in _bot.Parts.spinner) rotor.localScale *= rotorScale;
            if (damageStage > 0) _bot.ForceDamageStage(damageStage);
            // proof for the pass's log: what was made, where, and how many of its renderers are on
            int visible = 0;
            foreach (var r in _bot.GetComponentsInChildren<Renderer>(false)) if (r.enabled) visible++;
            var still = GetComponent<SkylineStillCamera>();
            Debug.Log("SKYLINE_STILL display bot shot=" + (still != null ? still.shotName : name) + " type=" + enemyId + " at=" +
                      _bot.transform.position.ToString("0.00") + " visibleRenderers=" + visible + " hiddenOthers=" + _hidden.Count);
        }

        public void Hide()
        {
            if (_bot != null) DestroyImmediate(_bot.gameObject);
            _bot = null;
            foreach (var r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
        }

        /// <summary>Switches off the renderers of every live or wrecked bot and every bolt, remembering the ones that were on.</summary>
        void HideOthers()
        {
            foreach (var b in FindObjectsByType<SkylineBot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) HideAll(b.gameObject);
            foreach (var w in FindObjectsByType<SkylineWreck>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) HideAll(w.gameObject);
            foreach (var b in FindObjectsByType<SkylineBolt>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) HideAll(b.gameObject);
        }

        void HideAll(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                if (r.enabled) { r.enabled = false; _hidden.Add(r); }
        }
    }
}
