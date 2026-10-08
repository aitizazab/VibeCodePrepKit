// =====================================================================
// LAST TRAIN HOME: the story template's sample game (main file).
//
// PLACEHOLDER PROSE. Every line of story in these files is a stand-in written to show the
// template's features. Replace it with your own writing; keep or delete the tags as you need.
// Files:  LastTrain.ink (this: prologue, platform, waiting room, the train),
//         LastTrain_Office.ink (the lost property office: a point-and-click scene),
//         LastTrain_Endings.ink (four endings, one only on a second playthrough),
//         StoryKit.ink (the toolkit: memory functions and inventory helpers; don't edit).
// Tags are explained in docs/TEMPLATE-STORY.md. Play this file in Inky as it is.
// =====================================================================

INCLUDE StoryKit.ink
INCLUDE LastTrain_Office.ink
INCLUDE LastTrain_Endings.ink

LIST Items = brass_key, ticket, photograph

VAR trust = 0
VAR helped = false
VAR saw_photo = false
// What the player said first last time (read before this playthrough overwrites it).
VAR last_first_words = ""

-> prologue

// ---------------------------------------------------------------------
// Full-screen text (#nvl) suits a page of prose. #clear starts a new page.
=== prologue ===
#scene:black cut
#music:music_story_calm 3
#ambience:amb_rain 2
#nvl
The last train out of Hollin Cross leaves at eight minutes to midnight.
Everybody in town knows it, the way you know where the stairs creak in your own house.
Hollin Cross has one cinema, two churches and a railway station that time seems to have misplaced.
Nobody ever seems to catch it. They mean to. They run for it, coats flapping, and then they are standing on the platform watching two red lights shrink into the rain.
{playthrough_count() > 1: <i>You have the strange feeling you have read this page before.</i>}
#clear
You have a bag, a ticket in your coat pocket, and a reason for leaving that you would rather not think about tonight.
Tonight you are not going to miss it.<pause=0.6> Tonight you are early.
#adv
-> platform

// ---------------------------------------------------------------------
=== platform ===
#chapter:One - The Last Platform
#scene:platform_night fade 2
You come down the steps two at a time and shake the rain off your sleeves. #sfx:footsteps
The board above the platform flickers: <b>23:52 WEXLEY</b>. Twelve minutes.
#show:iris neutral right fade
There is one other person here. A woman in a yellow raincoat, sitting on a battered suitcase as if she has been waiting a long, long time.
{knows("first_words"):
    ~ last_first_words = recall("first_words")
}
Rain drums on the canopy. The platform lamp hums to itself, like someone trying to remember a song.
{
- playthrough_count() > 1 && times_quit() > 0:
    Iris: Oh. You came back. #show:iris surprised
    Iris: You left so suddenly last time. One moment you were here, and then... nothing. Like someone switched you off. #show:iris sad
- playthrough_count() > 1:
    Iris: You're early this time. #show:iris knowing
- else:
    Iris: Is it nearly midnight? #show:iris neutral
}
* ["Twelve minutes to go."]
    ~ remember("first_words", "the time")
    "Twelve minutes," you say. "If it's on time."
    Iris: It's never on time. That's the one thing you can count on. #show:iris smile
    ~ trust += 1
* ["Are you all right?"]
    ~ remember("first_words", "a kindness")
    "Are you all right? You look frozen."
    Iris: That's kind. Nobody asks that any more. #show:iris smile
    ~ trust += 2
* [Say nothing. Check the board again.]
    ~ remember("first_words", "nothing at all")
    You look at the board. The board looks back at you. #thought
    Iris: Not a talker. That's all right. I've done enough talking to rain to last me. #show:iris neutral
* {playthrough_count() > 1 || endings_seen() > 0} ["We've done this before, haven't we?"]
    -> deja_vu
