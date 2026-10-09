using System.Collections;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>Presenter mode: the chapter jumps (each place with the journal it would have by then,
    /// the argument, each end card) and attract mode's driver (Nadia walks up to things, turns to
    /// them like a stick turns, and looks; the argument plays by its autopilot at real speed).</summary>
    public sealed partial class VisitingHoursGame
    {
        AttractDriver _attractDriver;

        static readonly string[] HospitalRoute = { "chart", "effects", "police_report", "photo" };
        static readonly string[] StreetRoute = { "newspaper", "shop_sign", "roses", "walt" };

        void SetupPresenter()
        {
            VhPresenter.Register(PresenterMode.Chapters, this, this);
            _attractDriver = AttractDrive;
            PresenterMode.RegisterDriver(this, VhPresenter.Scene, _attractDriver);
        }

        void TeardownPresenter() => PresenterMode.Unregister(this);

        /// <summary>A fresh journal and nothing left running (lines, cards, the argument, the autopilot).</summary>
        void ResetForJump()
        {
            StopAllCoroutines();
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            _pilot = null;
            BattleSpeed = 1f;
            _battleInvulnerable = true;
            _state = new SliceState(config.MakeBook());
            _journal.Attach(this, config, _state.journal);
            _rushingShown = 0;
            _waltWaved = false;
            GameRoot.Audio.StopMusic(0.5f);
        }

        static readonly string[] Day1Facts = { SliceContent.FactChart, SliceContent.FactEffects, SliceContent.FactPoliceReport, SliceContent.FactPhoto };

        public void PresenterDay1()
        {
            ResetForJump();
            DebugJumpTo(SliceArea.Hospital);
            _hud.ShowControls(16f);
            StartCoroutine(PlayWhenFree(SliceContent.KnotDay1Intro));
        }

        public void PresenterBooth()
        {
            ResetForJump();
            DebugJumpTo(SliceArea.Booth, Day1Facts);
            StartCoroutine(PlayWhenFree(SliceContent.KnotBooth));
        }

        public void PresenterStreet()
        {
            ResetForJump();
            DebugJumpTo(SliceArea.Street, Day1Facts);
            StartCoroutine(PlayWhenFree(SliceContent.KnotNightArrive));
        }

        /// <summary>In front of Walt with everything found and worked out, the argument starting.</summary>
        public void PresenterArgument()
        {
            ResetForJump();
            DebugJumpTo(SliceArea.Street, SliceContent.KnownBeforeWalt());
            _waltWaved = true;
            StandAt("walt");
            StartBattle();
        }

        /// <summary>The slice's end card for <paramref name="outcome"/>, on the dimmed street.</summary>
        public void PresenterEnd(BattleOutcome outcome)
        {
            ResetForJump();
            DebugJumpTo(SliceArea.Street, SliceContent.KnownBeforeWalt());
            _waltWaved = true;
            _state.waltOutcome = outcome;
            var walt = config.walt;
            if (outcome == BattleOutcome.Convinced && walt != null) _state.journal.Record(walt.convincedEntry);
            if (outcome == BattleOutcome.HeardOut && walt != null) _state.journal.Record(walt.heardOutEntry);
            GameRoot.Run.Set("Walt", OutcomeName(outcome));
            GameRoot.Audio.StopAmbience(0.5f);
            _hud.SetPrompt("");
            _hud.SetDim(1f);
            ShowEndCard(outcome);
        }

        // ---------- Attract mode ----------

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            float end = Time.unscaledTime + seconds;
            if (segment == "argument")
            {
                yield return AttractArgument(end);
                yield break;
            }
            var route = Area == SliceArea.Street ? StreetRoute : Area == SliceArea.Hospital ? HospitalRoute : null;
            if (route != null)
                foreach (var id in route)
                {
                    if (Time.unscaledTime >= end) break;
                    yield return AttractWalkTo(id, id != "walt", end, input);
                }
            // Time left: a slow look around.
            while (Time.unscaledTime < end)
            {
                input.move = Vector2.zero;
                input.look = Ready ? new Vector2(0.12f, 0f) : Vector2.zero;
                yield return null;
            }
            input.Clear();
        }

        /// <summary>Walks to a thing's approach point, turns to it, presses E when it's the prompt, and
        /// waits for its lines (the shell's attract loop reads them at the auto pace). A walk that gets
        /// stuck for a few seconds cuts to the spot instead.</summary>
        IEnumerator AttractWalkTo(string id, bool examine, float end, SyntheticInput input)
        {
            var spot = _area != null ? _area.Spot(id) : null;
            if (spot == null) yield break;
            var gameInput = GameRoot.Input;
            Vector3 goal = Approach(id, spot.position);
            float started = Time.unscaledTime, stuck = 0f;
            Vector3 last = player.transform.position;
            var stick = System.Numerics.Vector2.Zero;
            while (Time.unscaledTime < end)
            {
                if (!Ready)
                {
                    input.move = Vector2.zero;
                    input.look = Vector2.zero;
                    stick = System.Numerics.Vector2.Zero;
                    yield return null;
                    continue;
                }
                float dt = Time.unscaledDeltaTime;
                Vector3 pos = player.transform.position;
                Vector3 to = goal - pos;
                to.y = 0f;
                bool arrived = to.magnitude < 0.55f;
                Vector3 face = arrived ? spot.position - player.EyePosition : to;
                float flat = new Vector2(face.x, face.z).magnitude;
                float targetYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                float targetPitch = arrived ? -Mathf.Atan2(face.y, Mathf.Max(0.1f, flat)) * Mathf.Rad2Deg : 0f;
                float gap = Mathf.Abs(NaturalStick.DeltaAngle(player.Yaw, targetYaw));
                float yawIn = NaturalStick.Turn(player.Yaw, targetYaw, 55f);
                float pitchIn = Mathf.Clamp((player.Pitch - targetPitch) / 30f, -1f, 1f);
                stick = NaturalStick.Step(stick, new System.Numerics.Vector2(yawIn * 0.8f, pitchIn * 0.6f), 3f, dt);
                input.look = new Vector2(stick.X, stick.Y);
                Vector2 move = arrived ? Vector2.zero : new Vector2(0f, gap < 25f ? 0.85f : gap < 60f ? 0.35f : 0f);
                input.move = Vector2.Lerp(input.move, move, Mathf.Clamp01(dt * 5f));

                if (arrived && (!examine || gap < 10f))
                {
                    if (!examine) break;
                    if (_target != null && _target.def.id == id)
                    {
                        input.move = Vector2.zero;
                        input.look = Vector2.zero;
                        input.Tap(gameInput.Interact);
                        yield return null;
                        yield return null;
                        while (GameRoot.Dialogue.IsActive && Time.unscaledTime < end) yield return null;
                        break;
                    }
                }
                stuck = !arrived && (pos - last).sqrMagnitude < 0.0001f ? stuck + dt : 0f;
                last = pos;
                if (stuck > 2.5f || Time.unscaledTime - started > 14f)
                {
                    StandAt(id); // a cut, as an editor would make one
                    goal = player.transform.position;
                    stuck = 0f;
                    started = Time.unscaledTime;
                }
                yield return null;
            }
            input.move = Vector2.zero;
            input.look = Vector2.zero;
        }

        IEnumerator AttractArgument(float end)
        {
            while (_mode != Mode.Battle && Time.unscaledTime < end) yield return null;
            Pilot = new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Touch, PilotAction.Touch };
            PilotInvulnerable = true;
            BattleSpeed = 1f;
            while (Time.unscaledTime < end && _battle != null && _battle.Phase != BattlePhase.Done) yield return null;
            while (Time.unscaledTime < end) yield return null;
            Pilot = null;
        }
    }
}
