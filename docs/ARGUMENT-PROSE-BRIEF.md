# The argument with Dream-Lena: what each line has to say

> **2026-10-12, the real-time redesign (`docs/redesign/battle.md`, section 9):** new ids in both battle files:
> `<premise>.weak` (the words that glow as a statement flies; the evidence has to hit them), `composureLostReply` (their
> answer as Nadia leaves the table), `showdown.1`.. (the words they cut back with when Nadia presses), `bark.idle`,
> `bark.hesitate`, `bark.glance`, `bark.ring`, `bark.stand`, `pressureWords` (split by `|`). Lena's L5 is rewritten to the
> bad dream as settled (Elias in scrubs walking out of the door: "He's hiding from *him*"; can't be broken tonight), Theo's
> drawing now breaks L3, and L4 has `[sorry=1]` variants for the day Nadia said sorry at his bed. Walt's file is written
> for real. The tables below predate this and are history.

This is a brief for writing the real lines. Every line in the prototype is a placeholder (in
`Packages/com.vibejam.jamkit/Runtime/Core/VisitingHours/Argument/DreamLenaDemo.cs`; the controls agent is moving them into
an editable text file with the same ids). For each line below: **who says it**, **when it shows**, **what it has to get
across** (the logic the battle depends on), and the placeholder. Write over the placeholder freely; keep the job.

## Relic lines: agent placeholders that contradict the creator's decisions (marked 2026-10-12)
**Who wrote the battle text:** agents. Every line in `lena_night2.txt` and `screen.txt` is agent-written placeholder
prose (moved out of `DreamLenaDemo.cs` in e94c1e3); the creator has never edited either file ("I never touched that
file", 2026-10-12; see `docs/AUTHORSHIP.md`). The creator will write the battle's prose. Until then, each placeholder
below that contradicts a decision of theirs is marked **RELIC** in the tables further down. Only factual data was
fixed in the game file (the keepsake's name, the police report card's place, and, waiting in
`docs/_pending/ArgumentText/` while the file is open in their editor, forty-two to forty-one).

| Line ids (in `lena_night2.txt`) | What the placeholder says | What it contradicts | The creator's version |
|---|---|---|---|
| `L1b.sound.reply`, every `L1c` line (the fallback premise L1'') | "He set it every Sunday for a year. You stopped coming." | STORY-PROSE:124: "Nadia shou;dn't have stopped coming to Sunday pancakes, at least if there was no time between the fight and accident" | Nadia never stopped coming; the fourth chair was always hers. L1' needs a different fallback (undecided) |
| `L3.askAgainReply`, every `L5` line (`text`, `askReply`, `concedeLine`/`concedeReply`, `trueReply`, `pressLine`, `pressReply`, `slipName`), `pressedReply` | "To him, you're the bad dream: the woman in scrubs" (Theo's drawing; the paper plane "THE BAD DREAM") | STORY-PROSE:134: "Theo sliding his drawings across the table as some sort of psychological attack is out of character for how Elias would remember his son" | The drawing as an attack is cut. The creator's Night 2 idea: Nadia is the intruder, and Lena keeps Elias away from her, afraid she'd convince him to leave (STORY-PROSE:140). What replaces the buried premise is undecided |
| `personalText` (and the note on `letItRestReply`) | "Lena's apron hair tie." | Lena has no apron, clay or workwear (CHARACTERS, round 3); she's a stargazer (the creator, 2026-10-12) | The keepsake is open, to be decided in discussion. **Changed in the game file** to `[keepsake: undecided]` so play doesn't show the apron |
| `truth_walt` (the card), `L3c.beside`, `L3c.nonsequitur` and their replies, the screen's `menu.noWalt` | Walt's truth ("Patching it isn't fixing it"), won by fully convincing Walt on Night 1 | STORY-PROSE:104: Walt's tutorial is "Rejected ... not the way it's currently built" | Whether Walt's battle survives, and so whether his truth exists as evidence, is undecided. "The same Saturday" (Walt's ledger, a deduction) still breaks L3'' without him |
| `L3c.sound`, `L3c.sound.reply` | "forty-two times" | STORY-PROSE:51 and the ledger: 41 Saturdays | 41: the factual fix (forty-one) waits in `docs/_pending/ArgumentText/lena_night2.txt` |
| `opening` | "Sit down, Nadia. Pancakes are nearly done. He stays." (the battle opens before the pancakes) | STORY-PROSE:140: Nadia sits down for pancakes and plays hide-and-seek waiting for Elias; when he never comes she questions Lena, and Lena admits she won't let him go | The battle starts after the pancakes and the game, from Nadia's question about Elias |
| `L4` (all lines) | "He called you first. You didn't pick up. So he called me." | Not a contradiction, but undecided: whether it stays in Night 2's argument (DESIGN-DECISIONS question 22). Nadia's guilt is survivor's guilt, not blame for the crash (STORY-PROSE:37) | Undecided |
| The outcomes table above | Convinced and Heard out both leave Lena "Amenable" | STORY-PROSE:140: the battle ends with Lena "unsure of her conviction" (open to giving her blessing later) or condemning Nadia | Lena ends unsure or condemns |
| `fact_police_report.note` | "The crash on Larch Street." | The crash was on Route 9 (Larch Street is the dream's childhood street) | **Changed in the game file** to "The crash on Route 9." |

## The situation
Night 2, the kitchen of the dream house. Dream-Lena (Elias's wife, as his dream remembers her) is making pancakes. Her
**claim**: Elias should stay in the dream; this is his home. Nadia has to take the claim apart, or hear Lena out.

## How the encounter flows
1. **Opening.** Lena states the claim. A short pressure phase (the dodge box) follows: the argument starting.
2. **The claims screen.** The claim sits in the middle. Under it are its **premises**: the reasons Lena believes it.
   Some show at the start (L1, L2, L3); the rest are hidden ("?") and appear as the argument digs.
   - A premise is a **fact** (something that happened) or a **link** (a step of reasoning: "so...").
   - Facts are usually **true**: Nadia can't break them, only concede them. Conceding a true thing calms her.
   - The **key** links are the false steps. Break those, and the claim falls.
3. **Nadia's verbs**, on any premise:
   - **Ask** ("Why?"): Lena explains. Asking can reveal the premise underneath.
   - **Challenge**: show a piece of evidence, then pick **why** it answers the premise, from cards. One card is
     sound, one is true but beside the point, one doesn't follow (and sometimes one antagonises her). Only the sound
     one breaks the premise; each wrong one gets its own reply saying why it doesn't work.
   - **Concede** ("That's true."): agree. On a true fact, that calms Nadia and often reveals the next link.
   - **Press** (hold): just insist, louder. It forces a piece but costs: heat, and Lena ends up colder.
   - **Let it rest**: once every premise has been heard, Nadia can stop arguing and listen.
4. **Pressure phases**: when the argument heats up (a wrong reason, a press, an antagonising reply, or something
   painful surfacing), the dodge box comes back for 5–8 s. Getting hit raises Nadia's heart rate (70 to 160 bpm);
   160 ends the battle.
5. **Endings**:

   | Ending | What happened | Lena's stance |
   |---|---|---|
   | Convinced | Nadia broke every key link without pressing | Amenable (she gives up a truth) |
   | Heard out | Nadia let it rest | Amenable (she gives a keepsake) |
   | Pressed | Nadia forced it | Unsure, or condemns |
   | Composure lost | Nadia's heart hit 160 | Unsure |
   | Yielded | Nadia conceded the claim itself | Unsure |

   Anger (each antagonising reply) pushes the stance toward **condemns**. Three antagonisms always mean she condemns.

## The shape of Lena's case (why the lines are what they are)
Lena's case has three branches, and one buried root:
- **L1 branch, "nothing's left for him out there":** true that Theo (their son) is dead out there. False that this
  means nothing's left: in the dream, Elias still sets a fourth chair, for Nadia. Lena falls back to a true sting
  (L1''). **RELIC:** the placeholder sting is that Nadia stopped coming on Sundays, which the creator rejected (see
  Relic lines).
- **L2 branch, "waking means searching forever":** true that nobody knows where the real Lena is. The link (L2') **can't
  be broken tonight**: Nadia can't promise an ending. That's the lesson; conceding it is right.
- **L3 branch, "he chose this":** half true. False that this is the life he wanted: he worked that Saturday, so this
  breakfast is a regret, not a choice. Lena falls back: "then let him have his wish" (L3''), which only breaks with
  **Walt's truth** or **the same Saturday**: he's lived this morning 41 times, and Walt patches the same flat tyre
  every time. A wish that needs repairing every night isn't peace. **RELIC:** Walt's truth depends on Walt's battle,
  which the creator rejected as built; the placeholder line says forty-two.
- **The painful premise (L4)** surfaces by itself after L1'' or L3': Elias called Nadia first that night, and she didn't
  pick up. It isn't arguable; it's the truth the night is about.
- **The buried root (L5)**: **RELIC.** The placeholder (to Elias, Nadia is the bad dream, from Theo's drawing) is cut
  as out of character. What the buried premise is now is undecided. It's found by catching a slip (the paper plane) or
  asking about L3 twice.

---

## The lines

### The claim and the frame
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `claim` | The centre of the screen, the whole battle | Lena's position in one short line: he stays, this is home | He stays. This is his home. |
| `opening` | Lena, as the battle starts | **RELIC** (the battle now starts after the pancakes, from Nadia's question about Elias). The claim, said as settled fact | Sit down, Nadia. Pancakes are nearly done. He stays. This is his home. |
| `noTouchReply` | Lena, when Nadia's evidence doesn't touch that premise | "That isn't what I'm saying": a redirect, a little irritated | That's not what I said. Listen to me. |

### Ending the argument on the claim itself
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `pressClaimLine` | Nadia, pressing the claim (forcing it) | A flat refusal, raised voice | He is NOT staying. |
| `pressClaimReply` | Lena, answering | Bitter surrender; an old grievance against Nadia (and the family) | ...Then take him. You always take everything. |
| `concedeClaimLine` | Nadia, conceding the claim (giving in) | Nadia gives up: maybe he should stay | ...Maybe he should stay. |
| `concedeClaimReply` | Lena, answering | Relief and warmth; back to the domestic | Thank you. Now eat something. |
| `letItRestLine` | Nadia, letting it rest | Nadia stops fighting and simply hears her | I hear you, Lena. |
| `letItRestReply` | Lena, answering; she hands over the keepsake | She's moved that Nadia listened; gives the keepsake (undecided) "for him" | ...You do, don't you. Here. Take this. Keep it for him. |

### The outcome lines
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `truthText` | The truth Nadia wins (Convinced), shown and kept | What Nadia now understands about the real Lena: she wouldn't let him hide; she'd want him to live, even without her | The Lena he remembers wouldn't let him hide. She'd want him to live, even without her. |
| `personalText` | The keepsake Nadia gets (Heard out) | Names the object. **RELIC:** the apron hair tie is gone (no apron); the object is undecided | [keepsake: undecided] |
| `convincedReply` | Lena, Convinced | She sees it: the real Lena would hate her keeping him here | ...She'd hate this. Me, keeping him here. She'd hate it. |
| `heardOutReply` | Lena, Heard out | Not convinced, but respected | I still think he's safer here. But I heard you. |
| `pressedReply` | Lena, Pressed | **RELIC** (sets up the cut "bad dream" premise). Nadia won by force | ...You sound like the bad dream. |
| `composureLostLine` | Nadia, at 160 bpm | Panic; she has to stop | I can't... I can't breathe in here. |

### L1, a fact (true): "Out there, Theo is dead."
Visible from the start. Can't be broken; conceding it reveals L1'.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L1.text` | The premise card | Their son is dead in the real world | Out there, Theo is dead. |
| `L1.askReply` | Lena, if asked "why?" | Nadia knows it (the police report); her grief | You read the report. You know what happened to my boy. |
| `L1.concedeLine` | Nadia, conceding | Gentle agreement | ...Out there, Theo is gone. |
| `L1.concedeReply` | Lena | Draws the conclusion: so there's nothing left for him (states L1') | Yes. So out there, there's nothing left for him. |
| `L1.trueReply` | Lena, if Nadia tries to challenge something true | Don't you dare deny it | Don't. Don't you dare tell me that isn't true. |
| `L1.pressLine` | Nadia, pressing | Lashing out, refusing to hear it | Stop saying that! |
| `L1.pressReply` | Lena | Volume doesn't change the fact | Saying it louder doesn't bring him back. |

### L1', a link (false, KEY): "So there's nothing left for him out there."
Hidden until L1 is conceded. Broken with **the fourth chair**; Lena then falls back to L1''.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L1b.text` | The premise card | The (wrong) step from "Theo is dead" | So there's nothing left for him out there. |
| `L1b.askReply` | Lena | What's out there: an empty house and a hospital bed | What's out there? An empty house and a hospital bed. |
| `L1b.pressLine` / `pressReply` | Nadia insists / Lena | Nadia: there's plenty. Lena: name one thing | There's plenty left! / Name one thing. You can't. |
| Sound reason | Nadia, with the fourth chair | **The proof**: in the dream he still sets a place for someone, and it's for Nadia. So someone is out there | In here he still sets a place for someone. He sets it for me. There's someone out there. |
| its reply | Lena, rattled | **RELIC** (Nadia never stopped coming). Concedes the point, but stings back (this is L1'') | ...He set it every Sunday for a year. You stopped coming. |
| Beside the point | Nadia | True, but it doesn't answer "nothing is left" | Theo would want his dad to wake up. |
| its reply | Lena, angry | Why it misses: you don't speak for my son | Don't tell me what my son would want. |
| Doesn't follow | Nadia | A bad inference from the same evidence | He set a chair, so he's ready to leave. |
| its reply | Lena | Why it doesn't follow | Setting a table isn't packing a bag. |
| Antagonising | Nadia | Dismisses her as unreal instead of answering (makes her angrier, harder patterns) | You're a dream. A dream doesn't get a say in his life. |
| its reply | Lena, angry | Hurt and fury; throws Nadia out | A dream. Is that all I am to you? Get out of my kitchen. |

### L1'', a fact (true): "He set it every Sunday for a year. You stopped coming."
**RELIC, the whole premise:** the creator rejected Nadia abandoning Sunday pancakes (STORY-PROSE:124). Lena's fallback
here needs new content (undecided).
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L1c.text` | The card | Nadia abandoned the Sunday dinners | He set it every Sunday for a year. You stopped coming. |
| `L1c.askReply` | Lena | Rubs it in: your chair, empty | Every Sunday. Your chair. Empty. |
| `L1c.concedeLine` / `concedeReply` | Nadia admits it / Lena | Admission / grudging acceptance | I did. I stopped coming. / ...At least you admit it. |
| `L1c.trueReply` | Lena | It's true, don't argue | You know it's true. Don't argue with me about it. |

### L2, a fact (true): "You don't know where I am. Neither does he."
Visible from the start. Lena also **asks Nadia a question** here, which Nadia can dodge with a Retort (antagonising).
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L2.text` | The card | The real Lena is missing; nobody knows where | You don't know where I am. Neither does he. |
| `L2.question` | Lena, asking Nadia directly | A challenge Nadia can't answer | Then tell me where I am, Nadia. |
| `L2.askReply` | Lena | The official word: missing | Missing. That's the word they used. Missing. |
| `L2.concedeLine` / `concedeReply` | Nadia / Lena | Admission / Lena draws L2': waking means searching forever | No. I don't know where you are. / So waking up means searching. Forever. |
| `L2.trueReply` | Lena | Then answer the question | Then where am I? Go on. |
| Retort | Nadia, dodging the question | Attacks her reality instead of answering | You don't get to ask me that. You're not real. Stop acting like you are. |
| its reply | Lena, angry | Defends her reality with the kitchen around them | Not real. Then whose kitchen is this? Whose pancakes are these? Say it to my face. |

### L2', a link (half true, UNBREAKABLE tonight): "So waking means searching forever."
The lesson: Nadia can't promise an ending. Conceding is the right move.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L2b.text` | The card | Waking up would mean an endless search for her | So waking means searching forever. |
| `L2b.askReply` | Lena | He'd look every night and never find her | Every night he'd go looking. Every night he wouldn't find me. |
| `L2b.concedeLine` / `concedeReply` | Nadia / Lena | "It might." / then let him rest | ...It might. / Then let him rest. |
| `L2b.unbreakableReply` | Lena, if Nadia tries to break it | Why it can't be broken: nobody can promise an ending | You can't promise him an ending. Nobody can. Not tonight. |

### L3, a link (half true): "He chose this. I know him."
Visible from the start. Asking reveals L3'; asking **twice** reveals L5.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L3.text` | The card | Elias chose the dream; she knows him best | He chose this. I know him. |
| `L3.askReply` | Lena | Twelve years married; this is the life he wanted (states L3') | Twelve years. I know what he wants. This is the life he wanted. |
| `L3.askAgainReply` | Lena, asked again | **RELIC** (the cut "bad dream"). Lets slip what lies under it (reveals L5) | ...You want to know what he dreams about? The bad dream. A woman in scrubs who comes to take us away. |
| `L3.concedeLine` / `concedeReply` | Nadia / Lena | You know him best / so trust me | You know him better than anyone. / I do. So trust me. |
| `L3.trueReply` | Lena | You weren't there | You weren't there. I was. |

### L3', a link (false, KEY): "This is the life he wanted."
Broken with **Saturday off**; Lena falls back to L3''.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L3b.text` | The card | This breakfast is his chosen life | This is the life he wanted. |
| `L3b.askReply` | Lena | Pancakes, Sunday, all of us | Pancakes. Sunday. All of us at the table. What else would he want? |
| `L3b.pressLine` / `pressReply` | Nadia / Lena | "This isn't a life!" / "it's all he's got left" | This isn't a life! / It's the only one he's got left. |
| Sound reason | Nadia, with Saturday off | **The proof**: he worked that Saturday; this is the breakfast he missed. A regret, not a choice | He worked that Saturday. This is the breakfast he never got to have. That's a regret, not a choice. |
| its reply | Lena, rattled | Concedes, falls back to L3'' | ...A regret. Fine. Then let him have his wish. |
| Beside the point | Nadia | True, but it supports Lena | He loved Saturdays with you. |
| its reply | Lena | "Exactly my point" | Yes. That's my point. |
| Doesn't follow | Nadia | An unfair leap from the evidence | He worked that day, so he never really wanted a family. |
| its reply | Lena, angry | Cruel, and false | That's cruel, and it isn't even true. |

### L3'', a link (false, KEY): "Then let him have his wish."
Breaks only with **Walt's truth** or **the same Saturday** (both give the same reasons). **RELIC:** Walt's truth
depends on Walt's battle, rejected as built.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L3c.text` | The card | Even if it's a regret, let him keep it | Then let him have his wish. |
| `L3c.askReply` | Lena | Just one morning | One morning. Let him keep one morning. |
| `L3c.pressLine` / `pressReply` | Nadia / Lena | "He can't have it" / you'd take this too? | He can't have it! / You'd take that from him too? |
| Sound reason | Nadia | **The proof**: he's lived this morning 41 times, and Walt patches the same flat every time. A wish that needs repairing every night isn't peace. **RELIC:** the placeholder says forty-two | He's had this morning forty-two times, and Walt patches the same flat every one. A wish you have to repair every night isn't peace. |
| its reply | Lena, softened | Shock: she thought it was the first time, every time. **RELIC:** forty-two, not 41 | ...Forty-two. I thought it was the first time. Every time. |
| Beside the point | Nadia | True, but it supports staying | Walt misses him too. |
| its reply | Lena | Everyone misses him, which is why he should stay | Everyone misses him. That's why he should stay. |
| Doesn't follow | Nadia | An appeal to authority | Walt said so, so it's true. |
| its reply | Lena | Dismissive | Walt says a lot of things. |

### L4, a fact (true, PAINFUL): "He called you first."
Whether this stays in Night 2's argument is undecided. Surfaces by itself (with the high pressure phase) after L1'' or L3'. Lena asks a question; Nadia can retort.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L4.text` | The card | Nadia isn't here for him. That night he called her first; she didn't pick up; so he called Lena | You're not here for him. He called you first. You didn't pick up. So he called me. |
| `L4.question` | Lena | The question at the heart of the game | Why didn't you pick up, Nadia? |
| `L4.askReply` | Lena | What followed: three rings, then Lena went out in the rain | Three rings. Then he called me. And I went out in the rain. |
| `L4.concedeLine` / `concedeReply` | Nadia admits it / Lena | The admission; Lena's thanks for it | ...I know. I didn't pick up. / ...Thank you for saying it. |
| `L4.trueReply` | Lena | The evidence is on her phone | Check your phone. It's all there. |
| Retort | Nadia, dodging | Calls her a dream wearing Lena's face | Stop pretending you're her. You're a dream wearing her face. |
| its reply | Lena, angry | Sees through it (Nadia is shaking); throws her out | Then why are you shaking? Get out. Get out of my house. |

### L5, a link (half true, the buried root): "To him, you're the bad dream."
**RELIC, the whole premise:** Theo's drawing as an attack is out of character (STORY-PROSE:134). What replaces it is
undecided. Found by catching the paper-plane slip or asking L3 twice.
| Id | Who / when | What it must get across | Placeholder |
|---|---|---|---|
| `L5.slipName` | The slip, as it flies | What the paper plane says | a paper plane: THE BAD DREAM |
| `L5.text` | The card | In Elias's dream, Nadia is the woman in scrubs who takes people away | To him, you're the bad dream: the woman in scrubs who takes people away. |
| `L5.askReply` | Lena | Theo drew her, from what his dad says in his sleep | He drew you. Theo drew you, from what his dad said in his sleep. |
| `L5.concedeLine` / `concedeReply` | Nadia / Lena | "Maybe I am." / then prove you're not | ...Maybe I am. / Then prove you're not. |
| `L5.trueReply` | Lena | Look at the drawing | Look at the drawing. Look at it. |

### The evidence cards (name + note)
| Id | Name | What it is |
|---|---|---|
| `mem_fourth_chair` | the fourth chair | A place set for someone who isn't here (breaks L1') |
| `ded_saturday_off` | Saturday off | He was on shift the day this breakfast never happened (breaks L3') |
| `truth_walt` | Walt's truth | "Patching it isn't fixing it." (breaks L3''). **RELIC:** depends on Walt's battle, rejected as built |
| `ded_same_saturday` | the same Saturday | The same day every night: the same flat tyre (breaks L3'') |
| `ded_why_lena_came` | why Lena came out | The call log and the voicemail (no premise uses it yet) |
| `fact_chart` | his chart | Bed 14 (a decoy) |
| `fact_police_report` | the police report | The crash on Route 9 (a decoy) |
| `fact_photo` | the family photo | Four of them, at the lake (a decoy) |

### The screen's own words (prompts, not dialogue)
These are the interface: instructions and the transcript's sentences. They also need to be clearer.
| Where | What it must say | Placeholder |
|---|---|---|
| Ask button / Nadia's Ask line | Nadia asks why | Why do you say that? |
| Challenge, step 1 | Pick the evidence to show | Show what? (pick evidence) |
| Challenge, step 2 | Pick why the evidence answers the premise | Why does it answer that? (pick the reason that follows) |
| Concede line (Nadia) | Nadia agrees | (each premise's own concede line) |
| Under the claim | How to win | The claim. Break its key premises, or let it rest once you've heard them all. |
| Challenging the claim directly | You can't attack the claim; attack its premises | The claim stands on its premises: challenge those. |
| Challenging a true fact | It's true; concede it instead | That's a true spoke. Leave it. |
| Let it rest too early | Hear every premise first | Not yet: hear every premise first (hidden ones too). |
| Pressure phase | Dodge | Pressure: stay clear of ... |
| Transcript, a break | You said X; this evidence shows Y; so X doesn't hold | You said X. The fourth chair shows Y. So X doesn't hold. |
| Transcript, others | "Nadia conceded ...", "Nadia forced ...", "Nadia caught ..." (a slip), "Nadia's heart reached ..." | |
