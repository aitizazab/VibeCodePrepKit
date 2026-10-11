using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The title as a table (docs/OPENING-AND-TITLE.md): the tablet is Play, the coffee mug Quit (not
    /// in the web build), Nadia's ID badge Settings, the notepad and pen Credits. The mouse highlights
    /// what it's over and a click chooses it; arrow keys, the d-pad or the stick move the highlight to
    /// the nearest thing in that direction, and Enter or (A) chooses. The first key press only highlights
    /// (the tablet). Only while the title is the screen on top (Settings, the presenter panel and
    /// the credits pause it), and only once the opening has made the table interactive.
    /// Choices go through the shell's own title buttons, so Play, Settings, Credits and Quit behave
    /// exactly like the menu's.
    /// </summary>
    public sealed class VhTitleTable : MonoBehaviour
    {
        /// <summary>Tests: return true to swallow a choice (Quit would stop play mode).</summary>
        public static System.Func<VhTitleAction, bool> Intercept;

        public Camera cam;
        public readonly List<VhTitlePiece> pieces = new List<VhTitlePiece>();
        public bool interactive;

        VhTitlePiece _current;
        Vector2 _lastMouse;
        bool _mouseSeen;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        public VhTitlePiece Current => _current;
        public string CurrentGroup => _current != null ? _current.Group : "";
        VhTitleAction _lastChosen;
        public VhTitleAction LastChosen => _lastChosen;

        /// <summary>Quit is shown only where quitting makes sense (not in a browser).</summary>
        public static bool QuitAvailable => Application.platform != RuntimePlatform.WebGLPlayer;

        public VhTitlePiece Find(string id)
        {
            foreach (var p in pieces)
                if (p != null && p.id == id) return p;
            return null;
        }

        public bool Selectable(VhTitlePiece p) =>
            p != null && p.action != VhTitleAction.None && (p.action != VhTitleAction.Quit || QuitAvailable) && p.Group == p.id;

        /// <summary>True while the table takes input: interactive, the title on top, nothing loading, and the opening
        /// editor closed (a click in the editor's view never presses the tablet's Play).</summary>
        public bool Live =>
            interactive && GameRoot.Exists && GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene
            && GameRoot.Ui.Top == GameRoot.Ui.Title && GameRoot.Ui.Title.IsVisible
            && (VhOpeningEditor.Instance == null || !VhOpeningEditor.Instance.IsOpen);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null) pieces[i].Tick(dt);
            if (!Live)
            {
                if (_current != null && !interactive) Highlight(null);
                return;
            }
            var input = GameRoot.Input;

            // Mouse: only when it moves (a still mouse doesn't fight the keyboard).
            var synth = input.Synthetic;
            var mouse = synth == null ? Mouse.current : null;
            if (mouse != null && cam != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                bool moved = !_mouseSeen || (pos - _lastMouse).sqrMagnitude > 4f;
                _mouseSeen = true;
                _lastMouse = pos;
                if (moved) Highlight(PieceAt(pos));
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    var hit = PieceAt(pos);
                    if (hit != null)
                    {
                        Highlight(hit);
                        Choose(hit);
                        return;
                    }
                }
            }

            bool nav = synth != null ? synth.WasTapped(input.Navigate) : input.Navigate.WasPressedThisFrame();
            bool submit = synth != null ? synth.WasTapped(input.Submit) : input.Submit.WasPressedThisFrame();
            if (nav)
            {
                Vector2 dir = synth != null ? synth.move : input.Navigate.ReadValue<Vector2>();
                if (_current == null) Highlight(Find("tablet"));
                else Highlight(Neighbour(_current, dir));
                GameRoot.Ui.PlayUiSound("ui_move");
            }
            if (submit)
            {
                if (_current == null)
                {
                    Highlight(Find("tablet"));
                    GameRoot.Ui.PlayUiSound("ui_move");
                }
                else Choose(_current);
            }
        }

        VhTitlePiece PieceAt(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            int n = Physics.RaycastNonAlloc(ray, _hits, 10f, ~0, QueryTriggerInteraction.Collide);
            VhTitlePiece best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var p = _hits[i].collider != null ? _hits[i].collider.GetComponentInParent<VhTitlePiece>() : null;
                if (p == null) continue;
                var owner = Owner(p);
                if (!Selectable(owner) || _hits[i].distance >= bestD) continue;
                best = owner;
                bestD = _hits[i].distance;
            }
            return best;
        }

        /// <summary>The selectable piece a piece belongs to (the pen belongs to the notepad).</summary>
        VhTitlePiece Owner(VhTitlePiece p) => p.Group == p.id ? p : Find(p.Group);

        /// <summary>The nearest selectable piece on screen in direction <paramref name="dir"/> (wrapping round when there's none).</summary>
        VhTitlePiece Neighbour(VhTitlePiece from, Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.01f) return from;
            dir.Normalize();
            Vector2 a = Screen(from);
            VhTitlePiece best = null, far = null;
            float bestScore = float.MaxValue, farScore = float.MinValue;
            foreach (var p in pieces)
            {
                if (p == from || !Selectable(p)) continue;
                Vector2 d = Screen(p) - a;
                float along = Vector2.Dot(d, dir);
                if (along > 1f)
                {
                    float across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                    float score = along + across * 2f;
                    if (score < bestScore) { bestScore = score; best = p; }
                }
                else if (-along > farScore) { farScore = -along; far = p; }
            }
            return best != null ? best : (far != null ? far : from);
        }

        Vector2 Screen(VhTitlePiece p) => cam != null ? (Vector2)cam.WorldToScreenPoint(p.transform.position) : Vector2.zero;

        public void Highlight(VhTitlePiece p)
        {
            if (p == _current) return;
            _current = p;
            string g = p != null ? p.Group : null;
            foreach (var q in pieces)
                if (q != null) q.SetHighlight(g != null && q.Group == g);
        }

        /// <summary>Chooses a piece (by id): what a click or Enter does. False if it isn't a choice right now.</summary>
        public bool Choose(string id)
        {
            var p = Find(id);
            if (p == null) return false;
            return Choose(Owner(p));
        }

        public bool Choose(VhTitlePiece p)
        {
            if (!Selectable(p) || !interactive) return false;
            Highlight(p);
            _lastChosen = p.action;
            if (Intercept != null && Intercept(p.action)) return true;
            GameRoot.Ui.PlayUiSound("ui_select");
            var title = GameRoot.Ui.Title;
            switch (p.action)
            {
                case VhTitleAction.Play: return title.Activate("play");
                case VhTitleAction.Settings: return title.Activate("settings");
                case VhTitleAction.Credits: return title.Activate("credits");
                case VhTitleAction.Quit: return QuitAvailable && title.Activate("quit");
                default: return false;
            }
        }
    }
}