- (after_greeting)
#sfx:chime
#shake:0.2
Bram: <i>Attention please.</i> The twenty-three fifty-two to Wexley is delayed by approximately... twenty minutes. #wait:0.4
Bram: Due to, uh. Leaves on the line. In October. As usual.
The intercom clicks off with the sound of a man putting down a very large mug.
#show:iris sad
Iris: Twenty minutes. That's all right. It doesn't matter anyway.
"Why not?"
Iris: I've lost my ticket. Somewhere between the waiting room and here, it just... went. #show:iris sad
Iris: Bram says the lost property office is locked for the night, but it isn't. It never is. I just can't make myself go in there on my own.
* [Offer to help her look.]
    ~ helped = true
    ~ trust += 1
    "I'll come with you. Twenty minutes is a long time to stand in the rain."
    Iris: You'd do that? <pause=0.3>For a stranger? #show:iris surprised
    Iris: All right. Thank you. #show:iris smile
* [Tell her to ask Bram.]
    "Can't the stationmaster sort it out?"
    Iris: Bram? Bram has been asleep in that box since nineteen... a long time. #show:iris knowing
    Iris: I'll go on my own, then. Unless you're coming? #show:iris neutral
    She doesn't wait for an answer. You find yourself following anyway. #thought
- -> waiting_room

