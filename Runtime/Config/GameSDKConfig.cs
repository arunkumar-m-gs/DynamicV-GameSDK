using UnityEngine;

namespace DynamicV.GameSDK
{
    /// <summary>
    /// Per-project settings for the SDK. Create exactly one via
    /// Assets &gt; Create &gt; DynamicV &gt; Game SDK Config, save it at
    /// "Assets/Resources/GameSDKConfig.asset" (the bootstrapper loads it by that
    /// exact Resources path), and fill in this project's own IDs. Firebase itself
    /// is configured separately via google-services.json / GoogleService-Info.plist,
    /// not through this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "DynamicV/Game SDK Config", fileName = "GameSDKConfig")]
    public class GameSDKConfig : ScriptableObject
    {
        [Header("Bootstrap")]
        [Tooltip("If true, the SDK initializes itself automatically before the first scene loads. Turn off only if you need to call GameSDK.Initialize() manually at a specific point.")]
        public bool autoInitializeOnLaunch = true;

        [Header("Ads (LevelPlay)")]
        public bool adsEnabled = true;
        [Tooltip("LevelPlay app key from the ironSource/LevelPlay dashboard for THIS project.")]
        public string levelPlayAppKey;
        public string interstitialAdUnitId;
        public string rewardedAdUnitId;
        public string bannerAdUnitId;

        [Header("Firebase Analytics")]
        public bool analyticsEnabled = true;

        [Header("Firebase Crashlytics")]
        public bool crashlyticsEnabled = true;

        [Header("Firebase Auth / Google Sign-In")]
        public bool authEnabled = true;
        [Tooltip("Sign in anonymously on first launch if no user session exists yet.")]
        public bool autoSignInAnonymously = true;
        [Tooltip("OAuth 2.0 Web Client ID (type 3) from the google-services.json currently bundled in THIS project. Must match that file's project or sign-in fails at runtime with an InvalidCredential error, not a compile error.")]
        public string googleWebClientId;
    }
}
