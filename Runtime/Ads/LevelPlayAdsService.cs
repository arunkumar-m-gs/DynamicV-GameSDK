using System;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace DynamicV.GameSDK
{
    /// <summary>
    /// Ads facade backed by Unity LevelPlay mediation. LevelPlay itself is what talks to
    /// AdMob / Meta Audience Network / Unity Ads / etc, so this class never touches any
    /// individual ad network SDK directly - swapping or adding a mediated network is a
    /// LevelPlay dashboard + adapter change, not a code change here.
    /// </summary>
    public class LevelPlayAdsService : IAdsService, IDisposable
    {
        private readonly GameSDKConfig _config;
        private LevelPlayInterstitialAd _interstitial;
        private LevelPlayRewardedAd _rewarded;
        private LevelPlayBannerAd _banner;
        private bool _rewardGranted;

        public bool IsInterstitialReady => _interstitial != null && _interstitial.IsAdReady();
        public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady();

        public event Action OnInterstitialClosed;
        public event Action<string> OnRewardedFailedToShow;

        public LevelPlayAdsService(GameSDKConfig config)
        {
            _config = config;
        }

        /// <summary>Call once LevelPlay.Init has raised OnInitSuccess.</summary>
        public void CreateAndLoadAds()
        {
            if (!string.IsNullOrEmpty(_config.interstitialAdUnitId))
            {
                _interstitial = new LevelPlayInterstitialAd(_config.interstitialAdUnitId);
                _interstitial.OnAdClosed += _ =>
                {
                    OnInterstitialClosed?.Invoke();
                    _interstitial.LoadAd();
                };
                _interstitial.OnAdLoadFailed += error =>
                    Debug.LogWarning($"[GameSDK.Ads] Interstitial failed to load: {error}");
                _interstitial.LoadAd();
            }

            if (!string.IsNullOrEmpty(_config.rewardedAdUnitId))
            {
                _rewarded = new LevelPlayRewardedAd(_config.rewardedAdUnitId);
                _rewarded.OnAdRewarded += (info, reward) => _rewardGranted = true;
                _rewarded.OnAdClosed += _ => _rewarded.LoadAd();
                _rewarded.OnAdLoadFailed += error =>
                    Debug.LogWarning($"[GameSDK.Ads] Rewarded failed to load: {error}");
                _rewarded.LoadAd();
            }

            if (!string.IsNullOrEmpty(_config.bannerAdUnitId))
            {
                _banner = new LevelPlayBannerAd(_config.bannerAdUnitId);
                _banner.OnAdLoadFailed += error =>
                    Debug.LogWarning($"[GameSDK.Ads] Banner failed to load: {error}");
                _banner.LoadAd();
            }
        }

        public void ShowInterstitial(string placementName = null)
        {
            if (_interstitial == null || !_interstitial.IsAdReady())
            {
                Debug.LogWarning("[GameSDK.Ads] Interstitial requested but not ready.");
                return;
            }

            _interstitial.ShowAd(placementName);
        }

        public void ShowRewarded(string placementName, Action<bool> onComplete)
        {
            if (_rewarded == null || !_rewarded.IsAdReady())
            {
                onComplete?.Invoke(false);
                OnRewardedFailedToShow?.Invoke("not-ready");
                return;
            }

            _rewardGranted = false;

            void OnClosed(LevelPlayAdInfo info)
            {
                _rewarded.OnAdClosed -= OnClosed;
                onComplete?.Invoke(_rewardGranted);
            }

            _rewarded.OnAdClosed += OnClosed;
            _rewarded.ShowAd(placementName);
        }

        public void ShowBanner() => _banner?.ShowAd();
        public void HideBanner() => _banner?.HideAd();

        public void Dispose()
        {
            _interstitial?.Dispose();
            _rewarded?.Dispose();
            _banner?.DestroyAd();
        }
    }
}
