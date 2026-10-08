# Bullet-hell roguelite template: how to change it without code

A vertical shoot-'em-up run: **3 floors**, each a row of 5-6 rooms ending in a **boss**. Battle
rooms play waves of enemies; between rooms the player picks **1 of 3 upgrades**, chooses doors,
shops, rests or meets a stranger (an ink dialogue). Beat the third boss to win. A full run takes
about 10-14 minutes.

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
| Mystery-room dialogue | `Dialogue/BulletHellEvents.ink` |
| HUD and card layout | `UI/BulletHell.uss` (colours come from `Assets/JamKit/UI/Theme.uss`) |
| Placeholder art | `Art/Generated/*.png` |

After editing, run **JamKit > Bullet Hell > Validate Content**: it lists every problem in plain
words (an empty room pool, an enemy with 0 health, an upgrade with an unknown effect...). The
EditMode tests run the same checks.

**JamKit > Rebuild Shell Scenes** rebuilds the scene and art but **keeps your data**: it only creates
assets that are missing. **JamKit > Bullet Hell > Reset Content to Defaults** puts every data asset
back to the template's values (your changes to them are lost).

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
   what it does), **Rarity** (Common, Rare, Epic, Legendary: rarer = shown less often),
   **Max Stacks** (how many times it can be taken; 0 = unlimited).
3. What it does, either or both:
   - **Modifiers** change numbers. *Stat* is one of: `damage`, `fire_rate`, `move_speed`,
     `shot_speed`, `graze_radius`, `luck`, `magnet`, `coin_gain`, `bomb_power`. *Op*: Flat (+value),
     PercentAdd (0.2 = +20%), Multiply (1.5 = x1.5).
   - **Effects** are special abilities, by id: `spread` (+2 side streams per stack), `pierce` (shots go
     through one more enemy per stack), `homing` (side streams curve to enemies), `rear_shot`,
     `bigger_bombs` (+50% per stack), `graze_heal` (grazes restore life), `bomb_plus` (+1 bomb now
     and +1 max), `life_up` (+1 life), `auto_bomb` (a hit fires a bomb instead), `focus_slow`
     (enemy bullets slow while focusing), `coins:15` (15 coins now).
   - *Requires* / *Excludes*: ids it needs, or can't be owned with.
4. **Icon Text** (2-3 letters) and **Icon Color** for the HUD, or an **Icon** sprite.
5. Add it to `BulletHellConfig > Upgrades`. The **Fallback Upgrade** (Pocket Change) is offered when
   nothing else can be.

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

- **Player.asset**: *Lives* (3), *Bombs Per Life* (2), *Move Speed* / *Focus Speed*, *Hitbox Radius*
  (tiny = forgiving), *Graze Radius*, *Invulnerable After Hit*, the gun's *Fire Interval*,
  *Shot Damage*, *Auto Fire*.
- **Per floor** (config > Floors): *Hp Scale* multiplies every enemy's and the boss's health;
  *Coin Scale* the drops.
- **Run shape** (config > Run): *Floors*, *Rooms Per Floor Min/Max*, the weights for each room type
  (0 turns a type off), *Branch Chance* (how often there are two doors).
- **Prices** (config > Economy): shop prices per rarity, life and bomb prices, reroll cost, draft size,
  grazes per heal, extra luck after elites and bosses.
- **Pacing** (config): *Room Intro Seconds*, *Room Cleared Seconds*, *Wave Gap Seconds*.
- **Player difficulty settings** (Settings menu) are applied automatically: Story / Normal / Hard
  change enemy bullet speed, bullet density, damage taken (Story: half a life per hit) and extra lives.
- A run is about 10-14 minutes: ~2-3 battle rooms of ~30-40 s per floor plus a 45-75 s boss.
  More waves per room or more rooms per floor make it longer.

## Mystery rooms (ink)

`Dialogue/BulletHellEvents.ink` has one knot per event (shrine, gambler, medic, cache). Add a knot,
then add its name to `BulletHellConfig > Event Knots`. Effects are tags:
`#event:bh:coins:40` (or -15), `#event:bh:life:1`, `#event:bh:bomb:1`, `#event:bh:score:500`,
`#event:bh:draft` (or `bh:draft:rare`), `#event:bh:pay:20` (pay 20 coins, if they have them, for a
draft). `has_flag`/`set_flag` remember things between runs. All the shell's dialogue tags work
(see docs/SHELL.md).

## Controls

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD, arrows | left stick, d-pad |
| Focus (slow, shows hitbox) | Left Shift | LB |
| Fire | automatic (or J / left mouse if Auto Fire is off) | RT |
| Bomb | K, right mouse, Space | A, LT, RB |
| Pick a card / door | arrows + Enter, or click | stick + A |
| Pause | Escape, P | Start |

## Debug

The debug menu (` or F1): **God mode**, time x0.25 / x2, **Win**, **Lose** and **Next scene**, which
here **skips the current step**: kills the room's enemies, kills the boss, takes a choice or ends a
dialogue. From code: `BulletHellGame.Current.DebugSkip()`, `.Bomb()`,
`BulletHellGame.SeedOverride = 123` (the next run uses that seed: same rooms, same drafts).

## For programmers

- Pure rules (no Unity) are in `Packages/com.vibejam.jamkit/Runtime/Core/BulletHell`: `BulletPool`
  (struct-array bullets, culling, hit/graze/bomb tests), `EnemyMover`, `BossPhaseTracker`,
  `ShmupLoadout` (stats + upgrade effects + the shot volley), `ShmupEconomy`, `RoomEventEffect`.
  Tested with `dotnet test core-tests/BulletHell/BulletHell.Tests.csproj`.
- The Unity layer is `Templates/BulletHell/Scripts`: `BulletHellGame` (the run and the per-frame
  order), `BulletField` (bullets, drawn with GPU instancing: `InstancedQuads` +
  `Shaders/BulletInstanced.shader`), `ShmupPlayer`, `ShmupEnemy`, `ShmupBoss`, `FxField`, `CoinField`,
  `UI/BulletHellHud`, `UI/ChoicePanel`.
- The editor builder is `Templates/BulletHell/Editor`: `BulletHellContent` (the default data, in
  code), `BulletHellArt` (placeholder art), `BulletHellBuilder` (scene, material, preset),
  `BulletHellTemplate` (the Use Template menu). Batch:
  `-executeMethod JamKit.BulletHell.EditorTools.BulletHellTemplate.UseFromCommandLine`.
- Delete the whole `Templates/BulletHell` folder to drop the template.
