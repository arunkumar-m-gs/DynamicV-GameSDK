using Firebase;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;
using Google;

namespace DynamicV.GameSDK
{
    /// <summary>
    /// Google/Firebase auth wrapper. Must live on a MonoBehaviour (StartCoroutine, OnApplicationFocus),
    /// so GameSDKRunner adds it as a component rather than constructing it directly like the other services.
    /// </summary>
    public class FirebaseAuthService : MonoBehaviour, IAuthService
    {
        // How long to wait for the Google account picker to appear before declaring the request lost.
        // Only ever evaluated while the game still holds focus, so a slow player at the picker is
        // never affected by it -- see PickerWatchdog.
        private const float PickerWatchdogSeconds = 12f;

        private GameSDKConfig _config;
        private FirebaseAuth _auth;
        private FirebaseUser _currentUser;
        private bool _googleConfigured;
        private bool _signInInFlight;
        private bool _lostFocusDuringSignIn;

        public event Action<FirebaseUser, bool> OnSignInSuccess;
        public event Action<string> OnSignInFailed;
        public event Action OnSignOut;
        public event Action OnAuthStateChanged;

        public FirebaseUser CurrentUser => _currentUser;

        /// <summary>True only for a real (Google-linked) account. An anonymous session is still a signed-in
        /// FirebaseUser, so CurrentUser != null on its own is not enough to decide UI state.</summary>
        public bool IsSignedInWithGoogle =>
            _auth != null && _auth.CurrentUser != null && !_auth.CurrentUser.IsAnonymous;

        internal void Initialize(GameSDKConfig config)
        {
            _config = config;
            _auth = FirebaseAuth.DefaultInstance;
            _auth.StateChanged += AuthStateChanged;

            WarmUpGoogleSignIn();

            if (_config.autoSignInAnonymously && _auth.CurrentUser == null)
            {
                SignInAnonymously();
            }
        }

