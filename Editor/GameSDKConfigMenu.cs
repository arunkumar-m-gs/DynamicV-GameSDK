using System.IO;
using UnityEditor;
using UnityEngine;

namespace DynamicV.GameSDK.Editor
{
    internal static class GameSDKConfigMenu
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string AssetPath = ResourcesFolder + "/GameSDKConfig.asset";

        [MenuItem("DynamicV/Game SDK/Create Config Asset", priority = 0)]
        private static void CreateConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameSDKConfig>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[GameSDK] Config asset already exists; selecting it.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var config = ScriptableObject.CreateInstance<GameSDKConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log($"[GameSDK] Created config asset at {AssetPath}. Fill in the fields, then it auto-initializes on launch.");
        }

        [MenuItem("DynamicV/Game SDK/Select Config Asset", priority = 1)]
        private static void SelectConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameSDKConfig>(AssetPath);
            if (existing == null)
            {
                Debug.LogWarning("[GameSDK] No config asset found yet. Use DynamicV > Game SDK > Create Config Asset first.");
                return;
            }

            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }
    }
}