// ---------------------------------------------------------------------
=== waiting_room ===
#scene:waiting_room black
#ambience:amb_hum 2
#show:iris neutral left
The waiting room is warmer than it has any right to be. A radiator ticks. Somebody has left a newspaper folded to the crossword, three answers filled in, all wrong.
Iris: The office is through there. #move:iris center 0.8
She points at a door with a frosted window. The words <b>LOST PROPERTY</b> are painted on it in letters that were gold once.
{playthrough_count() > 1 && last_first_words != "": Iris: Last time, the first thing you gave me was {last_first_words}. Funny what sticks. #show:iris knowing}
Iris: I'll wait here. I'm not good with that room. Too many things nobody came back for. #show:iris sad
She sits on the very edge of the bench, the way people sit when they expect to be asked to move. #thought
* ["What does the ticket look like?"]
    Iris: Brown card. Wexley, single. My name on the back in pencil. #show:iris neutral
    Iris: Bram keeps the important things locked in his desk. The key's never far from the desk. He's a creature of habit.
* ["Why Wexley?"]
    Iris: Someone's meeting me there. They said they'd wait. #show:iris smile
    Iris: They said that a while ago, now. #show:iris sad #wait:0.3
* ["Back in a minute."]
    Iris: Take your time. I'm very good at waiting. #show:iris smile
- #hide:iris fade
-> office

// ---------------------------------------------------------------------
// Back from the office (the office knots end with #leave and come here).
=== waiting_room_return ===
#scene:waiting_room fade
#show:iris neutral center
#stopambience:2
Iris: You found it? #show:iris surprised
You hold up the brown card. WEXLEY, SINGLE. On the back, in pencil, faded almost to nothing: <i>Iris</i>.
Iris: Oh. <pause=0.5>Oh, I could kiss you. I won't. But I could. #show:iris smile
~ trust += 1
{has(photograph): -> the_photograph}
-> bram_visits

= the_photograph
You still have the photograph from the shelf. You aren't sure why you took it.
* [Show her the photograph.]
    ~ saw_photo = true
    #cg:cg_photo fade 1.5
    #filter:sepia 0.9 1.5
    #blur:2 1.5
    #music:music_story_sad 3
    Two people on this same platform, squinting into the sun. One of them wears a yellow raincoat.
    The date on the back says <c=accent>14 October 1958</c>. #wait:0.5
    Iris: Where did you find that? #show:iris sad
    Iris: I was waiting for him. He said he'd be on the next train. I said I'd be right here. #auto:3
    Iris: And I was. I am. That's the trouble with being reliable. #show:iris sad
    #cg:off fade 1.5
    #filter:none 1.5
    #blur:0 1.5
    You look at her properly for the first time. The rain has not touched her coat. Not one drop. #thought
* [Keep it in your pocket.]
    Some things you don't hand back without asking first. You leave it where it is. #thought
- -> bram_visits

// ---------------------------------------------------------------------
=== bram_visits ===
#sfx:door_knock
The outer door bangs open and lets in a gust of rain and a large man in a wet uniform. #shake:0.3
#move:iris right 0.6
#show:bram neutral left slide
Bram: Evening. Morning. Whichever it is. #show:bram stern
Bram: You'll be for the twenty-three fifty-two, then. Don't get your hopes up.
Iris doesn't look at him. He doesn't look at her either. Not once. #thought
* ["She's been waiting for her ticket."]
    Bram: Who has? #show:bram stern
    You glance at Iris. She gives you a small, tired shake of the head. #show:iris sad
    Bram: Talking to yourself is the first sign, you know. Second sign is talking to the timetable. I'm on the third sign. #show:bram laugh
* ["How long have you worked here?"]
    Bram: Forty-one years in March. #show:bram neutral
    Bram: My father had it before me. He used to say this station keeps things. Umbrellas. Gloves. People, if you're not careful. #show:bram stern
    Iris: He's not wrong. #show:iris knowing
- Bram: Right. Kettle. #show:bram laugh
#hide:bram slide
He goes back out the way he came in, humming something that might once have been a song.
-> train_arrives

// ---------------------------------------------------------------------
=== train_arrives ===
#chapter:Two - The 23:52
#scene:platform_night fade 1.5
#show:iris neutral right
#music:music_story_tense 2
#ambience:amb_rain 1
{ was_reloaded():
    -> noticed_reload ->
}
A light grows at the end of the tunnel, low and yellow, and the rails begin to sing. #sfx:train_horn
#cg:cg_train flash 0.4
#shake:0.6 1.5
#sfx:train_pass
The 23:52 to Wexley pulls in only twenty-one minutes late, which in Hollin Cross is practically early.
#cg:off fade 1
Iris: Well. That's yours, then. #show:iris smile
{trust >= 3: Iris: It was nice. Being looked after for a bit. Thank you. #show:iris smile}
The doors hiss open. A warm yellow carriage, empty except for one forgotten umbrella.
* [Get on the train.]
    -> ending_last_train
* [Stay on the platform with her.]
    -> ending_missed_connection
* {has(photograph) && saw_photo} [Give her your ticket.]
    -> ending_lost_property

// A tunnel: the story steps in here and comes back (->->) when it's done.
= noticed_reload
#glitch:0.5 0.6
Iris: Did you... go back, just now? #show:iris surprised
Iris: The floor tilted. Like a record skipping. {rewinds() > 0: You went back further than you meant to, didn't you?}
Iris: Never mind. I'm imagining things. I do that. #show:iris neutral
->->

// ---------------------------------------------------------------------
// Only offered on a second playthrough (or after any ending): see the platform's last choice.
=== deja_vu ===
#music:music_story_sad 2
#show:iris knowing center
Iris: <speed=0.6>Yes.</speed> <pause=0.5>We have.
{last_ending() != "": Iris: Last time, it ended with {last_ending_title()}. Do you remember?}
#glitch:0.4 0.8
Iris: Every night the same platform, the same rain, the same twelve minutes. And every night somebody comes down those steps.
Iris: Most of them never notice. You did.
{times_quit() > 0: Iris: You even tried leaving in the middle once. Just... closed your eyes on me. It didn't stick. #show:iris sad}
* ["Then let's change the ending."]
    -> ending_return_ticket
* ["I don't know what you mean."]
    Iris: No. Of course not. Forget I said anything. #show:iris sad
    #glitch:0.2 0.3
    -> platform.after_greeting

=== function last_ending_title()
{ last_ending():
- "last_train":
    ~ return "you on the train, and me on the platform"
- "missed_connection":
    ~ return "the two of us walking home in the rain"
- "lost_property":
    ~ return "me on the train, and you holding a photograph"
- else:
    ~ return "something I can't quite remember"
}
