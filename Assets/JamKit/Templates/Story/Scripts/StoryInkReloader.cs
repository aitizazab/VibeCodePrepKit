#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using JamKit.Shell;
using UnityEditor;
using UnityEngine;

namespace JamKit.Narrative
{
    /// <summary>
    /// Editor only: watches the story's .ink files while you play. Save an edit (in Inky or any editor)
    /// and within a second the story recompiles and the current knot starts again with the new text,
    /// keeping variables and the stage. Compile errors show as a message and in the Console; the old
    /// story keeps running.
    /// </summary>
    public sealed class StoryInkReloader : MonoBehaviour
    {
        StoryGame _game;
        string _masterPath = "";
        readonly Dictionary<string, DateTime> _stamps = new Dictionary<string, DateTime>();
        float _clock;

        int _reloads;

        /// <summary>How many times the story was swapped (tests).</summary>
        public int Reloads => _reloads;

        public void Init(StoryGame game)
        {
            _game = game;
            if (game.config == null || game.config.story == null) { enabled = false; return; }
            _masterPath = AssetDatabase.GetAssetPath(game.config.story);
            if (string.IsNullOrEmpty(_masterPath) || !File.Exists(_masterPath)) { enabled = false; return; }
            Snapshot();
        }

        IEnumerable<string> Files()
        {
            yield return _masterPath;
            string dir = Path.GetDirectoryName(_masterPath);
            foreach (var f in Directory.GetFiles(dir, "*.ink", SearchOption.AllDirectories)) yield return f.Replace('\\', '/');
        }

        void Snapshot()
        {
            _stamps.Clear();
            foreach (var f in Files()) _stamps[f] = File.GetLastWriteTimeUtc(f);
        }

        bool Changed()
        {
            foreach (var f in Files())
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (!_stamps.TryGetValue(f, out var old) || old != t) return true;
            }
            return false;
        }

        void Update()
        {
            _clock += Time.unscaledDeltaTime;
            if (_clock < 0.75f) return;
            _clock = 0f;
            if (!Changed()) return;
            Snapshot();
            Reload();
        }

        /// <summary>Recompiles the master ink file and swaps it into the running story.</summary>
        public bool Reload()
        {
            var errors = new List<string>();
            string json = Compile(_masterPath, errors);
            if (json == null)
            {
                string first = errors.Count > 0 ? errors[0] : "unknown error";
                GameRoot.Hud.Message("ink has errors, still playing the old version: " + first, 6f);
                foreach (var e in errors) Debug.LogWarning("ink (hot reload): " + e);
                return false;
            }
            var d = GameRoot.Dialogue;
            string knot = d.CurrentKnot;
            if (!d.HotSwap(json, knot)) return false;
            _reloads++;
            GameRoot.Hud.Message("ink reloaded: " + (string.IsNullOrEmpty(knot) ? "story" : knot) + " restarted", 2.5f);
            return true;
        }

        /// <summary>Compiles an ink file (and its INCLUDEs) to JSON, or null with the errors.</summary>
        public static string Compile(string path, List<string> errors)
        {
            string text = File.ReadAllText(path);
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            var compiler = new Ink.Compiler(text, new Ink.Compiler.Options
            {
                countAllVisits = true,
                sourceFilename = Path.GetFileName(path),
                fileHandler = new FolderFileHandler(dir),
                errorHandler = (message, type) => { if (type == Ink.ErrorType.Error) errors.Add(message); },
            });
            try
            {
                var story = compiler.Compile();
                if (story == null || errors.Count > 0) return null;
                return story.ToJson();
            }
            catch (Exception e)
            {
                errors.Add(e.Message);
                return null;
            }
        }

        sealed class FolderFileHandler : Ink.IFileHandler
        {
            readonly string _dir;
            public FolderFileHandler(string dir) { _dir = dir; }
            public string ResolveInkFilename(string includeName) => Path.Combine(_dir, includeName);
            public string LoadInkFileContents(string fullFilename) => File.ReadAllText(fullFilename);
        }
    }
}
#endif
