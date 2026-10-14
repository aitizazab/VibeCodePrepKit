using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The ammo counter (docs/SKYLINE-HUD.md): the magazine count in big display-font digits (96 px at the 1920 x 1080
    /// reference) with an ink keyline, the gun's slanted silhouette icon beside them (about 64 px), a small reserve (36 px) for
    /// special weapons only (the Writ shows none), and the gun's name in a row of its own UNDER the digits (a flow layout, so
    /// the name can never overlap the digits and stays inside the safe area). The face is
    /// <see cref="SkylineHudStyle.AmmoFontName"/>, one swappable setting. <see cref="Flash"/> tints the numbers for a moment
    /// (A2/H5 use it for the green special-ammo flash).
    /// </summary>
    public sealed class SkylineHudAmmo
    {
        public const float DigitPx = 96f, ReservePx = 36f, NamePx = 22f, IconW = 72f, IconH = 44f;
        public const float FlashSeconds = 0.4f;

        VisualElement _root, _icon;
        KeylinedLabel _digits, _reserve, _name;
        SkylinePlayer _player;
        string _iconGun;
        int _lastAmmo = -2, _lastReserve = -2, _lastMag = -1;
        bool _lastReloading, _lastSpecial;
        string _lastName;
        float _flash;
        Color _flashColour = SkylineHudStyle.Green;

        public VisualElement Element => _root;
        /// <summary>The magazine digits' element (the pass checks it is a slanted display-font text, not a box).</summary>
        public KeylinedLabel Digits => _digits;
        public KeylinedLabel Reserve => _reserve;
        public KeylinedLabel GunName => _name;

        /// <summary>Adds the counter to <paramref name="parent"/>, bottom right, <paramref name="margin"/> from the edges.</summary>
        public VisualElement Build(VisualElement parent, float margin)
        {
            string face = SkylineHudStyle.AmmoFontName;
            _root = SkylineHudStyle.Layer(parent, "hud-ammo");
            _root.style.right = margin;
            _root.style.bottom = margin;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.alignItems = Align.FlexEnd;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            _root.Add(row);

            _icon = new VisualElement { pickingMode = PickingMode.Ignore };
            _icon.style.width = IconW;
            _icon.style.height = IconH;
            _icon.style.marginRight = 14f;
            _icon.style.marginBottom = 14f;
            _icon.generateVisualContent += DrawIcon;
            row.Add(_icon);

            _digits = new KeylinedLabel(DigitPx, SkylineHudStyle.KeylinePx, face);
            row.Add(_digits);
            _reserve = new KeylinedLabel(ReservePx, 2f, face);
            _reserve.style.marginLeft = 8f;
            _reserve.style.marginBottom = 10f;
            _reserve.style.display = DisplayStyle.None;
            row.Add(_reserve);

            _name = new KeylinedLabel(NamePx, 2f, face);
            _name.style.marginTop = 2f;
            _name.Front.style.letterSpacing = 3f;
            _root.Add(_name);
            return _root;
        }

        public void Bind(SkylinePlayer player) => _player = player;

        /// <summary>The numbers flash <paramref name="colour"/> for <see cref="FlashSeconds"/> and ease back.</summary>
        public void Flash(Color colour)
        {
            _flash = FlashSeconds;
            _flashColour = colour;
        }

        /// <summary>Per frame (unscaled seconds).</summary>
        public void Tick(float dt)
        {
            if (_root == null) return;
            var lo = _player != null ? _player.Loadout : null;
            if (lo == null) return;
            var g = lo.Active;
            if (g == null) return;
            int mag = g.Def.weapon.magazineSize, ammo = g.Ammo;
            bool reloading = g.State.IsReloading, special = g.Def.special;
            int reserve = g.State.Reserve;

            if (_iconGun != g.Def.id)
            {
                _iconGun = g.Def.id;
                _icon.MarkDirtyRepaint();
            }
            if (ammo != _lastAmmo || mag != _lastMag)
            {
                _lastAmmo = ammo;
                _lastMag = mag;
                _digits.SetText(ammo.ToString());
            }
            if (special != _lastSpecial || reserve != _lastReserve)
            {
                _lastSpecial = special;
                _lastReserve = reserve;
                _reserve.style.display = special ? DisplayStyle.Flex : DisplayStyle.None;
                if (special) _reserve.SetText("/" + reserve);
            }
            string name = g.Def.displayName.ToUpperInvariant();
            if (reloading != _lastReloading || name != _lastName)
            {
                _lastReloading = reloading;
                _lastName = name;
                _name.SetText(reloading ? name + " - RELOADING" : name);
            }

            // Colours: warm white; orange when the magazine is nearly empty; dimmed while reloading; a flash on top.
            Color body = SkylineHudStyle.Paper;
            if (mag > 0 && ammo * 4 <= mag) body = SkylineHudStyle.Chassis;
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt);
                body = Color.Lerp(body, _flashColour, _flash / FlashSeconds);
            }
            _digits.SetColor(body);
            _digits.style.opacity = reloading ? 0.5f : 1f;
            Color res = special && reserve <= 0 ? SkylineHudStyle.Chassis : body;
            _reserve.SetColor(res);
            _icon.style.opacity = reloading ? 0.5f : 1f;
        }

        void DrawIcon(MeshGenerationContext ctx)
        {
            SkylineHudIcons.DrawGun(ctx.painter2D, _iconGun, new Rect(3f, 3f, IconW - 6f, IconH - 6f),
                SkylineHudStyle.Paper, SkylineHudStyle.Ink, SkylineHudStyle.KeylinePx);
        }
    }
}
