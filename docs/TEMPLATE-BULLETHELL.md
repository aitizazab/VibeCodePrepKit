# Bullet-hell roguelite template: how to change it without code

A vertical shoot-'em-up run: **3 floors**, each a row of 5-6 rooms ending in a **boss**. Battle
rooms play waves of enemies, then the player picks **1 of 3 upgrades** (commons and rares); a boss
gives a **choice of 3 epics**. Between battles: doors, shops, rests, strangers (ink dialogues) and the
**encounter rooms** where the player chooses a risk: the Forge, the Altar, a Wager, a Trial board and the
Trader. A companion fights beside the ship, and a dead run leaves a **remnant** for the next. Beat the
third boss to win. A full run takes about 10-15 minutes. The design rules behind all this are in
`docs/DESIGN-ROGUELITE.md`.

Everything you'd want to change is a **data asset** in the Inspector. Duplicate an asset
(Ctrl+D), change numbers, drag it into a list. No code.

## Switch to this template

- Menu **JamKit > Use Template > Bullet Hell**: Play on the title screen now starts the bullet hell
  (it copies the template's title, tagline and How to Play cards into the active GameConfig and puts
  the scene after Boot and Title in Build Settings). **JamKit > Use Template > Sandbox** goes back.
- The template's own title, tagline and How to Play cards are in
  `Assets/JamKit/Templates/BulletHell/BulletHellGameConfig.asset`. Edit them there, then use the menu
  again (or edit the active GameConfig directly).
- To test the scene alone, open `Templates/BulletHell/Scenes/BulletHell.unity` and press Play.

## Where things are

| What | Where (under `Assets/JamKit/Templates/BulletHell/`) |
|---|---|
| The one config (floors, run shape, prices, palette) | `Data/BulletHellConfig.asset` |
| The ship (speed, lives, bombs, gun) | `Data/Player.asset` |
| Enemies | `Data/Enemies/*.asset` |
| Waves (who comes when, from where) | `Data/Waves/*.asset` |
| Rooms (a list of waves) | `Data/Rooms/*.asset` |
| Bosses (phases) | `Data/Bosses/*.asset` |
| Upgrades | `Data/Upgrades/*.asset` |
| The companion | `Data/Companion.asset` |
| Reward tables, trials, wagers, forge/altar/trader rules, remnants | `Data/BulletHellConfig.asset` |
| Mystery-room dialogue, story hooks, encounter lines, companion barks | `Dialogue/BulletHellEvents.ink` |
| HUD and card layout | `UI/BulletHell.uss` (colours come from `Assets/JamKit/UI/Theme.uss`) |
| Placeholder art | `Art/Generated/*.png` |

After editing, run **JamKit > Bullet Hell > Validate Content**: it lists every problem in plain
words (an empty room pool, an enemy with 0 health, an upgrade with an unknown effect...). The
EditMode tests run the same checks.

**JamKit > Rebuild Shell Scenes** rebuilds the scene and art but **keeps your data**: it only creates
assets that are missing. **JamKit > Bullet Hell > Reset Content to Defaults** puts every data asset
back to the template's values (your changes to them are lost). One exception: when the template's
content changes (a *content revision*, stored in the config), the first rebuild after it rewrites the
assets that revision changed, once. Revision 2 (the roguelite round) rewrote the config, the player and
the upgrades, and deleted Rear Guard and Overdrive.

## Coordinates

The playfield is **12 wide and 10.6 tall**, centred on 0: x from **-6 (left) to 6 (right)**,
y from **-5.3 (bottom) to 5.3 (top)**. The player starts at (0, -3.6). Enemies usually sit at
y 2 to 4.5. Angles are degrees: **0 = right, 90 = up, -90 = down**.

## Add an enemy

1. Duplicate an enemy in `Data/Enemies` (Turret is a good start) and rename it.
2. **Hp**, **Sprite** and **Color** (sprites are white; the colour tints them), **Size** (drawn) and
   **Radius** (what bullets hit; keep it a bit smaller than the size).
3. **Movement > Kind**:
   - *Hover*: flies to its spot and stays. *Act Seconds* > 0 makes it fly away after that long.
   - *Strafe*: flies to its spot and slides left/right (*Amplitude* wide, *Frequency* sways per second).
   - *Dive*: flies in, waits *Act Seconds*, then charges at the player at *Speed*.
   - *Sweep*: crosses the screen sideways in a wavy line (spawn it from Left or Right).
   - *Drift*: drifts down the screen, swaying.
   *Enter Seconds* is how long the fly-in takes.
