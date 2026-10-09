// Visiting Hours: the vertical slice's lines (placeholder writing in the creator's direction:
// docs/SLICE-VISITING-HOURS.md; the creator rewrites them). Play it in Inky: the fallback
// functions at the bottom stand in for the journal.
//
// The game listens for these tags (VisitingHoursGame):
//   #event:vh:record:<entry id>     Nadia writes the entry in her journal (a toast says so)
//   #event:vh:open_journal          the journal opens (the first time, Mags's tutorial)
//   #event:vh:questions             the journal opens on its open questions
//   #event:vh:to_booth              11 PM: the sleep-research wing
//   #event:vh:dive                  into the dream
//   #event:vh:battle                the argument starts (after the knot ends)
// Shell tags work too: #speaker, #portrait:Walt sad left, #sfx, #wait, #auto, #thought, #shake.
// Portraits: Mags (neutral), Walt (neutral, sad, firm).

EXTERNAL knows(id)
EXTERNAL open_questions()

-> day1_intro

// ---------- Day 1: St. Brigid's, six weeks later ----------

=== day1_intro ===
#speaker:Nadia #portrait:Mags hide #portrait:Walt hide
<i>Six weeks.</i> #thought #auto:2.2
<i>Room 14 is at the end of the corridor. It always is.</i> #thought #auto:2.6
-> END

=== examine_chart ===
#speaker:Nadia
The chart hangs off the foot of the bed. My handwriting, from last night.
<i>Charting: GCS 6. No sedation. Scans normal.</i> #thought
<i>He just won't wake up.</i> #thought #event:vh:record:fact_chart #sfx:page
-> END

=== examine_effects ===
#speaker:Nadia
His effects bag. "VOSS, E." in marker, in somebody else's writing.
A keyring. The tag says "W.A. workshop". I haven't seen that tag since we were kids.
And a card, folded small. <i>Walter Abernathy, 1941–2015.</i> #event:vh:record:fact_effects #sfx:page
<i>He kept it. All this time.</i> #thought
-> END

=== examine_police_report ===
#speaker:Nadia
An envelope at the station, for the family. They mean me. I'm the family now.
#event:vh:record:fact_police_report #sfx:page
I read it standing up.
-> END

=== examine_photo ===
#speaker:Nadia
The photo from his bedside table. Port Ellis, the summer before last.
Lena's laughing at something off-camera. Theo's laughing so hard he's a blur. #event:vh:record:fact_photo #sfx:page
-> END

=== examine_elias ===
#speaker:Nadia
{knows("fact_chart"):
    <i>Hi, Eli.</i> #thought
    The monitor keeps time for both of us.
- else:
    <i>Hi, Eli.</i> #thought
    <i>Chart first. Then you can talk to him.</i> #thought
}
-> END

=== examine_station ===
#speaker:Nadia
The nurses' station. Somebody's left the radio on low.
-> END

=== stairwell ===
#speaker:Nadia
{not knows("fact_chart"):
    <i>Not yet. I haven't even charted him.</i> #thought
    -> END
}
Stairwell B. The sleep-research wing is two floors down. It closes at nine.
<i>Mags said eleven.</i> #thought
* [Go down to the sleep wing]
    #event:vh:to_booth
    -> END
* [Not yet]
    -> END

// ---------- 11 PM: the closed sleep-research wing ----------

=== booth_mags ===
#speaker:Mags #portrait:Mags neutral right
You came. Good. Shut the door, the corridor lights bleed in.
#speaker:Nadia
Is this it? It looks like a dentist's chair with a hangover.
#speaker:Mags
Two hundred and twelve dives in that chair. It's very comfortable. Lie back.
Here's the rig, short version. You go under, you'll be standing in his dream. It'll feel like walking. It is walking.
Same as out here: walk with WASD or the stick, look with the mouse. Shift if you need to hurry. You won't need to hurry.
E to look at something properly. Your hands will work. Mostly.
#speaker:Nadia
And how do I find him?
#speaker:Mags
You don't find him. You find out where he is. That's what your notebook's for.
Everything you write down out here comes in with you. Facts. The things you remember in there. Put two together and sometimes they make a third.
Show me the notebook.
#event:vh:open_journal
-> END

