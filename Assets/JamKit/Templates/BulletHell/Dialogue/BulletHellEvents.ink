// Mystery rooms for the bullet-hell template. A Mystery room plays one knot listed in
// BulletHellConfig > Event Knots. Gameplay effects are #event tags the game understands:
//
//   #event:bh:coins:40     (or a negative number)    #event:bh:life:1      #event:bh:bomb:1
//   #event:bh:score:500    #event:bh:draft           #event:bh:draft:rare  (Rare or better)
//   #event:bh:pay:20       pay 20 coins, if the player has them, for a draft
//
// Flags (has_flag / set_flag) are kept in the save file, so the second visit can be different.
// Every shell tag works too (#speaker, #portrait, #sfx, #shake, #flash...). Play it in Inky.
//
// The STORY HOOKS (boss lines, the death screen, Rest rooms, run starts) are at the end of this file.

EXTERNAL has_flag(name)
EXTERNAL set_flag(name)
EXTERNAL clear_flag(name)
EXTERNAL run_count()
EXTERNAL current_floor()
EXTERNAL killer()

-> shrine

=== shrine ===
#speaker:Shrine #portrait:Guide hide #portrait:Rival hide
{ has_flag("bh_shrine_seen"):
    The quiet shrine again. The candles flicker as if they know you.
- else:
    A quiet shrine floats between the bullets. Candles burn without smoke.
}
~ set_flag("bh_shrine_seen")
* [Leave an offering (20 coins)]
    #event:bh:pay:20 #sfx:coin
    The candles flare. Something answers.
* [Pray for protection]
    #event:bh:bomb:1 #flash:0.2
    A warm light settles on your ship. (+1 bomb)
* [Walk on]
    You leave the shrine in peace.
- -> END

=== gambler ===
#speaker:Gambler #portrait:Rival smug right
{ has_flag("bh_met_gambler"):
    You again! Feeling lucky this time?
- else:
    Psst. Pilot. Fancy a little bet? Double or nothing.
}
~ set_flag("bh_met_gambler")
* [Bet 15 coins]
    #event:bh:coins:-15
    The coin spins...
    { RANDOM(1, 2) == 1:
        #event:bh:coins:40 #sfx:coin #shake:0.2
        Heads! You win 40 coins.
    - else:
        #portrait:Rival smug
        Tails. Better luck next time.
    }
* [No thanks]
    Suit yourself. #portrait:Rival neutral
- #portrait:Rival hide
-> END

=== medic ===
#speaker:Medic #portrait:Guide worried left
You look like you've been through a storm of bullets.
* [Patch me up]
    #portrait:Guide happy #event:bh:life:1 #sfx:powerup
    There. Good as new. (+1 life)
* [Teach me a trick]
    #portrait:Guide happy #event:bh:draft
    Here's something I picked up on the lower floors.
- #portrait:Guide hide
-> END

=== cache ===
#speaker: #portrait:Guide hide #portrait:Rival hide
A sealed supply cache drifts past, its lock blinking.
* [Shoot the lock]
    #shake:0.3 #event:bh:coins:25 #sfx:coin
    It bursts open: 25 coins spill out.
* [Pry it open carefully]
    #event:bh:score:1500
    Inside: a data chip worth 1500 points.
- -> END


// =====================================================================================
// STORY HOOKS. The game looks these knots up BY NAME, most specific first, and plays the
// first one that exists. A missing knot is fine: nothing plays, no error. So you add a line
// by adding a knot. Full list: docs/TEMPLATE-BULLETHELL.md, "Story hooks".
//
//   run_start_<run>      -> run_start                  a run begins (run 1, 2, 3...)
//   rest_floor_<floor>   -> rest                       before a Rest room's choice
//   boss_<id>_intro      -> boss_intro                 before a boss fight
//   boss_<id>_phase_<n>  -> boss_<id>_phase -> boss_phase   phase n starts (2, 3...)
//   boss_<id>_defeat     -> boss_defeat                it falls
//   death_<killer>       -> death_floor_<floor> -> death   the Game Over screen's line
//
// Boss ids: gatekeeper, forgemother, the_last_light (or a boss's Story Id field).
// killer = a boss id in a boss fight, else "elite" or "battle".
// Functions: run_count() (1 on the first run), current_floor(), killer(), has_flag(...).
// Boss and run-start lines stop the game while they show; boss lines clear the bullets.
// The death knot is not a dialogue: only its FIRST line of text shows, on the Game Over screen.
//
// EVERYTHING BELOW IS PLACEHOLDER TEXT, there only to show each hook working.
// Replace it with your story (keep the knot names), or delete a knot to switch that hook off.
// =====================================================================================

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

=== rest ===
// PLACEHOLDER: the companion's line before the Rest room's choice.
#speaker:Companion #portrait:Guide neutral left
(Placeholder) Catch your breath. Floor {current_floor()} of 3.
#portrait:Guide hide
-> END

=== boss_gatekeeper_intro ===
// PLACEHOLDER: what this boss wants, before the fight.
#speaker:Gatekeeper #portrait:Guide hide #portrait:Rival hide
(Placeholder) None shall pass this gate.
-> END

=== boss_gatekeeper_phase_2 ===
// PLACEHOLDER: the boss gets serious (its second phase).
#speaker:Gatekeeper
(Placeholder) You're stronger than the others.
-> END

=== boss_gatekeeper_defeat ===
// PLACEHOLDER: last words.
#speaker:Gatekeeper
(Placeholder) Then go. The gate is yours.
-> END

=== death ===
// PLACEHOLDER: the Game Over screen's line. Inputs: killer(), current_floor(), run_count(), flags.
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


// Fallbacks, used only when the story runs outside the game (Inky).
=== function has_flag(name) ===
~ return false

=== function set_flag(name) ===
~ return

=== function clear_flag(name) ===
~ return

=== function run_count() ===
~ return 1

=== function current_floor() ===
~ return 1

=== function killer() ===
~ return "battle"
