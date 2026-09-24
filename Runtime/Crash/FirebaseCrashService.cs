using System;
using Firebase.Crashlytics;

namespace DynamicV.GameSDK
{
    public class FirebaseCrashService : ICrashService
    {
        public FirebaseCrashService()
        {
            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            Crashlytics.IsCrashlyticsCollectionEnabled = true;
        }

        public void Log(string message) => Crashlytics.Log(message);
        public void RecordException(Exception exception) => Crashlytics.LogException(exception);
        public void SetCustomKey(string key, string value) => Crashlytics.SetCustomKey(key, value);
        public void SetUserId(string userId) => Crashlytics.SetUserId(userId);
    }
}
