using JamKit.Shell;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The chase's pursuer as you see and hear it: a tall curtain of dream-static across the route at the
    /// pursuer's distance (core <c>Pursuer</c>), hissing louder and burning hotter as it closes in.
    /// PLACEHOLDER look for the dream sequences.
    /// </summary>
    public sealed class LabPursuerView : MonoBehaviour
    {
        public Renderer curtain;
        public AudioSource hiss;
        [Range(0f, 1f)] public float hissVolume = 0.6f;

        static readonly int ThreatId = Shader.PropertyToID("_Threat");
        MaterialPropertyBlock _block;
        float _threat;

        void Start()
        {
            _block = new MaterialPropertyBlock();
            if (hiss != null)
            {
                hiss.clip = GameRoot.Exists ? GameRoot.Audio.ClipFor("mv_static") : ProceduralAudio.Get("mv_static");
                hiss.loop = true;
                hiss.spatialBlend = 0.7f;
                hiss.dopplerLevel = 0f;
                hiss.minDistance = 4f;
                hiss.maxDistance = 40f;
                hiss.volume = 0f;
                if (hiss.clip != null) hiss.Play();
            }
            Show(false);
        }

        public void Show(bool shown)
        {
            if (curtain != null) curtain.enabled = shown;
            if (!shown && hiss != null) hiss.volume = 0f;
        }

        /// <summary>Places the curtain at distance <paramref name="z"/> along the route, around height <paramref name="y"/>.</summary>
        public void Place(float z, float y, float threat)
        {
            transform.position = new Vector3(0f, y, z);
            _threat = Mathf.Lerp(_threat, threat, 0.2f);
            if (curtain != null)
            {
                curtain.GetPropertyBlock(_block);
                _block.SetFloat(ThreatId, _threat);
                curtain.SetPropertyBlock(_block);
            }
            if (hiss != null)
            {
                float sfx = GameRoot.Exists ? GameRoot.Settings.sfxVolume : 0.8f;
                hiss.volume = (0.35f + 0.65f * _threat) * hissVolume * sfx;
                hiss.pitch = 0.9f + 0.3f * _threat;
                hiss.mute = GameRoot.Exists && GameRoot.Audio.Muted;
            }
        }
    }
}
