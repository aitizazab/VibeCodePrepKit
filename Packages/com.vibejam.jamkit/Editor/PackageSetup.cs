using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace JamKit.EditorTools
{
    /// <summary>
    /// Batch-mode helper: asks the Package Manager for the recommended version of each package we
    /// pin, and logs "JAMKIT_PKGVER name version" lines. Search never edits the manifest, so no
    /// domain reload interrupts it (an Add/Remove from batch mode would lose its callback).
    /// </summary>
    public static class PackageSetup
    {
        static readonly string[] Wanted = { "com.unity.cinemachine", "com.unity.probuilder", "com.unity.test-framework", "com.unity.recorder" };

        static readonly List<SearchRequest> Pending = new List<SearchRequest>();

        public static void ResolveVersions()
        {
            foreach (string id in Wanted) Pending.Add(Client.Search(id));
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            foreach (var r in Pending)
                if (!r.IsCompleted) return;
            EditorApplication.update -= Poll;
            bool ok = true;
            foreach (var r in Pending)
            {
                if (r.Status != StatusCode.Success || r.Result == null || r.Result.Length == 0)
                {
                    ok = false;
                    Debug.LogError("JAMKIT_PKGVER_FAIL " + (r.Error != null ? r.Error.message : "no result"));
                    continue;
                }
                var info = r.Result[0];
                string version = !string.IsNullOrEmpty(info.versions.recommended) ? info.versions.recommended : info.versions.latestCompatible;
                Debug.Log($"JAMKIT_PKGVER {info.name} {version}");
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
