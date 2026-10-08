using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Turns a parsed map into a level: floor, ceiling and wall meshes (one per material, textures
    /// tiled once per cell, light baked into vertex colours), box colliders, doors, items, scenery,
    /// enemy spawn points, and a NavMesh (AI Navigation NavMeshSurface over the floor and walls).
    /// Runs at play time when a level starts (so editing a map and pressing Play is all it takes),
    /// and in the editor for validation and previews.
    /// </summary>
    public static class LevelBuilder
    {
        /// <summary>Layer for walls, floors and doors (raycasts against the world use it).</summary>
        public const int WorldLayer = 0;
        /// <summary>Layer for enemies (Unity's built-in layer 1, so no project setup is needed).</summary>
        public const int ActorLayer = 1;
        public const int WorldMask = 1 << WorldLayer;
        public const int ActorMask = 1 << ActorLayer;
        /// <summary>The built-in "Not Walkable" NavMesh area.</summary>
        const int NotWalkableArea = 1;

        static readonly int[] Dx = { 0, 0, 1, -1 };
        static readonly int[] Dy = { -1, 1, 0, 0 }; // north (up in the file), south, east, west

        public static BuiltLevel Build(MapData map, DoomlikeConfig config, Transform parent, bool buildNavMesh)
        {
            var theme = config.Theme(map.theme);
            float cs = Mathf.Max(0.5f, config.cellSize);
            float h = config.CeilingFor(map);

            var root = new GameObject("Level: " + map.name);
            if (parent != null) root.transform.SetParent(parent, false);
            var level = root.AddComponent<BuiltLevel>();
            level.map = map;
            level.theme = theme;
            level.cellSize = cs;
            level.ceiling = h;
            level.SetDoorGrid(map.width, map.height);

            level.geometry = Child(root.transform, "Geometry");
            var floorRoot = Child(level.geometry, "Floor");
            var wallRoot = Child(level.geometry, "Walls");
            var ceilingRoot = Child(root.transform, "Ceiling");
            level.doorsRoot = Child(root.transform, "Doors");
            level.thingsRoot = Child(root.transform, "Things");

            BuildSurfaces(map, config, theme, cs, h, floorRoot, wallRoot, ceilingRoot);
            BuildColliders(map, cs, h, floorRoot, wallRoot, ceilingRoot);
            BuildDoors(map, config, theme, cs, h, level);
            BuildThings(map, config, cs, h, level, wallRoot);

            var sx = map.startX < 0 ? 0 : map.startX;
            var sy = map.startY < 0 ? 0 : map.startY;
            level.startPosition = level.CellCenter(sx, sy);
            level.startYaw = GridMath.YawFromAngle(map.ResolveStartFacingDegrees() * GridMath.Deg);
            level.totalSecrets = map.Count(CellKind.Secret);

            var modifier = wallRoot.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NotWalkableArea;

            var surface = level.geometry.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.18f;
            surface.minRegionArea = 2f;
            level.surface = surface;
            if (buildNavMesh) BuildNavMesh(level);
            return level;
        }

        public static void BuildNavMesh(BuiltLevel level)
        {
            if (level.surface == null) return;
            level.surface.BuildNavMesh();
            level.navMeshBuilt = level.surface.navMeshData != null;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = WorldLayer;
            return go.transform;
        }

        // ---------- Floors, walls, ceiling ----------

        static void BuildSurfaces(MapData map, DoomlikeConfig config, LevelTheme theme, float cs, float h,
            Transform floorRoot, Transform wallRoot, Transform ceilingRoot)
        {
            var batches = new Dictionary<Material, MeshBatch>();
            var parents = new Dictionary<Material, Transform>();
            float light = Mathf.Clamp(map.light, 0.2f, 1.5f);

            MeshBatch Batch(Material m, Transform parent)
            {
                if (m == null) m = config.floorMaterials != null && config.floorMaterials.Count > 0 ? config.floorMaterials[0] : null;
                var key = m != null ? m : FallbackMaterial;
                if (!batches.TryGetValue(key, out var b))
                {
                    b = new MeshBatch();
                    batches[key] = b;
                    parents[key] = parent;
                }
                return b;
            }

            Color floorTint = theme.floorTint * light;
            Color ceilTint = theme.ceilingTint * light;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    if (!map.IsOpen(x, y)) continue;
                    var cell = map.Get(x, y);
                    float x0 = x * cs, x1 = x0 + cs;
                    float z0 = (map.height - 1 - y) * cs, z1 = z0 + cs;
                    var uv0 = new Vector2(x, map.height - 1 - y);

                    // Floor (seen from above: up is +Z).
                    Material fm = FloorMaterial(config, cell);
                    var fb = Batch(fm, floorRoot);
                    Color32 fc = MeshKit.Shade(floorTint, 0.9f);
                    fb.Quad(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0),
                        uv0, uv0 + new Vector2(0, 1), uv0 + new Vector2(1, 1), uv0 + new Vector2(1, 0), fc, fc);

                    // Ceiling (facing down).
                    var cb = Batch(config.ceilingMaterial, ceilingRoot);
                    Color32 cc = MeshKit.Shade(ceilTint, 0.75f);
                    cb.Quad(new Vector3(x0, h, z1), new Vector3(x0, h, z0), new Vector3(x1, h, z0), new Vector3(x1, h, z1),
                        uv0 + new Vector2(0, 1), uv0, uv0 + new Vector2(1, 0), uv0 + new Vector2(1, 1), cc, cc);

                    // Walls where this open cell meets solid ones.
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Dx[d], ny = y + Dy[d];
                        if (!map.IsSolidGeometry(nx, ny)) continue;
                        int variant = map.WallVariantAt(nx, ny);
                        var wm = WallMaterial(config, variant);
                        var wb = Batch(wm, wallRoot);
                        Color tint = theme.WallTint(variant) * light;
                        float shade = d < 2 ? 0.8f : 1f; // north/south faces darker: the classic fake light
                        Color32 top = MeshKit.Shade(tint, shade), bottom = MeshKit.Shade(tint, shade * 0.78f);
                        float v = h / cs;
                        Vector3 a, b, c, e;
                        switch (d)
                        {
                            case 0: // north wall at z1, facing -Z
                                a = new Vector3(x0, 0f, z1); b = new Vector3(x0, h, z1); c = new Vector3(x1, h, z1); e = new Vector3(x1, 0f, z1);
                                break;
                            case 1: // south wall at z0, facing +Z
                                a = new Vector3(x1, 0f, z0); b = new Vector3(x1, h, z0); c = new Vector3(x0, h, z0); e = new Vector3(x0, 0f, z0);
                                break;
                            case 2: // east wall at x1, facing -X
                                a = new Vector3(x1, 0f, z1); b = new Vector3(x1, h, z1); c = new Vector3(x1, h, z0); e = new Vector3(x1, 0f, z0);
                                break;
                            default: // west wall at x0, facing +X
                                a = new Vector3(x0, 0f, z0); b = new Vector3(x0, h, z0); c = new Vector3(x0, h, z1); e = new Vector3(x0, 0f, z1);
                                break;
                        }
                        wb.Quad(a, b, c, e, new Vector2(0f, 0f), new Vector2(0f, v), new Vector2(1f, v), new Vector2(1f, 0f), bottom, top);
                    }
                }

            foreach (var pair in batches)
            {
                var go = new GameObject(pair.Key.name);
                go.layer = WorldLayer;
                go.transform.SetParent(parents[pair.Key], false);
                go.AddComponent<MeshFilter>().sharedMesh = pair.Value.ToMesh(map.name + " " + pair.Key.name);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = pair.Key == FallbackMaterial ? null : pair.Key;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        /// <summary>Stands in for a missing material in the batch dictionary (keys can't be null).</summary>
        static Material _fallback;
        static Material FallbackMaterial
        {
            get
            {
                if (_fallback == null)
                {
                    var shader = Shader.Find("JamKit/Doomlike/World");
                    _fallback = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
                    _fallback.name = "Missing material";
                    _fallback.hideFlags = HideFlags.DontSave;
                }
                return _fallback;
            }
        }

        static Material FloorMaterial(DoomlikeConfig config, MapCell cell)
        {
            switch (cell.entry.kind)
            {
                case CellKind.Hazard: return config.hazardMaterial;
                case CellKind.Exit: return config.exitMaterial;
                case CellKind.Floor:
                    return Pick(config.floorMaterials, cell.entry.variant);
                default:
                    return Pick(config.floorMaterials, 0);
            }
        }

        public static Material WallMaterial(DoomlikeConfig config, int variant) => Pick(config.wallMaterials, variant);

        static Material Pick(List<Material> list, int i)
        {
            if (list == null || list.Count == 0) return null;
            return list[Mathf.Clamp(i, 0, list.Count - 1)];
        }

        // ---------- Colliders ----------

        static void BuildColliders(MapData map, float cs, float h, Transform floorRoot, Transform wallRoot, Transform ceilingRoot)
        {
            var floorGo = floorRoot.gameObject;
            var wallGo = wallRoot.gameObject;
            for (int y = -1; y <= map.height; y++)
            {
                int runStart = -2, floorStart = -2;
                for (int x = -1; x <= map.width + 1; x++)
                {
                    bool wall = x <= map.width && map.IsSolidGeometry(x, y) && TouchesOpen(map, x, y);
                    if (wall && runStart == -2) runStart = x;
                    if (!wall && runStart != -2)
                    {
                        AddBox(wallGo, map, cs, runStart, x - 1, y, h * 0.5f, h, cs);
                        runStart = -2;
                    }
                    bool open = x < map.width && map.IsOpen(x, y);
                    if (open && floorStart == -2) floorStart = x;
                    if (!open && floorStart != -2)
                    {
                        AddBox(floorGo, map, cs, floorStart, x - 1, y, -0.5f, 1f, cs);
                        floorStart = -2;
                    }
                }
            }
            // One slab over the whole map: shots and blasts stop at the ceiling. Not part of the NavMesh.
            var ceiling = ceilingRoot.gameObject.AddComponent<BoxCollider>();
            ceiling.center = new Vector3(map.width * cs * 0.5f, h + 0.5f, map.height * cs * 0.5f);
            ceiling.size = new Vector3(map.width * cs + 2f * cs, 1f, map.height * cs + 2f * cs);
        }

        static bool TouchesOpen(MapData map, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && map.IsOpen(x + dx, y + dy)) return true;
            return false;
        }

        static void AddBox(GameObject go, MapData map, float cs, int x0, int x1, int y, float centerY, float height, float depth)
        {
            var box = go.AddComponent<BoxCollider>();
            float cx = (x0 + x1 + 1) * 0.5f * cs;
            float cz = (map.height - 1 - y + 0.5f) * cs;
            box.center = new Vector3(cx, centerY, cz);
            box.size = new Vector3((x1 - x0 + 1) * cs, height, depth);
        }

        // ---------- Doors ----------

        static void BuildDoors(MapData map, DoomlikeConfig config, LevelTheme theme, float cs, float h, BuiltLevel level)
        {
            float light = Mathf.Clamp(map.light, 0.2f, 1.5f);
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var e = map.Get(x, y).entry;
                    DoomDoor.DoorKind kind;
                    switch (e.kind)
                    {
                        case CellKind.Door: kind = DoomDoor.DoorKind.Normal; break;
                        case CellKind.LockedDoor: kind = DoomDoor.DoorKind.Locked; break;
                        case CellKind.SecretDoor: kind = DoomDoor.DoorKind.Secret; break;
                        case CellKind.ClosetWall: kind = DoomDoor.DoorKind.Closet; break;
                        default: continue;
                    }

                    Vector3 size;
                    Material mat;
                    Color tint;
                    if (kind == DoomDoor.DoorKind.Normal || kind == DoomDoor.DoorKind.Locked)
                    {
                        bool spansX = map.IsSolidGeometry(x - 1, y) && map.IsSolidGeometry(x + 1, y);
                        size = spansX ? new Vector3(cs, h, 0.32f) : new Vector3(0.32f, h, cs);
                        mat = kind == DoomDoor.DoorKind.Locked && config.lockedDoorMaterial != null ? config.lockedDoorMaterial : config.doorMaterial;
                        tint = (kind == DoomDoor.DoorKind.Locked ? config.KeyColor(e.variant) : Color.white) * light;
                    }
                    else
                    {
                        int variant = NeighbourWallVariant(map, x, y);
                        size = new Vector3(cs, h, cs);
                        mat = WallMaterial(config, variant);
                        tint = theme.WallTint(variant) * light;
                    }

                    var go = new GameObject((kind == DoomDoor.DoorKind.Locked ? MapLegend.KeyName(e.variant) + " door" : kind + " door") + " " + (x + 1) + "," + (y + 1));
                    go.layer = WorldLayer;
                    go.transform.SetParent(level.doorsRoot, false);
                    go.transform.localPosition = level.CellCenter(x, y, h * 0.5f);
                    go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box(size, cs, tint);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var box = go.AddComponent<BoxCollider>();
                    box.size = size;
                    var door = go.AddComponent<DoomDoor>();
                    door.Init(kind, e.variant, x, y, kind == DoomDoor.DoorKind.Closet ? h + 0.05f : h - 0.12f);
                    if (kind != DoomDoor.DoorKind.Normal)
                    {
                        var ob = go.AddComponent<NavMeshObstacle>();
                        ob.shape = NavMeshObstacleShape.Box;
                        ob.size = size + new Vector3(0.3f, 0f, 0.3f);
                        ob.carving = true;
                        ob.carveOnlyStationary = true;
                        door.obstacle = ob;
                    }
                    level.RegisterDoor(door);
                }
        }

        static int NeighbourWallVariant(MapData map, int x, int y)
        {
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (map.KindAt(nx, ny) == CellKind.Wall) return map.WallVariantAt(nx, ny);
            }
            return 0;
        }

        // ---------- Things ----------

        static void BuildThings(MapData map, DoomlikeConfig config, float cs, float h, BuiltLevel level, Transform solidRoot)
        {
            int items = 0;
            for (int i = 0; i < map.things.Count; i++)
            {
                var t = map.things[i];
                Vector3 pos = level.CellCenter(t.x, t.y);
                var e = t.entry;
                if (e.kind == CellKind.Key)
                {
                    var bb = Billboard.Create(MapLegend.KeyName(e.variant) + " key", level.thingsRoot, pos + Vector3.up * 0.25f, 0.6f, 0.6f,
                        config.spriteMaterial, config.keyTexture, config.KeyColor(e.variant), 0.7f);
                    level.items.Add(new LevelItem { kind = LevelItem.ItemKind.Key, keyColor = e.variant, billboard = bb, basePosition = bb.transform.position, cellX = t.x, cellY = t.y, bobPhase = i * 0.7f });
                    continue;
                }
                switch (e.category)
                {
                    case EntityCategory.Enemy:
                    {
                        var asset = Get(config.enemies, e.variant);
                        if (asset == null) break;
                        level.enemySpawns.Add(new EnemySpawn { asset = asset, position = pos, facing = OpenFacing(map, t.x, t.y), cellX = t.x, cellY = t.y });
                        if (asset.isBoss) level.hasBoss = true;
                        break;
                    }
                    case EntityCategory.Pickup:
                    {
                        var p = Get(config.pickups, e.variant);
                        if (p == null) break;
                        var bb = Billboard.Create(p.displayName, level.thingsRoot, pos, Aspect(p.texture) * p.size, p.size, config.spriteMaterial, p.texture, p.tint);
                        level.items.Add(new LevelItem { kind = LevelItem.ItemKind.Pickup, pickup = p, billboard = bb, basePosition = pos, cellX = t.x, cellY = t.y, counts = p.countsAsItem, bobPhase = i * 0.7f });
                        if (p.countsAsItem) items++;
                        break;
                    }
                    case EntityCategory.Weapon:
                    {
                        var w = Get(config.weapons, e.variant);
                        if (w == null) break;
                        var bb = Billboard.Create(w.displayName, level.thingsRoot, pos, Aspect(w.pickupTexture) * w.pickupSize, w.pickupSize, config.spriteMaterial, w.pickupTexture, Color.white);
                        level.items.Add(new LevelItem { kind = LevelItem.ItemKind.Weapon, weapon = w, billboard = bb, basePosition = pos, cellX = t.x, cellY = t.y, counts = true, bobPhase = i * 0.7f });
                        items++;
                        break;
                    }
                    case EntityCategory.Decoration:
                    {
                        var d = Get(config.decorations, e.variant);
                        if (d == null) break;
                        var bb = Billboard.Create(d.displayName, level.thingsRoot, pos, Aspect(d.texture) * d.size, d.size, config.spriteMaterial, d.texture, d.tint, d.glow);
                        level.scenery.Add(bb);
                        if (d.solid)
                        {
                            var col = new GameObject(d.displayName + " (solid)");
                            col.layer = WorldLayer;
                            col.transform.SetParent(solidRoot, false);
                            col.transform.localPosition = pos + Vector3.up * (h * 0.5f);
                            var cap = col.AddComponent<CapsuleCollider>();
                            cap.radius = Mathf.Max(0.1f, d.radius);
                            cap.height = h;
                        }
                        break;
                    }
                }
            }
            level.totalItems = items;

            foreach (var exit in map.exits)
            {
                var bb = Billboard.Create("Exit sign", level.thingsRoot, level.CellCenter(exit.x, exit.y, 1.1f),
                    Aspect(config.exitSignTexture) * 0.7f, 0.7f, config.spriteMaterial, config.exitSignTexture, Color.white, 1f);
                level.scenery.Add(bb);
            }
        }

        static T Get<T>(List<T> list, int i) where T : class => list != null && i >= 0 && i < list.Count ? list[i] : null;

        static float Aspect(Texture t) => t == null || t.height == 0 ? 1f : t.width / (float)t.height;

        /// <summary>Facing toward the longest open run from a cell (enemies look into their room).</summary>
        static float OpenFacing(MapData map, int x, int y)
        {
            int best = -1;
            float facing = 0f;
            float[] angle = { 90f, 270f, 0f, 180f };
            for (int d = 0; d < 4; d++)
            {
                int run = 0, cx = x + Dx[d], cy = y + Dy[d];
                while (map.IsOpen(cx, cy) && run < 64) { run++; cx += Dx[d]; cy += Dy[d]; }
                if (run > best) { best = run; facing = angle[d]; }
            }
            return facing * GridMath.Deg;
        }
    }
}
