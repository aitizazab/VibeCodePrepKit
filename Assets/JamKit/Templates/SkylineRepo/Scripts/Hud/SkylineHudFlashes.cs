using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The flash curves and rules (plan task H5), pure statics so an EditMode test can check them: how bright the
    /// screen-edge flash is a moment after plating is gained, and when a special-ammo gain flashes the ammo counter.
    /// </summary>
    public static class SkylineHudFlashCurves
    {
        /// <summary>The plating flash's length (s) and its peak opacity (35%, docs/SKYLINE-PLAN-FLOOR1.md H5).</summary>
        public const float EdgeSeconds = 0.35f, EdgePeak = 0.35f;
        /// <summary>The flash snaps on over this many seconds (a hard hit of colour), then falls away.</summary>
        public const float EdgeAttack = 0.04f;

        /// <summary>The flash's shape <paramref name="t"/> seconds after it began: 0 before and after it, rising to 1 over
        /// <see cref="EdgeAttack"/>, then falling by a quadratic ease to 0 at <see cref="EdgeSeconds"/>. NaN gives 0.</summary>
        public static float EdgeShape(float t)
        {
            if (!(t > 0f) || t >= EdgeSeconds) return 0f;
            if (t < EdgeAttack) return t / EdgeAttack;
            float u = (t - EdgeAttack) / (EdgeSeconds - EdgeAttack);
            return (1f - u) * (1f - u);
        }

        /// <summary>The edge overlay's opacity, 0 to <see cref="EdgePeak"/>.</summary>
        public static float EdgeOpacity(float t) => EdgePeak * EdgeShape(t);

        /// <summary>Special ammo flashes the counter only while a special weapon is out and shots were really added.</summary>
        public static bool AmmoFlashWanted(bool activeGunIsSpecial, int shotsGained) => activeGunIsSpecial && shotsGained > 0;
    }

    /// <summary>
    /// The HUD's feedback flashes (docs/SKYLINE-HUD.md "Feedback flashes"; plan task H5). No text anywhere:
    /// <list type="bullet">
    /// <item><b>Plating gained</b> (<see cref="SkylineWreck.Stripped"/>): the screen edges flash light blue (the plating hue) for
    /// 0.35 s at up to 35% opacity, a hard-edged frame with chamfered corners and an ink keyline on its inner edge. One pooled
    /// element, repainted only when the screen size changes; a flash while one is running restarts it.</item>
    /// <item><b>Special ammo gained</b> (<see cref="SkylineAmmo.SpecialGained"/>) while a special weapon is out: the ammo digits
    /// flash green (<see cref="SkylineHudAmmo.Flash"/>, 0.4 s).</item>
    /// </list>
    /// The strippable wreck's thinning outline is the wreck's own (S0). Logs <c>SKYLINE_HUD flash plating</c> and
    /// <c>SKYLINE_HUD flash ammo</c>.
    /// </summary>
    public sealed class SkylineHudFlashes
    {
        /// <summary>The frame's thickness as a share of the screen's short side, and its corner chamfer in thicknesses.</summary>
        public const float Thickness = 0.055f, Chamfer = 3f;

        VisualElement _edge;
        SkylinePlayer _player;
        SkylineHudAmmo _ammo;
        float _t = -1f;
        bool _bound;

        /// <summary>The edge flash element (full screen, click-through; display none while idle).</summary>
        public VisualElement Edge => _edge;
        /// <summary>The edge flash's opacity right now (0 when idle).</summary>
        public float EdgeOpacityNow => _t < 0f ? 0f : SkylineHudFlashCurves.EdgeOpacity(_t);
        public bool EdgeActive => _t >= 0f;

        /// <summary>Adds the overlay to <paramref name="parent"/> (the HUD root, under the safe area so nothing it draws covers
        /// the bars).</summary>
        public VisualElement Build(VisualElement parent)
        {
            _edge = SkylineHudStyle.Layer(parent, "hud-flash-edge");
            _edge.style.left = 0;
            _edge.style.top = 0;
            _edge.style.right = 0;
            _edge.style.bottom = 0;
            _edge.style.opacity = 0f;
            _edge.style.display = DisplayStyle.None;
            _edge.generateVisualContent += Paint;
            _edge.RegisterCallback<GeometryChangedEvent>(_ => _edge.MarkDirtyRepaint());
            return _edge;
        }

        /// <summary>Starts listening for plating and special ammo gains (the HUD calls it once, and <see cref="Unbind"/> when it goes).</summary>
        public void Bind(SkylinePlayer player, SkylineHudAmmo ammo)
        {
            _player = player;
            _ammo = ammo;
            if (_bound) return;
            _bound = true;
            SkylineWreck.Stripped += OnStripped;
            SkylineAmmo.SpecialGained += OnSpecialGained;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;
            SkylineWreck.Stripped -= OnStripped;
            SkylineAmmo.SpecialGained -= OnSpecialGained;
        }

        void OnStripped(SkylineWreck wreck, float platingGained)
        {
            if (platingGained > 0f) PlatingGained();
        }

        void OnSpecialGained(int shots)
        {
            bool special = _player != null && _player.Loadout != null && _player.Loadout.Active.Def.special;
            if (SkylineHudFlashCurves.AmmoFlashWanted(special, shots)) AmmoGained();
        }

        /// <summary>Plating was gained: the screen edges flash light blue (restarts a running flash).</summary>
        public void PlatingGained()
        {
            _t = 0f;
            if (_edge != null) _edge.style.display = DisplayStyle.Flex;
            Debug.Log("SKYLINE_HUD flash plating");
        }

        /// <summary>Special ammo was gained with a special weapon out: the ammo digits flash green.</summary>
        public void AmmoGained()
        {
            if (_ammo != null) _ammo.Flash(SkylineHudStyle.Green);
            Debug.Log("SKYLINE_HUD flash ammo");
        }

        /// <summary>Per frame (unscaled seconds).</summary>
        public void Tick(float dt)
        {
            if (_t < 0f || _edge == null) return;
            _t += dt;
            if (_t >= SkylineHudFlashCurves.EdgeSeconds)
            {
                _t = -1f;
                _edge.style.opacity = 0f;
                _edge.style.display = DisplayStyle.None;
                return;
            }
            _edge.style.opacity = SkylineHudFlashCurves.EdgeOpacity(_t);
        }

        void Paint(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            float w = r.width, h = r.height;
            if (!(w > 1f) || !(h > 1f)) return;
            float t = Mathf.Min(w, h) * Thickness, c = t * Chamfer;
            var p = ctx.painter2D;
            p.fillColor = SkylineHudStyle.Plating;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, 0f));
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            Inner(p, w, h, t, c);
            p.Fill(FillRule.OddEven);

            // the ink keyline on the inner edge (a comic panel's border)
            p.BeginPath();
            Inner(p, w, h, t, c);
            p.strokeColor = SkylineHudStyle.Ink;
            p.lineWidth = SkylineHudStyle.KeylinePx;
            p.lineJoin = LineJoin.Miter;
            p.Stroke();
        }

        static void Inner(Painter2D p, float w, float h, float t, float c)
        {
            p.MoveTo(new Vector2(t + c, t));
            p.LineTo(new Vector2(w - t - c, t));
            p.LineTo(new Vector2(w - t, t + c));
            p.LineTo(new Vector2(w - t, h - t - c));
            p.LineTo(new Vector2(w - t - c, h - t));
            p.LineTo(new Vector2(t + c, h - t));
            p.LineTo(new Vector2(t, h - t - c));
            p.LineTo(new Vector2(t, t + c));
            p.ClosePath();
        }
    }
}
