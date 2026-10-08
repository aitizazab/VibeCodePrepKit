using JamKit.Flow;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// The base HUD: health bar, run timer, score line and a message line. A gameplay scene turns it
    /// on with <c>GameRoot.Hud.Show()</c> (it hides again on every scene change and outside a run).
    /// Extend it by adding elements to Hud.uxml and finding them with <c>GameRoot.Hud.Root.Q&lt;Label&gt;("name")</c>.
    /// </summary>
    public sealed class HudView
    {
        public readonly VisualElement Root;
        readonly VisualElement _healthBar, _healthFill;
        readonly Label _healthText, _timer, _score, _message;
        bool _requested;
        bool _timerShown = true;
        float _messageLeft;
        int _shownSeconds = -1;

        public bool IsShown => _requested;
        public string ScoreText => _score != null ? _score.text : "";
        public string MessageText => _message != null && _messageLeft > 0f ? _message.text : "";
        float _healthFraction = 1f;
        public float HealthFraction => _healthFraction;

        public HudView(VisualTreeAsset tree, VisualElement parent)
        {
            Root = tree != null ? (VisualElement)tree.Instantiate() : new VisualElement();
            Root.AddToClassList("jk-layer");
            Root.pickingMode = PickingMode.Ignore;
            parent.Add(Root);
            _healthBar = Root.Q<VisualElement>("hud-health");
            _healthFill = Root.Q<VisualElement>("hud-health-fill");
            _healthText = Root.Q<Label>("hud-health-text");
            _timer = Root.Q<Label>("hud-timer");
            _score = Root.Q<Label>("hud-score");
            _message = Root.Q<Label>("hud-message");
            ShowHealth(false);
            SetScoreText("");
            if (_message != null) _message.style.opacity = 0f;
            Root.style.display = DisplayStyle.None;
        }

        /// <summary>Turn the HUD on for this scene.</summary>
        public void Show() => _requested = true;

        public void Hide() => _requested = false;

        /// <summary>Called on every scene change: the new scene asks for the HUD again if it wants it.</summary>
        public void ResetForScene()
        {
            _requested = false;
            _timerShown = true;
            ShowHealth(false);
            SetScoreText("");
            _messageLeft = 0f;
            if (_message != null) _message.style.opacity = 0f;
        }

        /// <summary>Shows the bar at current/max, with "3 / 5" on it.</summary>
        public void SetHealth(float current, float max)
        {
            ShowHealth(true);
            float f = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            _healthFraction = f;
            if (_healthFill != null) _healthFill.style.width = Length.Percent(f * 100f);
            if (_healthText != null) _healthText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        }

        public void ShowHealth(bool shown)
        {
            if (_healthBar != null) _healthBar.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowTimer(bool shown) => _timerShown = shown;

        /// <summary>The line under the timer: "Score 120", "Coins 2 / 3". Empty hides it.</summary>
        public void SetScoreText(string text)
        {
            if (_score == null) return;
            _score.text = text ?? "";
            _score.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>A message at the top centre for a few seconds ("Press E to read the sign"). 0 seconds = until replaced.</summary>
        public void Message(string text, float seconds = 2.5f)
        {
            if (_message == null) return;
            _message.text = text ?? "";
            _messageLeft = string.IsNullOrEmpty(text) ? 0f : (seconds > 0f ? seconds : float.PositiveInfinity);
            _message.style.opacity = _messageLeft > 0f ? 1f : 0f;
        }

        public void ClearMessage() => Message("", 0.01f);

        public void Tick(float unscaledDt, bool inRun)
        {
            bool visible = _requested && inRun;
            Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            if (_timer != null)
            {
                _timer.style.display = _timerShown ? DisplayStyle.Flex : DisplayStyle.None;
                int secs = Mathf.FloorToInt(GameRoot.Run.time);
                if (secs != _shownSeconds)
                {
                    _shownSeconds = secs;
                    _timer.text = RunStats.FormatTime(secs);
                }
            }
            if (_messageLeft > 0f && !float.IsPositiveInfinity(_messageLeft))
            {
                _messageLeft -= unscaledDt;
                if (_messageLeft <= 0f && _message != null) _message.style.opacity = 0f;
            }
        }
    }
}
