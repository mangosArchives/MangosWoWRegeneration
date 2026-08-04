using System;
using System.Collections.Generic;
using System.Globalization;
using WoWRegeneration.Core;
using WoWRegeneration.Data;
using WoWRegeneration.Repositories;

namespace WoWRegeneration.UI
{
    public static class UserInputs
    {
        public static IWoWRepository SelectRepository()
        {
            AppEnvironment.Log("Which version of World of Warcraft do you want to restore:");
            int index = SelectFromList(BuildLabels(RepositoriesManager.Repositories), "Select version:");
            return RepositoriesManager.Repositories[index];
        }

        public static string SelectLocale(List<string> locales)
        {
            AppEnvironment.Log("Which locale do you want to use:");
            int index = SelectFromList(locales, "Select locale:");
            return locales[index];
        }

        public static string SelectOs()
        {
            var systems = new List<string> { "Win", "OSX" };
            AppEnvironment.Log("Which OS do you want to use:");
            int index = SelectFromList(systems, "Select OS:");
            return systems[index];
        }

        public static bool SelectContinueSession(Session previousSession)
        {
            AppEnvironment.Log("An unfinished session was found for:");
            AppEnvironment.Log("WoW Version : " + previousSession.WoWRepositoryName);
            AppEnvironment.Log("Locale      : " + previousSession.Locale);
            AppEnvironment.Log("OS          : " + previousSession.Os);
            AppEnvironment.Log("");
            AppEnvironment.Log("Do you want to continue this session? (y/n):");
            return ReadYesNo();
        }

        private static List<string> BuildLabels(List<IWoWRepository> repositories)
        {
            var labels = new List<string>();
            foreach (IWoWRepository repository in repositories)
                labels.Add(repository.GetVersionName());
            return labels;
        }

        private static int SelectFromList(List<string> items, string prompt)
        {
            AppEnvironment.Log("");
            for (int index = 0; index < items.Count; index++)
                AppEnvironment.Log("[" + (index + 1).ToString("00") + "] " + items[index]);
            AppEnvironment.Log("");
            AppEnvironment.Log(prompt);
            return ReadIndex(items.Count);
        }

        private static bool ReadYesNo()
        {
            while (true)
            {
                string line = Console.ReadLine();
                if (line == null)
                    return false;

                string input = line.Trim().ToLowerInvariant();
                if (input == "y" || input == "n")
                    return input == "y";

                AppEnvironment.Log("Please answer 'y' for yes or 'n' for no, try again", LogLevel.Error);
            }
        }

        private static int ReadIndex(int max)
        {
            while (true)
            {
                string line = Console.ReadLine();
                if (line == null)
                    return 0;

                int value;
                if (!int.TryParse(line.Trim(), out value))
                {
                    AppEnvironment.Log("Please enter a number, try again", LogLevel.Error);
                    continue;
                }
                if (value < 1 || value > max)
                {
                    AppEnvironment.Log(
                        "Please enter a number between 1 and " + max.ToString(CultureInfo.InvariantCulture) +
                        ", try again", LogLevel.Error);
                    continue;
                }
                return value - 1;
            }
        }
    }
}
