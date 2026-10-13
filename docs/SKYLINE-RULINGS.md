# Skyline Repo: the calls Claude made on its own (M0 and M1, to 2026-10-13)

Every decision Claude made without asking, taken from the build ledgers. Each was logged with what it costs if it's
wrong; most are a data change or one function. Your own calls are in `SKYLINE-DESIGN.md` section 14, not here.
Mark any you disagree with and they get reversed.

## Gameplay and balance (these change what you'll feel)

1. **Drones remember you for 8 seconds.** Spotting you again within that time skips their alert delay.
2. **Drones search when they lose you.** A drone that can still see where you were stays put; otherwise it flies to
   the nearest spot that can see that place, waits, and after 8 s goes home. Groups wake together.
3. **The main hall is 14 cover pieces with gaps**, not sealed rooms: where you stand decides which drone group sees you.
4. **The façade's drones cover the middle of the hall**, and the gondolas' rails sit at wall-run height (2.4 m), so the
   fast route along the wall runs past their mines.
5. **Floor-care units are fast and nasty:** 5 m/s, sweeping acid every 1.2 s for 14 damage. *For your play test.*
6. **Drones:** 4° shot spread, 26 damage per bolt, firing every 0.8 s, 70 chassis.
7. **Straight running vs. weaving against drones measures 2.43** (your band is 1.6-2.5; the error range reaches 2.55).
   The settings that pulled it lower took the pressure out of the food court. *For your play test.*
8. **The skylight and façade fights are gentle for a skilled player.** Two pressure tests are set aside for you rather
   than loosened. *For your play test.*
9. **The test bot knows the hall's sightlines exactly.** Accepted: you'd learn them in a run or two; it's its timing
   that had to be human.
10. **Every fight starts from the hall entrance.** After a clear, a lit marker shows there and CONCIERGE asks you to
    return (a draft line for you to rewrite).
11. **Teaching lines keep pace:** each babble lasts as long as its subtitle takes to read (1.5-4 s); combat warnings
    jump ahead of warm-up tips; a warm-up tip more than 20 s stale is dropped. Lines sharing a trigger all still play.
12. **The mezzanine is the north deck only, with no stairs** (nothing in M1 needs you up there; stairs would change the
    balanced sightlines).
13. **The arena's demo recording is up to 120 s** so the scripted player can reach the second fight.

## The look and the feel of hits (from your rules)

14. **A Writ weak spot is cracked casing:** a glowing seam with sparks and exposed wiring, not the plan's gold disc.
    Only weak spots glow or spark on a live bot; plates and bodies flash white and chip.
15. **Fresh wrecks spark, then go dark** when they go stale or are stripped.
16. **"Writ levels 1-3" means the three upgrade tiers;** the capstone raises the weak-spot cap to 5.

## What got built, and in what order

17. **The window-washer gondola became the fourth enemy in Unity** (nothing in the plan owned its Unity side): it rides
    its rail and lays wall mines that arm, warn, then blow. Its own attack is a harmless squeegee swing.
18. **The Unity enemies copy the tested simulation exactly**, so the balance numbers hold in the game.
19. **Hit and target rules were finished before the player and enemies were built on them:** a destroyed enemy can't be
    hit; a hit on you carries where it came from (for a direction cue); aim-assist targets have names for the log.
20. **The frame-rate test runs 45 s,** set in the scene task because no later task owned it.

## Measurement and process (no effect on the game itself)

21. **The fight tests measure over 400 seeds kept apart from the tuning seeds,** and the dodge band counts hits per
    second spent in a shooter's sight.
22. **Plating pressure is a test,** not a printed number: in each fight a typical run drops to 60 plating or lower, and
    at least 20 of 30 runs take damage.
23. **The "skilled player" test bot has human timing** (peeks 1-1.8 s, hides 2-4 s, reacts in 0.15-0.25 s).
24. **The first dodge-test bot dodged the wrong way** (toward and away from the shooter instead of across its fire); it
    was fixed to weave across the line of fire, averaged over several seeds.
25. **Sound panning uses true constant power** (both sides at 0.707 in the middle).
26. **The Unity pass skips recording when the code doesn't compile,** and records in a normal window (batch mode mutes
    audio).
27. **Skyline scenes remove Visiting Hours' shared GameRoot themselves,** instead of editing a file Visiting Hours owns.
28. **Builds and reviews:** work happens on main without a separate worktree; builders and reviewers mostly on Opus
    or Sonnet by the task's weight; the ears and the reference clips were built alongside the game code; the main
    session alone runs Unity.
29. **A few small review notes were fixed early** although the reviews passed (the loadout's first equip no longer logs
    a swap; a PowerShell folder-creation crash).
