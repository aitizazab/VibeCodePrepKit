# Story template: how to write a game with it

A narrative game kit: you write the story in **ink** (plain text, playable on its own in the free
**Inky** editor), and short **tags** at the end of lines put pictures, characters, sound and
effects on screen. On top of a visual novel's usual parts (backgrounds, characters with
expressions, choices, text box or full-screen text, save and load, history, auto and skip) it has
two extras:

- **A memory that survives restarts.** The game knows how many times it was played, which endings
  were reached, what the player said last time, whether they quit halfway or reloaded a save. ink
  can ask, so the story can change on a second playthrough, notice save-scumming, or rewrite its
  own title screen.
- **Point-and-click rooms.** A background with clickable areas, each running a knot of ink, and an
  inventory whose items can be used on them. Enough for an investigation or an escape room.

The sample, **Last Train Home** (about 10-12 minutes, four endings, one only on a second
playthrough), uses every feature. Its prose is placeholder: replace it with yours.

## Switch to this template

- Menu **JamKit > Use Template > Story**: Play on the title screen now starts the story (it copies
  the template's title, tagline, How to Play cards and settings options into the active GameConfig
  and puts the Story scene after Boot and Title). **Use Template > Bullet Hell / DOOM-like / Sandbox**
  switches back.
- The template's own title, tagline and How to Play cards are in
  `Assets/JamKit/Templates/Story/StoryGameConfig.asset`. Edit them there, then use the menu again.
