using UnityEngine;

namespace DynamicV.GameSDK
{
    /// <summary>
    /// Auto-boots the SDK before the first scene loads, provided a GameSDKConfig asset
    /// exists at Resources/GameSDKConfig and has autoInitializeOnLaunch set. This is what
    /// makes per-project setup "create one config asset" instead of "write bootstrap code".
    /// </summary>
    internal static class GameSDKBootstrapper
    {
        private const string ConfigResourcePath = "GameSDKConfig";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var config = Resources.Load<GameSDKConfig>(ConfigResourcePath);
            if (config == null)
            {
                Debug.LogWarning(
                    "[GameSDK] No GameSDKConfig found at Resources/GameSDKConfig.asset - SDK not initialized. " +
                    "Create one via Assets > Create > DynamicV > Game SDK Config.");
                return;
            }

            if (!config.autoInitializeOnLaunch) return;

            var runnerObject = new GameObject("[DynamicV.GameSDK]");
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<GameSDKRunner>().Initialize(config);
        }
    }
}
