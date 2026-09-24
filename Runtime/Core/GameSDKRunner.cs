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
            InitializeFirebaseAsync();

            if (_config.adsEnabled && !string.IsNullOrEmpty(_config.levelPlayAppKey))
            {
                InitializeAds();
            }
        }

        private async void InitializeFirebaseAsync()
        {
            DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"[GameSDK] Firebase dependencies are not available: {dependencyStatus}");
                FinishInit(null, null, null);
                return;
            }

            _ = FirebaseApp.DefaultInstance;

            IAnalyticsService analytics = null;
            ICrashService crash = null;
            IAuthService auth = null;

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

            FinishInit(analytics, crash, auth);
            Debug.Log("[GameSDK] Firebase initialized.");
        }

        private LevelPlayAdsService _adsService;

        private void InitializeAds()
        {
            _adsService = new LevelPlayAdsService(_config);

            LevelPlay.OnInitSuccess += _ =>
            {
                _adsService.CreateAndLoadAds();
                Debug.Log("[GameSDK] LevelPlay initialized.");
            };
            LevelPlay.OnInitFailed += error =>
                Debug.LogError($"[GameSDK] LevelPlay failed to initialize: {error}");

            LevelPlay.Init(_config.levelPlayAppKey);
        }

        private bool _finished;

        private void FinishInit(IAnalyticsService analytics, ICrashService crash, IAuthService auth)
        {
            if (_finished) return;
            _finished = true;
            GameSDK.MarkInitialized(_config, _adsService, analytics, crash, auth);
        }

        private void OnDestroy()
        {
            _adsService?.Dispose();
        }
    }
}
