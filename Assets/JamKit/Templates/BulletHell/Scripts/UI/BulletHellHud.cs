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
        readonly VisualElement _sprintHint, _sprintMeter, _sprintTank, _sprintFill;
        readonly Label _sprintText;
        readonly VisualElement _guardKicker, _guard, _guardFill, _trials, _bark;
        readonly Label _bombRecharge, _trialsKicker, _barkSpeaker, _barkText, _abilities;
        float _cardLeft, _sprintFlashLeft, _barkLeft, _shownGuard = -1f, _shownRecharge = -1f;
        int _shownGuardReady = -1, _shownMaxBombs = -1;
        string _shownTrials;
        float _shownLives = -1f, _shownMaxLives = -1f;
        int _shownBombs = -1, _shownCoins = -1, _shownGraze = -1;
        float _shownBoss = -1f, _shownSprintHint = -1f, _shownTank = -1f, _shownFill = -1f, _shownRoomSeconds = -1f;
        int _shownSprintState = -1;

        /// <summary>The small control legend (bottom left), unless GameConfig.showControlLegend is off.</summary>
        public const string LegendText =
            "Move: WASD / arrows / stick\nSprint: hold Shift / LB\nFire: automatic\nBomb: K, right click, Space / A\nPause: Esc / Start";

        public bool BossBarShown => _boss.style.display == DisplayStyle.Flex;
        public bool NameCardShown => _cardLeft > 0f;
        public string CoinsText => _coins.text;
        public bool LegendShown => _hint.style.display != DisplayStyle.None;
        public bool SprintHintShown => _sprintHint.style.display == DisplayStyle.Flex;
        /// <summary>The meter's widths as fractions of its track: the room's tank, and the stamina inside it.</summary>
        public float SprintTankShown => _shownTank;
        public float SprintFillShown => _shownFill;
        public bool SprintFlashing => _sprintFlashLeft > 0f;
        /// <summary>The companion's guard meter is on screen.</summary>
        public bool GuardShown => _guard.style.display == DisplayStyle.Flex;
        public float GuardShownFraction => _shownGuard;
        /// <summary>The bark on screen ("" when none).</summary>
        public string BarkText => _barkLeft > 0f ? _barkText.text : "";
        /// <summary>The ability line under the SPRINT meter (Ghost Step's phases, Guardian Angel's cooldown).</summary>
        public string AbilitiesText => _abilities.text;
        /// <summary>The trial lines as shown (one per line).</summary>
        public string TrialsText => _shownTrials ?? "";

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
            _bombRecharge = new Label { pickingMode = PickingMode.Ignore };
            _bombRecharge.AddToClassList("bh-sprint-text");
            left.Add(_bombRecharge);
            _bombRecharge.style.display = DisplayStyle.None;
            Kicker(left, "COINS");
            _coins = Value(left);
            Kicker(left, "GRAZE");
            _graze = Value(left);

            // Sprint: one meter, two layers. The dim layer is this room's sprint budget (it only
            // shrinks until the next fight room); the bright fill is the stamina bar inside it.
            Kicker(left, "SPRINT");
            _sprintMeter = new VisualElement { name = "bh-sprint", pickingMode = PickingMode.Ignore };
            _sprintMeter.AddToClassList("bh-sprint");
            _sprintTank = new VisualElement { pickingMode = PickingMode.Ignore };
            _sprintTank.AddToClassList("bh-sprint-tank");
            _sprintFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _sprintFill.AddToClassList("bh-sprint-fill");
            _sprintMeter.Add(_sprintTank);
            _sprintMeter.Add(_sprintFill);
            left.Add(_sprintMeter);
            _sprintText = new Label { pickingMode = PickingMode.Ignore };
            _sprintText.AddToClassList("bh-sprint-text");
            left.Add(_sprintText);
            _abilities = new Label { name = "bh-abilities", pickingMode = PickingMode.Ignore };
            _abilities.AddToClassList("bh-sprint-text");
            left.Add(_abilities);
            _abilities.style.display = DisplayStyle.None;

            // The companion's guard: full and bright = it will take the next bullet for you.
            _guardKicker = new Label("GUARD") { pickingMode = PickingMode.Ignore };
            _guardKicker.AddToClassList("bh-kicker");
            left.Add(_guardKicker);
            _guard = new VisualElement { name = "bh-guard", pickingMode = PickingMode.Ignore };
            _guard.AddToClassList("bh-guard");
            _guardFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _guardFill.AddToClassList("bh-guard-fill");
            _guard.Add(_guardFill);
            left.Add(_guard);
            _guardKicker.style.display = DisplayStyle.None;
            _guard.style.display = DisplayStyle.None;

            _hint = new Label(LegendText) { name = "bh-legend", pickingMode = PickingMode.Ignore };
            _hint.AddToClassList("bh-hint");
            left.Add(_hint);
            _hint.style.display = showLegend ? DisplayStyle.Flex : DisplayStyle.None;

            // The first-run Sprint hint: a key cap and a plain sentence, low over the playfield.
            _sprintHint = new VisualElement { name = "bh-sprint-hint", pickingMode = PickingMode.Ignore };
            _sprintHint.AddToClassList("bh-focus-hint");
            var key = new Label("SHIFT") { pickingMode = PickingMode.Ignore };
            key.AddToClassList("bh-key");
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            var line = new Label("Hold Shift to sprint") { pickingMode = PickingMode.Ignore };
            line.AddToClassList("bh-focus-hint-text");
            var sub = new Label("Limited stamina per room: watch the SPRINT bar on the left. (Gamepad: LB)") { pickingMode = PickingMode.Ignore };
            sub.AddToClassList("bh-focus-hint-sub");
            words.Add(line);
            words.Add(sub);
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("bh-focus-hint-pill");
            pill.Add(key);
            pill.Add(words);
            _sprintHint.Add(pill);
            Root.Add(_sprintHint);
            _sprintHint.style.display = DisplayStyle.None;

            var right = Panel("bh-side bh-side--right");
            _floor = Value(right);
            _floor.AddToClassList("bh-floor");
            _floorName = new Label { pickingMode = PickingMode.Ignore };
            _floorName.AddToClassList("bh-floor-name");
            right.Add(_floorName);
            _map = Row(right, "bh-map");
            Kicker(right, "UPGRADES");
            _upgrades = Row(right, "bh-upgrades");
            _trialsKicker = new Label("TRIALS") { pickingMode = PickingMode.Ignore };
            _trialsKicker.AddToClassList("bh-kicker");
            right.Add(_trialsKicker);
            _trials = Row(right, "bh-trials");
            _trials.style.flexDirection = FlexDirection.Column;
            _trialsKicker.style.display = DisplayStyle.None;

            // A companion bark: a short line low over the playfield that doesn't stop the game.
            _bark = new VisualElement { name = "bh-bark", pickingMode = PickingMode.Ignore };
            _bark.AddToClassList("bh-bark");
            var barkPill = new VisualElement { pickingMode = PickingMode.Ignore };
            barkPill.AddToClassList("bh-bark-pill");
            _barkSpeaker = new Label { pickingMode = PickingMode.Ignore };
            _barkSpeaker.AddToClassList("bh-bark-speaker");
            _barkText = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
            _barkText.AddToClassList("bh-bark-text");
            barkPill.Add(_barkSpeaker);
            barkPill.Add(_barkText);
            _bark.Add(barkPill);
            Root.Add(_bark);
            _bark.style.display = DisplayStyle.None;

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

        /// <summary>Bomb pips; <paramref name="recharge"/> (0..1) is the Bomb Bay's progress to the next one.</summary>
        public void SetBombs(int bombs, int max, float recharge = 0f)
        {
            float r = Mathf.Floor(Mathf.Clamp01(recharge) * 20f) / 20f;
            if (!Mathf.Approximately(r, _shownRecharge) || max != _shownMaxBombs)
            {
                _shownRecharge = r;
                bool show = max > 0 && bombs < max;
                _bombRecharge.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) _bombRecharge.text = "Next bomb " + Mathf.RoundToInt(r * 100f) + "%";
            }
            if (bombs == _shownBombs && max == _shownMaxBombs) return;
            _shownBombs = bombs;
            _shownMaxBombs = max;
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
                var none = new Label(max <= 0 ? "none (Bomb Bay)" : "recharging") { pickingMode = PickingMode.Ignore };
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
                int forged = inventory.BonusLevels(def.id);
                if (forged > 0)
                {
                    var f = new Label("+" + forged) { pickingMode = PickingMode.Ignore };
                    f.AddToClassList("bh-upgrade-forged");
                    icon.Add(f);
                }
                if (Remnants.IsRemnant(def)) icon.AddToClassList("bh-upgrade--remnant");
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

        /// <summary>The ability line (empty hides it). The game only calls this when the text changes.</summary>
        public void SetAbilities(string text)
        {
            _abilities.text = text ?? "";
            _abilities.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The companion's guard meter (hidden when there's no companion).</summary>
        public void SetGuard(bool shown, float fraction, bool ready)
        {
            var d = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (_guard.style.display != d)
            {
                _guard.style.display = d;
                _guardKicker.style.display = d;
            }
            if (!shown) return;
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - _shownGuard) >= 0.01f)
            {
                _shownGuard = fraction;
                _guardFill.style.width = Length.Percent(fraction * 100f);
            }
            int r = ready ? 1 : 0;
            if (r == _shownGuardReady) return;
            _shownGuardReady = r;
            _guard.EnableInClassList("bh-guard--ready", ready);
        }

        /// <summary>The active trials, one line each ("Untouched: 1 fight left"). Rebuilt only when the text changes.</summary>
        public void SetTrials(IReadOnlyList<TrialTracker> trials)
        {
            string text = "";
            if (trials != null)
                for (int i = 0; i < trials.Count; i++) text += (i > 0 ? "\n" : "") + trials[i].Status();
            if (text == _shownTrials) return;
            _shownTrials = text;
            _trials.Clear();
            var d = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _trialsKicker.style.display = d;
            if (trials == null) return;
            for (int i = 0; i < trials.Count; i++)
            {
                var l = new Label(trials[i].Status()) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("bh-trial-line");
                if (trials[i].State == TrialState.Passed) l.AddToClassList("bh-trial-line--done");
                else if (trials[i].State == TrialState.Failed) l.AddToClassList("bh-trial-line--failed");
                _trials.Add(l);
            }
        }

        /// <summary>A short companion line low over the playfield, for <paramref name="seconds"/> (the game doesn't stop).</summary>
        public void Bark(string speaker, string text, float seconds)
        {
            _barkSpeaker.text = speaker ?? "";
            _barkSpeaker.style.display = string.IsNullOrEmpty(speaker) ? DisplayStyle.None : DisplayStyle.Flex;
            _barkText.text = text ?? "";
            _barkLeft = Mathf.Max(0.5f, seconds);
            _bark.style.display = DisplayStyle.Flex;
            _bark.style.opacity = 1f;
        }

        public void HideBark()
        {
            _barkLeft = 0f;
            _bark.style.display = DisplayStyle.None;
        }

        public void Tick(float unscaledDt)
        {
            if (_barkLeft > 0f)
            {
                _barkLeft -= unscaledDt;
                _bark.style.opacity = Mathf.Clamp01(_barkLeft / 0.4f);
                if (_barkLeft <= 0f) _bark.style.display = DisplayStyle.None;
            }
            if (_sprintFlashLeft > 0f)
            {
                _sprintFlashLeft -= unscaledDt;
                if (_sprintFlashLeft <= 0f) _sprintMeter.RemoveFromClassList("bh-sprint--flash");
            }
            if (_cardLeft <= 0f) return;
            _cardLeft -= unscaledDt;
            _card.style.opacity = Mathf.Clamp01(_cardLeft / 0.5f);
            if (_cardLeft <= 0f) _card.style.display = DisplayStyle.None;
        }

        /// <summary>The first-run Sprint hint at this opacity (0 hides it). Only touches styles when it changes.</summary>
        public void SetSprintHint(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (Mathf.Abs(alpha - _shownSprintHint) < 0.01f && (alpha > 0f) == SprintHintShown) return;
            _shownSprintHint = alpha;
            _sprintHint.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            _sprintHint.style.opacity = alpha;
        }

        /// <summary>The sprint meter: the tank is the room's remaining budget (of the room's full budget),
        /// the fill is the stamina bar inside it. The line under it says what's left. Styles only change
        /// when a value moves visibly.</summary>
        public void SetSprint(SprintMeter m)
        {
            if (m == null) return;
            float tank = m.BudgetFraction;
            float fill = tank * m.StaminaFraction;
            if (Mathf.Abs(tank - _shownTank) >= 0.004f)
            {
                _shownTank = tank;
                _sprintTank.style.width = Length.Percent(tank * 100f);
            }
            if (Mathf.Abs(fill - _shownFill) >= 0.004f || (fill <= 0f) != (_shownFill <= 0f))
            {
                _shownFill = fill;
                _sprintFill.style.width = Length.Percent(fill * 100f);
            }
            int state = m.BudgetEmpty ? 2 : m.Exhausted ? 1 : m.IsSprinting ? 3 : 0;
            float seconds = Mathf.Ceil(m.RoomBudget * 10f) / 10f;
            if (state == _shownSprintState && Mathf.Approximately(seconds, _shownRoomSeconds)) return;
            _shownSprintState = state;
            _shownRoomSeconds = seconds;
            _sprintMeter.EnableInClassList("bh-sprint--tired", state == 1);
            _sprintMeter.EnableInClassList("bh-sprint--empty", state == 2);
            _sprintMeter.EnableInClassList("bh-sprint--active", state == 3);
            _sprintText.text = state == 2 ? "None left this room"
                : state == 1 ? "Catching breath..."
                : seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s left this room";
        }

        /// <summary>The meter flashes (stamina or the room's budget just ran out).</summary>
        public void FlashSprint(bool roomEmpty)
        {
            _sprintFlashLeft = roomEmpty ? 0.6f : 0.35f;
            _sprintMeter.AddToClassList("bh-sprint--flash");
        }
    }
}
