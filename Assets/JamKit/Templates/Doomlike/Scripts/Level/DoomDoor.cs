using UnityEngine;
using UnityEngine.AI;

namespace JamKit.Doomlike
{
    /// <summary>
    /// A door, a secret wall or a monster-closet wall. Doors and secret walls rise into the ceiling,
    /// closet walls sink into the floor; each stays open. Locked doors, secret walls and closet walls
    /// cut themselves out of the NavMesh while closed, so enemies don't try to walk through them;
    /// plain doors don't (enemies open them by walking up). DoomGame decides when to open.
    /// </summary>
    public sealed class DoomDoor : MonoBehaviour
    {
        public enum DoorKind { Normal, Locked, Secret, Closet }

        public DoorKind kind;
        /// <summary>Locked doors: the key colour (core KeyColor as int).</summary>
        public int keyColor;
        public int cellX, cellY;
        public float travel = 3f;
        public float openSeconds = 0.55f;
        public NavMeshObstacle obstacle;

        Vector3 _closed;
        float _t;
        bool _opening, _open;

        public bool IsOpen => _open;
        /// <summary>Opening or open: no longer blocking anyone who waits a moment.</summary>
        public bool IsOpening => _opening || _open;
        public float Progress01 => _t;

        public void Init(DoorKind kind, int keyColor, int x, int y, float travel)
        {
            this.kind = kind;
            this.keyColor = keyColor;
            cellX = x;
            cellY = y;
            this.travel = travel;
            _closed = transform.localPosition;
        }

        /// <summary>Starts opening. False if it already is.</summary>
        public bool Open()
        {
            if (_opening || _open) return false;
            _opening = true;
            if (obstacle != null) obstacle.enabled = false;
            return true;
        }

        /// <summary>Opens fully at once (debug, tests).</summary>
        public void OpenNow()
        {
            Open();
            _t = 1f;
            Place();
            _opening = false;
            _open = true;
        }

        public void Tick(float dt)
        {
            if (!_opening) return;
            _t = Mathf.Min(1f, _t + dt / Mathf.Max(0.05f, openSeconds));
            Place();
            if (_t >= 1f)
            {
                _opening = false;
                _open = true;
            }
        }

        void Place()
        {
            float s = _t * _t * (3f - 2f * _t);
            float dir = kind == DoorKind.Closet ? -1f : 1f;
            transform.localPosition = _closed + Vector3.up * (dir * travel * s);
        }
    }
}
