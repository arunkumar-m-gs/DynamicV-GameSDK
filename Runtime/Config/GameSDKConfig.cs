using System;
using System.Collections.Generic;
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
        [Tooltip("LevelPlay app key for the Android app, from the LevelPlay dashboard.")]
        public string levelPlayAppKeyAndroid;
        [Tooltip("LevelPlay app key for the iOS app, from the LevelPlay dashboard.")]
        public string levelPlayAppKeyIos;
        public PlatformAdUnit interstitial;
        public PlatformAdUnit banner;
        [Tooltip("One entry per rewarded ad unit. Game code picks one by key: GameSDK.Ads.ShowRewarded(\"hints\", ...).")]
        public List<RewardedAdUnit> rewardedUnits = new List<RewardedAdUnit>();

        [Header("Firebase Analytics")]
        public bool analyticsEnabled = true;

        [Header("Firebase Crashlytics")]
        public bool crashlyticsEnabled = true;

        [Header("Firebase Cloud Messaging (optional module)")]
        public bool messagingEnabled = true;

        [Header("Firebase Realtime Database (optional module)")]
        public bool databaseEnabled = true;

        [Header("Firebase Auth / Google Sign-In")]
        public bool authEnabled = true;
        [Tooltip("Sign in anonymously on first launch if no user session exists yet.")]
        public bool autoSignInAnonymously = true;
        [Tooltip("OAuth 2.0 Web Client ID (type 3) from the google-services.json currently bundled in THIS project. Must match that file's project or sign-in fails at runtime with an InvalidCredential error, not a compile error.")]
        public string googleWebClientId;

        /// <summary>LevelPlay app key for the platform this build runs on. The Editor uses the Android one.</summary>
        public string ResolveAppKey()
        {
#if UNITY_IOS
            return levelPlayAppKeyIos;
#else
            return levelPlayAppKeyAndroid;
#endif
        }
    }

    /// <summary>An ad unit that has a different ID on each platform.</summary>
    [Serializable]
    public class PlatformAdUnit
    {
        public string androidAdUnitId;
        public string iosAdUnitId;

        public string Resolve()
        {
#if UNITY_IOS
            return iosAdUnitId;
#else
            return androidAdUnitId;
#endif
        }
    }

    /// <summary>A rewarded ad unit game code refers to by <see cref="key"/>.</summary>
    [Serializable]
    public class RewardedAdUnit : PlatformAdUnit
    {
        [Tooltip("Name game code passes to ShowRewarded, e.g. \"hints\" or \"lives\".")]
        public string key;
    }
}
