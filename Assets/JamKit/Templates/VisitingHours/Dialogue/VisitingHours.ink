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
// PLACEHOLDER (agent-written, 2026-10-12): Rafael's one-line handover (docs/NIGHTS-1-2.md, Day 1).
#speaker:Rafael
Evening. Room 14's yours: obs due, the nine o'clock drip's up. He's had a quiet day.
#speaker:Nadia
<i>Wristband, then his vitals. Then the chart.</i> #thought #auto:2.6
-> END

// PLACEHOLDER (agent-written): Rafael, if she talks to him again.
=== day1_rafael ===
#speaker:Rafael
You don't have to take him every night, you know. I can swap.
#speaker:Nadia
I know. Thanks, Raf.
-> END

// PLACEHOLDER (agent-written): the tutorial duty round, three steps at his bed.
=== duty_wristband ===
#speaker:Nadia
His wristband. LASNAME, ELIAS. Date of birth, hospital number. Same as the chart.
<i>Right patient. It's always the right patient.</i> #thought
-> END

=== duty_vitals ===
#speaker:Nadia
Pulse sixty-two. Pressure one-eighteen over seventy-four. Sats ninety-eight.
<i>Everything a well man has.</i> #thought
-> END

// PLACEHOLDER (agent-written): LeBaget's bouquet.
=== examine_bouquet ===
#speaker:Nadia
Lilies on the windowsill. Too many for the sill. #event:vh:record:fact_bouquet #sfx:page
The card: <i>With our deepest sympathies. LeBaget, Community Partnerships.</i>
<i>Who sends lilies to a man who isn't dead?</i> #thought
-> END

// PLACEHOLDER (agent-written): her phone ("Not now." is the plan's).
=== day1_phone ===
#speaker:Nadia
My phone, face down on the desk. It buzzes.
<i>Not now.</i> #thought
-> END

=== examine_chart ===
#speaker:Nadia
The chart hangs off the foot of the bed. My handwriting, from last night.
<i>Charting: GCS 6. No sedation. Scans normal.</i> #thought
<i>He just won't wake up.</i> #thought #event:vh:record:fact_chart #sfx:page
-> END

=== examine_effects ===
#speaker:Nadia
His effects bag. "LASNAME, E." in marker, in somebody else's writing.
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
// The creator's scene (docs/STORY-PROSE.md 83-92), word for word: the opening stage direction as Nadia's thought, first
// person (the creator: "change the writing to the correct tense or POV"); two spellings fixed (particularly, irreversible).
// The phone-charging ritual is dropped (the creator rewrote the scene without it).
#speaker:Nadia #portrait:Mags neutral right
<i>I close the door behind me. Mags turns at the noise, and her face softens when she sees it's me.</i> #thought
#speaker:Mags
Dream Walking isn't that easy, you know. Hard to tell apart reality, and their perception of it. And when you try to get him out, well...
There's a reason he's still under. It won't be easy. But he's your brother; I know you'd do anything for him.
#speaker:Nadia
Only way I'd have it. You gonna follow me in?
#speaker:Mags
Goodness, no. The Dream is tethered within his consciousness, and the more psyches invading his mind, the less stable he becomes.
I'd give you Five Nights, maybe Six before it's too dangerous to continue, just on your own. Add to that the fact I'm not someone he doesn't have such a strong tie to, like you do... No point discussing it.
And remember, inside the Dream, only the Truth is on your side. His consciousness can conjure up falsehoods and convincing manipulations, if his memories are particularly vivid, but your only bastion to fight with is the Truth.
Enter prepared, or you'll be wasting our time. Regardless, get comfortable, we have a Long Night ahead of us.
And whatever happens in there, stay calm. Undue distress could cause irreversible damage to Elias' wellbeing. No pressure.
// PLACEHOLDER (agent-written): the journal's introduction (the game's tutorial for it).
Your notebook. Show me before you go under.
#event:vh:open_journal
-> END

=== booth_after_journal ===
#speaker:Mags #portrait:Mags neutral right
// PLACEHOLDER (agent-written).
J or Tab opens it in there too. Pick two things, see if they fit.
Count back from ten. I'll be right here.
#event:vh:dive
-> END

// ---------- Day 2 (the chain, docs/NIGHTS-1-2.md): all PLACEHOLDER, agent-written ----------

=== day2_intro ===
#speaker:Nadia #portrait:Mags hide #portrait:Walt hide
<i>Day two. Four hours' sleep.</i> #thought #auto:2.2
<i>Room 14. Then the stairs.</i> #thought #auto:2.2
-> END

=== day2_rafael ===
#speaker:Rafael
I was on when they brought him in. Did anyone tell you?
He said one thing before they put him under. "Did Lena get out?" #event:vh:record:fact_did_lena #sfx:page
#speaker:Nadia
<i>Did she?</i> #thought
-> END

=== day2_visitor ===
#speaker:Visitor
Ms. Lasname? I'm from LeBaget. Community Partnerships. We were so sorry to hear.
I brought fruit. I wasn't sure what he's allowed.
Has he said anything? #event:vh:record:fact_visitor #sfx:page
#speaker:Nadia
He's in a coma.
#speaker:Visitor
Of course. Of course he is.
-> END

=== day2_wristband ===
#speaker:Nadia
Wristband. Same name, same number.
-> END

=== day2_round ===
#speaker:Nadia
The drip's due. New bag, same rate. The line's clean.
-> END

=== day2_vitals ===
#speaker:Nadia
Pulse sixty. Pressure one-sixteen over seventy-two. Sats ninety-eight.
<i>Everything's the same as yesterday.</i> #thought
-> END

=== day2_sit ===
#speaker:Nadia
<i>I sit. The chair's where I left it.</i> #thought
* [Talk to him]
    It's Nadia. I'm here, Eli. I'm right here.
    <i>His monitor doesn't change.</i> #thought
    -> END
* [Just sit]
    <i>I sit with him a while.</i> #thought
    -> END

=== day2_stairwell ===
#speaker:Nadia
Stairwell B. Down to the closed wing.
* [Go down to the sleep wing]
    #event:vh:to_booth
    -> END
* [Not yet]
    -> END

=== booth_mags2 ===
#speaker:Mags #portrait:Mags neutral right
Again, then. You found the house. Tonight you go inside.
Same rules. Only the Truth is on your side. Count back from ten.
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
Nadia Lasname. Look at you, all grown up.
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
