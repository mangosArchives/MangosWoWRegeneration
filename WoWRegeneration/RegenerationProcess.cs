using System.Collections.Generic;
using WoWRegeneration.Core;
using WoWRegeneration.Data;
using WoWRegeneration.Repositories;
using WoWRegeneration.UI;

namespace WoWRegeneration
{
    public static class RegenerationProcess
    {
        public static void Run()
        {
            Session previous = Session.Load();

            if (previous == null || previous.SessionCompleted || !UserInputs.SelectContinueSession(previous))
                StartNewSession();
            else
                ResumeSession(previous);
        }

        private static void StartNewSession()
        {
            IWoWRepository repository = UserInputs.SelectRepository();
            ManifestFile manifest = ManifestFile.FromRepository(repository);
            if (manifest == null)
                return;

            List<string> locales = manifest.GetLocales();
            if (locales.Count == 0)
            {
                AppEnvironment.Log("No locale found in the manifest file.", LogLevel.Error);
                return;
            }

            string locale = UserInputs.SelectLocale(locales);
            string os = UserInputs.SelectOs();

            Session.Current = new Session(repository.GetMFilName(), locale, os);
            Session.Current.Save();

            Download(repository, manifest);
        }

        private static void ResumeSession(Session previous)
        {
            Session.Current = previous;

            IWoWRepository repository = RepositoriesManager.GetRepositoryByMfil(previous.MFil);
            if (repository == null)
            {
                AppEnvironment.Log("The saved session refers to an unknown manifest: " + previous.MFil, LogLevel.Error);
                return;
            }

            ManifestFile manifest = ManifestFile.FromRepository(repository);
            if (manifest == null)
                return;

            Download(repository, manifest);
        }

        private static void Download(IWoWRepository repository, ManifestFile manifest)
        {
            AppEnvironment.Log("Generating file list");
            List<FileObject> files = manifest.GenerateFileList();

            if (files.Count == 0)
            {
                Complete();
                AppEnvironment.Log("Nothing left to download, all files are already present.", LogLevel.Success);
                return;
            }

            var downloader = new FileDownloader(repository, files);
            var progress = new ConsoleDownloadProgressBar(downloader);
            AppEnvironment.Log("Target directory: " + downloader.BasePath, LogLevel.Detail);
            AppEnvironment.Log("Total to fetch  : " + ConsoleDownloadProgressBar.HumanReadableByteCount(downloader.TotalBytes),
                               LogLevel.Detail);

            downloader.Start();
            progress.ClearLine();

            if (downloader.Failed.Count > 0)
            {
                AppEnvironment.Log(downloader.Failed.Count + " file(s) failed, run the tool again to retry them:",
                                   LogLevel.Error);
                foreach (FileObject file in downloader.Failed)
                    AppEnvironment.Log("  " + file.Path, LogLevel.Error);
                Session.Current.Save();
                return;
            }

            Complete();
            AppEnvironment.Log("Download complete!", LogLevel.Success);
        }

        private static void Complete()
        {
            Session.Current.SessionCompleted = true;
            Session.Current.Save();
            Session.Current.Destroy();
        }
    }
}
