using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using WoWRegeneration.Core;
using WoWRegeneration.Repositories;

namespace WoWRegeneration.Data
{
    public class ManifestFile
    {
        private const string LocalePrefix = "locale_";

        private static readonly string[] NonLocaleTags =
        {
            "base", "OSX", "Win", "ALT", "EXP1", "EXP2", "EXP3", "EXP4"
        };

        private ManifestFile()
        {
            Entries = new List<Entry>();
            Locales = new List<string>();
        }

        public int Version { get; private set; }
        private List<Entry> Entries { get; set; }
        private List<string> Locales { get; set; }

        public List<string> GetLocales()
        {
            return Locales;
        }

        public static ManifestFile FromRepository(IWoWRepository repository)
        {
            try
            {
                using (var client = new WebClient())
                {
                    string content = client.DownloadString(repository.GetBaseUrl() + repository.GetMFilName());
                    return Parse(content, repository.GetBaseUrl());
                }
            }
            catch (Exception ex)
            {
                AppEnvironment.Log("Unable to retrieve manifest file", LogLevel.Error);
                AppEnvironment.Log(ex.Message, LogLevel.Error);
                return null;
            }
        }

        public static ManifestFile Parse(string content, string baseUrl)
        {
            var manifest = new ManifestFile();
            Entry current = null;

            foreach (string raw in content.Split('\n'))
            {
                string line = raw.Trim();
                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);

                switch (key)
                {
                    case "version":
                        int version;
                        if (int.TryParse(value, out version))
                            manifest.Version = version;
                        break;

                    case "serverpath":
                        current = null;
                        if (manifest.Version == 2 && value.StartsWith(LocalePrefix))
                            manifest.AddLocale(value.Substring(LocalePrefix.Length));
                        break;

                    case "file":
                        current = new Entry { RemotePath = value, Name = value, Url = baseUrl + value };
                        manifest.Entries.Add(current);
                        break;

                    case "name":
                        if (current != null)
                            current.Name = value;
                        break;

                    case "size":
                        long size;
                        if (current != null && long.TryParse(value, out size))
                            current.Size = size;
                        break;

                    case "path":
                        if (current != null && value.StartsWith(LocalePrefix))
                            current.Locale = value.Substring(LocalePrefix.Length);
                        break;

                    case "tag":
                        if (manifest.Version == 3 && !NonLocaleTags.Contains(value))
                            manifest.AddLocale(value);
                        break;
                }
            }

            return manifest;
        }

        public List<FileObject> GenerateFileList()
        {
            IWoWRepository repository = RepositoriesManager.GetRepositoryByMfil(Session.Current.MFil);
            string basePath = AppEnvironment.ExecutionPath + repository.GetDefaultDirectory();

            var candidates = new List<FileObject>();
            foreach (Entry entry in Entries)
            {
                if (!entry.IsFile)
                    continue;

                var file = new FileObject
                {
                    Url = entry.Url,
                    Path = entry.Name,
                    Info = entry.Locale,
                    Size = entry.Size
                };

                if (IsAcceptedFile(file))
                    candidates.Add(file);
            }

            var files = new List<FileObject>();
            int skipped = 0;
            bool sessionChanged = false;

            foreach (IGrouping<string, FileObject> group in
                candidates.GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                FileObject file = PickVariant(group);
                if (file == null)
                    continue;

                if (IsAlreadyDownloaded(basePath, file, ref sessionChanged))
                {
                    skipped++;
                    continue;
                }

                files.Add(file);
            }

            if (sessionChanged)
                Session.Current.Save();
            if (skipped > 0)
                AppEnvironment.Log("Skipping " + skipped + " file(s) already downloaded", LogLevel.Detail);

            return files;
        }

        /// <summary>
        ///     A path listed several times carries one block per locale, so the locale decides.
        ///     A path listed once is taken as is: shared files such as Data/Interface/Cinematics
        ///     are tagged with an arbitrary locale in the 4.3.4 manifest but belong to every client.
        /// </summary>
        private static FileObject PickVariant(IEnumerable<FileObject> variants)
        {
            var list = variants.ToList();
            if (list.Count == 1)
                return list[0];

            return list.FirstOrDefault(item => item.Info == Session.Current.Locale) ??
                   list.FirstOrDefault(item => item.Info == null);
        }

        private static bool IsAcceptedFile(FileObject file)
        {
            Session session = Session.Current;

            if (session.Os == "Win" && file.Filename == "base-OSX.MPQ")
                return false;
            if (session.Os == "OSX" && file.Filename == "base-Win.MPQ")
                return false;

            if (file.Directory == "Data/")
                return true;
            if (file.Directory.StartsWith("Data/Interface/"))
                return true;
            return file.Directory.StartsWith("Data/" + session.Locale);
        }

        private static bool IsAlreadyDownloaded(string basePath, FileObject file, ref bool sessionChanged)
        {
            Session session = Session.Current;
            if (!session.CompletedFiles.Contains(file.Path))
                return false;

            var info = new FileInfo(basePath + file.LocalPath);
            if (info.Exists && (file.Size <= 0 || info.Length == file.Size))
                return true;

            session.CompletedFiles.Remove(file.Path);
            sessionChanged = true;
            return false;
        }

        private void AddLocale(string locale)
        {
            if (!Locales.Contains(locale))
                Locales.Add(locale);
        }

        private class Entry
        {
            public string Url { get; set; }
            public string RemotePath { get; set; }
            public string Name { get; set; }
            public string Locale { get; set; }
            public long Size { get; set; }

            /// <summary>Directory entries carry no extension on their last segment</summary>
            public bool IsFile
            {
                get
                {
                    int slash = RemotePath.LastIndexOf('/');
                    string last = slash < 0 ? RemotePath : RemotePath.Substring(slash + 1);
                    return last.Contains(".");
                }
            }
        }
    }
}
