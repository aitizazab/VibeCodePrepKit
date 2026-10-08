# DOOM-like template: how to change it without code

A fast first-person shooter in three levels. Each level is a **text map**: walls, doors, key
cards, enemies and pickups are characters in a plain `.txt` file. Find the key cards, open the
coloured doors, reach the glowing **EXIT** pad. Between levels you pick **1 of 3 upgrades**. The
last level has a boss (the Warden) guarding the yellow key. A full run takes about 10-14 minutes.

Everything else you'd want to change is a **data asset** in the Inspector: duplicate one (Ctrl+D),
change numbers, add it to a list in the config. No code.

## Switch to this template

- Menu **JamKit > Use Template > DOOM-like**: Play on the title screen now starts the DOOM-like (it
  copies the template's title, tagline and How to Play cards into the active GameConfig and puts
  the scene after Boot and Title in Build Settings). **Use Template > Bullet Hell** or **> Sandbox**
  switches back.
- The template's own title, tagline and How to Play cards are in
  `Assets/JamKit/Templates/Doomlike/DoomlikeGameConfig.asset`. Edit them there, then use the menu again.
- To test alone, open `Templates/Doomlike/Scenes/Doomlike.unity` and press Play. Click the Game view
  so it can hold the mouse (Esc lets go and pauses).

## Where things are

| What | Where (under `Assets/JamKit/Templates/Doomlike/`) |
|---|---|
| The one config (levels, player, lists of everything, look, draft) | `Data/DoomlikeConfig.asset` |
| Level maps (plain text) | `Levels/Level1.txt`, `Level2.txt`, `Level3.txt` |
| Weapons | `Data/Weapons/*.asset` |
| Enemies | `Data/Enemies/*.asset` |
| Pickups (health, ammo) | `Data/Pickups/*.asset` |
| Decorations (pillars, junk) | `Data/Decorations/*.asset` |
| Upgrades (the between-levels draft) | `Data/Upgrades/*.asset` |
| HUD, tally and draft layout | `UI/Doomlike.uss` (colours come from `Assets/JamKit/UI/Theme.uss`) |
| Placeholder art (all generated) | `Art/Generated/*.png`, materials in `Art/*.mat` |

After editing, run **JamKit > DOOM-like > Validate Content**: it lists every problem in plain words
(a door with no key on the map, a key behind its own door, an unknown map character, an enemy with
no sprite...). The EditMode tests run the same checks. **JamKit > DOOM-like > Preview Level 1/2/3 in
Scene** builds a level into the open scene so you can fly around it in the Scene view (it isn't
saved; **Clear Level Preview** removes it).

**JamKit > Rebuild Shell Scenes** rebuilds the scene and art but **keeps your data**: it only creates
assets that are missing. **JamKit > DOOM-like > Reset Content to Defaults** puts every data asset
back to the template's values (map files are never touched).

## Edit or add a level

Levels are built from their map **when they start**, so: edit the `.txt`, save, press Play.

A map file is an optional header, a line of three dashes, then the map. One character is one
square of floor (2.5 metres, `Cell Size` in the config). The top of the file is north.

```
// Lines starting with // are comments (header only).
name: Pump Works
subtitle: Red key opens the valves.
theme: toxic
ceiling: 4
light: 0.9
par: 3:30
facing: north
---
#########
#P..m..E#
#########
```

| Header | Means |
|---|---|
| `name` | the level card and tally title |
| `subtitle` | the line under it ("Find the red key card") |
| `theme` | colours and fog: `base`, `rust`, `toxic`, `ember` (config > Themes; add your own) |
| `ceiling` | height in metres (2.5 to 20; default 3.6) |
| `light` | 0.2 (dark) to 1.5 (bright); 1 is normal |
| `par` | par time on the tally, seconds or m:ss |
| `facing` | which way the player starts looking (default: down the longest corridor) |
| `music` | a sound id; default `music_game`, or `music_boss` when the map has a boss |

### The legend

