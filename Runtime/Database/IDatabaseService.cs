using System.Threading.Tasks;

namespace DynamicV.GameSDK
{
    public interface IDatabaseService
    {
        /// <summary>Writes a JSON string at the given path (e.g. "users/abc/score").</summary>
        Task SetJsonAsync(string path, string json);

        /// <summary>Reads the value at the path as JSON, or null if it doesn't exist.</summary>
        Task<string> GetJsonAsync(string path);

        Task RemoveAsync(string path);
    }
}