=== booth_after_journal ===
#speaker:Mags #portrait:Mags neutral right
Good. J or Tab opens it in there too. Pick two things, see if they fit.
And when somebody in there argues with you, and they will, use what you've worked out. You can't say what you don't know.
#speaker:Nadia
How many of the two hundred and twelve went wrong?
#speaker:Mags
Nine.
...Let's not do that tonight.
Count back from ten. I'll be right here.
#event:vh:dive
-> END

// ---------- Night 1: Walt's street (the dream) ----------

=== night_arrive ===
#speaker:Nadia #portrait:Mags hide
<i>Oh.</i> #thought #auto:1.6
<i>It's Saturday on Larch Street.</i> #thought #auto:2.4
#speaker:Mags
I can see you. You're in. Walk.
-> END

=== examine_newspaper ===
#speaker:Nadia
The Courier, still in its plastic on somebody's porch.
Sunday, 23 August. #event:vh:record:mem_newspaper #sfx:page
<i>That morning.</i> #thought
-> END

=== examine_shop_sign ===
#speaker:Nadia
Abernathy Cycles, since 1971. The garage door's up. It smells of oil and cut grass. #event:vh:record:mem_shop_sign #sfx:page
<i>Eli spent every summer in there.</i> #thought
-> END

=== examine_roses ===
#speaker:Mrs. Lindqvist
Hello, dear! Isn't it a lovely Saturday?
#speaker:Nadia
Is it Saturday?
#speaker:Mrs. Lindqvist
It's always Saturday on Larch Street! #event:vh:record:mem_saturday #sfx:page
-> END

=== examine_grill ===
#speaker:Mr. Okafor
Burgers in five. Radio says it's the hottest Saturday of the year.
The radio: "...and that's your Saturday afternoon, folks, so stay cool out there..." #event:vh:record:mem_saturday #sfx:page
-> END

=== examine_dog ===
#speaker:Nadia
Biscuit. Walt's dog. He's been dead longer than Walt has.
He doesn't know that either.
-> END

=== walt_wave ===
#speaker:Nadia
Mr. Abernathy is at the grill, waving at me with the tongs. #event:vh:record:mem_walt_alive #sfx:page
<i>Exactly like when we were kids.</i> #thought
-> END

=== walt_rushing ===
#speaker:Nadia #portrait:Walt hide
<i>I don't know enough yet.</i> #thought #event:vh:questions_hint
* [Go up to him anyway]
    -> walt_intro
* [Not yet]
    #event:vh:questions
    -> END

=== walt_intro ===
#speaker:Walt #portrait:Walt neutral left
Nadia Voss. Look at you, all grown up.
You've come to take him home.
#speaker:Nadia
Walt, I...
#speaker:Walt #portrait:Walt firm left
Easy. Watch the pattern first.
#event:vh:battle
-> END

// ---------- After the argument ----------

=== walt_convinced ===
#speaker:Walt #portrait:Walt sad left
He brings me back because I'm the one who told him you can fix anything. #event:vh:record:truth_walt #sfx:chime
I said it when he was nine, over a bike with a bent wheel. He believed me.
...I was wrong, Nadia. Not about the bike.
Go on. He's down at the house. Mind how you go.
-> END

=== walt_heard ===
#speaker:Walt #portrait:Walt neutral left
You listened. Most people don't.
Here. Hold out your hand. #event:vh:record:pers_wrench #sfx:item
For the next thing that needs fixing.
#portrait:Walt sad left
Go on, then. Gently.
-> END

=== walt_pressed ===
#speaker:Walt #portrait:Walt firm left
Fine. Push, then. That's how you strip a thread, you know.
#portrait:Walt sad left
He steps aside. He won't look at me.
-> END

=== walt_lost ===
#speaker:Nadia #portrait:Walt hide
<i>I can't... I can't breathe...</i> #thought #shake:0.4
#speaker:Mags
Nadia. Nadia, I've got you. Coming up. Now.
-> END

=== slice_end ===
#speaker:Mags #portrait:Walt hide
Nadia. Come back up. That's it.
...There you are. Drink this.
#speaker:Nadia
<i>The street's still on my clothes. Cut grass and charcoal.</i> #thought
-> END

// Fallbacks, used only when the story runs outside the game (Inky).
=== function knows(id) ===
~ return false

=== function open_questions() ===
~ return 0
