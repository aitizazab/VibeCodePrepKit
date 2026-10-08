// =====================================================================
// LAST TRAIN HOME: the endings. PLACEHOLDER PROSE.
//
// Each ending has an #ending:id tag (the id is in the story config's Endings list, which gives it
// a title for the ending card and the gallery) and ends with -> END. The game then shows the
// ending card, the victory screen and the credits, and remembers the ending for next time.
// "Return Ticket" is only offered on a second playthrough (see deja_vu in LastTrain.ink).
// =====================================================================

=== ending_last_train ===
#music:music_story_sad 3
You step aboard. The doors close between you like a page being turned. #sfx:door_knock
#scene:train_interior fade 1.5
#ambience:amb_hum 1
Through the window, Iris lifts one hand. Then the rain fills in the place where she was standing.
The carriage is warm. On the seat opposite, the forgotten umbrella has a name tag tied to its handle. #thought
It says <i>Iris</i>. #wait:0.6
You get off at Wexley a little after one. Nobody is waiting there for anyone. #ending:last_train
#stopmusic:3
#stopambience:3
-> END

=== ending_missed_connection ===
#music:music_story_warm 3
You let the doors close. The 23:52 sighs, and leaves without you. #sfx:train_pass
#hide:iris fade
#cg:cg_umbrella fade 2
Iris: You'll catch the next one. #show:iris smile
"There isn't a next one."
Iris: Then I suppose you'll have to walk. <pause=0.4>I'll walk with you. I've been meaning to stretch my legs for... oh, years. #show:iris smile
#scene:platform_dawn fade 3
#ambience:amb_wind 2
You talk the whole way. About Wexley, and the person who said they'd wait, and the crossword in the waiting room that nobody will ever finish.
When the sun comes up she is still beside you. Her coat is wet, at last. She looks delighted about it. #ending:missed_connection
-> END

=== ending_lost_property ===
#music:music_story_warm 3
You press the ticket into her hand. "I'll get the next one."
Iris: There isn't a next one. #show:iris surprised
"Then I'll walk." #wait:0.3
She looks at the ticket, and at you, and at the photograph in your other hand, for a long moment. #show:iris sad
Iris: Keep that. Somebody should remember us. #show:iris smile
#hide:iris fade 1.5
The doors close. Two red lights shrink into the rain, the way they always do. #sfx:train_pass
#cg:cg_photo fade 2
#filter:sepia 0.5 2
On the way home you look at the photograph again. #flash:white 0.6
It shows a platform you don't recognise, a sign that says WEXLEY, and two people squinting into the sun. Both of them are smiling. #ending:lost_property
#filter:none 2
-> END

// The secret ending: only reachable on a second playthrough. It breaks the fourth wall on purpose.
=== ending_return_ticket ===
#stopmusic:2
#show:iris knowing center
Iris: Then let's change the ending. #glitch:0.6 0.8
Iris: Hold on. This part might feel strange.
#meta:popup Iris would like to leave the game. Is that all right?
#meta:fakecrash 2.5
#fade:out white 1.5
#ambience:amb_wind 2
#scene:street_rain cut
#fade:in 2
#music:music_story_warm 3
You are standing in a street you have never seen, at the top of the station steps. The rain is thinning.
Iris: Oh. Oh, look at that. There's a <i>street</i>. #show:iris surprised left
Iris: I never once came up the steps. It never occurred to me that I could. #show:iris smile
~ set_flag("iris_left_the_station")
#meta:title Next Train Home
#meta:tagline Thank you for coming back for her.
#meta:playlabel Visit the station
Iris: Will you remember me? Next time you start this all again? #show:iris knowing
* ["I'll remember."]
    ~ remember("promise", true)
    Iris: Then that's enough. That's more than enough. #show:iris smile
* ["Will you remember me?"]
    ~ remember("promise", false)
    Iris: I think I already do. #show:iris smile
- #scene:platform_dawn fade 3
You walk her as far as the end of the street, where the morning starts. #ending:return_ticket
-> END
