# Visiting Hours: which design decisions are still open

Written 2026-10-12 to answer: "Did STORY-PROSE not answer some of the design decision questions? Or do I need to look
through all of them to confirm?" Short answer: your STORY-PROSE notes answer some of them, but not most. You only need
to read the **Still open** list below. The per-question detail further down shows where each answer came from.

**How this was judged (strictly).** A question counts as answered only where your own words decide it. Your own words
are your `//` notes in STORY-PROSE and STORY-BIBLE (commit 732485f), your opening script, and your decisions as logged in
VISITING-HOURS, CHARACTERS, SLICE-VISITING-HOURS and WORKLOG. An agent's proposal never counts, and that includes all of
STORY-TALK and the bibles' own text. Silence doesn't count as a yes either. Many [CONFIRM] items were never confirmed;
they're marked **Open (confirm only)**, so one word from you closes each.

**References.** PROSE = STORY-PROSE, SB = STORY-BIBLE, CB = CHARACTER-BIBLE, NG = NIGHTS-GAMEPLAY, AB = ARGUMENT-BATTLES,
VH = VISITING-HOURS, CH = CHARACTERS, WL = WORKLOG, each followed by line numbers as of today. DESIGN-DECISIONS numbers only its
first three questions. The rest are numbered 4 to 51 here in the same order.

**Counts (51 questions):** 11 answered, 21 partly answered, 19 open (9 of the open ones just need a yes or no).

---

## Still open (what you still need to decide), most blocking first

**Blocks gameplay and art now**
1. **The Night 4 truth** (question 13): what the crash memory reveals, now that Lena walking into the light is cut. Night 4's
   chase (in the first build tier), "the hope" (39), Lena's truth (2) and the purpose of the phone call (33) all wait on it.
   You wrote "Start discussion on this immediately."
2. **The setting** (not in DESIGN-DECISIONS; PROSE:51, PROSE:99): does one golden-hour Saturday street become dense
   enough for stealth, or does it link to a denser place? You asked for "some good answers". The deep-block greybox
   (layout A) is built, but you haven't picked it. // I didn't like the deep-block setting that much, because it didn't reveal how large the setting was to the player, but it's the best of the proposed solution, unless you're willing to work vertically and set it on a hill.
3. **Night 1's tutorial** (50; PROSE:104): you rejected Walt's tutorial "the way it's currently built". What teaches the
   argument, and how does Night 1 end without the stage-strike exit? // Night 1 needs a complete restructure after the purpose of each night is defined among the five.
4. **What each night is about** (30), Night 3 above all: what the dream hospital is for (32), what the Doctor argues
   and what wins it without the proxy form (51), and whether he's a second Elias (15). // Exaclty my questions. We need to talk on this as well, after the Night 4 truth.
5. **Theo: in or out** (1, 24, 48): you consider cutting him, but you also endorsed his hide-and-seek on Night 2 and his
   torch on Night 4. His blessing, his guide role and the "Tell" verb all hang on this. // I think he may need to stay. Mark where this means his presence is affecting things that needs to be addressed. 
6. **The Long Night's lock-in** (3): which curious choices count, how many lock the route, and how it can be aborted.
   The umbrella is already one of them in the build. // We'll do this when inspiration strikes me, or after the normal rotue is made in its entirety. 
7. **The final arena** (31): is it the car, and if so, why are they back at the beginning? // beats me. 
8. **Lena's Thursdays and the fridge keys** (20): with no studio, what did she say she was doing, and what do the keys
   mean? The fridge calendar is already in the house art. // beats me. Does it matter within the scope of this game?
9. **Night 2's argument** (22): does "He called you first" stay in it, and what exactly makes Lena open to giving her
   blessing later? // I rewrote the script, so that might answer it, but to answer the second part, the argument on Night 2 is exactly what leads Lena to be able to change her mind if you play your cards right. 

**Blocks the story and the endings**
10. **Elias's guilt** (17): what he actually feels guilty about (you said being the driver doesn't matter), whether he
    represses it or comes to it over the nights, and what releases each of the four holds. // Guilty for getting his wife and son killed in his mind.
