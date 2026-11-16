using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace makePSClassicsfPKG
{

    public class PS2Game
    {

        /// <summary>
        /// Extracts the game ID from the listing printed by "7z l -ba game.iso".
        /// The ID is either found in the volume header ("Volume: SLUS_123.45") or as the name of the boot ELF file in the root.
        /// Returns an empty string when no ID is found.
        /// </summary>
        public static string ParseGameIDFromListing(IEnumerable<string> ListingLines)
        {
            foreach (string Line in ListingLines)
            {
                if (Line.Contains("SLES_") | Line.Contains("SLUS_") | Line.Contains("SCES_") | Line.Contains("SCUS_"))
                {
                    if (Line.Contains("Volume:")) // ID found in the ISO Header
                    {
                        int VolumeIndex = Line.IndexOf("Volume: ", StringComparison.Ordinal);
                        if (VolumeIndex >= 0)
                        {
                            string VolumeID = Line.Substring(VolumeIndex + "Volume: ".Length).Trim();
                            if (VolumeID.Length > 0)
                            {
                                return VolumeID;
                            }
                        }
                    }
                    else // ID found in the ISO files
                    {
                        string[] LineParts = Line.Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries);
                        if (LineParts.Length > 5)
                        {
                            return LineParts[5].Trim();
                        }
                    }
                }
            }

            return "";
        }

        public static string GetPS2GameID(string GameISO)
        {
            using var Bash = new Process();

            var StringsCMD = $"7z l -ba \"{GameISO}\"";
            var EscapedArgs = StringsCMD.Replace("\"", "\\\"");

            Bash.StartInfo.FileName = "/bin/bash";
            Bash.StartInfo.Arguments = $"-c \"{EscapedArgs}\"";
            Bash.StartInfo.RedirectStandardOutput = true;
            Bash.StartInfo.RedirectStandardError = true;
            Bash.StartInfo.UseShellExecute = false;
            Bash.StartInfo.CreateNoWindow = false;
            Bash.Start();

            // Read the output before waiting for the exit, otherwise a full pipe buffer blocks the child process forever
            var OutputReader = Bash.StandardOutput;
            string[] ProcessOutput = OutputReader.ReadToEnd().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Bash.WaitForExit();

            return ParseGameIDFromListing(ProcessOutput);
        }

        public static string GetPS2GameTitleFromDatabaseList(string GameID)
        {
            return GetPS2GameTitleFromDatabaseList(GameID, Directory.GetCurrentDirectory() + @"/Tools/ps2ids.txt");
        }

        /// <summary>Looks up the title of a game in a "ID;Title" database file. Returns "Unknown PS2 game" when the game is not listed.</summary>
        public static string GetPS2GameTitleFromDatabaseList(string GameID, string DatabaseFile)
        {
            string FoundGameTitle = "";
            GameID = GameID.Replace("-", "");
            if (string.IsNullOrEmpty(GameID))
            {
                return "Unknown PS2 game";
            }

            foreach (string GameTitle in File.ReadLines(DatabaseFile))
            {
                if (GameTitle.Contains(GameID))
                {
                    string[] Parts = GameTitle.Split(';');
                    if (Parts.Length > 1)
                    {
                        FoundGameTitle = Parts[1];
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(FoundGameTitle))
            {
                return "Unknown PS2 game";
            }
            else
            {
                return FoundGameTitle;
            }
        }

    }
}
