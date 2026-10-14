using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The one health bar (docs/SKYLINE-HUD.md): a layered parallelogram. The chassis (health) is the slanted orange bar; the
    /// plating (the shield) is a brighter light-blue slab over it, offset up and left; hard edges, 3 px ink keylines, ink
    /// tick marks, no rounded corners. Plating breaking shatters the slab into eight shards that fall for 0.5 s. Chassis
    /// under 35% pulses the bar (the screen edges tint red: <see cref="EdgeTint"/>, drawn by <see cref="SkylineHud"/>).
    /// Drawn with <c>Painter2D</c> in a <c>generateVisualContent</c> callback; the numbers come from V1's
    /// <see cref="HealthBarModel"/>. Sizes are panel units at the 1920 x 1080 reference.
    /// </summary>
    public sealed class SkylineHudHealth
    {
        // Geometry (reference units).
        public const float BarW = 300f, BarH = 22f, Skew = 14f;
        public const float PlateH = 14f, PlateDx = -6f, PlateDy = -9f;
        /// <summary>The plate leans at the chassis bar's angle.</summary>
        public const float PlateSkew = Skew * PlateH / BarH;
        public const int ShardCount = 8;
        public const float ShatterSeconds = 0.5f, ShardFall = 34f, FlashSeconds = 0.28f;
        // The bar's origin inside its element (room for the plate sticking out up-left).
        const float OriginX = 12f, OriginY = 14f;
        public const float ElementW = OriginX + BarW + Skew + 12f, ElementH = OriginY + BarH + ShardFall + 2f;

        VisualElement _el;
        SkylinePlayer _player;
        bool _hasPlating;
        int _seenShatter;
        float _shatterT = -1f, _flash, _lastKey = float.NaN;

        /// <summary>The numbers the bar draws (null until <see cref="Bind"/>).</summary>
        public HealthBarModel Model { get; private set; }
        public VisualElement Element => _el;
        /// <summary>0..1 screen-edge red tint that follows the low-chassis pulse.</summary>
        public float EdgeTint => Model != null ? Model.EdgeTint : 0f;
        public float ChassisFrac => Model != null ? Model.ChassisFrac : 1f;
        public bool Shattering => _shatterT >= 0f;

        /// <summary>Adds the bar's element to <paramref name="parent"/> (bottom left, inside the safe area).</summary>
        public VisualElement Build(VisualElement parent, float margin)
        {
            _el = SkylineHudStyle.Layer(parent, "hud-health");
            _el.style.left = margin - OriginX;
            _el.style.bottom = margin - ShardFall - 2f;
            _el.style.width = ElementW;
            _el.style.height = ElementH;
            _el.generateVisualContent += Draw;
            return _el;
        }

        /// <summary>Starts following the player's armour (its maxima set the model's scale).</summary>
        public void Bind(SkylinePlayer player)
        {
            _player = player;
            var a = player != null ? player.Armor : null;
            if (a == null) return;
            Model = new HealthBarModel(a.ChassisMax, a.PlatingMax);
            Model.Set(a.Chassis, a.Plating);
            _hasPlating = a.PlatingMax > 0f;
            _seenShatter = Model.ShatterEvents;
            _lastKey = float.NaN;
            _el?.MarkDirtyRepaint();
        }

        /// <summary>The bar flashes for a moment (a hit that broke the plating).</summary>
        public void Flash() => _flash = FlashSeconds;

        /// <summary>Per frame (unscaled seconds): follows the armour, advances the pulse, the flash and the shatter.</summary>
        public void Tick(float dt)
        {
            if (_el == null) return;
            if (Model == null && _player != null && _player.Armor != null) Bind(_player);
            if (Model == null) return;
            var a = _player.Armor;
            Model.Set(a.Chassis, a.Plating);
            Model.Tick(dt);
            if (Model.ShatterEvents != _seenShatter)
            {
                _seenShatter = Model.ShatterEvents;
                _shatterT = 0f;
            }
            if (_shatterT >= 0f)
            {
                _shatterT += dt;
                if (_shatterT >= ShatterSeconds) _shatterT = -1f;
            }
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt);
            // Repaint only when something it draws has changed.
            float key = Model.ChassisFrac * 977f + Model.PlatingFrac * 31f + Model.LowPulse * 7919f + _shatterT * 1e4f + _flash * 311f;
            if (_shatterT >= 0f || _flash > 0f || key != _lastKey)
            {
                _lastKey = key;
                _el.MarkDirtyRepaint();
            }
        }

        // ------------------------------------------------------------------ geometry (testable)

        /// <summary>The four corners (bottom-left, top-left, top-right, bottom-right) of a parallelogram with a width of
        /// <paramref name="w"/> whose top edge leans right by <paramref name="skew"/>.</summary>
        public static Vector2[] Corners(float x, float y, float w, float h, float skew) => new[]
        {
            new Vector2(x, y + h), new Vector2(x + skew, y), new Vector2(x + skew + w, y), new Vector2(x + w, y + h),
        };

        /// <summary>Shard <paramref name="i"/> of the shattering plating slab at progress <paramref name="t01"/>: its four
        /// corners (fallen, drifted and tilted) and its alpha. The slab is cut into <see cref="ShardCount"/> slanted pieces.</summary>
        public static Vector2[] Shard(int i, float t01, out float alpha)
        {
            float segW = BarW / ShardCount;
            float x = OriginX + PlateDx + i * segW, y = OriginY + PlateDy;
            var c = Corners(x, y, segW, PlateH, PlateSkew);
            float e = t01 * t01;
            float fall = ShardFall * e * (0.65f + 0.35f * ((i * 5) % 3) / 2f);
            float drift = (i - (ShardCount - 1) * 0.5f) * 2.4f * t01;
            float rot = ((i & 1) == 0 ? 1f : -1f) * (0.4f + 0.12f * (i % 4)) * t01;
            var centre = new Vector2(x + PlateSkew * 0.5f + segW * 0.5f, y + PlateH * 0.5f);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int k = 0; k < 4; k++)
            {
                var d = c[k] - centre;
                c[k] = centre + new Vector2(d.x * cs - d.y * sn + drift, d.x * sn + d.y * cs + fall);
            }
            alpha = 1f - t01 * t01;
            return c;
        }

        // ------------------------------------------------------------------ drawing

        void Draw(MeshGenerationContext ctx)
        {
            if (Model == null) return;
            var p = ctx.painter2D;
            float cf = Model.ChassisFrac, pf = Model.PlatingFrac, pulse = Model.LowPulse;
            float ox = OriginX, oy = OriginY;

            // The chassis: a dark track, the orange fill (hot at the peak of the low pulse), ink ticks, a keyline.
            SkylineHudStyle.Parallelogram(p, ox, oy, BarW, BarH, Skew);
            p.fillColor = SkylineHudStyle.Track;
            p.Fill();
            if (cf > 0f)
            {
                var fillColor = Color.Lerp(SkylineHudStyle.Chassis, SkylineHudStyle.ChassisHot, pulse);
                if (_flash > 0f && Mathf.Repeat(_flash, 0.14f) > 0.07f) fillColor = Color.white;
                SkylineHudStyle.Parallelogram(p, ox, oy, BarW * cf, BarH, Skew);
                p.fillColor = fillColor;
                p.Fill();
                // A lighter band along the top edge: the cel highlight.
                SkylineHudStyle.Parallelogram(p, ox + Skew * (1f - 4f / BarH), oy, BarW * cf, 4f, Skew * 4f / BarH);
                p.fillColor = new Color(1f, 1f, 1f, 0.28f);
                p.Fill();
            }
            p.strokeColor = SkylineHudStyle.Ink;
            p.lineWidth = 2f;
            for (int q = 1; q < 4; q++)
            {
                float tx = ox + BarW * q * 0.25f;
                p.BeginPath();
                p.MoveTo(new Vector2(tx, oy + BarH));
                p.LineTo(new Vector2(tx + Skew, oy));
                p.Stroke();
            }
            SkylineHudStyle.Parallelogram(p, ox, oy, BarW, BarH, Skew);
            p.strokeColor = SkylineHudStyle.Ink;
            p.lineWidth = SkylineHudStyle.KeylinePx;
            p.lineJoin = LineJoin.Miter;
            p.Stroke();

            // The plating: a brighter, slanted slab over the chassis, offset up and left; its own dark track and keyline.
            if (_hasPlating && pf > 0f)
            {
                float px = ox + PlateDx, py = oy + PlateDy;
                SkylineHudStyle.Parallelogram(p, px, py, BarW, PlateH, PlateSkew);
                p.fillColor = new Color(0.086f, 0.078f, 0.122f, 0.65f);
                p.Fill();
                SkylineHudStyle.Parallelogram(p, px, py, BarW * pf, PlateH, PlateSkew);
                p.fillColor = _flash > 0f && Mathf.Repeat(_flash, 0.14f) > 0.07f ? Color.white : SkylineHudStyle.Plating;
                p.Fill();
                SkylineHudStyle.Parallelogram(p, px + PlateSkew * (1f - 3f / PlateH), py, BarW * pf, 3f, PlateSkew * 3f / PlateH);
                p.fillColor = new Color(1f, 1f, 1f, 0.45f);
                p.Fill();
                SkylineHudStyle.Parallelogram(p, px, py, BarW, PlateH, PlateSkew);
                p.strokeColor = SkylineHudStyle.Ink;
                p.lineWidth = SkylineHudStyle.KeylinePx;
                p.lineJoin = LineJoin.Miter;
                p.Stroke();
            }

            // The plating breaking: eight shards fall off the slab.
            if (_shatterT >= 0f)
            {
                float t01 = Mathf.Clamp01(_shatterT / ShatterSeconds);
                for (int i = 0; i < ShardCount; i++)
                {
                    var c = Shard(i, t01, out float alpha);
                    p.BeginPath();
                    p.MoveTo(c[0]);
                    p.LineTo(c[1]);
                    p.LineTo(c[2]);
                    p.LineTo(c[3]);
                    p.ClosePath();
                    var fill = SkylineHudStyle.Plating;
                    fill.a = alpha;
                    var ink = SkylineHudStyle.Ink;
                    ink.a = alpha;
                    p.fillColor = fill;
                    p.Fill();
                    p.strokeColor = ink;
                    p.lineWidth = 2f;
                    p.lineJoin = LineJoin.Miter;
                    p.Stroke();
                }
            }
        }
    }
}
