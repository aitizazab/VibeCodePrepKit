using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The opening's two 3D sets, built in code into the title scene by <see cref="VhStageBuilder"/>:
    /// Nadia's bedroom at night (a real room: the bed, the nightstand and the phone, the window with its
    /// blinds and the streetlight, a few of her things) and, 40 m away, the title table in the morning
    /// (the tablet, the mug, the notepad and pen, her ID badge). One camera moves between named poses
    /// (pillow, nightstand, away, overhead, table). The opening (<see cref="VhOpening"/>) drives it
    /// through the methods below; the title (<see cref="VhTitleTable"/>) takes over at the end.
    /// </summary>
    public sealed class VhStage : MonoBehaviour
    {
        public static readonly Vector3 TableOrigin = new Vector3(40f, 0f, 0f);

        public VisitingHoursBoot boot;
        public Camera cam;
        public Camera hiddenSceneCamera;
        public Transform bedroom, tableRoot;
        public VhNadiaRig nadia;
        public Transform phone;
        public Renderer phoneScreen;
        public Material phoneScreenMat, tabletScreenMat;
        public Light phoneGlow, moon, street, tableKey;
        public SkinnedMeshRenderer blanket;
        public VhTitleTable table;
        public VhPhoneScreen phoneUi;
        public VhTabletScreen tabletUi;
        public VhScreenPanel phonePanel, tabletPanel;
        public AudioSource ring, vibrate, voiceClean, voicePhone, news;
        public Color bedBackground = new Color(0.01f, 0.012f, 0.02f), tableBackground = new Color(0.10f, 0.10f, 0.11f);
        public VhWakingStyle style;
        public VhMeshKit meshes;
        public VhTextures textures;
        public readonly List<string> missingArt = new List<string>();
        /// <summary>What the opening editor can move (made by the builder; see <see cref="VhSceneEdits"/>).</summary>
        public VhSceneEdits edits;
        /// <summary>The opening editor flies the camera itself (the shots stay as they are).</summary>
        public bool editorCamera;
        /// <summary>The blanket needs reshaping over her (she was moved); the editor does it when a drag ends.</summary>
        public bool blanketDirty;
        /// <summary>Where the blanket's grid sits in the default bedroom (its parent's place there): see VhStageBuilder.ReshapeBlanket.</summary>
        public Vector3 blanketOrigin;
        public Material blanketMaterial;

        // The camera's poses (made by the builder).
        public readonly List<string> poseNames = new List<string>();
        public readonly List<Vector3> posePositions = new List<Vector3>();
        public readonly List<Quaternion> poseRotations = new List<Quaternion>();
        public readonly List<float> poseFov = new List<float>();

        // The phone and her hand.
        public Vector3 phoneRestPos;
        public Quaternion phoneRestRot = Quaternion.identity;
        public Vector3 shoulder;
        public float streetIntensity = 6f, moonIntensity = 0.25f, phoneGlowIntensity = 0.5f;
        public Color bedSky, bedGround, tableSky, tableGround;

        // Camera.
        string _pose = "";
        Vector3 _fromPos, _toPos;
        Quaternion _fromRot = Quaternion.identity, _toRot = Quaternion.identity;
        float _fromFov = 60f, _toFov = 60f, _camT = 1f, _camSpeed = 1f, _clock, _poseClock;
        // Lights and the phone's screen.
        float _lights = 1f, _lightsTarget = 1f, _lightsSpeed = 1f;
        float _screen, _screenTarget, _screenSpeed = 1f;
        // Arm and phone.
        string _arm = "rest";
        float _armT = 1f, _armSpeed = 1f, _handWeight, _handFrom, _thumb;
        bool _held;
        Vector3 _dropFromPos;
        Quaternion _dropFromRot = Quaternion.identity;
        // Breathing.
        bool _breathing;
        float _breathClock;
        AudioSource _breath;
        float _playback;

        public string Pose => _pose;
        public bool InBedroom => _pose != "table";
        public string Arm => _arm;
        public bool PhoneHeld => _held;
        /// <summary>The phone is up in her hand (lifted or held): its place is the held pose, not where it rests.</summary>
        public bool PhoneInHand => _arm == "lift" || _arm == "tap";
        /// <summary>The last held pose was pulled back to stay within her reach (the editor says so).</summary>
        public bool HoldClamped => _holdClamped;
        bool _holdClamped;

        /// <summary>Where she holds the phone up, as a place in the "looking at the nightstand" shot's frame (the opening
        /// editor's "phone-held", a child of that shot's handle, so it follows the shot). Null: the built default.</summary>
        public Transform heldPhone;
        /// <summary>The built held pose in that shot's frame: close in front of her eyes, a little right and down, screen to her.</summary>
        public static readonly Vector3 HeldOffset = new Vector3(-0.10f, -0.03f, 0.20f);
        public static readonly Quaternion HeldTurn = Quaternion.LookRotation(Vector3.up, Vector3.back) * Quaternion.Euler(-12f, 0f, 0f);
        public float ScreenBrightness => _screen;
        public float LightsLevel => _lights;
        public bool Breathing => _breathing;
        public bool Interactive => table != null && table.interactive;

        public int PoseIndex(string pose) => poseNames.IndexOf(pose);

        public void AddPose(string name, Vector3 pos, Vector3 lookAt, Vector3 up, float roll, float fov)
        {
            var rot = Quaternion.LookRotation((lookAt - pos).normalized, up) * Quaternion.Euler(0f, 0f, roll);
            poseNames.Add(name);
            posePositions.Add(pos);
            poseRotations.Add(rot);
            poseFov.Add(fov);
        }

        // ---------- What the opening calls ----------

        /// <summary>Moves the camera to a named pose over <paramref name="seconds"/> (0 = a cut).</summary>
        public bool MoveCamera(string pose, float seconds)
        {
            int i = PoseIndex(pose);
            if (i < 0 || cam == null) return false;
            _pose = pose;
            _poseClock = 0f;
            _fromPos = cam.transform.position;
            _fromRot = cam.transform.rotation;
            _fromFov = cam.fieldOfView;
            _toPos = posePositions[i];
            _toRot = poseRotations[i];
            _toFov = poseFov[i];
            if (seconds <= 0f) _camT = 1f;
            else
            {
                _camT = 0f;
                _camSpeed = 1f / seconds;
            }
            bool bed = pose != "table";
            if (nadia != null) nadia.SetMode(pose == "overhead" ? VhNadiaRig.Mode.Asleep : VhNadiaRig.Mode.FirstPerson);
            ApplySet(bed);
            ApplyCamera(0f);
            return true;
        }

        void ApplySet(bool bed)
        {
            if (moon != null) moon.enabled = bed;
            if (street != null) street.enabled = bed;
            if (phoneGlow != null) phoneGlow.enabled = bed;
            if (tableKey != null) tableKey.enabled = !bed;
            if (bedroom != null) bedroom.gameObject.SetActive(bed);
            if (tableRoot != null) tableRoot.gameObject.SetActive(!bed);
            if (cam != null) cam.backgroundColor = bed ? bedBackground : tableBackground;
            ApplyAmbient();
        }

        void ApplyAmbient()
        {
            // The opening editor's work light (L) adds an even light everywhere, so the set shows at dark moments.
            Color boost = new Color(0.32f, 0.32f, 0.35f) * workLight;
            if (InBedroom) VhWakingStyle.SetAmbient(bedSky * _lights + boost, bedGround * _lights + boost, Color.black, 0f, 0.1f * _lights + 0.6f * workLight);
            else VhWakingStyle.SetAmbient(tableSky + boost, tableGround + boost, Color.black, 0f, 0.15f + 0.6f * workLight);
        }

        /// <summary>The opening editor's work light: 0 off (as played), 1 on. Only while editing; never saved.</summary>
        public float workLight;

        /// <summary>The bedroom's night light (moon, streetlight, ambient) to <paramref name="level"/> over <paramref name="seconds"/>.</summary>
        public void SetLights(float level, float seconds)
        {
            _lightsTarget = Mathf.Clamp01(level);
            if (seconds <= 0f) _lights = _lightsTarget;
            else _lightsSpeed = 1f / seconds;
        }

        /// <summary>The phone's screen: incoming, missed, playing, dim (to <paramref name="value"/>) or off.</summary>
        public void SetPhone(string state, float seconds, float value)
        {
            float target;
            switch (state)
            {
                case "dim": target = Mathf.Clamp01(value); break;
                case "off": target = 0f; break;
                default:
                    target = 1f;
                    if (phoneUi != null) phoneUi.Show(state);
                    break;
            }
            _screenTarget = target;
            if (seconds <= 0f) _screen = target;
            else _screenSpeed = Mathf.Abs(target - _screen) / seconds;
            if (_screenSpeed <= 0f) _screenSpeed = 1f;
        }

        /// <summary>A shot was edited: if the camera is on it, it takes the new place now.</summary>
        public void RefreshPose()
        {
            int i = PoseIndex(_pose);
            if (i < 0) return;
            _toPos = posePositions[i];
            _toRot = poseRotations[i];
            _toFov = poseFov[i];
            _fromPos = _toPos;
            _fromRot = _toRot;
            _fromFov = _toFov;
            _camT = 1f;
            ApplyCamera(0f);
        }

        /// <summary>Where the phone rests on the nightstand (the opening editor moved it): it goes there now unless she holds it.</summary>
        public void SetPhoneRest(Vector3 pos, Quaternion rot)
        {
            phoneRestPos = pos;
            phoneRestRot = rot;
            if (phone != null && (_arm == "rest" || _arm == "reach" || _arm == "grab" || _arm == "hide"))
                phone.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Every sound the stage plays (the editor pauses and stops them).</summary>
        public IEnumerable<AudioSource> Sounds()
        {
            if (ring != null) yield return ring;
            if (vibrate != null) yield return vibrate;
            if (voiceClean != null) yield return voiceClean;
            if (voicePhone != null) yield return voicePhone;
            if (news != null) yield return news;
            if (_breath != null) yield return _breath;
        }

        public void SetSpeaker(bool on)
        {
            if (phoneUi != null) phoneUi.SetSpeaker(on);
        }

        /// <summary>Nadia's arm: reach, grab, lift, tap, drop or hide, over <paramref name="seconds"/>.</summary>
        public void PlayArm(string action, float seconds)
        {
            if (action == "drop" || action == "lift")
            {
                _dropFromPos = phone.position;
                _dropFromRot = phone.rotation;
            }
            _arm = action;
            _armT = seconds <= 0f ? 1f : 0f;
            _armSpeed = seconds > 0f ? 1f / seconds : 1f;
            _handFrom = _handWeight;
            if (action == "grab" || action == "lift") _held = true;
            if (action == "drop") _held = false;
            if (nadia != null && action != "hide") nadia.ShowArm(true);
            TickArm(0f);
        }

        public void SetBreathing(bool on)
        {
            _breathing = on;
            if (_breath == null) return;
            if (on)
            {
                _breath.mute = JamKit.Shell.GameRoot.Exists && JamKit.Shell.GameRoot.Audio.Muted;
                _breath.volume = 0.5f * (JamKit.Shell.GameRoot.Exists ? JamKit.Shell.GameRoot.Settings.sfxVolume : 1f);
                if (!_breath.isPlaying) _breath.Play();
            }
            else _breath.Stop();
        }

        /// <summary>How far into the voicemail we are (the phone's progress bar).</summary>
        public void SetPlayback(float seconds) => _playback = seconds;

        /// <summary>A table piece fades in (or <paramref name="shown"/> false: out).</summary>
        public bool ShowPiece(string id, float seconds, bool shown = true)
        {
            var p = table != null ? table.Find(id) : null;
            if (p == null) return false;
            p.FadeTo(shown ? 1f : 0f, seconds);
            return true;
        }

        public void SetInteractive(bool on)
        {
            if (table != null) table.interactive = on;
            if (tabletUi != null) tabletUi.SetPaused(on);
            if (!on && table != null) table.Highlight(null);
        }

        /// <summary>The start of the opening: black, the bedroom, the phone ringing on the nightstand, the table hidden.</summary>
        public void ResetForOpening()
        {
            SetInteractive(false);
            if (table != null)
                foreach (var p in table.pieces) p.FadeTo(0f, 0f);
            phone.position = phoneRestPos;
            phone.rotation = phoneRestRot;
            _held = false;
            _arm = "rest";
            _handWeight = 0f;
            _armT = 1f;
            if (nadia != null) nadia.ShowArm(true);
            SetLights(1f, 0f);
            SetPhone("incoming", 0f, 1f);
            SetSpeaker(false);
            SetBreathing(false);
            if (tabletUi != null) tabletUi.SetPaused(false);
            MoveCamera("pillow", 0f);
        }

        /// <summary>The title, finished: the table, every piece shown, interactive, the tablet's play button up.</summary>
        public void ShowTitleFinal()
        {
            SetBreathing(false);
            MoveCamera("table", 0f);
            if (table != null)
                foreach (var p in table.pieces) p.FadeTo(1f, 0f);
            SetInteractive(true);
        }

        // ---------- Per frame ----------

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _clock += dt;
            _poseClock += dt;
            if (_camT < 1f) _camT = Mathf.Min(1f, _camT + dt * _camSpeed);
            ApplyCamera(dt);

            if (!Mathf.Approximately(_lights, _lightsTarget)) _lights = Mathf.MoveTowards(_lights, _lightsTarget, dt * _lightsSpeed);
            if (moon != null) moon.intensity = moonIntensity * _lights;
            if (street != null) street.intensity = streetIntensity * _lights;
            ApplyAmbient();

            if (!Mathf.Approximately(_screen, _screenTarget)) _screen = Mathf.MoveTowards(_screen, _screenTarget, dt * _screenSpeed);
            if (phoneScreenMat != null) VhWakingStyle.SetEmission(phoneScreenMat, Color.white * (1.25f * _screen));
            if (phoneGlow != null) phoneGlow.intensity = phoneGlowIntensity * _screen;
            if (phoneUi != null && InBedroom) phoneUi.Tick(dt, _playback);

            TickArm(dt);
            TickBreath(dt);

            if (tabletUi != null && !InBedroom)
            {
                tabletUi.SetHover(table != null && table.CurrentGroup == "tablet");
                tabletUi.Tick(dt);
            }
        }

        void ApplyCamera(float dt)
        {
            if (cam == null || editorCamera) return;
            float e = VhUtil.Ease(_camT);
            Vector3 pos = Vector3.Lerp(_fromPos, _toPos, e);
            Quaternion rot = Quaternion.Slerp(_fromRot, _toRot, e);
            // A little life: her breathing on the pillow, a slow push on the phone, a drift over the table.
            Vector3 sway = Vector3.zero;
            switch (_pose)
            {
                case "pillow":
                case "nightstand":
                case "away":
                    sway = Vector3.up * (Mathf.Sin(_clock * 1.3f) * 0.003f);
                    break;
                case "overhead":
                    sway = rot * Vector3.forward * Mathf.Min(0.06f, _poseClock * 0.0025f);
                    break;
                case "table":
                    sway = new Vector3(Mathf.Sin(_clock * 0.21f) * 0.004f, Mathf.Sin(_clock * 0.17f) * 0.003f, 0f);
                    break;
            }
            cam.transform.SetPositionAndRotation(pos + sway, rot);
            cam.fieldOfView = Mathf.Lerp(_fromFov, _toFov, e);
        }

        /// <summary>Where the phone is held: close in front of her eyes at their height, a little towards her feet (her right
        /// shoulder is low and towards her feet: the arm stays bent, the hand reaches it), screen towards her (from the
        /// nightstand pose). Her hand under it and her wrist stay in view.</summary>
        void HoldPose(out Vector3 pos, out Quaternion rot)
        {
            if (heldPhone != null)
            {
                pos = heldPhone.position;
                rot = heldPhone.rotation;
            }
            else
            {
                int i = PoseIndex("nightstand");
                Vector3 p = i >= 0 ? posePositions[i] : cam.transform.position;
                Quaternion r = i >= 0 ? poseRotations[i] : cam.transform.rotation;
                pos = p + r * HeldOffset;
                rot = r * HeldTurn;
            }
            _holdClamped = false;
            // Within her reach: her wrist sits about 12 cm under the phone's middle, and the grip may slide it a few
            // centimetres more; keep that point inside 80% of her arm's length (bring the phone towards her shoulder).
            if (nadia != null && nadia.ArmReach(out var shoulderNow, out var arm))
            {
                Vector3 wristNear = pos - (rot * Vector3.forward) * 0.12f;
                float d = Vector3.Distance(wristNear, shoulderNow), keep = arm * 0.8f;
                if (d > keep)
                {
                    pos += (shoulderNow - wristNear).normalized * (d - keep);
                    _holdClamped = d - keep > 0.003f;
                }
            }
        }

        void TickArm(float dt)
        {
            if (phone == null) return;
            if (_armT < 1f) _armT = Mathf.Min(1f, _armT + dt * _armSpeed);
            float e = VhUtil.Ease(_armT);
            HoldPose(out var holdPos, out var holdRot);
            switch (_arm)
            {
                case "reach":
                    _handWeight = Mathf.Lerp(_handFrom, 1f, e);
                    break;
                case "grab":
                    _handWeight = 1f;
                    break;
                case "lift":
                    _handWeight = 1f;
                    phone.SetPositionAndRotation(Arc(_dropFromPos, holdPos, e, 0.05f), Quaternion.Slerp(_dropFromRot, holdRot, e));
                    break;
                case "tap":
                    _handWeight = 1f;
                    phone.SetPositionAndRotation(holdPos, holdRot);
                    _thumb = Mathf.Sin(e * Mathf.PI);
                    break;
                case "drop":
                    _handWeight = 1f;
                    // Let go a few centimetres above the nightstand: a short arc, then it lands.
                    phone.SetPositionAndRotation(Arc(_dropFromPos, phoneRestPos, e, 0.04f), Quaternion.Slerp(_dropFromRot, phoneRestRot, Mathf.Min(1f, e * 1.3f)));
                    break;
                case "hide":
                    _handWeight = Mathf.Lerp(_handFrom, 0f, e);
                    if (_armT >= 1f && nadia != null) nadia.ShowArm(false);
                    break;
                default:
                    _handWeight = 0f;
                    break;
            }
            if (_arm != "tap") _thumb = Mathf.MoveTowards(_thumb, 0f, dt * 4f);
            // How she holds it: from above while it lies on the nightstand, cradled from behind once it's up, and how
            // closed her fingers are round its edges.
            float held, curl;
            switch (_arm)
            {
                case "reach": held = 0f; curl = 0.25f * e; break;
                case "grab": held = 0f; curl = Mathf.Lerp(0.25f, 1f, e); break;
                case "lift": held = e; curl = 1f; break;
                case "tap": held = 1f; curl = 1f; break;
                case "drop": held = 1f - e; curl = Mathf.Lerp(1f, 0.2f, e); break;
                default: held = 0f; curl = 0.2f; break;
            }
            if (nadia == null) return;
            GripPose(held, out var wrist, out var palm);
            nadia.SetHand(wrist, palm, _handWeight);
            Vector3 centre = phone.position + phone.rotation * new Vector3(0f, phoneSize.y * 0.5f, 0f);
            nadia.SetGrip(centre, phone.rotation, phoneSize, curl, _thumb, held);
            nadia.SetThumb(_thumb);
        }

        /// <summary>The phone's size (width, thickness, length), from the builder: the hand is seated round it.</summary>
        public Vector3 phoneSize = new Vector3(0.071f, 0.0082f, 0.147f);

        /// <summary>
        /// Where her right wrist goes and how her hand is turned for a grip on the phone. <paramref name="held"/> 0: by its end
        /// as it lies on the nightstand (the end overhangs the edge: the fingers under it, the thumb on its face); 1: up in her
        /// hand (cradled from below: the wrist just under its bottom edge towards the right, the palm against its lower back,
        /// the fingers up and across it to wrap the left edge, the thumb free in front). The hand and wrist show below the
        /// phone instead of hiding behind it. The two share the bottom edge, so the change turns round it, not through it.
        /// </summary>
        public void GripPose(float held, out Vector3 wrist, out Quaternion palm)
        {
            Vector3 up = phone.rotation * Vector3.up, top = phone.rotation * Vector3.forward, right = phone.rotation * Vector3.right;
            float hw = phoneSize.x * 0.5f, th = phoneSize.y, hl = phoneSize.z * 0.5f;
            Vector3 centre = phone.position + up * (th * 0.5f);
            // "Behind" in world terms: the side of the phone away from her eyes (the camera), whichever way it's turned.
            Vector3 front = cam != null && Vector3.Dot(up, cam.transform.position - centre) < 0f ? -up : up;
            Vector3 behindDir = -front;
            // On the nightstand she takes it by its end, back to her palm: the phone's bottom end overhangs the edge, her
            // hand comes over the bed's edge from above it, the fingers hook under that end and the thumb on its face.
            Vector3 pinchWrist = centre - top * (hl + 0.06f) + front * 0.05f;
            Vector3 pinchFingers = (top * 0.65f + behindDir * 0.76f).normalized, pinchPalm = (top * 0.76f + front * 0.65f).normalized;
            Quaternion pinch = Quaternion.LookRotation(pinchFingers, -pinchPalm);
            Vector3 behindWrist = centre - top * (hl + 0.025f) + right * (hw * 0.45f) + behindDir * (th * 0.5f + 0.012f);
            Quaternion behind = Quaternion.LookRotation((top * 0.8f - right * 0.6f).normalized, behindDir);
            float k = VhUtil.Ease(held);
            // From the end to the back: the palm stays on the phone's back all the way (no turn over).
            wrist = Vector3.Lerp(pinchWrist, behindWrist, k);
            palm = Quaternion.Slerp(pinch, behind, k);
        }

        static Vector3 Arc(Vector3 a, Vector3 b, float t, float height) => Vector3.Lerp(a, b, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * height);

        /// <summary>Breath depth for the loop's shape (vh_breath: in over 1.7 s, out from 1.9 s to 4.3 s, 4.8 s a breath).</summary>
        public static float BreathAt(float t)
        {
            t = Mathf.Repeat(t, 4.8f);
            if (t < 1.7f) return VhUtil.Ease(t / 1.7f);
            if (t < 1.9f) return 1f;
            if (t < 4.3f) return 1f - VhUtil.Ease((t - 1.9f) / 2.4f);
            return 0f;
        }

        void TickBreath(float dt)
        {
            float amount = 0f;
            if (_breathing)
            {
                _breathClock = _breath != null && _breath.isPlaying && !_breath.mute ? _breath.time : _breathClock + dt;
                amount = BreathAt(_breathClock);
            }
            if (nadia != null) nadia.SetBreath(amount);
            if (blanket != null && blanket.sharedMesh != null && blanket.sharedMesh.blendShapeCount > 0) blanket.SetBlendShapeWeight(0, amount * 100f);
        }

        public void SetBreathSource(AudioSource source) => _breath = source;

        void OnDestroy() => DisposeOwned();

        /// <summary>Frees what the builder made for this stage (materials, meshes, pictures, screens) and wakes the scene's own camera.
        /// Called on destroy; edit-mode code (tests) calls it before DestroyImmediate.</summary>
        public void DisposeOwned()
        {
            if (hiddenSceneCamera != null) hiddenSceneCamera.enabled = true;
            hiddenSceneCamera = null;
            phonePanel?.Dispose();
            tabletPanel?.Dispose();
            style?.Dispose();
            meshes?.Dispose();
            textures?.Dispose();
            phonePanel = null;
            tabletPanel = null;
            style = null;
            meshes = null;
            textures = null;
        }
    }
}