        /// <summary>
        /// Creates the GoogleSignIn singleton at boot so its Android fragment is attached and READY
        /// long before the player can tap Sign In. This is the fix for the intermittent "SIGNING IN..."
        /// hang, and it must not be moved into the sign-in path itself.
        /// </summary>
        /// <remarks>
        /// A single C# SignIn() produces TWO native configure() calls: one from GoogleSignInImpl's
        /// constructor, and one from inside SignIn() itself. Each builds a TokenRequest and hands it to
        /// GoogleSignInFragment.submitRequest(), which only accepts a replacement when the fragment
        /// holds no request or has reached State.READY. The fragment is attached with
        /// commitAllowingStateLoss() and reaches READY only when Android delivers onResume on the UI
        /// thread, so on a cold start both calls land (~10ms apart) while state is still null. The
        /// second is rejected -- and it is the one whose handle backs the Task we await, so that Task
        /// never completes: no account picker, no callback, not even a faulted task. The fragment is
        /// then parked in PENDING, which rejects every later attempt for the rest of the process.
        ///
        /// Doing the first configure() here, seconds ahead of any tap, means the fragment is READY by
        /// the time the real request arrives, so it replaces cleanly and keeps the correct handle.
        /// </remarks>
        private void WarmUpGoogleSignIn()
        {
            try
            {
                EnsureGoogleConfigured();
                GoogleSignIn instance = GoogleSignIn.DefaultInstance;
                Debug.Log($"[GameSDK.Auth] Google Sign-In warmed up ({(instance != null ? "ok" : "null")}).");
            }
            catch (Exception ex)
            {
                // Boot must survive a missing/broken native plugin; SignInWithGoogle re-checks anyway.
                Debug.LogWarning($"[GameSDK.Auth] Google Sign-In warm-up skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the GoogleSignIn configuration exactly once per app run. The plugin throws if
        /// Configuration is reassigned after its lazy singleton has been created.
        /// </summary>
        private void EnsureGoogleConfigured()
        {
            if (_googleConfigured) return;

            if (string.IsNullOrEmpty(_config.googleWebClientId))
            {
                Debug.LogError("[GameSDK.Auth] No Google web client id set on GameSDKConfig; Google Sign-In is disabled.");
                return;
            }

            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId = _config.googleWebClientId,
                RequestIdToken = true
            };
            _googleConfigured = true;
        }

        private void AuthStateChanged(object sender, EventArgs e)
        {
            if (_auth != null && _auth.CurrentUser != _currentUser)
            {
                _currentUser = _auth.CurrentUser;
            }

            // Firebase raises StateChanged once on subscribe, so this also covers the cold-start case
            // where a persisted Google session is restored after the home screen has already built.
            OnAuthStateChanged?.Invoke();
        }

        private async void SignInAnonymously()
        {
            try
            {
                var authResult = await _auth.SignInAnonymouslyAsync();
                Debug.Log($"[GameSDK.Auth] Signed in anonymously: {authResult.User.UserId}");
            }
            catch (Exception ex)
            {
                int? code = null;
                for (var e = ex; e != null; e = e.InnerException) if (e is FirebaseException fe) { code = fe.ErrorCode; break; }
                string decoded = code.HasValue ? ((AuthError)code.Value).ToString() : "not-firebase";
                Debug.LogError($"[GameSDK.Auth] Anonymous Auth Error [{ex.GetType().Name}/{decoded}/{code}]: {ex.Message}");
            }
        }

        public void SignInWithGoogle()
        {
            // The button is disabled on click, but a second entry from anywhere else would submit a
            // competing request to the shared fragment and re-create the collision described in
            // WarmUpGoogleSignIn.
            if (_signInInFlight)
            {
                Debug.LogWarning("[GameSDK.Auth] Google Sign-In already in progress; ignoring request.");
                return;
            }

            try
            {
                EnsureGoogleConfigured();

                if (!_googleConfigured)
                {
                    OnSignInFailed?.Invoke("Google Sign-In is not configured for this build.");
                    return;
                }

                _signInInFlight = true;
                _lostFocusDuringSignIn = false;
                StartCoroutine(PickerWatchdog());

                // Capture Unity's main-thread synchronization context while we are still on it.
                // The native GoogleSignIn plugin completes this task on a background thread; without
                // forcing the continuation back onto the main thread, every downstream call (Firebase
                // auth calls, and any UI touched by OnSignInSuccess/OnSignInFailed) runs off-thread.
                // Unity engine API calls from a non-main thread are undefined behavior on device.
                var mainThread = TaskScheduler.FromCurrentSynchronizationContext();

                GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
                {
                    _signInInFlight = false;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        string reason = task.IsFaulted && task.Exception != null
                            ? task.Exception.GetBaseException().Message
                            : "canceled";
                        Debug.LogWarning($"[GameSDK.Auth] Google Sign-In failed: {reason}");
                        OnSignInFailed?.Invoke("Google Sign-In failed or canceled.");
                        return;
                    }

                    Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
                    AuthenticateWithFirebase(credential);
                }, mainThread);
            }
            catch (Exception ex)
            {
                // Guards against synchronous failures from the native plugin itself (e.g. the
                // GoogleSignIn native library failing to load on this device/OS build) so the
                // Sign-In button always gets reset instead of getting stuck on "SIGNING IN...".
                _signInInFlight = false;
                Debug.LogError($"[GameSDK.Auth] Google Sign-In could not start: {ex.Message}");
                OnSignInFailed?.Invoke("Google Sign-In is unavailable on this device.");
            }
        }

        /// <summary>
        /// Detaches the Google account from this device. Local progress is deliberately left intact --
        /// only the cloud link is broken -- and the session drops back to anonymous, which is the same
        /// state a fresh install boots into.
        /// </summary>
        public void SignOut()
        {
            // Signing out mid-request would strand the in-flight task and leave the shared
            // GoogleSignInFragment in PENDING, which rejects every later attempt (see WarmUpGoogleSignIn).
            if (_signInInFlight)
            {
                Debug.LogWarning("[GameSDK.Auth] Sign-in in progress; ignoring sign-out request.");
                return;
            }

            try
            {
                // Clears the cached Google account so the next SignIn() shows the picker again rather
                // than silently re-authenticating the account the player just logged out of.
                GoogleSignIn.DefaultInstance.SignOut();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSDK.Auth] Google sign-out failed: {ex.Message}");
            }

            try
            {
                _auth?.SignOut();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSDK.Auth] Firebase sign-out failed: {ex.Message}");
            }

            _currentUser = null;
            Debug.Log("[GameSDK.Auth] Signed out.");

            OnSignOut?.Invoke();

