using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument
{
    /// <summary>
    /// The prototype's scene (ArgumentPrototype, built by ArgumentPrototypeBuilder): a menu on black to start Dream-Lena's
    /// battle in each setting (Normal, Story, the Long Night's "scripted but beatable", without Walt's truth, or watched
    /// by its autopilot), what the save remembers, and the way back. The menu uses the battle's focus model (ArgueNav):
    /// up / down to choose, A / Enter to start; the mouse hovers and clicks. Presenter chapters (Ctrl+Shift+P, or hold
    /// L3+R3) start each setting directly; Visiting Hours' and the Dream Escape's chapter lists also carry "Argument
    /// prototype (Dream-Lena)", which jumps here.
    ///
    /// The battle style (docs/redesign/battle.md, round 2): a menu line switches every battle between the sections (arguing
    /// and dodging take turns) and the debate (milestone 1), kept for the whole game (ArgumentHost.DefaultPresenter); the
    /// chapter "Dream-Lena: the debate (milestone 1)" plays the debate whatever the switch says.
    /// </summary>
    public sealed class ArgumentPrototype : MonoBehaviour
    {
        public const string Scene = "ArgumentPrototype";
        public const string Normal = "Dream-Lena: Normal";
        public const string Story = "Dream-Lena: Story (nothing hurts)";
        public const string LongNight = "Dream-Lena: Long Night (scripted but beatable)";
        public const string NoWalt = "Dream-Lena: without Walt's truth";
        public const string Watch = "Dream-Lena: watch it play";
        public const string Walt = "Walt: Night 1 tutorial (Mags coaching)";
        public const string Debate = "Dream-Lena: the debate (milestone 1)";
        public static readonly string[] Chapters = { Normal, Story, LongNight, NoWalt, Watch, Walt, Debate };

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
        Label _saved, _hint;
        readonly ArgueNav _nav = new ArgueNav();
        readonly NavInput _navInput = new NavInput();
        readonly List<NavItem> _items = new List<NavItem>(8);
        float _shownAt = -10f;

        static ArgumentStrings S => ArgumentStrings.Shared;

        void Start()
        {
            if (!GameRoot.Exists) { enabled = false; return; }
            ArgumentContent.Ensure();
            _current = this;
            PresenterMode.Register(Scene, Normal, () => Play(Normal), this);
            PresenterMode.Register(Scene, Story, () => Play(Story), this);
            PresenterMode.Register(Scene, LongNight, () => Play(LongNight), this);
            PresenterMode.Register(Scene, NoWalt, () => Play(NoWalt), this);
            PresenterMode.Register(Scene, Watch, () => Play(Watch), this);
            PresenterMode.Register(Scene, Walt, () => Play(Walt), this);
            PresenterMode.Register(Scene, Debate, () => Play(Debate), this);
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
                    setup.difficulty = ArgumentDifficulty.Story;
                    setup.allEvidence = true;
                    break;
                case Watch:
                    setup.allEvidence = true;
                    break;
                case LongNight:
                    setup.difficulty = ArgumentDifficulty.ScriptedButBeatable;
                    setup.allEvidence = true;
                    break;
                case NoWalt:
                    setup.evidence.AddRange(DreamLenaDemo.EvidenceWithoutWalt);
                    break;
                case Walt:
                    // Night 1's tutorial with what the street gives her by then.
                    setup.evidence.AddRange(new[] { WaltNight1.WaltDead, WaltNight1.SameSaturday, WaltNight1.WaltAlive });
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
            ShowMenu(false);
            var def = chapter == Walt ? WaltNight1.Build() : DreamLenaDemo.Build();
            ArgumentPresenter? presenter = chapter == Debate ? ArgumentPresenter.Debate : (ArgumentPresenter?)null;
            var host = ArgumentHost.Begin(def, SetupFor(chapter), _ => ShowMenu(true), presenter);
            // Watched, it plays like a first-time player (reads before it acts, and slips the way people do).
            if (host != null && chapter == Watch) host.SetAutopilot(true, true, BattlePilot.FirstTime);
            return host;
        }

        // ---------- The menu ----------

        void BuildMenu()
        {
            var parent = GameRoot.Hud.Root.parent ?? GameRoot.Ui.Root;
            _menu = new VisualElement { name = "argument-prototype-menu", pickingMode = PickingMode.Position };
            _menu.AddToClassList(UiHider.KeepClass);
            ArgueUi.Fill(_menu);
            _menu.style.backgroundColor = ArgueUi.Black;
            _menu.style.alignItems = Align.Center;
            _menu.style.paddingTop = 110;
            parent.Add(_menu);

            var title = ArgueUi.Text(_menu, S["menu.title"], ArgumentArt.Display, 56, ArgueUi.Gold, "argument-menu-title");
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            var sub = ArgueUi.Text(_menu, S["menu.subtitle"], ArgumentArt.BodyItalic, 24, ArgueUi.Dim);
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            sub.style.marginTop = 8;
            sub.style.marginBottom = 34;
            sub.style.width = 1100;

            Add("argument-play-normal", S["menu.normal"], () => Play(Normal));
            Add("argument-play-story", S["menu.story"], () => Play(Story));
            Add("argument-play-longnight", S["menu.longNight"], () => Play(LongNight));
            Add("argument-play-nowalt", S["menu.noWalt"], () => Play(NoWalt));
            Add("argument-play-watch", S["menu.watch"], () => Play(Watch));
            Add("argument-play-walt", S["menu.walt"], () => Play(Walt));
            _presenterLine = Add("argument-presenter", PresenterLabel(), TogglePresenter);
            Add("argument-forget", S["menu.forget"], Forget);
            Add("argument-title", S["menu.title.back"], () => GameRoot.Instance.QuitToTitle());

            _saved = ArgueUi.Text(_menu, "", ArgumentArt.BodyItalic, 22, ArgueUi.Dim, "argument-menu-saved");
            _saved.style.marginTop = 22;
            _saved.style.width = 1000;
            _saved.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint = ArgueUi.Text(_menu, S["menu.hint"], ArgumentArt.Body, 18, new Color(0.5f, 0.48f, 0.56f));
            _hint.style.marginTop = 10;

            foreach (var i in _items) _nav.AddLine(i);
        }

        Label _presenterLine;

        static string PresenterLabel() => S[ArgumentHost.DefaultPresenter == ArgumentPresenter.Debate ? "menu.presenter.debate" : "menu.presenter.sections"];

        /// <summary>Switches every battle between the sections and the debate (kept for the whole game).</summary>
        void TogglePresenter()
        {
            ArgumentHost.DefaultPresenter = ArgumentHost.DefaultPresenter == ArgumentPresenter.Debate ? ArgumentPresenter.Sections : ArgumentPresenter.Debate;
            if (_presenterLine != null) _presenterLine.text = PresenterLabel();
        }

        Label Add(string name, string text, Action action)
        {
            var b = ArgueUi.CardBox(_menu, name, ArgueUi.Card, ArgueUi.CardEdge);
            b.style.width = 760;
            b.style.minHeight = 64;
            b.style.paddingLeft = b.style.paddingRight = 16;
            b.style.marginTop = b.style.marginBottom = 6;
            b.style.justifyContent = Justify.Center;
            var label = ArgueUi.Text(b, text, ArgumentArt.BodyBold, 26, ArgueUi.Ink);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            var item = new NavItem { id = name, element = b, activate = action };
            b.RegisterCallback<PointerEnterEvent>(_ => { if (MenuShown && GameRoot.Input.HoverAllowed) _nav.Focus(item); });
            b.RegisterCallback<ClickEvent>(_ => Activate(item));
            _items.Add(item);
            return label;
        }

        void Activate(NavItem item)
        {
            // The press that closed the end card mustn't also choose here.
            if (!MenuShown || Time.unscaledTime - _shownAt < 0.25f || item?.activate == null) return;
            GameRoot.Audio.Play("ui_select");
            item.activate();
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
            if (!shown) return;
            _shownAt = Time.unscaledTime;
            _navInput.Reset();
            RefreshSaved();
            if (_nav.Focused == null) _nav.FocusFirst();
            _nav.Highlight();
        }

        public bool MenuShown => _menu != null && _menu.style.display == DisplayStyle.Flex;

        /// <summary>The focused menu line's name (tests).</summary>
        public string FocusedId => _nav.FocusedId;

        void RefreshSaved()
        {
            if (_saved == null) return;
            var r = ArgumentHost.LoadLedger().Get(DreamLenaDemo.Id);
            _saved.text = r == null ? S["menu.savedNone"]
                : S.Format("menu.saved", "stance", ArgumentEngine.StanceWords(r.stance, S), "outcome", S["outcome." + r.outcome],
                    "n", r.anger.ToString(CultureInfo.InvariantCulture), "keys", r.plays.ToString(CultureInfo.InvariantCulture));
        }

        void Update()
        {
            if (!MenuShown || ArgumentHost.Running || GameRoot.Ui.Top != null || GameRoot.Flow.Current != GameState.Playing) return;
            var input = GameRoot.Input;
            var step = _navInput.Step(input, Time.unscaledDeltaTime);
            if (step.y != 0 && _nav.Move(0, step.y)) GameRoot.Audio.Play("ui_move");
            if (input.Submit.WasPressedThisFrame()) Activate(_nav.Focused);
        }
    }
}
