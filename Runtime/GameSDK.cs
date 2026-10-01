using System;

namespace DynamicV.GameSDK
{
    /// <summary>
    /// Single entry point consuming game code talks to. Never reference Firebase.* or
    /// Unity.Services.LevelPlay.* directly from game code - go through these properties instead,
    /// so swapping an implementation later doesn't ripple through the whole project.
    /// </summary>
    public static class GameSDK
    {
        public static bool IsInitialized { get; private set; }
        public static GameSDKConfig Config { get; private set; }

        public static IAdsService Ads { get; private set; }
        public static IAnalyticsService Analytics { get; private set; }
        public static ICrashService Crash { get; private set; }
        public static IAuthService Auth { get; private set; }
        /// <summary>Null unless the Cloud Messaging module is installed and enabled.</summary>
        public static IMessagingService Messaging { get; private set; }
        /// <summary>Null unless the Realtime Database module is installed and enabled.</summary>
        public static IDatabaseService Database { get; private set; }

        /// <summary>Fires once, after Firebase + LevelPlay have both finished initializing.</summary>
        public static event Action OnInitialized;

        internal static void MarkInitialized(
            GameSDKConfig config,
            IAdsService ads,
            IAnalyticsService analytics,
            ICrashService crash,
            IAuthService auth,
            IMessagingService messaging = null,
            IDatabaseService database = null)
        {
            Config = config;
            Ads = ads;
            Analytics = analytics;
            Crash = crash;
            Auth = auth;
            Messaging = messaging;
            Database = database;
            IsInitialized = true;
            OnInitialized?.Invoke();
        }
    }
}
