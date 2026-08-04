using System;
using System.IO;
using System.Reflection;

namespace WoWRegeneration.Core
{
    public enum LogLevel
    {
        Detail,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    ///     Shared state and message sink, so the console and the GUI can drive the same core.
    /// </summary>
    public static class AppEnvironment
    {
        static AppEnvironment()
        {
            ExecutionPath = EnsureTrailingSeparator(AppDomain.CurrentDomain.BaseDirectory);
        }

        public static string ExecutionPath { get; set; }

        public static event Action<string, LogLevel> Message;

        public static void Log(string text)
        {
            Log(text, LogLevel.Info);
        }

        public static void Log(string text, LogLevel level)
        {
            Action<string, LogLevel> handler = Message;
            if (handler != null)
                handler(text, level);
        }

        public static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                return path;
            return path + Path.DirectorySeparatorChar;
        }

        public static string GetVersion()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return assembly.GetName().Version.ToString();
        }
    }
}
