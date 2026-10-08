namespace JamKit.Patterns
{
    /// <summary>
    /// Ready-made attacks that read well in a bullet hell, as starting points to tweak. Each call
    /// returns a fresh PatternDef. "Down" is -90 degrees.
    /// </summary>
    public static class PatternLibrary
    {
        /// <summary>One dense ring outwards: the classic "boss pops" burst.</summary>
        public static PatternDef RingBurst(int count = 24, float speed = 4f) => new PatternDef
        {
            shape = PatternShape.Ring,
            count = count,
            speed = speed,
        };

        /// <summary>Spiral arms with a counter-rotating mirror twin, crossing into petals.</summary>
        public static PatternDef SpiralFlower(int arms = 4, float speed = 3.5f) => new PatternDef
        {
            shape = PatternShape.Spiral,
            count = arms,
            rotatePerShotDegrees = 9f,
            shots = 80,
            shotInterval = 0.06f,
            speed = speed,
            mirror = true,
            lifetime = 10f,
        };

        /// <summary>Three quick bursts of three at the player: punishes standing still.</summary>
        public static PatternDef AimedTriple(float speed = 7f) => new PatternDef
        {
            shape = PatternShape.Aimed,
            count = 3,
            spreadDegrees = 24f,
            shots = 3,
            shotInterval = 0.12f,
            speed = speed,
        };

        /// <summary>A line of bullets at the player, each faster than the last: a stack that
        /// stretches out as it flies.</summary>
        public static PatternDef AimedStack(int count = 5, float speed = 4f) => new PatternDef
        {
            shape = PatternShape.Aimed,
            count = count,
            spreadDegrees = 0f,
            speed = speed,
            speedStep = 0.8f,
        };

        /// <summary>A slow wall sweeping down the screen; leave gaps by layering two with offsets.</summary>
        public static PatternDef Wall(int count = 14, float width = 12f, float speed = 2.5f) => new PatternDef
        {
            shape = PatternShape.Line,
            count = count,
            lineWidth = width,
            baseAngleDegrees = -90f,
            speed = speed,
        };

        /// <summary>A random cone at the player with mixed speeds; also the FPS enemy shotgun.</summary>
        public static PatternDef Shotgun(int count = 10, float speed = 6f) => new PatternDef
        {
            shape = PatternShape.Shotgun,
            count = count,
            spreadDegrees = 35f,
            aimAtTarget = true,
            speed = speed,
            speedStep = 0.25f,
        };

        /// <summary>A ring whose bullets curl as they fly, mirrored so the curls cross into a lattice.</summary>
        public static PatternDef CurlRing(int count = 16, float speed = 3f) => new PatternDef
        {
            shape = PatternShape.Ring,
            count = count,
            speed = speed,
            angularVelocityDegrees = 40f,
            mirror = true,
            shots = 3,
            shotInterval = 0.5f,
        };
    }
}
