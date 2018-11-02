using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Console = Colorful.Console;

namespace EnableNewSteamFriendsSkin
{
    internal class Program : Util
    {
        #region global variables

        /*
        /// max time we will wait for steam friends list to be detected in seconds
        ///
        */
        private static readonly int timeout = 300; // 5 minutes

        // arguments to be sent to steam
        private static string steamargs = null;

        // whether or not the program should display a window
        internal static bool silent = false;

        // location of steam directory
        private static string steamDir = FindSteamDir();

        // current language set on steam
        private static string steamLang = FindSteamLang();

        // location of file containing translations for set language
        private static string steamLangFile = steamDir + "\\friends\\trackerui_" + steamLang + ".txt";

        // title of friends window in set language
        private static readonly string friendsString = FindFriendsListString();

        // object to lock when writing to console to maintain thread safety
        private static object _MessageLock = new object();

        #endregion global variables

        private static void Main(string[] args)
        {
            string silentregex = "-s$|--silent$";
            string passregex = "(?<=-p=)(.*)|(?<=--pass=)(.*)";
            string steampathregex = "(?<=-sp=)(.*)|(?<=--steampath=)(.*)";
            string steamlangregex = "(?<=-sl=)(.*)|(?<=--steamlang=)(.*)";
            foreach (string s in args)
            {
                if (Regex.Match(s, passregex).Success)
                    steamargs = Regex.Match(s, passregex).Value;

                if (Regex.Match(s, steampathregex).Success)
                    steamDir = Regex.Match(s, steampathregex).Value;

                if (Regex.Match(s, steamlangregex).Success)
                {
                    steamLang = Regex.Match(s, steamlangregex).Value;
                    steamLangFile = steamDir + "\\friends\\trackerui_" + steamLang + ".txt";
                }

                if (Regex.Match(s, silentregex).Success)
                    silent = true;
            }

            CreateConsole();

            if (!Directory.Exists(steamDir))
            {
                Println("Steam directory not found. Please specify correct Steam path with the -sp argument.", "warning");
                Println("For example: -sp=\"C:/Program Files (x86)/Steam/\"", "warning");
            }

            if (!File.Exists(steamLangFile))
            {
                Println("Steam language file not found. Please specify correct language with the -sl argument.", "warning");
                Println("If your language is english this would be -sl=\"english\"", "warning");
            }

            StartAndWaitForSteam();

            FindCacheFile();
        }

        private static byte[] GetLatestFriendsCSS()
        {
            Uri LatestURI = new Uri("https://google.com/");
            WebClient downloadFile = new WebClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            return downloadFile.DownloadData("https://steamcommunity-a.akamaihd.net/public/css/webui/friends.css");
        }

        private static string FindSteamDir()
        {
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam"))
            {
                string filePath = null;
                var regFilePath = registryKey?.GetValue("SteamPath");
                if (regFilePath != null)
                {
                    filePath = regFilePath.ToString().Replace(@"/", @"\");
                }
                return filePath;
            }
        }

        private static string FindSteamLang()
        {
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam"))
                return registryKey?.GetValue("Language").ToString();
        }

        private static string FindFriendsListString()
        {
            string regex = "(?<=\"Friends_InviteInfo_FriendsList\"\\t{1,}\")(.*?)(?=\")";
            string s = null;
            string tracker = null;
            string smatch = null;
            if (File.Exists(steamLangFile))
                tracker = File.ReadAllText(steamLangFile);
            if (tracker != null)
                smatch = Regex.Match(tracker, regex).Value;
            if (smatch != null)
                s = smatch;
            return s;
        }

        private static void StartAndWaitForSteam()
        {
            if (Process.GetProcessesByName("Steam").Length == 0 && Directory.Exists(steamDir))
            {
                Println("Starting Steam...");
                Process.Start(steamDir + "\\Steam.exe", steamargs);
                Println("Waiting for friends list to open...");
                Println("If friends list does not open automatically, please open manually.");
                if (friendsString == null)
                    Println("Steam translation file not found, checking for friends class name only.", "warning");
                int countdown = timeout;
                while (!FindFriendsWindow())
                {
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://open/friends");
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/online");
                    countdown--;
                }

                if (countdown == 0)
                {
                    Println("Friends list could not be found.", "error");
                    Println("If your friends list is open, please report this to the developer.", "error");
                    Println("Otherwise, open your friends list and restart the program.", "error");
                    PromptForExit();
                }
            }
        }

        private static byte[] PrependFile(byte[] file)
        {
            string appendText = "@import url(\"https://steamloopback.host/friends.custom.css\");\n";
            byte[] append = Encoding.ASCII.GetBytes(appendText);
            byte[] output = append.Concat(file).ToArray();
            return output;
        }

        private static bool FindFriendsWindow()
        {
            if (WindowSearch.FindWindowLike.Find(0, friendsString, "SDL_app").Count() > 0)
                return true;
            else
                return false;
        }

        private static void PromptForExit()
        {
            Println("Press any key to exit.");
            if (!silent)
                Console.ReadKey();
            Environment.Exit(0);
        }

        private static void Println(string message = null, string messagetype = "info")
        {
            if (!silent)
            {
                lock (_MessageLock)
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        Console.WriteLine();
                        return;
                    }

                    Color c = Color.White;
                    if (messagetype == "error")
                        c = Color.Red;
                    if (messagetype == "warning")
                        c = Color.Yellow;
                    if (messagetype == "info")
                        c = Color.GhostWhite;
                    if (messagetype == "success")
                        c = Color.Green;

                    Console.WriteLine(message, c);
                }
            }

