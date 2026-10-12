using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// A memory in the house (<see cref="HouseMemory"/>): the thing itself (a calendar, a photo, a drawing, a radio, a paper
    /// hat, a star wheel, the height marks), a soft camera-facing glow round it (JamKit/House/MemoryGlow: depth-faded, no
    /// hard edge) that breathes so it can be found, a small light on the thing, and the picture shown up close
    /// when she takes it. The game moves it when the house takes it back (<see cref="MoveTo"/>) and hides it once found.
    /// </summary>
    public sealed class HouseMemoryView : MonoBehaviour
    {
        public string id;
        /// <summary>What she sees up close (the memory's picture), or null.</summary>
        public Texture2D picture;
        public Transform glow;
        public Light lamp;

        float _base = 1f, _t;
        bool _shown = true;

        bool _found;
        public bool Shown => _shown;
        public bool Found => _found;

        /// <summary>Taken (she has it as a card now): the thing stays where it is, its glow goes out.</summary>
        public void SetFound(bool found)
        {
            _found = found;
            if (glow != null) glow.gameObject.SetActive(!found);
            if (lamp != null) lamp.enabled = !found;
        }

        void Start()
        {
            if (glow != null) _base = glow.localScale.x;
        }

        public void Show(bool on)
        {
            _shown = on;
            gameObject.SetActive(on);
        }

        /// <summary>The house hid it here (world position; the thing faces <paramref name="yaw"/>).</summary>
        public void MoveTo(Vector3 world, float yaw)
        {
            transform.SetPositionAndRotation(world, Quaternion.Euler(0f, yaw, 0f));
            Show(true);
            SetFound(false);
        }

        /// <summary>She can take it: near enough and looking at it (a cone of <paramref name="cone"/> degrees).</summary>
        public bool InReach(Vector3 eye, Vector3 forward, float range = 2.3f, float cone = 26f)
        {
            if (!_shown || _found) return false;
            var d = transform.position - eye;
            float len = d.magnitude;
            if (len > range) return false;
            if (len < 0.35f) return true;
            return Vector3.Angle(forward, d) < cone + Mathf.Lerp(18f, 0f, len / range);
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = 1f + 0.12f * Mathf.Sin(_t * 2.4f);
            if (glow != null) glow.localScale = Vector3.one * (_base * k);
            if (lamp != null) lamp.intensity = 0.5f + 0.1f * Mathf.Sin(_t * 2.4f);
        }
    }
}
