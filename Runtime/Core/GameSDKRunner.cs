using Firebase;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace DynamicV.GameSDK
{
    /// <summary>
    /// Drives the actual init sequence: resolve Firebase dependencies, then bring up
    /// whichever services the config asset has enabled. Lives on the DontDestroyOnLoad
    /// object GameSDKBootstrapper creates - never add this manually to a scene.
    /// </summary>
    internal class GameSDKRunner : MonoBehaviour
    {
        private GameSDKConfig _config;

        internal void Initialize(GameSDKConfig config)
        {
            _config = config;

            // Ads first: _adsService must exist before FinishInit hands it to GameSDK.
            if (_config.adsEnabled)
            {
                if (string.IsNullOrEmpty(_config.ResolveAppKey()))
                {
                    Debug.LogWarning("[GameSDK] Ads are enabled but no LevelPlay app key is set for this platform; ads stay off.");
                }
                else
                {
                    InitializeAds();
                }
            }

            // Firebase only allows one CheckAndFixDependenciesAsync at a time, and it throws if
            // anything else calls into Firebase while it runs. A game that has its own Firebase
            // setup and only uses the SDK for ads must therefore not start one here.
            if (UsesFirebase)
            {
                InitializeFirebaseAsync();
            }
            else
            {
                FinishInit(null, null, null, null, null);
            }
        }

        private bool UsesFirebase =>
            _config.analyticsEnabled || _config.crashlyticsEnabled || _config.authEnabled
            || _config.messagingEnabled || _config.databaseEnabled;

        private async void InitializeFirebaseAsync()
        {
            DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"[GameSDK] Firebase dependencies are not available: {dependencyStatus}");
                FinishInit(null, null, null, null, null);
                return;
            }

            _ = FirebaseApp.DefaultInstance;

            IAnalyticsService analytics = null;
            ICrashService crash = null;
            IAuthService auth = null;
            IMessagingService messaging = null;
            IDatabaseService database = null;

            if (_config.analyticsEnabled)
            {
                analytics = new FirebaseAnalyticsService();
            }

            if (_config.crashlyticsEnabled)
            {
                crash = new FirebaseCrashService();
            }

            if (_config.authEnabled)
            {
                var authService = gameObject.AddComponent<FirebaseAuthService>();
                authService.Initialize(_config);
                auth = authService;
            }

#if DV_FIREBASE_MESSAGING
            if (_config.messagingEnabled)
            {
                messaging = new FirebaseMessagingService();
            }
#endif

#if DV_FIREBASE_DATABASE
            if (_config.databaseEnabled)
            {
                database = new FirebaseDatabaseService();
            }
#endif

            FinishInit(analytics, crash, auth, messaging, database);
            Debug.Log("[GameSDK] Firebase initialized.");
        }

        private LevelPlayAdsService _adsService;

        private void InitializeAds()
        {
            _adsService = new LevelPlayAdsService(_config, this);

            LevelPlay.OnInitSuccess += _ =>
            {
                _adsService.CreateAndLoadAds();
                Debug.Log("[GameSDK] LevelPlay initialized.");
            };
            LevelPlay.OnInitFailed += error =>
                Debug.LogError($"[GameSDK] LevelPlay failed to initialize: {error}");

            LevelPlay.Init(_config.ResolveAppKey());
        }

        private bool _finished;

        private void FinishInit(IAnalyticsService analytics, ICrashService crash, IAuthService auth,
            IMessagingService messaging, IDatabaseService database)
        {
            if (_finished) return;
            _finished = true;
            GameSDK.MarkInitialized(_config, _adsService, analytics, crash, auth, messaging, database);
        }

        private void OnDestroy()
        {
            _adsService?.Dispose();
        }
    }
}
