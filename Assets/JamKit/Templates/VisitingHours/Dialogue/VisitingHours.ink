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
EXTERNAL vh_state(name)
EXTERNAL vh_topic(id)

-> day1_intro

// ---------- Day 1: St. Brigid's, Ward 4 West, the late shift (docs/redesign/day.md) ----------
// Written by the Day agent in the creator's voice (2026-10-12) except where marked as the creator's. The game listens for:
//   #event:vh:handover            the round starts (the round sheet shows)
//   #event:vh:round:<task>:ok     a round task done right;  #event:vh:round:<task>:slip  done after a slip she caught
//   #event:vh:elias:fingers|heart|calm   his body answers while she talks (the hand in frame, the monitor)
//   #event:vh:told:<topic>        what she told him (the dream hears it: DayContent.Topics, Chain.Tell)
//   #event:vh:bedside:rise        she's done talking (E stands her up)
// vh_state(name) asks the game: "handover", "round_done", "sat", "told_any", "day2", "slipped", "forced_out", "told:<topic>".

=== day1_intro ===
#speaker:Nadia #portrait:Mags hide #portrait:Walt hide
<i>Six weeks.</i> #thought #auto:2.2
<i>Ward 4 West. Three o'clock. Rafael will be at the station, waiting to go home.</i> #thought #auto:3.2
-> END

=== day1_rafael ===
{vh_state("handover"):
    #speaker:Rafael
    {vh_state("round_done"):
        Round's done? Then go and sit with him. I'll cover the bells for ten minutes.
    - else:
        Round sheet's on the board. I'm in the staff room. Not that you ever need me.
    }
    -> END
}
#speaker:Rafael
Afternoon. You're early.
#speaker:Nadia
I'm on time. You're late going home.
#speaker:Rafael
Handover, then. Room 14 had a quiet one. His band's been checked twice; check it again, Sister Hale's on the warpath. Obs are due.
Mrs. Okafor in 12 wants her paracetamol at six, and she'll start telling you about it at five.
That's the round. It's on the board.
#speaker:Nadia
Thanks, Raf.
#speaker:Rafael
You don't have to take him every shift, you know. I can swap.
#speaker:Nadia
<i>I know. I won't.</i> #thought
#event:vh:handover
-> END

=== examine_station ===
#speaker:Nadia
The nurses' station. Somebody's left the radio on low. The bells board is quiet, for once.
-> END

=== examine_round_board ===
#speaker:Nadia
The round board. Six o'clock, in Rafael's capitals.
{vh_state("day2"):
    ROOM 16, BRANDT: DRUG CHART, CHECK BAND. ROOM 12, OKAFOR: OBS, ANTIBIOTIC. ROOM 14, LASNAME: NEW BAG, OBS.
- else:
    ROOM 14, LASNAME: BAND, OBS. ROOM 12, OKAFOR: PARACETAMOL 6PM (SHE WILL REMIND YOU).
}
<i>He's drawn a little sun next to Eli's name. He does that.</i> #thought
-> END

// ---------- The duty round, Day 1: the tutorial (three tasks) ----------

=== round_not_handed_over ===
#speaker:Nadia
<i>Handover first. Rafael's at the station.</i> #thought
-> END

=== round_r1_band ===
#speaker:Nadia
His wristband. LASNAME, ELIAS. Born 14/03/1983. Hospital number 40-11-583.
The chart at the foot of the bed. LASNAME, ELIAS. 14/03/1983. 40-11-583.
* [They match]
    #event:vh:round:r1_band:ok
    <i>They match. They always match. You still check. That's the job.</i> #thought
