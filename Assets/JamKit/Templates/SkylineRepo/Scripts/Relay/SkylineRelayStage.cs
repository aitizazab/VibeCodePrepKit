using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Relay's change to the hall's geometry (docs/SKYLINE-RELAY.md), built at run time from <see cref="RelayArena.StageSolids"/>:
    /// the four lane walls (a jump and a mantle high, with an opening at one end each) and the two climbing blocks to the catwalk.
    /// A bright cap strip along the top of every one marks it as a ledge to mantle. M1's own hall cover and the mezzanine's glass
    /// upstand are turned off (the upstand's face would stop the mantle onto the deck; the cover would sit in the lanes).
    /// No ramps and no stairs.
    /// </summary>
    public static class SkylineRelayStage
    {
        static readonly Color WallColor = new Color(0.72f, 0.76f, 0.84f), BlockColor = new Color(0.86f, 0.74f, 0.55f);

        /// <summary>Builds the walls and blocks under a new "Relay Stage" object, turns M1's cover and glass upstand off, returns the object.</summary>
        public static Transform Build()
        {
            var root = new GameObject("Relay Stage").transform;
            foreach (var s in RelayArena.StageSolids())
            {
                bool block = s.id.StartsWith("climb", System.StringComparison.Ordinal);
                var mat = SkylineInkRoles.Flat(block ? BlockColor : WallColor, SkylineInkRoles.None) ?? SkylineBotLook.Lit(WallColor);
                Vector3 min = U(s.box.min), max = U(s.box.max);
                var go = SkylineBotLook.Visual(PrimitiveType.Cube, s.id, root, mat);
                go.transform.position = (min + max) * 0.5f;
                go.transform.localScale = max - min;
                go.AddComponent<BoxCollider>();
                // the cap strip: a thin bright slab on top, not solid
                var cap = SkylineBotLook.Visual(PrimitiveType.Cube, s.id + " cap", root,
                    SkylineInkRoles.Flat(SkylineRelayProp.Orange, SkylineInkRoles.None) ?? SkylineBotLook.Lit(SkylineRelayProp.Orange), false);
                cap.transform.position = new Vector3((min.x + max.x) * 0.5f, max.y + 0.02f, (min.z + max.z) * 0.5f);
                cap.transform.localScale = new Vector3(max.x - min.x + 0.02f, 0.05f, Mathf.Min(max.z - min.z, 0.3f));
            }
            TurnOffM1();
            return root;
        }

        /// <summary>Turns off M1's hall cover (the group named Cover in the Main Hall) and the mezzanine's glass upstand.</summary>
        static void TurnOffM1()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                bool cover = t.name == "Cover" && t.parent != null && t.parent.name == "Main Hall";
                bool rail = t.name.StartsWith("Mezzanine Balustrade", System.StringComparison.Ordinal);
                if (cover || rail) t.gameObject.SetActive(false);
            }
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
