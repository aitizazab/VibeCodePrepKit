using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The title and the opening's set, cheaper when nothing changes on screen (docs/redesign/perf.md, Day item 6, win 23):
    /// Nadia (and the blanket) skin only while they're on screen, inside generous fixed bounds (they skinned every frame
    /// off-screen); a light dimmed to nothing is switched off rather than left drawing at intensity 0; the phone's and the
    /// tablet's UI panels stop redrawing into their textures while their screens are out of view. On the stage's root;
    /// nothing allocated per frame. Play mode only (the editor's captures keep skinning on every render).
    /// </summary>
    public sealed class VhTitlePerf : MonoBehaviour
    {
        /// <summary>Metres added round each skinned mesh's bounds (her arm reaches for the phone, the blanket moves).</summary>
        public const float BoundsMargin = 0.8f;

        Light[] _lights;
        VhStage _stage;
        Renderer _phone;
        readonly List<Renderer> _tablet = new List<Renderer>();

        /// <summary>Settles the built stage (VhStageBuilder.Build calls it once).</summary>
        public static void Settle(VhStage s)
        {
            if (s == null || !Application.isPlaying) return;
            foreach (var smr in s.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.updateWhenOffscreen) continue;
                var b = smr.sharedMesh != null ? smr.sharedMesh.bounds : smr.localBounds;
                b.Expand(BoundsMargin * 2f);
                smr.updateWhenOffscreen = false;
                smr.localBounds = b;
            }
            var p = s.gameObject.GetComponent<VhTitlePerf>();
            if (p == null) p = s.gameObject.AddComponent<VhTitlePerf>();
            p.Bind(s);
        }

        void Bind(VhStage s)
        {
            _stage = s;
            _lights = s.GetComponentsInChildren<Light>(true);
            _phone = s.phoneScreen;
            _tablet.Clear();
            if (s.tabletScreenMat != null)
                foreach (var r in s.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial == s.tabletScreenMat) _tablet.Add(r);
        }

        void LateUpdate()
        {
            if (_lights != null)
                for (int i = 0; i < _lights.Length; i++)
                {
                    var l = _lights[i];
                    if (l == null) continue;
                    bool on = l.intensity > 0.0005f;
                    if (l.enabled != on) l.enabled = on;
                }
            if (_stage == null) return;
            if (_stage.phonePanel != null && _phone != null) _stage.phonePanel.SetDrawing(_phone.isVisible);
            if (_stage.tabletPanel != null && _tablet.Count > 0)
            {
                bool seen = false;
                for (int i = 0; i < _tablet.Count && !seen; i++) seen = _tablet[i] != null && _tablet[i].isVisible;
                _stage.tabletPanel.SetDrawing(seen);
            }
        }
    }
}
