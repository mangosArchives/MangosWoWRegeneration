using System;
using WoWRegeneration.Core;
using WoWRegeneration.UI;

namespace WoWRegeneration
{
    internal class Program
    {
        private static void Main()
        {
            InitConsole();
            AppEnvironment.Message += WriteToConsole;

            RegenerationProcess.Run();

            AppEnvironment.Log("");
            AppEnvironment.Log("Press enter to exit program.");
            Console.ReadLine();
        }

        private static void WriteToConsole(string text, LogLevel level)
        {
            ConsoleDownloadProgressBar.ClearActiveLine();
            Console.ForegroundColor = ColorOf(level);
            Console.WriteLine(text);
            Console.ResetColor();
        }

        private static ConsoleColor ColorOf(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Detail:
                    return ConsoleColor.DarkGray;
                case LogLevel.Success:
                    return ConsoleColor.Green;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Error:
                    return ConsoleColor.Red;
                default:
                    return ConsoleColor.White;
            }
        }

        private static void InitConsole()
        {
            try
            {
                Console.Clear();
                Console.Title = "Mangos WoW Regeneration - " + AppEnvironment.GetVersion();
            }
            catch (Exception)
            {
                // output is redirected, cursor and title are unavailable
            }
        }
    }
}
