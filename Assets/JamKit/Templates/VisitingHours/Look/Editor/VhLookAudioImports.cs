using System.IO;
using UnityEditor;
using UnityEngine;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Import settings for Visiting Hours' voice-over and music (the Perf agent's list, item 18: "Decompress On Load VO and
    /// music": 20-150 ms load spikes). Clips of 8 s or more under Visiting Hours (outside the Look's own ambience, which
    /// <see cref="VhLookAmbience.ConfigureImporters"/> sets) become Streaming, Vorbis, loaded in the background, not preloaded;
    /// shorter ones are left alone (decompressed one-shots cost nothing to fire). These are other owners' clips, so this is
    /// run on request, not by the LookLab build:
    /// <code>-executeMethod JamKit.Look.EditorTools.VhLookAudioImports.ConfigureFromCommandLine</code>
    /// </summary>
    public static class VhLookAudioImports
    {
        public const string Root = "Assets/JamKit/Templates/VisitingHours";
        public const float StreamSeconds = 8f;

        [MenuItem("JamKit/Look/Stream Long Voice and Music Clips")]
        public static int Configure()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(VhLookAmbience.Dir, System.StringComparison.Ordinal)) continue;
                if (path.Replace('\\', '/').Contains("/Takes/")) continue;   // alternate takes: not shipped
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (imp == null || clip == null || clip.length < StreamSeconds) continue;
                var s = imp.defaultSampleSettings;
                if (s.loadType == AudioClipLoadType.Streaming && s.compressionFormat == AudioCompressionFormat.Vorbis && imp.loadInBackground) continue;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = false;
                imp.defaultSampleSettings = s;
                imp.loadInBackground = true;
                imp.SaveAndReimport();
                changed++;
                Debug.Log("JAMKIT_LOOK streaming: " + path + " (" + clip.length.ToString("0.0") + " s)");
            }
            Debug.Log("JAMKIT_LOOK voice/music imports changed: " + changed);
            return changed;
        }

        public static void ConfigureFromCommandLine()
        {
            int code = 0;
            try { Configure(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
