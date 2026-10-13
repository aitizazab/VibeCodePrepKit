# Skyline Repo: candidate reference clips (gun feel)

These clips are **measuring sticks only**. They are never shipped, never copied into `Assets/` or the tools, and
never committed. They live in `out/skyline-refs/` (git-ignored by `/out/`). The Writ and Breacher agents (Tasks 18-19)
compare their forged sounds against the ones marked approved with `listen.py compare`.

Re-downloading: every row gives the video and the time range in the *source video's* timeline. The files in
`out/skyline-refs/raw/` are the audio-only downloads (`yt-dlp -f bestaudio`, native Opus, whole video); the trimmed
clips are 48 kHz 16-bit WAV, stereo as the source, cut with a 20 ms pre-roll and the tail up to the next sound.
Rejected clips and why are at the end. Spectrogram sheets: `out/skyline-refs/sheets/<gun>-<clip>.png`.

Measurements come from `listen.py measure` (peak = sample peak / true peak in dB; LUFS integrated; attack and tail in
ms; centroid in Hz; band shares sub / low / mid / presence / air). All kept clips: 0 clipped samples, no music or
speech visible in the spectrogram. Everything is YouTube audio (lossy), so treat levels as relative, not absolute.

How the cuts were found: the ears' event list was too dense on reverb tails, so I used a 5 ms envelope with an
onset rule (a rise of 12 dB or more over the previous 40 ms, near the window maximum) and checked the result against
spectrogram pictures. I did not listen; the "why it fits" column is from the spectrum, the timing and the weapon's
known identity. **Audit 2026-10-13:** the "why it fits" column below was written without checking for mixed sounds and
was wrong for two clips; the "Contents" section below lists what each clip and each new clean piece really holds.

## Writ (precise sidearm, hand-cannon weight, low RPM)