| Char | What |
|---|---|
| `#` `%` `&` `=` | walls with textures 1-4 (brick, metal panels, tech, rock) |
| `.` `,` | floor, and floor with other tiles (grating) |
| `~` | slime: walkable, hurts (12 per second) |
| (space) | nothing: solid, never drawn. Walls appear wherever floor meets it |
| `D` | a door: opens when you (or an enemy) walk up |
| `R` `B` `Y` | red, blue, yellow door: needs that key card |
| `r` `b` `y` | red, blue, yellow key card |
| `?` | secret door: looks like a wall, opens when you walk into it or press E / Space |
| `S` | secret spot: stepping on it counts a secret (one patch of touching `S` = one secret) |
| `!` | ambush trigger: opens every closet wall and wakes every enemy within 8 cells (`Ambush Radius`) |
| `\|` | closet wall: looks like a wall, sinks into the floor when a nearby `!` fires |
| `P` | player start (exactly one) |
| `E` | exit (at least one) |
| `m` `g` `s` `h` `w` | enemies: Husk (melee rusher), Gunner (aimed shots), Spitter (fireballs), Brute (heavy melee), Wisp (floating, ring of shots) |
| `W` | the Warden (boss: two pattern attacks, gets angry at half health) |
| `+` `H` | health patch (10), medkit (25) |
| `a` `A` `c` `o` | bullet clip (15), box of bullets (50), shells (6), charges (3) |
| `3` `4` `5` | Scattergun, Rivet Gun, Pulse Launcher lying on the floor |
| `T` `j` | lamp pillar (solid), bones (decoration) |

Enemy, pickup, weapon and decoration characters come from their assets' **Map Char**, so adding an
asset adds a character (Validate Content tells you if two clash).

### Rules the validator checks

