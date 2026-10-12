using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Where the hill's generated meshes go (World round 2, the memory fix). Milestone 1 wrote one asset file per mesh
    /// (3,876 files, 525 MB of text) and kept every mesh in memory until the scene was saved; the build died at 6.8 GB.
    /// Now each chunk is ONE asset (its meshes as sub-assets: <see cref="Begin"/>, <see cref="Add"/>), saved with the band
    /// it belongs to (<see cref="Flush"/>), after which its meshes are unloaded from memory (the scene keeps its references
    /// to the saved assets and Unity loads them again only when something reads them).
    /// <para>The counts (<see cref="Meshes"/>, <see cref="Vertices"/>, <see cref="Bytes"/>) feed the build's report.</para>
    /// </summary>
    public sealed class HillMeshStore
    {
        /// <summary>The store HillMesh.Build saves into while the builder runs (null: HillMesh's old one-file-per-mesh).</summary>
        public static HillMeshStore Current;

        readonly string _folder;
        string _container;
        readonly List<Mesh> _pending = new List<Mesh>();
        readonly List<Mesh> _saved = new List<Mesh>();
        readonly HashSet<Mesh> _keepReadable = new HashSet<Mesh>();
        int _files;
        public int Files => _files;

        static long _meshes, _vertices, _indices, _bytes;
        public static long Meshes => _meshes;
        public static long Vertices => _vertices;
        public static long Indices => _indices;
        /// <summary>Vertex and index bytes of everything made (one copy: CPU or GPU).</summary>
        public static long Bytes => _bytes;

        public static void ResetCounts() { _meshes = _vertices = _indices = _bytes = 0; }

        public static void Count(int vertices, int indices, bool shortIndices)
        {
            _meshes++;
            _vertices += vertices;
            _indices += indices;
            _bytes += (long)vertices * HillMesh.VertexBytes + (long)indices * (shortIndices ? 2 : 4);
        }

        public HillMeshStore(string folder) { _folder = folder; }

        /// <summary>Starts a container: the next meshes go into "&lt;folder&gt;/&lt;name&gt;.asset".</summary>
        public void Begin(string name)
        {
            _container = _folder + "/" + Safe(name) + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(_container) != null) _container = AssetDatabase.GenerateUniqueAssetPath(_container);
        }

        public string ContainerPath => _container;

        /// <summary>Adds a mesh to the current container. Render meshes are saved not readable (the player drops their CPU
        /// copy; the geometry lint reads them through MeshData); <paramref name="keepReadable"/> for collider meshes.</summary>
        public void Add(Mesh mesh, bool keepReadable = false)
        {
            if (mesh == null) return;
            if (keepReadable) _keepReadable.Add(mesh);
            if (_container == null) Begin("Misc");
            if (AssetDatabase.LoadMainAssetAtPath(_container) == null)
            {
                AssetDatabase.CreateAsset(mesh, _container);
                _files++;
            }
            else AssetDatabase.AddObjectToAsset(mesh, _container);
            _pending.Add(mesh);
        }

        /// <summary>A mesh someone else already put into this store's container (the LOD meshes): unloaded with the rest.</summary>
        public void Track(Mesh mesh)
        {
            if (mesh != null) _pending.Add(mesh);
        }

        /// <summary>Writes what's pending; with <paramref name="unload"/> the meshes leave memory (Resources.UnloadAsset on
        /// each saved mesh; the scene's references stay valid and load them back on demand).</summary>
        public void Flush(bool unload)
        {
            foreach (var m in _pending)
                if (m != null && !_keepReadable.Contains(m)) JamKit.Perf.EditorTools.VhPerfMesh.MarkNotReadable(m);
            AssetDatabase.SaveAssets();
            _saved.AddRange(_pending);
            _pending.Clear();
            if (!unload) return;
            foreach (var m in _saved)
            {
                if (m == null || !EditorUtility.IsPersistent(m)) continue;
                try { Resources.UnloadAsset(m); } catch (Exception e) { Debug.LogWarning("JAMKIT_STREET couldn't unload " + m.name + ": " + e.Message); }
            }
            _saved.Clear();
            _keepReadable.Clear();
        }

        static string Safe(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }
}
