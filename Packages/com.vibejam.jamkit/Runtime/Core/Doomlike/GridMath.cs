using System;
using System.Numerics;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Map cells to world space and 2D angles to Unity yaw. The map's x (column) runs east (+X), its
    /// rows run south, so the top row is the northern edge (+Z). 2D angles follow the JamKit
    /// convention (radians inside, 0 = +X, counter-clockwise, here toward +Z), so a pattern's 2D
    /// output maps straight onto the XZ plane: (x, y) becomes (X, Z).
    /// </summary>
    public static class GridMath
    {
        public const float Deg = (float)(Math.PI / 180.0);
        public const float TwoPi = (float)(Math.PI * 2.0);

        /// <summary>World XZ of a cell's centre (Vector2.X = world X, Vector2.Y = world Z).</summary>
        public static Vector2 CellCenter(int x, int y, int mapHeight, float cellSize) =>
            new Vector2((x + 0.5f) * cellSize, (mapHeight - y - 0.5f) * cellSize);

        /// <summary>The cell containing a world XZ point; false (with the nearest cell) when outside.</summary>
        public static bool WorldToCell(Vector2 xz, int mapWidth, int mapHeight, float cellSize, out int x, out int y)
        {
            x = (int)Math.Floor(xz.X / cellSize);
            y = mapHeight - 1 - (int)Math.Floor(xz.Y / cellSize);
            bool inside = x >= 0 && y >= 0 && x < mapWidth && y < mapHeight;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= mapWidth) x = mapWidth - 1;
            if (y >= mapHeight) y = mapHeight - 1;
            return inside;
        }

        /// <summary>Unity yaw (degrees, clockwise from +Z seen from above) for a 2D angle in radians.</summary>
        public static float YawFromAngle(float angleRadians) => 90f - angleRadians / Deg;

        /// <summary>2D angle (radians, counter-clockwise from +X) for a Unity yaw in degrees.</summary>
        public static float AngleFromYaw(float yawDegrees) => (90f - yawDegrees) * Deg;

        public static Vector2 Direction(float angleRadians) =>
            new Vector2((float)Math.Cos(angleRadians), (float)Math.Sin(angleRadians));

        public static float AngleOf(Vector2 v) => (float)Math.Atan2(v.Y, v.X);

        /// <summary>Into [0, 2 pi).</summary>
        public static float Wrap(float radians)
        {
            float r = radians % TwoPi;
            if (r < 0f) r += TwoPi;
            if (r >= TwoPi) r -= TwoPi;
            return r;
        }

        /// <summary>Into (-pi, pi]: the signed shortest turn.</summary>
        public static float WrapSigned(float radians)
        {
            float r = Wrap(radians);
            return r > (float)Math.PI ? r - TwoPi : r;
        }
    }
}
