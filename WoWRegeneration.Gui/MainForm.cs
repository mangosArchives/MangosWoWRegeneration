using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using WoWRegeneration.Core;
using WoWRegeneration.Data;
using WoWRegeneration.Repositories;
using WoWRegeneration.UI;

namespace WoWRegeneration.Gui
{
    public class MainForm : Form
    {
        private const int ProgressScale = 10000;
        private const int UiRefreshMs = 100;

        private readonly Button _browseButton = new Button();
        private readonly Button _cancelButton = new Button();
        private readonly ProgressBar _fileBar = new ProgressBar();
        private readonly Label _fileLabel = new Label();
        private readonly TextBox _folderBox = new TextBox();
        private readonly Button _loadButton = new Button();
        private readonly ComboBox _localeBox = new ComboBox();
        private readonly TextBox _logBox = new TextBox();
        private readonly ComboBox _osBox = new ComboBox();
        private readonly ProgressBar _overallBar = new ProgressBar();
        private readonly Label _overallLabel = new Label();
        private readonly Button _startButton = new Button();
        private readonly ComboBox _versionBox = new ComboBox();

        private long _bytesDone;
        private long _bytesTotal;
        private FileDownloader _downloader;
        private DateTime _lastRefresh = DateTime.MinValue;
        private ManifestFile _manifest;
        private Session _resumable;

        public MainForm()
        {
            BuildLayout();
            AppEnvironment.Message += OnMessage;
            Load += OnFormLoad;
            FormClosing += OnFormClosing;
        }

        private IWoWRepository SelectedRepository
        {
            get
            {
                int index = _versionBox.SelectedIndex;
                return index < 0 ? null : RepositoriesManager.Repositories[index];
            }
        }

        private bool Downloading
        {
            get { return _downloader != null; }
        }

