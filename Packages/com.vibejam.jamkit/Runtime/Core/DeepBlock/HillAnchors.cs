using System.Numerics;

namespace JamKit.DeepBlock
{
    /// <summary>
    /// The shared anchors of Larch Hill (docs/redesign/world.md "Anchors"): where the Lasname house stands on the one hill
    /// that Nights 1, 2 and 3 share, its doors and gates, where Night 2 starts outside it, and where Night 2's escape
    /// bursts out of it into the middle band. The World agent owns the hill and these numbers; the House agent places the
    /// house (its interior, exterior and escape) by them. Change them only together (tell the other owner).
    /// <para>World axes: X east, Z north (up the hill), Y up. The creek's north bank (Walt's apron) is Y 0.</para>
    /// <para>The house's own frame is <c>DreamHouseExterior</c>'s: the front door's threshold at the origin, the front
    /// facing local -Z, the house running back along local +Z. On the hill it stands at <see cref="HouseOrigin"/> with no
    /// rotation (<see cref="HouseRootYaw"/> 0), so its front faces south, down the hill, onto Rowan Street.</para>
    /// </summary>
    public static class HillAnchors
    {
        // ---------- The bands (Y is the ground's height) ----------

        /// <summary>The foot: the creek's north bank, Walt's bike shop and yard, the Foot Square (Y 0).</summary>
        public const float FootY = 0f;
        /// <summary>Mill Street, the first terrace street (Y 4); Back Lane B behind its houses (Y 5).</summary>
        public const float MillY = 4f, BackLaneY = 5f;
        /// <summary>Rowan Street, the house's street (Y 9): the top of the lower band.</summary>
        public const float RowanY = 9f;
        /// <summary>The middle band's terraces, one above the other (Chapel Row, School Lane, Bell Row, Upper Row).</summary>
        public const float M1Y = 15f, M2Y = 22f, M3Y = 29f, M4Y = 36f;
        /// <summary>Crest Road and its shops (Y 42); the hospital's forecourt on the summit (Y 46).</summary>
        public const float CrestY = 42f, SummitY = 46f;

        // ---------- The Lasname house ----------

        /// <summary>The house's root: its front door's threshold (the main floor's level), on the north side of Rowan Street.</summary>
        public static readonly Vector3 HouseOrigin = new Vector3(24f, 9.6f, 122f);
        /// <summary>The house root's rotation about Y (degrees): 0, so the front (local -Z) faces south, down the hill.</summary>
        public const float HouseRootYaw = 0f;
        /// <summary>The way the front door faces, as a world yaw (180: south).</summary>
        public const float HouseFrontYaw = 180f;
        /// <summary>The Lasnames' lot (world X and Z): the front garden from Rowan Street's north footpath (Z 116) to the
        /// house, the side garden (the barbecue) east of it, the back garden up to the middle band's first wall (Z 146).
        /// Nothing of the hill's stands inside it except the lot's fences, gates and the ground.</summary>
        public const float LotX0 = 10f, LotX1 = 42f, LotZ0 = 116f, LotZ1 = 146f;
        /// <summary>The most the house's body may take (world): X 14-36, Z 122-142, up to Y <see cref="HouseMaxTop"/>.
        /// The round 5 exterior (13.6 x 15.4 m) fills X 16.4-30, Z 122-137.4.</summary>
        public const float HouseMaxX0 = 14f, HouseMaxX1 = 36f, HouseMaxZ0 = 122f, HouseMaxZ1 = 142f, HouseMaxTop = 9.6f + 22f;
        /// <summary>The lot's ground (the gardens): 0.6 m above Rowan Street, three steps up from the gate.</summary>
        public const float LotGroundY = 9.6f;

        /// <summary>The front door (= <see cref="HouseOrigin"/>), facing <see cref="HouseFrontYaw"/>.</summary>
        public static readonly Vector3 FrontDoor = HouseOrigin;
        /// <summary>The front garden's gate in the picket fence on Rowan Street's north footpath, where Night 1 ends
        /// (she's held there; through the front window she sees the kitchen, Elias at the stove).</summary>
        public static readonly Vector3 FrontGate = new Vector3(24f, RowanY, 116.3f);
        /// <summary>The back door, in the house's back wall (the House agent may move it along that wall).</summary>
        public static readonly Vector3 BackDoor = new Vector3(24f, 9.6f, 137.4f);
        /// <summary>The back garden's gate at the foot of the Chapel Steps, the stairs that climb east along the first middle-band wall (X 30-40) to Chapel Row.</summary>
        public static readonly Vector3 BackGate = new Vector3(29f, 9.6f, 144f);

        // ---------- Night 2 ----------

        /// <summary>Night 2 starts here: on Rowan Street outside the house, the neighbours' loop replaying, facing the gate.</summary>
        public static readonly Vector3 Night2Start = new Vector3(8f, RowanY, 110f);
        public const float Night2StartYaw = 60f;
        /// <summary>Where Night 2's escape bursts out of the house into the middle band: the top of the Chapel Steps on
        /// Chapel Row (the first terrace above the house, Y 15), facing north up the hill. The middle band's escape line
        /// (docs/redesign/world.md) runs from here up the terraces to the bus stop on Hill Road.</summary>
        public static readonly Vector3 EscapeExit = new Vector3(24f, M1Y, 153f);
        public const float EscapeExitYaw = 0f;
        /// <summary>Where the middle band's escape line ends: the Route 9 bus stop on Hill Road (M3's level).</summary>
        public static readonly Vector3 EscapeEnd = new Vector3(110f, M3Y + 0.15f, 226f);

        // ---------- Night 1 ----------

        /// <summary>Night 1 starts here: the creek's far bank, the whole hill ahead.</summary>
        public static readonly Vector3 Night1Arrival = new Vector3(128f, 3f, -42f);
        public const float Night1ArrivalYaw = -28f;

        // ---------- Night 3 ----------

        /// <summary>St. Brigid's main entrance on the summit (the doors Night 3 opens; the exterior stands now).</summary>
        public static readonly Vector3 HospitalEntrance = new Vector3(0f, SummitY, 322f);
        public const float HospitalEntranceYaw = 180f;
    }
}
