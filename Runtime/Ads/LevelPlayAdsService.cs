using System;
using System.Collections;
using System.Collections.Generic;
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
        private const float BannerHeight = 50f; // LevelPlayAdSize.BANNER is 320x50 dp
        private static readonly float[] RetryDelays = { 2f, 4f, 8f, 16f, 30f };

        private readonly GameSDKConfig _config;
        private readonly MonoBehaviour _host;

        private LevelPlayInterstitialAd _interstitial;
        private Action<bool> _interstitialFinished;
        private int _interstitialRetry;

        private LevelPlayBannerAd _banner;
        private bool _bannerWanted;
        private bool _bannerLoaded;
        private bool _bannerShown;
        private int _bannerRetry;

        private readonly Dictionary<string, RewardedSlot> _rewarded = new Dictionary<string, RewardedSlot>();

        private class RewardedSlot
        {
            public string Key;
            public LevelPlayRewardedAd Ad;
            public bool RewardGranted;
            public Action<bool> Pending;
            public int Retry;
        }

        public bool IsInterstitialReady => _interstitial != null && _interstitial.IsAdReady();
        public bool IsBannerVisible => _bannerShown;
        public float BannerHeightDp => BannerHeight;

        public event Action OnInterstitialClosed;
        public event Action<string> OnRewardedFailedToShow;
        public event Action<bool> OnBannerVisibilityChanged;

        public LevelPlayAdsService(GameSDKConfig config, MonoBehaviour coroutineHost)
        {
            _config = config;
            _host = coroutineHost;
        }

        public bool IsRewardedReady(string unitKey)
        {
            return unitKey != null && _rewarded.TryGetValue(unitKey, out RewardedSlot slot) && slot.Ad.IsAdReady();
        }

        /// <summary>Call once LevelPlay.Init has raised OnInitSuccess.</summary>
        public void CreateAndLoadAds()
        {
            CreateInterstitial();
            CreateBanner();
            CreateRewardedUnits();
        }

        // ---------------------------------------------------------------- interstitial

        private void CreateInterstitial()
        {
            string id = _config.interstitial?.Resolve();
            if (string.IsNullOrEmpty(id)) return;

            _interstitial = new LevelPlayInterstitialAd(id);
            _interstitial.OnAdLoaded += _ => _interstitialRetry = 0;
            _interstitial.OnAdLoadFailed += error =>
            {
                Debug.LogWarning($"[GameSDK.Ads] Interstitial failed to load: {error}");
                RetryLater(ref _interstitialRetry, () => _interstitial?.LoadAd());
            };
            _interstitial.OnAdClosed += _ =>
            {
                OnInterstitialClosed?.Invoke();
                FinishInterstitial(true);
                _interstitial.LoadAd();
            };
            _interstitial.OnAdDisplayFailed += (_, error) =>
            {
                Debug.LogWarning($"[GameSDK.Ads] Interstitial failed to display: {error}");
                FinishInterstitial(false);
                _interstitial.LoadAd();
            };
            _interstitial.LoadAd();
        }

        private void FinishInterstitial(bool shown)
        {
            Action<bool> callback = _interstitialFinished;
            _interstitialFinished = null;
            callback?.Invoke(shown);
        }

        public void ShowInterstitial(Action<bool> onFinished = null)
        {
            if (_interstitial == null || !_interstitial.IsAdReady() || _interstitialFinished != null)
            {
                Debug.LogWarning("[GameSDK.Ads] Interstitial requested but not ready.");
                onFinished?.Invoke(false);
                return;
            }

            _interstitialFinished = onFinished ?? (_ => { });
            _interstitial.ShowAd();
        }

        // ---------------------------------------------------------------- rewarded

        private void CreateRewardedUnits()
        {
            if (_config.rewardedUnits == null) return;

            foreach (RewardedAdUnit unit in _config.rewardedUnits)
            {
                string id = unit?.Resolve();
                if (unit == null || string.IsNullOrEmpty(unit.key) || string.IsNullOrEmpty(id)) continue;
                if (_rewarded.ContainsKey(unit.key))
                {
                    Debug.LogWarning($"[GameSDK.Ads] Duplicate rewarded unit key '{unit.key}' ignored.");
                    continue;
                }

                var slot = new RewardedSlot { Key = unit.key, Ad = new LevelPlayRewardedAd(id) };
                slot.Ad.OnAdLoaded += _ => slot.Retry = 0;
                slot.Ad.OnAdLoadFailed += error =>
                {
                    Debug.LogWarning($"[GameSDK.Ads] Rewarded '{slot.Key}' failed to load: {error}");
                    RetryLater(ref slot.Retry, () => slot.Ad.LoadAd());
                };
                slot.Ad.OnAdRewarded += (info, reward) => slot.RewardGranted = true;
                slot.Ad.OnAdClosed += _ =>
                {
                    FinishRewarded(slot, slot.RewardGranted);
                    slot.Ad.LoadAd();
                };
                slot.Ad.OnAdDisplayFailed += (_, error) =>
                {
                    Debug.LogWarning($"[GameSDK.Ads] Rewarded '{slot.Key}' failed to display: {error}");
                    OnRewardedFailedToShow?.Invoke(slot.Key);
                    FinishRewarded(slot, false);
                    slot.Ad.LoadAd();
                };

                _rewarded[unit.key] = slot;
                slot.Ad.LoadAd();
            }
        }

        private static void FinishRewarded(RewardedSlot slot, bool granted)
        {
            Action<bool> callback = slot.Pending;
            slot.Pending = null;
            slot.RewardGranted = false;
            callback?.Invoke(granted);
        }

        public void ShowRewarded(string unitKey, Action<bool> onComplete)
        {
            if (unitKey == null || !_rewarded.TryGetValue(unitKey, out RewardedSlot slot))
            {
                Debug.LogWarning($"[GameSDK.Ads] Unknown rewarded unit '{unitKey}'.");
                onComplete?.Invoke(false);
                OnRewardedFailedToShow?.Invoke(unitKey);
                return;
            }

            if (!slot.Ad.IsAdReady() || slot.Pending != null)
            {
                onComplete?.Invoke(false);
                OnRewardedFailedToShow?.Invoke(unitKey);
                return;
            }

            slot.RewardGranted = false;
            slot.Pending = onComplete ?? (_ => { });
            slot.Ad.ShowAd();
        }

        // ---------------------------------------------------------------- banner

        private void CreateBanner()
        {
            string id = _config.banner?.Resolve();
            if (string.IsNullOrEmpty(id)) return;

            // displayOnLoad is off so the game decides when the banner appears (e.g. only from level 7).
            var bannerConfig = new LevelPlayBannerAd.Config.Builder()
                .SetSize(LevelPlayAdSize.BANNER)
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetDisplayOnLoad(false)
                .SetRespectSafeArea(false)
                .Build();

            _banner = new LevelPlayBannerAd(id, bannerConfig);
            _banner.OnAdLoaded += _ =>
            {
                _bannerRetry = 0;
                _bannerLoaded = true;
                ApplyBannerState();
            };
            _banner.OnAdLoadFailed += error =>
            {
                Debug.LogWarning($"[GameSDK.Ads] Banner failed to load: {error}");
                RetryLater(ref _bannerRetry, () => _banner?.LoadAd());
            };
            _banner.LoadAd();
            ApplyBannerState();
        }

        public void ShowBanner()
        {
            _bannerWanted = true;
            ApplyBannerState();
        }

        public void HideBanner()
        {
            _bannerWanted = false;
            ApplyBannerState();
        }

        private void ApplyBannerState()
        {
            if (_banner == null) return;

            bool shouldShow = _bannerWanted && _bannerLoaded;
            if (shouldShow == _bannerShown) return;

            if (shouldShow) _banner.ShowAd();
            else _banner.HideAd();

            _bannerShown = shouldShow;
            OnBannerVisibilityChanged?.Invoke(_bannerShown);
        }

        // ---------------------------------------------------------------- shared

        private void RetryLater(ref int attempt, Action load)
        {
            float delay = RetryDelays[Mathf.Min(attempt, RetryDelays.Length - 1)];
            attempt++;
            if (_host != null) _host.StartCoroutine(RetryRoutine(delay, load));
        }

        private static IEnumerator RetryRoutine(float delay, Action load)
        {
            yield return new WaitForSecondsRealtime(delay);
            load();
        }

        public void Dispose()
        {
            _interstitial?.Dispose();
            foreach (RewardedSlot slot in _rewarded.Values) slot.Ad.Dispose();
            _rewarded.Clear();
            _banner?.DestroyAd();
        }
    }
}
