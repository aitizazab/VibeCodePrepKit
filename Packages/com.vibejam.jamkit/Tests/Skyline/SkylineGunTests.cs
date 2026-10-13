using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Combat;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class SkylineGunTests
    {
        const float H = 1f / 60f;

        static HandlingClip Clip(HandlingClipKind k, float s, params HandlingEvent[] ev) => new HandlingClip(k, s, ev, null);

        static GunDef TestGun() => new GunDef
        {
            id = "test",
            weapon = new WeaponDef { magazineSize = 3, fireMode = FireMode.Semi, fireInterval = 0.25f },
            clips = new[]
            {
                Clip(HandlingClipKind.Equip, 0.5f, new HandlingEvent(0f, "unfold", "test/unfold"), new HandlingEvent(1f, "lock", "test/lock")),
                Clip(HandlingClipKind.Fire, 0.2f, new HandlingEvent(0f, "shot", "test/shot")),
                Clip(HandlingClipKind.DryFire, 0.2f, new HandlingEvent(0f, "click", "test/click")),
                Clip(HandlingClipKind.ReloadPartial, 1.0f, new HandlingEvent(0.2f, "mag_out", "test/mag_out"), new HandlingEvent(0.6f, "seat", "test/seat")),
                Clip(HandlingClipKind.ReloadEmpty, 1.5f, new HandlingEvent(0.2f, "mag_out", "test/mag_out"), new HandlingEvent(0.6f, "seat", "test/seat"), new HandlingEvent(0.9f, "bolt", "test/bolt")),
                Clip(HandlingClipKind.Inspect, 2f, new HandlingEvent(0.5f, "spin", "test/spin")),
            },
        };

        /// <summary>A gun ticked at 60 fps with every cue time-stamped.</summary>
        sealed class Rig
        {
            public readonly SkylineGun gun;
            public readonly List<GunCue> cues = new List<GunCue>();
            public readonly List<float> at = new List<float>();
            public float t;

            public Rig(GunDef d, bool waitForEquip = true)
            {
                gun = new SkylineGun(d);
                gun.Equip(cues);
                Stamp();
                if (waitForEquip) Idle(0.6f);
            }

            void Stamp() { while (at.Count < cues.Count) at.Add(t); }

            public void Idle(float seconds)
            {
                int steps = (int)Math.Round(seconds / H);
                for (int i = 0; i < steps; i++) { t += H; gun.Tick(H, false, false, cues); Stamp(); }
            }

            public void Press()
            {
                t += H;
                gun.Tick(H, true, true, cues);
                Stamp();
            }

            public void Reload() { gun.Reload(cues); Stamp(); }

            public int Count(Predicate<GunCue> p) { int n = 0; foreach (var c in cues) if (p(c)) n++; return n; }

            /// <summary>The time of the first cue matching <paramref name="p"/> at or after <paramref name="from"/>; NaN if none.</summary>
            public float TimeOf(Predicate<GunCue> p, float from = 0f)
            {
                for (int i = 0; i < cues.Count; i++) if (at[i] >= from && p(cues[i])) return at[i];
                return float.NaN;
            }
        }

        static bool Shot(GunCue c) => c.kind == GunCueKind.Shot;
        static Predicate<GunCue> Started(HandlingClipKind k) => c => c.kind == GunCueKind.ClipStarted && c.clip == k;
        static Predicate<GunCue> Event(string id) => c => c.kind == GunCueKind.Event && c.ev.id == id;

        [Test]
        public void EquipBlocksFiringUntilItsClipEnds()
        {
            var r = new Rig(TestGun(), waitForEquip: false);
            Assert.That(r.Count(Started(HandlingClipKind.Equip)), Is.EqualTo(1));
            Assert.That(r.Count(Event("unfold")), Is.EqualTo(1), "the event at 0 fires with the clip");
            r.Press();
            Assert.That(r.Count(Shot), Is.EqualTo(0), "can't fire while equipping");
            r.Idle(0.5f);
            Assert.That(r.TimeOf(Event("lock")), Is.EqualTo(0.5f).Within(H + 1e-4f));
            r.Press();
            Assert.That(r.Count(Shot), Is.EqualTo(1));
            Assert.That(r.gun.Ammo, Is.EqualTo(2));
        }

        [Test]
        public void EachShotPlaysTheFireClipAndItsShotSound()
        {
            var r = new Rig(TestGun());
            float t0 = r.t;
            r.Press();
            Assert.That(r.TimeOf(Started(HandlingClipKind.Fire), t0), Is.EqualTo(r.t));
            Assert.That(r.TimeOf(Shot, t0), Is.EqualTo(r.t));
            Assert.That(r.TimeOf(Event("shot"), t0), Is.EqualTo(r.t), "the shot sound on the same tick");
        }

        [Test]
        public void AnEmptyMagazineClicksThenReloadsFromEmpty()
        {
            var r = new Rig(TestGun());
            for (int i = 0; i < 3; i++) { r.Press(); r.Idle(0.3f); }
            Assert.That(r.gun.Ammo, Is.EqualTo(0));
            Assert.That(r.gun.State.IsReloading, Is.False, "no automatic reload on the last shot");
            r.Press();
            float dry = r.t;
            Assert.That(r.TimeOf(Started(HandlingClipKind.DryFire), dry), Is.EqualTo(dry));
            Assert.That(r.TimeOf(Event("click"), dry), Is.EqualTo(dry));
            r.Idle(2f);
            float reload = r.TimeOf(Started(HandlingClipKind.ReloadEmpty), dry);
            Assert.That(reload, Is.EqualTo(dry + 0.2f).Within(H + 1e-4f), "the from-empty reload starts when the click ends");
            Assert.That(r.TimeOf(Event("bolt"), reload), Is.EqualTo(reload + 0.9f * 1.5f).Within(H + 1e-4f));
            Assert.That(r.gun.Ammo, Is.EqualTo(3));
        }

        [Test]
        public void AManualReloadWithRoundsLeftIsThePartialReload()
        {
            var r = new Rig(TestGun());
            r.Press();
            r.Idle(0.3f);
            float t0 = r.t;
            r.Reload();
            var started = r.cues.FindLast(c => c.kind == GunCueKind.ClipStarted);
            Assert.That(started.clip, Is.EqualTo(HandlingClipKind.ReloadPartial));
            Assert.That(started.seconds, Is.EqualTo(1.0f).Within(1e-5f));
            r.Idle(1.05f);
            Assert.That(r.gun.Ammo, Is.EqualTo(3));
            Assert.That(r.Count(Started(HandlingClipKind.ReloadEmpty)), Is.EqualTo(0));
            Assert.That(r.TimeOf(Event("seat"), t0), Is.EqualTo(t0 + 0.6f).Within(H + 1e-4f));
        }

        [Test]
        public void ReloadSpeedRescalesTheReloadAndItsEvents()
        {
            var r = new Rig(TestGun());
            r.gun.ReloadSpeed = 1.25f;
            Assert.That(r.gun.ReloadSeconds(false), Is.EqualTo(0.8f).Within(1e-5f));
            r.Press();
            r.Idle(0.3f);
            float t0 = r.t;
            r.Reload();
            Assert.That(r.cues.FindLast(c => c.kind == GunCueKind.ClipStarted).seconds, Is.EqualTo(0.8f).Within(1e-5f));
            r.Idle(0.8f - 2f * H);
            Assert.That(r.gun.Ammo, Is.EqualTo(2), "the ammo isn't in before the clip ends");
            r.Idle(3f * H);
            Assert.That(r.gun.Ammo, Is.EqualTo(3), "and it is in when the clip ends");
            Assert.That(r.TimeOf(Event("seat"), t0), Is.EqualTo(t0 + 0.6f * 0.8f).Within(H + 1e-4f), "the seat moved with the reload");
        }

        [Test]
        public void SwappingMidReloadCancelsItWithNoLateSounds()
        {
            var cues = new List<GunCue>();
            var a = new SkylineGun(TestGun());
            var b = new SkylineGun(TestGun());
            var loadout = new Loadout(a, b, cues);
            for (int i = 0; i < 40; i++) a.Tick(H, false, false, cues);
            a.Tick(H, true, true, cues);
            for (int i = 0; i < 20; i++) a.Tick(H, false, false, cues);
            a.Reload(cues);
            for (int i = 0; i < 6; i++) a.Tick(H, false, false, cues);   // 0.1 s: before mag_out at 0.2 s
            loadout.Swap(cues);
            int afterSwap = cues.Count;
            Assert.That(loadout.Active, Is.SameAs(b));
            Assert.That(a.State.IsReloading, Is.False);
            Assert.That(a.Equipped, Is.False);
            Assert.That(a.Handling.Playing, Is.False, "the holstered gun's reload clip is stopped (a.Tick would return early, so check it directly)");
            Assert.That(a.Handling.Clip == null || a.Handling.Clip.kind != HandlingClipKind.ReloadPartial, Is.True);
            for (int i = 0; i < 120; i++) { a.Tick(H, false, false, cues); b.Tick(H, false, false, cues); }
            for (int i = afterSwap; i < cues.Count; i++)
                Assert.That(cues[i].clip == HandlingClipKind.ReloadPartial, Is.False, "no reload sound after the swap");
            Assert.That(a.Ammo, Is.EqualTo(2), "the cancelled reload moved no ammo");
            Assert.That(cues.FindAll(c => c.kind == GunCueKind.ClipStarted && c.clip == HandlingClipKind.Equip).Count, Is.EqualTo(2));
        }

        [Test]
        public void InspectPlaysOnlyWhenIdleAndFiringCancelsIt()
        {
            var r = new Rig(TestGun(), waitForEquip: false);
            Assert.That(r.gun.Inspect(r.cues), Is.False, "not while equipping");
            r.Idle(0.6f);
            Assert.That(r.gun.Inspect(r.cues), Is.True);
            Assert.That(r.gun.Inspecting, Is.True);
            r.Idle(0.2f);
            r.Press();
            Assert.That(r.gun.Inspecting, Is.False);
            Assert.That(r.Count(Shot), Is.EqualTo(1));
            Assert.That(r.Count(Event("spin")), Is.EqualTo(0), "the inspect's later event never fires");
        }

        [Test]
        public void AnIncompleteGunIsRefusedWithItsProblem()
        {
            var d = TestGun();
            d.clips[(int)HandlingClipKind.Inspect] = null;
            var e = Assert.Throws<ArgumentException>(() => new SkylineGun(d));
            Assert.That(e.Message, Does.Contain("Inspect"));
        }

        [Test]
        public void TheGunKeepsItsOwnCopyOfTheNumbers()
        {
            var d = TestGun();
            var g = new SkylineGun(d);
            Assert.That(d.weapon.autoReloadOnEmpty, Is.True, "the shared def is untouched");
            Assert.That(g.State.Def, Is.Not.SameAs(d.weapon));
            Assert.That(g.State.Def.infiniteReserve, Is.True);
        }

        [Test]
        public void FalloffIsFullUpCloseAndFloorsFarAway()
        {
            var d = new GunDef { falloffStart = 10f, falloffEnd = 30f, falloffMinScale = 0.4f };
            Assert.That(d.FalloffScale(5f), Is.EqualTo(1f));
            Assert.That(d.FalloffScale(20f), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(d.FalloffScale(100f), Is.EqualTo(0.4f));
            Assert.That(d.FalloffScale(float.NaN), Is.EqualTo(1f));
        }

        [Test]
        public void SeveralShotsInOneTickEachPlayTheirShotSound()
        {
            var d = TestGun();
            d.weapon = new WeaponDef { magazineSize = 30, fireMode = FireMode.Auto, fireInterval = 0.05f };
            var r = new Rig(d);
            r.Press();   // engages the trigger; the held auto fire then runs from here
            var before = r.cues.Count;
            r.gun.Tick(0.25f, true, false, r.cues);   // one long held tick
            int shots = 0, sounds = 0, starts = 0;
            for (int i = before; i < r.cues.Count; i++)
            {
                if (r.cues[i].kind == GunCueKind.Shot) shots++;
                if (r.cues[i].kind == GunCueKind.Event && r.cues[i].ev.id == "shot") sounds++;
                if (r.cues[i].kind == GunCueKind.ClipStarted && r.cues[i].clip == HandlingClipKind.Fire) starts++;
            }
            Assert.That(shots, Is.GreaterThanOrEqualTo(4), "a long tick fires several shots");
            Assert.That(starts, Is.EqualTo(shots));
            Assert.That(sounds, Is.EqualTo(shots), "one shot sound per shot, not one per tick");
        }

        [Test]
        public void TheLoadoutsFirstEquipIsNotASwapButLaterOnesAre()
        {
            var cues = new List<GunCue>();
            var loadout = new Loadout(new SkylineGun(TestGun()), new SkylineGun(TestGun()), cues);
            var log = new DecisionLog(8);
            int added = 0;
            foreach (var c in cues) added += GunDecisions.Append(log, c, 0, 0f, Vector3.Zero, DecisionContext.None, 0f, 3);
            Assert.That(added, Is.EqualTo(0), "the first equip when the loadout is built is not a decision");
            Assert.That(log.Count, Is.EqualTo(0));
            int from = cues.Count;
            loadout.Swap(cues);
            for (int i = from; i < cues.Count; i++) added += GunDecisions.Append(log, cues[i], 1, 1f, Vector3.Zero, DecisionContext.None, 0f, 3);
            Assert.That(added, Is.EqualTo(1), "swapping is a decision");
            Assert.That(log[0].kind, Is.EqualTo(DecisionKind.Swap));
        }

        [Test]
        public void CuesBecomeDecisions()
        {
            var log = new DecisionLog(16);
            var shot = new GunCue { kind = GunCueKind.Shot };
            var reload = new GunCue { kind = GunCueKind.ClipStarted, clip = HandlingClipKind.ReloadEmpty };
            var equip = new GunCue { kind = GunCueKind.ClipStarted, clip = HandlingClipKind.Equip };
            var sound = new GunCue { kind = GunCueKind.Event, clip = HandlingClipKind.Fire };
            Assert.That(GunDecisions.Append(log, shot, 1, 2f, Vector3.One, DecisionContext.InCombat, 90f, 4), Is.EqualTo(1));
            Assert.That(GunDecisions.Append(log, reload, 1, 3f, Vector3.One, DecisionContext.None, 0f, 0), Is.EqualTo(1));
            Assert.That(GunDecisions.Append(log, equip, 0, 4f, Vector3.One, DecisionContext.None, 0f, 8), Is.EqualTo(1));
            Assert.That(GunDecisions.Append(log, sound, 0, 5f, Vector3.One, DecisionContext.None, 0f, 8), Is.EqualTo(0));
            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(log[0].kind, Is.EqualTo(DecisionKind.Fire));
            Assert.That(log[0].subject, Is.EqualTo(1));
            Assert.That(log[0].ammo, Is.EqualTo(4));
            Assert.That(log[1].kind, Is.EqualTo(DecisionKind.Reload));
            Assert.That(log[2].kind, Is.EqualTo(DecisionKind.Swap));
        }
    }
}
