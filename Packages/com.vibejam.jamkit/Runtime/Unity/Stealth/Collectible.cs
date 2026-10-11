using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.Events;

namespace JamKit.Stealth
{
    /// <summary>
    /// One thing to find: picked up when the <see cref="StealthPlayer"/> (or, without one, the main camera) comes within
    /// <see cref="radius"/>; it spins and bobs until then. Needs no collider. Registers with
    /// <see cref="CollectibleCounter.Current"/>.
    /// </summary>
    public sealed class Collectible : MonoBehaviour
    {
        public string id = "";
        public float radius = 1.1f;
        [Tooltip("The visual that spins and bobs (a child). Empty: this object.")]
        public Transform visual;
        public string sound = "coin";

        int _index = -1;
        CollectibleCounter _counter;
        Vector3 _rest;

        public bool Collected => _counter != null && _counter.Set.IsCollected(_index);

        void Start()
        {
            _counter = CollectibleCounter.Current;
            _index = _counter.Register(this);
            if (visual == null) visual = transform;
            _rest = visual.localPosition;
        }

        internal void Show(bool on)
        {
            if (visual != null) visual.gameObject.SetActive(on);
            enabled = on;
        }

        void Update()
        {
            if (_counter == null || _index < 0) return;
            float t = Time.time;
            if (visual != null && visual != transform)
            {
                visual.localPosition = _rest + Vector3.up * (0.12f * Mathf.Sin(t * 2.2f));
                visual.localRotation = Quaternion.Euler(0f, t * 90f, 0f);
            }
            var player = StealthPlayer.Current;
            Vector3 at;
            if (player != null) at = player.transform.position + Vector3.up * 0.9f;
            else if (Camera.main != null) at = Camera.main.transform.position;
            else return;
            if ((at - transform.position).sqrMagnitude > radius * radius) return;
            if (!_counter.Collect(_index)) return;
            if (GameRoot.Exists && !string.IsNullOrEmpty(sound)) GameRoot.Audio.Play(sound, 0.7f);
            Show(false);
        }
    }
}
