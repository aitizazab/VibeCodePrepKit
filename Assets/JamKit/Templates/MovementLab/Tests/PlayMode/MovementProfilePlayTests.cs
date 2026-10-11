using System.Collections;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The Movement Lab's movement-profile toggle (F7 / LB + d-pad right): the daytime profile turns the double
    /// jump, the wallrun and the slide off on the real controller, the HUD says which profile is on, and toggling back
    /// gives the dream's movement.</summary>
    public class MovementProfilePlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (MovementLabGame.Current != null && MovementLabGame.Current.player.Profile != MoveProfileKind.Dream)
                MovementLabGame.Current.player.SetProfile(MoveProfileKind.Dream);
            PresenterMode.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheDaytimeToggleTurnsTheAbilitiesOffAndBack()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True);
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && MovementLabGame.Current != null && MovementLabGame.Current.player.Sim != null, "the Movement Lab");
            var body = MovementLabGame.Current.player;
            Assert.That(body.Profile, Is.EqualTo(MoveProfileKind.Dream), "the lab starts in the dream");
            Assert.That(body.Sim.Profile, Is.Null, "the dream allows everything");
            Assert.That(body.Tuning, Is.SameAs(MovementTuning.Shared), "the shared tuning");

            MovementProfileSwitch.Toggle(body);
            Assert.That(body.Profile, Is.EqualTo(MoveProfileKind.Daytime));
            var p = body.Sim.Profile;
            Assert.That(p, Is.Not.Null);
            Assert.That(p.doubleJump || p.wallrun || p.slide, Is.False, "daytime: no double jump, wallrun or slide");
            Assert.That(p.airStrafeScale, Is.EqualTo(body.Tuning.daytime.airStrafeScale), "the shared tuning's daytime values");
            Assert.That(p.jumpFatigue, Is.EqualTo(body.Tuning.daytime.jumpFatigue));
            yield return WaitFor(() => MovementLabGame.Current.Hud.ExtraText.Contains("Movement: Daytime"), "the HUD showing the daytime profile", 2f);

            // A crouch at running speed only crouches now.
            body.Teleport(body.transform.position, body.Yaw, body.transform.forward * 9.5f);
            yield return null;
            Assert.That(body.Sim.Mode, Is.Not.EqualTo(MoveMode.Slide));

            MovementProfileSwitch.Toggle(body);
            Assert.That(body.Profile, Is.EqualTo(MoveProfileKind.Dream));
            Assert.That(body.Sim.Profile, Is.Null);
        }
    }
}
