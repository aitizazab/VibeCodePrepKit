using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JamKit.Audio;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The firing range's scripted showcase (docs/SKYLINE-PLAN-M1.md Task 14), run only under <see cref="SkylineRunArgs.Demo"/>.
    /// It drives the player's <see cref="SkylinePlayer.ScriptedInput"/>: in the editor capture, wait for the Recorder's
    /// constant capture step; then 2.0 s of lead-in; play the 80 ms 440 Hz marker beep and note the time; then for each gun
    /// (the Writ re-equipped, then a swap to the Breacher): three shots 1.0 s apart at the first plate, a partial reload, an
    /// inspect, fire at the fire interval + 0.1 s until empty, one more press (the dry fire, whose from-empty reload
    /// follows), and the reload's end plus 1 s. Every press is confirmed by the gun on the next frame and pressed again while
    /// the gun drops it (logged as <c>SKYLINE_RANGE &lt;gun&gt; retry &lt;what&gt;</c>), so no step of the set can go
    /// missing. Every Event cue with a sound is recorded as {t, gun, clip, id, sound}, t in seconds after the marker, and
    /// written to &lt;OutDir&gt;/firing-range-schedule.json; then <c>SKYLINE_RANGE done events=&lt;n&gt;</c>. Each clip start
    /// logs <c>SKYLINE_RANGE &lt;gun&gt; &lt;clip&gt;</c>. A last pad-look segment sweeps across a plate so aim assist logs
    /// <c>SKYLINE_AIMASSIST</c> (amendment 11). After it, the Breacher's shove (judge round 1, C1): a practice drone is
    /// put up at 6 m and the Breacher blasts it three times, so the recording shows the bot sliding back, its plates flying
    /// off along the shot and the camera kicking (<c>SKYLINE_RANGE shove ...</c> lines; outside the schedule, so the sync
    /// check is unchanged). The view turns to it smoothly, never in one frame.
    /// <para>The clock is <c>Time.time</c>, the game clock, not <c>Time.unscaledTime</c> (Task 17 revises amendment 4). Hit
    /// stop is off for the demo, so <c>Time.timeScale</c> stays 1 and <c>Time.time</c> is the clock the guns tick on. Under
    /// the Recorder's constant step it advances exactly one step per frame, which is the recording's own clock: its video
    /// frames, and the audio its AudioRenderer renders per frame. <c>Time.unscaledTime</c> stays wall-clock time under a
    /// capture step (the Recorder's own frame cap compares it with the frame count), so when the editor renders below 60 fps
    /// it runs ahead of the recording. In the first pass that put the schedule up to 5 s late against the audio, and the
    /// Breacher dropped the presses that came before its 0.75 s fire interval had passed on its own clock.</para>
    /// </summary>
    public sealed class SkylineRangeDirector : MonoBehaviour
    {
        const float LeadIn = 2f, BeepSeconds = 0.08f, BeepHz = 440f, BeepVolume = 0.5f;
        /// <summary>Quiet after the marker before the first gun sound (Task 13: at least 0.3 s after the beep ends).</summary>
        const float AfterBeep = 0.5f;
        const float ShotSpacing = 1f, AfterEquip = 0.3f, AfterClip = 0.5f, DumpSlack = 0.1f, AfterEmptyReload = 1f;
        /// <summary>The longest wait for the Recorder's capture step: both this many real seconds AND this many frames from the
        /// first frame after Start, so a few long frames (the scene's first, a shader compile) can't use the budget up.</summary>
        const float RecorderWait = 20f;
        const int RecorderWaitFrames = 240;
        /// <summary>Game seconds a press is retried for (once a frame) before the set moves on.</summary>
        const float RetryFor = 1.5f;
        const float ReloadStartWait = 2f;
        const float PadSweepDeg = 24f, PadSweepSeconds = 2.5f;
        /// <summary>The shove segment: the practice drone's place (left of the plates' lane, clear behind it), the turn to
        /// it, the gap between blasts, and the wait after the last.</summary>
        static readonly Vector3 ShoveBotAt = new Vector3(-3f, 1.5f, 6.5f);
        const float ShoveTurnDegPerSec = 140f, ShoveSettle = 0.6f, ShoveGap = 0.9f, ShoveAfter = 1.6f;
        const int ShoveShots = 3;
        const int SampleRate = 48000;

        public SkylinePlayer player;
        public SkylineTargetPlate[] plates;
        [Tooltip("2D source for the marker beep.")]
        public AudioSource beepSource;

        struct Entry
        {
            public float t;
            public string gun, clip, id, sound;
        }

        readonly List<Entry> _events = new List<Entry>();
        SkylineInputFrame _next;
        Vector3? _aimAt;
        float _marker;
        bool _markerSet, _padLook;
        float _padYawRate;
        /// <summary>Degrees a second the view may turn toward <see cref="_aimAt"/>; 0 snaps (the scheduled sets keep their
        /// timing; only the shove segment turns smoothly).</summary>
        float _turnRate;
        int _shedCount;
        /// <summary>Fire and DryFire clip starts so far: each one confirms a trigger press.</summary>
        int _triggerClips;

        /// <summary>The demo's clock (see the class notes): the game clock, which the guns tick on and the recording follows.</summary>
        static float Now => Time.time;

        void Start()
        {
            if (!SkylineRunArgs.Demo || player == null)
            {
                enabled = false;
                return;
            }
            player.HitStopEnabled = false;
            player.ScriptedInput = NextFrame;
            player.CueRaised += OnCue;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            SkylineBot.PlateShed -= OnPlateShed;
            if (player != null)
            {
                player.CueRaised -= OnCue;
                if (player.ScriptedInput == (System.Func<SkylineInputFrame>)NextFrame) player.ScriptedInput = null;
            }
        }

        SkylineInputFrame NextFrame()
        {
            var f = _next;
            _next = new SkylineInputFrame();       // presses last one frame
            if (_padLook)
            {
                f.lookFromPad = true;
                f.look = new Vector2(_padYawRate * Time.deltaTime, 0f);
            }
            else if (_aimAt.HasValue)
            {
                Vector3 d = _aimAt.Value - player.EyePosition;
                float flat = new Vector2(d.x, d.z).magnitude;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(-d.y, flat) * Mathf.Rad2Deg;
                if (_turnRate > 0f)
                {
                    float step = _turnRate * Time.deltaTime;
                    yaw = Mathf.MoveTowardsAngle(player.Yaw, yaw, step);
                    pitch = Mathf.MoveTowards(player.Pitch, pitch, step);
                }
                player.SetAim(yaw, pitch);
            }
            return f;
        }

        void OnCue(GunCue cue, int slot)
        {
            string gun = player.Loadout[slot].Def.id;
            if (cue.kind == GunCueKind.ClipStarted)
            {
                Debug.Log("SKYLINE_RANGE " + gun + " " + cue.clip);
                if (cue.clip == HandlingClipKind.Fire || cue.clip == HandlingClipKind.DryFire) _triggerClips++;
            }
            if (cue.kind != GunCueKind.Event || string.IsNullOrEmpty(cue.ev.sound) || !_markerSet) return;
            _events.Add(new Entry { t = Now - _marker, gun = gun, clip = cue.clip.ToString(), id = cue.ev.id, sound = cue.ev.sound });
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Now + seconds;
            while (Now < until) yield return null;
        }

        /// <summary>Waits for the Recorder's constant capture step (the capture starts it after the editor has settled in
        /// Play mode), so the lead-in and the marker are inside the recording. Counted from the first frame after Start, in
        /// real seconds AND frames, and logged either way.</summary>
        IEnumerator WaitForRecorder()
        {
            yield return null;                                  // Start's frame can carry the scene load: count from the next
            double from = Time.realtimeSinceStartupAsDouble;
            int frames = 0;
            while (!(Time.captureDeltaTime > 0f) &&
                   (frames < RecorderWaitFrames || Time.realtimeSinceStartupAsDouble - from < RecorderWait))
            {
                frames++;
                yield return null;
            }
            var ci = CultureInfo.InvariantCulture;
            string waited = (Time.realtimeSinceStartupAsDouble - from).ToString("0.0", ci) + " s, " + frames + " frames (realtime=" +
                            Time.realtimeSinceStartup.ToString("0.0", ci) + ")";
            if (Time.captureDeltaTime > 0f)
                Debug.Log("SKYLINE_RANGE capture step " + Time.captureDeltaTime.ToString("0.#####", ci) + " after " + waited);
            else
                Debug.Log("SKYLINE_RANGE no constant capture step after " + waited + "; starting anyway");
        }

        /// <summary>Presses the trigger and confirms it on the next frame by a Fire or DryFire clip start; presses again each
        /// frame while the gun drops it (a press before the gun is ready is dropped), for up to <see cref="RetryFor"/>.</summary>
        IEnumerator PullTrigger(SkylineGun gun, string what)
        {
            int before = _triggerClips;
            float giveUp = Now + RetryFor;
            int tries = 0;
            while (true)
            {
                _next.firePressed = true;
                _next.fireHeld = true;
                tries++;
                yield return null;                              // the player's Update takes the press on this frame
                if (_triggerClips != before || Now >= giveUp) break;
            }
            bool taken = _triggerClips != before;
            if (tries > 1 || !taken)
                Debug.Log("SKYLINE_RANGE " + gun.Def.id + " retry " + what + " x" + (tries - 1) + (taken ? "" : " (never taken)"));
        }

        /// <summary>Presses reload (or inspect) until the gun is reloading (or inspecting), as <see cref="PullTrigger"/> does.</summary>
        IEnumerator PressUntil(SkylineGun gun, bool reload)
        {
            float giveUp = Now + RetryFor;
            int tries = 0;
            bool taken;
            while (true)
            {
                if (reload) _next.reloadPressed = true;
                else _next.inspectPressed = true;
                tries++;
                yield return null;
                taken = reload ? gun.State.IsReloading : gun.Inspecting;
                if (taken || Now >= giveUp) break;
            }
            if (tries > 1 || !taken)
                Debug.Log("SKYLINE_RANGE " + gun.Def.id + " retry " + (reload ? "reload" : "inspect") + " x" + (tries - 1) + (taken ? "" : " (never taken)"));
        }

        Vector3 PlateCenter(int i) => plates != null && i < plates.Length && plates[i] != null ? plates[i].Center : player.EyePosition + Vector3.forward * 10f;

        IEnumerator Run()
        {
            // In the editor capture the lead-in counts from the Recorder's start, so the beep is always inside the recording.
            if (SkylineRunArgs.Has("-skylineOut")) yield return WaitForRecorder();
            _aimAt = PlateCenter(0);
            yield return Wait(LeadIn);

            beepSource.clip = MakeBeep();
            beepSource.Play();
            _marker = Now;
            _markerSet = true;
            Debug.Log("SKYLINE_RANGE marker");
            yield return Wait(AfterBeep);

            player.ReEquip();
            yield return GunPart(0);
            _next.swapPressed = true;
            yield return null;
            yield return GunPart(1);

            WriteSchedule();
            Debug.Log("SKYLINE_RANGE done events=" + _events.Count);

            // Amendment 11: a pad-look sweep across the middle plate (aim assist slows the turn and pulls toward it).
            _aimAt = null;
            var target = PlateCenter(1);
            Vector3 d = target - player.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            player.SetAim(yaw - PadSweepDeg * 0.5f, Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            Debug.Log("SKYLINE_RANGE pad-look");
            _padYawRate = PadSweepDeg / PadSweepSeconds;
            _padLook = true;
            yield return Wait(PadSweepSeconds);
            _padLook = false;
            Debug.Log("SKYLINE_RANGE pad-look end");

            yield return Shove();
        }

        /// <summary>The Breacher's identity on a live bot: a practice drone (never attacks, regrows its plates) 6 m out; the
        /// view turns to it; three blasts; then a wait while the plates land. The Breacher is in hand and full after its set.</summary>
        IEnumerator Shove()
        {
            int slot = player.Loadout.ActiveIndex;
            var gun = player.Loadout[slot];
            if (gun.Def.id != SkylineGuns.BreacherId)
            {
                Debug.Log("SKYLINE_RANGE shove skipped (the Breacher isn't in hand)");
                yield break;
            }
            Vector3 eye = player.EyePosition;
            float yaw = Mathf.Atan2(eye.x - ShoveBotAt.x, eye.z - ShoveBotAt.z) * Mathf.Rad2Deg;
            var bot = SkylineBot.Spawn(SkylineEnemies.DroneId, ShoveBotAt, yaw, 11UL, player, player.Writ, transform);
            if (bot == null)
            {
                Debug.Log("SKYLINE_RANGE shove skipped (no practice drone)");
                yield break;
            }
            bot.practice = true;
            bot.gameObject.name = "Practice " + bot.gameObject.name;
            _shedCount = 0;
            SkylineBot.PlateShed += OnPlateShed;
            Debug.Log("SKYLINE_RANGE shove start " + bot.name);

            _turnRate = ShoveTurnDegPerSec;
            _aimAt = bot.Center;
            yield return Wait(ShoveSettle);
            for (int i = 0; i < ShoveShots && bot != null; i++)
            {
                float from = Now;
                _aimAt = bot.Center;
                yield return null;                              // one frame to settle on where it is now
                Vector3 before = bot.transform.position;
                yield return PullTrigger(gun, "shove " + (i + 1));
                yield return Wait(ShoveGap - (Now - from));
                if (bot != null)
                    Debug.Log("SKYLINE_RANGE shove shot " + (i + 1) + " slid " +
                              (bot.transform.position - before).magnitude.ToString("0.00", CultureInfo.InvariantCulture) + " m, plates shed " + _shedCount);
            }
            _aimAt = bot != null ? bot.Center : _aimAt;
            yield return Wait(ShoveAfter);
            SkylineBot.PlateShed -= OnPlateShed;
            _turnRate = 0f;
            Debug.Log("SKYLINE_RANGE shove end plates shed " + _shedCount);
        }

        void OnPlateShed(SkylineBot bot, int plate) => _shedCount++;

        /// <summary>One gun's set. The gun has just been equipped (re-equip or swap) on the frame before.</summary>
        IEnumerator GunPart(int slot)
        {
            yield return null;                                  // the press or the re-equip is taken this frame
            var gun = player.Loadout[slot];
            _aimAt = PlateCenter(0);
            yield return Wait(gun.Def.Clip(HandlingClipKind.Equip).seconds + AfterEquip);

            for (int i = 0; i < 3; i++)
            {
                float from = Now;
                yield return PullTrigger(gun, "shot " + (i + 1));
                yield return Wait(ShotSpacing - (Now - from));
            }

            yield return PressUntil(gun, true);
            yield return Wait(gun.ReloadSeconds(false) + AfterClip);

            yield return PressUntil(gun, false);
            yield return Wait(gun.Def.Clip(HandlingClipKind.Inspect).seconds + AfterClip);

            _aimAt = PlateCenter(1);
            float gap = gun.Def.weapon.fireInterval + DumpSlack;
            int guard = gun.Def.weapon.magazineSize + 2;
            while (gun.Ammo > 0 && guard-- > 0)
            {
                float from = Now;
                yield return PullTrigger(gun, "dump");
                yield return Wait(gap - (Now - from));
            }

            yield return PullTrigger(gun, "dry fire");          // the dry fire; its from-empty reload follows
            float giveUp = Now + ReloadStartWait;
            while (!gun.State.IsReloading && Now < giveUp) yield return null;
            if (!gun.State.IsReloading) Debug.Log("SKYLINE_RANGE " + gun.Def.id + " no reload from empty after the dry fire");
            while (gun.State.IsReloading) yield return null;
            yield return Wait(AfterEmptyReload);
        }

        static AudioClip MakeBeep()
        {
            float[] data = ToneSynth.Sequence(SampleRate, 1u, new ToneSynth.Note(Waveform.Sine, BeepHz, BeepHz, BeepSeconds, BeepVolume));
            var clip = AudioClip.Create("marker-440", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void WriteSchedule()
        {
            string dir = SkylineRunArgs.OutDir;
            if (string.IsNullOrEmpty(dir))
            {
                Debug.Log("SKYLINE_RANGE no -skylineOut; the schedule is not written");
                return;
            }
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\"scene\":\"SkylineFiringRange\",\"clock\":\"time\",\"marker_hz\":").Append(BeepHz.ToString("0", ci))
              .Append(",\"marker_seconds\":").Append(BeepSeconds.ToString("0.###", ci)).Append(",\"events\":[");
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (i > 0) sb.Append(',');
                sb.Append("\n{\"t\":").Append(e.t.ToString("0.0000", ci))
                  .Append(",\"gun\":\"").Append(Esc(e.gun)).Append("\",\"clip\":\"").Append(Esc(e.clip))
                  .Append("\",\"id\":\"").Append(Esc(e.id)).Append("\",\"sound\":\"").Append(Esc(e.sound)).Append("\"}");
            }
            sb.Append("\n]}\n");
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "firing-range-schedule.json"), sb.ToString(), new UTF8Encoding(false));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("SKYLINE_RANGE could not write the schedule: " + ex.Message);
            }
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
