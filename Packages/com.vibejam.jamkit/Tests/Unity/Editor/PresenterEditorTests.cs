using System;
using System.Collections.Generic;
using System.IO;
using JamKit.Presenting;
using JamKit.Sandbox;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.Tests.Editor
{
    /// <summary>Presenter mode and footage capture, checked without playing: the Recorder package is
    /// pinned and JamKit > Record compiled against it, the sandbox's chapters and attract script line
    /// up, and the presenter panel's styles exist.</summary>
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
        public void TheSandboxChaptersAndAttractScriptLineUp()
        {
            var r = new ChapterRegistry();
            SandboxGame.RegisterChapters(r, null, new object());
            var idx = new List<int>();
            r.InGroup(SandboxGame.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            Assert.That(names, Is.EqualTo(SandboxGame.Chapters));
            var problems = SandboxGame.Attract().Validate(names, SandboxGame.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
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
