using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Comic lettering in play (plan task C2; docs/SKYLINE-HUD.md "Comic lettering on hits and kills"). It replaces the text
    /// bubbles above the bots: a sound-effect word ("KZZT!", "SPLORCH!", "OBJECTION!", "WHUMP!") that writes itself out beside
    /// the bot, holds, and fades, on kills, weak-point hits, Writ spot breaks and big Breacher shoves, not on every hit. The
    /// words, the faces (Luckiest Guy and Bangers, used interchangeably), the sizes, the tilt, the gap and the cap of two on
    /// screen are the pure <see cref="ComicLettering"/> (C1); the write-out timing and the placement are the pure
    /// <see cref="LetterWipe"/> and <see cref="LetterPlacement"/> (<c>LetterLayout.cs</c>). This class only feeds them and draws.
    /// <para>Hostile red-magenta fill with a 3 px ink keyline, a random tilt of up to 8 degrees; each glyph's width wipes open
    /// left to right (a clip over the whole word, opened by <see cref="LetterWipe.Revealed"/>), then it holds and fades. The word
    /// sits beside its bot in panel space and is kept 120 px clear of the crosshair, inside the safe area, and off the other
    /// word. Two elements are made once and reused: nothing is created after <see cref="Build"/>.</para>
    /// <para>Sources: <see cref="SkylineBot.Killed"/> (a kill, at once); <see cref="SkylinePlayer.HitLanded"/> for the rest (a
    /// Writ spot break, a big Breacher shove, a weak-point hit), held back one frame so a killing pellet in the same shot wins.
    /// No label text anywhere else: the lettering is the only wording the HUD adds (the creator's rule).</para>
    /// </summary>
    public sealed class SkylineLettering
    {
        public const string LayerName = "hud-lettering";
        /// <summary>Glyph height in panel px for size classes 1..3 at the 1080p reference, and the Bangers correction (it runs
        /// small next to Luckiest Guy).</summary>
        public static readonly float[] SizePx = { 0f, 56f, 84f, 116f };
        public const float BangersScale = 1.15f, KeylinePx = SkylineHudStyle.KeylinePx, EdgeMargin = 40f, Pad = 6f;
        /// <summary>A glyph is about this fraction of the font size wide, before the label has been laid out.</summary>
        const float LuckiestWidth = 0.62f, BangersWidth = 0.50f;
        /// <summary>A non-kill hit is found on its bot within this many metres of the bot's silhouette.</summary>
        const float FindSlack = 1.5f;

        sealed class Slot
        {
            public VisualElement el, clip;
            public KeylinedLabel label;
            public bool active, useWorld, visible;
            public LetterRequest req;
            public float t, w, h;
            public Vector3 world;
            public float worldRadius;
            public SkylineBot bot;
            public Vector2 fixedAnchor;
            public float fixedRadius;
            public int side;
            public Vector2 centre;      // last placed centre, layer-local panel px
        }

        readonly Slot[] _slots = new Slot[ComicLettering.MaxOnScreen];
        ComicLettering _core = new ComicLettering(0x4C455454UL);
        VisualElement _layer;
        SkylinePlayer _player;
        float _clock;
        readonly float[] _others = new float[4 * ComicLettering.MaxOnScreen];
        int _created;

        // a non-kill event waiting one frame, so a kill in the same shot wins
        bool _hasPending;
        LetterEvent _pendingEvent;
        SkylineBot _pendingBot;
        string _pendingGun;
        int _pendingFrame;
        SkylineBot _killBot;
        int _killFrame = -10;

        /// <summary>The layer holding the words (named <see cref="LayerName"/>), or null before <see cref="Build"/>.</summary>
        public VisualElement Layer => _layer;
        /// <summary>How many UI elements the pool has made, ever (constant after <see cref="Build"/>: the pool never grows).</summary>
        public int ElementsCreated => _created;
        /// <summary>The words on screen now, fading ones included.</summary>
        public int Active
        {
            get
            {
                int n = 0;
                foreach (var s in _slots) if (s != null && s.active) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ building

        /// <summary>Adds the (empty) lettering layer to <paramref name="parent"/> (the HUD's safe area) with the pooled words.</summary>
        public void Build(VisualElement parent)
        {
            _layer = SkylineHudStyle.Layer(parent, LayerName);
            _layer.style.left = 0;
            _layer.style.top = 0;
            _layer.style.right = 0;
            _layer.style.bottom = 0;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = MakeSlot(i);
        }

        Slot MakeSlot(int i)
        {
            var s = new Slot();
            s.el = SkylineHudStyle.Layer(_layer, "hud-lettering-" + i);
            s.el.style.display = DisplayStyle.None;
            s.el.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            s.clip = SkylineHudStyle.Layer(s.el, "hud-lettering-clip-" + i);
            s.clip.style.left = 0;
            s.clip.style.top = 0;
            s.clip.style.overflow = Overflow.Hidden;
            s.label = new KeylinedLabel(SizePx[3], KeylinePx, SkylineHudStyle.AmmoFontDefault);
            s.label.SetColor(SkylineHudStyle.Hostile);
            s.label.style.position = Position.Absolute;
            s.label.style.left = Pad;
            s.label.style.top = Pad;
            s.clip.Add(s.label);
            _created += 2 + 9;   // the word, its clip and the keylined label's nine labels
            return s;
        }

        /// <summary>Subscribes to the bots' kills and the player's hits. The HUD calls it from <c>Start</c>.</summary>
        public void Bind(SkylinePlayer player)
        {
            Unbind();
            _player = player;
            SkylineBot.Killed += OnKilled;
            if (_player != null) _player.HitLanded += OnHit;
        }

        /// <summary>Unsubscribes (the HUD calls it from <c>OnDestroy</c>).</summary>
        public void Unbind()
        {
            SkylineBot.Killed -= OnKilled;
            if (_player != null) _player.HitLanded -= OnHit;
            _player = null;
        }

        /// <summary>A wreck restart: every word goes, the picker forgets its gap.</summary>
        public void Reset()
        {
            foreach (var s in _slots) if (s != null) Deactivate(s);
            _core.Reset();
            _hasPending = false;
        }

        // ------------------------------------------------------------------ events

        void OnKilled(SkylineBot bot, GunDef gun)
        {
            _killBot = bot;
            _killFrame = Time.frameCount;
            if (_hasPending && _pendingBot == bot) _hasPending = false;
            ShowOnBot(LetterEvent.Kill, bot, gun != null ? gun.id : null);
        }

        void OnHit(SkylineHitInfo info, SkylineHitResult r)
        {
            if (r.killed || info.gun == null) return;   // kills are lettered from SkylineBot.Killed
            LetterEvent e;
            if (r.writ.spotHit) e = LetterEvent.WritSpotBreak;
            else if (info.gun.id == SkylineGuns.BreacherId && ComicLettering.IsBigShove(info.gun.enemyKnockback)) e = LetterEvent.BreacherShove;
            else if (info.zone == HitZone.WeakPoint) e = LetterEvent.WeakPoint;
            else return;
            var bot = BotAt(info.point);
            if (bot == null) return;
            if (_hasPending && ComicLettering.SizeOf(_pendingEvent) >= ComicLettering.SizeOf(e)) return;
            _hasPending = true;
            _pendingEvent = e;
            _pendingBot = bot;
            _pendingGun = info.gun.id;
            _pendingFrame = Time.frameCount;
        }

        /// <summary>The live bot a hit at <paramref name="point"/> landed on (the nearest silhouette), or null.</summary>
        static SkylineBot BotAt(Vector3 point)
        {
            SkylineBot best = null;
            float bestD = float.MaxValue;
            var live = SkylineBot.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var b = live[i];
                if (b == null) continue;
                float d = Vector3.Distance(b.Center, point) - b.Radius;
                if (d < bestD && d <= FindSlack) { bestD = d; best = b; }
            }
            return best;
        }

        // ------------------------------------------------------------------ showing

        /// <summary>Asks the picker for lettering for <paramref name="e"/> on <paramref name="bot"/> and shows it if it says
        /// yes. Does nothing when the bot is off screen or behind the camera.</summary>
        public bool ShowOnBot(LetterEvent e, SkylineBot bot, string gunId)
        {
            if (_layer == null || bot == null) return false;
            var cam = CameraOf();
            if (cam == null || _layer.panel == null) return false;
            Vector3 world = bot.Center;
            Vector3 vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0.1f || vp.x < -0.02f || vp.x > 1.02f || vp.y < -0.02f || vp.y > 1.02f) return false;
            var req = _core.Pick(e, bot.Def != null ? bot.Def.id : null, gunId, _clock, Active);
            if (req == null) return false;
            var s = Free();
            if (s == null) return false;
            Begin(s, req);
            s.useWorld = true;
            s.world = world;
            s.worldRadius = bot.Radius;
            s.bot = bot;
            return true;
        }

        /// <summary>The same, anchored at a point in panel px (layer-local) with an apparent bot radius, no camera: for tests and
        /// stills. Returns the request shown, or null when the picker or the cap refused.</summary>
        public LetterRequest ShowAtPanel(LetterEvent e, string botId, string gunId, Vector2 anchor, float radiusPx)
        {
            if (_layer == null) return null;
            var req = _core.Pick(e, botId, gunId, _clock, Active);
            if (req == null) return null;
            var s = Free();
            if (s == null) return null;
            Begin(s, req);
            s.useWorld = false;
            s.fixedAnchor = anchor;
            s.fixedRadius = radiusPx;
            return req;
        }

        Slot Free()
        {
            foreach (var s in _slots) if (s != null && !s.active) return s;
            return null;
        }

        void Begin(Slot s, LetterRequest req)
        {
            s.req = req;
            s.active = true;
            s.visible = false;
            s.t = 0f;
            s.bot = null;
            s.side = 0;
            bool bangers = req.font == LetterFont.Bangers;
            float px = SizePx[Mathf.Clamp(req.size, 1, 3)] * (bangers ? BangersScale : 1f);
            string face = bangers ? "Bangers" : "LuckiestGuy";
            SkylineHudStyle.ApplyFace(s.label.Front.style, face);
            foreach (var l in s.label.Children()) if (l is Label lab) { SkylineHudStyle.ApplyFace(lab.style, face); lab.style.fontSize = px; }
            s.label.Front.style.fontSize = px;
            s.label.SetText(req.text);
            int n = req.text.Length;
            s.w = n * px * (bangers ? BangersWidth : LuckiestWidth) + 2f * Pad + 2f * KeylinePx;
            s.h = px * 1.1f + 2f * Pad + 2f * KeylinePx;
            s.el.style.width = s.w;
            s.el.style.height = s.h;
            s.clip.style.height = s.h;
            s.clip.style.width = 0;
            s.el.style.rotate = new Rotate(new Angle(req.tiltDeg));
            s.el.style.opacity = 0f;
            s.el.style.display = DisplayStyle.Flex;
        }

        static void Deactivate(Slot s)
        {
            s.active = false;
            s.bot = null;
            s.el.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ the frame

        /// <summary>Per frame (unscaled seconds): ages the words, projects them to the panel, places them clear of the crosshair
        /// and the other word, and sets the wipe and the fade.</summary>
        public void Tick(float dt)
        {
            if (_layer == null) return;
            _clock += dt;
            if (_hasPending && Time.frameCount > _pendingFrame)
            {
                _hasPending = false;
                bool killed = _killBot == _pendingBot && _killFrame >= _pendingFrame;
                if (!killed && _pendingBot != null) ShowOnBot(_pendingEvent, _pendingBot, _pendingGun);
            }

            float pw = _layer.resolvedStyle.width, ph = _layer.resolvedStyle.height;
            if (!(pw > 1f) || !(ph > 1f)) { pw = 1920f; ph = 1080f; }
            float crossX = pw * 0.5f, crossY = ph * 0.5f;
            var cam = CameraOf();

            int others = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (!s.active) continue;
                s.t += dt;
                int n = s.req.text.Length;
                if (s.t >= s.req.seconds) { Deactivate(s); continue; }

                // where the bot is on the panel
                Vector2 anchor;
                float radius;
                if (!Project(s, cam, out anchor, out radius)) { s.el.style.display = DisplayStyle.None; s.visible = false; continue; }
                if (!LetterPlacement.Place(anchor.x, anchor.y, radius, s.w, s.h, crossX, crossY, pw, ph, EdgeMargin, s.side,
                        _others, others, out float cx, out float cy, out int side))
                {
                    s.el.style.display = DisplayStyle.None;
                    s.visible = false;
                    continue;
                }
                s.side = side;
                s.centre = new Vector2(cx, cy);
                s.visible = true;
                s.el.style.display = DisplayStyle.Flex;
                s.el.style.left = cx;
                s.el.style.top = cy;
                s.clip.style.width = s.w * LetterWipe.Revealed(n, s.t);
                s.el.style.opacity = LetterWipe.Alpha(n, s.t);
                _others[4 * others] = cx;
                _others[4 * others + 1] = cy;
                _others[4 * others + 2] = s.w;
                _others[4 * others + 3] = s.h;
                others++;
            }
        }

        bool Project(Slot s, Camera cam, out Vector2 anchor, out float radius)
        {
            anchor = s.fixedAnchor;
            radius = s.fixedRadius;
            if (!s.useWorld) return true;
            if (cam == null || _layer.panel == null) return false;
            Vector3 world = s.bot != null ? s.bot.Center : s.world;
            if (cam.WorldToViewportPoint(world).z <= 0.1f) return false;
            Vector2 p = _layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel, world, cam));
            Vector2 q = _layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel,
                world + cam.transform.right * s.worldRadius, cam));
            anchor = p;
            radius = Mathf.Max(8f, Vector2.Distance(p, q));
            return true;
        }

        Camera CameraOf() => _player != null && _player.Camera != null ? _player.Camera : Camera.main;

        // ------------------------------------------------------------------ for tests and stills

        /// <summary>The boxes (centre x, centre y, width, height; layer-local panel px) of the words showing after the last
        /// <see cref="Tick"/>.</summary>
        public void ActiveBoxes(List<Vector4> into)
        {
            into.Clear();
            foreach (var s in _slots) if (s != null && s.active && s.visible) into.Add(new Vector4(s.centre.x, s.centre.y, s.w, s.h));
        }

        /// <summary>The texts of the words showing or fading.</summary>
        public void ActiveTexts(List<string> into)
        {
            into.Clear();
            foreach (var s in _slots) if (s != null && s.active) into.Add(s.req.text);
        }

        /// <summary>The picker's clock (the lettering's own unscaled time).</summary>
        public float Clock => _clock;
    }
}
