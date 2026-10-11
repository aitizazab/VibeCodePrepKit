using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument
{
    /// <summary>
    /// The prototype's scene (ArgumentPrototype, built by ArgumentPrototypeBuilder): a kitchen table for four as the
    /// backdrop, a small menu to start Dream-Lena's battle in each setting (Normal, Story, the Long Night's "scripted
    /// but beatable", without Walt's truth, or watched by its autopilot), and what the save remembers. Presenter
    /// chapters (Ctrl+Shift+P, or hold L3+R3) start each setting directly; Visiting Hours' and the Dream Escape's
    /// chapter lists also carry "Argument prototype (Dream-Lena)", which jumps here.
    /// </summary>
    public sealed class ArgumentPrototype : MonoBehaviour
    {
        public const string Scene = "ArgumentPrototype";
        public const string Normal = "Dream-Lena: Normal";
        public const string Story = "Dream-Lena: Story (nothing hurts)";
        public const string LongNight = "Dream-Lena: Long Night (scripted but beatable)";
        public const string NoWalt = "Dream-Lena: without Walt's truth";
        public const string Watch = "Dream-Lena: watch it play";
        public static readonly string[] Chapters = { Normal, Story, LongNight, NoWalt, Watch };

        /// <summary>The chapter in Visiting Hours' and the Dream Escape's lists that jumps to the prototype.</summary>
        public const string FromElsewhere = "Argument prototype (Dream-Lena)";
        static readonly string[] Elsewhere = { "VisitingHours", "DreamEscape" };

        static ArgumentPrototype _current;
        public static ArgumentPrototype Current => _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            foreach (var group in Elsewhere)
            {
                PresenterMode.Declare(group, new[] { FromElsewhere });
                PresenterMode.Register(group, FromElsewhere, () => PresenterMode.Jump(Scene, Normal), PresenterMode.AnyTime);
            }
        }

        VisualElement _menu;
        Label _saved;
        readonly List<Button> _buttons = new List<Button>(8);
        string _lastChapter = Normal;
        float _shownAt = -10f;

        void Start()
        {
            if (!GameRoot.Exists) { enabled = false; return; }
            _current = this;
            PresenterMode.Register(Scene, Normal, () => Play(Normal), this);
            PresenterMode.Register(Scene, Story, () => Play(Story), this);
            PresenterMode.Register(Scene, LongNight, () => Play(LongNight), this);
            PresenterMode.Register(Scene, NoWalt, () => Play(NoWalt), this);
            PresenterMode.Register(Scene, Watch, () => Play(Watch), this);
            BuildMenu();
            ShowMenu(true);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            PresenterMode.Unregister(this);
            if (ArgumentHost.Current != null) ArgumentHost.Current.Close(false);
            _menu?.RemoveFromHierarchy();
        }

        /// <summary>The setup for one of the chapters (tests use it too).</summary>
        public static ArgumentSetup SetupFor(string chapter)
        {
            var setup = new ArgumentSetup { seed = (ulong)Environment.TickCount };
            switch (chapter)
            {
                case Story:
                case Watch:
                    setup.difficulty = ArgumentDifficulty.Story;
                    setup.allEvidence = true;
                    break;
                case LongNight:
                    setup.difficulty = ArgumentDifficulty.ScriptedButBeatable;
                    setup.allEvidence = true;
                    break;
                case NoWalt:
                    setup.evidence.AddRange(DreamLenaDemo.EvidenceWithoutWalt);
                    break;
                default:
                    setup.allEvidence = true;
                    break;
            }
            return setup;
        }

        /// <summary>Starts Dream-Lena's battle in the chapter's setting.</summary>
        public ArgumentHost Play(string chapter)
        {
            _lastChapter = chapter;
            ShowMenu(false);
            var host = ArgumentHost.Begin(DreamLenaDemo.Build(), SetupFor(chapter), _ => ShowMenu(true));
            if (host != null) host.Autopilot = chapter == Watch;
            return host;
        }

        // ---------- The menu ----------

        void BuildMenu()
        {
            var parent = GameRoot.Hud.Root.parent ?? GameRoot.Ui.Root;
            _menu = new VisualElement { name = "argument-prototype-menu" };
            _menu.AddToClassList(UiHider.KeepClass);
            _menu.style.position = Position.Absolute;
            _menu.style.left = 560;
            _menu.style.top = 150;
            _menu.style.width = 800;
            _menu.style.backgroundColor = new Color(0.1f, 0.09f, 0.16f, 0.94f);
            _menu.style.paddingLeft = _menu.style.paddingRight = 30;
            _menu.style.paddingTop = _menu.style.paddingBottom = 24;
            _menu.style.borderTopLeftRadius = _menu.style.borderTopRightRadius = 16;
            _menu.style.borderBottomLeftRadius = _menu.style.borderBottomRightRadius = 16;
            parent.Add(_menu);

            var title = new Label("Argument battle prototype: “Take it apart”");
            title.style.fontSize = 38;
            title.style.color = new Color(1f, 0.79f, 0.29f);
            _menu.Add(title);
            var sub = new Label("Dream-Lena at the kitchen table (Night 2). Placeholder lines: the creator writes the real ones.");
            sub.style.fontSize = 22;
            sub.style.color = new Color(0.8f, 0.78f, 0.86f);
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginBottom = 16;
            _menu.Add(sub);

            Add("argument-play-normal", "Play (Normal)", () => Play(Normal));
            Add("argument-play-story", "Play (Story: nothing hurts, slips collect themselves)", () => Play(Story));
            Add("argument-play-longnight", "Play (Long Night: scripted but beatable)", () => Play(LongNight));
            Add("argument-play-nowalt", "Play without Walt's truth (Heard out at best)", () => Play(NoWalt));
            Add("argument-play-watch", "Watch it play (the autopilot)", () => Play(Watch));
            Add("argument-forget", "Forget the saved results", Forget);
            Add("argument-title", "Back to the title", () => GameRoot.Instance.QuitToTitle());

            _saved = new Label();
            _saved.style.fontSize = 20;
            _saved.style.color = new Color(0.8f, 0.78f, 0.86f);
            _saved.style.whiteSpace = WhiteSpace.Normal;
            _saved.style.marginTop = 14;
            _menu.Add(_saved);
        }

        void Add(string name, string text, Action action)
        {
            // The press that closed the end card mustn't also click the menu's first button.
            var b = new Button(() => { if (MenuShown && Time.unscaledTime - _shownAt >= 0.25f) action(); }) { name = name, text = text };
            b.AddToClassList("jk-button");
            b.style.marginTop = 6;
            b.style.marginBottom = 6;
            _menu.Add(b);
            _buttons.Add(b);
        }

        void Forget()
        {
            GameRoot.Save.Data.SetEntry(ArgumentLedger.SaveKey, null);
            GameRoot.Save.Save();
            RefreshSaved();
        }

        void ShowMenu(bool shown)
        {
            if (_menu == null) return;
            _menu.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown)
            {
                // Hidden buttons keep focus otherwise, and Enter would click them behind the battle.
                var f = _menu.focusController != null ? _menu.focusController.focusedElement as VisualElement : null;
                if (f != null && _buttons.Contains(f as Button)) f.Blur();
                return;
            }
            _shownAt = Time.unscaledTime;
            RefreshSaved();
            FocusMenu();
        }

        public bool MenuShown => _menu != null && _menu.style.display == DisplayStyle.Flex;

        void RefreshSaved()
        {
            if (_saved == null) return;
            var ledger = ArgumentHost.LoadLedger();
            var r = ledger.Get(DreamLenaDemo.Id);
            _saved.text = r == null
                ? "Saved: nothing yet. Each battle's result is kept, and a later battle reads it (Lena's blessing, or not)."
                : "Saved: Lena is " + ArgumentEngine.StanceWords(r.stance) + " (" + r.outcome + ", anger " + r.anger + ", presses " + r.presses + ", played " + r.plays + "x).";
        }

        void FocusMenu()
        {
            if (_buttons.Count == 0) return;
            _buttons[0].Focus();
            _menu.schedule.Execute(() => { if (MenuShown) _buttons[0].Focus(); });
        }

        void Update()
        {
            // Keyboard and pad: keep focus in the menu while it's up (a click on empty space, or the pause menu, drops it).
            if (!MenuShown || GameRoot.Ui.Top != null) return;
            var input = GameRoot.Input;
            if (!(input.Navigate.WasPressedThisFrame() || input.Submit.WasPressedThisFrame())) return;
            var focused = _menu.focusController != null ? _menu.focusController.focusedElement as VisualElement : null;
            if (focused == null || !_buttons.Contains(focused as Button)) FocusMenu();
        }
    }
}
