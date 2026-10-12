using System;
using System.Collections.Generic;
using JamKit.DeepBlock;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Stills of Larch Hill for the judge (docs/redesign/world.md "Captures"): the 25 views core <see cref="LarchHill"/>
    /// lists (a vista from each band, the house and its block, Walt's yard and shop, the hospital on the skyline, every
    /// challenge at player height, the middle band and the top), 1920 x 1080, through the Look agent's capture rig
    /// (<see cref="VhLookCapture"/>: the scene's own golden hour, post and haze), to out/hill-captures/&lt;time&gt;/.
    /// "-hillDreamChanges all" captures with the bedside's changes on (the plank, the lit hospital, the ledger).
    /// Needs the scene built and graphics: tools/street-capture.ps1. The recorded run is a PlayMode test
    /// (DreamStreetPlayTests.RecordTheNight1CriticalPath).
    /// </summary>
    public static class DreamCaptureStreet
    {
        [MenuItem("JamKit/Dream Street/Capture Stills")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(CaptureStreet());
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamCaptureStreet.CaptureStreetFromCommandLine [-hillDreamChanges all]</summary>
        public static void CaptureStreetFromCommandLine()
        {
            int code = 0;
            try
            {
                bool changes = Array.IndexOf(Environment.GetCommandLineArgs(), "-hillDreamChanges") >= 0;
                Debug.Log("JAMKIT_DREAM_CAPTURE_OK " + CaptureStreet(changes));
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string CaptureStreet(bool dreamChanges = false)
        {
            var h = LarchHill.Build();
            var shots = new List<VhLookCapture.Shot>();
            foreach (var v in h.views)
                shots.Add(new VhLookCapture.Shot(v.name, new Vector3(v.eye.X, v.eye.Y, v.eye.Z), new Vector3(v.look.X, v.look.Y, v.look.Z), 70f));
            return VhLookCapture.Capture(DreamStreetBuilder.ScenePath, shots, dreamChanges ? "hill-captures-changed" : "hill-captures", scene =>
            {
                var eyes = new List<Vector3>();
                foreach (var s in shots) eyes.Add(s.eye);
                // The cut-outs turn to the camera at runtime: here, each to its nearest shot.
                foreach (var f in UnityEngine.Object.FindObjectsByType<DreamCutoutView>(FindObjectsSortMode.None))
                {
                    var t = f.transform;
                    Vector3 best = eyes[0];
                    foreach (var e in eyes) if ((e - t.position).sqrMagnitude < (best - t.position).sqrMagnitude) best = e;
                    var d = best - t.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f) t.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                }
                var game = UnityEngine.Object.FindFirstObjectByType<DreamStreetGame>(FindObjectsInactive.Include);
                if (game != null && dreamChanges)
                {
                    if (game.creekPlank != null) game.creekPlank.SetActive(true);
                    if (game.hospitalLit != null) game.hospitalLit.SetActive(true);
                }
                if (game != null && game.ledger != null) game.ledger.SetActive(true);
            });
        }
    }
}
