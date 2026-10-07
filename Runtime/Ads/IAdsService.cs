using System;

namespace DynamicV.GameSDK
{
    public interface IAdsService
    {
        bool IsInterstitialReady { get; }

        /// <summary>True if the rewarded unit registered under <paramref name="unitKey"/> has an ad loaded.</summary>
        bool IsRewardedReady(string unitKey);

        /// <summary>
        /// Shows the interstitial if one is loaded. onFinished(true) fires after the ad is closed,
        /// onFinished(false) fires straight away if nothing was ready or the ad failed to display.
        /// A new one auto-loads afterwards.
        /// </summary>
        void ShowInterstitial(Action<bool> onFinished = null);

        /// <summary>
        /// Shows the rewarded ad for <paramref name="unitKey"/> (a key from GameSDKConfig.rewardedUnits).
        /// onComplete receives true only if the reward was actually granted (ad watched to completion).
        /// </summary>
        void ShowRewarded(string unitKey, Action<bool> onComplete);

        /// <summary>Asks for the banner to be visible. It appears as soon as it is loaded, and stays until HideBanner.</summary>
        void ShowBanner();
        void HideBanner();

        /// <summary>True while a banner is actually on screen (requested AND loaded).</summary>
        bool IsBannerVisible { get; }

        /// <summary>Height of the banner in density-independent pixels (dp), for reserving layout space.</summary>
        float BannerHeightDp { get; }

        event Action OnInterstitialClosed;
        event Action<string> OnRewardedFailedToShow;
        event Action<bool> OnBannerVisibilityChanged;
    }
}
