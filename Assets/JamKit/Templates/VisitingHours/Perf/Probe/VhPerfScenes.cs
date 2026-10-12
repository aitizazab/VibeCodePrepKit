using System;
using System.Collections.Generic;
using JamKit.Argument;
using JamKit.DeepBlock;
using JamKit.DreamEscape;
using JamKit.VisitingHours;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.Perf
{
    /// <summary>
    /// The gameplay scenes the perf probe measures and the flight through each, built from the game's own data (the hill's
    /// critical path and judge views, the house's rooms, the escape's rooms, the ward's framed stills), so the paths follow
    /// the content when builders change it. The editor estimate (VhPerfSceneReport) uses the same flights.
    /// </summary>
    public static class VhPerfScenes
    {
        public const string Hill1 = "hill1", Hill2 = "hill2", House = "house", Escape = "escape", Ward = "ward", Battle = "battle";
        public static readonly string[] All = { Hill1, Hill2, House, Escape, Ward, Battle };

        /// <summary>The Unity scene each probe scene runs in.</summary>
        public static string SceneOf(string id)
        {
            switch (id)
            {
                case Hill1: case Hill2: return DreamStreetPresenter.Scene;
                case House: case Escape: return DreamHousePresenter.Scene;
                case Ward: return VhPresenter.Scene;
                case Battle: return ArgumentPrototype.Scene;
                default: return null;
            }
        }

        /// <summary>The presenter chapter that starts it (the ward starts the slice scene directly).</summary>
        public static string ChapterOf(string id)
        {
            switch (id)
            {
                case Hill1: case Hill2: return LarchHill.ChapterArrival;
                case House: return DreamHouse.ChapterArrival;
                case Escape: return DreamHouse.ChapterTips;
                case Battle: return ArgumentPrototype.Watch;
                default: return null;
            }
        }

        public static string Describe(string id)
        {
            switch (id)
            {
                case Hill1: return "Larch Hill, Night 1: the critical path at a run, then the judge's vistas";
                case Hill2: return "Larch Hill, Night 2: Rowan Street to the gate, the middle band's line, the upper vistas";
                case House: return "The Lasname house: a slow look round every room at eye height";
                case Escape: return "The escape: down the middle of every escape room";
                case Ward: return "Ward 4 West, Day 1, Day 2 and the booth: the framed views, panning";
                case Battle: return "The argument battle (Dream-Lena), its autopilot playing; the camera is the battle's";
                default: return id;
            }
        }

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        // ---------- The hill ----------

        /// <summary>Night 1: every leg of the critical path at a run (foot polylines from the game), then the judge's views.</summary>
        public static VhPerfFlight Hill1Flight(LarchHill h, Vector3[][] routes)
        {
            var f = new VhPerfFlight(Hill1);
            if (routes != null)
                for (int i = 0; i < routes.Length; i++) f.Walk("path " + (i + 1), routes[i], 1.6f, 7f);
            if (h != null)
                foreach (var v in h.views) f.View("view: " + v.name, U(v.eye), U(v.look), 2.5f, 30f);
            return f;
        }

        /// <summary>Night 2: from Night2Start along Rowan Street to the gate, the middle band's escape line, the views uphill.</summary>
        public static VhPerfFlight Hill2Flight(LarchHill h)
        {
            var f = new VhPerfFlight(Hill2);
            var start = U(HillAnchors.Night2Start);
            var gate = U(HillAnchors.FrontGate);
            f.Walk("Rowan Street to the gate", new[] { start, Vector3.Lerp(start, gate, 0.5f), gate }, 1.6f, 5f);
            if (h != null && h.escapeLine != null) f.Walk("the middle band's line", RoutePoints(h, h.escapeLine), 1.6f, 7f);
            if (h != null)
                foreach (var v in h.views)
                    if (v.eye.Z > HillAnchors.FrontGate.Z) f.View("view: " + v.name, U(v.eye), U(v.look), 2.5f, 30f);
            return f;
        }

        /// <summary>A route's polyline at foot height (as DreamStreetGame.LintRoutes does for Night 1).</summary>
        public static Vector3[] RoutePoints(LarchHill h, BlockRoute route)
        {
            var line = route.course.route;
            int n = Mathf.Max(2, (int)(line.Length / 2f));
            var pts = new Vector3[n + 1];
            float y = route.start.Y;
            for (int k = 0; k <= n; k++)
            {
                var p = line.PointAt(line.Length * k / n, 0f);
                float top = h.TopAt(p.X, p.Z, y + 2.5f);
                if (top > -7f) y = top;
                pts[k] = new Vector3(p.X, y + 0.1f, p.Z);
            }
            return pts;
        }

        // ---------- The house and the escape ----------

        /// <summary>Every room of the house: stand in its middle at eye height and look all the way round.</summary>
        public static VhPerfFlight HouseFlight(Transform houseRoot)
        {
            var f = new VhPerfFlight(House);
            var plan = DreamHouse.Build();
            foreach (var room in plan.rooms)
            {
                if (room.rects.Count == 0) continue;
                var r = room.rects[0];
                var local = new Vector3(r.CX, room.floorY + 1.5f, r.CZ);
                var eye = houseRoot != null ? houseRoot.TransformPoint(local) : local;
                float yaw0 = houseRoot != null ? houseRoot.eulerAngles.y : 0f;
                f.Station("room: " + room.name, eye, yaw0, yaw0 + 360f, 8f, room.outside ? 4f : 3f);
            }
            return f;
        }

        /// <summary>The escape: down the middle of each room, upright, at a run (it's built along +Z from escapeRoot).</summary>
        public static VhPerfFlight EscapeFlight(Transform escapeRoot)
        {
            var f = new VhPerfFlight(Escape);
            var e = HouseEscape.Build();
            var o = escapeRoot != null ? escapeRoot.position : U(HouseEscape.Origin);
            foreach (var room in e.rooms)
            {
                float s0 = room.s0 + 1f, s1 = Mathf.Max(room.s0 + 1.5f, room.s1 - 1f);
                var a = o + new Vector3(0f, room.floorTop, s0);
                var b = o + new Vector3(0f, room.floorTop, s1);
                f.Walk("room: " + room.name, new[] { a, b }, 1.6f, 9f, 3f, 4f);
            }
            return f;
        }

        // ---------- The ward ----------

        /// <summary>The ward's framed stills (VhWardShot's), each panning 40 degrees; entering each area first.</summary>
        public static VhPerfFlight WardFlight(Action<SliceArea> enter)
        {
            var f = new VhPerfFlight(Ward);
            SliceArea built = (SliceArea)(-1);
            foreach (var fr in VhWardShot.Framings)
            {
                Action onStart = null;
                if (fr.area != built && enter != null)
                {
                    var area = fr.area;
                    onStart = () => enter(area);
                    built = fr.area;
                }
                f.Station(fr.area + ": " + fr.name, new Vector3(fr.at.x, 1.55f, fr.at.y), fr.yaw - 20f, fr.yaw + 20f, fr.pitch, 3f, true, onStart);
            }
            return f;
        }

        // ---------- The battle ----------

        public static VhPerfFlight BattleFlight(float seconds)
        {
            var f = new VhPerfFlight(Battle) { overrideCamera = false };
            f.Hold("the battle, its autopilot playing", seconds);
            return f;
        }
    }
}
