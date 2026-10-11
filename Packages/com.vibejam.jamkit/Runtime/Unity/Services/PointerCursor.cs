using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// The mouse cursor follows the device in use, everywhere (title, menus, Settings, the presenter panel,
    /// gameplay, the argument battle): the moment a gamepad is used it hides, and it comes back when the mouse
    /// really moves, clicks or scrolls (<see cref="InputService.PadMode"/>). In pad mode the shell's UI root also
    /// carries the <see cref="PadClass"/> class, which turns off the :hover looks (Shell.uss, the end of the file),
    /// so the hidden cursor's spot doesn't look like a second focus.
    ///
    /// A scene that locks the cursor for mouse-look (CursorLockMode.Locked) owns it: nothing here changes a
    /// locked cursor, and none of it unlocks one. GameRoot adds this; it runs after every scene's scripts (the
    /// execution order), so a scene that shows the cursor when it unlocks (a pause menu opened with Start) is
    /// hidden again the same frame while the pad is in use.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PointerCursor : MonoBehaviour
    {
        /// <summary>The class on the shell's UI root while the pad is in use.</summary>
        public const string PadClass = "jk-pad";

        static PointerCursor _instance;
        bool _hiddenByUs, _classOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        /// <summary>The cursor is hidden because the pad is in use (and the cursor isn't locked by the scene).</summary>
        public static bool HiddenForPad => _instance != null && _instance._hiddenByUs;

        void Awake() => _instance = this;

        void OnDestroy()
        {
            if (_instance != this) return;
            if (_hiddenByUs && Cursor.lockState != CursorLockMode.Locked) Cursor.visible = true;
            _instance = null;
        }

        void LateUpdate()
        {
            if (!GameRoot.Exists) return;
            bool pad = GameRoot.Input.PadMode;

            var root = GameRoot.Ui != null ? GameRoot.Ui.Root : null;
            if (root != null && _classOn != pad)
            {
                _classOn = pad;
                root.EnableInClassList(PadClass, pad);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // The scene's mouse-look owns it (and shows it again itself when it unlocks).
                _hiddenByUs = false;
                return;
            }
            if (pad)
            {
                if (Cursor.visible) Cursor.visible = false;
                _hiddenByUs = true;
            }
            else if (_hiddenByUs)
            {
                Cursor.visible = true;
                _hiddenByUs = false;
            }
        }
    }
}
