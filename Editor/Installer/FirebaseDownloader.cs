using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace DynamicV.GameSDK.Installer
{
    // Downloads only the requested .unitypackage files out of Google's latest Firebase Unity SDK
    // zip (~760 MB whole, ~63 MB per module) using HTTP range requests. Runs on a worker thread;
    // the window polls the volatile fields below.
    internal sealed class FirebaseDownloader
    {
        // Google redirects this to the current release zip.
        private const string LatestUrl = "https://firebase.google.com/download/unity";

        public volatile string Status = "";
        public volatile string Version = "";
        public volatile string Error;
        public volatile bool Finished;
        public long BytesDone;
        public long BytesTotal;
        public List<string> Files = new List<string>();

        public static string CacheRoot =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "DynamicV", "FirebaseSDK");

        public Task RunAsync(IReadOnlyList<CatalogEntry> entries)
        {
            return Task.Run(() =>
            {
                try { Run(entries); }
                catch (Exception ex) { Error = ex.Message; }
                finally { Finished = true; }
            });
        }

        private void Run(IReadOnlyList<CatalogEntry> entries)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            {
                foreach (var entry in entries.Where(e => e.Url != null)) DownloadDirect(http, entry);

                var packageFiles = entries.Where(e => e.Url == null).Select(e => e.PackageFile).ToList();
                if (packageFiles.Count > 0) DownloadFromFirebaseZip(http, packageFiles);
                Status = "Download complete.";
            }
        }

        private void DownloadDirect(HttpClient http, CatalogEntry entry)
        {
            var dir = Path.Combine(CacheRoot, "direct");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, entry.PackageFile);
            Files.Add(path);
            if (File.Exists(path)) return;

            Status = $"Downloading {entry.PackageFile}...";
            var part = path + ".part";
            using (var resp = http.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
            {
                resp.EnsureSuccessStatusCode();
                BytesTotal += resp.Content.Headers.ContentLength ?? 0;
                using (var src = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                using (var dst = File.Create(part))
                {
                    var buf = new byte[64 * 1024];
                    int n;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        dst.Write(buf, 0, n);
                        BytesDone += n;
                    }
                }
            }
            File.Move(part, path);
        }

        private void DownloadFromFirebaseZip(HttpClient http, IReadOnlyList<string> packageFiles)
        {
            {
                Status = "Finding latest Firebase Unity SDK...";
                Uri zipUrl;
                long zipLength;
                using (var req = new HttpRequestMessage(HttpMethod.Get, LatestUrl))
                {
                    req.Headers.Range = new RangeHeaderValue(0, 0);
                    using (var resp = http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                    {
                        resp.EnsureSuccessStatusCode();
                        zipUrl = resp.RequestMessage.RequestUri;
                        zipLength = resp.Content.Headers.ContentRange?.Length ?? resp.Content.Headers.ContentLength ?? 0;
                    }
                }
                if (zipLength <= 0) throw new InvalidOperationException("Could not determine Firebase SDK size.");

                var m = Regex.Match(zipUrl.AbsolutePath, @"firebase_unity_sdk_(\d+(\.\d+)*)\.zip");
                Version = m.Success ? m.Groups[1].Value : "latest";

                var dir = Path.Combine(CacheRoot, Version);
                Directory.CreateDirectory(dir);

                using (var stream = new HttpRangeStream(http, zipUrl, zipLength))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var wanted = new List<(ZipArchiveEntry entry, string path)>();
                    foreach (var file in packageFiles)
                    {
                        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + file, StringComparison.OrdinalIgnoreCase));
                        if (entry == null) throw new FileNotFoundException(file + " not found in Firebase SDK " + Version);
                        var path = Path.Combine(dir, file);
                        wanted.Add((entry, path));
                    }

                    BytesTotal += wanted.Where(w => !File.Exists(w.path)).Sum(w => w.entry.Length);

                    foreach (var (entry, path) in wanted)
                    {
                        Files.Add(path);
                        if (File.Exists(path)) continue; // cached from a previous run

                        Status = $"Downloading {entry.Name} ({Version})...";
                        var part = path + ".part";
                        using (var src = entry.Open())
                        using (var dst = File.Create(part))
                        {
                            var buf = new byte[256 * 1024];
                            int n;
                            while ((n = src.Read(buf, 0, buf.Length)) > 0)
                            {
                                dst.Write(buf, 0, n);
                                BytesDone += n;
                            }
                        }
                        File.Move(part, path);
                    }
                }
            }
        }
    }
}