11. **Lena's truth** (2): what she gives up, when (a later battle, or the back seat), and in what words. // Does it need to be a physical object?
12. **Morning, forced** (42): you want it redone. How does it play, and how does it end? // What do you mean? Did we not write words on this?
13. **Morning, chosen** (43): beyond the stargazing, does "Where's Lena?" / "We don't know." stay, and what's the last image? // The stargazing is the last image.
14. **Evening** (49): can Nadia choose it by not arguing ("Let it rest"), or is it only what happens when she fails to
    convince him? // Both.
15. **The Long Night's last image** (3): you asked for suggestions; the default is Nadia's exhausted face. // Yeah?
16. **The rest of the voicemail** (6): on which night, or in which ending, Nadia finally hears the apology, and what it
    changes. // I'm not sure if that fight's emotions are even lingering in her mind anymore, but if they are, then it would certainly be a pivtol moment, where her doubts about her brother are quashed.
17. **The unwelcome visitor on Day 2** (28): you like the idea, but not LeBaget's representative. Who is it? // It can be LeBaget, but I'm open to other ideas.
18. **Julian Cho's replacement** (26): what stands in for him as Mags's past. // Does this need to exist?
19. **Rafael** (9, 27): he's now an intern. Is he still the other candidate for Nadia's job, and did Elias fund a post
    for him? // I'd thought 'he' was a 'she', but yeah, he works at the hospital now.
20. **The two sides on Route 9** (4): is Larpaca the gang, and does the news stop naming LeBaget? // Yes.
21. **The family money** (10): inherited or earned? This only matters if any clue about it stays in. // Doesn't matter/
22. **The hope** (39): waits on the Night 4 truth. // What does this mean?

**New questions your notes raised that DESIGN-DECISIONS doesn't list** (STORY-TALK has options for most of them) // gonna do this tomorrow
- Nadia's survivor's guilt, and what Lena did or was that makes it make sense (PROSE:37, PROSE:56).
- The siblings' history with Lena (PROSE:58).
- What earns Mags's trust, and what it unlocks (PROSE:174).
- Which closing line of the hide-and-seek search belongs to the Despair route (PROSE:125).
- The "Appointed on merit" letter and the "perfect line" monitors on Night 3: keep, fix or cut (PROSE:160).
- A catalogue of where each fact is used, which is needed and which is optional (PROSE:152; `STORY-FACTS.md` is the
  start of this, and nobody has reviewed it yet).

