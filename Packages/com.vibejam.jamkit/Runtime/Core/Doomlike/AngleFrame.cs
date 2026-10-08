using System;
using System.Numerics;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Which of a billboard's 8 viewing angles to show, from the enemy's facing and where the camera
    /// is. Frame k means the camera stands k x 45 degrees counter-clockwise (seen from above) from
    /// the direction the enemy faces:
    /// <code>
    /// 0 front (it looks at you)      4 back
    /// 1 front-left                   5 back-right
    /// 2 left side (it faces your left)  6 right side (it faces your right)
    /// 3 back-left                    7 front-right
    /// </code>
    /// Sprite sheets may hold all 8, or only 0..4 with 5, 6, 7 drawn as mirrored 3, 2, 1 (the usual
    /// trick for symmetric characters), or a single view.
    /// </summary>
    public static class AngleFrame
    {
        /// <param name="facing">The enemy's facing, a 2D angle in radians.</param>
        /// <param name="toViewer">The direction from the enemy to the camera, a 2D angle in radians.</param>
        public static int Frame(float facing, float toViewer, int directions = 8)
        {
            if (directions <= 1) return 0;
            float rel = GridMath.Wrap(toViewer - facing);
            float step = GridMath.TwoPi / directions;
            int f = (int)Math.Floor(rel / step + 0.5f);
            return f % directions;
        }

        /// <summary>From positions on the XZ plane (Vector2.Y = world Z).</summary>
        public static int Frame(float facing, Vector2 enemy, Vector2 viewer, int directions = 8)
        {
            Vector2 d = viewer - enemy;
            if (d.LengthSquared() < 1e-8f) return 0;
            return Frame(facing, GridMath.AngleOf(d), directions);
        }

        /// <summary>The sheet column for an 8-way frame. <paramref name="sheetDirections"/>: 8 (all
        /// drawn), 5 (0..4 drawn, the rest mirrored) or 1 (one view). Mirrored columns set
        /// <paramref name="mirror"/>: flip the sprite horizontally.</summary>
        public static int SheetColumn(int frame8, int sheetDirections, out bool mirror)
        {
            mirror = false;
            int f = ((frame8 % 8) + 8) % 8;
            if (sheetDirections >= 8) return f;
            if (sheetDirections <= 1) return 0;
            if (f <= 4) return Math.Min(f, sheetDirections - 1);
            mirror = true;
            return Math.Min(8 - f, sheetDirections - 1);
        }
    }
}
