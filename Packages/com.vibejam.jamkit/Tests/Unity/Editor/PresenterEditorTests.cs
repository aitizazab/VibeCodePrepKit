using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.Tests.Editor
{
    /// <summary>Presenter mode and footage capture, checked without playing: the Recorder package is
    /// pinned and JamKit > Record compiled against it, Recordings/ is ignored, and the presenter
    /// panel's styles exist. (The sandbox's chapters are checked in PresenterPlayTests.)</summary>
    public class PresenterEditorTests
    {
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [Test]
        public void TheRecorderPackageIsPinned()
        {
            string manifest = File.ReadAllText(Path.Combine(ProjectRoot, "Packages", "manifest.json"));
            Assert.That(manifest, Does.Contain("\"com.unity.recorder\": \"5.1.7\""));
        }

        [Test]
        public void TheRecordMenuCompiledAgainstTheRecorder()
        {
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                found = asm.GetType("JamKit.EditorTools.Recording.JamKitRecorder");
                if (found != null) break;
            }
            Assert.That(found, Is.Not.Null, "JamKit.Recording.Editor is compiled only when com.unity.recorder resolved (versionDefines JAMKIT_RECORDER)");
            Assert.That(found.GetMethod("RecordAttractFromCommandLine"), Is.Not.Null, "the batch entry point tools/record-attract.ps1 calls");
        }

        [Test]
        public void RecordingsAreGitIgnored()
        {
            string ignore = File.ReadAllText(Path.Combine(ProjectRoot, ".gitignore"));
            Assert.That(ignore, Does.Contain("/[Rr]ecordings/"));
        }

        [Test]
        public void ThePresenterPanelHasItsStyles()
        {
            string uss = File.ReadAllText(Path.Combine(Application.dataPath, "JamKit", "UI", "Shell.uss"));
            foreach (var cls in new[] { ".jk-presenter-panel", ".jk-presenter-button", ".jk-presenter-indicator" })
                Assert.That(uss, Does.Contain(cls), cls);
        }
    }
}