4. **Attack**: a list of pattern steps (next section). Leave it empty for an enemy that only rams.
   *Attack Delay* is the pause after it arrives.
5. **Score** and **Coins** it drops. Tick **Big** for a bigger explosion and a score popup.
6. Put it in a wave (below).

## Add or change a pattern (an attack)

An attack is a **Pattern Sequence**: **Steps** (each a time in seconds and a pattern), plus **Loop**
(repeat the whole thing) and **Loop Delay**. Each pattern:

| Field | Does |
|---|---|
| Shape | *Ring* (all around), *Spread* (a fan), *Spiral* (a ring that turns each volley), *Aimed* (a fan at the player), *Shotgun* (random cone), *Line* (a wall) |
| Count | bullets per volley |
| Spread Degrees / Line Width | how wide the fan or wall is |
| Base Angle Degrees | which way (-90 = down); with *Aim At Target*, an offset from the player's direction |
| Rotate Per Shot Degrees | each volley turns this much: spirals |
| Shots / Shot Interval | how many volleys, how far apart (Shots 0 = forever) |
| Speed, Speed Step, Acceleration | bullet speed; Speed Step makes each bullet of a volley faster (a stack) |
| Angular Velocity Degrees | bullets curve as they fly |
| Mirror | also fires the mirror image: spirals gain a counter-rotating twin |
| Style Index | which bullet look (see the palette below) |
| Lifetime | seconds before a bullet disappears (it also vanishes off-screen) |

Tips: slow, dense, readable patterns feel better than fast ones. Leave gaps. Big bullets (style 3, 8)
should be slow. Test with the debug menu's x0.25 time.

## Add a wave

1. Duplicate a wave in `Data/Waves`.
2. Each **Spawn**: *Time* (seconds into the wave), *Enemy*, *Position* (where it flies to; for Sweep
   and Drift, the line it travels on), *From* (Top, Left, Right, TopLeft, TopRight), *Count* (a group),
   *Spacing* (offset between the group's enemies, centred on Position), *Interval* (seconds between
   them) and *Mirror* (a copy on the other side).
