using System;
using System.Collections.Generic;

namespace JamKit.Narrative
{
    /// <summary>A character on stage: who, which face, where (0..1 across the stage), mirrored or not.</summary>
    [Serializable]
    public class StageCharacter
    {
        public string id = "";
        /// <summary>Empty = the character's default expression.</summary>
        public string expression = "";
        public float x = 0.5f;
        public bool flip;

        public StageCharacter Clone() => (StageCharacter)MemberwiseClone();
    }

    /// <summary>
    /// What the stage shows, as data: background, CG, characters, music, ambience, text mode and the
    /// camera effects that last (zoom, blur, filter, fade). Story tags change it through
    /// <see cref="Apply"/>; the Unity stage draws it (animating the change), and a save stores a copy
    /// so loading puts the exact same picture back. Momentary effects (shake, flash, glitch, sounds)
    /// aren't part of it.
    /// </summary>
    [Serializable]
    public class StageState
    {
        public string background = "";
        public string cg = "";
        public List<StageCharacter> characters = new List<StageCharacter>();
        public string music = "";
        public string ambience = "";
        /// <summary>Full-screen text (NVL) instead of the text box (ADV).</summary>
        public bool nvl;
        public float zoom = 1f;
        public float zoomX = 0.5f;
        public float zoomY = 0.5f;
        public float blur;
        /// <summary>"" or none, sepia, gray, invert, night, dream.</summary>
        public string filter = "";
        public float filterAmount = 1f;
        public bool fadedOut;
        public string fadeColor = "black";

        /// <summary>Default places tried, in order, for a character shown without one.</summary>
        static readonly float[] AutoPlaces = { 0.5f, 0.27f, 0.73f, 0.12f, 0.88f };

        public StageCharacter Find(string id)
        {
            if (characters == null || string.IsNullOrEmpty(id)) return null;
            foreach (var c in characters)
                if (c != null && string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        public bool IsShown(string id) => Find(id) != null;

        /// <summary>Changes the state for a stage command. Returns false for commands that don't touch the
        /// lasting state (sounds that play once, shakes, text commands) or that change nothing.</summary>
        public bool Apply(StoryCommand cmd)
        {
            if (characters == null) characters = new List<StageCharacter>();
            switch (cmd.kind)
            {
                case StoryCommandKind.Background:
                    if (background == cmd.id) return false;
                    background = cmd.id;
                    return true;
                case StoryCommandKind.Scene:
                    background = cmd.id;
                    characters.Clear();
                    cg = "";
                    return true;
                case StoryCommandKind.Show:
                {
                    var c = Find(cmd.id);
                    if (c == null)
                    {
                        c = new StageCharacter { id = cmd.id, x = cmd.hasX ? cmd.x : FreePlace() };
                        characters.Add(c);
                    }
                    else if (cmd.hasX) c.x = cmd.x;
                    if (!string.IsNullOrEmpty(cmd.expression)) c.expression = cmd.expression;
                    if (cmd.hasFlip) c.flip = cmd.flip;
                    return true;
                }
                case StoryCommandKind.Hide:
                {
                    var c = Find(cmd.id);
                    return c != null && characters.Remove(c);
                }
                case StoryCommandKind.HideAll:
                    if (characters.Count == 0) return false;
                    characters.Clear();
                    return true;
                case StoryCommandKind.Move:
                {
                    var c = Find(cmd.id);
                    if (c == null) return false;
                    if (cmd.hasX) c.x = cmd.x;
                    if (cmd.hasFlip) c.flip = cmd.flip;
                    return true;
                }
                case StoryCommandKind.Cg:
                    if (cg == cmd.id) return false;
                    cg = cmd.id;
                    return true;
                case StoryCommandKind.CgOff:
                    if (cg.Length == 0) return false;
                    cg = "";
                    return true;
                case StoryCommandKind.Music:
                    if (music == cmd.id) return false;
                    music = cmd.id;
                    return true;
                case StoryCommandKind.StopMusic:
                    if (music.Length == 0) return false;
                    music = "";
                    return true;
                case StoryCommandKind.Ambience:
                    if (ambience == cmd.id) return false;
                    ambience = cmd.id;
                    return true;
                case StoryCommandKind.StopAmbience:
                    if (ambience.Length == 0) return false;
                    ambience = "";
                    return true;
                case StoryCommandKind.Nvl:
                    if (nvl) return false;
                    nvl = true;
                    return true;
                case StoryCommandKind.Adv:
                    if (!nvl) return false;
                    nvl = false;
                    return true;
                case StoryCommandKind.Zoom:
                    zoom = cmd.AmountOr(1f);
                    if (cmd.hasX) zoomX = cmd.x;
                    if (cmd.hasY) zoomY = cmd.y;
                    if (zoom == 1f && !cmd.hasX) { zoomX = 0.5f; zoomY = 0.5f; }
                    return true;
                case StoryCommandKind.Blur:
                    blur = cmd.AmountOr(0f);
                    return true;
                case StoryCommandKind.Filter:
                    filter = cmd.id == "none" ? "" : cmd.id;
                    filterAmount = cmd.AmountOr(1f);
                    return true;
                case StoryCommandKind.Fade:
                    fadedOut = cmd.id == "out";
                    if (fadedOut) fadeColor = string.IsNullOrEmpty(cmd.text) ? "black" : cmd.text;
                    return true;
                default:
                    return false;
            }
        }

        float FreePlace()
        {
            foreach (float p in AutoPlaces)
            {
                bool taken = false;
                foreach (var c in characters)
                    if (c != null && Math.Abs(c.x - p) < 0.1f) { taken = true; break; }
                if (!taken) return p;
            }
            return 0.5f;
        }

        public StageState Clone()
        {
            var s = (StageState)MemberwiseClone();
            s.characters = new List<StageCharacter>();
            if (characters != null)
                foreach (var c in characters)
                    if (c != null) s.characters.Add(c.Clone());
            return s;
        }

        public void CopyFrom(StageState o)
        {
            if (o == null) { Reset(); return; }
            var c = o.Clone();
            background = c.background ?? "";
            cg = c.cg ?? "";
            characters = c.characters;
            music = c.music ?? "";
            ambience = c.ambience ?? "";
            nvl = c.nvl;
            zoom = c.zoom > 0f ? c.zoom : 1f;
            zoomX = c.zoomX;
            zoomY = c.zoomY;
            blur = c.blur;
            filter = c.filter ?? "";
            filterAmount = c.filterAmount;
            fadedOut = c.fadedOut;
            fadeColor = string.IsNullOrEmpty(c.fadeColor) ? "black" : c.fadeColor;
        }

        public void Reset() => CopyFrom(new StageState());

        /// <summary>Same picture and sound (for tests and "did loading restore it").</summary>
        public bool SameAs(StageState o)
        {
            if (o == null) return false;
            if (background != o.background || cg != o.cg || music != o.music || ambience != o.ambience || nvl != o.nvl
                || zoom != o.zoom || blur != o.blur || filter != o.filter || fadedOut != o.fadedOut) return false;
            int n = characters != null ? characters.Count : 0;
            int m = o.characters != null ? o.characters.Count : 0;
            if (n != m) return false;
            for (int i = 0; i < n; i++)
            {
                var a = characters[i];
                var b = o.characters[i];
                if (a.id != b.id || a.expression != b.expression || a.x != b.x || a.flip != b.flip) return false;
            }
            return true;
        }
    }
}
