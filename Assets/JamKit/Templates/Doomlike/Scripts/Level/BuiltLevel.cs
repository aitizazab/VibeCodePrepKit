using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>Something to pick up, lying in a level.</summary>
    public sealed class LevelItem
    {
        public enum ItemKind { Pickup, Weapon, Key }

        public ItemKind kind;
        public PickupAsset pickup;
        public WeaponAsset weapon;
        public int keyColor;
        public Billboard billboard;
        public Vector3 basePosition;
        public int cellX, cellY;
        public bool taken;
        /// <summary>Counts toward the tally's items % (dropped items don't).</summary>
        public bool counts;
        public float bobPhase;

        public string Name =>
            kind == ItemKind.Key ? MapLegend.KeyName(keyColor) + " key"
            : kind == ItemKind.Weapon ? (weapon != null ? weapon.displayName : "weapon")
            : (pickup != null ? pickup.displayName : "item");
    }

    /// <summary>Where an enemy starts.</summary>
    public struct EnemySpawn
    {
        public EnemyAsset asset;
        public Vector3 position;
        /// <summary>Its idle facing, a 2D angle in radians.</summary>
        public float facing;
        public int cellX, cellY;
    }

    /// <summary>
    /// A level built from a map by <see cref="LevelBuilder"/>: its geometry, doors, items and enemy
    /// spawn points, plus cell lookups between the map grid and world space.
    /// </summary>
    public sealed class BuiltLevel : MonoBehaviour
    {
        public MapData map;
        public LevelTheme theme;
        public float cellSize = 2.5f;
        public float ceiling = 3.6f;
        public Transform geometry, doorsRoot, thingsRoot;
        public NavMeshSurface surface;
        public bool navMeshBuilt;
        public Vector3 startPosition;
        public float startYaw;
        public int totalItems, totalSecrets;
        public bool hasBoss;

        public readonly List<DoomDoor> doors = new List<DoomDoor>();
        public readonly List<LevelItem> items = new List<LevelItem>();
        public readonly List<EnemySpawn> enemySpawns = new List<EnemySpawn>();
        /// <summary>Billboards that only need to face the camera (decorations, exit signs).</summary>
        public readonly List<Billboard> scenery = new List<Billboard>();
        DoomDoor[] _doorAt = new DoomDoor[0];

        public void SetDoorGrid(int width, int height) => _doorAt = new DoomDoor[width * height];

        public void RegisterDoor(DoomDoor door)
        {
            doors.Add(door);
            if (map.InBounds(door.cellX, door.cellY)) _doorAt[door.cellY * map.width + door.cellX] = door;
        }

        public DoomDoor DoorAt(int x, int y) =>
            map != null && map.InBounds(x, y) && _doorAt.Length == map.width * map.height ? _doorAt[y * map.width + x] : null;

        public Vector3 CellCenter(int x, int y, float height = 0f)
        {
            var c = GridMath.CellCenter(x, y, map.height, cellSize);
            return new Vector3(c.X, height, c.Y);
        }

        public bool WorldToCell(Vector3 p, out int x, out int y) =>
            GridMath.WorldToCell(new System.Numerics.Vector2(p.x, p.z), map.width, map.height, cellSize, out x, out y);

        public CellKind KindAt(Vector3 p) => WorldToCell(p, out int x, out int y) ? map.KindAt(x, y) : CellKind.Void;

        /// <summary>Destroys what the builder made in memory for this level (meshes, NavMesh data):
        /// they aren't scene objects, so destroying the GameObjects alone would leak them.</summary>
        public void DestroyGeneratedAssets()
        {
            if (surface != null)
            {
                surface.RemoveData();
                if (surface.navMeshData != null) DestroyAsset(surface.navMeshData);
                surface.navMeshData = null;
            }
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh != null && mesh != MeshKit.BillboardQuad) DestroyAsset(mesh);
            }
        }

        static void DestroyAsset(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>The first exit's centre (debug skip, tests).</summary>
        public Vector3 ExitPosition => map.exits.Count > 0 ? CellCenter(map.exits[0].x, map.exits[0].y) : startPosition;
    }
}
