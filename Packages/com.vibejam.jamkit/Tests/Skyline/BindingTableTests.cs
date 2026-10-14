using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class BindingTableTests
    {
        [Test]
        public void EveryActionHasADefaultOnBothDevices()
        {
            var t = new BindingTable();
            foreach (var a in BindingTable.AllActions)
            {
                Assert.That(t.Get(BindDevice.KeyboardMouse, a), Is.Not.Empty, a.ToString());
                Assert.That(t.Get(BindDevice.Pad, a), Is.Not.Empty, a.ToString());
                foreach (var p in BindingTable.Split(t.Get(BindDevice.KeyboardMouse, a)))
                    Assert.That(BindingTable.IsValidPath(BindDevice.KeyboardMouse, p), Is.True, p);
                foreach (var p in BindingTable.Split(t.Get(BindDevice.Pad, a)))
                    Assert.That(BindingTable.IsValidPath(BindDevice.Pad, p), Is.True, p);
            }
            Assert.That(BindingTable.AllActions.Length, Is.EqualTo(BindingTable.ActionCount));
        }

        [Test]
        public void TheDefaultsAreTheCreatorsScheme()
        {
            var t = new BindingTable();
            Assert.That(t.Get(BindDevice.KeyboardMouse, BindAction.Aim), Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(t.Get(BindDevice.KeyboardMouse, BindAction.Pause), Is.EqualTo("<Keyboard>/escape"));
            Assert.That(BindingTable.Uses(t.Get(BindDevice.KeyboardMouse, BindAction.Crouch), "<Keyboard>/leftCtrl"), Is.True);
            Assert.That(BindingTable.Uses(t.Get(BindDevice.KeyboardMouse, BindAction.Crouch), "<Keyboard>/c"), Is.True);
            Assert.That(t.Get(BindDevice.Pad, BindAction.Crouch), Is.EqualTo("<Gamepad>/rightStickPress"));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Aim), Is.EqualTo("<Gamepad>/leftTrigger"));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Fire), Is.EqualTo("<Gamepad>/rightTrigger"));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Pause), Is.EqualTo("<Gamepad>/start"));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Sprint), Is.EqualTo("<Gamepad>/leftStickPress"));
            // B / Circle is free by default
            foreach (var a in BindingTable.AllActions)
                Assert.That(BindingTable.Uses(t.Get(BindDevice.Pad, a), "<Gamepad>/buttonEast"), Is.False, a.ToString());
        }

        [Test]
        public void NoControlIsUsedTwiceByDefault()
        {
            var t = new BindingTable();
            for (int d = 0; d < 2; d++)
            {
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var a in BindingTable.AllActions)
                    foreach (var p in BindingTable.Split(t.Get((BindDevice)d, a)))
                        Assert.That(seen.Add(p), Is.True, "used twice: " + p);
            }
        }

        [Test]
        public void RebindingTakesTheNewControl()
        {
            var t = new BindingTable();
            var r = t.Set(BindDevice.Pad, BindAction.Crouch, "<Gamepad>/buttonEast");
            Assert.That(r, Is.EqualTo(BindResult.Set));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Crouch), Is.EqualTo("<Gamepad>/buttonEast"));
            Assert.That(t.Get(BindDevice.KeyboardMouse, BindAction.Crouch), Does.Contain("leftCtrl"));   // the other device is untouched
        }

        [Test]
        public void BindingAControlAnotherActionUsesSwapsTheTwo()
        {
            var t = new BindingTable();
            string jump = t.Get(BindDevice.Pad, BindAction.Jump);
            string crouch = t.Get(BindDevice.Pad, BindAction.Crouch);
            var r = t.Set(BindDevice.Pad, BindAction.Crouch, jump);
            Assert.That(r, Is.EqualTo(BindResult.Swapped));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Crouch), Is.EqualTo(jump));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Jump), Is.EqualTo(crouch));
        }

        [Test]
        public void ASwapKeepsTheOtherAlternativeOfAMultiControlBinding()
        {
            var t = new BindingTable();
            // Jump takes C, which is one of Crouch's two keys: Crouch keeps Left Ctrl and gets Jump's old Space for C.
            var r = t.Set(BindDevice.KeyboardMouse, BindAction.Jump, "<Keyboard>/c");
            Assert.That(r, Is.EqualTo(BindResult.Swapped));
            Assert.That(t.Get(BindDevice.KeyboardMouse, BindAction.Jump), Is.EqualTo("<Keyboard>/c"));
            Assert.That(BindingTable.Uses(t.Get(BindDevice.KeyboardMouse, BindAction.Crouch), "<Keyboard>/leftCtrl"), Is.True);
            Assert.That(BindingTable.Uses(t.Get(BindDevice.KeyboardMouse, BindAction.Crouch), "<Keyboard>/space"), Is.True);
            Assert.That(BindingTable.Uses(t.Get(BindDevice.KeyboardMouse, BindAction.Crouch), "<Keyboard>/c"), Is.False);
        }

        [Test]
        public void AControlOfTheOtherDeviceIsRefused()
        {
            var t = new BindingTable();
            Assert.That(t.Set(BindDevice.Pad, BindAction.Jump, "<Keyboard>/space"), Is.EqualTo(BindResult.Rejected));
            Assert.That(t.Set(BindDevice.KeyboardMouse, BindAction.Jump, "<Gamepad>/buttonSouth"), Is.EqualTo(BindResult.Rejected));
            Assert.That(t.Set(BindDevice.Pad, BindAction.Jump, ""), Is.EqualTo(BindResult.Rejected));
            Assert.That(t.Set(BindDevice.Pad, BindAction.Jump, "nonsense"), Is.EqualTo(BindResult.Rejected));
            Assert.That(t.Get(BindDevice.Pad, BindAction.Jump), Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void TheMouseBelongsToTheKeyboardSide()
        {
            var t = new BindingTable();
            Assert.That(t.Set(BindDevice.KeyboardMouse, BindAction.Aim, "<Mouse>/middleButton"), Is.EqualTo(BindResult.Set));
            Assert.That(t.Get(BindDevice.KeyboardMouse, BindAction.Aim), Is.EqualTo("<Mouse>/middleButton"));
        }

        [Test]
        public void RebindingToTheSameControlChangesNothing()
        {
            var t = new BindingTable();
            string before = t.Serialize();
            Assert.That(t.Set(BindDevice.Pad, BindAction.Jump, "<Gamepad>/buttonSouth"), Is.EqualTo(BindResult.Set));
            Assert.That(t.Serialize(), Is.EqualTo(before));
        }

        [Test]
        public void ResetToDefaultsRestoresTheTable()
        {
            var t = new BindingTable();
            string fresh = t.Serialize();
            t.Set(BindDevice.Pad, BindAction.Crouch, "<Gamepad>/buttonEast");
            t.Set(BindDevice.KeyboardMouse, BindAction.Aim, "<Keyboard>/z");
            t.Clear(BindDevice.Pad, BindAction.Inspect);
            Assert.That(t.Serialize(), Is.Not.EqualTo(fresh));
            t.ResetToDefaults();
            Assert.That(t.Serialize(), Is.EqualTo(fresh));
        }

        [Test]
        public void SavingAndLoadingRoundTrips()
        {
            var t = new BindingTable();
            t.Set(BindDevice.Pad, BindAction.Crouch, "<Gamepad>/buttonEast");
            t.Set(BindDevice.KeyboardMouse, BindAction.Aim, "<Keyboard>/z");
            t.Clear(BindDevice.Pad, BindAction.Inspect);
            var u = BindingTable.Deserialize(t.Serialize());
            foreach (var a in BindingTable.AllActions)
            {
                Assert.That(u.Get(BindDevice.Pad, a), Is.EqualTo(t.Get(BindDevice.Pad, a)), a.ToString());
                Assert.That(u.Get(BindDevice.KeyboardMouse, a), Is.EqualTo(t.Get(BindDevice.KeyboardMouse, a)), a.ToString());
            }
            Assert.That(u.Get(BindDevice.Pad, BindAction.Inspect), Is.Empty);
        }

        [Test]
        public void ADamagedSaveFallsBackToDefaults()
        {
            var fresh = new BindingTable().Serialize();
            Assert.That(BindingTable.Deserialize(null).Serialize(), Is.EqualTo(fresh));
            Assert.That(BindingTable.Deserialize("").Serialize(), Is.EqualTo(fresh));
            Assert.That(BindingTable.Deserialize("garbage\n==\nkb.Nope=<Keyboard>/x\n").Serialize(), Is.EqualTo(fresh));
            // a keyboard control saved against a pad slot is dropped, the rest still loads
            var t = BindingTable.Deserialize("pad.Jump=<Keyboard>/space\nkb.Reload=<Keyboard>/t\n");
            Assert.That(t.Get(BindDevice.Pad, BindAction.Jump), Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(t.Get(BindDevice.KeyboardMouse, BindAction.Reload), Is.EqualTo("<Keyboard>/t"));
        }

        [Test]
        public void NamesAreReadable()
        {
            Assert.That(BindingNames.Display("<Mouse>/rightButton"), Is.EqualTo("Right Mouse"));
            Assert.That(BindingNames.Display("<Gamepad>/rightStickPress"), Is.EqualTo("R3"));
            Assert.That(BindingNames.Display("<Gamepad>/buttonSouth"), Is.EqualTo("A / Cross"));
            Assert.That(BindingNames.Display("<Keyboard>/space"), Is.EqualTo("Space"));
            Assert.That(BindingNames.Display("<Keyboard>/r"), Is.EqualTo("R"));
            Assert.That(BindingNames.Display("<Keyboard>/leftCtrl|<Keyboard>/c"), Is.EqualTo("Left Ctrl / C"));
            Assert.That(BindingNames.Display("<Keyboard>/escape"), Is.EqualTo("Esc"));
            Assert.That(BindingNames.Display(""), Is.EqualTo("Unbound"));
            Assert.That(BindingNames.Display("<Gamepad>/dpad/up"), Is.EqualTo("D-pad Up"));
        }

        [Test]
        public void DeviceOfPaths()
        {
            BindDevice d;
            Assert.That(BindingTable.TryDeviceOf("<Keyboard>/a", out d), Is.True);
            Assert.That(d, Is.EqualTo(BindDevice.KeyboardMouse));
            Assert.That(BindingTable.TryDeviceOf("<Mouse>/leftButton", out d), Is.True);
            Assert.That(d, Is.EqualTo(BindDevice.KeyboardMouse));
            Assert.That(BindingTable.TryDeviceOf("<Gamepad>/start", out d), Is.True);
            Assert.That(d, Is.EqualTo(BindDevice.Pad));
            Assert.That(BindingTable.TryDeviceOf("<Touchscreen>/press", out d), Is.False);
            Assert.That(BindingTable.TryDeviceOf(null, out d), Is.False);
        }
    }
}
