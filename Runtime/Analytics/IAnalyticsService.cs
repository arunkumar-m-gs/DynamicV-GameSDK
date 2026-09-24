using System.Collections.Generic;

namespace DynamicV.GameSDK
{
    public interface IAnalyticsService
    {
        void LogEvent(string eventName);
        void LogEvent(string eventName, string paramName, string paramValue);
        void LogEvent(string eventName, string paramName, long paramValue);
        void LogEvent(string eventName, string paramName, double paramValue);
        void LogEvent(string eventName, IDictionary<string, object> parameters);
        void SetUserProperty(string name, string value);
    }
}
