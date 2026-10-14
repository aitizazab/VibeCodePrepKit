using System;
using System.Collections;
using JamKit.Skyline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Everything a fight would otherwise make on first use, made at scene start instead (Task 17's frame-time fix), so the
    /// first bolt, spark, puddle, spot or wreck mid-fight creates no objects, materials or textures:
    /// <list type="bullet">
    /// <item><see cref="Pools"/>: the tracer, impact, spark, bolt and acid-puddle pools filled; the crack's, the beam's and
    /// the bots' shared materials made; every bot's generated pixel faces made.</item>
    /// <item><see cref="ShowOnce"/>: one tiny copy of each kind of runtime drawing (a lit primitive, a glowing one, a bot's
    /// face, a bark bubble's text) shown in front of the camera for two frames, so the graphics driver builds their
    /// pipeline states during the scene's first frames (before the recording starts and inside the frame-time probe's
    /// warm-up), not on the frame a bot first sparks or barks.</item>
    /// <item>Judge round 2 (perf): every sound's data decoded and, in the arena, every bot's body built ahead, spread over the
    /// scene's first frames (<see cref="SkylineWarmQueue"/>, <see cref="SkylineBodyPool"/>).</item>
    /// </list>
    /// </summary>
    public static class SkylinePrewarm
    {
        public static void Pools()
        {
            SkylineTracer.Prewarm();
            SkylineImpact.Prewarm();
            SkylineSpark.Prewarm();
            SkylineBolt.Prewarm();
            SkylinePuddle.Prewarm();
            SkylineSpotMarker.PrewarmLooks();
            SkylineBotLook.Lit(SkylineBotLook.White);
            SkylineBotLook.Lit(SkylineBotLook.Gold);
            SkylineBotLook.Lit(SkylineBotLook.Dark);
            SkylineBotLook.Lit(SkylineBotLook.Grey);
            SkylineBotLook.Lit(SkylineBotLook.Lens);
            SkylineBotLook.Lit(SkylineBotLook.Acid);
            SkylineBotLook.Lit(SkylineBotLook.Scorched);
            foreach (var def in SkylineEnemies.All())
                foreach (BotFace face in Enum.GetValues(typeof(BotFace)))
                    SkylineBotFace.Texture(def.id, face);
            // the bots' models (Task 20 review, Minor 10): loaded now, not on an encounter's first spawn; held so an unload
            // of unused assets can't drop them. ShowOnce draws each once.
            var models = new System.Collections.Generic.List<GameObject>();
            foreach (var def in SkylineEnemies.All())
                foreach (var name in new[] { def.id, def.id + "_mount" })
                {
                    var m = Resources.Load<GameObject>(BotModelFolder + name);
                    if (m != null) models.Add(m);
                }
            _models = models.ToArray();
            WarmSounds();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == ArenaScene) WarmArenaBodies();
        }

        const string BotModelFolder = "SkylineModels/bots/", SoundFolder = "SkylineAudio", ArenaScene = "SkylineArena";
        static GameObject[] _models = new GameObject[0];
        static AudioClip[] _clips = new AudioClip[0];

        /// <summary>
        /// Judge round 2 (perf): every Skyline sound is imported with "preload audio data" off and "decompress on load", so a
        /// sound's first play loaded and decoded the whole clip on the main thread, mid-fight: a bot's idle loop as each bot
        /// spawned, its alert, shot, hit and wreck sounds the first time each happened. The clips are loaded now and their
        /// data decoded on <see cref="SkylineWarmQueue"/>, a few a frame, during the scene's first second (held here, so an
        /// unload of unused assets can't drop them). <see cref="SkylineSounds.Get"/> later loads the same objects, already
        /// decoded.
        /// </summary>
        static void WarmSounds()
        {
            _clips = Resources.LoadAll<AudioClip>(SoundFolder);
            foreach (var clip in _clips)
            {
                var c = clip;
                if (c == null || c.loadState == AudioDataLoadState.Loaded) continue;
                SkylineWarmQueue.Add(() => { if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData(); });
            }
        }

        /// <summary>The arena's bot bodies, one for every spawn of its three fights in fight order, built ahead on
        /// <see cref="SkylineWarmQueue"/> (<see cref="SkylineBodyPool"/>): an encounter's start then only moves them into place.</summary>
        static void WarmArenaBodies()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var e in GalleriaArena.Encounters())
                if (e != null && e.spawns != null)
                    foreach (var s in e.spawns) ids.Add(s.enemy);
            SkylineBodyPool.Fill(ids);
        }

        /// <summary>Shows one tiny copy of each runtime drawing 0.6 m in front of <paramref name="cam"/> for two frames, then
        /// removes it.</summary>
        public static IEnumerator ShowOnce(Camera cam)
        {
            if (cam == null) yield break;
            var root = new GameObject("Prewarm (two frames)").transform;
            root.SetPositionAndRotation(cam.transform.position + cam.transform.forward * 0.6f, cam.transform.rotation);
            root.localScale = Vector3.one * 0.01f;
            SkylineBotLook.Visual(PrimitiveType.Cube, "Lit", root, SkylineBotLook.Lit(SkylineBotLook.Grey));
            SkylineBotLook.Visual(PrimitiveType.Sphere, "Lit sphere", root, SkylineBotLook.Lit(SkylineBotLook.Gold)).transform.localPosition = Vector3.right;
            SkylineBotLook.Visual(PrimitiveType.Cube, "Glow", root, SkylineBotLook.Glow(new Color(5f, 3.8f, 1.6f)), false).transform.localPosition = Vector3.left;
            var face = SkylineBotLook.Visual(PrimitiveType.Quad, "Face", root, SkylineBotLook.Lit(Color.white), false);
            face.transform.localPosition = Vector3.up;
            var faceMat = new Material(face.GetComponent<Renderer>().sharedMaterial) { mainTexture = SkylineBotFace.Texture(SkylineEnemies.DroneId, BotFace.Happy) };
            face.GetComponent<Renderer>().sharedMaterial = faceMat;
            var bubble = SkylineBarkBubble.Create(root, Vector3.down * 0.01f, "Halt!");
            bubble.transform.localScale = Vector3.one * 0.01f;
            // one tiny copy of each bot model (no colliders yet: those are added by FromModel at spawn), so its meshes are
            // uploaded and its imported materials' pipeline states built now
            for (int i = 0; i < _models.Length; i++)
                if (_models[i] != null)
                    Object.Instantiate(_models[i], root, false).transform.localPosition = new Vector3(2f + i, 0f, 0f);
            yield return null;
            yield return null;
            if (bubble != null) Object.Destroy(bubble.gameObject);
            Object.Destroy(root.gameObject);
            Object.Destroy(faceMat);
        }
    }
}
