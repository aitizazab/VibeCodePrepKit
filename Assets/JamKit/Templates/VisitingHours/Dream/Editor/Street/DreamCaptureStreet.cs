using System;
using System.Collections.Generic;
using System.IO;
using JamKit.DeepBlock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Stills of Night 1's street (docs/LARCH-HILL.md): the views core <see cref="LarchHill"/> lists, from the bottom (the
    /// whole climb, the shop), the middle (up to the tower, down the road), the top (the vista from the lookout, the
    /// house), the rooftops and the ravine. Same camera and output as the house's stills (out/dream-captures/&lt;time&gt;/).
    /// Needs the scene built and graphics: tools/street-capture.ps1.
    /// </summary>
    public static partial class DreamCapture
    {
        [MenuItem("JamKit/Dream Street/Capture Stills")]
        static void StreetMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(CaptureStreet());
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamCapture.CaptureStreetFromCommandLine</summary>
        public static void CaptureStreetFromCommandLine()
        {
            int code = 0;
            try { Debug.Log("JAMKIT_DREAM_CAPTURE_OK " + CaptureStreet()); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string CaptureStreet()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", "dream-captures", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-street");
            Directory.CreateDirectory(dir);
            var spots = new List<Spot>();
            foreach (var v in LarchHill.Build().views)
                spots.Add(new Spot(v.name, new Vector3(v.eye.X, v.eye.Y, v.eye.Z), new Vector3(v.look.X, v.look.Y, v.look.Z)));
            Scene(DreamStreetBuilder.ScenePath, spots, dir);
            return dir;
        }
    }
}