            // Mirror what a cold boot does when there is no user, so the rest of the game stays in a
            // state it already understands.
            if (_config.autoSignInAnonymously && _auth != null && _auth.CurrentUser == null) SignInAnonymously();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // The account picker is a separate activity, so a request that actually reached Google
            // always costs us focus. Recording that is what lets the watchdog tell "the player is
            // still choosing an account" apart from "the request was swallowed".
            if (!hasFocus && _signInInFlight) _lostFocusDuringSignIn = true;
        }

        /// <summary>
        /// Safety net for the swallowed-request case that WarmUpGoogleSignIn is meant to prevent. If
        /// the picker never appeared, no callback is ever coming, so reset the UI and clear the
        /// plugin's stuck state rather than leaving the button dead for the rest of the session.
        /// </summary>
        private IEnumerator PickerWatchdog()
        {
            yield return new WaitForSecondsRealtime(PickerWatchdogSeconds);

            if (!_signInInFlight || _lostFocusDuringSignIn) yield break;

            _signInInFlight = false;
            Debug.LogError("[GameSDK.Auth] Google Sign-In never opened the account picker; recovering.");

            try
            {
                GoogleSignIn.DefaultInstance.SignOut();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameSDK.Auth] Could not reset Google Sign-In state: {ex.Message}");
            }

            OnSignInFailed?.Invoke("Google Sign-In did not start. Please try again.");
        }

        private async void AuthenticateWithFirebase(Credential credential)
        {
            try
            {
                if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
                {
                    // SDK 13.14.0 quirk: LinkWithCredentialAsync returns an AuthResult container.
                    AuthResult linkResult = await _auth.CurrentUser.LinkWithCredentialAsync(credential);
                    HandlePostLogin(linkResult.User, isNewUser: true);
                    return;
                }

                // SDK 13.14.0 quirk: SignInWithCredentialAsync returns the FirebaseUser directly.
                FirebaseUser user = await _auth.SignInWithCredentialAsync(credential);
                bool isNew = user.Metadata != null && (user.Metadata.CreationTimestamp == user.Metadata.LastSignInTimestamp);
                HandlePostLogin(user, isNew);
            }
            catch (Exception ex)
            {
                // Credential collision: this Google account already belongs to a real Firebase user.
                // Happens on every reinstall -- the fresh run creates a throwaway anonymous user, and
                // the returning player's account cannot be linked onto it. Sign in to the existing
                // account instead so their progress is fetched back down, and never report it as a
                // new signup (isNewUser: true would make a save-sync layer push this install's empty
                // save over their real cloud data).
                if (IsCredentialAlreadyRegistered(ex))
                {
                    Debug.LogWarning("[GameSDK.Auth] Credential already registered. Switching to standard sign-in...");
                    try
                    {
                        FirebaseUser user = await _auth.SignInWithCredentialAsync(credential);
                        HandlePostLogin(user, isNewUser: false);
                    }
                    catch (Exception signInEx)
                    {
                        // Without its own guard this would escape an async void method and take the
                        // process down instead of just resetting the Sign-In button.
                        Debug.LogError($"[GameSDK.Auth] Fallback sign-in failed: {signInEx.Message}");
                        OnSignInFailed?.Invoke($"Firebase Auth Error: {signInEx.Message}");
                    }
                }
                else
                {
                    int? code = GetAuthErrorCode(ex);
                    string decoded = code.HasValue ? ((AuthError)code.Value).ToString() : "not-a-firebase-auth-error";
                    Debug.LogError($"[GameSDK.Auth] Firebase Auth Error [{ex.GetType().Name}/{decoded}]: {ex.Message}");
                    OnSignInFailed?.Invoke($"Firebase Auth Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// True when a link/sign-in failed only because the credential is already attached to an
        /// existing Firebase account, meaning the correct recovery is to sign in as that account.
        /// </summary>
        private static bool IsCredentialAlreadyRegistered(Exception ex)
        {
            int? code = GetAuthErrorCode(ex);
            if (!code.HasValue) return false;

            switch ((AuthError)code.Value)
            {
                case AuthError.CredentialAlreadyInUse:
                case AuthError.EmailAlreadyInUse:
                case AuthError.AccountExistsWithDifferentCredentials:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Digs the Firebase AuthError code out of whatever the SDK threw, or null if it is not a
        /// Firebase auth failure at all.
        /// </summary>
        private static int? GetAuthErrorCode(Exception ex)
        {
            // Two distinct types have to be matched here:
            //   * LinkWithCredentialAsync throws FirebaseAccountLinkException, which derives straight
            //     from System.Exception and is NOT a FirebaseException.
            //   * SignInWithCredentialAsync throws FirebaseException.
            // 'await' unwraps the task's AggregateException, so the real exception is usually 'ex'
            // itself, but walk the chain anyway for failures surfaced from a nested task.
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is FirebaseAccountLinkException linkEx) return linkEx.ErrorCode;
                if (e is FirebaseException fbEx) return fbEx.ErrorCode;
            }

            return null;
        }

        private void HandlePostLogin(FirebaseUser user, bool isNewUser)
        {
            _currentUser = user;
            Debug.Log($"[GameSDK.Auth] User authenticated: {user.UserId}, IsNew: {isNewUser}");
            OnSignInSuccess?.Invoke(user, isNewUser);
        }
    }
}