| Event | File | Game | Weapon | Video + time range | Why it fits | Measurements | Approved? |
|---|---|---|---|---|---|---|---|
| shot | `writ/shot-destiny2-1.wav` | Destiny 2 | a hand cannon (compilation does not name it) | [Hand Cannons reference audio, Noah Sitrin](https://www.youtube.com/watch?v=JUvAJozy2kk) 2.31-4.5 s | In-game audio, no music; slow single shot, 2 s tail, the cleanest "hand cannon weight" stick. **Corrected 2026-10-13: the file is not just a shot, its last 0.1 s (from 2.09 s) is the start of a cocking click.** Use `writ/shot-destiny2-1-clean.wav` (shot only) and `writ/cock-destiny2-1.wav` (the click) | pk -12.4 / tp -12.1; -26.3 LUFS; atk 31 ms; tail 2129 ms; cen 450 Hz; 0/.56/.40/.03/.00 | Not picked yet (Writ being reworked) |
| shot | `writ/shot-destiny2-2.wav` | Destiny 2 | hand cannon (same compilation) | same video 27.60-30.2 s | Second shot of a different weapon in the list; a heavier low body | pk -13.1 / tp -12.4; -26.5 LUFS; atk 31 ms; tail 2518 ms; cen 428 Hz; .01/.61/.35/.03/.00 | |
| shot | `writ/shot-destiny2-3.wav` | Destiny 2 | hand cannon (same compilation) | same video 32.84-35.3 s | Same family, slightly different tail | pk -12.5 / tp -11.9; -26.7 LUFS; atk 31 ms; tail 2429 ms; cen 407 Hz; .01/.60/.37/.02/.00 | |
| shot | `writ/shot-destiny2-4.wav` | Destiny 2 | Cryosthesia 77K (exotic sidearm) | [Cryosthesia 77K, Ultraclocked](https://www.youtube.com/watch?v=P_QDxu_sHMA) 4.21-5.6 s | A brighter, snappier sidearm shot to bracket the heavier hand cannons; clipped short by the next shot at 5.68 s | pk -8.8 / tp -8.3; -27.9 LUFS; atk 34 ms; tail 1340 ms; cen 2004 Hz; .10/.14/.44/.28/.05 | |
| shot | `writ/shot-cyberpunk-1.wav` | Cyberpunk 2077 | Malorian Arms 3516 | [Malorian Arms 3516 Animations & Sounds, Ultraclocked](https://www.youtube.com/watch?v=rG9_I81dcZI) 3.56-5.2 s | The "Johnny's hand cannon" identity: low body with crack; slow cadence (about 1.7 s between shots in this capture) | pk -6.0 / tp -5.9; -20.5 LUFS; atk 51 ms; tail 1423 ms; cen 1356 Hz; .03/.57/.22/.15/.04 | |
| shot | `writ/shot-cyberpunk-2.wav` | Cyberpunk 2077 | Malorian Arms 3516 | same video 8.58-10.1 s | Second take for a spread | pk -6.5 / tp -6.0; -20.8 LUFS; atk 35 ms; tail 1402 ms; cen 1714 Hz; .01/.50/.24/.19/.06 | |
| reload-partial | `writ/reload-partial-cyberpunk-1.wav` | Cyberpunk 2077 | Malorian Arms 3516 | [All Weapons Reload Animations, snyplays](https://www.youtube.com/watch?v=TRgsBfXW1B4) 106.15-116.75 s (chapter "Malorian Arms 3516") | Clean handling clicks with a quiet bed, no shots | pk -4.7 / tp -4.4; -19.0 LUFS; atk 636 ms; tail 1281 ms; cen 6081 Hz; .01/.04/.20/.40/.35 | |
| reload-partial | `writ/reload-partial-witchfire-1.wav` | Witchfire | "Hunger" chapter of the All Weapons video (pistol-class) | [Witchfire All Weapons, snyplays](https://www.youtube.com/watch?v=Pyi807Mi7vQ) 10.55-12.75 s (chapter Hunger, 0:00-0:15) | Short, light mechanical clicks after the shots; a small-sidearm reload | pk -5.0 / tp -3.9; -26.4 LUFS; atk 341 ms; tail 401 ms; cen 2725 Hz; .14/.10/.39/.27/.10 | |
| reload-partial | `writ/reload-partial-witchfire-2.wav` | Witchfire | "Hangfire" chapter (pistol / revolver-class) | same video 26.85-28.45 s (chapter Hangfire, 0:15-0:31) | Heavier revolver-like handling, two clear clicks | pk -1.5 / tp -1.4; -19.2 LUFS; atk 91 ms; tail 1310 ms; cen 1729 Hz; .23/.29/.21/.22/.05 | |

Not collected for the Writ: `dry`, `reload-empty`, `equip`, `inspect`, `impact` (no clean isolated source found in the
time-box); a Destiny 2 reload (every reload source found was slow-motion or had music); a Witchfire shot (see the
rejected list).

## Breacher (door-breach shotgun, heavy knockback, mechanical cell reload)

| Event | File | Game | Weapon | Video + time range | Why it fits | Measurements | Approved? |
|---|---|---|---|---|---|---|---|
| shot | `breacher/shot-destiny2-1.wav` | Destiny 2 | The Fourth Horseman (exotic shotgun) | [The Fourth Horseman, Ultraclocked](https://www.youtube.com/watch?v=ignEBRBZ0cU) 9.79-12.3 s | Isolated single blast, 2.4 s tail, mostly low body: the "heavy knockback" stick (audit 2026-10-13: one event only, no click or second sound found) | pk -4.0 / tp -4.0; -17.3 LUFS; atk 109 ms; tail 2369 ms; cen 570 Hz; .02/.76/.15/.05/.02 | **PICKED by the creator (2026-10-13): "1. Fourth Horseman single shot"** |
| shot | `breacher/shot-destiny2-2.wav` | Destiny 2 | The Fourth Horseman | same video 29.49-31.9 s | Even lower and darker (92% in the low band) | pk -4.3 / tp -4.3; -14.4 LUFS; atk 110 ms; tail 2129 ms; cen 163 Hz; .01/.92/.06/.01/.00 | |
| shot | `breacher/shot-cyberpunk-1.wav` | Cyberpunk 2077 | Carnage (Budget Arms shotgun) | [Carnage Shotgun, BGV-Gaming](https://www.youtube.com/watch?v=ft8TraQ4M3k) 0.29-2.2 s | Carnage is the "door-breach" gun by name; sub-heavy blast with a short crack | pk -8.9 / tp -8.8; -21.6 LUFS; atk 25 ms; tail 1586 ms; cen 554 Hz; .19/.59/.15/.06/.01 | |
| shot | `breacher/shot-cyberpunk-2.wav` | Cyberpunk 2077 | Carnage | same video 4.47-6.5 s | Second take | pk -8.9 / tp -8.8; -21.6 LUFS; atk 41 ms; tail 1668 ms; cen 639 Hz; .15/.64/.12/.07/.01 | |
| shot | `breacher/shot-cyberpunk-3.wav` | Cyberpunk 2077 | Mox (shotgun) | [Mox Animations & Sounds, Ultraclocked](https://www.youtube.com/watch?v=MWSL0mrGAtk) 1.55-4.95 s | A louder, brighter blast with a long (3 s) room tail | pk -2.8 / tp -2.8; -16.4 LUFS; atk 18 ms; tail 3077 ms; cen 1072 Hz; .19/.47/.18/.14/.02 | |
| shot | `breacher/shot-cyberpunk-4.wav` | Cyberpunk 2077 | Mox | same video 8.37-11.4 s | Second take | pk -2.5 / tp -2.3; -16.6 LUFS; atk 13 ms; tail 2698 ms; cen 1023 Hz; .12/.50/.22/.13/.02 | |
| shot | `breacher/shot-witchfire-1.wav` | Witchfire | Echo (shotgun) | [Let's use the Echo (Shotgun), Arjan Nuts Gaming](https://www.youtube.com/watch?v=9BiRn52CHy8) 23.12-25.5 s | The Witchfire shotgun without clipping; sits under a game ambience bed | pk -10.9 / tp -10.6; -30.0 LUFS; atk 39 ms; tail 2337 ms; cen 1107 Hz; .23/.40/.26/.06/.05 | |
| shot | `breacher/shot-witchfire-2.wav` | Witchfire | Echo | same video 33.25-35.75 s | Same weapon; 90% of the energy is below 60 Hz and the tail beats at a low rate, so a drone or ambience is probably under it: use clip 1 first | pk -11.2 / tp -11.2; -27.9 LUFS; atk 59 ms; tail 2383 ms; cen 111 Hz; .90/.04/.06/.01/.00 | |
| reload-partial | `breacher/reload-partial-witchfire-1.wav` | Witchfire | Echo | [Witchfire All Weapons, snyplays](https://www.youtube.com/watch?v=Pyi807Mi7vQ) 222.3-225.3 s (chapter Echo, 3:25-3:48) | Clean mechanical clicks and a pump, light bed (audit 2026-10-13: a run of shell-insert groups, no shot; the "pump" is a guess from a low thump at 2.65 s) | pk -9.3 / tp -9.3; -29.2 LUFS; atk 199 ms; tail 2798 ms; cen 4771 Hz; .03/.07/.25/.42/.23 | **PICKED by the creator (2026-10-13): the reload reference** |
| equip | `breacher/equip-witchfire-1.wav` | Witchfire | Echo | same video 207.4-208.9 s | **Corrected 2026-10-13: not a pure equip.** Two shell inserts (0.06-0.42 s and 0.52-0.93 s) play before the equip, which only starts at about 1.0 s. Use `breacher/equip-witchfire-1-clean.wav` for the equip, and the two `shell-insert-witchfire-N.wav` pieces as reload references | pk -10.4 / tp -10.4; -28.9 LUFS; atk 351 ms; tail 292 ms; cen 4209 Hz; .03/.07/.32/.40/.19 | **PICKED by the creator (2026-10-13), but the picked sound is the clean piece, not this mixed file** |
| reload-partial | `breacher/reload-partial-cyberpunk-1.wav` | Cyberpunk 2077 | Carnage | [All Weapons Reload Animations, snyplays](https://www.youtube.com/watch?v=TRgsBfXW1B4) 651.2-659.3 s (chapter Carnage, 10:50-11:12) | Heavy clunks and a bolt-like action: the closest thing to a mechanical cell reload in the three games | pk -4.9 / tp -4.4; -20.3 LUFS; atk 3879 ms; tail 3732 ms; cen 2139 Hz; .23/.36/.08/.25/.08 | |
| reload-empty | `breacher/reload-empty-cyberpunk-1.wav` | Cyberpunk 2077 | Carnage | same video 659.5-672.3 s | The longer second sequence of the chapter | pk -4.2 / tp -3.8; -20.6 LUFS; atk 404 ms; tail 8168 ms; cen 2677 Hz; .18/.31/.12/.29/.10 | |

Not collected for the Breacher: `dry`, `inspect`, `impact`, a Destiny 2 reload or equip; a Witchfire `reload-empty`.

## Breacher picks (the creator, 2026-10-13)

| Event | Pick |
|---|---|
| shot | `breacher/shot-destiny2-1.wav` ("1. Fourth Horseman single shot"); audited, a single event, nothing to cut |
| reload | `breacher/reload-partial-witchfire-1.wav` (Witchfire Echo) |
| reload, extra references | `breacher/shell-insert-witchfire-1.wav` and `breacher/shell-insert-witchfire-2.wav` (the two single shell inserts cut out of the old equip clip) |
| equip | `breacher/equip-witchfire-1-clean.wav` (the Witchfire Echo equip without the two shell inserts that came before it) |

The Writ's clips are not picked yet (its design is being reworked). Its default shot is now
`writ/shot-destiny2-1-clean.wav`, with the cocking click kept apart as `writ/cock-destiny2-1.wav`.

## Contents (checked with the ears, 2026-10-13)

The creator noticed that the Echo "equip" clip held two shell inserts before the equip, and that the Writ's default shot
held a cocking click, so every clip was audited again. **How, and how far to trust it:** `listen.py events` (also with
`--range-db 50`) and `measure` on every clip, plus a high-pass (above 2.5 kHz) click finder and 5-50 ms level envelopes
of my own to tell clicks (short, high-frequency) from tail ripples (low-frequency, no click content), and spectrogram
pictures for the equip and Echo reload. **Gemini did not answer:** every `judge` call to `gemini-3.8-flash` came back
HTTP 503 ("high demand") or timed out, on the first try and on the retry (8 calls on 4 clips, 0 successes), so I stopped
calling it. No "heard" list exists for any clip. CLAP (local) was run on 9 clips only (the five reloads, `writ/shot-destiny2-1`
and the two Fourth Horseman shots; I stopped it to save time): `yes` for the three Cyberpunk and Witchfire-Hunger
reloads and the Writ shot, `unsure` for the picked Echo reload and the picked Fourth Horseman shot (best rival
"a sword clash" and "a loud explosion" within 0.03), `no` for `writ/reload-partial-witchfire-2` and
`breacher/shot-destiny2-2` (a contrast phrase ranked above the brief). It is weak evidence (it cannot name a click, and
it tells nothing about the cuts). So the labels below are **from the events, the measurements and the spectrograms,
not from listening or from a model's description**, and they are checked against the creator's own report for the two
clips they heard. A label "click" means a short high-frequency transient; "thump" means a short low-frequency one;
"ripple" means a faint low-frequency bump in a reverb tail with no high-frequency content (the ears' event detector
reports these as false events, which is why `events` still lists them in the "clean" Writ shot). Times are seconds
inside the file.

### New clean pieces

| File | Dur | Events (time: label) | Cut from | Use |
|---|---|---|---|---|
| `writ/shot-destiny2-1-clean.wav` | 2.06 s | 0.02: shot (atk 31 ms, pk -12.4 dBFS, -26.7 LUFS, cen 476 Hz); faint tail ripples at 1.0, 1.16, 1.48, 1.55, 1.69, 1.90 (no click content); tail decays to about -50 dB by 2.0 | original 0-2.06 s (silent gap at 2.0-2.08, 15 ms fade-out) | Writ shot reference; tail now 1988 ms |
| `writ/cock-destiny2-1.wav` | 0.48 s | 0.015, 0.054, 0.094: click, click, body (one cocking action; high-pass -22 dB, a click as loud as the shot's crack); starts from near-silence | the raw Hand Cannons audio, 4.36-4.84 s (the original clip held only its first 0.11 s, from 2.09 s) | cock/rechamber reference (pk -12.4 dBFS, -20.0 LUFS) |
| `breacher/shell-insert-witchfire-1.wav` | 0.46 s | 0.14, 0.17, 0.23: ticks; 0.275: 95 ms scrape; 0.33: broadband thump (loudest, hp -24 dB); one group = one shell insert (cen 4571 Hz, pk -10.4 dBFS) | equip-witchfire-1.wav 0.02-0.48 s | reload reference (single shell) |
| `breacher/shell-insert-witchfire-2.wav` | 0.43 s | 0.09, 0.17: ticks; ~0.27: scrape; 0.335: broadband thump (hp -23 dB); 0.40: small low thump; same pattern as piece 1 (cen 4472 Hz, pk -11.6 dBFS) | equip-witchfire-1.wav 0.50-0.93 s | reload reference (single shell) |
| `breacher/equip-witchfire-1-clean.wav` | 0.56 s | one sound: about 0.17-0.30 rattle/scrape, thump at 0.26; nothing else above the bed (cen 1548 Hz, pk -15.1 dBFS; the mixed file had cen 4209 Hz) | equip-witchfire-1.wav 0.94-1.50 s | the equip reference (picked) |

Cut checks (`events`): the two Writ pieces and the equip are single groups; the shell inserts each report their tick
and thump events (the intended group) and nothing from the neighbouring insert or the equip. The cuts used a 2-4 ms
fade-in and a 15-40 ms fade-out, 48 kHz 16-bit stereo PCM, no lossy step.

### Every original clip

| File | Dur | What it contains (events with time and label) | Use |
|---|---|---|---|
| `writ/shot-destiny2-1.wav` | 2.20 s | 0.02 shot; faint tail ripples 1.0-1.9; **2.09-2.20 cocking click (start of it, cut off by the end of the clip)** | mixed; use the two pieces above |
| `writ/shot-destiny2-2.wav` | 2.60 s | 0.02 shot; faint tail ripples at 1.46, 1.50, 2.08 (no click content); nothing else | shot |
| `writ/shot-destiny2-3.wav` | 2.50 s | 0.02 shot; faint ripples at 0.67-1.99; **2.37-2.42 a faint click (start of a following sound, 17 dB under the shot's high band)** | shot; trim the last 0.1 s if used with the tail |
| `writ/shot-destiny2-4.wav` | 1.40 s | 0.02 shot; nothing else (sharper: cen 2004 Hz) | shot |
| `writ/shot-cyberpunk-1.wav` | 1.65 s | 0.02 shot; faint bumps at 0.71, 0.80, 0.84 (about 19 dB under the shot: reflections, my guess) | shot |
| `writ/shot-cyberpunk-2.wav` | 1.50 s | 0.02 shot; one faint bump at 0.70 | shot |
| `writ/reload-partial-cyberpunk-1.wav` | 10.6 s | 12 to 13 handling-click groups, no shot (tails return to the bed within 250 ms), loudest clicks at 0.12, 0.59, 2.47, 5.49, 9.38, 9.45; quiet gaps at 1.0, 1.5, 3.25, 4.5, 8.0, 8.75 | a pool of animations, not one reload |
| `writ/reload-partial-witchfire-1.wav` | 2.20 s | clicks 0.22, 0.32, 0.40, 0.51; loud click at 0.685 (hp -18 dB); thump 1.44 with a click at 1.47; click 1.77; no shot, bed at -35 dB | light sidearm handling |
| `writ/reload-partial-witchfire-2.wav` | 1.60 s | **0.18-0.38 heavy thump (100 ms rise, peak 0.28 s at -8 dB, gone by 0.4 s: not a shot)**; clicks 0.895 and 0.955; click 1.10-1.13; nothing else | heavy handling; CLAP gave `no` for "revolver reload", so treat the thump as unidentified |
| `breacher/shot-destiny2-1.wav` | 2.50 s | 0.02 shot; a faint plateau at 0.70 inside the tail; nothing else | **picked shot** |
| `breacher/shot-destiny2-2.wav` | 2.40 s | 0.02 shot; faint clicks at 1.45, 1.62, 1.74 and 2.26, 2.38 (about 19 dB under the shot, 5 dB over its tail): unidentified mechanical sounds | shot; the tail after 1.4 s is not clean |
| `breacher/shot-cyberpunk-1.wav` | 1.95 s | 0.0 shot; a later bump at 1.2-1.3 (170 ms, 4 dB over the bed); the clip never drops below -36 dB (continuous bed) | shot, bed present |
| `breacher/shot-cyberpunk-2.wav` | 2.00 s | 0.0 shot; later bump at 1.2 (300 ms); continuous bed | shot, bed present |
| `breacher/shot-cyberpunk-3.wav` | 3.40 s | 0.02 shot (crack 0.135); bump at 0.69-0.80 (hp -28); **mechanical-sounding burst at 1.3-1.5 s (high band -27 dB, 0.2 s) after a quiet gap: probably a pump or rechamber, guessed from timing only**; faint 2.3-2.45 | shot up to 1.2 s; the tail after 1.3 s is not clean (not cut: its type could not be confirmed) |
| `breacher/shot-cyberpunk-4.wav` | 3.00 s | 0.02 shot (crack 0.085); bump at 0.70; **burst at 1.2-1.4 s, same pattern as clip 3**; faint 2.4 | same as clip 3 |
| `breacher/shot-witchfire-1.wav` | 2.40 s | 0.04 shot; tiny click 0.38; very faint clicks at 1.07, 1.12, 1.85 (high band about 35 dB under full scale, close to the bed); game ambience bed under everything | shot, bed present |
| `breacher/shot-witchfire-2.wav` | 2.50 s | 0.04 shot; sub-60 Hz energy (90%) and slow beating with scattered low bumps at 0.52-1.31 and 2.15: drone/ambience under the shot; faint click at 1.74 | weak candidate, as before |
| `breacher/reload-partial-witchfire-1.wav` | 3.00 s | no shot. A run of shell-insert groups with thumps at 0.46, 0.90, 1.33, 1.83, 2.29 (about 0.45 s apart, same pattern as the two cut shell inserts: ticks, scrape, broadband thump); an extra loud click at 0.20 (hp -22 dB) and clicks at 0.64, 2.12; a low-mid thump rising at about 2.6-2.75 (possibly the final seat or pump, a guess); bed at -30 dB | **picked reload** |
| `breacher/equip-witchfire-1.wav` | 1.50 s | **two shell inserts, 0.06-0.42 (thump 0.35) and 0.52-0.93 (thump 0.835, small low thump 0.90), then the equip 0.95-1.30 (scrape 1.12, thump 1.20)**; bed -38 dB | mixed; use the three pieces above |
| `breacher/reload-partial-cyberpunk-1.wav` | 8.10 s | no shot. 7 handling groups: 0.47-0.55 (sharp click, hp -18), 1.39-1.74, 3.19, 4.07-4.5 (heavy clunk, -18 dB overall), 5.26-5.43, 6.15 (heavy clunk, -15 dB hp), 7.0-7.43 | reload (mechanical) |
| `breacher/reload-empty-cyberpunk-1.wav` | 12.8 s | no shot. Same shape for the first 7.8 s (heavy clunks at 4.4 and 6.4, about 0.3 s later than in the partial clip), then three more groups at 8.8-9.3, 10.4-10.8, 12.2 | reload (longer); the "empty" label is still inferred |

### Label corrections from the audit

- `breacher/equip-witchfire-1.wav` was labelled "the draw before the first shot": wrong, it holds two shell inserts and
  then the equip (the creator's report, matched by the events: two identical tick-scrape-thump groups 0.47 s apart, then a
  lower-frequency thump; spectral centroid 4209 Hz mixed against 1548 Hz for the equip alone).
- `writ/shot-destiny2-1.wav` was labelled a single shot with an isolated tail: it ends in the start of a cocking click.
- `breacher/shot-cyberpunk-3.wav` and `-4.wav` ("louder blast with a long room tail") hold a later mechanical burst, so
  their 3 s tail is not all tail.
- `breacher/reload-partial-witchfire-1.wav` is a run of five shell-insert groups, not "clicks and a pump" as such.
- `breacher/shot-cyberpunk-1.wav` and `-2.wav` and the Echo shots sit on a continuous bed (never below -36 dB).
- Not corrected, only noted: the Cyberpunk reload partial/empty split is still inferred from length.

### Unusable

None is unusable. Weakest: `writ/reload-partial-witchfire-2.wav` (unidentified heavy thump), `breacher/shot-witchfire-2.wav`
(drone under it), `breacher/shot-destiny2-2.wav` and `breacher/shot-cyberpunk-3.wav`/`-4.wav` (later sounds in the tail).

## Caveats the creator should know

- **Reload type is inferred, not heard.** The reload-partial / reload-empty labels on the Cyberpunk clips follow the order
  and length of the sequences inside the chapter (the shorter first, the longer second); the Witchfire reloads are
  labelled partial because that is all the source shows. The Malorian reload clip (10.6 s, about 11 click groups)
  probably holds more than one animation; treat it as a pool, not one reload.
- **Cyberpunk and Witchfire sources have ambience.** The reload chapters have a noise floor 25-40 dB below the clicks;
  the Echo clips sit on a stronger bed (-30 LUFS overall).
- **Sources are lossy YouTube Opus**, so the air band above 16 kHz is not reliable.
- **Weapon names for Witchfire** (Hunger, Hangfire, Echo) are the video's chapter titles; I did not verify the weapon
  classes beyond the Echo being the shotgun (its video title says so).

## Rejected clips (kept in `out/skyline-refs/rejected/`)

| Clip | Reason |
|---|---|
| Witchfire Hunger shot (All Weapons video, 4.70 s) and Hangfire shot (19.80 s), for the Writ | clipped: 14 and 30 clipped samples, true peak +0.7 / +0.9 dBTP. Every shot in that video clips (11 to 90 samples), so no Witchfire sidearm shot is offered |
| Witchfire Echo shots from the same video (210.65 s, 218.09 s) | clipped: 49 and 90 samples, true peak +1.0 / +1.3 dBTP (replaced by the unclipped Echo capture above) |
| Destiny "Felwinter's Lie" meme video (8r-MZDHoFto) | speech and a noisy bed; not used |
| Witchfire "All Weapon Reload Animations" (cbihXtI5C4Y) | long loud broadband passages (shots / spells) inside each chapter; not used |

## Question for the creator

Approve or replace these; the Writ and Breacher agents compare against the approved ones (Tasks 18-19). Until you
answer, they use the first candidate of each event.
