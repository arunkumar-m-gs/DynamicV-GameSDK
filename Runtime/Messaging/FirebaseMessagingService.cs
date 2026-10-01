#if DV_FIREBASE_MESSAGING
using System;
using Firebase.Messaging;

namespace DynamicV.GameSDK
{
    public class FirebaseMessagingService : IMessagingService
    {
        public string Token { get; private set; }

        public event Action<string> OnTokenReceived;
        public event Action<string, string> OnMessageReceived;

        public FirebaseMessagingService()
        {
            FirebaseMessaging.TokenReceived += (_, e) =>
            {
                Token = e.Token;
                OnTokenReceived?.Invoke(e.Token);
            };
            FirebaseMessaging.MessageReceived += (_, e) =>
                OnMessageReceived?.Invoke(e.Message.Notification?.Title, e.Message.Notification?.Body);
        }

        public void SubscribeToTopic(string topic) => FirebaseMessaging.SubscribeAsync(topic);
        public void UnsubscribeFromTopic(string topic) => FirebaseMessaging.UnsubscribeAsync(topic);
    }
}
#endif
