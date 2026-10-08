using System.Collections.Generic;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The template's HUD, added to the shell's HUD (so it hides with it): lives and bombs as pips,
    /// coins, graze, the floor map, owned upgrades, the boss bar and the boss name card. The shell's
    /// own score line (top right) shows the score. Styles: Templates/BulletHell/UI/BulletHell.uss
    /// (it uses the shell's theme variables). Labels only change when their value does.
    /// </summary>
    public sealed class BulletHellHud
    {
        public readonly VisualElement Root;
        readonly VisualElement _lives, _bombs, _map, _upgrades, _boss, _bossFill, _card, _bossTicks;
        readonly Label _coins, _graze, _floor, _floorName, _bossName, _cardTitle, _cardSub, _hint;
        readonly VisualElement _focusHint;
        float _cardLeft;
        float _shownLives = -1f, _shownMaxLives = -1f;
        int _shownBombs = -1, _shownCoins = -1, _shownGraze = -1;
        float _shownBoss = -1f, _shownFocusHint = -1f;

        /// <summary>The small control legend (bottom left), unless GameConfig.showControlLegend is off.</summary>
        public const string LegendText =
            "Move: WASD / arrows / stick\nHold Shift / LB: slow + precise\nFire: automatic\nBomb: K, right click, Space / A\nPause: Esc / Start";

        public bool BossBarShown => _boss.style.display == DisplayStyle.Flex;
        public bool NameCardShown => _cardLeft > 0f;
        public string CoinsText => _coins.text;
        public bool LegendShown => _hint.style.display != DisplayStyle.None;
        public bool FocusHintShown => _focusHint.style.display == DisplayStyle.Flex;

        public BulletHellHud(VisualElement parent, StyleSheet style, bool showLegend = true)
        {
            Root = new VisualElement { name = "bh-hud", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("bh-hud");
            if (style != null) Root.styleSheets.Add(style);
            parent.Add(Root);

            var left = Panel("bh-side bh-side--left");
            Kicker(left, "LIVES");
            _lives = Row(left, "bh-pips");
            Kicker(left, "BOMBS");
            _bombs = Row(left, "bh-pips");
            Kicker(left, "COINS");
            _coins = Value(left);
            Kicker(left, "GRAZE");
            _graze = Value(left);
            _hint = new Label(LegendText) { name = "bh-legend", pickingMode = PickingMode.Ignore };
            _hint.AddToClassList("bh-hint");
            left.Add(_hint);
            _hint.style.display = showLegend ? DisplayStyle.Flex : DisplayStyle.None;

            // The first-run Focus hint: a key cap and a plain sentence, low over the playfield.
            _focusHint = new VisualElement { name = "bh-focus-hint", pickingMode = PickingMode.Ignore };
            _focusHint.AddToClassList("bh-focus-hint");
            var key = new Label("SHIFT") { pickingMode = PickingMode.Ignore };
            key.AddToClassList("bh-key");
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            var line = new Label("Hold for slow, precise movement") { pickingMode = PickingMode.Ignore };
            line.AddToClassList("bh-focus-hint-text");
            var sub = new Label("It shows your hitbox. It does not speed you up. (Gamepad: LB)") { pickingMode = PickingMode.Ignore };
            sub.AddToClassList("bh-focus-hint-sub");
            words.Add(line);
            words.Add(sub);
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("bh-focus-hint-pill");
            pill.Add(key);
            pill.Add(words);
            _focusHint.Add(pill);
            Root.Add(_focusHint);
            _focusHint.style.display = DisplayStyle.None;

            var right = Panel("bh-side bh-side--right");
            _floor = Value(right);
            _floor.AddToClassList("bh-floor");
            _floorName = new Label { pickingMode = PickingMode.Ignore };
            _floorName.AddToClassList("bh-floor-name");
            right.Add(_floorName);
            _map = Row(right, "bh-map");
            Kicker(right, "UPGRADES");
            _upgrades = Row(right, "bh-upgrades");

            _boss = new VisualElement { pickingMode = PickingMode.Ignore };
            _boss.AddToClassList("bh-boss");
            _bossName = new Label { pickingMode = PickingMode.Ignore };
            _bossName.AddToClassList("bh-boss-name");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("bh-boss-bar");
            _bossFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _bossFill.AddToClassList("bh-boss-fill");
            _bossTicks = new VisualElement { pickingMode = PickingMode.Ignore };
            _bossTicks.AddToClassList("bh-boss-ticks");
            bar.Add(_bossFill);
            bar.Add(_bossTicks);
            _boss.Add(_bossName);
            _boss.Add(bar);
            Root.Add(_boss);
            _boss.style.display = DisplayStyle.None;

            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.AddToClassList("bh-namecard");
            _cardTitle = new Label { pickingMode = PickingMode.Ignore };
            _cardTitle.AddToClassList("bh-namecard-title");
            _cardSub = new Label { pickingMode = PickingMode.Ignore };
            _cardSub.AddToClassList("bh-namecard-sub");
            _card.Add(_cardTitle);
            _card.Add(_cardSub);
            Root.Add(_card);
            _card.style.display = DisplayStyle.None;
        }

        VisualElement Panel(string classes)
        {
            var p = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) p.AddToClassList(c);
            Root.Add(p);
            return p;
        }

        static void Kicker(VisualElement parent, string text)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("bh-kicker");
            parent.Add(l);
        }

        static Label Value(VisualElement parent)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("bh-value");
            parent.Add(l);
            return l;
        }

        static VisualElement Row(VisualElement parent, string cls)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.AddToClassList(cls);
            parent.Add(r);
            return r;
        }

        public void Remove() => Root.RemoveFromHierarchy();

        // ---------- Values ----------

        public void SetLives(float current, float max)
        {
            if (Mathf.Approximately(current, _shownLives) && Mathf.Approximately(max, _shownMaxLives)) return;
            _shownLives = current;
            _shownMaxLives = max;
            _lives.Clear();
            int slots = Mathf.CeilToInt(max);
            if (slots > 10)
            {
                var l = new Label(Mathf.CeilToInt(current) + " / " + slots) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("bh-value");
                _lives.Add(l);
                return;
            }
            for (int i = 0; i < slots; i++)
            {
                float fill = Mathf.Clamp01(current - i);
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("bh-pip");
                pip.AddToClassList("bh-pip--life");
                if (fill <= 0f) pip.AddToClassList("bh-pip--empty");
                else if (fill < 1f) pip.AddToClassList("bh-pip--half");
                _lives.Add(pip);
            }
        }

        public void SetBombs(int bombs, int max)
        {
            if (bombs == _shownBombs) return;
            _shownBombs = bombs;
            _bombs.Clear();
            for (int i = 0; i < Mathf.Max(bombs, 0); i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("bh-pip");
                pip.AddToClassList("bh-pip--bomb");
                _bombs.Add(pip);
            }
            if (bombs <= 0)
            {
                var none = new Label("none") { pickingMode = PickingMode.Ignore };
                none.AddToClassList("bh-dim");
                _bombs.Add(none);
            }
        }

        public void SetCoins(int coins)
        {
            if (coins == _shownCoins) return;
            _shownCoins = coins;
            _coins.text = coins.ToString();
        }

        public void SetGraze(int graze)
        {
            if (graze == _shownGraze) return;
            _shownGraze = graze;
            _graze.text = graze.ToString();
        }

        /// <summary>The floor's rooms as a row of letters: done, current (outlined), to come; forks stacked.</summary>
        public void SetMap(RunState run, BulletHellConfig config)
        {
            _map.Clear();
            if (run == null || run.Plan == null) return;
            int floor = Mathf.Min(run.floor, run.Plan.floors.Count - 1);
            var f = config.Floor(floor);
            _floor.text = "FLOOR " + (floor + 1) + " / " + run.Plan.floors.Count;
            _floorName.text = f != null ? f.name : "";
            var steps = run.Plan.floors[floor].steps;
            for (int s = 0; s < steps.Count; s++)
            {
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.AddToClassList("bh-map-step");
                var options = steps[s].options;
                for (int o = 0; o < options.Count; o++)
                {
                    var t = options[o];
                    var cell = new Label(RoomText.Letter(t)) { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList("bh-map-cell");
                    if (options.Count > 1) cell.AddToClassList("bh-map-cell--fork");
                    var c = config.RoomColor(t);
                    cell.style.backgroundColor = new Color(c.r, c.g, c.b, 0.85f);
                    bool chosen = s < run.step ? ChoiceAt(run, floor, s) == o : s == run.step && run.choice == o;
                    if (s < run.step) cell.AddToClassList(chosen ? "bh-map-cell--done" : "bh-map-cell--skipped");
                    else if (s == run.step && chosen) cell.AddToClassList("bh-map-cell--here");
                    col.Add(cell);
                }
                _map.Add(col);
            }
        }

        /// <summary>The door taken at a past step of this floor (from RunState.choices).</summary>
        static int ChoiceAt(RunState run, int floor, int step)
        {
            if (step == 0) return 0;
            // choices has one entry per Advance: steps 1.. of floor 0, then 0.. of later floors.
            int index = -1;
            for (int f = 0; f < floor; f++) index += run.Plan.floors[f].steps.Count;
            index += step;
            return index >= 0 && index < run.choices.Count ? run.choices[index] : 0;
        }

        public void SetUpgrades(IReadOnlyList<UpgradeDef> owned, UpgradeInventory inventory, System.Func<string, UpgradeAsset> lookup, BulletHellConfig config)
        {
            _upgrades.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                var def = owned[i];
                var asset = lookup(def.id);
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("bh-upgrade");
                var rc = config.RarityColor(def.rarity);
                icon.style.borderTopColor = rc;
                icon.style.borderBottomColor = rc;
                icon.style.borderLeftColor = rc;
                icon.style.borderRightColor = rc;
                if (asset != null && asset.icon != null) icon.style.backgroundImage = new StyleBackground(asset.icon);
                else
                {
                    var c = asset != null ? asset.iconColor : rc;
                    icon.style.backgroundColor = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.95f);
                    var letters = new Label(asset != null ? asset.iconText : def.id.Substring(0, Mathf.Min(2, def.id.Length))) { pickingMode = PickingMode.Ignore };
                    letters.AddToClassList("bh-upgrade-text");
                    icon.Add(letters);
                }
                int stacks = inventory.Count(def.id);
                if (stacks > 1)
                {
                    var n = new Label("x" + stacks) { pickingMode = PickingMode.Ignore };
                    n.AddToClassList("bh-upgrade-count");
                    icon.Add(n);
                }
                icon.tooltip = def.displayName;
                _upgrades.Add(icon);
            }
        }

        // ---------- Boss ----------

        public void ShowBoss(string name, IReadOnlyList<BossPhase> phases)
        {
            _boss.style.display = DisplayStyle.Flex;
            _bossName.text = name;
            _shownBoss = -1f;
            _bossTicks.Clear();
            if (phases != null)
                for (int i = 1; i < phases.Count; i++)
                {
                    var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                    tick.AddToClassList("bh-boss-tick");
                    tick.style.left = Length.Percent(Mathf.Clamp01(phases[i].startsAtHealth) * 100f);
                    _bossTicks.Add(tick);
                }
            SetBossHealth(1f);
        }

        public void SetBossHealth(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - _shownBoss) < 0.002f) return;
            _shownBoss = fraction;
            _bossFill.style.width = Length.Percent(fraction * 100f);
        }

        public void HideBoss() => _boss.style.display = DisplayStyle.None;

        /// <summary>A big centred title that fades out ("GATEKEEPER / Warden of the first floor").</summary>
        public void NameCard(string title, string subtitle, float seconds = 2.6f)
        {
            _cardTitle.text = title;
            _cardSub.text = subtitle;
            _cardSub.style.display = string.IsNullOrEmpty(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
            _card.style.display = DisplayStyle.Flex;
            _card.style.opacity = 1f;
            _cardLeft = seconds;
        }

        public void Tick(float unscaledDt)
        {
            if (_cardLeft <= 0f) return;
            _cardLeft -= unscaledDt;
            _card.style.opacity = Mathf.Clamp01(_cardLeft / 0.5f);
            if (_cardLeft <= 0f) _card.style.display = DisplayStyle.None;
        }

        /// <summary>The first-run Focus hint at this opacity (0 hides it). Only touches styles when it changes.</summary>
        public void SetFocusHint(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (Mathf.Abs(alpha - _shownFocusHint) < 0.01f && (alpha > 0f) == FocusHintShown) return;
            _shownFocusHint = alpha;
            _focusHint.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            _focusHint.style.opacity = alpha;
        }
    }
}
