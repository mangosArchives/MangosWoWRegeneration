using System;
using WoWRegeneration.Data;

namespace WoWRegeneration.UI
{
    public class ConsoleDownloadProgressBar
    {
        private const int BarWidth = 28;
        private const int RedrawIntervalMs = 200;

        private int _lastLineLength;
        private DateTime _lastRedraw = DateTime.MinValue;

        public ConsoleDownloadProgressBar(FileDownloader downloader)
        {
            Active = this;
            downloader.FileStarted += (sender, e) =>
            {
                _lastRedraw = DateTime.MinValue;
                Render(e);
            };
            downloader.Progress += (sender, e) => Render(e);
            downloader.FileCompleted += (sender, e) => ClearLine();
        }

        /// <summary>Set so log output can wipe the in place progress line before writing</summary>
        public static ConsoleDownloadProgressBar Active { get; private set; }

        public static void ClearActiveLine()
        {
            if (Active != null)
                Active.ClearLine();
        }

        public static string HumanReadableByteCount(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double length = bytes;
            int order = 0;
            while (length >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                length = length / 1024;
            }
            return string.Format("{0:0.##} {1}", length, sizes[order]);
        }

        public void ClearLine()
        {
            if (_lastLineLength > 0)
                Console.Write("\r" + new string(' ', _lastLineLength) + "\r");
            _lastLineLength = 0;
        }

        private void Render(DownloadProgressEventArgs e)
        {
            if (DateTime.UtcNow.Subtract(_lastRedraw).TotalMilliseconds < RedrawIntervalMs)
                return;
            _lastRedraw = DateTime.UtcNow;

            double ratio = e.BytesTotal > 0 ? Math.Min(1.0, e.BytesReceived/(double) e.BytesTotal) : 0;
            int filled = (int) Math.Round(ratio*BarWidth);

            string line = string.Format("[{0}/{1}] [{2}{3}] {4,5:0.0}%  {5} / {6}  {7}",
                                        e.FileIndex, e.FileCount,
                                        new string('#', filled), new string('.', BarWidth - filled),
                                        ratio*100,
                                        HumanReadableByteCount(e.BytesReceived),
                                        HumanReadableByteCount(e.BytesTotal),
                                        e.File.Filename);

            WriteInPlace(line);
        }

        private void WriteInPlace(string line)
        {
            int width = SafeWidth();
            if (line.Length > width)
                line = line.Substring(0, width);
            if (line.Length < _lastLineLength)
                line = line.PadRight(_lastLineLength);

            _lastLineLength = line.Length;
            Console.Write("\r" + line);
        }

        private static int SafeWidth()
        {
            try
            {
                return Math.Max(40, Console.BufferWidth - 1);
            }
            catch (Exception)
            {
                return 100;
            }
        }
    }
}