- One `P`, at least one `E`, and the exit reachable on foot.
- **Keys before doors**: every coloured door has its key on the map, and each key can be reached
  before its door (a key behind its own door, or two keys behind each other's doors, is an error).
- A `D`/`R`/`B`/`Y` door sits between two walls (left and right, or above and below), like a door
  in a corridor.
- Warnings: enemies or pickups nobody can reach, a `|` with no `!` near it, a `!` with nothing to wake.

### Tips for a good level

- 3-5 minutes each: about 20-30 enemies, one or two keys, a loop that brings you back past cleared
  rooms. The samples are 52x32 to 63x42 cells.
- Show the next key's door before the key, so players know where they're going.
- Corridors at least 1 cell wide (2 for big enemies), rooms 5+ cells. Pillars (`T`) give cover.
- Ambush: put `!` across a corridor the player must take (two `!` side by side so nobody slips past),
  and `|` closets with enemies behind them nearby.
- To add a level, make `Level4.txt` (copy one), then add it to config > **Levels**. Order = play order.

## Add an enemy

1. Duplicate an enemy in `Data/Enemies` (Gunner for a shooter, Husk for a biter) and give it a new
   **Map Char** (one letter no one uses).
2. **Health**, **Speed** (the player runs at 9.5), **Radius**, **Height** (drawn size in metres),
   **Hover Height** (above 0 floats).
3. **Brain**: *Sight Range*, *Fov Degrees* (200 = it doesn't see behind; 360 = eyes everywhere),
   *Hearing Range* (gunfire wakes it through walls), *Reaction Seconds*, *Attack Range*, *Attack
   Cooldown*, *Aggression* (attacks per second once ready), *Windup Seconds* (the telegraph: how long
   it glows / shows its red aim line before the hit lands; keep it 0.4+ for fairness), *Pain Chance*
   (a hit flinches it), *Enrage Below* (bosses: faster below this health fraction).
4. **Attack** (and optional **Secondary Attack** with **Secondary Chance**):
   - *Melee*: *Damage*, *Melee Range*. Keep *Attack Range* small (2-3).
   - *Hitscan*: *Damage* per pellet, *Pellets*, *Accuracy* (falls with distance and your speed:
     strafing works). A red aim line shows during the wind-up; break line of sight to dodge.
   - *Projectile*: a **bullet pattern** from the shared pattern system (same fields as the bullet
     hell's patterns, laid flat on the floor: angles go around the enemy, *Aim At Target* fires at
     you, speeds are metres per second, keep them 8-15 so they can be dodged). Plus *Projectile
     Radius*, *Splash Radius* (explodes) and *Projectile Color*.
5. **Gib Overkill** (damage past death that splatters it; 0 = never), **Score**, **Drops** (pickups
   or weapons with a chance each).
6. **Is Boss** shows a health bar while it's awake (with **Boss Title**).
7. Add it to config > **Enemies**, then put its character on a map.

## Swap in real sprites (8 viewing angles)

An enemy's **Sprite > Texture** is one sheet of **8 columns x 5 rows** of equal cells (the
generated ones are 64 x 64 pixels per cell; any size works, point filtering keeps pixels sharp).

- **Columns are viewing angles**, measured from the enemy's facing, counter-clockwise seen from
  above: `0` front (it looks at you), `1` front-left, `2` left side (it faces your left), `3`
  back-left, `4` back, `5` back-right, `6` right side (it faces your right), `7` front-right.
- **Rows** from the top: walk A, walk B, attack (wind-up and firing), pain, death. The death row has
  the death frames from the left (**Death Frames**, last = the corpse that stays), then the gib frames
  (**Gib Frames**).
- **Directions = 5**: draw only columns 0-4; columns 5, 6, 7 are shown as 3, 2, 1 mirrored (the usual
  trick for left-right symmetric characters). **Directions = 1**: column 0 from every side.
- From Blender: render the model 8 times turning it 45 degrees **clockwise** each time (seen from
  above) starting from facing the camera, so turn 1 shows its front-left; paste the renders into the
  columns. Feet at the bottom of the cell, transparent background (alpha is cut at 50%).
- Import settings for new textures: Filter Mode **Point**, Compression **None**, Non-Power of 2
  **None**, Alpha Is Transparency on, mipmaps off. The tint (**Tint**) multiplies the sheet.

Pickups and decorations use any single image (**Texture**, drawn **Size** metres tall). Weapons in
your hands use a horizontal strip (**View Texture**): frame 0 at rest, then the firing frames
(**View Frames** in total), centred at the bottom. Walls, floors and the ceiling are the materials in
`Art/` (64 x 64 tiling textures, one repeat per cell).

## Add or change a weapon

Duplicate one in `Data/Weapons` and add it to config > **Weapons** (and **Starting Weapons** to own
it from the start). **Slot** is its number key (1-9, one weapon per slot). **Kind**: *Melee*,
*Hitscan* (instant rays: *Pellets*, *Spread Degrees*, *Range*, *Falloff Start* / *Min Falloff* for
damage over distance) or *Projectile* (*Projectile Speed*, *Splash Radius*, *Self Damage Scale*).
**Def > Fire Mode / Fire Interval** set the rate (Auto = hold to fire); keep **Magazine Size 0**
(guns draw from the shared ammo pool). **Ammo Type** is an id from config > **Ammo Types** (two guns
with the same id share it, like the pistol and the rivet gun); empty = needs no ammo. **Map Char**
lets it lie on maps, **Pickup Ammo** is what it gives when picked up, **Priority** picks the gun to
switch to when one runs dry. Feel: **Recoil Degrees**, **Shake**, **Noise Radius** (wakes enemies),
**Fire Sound**.

## Upgrades between levels

After every level but the last, the player picks 1 of 3 (config > **Upgrade Draft**, on by default;
**Draft Size** is the number of cards). Each upgrade's **Def** has an **Id**, **Display Name**,
**Description**, **Rarity** (rarer = shown less often) and **Max Stacks** (0 = unlimited), and does
one or both of:

- **Modifiers** on stats: `damage`, `fire_rate`, `move_speed`, `max_health` (points), `ammo_capacity`,
  `melee_damage`, `splash_radius`, `pickup_bonus`, `armor` (0.15 = 15% less damage, max 0.75).
  *Op*: Flat (+value), PercentAdd (0.15 = +15%), Multiply.
- **Effects**: `heal_full`, `heal:30` (heal 30), `fill_ammo`, `lifesteal` (+4 health per kill per stack).

**Icon Text** (2-3 letters) and **Icon Color** show on the card and the HUD. The **Fallback Upgrade**
(Second Wind) is offered when nothing else can be. Turn the draft off for a straight shooter.

## Tune the difficulty and the feel

- **Player** (config > Player): *Max Health*, *Motion* (*Run Speed* 9.5, *Walk Speed*, *Acceleration*,
  *Friction*), *Mouse Degrees Per Pixel*, *Gamepad Degrees Per Second*, *Bob Amount* (0 = no head bob),
  *Hazard Damage Per Second*, *Vertical Auto Aim Degrees* (shots bend up/down to an enemy near the
  crosshair; 0 = off).
- **Health Floor Per Level**: health tops up to at least this at the start of each level (0 = off).
- Enemy health, damage, *Aggression*, *Attack Cooldown* and *Windup Seconds* are the big difficulty
  knobs; more enemies per map and fewer medkits make it harder.
- **Player difficulty settings** (Settings menu) apply by themselves: Story / Normal / Hard change
  damage taken and enemy shot speed and density.
- Feel: config > *Kill Hit Stop* (a tiny freeze on kills), *Hurt Shake*, *Hurt Flash Alpha*.

## The look (pixel look, palette)

- **Pixel look** is on by default: the world renders at about 480 x 270 and is scaled up with hard
  pixels, then reduced to fewer colours with a dither, so mixed art reads as one set. Players switch
  it in **Settings > Pixel look** (the preset turns that option on: GameConfig > *Show Pixel Look
  Setting*). Config > **Retro**: *Pixel Height* (270; 360 is crisper), *Color Levels* (steps per
  colour channel; 0 = no reduction), *Dither*.
- **Themes** (config > Themes): wall tints per texture, floor and ceiling tints, fog colour and
  distance. A map picks one with `theme:`.
- **Key Colors**: red, blue, yellow (cards, doors and the HUD).
- **Sounds** (ids in `Assets/JamKit/Audio/AudioRegistry.asset`; all have generated placeholders):
  `punch pistol shotgun rapid launcher door item key step alert pain enemy_shot bite secret
  weapon_up level_done hurt explosion enemy_die blip`.

## Controls

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Move (always running) | WASD, arrows | left stick |
| Walk | Left Shift | LB |
| Look | mouse | right stick |
| Fire | left mouse, J | RT |
| Weapons | 1-5, mouse wheel, Q (previous) | Y (next) |
| Use (doors, secret walls) | E, Space | X, A |
| Pause | Escape, P | Start |

In a browser the game needs a click to hold the mouse: play starts (and resumes after a pause)
behind a "click to play" screen. A gamepad button also starts it.

## Debug

The debug menu (` or F1): **God mode**, time x0.25 / x2, **Win**, **Lose**, **Next scene** (here:
jump to the exit and finish the level; on the tally, continue; in the draft, take the first card)
and **Give all (weapons, keys, ammo)**. From code: `DoomGame.Current.DebugSkip()`, `.GiveAll()`,
`.DebugSpawnEnemies(40, position, 5f)`, `DoomGame.SeedOverride = 123` (same drops and drafts),
`DoomGame.StartLevel = 2` (start the next run on level 3), `DoomGame.SkipClickToPlay = true`.

## For programmers

- Pure rules (no Unity) are in `Packages/com.vibejam.jamkit/Runtime/Core/Doomlike`: `MapParser`,
  `MapLegend`, `MapValidator` (key-order reachability), `AngleFrame` (8-angle sprite pick),
  `AmmoPools` (shared ammo feeding magazine-less `WeaponState`s), `HitscanMath` (falloff, seeded
  pellet spread, splash), `EnemyBrain` (core `StateMachine`: idle, alert, chase, attack with wind-up,
  pain, dead), `PlayerMotion`, `LevelTally`, `DoomStats`, `GridMath` (cells to world, 2D angles to
  yaw; pattern output maps onto XZ). Tested with `dotnet test core-tests/Doomlike/Doomlike.Tests.csproj`
  (which also validates the real level files).
- The Unity layer is `Templates/Doomlike/Scripts`: `DoomGame` (the run and the per-frame order, split
  into `DoomGame.cs`, `.Combat.cs`, `.Levels.cs`), `Level/LevelBuilder` (map to meshes, colliders,
  doors, things and a runtime NavMeshSurface), `Actors/DoomPlayer`, `PlayerArsenal`, `WeaponView`,
  `DoomEnemy`, `Fx/ProjectileField`, `FxField`, `RetroCamera` (pixel look), `UI/DoomHud`,
  `DoomChoicePanel`, `Data/*` (assets and `DoomlikeValidation`). Shaders in `Shaders/`.
- The editor side is `Templates/Doomlike/Editor`: `DoomlikeArt` (placeholder art, drawn in code with
  `Px`), `DoomlikeContent` (default data), `DoomlikeBuilder` (materials, preset, scene, previews),
  `DoomlikeTemplate` (Use Template). Batch:
  `-executeMethod JamKit.Doomlike.EditorTools.DoomlikeTemplate.UseFromCommandLine`.
- Enemies are on Unity's built-in layer 1 and the player on layer 2 (Ignore Raycast), so no project
  layer setup is needed. Levels are built at runtime (about a tenth of a second with the NavMesh), so
  map edits need no rebuild.
- Delete the whole `Templates/Doomlike` folder to drop the template.
