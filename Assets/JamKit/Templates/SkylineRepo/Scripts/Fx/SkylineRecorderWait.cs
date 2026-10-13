using System.Collections;
using System.Globalization;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The wait for the Recorder's constant capture step in the editor capture (the range director's rule, Task 17 commit
    /// 9b306ba): it gives up only after BOTH <see cref="Seconds"/> real seconds AND <see cref="Frames"/> frames, counted from
    /// the frame after the caller's Start (Start's frame can carry the scene load), and logs how long it took as
    /// <c>&lt;tag&gt; capture step ...</c> or <c>&lt;tag&gt; no constant capture step ...</c>.
    /// <para>Everything scripted after it runs on <c>Time.time</c>: under the capture step that clock advances one step per
    /// frame, as the recording's video, its audio and the guns do, while <c>Time.unscaledTime</c> stays on the wall clock and
    /// runs ahead whenever the editor renders slower than the capture rate.</para>
    /// </summary>
    public static class SkylineRecorderWait
    {
        public const float Seconds = 20f;
        public const int Frames = 240;

        public static IEnumerator Wait(string tag)
        {
            yield return null;
            double from = Time.realtimeSinceStartupAsDouble;
            int frames = 0;
            while (!(Time.captureDeltaTime > 0f) && (frames < Frames || Time.realtimeSinceStartupAsDouble - from < Seconds))
            {
                frames++;
                yield return null;
            }
            var ci = CultureInfo.InvariantCulture;
            string waited = (Time.realtimeSinceStartupAsDouble - from).ToString("0.0", ci) + " s, " + frames + " frames";
            if (Time.captureDeltaTime > 0f) Debug.Log(tag + " capture step " + Time.captureDeltaTime.ToString("0.#####", ci) + " after " + waited);
            else Debug.Log(tag + " no constant capture step after " + waited + "; starting anyway");
        }
    }
}
