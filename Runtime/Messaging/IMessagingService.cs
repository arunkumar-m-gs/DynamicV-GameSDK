using System;

namespace DynamicV.GameSDK
{
    public interface IMessagingService
    {
        /// <summary>FCM registration token, or null until it has been issued.</summary>
        string Token { get; }

        event Action<string> OnTokenReceived;
        event Action<string, string> OnMessageReceived; // title, body

        void SubscribeToTopic(string topic);
        void UnsubscribeFromTopic(string topic);
    }
}
