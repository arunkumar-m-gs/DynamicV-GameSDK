using System;

namespace DynamicV.GameSDK
{
    public interface IAdsService
    {
        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }

        /// <summary>Shows the interstitial if one is loaded; no-ops otherwise. A new one auto-loads after close.</summary>
        void ShowInterstitial(string placementName = null);

        /// <summary>Shows the rewarded ad if one is loaded. onComplete receives true only if the reward was actually granted (ad watched to completion).</summary>
        void ShowRewarded(string placementName, Action<bool> onComplete);

        void ShowBanner();
        void HideBanner();

        event Action OnInterstitialClosed;
        event Action<string> OnRewardedFailedToShow;
    }
}
