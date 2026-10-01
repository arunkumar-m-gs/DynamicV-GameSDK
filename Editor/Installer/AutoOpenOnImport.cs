using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DynamicV.GameSDK.Installer
{
    // Opens the setup window once per project the first time the package is imported and any
    // required module is missing. After that it's only reachable from the DynamicV menu.
    [InitializeOnLoad]
    internal static class AutoOpenOnImport
    {
        static AutoOpenOnImport()
        {
            var key = "DynamicV.GameSDK.SetupShown." + Application.dataPath.GetHashCode();
            if (EditorPrefs.GetBool(key, false)) return;

            EditorApplication.delayCall += () =>
            {
                if (PackageCatalog.Entries.Where(e => e.Required).All(FirebaseState.IsInstalled)) return;
                EditorPrefs.SetBool(key, true);
                SetupWindow.Open();
            };
        }
    }
}