- To try it alone, open `Templates/Story/Scenes/Story.unity` and press Play.
- On the title screen the story adds **Continue** (when there's an autosave), **Load** and **Endings**.

## Where things are

| What | Where (under `Assets/JamKit/Templates/Story/`) |
|---|---|
| The story (ink) | `Dialogue/LastTrain.ink` (main), `LastTrain_Office.ink`, `LastTrain_Endings.ink`, and `StoryKit.ink` (the toolkit; don't edit) |
| Every id ink can use: backgrounds, characters, CGs, items, endings, colours | `Resources/JamKit/StoryConfig.asset` (the story config) |
| Characters (base picture and expressions) | `Data/Characters/Iris.asset`, `Bram.asset` (the shell's Portrait assets) |
| Point-and-click rooms | `Data/Hotspots/Office.asset` |
| Placeholder art (generated) | `Art/Generated/*.png` |
| The look (text box, menus, colours) | `UI/Story.uss`; colours come from `Assets/JamKit/UI/Theme.uss` |

Open the main ink file from the menu: **JamKit > Story > Open Main Ink File** (it opens in Inky if
.ink files are associated with it).

## The writing loop

1. Edit the .ink in Inky (or any text editor). Inky plays it as you type; the game's memory
   functions have stand-ins there (see "Test a second playthrough in Inky").
2. Save. If Unity is open, the **story check** runs at once and lists any problem in the Console:
   a tag that doesn't exist, a background id with a typo, an expression the character doesn't have,
   a divert to nowhere, a knot nothing reaches. Errors name the file and line.
3. **While playing in the editor**, saving an .ink file **reloads it within a second**: the knot
   you're in starts again with the new text, keeping variables and the picture. (Compile errors
   show as a message; the old version keeps running.)
4. **Debug menu** (` or F1) while playing: *Story: skip to the next choice*, *Next scene* (skips to
   the next knot, or through a point-and-click room), *Story: jump to knot...*, *give all items*,
   *show all hotspots*, *unlock all endings*, *reset memory (all playthroughs)*.
5. Before the build: **JamKit > Story > Validate Ink** (also shows warnings). The EditMode tests and
   the Windows/WebGL build run the same check, and **a build with story errors stops**, so a typo
   can't reach the jam build.

## ink in 10 minutes

ink is plain text. Every example below is from the sample.

**Lines.** One line of text = one line in the box. A name and a colon before it makes it dialogue:

```
The board above the platform flickers: <b>23:52 WEXLEY</b>. Twelve minutes.
Iris: Is it nearly midnight?
```

The name must be a character's id or display name (`iris` or `Iris`); anything else is narration
("Note: the door is open" stays narration). `#thought` on a line makes it a thought (italic, dimmer).

**Knots** are scenes; `->` goes to one. `-> END` ends the story; `-> DONE` stops here for now
(used by point-and-click rooms).

```
=== waiting_room ===
The waiting room is warmer than it has any right to be.
-> office
```

**Stitches** are parts of a knot (`= the_photograph`, reached with `-> the_photograph` inside the
knot or `-> waiting_room_return.the_photograph` from outside).

**Choices.** `*` choices disappear once taken, `+` choices stay. Text in `[brackets]` shows on the
button but isn't repeated after choosing. A line with only `-` (a gather) is where the branches meet
again; `- (name)` gives it a label you can divert to (`-> platform.after_greeting`).

```
* ["Twelve minutes to go."]
    "Twelve minutes," you say. "If it's on time."
    ~ trust += 1
* [Say nothing. Check the board again.]
    You look at the board. The board looks back at you. #thought
- (after_greeting)
```

Players choose by clicking, with the number keys, or with the arrows and Enter.

**Variables.** `VAR trust = 0` at the top (outside knots). `~ trust += 1` changes it. Print it with
`{trust}`. Variables are saved in save slots, and start over on a new playthrough.

**Conditions.**

```
{trust >= 3: Iris: It was nice. Being looked after for a bit.}       one line, or nothing
{has(photograph): -> the_photograph}                                  a divert
{was_reloaded(): Did you go back? | Never mind.}                      true | false
{ playthrough_count() > 1:                                            several lines
    Iris: You're early this time.
- else:
    Iris: Is it nearly midnight?
}
* {has(photograph) && saw_photo} [Give her your ticket.]               a choice only when true
```

`and`, `or`, `not`, `==`, `!=`, `>`, `<` all work. A knot's name is how many times it was visited:
`{office_coat > 1: ...}`.

**Variety.** `{~That doesn't help here.|You try it. Nothing happens.|No. Not that.}` picks one at
random; `{&a|b|c}` cycles; `{a|b|c}` goes through once and stays on the last.

**Functions.** `=== function last_ending_title() ===` with `~ return "..."`; call them in text
`{last_ending_title()}` or logic `~ x = f()`.

**Tunnels** step into a knot or stitch and come back: `-> noticed_reload ->` runs it; it ends with `->->`.

**Several files.** `INCLUDE LastTrain_Office.ink` at the top of the main file. The included files'
knots are part of the same story.

**Comments:** `// to the end of the line`, `/* across lines */`.

**What a line can't contain** (ink reads them as code): `#` starts a tag, `//` starts a comment,
`{ }` are logic, `->` is a divert, `<>` is glue, and `[ ]` inside choice text are the brackets.
Write `\#` or `\{` if you really need the character.

## Tags: the full reference

A tag goes at the end of a line (`Line text #tag:value`) or on a line of its own. Several tags can
share a line. **Tags on a line of text all start together, and the text waits for anything slow
(a fade, a chapter card) to finish. Tags on lines of their own play one after another**, each
waiting for the slow ones before it, so you can stage a sequence:

```
#meta:popup Iris would like to leave the game. Is that all right?
#meta:fakecrash 2.5
#fade:out white 1.5
#scene:street_rain cut
#fade:in 2
You are standing in a street you have never seen.
```

A click (or Enter) skips whatever is playing. Words after the colon can come in any order where it
makes sense (`#show:iris left smile` = `#show:iris smile left`). Durations are seconds (`2` or `2s`).

### Stage

| Tag | Does | Example |
|---|---|---|
| `#bg:id [transition] [seconds]` | change the background; characters stay | `#bg:platform_night fade 1.5` |
| `#scene:id [transition] [seconds]` | new scene: background, and characters and CG leave (default: through black) | `#scene:office black 1` |
| `#cg:id [transition] [seconds]` | a full-screen picture over everything | `#cg:cg_photo fade 1.5` |
| `#cg:off [transition] [seconds]` | remove it | `#cg:off fade 1` |

Transitions: `fade` (crossfade, the default for #bg and #cg), `cut` (instant), `flash` (white flash),
`black` (dip to black and back), `slide` (characters only). `#bg:black` and `#bg:white` are plain
colours in the sample's config.

### Characters

| Tag | Does | Example |
|---|---|---|
| `#show:id [expression] [place] [transition] [seconds] [flip]` | bring a character on, or change their expression / place | `#show:iris smile right fade` |
| `#hide:id [transition] [seconds]` | take them off (`#hide:all` for everyone) | `#hide:bram slide` |
| `#move:id place [seconds]` | slide to a place | `#move:iris center 0.8` |

Places: `farleft`, `left`, `center`, `right`, `farright`, `offleft`, `offright`, or a share of the
width like `40%`. Without a place a new character takes a free spot (centre first). `flip` mirrors
them, `noflip` undoes it. When someone speaks, the other characters dim a little. The expression
names are the ones in the character's Portrait asset; leaving it out keeps the current face (the
default expression on first show).

### Camera and effects

| Tag | Does | Example |
|---|---|---|
| `#shake:[strength] [seconds]` | shake the picture (strength 0..1; follows the Screen shake setting) | `#shake:0.6 1.5` |
| `#flash:[colour] [seconds]` | full-screen flash | `#flash:white 0.6` |
| `#fade:out [colour] [seconds]` / `#fade:in [seconds]` | fade the picture to a colour (the text stays readable), and back | `#fade:out black 1` |
| `#zoom:scale [seconds] [x% y%]` | zoom in on a point (`#zoom:1` back to normal) | `#zoom:1.25 0.8 70% 30%` |
| `#blur:[pixels] [seconds]` | soft focus (`#blur:0` sharp again; `#blur` alone is 6) | `#blur:2 1.5` |
| `#filter:name [amount] [seconds]` | colour filter: `sepia`, `gray`, `invert`, `night`, `dream`, `none` | `#filter:sepia 0.9 1.5` |
| `#glitch:[strength] [seconds]` | digital glitch: the picture jumps, bars flicker, letters scramble | `#glitch:0.6 0.8` |

Colours are `white`, `black`, `red`, `green`, `blue`, `yellow`, `orange`, `purple`, `gray`, `pink`,
`cyan`, or a palette name from the story config.

### Sound

| Tag | Does | Example |
|---|---|---|
| `#music:id [fade seconds]` | music (crossfades; `#music:stop`) | `#music:music_story_sad 3` |
| `#stopmusic:[seconds]` | fade the music out | `#stopmusic:3` |
| `#ambience:id [fade seconds]` | a looping background sound (rain, hum) | `#ambience:amb_rain 2` |
| `#stopambience:[seconds]` | fade it out | `#stopambience:2` |
| `#sfx:id` | a sound effect | `#sfx:door_knock` |

Placeholder sounds generated in code: music `music_story_calm`, `music_story_tense`,
`music_story_sad`, `music_story_warm` (and the shell's `music_title`, `music_game`); ambience
`amb_rain`, `amb_hum`, `amb_wind`; effects `door_knock`, `page`, `drawer`, `unlock`, `glitch`,
`heartbeat`, `chime`, `train_horn`, `train_pass`, `thunder`, `footsteps`, `click`, `item` and the
shell's others. Character voices (the blips while their lines type): `voice_low`, `voice_high`.

### Text and pacing

| Tag | Does | Example |
|---|---|---|
| `#nvl` | full-screen text: lines stack up like a page (good for prose) | `#nvl` |
| `#adv` | back to the text box | `#adv` |
| `#clear` | start a new full-screen page (it also clears itself when full) | `#clear` |
| `#thought` | this line is a thought | `You look at the board. #thought` |
| `#speaker:Name` | the name in the box for the following lines, until `#speaker:` (the "Name: line" way is easier) | `#speaker:Iris` |
| `#chapter:Title` | a chapter title card | `#chapter:One - The Last Platform` |
| `#wait:seconds` | pause before the next line types | `#wait:0.5` |
| `#auto:[seconds]` | this line goes on by itself after it has typed (default 1.5) | `I was waiting for him. #auto:3` |

### Story structure

| Tag | Does | Example |
|---|---|---|
| `#ending:id` | this route's ending; when the story then reaches `-> END` the game shows the ending card, the victory screen and the credits, and remembers it | `#ending:last_train` |
| `#investigate:id` | open a point-and-click room (see below) | `#investigate:office` |
| `#leave` | close the room; the story goes on from there | `You have what you came for. #leave` |
| `#event:name` | tells your own code something happened (`GameRoot.Dialogue.EventRaised`) | `#event:door_opened` |

### Fourth-wall tricks (`#meta`)

| Tag | Does |
|---|---|
| `#meta:title New Title` | the title screen shows this title from now on (also after restarting the game) |
| `#meta:tagline Text` | the same for the line under the title |
| `#meta:playlabel Text` | renames the title screen's Play button |
| `#meta:reset` | puts title, tagline and Play back |
| `#meta:popup Text` | a system-style message box; the story waits until it's closed |
| `#meta:fakecrash [seconds]` | the game pretends to hang ("Not Responding") for a moment; input waits |
| `#meta:erase` | deletes the save slots (the memory stays). Use with care, and on purpose |

### Inline markup in a line

| Markup | Does |
|---|---|
| `<b>bold</b>`, `<i>italic</i>`, `<u>`, `<s>`, `<size=120%>` | text styles |
| `<c=accent>word</c>` | a palette colour from the story config (ink won't allow `#` in a line, so hex colours can't be written there) |
| `<color=red>word</color>` | a named colour |
| `<pause=0.5>` | the typing stops for half a second here |
| `<speed=0.5>slowly</speed>` | types this part at half speed (`2` = twice as fast) |

Markup can't run across lines. A `<` that isn't a tag is shown as text.

### Your own tags

Anything else is an error in the check, so typos are caught. If your code handles a tag of its own
(through `GameRoot.Dialogue.TagReceived`), add its name to the story config's **Extra Tags**.

## Add a background, a CG or a sound

- **Background:** story config > **Backgrounds** > +: an **Id** (what ink writes after `#bg:`), a
  **Sprite** (16:9 fills the screen; other shapes are cropped) or just a **Color**. Import pictures
  as Sprite (2D and UI).
- **CG:** the same in **Cgs** (`#cg:id`).
- **Sound:** `Assets/JamKit/Audio/AudioRegistry.asset` > + : an id and a clip (tick **Loop** for
  music and ambience). An entry with the id of a placeholder replaces it everywhere.
- **Palette colour:** story config > **Palette**: a name and a colour, for `<c=name>`, `#flash:name`,
  `#fade:out name`.

## Add a character

1. Duplicate `Data/Characters/Iris.asset` (Ctrl+D). Set **Character Id**, a **Base Sprite** (the whole
   character, transparent around) and **Expressions**: each a name and a sprite of the same size
   drawn over the base (just the face, the rest transparent). **Default Expression** is used on the
   first `#show` without one.
2. Story config > **Characters** > +: **Id** (for `#show:id` and `id: line`), **Display Name** (in
   the name box; also works as `Name: line`), **Name Color**, **Portrait** (the asset above),
   **Height** (share of the screen; 0.9 is nearly full height), **Y Offset**, **Voice** (a sound id
   for the typing blips).

## Build a point-and-click room

1. Draw or pick a background and add it as a background id.
2. **Create > JamKit > Story > Hotspot Scene** (or duplicate `Data/Hotspots/Office.asset`). Set its
   **Id** (for `#investigate:id`), **Background**, **Prompt** (the hint line at the top) and add it
   to the story config's **Hotspot Scenes**.
3. Add **Hotspots**. Each has an **Id**, a **Label** (shown when pointed at), a **Shape** and a
   **Knot** to run when clicked:
   - **Rect**: x, y (top-left corner), width, height, as **fractions of the picture**: x 0 is the left
     edge, 1 the right; y 0 the top, 1 the bottom. A pixel at (480, 270) on a 1920x1080 picture is
     (0.25, 0.25).
   - **Polygon**: the corners in order, same fractions (the sample's coat and desk are polygons).
   - When shapes overlap, the smaller one wins (the drawer inside the desk).
   - Check your shapes in Play mode with the debug menu's **Story: show all hotspots**.
4. **Show If** (optional) hides a hotspot until something is true: `!item:ticket`, `seen:office_coat`,
   `drawer_open`, `trust >= 2 && !found_key`. Names are ink variables, items (`item:`), knots visited
   this playthrough (`seen:`), flags (`flag:`), endings ever reached (`ending:`), remembered values
   (`memory:`), and `playthrough`, `endings_seen`, `last_ending`, `times_quit`, `loads`, `rewinds`.
   **Hide After Use** removes it once its knot has run.
5. In ink: a knot that opens the room and stops, one knot per hotspot that ends with `-> DONE` (back
   to the room), and one that leaves with `#leave` and goes on:

```
=== office ===
#scene:office black 1
Somewhere in here is a brown ticket with a name on the back. #investigate:office
-> DONE

=== office_board ===
A notice board. Most of the notices are about a jumble sale in 1987.
-> DONE

=== office_door ===
{ has(ticket):
    You have what you came for. #leave
    -> waiting_room_return
- else:
    You can't go back to her empty-handed. Not yet. #thought
    -> DONE
}
```

6. **Items.** List them in ink and in the config:
   - ink (main file): `LIST Items = brass_key, ticket, photograph`. Give, take and test with
     `~ give(brass_key)`, `~ take(brass_key)`, `{has(brass_key): ...}` (from StoryKit.ink).
   - story config > **Items**: the same ids, each with a **Display Name**, **Description** and **Icon**.
   - Items appear at the bottom left while a room is open (a "Got: ..." message when one is given).
     Players click one (or press its number) to hold it, then click a hotspot to use it there.
   - Hotspot > **Item Uses**: which item runs which knot here (`brass_key` > `office_drawer_key`).
     Any other item runs the room's **Fallback Item Knot** ("That doesn't help here"). The item last
     used is in the ink variable `used_item`.
7. **Walkthrough**: the hotspots the smoke test clicks to get through the room, in order, with
   `item>hotspot` for using an item: `coat`, `brass_key>desk_drawer`, `shelves`, `door`. It must
   reach `#leave`.

Players can also use the keyboard or a pad: arrows / stick move between hotspots, Tab or the
shoulder buttons cycle, Enter / (A) clicks, the number keys or (Y) pick items, Backspace drops one.
Small markers show where hotspots are (dimmer once looked at); turn them off with **Show Markers**.

**Recipe: a map.** A "go to" screen is a hotspot scene too: a map background, one hotspot per
place, and each place's knot does `#leave` and diverts there.

## Memory, endings and the fourth wall

### What the game remembers

These ink functions read the memory that survives restarts (declared in StoryKit.ink):

| Function | Gives |
|---|---|
| `playthrough_count()` | 1 on the first playthrough, 2 on the second... |
| `endings_seen()` | how many different endings were reached |
| `seen_ending("id")` | true if that ending was ever reached |
| `last_ending()` | the id of the last ending reached ("" if none) |
| `remember("key", value)` | keep a value (text, number, true/false) forever |
| `recall("key")` / `knows("key")` | get it back (0 if never remembered) / whether it exists |
| `visited_ever("knot")` | that knot was seen in any playthrough (this one included) |
| `times_quit()` | how often the player left mid-story (quit to the title, restarted, closed the game) |
| `loads()` / `rewinds()` / `was_reloaded()` | saves loaded this playthrough / loads that went back in time / loads() > 0 |
| `has_flag("x")`, `set_flag("x")`, `clear_flag("x")` | the shell's flags, also kept forever |

Ink variables (`VAR`) belong to one playthrough and live in the save slots; the memory doesn't, so
loading an earlier save can't undo it. Continuing from the autosave isn't counted as a load.

### Recipes

**A choice that only exists the second time round** (sample: the platform):

```
* {playthrough_count() > 1 || endings_seen() > 0} ["We've done this before, haven't we?"]
    -> deja_vu
```

**Remember what the player did, and bring it up next time** (sample: the first words). Read it
before this playthrough overwrites it:

```
VAR last_first_words = ""
...
{knows("first_words"):
    ~ last_first_words = recall("first_words")
}
* ["Are you all right?"]
    ~ remember("first_words", "a kindness")
...
{playthrough_count() > 1 && last_first_words != "": Iris: Last time, the first thing you gave me was {last_first_words}.}
```

**Notice that the player quit or reloaded** (sample: the platform and the train):

```
{ times_quit() > 0: Iris: You left so suddenly last time. }
{ was_reloaded():
    -> noticed_reload ->
}
= noticed_reload
#glitch:0.5 0.6
Iris: Did you... go back, just now? {rewinds() > 0: You went back further than you meant to.}
->->
```

**Talk about the last ending:** `{last_ending() == "last_train": ...}` or a function that turns the
id into words (sample: `last_ending_title()`).

**A secret ending.** Tick **Secret** on it in the story config: the gallery shows only that a secret
exists until it's found. Offer its route only on a later playthrough (the choice above).

**Change the title screen.** Two ways, and the first wins:
- from ink, at the right moment: `#meta:title Next Train Home`, `#meta:tagline ...`,
  `#meta:playlabel ...` (sample: the secret ending);
- from data: story config > **Title Variants**: a **Condition** (the same language as Show If, e.g.
  `endings_seen >= 1 && !ending:return_ticket`) and the title, tagline or Play text to show. The
  first true one is used (sample: "Somebody is still waiting on the platform." / "Go back").

**Fake interface moments.** `#meta:popup Iris would like to leave the game. Is that all right?`
(a system-looking box the player must close), `#meta:fakecrash 2.5` (a frozen, washed-out screen
titled "Not Responding" that recovers by itself), `#glitch` with letters scrambling in the text box.
Combine them on their own lines so they play in order. `#meta:erase` really deletes the save slots:
a strong move ("there's no going back now") that some players will hate, so be sure.

**Endings.** Story config > **Endings**: **Id** (for `#ending:id`), **Title** and **Description**
(the ending card and the gallery), **Hint** (shown on a locked card), **Secret**, an **Image** and a
**Color**. The order is the gallery's order. Players see the gallery from the title (**Endings**).

### Test a second playthrough

- **In the game:** play to an ending, then play again (or **unlock all endings** in the debug menu).
  **Story: reset memory** (debug menu) forgets every playthrough. In the editor, **JamKit > Story >
  Forget All Playthroughs** deletes the memory and saves when not playing.
- **In Inky:** the memory functions have stand-ins at the top of StoryKit.ink. Set
  `VAR inky_playthrough = 2` and `VAR inky_last_ending = "last_train"` (also `inky_endings_seen`,
  `inky_times_quit`, `inky_loads`), play, then set them back. The game ignores them.

## Players' controls

| | Keyboard / mouse | Gamepad |
|---|---|---|
| Next line (a press while typing shows the whole line) | click, Space, Enter, E | (A), (X) |
| Choose | click, number keys, arrows + Enter | stick / d-pad + (A) |
| History (backlog) | L, mouse wheel up | (Y) |
| Auto (lines go on by themselves) | A | LB |
| Skip (read text only, unless Settings says otherwise) | S, or hold Ctrl | RB |
| Hide the text | H, right click | View / Select |
| Quick save / quick load | F5 / F9 | |
| Menu (save, load, history, settings) | Escape | Start |

The text box also has small buttons: Log, Auto, Skip, Save, Load, Q.Save, Q.Load, Menu.
**Settings** has Text speed (up to Instant), Auto-advance wait and "Skip also skips unread text".

## Saves

- Three slots, a quick save and an autosave (made at every new knot, every few lines, in a
  point-and-click room, and when the player quits). Save and Load are in the pause menu, the text
  box's buttons and (Load) the title screen. A save holds the ink state (variables, position,
  choices, inventory), the picture, the history and the line on screen.
- Files go next to the shell's save (`persistentDataPath`, PlayerPrefs in a browser):
  `save_story_memory.json` and `save_story_slot_*.json`.
- When a story is over, its autosave is deleted, so Continue only resumes unfinished stories.

## For programmers

- Pure rules (no Unity) are in `Packages/com.vibejam.jamkit/Runtime/Core/Story` (namespace
  `JamKit.Narrative`): `StoryCommand` and `StoryTags` (tag parsing, the tag list), `StageState`
  (what the stage shows, as data), `Backlog`, `StoryMemory`, `EndingsRegistry`, `StorySnapshot`,
  `StoryCondition` (the Show If language), `HotspotShape` / `HotspotGeometry` / `HotspotPicker`,
  `InkScan` and `StoryValidator`, `StoryLine`. `RichLine` (the markup) and the `Typewriter` are in
  `Core/Dialogue`. Tests: `dotnet test core-tests/Story/Story.Tests.csproj`, which also compiles the
  sample with the real ink compiler and plays it headless (every ending, the second playthrough).
- The Unity layer is `Templates/Story/Scripts`: `StoryGame` (split into `.Saves`, `.Hotspots`,
  `.Memory`, `.Debug`), `Stage/StoryStage` (UI Toolkit stage, `Tweens`), `UI/StoryTextView`,
  `HotspotLayer`, `InventoryBar`, `StoryScreens`, `StoryStorage`, `StoryBoot` (title screen),
  `StoryInkReloader` (editor-only hot reload), `Data/StoryConfig`, `HotspotScene`, `StoryValidation`.
- It runs on the shell's `DialogueService`, which grew the hooks for it: `UseView(IDialogueView)`,
  `Presenter` (an `IDialoguePresenter` sees every tag first, can hold lines back during transitions
  and keep a story waiting with `Park` / `Unpark`), `Resume` from a saved ink state, `HotSwap`,
  `AutoMode`, `SkipMode`, `SkipToChoice`, `JumpTo`, `InlineSpeakers`, `Palette`, and the events
  `StoryCreated` (bind your own ink functions), `LineShown`, `ChoiceMade`, `Parked`.
- From code: `StoryGame.Current.SaveTo("slot1")`, `.LoadNow(...)`, `.UseHotspot("door", "ticket")`,
  `.HasItem(...)`, `.GiveAllItems()`, `.JumpToKnot(...)`, `.PlayToEnding()` (a coroutine: first choices
  and the rooms' walkthroughs, for tests and the smoke run), `StoryStorage.Memory`.
- The smoke run (`-jamSmoke`) plays the story itself and logs what it did as `JAMKIT_SMOKE story ...`
  lines: each choice taken (in which knot), each hotspot room opened and hotspot used, and the ending
  reached, then a summary. No ending reached is an error, so a passing smoke run proves the story
  was played, not only the debug win.
- Editor: `Templates/Story/Editor`: `StoryArt` (placeholder art drawn in code), `StoryContent`
  (sample data, only what's missing), `StoryBuilder` (preset and scene; build step 220),
  `StoryTemplate` (Use Template; batch `-executeMethod JamKit.Narrative.EditorTools.StoryTemplate.UseFromCommandLine`),
  `StoryInkCheck` (menu, on-save check, build check). **JamKit > Story > Reset Content to Defaults**
  puts the config, characters and office back (the .ink files are never touched).
- Delete the whole `Templates/Story` folder to drop the template.

## Known limits

- One story file tree per game (the main .ink and its INCLUDEs).
- Characters are a base picture plus one expression layer (no separate outfits or animation).
- `#blur` and `#filter` use UI Toolkit filters; very old GPUs or a platform without them just show
  the picture unfiltered. They're off in batch mode (test runs, the smoke run), where nothing is drawn.
- The hotspot editor is the Inspector (numbers); there's no drawing tool. The debug menu's "show all
  hotspots" checks the shapes in play.
- Hot reload restarts the current knot: lines already shown in it are shown again.
- No voice acting support beyond the typing blips (an `#sfx` per line works for recorded lines).
