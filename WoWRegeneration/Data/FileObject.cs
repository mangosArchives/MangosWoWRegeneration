namespace WoWRegeneration.Data
{
    public class FileObject
    {
        public string Url { get; set; }

        /// <summary>Manifest style path, always separated by '/'</summary>
        public string Path { get; set; }

        /// <summary>Locale this entry belongs to, null when locale independent</summary>
        public string Info { get; set; }

        public long Size { get; set; }

        public string Directory
        {
            get
            {
                int index = Path.LastIndexOf('/');
                return index < 0 ? "" : Path.Substring(0, index + 1);
            }
        }

        public string Filename
        {
            get
            {
                int index = Path.LastIndexOf('/');
                return index < 0 ? Path : Path.Substring(index + 1);
            }
        }

        public string LocalPath
        {
            get { return Path.Replace('/', System.IO.Path.DirectorySeparatorChar); }
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
