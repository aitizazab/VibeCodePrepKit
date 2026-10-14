using JamKit.Movement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the player rig into the open scene (Task 14): body (CharacterController, MovementController,
    /// MovementCamera, SkylinePlayer) &gt; Head (pitch, eye height) &gt; Rig (MovementCamera's bob, roll and dip) &gt; Kick
    /// (SkylinePlayer's kick and shake) &gt; Camera (MainCamera, AudioListener), plus the code-built viewmodel. The guns,
    /// armour and boost are made at run time from the pure data.</summary>
    public static class SkylinePlayerBuilder
    {
        const float NearClip = 0.05f, FarClip = 600f, BaseFov = 80f;

        public static SkylinePlayer Build(Transform parent, Vector3 feet, float yaw)
        {
            var body = new GameObject("SkylinePlayer");
            if (parent != null) body.transform.SetParent(parent, false);
            body.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));

            var head = new GameObject("Head").transform;
            head.SetParent(body.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("Rig").transform;
            rig.SetParent(head, false);
            var kick = new GameObject("Kick").transform;
            kick.SetParent(rig, false);
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(kick, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = NearClip;
            cam.farClipPlane = FarClip;          // the viewmodel's anchor (1 km down) is always outside it
            cam.fieldOfView = BaseFov;
            camGo.AddComponent<AudioListener>();

            var controller = body.AddComponent<CharacterController>();
            var move = body.AddComponent<MovementController>();
            move.controller = controller;
            move.head = head;
            move.cam = cam;
            move.managePointerLock = false;
            move.onlyMarkedWalls = true;

            var mc = body.AddComponent<MovementCamera>();
            mc.body = move;
            mc.rig = rig;
            mc.cam = cam;
            SkylinePlayer.ApplyViewFeel(mc);   // no view kick on a sprint start; the speed widening only well above sprint speed

            var player = body.AddComponent<SkylinePlayer>();
            player.movement = move;
            player.cam = cam;
            player.kickPivot = kick;

            var vm = new GameObject("Viewmodel").AddComponent<SkylineViewmodel>();
            vm.transform.SetParent(body.transform, false);
            vm.player = player;
            player.viewmodel = vm;
            return player;
        }
    }
}
