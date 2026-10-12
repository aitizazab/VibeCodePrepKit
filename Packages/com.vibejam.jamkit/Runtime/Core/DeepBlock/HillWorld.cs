using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.DeepBlock
{
    /// <summary>How a house's roof is shaped. Ridge "along" means parallel to the house's front.</summary>
    public enum HillRoof { GableFront, GableSide, Hip, Gambrel, Flat, Shed }

    /// <summary>Which way a building's front faces (only the four directions: the hill's streets are square).</summary>
    public enum HillFacing { South, East, North, West }

    /// <summary>
    /// One house on the hill, as data (docs/redesign/world.md "Houses"): the core makes its colliders from it (the walls'
    /// box, the roof's slopes, the porch, the steps) and the editor's HillHouses draws it from the same numbers, so what
    /// you see is what you stand on. The footprint is the walls' outer faces; the front faces <see cref="facing"/>.
    /// </summary>
    public sealed class HillHouse
    {
        public string name = "", lot = "";
        /// <summary>The footprint's centre at the ground floor's level (Y = the floor; the plinth runs down to the ground).</summary>
        public Vector3 center;
        /// <summary>Across the front (width) and front to back (depth), metres.</summary>
        public float width = 9f, depth = 10f;
        public HillFacing facing = HillFacing.South;
        /// <summary>Storeys of <see cref="storeyHeight"/> (1, 1.5 for a storey and a half under the roof, 2, 2.5, 3).</summary>
        public float storeys = 2f, storeyHeight = 2.9f;
        public HillRoof roof = HillRoof.GableSide;
        public float pitch = 35f, overhang = 0.45f;
        /// <summary>The plinth: how far the ground floor stands above the lowest ground round the house (hillside houses
        /// stand on taller plinths at their downhill side).</summary>
        public float plinth = 0.6f;
        /// <summary>Material ids (the editor's HillMat): walls, roof, trim, front door.</summary>
        public string wall = "siding_white", roofMat = "roof_shingle_grey", trim = "trim_white", door = "door_red";
        public bool porch = true, porchRoof = true, bay, chimney = true, shutters, gutters = true;
        /// <summary>Dormers on the front slope (0-3); a turret on a front corner (-1 left, 0 none, 1 right).</summary>
        public int dormers, turret;
        /// <summary>Porch depth (metres out from the front) and width (0: 60 % of the front).</summary>
        public float porchDepth = 2.2f, porchWidth;
        /// <summary>Varies windows, trim details and colours that aren't given.</summary>
        public int seed;
        /// <summary>Lit windows (0-1): how many rooms have a lamp on (golden hour: a few).</summary>
        public float litShare = 0.25f;

        public float EaveHeight => (float)Math.Floor(storeys) * storeyHeight + (storeys % 1f > 0.01f ? 0.9f : 0f) + 0.3f;
        /// <summary>The ridge's height above the floor (a flat roof: its parapet).</summary>
        public float RidgeHeight
        {
            get
            {
                float span = RidgeAcrossSpan * 0.5f + overhang;
                switch (roof)
                {
                    case HillRoof.Flat: return EaveHeight + 0.6f;
                    case HillRoof.Shed: return EaveHeight + (float)Math.Tan(pitch * Math.PI / 180.0) * (RidgeAcrossSpan + overhang * 2f) * 0.5f;
                    case HillRoof.Gambrel: return EaveHeight + span * 0.95f;
                    default: return EaveHeight + (float)Math.Tan(pitch * Math.PI / 180.0) * span;
                }
            }
        }

        /// <summary>The span the roof slopes across: the depth for a ridge parallel to the front (GableSide, Hip, Gambrel,
        /// Shed), the width for a gable facing the street (GableFront).</summary>
        public float RidgeAcrossSpan => roof == HillRoof.GableFront ? width : depth;

        /// <summary>The house's local axes in the world: "right" along the front (the house's own right, as it looks out of its front door), "back" from the front into the house.</summary>
        public Vector3 Right => facing == HillFacing.South ? new Vector3(-1f, 0f, 0f) : facing == HillFacing.North ? new Vector3(1f, 0f, 0f)
            : facing == HillFacing.East ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 0f, -1f);
        public Vector3 Back => facing == HillFacing.South ? new Vector3(0f, 0f, 1f) : facing == HillFacing.North ? new Vector3(0f, 0f, -1f)
            : facing == HillFacing.East ? new Vector3(-1f, 0f, 0f) : new Vector3(1f, 0f, 0f);
        /// <summary>The world yaw the front faces (South 180, East 90, North 0, West 270).</summary>
        public float FrontYaw => facing == HillFacing.South ? 180f : facing == HillFacing.East ? 90f : facing == HillFacing.North ? 0f : 270f;
        /// <summary>The world extents of the walls: X and Z sizes.</summary>
        public Vector2 WorldSize => facing == HillFacing.South || facing == HillFacing.North ? new Vector2(width, depth) : new Vector2(depth, width);
        /// <summary>The front door's threshold (world, at the floor).</summary>
        public Vector3 FrontDoor => center - Back * (depth * 0.5f);
    }

    /// <summary>
    /// The colliders of the hill's houses and props, made from their data so the core's tests and the Unity scene stand on
    /// the same surfaces (the editor's drawings follow these exactly: walls on the footprint, roof shingles on the slopes).
    /// </summary>
    public static class HillColliders
    {
        /// <summary>Porch roofs stand this far above the floor; their tops are walkable.</summary>
        public const float PorchRoofHeight = 2.75f, PorchRoofThick = 0.18f, PorchDeckHeight = 0.0f;

        static BlockPiece Box(string name, BlockKind kind, Vector3 min, Vector3 max, string layer)
        {
            var mn = Vector3.Min(min, max);
            var mx = Vector3.Max(min, max);
            return new BlockPiece { name = name, kind = kind, center = (mn + mx) * 0.5f, size = mx - mn, layer = layer, runnable = true };
        }

        /// <summary>A world box from a box in the house's local frame (x along
        /// <see cref="HillHouse.Right"/>, z along <see cref="HillHouse.Back"/>, y up from the floor; the origin is the footprint's centre).</summary>
        static BlockPiece Local(HillHouse h, string name, BlockKind kind, float x0, float x1, float y0, float y1, float z0, float z1, string layer)
        {
            var a = h.center + h.Right * x0 + h.Back * z0 + new Vector3(0f, y0, 0f);
            var b = h.center + h.Right * x1 + h.Back * z1 + new Vector3(0f, y1, 0f);
            return Box(name, kind, a, b, layer);
        }

        /// <summary>A roof slope in the house's frame: across local x (alongX) or local z, from height <paramref name="low"/>
        /// at local <paramref name="lowAt"/> to <paramref name="high"/> at <paramref name="highAt"/>, solid down to <paramref name="bottom"/>.</summary>
        static BlockPiece Slope(HillHouse h, string name, bool alongLocalX, float lowAt, float highAt, float across0, float across1, float low, float high, float bottom, string layer)
        {
            BlockPiece p = alongLocalX
                ? Local(h, name, BlockKind.Roof, lowAt, highAt, bottom, Math.Max(low, high), across0, across1, layer)
                : Local(h, name, BlockKind.Roof, across0, across1, bottom, Math.Max(low, high), lowAt, highAt, layer);
            // Which world axis the slope runs along, and which end is low.
            Vector3 axis = alongLocalX ? h.Right : h.Back;
            bool worldX = Math.Abs(axis.X) > 0.5f;
            float dirSign = worldX ? axis.X : axis.Z;
            bool lowAtMin = (highAt > lowAt) == (dirSign > 0f);
            p.rampAxis = worldX ? 1 : 2;
            p.rampLow = lowAtMin ? low : high;
            p.rampHigh = lowAtMin ? high : low;
            return p;
        }

        /// <summary>Everything of a house you can touch: the walls' box (down through the plinth), the roof's slopes (or flat
        /// top and parapet), the porch's deck, posts and roof, the steps up to it, the chimney.</summary>
        public static void House(HillHouse h, List<BlockPiece> into, string layer)
        {
            float hw = h.width * 0.5f, hd = h.depth * 0.5f, eave = h.EaveHeight, ridge = h.RidgeHeight, o = h.overhang;
            float floorY = 0f, bottom = -h.plinth;
            into.Add(Local(h, h.name + " (walls)", BlockKind.House, -hw, hw, bottom, eave, -hd, hd, layer));
            float rb = eave - 0.25f;   // the roof's solid starts just under the eaves
            switch (h.roof)
            {
                case HillRoof.Flat:
                    into.Add(Local(h, h.name + " (parapet, front)", BlockKind.House, -hw, hw, eave, eave + 0.6f, -hd, -hd + 0.25f, layer));
                    into.Add(Local(h, h.name + " (parapet, back)", BlockKind.House, -hw, hw, eave, eave + 0.6f, hd - 0.25f, hd, layer));
                    into.Add(Local(h, h.name + " (parapet, left)", BlockKind.House, -hw, -hw + 0.25f, eave, eave + 0.6f, -hd, hd, layer));
                    into.Add(Local(h, h.name + " (parapet, right)", BlockKind.House, hw - 0.25f, hw, eave, eave + 0.6f, -hd, hd, layer));
                    break;
                case HillRoof.GableFront:
                {
                    float rise = ridge - eave, dropAtEdge = (float)Math.Tan(h.pitch * Math.PI / 180.0) * o;
                    into.Add(Slope(h, h.name + " (roof, left)", true, -hw - o, 0f, -hd - o, hd + o, eave - dropAtEdge, ridge, rb - dropAtEdge, layer));
                    into.Add(Slope(h, h.name + " (roof, right)", true, hw + o, 0f, -hd - o, hd + o, eave - dropAtEdge, ridge, rb - dropAtEdge, layer));
                    break;
                }
                case HillRoof.Shed:
                {
                    float dropAtEdge = (float)Math.Tan(h.pitch * Math.PI / 180.0) * o;
                    into.Add(Slope(h, h.name + " (roof)", false, -hd - o, hd + o, -hw - o, hw + o, eave - dropAtEdge, ridge, rb - dropAtEdge, layer));
                    break;
                }
                default:
                {
                    // GableSide, Hip, Gambrel: the ridge runs along the front; the slopes go front-to-back.
                    float dropAtEdge = h.roof == HillRoof.Gambrel ? o * 1.6f : (float)Math.Tan(h.pitch * Math.PI / 180.0) * o;
                    into.Add(Slope(h, h.name + " (roof, front)", false, -hd - o, 0f, -hw - o, hw + o, eave - dropAtEdge, ridge, rb - dropAtEdge, layer));
                    into.Add(Slope(h, h.name + " (roof, back)", false, hd + o, 0f, -hw - o, hw + o, eave - dropAtEdge, ridge, rb - dropAtEdge, layer));
                    break;
                }
            }
            if (h.porch)
            {
                float pw = (h.porchWidth > 0f ? h.porchWidth : h.width * 0.6f) * 0.5f, pd = h.porchDepth;
                var deck = Local(h, h.name + " (porch)", BlockKind.Deck, -pw, pw, bottom, floorY, -hd - pd, -hd, layer);
                deck.runnable = false;
                into.Add(deck);
                if (h.porchRoof)
                {
                    float y0 = PorchRoofHeight, y1 = PorchRoofHeight + PorchRoofThick;
                    into.Add(Local(h, h.name + " (porch roof)", BlockKind.Roof, -pw - 0.15f, pw + 0.15f, y0, y1, -hd - pd - 0.15f, -hd, layer));
                    foreach (float x in new[] { -pw + 0.12f, pw - 0.12f })
                        into.Add(Local(h, h.name + " (porch post)", BlockKind.Post, x - 0.08f, x + 0.08f, floorY, y0, -hd - pd + 0.04f, -hd - pd + 0.2f, layer));
                }
                // The porch railing (0.92 m): along the front either side of the steps, and down each side to the wall.
                if (pw > 1.3f)
                {
                    into.Add(Local(h, h.name + " (porch railing)", BlockKind.Fence, -pw + 0.2f, -0.95f, floorY, floorY + 0.92f, -hd - pd + 0.04f, -hd - pd + 0.2f, layer));
                    into.Add(Local(h, h.name + " (porch railing)", BlockKind.Fence, 0.95f, pw - 0.2f, floorY, floorY + 0.92f, -hd - pd + 0.04f, -hd - pd + 0.2f, layer));
                }
                foreach (float x in new[] { -pw + 0.12f, pw - 0.12f })
                    into.Add(Local(h, h.name + " (porch railing)", BlockKind.Fence, x - 0.06f, x + 0.06f, floorY, floorY + 0.92f, -hd - pd + 0.2f, -hd - 0.02f, layer));
                // The steps: a slope from the ground in front up to the porch (as wide as the door's bay).
                if (h.plinth > 0.15f)
                {
                    float run = h.plinth / 0.6f;   // 0.18 m risers on 0.3 m treads: a 31 degree flight
                    var steps = Slope(h, h.name + " (steps)", false, -hd - pd - run, -hd - pd, -0.9f, 0.9f, -h.plinth, floorY, bottom, layer);
                    steps.kind = BlockKind.Ground;
                    steps.runnable = false;
                    into.Add(steps);
                }
            }
            else if (h.plinth > 0.15f)
            {
                float run = h.plinth / 0.6f;
                var steps = Slope(h, h.name + " (steps)", false, -hd - run, -hd, -0.9f, 0.9f, -h.plinth, floorY, bottom, layer);
                steps.kind = BlockKind.Ground;
                steps.runnable = false;
                into.Add(steps);
            }
            if (h.chimney && h.roof != HillRoof.Flat)
            {
                float cx = hw - 1.1f;
                into.Add(Local(h, h.name + " (chimney)", BlockKind.BrickWall, cx - 0.4f, cx + 0.4f, eave - 0.5f, ridge + 0.9f, -0.35f, 0.45f, layer));
            }
            if (h.turret != 0)
            {
                float tx = h.turret * (hw - 1.2f);
                into.Add(Local(h, h.name + " (turret)", BlockKind.House, tx - 1.5f, tx + 1.5f, bottom, eave + 2.2f, -hd - 1.2f, -hd + 1.8f, layer));
            }
        }

        /// <summary>A prop's collider (props you'd bump into): its kind's box, turned by its yaw (to the nearest quarter).</summary>
        public static BlockPiece Prop(HillProp p, string layer)
        {
            if (!p.Solid) return null;
            var s = p.Size;
            int q = ((int)Math.Round(p.yaw / 90f) % 4 + 4) % 4;
            if (q == 1 || q == 3) s = new Vector3(s.Z, s.Y, s.X);
            var b = Box(p.kind + " " + p.tag, BlockKind.Prop, p.position - new Vector3(s.X * 0.5f, 0f, s.Z * 0.5f), p.position + new Vector3(s.X * 0.5f, s.Y, s.Z * 0.5f), layer);
            b.runnable = p.kind == HillPropKind.Shed || p.kind == HillPropKind.Van || p.kind == HillPropKind.BusShelter;
            return b;
        }

        /// <summary>The front edge's direction for a footprint: the outward normal of the side the building faces.</summary>
        public static Vector3 FrontNormal(HillFacing f) => f == HillFacing.South ? new Vector3(0f, 0f, -1f) : f == HillFacing.North ? new Vector3(0f, 0f, 1f)
            : f == HillFacing.East ? new Vector3(1f, 0f, 0f) : new Vector3(-1f, 0f, 0f);

        /// <summary>
        /// A big building's colliders (the editor's HillLandmarks draws within and on them):
        /// <list type="bullet">
        /// <item>every kind: the footprint from its floor (it stands on the ground) to <see cref="HillBuilding.height"/> above it;</item>
        /// <item>Church: that box is the nave up to its eaves, plus the nave's two roof slopes (the ridge along the depth),
        /// plus the bell tower, a 6 m square centred on the front edge, standing 6 m out of the front and rising to
        /// height + 12 (the spire above it is drawn, out of reach);</item>
        /// <item>Hospital: the main block (height), the tower (the middle 34 m of the width, the back 70 % of the depth, to
        /// height + 12), the entrance canopy (16 m wide, 7 m out of the front's middle, 4.2-4.6 m up, on two rows of
        /// columns);</item>
        /// <item>ShopRow: the box (flat roof) plus a parapet ring 0.9 m high, and one awning per shopfront (the front
        /// split evenly by <see cref="HillBuilding.signs"/>.Count), 1.6 m deep, 2.7-2.85 m up, 0.3 m in from each
        /// shopfront's sides; awnings are walkable;</item>
        /// <item>School, CornerShop, Kiosk: flat roofs with a 0.6 m parapet ring; Garage (lock-ups): a flat roof with no
        /// parapet (you run along a row of them).</item>
        /// </list>
        /// </summary>
        public static void Building(HillBuilding b, List<BlockPiece> into, string layer)
        {
            float y0 = b.floorY, top = b.floorY + b.height;
            var min = new Vector3(b.min.X, y0, b.min.Y);
            var max = new Vector3(b.max.X, top, b.max.Y);
            into.Add(Box(b.name + " (body)", BlockKind.House, min, max, layer));
            var n = FrontNormal(b.facing);
            bool frontAlongX = Math.Abs(n.Z) > 0.5f;
            float frontPos = n.X + n.Z > 0f ? (frontAlongX ? b.max.Y : b.max.X) : (frontAlongX ? b.min.Y : b.min.X);
            float midAlong = frontAlongX ? (b.min.X + b.max.X) * 0.5f : (b.min.Y + b.max.Y) * 0.5f;
            float outSign = n.X + n.Z;
            // A box measured from the front: "along" the front (centre, width), "out" from the front face (from, to; negative = inside).
            BlockPiece Front(string name, BlockKind kind, float alongC, float width, float out0, float out1, float yA, float yB)
            {
                float o0 = frontPos + outSign * out0, o1 = frontPos + outSign * out1;
                Vector3 a = frontAlongX ? new Vector3(alongC - width * 0.5f, yA, o0) : new Vector3(o0, yA, alongC - width * 0.5f);
                Vector3 c = frontAlongX ? new Vector3(alongC + width * 0.5f, yB, o1) : new Vector3(o1, yB, alongC + width * 0.5f);
                return Box(name, kind, a, c, layer);
            }
            void Parapet(float h)
            {
                into.Add(Box(b.name + " (parapet)", BlockKind.House, new Vector3(min.X, top, min.Z), new Vector3(max.X, top + h, min.Z + 0.3f), layer));
                into.Add(Box(b.name + " (parapet)", BlockKind.House, new Vector3(min.X, top, max.Z - 0.3f), new Vector3(max.X, top + h, max.Z), layer));
                into.Add(Box(b.name + " (parapet)", BlockKind.House, new Vector3(min.X, top, min.Z), new Vector3(min.X + 0.3f, top + h, max.Z), layer));
                into.Add(Box(b.name + " (parapet)", BlockKind.House, new Vector3(max.X - 0.3f, top, min.Z), new Vector3(max.X, top + h, max.Z), layer));
            }
            float frontLen = frontAlongX ? b.max.X - b.min.X : b.max.Y - b.min.Y;
            float depth = frontAlongX ? b.max.Y - b.min.Y : b.max.X - b.min.X;
            switch (b.kind)
            {
                case HillBuildingKind.Church:
                {
                    into.Add(Front(b.name + " (bell tower)", BlockKind.House, midAlong, 6f, 6f, -0.01f, y0, top + 12f));
                    // The nave's roof: the ridge runs front to back; slopes across the front's width.
                    float ridge = top + frontLen * 0.5f * 0.9f;
                    var left = Box(b.name + " (roof, left)", BlockKind.Roof, frontAlongX ? new Vector3(b.min.X - 0.4f, top - 0.3f, b.min.Y) : new Vector3(b.min.X, top - 0.3f, b.min.Y - 0.4f),
                        frontAlongX ? new Vector3(midAlong, ridge, b.max.Y) : new Vector3(b.max.X, ridge, midAlong), layer);
                    left.rampAxis = frontAlongX ? 1 : 2; left.rampLow = top - 0.35f; left.rampHigh = ridge;
                    var right = Box(b.name + " (roof, right)", BlockKind.Roof, frontAlongX ? new Vector3(midAlong, top - 0.3f, b.min.Y) : new Vector3(b.min.X, top - 0.3f, midAlong),
                        frontAlongX ? new Vector3(b.max.X + 0.4f, ridge, b.max.Y) : new Vector3(b.max.X, ridge, b.max.Y + 0.4f), layer);
                    right.rampAxis = frontAlongX ? 1 : 2; right.rampLow = ridge; right.rampHigh = top - 0.35f;
                    into.Add(left);
                    into.Add(right);
                    break;
                }
                case HillBuildingKind.Hospital:
                {
                    into.Add(Front(b.name + " (tower)", BlockKind.House, midAlong, 34f, -depth * 0.3f, -depth, y0, top + 12f));
                    into.Add(Front(b.name + " (entrance canopy)", BlockKind.Roof, midAlong, 16f, 7f, -0.01f, b.floorY + 4.2f, b.floorY + 4.6f));
                    for (int i = 0; i < 4; i++)
                        into.Add(Front(b.name + " (canopy column)", BlockKind.Post, midAlong - 7f + i * (14f / 3f), 0.5f, 6.6f, 6.1f, b.floorY, b.floorY + 4.2f));
                    Parapet(1.1f);
                    break;
                }
                case HillBuildingKind.ShopRow:
                {
                    Parapet(0.9f);
                    int shops = Math.Max(1, b.signs.Count);
                    float w = frontLen / shops;
                    float start = midAlong - frontLen * 0.5f;
                    for (int i = 0; i < shops; i++)
                    {
                        var awn = Front(b.name + " (awning " + (i + 1) + ")", BlockKind.Roof, start + w * (i + 0.5f), w - 0.6f, 1.6f, 0f, b.floorY + 2.7f, b.floorY + 2.85f);
                        awn.runnable = false;
                        into.Add(awn);
                    }
                    break;
                }
                case HillBuildingKind.School:
                case HillBuildingKind.CornerShop:
                case HillBuildingKind.Kiosk:
                    Parapet(0.6f);
                    break;
            }
        }

        /// <summary>A tree's trunk collider (its canopy is soft: you pass through leaves).</summary>
        public static BlockPiece Trunk(HillTree t, string layer)
        {
            // Full height (inside the canopy): a trunk is never a step up to anything.
            float r = t.trunk;
            return Box(t.name + " (trunk)", BlockKind.Tree, t.foot - new Vector3(r, 0f, r), t.foot + new Vector3(r, t.height, r), layer);
        }
    }

    /// <summary>A tree (the editor draws it: a trunk, its branches and the leaf masses; the core gives it a trunk collider
    /// and, for the climbable ones, branch platforms).</summary>
    public sealed class HillTree
    {
        public string name = "";
        public Vector3 foot;
        public float height = 9f, canopy = 3.5f, trunk = 0.35f;
        /// <summary>0 broadleaf (oak, maple), 1 tall (poplar, lime), 2 small (cherry, blossom), 3 conifer, 4 the big oak (climbable).</summary>
        public int kind;
        public int seed;
    }

    /// <summary>The props the hill dresses itself with (each has a drawing in the editor's HillProps and, when it's
    /// something you'd bump into, a collider box from <see cref="HillProp.Size"/>).</summary>
    public enum HillPropKind
    {
        Car, Van, Bike, BikeRack, Bin, Wheelie, Mailbox, Hydrant, Streetlight, PowerPole, Bench, Planter, BusShelter, BusStopSign,
        PhoneBox, Fountain, Swing, Slide, Trampoline, Shed, Greenhouse, WashingLine, Grill, Sprinkler, Mower, YardSaleTable,
        PicnicTable, Sandbox, Crates, Pallets, Barrel, TyreStack, Workbench, Ladder, Wheelbarrow, Kennel, BirdBath, Gnome,
        StreetSign, Bollard, Railing, FlowerBed, Bush, Rock, AcUnit, SatelliteDish, Hose, PaddlingPool, Chalk, Ball,
    }

    public sealed class HillProp
    {
        public HillPropKind kind;
        public Vector3 position;
        public float yaw;
        /// <summary>A free number for variants (a car's colour, a sign's text index, a bush's size).</summary>
        public int variant;
        /// <summary>Overrides the kind's size (metres; zero: the kind's own).</summary>
        public Vector3 size;
        public string tag = "";

        /// <summary>The kind's footprint and height (what its collider is).</summary>
        public Vector3 Size => size != Vector3.Zero ? size : SizeOf(kind);

        /// <summary>Whether you'd bump into it (gets a collider). Grass, flowers, chalk and the like don't.</summary>
        public bool Solid => kind != HillPropKind.Chalk && kind != HillPropKind.FlowerBed && kind != HillPropKind.Hose
            && kind != HillPropKind.Sprinkler && kind != HillPropKind.Ball && kind != HillPropKind.WashingLine && kind != HillPropKind.PowerPole
            && kind != HillPropKind.StreetSign && kind != HillPropKind.Gnome;

        public static Vector3 SizeOf(HillPropKind k)
        {
            switch (k)
            {
                case HillPropKind.Car: return new Vector3(1.8f, 1.45f, 4.3f);
                case HillPropKind.Van: return new Vector3(2f, 2.1f, 5f);
                case HillPropKind.Bike: return new Vector3(0.5f, 1f, 1.7f);
                case HillPropKind.BikeRack: return new Vector3(0.5f, 0.8f, 2.2f);
                case HillPropKind.Bin: return new Vector3(0.55f, 0.95f, 0.55f);
                case HillPropKind.Wheelie: return new Vector3(0.6f, 1.05f, 0.7f);
                case HillPropKind.Mailbox: return new Vector3(0.4f, 1.15f, 0.5f);
                case HillPropKind.Hydrant: return new Vector3(0.35f, 0.8f, 0.35f);
                case HillPropKind.Streetlight: return new Vector3(0.3f, 6f, 0.3f);
                case HillPropKind.PowerPole: return new Vector3(0.35f, 9f, 0.35f);
                case HillPropKind.Bench: return new Vector3(1.7f, 0.85f, 0.6f);
                case HillPropKind.Planter: return new Vector3(1.2f, 0.6f, 0.6f);
                case HillPropKind.BusShelter: return new Vector3(3.6f, 2.6f, 1.6f);
                case HillPropKind.BusStopSign: return new Vector3(0.12f, 2.8f, 0.5f);
                case HillPropKind.PhoneBox: return new Vector3(1f, 2.5f, 1f);
                case HillPropKind.Fountain: return new Vector3(2.4f, 1.4f, 2.4f);
                case HillPropKind.Swing: return new Vector3(3.2f, 2.3f, 1.6f);
                case HillPropKind.Slide: return new Vector3(0.8f, 2.1f, 3.6f);
                case HillPropKind.Trampoline: return new Vector3(3.4f, 0.9f, 3.4f);
                case HillPropKind.Shed: return new Vector3(2.6f, 2.4f, 2f);
                case HillPropKind.Greenhouse: return new Vector3(2.4f, 2.3f, 3.2f);
                case HillPropKind.WashingLine: return new Vector3(0.1f, 1.9f, 6f);
                case HillPropKind.Grill: return new Vector3(0.6f, 0.95f, 0.6f);
                case HillPropKind.Sprinkler: return new Vector3(0.2f, 0.2f, 0.2f);
                case HillPropKind.Mower: return new Vector3(0.55f, 1f, 1.4f);
                case HillPropKind.YardSaleTable: return new Vector3(1.8f, 0.75f, 0.8f);
                case HillPropKind.PicnicTable: return new Vector3(1.9f, 0.75f, 1.6f);
                case HillPropKind.Sandbox: return new Vector3(2f, 0.3f, 2f);
                case HillPropKind.Crates: return new Vector3(1.2f, 1.2f, 1.2f);
                case HillPropKind.Pallets: return new Vector3(1.2f, 0.6f, 1f);
                case HillPropKind.Barrel: return new Vector3(0.6f, 0.9f, 0.6f);
                case HillPropKind.TyreStack: return new Vector3(0.7f, 1f, 0.7f);
                case HillPropKind.Workbench: return new Vector3(2f, 0.92f, 0.75f);
                case HillPropKind.Ladder: return new Vector3(0.5f, 3f, 0.9f);
                case HillPropKind.Wheelbarrow: return new Vector3(0.7f, 0.7f, 1.5f);
                case HillPropKind.Kennel: return new Vector3(1f, 1f, 1.2f);
                case HillPropKind.BirdBath: return new Vector3(0.6f, 0.9f, 0.6f);
                case HillPropKind.Gnome: return new Vector3(0.25f, 0.4f, 0.25f);
                case HillPropKind.StreetSign: return new Vector3(0.1f, 2.6f, 0.9f);
                case HillPropKind.Bollard: return new Vector3(0.25f, 0.9f, 0.25f);
                case HillPropKind.Railing: return new Vector3(0.08f, 1.05f, 3f);
                case HillPropKind.FlowerBed: return new Vector3(1.2f, 0.35f, 3f);
                case HillPropKind.Bush: return new Vector3(1.4f, 1.1f, 1.4f);
                case HillPropKind.Rock: return new Vector3(1.2f, 0.7f, 1f);
                case HillPropKind.AcUnit: return new Vector3(0.8f, 0.6f, 0.4f);
                case HillPropKind.SatelliteDish: return new Vector3(0.7f, 0.7f, 0.4f);
                case HillPropKind.Hose: return new Vector3(0.6f, 0.4f, 0.4f);
                case HillPropKind.PaddlingPool: return new Vector3(2f, 0.35f, 2f);
                case HillPropKind.Chalk: return new Vector3(2f, 0.01f, 2f);
                case HillPropKind.Ball: return new Vector3(0.22f, 0.22f, 0.22f);
                default: return new Vector3(1f, 1f, 1f);
            }
        }
    }

    /// <summary>The big buildings: each has its own drawing (the editor's HillLandmarks) round the same collider boxes.</summary>
    public enum HillBuildingKind { BikeShop, CornerShop, ShopRow, Church, School, Hospital, Garage, Boathouse, WaterTower, Kiosk, Terrace, Treehouse }

    public sealed class HillBuilding
    {
        public HillBuildingKind kind;
        public string name = "";
        /// <summary>The footprint (world XZ min and max) and its floor's height.</summary>
        public Vector2 min, max;
        public float floorY, height = 4f;
        public HillFacing facing = HillFacing.South;
        /// <summary>Material ids for the walls and roof (empty: the kind's own).</summary>
        public string wall = "", roofMat = "";
        /// <summary>Shop rows: one entry per shopfront (its sign's index in the sign atlas, or -1).</summary>
        public readonly List<int> signs = new List<int>();
        public int seed;
    }
}
