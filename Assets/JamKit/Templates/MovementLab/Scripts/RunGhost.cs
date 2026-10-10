using JamKit.Movement;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>Which ghost races you: your best run, the developer's (the creator's shipped best), or none.</summary>
    public enum GhostChoice { Yours, Developer, Off }

    /// <summary>
    /// The ghost of a timed run (the Gauntlet, the dream tool chapter), as Titanfall 2's Gauntlet has it: your best run
    /// or the developer's, picked with the Ghost action, which cycles your best → the developer's → off (skipping a
    /// ghost that doesn't exist yet). One translucent figure, tinted cool for yours and gold for the developer's, its
    /// feet on the recorded position at the run's time. Plain C#, owned by the run component; no allocation per frame.
    /// </summary>
    public sealed class RunGhost
    {
        public readonly GhostTrack best = new GhostTrack();
        public readonly GhostTrack developer = new GhostTrack();
        readonly Transform _figure;
        readonly Renderer[] _renderers;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        GhostChoice _choice = GhostChoice.Yours;
        GhostChoice _tinted = (GhostChoice)(-1);

        static readonly int ColorId = Shader.PropertyToID("_Color"), RimId = Shader.PropertyToID("_RimColor");
        static readonly Color YoursColor = new Color(0.55f, 0.85f, 1f, 0.28f), YoursRim = new Color(0.8f, 0.95f, 1f, 0.9f);
        static readonly Color DevColor = new Color(1f, 0.82f, 0.4f, 0.3f), DevRim = new Color(1f, 0.95f, 0.72f, 0.95f);

        public RunGhost(Transform figure)
        {
            _figure = figure;
            _renderers = figure != null ? figure.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (_figure != null) _figure.gameObject.SetActive(false);
        }

        public GhostChoice Choice => _choice;
        public bool Visible => _figure != null && _figure.gameObject.activeSelf;
        public bool Has(GhostChoice c) => c == GhostChoice.Off || (c == GhostChoice.Yours ? best.Count > 1 : developer.Count > 1);
        public GhostTrack Track => _choice == GhostChoice.Yours ? best : _choice == GhostChoice.Developer ? developer : null;

        /// <summary>What the HUD says about the ghost.</summary>
        public string Label => _choice == GhostChoice.Off ? "Ghost: off"
            : !Has(_choice) ? (_choice == GhostChoice.Yours ? "Ghost: your best (none yet)" : "Ghost: the developer's (none yet)")
            : _choice == GhostChoice.Yours ? "Ghost: your best" : "Ghost: the developer's";

        /// <summary>Your best if you have one, else the developer's, else off.</summary>
        public void PickDefault()
        {
            _choice = Has(GhostChoice.Yours) ? GhostChoice.Yours : Has(GhostChoice.Developer) ? GhostChoice.Developer : GhostChoice.Off;
        }

        /// <summary>The Ghost action: your best → the developer's → off → your best, skipping one that doesn't exist.</summary>
        public void Cycle()
        {
            for (int i = 0; i < 3; i++)
            {
                _choice = _choice == GhostChoice.Yours ? GhostChoice.Developer : _choice == GhostChoice.Developer ? GhostChoice.Off : GhostChoice.Yours;
                if (Has(_choice)) return;
            }
        }

        public void Set(GhostChoice c) => _choice = c;

        public void Hide()
        {
            if (_figure != null && _figure.gameObject.activeSelf) _figure.gameObject.SetActive(false);
        }

        /// <summary>Shows the chosen ghost <paramref name="time"/> seconds into the run, or hides it.</summary>
        public void Place(bool running, float time)
        {
            if (_figure == null) return;
            var track = Track;
            bool show = running && track != null && track.Count > 1;
            Vector3 at = Vector3.zero;
            float yaw = 0f;
            if (show)
            {
                show = track.Sample(time, out var p, out yaw);
                at = new Vector3(p.X, p.Y, p.Z);
            }
            if (_figure.gameObject.activeSelf != show) _figure.gameObject.SetActive(show);
            if (!show) return;
            _figure.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            if (_tinted != _choice)
            {
                _tinted = _choice;
                bool dev = _choice == GhostChoice.Developer;
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetColor(ColorId, dev ? DevColor : YoursColor);
                    _block.SetColor(RimId, dev ? DevRim : YoursRim);
                    r.SetPropertyBlock(_block);
                }
            }
        }
    }
}