**Just say yes or no** (the docs are consistent; you haven't confirmed them)
- 8: Elias never drives after night shifts, and broke his own rule that night.
- 14: six weeks on, he's unresponsive with normal scans, and his right hand is crushed for good.
- 18: Walt calls Nadia "Bell".
- 21: dream-Lena knows she's his memory.
- 25: Mags came from a corporate sleep lab; Elias gave her the closed wing and paid for the rig in cash.
- 35: failing costs little (a chase resets at speed, a lost argument means a re-dive that costs his heart, the assist
  has no story cost).
- 36: build tier A first.
- 45: every challenge needs a reason, picked from three (it's in the prototype you played and called "structure isn't bad").
- 47: composure is Nadia's heart rate; Press is a hold; "Let it rest" ends a battle as heard out (also in the prototype).

---

## Contradictions: where your notes say one thing and the other docs or the game say another

1. **Lena's passion.** You've settled on stargazing: "We'd settled on giving her a stargazing hobby, so maybe a small
   observatory in the attic?" (PROSE:115), and you approved the attic observatory (WL:1395). CH:60-62 (your round 3) rules out clay.
   Still saying ceramics: CB:15 ("Lena is a ceramicist"), CB:33 and CB:136-138 (Ines and the kiln co-op), SB:143 and
   PROSE:113 (the pottery studio), AB:285 and ARGUMENT-PROSE-BRIEF:83 (the apron hair tie), BORROWED-MECHANICS:38
   (bowls, glaze, the kiln), CH:43 ("paint or clay on her hands"), and CH:72-74 (an agent's proposal of notebooks about
   the city's vanishing places). **In the game:** the Lena battle's keepsake is still "Lena's apron hair tie"
   (`ArgumentText/lena_night2.txt`, line 28).
2. **Is Lena missing or dead?** Your notes treat her as dead: "she thinks the world would have been better off if Lena
   had lived" (PROSE:37), "responsible ... for Lena's and Theo's deaths" and "Lena lives on in his heart" (PROSE:56),
   and "If LaBaget are reponsible for Lena's death" (PROSE:233). Your own earlier rules keep her missing: VH:18 ("her
   body was never found"), CH:64-65 ("so that her going missing stays a real open question and not an allegory for
   death"), the opening headline ("WIFE MISSING") and SB's Morning ending ("Where's Lena?" "We don't know.").
3. **Who fought on Route 9.** Your notes say a corporation and a gang: "corpo and a gang, but it doesn't matter within the
   scope of this game" (SB:36). PROSE:233 then names LeBaget "along with Larpaca". **In the game:** your opening script's news,
   which is generated into the build, says "the crossfire between LeBaget and the Larpaca corporation"
   (`script/opening-script.md`:12), while its headline says "GANG VIOLENCE". VH:7 says two corporations. CH:12 still says the
   second name is undecided, and CH:105-108 still warns about WeWork, which your script has already replaced. PROSE:233
   also says the news must stop naming LeBaget if LeBaget shares the blame.
4. **The Long Night.** Yours (PROSE:56): Nadia is pulled in by despair and feeling unredeemable, loses a scripted battle,
   takes Elias's hand, and Elias "already died near the start of the battle". SB:310-338 and SB:465-481: comfort and a
   hidden "Belonging" value, a living Elias in an endless Saturday, the 3% phone as the lock-in, and Mags moving her body to the
   bed beside Julian Cho. SLICE:23 (your own 2026-10-09 rule: "a hidden despair value") agrees with you, not the bible.
   **In the game:** the umbrella's Despair flag and "condemns feeds Despair" follow your version.
5. **Evening.** Yours: "she fails to convince him", ending in "a memorial and a funeral, Nadia left on her own as it
   rains" (PROSE:56). SB's Evening, and the verb table (SB section 9, AB:343-348): she chooses not to argue ("Let it rest"),
   and it ends with two graves, an empty stone and Nadia back on shift.
6. **Morning, chosen.** Yours: she convinces him Lena wouldn't want them to give in to the dark, then "takes Elias out of
   the city to go stargazing" (PROSE:56). SB:403-406: "Where's Lena?", the LeBaget plaque, the phone call "I'm already outside".
7. **Nadia's guilt.** Yours: survivor's guilt, not blame for the crash ("Why? She's not responsible for 'gang' violence",
   PROSE:37). SB's spine and the final talk (SB section 9): her guilt is having gone back to sleep, and her confession of
   it releases his guilt. You rejected that release (PROSE:208).
8. **Elias's guilt and the driving.** Yours: "Being the driver here doesn't matter" (PROSE:38). SB section 9's guilt
   hold ("I drove"), CB:14 (he never drives after night shifts and broke his rule) and SB:71-72 (Lena offered to drive,
   he insisted) all make the driving matter.
9. **How Elias decides.** Yours: "I'd like for it to be granular. Anything that could affect his decision, should"
   (SB:22). SB:376 and AB:346 still use a threshold (the confession plus two of Theo, Lena and purpose).
10. **Night 2.** Your re-plot (PROSE:140): Nadia waits for Elias, who never comes; Lena admits she won't let him go; the
    battle ends with Lena unsure (open to a blessing later) or condemning Nadia. You also wrote that Nadia "shou;dn't have stopped
    coming to Sunday pancakes" (PROSE:124) and that Theo's drawings as an attack are "out of character" (PROSE:134).
    **In the game** (`lena_night2.txt`, agent placeholder text): "You stopped coming" (lines 70, 86, 90), and "To him,
    you're the bad dream" from Theo's drawing as the hidden deepest premise (lines 144, 232, 246). CB's relationships
    table has Lena "hurt she stopped coming".
11. **Rafael.** Yours: he covers her extra hours "as an intern" (PROSE:45). CB:12 makes him "the other candidate for
    Nadia's job", SB:69 has him as the night nurse, and SB:184-186 has Elias funding a senior post for him.
12. **Julian Cho.** Yours: "Julian Cho has been retconned at this point" (PROSE:173), and the bed visit is "Rejected"
    (PROSE:116). He's still in CB:18, in Mags's history, and in SB's Long Night ending (three monitors). He isn't in
    the game.
13. **The clock.** Yours: Mags gives "Five Nights, maybe Six before it's too dangerous" (your scene, PROSE:88). SB's
    LeBaget transfer to Halcyon on day 6, and Corinne's whole role (CB:295-312), are still in the bibles.
14. **Walt's tutorial.** Yours: "Rejected. A tutorial is nice, but not the way it's currently built" (PROSE:104). Your
    own 2026-10-09 rule had "Walt Abernathy teaches the battle box" (SLICE:33), and SB, NG and AB all build Night 1 on it.
    **In the game:** the Lena battle still uses "Walt's truth" as evidence (`lena_night2.txt`, lines 196-206 and 258-260).
15. **The voicemail.** Yours: "The voicemail auto-plays to its end when she's falling back asleep" (PROSE:76). CH:20 says
    "she slept through his call", SB:28 calls the dead phone "the original wound", and SB:106 and SB:477 keep the label
    "played to 0:09", which you questioned. **The game's opening** follows you: she visibly falls asleep.
16. **The family money.** Yours: "Not my idea exactly. The Lasnames were immigrants ... Nadia refuses to use his money
    when he's in a coma" (SB:20). CB:13 and CB:44-45 still have her refusing her share at 20 and sending the estate's letter
    back, and Night 3's day (PROSE:151) has the family trust's letter.
17. **Theo, within your own notes.** PROSE:56 and PROSE:209 consider cutting him. PROSE:125 calls his hide-and-seek
    "great", and PROSE:180 calls his torch-lit Night 4 escape "cool". **In the game:** Night 2's house has
    hide-and-seek with Theo.
18. **Small ones.** Your Mags scene spells her "Megs" twice (PROSE:83-84). Lena's dream height is still marked open in
    CH:85-86, though you said 6'2" is fine (WL:982). The loop is 41 Saturdays in SB and PROSE but "forty-two times" in
    the game's Lena battle (`lena_night2.txt`, line 196).

---

## Every question, in DESIGN-DECISIONS order

### The three first

**1. Where Theo gives his blessing, and why**
- **Status:** Open
- **Where:** PROSE:209: "If he ends up existing in the story at all. For him and Lena, I was considering having them
  appear in the backseats to support Nadia's arguments". PROSE:56: "might warrant removing him from the story".
- **What's left:** whether Theo stays at all, and if he does, whether his blessing comes at the guardrail on Night 4 or from the back seat
  of the car.

**2. What "Lena's truth" is**
- **Status:** Partly answered
- **Where:** PROSE:56: "she believes Lena wouldn't want either of them to sucumb to their darkness, and convinves Elias of
  that" and "to show him that Lena lives on in his heart". PROSE:140: the Night 2 battle decides "whether Lena is
  amenable in future argument battles to give her blessing". PROSE:211: "kind of a lackluster result".
- **What's left:** the truth itself, and where she gives it (a later battle, or the back seat), now that she isn't won over at
  the kitchen table. It must also fit her being missing rather than dead (see contradiction 2).

**3. What the Long Night is**
- **Status:** Partly answered
- **Where:** PROSE:56 gives the whole route: "her bad habits and unhealthy schedule leaves her vulenrable to the
  manifestations of guilt ... until she ... starts thinking she's unredeemable", then a scripted loss, and "the Elias in front of her isn't
  real, that he already died". SB:28 on the lock-in: "it needs to sneak up on the player ... like in Deltarune's
  Snowgrave". PROSE:140: no umbrella "allows her to continue the Despair route".
- **What's left:** the exact lock-in rule (which choices, how many, how the route is aborted) and the last image (you
  asked for suggestions).

### Premise and the night of the crash

**4. Who fights whom?**
- **Status:** Partly answered
- **Where:** SB:36: "corpo and a gang, but it doesn't matter within the scope of this game". PROSE:233: "If LaBaget are
  reponsible for Lena's death along with Larpaca, that fact needs to be hidden".
- **What's left:** whether Larpaca is the gang (your script calls it "the Larpaca corporation"), and whether the news
  stops naming LeBaget.

**5. Did Nadia hear the call?**
- **Status:** Answered
- **Where:** PROSE:76-77: "The voicemail auto-plays to its end when she's falling back asleep" and "make it more obvious
  Nadia falls asleep, maybe by closing her eyes and fading the voicemail's speech volume to 0". VH:61: she drops the
  phone at "You're probably asleep".
- **Note:** the phone was on; she heard the start and fell asleep. Only the "played to 0:09" label still needs fixing
  (contradiction 15).

**6. When does Nadia hear the rest of the voicemail, and what does it do to her?**
- **Status:** Open
- **Where:** your question at SB:19: "which episode is Nadia told about this, and what does it do to her?" Nothing in
  PROSE answers it.
- **What's left:** the night or ending where she hears the apology, and what it changes.

**7. [CONFIRM] Lena came because Nadia didn't**
- **Status:** Partly answered
- **Where:** your voicemail line: "I'll see if I can get a cab back home, or maybe call Lena..."
  (`script/opening-script.md`:9). PROSE:38: "Being the driver here doesn't matter."
- **What's left:** whether "Lena offered to drive and he insisted" stays, since you say the driving doesn't matter, and
  whether Theo was in the car (question 1).

**8. [CONFIRM] Elias never drives after night shifts**
- **Status:** Open (confirm only)
- **Where:** none. VH:11 (yours) only says Nadia "usually picks him up from work".
- **What's left:** yes or no; PROSE:38 suggests the rule may not be needed.

**9. [CONFIRM] The half truth**
- **Status:** Partly answered
- **Where:** VH:12-13 (your premise): "half true: she was qualified, but the other candidate was just as good". PROSE:213
  asks whether the Rafael paperwork is the Doctor's truth, without deciding.
- **What's left:** whether the panel split two to two and Elias later funded a post for Rafael, which sits oddly with
  Rafael now being an intern.

**10. The family money**
- **Status:** Partly answered
- **Where:** SB:20: "The Lasnames were immigrants, and it's unclear whether Elias inherited the fortune as the eldest
  son, or worked for it. Nadia refuses to use his money when he's in a coma" and "The relationship between the siblings
  and their parents is unclear, and not explored within the scope of this game."
- **What's left:** inherited or earned, which matters only if clues like the trust's letter stay in.

**11. The proxy form at 2:47 AM**
- **Status:** Answered (cut)
- **Where:** SB:23: "doesn't make sense to me ... this story isn't an action thriller" and "No. We don't keep".
- **Note:** the Doctor's battle loses its truth; that's tracked under question 51.

**12. The clock**
- **Status:** Answered
- **Where:** SB:24: "What's the point of having a second timer on the bomb?" Your Mags scene (PROSE:88): "I'd give
  you Five Nights, maybe Six before it's too dangerous to continue".
- **Note:** your Mags scene sets the limit, so the LeBaget transfer goes.

**13. The one reframing reveal**
- **Status:** Open
- **Where:** SB:21: "cut this entirely ... this still needs new material". PROSE:33: "Start discussion on this
  immediately." PROSE:211: "The truth itself should reveal something major to the player if possible ... even if Nadia
  doesn't realize it herself."
- **What's left:** what the memory shows, what leads to it, and why it matters (your questions at PROSE:186-187).

**14. [CONFIRM] Six weeks on**
- **Status:** Open (confirm only)
- **Where:** none. Your question at PROSE:213 ("more than his surgical ability") assumes the crushed hand.
- **What's left:** yes or no.

### Elias

**15. Two Eliases in the dream**
- **Status:** Partly answered
- **Where:** PROSE:167: "Need to expand on this. Possibly the most important battle, since it's the player's first
  introduction to Elias' character." SB:25: "Worth thinking about."
- **What's left:** whether the Doctor is a separate figure from the soft host, and what that battle is about.

**16. His choice**
- **Status:** Answered
- **Where:** SB:22: "If possible, I'd like for it to be granular. Anything that could affect his decision, should ... And
  for the major ones ... he should mention them."
- **Note:** the bibles still use a threshold (contradiction 9). Turning "granular" into rules is design work, not a
  decision you still owe.

**17. [CONFIRM] What holds him: guilt, Theo, Lena and purpose**
- **Status:** Partly answered
- **Where:** PROSE:208 on guilt: "Rejected. Having another party to blame doesn't relieve of him of his own guilt".
  PROSE:209 on Theo: "If he ends up existing in the story at all." PROSE:211 on Lena: "lackluster". PROSE:213 on
  purpose: "What is the Doctor's truth here?" PROSE:38: "does Elias come to a realization ... or does he just repress
  the memories".
- **What's left:** what his guilt actually is and what releases it, and whether Theo stays as one of the holds.

### Nadia

**18. [CONFIRM] Walt calls her "Bell"**
- **Status:** Open (confirm only)
- **Where:** none.
- **What's left:** yes or no.

### Lena

**19. Her passion**
- **Status:** Answered: stargazing
- **Where:** PROSE:115: "We'd settled on giving her a stargazing hobby, so maybe a small observatory in the attic?" WL:1395:
  "the attic observatory is approved". CH:60-62: "No workwear, no apron, no clay".
- **Note:** many docs, and the game, still say ceramics (contradiction 1).

**20. [CONFIRM] Her Thursdays**
- **Status:** Partly answered
- **Where:** PROSE:115: "Instead of a friend, something else that shows a hole in who Lena is." PROSE:125: "my question
  is what the keys on the fridge represent, or even mean?"
- **What's left:** what she said she was doing on Thursdays now there's no studio, what the keys mean, and what shows the
  gap in her life in place of the friend.

**21. [CONFIRM] Dream-Lena knows she's his memory**
- **Status:** Open (confirm only)
- **Where:** none. Your antagonising reply (PROSE:133-134: "Dream-Lena's in the wrong for acting like she's true when
  she's not a real person") works either way.
- **What's left:** yes or no.

**22. [CONFIRM] She becomes a partner only if fully convinced and Nadia owns "He called you first"**
- **Status:** Partly answered
- **Where:** PROSE:140: the battle's results "determine whether Lena is amenable in future argument battles to give her
  blessing ... or if she condemns Nadia". PROSE:209: Lena and Theo "in the backseats to support Nadia's arguments".
- **What's left:** what exactly earns her support, and whether "He called you first" is still part of the Night 2 argument.

**23. Her dream billboard reads taller than the rest**
- **Status:** Answered
- **Where:** WL:982 (your answer as logged): "Dream Lena at 6'2" is fine (the creator). No change."

### Theo

**24. [CONFIRM] He asks his question twice**
- **Status:** Partly answered
- **Where:** PROSE:180, on the Night 4 escape: "needs you to navigate in low-light, or no-light without Theo? Seems
  cool." PROSE:209: "If he ends up existing in the story at all."
- **What's left:** whether Theo stays; you said nothing about the two questions themselves.

### Mags and the supporting cast

**25. [CONFIRM] Mags came from a corporate sleep lab**
- **Status:** Open (confirm only)
- **Where:** none. Your Mags scene (PROSE:83-92) doesn't touch her past.
- **What's left:** yes or no.

**26. [CONFIRM] Julian Cho**
- **Status:** Partly answered (cut)
- **Where:** PROSE:173: "Julian Cho has been retconned at this point ... It would need to be replaced with something
  else." PROSE:116: "Rejected."
- **What's left:** what replaces him as Mags's past.

**27. [CONFIRM] Rafael Duarte, the other candidate, now covering her shifts**
- **Status:** Partly answered
- **Where:** PROSE:45: "now covers her extra hours // as an intern".
- **What's left:** whether an intern can still be the other candidate for her nurse's post.

**28. Corinne Vasse and Ines Varga**
- **Status:** Partly answered
- **Where:** PROSE:109: "Having an unwelcome visitor is a good diea ... we probably want to reconsider who it is."
  PROSE:115: "Instead of a friend, something else". The clock (12) and the ceramics (19) they carried are gone.
- **What's left:** who the unwelcome Day 2 visitor is. Ines is out with the studio.

**29. [CONFIRM] The mother is never mentioned**
- **Status:** Answered
- **Where:** SB:20: the parents are "not explored within the scope of this game".

### The nights

**30. What each night is about**
- **Status:** Partly answered
- **Where:** Night 2 is re-plotted in your words (PROSE:140): pancakes, hide-and-seek, Lena keeping Elias away, the
  argument, the umbrella. Night 4's dark escape: "Seems cool" (PROSE:180). Night 1: "Rejected" tutorial (PROSE:104) and
  the density question (PROSE:51). Night 3: "Need to expand on this" (PROSE:167).
- **What's left:** what Nights 1, 3, 4 and 5 are about.

**31. The final arena: the car**
- **Status:** Open
- **Where:** SB:26: "tying things together in a neat knot doesn't matter if it doesn't make a sense why they're back at
  the beginning". Your back-seat idea (PROSE:209) leans toward the car but doesn't decide it.
- **What's left:** whether it's the car, and why they're back at the beginning.

**32. Night 3's signature: the badge flips the hospital**
- **Status:** Open
- **Where:** PROSE:160: "the contrast between what Elias wished for the hospital to be, versus what it actually was
  doesn't seem to serve a purpose".
- **What's left:** keep, change or drop the two-moments hospital.

**33. Night 4: no battle; the phone rings mid-chase**
- **Status:** Open
- **Where:** PROSE:184: "What are the consequences of skipping the truth?" PROSE:186-187: "What leads to the truth being
  revealed anyways exactly? And why does it matter, again?"
- **What's left:** waits on the Night 4 truth (13).

**34. [CONFIRM] Days run on time slots; duties give evidence**
- **Status:** Partly answered
- **Where:** SLICE:18 (your 2026-10-09 rule): "Days are real gameplay: short first-person hospital and real-world
  scenes where facts the dream can't give are found."
- **What's left:** the time slots themselves, which you haven't confirmed.

**35. [CONFIRM] Failing costs little**
- **Status:** Open (confirm only)
- **Where:** none for the chase or the argument. Your stealth rule (WL:1375, "Being caught = checkpoint restart or failing the
  subquest") fits it. Your Mags scene ("Undue distress could cause irreverisble damage", PROSE:92) fits "costs his heart".
- **What's left:** yes or no.

**36. [CONFIRM] Build tier A first**
- **Status:** Open (confirm only)
- **Where:** none.
- **What's left:** yes or no. Its Night 4 chase waits on question 13.

### The endings and routes

**37. [CONFIRM] His Choice is shown, both ways, inside Morning**
- **Status:** Answered
- **Where:** VH:32-35 (yours): Morning absorbs His Choice. SB:22: you accepted "it can go either way" ("one note about
  this"). PROSE:56: if she fails to convince him, it's Evening.

**38. [CONFIRM] Evening means he dies**
- **Status:** Answered
- **Where:** SB:24: "Either he wakes up, or he dies in his sleep." PROSE:56: "The ending ends with a memorial and a
  funeral, Nadia left on her own as it rains."
- **Note:** the way it's reached and its last image differ from SB (contradiction 5).

**39. The hope: "Lena got out of the car"**
- **Status:** Open
- **Where:** SB:29: "depends on what we decide for the actual NIght 4."
- **What's left:** waits on question 13.

**40. [CONFIRM] The Force route's rule-break: Lena**
- **Status:** Answered
- **Where:** SLICE:26-27 (yours, 2026-10-09): "In the Force route it's Lena as a character: she breaks the game's rules
  wherever she appears".

**41. [CONFIRM] The Long Night's rule-break: billboards leak into the waking world**
- **Status:** Partly answered
- **Where:** SLICE:28-29 (yours): "the game itself stops behaving normally". SB:28: "deteoriations in the art style,
  colors, harder encounters".
- **What's left:** whether billboards leaking into the waking world is the form it takes (your words fit it, but don't name it).

**42. [CONFIRM] Morning, forced**
- **Status:** Open
- **Where:** PROSE:244: "Both of these probably need complete re-do's. Talk about it." PROSE:56: she "is willing to force
  him out if he won't see reason".
- **What's left:** how the forced ending plays and how it ends.

**43. [CONFIRM] Morning, chosen ends on "Where's Lena?"**
- **Status:** Partly answered
- **Where:** PROSE:56: "When it's amicable, Nadia takes Elias out of the city to go stargazing, to show him that Lena
  lives on in his heart". PROSE:244: needs a redo.
- **What's left:** the rest of the ending, and whether "Where's Lena?" / "We don't know." and the LeBaget plaque stay.

### The battles

**44. [CONFIRM] The concept "Take it apart"**
- **Status:** Answered
- **Where:** WL:1154 (as logged): "a prototype of the argument battle, concept A ("Take it apart"), approved by the
  creator". WL:1391: "structure isn't bad".

**45. [CONFIRM] Every challenge needs a reason, picked from three**
- **Status:** Open (confirm only)
- **Where:** none in words; it's in the prototype you played.
- **What's left:** yes or no.

**46. [CONFIRM] A short action part, only as a consequence**
- **Status:** Partly answered
- **Where:** WL:1391 (yours): "except the Long Night, every attack enters from off-screen or is telegraphed" and "The dodge
  phase feels small next to the thinking." Your additions (ARGUMENT-PROTOTYPE:169-190) build harder phases on it.
- **What's left:** whether the action part stays short and "only as a consequence", since you find it too small.

**47. [CONFIRM] Composure, Press, Let it rest**
- **Status:** Open (confirm only)
- **Where:** none in words; they're in the prototype you played.
- **What's left:** yes or no.

**48. [CONFIRM] Tell arrives with Theo; waking-world arguments have no action part**
- **Status:** Open
- **Where:** PROSE:209 (Theo may go; Lena and Theo may speak from the back seat instead).
- **What's left:** what brings the Tell verb if Theo goes; the waking-world part is unconfirmed.

**49. [CONFIRM] The verbs are the endings**
- **Status:** Partly answered
- **Where:** PROSE:56: Morning when she convinces him or forces him, Evening when "she fails to convince him", the Long
  Night after a scripted loss where "None of the dialogue trees get you out".
- **What's left:** whether Evening can be chosen with "Let it rest", or only reached by failing.

**50. [CONFIRM] Walt's battle as the model of an argument that makes sense**
- **Status:** Partly answered
- **Where:** PROSE:104: "Rejected. A tutorial is nice, but not the way it's currently built. Discuss this after the
  setting talk is done."
- **What's left:** whether Walt's battle survives in another form, and whether it still serves as the model.

**51. The Doctor's battle**
- **Status:** Open
- **Where:** PROSE:167: "Need to expand on this. Possibly the most important battle". PROSE:213: "What is the Doctor's
  truth here?" The proxy form it rested on is cut (11).
- **What's left:** what the Doctor argues and what truth wins it.

### Facts that disagree (DESIGN-DECISIONS: "fix later, nothing to decide")
- 41 against 42 Saturdays: still unfixed, and the game's Lena battle says 42.
- Rafael witnessing the form at 2:47 or 2:50: moot, since the form is cut.
- CH:43 "paint or clay on her hands": still there; stargazing settles it.
- Who forces in the Force route: your PROSE:56 has Nadia "willing to force him out", which matches neither
  wording exactly. Still to tidy.
