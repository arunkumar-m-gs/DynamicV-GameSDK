using System.Collections.Generic;
using Firebase.Analytics;

namespace DynamicV.GameSDK
{
    public class FirebaseAnalyticsService : IAnalyticsService
    {
        public FirebaseAnalyticsService()
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
        }

        public void LogEvent(string eventName) => FirebaseAnalytics.LogEvent(eventName);

        public void LogEvent(string eventName, string paramName, string paramValue) =>
            FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);

        public void LogEvent(string eventName, string paramName, long paramValue) =>
            FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);

        public void LogEvent(string eventName, string paramName, double paramValue) =>
            FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);

        public void LogEvent(string eventName, IDictionary<string, object> parameters)
        {
            var list = new List<Parameter>(parameters.Count);
            foreach (var kv in parameters)
            {
                switch (kv.Value)
                {
                    case string s: list.Add(new Parameter(kv.Key, s)); break;
                    case int i: list.Add(new Parameter(kv.Key, i)); break;
                    case long l: list.Add(new Parameter(kv.Key, l)); break;
                    case float f: list.Add(new Parameter(kv.Key, f)); break;
                    case double d: list.Add(new Parameter(kv.Key, d)); break;
                    default: list.Add(new Parameter(kv.Key, kv.Value?.ToString() ?? string.Empty)); break;
                }
            }

            FirebaseAnalytics.LogEvent(eventName, list.ToArray());
        }

        public void SetUserProperty(string name, string value) =>
            FirebaseAnalytics.SetUserProperty(name, value);
    }
}
