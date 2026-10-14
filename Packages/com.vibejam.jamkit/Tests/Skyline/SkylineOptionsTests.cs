using System;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class SkylineOptionsTests
    {
        [Test]
        public void DefaultsAreFullVolumeAndNormalLook()
        {
            var o = new SkylineOptions();
            Assert.That(o.master, Is.EqualTo(1f));
            Assert.That(o.music, Is.EqualTo(1f));
            Assert.That(o.effects, Is.EqualTo(1f));
            Assert.That(o.voices, Is.EqualTo(1f));
            Assert.That(o.mouseSensitivity, Is.EqualTo(1f));
            Assert.That(o.padSensitivity, Is.EqualTo(1f));
            Assert.That(o.invertY, Is.False);
            Assert.That(o.aimAssist, Is.True);
            Assert.That(o.fieldOfView, Is.EqualTo(SkylineOptions.DefaultFieldOfView), "the default view is what the game had before the slider");
        }

        [Test]
        public void TheFieldOfViewIsSavedClampedAndSurvivesAnOldSave()
        {
            var o = new SkylineOptions { fieldOfView = 95f };
            Assert.That(SkylineOptions.Deserialize(o.Serialize()).fieldOfView, Is.EqualTo(95f).Within(1e-4f));
            o.fieldOfView = 300f;
            o.Clamp();
            Assert.That(o.fieldOfView, Is.EqualTo(SkylineOptions.MaxFieldOfView));
            o.fieldOfView = float.NaN;
            o.Clamp();
            Assert.That(o.fieldOfView, Is.EqualTo(SkylineOptions.DefaultFieldOfView));
            Assert.That(SkylineOptions.Deserialize("skyline-options 1\nmaster=0.5\n").fieldOfView, Is.EqualTo(SkylineOptions.DefaultFieldOfView), "a save from before the slider");
            Assert.That(SkylineOptions.Deserialize("fov=20\n").fieldOfView, Is.EqualTo(SkylineOptions.MinFieldOfView));
        }

        [Test]
        public void TheSubtitleSettingsDefaultToDialogueAtMediumAndRoundTrip()
        {
            var d = new SkylineOptions();
            Assert.That(d.subtitleMode, Is.EqualTo(SubtitleMode.Dialogue));
            Assert.That(d.subtitleSize, Is.EqualTo(SubtitleSize.Medium));
            foreach (SubtitleMode m in Enum.GetValues(typeof(SubtitleMode)))
                foreach (SubtitleSize s in Enum.GetValues(typeof(SubtitleSize)))
                {
                    var u = SkylineOptions.Deserialize(new SkylineOptions { subtitleMode = m, subtitleSize = s }.Serialize());
                    Assert.That(u.subtitleMode, Is.EqualTo(m));
                    Assert.That(u.subtitleSize, Is.EqualTo(s));
                }
        }

        [Test]
        public void ABadOrMissingSubtitleSettingFallsBackToTheDefault()
        {
            var o = SkylineOptions.Deserialize("subtitles=9\nsubtitleSize=-4\n");
            Assert.That(o.subtitleMode, Is.EqualTo(SubtitleMode.Dialogue));
            Assert.That(o.subtitleSize, Is.EqualTo(SubtitleSize.Medium));
            o = SkylineOptions.Deserialize("subtitles=abc\n");
            Assert.That(o.subtitleMode, Is.EqualTo(SubtitleMode.Dialogue));
            Assert.That(SkylineOptions.Deserialize("skyline-options 1\nmaster=0.5\n").subtitleMode, Is.EqualTo(SubtitleMode.Dialogue), "a save from before the setting");
            Assert.That(SkylineOptions.Deserialize("subtitles=0\n").subtitleMode, Is.EqualTo(SubtitleMode.Off));
        }

        [Test]
        public void SavingAndLoadingRoundTrips()
        {
            var o = new SkylineOptions { master = 0.5f, music = 0.25f, effects = 0.75f, voices = 0.1f, mouseSensitivity = 1.5f, padSensitivity = 0.5f, invertY = true, aimAssist = false };
            var u = SkylineOptions.Deserialize(o.Serialize());
            Assert.That(u.master, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(u.music, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(u.effects, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(u.voices, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(u.mouseSensitivity, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(u.padSensitivity, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(u.invertY, Is.True);
            Assert.That(u.aimAssist, Is.False);
        }

        [Test]
        public void ValuesAreClampedToTheirRanges()
        {
            var o = SkylineOptions.Deserialize("master=7\nmusic=-3\nmouse=100\npad=0\n");
            Assert.That(o.master, Is.EqualTo(1f));
            Assert.That(o.music, Is.EqualTo(0f));
            Assert.That(o.mouseSensitivity, Is.EqualTo(SkylineOptions.MaxSensitivity));
            Assert.That(o.padSensitivity, Is.EqualTo(SkylineOptions.MinSensitivity));
            var n = new SkylineOptions { master = float.NaN, mouseSensitivity = float.PositiveInfinity };
            n.Clamp();
            Assert.That(n.master, Is.EqualTo(1f));
            Assert.That(n.mouseSensitivity, Is.EqualTo(1f));
        }

        [Test]
        public void ADamagedSaveGivesDefaults()
        {
            var o = SkylineOptions.Deserialize("nonsense\nmaster=abc\n=\nmouse\n");
            Assert.That(o.Serialize(), Is.EqualTo(new SkylineOptions().Serialize()));
            Assert.That(SkylineOptions.Deserialize(null).Serialize(), Is.EqualTo(new SkylineOptions().Serialize()));
        }
    }

    public class AimDownSightsTests
    {
        [Test]
        public void EasesInOverAFifteenthOfASecondsAndOutOverTwelve()
        {
            float a = 0f;
            for (int i = 0; i < 15; i++) a = AimDownSights.Step(a, true, 0.01f);
            Assert.That(a, Is.EqualTo(1f).Within(1e-4f));
            a = AimDownSights.Step(0f, true, 0.075f);
            Assert.That(a, Is.EqualTo(0.5f).Within(1e-4f));
            a = 1f;
            for (int i = 0; i < 12; i++) a = AimDownSights.Step(a, false, 0.01f);
            Assert.That(a, Is.EqualTo(0f).Within(1e-4f));
            a = AimDownSights.Step(1f, false, 0.06f);
            Assert.That(a, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void TheAmountNeverLeavesZeroToOne()
        {
            Assert.That(AimDownSights.Step(0.9f, true, 5f), Is.EqualTo(1f));
            Assert.That(AimDownSights.Step(0.1f, false, 5f), Is.EqualTo(0f));
            Assert.That(AimDownSights.Step(0.4f, true, 0f), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(AimDownSights.Step(2f, true, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void TheEaseIsSmoothAndMeetsItsEnds()
        {
            Assert.That(AimDownSights.Ease(0f), Is.EqualTo(0f));
            Assert.That(AimDownSights.Ease(1f), Is.EqualTo(1f));
            Assert.That(AimDownSights.Ease(0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(AimDownSights.Ease(0.25f), Is.LessThan(0.25f));
        }

        [Test]
        public void ScalesRunFromOneAtTheHipToTheGunsNumberFullyAimed()
        {
            Assert.That(AimDownSights.Scale(0.75f, 0f), Is.EqualTo(1f));
            Assert.That(AimDownSights.Scale(0.75f, 1f), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(AimDownSights.Scale(0.75f, 0.5f), Is.EqualTo(0.875f).Within(1e-6f));
            Assert.That(AimDownSights.Scale(1f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void NoAimWhileEquippingInspectingOrReloading()
        {
            Assert.That(AimDownSights.CanAim(true, false, false, false), Is.True);
            Assert.That(AimDownSights.CanAim(true, true, false, false), Is.False);
            Assert.That(AimDownSights.CanAim(true, false, true, false), Is.False);
            Assert.That(AimDownSights.CanAim(true, false, false, true), Is.False);
            Assert.That(AimDownSights.CanAim(false, false, false, false), Is.False);
        }

        [Test]
        public void TheWritsSightsLineUpWithTheScreenCentre()
        {
            // the window (0, 0.088, -0.079) and the bead (0, 0.088, 0.202) from the Writ's model, the window 13 cm from the eye
            var rear = new Vector3(0f, 0.088f, -0.079f);
            var front = new Vector3(0f, 0.088f, 0.202f);
            Vector3 pos;
            Quaternion rot;
            AimDownSights.SolveSights(rear, front, 0.13f, out pos, out rot);
            // the horizontal sight line needs no turn, and the gun sits where the inspect's sight check puts it
            Assert.That(rot.Length(), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Math.Abs(rot.X) + Math.Abs(rot.Y) + Math.Abs(rot.Z), Is.LessThan(1e-4f));
            Assert.That(pos.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(pos.Y, Is.EqualTo(-0.088f).Within(1e-5f));
            Assert.That(pos.Z, Is.EqualTo(0.209f).Within(1e-5f));
            // and that is the Writ's rest plus the sight-check pose (SkylineGuns.WritSightCheck), which the inspect already uses
            var rest = new Vector3(0.2f, -0.13f, 0.5f);
            var check = SkylineGuns.WritSightCheck;
            Assert.That(rest.X + check.X, Is.EqualTo(pos.X).Within(1e-4f));
            Assert.That(rest.Y + check.Y, Is.EqualTo(pos.Y).Within(1e-4f));
            Assert.That(rest.Z + check.Z, Is.EqualTo(pos.Z).Within(1e-4f));
        }

        [Test]
        public void ATiltedSightLineIsTurnedOntoTheCameraAxis()
        {
            // the Breacher's rear notch and the top of its front sight, from its model
            var rear = new Vector3(0f, 0.1f, 0.025f);
            var front = new Vector3(0f, 0.105f, 0.222f);
            Vector3 pos;
            Quaternion rot;
            AimDownSights.SolveSights(rear, front, 0.2f, out pos, out rot);
            var r = Vector3.Transform(rear, rot) + pos;
            var f = Vector3.Transform(front, rot) + pos;
            Assert.That(r.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(r.Y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(r.Z, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(f.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(f.Y, Is.EqualTo(0f).Within(1e-4f));   // the front sight's top is on the centre line too
            Assert.That(f.Z, Is.GreaterThan(r.Z));
        }

        [Test]
        public void ASightLineTurnedInAnyDirectionLandsOnTheAxis()
        {
            var rear = new Vector3(0.01f, -0.02f, 0.03f);
            var front = new Vector3(0.05f, 0.04f, 0.4f);
            Vector3 pos;
            Quaternion rot;
            AimDownSights.SolveSights(rear, front, 0.15f, out pos, out rot);
            var r = Vector3.Transform(rear, rot) + pos;
            var f = Vector3.Transform(front, rot) + pos;
            Assert.That(r.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(r.Y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(f.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(f.Y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(f.Z, Is.GreaterThan(r.Z));
        }

        [Test]
        public void WithoutSightsTheMuzzleLineSitsJustBelowTheCentre()
        {
            Vector3 pos;
            Quaternion rot;
            AimDownSights.SolveMuzzle(new Vector3(0f, 0.045f, 0.23f), 0.02f, 0.2f, out pos, out rot);
            var m = Vector3.Transform(new Vector3(0f, 0.045f, 0.23f), rot) + pos;
            Assert.That(m.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(m.Y, Is.EqualTo(-0.02f).Within(1e-6f));
        }

        [Test]
        public void PlainSprintingNeverWidensTheView()
        {
            // the creator, 2026-10-14: the view's kick is for much faster movement than a sprint
            float sprint = new JamKit.Movement.MoveDef().sprintSpeed;
            Assert.That(SkylineViewFeel.SprintKickFov, Is.EqualTo(0f), "starting a sprint must not punch the view out");
            Assert.That(SkylineViewFeel.SpeedFovFrom, Is.GreaterThanOrEqualTo(sprint + 3f), "the widening starts a good margin above sprint speed");
            Assert.That(SkylineViewFeel.SpeedFovFull, Is.GreaterThan(SkylineViewFeel.SpeedFovFrom + 8f), "and eases in over a wide range of speed");
            Assert.That(SkylineViewFeel.SpeedFovRate, Is.LessThan(SkylineViewFeel.OldSpeedFovRate));
            Assert.That(JamKit.Movement.MoveMath.SmoothStep(SkylineViewFeel.SpeedFovFrom, SkylineViewFeel.SpeedFovFull, sprint), Is.EqualTo(0f));
        }
    }
}