            #if DEBUG
            Debug.WriteLine(message);
            #endif
        }

        private static void PatchCacheFile(string friendscachefile, byte[] decompressedcachefile)
        {
            Println("Adding import line to friends.css...");
            decompressedcachefile = PrependFile(decompressedcachefile);

            Println("Recompressing friends.css...");
            byte[] cachefile = Compress(decompressedcachefile);

            Println("Overwriting original friends.css...");
            File.WriteAllBytes(friendscachefile, cachefile);

            if (Process.GetProcessesByName("Steam").Length > 0 && Directory.Exists(steamDir))
            {
                Println("Reloading friends window...");
                Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/offline");
                Thread.Sleep(1000);
                Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/online");
                Thread.Sleep(1000);
                Process.Start(steamDir + "\\Steam.exe", @"steam://open/friends");
            }

            Println("Finished! Put your custom css in " + steamDir + "\\clientui\\friends.custom.css", "success");
            Println("Close and reopen your Steam friends window to see changes.", "success");
            Println("Run this program again if your changes disappear as it likely means Valve updated the friends css file.", "success");
            PromptForExit();
        }

        /// <summary>
        /// Main function to find cache file
        /// </summary>
        private static void FindCacheFile()
        {
            string cachepath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "Steam\\htmlcache\\Cache\\");
            Println("Downloading latest friends.css from Steam...");
            byte[] originalcss = null;
            try
            {
                originalcss = GetLatestFriendsCSS();
                if (originalcss.Length > 0)
                    Println("Download successful.", "success");
            }
            catch (Exception e)
            {
                Println("Error downloading friends.css: " + e, "error");
                PromptForExit();
            }
            Println("Finding list of possible cache files...");
            if (!Directory.Exists(cachepath))
            {
                Println("Cache folder does not exist.", "error");
                Println("Please confirm that Steam is running and that the friends list is open and try again.", "error");
                PromptForExit();
            }
            double maxKbFileSize = 100;
            List<string> validFiles = Directory.EnumerateFiles(cachepath, "f_*", SearchOption.TopDirectoryOnly).Where(file => file.Length / 1024d < maxKbFileSize).ToList();
            Println("Found " + validFiles.Count() + " possible cache files");
            if (validFiles.Count() == 0)
            {
                Println("Cache files have not been generated yet.", "error");
                Println("Please confirm that Steam is running and that the friends list is open and try again.", "error");
                PromptForExit();
            }
            string friendscachefile = null;

            Println("Checking cache files for match...");
            Parallel.ForEach(validFiles, (s, state) =>
            {
                byte[] cachefile = File.ReadAllBytes(s);

                if (IsGZipHeader(cachefile))
                {
                    byte[] decompressedcachefile = Decompress(cachefile);
                    if (ByteArrayCompare(decompressedcachefile, originalcss))
                    {
                        state.Stop();
                        Println("Success! Matching friends.css found at " + s, "success");
                        friendscachefile = s;
                        PatchCacheFile(friendscachefile, decompressedcachefile);
                    }
                }
            });

            if (string.IsNullOrEmpty(friendscachefile))
            {
                if (silent)
                    PromptForExit();
                bool validresponse = false;
                while (!validresponse)
                {
                    Println("friends.css location not found, would you like to clear your Steam cache and try again? Y/n", "error");
                    var cki = Console.ReadKey();
                    var keypressed = cki.KeyChar.ToString().ToLower();
                    Println();
                    if (keypressed == "n")
                    {
                        validresponse = true;
                        Println("Could not find friends.css", "error");
                        Println("If Steam is not already patched please clear your Steam cache and try again or contact the developer.", "error");
                        PromptForExit();
                    } else {
                        Println(keypressed);
                        validresponse = true;
                        if (Process.GetProcessesByName("Steam").Length > 0 && Directory.Exists(steamDir))
                        {
                            Println("Shutting down Steam to clear cache...");
                            Process.Start(steamDir + "\\Steam.exe", "-shutdown");

                            int count = 0;
                            while (Process.GetProcessesByName("Steam").Length > 0 && count <= 10)
                            {
                                Thread.Sleep(1000);
                                count++;
                            }
                            if (count > 10)
                            {
                                Println("Could not successfully shutdown Steam, please manually shutdown Steam and try again.", "error");
                                PromptForExit();
                            }
                        }

                        if (!Directory.Exists(steamDir))
                        {
                            Println("Cannot find Steam directory to shutdown, please specify correct Steam path with -sp and try again.", "error");
                            PromptForExit();
                        }

                        Println("Deleting cache files...");
                        Directory.Delete(cachepath, true);

                        StartAndWaitForSteam();

                        if (!Directory.Exists(cachepath))
                        {
                            Println("Waiting for cache folder to be created...");
                            while (!Directory.Exists(cachepath))
                                Thread.Sleep(1000);
                        }

                        FindCacheFile();
                    }
                }
            }
        }
    }
}