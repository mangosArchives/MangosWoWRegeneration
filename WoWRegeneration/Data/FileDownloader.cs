using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using WoWRegeneration.Core;
using WoWRegeneration.Repositories;

namespace WoWRegeneration.Data
{
    public class DownloadProgressEventArgs : EventArgs
    {
        public int FileIndex { get; set; }
        public int FileCount { get; set; }
        public FileObject File { get; set; }
        public long BytesReceived { get; set; }
        public long BytesTotal { get; set; }
        public bool Success { get; set; }
    }

    public class FileDownloader
    {
        private const int BufferSize = 128 * 1024;
        private const int MaxAttempts = 3;
        private const int RequestTimeoutMs = 30000;
        private const int TransferTimeoutMs = 120000;

        private volatile bool _cancelled;

        public FileDownloader(IWoWRepository repository, List<FileObject> files)
        {
            Files = files;
            BasePath = AppEnvironment.ExecutionPath + repository.GetDefaultDirectory();
            Failed = new List<FileObject>();
        }

        public List<FileObject> Files { get; private set; }
        public List<FileObject> Failed { get; private set; }
        public string BasePath { get; private set; }

        public bool Cancelled
        {
            get { return _cancelled; }
        }

        public long TotalBytes
        {
            get
            {
                long total = 0;
                foreach (FileObject file in Files)
                    total += file.Size;
                return total;
            }
        }

        public event EventHandler<DownloadProgressEventArgs> Progress;
        public event EventHandler<DownloadProgressEventArgs> FileStarted;
        public event EventHandler<DownloadProgressEventArgs> FileCompleted;

        public void Cancel()
        {
            _cancelled = true;
        }

        public void Start()
        {
            ServicePointManager.DefaultConnectionLimit = 8;
            AppEnvironment.Log("Downloading " + Files.Count + " file(s)", LogLevel.Info);

            for (int index = 0; index < Files.Count && !_cancelled; index++)
            {
                FileObject file = Files[index];
                Raise(FileStarted, index + 1, file, 0, file.Size, false);

                bool succeeded = Download(file, index + 1);
                if (succeeded)
                {
                    Session.Current.CompletedFiles.Add(file.Path);
                    Session.Current.Save();
                }
                else if (!_cancelled)
                {
                    Failed.Add(file);
                }

                // a cancelled or failed file keeps only what reached the disk, so callers can bill it honestly
                Raise(FileCompleted, index + 1, file, BytesOnDisk(file), file.Size, succeeded);
            }
        }

        private long BytesOnDisk(FileObject file)
        {
            var info = new FileInfo(BasePath + file.LocalPath);
            return info.Exists ? info.Length : 0;
        }

        private bool Download(FileObject file, int index)
        {
            string target = BasePath + file.LocalPath;
            string directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            for (int attempt = 1; attempt <= MaxAttempts && !_cancelled; attempt++)
            {
                try
                {
                    if (Transfer(file, index, target))
                        return true;
                }
                catch (Exception ex)
                {
                    AppEnvironment.Log(
                        file.Filename + ": attempt " + attempt + "/" + MaxAttempts + " failed - " + ex.Message,
                        LogLevel.Warning);
                }
            }

            if (!_cancelled)
                AppEnvironment.Log("Giving up on " + file.Path, LogLevel.Error);
            return false;
        }

        private bool Transfer(FileObject file, int index, string target)
        {
            long offset = ResumeOffset(file, target);
            if (offset < 0)
                return true;

            var request = (HttpWebRequest) WebRequest.Create(file.Url);
            request.Timeout = RequestTimeoutMs;
            request.ReadWriteTimeout = TransferTimeoutMs;
            request.UserAgent = "WoWRegeneration/" + AppEnvironment.GetVersion();
            if (offset > 0)
                request.AddRange(offset);

            using (var response = (HttpWebResponse) request.GetResponse())
            using (Stream input = response.GetResponseStream())
            {
                if (input == null)
                    return false;
                if (offset > 0 && response.StatusCode != HttpStatusCode.PartialContent)
                    offset = 0;

                long total = file.Size > 0
                                 ? file.Size
                                 : (response.ContentLength > 0 ? offset + response.ContentLength : 0);
                long received = offset;

                using (var output = new FileStream(target, offset > 0 ? FileMode.Append : FileMode.Create,
                                                   FileAccess.Write, FileShare.None, BufferSize))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        received += read;
                        Raise(Progress, index, file, received, total, false);
                        if (_cancelled)
                            return false;
                    }
                }
            }

            return Verify(file, target);
        }

        /// <summary>Returns the byte offset to resume from, or -1 when the file is already complete</summary>
        private static long ResumeOffset(FileObject file, string target)
        {
            var info = new FileInfo(target);
            if (!info.Exists)
                return 0;

            if (file.Size <= 0)
            {
                info.Delete();
                return 0;
            }
            if (info.Length == file.Size)
                return -1;
            if (info.Length < file.Size)
                return info.Length;

            info.Delete();
            return 0;
        }

        private static bool Verify(FileObject file, string target)
        {
            var info = new FileInfo(target);
            if (!info.Exists)
                return false;
            if (file.Size <= 0 || info.Length == file.Size)
                return true;

            AppEnvironment.Log(
                file.Filename + ": size mismatch, got " + info.Length + " of " + file.Size + " bytes",
                LogLevel.Warning);
            info.Delete();
            return false;
        }

        private void Raise(EventHandler<DownloadProgressEventArgs> handler, int index, FileObject file,
                           long received, long total, bool success)
        {
            if (handler == null)
                return;

            handler(this, new DownloadProgressEventArgs
            {
                FileIndex = index,
                FileCount = Files.Count,
                File = file,
                BytesReceived = received,
                BytesTotal = total,
                Success = success
            });
        }
    }
}
