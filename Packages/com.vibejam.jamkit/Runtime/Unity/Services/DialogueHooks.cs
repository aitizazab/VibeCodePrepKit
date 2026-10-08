using System;
using System.Collections.Generic;
using JamKit.Dialogue;

namespace JamKit.Shell
{
    public enum DialogueLineKind { Narration, Dialogue, Thought }

    /// <summary>The line on screen: who says it (display name), what kind of line it is, the text as written.</summary>
    public struct DialogueLineInfo
    {
        public string speaker;
        /// <summary>The name as written before the colon ("iris: ..."), or the #speaker value; "" for narration.</summary>
        public string speakerId;
        /// <summary>The text after the speaker prefix, with its inline markup (&lt;b&gt;, &lt;pause&gt;...).</summary>
        public string text;
        public DialogueLineKind kind;
        /// <summary>From #auto: the line goes on by itself after this many seconds (0 = waits for the player).</summary>
        public float autoSeconds;
        /// <summary>The knot the line is in ("" if unknown).</summary>
        public string knot;
    }

    /// <summary>
    /// Where the dialogue is drawn. The shell's own box (<see cref="DialogueView"/>) is the default; a
    /// template can draw it differently (the story template's text box and full-screen text) with
    /// <c>GameRoot.Dialogue.UseView(view)</c>.
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>The service's choice handler: call it with the choice index when one is picked.</summary>
        void SetChoiceHandler(Action<int> onChoice);
        void Open();
        void Close();
        void SetSpeaker(string name);
        /// <summary>A new line starts (before any of it is shown).</summary>
        void BeginLine(DialogueLineInfo line, RichLine rich);
        /// <summary>The first <paramref name="visible"/> characters of the line are shown.</summary>
        void SetTyped(RichLine rich, int visible);
        void ShowChoices(IList<string> texts);
        void HideChoices();
        void SetContinueHint(bool shown);
        /// <summary>The shell's #portrait tag. Return false when the character has no portrait.</summary>
        bool ApplyPortrait(PortraitCommand cmd, Portrait portrait, string speaker);
    }

    /// <summary>
    /// A template's say in how a story runs (the story template sets one): it sees every tag first
    /// and can claim it, can hold a line back while its own transition plays, decides what happens
    /// when ink runs out (finish, or wait in a hotspot scene), and knows which lines were read before.
    /// </summary>
    public interface IDialoguePresenter
    {
        /// <summary>Called for every tag, before the shell's own handling. Return true to claim it.</summary>
        bool HandleTag(DialogueTag tag);
        /// <summary>While true the next line doesn't start typing (a background is still fading in...).</summary>
        bool IsBusy { get; }
        /// <summary>The player pressed to go on during <see cref="IsBusy"/>: finish transitions now.</summary>
        void Hurry();
        /// <summary>Called before a line is shown; may change its speaker, kind or text.</summary>
        void OnLine(ref DialogueLineInfo line);
        /// <summary>ink has no more content and no choices. Return true to keep the story waiting
        /// (<see cref="DialogueService.IsParked"/>) instead of ending it.</summary>
        bool OnOutOfContent();
        /// <summary>Skip mode stops at lines that return false (unless Settings.skipUnread).</summary>
        bool IsRead(DialogueLineInfo line);
        /// <summary>False while the presenter wants the advance keys for itself (a menu of its own is open,
        /// the pointer is over its buttons).</summary>
        bool AllowAdvanceInput();
    }
}
