# Jam assets, capture and presentation

Research brief for a 3-day Unity 6.3 LTS hackathon (URP 17.3, Windows build, maybe WebGL). Theme is
revealed on day 1 and the game is presented to a judging panel on day 3. Every licence below was
read on the source page on **2026-10-09** unless marked otherwise. Licences change, so re-check
anything you ship.

**Rules recap that drives this doc:** pre-made assets and AI output are allowed, but a game "found
copied or already available online" is disqualified. Assets are fine; a whole Asset Store
"complete project" or someone else's sample game passed off as yours is not. Build the game on our
own templates.

---

## 0. The traps (read first)

| Trap | What to do |
|---|---|
| **Urdu/Arabic text in TextMeshPro (uGUI) is not shaped.** Letters show unjoined, left to right. | See section 2. Use UI Toolkit with the Advanced Text Generator, or pre-render Urdu as images. |
| **Asset Store "Extension Asset" licences are per seat.** PrimeTween, DOTween, Unity's Particle Pack and Cartoon FX Free are all listed as "Extension Asset". | Each team member adds the free asset to **their own** Unity account before using it ([EULA FAQ](https://assetstore.unity.com/browse/eula-faq): "buy and assign a seat for each person in your project where this asset is installed"). |
| **Asset Store files must not be redistributed raw.** | Keep the repo **private** or git-ignore Asset Store folders. A public GitHub repo holding them is redistribution ([EULA FAQ](https://assetstore.unity.com/browse/eula-faq): only "to the extent that assets are embedded or incorporated into a game"). |
| **OFL fonts need their licence shipped with the build.** | Put `OFL.txt` (plus copyright line) next to the .exe / in the WebGL folder ([OFL condition 2](https://openfontlicense.org/open-font-license-official-text/)). |
| **OpenGameArt and Freesound mix licences.** Some are CC-BY-SA, GPL or **CC BY-NC**. | Filter to CC0. Log every non-CC0 file in a `CREDITS.md` as you import it. |
| **Free tiers of Suno, Udio, Stable Audio and ElevenLabs are non-commercial.** ElevenLabs free also *requires* a credit. Udio currently has **no downloads at all**. | Fine for a non-commercial jam demo if the rules allow AI; not fine if the game is later sold or the prize is treated as commercial. Details in section 4. |
| **Tripo and Meshy free outputs are CC BY 4.0 (public).** | Credit them on the credits screen. |
| **Kevin MacLeod / incompetech is CC BY 4.0.** The credit is mandatory and has a set format. | See the attribution formats at the end of section 1. |
| **Unity Recorder only works in the Editor**, not in builds. | Record footage in Play mode, or use OBS for builds (section 5). |

---

## 1. Free asset sources, organised per template

### Licences of the main sources (verified)

| Source | Licence | Attribution | Proof |
|---|---|---|---|
| Kenney | CC0 | Not required ("credit is appreciated"); don't use the Kenney logo | [kenney.nl/support](https://kenney.nl/support) |
| Quaternius | CC0 | "No, attribution is not necessary" | [quaternius.com/faq](https://quaternius.com/faq.html) |
| Poly Haven (HDRIs, textures, models) | CC0 | "You do not need to give credit" | [polyhaven.com/license](https://polyhaven.com/license) |
| ambientCG | CC0 1.0 | Not required | [docs.ambientcg.com/license](https://docs.ambientcg.com/license/) |
| Screaming Brain Studios (texture packs) | CC0 ("All assets have been released under the Public Domain (CC0) license") | Not required | [screamingbrainstudios.itch.io](https://screamingbrainstudios.itch.io/) |
| Pixabay (music/SFX/images) | Pixabay Content License | Not required. You can't redistribute the files **standalone**, and can't use content with visible brands commercially | [license summary](https://pixabay.com/service/license-summary/) |
| Freesound | Per sound: CC0, CC BY 4.0 or CC BY-NC 4.0 (old ones may be Sampling+) | Required for CC BY | [freesound FAQ](https://freesound.org/help/faq/) |
| Sonniss GDC Game Audio Bundles | Royalty-free licence, unlimited projects | "Attribution is always appreciated, but never required." No raw redistribution, no AI/ML training | [sonniss.com/gameaudiogdc](https://sonniss.com/gameaudiogdc) |
| incompetech (Kevin MacLeod) | CC BY 4.0 (the free option "Requires that you credit the music") | **Required** | [incompetech licences](https://incompetech.com/music/royalty-free/licenses/), [CC case study](https://wiki.creativecommons.org/Case_Studies/Kevin_MacLeod) |
| soundimage.org (Eric Matyas) | Free with attribution | **Required, in the game itself**: "Music by Eric Matyas" and "www.soundimage.org" | [attribution info](https://soundimage.org/attribution-info/) |
| FreePD | Public domain, but **the homepage now says the site is closed** | n/a | [freepd.com](https://freepd.com/) |
| Freedoom (Doom-style textures/sprites) | BSD licence | Keep the copyright notice and credit the Freedoom project | [freedoom about](https://freedoom.github.io/about.html) |

### 2D bullet-hell roguelite

| Category | Picks that go together | Licence |
|---|---|---|
| Ships, bullets, tiles | [Kenney Pixel Shmup](https://kenney.nl/assets/pixel-shmup); [Space Shooter Remastered](https://kenney.nl/assets/space-shooter-remastered) + [Extension](https://kenney.nl/assets/space-shooter-extension) (cleaner vector look; pick one style, not both) | CC0 |
| Dungeon / arena tiles | [0x72 16x16 DungeonTileset II](https://0x72.itch.io/dungeontileset-ii) (CC0, "Credit is not necessary"); [Kenney Tiny Dungeon](https://kenney.nl/assets/tiny-dungeon); [Kenney 1-Bit Pack](https://kenney.nl/assets/1-bit-pack) for a stark one-colour look | CC0 |
| All-in-one fallback | [Ninja Adventure pack](https://pixel-boy.itch.io/ninja-adventure-asset-pack): 50+ characters, 30+ monsters, 9 bosses, tiles, 100+ SFX, 37 music tracks, UI, VFX, 2 fonts | CC0 |
| UI / prompts | [Kenney UI Pack](https://kenney.nl/assets/ui-pack), [Input Prompts](https://kenney.nl/assets/input-prompts), [Crosshair Pack](https://kenney.nl/assets/crosshair-pack), [Game Icons](https://kenney.nl/assets/game-icons) | CC0 |
| SFX | [SubspaceAudio 512 Sound Effects (8-bit)](https://opengameart.org/content/512-sound-effects-8-bit-style) (CC0); [Kenney Digital Audio](https://kenney.nl/assets/digital-audio); [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) | CC0 |
| Music | [Juhani Junkala, 5 Chiptunes (Action)](https://opengameart.org/content/5-chiptunes-action) (CC0); Ninja Adventure's 37 tracks (CC0) | CC0 |

### 3D DOOM-like (billboard sprites, low-res)

| Category | Picks | Licence |
|---|---|---|
| Low-res wall/floor textures | [Screaming Brain Tiny Texture Packs 1-3](https://screamingbrainstudios.itch.io/) (375 / 480 / 720 tiny textures, plus a Horror pack); [Kenney Retro Textures Fantasy](https://kenney.nl/assets/retro-textures-fantasy); [Kenney Prototype Textures](https://kenney.nl/assets/prototype-textures) for greyboxing | CC0 |
| Doom-native sprites and textures | [Freedoom](https://freedoom.github.io/about.html). Very on-genre, but **BSD: ship its notice** | BSD |
| Level kits (3D) | [Kenney Retro Urban Kit](https://kenney.nl/assets/retro-urban-kit), [Modular Dungeon Kit](https://kenney.nl/assets/modular-dungeon-kit), [Graveyard Kit](https://kenney.nl/assets/graveyard-kit), [Retro Fantasy Kit](https://kenney.nl/assets/retro-fantasy-kit) | CC0 |
| Weapons / enemies (render to sprites or use as low-poly) | [Kenney Blaster Kit](https://kenney.nl/assets/blaster-kit); Quaternius *Ultimate Monsters*, *Bestiary - Dungeon Monsters Kit*, *Sci-Fi Modular Gun Pack*, *Animated Guns Pack* ([quaternius.com](https://quaternius.com/)) | CC0 |
| Sky / light | [Kenney Skyboxes](https://kenney.nl/assets/skyboxes); [Poly Haven HDRIs](https://polyhaven.com/hdris) (downscale for the retro look) | CC0 |
| SFX | [Kenney Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds), [Impact Sounds](https://kenney.nl/assets/impact-sounds); Sonniss GDC bundles for weapons and ambience | CC0 / Sonniss |

### Story / visual novel with point-and-click

| Category | Picks | Licence |
|---|---|---|
| Backgrounds / characters | Best done with AI (Gemini via Flow, section 4) at 16:9, since free VN art with clean licences is rare. Or render Poly Haven / Quaternius interiors (*Ultimate House Interior Pack*, *Ultimate Furniture Pack*) with a painterly post filter | AI terms / CC0 |
| UI frames, dialogue boxes | [Kenney UI Pack](https://kenney.nl/assets/ui-pack), [UI Pack RPG Expansion](https://kenney.nl/assets/ui-pack-rpg-expansion), [Fantasy UI Borders](https://kenney.nl/assets/fantasy-ui-borders), [Cursor Pack](https://kenney.nl/assets/cursor-pack), [Emotes Pack](https://kenney.nl/assets/emotes-pack) | CC0 |
| UI sounds / voice blips | [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), [UI Audio](https://kenney.nl/assets/ui-audio), [Voiceover Pack](https://kenney.nl/assets/voiceover-pack) | CC0 |
| Music | Kevin MacLeod (CC BY, credit required); Pixabay Music (no credit); [Kenney Music Jingles](https://kenney.nl/assets/music-jingles) for stingers | see table above |

### First-person narrative hybrid

| Category | Picks | Licence |
|---|---|---|
| Controller | Unity *First Person + Third Person Character Controllers* (section 3) | Asset Store EULA |
| Environments | Quaternius *Downtown City MegaKit*, *Medieval Village MegaKit*, *Modular Sci-Fi Megakit*, *Fantasy Props MegaKit*; [Kenney Furniture Kit](https://kenney.nl/assets/furniture-kit) | CC0 |
| PBR materials | [ambientCG](https://ambientcg.com/) (download 1K-2K, not 8K); [Poly Haven textures](https://polyhaven.com/textures) | CC0 |
| Lighting | [Poly Haven HDRIs](https://polyhaven.com/hdris) for sky + reflections | CC0 |
| Ambience / foley | Sonniss GDC bundles; [Freesound filtered to CC0](https://freesound.org/search/?f=license:%22Creative+Commons+0%22) | Sonniss / CC0 |

### Fonts (all OFL 1.1 from Google Fonts unless noted; ship OFL.txt)

| Use | Fonts |
|---|---|
| Pixel / retro | [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P), [VT323](https://fonts.google.com/specimen/VT323), [Silkscreen](https://fonts.google.com/specimen/Silkscreen), [Pixelify Sans](https://fonts.google.com/specimen/Pixelify+Sans); [Kenney Fonts](https://kenney.nl/assets/kenney-fonts) (CC0) |
| Story / UI | [Cinzel](https://fonts.google.com/specimen/Cinzel) (titles), [Lora](https://fonts.google.com/specimen/Lora) (body), [Inter](https://fonts.google.com/specimen/Inter) (UI) |
| **Urdu Nastaliq** | [Noto Nastaliq Urdu](https://fonts.google.com/noto/specimen/Noto+Nastaliq+Urdu); [Gulzar](https://fonts.google.com/specimen/Gulzar) (more typographic Nastaliq with a matching Latin, by Borna Izadpanah, Simon Cozens et al.; [github](https://github.com/googlefonts/Gulzar)) |
| Arabic-script fallback (Naskh, simpler shaping) | [Noto Naskh Arabic](https://fonts.google.com/noto/specimen/Noto+Naskh+Arabic) (covers Urdu letters), [Vazirmatn](https://fonts.google.com/specimen/Vazirmatn) |

### VFX for URP (Unity 6.3)

| Pack | Status (store page, 2026-10-09) | Licence |
|---|---|---|
| [Particle Pack, Starter Assets (Unity Technologies)](https://assetstore.unity.com/packages/slug/127325) | v3.3, updated 9 Oct 2026, **URP only**, compatible with 6000.3.0f1 | Extension Asset, Standard EULA (per seat) |
| [Cartoon FX Remaster Free (Jean Moreno)](https://assetstore.unity.com/packages/slug/109565) | R 1.5.1 (Dec 2025); store lists only 2021.3/2020.3 but supports Built-in + URP. If effects vanish in URP, **turn on Depth Texture** in the URP asset (soft particles need it) | Extension Asset, Standard EULA |
| [Kenney Particle Pack](https://kenney.nl/assets/particle-pack) | Sprites to drive your own Particle System or VFX Graph | CC0 |
| URP package samples | Package Manager > Universal RP > Samples > Import ([docs](https://docs.unity3d.com/6000.4/Documentation/Manual/urp/package-samples.html)) | Unity Companion |

### Attribution formats to copy

* **Generic CC BY (TASL):** `"Title" by Author (link), licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/)` ([CC best practices](https://wiki.creativecommons.org/wiki/Best_practices_for_attribution)).
* **Kevin MacLeod** (the form used across itch.io credits; incompetech's credit generator produces the exact text):
  `"Track Title" Kevin MacLeod (incompetech.com)`
  `Licensed under Creative Commons: By Attribution 4.0 License`
  `http://creativecommons.org/licenses/by/4.0/`
* **Freesound CC BY** (format from their FAQ): `"sound2", "sound3" by user2 ( http://freesound.org/people/user2/ ) licensed under CCBY 4.0`.
* **Eric Matyas:** `Music by Eric Matyas` / `www.soundimage.org`, **inside the game's credits**.
* **Meshy / Tripo free tier (CC BY 4.0):** `3D model "<name>" generated with Meshy (meshy.ai), CC BY 4.0`.
* **ElevenLabs free tier:** include `elevenlabs.io` (or `11.ai`) with the credit for that audio.
* **Freedoom:** include the Freedoom BSD copyright notice + "Contains assets from the Freedoom project".
* CC0 packs: no credit needed, but a "Kenney / Quaternius / Poly Haven" line costs nothing and judges notice honesty.

---

## 2. Can Unity 6.3 render Urdu Nastaliq / RTL properly? (honest answer)

| Text system | Arabic-script joining + RTL | Urdu | Nastaliq |
|---|---|---|---|
| **uGUI + TextMeshPro** (what most templates use) | **No** native shaping or bidi | No | No |
| TMP + [RTLTMPro](https://github.com/mnarimani/RTLTMPro) (free, open source) | Yes: Arabic, Farsi, Hebrew, tashkeel. Input fields and dropdowns have "known issues" | **Not listed** | No (it maps to joined forms; Nastaliq's cascading layout needs full OpenType shaping. This is our inference) |
| TMP + [UnityDirectTMP](https://github.com/AmirCollider/UnityDirectTMP) (MIT, small project) | Claims joined RTL Persian/Arabic/**Urdu** from the font's OpenType tables, Unity 2021.3+ | Claimed | **Explicitly not**: "you get the standard joined shape, not Nastaliq typesetting" |
| **UI Toolkit + Advanced Text Generator (ATG)** | Yes. Uses **HarfBuzz + ICU + FreeType**; Unity names Arabic and Hebrew as RTL examples ([ATG docs](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-advanced-text-generator.html)) | "We haven't fully tested each language" (Unity staff answering an Urdu question, [forum, Oct 2024](https://discussions.unity.com/t/announcing-full-rtl-language-support/1544214)) | **Not mentioned anywhere by Unity.** HarfBuzz can shape Nastaliq (it is what browsers use), so it *may* work. Untested. |

**ATG facts for 6.3** ([enable-and-use page](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/enable-and-use-atg.html)):
* "not enabled by default": tick **Edit > Project Settings > UI Toolkit > Enable Advanced Text Generator**.
* Then set USS `-unity-text-generator: advanced` on the root element (or UI Builder > Inlined Styles > Text > Text Generator Type = Advanced).
* Static font assets are not supported; use a dynamic font asset. "Customization of glyph metrics is not available."
* UI Toolkit only. Unity staff: "Yes. Only UI Toolkit." No uGUI/TMP support was announced.
* ICU data no longer has to sit in Assets from 6000.0.29f1 on. WebGL isn't mentioned anywhere, so test it.

**Nastaliq-specific risks:** the script needs a lot of vertical room (a ~1.8 line-height is recommended, or dots clip: [Urdu design guide](https://open-design.ai/plugins/design-system-urdu/)). Engines often misplace its baseline ([Android write-up](https://proandroiddev.com/taming-noto-nastaliq-fixing-urdu-font-rendering-issues-in-android-c65e83b40b34)). Even the HarfBuzz author's [Noto Nastaliq test page](https://behdad.org/urdu/) calls it hard to get right.

**Recommendation for a 3-day jam:**
1. **Before the jam (30 min spike):** in a blank 6.3 URP project, turn on ATG and make a UI Toolkit label with Gulzar and Noto Nastaliq Urdu. Show a known sentence and compare it with the same text in Chrome or Word. Check both a Windows build and a WebGL build. Only this test settles it.
2. If it passes, put all Urdu in UI Toolkit (dialogue boxes, menus) and keep world-space text English or image-based.
3. If it fails, or for **titles/key art: pre-render Urdu as PNGs** from any HarfBuzz app (browser, Inkscape, Figma). This always works, looks best, and suits a fixed script. Use Noto Naskh Arabic through ATG for any dynamic Urdu.
4. Don't attempt Urdu in TMP during the jam.

---

## 3. Asset Store / Package Manager things to pre-install (URP 17.3)

| Package | Version / 6.3 status | Licence | Why |
|---|---|---|---|
| [First Person + Third Person Character Controllers (Unity "Starter Assets")](https://assetstore.unity.com/packages/slug/196526) | v2.0.1, 17 Sep 2026; lists **6000.3.0f1, URP** | Standard EULA, free | FPS template + narrative hybrid. Pulls in Input System + Cinemachine. (The old separate FirstPerson URP listing says "no longer available".) |
| Cinemachine (Package Manager) | **3.1.7** for 6000.3 ([docs](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.cinemachine.html)) | Unity Companion | Camera feel, Impulse screenshake (free "juice") |
| Recorder (Package Manager) | **5.1.7** for 6000.3 ([docs](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.recorder.html)) | Unity Companion | Trailer footage (section 5) |
| [PrimeTween](https://assetstore.unity.com/packages/slug/252960) | 1.4.12, Jul 2026; lists 6000.0 + 2022.3, all pipelines; no dependencies | Extension Asset (per seat) | Zero-allocation tweens; our pick |
| [DOTween (HOTween v2)](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) | 1.3.030, Jun 2026; store lists 2022.3 (widely used on 6) | Extension Asset (per seat) | Alternative if someone already knows it. **Pick one tween library, not both** |
| [Particle Pack (Unity)](https://assetstore.unity.com/packages/slug/127325) | v3.3, URP, 6000.3 | Extension Asset | Explosions, fire, muzzle flashes |
| [Cartoon FX Remaster Free](https://assetstore.unity.com/packages/slug/109565) | R 1.5.1, Built-in/URP | Extension Asset | Stylised hits/pickups |
| URP samples | Import from the Universal RP package page | Unity Companion | Renderer features, post-processing examples |

The free tier of Feel (MoreMountains) isn't verified, so we didn't list it. Cinemachine Impulse,
URP Volume overrides (bloom, chromatic aberration, vignette) and PrimeTween punch/shake cover most
"juice" for free.

**EULA basics** ([FAQ](https://assetstore.unity.com/browse/eula-faq)): "commercial use and
noncommercial use are both fine" when the asset is built into your product. Extension Assets need a
seat per person; others are licensed per legal entity. A team of individuals isn't one company, so
each person should "get" every free asset on their own account.

---

## 4. AI asset tools: free-tier terms

| Tool | Free tier: who owns it / can you use it | Attribution | Source |
|---|---|---|---|
| **Suno** | Basic (free): "we retain ownership of the songs you generate"; use is **non-commercial only**. Paid (Pro/Premier): "you are considered the owner", with commercial rights kept after cancelling. Subscribing later does **not** license old free songs retroactively | None stated | [Suno help](https://help.suno.com/en/articles/2416769), [Music in Africa on the WMG-era terms](https://www.musicinafrica.net/magazine/suno-adjusts-ai-music-ownership-terms-after-warner-music-partnership) |
| **Udio** | **Downloads disabled since 29-30 Oct 2025** (UMG settlement); a licensed relaunch is still in staged rollout. Free use is personal | n/a | [Digital Music News](https://www.digitalmusicnews.com/2025/10/31/udio-umg-deal-downloads-resumed-48-hours/), [RouteNote](https://routenote.com/blog/udio-stops-user-downloads-after-umg-deal-heres-why/) |
| **Stable Audio** (stableaudio.com) | Free = non-commercial; a paid plan is required for commercial use | Not stated | [FAQ](https://stableaudio.com/faqs). The open-weights model under Stability's Community Licence is a separate route ([explainer](https://stability.ai/explainers/stable-audio-vs-competitors-licensing-export-rights-and-self-hosting-compared)) |
| **ElevenLabs** | Free: "does not include a commercial license". Paid plans include one (except Beta features) | **Required on free:** "you must attribute it to ElevenLabs by including 'elevenlabs.io' or '11.ai' in the title" | [ElevenLabs help](https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform) |
| **Meshy** | Free: 100 credits/month, assets under **CC BY 4.0**; paid: you "own all assets" | **CC BY: credit Meshy** | [meshy.ai/pricing](https://www.meshy.ai/pricing) |
| **Tripo** | Free (Basic, 300 credits/mo as of May 2026): **public models, CC BY 4.0**; paid = private + commercial. Sources disagree on whether free output may be used commercially with attribution | Credit Tripo | [costbench snapshot](https://costbench.com/software/ai-3d-generation/tripo-ai/), [Tripo blog](https://www.tripo3d.ai/blog/commercial-use-ai-3d-models). The pricing page blocked our fetch, so check it |
| **Gemini / Nano Banana in Google Flow** | "Google won't claim ownership over that content"; Google "may generate the same or similar content for others". All Flow output carries invisible **SynthID**; the visible watermark is a toggle (forced on in India, South Korea and Vietnam) | Not required | [Flow help](https://support.google.com/flow/answer/16353333?hl=en), [Gen AI terms (archived)](https://ai.google.dev/gemini-api/terms-archive/terms_12_20_24) |

**Is a hackathon "commercial"?** None of these terms define it. A non-revenue jam demo is the
normal reading of non-commercial. A cash prize or publishing the game afterwards is grey. The safe
order is CC0 asset first, then a paid-tier generation, then a free-tier generation with credit.
Put every AI tool on the credits screen. Judges reward honesty, and rules that allow AI rarely
forbid crediting it.

---

## 5. Capturing gameplay footage

### Unity Recorder 5.1.7 (Editor only)
"The Recorder is designed to work only in the Unity Editor in Play mode... It does not work in
standalone Unity Players or builds" ([manual](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/index.html)).

**1080p60 from the Game view:**
1. Package Manager > Unity Registry > Recorder > Install. Open **Window > General > Recorder > Recorder Window**.
2. Set the Game view resolution dropdown to **1920x1080** (fixed, not Free Aspect).
3. Add Recorder > **Movie**. Source: Game View (or Targeted Camera to avoid capturing editor overlays). Output resolution: 1920x1080.
4. Frame Rate: **Playback = Constant, Target 60, Cap FPS on** for trailer shots. The editor may
   slow down, but the file is a smooth 60 ([recording properties](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecorderWindowRecordingProperties.html)).
   Use Variable (Max FPS 60) when a human is playing live and timing must feel real.
5. Encoder: **Unity Media Encoder, H.264 MP4, Quality High**, or ProRes 422 HQ for heavy grading
   ([Movie recorder](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecorderMovie.html)).
   Include Audio needs Project Settings > Audio > Default Speaker Mode = Stereo.
6. **Image sequence (PNG)** is the safest option for short hero shots: no compression artefacts,
   and frames survive a crash. Assemble with ffmpeg (below).

### OBS for builds (laptop, RTX 3050)
| Setting | Value |
|---|---|
| Windows graphics setting for `obs64.exe` | **High performance** (NVIDIA) for Game Capture. On a hybrid-GPU laptop, OBS must run on the GPU drawing the game or you get a black screen ([OBS forum](https://obsproject.com/forum/threads/laptop-black-screen-when-capturing-read-here-first.5965/latest)) |
| Source | Game Capture (fallback: Window Capture, "Windows 10 (1903 and up)"); run the game **borderless windowed** |
| Video | Base = Output = 1920x1080, **60 fps** |
| Encoder | NVENC **H.264** or **HEVC** (RTX 30-series has no AV1 encode: [NVIDIA matrix](https://developer.nvidia.com/video-encode-and-decode-gpu-support-matrix-new)), rate control **CQP 18-20**, preset P5, keyframe interval 2 ([guide](https://obs-versions.com/blog/obs-recording-settings-guide)) |
| Container | **Hybrid MP4** (crash-safe, finalises as normal MP4) or MKV, then File > Remux ([Radboud OBS wiki](https://tsgdoc.socsci.ru.nl/index.php/OBS_Studio)) |
| Laptop | Plug in the charger, set Windows to Best performance, close Chrome |

### ffmpeg trailer assembly ([H.264 guide](https://trac.ffmpeg.org/wiki/Encode/H.264))
```bash
# PNG sequence -> 1080p60 MP4 (judge-safe yuv420p)
ffmpeg -framerate 60 -i shot_%04d.png -c:v libx264 -crf 18 -preset slow -pix_fmt yuv420p shot.mp4
# Fast GPU encode
ffmpeg -i in.mkv -c:v h264_nvenc -cq 19 -preset p5 -c:a aac -b:a 192k out.mp4
# Trim without re-encoding (cuts land on keyframes)
ffmpeg -ss 00:00:12 -to 00:00:20 -i take.mp4 -c copy clip.mp4
# Join clips (same codec/size): list.txt lines -> file 'clip1.mp4'
ffmpeg -f concat -safe 0 -i list.txt -c copy trailer_silent.mp4
# Lay music under it, fade the end, normalise loudness
ffmpeg -i trailer_silent.mp4 -i music.wav -filter_complex "[1:a]afade=t=out:st=55:d=3,loudnorm=I=-16:TP=-1.5[a]" -map 0:v -map "[a]" -c:v copy -c:a aac -shortest trailer.mp4
```
Keep the backup video at **60-90 s, gameplay in the first second** ([Kenney](https://kenney.nl/learn/improving-the-presentation-of-jam-games):
"start with gameplay immediately, with no intro, logos").

---

## 6. Presenting to the judging panel

### Evidence vs opinion

| Claim | Strength | Source |
|---|---|---|
| Presenting **later** in a sequence tends to score higher (Eurovision 1957-2003, figure skating 1994-2000), whether judges score each act right away or at the end | **Evidence** (observational, large data) | [Bruine de Bruin 2005, "Save the last dance for me"](https://research.tue.nl/en/publications/save-the-last-dance-for-me-unwanted-serial-position-effects-in-ju/) |
| The same order effect shows in a piano competition (2004-2022 data plus a 2023 randomised trial); warning judges about it didn't clearly fix it | **Evidence** | [RIETI discussion paper](https://www.rieti.go.jp/en/publications/summary/26020014.html) |
| If you can choose a slot, ask for a late one; otherwise make the first 30 s memorable | Inference from the above | — |
| Show something working within ~90 s; one flow beats a tour of features | Judges' opinion (hackathon) | [JetBrains, "Notes from the judging table", Jun 2026](https://blog.jetbrains.com/ai/2026/06/how-to-win-a-hackathon-notes-from-the-judging-table/) |
| Be upfront about what doesn't work; it reads as confidence | Opinion | same |
| A university jam's rubric: 5-min talk covering idea vs theme, implementation, a video showcase and dev experience; every member must speak | Real rubric (one event) | [CSU 24-hour Game Jam](https://engineering.csuohio.edu/coaching/computer-peer-teacher-cpt-24-hour-game-jam) |
| A judge can state the game in one sentence | Opinion (Rami Ismail, GDC Europe 2014, second-hand) | [Unity pitch guide](https://unity.com/blog/how-to-prepare-and-pitch-your-indie-game), [Adam Clare](https://www.realityisagame.com/archives/tag/pitch/) |
| Clashing assets lower presentation scores; custom art isn't required | A rubric statement | [itch.io jam criteria thread](https://itch.io/jam/rpg-maker-2025-game-jam/topic/4685664/judging-criteria) |
| A truly unique idea beats a polished familiar one | A rubric statement | same |

### A 4-minute pitch (our synthesis)

| Time | Content | Criterion it feeds |
|---|---|---|
| 0:00-0:20 | Title + **one sentence** pitch. Say how it reads the theme in plain words | Theme, Originality |
| 0:20-2:20 | **Live demo** of the best 2 minutes, starting mid-action (save/chapter select). One person plays, one narrates *what the player is feeling*, not the controls | Fun, Art & Design |
| 2:20-3:10 | "Under the hood": 1-2 technical things they can't see (e.g. procedural waves, a shader, the dialogue system) shown with a quick debug overlay or a side-by-side | Technical |
| 3:10-3:40 | The twist/ending or the strongest moment, then the credits slide (assets + AI tools named) | Originality, honesty |
| 3:40-4:00 | What you'd build next, thanks. Every member speaks once | Presentation |

### Live-demo risk kit (prepare on day 2 evening)
* **Debug keys** in the build: F1 invincible, F2 skip room/wave, F3 jump to chapter N, F4 spawn the boss, F5 restart. Hide them behind a toggle, not a menu.
* **Chapter select** on the title screen (VN/narrative templates) and a seeded run for the roguelite so the good room appears.
* **Recorded backup video** (60-90 s) on the laptop *and* a USB stick; if the demo crashes, switch to it without apologising at length.
* Test the presentation machine's HDMI, resolution (1080p) and audio output **before** your slot; have a 3.5 mm/USB-C fallback; set game volume so music doesn't drown the narration.
* Ship a **Windows build** (no Editor on stage); WebGL only as a second option.
* Rehearse aloud with a timer at least twice ([JetBrains](https://blog.jetbrains.com/ai/2026/06/how-to-win-a-hackathon-notes-from-the-judging-table/)).

### Common mistakes (all opinion, from the sources above)
Explaining controls instead of showing play; a long title or logo intro; demoing half-finished
features; talking about features that don't exist; no theme sentence; mixed art styles; going over
time; one person doing all the talking.

---

## 7. Past jam themes for ideation drills

| Jam | Theme |
|---|---|
| GMTK 2017 | Dual Purpose Design |
| GMTK 2018 | Genre without Mechanic |
| GMTK 2019 | Only One |
| GMTK 2020 | Out of Control |
| GMTK 2021 | Joined Together |
| GMTK 2022 | Roll of the Dice |
| GMTK 2023 | Roles Reversed |
| GMTK 2024 | Built to Scale |
| GMTK 2025 | Loop |
| GMTK 2026 | Count Down |
| Ludum Dare 41 | Combine two incompatible genres |
| Ludum Dare 44 | Your life is currency |
| Ludum Dare 45 | Start with nothing |
| Ludum Dare 46 | Keep it alive |
| Ludum Dare 50 | Delay the inevitable |
| Ludum Dare 51 | Every 10 seconds |
| Ludum Dare 54 | Limited space |
| Ludum Dare 58 | Collector |
| Ludum Dare 59 (Apr 2026) | Signal |
| GGJ 2017 | Waves |
| GGJ 2020 | Repair |
| GGJ 2022 | Duality |
| GGJ 2023 | Roots |
| GGJ 2024 | Make Me Laugh |
| GGJ 2025 | Bubble |
| GGJ 2026 | Mask |
| Brackeys 2023.1 | An end is a new beginning |

Sources: [GMTK jam archive](https://gamemakerstoolkit.com/jam),
[Ludum Dare (Wikipedia)](https://en.wikipedia.org/wiki/Ludum_Dare),
[Global Game Jam (Wikipedia)](https://en.wikipedia.org/wiki/Global_Game_Jam),
[TU Crete on Brackeys 2023.1](https://ece.tuc.gr/en/news/item/1st-place-worldwide-in-the-innovation-category-at-brackeys-game-jam-20231-for-ece-students).
We found no reliable list of other Brackeys themes.

**Drill:** draw a theme, take 10 minutes, and pitch one idea per template (bullet-hell / DOOM-like /
VN / FP narrative) in one sentence each. Pick the one where the theme changes the *core verb*, not
just the skin.
