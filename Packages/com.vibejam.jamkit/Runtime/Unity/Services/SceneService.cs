using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.Shell
{
    /// <summary>
    /// Scene changes behind a fade: fade to black, load asynchronously, reset hit stop and shake,
    /// fade back in. One load at a time (a second request while loading is refused). Scenes must be
    /// listed in Build Settings ("JamKit/Rebuild Shell Scenes" writes Boot, Title, Sandbox; add yours
    /// in File > Build Profiles, or to SceneBuilder.ExtraScenes).
    /// </summary>
    public sealed class SceneService : MonoBehaviour
    {
        public float fadeOutSeconds = 0.25f;
        public float fadeInSeconds = 0.3f;

        ShellUI _ui;
        JuiceService _juice;
        bool _loading;
        string _target = "";

        public bool IsLoading => _loading;
        /// <summary>The scene being loaded right now (empty when idle).</summary>
        public string Target => _target;

        /// <summary>Raised after a scene loaded through this service is active (before the fade-in).</summary>
        public event Action<string> Loaded;

        public void Init(ShellUI ui, JuiceService juice)
        {
            _ui = ui;
            _juice = juice;
        }

        public static bool Exists(string sceneName) => !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        public static string ActiveName => SceneManager.GetActiveScene().name;

        /// <summary>Starts a fade-and-load. Returns false if a load is already running or the scene isn't in Build Settings.</summary>
        public bool Load(string sceneName)
        {
            if (_loading) return false;
            if (!Exists(sceneName))
            {
                Debug.LogError($"JamKit: can't load scene \"{sceneName}\". Is it in Build Settings (and spelled the same in GameConfig)?");
                return false;
            }
            StartCoroutine(LoadRoutine(sceneName));
            return true;
        }

        /// <summary>Loads once the current load (if any) has finished.</summary>
        public void LoadQueued(string sceneName)
        {
            if (!_loading) { Load(sceneName); return; }
            StartCoroutine(WaitThenLoad(sceneName));
        }

        IEnumerator WaitThenLoad(string sceneName)
        {
            while (_loading) yield return null;
            Load(sceneName);
        }

        /// <summary>The next scene in Build Settings, or false if this is the last one.</summary>
        public bool LoadNext()
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next <= 0 || next >= SceneManager.sceneCountInBuildSettings) return false;
            string path = SceneUtility.GetScenePathByBuildIndex(next);
            return Load(System.IO.Path.GetFileNameWithoutExtension(path));
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            _loading = true;
            _target = sceneName;
            if (_ui != null) _ui.SetFadeBlocking(true);
            float from = _ui != null ? _ui.FadeAlpha : 0f;
            yield return Fade(from, 1f, fadeOutSeconds * (1f - from));

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;

            if (_juice != null) _juice.ResetAll();
            Time.timeScale = 1f;
            Loaded?.Invoke(sceneName);
            _target = "";
            _loading = false;
            yield return null;
            yield return Fade(1f, 0f, fadeInSeconds);
            if (_ui != null) _ui.SetFadeBlocking(false);
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            if (_ui == null) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _ui.SetFade(Mathf.Lerp(from, to, seconds > 0f ? t / seconds : 1f));
                yield return null;
            }
            _ui.SetFade(to);
        }
    }
}
