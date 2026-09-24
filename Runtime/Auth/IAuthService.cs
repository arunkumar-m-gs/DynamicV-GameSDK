using System;
using Firebase.Auth;

namespace DynamicV.GameSDK
{
    public interface IAuthService
    {
        /// <summary>True only for a real (Google-linked) account; an anonymous session is signed in too, so this is the check UI should use.</summary>
        bool IsSignedInWithGoogle { get; }
        FirebaseUser CurrentUser { get; }

        void SignInWithGoogle();
        void SignOut();

        event Action<FirebaseUser, bool> OnSignInSuccess;
        event Action<string> OnSignInFailed;
        event Action OnSignOut;
        event Action OnAuthStateChanged;
    }
}
