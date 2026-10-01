# DynamicV Game SDK

Studio-wide Unity SDK wrapping Firebase (Analytics, Auth/Google Sign-In, Crashlytics) and
ad mediation (LevelPlay: AdMob, Meta Audience Network, Unity Ads, and any other LevelPlay
adapter) behind one facade: `DynamicV.GameSDK.GameSDK`.

Game code never touches `Firebase.*` or `Unity.Services.LevelPlay.*` directly - it calls
`GameSDK.Ads`, `GameSDK.Analytics`, `GameSDK.Crash`, `GameSDK.Auth`. Swapping an ad network
or a Firebase implementation detail later is a change inside this package, not a hunt
through every game that uses it.

## What's NOT automatic (and why)

Unity has no way to auto-install Firebase's native Android/iOS dependencies or per-project
OAuth credentials from a UPM package - these two steps are genuinely one-time-per-project
manual work, in every Unity+Firebase project regardless of tooling:

1. **Firebase SDK modules** (Analytics, Auth, Crashlytics, + any others you need) must be
   imported into the consuming project via Firebase's own `.unitypackage` installers (which
   also bring in External Dependency Manager / EDM4U to resolve native Android AARs & iOS
   pods at build time). This package assumes those modules are already present.
2. **`google-services.json`** (Android) / **`GoogleService-Info.plist`** (iOS) for that
   project's own Firebase project must be dropped into `Assets/`.
3. **Google Sign-In web client ID + SHA-1 fingerprint** must be registered in Firebase
   Console for that project's keystore before Google Sign-In will work - a missing/wrong
   SHA-1 fails silently at sign-in time (`InvalidCredential`), not at compile time.

Everything else - wiring the services together, initializing them in the right order,
handling the LevelPlay/Firebase async startup dance, the Google Sign-In native-fragment
race condition, the credential-collision-on-reinstall case - is handled by this package.

## Automatic Firebase install

On first import a **DynamicV Game SDK Setup** window opens (reopen any time via
`DynamicV > Game SDK > Setup Dependencies`; `Install Required Firebase Modules` in the same
menu does it without the window). Firebase's Unity SDK isn't on any UPM registry, so the
installer downloads only the ticked modules (~60 MB each) out of Google's latest official zip
via HTTP range requests, and imports the `.unitypackage` files. It also installs the Google
Sign-In plugin (a separate Google project, not part of Firebase). Firebase brings EDM4U with it.

Optional modules (Cloud Messaging, Realtime Database, Remote Config, Firestore, Cloud Storage) are
ticked in the same window. Messaging and Database have SDK services: `GameSDK.Messaging` and
`GameSDK.Database` (null when the module isn't installed or is disabled in the config).

Three things it does for you that a plain import gets wrong:
- Sets the `DV_FIREBASE_*` / `DV_GOOGLE_SIGNIN` scripting defines. The runtime assembly only
  compiles once all required modules are present, so a fresh import shows no compile errors
  while you're still in the window.
- Wraps `Assets/GoogleSignIn` in an asmdef. The plugin ships loose `.cs` files, which land in
  `Assembly-CSharp`, and a named asmdef assembly can't reference that.
- Deletes `Assets/Parse` (only if the plugin created it). The 2018 plugin bundles
  `Unity.Tasks`/`Unity.Compat` shim DLLs that duplicate types in .NET Standard 2.1 and break
  compilation of Burst, UGUI, Timeline, Input System, etc.

Firebase you imported by hand is detected too. Don't mix this with Firebase installed as UPM
tarballs from a different source.

Still manual: `google-services.json` / `GoogleService-Info.plist` and the SHA-1 / web client ID.

## Per-project setup (minimal path)

1. In the target project's `Packages/manifest.json`, add:
   ```json
   "com.dynamicv.gamesdk": "https://<your-git-remote>/DynamicV-GameSDK.git"
   ```
   (LevelPlay is pulled in automatically as a dependency of this package.)
2. Import the Firebase modules you need (Analytics / Auth / Crashlytics at minimum) via
   Firebase's `.unitypackage` installers, and let EDM4U resolve dependencies.
3. Drop that project's `google-services.json` / `GoogleService-Info.plist` into `Assets/`.
4. `DynamicV > Game SDK > Create Config Asset` - creates
   `Assets/Resources/GameSDKConfig.asset` (must stay at exactly that path; the bootstrapper
   loads it via `Resources.Load`).
5. Fill in the config asset: LevelPlay app key, ad unit IDs, Google web client ID. Toggle
   off anything the project doesn't use (e.g. `adsEnabled = false` for a build with no ads).
6. Done. The SDK initializes itself before the first scene loads - no bootstrap code, no
   scene setup, no singleton wiring required in the consuming project.

## Usage

```csharp
using DynamicV.GameSDK;

GameSDK.Analytics.LogEvent("level_complete", "level", 12);
GameSDK.Crash.Log("entered shop");
GameSDK.Ads.ShowInterstitial();
GameSDK.Ads.ShowRewarded("shop_double_coins", granted =>
{
    if (granted) GrantDoubleCoins();
});

GameSDK.Auth.OnSignInSuccess += (user, isNew) => { /* ... */ };
GameSDK.Auth.SignInWithGoogle();
```

Everything is null until `GameSDK.IsInitialized` is true; subscribe to
`GameSDK.OnInitialized` if you need to act the instant it comes up, otherwise by the
time your first scene's UI is interactive it's normally already ready.

## Package layout

```
Runtime/
  GameSDK.cs               - static facade game code calls into
  Core/                    - bootstrap + init sequencing (internal)
  Config/GameSDKConfig.cs  - per-project ScriptableObject config
  Ads/                     - IAdsService + LevelPlay implementation
  Analytics/               - IAnalyticsService + Firebase implementation
  Crash/                   - ICrashService + Firebase Crashlytics implementation
  Auth/                    - IAuthService + Firebase Auth / Google Sign-In implementation
Editor/                    - config asset creation menu
```

## Extending to another ad/analytics provider

Add a new class implementing `IAdsService` / `IAnalyticsService` / etc. and swap which one
`GameSDKRunner` constructs - the interface is what every game already codes against, so
nothing outside this package needs to change.
