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
            yield return null;
            yield return null;
            if (bubble != null) Object.Destroy(bubble.gameObject);
            Object.Destroy(root.gameObject);
            Object.Destroy(faceMat);
        }
    }
}
