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
    /// It drives the player's <see cref="SkylinePlayer.ScriptedInput"/>: wait 2.0 s (in the editor capture, from the moment
    /// the Recorder takes over the clock); play the 80 ms 440 Hz marker beep and note the time; then for each gun (the Writ
    /// re-equipped, then a swap to the Breacher): three shots 1.0 s apart at the first plate, a partial reload, an inspect,
    /// fire at the fire interval + 0.1 s until empty, one more press (the dry fire, whose from-empty reload follows), and the
    /// reload's end plus 1 s. Every Event cue with a sound is recorded as {t, gun, clip, id, sound}, t in seconds after the
    /// marker on <c>Time.unscaledTime</c> (amendment 4; hit stop is off for the demo), and written to
    /// &lt;OutDir&gt;/firing-range-schedule.json; then <c>SKYLINE_RANGE done events=&lt;n&gt;</c>. Each clip start logs
    /// <c>SKYLINE_RANGE &lt;gun&gt; &lt;clip&gt;</c>. A last pad-look segment sweeps across a plate so aim assist logs
    /// <c>SKYLINE_AIMASSIST</c> (amendment 11).
    /// </summary>
    public sealed class SkylineRangeDirector : MonoBehaviour
    {
        const float LeadIn = 2f, BeepSeconds = 0.08f, BeepHz = 440f, BeepVolume = 0.5f;
        /// <summary>Quiet after the marker before the first gun sound (Task 13: at least 0.3 s after the beep ends).</summary>
        const float AfterBeep = 0.5f;
        const float ShotSpacing = 1f, AfterEquip = 0.3f, AfterClip = 0.5f, DumpSlack = 0.1f, AfterEmptyReload = 1f;
        const float RecorderWait = 8f, ReloadStartWait = 2f;
        const float PadSweepDeg = 24f, PadSweepSeconds = 2.5f;
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
                player.SetAim(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Atan2(-d.y, flat) * Mathf.Rad2Deg);
            }
            return f;
        }

        void OnCue(GunCue cue, int slot)
        {
            string gun = player.Loadout[slot].Def.id;
            if (cue.kind == GunCueKind.ClipStarted) Debug.Log("SKYLINE_RANGE " + gun + " " + cue.clip);
            if (cue.kind != GunCueKind.Event || string.IsNullOrEmpty(cue.ev.sound) || !_markerSet) return;
            _events.Add(new Entry { t = Time.unscaledTime - _marker, gun = gun, clip = cue.clip.ToString(), id = cue.ev.id, sound = cue.ev.sound });
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        Vector3 PlateCenter(int i) => plates != null && i < plates.Length && plates[i] != null ? plates[i].Center : player.EyePosition + Vector3.forward * 10f;

        IEnumerator Run()
        {
            // In the editor capture the Recorder starts about a second into Play mode and then sets a constant capture step;
            // the lead-in counts from there, so the beep is always inside the recording.
            if (SkylineRunArgs.Has("-skylineOut"))
            {
                float giveUp = Time.unscaledTime + RecorderWait;
                while (!(Time.captureDeltaTime > 0f) && Time.unscaledTime < giveUp) yield return null;
                if (!(Time.captureDeltaTime > 0f)) Debug.Log("SKYLINE_RANGE no constant capture step after " + RecorderWait + " s; starting anyway");
            }
            _aimAt = PlateCenter(0);
            yield return Wait(LeadIn);

            beepSource.clip = MakeBeep();
            beepSource.Play();
            _marker = Time.unscaledTime;
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
        }

        /// <summary>One gun's set. The gun has just been equipped (re-equip or swap) on the frame before.</summary>
        IEnumerator GunPart(int slot)
        {
            yield return null;                                  // the press or the re-equip is taken this frame
            var gun = player.Loadout[slot];
            _aimAt = PlateCenter(0);
            yield return Wait(gun.Def.Clip(HandlingClipKind.Equip).seconds + AfterEquip);

            for (int i = 0; i < 3; i++)
            {
                _next.firePressed = true;
                _next.fireHeld = true;
                yield return Wait(ShotSpacing);
            }

            _next.reloadPressed = true;
            yield return null;
            yield return Wait(gun.ReloadSeconds(false) + AfterClip);

            _next.inspectPressed = true;
            yield return null;
            yield return Wait(gun.Def.Clip(HandlingClipKind.Inspect).seconds + AfterClip);

            _aimAt = PlateCenter(1);
            float gap = gun.Def.weapon.fireInterval + DumpSlack;
            int guard = gun.Def.weapon.magazineSize + 2;
            while (gun.Ammo > 0 && guard-- > 0)
            {
                _next.firePressed = true;
                _next.fireHeld = true;
                yield return Wait(gap);
            }

            _next.firePressed = true;                           // the dry fire; its from-empty reload follows
            _next.fireHeld = true;
            float giveUp = Time.unscaledTime + ReloadStartWait;
            while (!gun.State.IsReloading && Time.unscaledTime < giveUp) yield return null;
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
            sb.Append("{\"scene\":\"SkylineFiringRange\",\"clock\":\"unscaledTime\",\"marker_hz\":").Append(BeepHz.ToString("0", ci))
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
