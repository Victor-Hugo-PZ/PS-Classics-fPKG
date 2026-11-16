using System.IO;

namespace makePSClassicsfPKG
{
    public class PS1Game
    {

        public static string GetPS1GameTitleFromDatabaseList(string GameID)
        {
            return GetPS1GameTitleFromDatabaseList(GameID, Directory.GetCurrentDirectory() + @"/Tools/ps1ids.txt");
        }

        /// <summary>Looks up the title of a game in a "ID;Title" database file. Returns an empty string when the game is unknown.</summary>
        public static string GetPS1GameTitleFromDatabaseList(string GameID, string DatabaseFile)
        {
            if (string.IsNullOrEmpty(GameID))
            {
                return "";
            }

            foreach (string GameTitle in File.ReadLines(DatabaseFile))
            {
                if (GameTitle.Contains(GameID))
                {
                    string[] Parts = GameTitle.Split(';');
                    if (Parts.Length > 1)
                    {
                        return Parts[1];
                    }
                }
            }

            return "";
        }

        public static string IsGameProtected(string GameID)
        {
            return IsGameProtected(GameID, Directory.GetCurrentDirectory() + @"/Tools/libcrypt.txt");
        }

        /// <summary>Returns the libcrypt value of a protected game ("SLES_012.26 03E7" lists), or an empty string when the game is not in the list.</summary>
        public static string IsGameProtected(string GameID, string DatabaseFile)
        {
            if (string.IsNullOrEmpty(GameID))
            {
                return "";
            }

            foreach (string GameIDInFile in File.ReadLines(DatabaseFile))
            {
                if (GameIDInFile.Contains(GameID))
                {
                    string[] Parts = GameIDInFile.Split(' ');
                    if (Parts.Length > 1)
                    {
                        return Parts[1];
                    }
                }
            }

            return "";
        }

    }
}
