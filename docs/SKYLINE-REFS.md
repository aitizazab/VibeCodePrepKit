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
known identity.

## Writ (precise sidearm, hand-cannon weight, low RPM)

| Event | File | Game | Weapon | Video + time range | Why it fits | Measurements | Approved? |
|---|---|---|---|---|---|---|---|
| shot | `writ/shot-destiny2-1.wav` | Destiny 2 | a hand cannon (compilation does not name it) | [Hand Cannons reference audio, Noah Sitrin](https://www.youtube.com/watch?v=JUvAJozy2kk) 2.31-4.5 s | In-game audio, no music; slow single shot, one isolated 2 s tail; the cleanest "hand cannon weight" stick | pk -12.4 / tp -12.1; -26.3 LUFS; atk 31 ms; tail 2129 ms; cen 450 Hz; 0/.56/.40/.03/.00 | |
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
| shot | `breacher/shot-destiny2-1.wav` | Destiny 2 | The Fourth Horseman (exotic shotgun) | [The Fourth Horseman, Ultraclocked](https://www.youtube.com/watch?v=ignEBRBZ0cU) 9.79-12.3 s | Isolated single blast, 2.4 s tail, mostly low body: the "heavy knockback" stick | pk -4.0 / tp -4.0; -17.3 LUFS; atk 109 ms; tail 2369 ms; cen 570 Hz; .02/.76/.15/.05/.02 | |
| shot | `breacher/shot-destiny2-2.wav` | Destiny 2 | The Fourth Horseman | same video 29.49-31.9 s | Even lower and darker (92% in the low band) | pk -4.3 / tp -4.3; -14.4 LUFS; atk 110 ms; tail 2129 ms; cen 163 Hz; .01/.92/.06/.01/.00 | |
| shot | `breacher/shot-cyberpunk-1.wav` | Cyberpunk 2077 | Carnage (Budget Arms shotgun) | [Carnage Shotgun, BGV-Gaming](https://www.youtube.com/watch?v=ft8TraQ4M3k) 0.29-2.2 s | Carnage is the "door-breach" gun by name; sub-heavy blast with a short crack | pk -8.9 / tp -8.8; -21.6 LUFS; atk 25 ms; tail 1586 ms; cen 554 Hz; .19/.59/.15/.06/.01 | |
| shot | `breacher/shot-cyberpunk-2.wav` | Cyberpunk 2077 | Carnage | same video 4.47-6.5 s | Second take | pk -8.9 / tp -8.8; -21.6 LUFS; atk 41 ms; tail 1668 ms; cen 639 Hz; .15/.64/.12/.07/.01 | |
| shot | `breacher/shot-cyberpunk-3.wav` | Cyberpunk 2077 | Mox (shotgun) | [Mox Animations & Sounds, Ultraclocked](https://www.youtube.com/watch?v=MWSL0mrGAtk) 1.55-4.95 s | A louder, brighter blast with a long (3 s) room tail | pk -2.8 / tp -2.8; -16.4 LUFS; atk 18 ms; tail 3077 ms; cen 1072 Hz; .19/.47/.18/.14/.02 | |
| shot | `breacher/shot-cyberpunk-4.wav` | Cyberpunk 2077 | Mox | same video 8.37-11.4 s | Second take | pk -2.5 / tp -2.3; -16.6 LUFS; atk 13 ms; tail 2698 ms; cen 1023 Hz; .12/.50/.22/.13/.02 | |
| shot | `breacher/shot-witchfire-1.wav` | Witchfire | Echo (shotgun) | [Let's use the Echo (Shotgun), Arjan Nuts Gaming](https://www.youtube.com/watch?v=9BiRn52CHy8) 23.12-25.5 s | The Witchfire shotgun without clipping; sits under a game ambience bed | pk -10.9 / tp -10.6; -30.0 LUFS; atk 39 ms; tail 2337 ms; cen 1107 Hz; .23/.40/.26/.06/.05 | |
| shot | `breacher/shot-witchfire-2.wav` | Witchfire | Echo | same video 33.25-35.75 s | Same weapon; 90% of the energy is below 60 Hz and the tail beats at a low rate, so a drone or ambience is probably under it: use clip 1 first | pk -11.2 / tp -11.2; -27.9 LUFS; atk 59 ms; tail 2383 ms; cen 111 Hz; .90/.04/.06/.01/.00 | |
| reload-partial | `breacher/reload-partial-witchfire-1.wav` | Witchfire | Echo | [Witchfire All Weapons, snyplays](https://www.youtube.com/watch?v=Pyi807Mi7vQ) 222.3-225.3 s (chapter Echo, 3:25-3:48) | Clean mechanical clicks and a pump, light bed | pk -9.3 / tp -9.3; -29.2 LUFS; atk 199 ms; tail 2798 ms; cen 4771 Hz; .03/.07/.25/.42/.23 | |
| equip | `breacher/equip-witchfire-1.wav` | Witchfire | Echo | same video 207.4-208.9 s | The draw before the first shot | pk -10.4 / tp -10.4; -28.9 LUFS; atk 351 ms; tail 292 ms; cen 4209 Hz; .03/.07/.32/.40/.19 | |
| reload-partial | `breacher/reload-partial-cyberpunk-1.wav` | Cyberpunk 2077 | Carnage | [All Weapons Reload Animations, snyplays](https://www.youtube.com/watch?v=TRgsBfXW1B4) 651.2-659.3 s (chapter Carnage, 10:50-11:12) | Heavy clunks and a bolt-like action: the closest thing to a mechanical cell reload in the three games | pk -4.9 / tp -4.4; -20.3 LUFS; atk 3879 ms; tail 3732 ms; cen 2139 Hz; .23/.36/.08/.25/.08 | |
| reload-empty | `breacher/reload-empty-cyberpunk-1.wav` | Cyberpunk 2077 | Carnage | same video 659.5-672.3 s | The longer second sequence of the chapter | pk -4.2 / tp -3.8; -20.6 LUFS; atk 404 ms; tail 8168 ms; cen 2677 Hz; .18/.31/.12/.29/.10 | |

Not collected for the Breacher: `dry`, `inspect`, `impact`, a Destiny 2 reload or equip; a Witchfire `reload-empty`.

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
