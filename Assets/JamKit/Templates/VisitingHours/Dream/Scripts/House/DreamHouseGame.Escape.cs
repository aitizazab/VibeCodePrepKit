using System.Collections;
using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Shell;
using JamKit.VisitingHours;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The house throws her out (docs/redesign/house.md "The escape"): the tip-over, the fever-dream run through rooms the
    /// house can't hold (<see cref="HouseEscape"/>, built upright at <see cref="escapeRoot"/>), the house folding shut behind
    /// her (<see cref="HouseFold"/>), the corridor that keeps getting longer (<see cref="StretchCorridor"/>: a stretch moves
    /// her and the fold back one bay under a dolly zoom), the cinematic beats (slow motion, a roll, a title), the rooms' light
    /// and sound, out of the front door onto Chapel Row in the rain (the umbrella, Lena at the tower's window); or, if she
    /// falls or the house closes on her, thrown out of the dream: Mags pulls her out and it costs (Chain.ForceOut).
    /// </summary>
    public sealed partial class DreamHouseGame
    {
        /// <summary>The night sky (on the camera) for the attic open to the stars.</summary>
        public GameObject nightSky;

        LabCourse _ec;
        CourseAutopilot _escapePilot;
        readonly HouseFold _fold = new HouseFold();
        Vector3 _e0, _exitPos, _exitFwd;
        float _s, _prevS, _hostS = -9999f;
        bool _chasing, _outside, _leaving, _sawLena;
        int _room = -1;
        bool[] _beatDone;
        bool[] _roomGone;
        DreamRainSpot _rainWorld;
        Color _sky0, _equator0, _fog0;
        Coroutine _mood;

        /// <summary>Distance along the route (the escape's, then the rain path on the hill): the umbrella reads it.</summary>
        public float S => _hostS;
        public DreamRainSpot RainSpot => _rainWorld;
        public bool Outside => _outside;
        public bool Chasing => _chasing;
        public float FoldGap => _fold.Gap(_s);

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
        static NVec3 N(Vector3 v) => new NVec3(v.x, v.y, v.z);

        /// <summary>The escape's course with its route where the escape stands in the world (the autopilot and the
        /// distances read it).</summary>
        void BuildEscapeCourse()
        {
            _e0 = escapeRoot.position;
            var c = _e.course;
            _ec = new LabCourse();
            _ec.cues.AddRange(c.cues);
            _ec.lane.AddRange(c.lane);
            _ec.sections.AddRange(c.sections);
            _ec.route = new PathLine(new[] { N(_e0), N(_e0 + new Vector3(0f, 0f, _e.frontDoorS + 60f)) });
            _ec.chaseStartS = c.chaseStartS;
            _ec.chaseEndS = c.chaseEndS;
            _ec.finishS = c.finishS;
            _ec.killY = c.killY + _e0.y;
            _escapePilot = new CourseAutopilot(_ec);
            var p = c.pursuer;
            _fold.targetGap = p.targetGap; _fold.k = p.k; _fold.minSpeed = p.minSpeed; _fold.maxSpeed = p.maxSpeed;
            _beatDone = new bool[_e.beats.Count];
            _roomGone = new bool[_e.rooms.Count];
            _exitPos = ToWorld(_h.exitDoor);
            _exitFwd = Quaternion.Euler(0f, WorldYaw(_h.exitYaw), 0f) * Vector3.forward;
            // The umbrella waits by the last hall's front door (in the escape); the rain is on the hill.
            var r = _e.rain.Copy();
            r.umbrella = N(_e0 + U(r.umbrella));
            r.floorY = _exitPos.y;
            _rainWorld = r;
        }

        void ResetEscape()
        {
            _chasing = _outside = _leaving = _sawLena = false;
            _room = -1;
            _hostS = -9999f;
            _s = _prevS = 0f;
            Time.timeScale = 1f;
            for (int i = 0; i < _beatDone.Length; i++) _beatDone[i] = false;
            for (int i = 0; i < _roomGone.Length; i++) _roomGone[i] = false;
            if (escapeRoomGroups != null) foreach (var g in escapeRoomGroups) if (g != null) g.gameObject.SetActive(true);
            _e.corridor.Reset();
            if (corridorDoor != null) corridorDoor.enabled = true;
            if (corridorDoorLeaf != null)
            {
                corridorDoorLeaf.localRotation = Quaternion.identity;
                var pair = corridorDoorLeaf.GetComponent<EscapeDoubleDoor>();
                if (pair != null) pair.Close();
            }
            if (fold != null) fold.gameObject.SetActive(false);
            if (nightSky != null) nightSky.SetActive(false);
            SetMood(-1, true);
            bool condemned = LenaCondemned;
            _rainWorld = DreamHouseStory.RainFor(_rainWorld, condemned);
            if (umbrella != null) umbrella.UseSpot(_rainWorld, DreamHouseStory.RainDefFor(condemned));
        }

        // =====================================================================================================
        // The tip-over and the start
        // =====================================================================================================

        IEnumerator TipOver()
        {
            _busy = true;
            player.InputEnabled = false;
            GameRoot.Audio.Play("mv_collapse", 0.9f);
            GameRoot.Juice.Shake(0.55f);
            if (cameraFx != null) cameraFx.Roll(85f, 2.4f);
            yield return new WaitForSecondsRealtime(0.7f);
            _hud.Fade(new Color(1f, 0.98f, 0.95f), 1f, 3f);
            yield return new WaitForSecondsRealtime(0.4f);
            BeginEscape(_e.startCheckpoint);
            _hud.Fade(new Color(1f, 0.98f, 0.95f), 0f, 2.5f);
            player.InputEnabled = true;
            _busy = false;
            Say(DreamHouseLines.EscapeStart, null);
        }

        /// <summary>She's in the escape at checkpoint <paramref name="index"/>, running (the tip-over, chapter jumps).</summary>
        void BeginEscape(int index)
        {
            var cp = _e.course.checkpoints[Mathf.Clamp(index, 0, _e.course.checkpoints.Count - 1)];
            ResetEscape();
            bool condemned = _story.LenaCondemned || LenaCondemned;
            _e.corridor.ApplyOutcome(_story.Outcome == ArgumentOutcome.None ? ArgumentOutcome.Unsure : _story.Outcome);
            if (condemned)
            {
                _rainWorld = DreamHouseStory.RainFor(_rainWorld, true);
                if (umbrella != null) umbrella.UseSpot(_rainWorld, DreamHouseStory.RainDefFor(true));
            }
            _hud.ShowCounter(false);
            SetPrompt(null, null);
            if (cameraFx != null) cameraFx.Sit(false);
            Teleport(_e0 + U(cp.position), 0f, Vector3.forward * player.Def.sprintSpeed);
            _s = _prevS = _hostS = cp.s;
            for (int i = 0; i < _beatDone.Length; i++) _beatDone[i] = _e.beats[i].s < cp.s - 0.5f;
            _chasing = cp.s >= _ec.chaseStartS && cp.s < _ec.chaseEndS;
            _fold.targetGap = _e.course.pursuer.targetGap;
            _fold.Reset(cp.s, player.Def.sprintSpeed);
            if (fold != null) fold.gameObject.SetActive(_chasing);
            _escapePilot.Reset(cp.s);
            if (AutopilotOn) player.Autopilot = _escapePilot.Next;
            Debug.Log("JAMKIT_HOUSE the escape from \"" + cp.name + "\" (Lena " + _story.Outcome + ")");
        }

        // =====================================================================================================
        // The run
        // =====================================================================================================

        void TickEscape(float dt)
        {
            if (_story.Phase == HousePhase.ThrownOut || _leaving) return;
            var p = player.transform.position;
            if (_outside) { TickOutside(p); return; }
            _s = _ec.route.Project(N(p));
            _hostS = _s;
            // The corridor that keeps getting longer.
            switch (_e.corridor.Check(_prevS, _s, player.Velocity.z))
            {
                case StretchCorridor.Result.Stretch: Stretch(p); return;
                case StretchCorridor.Result.BreakThrough: BreakThrough(); break;
            }
            _prevS = _s;
            Beats();
            Rooms();
            // The house folding shut behind her.
            if (_chasing && _s >= _ec.chaseEndS) { _chasing = false; if (fold != null) fold.gameObject.SetActive(false); }
            if (_chasing)
            {
                bool caught = _fold.Step(_s, player.Velocity.z, dt);
                PlaceFold(p);
                if (caught) { StartCoroutine(ThrowOut("the house closed on her in " + _ec.SectionAt(_s).ToLowerInvariant())); return; }
            }
            if (p.y < _ec.killY) { StartCoroutine(ThrowOut("she fell out of " + _ec.SectionAt(_s).ToLowerInvariant())); return; }
            // Through the front door: out onto the hill.
            if (_s >= _e.frontDoorS - 0.1f) ExitOntoTheHill();
        }

        void Stretch(Vector3 p)
        {
            var shift = U(_e.corridor.Shift);
            player.Teleport(p + shift, player.Yaw, player.Velocity, player.Pitch);
            _s = _ec.route.Project(N(p + shift));
            _prevS = _s;
            _hostS = _s;
            _fold.Shift(-_e.corridor.bay);
            _fold.targetGap = _e.corridor.FoldGapAfterStretches(_e.course.pursuer.targetGap);
            _escapePilot.Reset(_s);
            if (cameraFx != null) cameraFx.Stretch();
            GameRoot.Audio.Play("glitch", 0.6f);
            int n = _e.corridor.Stretches;
            var lines = new List<HouseLine>(DreamHouseLines.Stretched[Mathf.Min(n, DreamHouseLines.Stretched.Length) - 1]);
            if (n == 1 && _story.LenaCondemned) lines.AddRange(DreamHouseLines.CondemnedCorridor);
            if (_e.corridor.TheoHolds)
            {
                if (theoAtTheDoor != null && !theoAtTheDoor.activeSelf) { theoAtTheDoor.SetActive(true); lines = new List<HouseLine>(DreamHouseLines.TheoDoor); }
            }
            Say(lines, null);
            Debug.Log("JAMKIT_HOUSE the corridor stretched (" + n + ") at " + player.Velocity.z.ToString("0.0") + " m/s");
        }

        void BreakThrough()
        {
            if (corridorDoor != null) corridorDoor.enabled = false;
            if (corridorDoorLeaf != null) StartCoroutine(SwingOpen(corridorDoorLeaf));
            _fold.targetGap = _e.course.pursuer.targetGap;
            GameRoot.Audio.Play("chime", 0.8f);
            GameRoot.Juice.Flash(new Color(1f, 0.95f, 0.85f, 0.5f), 0.25f);
            Say(DreamHouseLines.BrokeThrough, null);
            Debug.Log("JAMKIT_HOUSE through the corridor's door after " + _e.corridor.Stretches + " stretches");
        }

        static IEnumerator SwingOpen(Transform leaf)
        {
            // The corridor's front door is a pair of leaves (EscapeDoubleDoor); an older build's single leaf still swings.
            var pair = leaf != null ? leaf.GetComponent<EscapeDoubleDoor>() : null;
            for (float t = 0f; t < 0.35f && leaf != null; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
                if (pair != null) pair.Open(k);
                else leaf.localRotation = Quaternion.Euler(0f, -110f * k, 0f);
                yield return null;
            }
            if (pair != null) pair.Open(1f);
        }

        void PlaceFold(Vector3 p)
        {
            if (fold == null) return;
            fold.position = new Vector3(_e0.x, p.y - 1.5f, _e0.z + _fold.S);
            float threat = _fold.Threat(_s);
            fold.localScale = Vector3.one * (1f + 0.15f * threat);
        }

        void Beats()
        {
            for (int i = 0; i < _beatDone.Length; i++)
            {
                if (_beatDone[i] || _s < _e.beats[i].s) continue;
                _beatDone[i] = true;
                var b = _e.beats[i];
                switch (b.kind)
                {
                    case "slowmo": GameRoot.Juice.HitStop(b.seconds, b.value); break;
                    case "title": _hud.Title(b.name, b.seconds); break;
                    case "roll": if (cameraFx != null) cameraFx.Roll(b.value, b.seconds); break;
                }
            }
        }

        void Rooms()
        {
            int r = _e.RoomAt(_s);
            if (r != _room)
            {
                _room = r;
                if (r >= 0)
                {
                    var room = _e.rooms[r];
                    SetMood(r, false);
                    if (nightSky != null) nightSky.SetActive(room.open);
                    GameRoot.Audio.Play(room.sound, 0.5f);
                    if (room.roll != 0f && cameraFx != null) cameraFx.Roll(Mathf.Sign(room.roll) * 16f, 1.3f);
                    var lines = DreamHouseLines.EscapeRoom(r);
                    if (lines.Length > 0 && !_speaking) Say(lines, null);
                }
            }
            // The rooms fold away behind her as the fold passes them.
            if (escapeRoomGroups == null) return;
            for (int i = 0; i < _roomGone.Length && i < escapeRoomGroups.Length; i++)
            {
                if (_roomGone[i] || escapeRoomGroups[i] == null || _e.rooms[i].s1 > _fold.S - 3f || !_chasing) continue;
                _roomGone[i] = true;
                escapeRoomGroups[i].gameObject.SetActive(false);
            }
        }

        void SetMood(int i, bool now)
        {
            Color sky = _sky0, eq = _equator0, fog = _fog0;
            if (i >= 0)
            {
                var r = _e.rooms[i];
                sky = new Color(r.ambient.X, r.ambient.Y, r.ambient.Z);
                eq = Color.Lerp(sky, _equator0, 0.4f);
                fog = new Color(r.fog.X, r.fog.Y, r.fog.Z);
            }
            if (_mood != null) { StopCoroutine(_mood); _mood = null; }
            if (now || !isActiveAndEnabled)
            {
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.fogColor = fog;
                return;
            }
            _mood = StartCoroutine(Mood(sky, eq, fog));
        }

        IEnumerator Mood(Color sky, Color eq, Color fog)
        {
            Color s0 = RenderSettings.ambientSkyColor, e0 = RenderSettings.ambientEquatorColor, f0 = RenderSettings.fogColor;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                RenderSettings.ambientSkyColor = Color.Lerp(s0, sky, k);
                RenderSettings.ambientEquatorColor = Color.Lerp(e0, eq, k);
                RenderSettings.fogColor = Color.Lerp(f0, fog, k);
                yield return null;
            }
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.fogColor = fog;
            _mood = null;
        }

        // =====================================================================================================
        // Out onto the hill
        // =====================================================================================================

        void ExitOntoTheHill()
        {
            _outside = true;
            _chasing = false;
            if (fold != null) fold.gameObject.SetActive(false);
            if (nightSky != null) nightSky.SetActive(false);
            SetMood(-1, true);
            var v = player.Velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            Teleport(_exitPos + _exitFwd * 0.6f, WorldYaw(_h.exitYaw), _exitFwd * speed);
            _hostS = _e.frontDoorS + 0.6f;
            if (lenaInWindow != null) lenaInWindow.SetActive(true);
            GameRoot.Audio.Play("chime", 0.6f);
            Debug.Log("JAMKIT_HOUSE out of the front door onto the hill" + (umbrella != null && umbrella.HasUmbrella ? ", under the umbrella" : ", in the rain"));
            StartCoroutine(LookBack());
        }

        /// <summary>A beat out of the door: the rain, and a look back at the house: Lena in the tower's round window.</summary>
        IEnumerator LookBack()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (lenaInWindow != null && cameraFx != null)
            {
                GameRoot.Juice.HitStop(1.6f, 0.35f);
                cameraFx.Hold(true, lenaInWindow.transform.position + Vector3.up * 1.4f);
                Say(DreamHouseLines.LenaAtTheWindow, null);
                yield return new WaitForSecondsRealtime(2.2f);
                cameraFx.Hold(false);
            }
            _sawLena = true;
        }

        void TickOutside(Vector3 p)
        {
            _hostS = _e.rain.rainStartS + Mathf.Max(0f, Vector3.Dot(p - _exitPos, _exitFwd));
            if (p.y < _exitPos.y - 12f) { StartCoroutine(ThrowOut("she fell from Chapel Row")); return; }
            if (_hostS >= _rainWorld.rainEndS && !_leaving) StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            _leaving = true;
            _story.Escaped();
            _hud.Fade(new Color(1f, 0.97f, 0.92f), 0.85f, 0.8f);
            Debug.Log("JAMKIT_HOUSE she's out" + (DreamEscapeGame.Despair ? " (Despair set)" : ""));
            yield return new WaitForSecondsRealtime(1.4f);
            if (_escaped != null) { _escaped.Invoke(); yield break; }
            // Standing alone (no chain): a moment, then a new visit.
            _hud.Card("Night 2's ending: out of the house, in the rain." + (umbrella != null && umbrella.HasUmbrella ? " Under her umbrella." : ""), null);
            yield return new WaitForSecondsRealtime(3f);
            NewVisit();
        }

        // =====================================================================================================
        // Thrown out of the dream
        // =====================================================================================================

        /// <summary>She fell, or the house closed on her: Mags pulls her out. It costs: Lena's mind is made up (condemning),
        /// and the chain's cost of a forced exit (Chain.ForceOut: Despair a step, the crash call the next day, one fewer of
        /// Mags's nights); Night 2's ending (the umbrella, Lena at the window) is lost.</summary>
        /// <summary>Debug and tests: the dream throws her out now (the same cost as a fall).</summary>
        public void ThrowOutNow(string reason) => StartCoroutine(ThrowOut(reason));

        IEnumerator ThrowOut(string reason)
        {
            if (!_story.Thrown()) yield break;
            _chasing = false;
            player.Autopilot = null;
            player.InputEnabled = false;
            Debug.Log("JAMKIT_HOUSE thrown out of the dream: " + reason);
            if (GameRoot.Exists)
            {
                var save = GameRoot.Save.Data;
                save.SetFlag(DreamHouseStory.ThrownOutFlag);
                save.SetFlag(DreamHouseStory.CondemnsFlag);
                Chain.SetLenaOutcome(save, "Condemns");
                GameRoot.Save.Save();
            }
            Time.timeScale = 0.3f;
            if (cameraFx != null) cameraFx.Roll(28f, 2.6f);
            GameRoot.Audio.Play("mv_caught", 0.8f);
            Say(DreamHouseLines.ThrownOutFalling, null);
            yield return new WaitForSecondsRealtime(1.4f);
            _hud.Fade(Color.black, 1f, 1.2f);
            yield return new WaitForSecondsRealtime(1.0f);
            Time.timeScale = 1f;
            bool done = false;
            _hud.Card(null, DreamHouseLines.ThrownOutCosts);
            Say(DreamHouseLines.ThrownOutBooth, () => done = true);
            while (!done) yield return null;
            yield return new WaitForSecondsRealtime(1.2f);
            // The chain ends the night with the cost kept (the next day opens on the crash call); alone, a new visit.
            Chain.ForceOut(reason);
            yield return new WaitForSecondsRealtime(2.5f);
            if (this == null || !isActiveAndEnabled) yield break;
            _hud.Card(null, null);
            NewVisit();
        }
    }
}
