using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Threading;
/*using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.InteropServices;*/


namespace EnableNewSteamFriendsSkin
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "EnableNewSteamFriendsSkin";
            PatchCacheFile();
        }

        static bool IsGZipHeader(byte[] arr)
        {
            return arr.Length >= 2 &&
                arr[0] == 31 &&
                arr[1] == 139;
        }

        /// <summary>
        /// Compresses byte array to new byte array.
        /// </summary>
        static byte[] Compress(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory,
                    CompressionMode.Compress, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }

        static byte[] Decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip),
                CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        static byte[] GetLatestFriendsCSS()
        {
            Uri LatestURI = new Uri("https://google.com/");
            WebClient downloadFile = new WebClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            return downloadFile.DownloadData("https://steamcommunity-a.akamaihd.net/public/css/webui/friends.css");
        }

        static readonly string steamDir = FindSteamDir();
        static string FindSteamDir()
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

        /*
        static readonly string steamLang = FindSteamLang();
        static string FindSteamLang()
        {
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam"))
                return registryKey?.GetValue("Language").ToString();
        }

        static readonly string friendsString = FindFriendsListString();
        static string FindFriendsListString()
        {
            string tracker = File.ReadAllText(steamDir + "\\friends\\trackerui_" + steamLang + ".txt");
            string regex = "(?<=\"Friends_InviteInfo_FriendsList\"\\t{1,}\")(.*?)(?=\")";
            return Regex.Match(tracker, regex).Value;

        }

        static readonly string friendsWindow = FindFriendsWindow();
        static string FindFriendsWindow()
        {
            Process[] processlist = Process.GetProcesses();
            List<String> windowlist = new List<string>();
            foreach(Process process in processlist)
            {
                if (!String.IsNullOrEmpty(process.MainWindowTitle))
                {
                    windowlist.Add(process.MainWindowTitle);
                }
            }

            string match = windowlist.First(s => s == friendsString + "*");
            if (match != null)
                return match;
            else
                return null;
        }

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        static extern int FindWindow(string lpClassName, string lpWindowName);*/

        static void PromptForExit()
        {
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(0);
        }

        static void PatchCacheFile()
        {
            string cachepath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "Steam\\htmlcache\\Cache\\");
            Console.WriteLine("Downloading latest friends.css from Steam...");
            byte[] originalcss = GetLatestFriendsCSS();
            Console.WriteLine("Download successful.");
            Console.WriteLine("Finding list of possible cache files...");
            if (!Directory.Exists(cachepath))
            {
                Console.WriteLine("Cache folder does not exist.");
                Console.WriteLine("Please confirm that Steam is running and that the friends list is open and try again.");
                PromptForExit();
            }
            string[] files = Directory.GetFiles(cachepath, "f_*");
            Console.WriteLine("Found " + files.Length + " possible cache files");
            if(files.Length == 0)
            {
                Console.WriteLine("Cache files have not been generated yet.");
                Console.WriteLine("Please confirm that Steam is running and that the friends list is open and try again.");
                PromptForExit();
            }
            byte[] cachefile;
            byte[] decompressedcachefile;
            string friendscachefilelocation = null;
            string friendscachefilename = null;

            Console.WriteLine("Checking cache files for match...");
            foreach (string s in files)
            {
                cachefile = File.ReadAllBytes(s);

                if (IsGZipHeader(cachefile) && cachefile.Length < 100000)
                {
                    decompressedcachefile = Decompress(cachefile);
                    if (decompressedcachefile.SequenceEqual(originalcss))
                    {
                        Console.WriteLine("Success! Matching friends.css found at " + s);
                        friendscachefilelocation = s;
                        friendscachefilename = Path.GetFileName(s);
                        Console.WriteLine("Writing friends.css to disk");
                        File.WriteAllBytes(friendscachefilename + "-tmp", decompressedcachefile);
                        break;
                    }
                }
            }

            if (friendscachefilelocation == null || friendscachefilename == null)
            {
                bool validresponse = false;
                ConsoleKeyInfo cki;
                string keypressed = null;
                while (!validresponse)
                {
                    Console.WriteLine("friends.css location not found, would you like to clear your Steam cache and try again? Y/n");
                    cki = Console.ReadKey();
                    keypressed = cki.Key.ToString().ToLower();
                    Console.WriteLine();
                    if (keypressed == "y")
                    {
                        validresponse = true;
                        if (Process.GetProcessesByName("Steam").Length > 0)
                        {
                            Console.WriteLine("Shutting down Steam to clear cache...");
                            Process.Start(steamDir + "\\Steam.exe", "-shutdown");

                            int count = 0;
                            while (Process.GetProcessesByName("Steam").Length > 0 && count <= 5)
                            {
                                Thread.Sleep(1000);
                                count++;
                            }
                            if (count > 5)
                            {
                                Console.WriteLine("Could not successfully shutdown Steam, please manually shutdown Steam and try again.");
                                PromptForExit();
                            }
                        }

                        Console.WriteLine("Deleting cache files...");
                        Directory.Delete(cachepath, true);

                        Console.WriteLine("Restarting Steam...");
                        Process.Start(steamDir + "\\Steam.exe");

                        /*Console.WriteLine("Waiting for friends list to open...");
                        while (FindWindow("SDL_app", FindFriendsWindow()) == 0)
                            Thread.Sleep(1000);*/

                        Console.WriteLine("Waiting for cache folder to be created...");
                        while (!Directory.Exists(cachepath))
                            Thread.Sleep(1000);

                        Thread.Sleep(5000);

                        PatchCacheFile();
                    }
                    if (keypressed == "n")
                    {
                        validresponse = true;
                        Console.WriteLine("Could not find friends.css, please clear your Steam cache and try again or contact the developer.");
                        PromptForExit();
                    }
                }

            }

            Console.WriteLine("Adding import line to friends.css...");
            string importtext = "@import url(\"https://steamloopback.host/friends.custom.css\");\n";
            File.WriteAllText(friendscachefilename, importtext + File.ReadAllText(friendscachefilename + "-tmp"));

            Console.WriteLine("Recompressing friends.css...");
            cachefile = Compress(File.ReadAllBytes(friendscachefilename));

            Console.WriteLine("Overwriting original friends.css...");
            File.WriteAllBytes(friendscachefilelocation, cachefile);

            Console.WriteLine("Cleaning up...");
            File.Delete(friendscachefilename);
            File.Delete(friendscachefilename + "-tmp");

            Console.WriteLine("Finished! Put your custom css in " + steamDir + "\\clientui\\friends.custom.css");
            Console.WriteLine("Close and reopen your Steam friends window to see changes.");
            Console.WriteLine("Run this program again if your changes disappear as it likely means Valve updated the friends css file.");
            PromptForExit();
        }
    }
}