3. **Max Duration**: after this many seconds the next wave comes even if enemies remain
   (0 = wait until it's cleared). The room ends when every wave is done and every enemy is gone.
4. Add the wave to a room's **Waves** list.

## Add a room

Duplicate a room in `Data/Rooms`, set **Type** (Combat or Elite), the **Waves** in order and the
**Bonus Coins** for clearing it. Then add it to a floor's **Combat Rooms** or **Elite Rooms** in
`BulletHellConfig > Floors`. Each Battle door picks one room from its floor's pool at random.

## Add an upgrade

1. Duplicate an upgrade in `Data/Upgrades`.
2. **Def**: a unique **Id** (lowercase, no spaces), **Display Name**, **Description** (one line,
   what it does), **Rarity** (Common, Rare, Epic, Legendary), **Max Stacks** (how many times it can be
   taken; 0 = unlimited) and **Tags** (`gun`, `move`, `bomb`, `defense`, `economy`, `risk`, `companion`:
   trials count them, e.g. "own 3 gun upgrades"; `companion` upgrades leave the pool when the
   companion is off).
3. What it does, either or both:
   - **Modifiers** change numbers. *Stat* is one of: `damage`, `fire_rate`, `move_speed`,
     `shot_speed`, `graze_radius`, `luck`, `magnet`, `coin_gain`, `bomb_power`, for the sprint
     `sprint_speed` (the sprint's extra speed), `stamina_max` (a longer bar), `stamina_regen` (faster
     recharge), `room_sprint_budget` (more sprint per room), `hitbox_size` (0.8 = a 20% smaller
     hitbox), for the Bomb Bay `bomb_recharge` (faster
     recharge), and for the companion `companion_damage` and `companion_shield_cooldown` (0.7 = its guard
     is back 30% sooner); all start at 1. *Op*: Flat (+value), PercentAdd (0.2 = +20%), Multiply (1.5 = x1.5).
   - **Effects** are special abilities, by id: `spread` (+2 side streams per stack), `pierce` (shots go
     through one more enemy per stack), `homing` (side streams curve to enemies), `rebound` (two
     streams fire down, bounce off the floor and climb back +60% hotter), `bomb_bay` (bombs: see below),
     `bomb_plus` (+1 bomb in the Bay), `bigger_bombs` (+50% per stack), `auto_bomb` (a hit fires a
     bomb instead, spending every stored bomb, then a cooldown), `ghost_step` (a sprint's first half
     second passes through bullets; a few per room), `graze_power` (grazes heat your guns: up to +60%
     damage), `last_stand` (Last Gambit: on your last life +75% damage, hits ricochet, kills blast),
     `still_point` (holding still ramps damage up to double), `scatter` / `blade` (the main guns become
     a short-range shotgun cone / an arc slash; one or the other),
     `focus_slow` (bullets near you move at half speed), `graze_heal` (grazes restore life), `life_up`
     (+1 life), `coins:15` (15 coins now). `rear_shot` (two streams backwards) still works but isn't
     shipped: the balance report flags it (players sit low, nothing gets behind them).
   - *Requires* / *Excludes*: ids it needs (an upgrade id or an effect id, e.g. `bomb_bay`), or can't be
     owned with. A required upgrade is only offered once its base is owned.
4. **Icon Text** (2-3 letters) and **Icon Color** for the HUD, or an **Icon** sprite.
5. **Forge**: what one Forge push adds (*Forge Modifiers*, *Forge Effects*, a few words in *Forge
   Text*). Left empty, a push adds half of the upgrade's own modifiers.
6. **Epics and legendaries** must fill the **Rubric** (docs/DESIGN-ROGUELITE.md, section 2): the *Verb* it
   changes (Move, Aim, Dodge, Risk), at least two *Situations* (`boss`, `waves`, `open`, `crowded`,
   `divers`, `sides`, `turrets`, `low_life`, `long_fights`), *Where Player Is* when it works, and the
   in-universe *Idea*; plus **Design Notes** (which situations it's for, what the balance report said).
   Validate Content and the EditMode tests hold every epic to it.
7. Add it to `BulletHellConfig > Upgrades`. The **Fallback Upgrade** (Pocket Change) is offered when
   nothing else can be. Then run the **Balance Report** (below).

### The shipped upgrades

| | Upgrade | Does | For |
|---|---|---|---|
| Common | Sharp Rounds, Rapid Trigger, Hot Barrels | damage, fire rate, shot speed and +12% damage | |
| Common | Light Frame, Wide Halo, Coin Magnet | a 20% smaller hitbox and faster stamina, graze ring, coins | |
| Common | Spare Bomb, Quick Fuse | +1 bomb in the Bay, Bay recharges 70% faster (need the Bomb Bay) | |
| Rare | Twin Spread, Piercing Shot, Backdraft | +2 side streams; pierce +1 and +8% damage; two streams bounce up off the floor, hotter | |
| Rare | Big Bang (needs the Bay), Lucky Charm, Close Shave, Extra Life | bigger bombs, luck, grazes heal, +1 life | |
| Rare | Kindred Sting, Steady Wings | companion damage +60%, companion guard back 30% sooner | |
| **Epic** | **Seeker Rounds** | every third volley, slow seekers home in | aim: dodge off the line and still land some damage |
| **Epic** | **Bomb Bay** | 2 bombs that recharge every 20 s of fighting | risk: a panic button for bosses and crowds |
| **Epic** | **Guardian Angel** (needs the Bay) | a hit fires every stored bomb instead; 10 s to recover | risk: insurance, not invulnerability |
| **Epic** | **Ghost Step** | a sprint's first 0.5 s passes through bullets; 3 a room, 5 s apart | dodge: dash through a wall, at the right moment |
| **Epic** | **Ember Hunger** | grazes heat your guns, up to +60% | risk: stay close to the bullets |
| **Epic** | **Still Point** | holding still ramps damage to double in 0.6 s; any move resets it | move: bursts between gaps, then plant and burn |
| **Epic** | **Scatter** | the guns become a shotgun cone, about 3x up close, half at 5 units | risk: get close (the opposite of sitting low) |
| **Epic** | **Blade** | the guns become an arc slash 1.7 units in front, 3.5x the damage | risk: closest range, most hits taken |
| **Legendary** | **Time Dilation** | bullets near you move at half speed | dodge: dense boss patterns |
| **Legendary** | **Last Gambit** | on your last life: +75% damage, hits ricochet, kills blast (the ship pulses red) | risk: pairs with the Altar's life offering |

The numbers behind them are data: `BulletHellConfig > Boosts` (Ember Hunger, Last Gambit, Still Point,
Ghost Step, Guardian Angel, Time Dilation) and `> Shots` (side streams, Seeker's cadence and speed,
Backdraft, Scatter, Blade); Seeker's turn rate is Player > Homing Turn Rate. Ghost Step's phases and
Guardian Angel's cooldown show under the SPRINT meter.

## Where power comes from (reward tables)

`BulletHellConfig > Rewards` has one **reward table** per source. Each has a weight per rarity (0 =
never), **Epic Chance** (the small chance that one card is swapped for an epic), **Cards** (0 = the
economy's draft size), **Luck Bonus** and **Include Remnants**. The defaults follow
docs/DESIGN-ROGUELITE.md, section 1:

| Source | Offers |
|---|---|
| Battle (after every Battle room) | commons and rares, 5% chance of one epic (turn **Enabled** off for no draft after battles) |
| Elite, Treasure, Rest (Train), Mystery (`bh:draft`), Shop | commons and rares, 5-15% epic chance |
| **Boss** | **a guaranteed choice of 3 epics** (legendaries rarely) |
| **Trial**, **Sacrifice** (Altar), **Wager** | epics: a reward for a condition you planned for, or something given up |
| Trader | rares |

When an epic screen runs out of epics (late in a run), it tops up from the next rarity down. Validate
Content checks the rules: bosses, trials, sacrifices and wagers offer only epics, and normal battles
get epics only through the small chance.

## Bombs (the Bomb Bay)

There are **no bombs in the starting kit** (*Bombs Per Life* 0 in Player.asset). The **Bomb Bay** epic
gives *Bay Bombs* (2) that recharge one every *Bomb Recharge Seconds* (20) while fighting; a new floor
fills it. Its upgrades stack on it: Spare Bomb (+1), Quick Fuse (recharge), Big Bang (power), Guardian
Angel (auto-bomb). Without a Bay, the shop sells no bombs and a bomb from a Rest or an event becomes
coins. To give bombs from the start again, set *Bombs Per Life* above 0.

## Encounter rooms (between battles)

Five room types where the player chooses a risk (docs/DESIGN-ROGUELITE.md, section 3). Their weights
are in `Run` (*Forge Weight*, *Altar Weight*, *Wager Weight*, *Trial Weight*, *Trader Weight*; 0 turns
one off); each plays its ink line first (`forge`, `altar`, `wager`, `trial_board`, `trader`, or
`<kind>_floor_<n>`), then a choice screen. The numbers and lists are in the config:

| Room | What happens | Data |
|---|---|---|
| **Forge** | Pick an owned upgrade and push it: +1 level (its Forge bonus). The first push is safe; each further push on the visit shows its odds of **breaking** the upgrade (25%, 50%...), and a break loses it with everything forged into it | `Forge`: break chances, max pushes, default bonus scale, scrap coins |
| **Altar** | One offering: an upgrade, a life, or your breath (max stamina -30% for the run) for a choice of epics; or feed one upgrade to another (+2 forge levels, no risk) | `Altar` |
| **Wager** | Up to 2 deals: stake a life, coins or a curse (a stat penalty until the condition ends) on the next fight's condition; win and choose an epic | `Wagers` (a stake, a curse, a condition) |
| **Trial board** | Up to 3 conditions for the next fight(s): no hits, no sprint, a timer, own N upgrades of a tag, graze N bullets, no bombs. Meet it, choose an epic; fail, lose nothing | `Trials` |
| **Trader** | Two commons (one stack each) for a choice of rares, or sell a stack for half its shop price | `Trader` |

Running trials and wagers are listed under **TRIALS** on the right of the HUD. Trials count Battle and
Elite rooms only (a boss fight doesn't count or break them). Instant pickups (Pocket Change, Extra
Life) can't be offered, traded or sold.

## Dying leaves a remnant

Dying is never limited, and something carries forward (docs/DESIGN-ROGUELITE.md, section 5). On death
the run's **best upgrade** (the rarest with a lasting effect, then the most stacks) becomes a
**remnant**: its stat modifiers at half strength, its effects kept, plus a small stat from how you died
(a boss: damage; an elite: fire rate; a battle: move speed). The save keeps the newest 3
(`BulletHellConfig > Remnants`, in `SaveData.entries` under `bh_remnants`). Later drafts can show one,
marked **REMNANT** (always on the first draft of a run, then a 40% chance per draft); taking it uses
it up. The Game Over screen lists "Remnant left".

## The companion

`Data/Companion.asset` (a CompanionDef; *Companion Enabled* in the config turns it off). In battle it
orbits the ship and **shoots** the nearest enemy (40% of a ship shot), **guards**: takes one bullet
meant for you, then recharges (18 s; the GUARD meter on the left and the ring around it show when it's
ready), and **barks**: a one-line call-out of the boss's next pattern a moment before it starts, from
ink, without stopping the game. Its conversations between battles are the Rest room's line. Upgrades
target it with `companion_damage` and `companion_shield_cooldown` (Kindred Sting, Steady Wings).

Hooks for a companion the player looks after (all off by default, in the Companion asset's rules):
*Join After Bosses* (1 = it's met after the first boss; its upgrades aren't offered before that),
*Max Health* (above 0: enemy bullets that touch it hurt it; at 0 it's down: no shots, no guard,
`companion_bark_down`) and *Revive Seconds* (a Rest room also revives it). The companion's design is
being reworked (2026-10-08); expect these to change.

## Balance report

**JamKit > Bullet Hell > Balance Report** (a minute or two): a scripted dodge bot plays every Battle and
Elite room (and each boss's first phase) with each upgrade alone and with none, and writes
`out/balance-report.md` and `.csv`: per upgrade its **DPS against the baseline**, its own **share of the
damage** (its streams, bombs, companion), **hits taken** and **time to clear**. Anything that improves
none of them by 5% is **FLAGGED**. The bot sits low (where players are), lines up under enemies,
reacts every 0.18 s, and plays to its upgrade: it grazes on purpose with Ember Hunger, sprints into
danger when Ghost Step has a phase, holds still between bursts with Still Point, and closes in under
its target with Scatter or the Blade. Fewer hits count once they're more than noise (at least 2, and
the square root of the baseline's hits). Upgrades that only touch coins, luck or lives, or only the
graze ring (Wide Halo: score and graze synergies), are listed as not simulated (the graze one with its
grazes). Batch: `-executeMethod JamKit.BulletHell.EditorTools.BulletHellBalance.RunFromCommandLine`
(optional `-balanceSeeds N`, default 8). The simulation is pure C# (`BalanceSim`, `BalanceReport`),
tested in core-tests.

## Add a boss phase, or a boss

A boss (`Data/Bosses`) has **Hp**, a look, a **Home Position**, and **Phases**. Each phase starts when
the boss's health drops to **Starts At Health** (the first phase: 1; then e.g. 0.66 and 0.33, always
going down). A phase has its own **Attack** (a pattern sequence, as above), **Movement** (*Hold*,
*Sway*, *Figure8*, *Chase* the player's x, *Dash* between random spots), **Move Width**, **Move
Speed** and a **Tint**. When a phase starts the screen flashes, the bullets clear and the boss can't
be hurt for **Phase Change Invulnerable** seconds. The **Title** is the line on its intro card.

To add a boss: duplicate one, edit it, and set it as a floor's **Boss** in the config.

## Change the look (palette)

- **Bullet styles**: `BulletHellConfig > Bullet Styles`. A pattern's *Style Index* 0, 1, 2... picks
  them in order. Each has a **Color**, a drawn **Size**, a **Hit Radius** (smaller than the size is
  fairer) and **Stretch** (1 = round, 2-3 = a long bullet that points where it flies). Style 6 is the
  player's shot and 7 its homing shot (set in `Player.asset`).
- **Floors**: each floor in the config has a **Background** colour, an **Accent** (stars) and a name.
- **Rarity Colors**, **Room Colors** (map and doors), **Coin Color**, **Side Color** (outside the
  playfield): in the config.
- **Enemies and bosses**: their Sprite and Color. The ship: `Player.asset`.
- **UI**: all colours and fonts follow the shell theme (`Assets/JamKit/UI/Theme.uss`); the HUD and
  card layout is `UI/BulletHell.uss`.
- **Sounds**: ids in `Assets/JamKit/Audio/AudioRegistry.asset`. The template uses `shoot`, `graze`,
  `bomb`, `enemy_hit`, `enemy_die`, `boss_phase`, `powerup`, `coin`, `hurt`, `explosion` and
  `music_boss` (all have placeholder tones until you give them a clip).

## Tune the difficulty and the pace

- **Player.asset**: *Lives* (3), *Bombs Per Life* (0: bombs come from the Bomb Bay epic), *Move Speed*, *Hitbox Radius*
  (tiny = forgiving), *Graze Radius*, *Invulnerable After Hit*, the gun's *Fire Interval*,
  *Shot Damage*, *Auto Fire*.
- **Sprint** (Player.asset > Sprint): *Speed Multiplier* (1.6 = 60% faster), *Stamina Seconds* (1.5: one
  burst), *Regen Delay* (0.4 s after letting go), *Refill Seconds* (2: empty to full), *Room Budget
  Seconds* (8: all the sprint one fight room allows; refilled when the next combat, elite or boss
  room starts, so dragging a fight out gains nothing), *Restart Fraction* (0.25: after running dry,
  the bar must be a quarter full before sprinting again). The pure rules are `SprintMeter`
  (Runtime/Core/BulletHell/Sprint.cs), tested in core-tests.
- **Focus mode** (off): the classic slow, precise movement with the hitbox shown. Tick *Focus
  Enabled* in Player.asset and give the **Focus** action a key in
  `Assets/JamKit/Input/JamKitControls.inputactions` (it has none: Shift is Sprint). *Focus Speed*
  sets its speed.
- **Per floor** (config > Floors): *Hp Scale* multiplies every enemy's and the boss's health;
  *Coin Scale* the drops.
- **Run shape** (config > Run): *Floors*, *Rooms Per Floor Min/Max*, the weights for each room type
  (0 turns a type off), *Branch Chance* (how often there are two doors).
- **Prices** (config > Economy): shop prices per rarity, life and bomb prices, reroll cost, draft size,
  grazes per heal, extra luck after elites and bosses.
- **Pacing** (config): *Room Intro Seconds*, *Room Cleared Seconds*, *Wave Gap Seconds*.
- **Player difficulty** is chosen when the run starts: Play opens a Story / Normal / Hard picker
  (Normal the first time, then the last choice; Settings shows and changes the same value). It changes
  enemy bullet speed, bullet density, damage taken (Story: half a life per hit) and extra lives.
  The creator's first playtest reached floor 3 in under 10 minutes on Normal, so Normal suits a
  practised player; Story is the one for a nervous stranger. To skip the picker, untick
  **Choose Difficulty On Play** in GameConfig.
- A run is about 10-15 minutes: ~2-3 fights of ~30-40 s per floor plus a 45-75 s boss, a draft after each
  fight (a few seconds) and 1-2 encounter rooms per floor (the weights keep fights at about 2.5 steps a floor).
  More waves per room or more rooms per floor make it longer.

## Mystery rooms (ink)

`Dialogue/BulletHellEvents.ink` has one knot per event (shrine, gambler, medic, cache). Add a knot,
then add its name to `BulletHellConfig > Event Knots`. Effects are tags:
`#event:bh:coins:40` (or -15), `#event:bh:life:1`, `#event:bh:bomb:1`, `#event:bh:score:500`,
`#event:bh:draft` (or `bh:draft:rare`), `#event:bh:pay:20` (pay 20 coins, if they have them, for a
draft). `has_flag`/`set_flag` remember things between runs. All the shell's dialogue tags work
(see docs/SHELL.md).

## Story hooks (boss lines, the death screen, Rest rooms, run starts)

The game asks the same ink file (`Dialogue/BulletHellEvents.ink`, its end) for a knot at each story
moment. It looks names up **most specific first** and plays the first knot that exists; when none
exists **nothing happens, silently** (no error, no empty box). So you write a line by adding a knot,
and switch a hook off by deleting it. The story itself is yours to write
(`docs/STORY-STRUCTURE-BULLETHELL.md` has the method and a worksheet); the template only ships
one-line placeholders that say "(Placeholder)" in game.

| Moment | Knots tried, in order | How it shows |
|---|---|---|
| A run begins | `run_start_<run>`, `run_start` | a dialogue; the game waits |
| Rest room, before the Heal / Train choice | `rest_floor_<floor>`, `rest` | a dialogue (the companion's room) |
| A boss appears (still flying in, not firing) | `boss_<id>_intro`, `boss_intro` | a dialogue; bullets cleared before and after |
| A boss phase starts (2, 3...) | `boss_<id>_phase_<n>`, `boss_<id>_phase`, `boss_phase` | a dialogue; the boss has just cleared its bullets; cleared again after |
| A boss falls | `boss_<id>_defeat`, `boss_defeat` | a dialogue, then "Boss defeated!" |
| The player dies | `death_<killer>`, `death_floor_<floor>`, `death` | **not a dialogue**: the knot's first line of text shows on the Game Over screen, with its `#speaker` |
| An encounter room, before its choice | `forge_floor_<floor>`, `forge` (also `altar`, `wager`, `trial_board`, `trader`) | a dialogue |
| The companion calls out the boss's next pattern | `companion_bark_<id>_<shape>`, `companion_bark_<shape>` (`ring`, `aimed`, `spiral`, `wall`, `spread`, `shotgun`) | **not a dialogue**: its first line shows low on the screen for a moment; the game keeps going |
| The companion takes a bullet; your last life | `companion_bark_guard`, `companion_bark_low_life` | the same kind of bark |

- **`<id>`** is the boss's **Story Id** (BossDef), or, when that's empty, its display name in lowercase
  with underscores: `gatekeeper`, `forgemother`, `the_last_light`.
- **`<killer>`** is the boss's id when you die in a boss fight, otherwise `elite` or `battle`.
- **`<run>`** counts runs started on this save (1 on the very first run), **`<floor>`** is 1, 2 or 3.
- **ink functions** for choosing lines (declare them with `EXTERNAL`, as the file does):
  `run_count()` (1 on the first run: the persistent playthrough counter, from the shell, saved
  across runs and restarts), `current_floor()`, `killer()`, `runs_won()`, `best_floor()`,
  `companion_name()`, `companion_guards()` (bullets the companion took for you this run: the Rest line
  uses it), and `has_flag(name)` / `set_flag(name)` (saved across runs). So one `death` knot can say
  something different on the first death, to a particular boss, or after an earlier choice.
- Barks are throttled (one every 6 s at most, each boss pattern step called out once per phase);
  keep them to a few words.
- While a line shows, the game is in the Dialogue state: time stops, Pause works, nothing moves.
  After any boss line the enemy bullets are cleared and the ship gets 1 s of safety, so nothing that
  was in the air hits the player when the line closes.
- Hook knots are lines only: the `#event:bh:...` effects work in Mystery rooms, not here. Every
  shell dialogue tag works (`#speaker`, `#portrait`, `#sfx`, `#shake`...).
- Keep them short (1-3 lines): a boss line interrupts the fight.

PLACEHOLDER examples (what the file ships with; replace the text, keep the names):

```ink
=== run_start ===
// PLACEHOLDER: run 1 and later runs say different things.
#speaker:Companion #portrait:Guide happy left
{ run_count() <= 1:
    (Placeholder) Three floors up. Stay close and keep moving.
- else:
    (Placeholder) Run {run_count()}. You know the way now.
}
#portrait:Guide hide
-> END

=== boss_gatekeeper_intro ===
// PLACEHOLDER: what this boss wants, before the fight.
#speaker:Gatekeeper
(Placeholder) None shall pass this gate.
-> END

=== death ===
// PLACEHOLDER: the Game Over line. Inputs: killer(), current_floor(), run_count(), flags.
#speaker:Companion
{
    - killer() == "gatekeeper":
        (Placeholder) The Gatekeeper held. Watch for its walls next time.
    - run_count() <= 1:
        (Placeholder) Everyone falls once. Again?
    - else:
        (Placeholder) Floor {current_floor()}, run {run_count()}. Again?
}
-> END
```

The shipped placeholders: `run_start`, `rest`, `boss_gatekeeper_intro`, `boss_gatekeeper_phase_2`,
`boss_gatekeeper_defeat`, `death`, the five encounter lines, and barks for every pattern shape plus
`companion_bark_gatekeeper_ring`, `companion_bark_guard` and `companion_bark_low_life`. The other two
bosses are silent until someone writes theirs.
`dotnet test core-tests/BulletHell/BulletHell.Tests.csproj` compiles the ink and plays every hook headless.

## Controls

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD, arrows | left stick, d-pad |
| Sprint: hold (limited stamina, and a sprint budget per fight room) | Left Shift | LB |
| Fire | automatic (or J / left mouse if Auto Fire is off) | RT |
| Bomb (once you have the Bomb Bay) | K, right mouse, Space | A, LT, RB |
| Pick a card / door | arrows + Enter, or click | stick + A |
| Pause | Escape, P | Start |
| Focus (off by default: slow, precise, hitbox shown) | no key until you bind one | - |

The creator's first run held Shift expecting a sprint, so **Shift is Sprint**:
- **The SPRINT meter** (left panel, under GRAZE) has two layers: the dim tank is what's left of this
  room's sprint budget (it only shrinks until the next fight room), the bright fill is the stamina
  bar inside it. Under it: "6.2 s left this room", "Catching breath..." or "None left this room".
  When the bar or the room's budget runs out the meter flashes red and a soft sound plays.
- The ship's tiny hitbox dot is always shown (only it can be hit), and a faint aura glows while sprinting.
- The first How to Play card says "You fire automatically" and "Hold Shift (LB) to sprint: limited
  stamina per room (the SPRINT bar on the left)."
- On the **first run only**, the first fight shows a hint low over the playfield (a SHIFT key cap:
  "Hold Shift to sprint", "Limited stamina per room: watch the SPRINT bar on the left. (Gamepad:
  LB)"). It fades after the player has sprinted (at least ~1 s on screen) or after ~8 s, and is
  remembered in the save (flag `bh_hint_sprint_seen`).
- A small control legend sits at the bottom of the left panel. Untick **Show Control Legend** in
  GameConfig to hide it.
- The Time Dilation upgrade (`focus_slow`) now slows enemy bullets while sprinting.

## Debug

The debug menu (` or F1): **God mode**, time x0.25 / x2, **Win**, **Lose** and **Next scene**, which
here **skips the current step**: kills the room's enemies, kills the boss, takes a choice (walks out of
shops and encounter rooms) or ends a dialogue. From code: `BulletHellGame.Current.DebugSkip()`,
`.Bomb()`, `.DebugEnterRoom(RoomType.Forge)` (turns the current room into any room type, to try an
encounter or a boss at once), `.DebugAddTrial(def)`, `BulletHellGame.SeedOverride = 123` (the next run
uses that seed: same rooms, same drafts).

The smoke run (`tools/smoke.ps1`) takes at least one draft and one encounter (a Forge if the doors
didn't offer one) and logs both (`JAMKIT_SMOKE bullet hell draft from ...`, `... encounter ...`).

## For programmers

- Pure rules (no Unity) are in `Packages/com.vibejam.jamkit/Runtime/Core/BulletHell`: `BulletPool`
  (struct-array bullets, culling, hit/graze/bomb tests, bounce, Time Dilation's `SlowNear`),
  `EnemyMover`, `BossPhaseTracker`, `ShmupLoadout` (stats + upgrade effects + the shot volley),
  `ShmupEconomy`, `RoomEventEffect`, `Rewards` (reward tables and `RewardDraw`), `Trials` (trials,
  wagers, `TrialTracker`), `Encounters` (forge, altar, trader rules, `EpicRubric`), `Remnants`,
  `Abilities` (`BombBay`, `PowerBoosts`), `Companion` (`CompanionState`, `PatternCallout`) and
  `BalanceSim` (`BalanceReport`). `UpgradeInventory` (core Roguelite) can remove stacks and carries
  bonus levels. Tested with `dotnet test core-tests/BulletHell/BulletHell.Tests.csproj`.
- The Unity layer is `Templates/BulletHell/Scripts`: `BulletHellGame` (the run and the per-frame
  order; encounters in `BulletHellGame.Encounters.cs`, the companion in `BulletHellGame.Companion.cs`),
  `BulletField` (bullets, drawn with GPU instancing: `InstancedQuads` +
  `Shaders/BulletInstanced.shader`), `ShmupPlayer`, `ShmupEnemy`, `ShmupBoss`, `FxField`, `CoinField`,
  `UI/BulletHellHud`, `UI/ChoicePanel`.
- The editor builder is `Templates/BulletHell/Editor`: `BulletHellContent` (the default data, in
  code, with its content revision), `BulletHellArt` (placeholder art), `BulletHellBuilder` (scene,
  material, preset), `BulletHellBalance` (the balance report), `BulletHellTemplate` (the Use Template menu). Batch:
  `-executeMethod JamKit.BulletHell.EditorTools.BulletHellTemplate.UseFromCommandLine`.
- Delete the whole `Templates/BulletHell` folder to drop the template.
