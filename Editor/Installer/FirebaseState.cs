using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DynamicV.GameSDK.Installer
{
    // Detects which Firebase modules are present and keeps the DV_FIREBASE_* scripting defines
    // (which gate the runtime assembly) in sync. Works for .unitypackage imports, which
    // asmdef versionDefines can't see because they aren't UPM packages.
    [InitializeOnLoad]
    internal static class FirebaseState
    {
        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS,
        };

        static FirebaseState()
        {
            EditorApplication.delayCall += SyncDefines;
        }

        public static bool IsInstalled(CatalogEntry entry)
        {
            var assets = Application.dataPath;
            if (entry.DetectFolder != null) return Directory.Exists(Path.Combine(assets, entry.DetectFolder));

            if (Directory.Exists(Path.Combine(assets, "Firebase")) &&
                Directory.GetFiles(Path.Combine(assets, "Firebase"), entry.Dll, SearchOption.AllDirectories).Length > 0)
                return true;

            // Firebase installed as UPM tarballs (com.google.firebase.<x>) is also fine.
            var manifest = Path.Combine(Directory.GetParent(assets).FullName, "Packages", "manifest.json");
            var upmName = "com.google.firebase." + entry.Dll.Replace("Firebase.", "").Replace(".dll", "").ToLowerInvariant();
            return File.Exists(manifest) && File.ReadAllText(manifest).Contains(upmName);
        }

        // The plugin ships loose .cs files, which compile into Assembly-CSharp. asmdef assemblies
        // (like DynamicV.GameSDK) can't reference Assembly-CSharp, so wrap the plugin in its own.
        private const string GoogleSignInAsmdef = "Assets/GoogleSignIn/GoogleSignIn.asmdef";

        private static void EnsureGoogleSignInAsmdef()
        {
            var dir = Path.Combine(Application.dataPath, "GoogleSignIn");
            var path = Path.Combine(dir, "GoogleSignIn.asmdef");
            if (!Directory.Exists(dir) || File.Exists(path)) return;

            File.WriteAllText(path,
                "{\n    \"name\": \"GoogleSignIn\",\n    \"rootNamespace\": \"Google\",\n" +
                "    \"references\": [],\n    \"includePlatforms\": [],\n    \"excludePlatforms\": [],\n" +
                "    \"allowUnsafeCode\": false,\n    \"overrideReferences\": false,\n" +
                "    \"precompiledReferences\": [],\n    \"autoReferenced\": true,\n" +
                "    \"defineConstraints\": [],\n    \"versionDefines\": [],\n    \"noEngineReferences\": false\n}\n");
            AssetDatabase.ImportAsset(GoogleSignInAsmdef);
        }

        public static void SyncDefines()
        {
            EnsureGoogleSignInAsmdef();

            foreach (var target in Targets)
            {
                string[] current;
                try { current = PlayerSettings.GetScriptingDefineSymbols(target).Split(';'); }
                catch { continue; }

                var set = current.Where(s => s.Length > 0).ToList();
                var changed = false;
                foreach (var entry in PackageCatalog.Entries.Where(e => e.Define != null))
                {
                    var has = set.Contains(entry.Define);
                    var want = IsInstalled(entry);
                    if (want && !has) { set.Add(entry.Define); changed = true; }
                    else if (!want && has) { set.Remove(entry.Define); changed = true; }
                }
                if (changed) PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", set));
            }
        }
    }
}
