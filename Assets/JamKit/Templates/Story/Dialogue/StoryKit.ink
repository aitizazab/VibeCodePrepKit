// =====================================================================
// StoryKit.ink: the story template's toolkit. INCLUDE it at the top of your main file.
// You don't need to edit this file. It declares the game's memory functions and gives each one
// a stand-in ("fallback") so the story also plays in Inky, where the game isn't running.
//
// TESTING A SECOND PLAYTHROUGH IN INKY: change the inky_ values below (for example
// inky_playthrough = 2 and inky_last_ending = "last_train"), play, then set them back.
// The game itself ignores these: it uses its real memory.
// =====================================================================

VAR inky_playthrough = 1
VAR inky_endings_seen = 0
VAR inky_last_ending = ""
VAR inky_times_quit = 0
VAR inky_loads = 0

// What the player carries (ink LIST items, see LIST Items in your main file).
VAR inventory = ()
// The item last used on a hotspot ("" when the hotspot was just clicked).
VAR used_item = ""

// ---------- Memory that survives restarts (the game remembers past playthroughs) ----------
EXTERNAL playthrough_count()
EXTERNAL endings_seen()
EXTERNAL seen_ending(id)
EXTERNAL last_ending()
EXTERNAL remember(key, value)
EXTERNAL recall(key)
EXTERNAL knows(key)
EXTERNAL visited_ever(knot)
EXTERNAL times_quit()
EXTERNAL loads()
EXTERNAL rewinds()
EXTERNAL was_reloaded()
// The shell's flags: also kept forever.
EXTERNAL has_flag(name)
EXTERNAL set_flag(name)
EXTERNAL clear_flag(name)

// ---------- Inventory helpers ----------
// ~ give(brass_key)        {has(brass_key): ...}        ~ take(brass_key)

=== function give(item)
~ inventory += item

=== function take(item)
~ inventory -= item

=== function has(item)
~ return inventory ? item

// ---------- Stand-ins for Inky (the game replaces all of these) ----------

=== function playthrough_count()
~ return inky_playthrough

=== function endings_seen()
~ return inky_endings_seen

=== function seen_ending(id)
~ return inky_last_ending == id

=== function last_ending()
~ return inky_last_ending

=== function remember(key, value)
~ return

=== function recall(key)
~ return 0

=== function knows(key)
~ return false

=== function visited_ever(knot)
~ return inky_playthrough > 1

=== function times_quit()
~ return inky_times_quit

=== function loads()
~ return inky_loads

=== function rewinds()
~ return 0

=== function was_reloaded()
~ return inky_loads > 0

=== function has_flag(name)
~ return false

=== function set_flag(name)
~ return

=== function clear_flag(name)
~ return
