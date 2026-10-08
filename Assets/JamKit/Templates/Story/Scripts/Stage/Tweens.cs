using System;
using System.Collections.Generic;

namespace JamKit.Narrative
{
    /// <summary>
    /// Tiny tween runner on real time: each tween calls apply(0..1, eased) every frame and done at the
    /// end. A new tween with the same key finishes the old one first. Blocking tweens make
    /// <see cref="Busy"/> true (the next line waits); <see cref="CompleteAll"/> jumps every tween to its end.
    /// </summary>
    public sealed class Tweens
    {
        sealed class Tween
        {
            public object key;
            public float duration, t;
            public Action<float> apply;
            public Action done;
            public bool blocking;
            public bool finished;
        }

        static readonly Predicate<Tween> IsFinished = x => x.finished;
        readonly List<Tween> _active = new List<Tween>();
        readonly List<Tween> _finishing = new List<Tween>();

        public bool Busy
        {
            get
            {
                foreach (var t in _active) if (t.blocking && !t.finished) return true;
                return false;
            }
        }

        public int Count => _active.Count;

        public static float Smooth(float k) => k <= 0f ? 0f : k >= 1f ? 1f : k * k * (3f - 2f * k);

        /// <summary>Starts a tween. Zero seconds applies the end at once.</summary>
        public void Start(object key, float seconds, Action<float> apply, Action done = null, bool blocking = false)
        {
            if (key != null) Finish(key);
            if (!(seconds > 0.0001f))
            {
                apply?.Invoke(1f);
                done?.Invoke();
                return;
            }
            apply?.Invoke(0f);
            _active.Add(new Tween { key = key, duration = seconds, apply = apply, done = done, blocking = blocking });
        }

        /// <summary>Jumps the tween with this key to its end.</summary>
        public void Finish(object key)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var t = _active[i];
                if (t.finished || !Equals(t.key, key)) continue;
                End(t);
            }
            _active.RemoveAll(IsFinished);
        }

        public bool IsRunning(object key)
        {
            foreach (var t in _active) if (!t.finished && Equals(t.key, key)) return true;
            return false;
        }

        public void CompleteAll()
        {
            // Done callbacks can start new tweens (a fade to black, then back): keep going until settled.
            for (int guard = 0; guard < 8 && _active.Count > 0; guard++)
            {
                _finishing.Clear();
                _finishing.AddRange(_active);
                foreach (var t in _finishing) if (!t.finished) End(t);
                _active.RemoveAll(IsFinished);
            }
        }

        public void Tick(float dt)
        {
            if (_active.Count == 0) return;
            _finishing.Clear();
            _finishing.AddRange(_active);
            foreach (var t in _finishing)
            {
                if (t.finished) continue;
                t.t += dt;
                float k = t.t / t.duration;
                if (k >= 1f) End(t);
                else t.apply?.Invoke(Smooth(k));
            }
            _active.RemoveAll(IsFinished);
        }

        static void End(Tween t)
        {
            t.finished = true;
            t.apply?.Invoke(1f);
            t.done?.Invoke();
        }

        public void Clear() => _active.Clear();
    }
}
