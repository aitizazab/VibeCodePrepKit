// Mystery rooms for the bullet-hell template. A Mystery room plays one knot listed in
// BulletHellConfig > Event Knots. Gameplay effects are #event tags the game understands:
//
//   #event:bh:coins:40     (or a negative number)    #event:bh:life:1      #event:bh:bomb:1
//   #event:bh:score:500    #event:bh:draft           #event:bh:draft:rare  (Rare or better)
//   #event:bh:pay:20       pay 20 coins, if the player has them, for a draft
//
// Flags (has_flag / set_flag) are kept in the save file, so the second visit can be different.
// Every shell tag works too (#speaker, #portrait, #sfx, #shake, #flash...). Play it in Inky.

EXTERNAL has_flag(name)
EXTERNAL set_flag(name)
EXTERNAL clear_flag(name)

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

// Fallbacks, used only when the story runs outside the game (Inky).
=== function has_flag(name) ===
~ return false

=== function set_flag(name) ===
~ return

=== function clear_flag(name) ===
~ return