        private void BuildLayout()
        {
            Text = "Mangos WoW Regeneration " + AppEnvironment.GetVersion();
            ClientSize = new Size(702, 540);
            MinimumSize = new Size(560, 420);
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;

            AddLabel("WoW version", 12, 15);
            Place(_versionBox, 110, 12, 380, 24, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            _versionBox.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (IWoWRepository repository in RepositoriesManager.Repositories)
                _versionBox.Items.Add(repository.GetVersionName());
            _versionBox.SelectedIndex = RepositoriesManager.Repositories.Count - 1;
            _versionBox.SelectedIndexChanged += (s, e) => ResetManifest();

            Place(_loadButton, 500, 11, 190, 26, AnchorStyles.Top | AnchorStyles.Right);
            _loadButton.Text = "Load manifest";
            _loadButton.Click += OnLoadManifest;

            AddLabel("Locale", 12, 51);
            Place(_localeBox, 110, 48, 180, 24, AnchorStyles.Top | AnchorStyles.Left);
            _localeBox.DropDownStyle = ComboBoxStyle.DropDownList;

            AddLabel("OS", 310, 51, 30);
            Place(_osBox, 350, 48, 140, 24, AnchorStyles.Top | AnchorStyles.Left);
            _osBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _osBox.Items.AddRange(new object[] { "Win", "OSX" });
            _osBox.SelectedIndex = 0;

            AddLabel("Destination", 12, 87);
            Place(_folderBox, 110, 84, 380, 24, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            _folderBox.ReadOnly = true;
            _folderBox.Text = AppEnvironment.ExecutionPath;

            Place(_browseButton, 500, 83, 190, 26, AnchorStyles.Top | AnchorStyles.Right);
            _browseButton.Text = "Browse...";
            _browseButton.Click += OnBrowse;

            Place(_startButton, 110, 120, 130, 30, AnchorStyles.Top | AnchorStyles.Left);
            _startButton.Text = "Start download";
            _startButton.Enabled = false;
            _startButton.Click += OnStart;

            Place(_cancelButton, 250, 120, 130, 30, AnchorStyles.Top | AnchorStyles.Left);
            _cancelButton.Text = "Cancel";
            _cancelButton.Enabled = false;
            _cancelButton.Click += (s, e) => CancelDownload();

            Place(_overallLabel, 12, 164, 678, 18, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            _overallLabel.Text = "Overall: idle";
            Place(_overallBar, 12, 184, 678, 20, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            _overallBar.Maximum = ProgressScale;

            Place(_fileLabel, 12, 214, 678, 18, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            _fileLabel.Text = "Current file: none";
            Place(_fileBar, 12, 234, 678, 20, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            _fileBar.Maximum = ProgressScale;

            Place(_logBox, 12, 268, 678, 260, AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.BackColor = SystemColors.Window;
            _logBox.Font = new Font(FontFamily.GenericMonospace, 8.5f);
        }

        private void AddLabel(string text, int x, int y, int width = 95)
        {
            var label = new Label { Text = text, AutoSize = false };
            Place(label, x, y, width, 20, AnchorStyles.Top | AnchorStyles.Left);
        }

        private void Place(Control control, int x, int y, int width, int height, AnchorStyles anchor)
        {
            control.SetBounds(x, y, width, height);
            control.Anchor = anchor;
            Controls.Add(control);
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            _resumable = Session.Load();
            if (_resumable == null || _resumable.SessionCompleted)
            {
                _resumable = null;
                return;
            }

            string question = "An unfinished session was found:\n\n" +
                              "Version : " + _resumable.WoWRepositoryName + "\n" +
                              "Locale  : " + _resumable.Locale + "\n" +
                              "OS      : " + _resumable.Os + "\n\n" +
                              "Resume it?";

            if (MessageBox.Show(this, question, "Resume session", MessageBoxButtons.YesNo, MessageBoxIcon.Question) !=
                DialogResult.Yes)
            {
                _resumable.Destroy();
                _resumable = null;
                return;
            }

            ApplyResumableSelection();
            OnLoadManifest(this, EventArgs.Empty);
        }

        private void ApplyResumableSelection()
        {
            for (int index = 0; index < RepositoriesManager.Repositories.Count; index++)
            {
                if (RepositoriesManager.Repositories[index].GetMFilName() != _resumable.MFil)
                    continue;
                _versionBox.SelectedIndex = index;
                break;
            }
            _osBox.SelectedItem = _resumable.Os;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Downloading)
                return;

            if (MessageBox.Show(this, "A download is running. Stop it and close?", "Mangos WoW Regeneration",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            CancelDownload();
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose where the Data folder should be written";
                dialog.SelectedPath = AppEnvironment.ExecutionPath;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                AppEnvironment.ExecutionPath = AppEnvironment.EnsureTrailingSeparator(dialog.SelectedPath);
                _folderBox.Text = AppEnvironment.ExecutionPath;
            }
        }

        private void ResetManifest()
        {
            _manifest = null;
            _localeBox.Items.Clear();
            _startButton.Enabled = false;
        }

        private void OnLoadManifest(object sender, EventArgs e)
        {
            IWoWRepository repository = SelectedRepository;
            if (repository == null || Downloading)
                return;

            ResetManifest();
            _loadButton.Enabled = false;
            AppEnvironment.Log("Fetching manifest for " + repository.GetVersionName());

            Task.Factory.StartNew(() => ManifestFile.FromRepository(repository))
                .ContinueWith(task => Ui(() => OnManifestLoaded(task)));
        }

        private void OnManifestLoaded(Task<ManifestFile> task)
        {
            _loadButton.Enabled = true;

            if (task.IsFaulted)
            {
                AppEnvironment.Log("Manifest download failed: " + task.Exception.GetBaseException().Message,
                                   LogLevel.Error);
                return;
            }
            if (task.Result == null)
                return;

            _manifest = task.Result;
            List<string> locales = _manifest.GetLocales();
            if (locales.Count == 0)
            {
                AppEnvironment.Log("No locale found in the manifest file.", LogLevel.Error);
                return;
            }

            foreach (string locale in locales)
                _localeBox.Items.Add(locale);

            _localeBox.SelectedIndex = _resumable != null && locales.Contains(_resumable.Locale)
                                           ? locales.IndexOf(_resumable.Locale)
                                           : 0;
            _startButton.Enabled = true;
            AppEnvironment.Log("Manifest version " + _manifest.Version + ", " + locales.Count + " locales available",
                               LogLevel.Success);
        }

        private void OnStart(object sender, EventArgs e)
        {
            IWoWRepository repository = SelectedRepository;
            if (repository == null || _manifest == null || Downloading)
                return;

            var locale = (string) _localeBox.SelectedItem;
            var os = (string) _osBox.SelectedItem;
            Session.Current = ReuseOrCreateSession(repository, locale, os);
            Session.Current.Save();

            SetInputsEnabled(false);
            _bytesDone = 0;
            _lastRefresh = DateTime.MinValue;
            ManifestFile manifest = _manifest;

            Task.Factory.StartNew(() => RunDownload(repository, manifest))
                .ContinueWith(task => Ui(() => OnDownloadFinished(task)));
        }

        private Session ReuseOrCreateSession(IWoWRepository repository, string locale, string os)
        {
            if (_resumable != null && _resumable.MFil == repository.GetMFilName() &&
                _resumable.Locale == locale && _resumable.Os == os)
                return _resumable;

            if (_resumable != null)
            {
                _resumable.Destroy();
                _resumable = null;
            }
            return new Session(repository.GetMFilName(), locale, os);
        }

        private FileDownloader RunDownload(IWoWRepository repository, ManifestFile manifest)
        {
            AppEnvironment.Log("Generating file list");
            List<FileObject> files = manifest.GenerateFileList();

            var downloader = new FileDownloader(repository, files);
            _bytesTotal = downloader.TotalBytes;
            _downloader = downloader;

            downloader.FileStarted += OnFileStarted;
            downloader.Progress += OnProgress;
            downloader.FileCompleted += OnFileCompleted;

            AppEnvironment.Log("Target directory: " + downloader.BasePath, LogLevel.Detail);
            downloader.Start();
            return downloader;
        }

        private void OnFileStarted(object sender, DownloadProgressEventArgs e)
        {
            _lastRefresh = DateTime.MinValue;
            RefreshProgress(e, true);
        }

        private void OnProgress(object sender, DownloadProgressEventArgs e)
        {
            RefreshProgress(e, false);
        }

        private void OnFileCompleted(object sender, DownloadProgressEventArgs e)
        {
            _bytesDone += e.BytesReceived;
            RefreshProgress(e, true);
        }

        private void RefreshProgress(DownloadProgressEventArgs e, bool force)
        {
            if (!force && DateTime.UtcNow.Subtract(_lastRefresh).TotalMilliseconds < UiRefreshMs)
                return;
            _lastRefresh = DateTime.UtcNow;

            int index = e.FileIndex;
            int count = e.FileCount;
            string name = e.File.Path;
            long received = e.BytesReceived;
            long total = e.BytesTotal;
            long overall = _bytesDone + (force ? 0 : received);

            Ui(() =>
            {
                _fileLabel.Text = string.Format("File {0} of {1}: {2}  ({3} / {4})", index, count, name,
                                                Bytes(received), Bytes(total));
                _fileBar.Value = Ratio(received, total);
                _overallLabel.Text = string.Format("Overall: {0} / {1}", Bytes(overall), Bytes(_bytesTotal));
                _overallBar.Value = Ratio(overall, _bytesTotal);
            });
        }

        private void OnDownloadFinished(Task<FileDownloader> task)
        {
            FileDownloader downloader = _downloader;
            _downloader = null;
            SetInputsEnabled(true);

            if (task.IsFaulted)
            {
                AppEnvironment.Log("Download aborted: " + task.Exception.GetBaseException().Message, LogLevel.Error);
                return;
            }
            if (downloader == null)
                return;

            if (downloader.Cancelled)
            {
                AppEnvironment.Log("Cancelled. Progress is saved, start again to resume.", LogLevel.Warning);
                return;
            }

            if (downloader.Failed.Count > 0)
            {
                AppEnvironment.Log(downloader.Failed.Count + " file(s) failed, start again to retry them:",
                                   LogLevel.Error);
                foreach (FileObject file in downloader.Failed)
                    AppEnvironment.Log("  " + file.Path, LogLevel.Error);
                Session.Current.Save();
                return;
            }

            Session.Current.SessionCompleted = true;
            Session.Current.Save();
            Session.Current.Destroy();
            _resumable = null;
            _overallBar.Value = ProgressScale;
            AppEnvironment.Log("Download complete!", LogLevel.Success);
        }

        private void CancelDownload()
        {
            FileDownloader downloader = _downloader;
            if (downloader == null)
                return;

            downloader.Cancel();
            _cancelButton.Enabled = false;
            AppEnvironment.Log("Cancelling after the current chunk...", LogLevel.Warning);
        }

        private void SetInputsEnabled(bool enabled)
        {
            _versionBox.Enabled = enabled;
            _localeBox.Enabled = enabled;
            _osBox.Enabled = enabled;
            _loadButton.Enabled = enabled;
            _browseButton.Enabled = enabled;
            _startButton.Enabled = enabled && _manifest != null;
            _cancelButton.Enabled = !enabled;
        }

        private void OnMessage(string text, LogLevel level)
        {
            string line = level == LogLevel.Info || level == LogLevel.Detail
                              ? text
                              : level.ToString().ToUpperInvariant() + ": " + text;

            Ui(() =>
            {
                _logBox.AppendText(line + Environment.NewLine);
                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.ScrollToCaret();
            });
        }

        private static int Ratio(long value, long total)
        {
            if (total <= 0)
                return 0;
            double ratio = value/(double) total;
            return (int) Math.Max(0, Math.Min(ProgressScale, Math.Round(ratio*ProgressScale)));
        }

        private static string Bytes(long value)
        {
            return ConsoleDownloadProgressBar.HumanReadableByteCount(value);
        }

        private void Ui(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
            }
            catch (ObjectDisposedException)
            {
                // form closed while a worker was still reporting
            }
        }
    }
}
