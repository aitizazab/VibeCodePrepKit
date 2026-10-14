using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Keeps the built game's window on the screen (judge round 1, E6: the build ran in a window as big as the 1920 x 1080
    /// display, so its title bar and the taskbar pushed the bottom of the picture, with the HUD and the subtitles, off the
    /// screen). In a player (not the editor, not batch mode), a window that doesn't fit its display (within 92% of its height
    /// or 95% of its width: a title bar and a taskbar need the rest) becomes a borderless full-screen window at the display's
    /// own resolution, so the whole picture, and the HUD anchored to its edges, is always visible at any resolution and any
    /// Windows scaling. A window that fits is left alone. <c>-skyline-keep-window</c> turns it off. Logs
    /// <c>SKYLINE_SCREEN</c>.
    /// </summary>
    public static class SkylineScreenFit
    {
        const float FitWidth = 0.95f, FitHeight = 0.92f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Fit()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            if (SkylineRunArgs.Has("-skyline-keep-window")) return;
            if (Screen.fullScreenMode != FullScreenMode.Windowed) return;
            var d = Display.main;
            int dw = d != null && d.systemWidth > 0 ? d.systemWidth : Screen.currentResolution.width;
            int dh = d != null && d.systemHeight > 0 ? d.systemHeight : Screen.currentResolution.height;
            if (dw <= 0 || dh <= 0) return;
            bool fits = Screen.width <= dw * FitWidth && Screen.height <= dh * FitHeight;
            Debug.Log("SKYLINE_SCREEN window " + Screen.width + "x" + Screen.height + " display " + dw + "x" + dh +
                      (fits ? " fits" : " doesn't fit: borderless full screen at " + dw + "x" + dh));
            if (!fits) Screen.SetResolution(dw, dh, FullScreenMode.FullScreenWindow);
        }
    }
}
