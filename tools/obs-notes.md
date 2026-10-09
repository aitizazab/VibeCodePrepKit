# Recording a build with OBS

Unity Recorder only works in the editor, so use OBS to record the Windows build. These settings are
from `docs/JAM-ASSETS-AND-PRESENTATION.md`, section 5, written for this laptop (RTX 3050, hybrid GPU).

| Setting | Value |
|---|---|
| Windows graphics setting for `obs64.exe` | **High performance** (NVIDIA), needed for Game Capture. On a hybrid-GPU laptop OBS must run on the GPU that draws the game, or the capture is black. |
| Source | **Game Capture**. If that fails, use Window Capture ("Windows 10 (1903 and up)"). Run the game **borderless windowed**. |
| Video | Base = Output = **1920x1080**, **60 fps** |
| Encoder | NVENC **H.264** or **HEVC** (the RTX 30 series has no AV1 encoder), rate control **CQP 18-20**, preset **P5**, keyframe interval **2** |
| Container | **Hybrid MP4**, which survives a crash and finishes as a normal MP4. Or MKV, then File > Remux. |
| Laptop | Plug in the charger, set Windows to Best performance, close Chrome. |

## B-roll from a build

1. Start the build with `JamKit.exe -jamAttract`. After the title it plays the template's best
   moments by itself, in a loop, at normal speed and invincible.
2. Start recording in OBS. Stop after one or two loops (each is about 70-85 s).
3. Press Escape in the game to stop attract mode.

For screenshots, open presenter mode with Ctrl+Shift+P and press **Photo**. It saves a 2x PNG to
Pictures/JamKit.

## Cutting

See `docs/PRESENTING.md`, section 5, for the ffmpeg commands: trim, join, and PNG frames to MP4.
Keep the backup video at 60-90 s, with gameplay in the first second.
