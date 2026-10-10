using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The phone's screen (drawn into a texture on the phone in the bedroom): Elias calling, the missed
    /// call with its voicemail, and the voicemail playing (a progress bar, the speaker lit after the drop).
    /// </summary>
    public sealed class VhPhoneScreen
    {
        public const int Width = 360, Height = 740;
        readonly VisualElement _incoming, _missed, _playing, _fill, _speaker;
        readonly VisualElement[] _rings = new VisualElement[3];
        readonly Label _elapsed;
        readonly float _length;
        float _pulse;
        int _shownSecond = -1;
        string _state = "";

        public string State => _state;

        public VhPhoneScreen(VisualElement root, Texture2D wallpaper, float voicemailLength)
        {
            _length = voicemailLength > 0f ? voicemailLength : 32f;
            root.AddToClassList("vh-phone");
            if (wallpaper != null) root.style.backgroundImage = new StyleBackground(wallpaper);
            var status = Add(root, "vh-phone__status");
            status.Add(Text("3:04", "vh-phone__status-time"));
            status.Add(Text("82%", "vh-phone__status-battery"));

            _incoming = Add(root, "vh-phone__page");
            var ringBox = Add(_incoming, "vh-phone__rings");
            for (int i = 0; i < _rings.Length; i++) _rings[i] = Add(ringBox, "vh-phone__ring");
            var avatar = Add(ringBox, "vh-phone__avatar");
            avatar.Add(Text("E", "vh-phone__avatar-letter"));
            _incoming.Add(Text("Elias", "vh-phone__name"));
            _incoming.Add(Text("mobile", "vh-phone__sub"));
            var buttons = Add(_incoming, "vh-phone__buttons");
            Add(buttons, "vh-phone__button").AddToClassList("vh-phone__button--decline");
            Add(buttons, "vh-phone__button").AddToClassList("vh-phone__button--accept");

            _missed = Add(root, "vh-phone__page");
            _missed.Add(Text("3:04", "vh-phone__clock"));
            _missed.Add(Text("Sunday, 23 August", "vh-phone__date"));
            var card = Add(_missed, "vh-phone__card");
            card.Add(Text("Missed call", "vh-phone__card-title"));
            card.Add(Text("Elias", "vh-phone__card-name"));
            var vm = Add(_missed, "vh-phone__card");
            vm.AddToClassList("vh-phone__card--voicemail");
            vm.Add(Text("Voicemail", "vh-phone__card-title"));
            vm.Add(Text("Elias  ·  " + Clock(_length), "vh-phone__card-name"));
            vm.Add(Text("Play", "vh-phone__card-play"));

            _playing = Add(root, "vh-phone__page");
            _playing.Add(Text("Voicemail", "vh-phone__header"));
            var av2 = Add(_playing, "vh-phone__avatar");
            av2.AddToClassList("vh-phone__avatar--small");
            av2.Add(Text("E", "vh-phone__avatar-letter"));
            _playing.Add(Text("Elias", "vh-phone__name"));
            _playing.Add(Text("Today, 3:03 AM", "vh-phone__sub"));
            var bar = Add(_playing, "vh-phone__bar");
            _fill = Add(bar, "vh-phone__bar-fill");
            _elapsed = Text("0:00 / " + Clock(_length), "vh-phone__time");
            _playing.Add(_elapsed);
            _speaker = Text("Speaker", "vh-phone__speaker");
            _playing.Add(_speaker);
            Show("");
        }

        static VisualElement Add(VisualElement parent, string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent.Add(e);
            return e;
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            return l;
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>incoming, missed, playing (anything else: the lock screen's missed page stays).</summary>
        public void Show(string state)
        {
            _state = state;
            _incoming.style.display = state == "incoming" ? DisplayStyle.Flex : DisplayStyle.None;
            _missed.style.display = state == "missed" ? DisplayStyle.Flex : DisplayStyle.None;
            _playing.style.display = state == "playing" ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetSpeaker(bool on) => _speaker.EnableInClassList("vh-phone__speaker--on", on);

        /// <summary>Animates the rings and the voicemail's progress (<paramref name="playback"/> seconds in).</summary>
        public void Tick(float dt, float playback)
        {
            _pulse += dt;
            if (_state == "incoming")
                for (int i = 0; i < _rings.Length; i++)
                {
                    float t = Mathf.Repeat(_pulse * 0.6f + i / 3f, 1f);
                    _rings[i].style.scale = new Scale(new Vector3(0.6f + t * 0.9f, 0.6f + t * 0.9f, 1f));
                    _rings[i].style.opacity = (1f - t) * 0.6f;
                }
            if (_state == "playing")
            {
                float k = Mathf.Clamp01(playback / _length);
                _fill.style.width = Length.Percent(k * 100f);
                int s = Mathf.FloorToInt(playback);
                if (s != _shownSecond)
                {
                    _shownSecond = s;
                    _elapsed.text = Clock(playback) + " / " + Clock(_length);
                }
            }
        }
    }

    /// <summary>
    /// The tablet's screen: the news report's video (its frames crossfading with a slow drift, the
    /// emergency lights pulsing), a LIVE tag, and its own headline scrolling along the bottom of the
    /// video. When the report ends the video pauses and its play button appears: that's Play.
    /// </summary>
    public sealed class VhTabletScreen
    {
        public const int Width = 1024, Height = 640;
        readonly VisualElement _a, _b, _veil, _play, _red, _blue;
        readonly Label _headline;
        readonly List<Texture2D> _frames = new List<Texture2D>();
        readonly float _speed;
        float _clock, _frameClock, _headX = Width, _headW = 1600f;
        int _frame;
        bool _paused, _hover;
        float _playShown, _playHover;

        public bool Paused => _paused;
        public string Headline => _headline.text;
        public bool PlayButtonShown => _playShown > 0.5f;

        public VhTabletScreen(VisualElement root, IList<Texture2D> frames, Texture2D blob, string headline, float speed)
        {
            _speed = speed > 0f ? speed : 90f;
            foreach (var f in frames) if (f != null) _frames.Add(f);
            root.AddToClassList("vh-tablet");
            var video = Add(root, "vh-tablet__video");
            var layer = Add(video, "vh-tablet__frames");
            _b = Add(layer, "vh-tablet__frame");
            _a = Add(layer, "vh-tablet__frame");
            if (_frames.Count > 0) _a.style.backgroundImage = new StyleBackground(_frames[0]);
            if (_frames.Count > 1) _b.style.backgroundImage = new StyleBackground(_frames[1]);
            _b.style.opacity = 0f;
            _red = Add(video, "vh-tablet__light");
            _red.AddToClassList("vh-tablet__light--red");
            _blue = Add(video, "vh-tablet__light");
            _blue.AddToClassList("vh-tablet__light--blue");
            if (blob != null)
            {
                _red.style.backgroundImage = new StyleBackground(blob);
                _blue.style.backgroundImage = new StyleBackground(blob);
            }
            var live = Add(video, "vh-tablet__live");
            live.Add(Text("LIVE", "vh-tablet__live-text"));
            var bug = Text("CHANNEL 4 NEWS", "vh-tablet__bug");
            video.Add(bug);
            var lower = Add(video, "vh-tablet__lower");
            var tag = Add(lower, "vh-tablet__tag");
            tag.Add(Text("BREAKING", "vh-tablet__tag-text"));
            var strip = Add(lower, "vh-tablet__strip");
            _headline = Text(headline, "vh-tablet__headline");
            strip.Add(_headline);
            _veil = Add(root, "vh-tablet__veil");
            _play = new VisualElement { pickingMode = PickingMode.Ignore };
            _play.AddToClassList("vh-tablet__play");
            _play.generateVisualContent += DrawPlay;
            root.Add(_play);
            _veil.style.opacity = 0f;
            _play.style.opacity = 0f;
        }

        static VisualElement Add(VisualElement parent, string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent.Add(e);
            return e;
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            return l;
        }

        static void DrawPlay(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            var p = ctx.painter2D;
            float cx = r.width * 0.5f, cy = r.height * 0.5f, rad = Mathf.Min(r.width, r.height) * 0.5f - 4f;
            p.fillColor = new Color(0f, 0f, 0f, 0.45f);
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), rad, 0f, 360f);
            p.Fill();
            p.strokeColor = new Color(1f, 1f, 1f, 0.92f);
            p.lineWidth = 6f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), rad, 0f, 360f);
            p.Stroke();
            p.fillColor = new Color(1f, 1f, 1f, 0.95f);
            p.BeginPath();
            float s = rad * 0.55f;
            p.MoveTo(new Vector2(cx - s * 0.45f, cy - s * 0.75f));
            p.LineTo(new Vector2(cx + s * 0.85f, cy));
            p.LineTo(new Vector2(cx - s * 0.45f, cy + s * 0.75f));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>The report is over (true): the video pauses on its frame and the play button shows.</summary>
        public void SetPaused(bool paused) => _paused = paused;

        public void SetHover(bool hover) => _hover = hover;

        public void Tick(float dt)
        {
            _clock += dt;
            // The headline scrolls, always (a ticker runs under a paused video too).
            float w = _headline.resolvedStyle.width;
            if (!float.IsNaN(w) && w > 10f) _headW = w;
            _headX -= dt * _speed;
            if (_headX < -_headW) _headX = Width * 0.85f;
            _headline.style.translate = new Translate(_headX, 0f);

            _playShown = Mathf.MoveTowards(_playShown, _paused ? 1f : 0f, dt * 1.5f);
            _playHover = Mathf.MoveTowards(_playHover, _hover && _paused ? 1f : 0f, dt * 5f);
            _veil.style.opacity = _playShown * (0.45f - 0.15f * _playHover);
            _play.style.opacity = _playShown;
            float ps = 1f + 0.12f * VhUtil.Ease(_playHover) + 0.03f * Mathf.Sin(_clock * 2.4f) * _playShown;
            _play.style.scale = new Scale(new Vector3(ps, ps, 1f));
            if (_paused) return;

            // The video: frames crossfade every 6 s with a slow push in and drift.
            _frameClock += dt;
            float hold = 6f, fade = 1.5f;
            if (_frames.Count > 1)
            {
                if (_frameClock > hold)
                {
                    _frameClock -= hold;
                    _frame = (_frame + 1) % _frames.Count;
                    var front = _frame % 2 == 0 ? _a : _b;
                    front.style.backgroundImage = new StyleBackground(_frames[_frame]);
                    // The new frame fades in over the old one (inside the frames layer, under the lights and the lower third).
                    front.style.opacity = 0f;
                    front.BringToFront();
                }
                float k = _frame == 0 && _clock < hold ? 1f : Mathf.Clamp01(_frameClock / fade);
                bool aFront = _frame % 2 == 0;
                (aFront ? _a : _b).style.opacity = k;
                (aFront ? _b : _a).style.opacity = 1f;
            }
            float z = 1.04f + 0.06f * Mathf.Repeat(_clock / 18f, 1f);
            var drift = new Translate(Mathf.Sin(_clock * 0.07f) * 14f, Mathf.Cos(_clock * 0.05f) * 8f);
            _a.style.scale = new Scale(new Vector3(z, z, 1f));
            _b.style.scale = new Scale(new Vector3(z, z, 1f));
            _a.style.translate = drift;
            _b.style.translate = drift;
            // Emergency lights: red and blue, alternating.
            float red = Mathf.Max(0f, Mathf.Sin(_clock * 7.5f)), blue = Mathf.Max(0f, Mathf.Sin(_clock * 7.5f + Mathf.PI));
            _red.style.opacity = 0.15f + 0.55f * red * red;
            _blue.style.opacity = 0.15f + 0.55f * blue * blue;
        }
    }
}
