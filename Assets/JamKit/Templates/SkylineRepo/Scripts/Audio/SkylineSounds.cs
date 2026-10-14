using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Skyline's sounds by name ("writ/mag_out"): clips from Resources/SkylineAudio/&lt;sound&gt;, played through the
    /// mix (<see cref="SkylineMix"/>): two pools of voices (2D for the viewmodel, 3D in the world), one sound per voice, each
    /// at its call volume times its bus gain and trim, ducked under speech, with a voice budget that steals the least
    /// audible voice, at most <see cref="SkylineMix.MaxSameSound"/> of a sound at once, one hit sound a shot, and the
    /// master limiter on the listener (<see cref="SkylineMixer"/> runs it all and attaches the limiter). A missing clip
    /// logs <c>SKYLINE_SOUND missing &lt;sound&gt;</c> once and plays nothing.</summary>
    public static class SkylineSounds
    {
        const string Folder = "SkylineAudio/";
        const float WorldMinDistance = 2f, WorldMaxDistance = 60f;

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<AudioClip, string> _names = new Dictionary<AudioClip, string>();
        static readonly HashSet<string> _missing = new HashSet<string>();
        static GameObject _host;

        sealed class Voice
        {
            public AudioSource src;
            public string sound;
            public SkylineSoundCategory cat;
            public float gain, fade = 1f, started;
            public bool busy, fading;
            public int gateId = -1;
            public SkylineVoiceEq eq;
        }

        sealed class Pool
        {
            public Voice[] voices;
            public int budget;
            public bool world;
        }

        struct Pending
        {
            public string sound;
            public AudioClip clip;
            public bool world;
            public Vector3 position;
            public float volume;
            public int frame;
        }

        sealed class Loop
        {
            public AudioSource src;
            public string sound;
            public float original, lastSet = -1f, gain;
        }

        struct BarkVoice
        {
            public AudioSource src;
            public int gateId;
            public float until;
        }

        struct Bark
        {
            public float until, weight;
        }

        static Pool _view, _world;
        static readonly List<Pending> _pending = new List<Pending>();
        static readonly HashSet<string> _frameSounds = new HashSet<string>();
        static readonly Dictionary<string, float> _lastStart = new Dictionary<string, float>();
        static readonly List<Loop> _loops = new List<Loop>();
        static readonly List<Bark> _barks = new List<Bark>();
        static SkylineSpeechTap _tap;
        static float _clock, _duck, _speakingUntil = -1f, _duckedSeconds;
        static float _shotUntil = -1f, _shotDuck;
        static Vector3 _listener;
        static readonly SkylineVoiceGate _gate = new SkylineVoiceGate();
        static readonly List<BarkVoice> _barkVoices = new List<BarkVoice>();
        static readonly Dictionary<string, int> _soundCounts = new Dictionary<string, int>();
        static float[] _loopAudible = new float[16], _loopGains = new float[16];

        /// <summary>Counts since the scene started, for the pass's log (<see cref="Report"/>).</summary>
        public static int Started, Stolen, Capped, OneHitDropped, GateDropped, BarksPlayed, BarksDropped;

        /// <summary>The current duck depth (0 = none, 1 = a teaching line).</summary>
        public static float DuckDepth => _duck;

        /// <summary>The current sidechain depth (0 = none, 1 = the player's shot or hit is sounding).</summary>
        public static float ShotDuckDepth => _shotDuck;

        /// <summary>The clip for <paramref name="sound"/>, or null (logged once).</summary>
        public static AudioClip Get(string sound)
        {
            if (string.IsNullOrEmpty(sound)) return null;
            if (_clips.TryGetValue(sound, out var c) && c != null) return c;
            if (_missing.Contains(sound)) return null;   // a known miss: no Resources.Load per request
            c = Resources.Load<AudioClip>(Folder + sound);
            if (c == null)
            {
                if (_missing.Add(sound)) Debug.Log("SKYLINE_SOUND missing " + sound);
                return null;
            }
            _clips[sound] = c;
            _names[c] = sound;
            return c;
        }

        /// <summary>The sound name a clip from <see cref="Get"/> was loaded for.</summary>
        public static bool TryNameOf(AudioClip clip, out string sound)
        {
            sound = null;
            return clip != null && _names.TryGetValue(clip, out sound);
        }

        /// <summary>Plays a first-person sound (the guns' handling events): 2D, no distance.</summary>
        public static void PlayViewmodel(string sound, float volume = 1f) => Play(sound, false, Vector3.zero, volume);

        /// <summary>Plays a sound at a point in the world (3D).</summary>
        public static void PlayAt(string sound, Vector3 position, float volume = 1f) => Play(sound, true, position, volume);

        /// <summary>A bark or other line was spoken from <paramref name="source"/> for <paramref name="seconds"/>: the effects
        /// duck by <see cref="SkylineMix.BarkDuckShare"/>, scaled by how loud the source is where the listener stands.</summary>
        public static void NoteSpeech(AudioSource source, float seconds)
        {
            if (source == null || seconds <= 0f) return;
            Ensure();
            float w = 1f;
            if (source.spatialBlend >= 0.5f)
                w = Rolloff(Vector3.Distance(_listener, source.transform.position), source.minDistance, source.maxDistance);
            w *= SkylineMix.BarkDuckShare;
            if (w > 0.01f) _barks.Add(new Bark { until = _clock + seconds, weight = w });
        }

        static void Play(string sound, bool world, Vector3 position, float volume)
        {
            var clip = Get(sound);
            if (clip == null) return;
            Ensure();
            if (SkylineMix.Deferred(sound))
            {
                _pending.Add(new Pending { sound = sound, clip = clip, world = world, position = position, volume = volume, frame = Time.frameCount });
                return;
            }
            Start(world ? _world : _view, sound, clip, position, volume);
        }

        /// <summary>Plays the hit sounds held back this frame, minus those a gun's own impact wins over (one hit sound a
        /// shot). <see cref="SkylineMixer"/> calls it in LateUpdate.</summary>
        internal static void FlushPending()
        {
            if (_pending.Count == 0) return;
            int i = 0;
            while (i < _pending.Count)
            {
                int frame = _pending[i].frame, j = i;
                _frameSounds.Clear();
                while (j < _pending.Count && _pending[j].frame == frame) _frameSounds.Add(_pending[j++].sound);
                for (int k = i; k < j; k++)
                {
                    var p = _pending[k];
                    if (SkylineMix.LosesTo(p.sound, _frameSounds)) { OneHitDropped++; continue; }
                    Start(p.world ? _world : _view, p.sound, p.clip, p.position, p.volume);
                }
                i = j;
            }
            _pending.Clear();
        }

        static void Start(Pool pool, string sound, AudioClip clip, Vector3 position, float volume)
        {
            if (pool == null || pool.voices == null) return;
            var cat = SkylineMix.CategoryOf(sound);
            bool player = cat == SkylineSoundCategory.Guns || cat == SkylineSoundCategory.Handling;
            if (!player && _lastStart.TryGetValue(sound, out var last) && _clock - last < SkylineMix.RetriggerFor(sound))
            {
                Capped++;
                return;
            }

            float gain = Mathf.Clamp01(volume * SkylineMix.GainOf(sound) * SkylineMix.UserGain(cat));
            float audible = gain * (pool.world ? Rolloff(Vector3.Distance(_listener, position), WorldMinDistance, WorldMaxDistance) : 1f);

            // The mix pass: the bots' one-shots go through the voice gate (a few of each kind at once, the nearest wins).
            int gateId = -1;
            string group = SkylineMix.GroupOf(sound);
            if (group != null)
            {
                var verdict = _gate.Request(group, _clock, clip.length, audible);
                if (!verdict.admit) { GateDropped++; return; }
                gateId = verdict.id;
                if (verdict.displaced != null) foreach (int id in verdict.displaced) FadeGate(id);
            }

            int active = 0, same = 0;
            Voice oldestSame = null, victim = null;
            float victimScore = float.MaxValue;
            foreach (var v in pool.voices)
            {
                if (v.busy && (v.src == null || !v.src.isPlaying)) { v.busy = false; v.fading = false; }
                if (!v.busy || v.fading) continue;
                active++;
                if (v.sound == sound)
                {
                    same++;
                    if (oldestSame == null || v.started < oldestSame.started) oldestSame = v;
                }
                float s = Score(pool, v);
                if (s < victimScore) { victimScore = s; victim = v; }
            }
            if (same >= SkylineMix.MaxSameSound && oldestSame != null)
            {
                FadeOut(oldestSame);
                Capped++;
                active--;
                if (victim == oldestSame) victim = null;
            }
            if (active >= pool.budget)
            {
                if (victim == null || victimScore > audible * (cat == SkylineSoundCategory.Guns ? 4f : 1f))
                {
                    Stolen++;   // the new sound is the least audible: it is the one dropped
                    if (gateId >= 0) _gate.End(gateId);
                    return;
                }
                FadeOut(victim);
                Stolen++;
            }

            Voice slot = null;
            foreach (var v in pool.voices) if (!v.busy) { slot = v; break; }
            if (slot == null)
                foreach (var v in pool.voices)
                    if (v.fading && (slot == null || v.fade < slot.fade)) slot = v;
            if (slot == null) { Stolen++; if (gateId >= 0) _gate.End(gateId); return; }
            if (slot.busy) slot.src.Stop();

            var src = slot.src;
            src.clip = clip;
            src.priority = SkylineMix.Priority[(int)cat];
            src.volume = gain * SkylineMix.DuckGain(cat, _duck, _shotDuck);
            if (pool.world) src.transform.position = position;
            if (slot.eq != null) slot.eq.SetCut(SkylineMix.HarshCutDb(sound));
            src.Play();
            slot.sound = sound;
            slot.cat = cat;
            slot.gain = gain;
            slot.fade = 1f;
            slot.fading = false;
            slot.busy = true;
            slot.started = _clock;
            slot.gateId = gateId;
            _lastStart[sound] = _clock;
            Started++;
            _soundCounts[sound] = _soundCounts.TryGetValue(sound, out int count) ? count + 1 : 1;
            float hold = SkylineMix.ShotHoldFor(cat);   // the player's shot, hit or hurt cue: the bots duck under it
            if (hold > 0f) _shotUntil = Mathf.Max(_shotUntil, _clock + hold);
        }

        static void FadeGate(int id)
        {
            foreach (var pool in new[] { _world, _view })
            {
                if (pool == null || pool.voices == null) continue;
                foreach (var v in pool.voices) if (v.busy && v.gateId == id && !v.fading) { FadeOut(v); return; }
            }
        }

        /// <summary>Speaks a bot's bark (<paramref name="clip"/>, its babble) from <paramref name="source"/>, if the mix allows it:
        /// never while a teaching line is being spoken (dialogue wins), at most one bark at a time (the nearest wins), and at
        /// <see cref="SkylineMix.BarkGain"/> times <paramref name="userGain"/> (the Voices slider), ducked under the player's shots.
        /// Returns false when the bark stays silent (its bubble still shows).</summary>
        public static bool PlayBark(AudioSource source, AudioClip clip, float userGain)
        {
            if (source == null || clip == null) return false;
            Ensure();
            if (_tap != null && _clock < _speakingUntil) { BarksDropped++; return false; }
            for (int i = _barkVoices.Count - 1; i >= 0; i--)   // the same bot barking again replaces its own bark
                if (_barkVoices[i].src == source) { source.Stop(); _gate.End(_barkVoices[i].gateId); _barkVoices.RemoveAt(i); }
            float level = userGain * SkylineMix.BarkGain;
            float rolloff = source.spatialBlend >= 0.5f
                ? Rolloff(Vector3.Distance(_listener, source.transform.position), source.minDistance, source.maxDistance) : 1f;
            var verdict = _gate.Request("bark", _clock, clip.length, level * rolloff);
            if (!verdict.admit) { BarksDropped++; return false; }
            if (verdict.displaced != null)
                foreach (int id in verdict.displaced)
                    for (int i = _barkVoices.Count - 1; i >= 0; i--)
                        if (_barkVoices[i].gateId == id) { if (_barkVoices[i].src != null) _barkVoices[i].src.Stop(); _barkVoices.RemoveAt(i); }
            source.volume = SkylineMix.DuckGain(SkylineSoundCategory.Bots, 0f, _shotDuck);
            source.PlayOneShot(clip, level);
            _barkVoices.Add(new BarkVoice { src = source, gateId = verdict.id, until = _clock + clip.length });
            NoteSpeech(source, clip.length);
            BarksPlayed++;
            return true;
        }

        /// <summary>How much a playing voice would be missed: its level where the listener is, weighted by how much of it is
        /// left; the player's fresh shot counts four times.</summary>
        static float Score(Pool pool, Voice v)
        {
            float g = v.gain * (pool.world ? Rolloff(Vector3.Distance(_listener, v.src.transform.position), WorldMinDistance, WorldMaxDistance) : 1f);
            var clip = v.src.clip;
            float left = clip != null && clip.length > 0f ? 1f - Mathf.Clamp01(v.src.time / clip.length) : 0f;
            float s = g * (0.25f + 0.75f * left);
            if (v.cat == SkylineSoundCategory.Guns && _clock - v.started < 0.3f) s *= 4f;
            return s;
        }

        static void FadeOut(Voice v)
        {
            v.fading = true;
        }

        static float Rolloff(float d, float min, float max)
        {
            if (d <= min) return 1f;
            if (max <= min) return 0f;
            return Mathf.Clamp01(1f - (d - min) / (max - min));
        }

        /// <summary>One mix step (<see cref="SkylineMixer"/>'s Update): the duck depth from the voice tap and the barks,
        /// every voice's and adopted loop's volume, the fades of stolen voices.</summary>
        internal static void Tick(float dt, Vector3 listener)
        {
            _clock += dt;
            _listener = listener;
            if (_pending.Count > 0 && _pending[0].frame < Time.frameCount) FlushPending();   // in case LateUpdate missed them

            float depth = 0f;
            if (_tap != null)
            {
                if (_tap.TakePeak() > SkylineMix.SpeechThreshold) _speakingUntil = _clock + SkylineMix.SpeechHold;
                if (_clock < _speakingUntil) depth = 1f;
            }
            for (int i = _barks.Count - 1; i >= 0; i--)
            {
                if (_barks[i].until <= _clock) { _barks.RemoveAt(i); continue; }
                depth = Mathf.Max(depth, _barks[i].weight);
            }
            float tc = depth > _duck ? SkylineMix.DuckAttack : SkylineMix.DuckRelease;
            if (dt > 0f) _duck += (depth - _duck) * (1f - Mathf.Exp(-dt / tc));
            if (_duck > 0.5f) _duckedSeconds += dt;

            // the sidechain: the bots' chatter gets out of the way of the player's shot, hit and hurt cues
            float sdepth = _clock < _shotUntil ? 1f : 0f;
            float stc = sdepth > _shotDuck ? SkylineMix.ShotAttack : SkylineMix.ShotRelease;
            if (dt > 0f) _shotDuck += (sdepth - _shotDuck) * (1f - Mathf.Exp(-dt / stc));

            for (int i = _barkVoices.Count - 1; i >= 0; i--)
            {
                var b = _barkVoices[i];
                if (b.src == null) { _barkVoices.RemoveAt(i); continue; }
                if (b.until <= _clock) { b.src.volume = 1f; _barkVoices.RemoveAt(i); continue; }
                b.src.volume = SkylineMix.DuckGain(SkylineSoundCategory.Bots, 0f, _shotDuck);
            }

            TickPool(_view, dt);
            TickPool(_world, dt);

            // the beds: only the nearest few are heard (the rest fade out and are muted), the others follow the ambience bus
            for (int i = _loops.Count - 1; i >= 0; i--) if (_loops[i].src == null) _loops.RemoveAt(i);
            int n = _loops.Count;
            if (_loopAudible.Length < n) { _loopAudible = new float[n * 2]; _loopGains = new float[n * 2]; }
            for (int i = 0; i < n; i++)
            {
                var l = _loops[i];
                if (Mathf.Abs(l.src.volume - l.lastSet) > 1e-4f) l.original = l.src.volume;   // its owner changed it
                float reach = l.src.spatialBlend >= 0.5f
                    ? Rolloff(Vector3.Distance(_listener, l.src.transform.position), l.src.minDistance, l.src.maxDistance) : 1f;
                _loopAudible[i] = l.original * SkylineMix.GainOf(l.sound) * reach;
                _loopGains[i] = l.gain;
            }
            SkylineLoopBudget.Step(_loopAudible, _loopGains, n, SkylineMix.MaxLoops, dt, SkylineMix.LoopFadeSeconds);
            for (int i = 0; i < n; i++)
            {
                var l = _loops[i];
                l.gain = _loopGains[i];
                l.lastSet = Mathf.Clamp01(l.original * SkylineMix.GainOf(l.sound) * SkylineMix.UserGain(SkylineSoundCategory.Ambience)
                                          * SkylineMix.DuckGain(SkylineSoundCategory.Ambience, _duck, _shotDuck) * l.gain);
                l.src.volume = l.lastSet;
                bool silent = l.gain <= 0.001f;
                if (l.src.mute != silent) l.src.mute = silent;
            }
        }

        static void TickPool(Pool pool, float dt)
        {
            if (pool == null || pool.voices == null) return;
            foreach (var v in pool.voices)
            {
                if (!v.busy) continue;
                if (v.src == null || !v.src.isPlaying) { v.busy = false; v.fading = false; continue; }
                if (v.fading)
                {
                    v.fade -= dt / SkylineMix.StealFadeSeconds;
                    if (v.fade <= 0f) { v.src.Stop(); v.busy = false; v.fading = false; continue; }
                }
                v.src.volume = v.gain * SkylineMix.DuckGain(v.cat, _duck, _shotDuck) * v.fade;
            }
        }

        /// <summary>Takes the scene's looping beds (the bots' idle loops: sources playing a clip from <see cref="Get"/> named
        /// */idle_loop) onto the ambience bus. Their owners' volume is kept as the base; a change by the owner is followed.</summary>
        internal static void AdoptLoops(AudioSource[] sources)
        {
            if (sources == null) return;
            foreach (var s in sources)
            {
                if (s == null || !s.loop || s.clip == null) continue;
                if (_host != null && s.transform.IsChildOf(_host.transform)) continue;
                if (!TryNameOf(s.clip, out var name) || !name.EndsWith("/idle_loop", StringComparison.Ordinal)) continue;
                bool known = false;
                foreach (var l in _loops) if (l.src == s) { known = true; break; }
                if (!known) _loops.Add(new Loop { src = s, sound = name, original = s.volume });
            }
        }

        /// <summary>Follows <paramref name="tap"/> (the teaching-line voice) for ducking.</summary>
        internal static void SetSpeechTap(SkylineSpeechTap tap) => _tap = tap;

        internal static bool HasSpeechTap => _tap != null;

        /// <summary>One line for the pass's log.</summary>
        public static string Report()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "SKYLINE_MIX voices started {0}, stolen {1}, capped {2}, hit sounds dropped (one a shot) {3}, ducked {4:F1} s, loops on the ambience bus {5}, "
                + "gate dropped {6} (quiet {7}, full {8}, gap {9}, displaced {10}), barks played {11} dropped {12}, sidechain depth now {13:F2}",
                Started, Stolen, Capped, OneHitDropped, _duckedSeconds, _loops.Count,
                GateDropped, _gate.DroppedQuiet, _gate.DroppedFull, _gate.DroppedGap, _gate.Displaced, BarksPlayed, BarksDropped, _shotDuck);
        }

        /// <summary>The second line for the pass's log: how many times each sound started (the pool's voices), most first, so a
        /// recording's crowding can be mapped to its sources.</summary>
        public static string ReportSounds()
        {
            var list = new List<KeyValuePair<string, int>>(_soundCounts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder("SKYLINE_MIX per-sound starts");
            foreach (var kv in list) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            return sb.ToString();
        }

        /// <summary>Builds the voices and the mixer for this scene (again after a scene change destroyed them).</summary>
        public static void Ensure()
        {
            if (_host != null) return;      // Unity's null: also rebuilt after a scene change destroyed it
            _host = new GameObject("SkylineSounds");
            _pending.Clear();
            _lastStart.Clear();
            _loops.Clear();
            _barks.Clear();
            _barkVoices.Clear();
            _soundCounts.Clear();
            _gate.Clear();
            SkylineMix.ConfigureGate(_gate);
            _shotUntil = -1f;
            _shotDuck = 0f;
            _tap = null;
            _clock = 0f;
            _duck = 0f;
            _speakingUntil = -1f;
            _duckedSeconds = 0f;
            Started = Stolen = Capped = OneHitDropped = GateDropped = BarksPlayed = BarksDropped = 0;

            _view = new Pool { budget = SkylineMix.ViewVoices, world = false, voices = new Voice[SkylineMix.ViewVoices + SkylineMix.SpareVoices] };
            for (int i = 0; i < _view.voices.Length; i++)
            {
                var s = _host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _view.voices[i] = new Voice { src = s };
            }
            _world = new Pool { budget = SkylineMix.WorldVoices, world = true, voices = new Voice[SkylineMix.WorldVoices + SkylineMix.SpareVoices] };
            for (int i = 0; i < _world.voices.Length; i++)
            {
                var go = new GameObject("World voice " + i);
                go.transform.SetParent(_host.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = WorldMinDistance;
                s.maxDistance = WorldMaxDistance;
                _world.voices[i] = new Voice { src = s, eq = go.AddComponent<SkylineVoiceEq>() };
            }
            _host.AddComponent<SkylineMixer>();
        }
    }
}
