using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace DynamicV.GameSDK.Installer
{
    // Installs LevelPlay's native SDK by running the same code path as the button in
    // Unity Services > Ads Mediation > Network Manager. That code is internal to the LevelPlay
    // package, so it is reached by reflection; if a LevelPlay update renames it, the task faults
    // with a message and the manual route (open the Network Manager) still works.
    internal static class LevelPlayInstaller
    {
        private const string WindowTypeName = "LevelPlayDependenciesManager";
        private const string InstallMethodName = "DownloadSdkAction";

        public static Task InstallAsync()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException) { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == WindowTypeName);
            if (type == null)
                return Fail("LevelPlay package (com.unity.services.levelplay) not found.");

            const BindingFlags all = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            type.GetMethod("ShowDependenciesManager", all)?.Invoke(null, null);
            var window = Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
            var install = type.GetMethod(InstallMethodName, all);
            if (window == null || install == null)
                return Fail("LevelPlay Network Manager API changed; install the SDK from Unity Services > Ads Mediation > Network Manager.");

            return (Task)install.Invoke(window, null);
        }

        private static Task Fail(string message) => Task.FromException(new InvalidOperationException(message));
    }
}
