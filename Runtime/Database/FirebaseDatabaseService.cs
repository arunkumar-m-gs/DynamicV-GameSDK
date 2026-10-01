#if DV_FIREBASE_DATABASE
using System.Threading.Tasks;
using Firebase.Database;

namespace DynamicV.GameSDK
{
    public class FirebaseDatabaseService : IDatabaseService
    {
        private readonly DatabaseReference _root = FirebaseDatabase.DefaultInstance.RootReference;

        public Task SetJsonAsync(string path, string json) =>
            _root.Child(path).SetRawJsonValueAsync(json);

        public async Task<string> GetJsonAsync(string path)
        {
            var snapshot = await _root.Child(path).GetValueAsync();
            return snapshot.Exists ? snapshot.GetRawJsonValue() : null;
        }

        public Task RemoveAsync(string path) => _root.Child(path).RemoveValueAsync();
    }
}
#endif
