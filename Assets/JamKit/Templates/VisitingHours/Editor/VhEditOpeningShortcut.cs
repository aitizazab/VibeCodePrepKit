using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// After a Windows build with Visiting Hours in it: writes "Edit the opening.cmd" next to the exe, which starts the
    /// game straight in the opening editor (the -edit-opening flag; docs/OPENING-EDITOR.md).
    /// </summary>
    public sealed class VhEditOpeningShortcut : IPostprocessBuildWithReport
    {
        public const string FileName = "Edit the opening.cmd";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            var s = report.summary;
            if (s.platform != BuildTarget.StandaloneWindows64 && s.platform != BuildTarget.StandaloneWindows) return;
            bool hasSlice = false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && scene.path.Contains("VisitingHours")) hasSlice = true;
            if (!hasSlice) return;
            string exe = s.outputPath;
            string dir = Path.GetDirectoryName(exe);
            if (string.IsNullOrEmpty(dir)) return;
            string text = "@echo off\r\nrem Starts Visiting Hours straight in the opening editor (docs/OPENING-EDITOR.md).\r\n" +
                          "start \"\" \"%~dp0" + Path.GetFileName(exe) + "\" " + VhOpeningEditor.LaunchFlag + "\r\n";
            File.WriteAllText(Path.Combine(dir, FileName), text);
            Debug.Log("JAMKIT_VH wrote " + Path.Combine(dir, FileName));
        }
    }
}
