using System.Collections.Generic;

namespace DynamicV.GameSDK.Installer
{
    internal sealed class CatalogEntry
    {
        public string Label;
        public bool Required;
        // Name of the .unitypackage inside Google's Firebase Unity SDK zip. Also the entry's key.
        public string PackageFile;
        // Set for packages that aren't in the Firebase zip: downloaded whole from this URL instead.
        public string Url;
        // Managed assembly the package installs; presence means "installed".
        public string Dll;
        // Alternative to Dll: a folder under Assets/ whose presence means "installed".
        public string DetectFolder;
        // Folder under Assets/ the package ships that breaks modern Unity (see the Google Sign-In
        // entry). Removed after import, but only if it didn't exist before we imported.
        public string RemoveAfterImport;
        // Scripting define the runtime assembly is gated on (set for every module so optional ones are detected too; only required ones gate compilation).
        public string Define;
    }

    // Firebase's Unity SDK is only published as one zip of .unitypackage files (no UPM registry),
    // so this list is keyed by the file names inside that zip. Add a row to surface another module.
    internal static class PackageCatalog
    {
        public static readonly IReadOnlyList<CatalogEntry> Entries = new List<CatalogEntry>
        {
            // FirebaseAuthService uses this plugin for the native Google account picker. It is a
            // separate Google project (googlesamples/google-signin-unity), not part of Firebase.
            new CatalogEntry
            {
                Label = "Google Sign-In plugin", Required = true,
                PackageFile = "google-signin-plugin-1.0.4.unitypackage",
                Url = "https://github.com/googlesamples/google-signin-unity/releases/download/v1.0.4/google-signin-plugin-1.0.4.unitypackage",
                DetectFolder = "GoogleSignIn", Define = "DV_GOOGLE_SIGNIN",
                // The 2018 plugin bundles Unity.Tasks/Unity.Compat shim DLLs (Assets/Parse) that
                // duplicate types now in .NET Standard 2.1 and break compilation of every package
                // using Task/Tuple (CS0433/CS0121 in Burst, UGUI, Timeline, Input System...).
                RemoveAfterImport = "Parse",
            },
            new CatalogEntry { Label = "Analytics", Required = true, PackageFile = "FirebaseAnalytics.unitypackage", Dll = "Firebase.Analytics.dll", Define = "DV_FIREBASE_ANALYTICS" },
            new CatalogEntry { Label = "Authentication (Google Sign-In)", Required = true, PackageFile = "FirebaseAuth.unitypackage", Dll = "Firebase.Auth.dll", Define = "DV_FIREBASE_AUTH" },
            new CatalogEntry { Label = "Crashlytics", Required = true, PackageFile = "FirebaseCrashlytics.unitypackage", Dll = "Firebase.Crashlytics.dll", Define = "DV_FIREBASE_CRASHLYTICS" },
            new CatalogEntry { Label = "Remote Config", PackageFile = "FirebaseRemoteConfig.unitypackage", Dll = "Firebase.RemoteConfig.dll", Define = "DV_FIREBASE_REMOTECONFIG" },
            new CatalogEntry { Label = "Cloud Messaging", PackageFile = "FirebaseMessaging.unitypackage", Dll = "Firebase.Messaging.dll", Define = "DV_FIREBASE_MESSAGING" },
            new CatalogEntry { Label = "Firestore", PackageFile = "FirebaseFirestore.unitypackage", Dll = "Firebase.Firestore.dll", Define = "DV_FIREBASE_FIRESTORE" },
            new CatalogEntry { Label = "Realtime Database", PackageFile = "FirebaseDatabase.unitypackage", Dll = "Firebase.Database.dll", Define = "DV_FIREBASE_DATABASE" },
            new CatalogEntry { Label = "Cloud Storage", PackageFile = "FirebaseStorage.unitypackage", Dll = "Firebase.Storage.dll", Define = "DV_FIREBASE_STORAGE" },
        };
    }
}
