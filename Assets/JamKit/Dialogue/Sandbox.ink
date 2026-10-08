// The sandbox's dialogue: an example of every tag and function the JamKit shell understands.
// Play it on its own in Inky (the fallback functions below stand in for the game's save data).
//
//   #speaker:Name                        name in the box (stays until changed; "#speaker:" clears it)
//   #portrait:Name expression left|right  show a character (expression optional); "#portrait:Name hide"
//   #sfx:id   #music:id (or stop)        sounds by id from the Audio Registry
//   #shake:0.4   #flash   #wait:0.5      screen shake (0..1), white flash, pause before the line
//   #event:name                          game code hears GameRoot.Dialogue.EventRaised("name")
//   has_flag("x") / set_flag("x") / clear_flag("x")   flags kept in the save file

EXTERNAL has_flag(name)
EXTERNAL set_flag(name)
EXTERNAL clear_flag(name)

-> intro

=== intro ===
#speaker:Guide #portrait:Guide happy left
{ has_flag("met_guide"):
    Oh, you're back! Same deal as last time. #sfx:ui_select
- else:
    Welcome to the JamKit sandbox! #sfx:ui_select
}
This tiny game checks that every part of the shell works.
#speaker:Rival #portrait:Rival smug right
Three coins. One red block. Try not to touch it. #shake:0.4
#speaker:Guide #portrait:Guide worried
Ready to go?
* [Ready!]
    #speaker:Guide #portrait:Guide happy
    Go get them! #flash
* [Wait, how do I play?]
    #speaker:Guide
    Move with WASD, the arrow keys or the left stick. Space or (A) dashes.
    Escape or Start pauses the game. #wait:0.3
    #portrait:Guide happy
    That's all. Go get them!
- (after_choice)
~ set_flag("met_guide")
#event:intro_done
#portrait:Rival hide
#speaker:Rival
Hmph. Good luck.
-> END

=== sign ===
#speaker:Sign #portrait:Guide hide #portrait:Rival hide
The sign says: "Coins: 3. Red blocks: 1. Patience: optional."
-> END

// Fallbacks, used only when the story runs outside the game (Inky).
=== function has_flag(name) ===
~ return false

=== function set_flag(name) ===
~ return

=== function clear_flag(name) ===
~ return
