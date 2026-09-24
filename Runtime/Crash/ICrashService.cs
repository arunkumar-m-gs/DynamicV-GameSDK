using System;

namespace DynamicV.GameSDK
{
    public interface ICrashService
    {
        void Log(string message);
        void RecordException(Exception exception);
        void SetCustomKey(string key, string value);
        void SetUserId(string userId);
    }
}
