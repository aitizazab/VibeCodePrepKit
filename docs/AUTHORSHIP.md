# Visiting Hours: who wrote which text

Written 2026-10-12, after the creator's correction: "I never touched that file [lena_night2.txt], so make sure there's
nothing else you think I completed that just has your placeholders." The precedence rule only works if we know which
words are theirs.

**How this was checked.**
- **Git.** Every commit in this repo carries a Claude co-author line, including the ones that commit the creator's own
  edits, so the trailer can't separate them. The evidence is each commit's message ("the creator's edit", "Story notes
  from the creator"), its diff (`git log -p`, `git blame`, `git show --word-diff`), and WORKLOG.
- **The `//` notes.** In STORY-PROSE and STORY-BIBLE, the creator's notes start with `//`.
- **Who.** **Creator** means the creator typed the words. **Agent** means an agent wrote them, including agent
  records of decisions the creator made in chat. **Mixed** gives line ranges as of commit 28f11b0 or later.

## The game's text and data

| Text | Who | Lines | Evidence |
|---|---|---|---|
| `Argument/Resources/ArgumentText/lena_night2.txt` (the Lena battle) | **Mixed** since 3d4a773 | Creator: the lines changed in 3d4a773 ("the creator's rewrite of Lena's Night 2 battle"), at HEAD about 85 lines, among them `opening`'s `//` note, `pressedReply`, `L1b.sound.reply`, `L1c`, `L4.text`, plus `//` notes on 16 lines (`git blame` the file for the exact lines). Agent: everything else, which is still e94c1e3's placeholders (for example `L3.askAgainReply`, the `L5` lines, `truth_walt`) and 28f11b0's two data fixes | Created in e94c1e3 by the controls agent, moving its own placeholders out of `DreamLenaDemo.cs`. Until 3d4a773 the creator had never touched it ("I never touched that file"), so 28f11b0 was **wrong to treat it as theirs**. The argument agent now edits the live file. |
| `Argument/Resources/ArgumentText/screen.txt` (the battle's interface words) | **Agent** (all of it) | all | e94c1e3 and 7d2600c, both agent commits; no creator edit recorded. |
| `script/opening-script.md` | **Mixed** | Creator: line 9 (the voicemail), line 12 (the news report) and line 15 (the headline). Agent: lines 1-7 (title, instructions and headings, 43cb3f6), lines 17-26 (the notes on broadcast style, 43cb3f6), and the words "a crossfire involving the Larpaca gang" in line 12 (28f11b0) | 43cb3f6 created the file with the **agent's** draft voicemail ("Could I bother you for a... it's five in the morning"), news and headline. 9872706 ("the creator's edited opening script") replaced all three with the creator's text. 06e0330 ("the creator's edit (Larpaca for WeWork; her condolences)") changed two words. WORKLOG:48 shows the audio agent had already swapped both at generation time, so whether the creator typed those two words themselves is worth a confirm. |
| `Dialogue/VisitingHours.ink` (the slice's Day 1 and Night 1 lines) | **Agent** (all of it) | all | One commit, 4c0ceea (2026-10-09). Its own header says "placeholder writing in the creator's direction ... the creator rewrites them". This pass fixed its surname ("VOSS, E." and "Nadia Voss" became Lasname). |
| `Resources/JamKit/VisitingHoursBoot.asset` (saved strings) | **Agent**, partly stale | the `opening:` block (lines 16-126) | Last committed at 7ebae1f (2026-10-09). It still holds the agent's first-draft voicemail lines ("Could I bother you for a...", "...it's five in the morning") and "Missed call: Elias, 5:52 AM". The editor builder (`VisitingHoursOpeningContent.Fill`) refills the live fields from code and the creator's script; the old block is left-over data. |
| `Resources/JamKit/VisitingHoursOpeningEdits.json` | **Creator** (positions, not words) | all | Saved by the opening editor when the creator moves things (WORKLOG 2026-10-12 ~04:10). Untracked. |
| The voicemail recording (`vo_elias_voicemail`) | **Creator's voice, mixed words** | n/a | The creator recorded an early take that mixes the agent's first draft and their own script (SliceContent `VoicemailTrack`: "the take mixes the old and new scripts"). A re-record of their final script is due. |
| The news recording (`vo_news_anchor`) | Creator's words, TTS voice | n/a | Generated from the script by `tools/audio/make-vh-news.py`. It predates 28f11b0, so it still says "LeBaget and the Larpaca corporation". |
| `SliceContent.cs`, `Data/VisitingHoursConfig.asset` (the slice's examine lines, the police report) | **Agent** | all | 4c0ceea, e19fd7f and 0f99efa, all agent commits. 0f99efa (another agent, today) already moved the police report to Lasname, Route 9 and the Larpaca gang, with a TODO for "dead at the scene". |
| `DreamHouseStory.cs` (`DreamHouseLines`, Night 2's house) | **Mixed** | Agent: every line marked `[placeholder]`. Creator: the two endings of the "anything left to find" line (`AllFoundEndings`) | Taken from STORY-PROSE:125 (the creator's `//` note). |
| The title's "Give Up" pill bottle | **Creator** (the idea) | n/a | WORKLOG 2026-10-12 ~06:00. |

## The docs

| Doc | Who | Lines | Evidence |
|---|---|---|---|
| `docs/STORY-PROSE.md` | **Mixed** | Agent: the prose (c6fbdd1). Creator: every `//` note, and the Mags scene at lines 81-92 (732485f, on lines 24, 33, 37-38, 45, 48, 51, 56-58, 76-77, 81-92, 99-100, 104, 109-110, 114-116, 124-125, 132-134, 138-140, 152, 160, 167, 173-174, 180, 184, 186-187, 208-209, 211, 213, 233, 244-245, 253, 266) | 732485f ("Story notes from the creator") only added text: its word diff removes nothing but two words where a note was inserted mid-line. On a mixed line, only the part after `//` is the creator's (on line 45, only "// as an intern"). |
| `docs/STORY-BIBLE.md` | **Mixed** | Agent: everything but the `//` notes (dd72559). Creator: the `//` notes on lines 12-13, 19-26, 28-29 and 36 (732485f) | The same commit; in the decisions table the creator's note shares a row with the agent's text. |
| `docs/DESIGN-DECISIONS.md` | **Agent**, with one creator note | all; a creator `//` at the end of question 1 (3d4a773) | Written by an agent on 2026-10-11 (WORKLOG:1016: "opened in the creator's editor for their answers"). |
| `docs/VISITING-HOURS.md` ("the premise (the creator's)") | **Agent** (a record of the creator's chat decisions) | all | The creator's premise of 2026-10-10 came in chat; agents wrote it down (f9fe474, 69c91a7). The facts are theirs, the wording isn't. |
| `docs/CHARACTERS.md` ("the creator's rules for Lena's look", rounds 2-4) | **Agent** (records of chat) | all; only the quoted phrases are the creator's words (e.g. "4-a is what we'll work with") | 9872706, 88c9662, 31748db, c7d6c5f: agent commits recording the creator's art notes. |
| `docs/SLICE-VISITING-HOURS.md` ("story decisions since draft 1 (the creator, 2026-10-09)") | **Agent** (a record of chat) | all | The examples in it are agent wording, such as "Your phone is at 3%", which the creator later sent back (SB:28). |
| `docs/OPENING-AND-TITLE.md` | **Agent** (a record of the creator's direction) | all | The creator's sketch (`creator-sketch.png`) and the timings they confirmed are theirs. |
| `docs/ARGUMENT-PROTOTYPE.md` ("the creator's additions", "Fair attacks") | **Agent** (a record of chat) | all; the quoted rule is the creator's words | WORKLOG:1154, WORKLOG:1190, WORKLOG:1391. |
| `docs/MUSIC-SKETCHES.md` | **Agent**, quoting the creator | the `>` quotes are the creator's | |
| `docs/argument-ideas/SELECTION.md` | **Agent**; one quoted line is the creator's (line 28) | | |
| `docs/DESIGN-DECISIONS-STATUS.md` | **Mixed** | Creator: the `//` answers under "Still open" and the answers quoted under "Answered" (from 3d4a773, kept word for word) | 3d4a773 committed the creator's answers into the agent's page. |
| `docs/NIGHTS-1-2.md` | **Agent** (main's plan) | all | 3d4a773; its [MAIN'S CALL] items are agent decisions the creator can veto. |
| Everything else under `docs/` (CHARACTER-BIBLE, NIGHTS-GAMEPLAY, ARGUMENT-BATTLES, ARGUMENT-PROSE-BRIEF, STORY-TALK, STORY-FACTS, DREAM-HOUSE, the other argument-ideas, WORKLOG) | **Agent** | all | Agent commits only; none records a creator edit. |

## What was wrongly treated as the creator's
- **The Lena battle text** (`lena_night2.txt`, and by extension `screen.txt`). 28f11b0 and the earlier DESIGN-DECISIONS-
  STATUS framed its relic lines as if the creator had left them stale. At the time they were agent placeholders the
  creator had never touched. `ARGUMENT-PROSE-BRIEF.md` now says so. The creator has since rewritten much of
  `lena_night2.txt` (3d4a773), so the file is now mixed.
- **Nothing else was claimed as the creator's text** beyond the script's three lines and the `//` notes, which check
  out. The agent "records of the creator's decisions" (VISITING-HOURS, CHARACTERS, SLICE) carry the creator's facts in
  agent wording. Where their wording has gone stale (such as the 3% example), it's agent text and can be fixed.
- **Code comments that call agent text the creator's:** `ArgumentProseTests.cs` ("Every word of the battle is in the
  creator's files") and the e94c1e3 commit message mean the files are *for* the creator to edit, not written by them.
