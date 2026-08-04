using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using WoWRegeneration.Core;
using WoWRegeneration.Repositories;

namespace WoWRegeneration.Data
{
    [Serializable]
    public class Session
    {
        private const string SessionFilename = "session.xml";

        public Session()
        {
            CompletedFiles = new List<string>();
            SessionCompleted = false;
        }

        public Session(string mfil, string locale, string os)
            : this()
        {
            MFil = mfil;
            Locale = locale;
            Os = os;
            IWoWRepository repository = RepositoriesManager.GetRepositoryByMfil(mfil);
            if (repository == null)
                throw new ArgumentException("Unknown mfil file: " + mfil);
            WoWRepositoryName = repository.GetVersionName();
        }

        public static Session Current { get; set; }

        public bool SessionCompleted { get; set; }
        public string MFil { get; set; }
        public string WoWRepositoryName { get; set; }
        public string Locale { get; set; }
        public string Os { get; set; }
        public List<string> CompletedFiles { get; set; }

        private static string SessionPath
        {
            get { return AppEnvironment.ExecutionPath + SessionFilename; }
        }

        public static Session Load()
        {
            if (!File.Exists(SessionPath))
                return null;

            try
            {
                var serializer = new XmlSerializer(typeof (Session));
                using (var stream = new FileStream(SessionPath, FileMode.Open, FileAccess.Read))
                {
                    var session = (Session) serializer.Deserialize(stream);
                    if (session.CompletedFiles == null)
                        session.CompletedFiles = new List<string>();
                    return session;
                }
            }
            catch (Exception ex)
            {
                AppEnvironment.Log("Ignoring unreadable session file: " + ex.Message, LogLevel.Warning);
                return null;
            }
        }

        public bool Save()
        {
            try
            {
                var serializer = new XmlSerializer(typeof (Session));
                using (var stream = new FileStream(SessionPath, FileMode.Create, FileAccess.Write))
                {
                    serializer.Serialize(stream, this);
                }
                return true;
            }
            catch (Exception ex)
            {
                AppEnvironment.Log("Unable to save session: " + ex.Message, LogLevel.Warning);
                return false;
            }
        }

        public void Destroy()
        {
            try
            {
                if (File.Exists(SessionPath))
                    File.Delete(SessionPath);
            }
            catch (Exception ex)
            {
                AppEnvironment.Log("Unable to remove session file: " + ex.Message, LogLevel.Warning);
            }
        }
    }
}
