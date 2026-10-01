using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace DynamicV.GameSDK.Installer
{
    internal sealed class SetupWindow : EditorWindow
    {
        private readonly Dictionary<string, bool> _selected = new Dictionary<string, bool>();
        private FirebaseDownloader _download;
        private Task _downloadTask;
        private List<CatalogEntry> _importing = new List<CatalogEntry>();
        private Task _levelPlayTask;
        private string _status = "Ready.";
        private Vector2 _scroll;

        [MenuItem("DynamicV/Game SDK/Setup Dependencies", priority = -10)]
        public static void Open()
        {
            var window = GetWindow<SetupWindow>(true, "DynamicV Game SDK Setup");
            window.minSize = new Vector2(440, 400);
            window.Show();
        }

        [MenuItem("DynamicV/Game SDK/Install Required Firebase Modules", priority = -9)]
        private static void InstallRequired()
        {
            Open();
            var window = GetWindow<SetupWindow>();
            if (window._download != null) return;
            var missing = PackageCatalog.Entries.Where(e => e.Required && !FirebaseState.IsInstalled(e)).ToList();
            if (missing.Count > 0) window.Install(missing);
        }

        private void OnEnable()
        {
            foreach (var e in PackageCatalog.Entries)
                if (!_selected.ContainsKey(e.PackageFile)) _selected[e.PackageFile] = e.Required;
            EditorApplication.update += Tick;
        }

        private void OnDisable() => EditorApplication.update -= Tick;

        private void Tick()
        {
            if (_levelPlayTask != null && _levelPlayTask.IsCompleted) FinishLevelPlay();
            if (_download == null) return;
            if (_download.Finished) { FinishDownload(); return; }
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Firebase modules", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Downloads the latest Firebase Unity SDK straight from Google (only the modules you tick, " +
                "~60 MB each) and imports them. Required modules are needed for the Game SDK to compile.",
                MessageType.Info);

            var busy = _download != null || _levelPlayTask != null;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var e in PackageCatalog.Entries)
            {
                var installed = FirebaseState.IsInstalled(e);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(installed || e.Required || busy))
                    {
                        _selected[e.PackageFile] = EditorGUILayout.ToggleLeft(
                            e.Label + (e.Required ? " (required)" : ""), installed || _selected[e.PackageFile]);
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(installed ? "Installed" : "Not installed", GUILayout.Width(90));
                }
            }
            EditorGUILayout.EndScrollView();

            if (busy)
            {
                var total = System.Math.Max(1, _download.BytesTotal);
                var rect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(rect, Mathf.Clamp01((float)_download.BytesDone / total),
                    $"{_download.BytesDone / 1048576} / {_download.BytesTotal / 1048576} MB");
                EditorGUILayout.LabelField(_download.Status, EditorStyles.miniLabel);
            }

            var pending = PackageCatalog.Entries
                .Where(e => _selected[e.PackageFile] && !FirebaseState.IsInstalled(e)).ToList();
            using (new EditorGUI.DisabledScope(pending.Count == 0 || busy))
            {
                var label = pending.Count == 0 ? "Everything selected is installed" : $"Download & install {pending.Count} module(s)";
                if (GUILayout.Button(label, GUILayout.Height(30))) Install(pending);
            }
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        private void Install(List<CatalogEntry> pending)
        {
            _status = "Starting...";
            _importing = pending;
            var downloads = pending.Where(e => !e.ViaLevelPlayManager).ToList();
            if (downloads.Count == 0) { StartLevelPlay(); return; }
            _download = new FirebaseDownloader();
            _downloadTask = _download.RunAsync(downloads);
        }

        private bool WantsLevelPlay => _importing.Any(e => e.ViaLevelPlayManager);

        private void StartLevelPlay()
        {
            if (!WantsLevelPlay) return;
            _status = "Installing LevelPlay native SDK...";
            _levelPlayTask = LevelPlayInstaller.InstallAsync();
        }

        private void FinishLevelPlay()
        {
            var t = _levelPlayTask;
            _levelPlayTask = null;
            if (t.IsFaulted)
            {
                _status = "LevelPlay install failed: " + t.Exception.GetBaseException().Message;
                Debug.LogError("[GameSDK] " + _status);
            }
            else
            {
                AssetDatabase.Refresh();
                FirebaseState.SyncDefines();
                _status = "LevelPlay native SDK installed. Run Assets > External Dependency Manager > Android Resolver > Force Resolve.";
            }
            Repaint();
        }

        private void FinishDownload()
        {
            var d = _download;
            _download = null;
            _downloadTask = null;

            if (d.Error != null)
            {
                _status = "Download failed: " + d.Error;
                Debug.LogError("[GameSDK] Firebase download failed: " + d.Error);
                Repaint();
                return;
            }

            _status = "Importing (Unity will recompile)...";

            // Only clean up folders a package created; never touch one that was already in the project.
            var toRemove = _importing
                .Where(e => e.RemoveAfterImport != null && !AssetDatabase.IsValidFolder("Assets/" + e.RemoveAfterImport))
                .Select(e => "Assets/" + e.RemoveAfterImport).ToList();

            foreach (var file in d.Files)
                AssetDatabase.ImportPackage(file, false);

            foreach (var folder in toRemove)
                if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.DeleteAsset(folder))
                    Debug.Log($"[GameSDK] Removed {folder} (incompatible legacy shim DLLs bundled with a plugin).");

            FirebaseState.SyncDefines();
            _status = "Imported. Next: DynamicV > Game SDK > Create Config Asset.";
            StartLevelPlay();
            Repaint();
        }
    }
}