* [Skip it. It's been checked twice today]
    #event:vh:round:r1_band:slip
    <i>...No. "It's been checked" is how the wrong patient gets the right drug. Band. Chart. Every time.</i> #thought
    <i>They match.</i> #thought
- -> END

=== round_r1_obs ===
#speaker:Nadia
The monitor. Pulse {vh_state("forced_out"):seventy-four|sixty-two}. Pressure one-eighteen over seventy-four. Sats ninety-eight. Breathing twelve.
* [Chart them as they are]
    #event:vh:round:r1_obs:ok
    <i>Everything a well man has. I write it down anyway.</i> #thought
* [Copy last night's line. It's always the same]
    #event:vh:round:r1_obs:slip
    <i>My pen's on last night's numbers before I stop. No. You write what's in front of you, not what you expect.</i> #thought
    <i>I write it down properly.</i> #thought
- -> END

=== round_r1_meds ===
#speaker:Mrs. Okafor
Six o'clock, nurse. I've been watching that clock since five.
#speaker:Nadia
Her drug chart: paracetamol, one gram, by mouth, six o'clock. No allergies. On the tray: two 500 mg tablets, a 1 g infusion bag, codeine 30 mg.
* [Two 500 mg tablets, with water]
    #event:vh:round:r1_meds:ok
    #speaker:Mrs. Okafor
    Thank you, dear. You have his frown, you know. Your brother's.
* [The 1 g infusion bag]
    #event:vh:round:r1_meds:slip
    <i>That's for a drip. She's eating toast. By mouth, Nadia.</i> #thought
    Two tablets, with water.
    #speaker:Mrs. Okafor
    You looked at that bag a long time, dear.
* [Codeine, 30 mg]
    #event:vh:round:r1_meds:slip
    <i>That's not on her chart. I'm tired, not careless.</i> #thought
    <i>...I'm tired.</i> #thought
    Two tablets, with water.
- -> END

=== talk_okafor ===
#speaker:Mrs. Okafor
{vh_state("day2"):
    They tell me I've got a temperature. I told them I've had one for seventy-six years.
- else:
    You're the Lasname girl. Elias's sister.
}
#speaker:Mrs. Okafor
I've lived on Larch Street forty years, you know. Number 9. Your brother fixed my gate with a bicycle chain when he was eleven. It's still holding. #event:vh:record:fact_okafor #sfx:page
He was always in Walt Abernathy's garage. Grease to the elbows. Walt used to send him home with a note for your mother.
#speaker:Nadia
<i>Walt. I haven't heard his name out loud in years.</i> #thought
-> END

=== round_done ===
#speaker:Nadia
{vh_state("slipped"):
    <i>Round's done. I made a mistake I caught. That's two more than I make on a good day.</i> #thought
- else:
    <i>Round's done. Clean.</i> #thought
}
<i>Now I can sit with him.</i> #thought
-> END

// ---------- Room 14 ----------

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
The photo on his locker. The lake, last summer. I took it.
Lena's laughing at something off-camera. Theo's laughing so hard he's a blur. #event:vh:record:fact_photo #sfx:page
-> END

=== examine_bouquet ===
#speaker:Nadia
Lilies on the windowsill. Too many for the sill. #event:vh:record:fact_bouquet #sfx:page
The card: <i>With our deepest sympathies. LeBaget, Community Partnerships.</i>
<i>Who sends lilies to a man who isn't dead?</i> #thought
-> END

=== examine_elias ===
#speaker:Nadia
{vh_state("sat"):
    <i>See you tomorrow, Eli. Or tonight. Whichever comes first.</i> #thought
    -> END
}
{vh_state("round_done"):
    <i>Hi, Eli. The round's done. I'll sit with you.</i> #thought
- else:
    <i>Hi, Eli.</i> #thought
    <i>The round first. Then I'll sit with you.</i> #thought
}
-> END

=== duty_wristband ===
#speaker:Nadia
LASNAME, ELIAS. Checked.
-> END

=== duty_vitals ===
#speaker:Nadia
The line goes up and down. Sixty-something. It always is.
-> END

// ---------- The staff room ----------

=== examine_locker ===
#speaker:Nadia
My locker. My bag, my coat, a spare pair of shoes I keep meaning to take home.
Taped inside the door: Eli and me at the creek below Larch Street, with our bikes. I'm seven and soaked to the knees. #event:vh:record:fact_locker_photo #sfx:page
<i>He's laughing because I made the jump and he didn't.</i> #thought
-> END

=== day1_phone ===
#speaker:Nadia
My phone, face down by my mug. One voicemail. Elias, forty-one seconds. Played to 0:09.
<i>Not now.</i> #thought
-> END

=== examine_rota ===
#speaker:Nadia
The rota. LASNAME, N.: late, late, late, late, late, late, late.
<i>Seven in a row. I asked for them. Days are worse.</i> #thought
-> END

=== examine_kettle ===
#speaker:Nadia
The kettle's still warm. Somebody's left a mug by it: WORLD'S OKAYEST NURSE.
<i>Rafael's. He'll say it was a gift.</i> #thought
-> END

// ---------- The family room ----------

=== day1_june ===
#speaker:Mrs. Brandt
Oh, you're his sister, aren't you? The doctor's. Sit, sit. I won't keep you.
Four years ago Harold's heart stopped in the car park downstairs. Your brother kept it going for forty minutes. Forty. They told me afterwards he wouldn't let anybody take over.
Harold's back in 16 with his chest again. He's asleep. He's always asleep when I visit. #event:vh:record:fact_june_brandt #sfx:page
Tell your brother Harold's still annoying me. He'll like that.
#speaker:Nadia
<i>He would.</i> #thought
-> END

=== examine_tv ===
#speaker:Nadia
The television's on mute. The ticker crawls along the bottom: ROUTE 9: SIX WEEKS ON, NO ARRESTS.
<i>They'll stop running it soon. Then it'll just be ours.</i> #thought
-> END

=== examine_vending ===
#speaker:Nadia
The machine takes my coin and thinks about it for a long time. Then it gives me tomato soup.
<i>I pressed coffee.</i> #thought
-> END

=== examine_window ===
#speaker:Nadia
The window at the end of the ward. From up here you can see all of Larch Hill, falling away to the creek.
<i>Our street's down there somewhere, under the trees. Walt's shop is a phone repair place now.</i> #thought
-> END

// ---------- The quiet room ----------

=== examine_book ===
#speaker:Nadia
The remembrance book. People write in it when they can't say it to anyone. Thank-yous. Names. A drawing of a dog.
A page in felt-tip, from last month: <i>For Theo, our left back. We kept your shirt. Number 4. From the Larch Hill Under-11s.</i> #event:vh:record:fact_theo_team #sfx:page
Eleven names. Two of them spelled "defender" wrong.
-> END

=== examine_candles ===
#speaker:Nadia
Battery candles. Real ones set off the alarms. Somebody's switched all of them on anyway.
<i>I leave them on.</i> #thought
-> END

// ---------- The bedside (both days; docs/redesign/day.md: the sit, the hand, the talk, the rise) ----------

=== bedside_sit ===
#speaker:Nadia
{vh_state("sat"):
    <i>Not again today. He's had enough of me for one day.</i> #thought
- else:
    <i>After the round. I'll sit with him when the round's done.</i> #thought
}
-> END

=== day2_sit ===
-> bedside_sit

=== bedside_intro ===
#speaker:Nadia
{vh_state("day2"):
    <i>Same chair. Same hand. Warmer than you'd think.</i> #thought
- else:
    <i>Mags says to talk to him. "They hear more than you'd think. And they keep it."</i> #thought
}
-> bedside_choose

=== bedside_more ===
-> bedside_choose

=== bedside_choose ===
+ {vh_topic("walt")} [Walt's shop] -> bedside_walt
+ {vh_topic("creek")} [The creek, and the bikes] -> bedside_creek
+ {vh_topic("patients")} [His patients ask after him] -> bedside_patients
+ {vh_topic("sorry")} [The phone] -> bedside_sorry
+ {vh_topic("lake")} [The lake, last summer] -> bedside_lake
+ {vh_topic("theo")} [Theo's team] -> bedside_theo
+ {vh_topic("lena")} [Lena] -> bedside_lena
+ {vh_topic("silence1") || vh_topic("silence2")} [Just sit with him] -> bedside_silence
+ {vh_state("told_any")} [That's enough for today] -> bedside_enough

=== bedside_walt ===
#speaker:Nadia
I found your keys, Eli. The tag still says "W.A. workshop".
Remember Saturdays? You'd wheel that bike down to Walt's with the back tyre flat again, every week, like it was a brand new disaster.
And he'd write it in that ledger. Every repair, in his tiny handwriting. #event:vh:elias:fingers
<i>His fingers moved. They do that. It doesn't mean anything.</i> #thought
<i>...It might mean something.</i> #thought #event:vh:told:walt
-> END

=== bedside_creek ===
#speaker:Nadia
There's a photo in my locker. You and me at the creek, with the bikes.
You laid that plank across so we wouldn't get our shoes wet. And I jumped it instead, because you said I couldn't. #event:vh:elias:heart
You didn't talk to me for a whole afternoon.
<i>Seventy-one. Seventy-four. His heart's up. He remembers.</i> #thought #event:vh:told:creek
-> END

=== bedside_patients ===
#speaker:Nadia
June Brandt's in the family room. She says Harold's still annoying her. She said you'd like that.
People ask after you, Eli. The porters. The cleaners. The man at the café downstairs won't let me pay for my coffee, and he won't say why. #event:vh:elias:heart
<i>His heart's up. Good. Be proud of something.</i> #thought #event:vh:told:patients
-> END

=== bedside_sorry ===
#speaker:Nadia
The police gave me the report. The phone log's in it.
...I'm sorry I didn't pick up. #event:vh:elias:fingers
<i>That's all I can say. That's all I can say today.</i> #thought #event:vh:told:sorry
-> END

=== bedside_lake ===
#speaker:Nadia
I keep looking at the photo on your locker. The lake.
Lena made you swim out to the platform and back. You swore the water was eight degrees. It was twenty. #event:vh:elias:heart
I took that picture. Theo laughed so hard he's just a blur.
<i>Eighty. Eighty-four. Come on, Eli.</i> #thought #event:vh:told:lake
-> END

=== bedside_theo ===
#speaker:Nadia
Theo's team wrote in the book downstairs. The Under-11s.
They kept his shirt. Number 4. Two of them spelled "defender" wrong. #event:vh:elias:fingers
<i>His hand closes. A little. For a second.</i> #thought #event:vh:told:theo
-> END

=== bedside_lena ===
#speaker:Nadia
Rafael says you asked about Lena. When they brought you in.
They haven't found her, Eli. They're still looking. #event:vh:elias:heart
I'm not going to lie to you. Not in here.
<i>His heart jumps. Somewhere, a door closes.</i> #thought #event:vh:told:lena
-> END

=== bedside_silence ===
#speaker:Nadia
<i>I don't say anything. I hold his hand and watch the line go up and down.</i> #thought #event:vh:elias:calm
<i>Sixty-two. Sixty. Fifty-eight. Somewhere in there, something slows down with me.</i> #thought #event:vh:told:silence
-> END

=== bedside_enough ===
#speaker:Nadia
{vh_state("told_any"):
    <i>His heart's up. That's enough for one day.</i> #thought
- else:
    <i>That's enough for one day.</i> #thought
}
#event:vh:bedside:rise
-> END

=== bedside_rise ===
#speaker:Nadia
<i>I'll be back, Eli. I'm always back.</i> #thought #auto:1.8
-> END

=== bedside_after_round ===
-> round_done

// ---------- The stairs down ----------

=== stairwell ===
#speaker:Nadia
{not vh_state("round_done"):
    <i>Not before the round. Room 14, room 12. The board's at the station.</i> #thought
    -> END
}
Stairwell B. The sleep-research wing is two floors down. It closes at nine.
{not vh_state("sat"):
    <i>I haven't sat with him today.</i> #thought
}
<i>Mags said eleven.</i> #thought
* [Go down to the sleep wing]
    #event:vh:to_booth
    -> END
* [Not yet]
    -> END

=== stairwell_no_bedside ===
-> stairwell

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
// The Day agent's lines from here (the creator's scene ends above).
Your notebook. Show me before you go under.
#event:vh:open_journal
-> END

=== booth_after_journal ===
#speaker:Mags #portrait:Mags neutral right
J or Tab opens it in there too. Pick two things, see if they fit.
-> booth_heard

// What Mags saw on his monitor log while Nadia sat with him (both days): the bedside's consequence, said out loud.
=== booth_heard ===
#speaker:Mags
{vh_state("told_any"):
    His heart did something this evening, while you were with him. Up eleven beats, and it stayed up.
    Whatever you told him, it's in there now. Watch for it.
- else:
    {vh_state("sat"):
        You sat with him. His heart came down to fifty-eight and stayed there. He's calm. Use it.
    - else:
        You didn't sit with him today. Then we go in with nothing new. That's allowed.
    }
}
{vh_state("slipped"):
    And your hands are shaking. Sit down before you fall down.
}
Count back from ten. I'll be right here.
#event:vh:dive
-> END

// ---------- Day 2 ----------

=== day2_intro ===
#speaker:Nadia #portrait:Mags hide #portrait:Walt hide
{vh_state("forced_out"):
    <i>Day two. They called a crash on him at four this morning. Rafael rang me.</i> #thought #auto:3
    <i>He came back. They always say that like it's the end of the sentence.</i> #thought #auto:3
- else:
    <i>Day two. Four hours' sleep.</i> #thought #auto:2.2
}
<i>The round, then him. Then the stairs.</i> #thought #auto:2.2
#event:vh:handover
-> END

=== day2_rafael ===
#speaker:Rafael
You look like you slept in a chair.
#speaker:Nadia
I slept in a chair.
#speaker:Rafael
I was on when they brought him in. Did anyone ever tell you that?
He was awake for about a minute. He grabbed my sleeve and said one thing. "Did Lena get out?" #event:vh:record:fact_did_lena #sfx:page
#speaker:Nadia
What did you tell him?
#speaker:Rafael
That they were looking. Then they put him under.
#speaker:Nadia
<i>Did she?</i> #thought
-> END

// LeBaget's representative is a woman (the creator, 2026-10-13): Corinne Vasseur, Community Partnerships, mid-forties,
// the aubergine blazer and the gold glasses on their chain (docs/redesign/people.md). She asks what he's said; there is
// no transfer clock.
=== day2_visitor ===
#speaker:The woman from LeBaget
Ms. Lasname? Corinne Vasseur, from LeBaget. Community Partnerships. I'm so sorry. We all are.
Dr. Lasname has been a friend to the foundation for years. I brought fruit. Nobody could tell me what he's allowed.
#speaker:Nadia
He's on a drip.
#speaker:The woman from LeBaget
Of course. Of course he is. That was silly of me.
Has he said anything? #event:vh:record:fact_visitor #sfx:page
#speaker:Nadia
He's in a coma.
#speaker:The woman from LeBaget
Yes. Forgive me. I only meant, if he does. Anything at all. We'd want to help. My card.
#speaker:Nadia
<i>Who asks what a man in a coma has said?</i> #thought
-> END

=== talk_brandt ===
#speaker:Mr. Brandt
Nurse. Where's the doctor with the bad jokes? The tall one. He told me I'd be too stubborn to die.
...Oh. June told me. I'm sorry, love. I forgot. #event:vh:record:fact_brandt #sfx:page
You tell him he was right. I'm still here, and I'm still stubborn.
#speaker:Nadia
<i>I'll tell him.</i> #thought
-> END

// ---------- The duty round, Day 2 (five tasks) ----------

=== round_r2_band ===
#speaker:Nadia
Mr. Brandt's wristband: BRANDT, HAROLD. 40-17-226.
His drug chart, from pharmacy: BRANDT, HAROLD. 40-17-262.
* [Close enough. Same name, same room]
    #event:vh:round:r2_band:slip
    <i>"Close enough" is how people die. Two numbers swapped.</i> #thought
    I hold his tablets and ring pharmacy. They typed it wrong.
* [They don't match. Hold his tablets and ring pharmacy]
    #event:vh:round:r2_band:ok
    I ring pharmacy. They typed it wrong. Somebody would have caught it.
    <i>Tonight it was me.</i> #thought
- -> END

=== round_r2_obs_okafor ===
#speaker:Nadia
Mrs. Okafor's monitor. Temperature thirty-eight four. Pulse ninety-six. Three days after a new hip.
* [Recheck in an hour]
    #event:vh:round:r2_obs_okafor:slip
    <i>No. Thirty-eight four and ninety-six after a hip isn't "in an hour".</i> #thought
    I bleep the doctor.
* [Call the doctor now]
    #event:vh:round:r2_obs_okafor:ok
    I bleep the doctor. Dr. Shah's on her way up.
- #speaker:Mrs. Okafor
You're fussing, dear.
#speaker:Nadia
I am.
-> END

=== round_r2_meds_okafor ===
#speaker:Nadia
Her six o'clock: co-amoxiclav, 625 mg, by mouth. Her wristband's red.
* [Give it. It's prescribed]
    #event:vh:round:r2_meds_okafor:slip
    <i>My hand's on the cup before I read the red band. Penicillin allergy. Co-amoxiclav is a penicillin.</i> #thought
    I put it down. I flag it.
* [Hold it. The red band: she's allergic to penicillin]
    #event:vh:round:r2_meds_okafor:ok
    It's on her band and not on her chart. I hold it and flag it.
- Dr. Shah swears and thanks me in the same breath.
-> END

=== round_r2_drip ===
#speaker:Nadia
His drip's nearly through. The chart: sodium chloride 0.9%, one litre, over eight hours. Two bags on the trolley, the same size.
* [Sodium chloride 0.9%]
    #event:vh:round:r2_drip:ok
    <i>I read the label twice. Hang it. Set the pump.</i> #thought
* [Glucose 5%]
    #event:vh:round:r2_drip:slip
    <i>Same bag, same size, different everything. I read the label again. Then a third time.</i> #thought
    Sodium chloride. Hang it. Set the pump.
- -> END

=== round_r2_obs ===
#speaker:Nadia
The monitor. Pulse {vh_state("forced_out"):seventy-eight|sixty-six}. Pressure one-twenty over seventy-six. Sats ninety-seven.
{vh_state("told_any"):
    <i>A little up on yesterday. Since I talked to him.</i> #thought
}
* [Chart them as they are]
    #event:vh:round:r2_obs:ok
    <i>Written down. Exactly.</i> #thought
* [Copy yesterday's line]
    #event:vh:round:r2_obs:slip
    <i>No. It's not yesterday's. Nothing's yesterday's, except in there.</i> #thought
    I write down what's in front of me.
- -> END

=== day2_wristband ===
#speaker:Nadia
LASNAME, ELIAS. Same name, same number.
-> END

=== day2_round ===
#speaker:Nadia
The bag's up. The line's clean.
-> END

=== day2_vitals ===
#speaker:Nadia
The line goes up and down.
-> END

=== day2_stairwell ===
#speaker:Nadia
{not vh_state("round_done"):
    <i>Not before the round. Sixteen, twelve, fourteen.</i> #thought
    -> END
}
Stairwell B. Down to the closed wing.
{not vh_state("sat"):
    <i>I haven't sat with him today.</i> #thought
}
* [Go down to the sleep wing]
    #event:vh:to_booth
    -> END
* [Not yet]
    -> END

=== booth_mags2 ===
#speaker:Mags #portrait:Mags neutral right
{vh_state("forced_out"):
    They called a crash on him at four. You came out of there like you'd been thrown. I'll say it once: five nights, now. Not six.
}
Again, then. You found the house. Tonight you go inside.
-> booth_heard

// Thrown out of Night 2 (the house's fall): she wakes in the booth's chair. The House agent's lines
// (DreamHouseLines.ThrownOutBooth in Runtime/Core/DreamEscape/DreamHouseStory.cs), word for word; the Day agent's booth
// plays them when the save has vh_n2_thrown_out. VisitingHoursInkTests checks they still match.
=== booth_thrown_out ===
#speaker:Mags #portrait:Mags neutral right
Breathe. You're out. You're in the booth.
#speaker:Nadia
Lena-- Theo--
#speaker:Mags
His heart went through the roof when you fell. I had to. That's a night we don't get back.
And they'll remember how you left.
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

=== function vh_state(name) ===
~ return false

=== function vh_topic(id) ===
~ return true
