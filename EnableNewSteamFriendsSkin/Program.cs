using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EnableNewSteamFriendsSkin
{
    internal class Program
    {
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
                Println("Steam directory not found. Please specify correct Steam path with the -sp argument.");
                Println("For example: -sp=\"C:/Program Files (x86)/Steam/\"");
            }

            if (!File.Exists(steamLangFile))
            {
                Println("Steam language file not found. Please specify correct language with the -sl argument.");
                Println("If your language is english this would be -sl=\"english\"");
            }

            StartAndWaitForSteam();

            PatchCacheFile();
        }

        private static bool IsGZipHeader(byte[] arr)
        {
            return arr.Length >= 2 &&
                arr[0] == 31 &&
                arr[1] == 139;
        }

        /// <summary>
        /// Compresses byte array to new byte array.
        /// </summary>
        private static byte[] Compress(byte[] raw)
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

        private static byte[] Decompress(byte[] gzip)
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

        private static byte[] GetLatestFriendsCSS()
        {
            Uri LatestURI = new Uri("https://google.com/");
            WebClient downloadFile = new WebClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            return downloadFile.DownloadData("https://steamcommunity-a.akamaihd.net/public/css/webui/friends.css");
        }

        private static string steamDir = FindSteamDir();

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

        private static string steamLang = FindSteamLang();
        private static string steamLangFile = steamDir + "\\friends\\trackerui_" + steamLang + ".txt";

        private static string FindSteamLang()
        {
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam"))
                return registryKey?.GetValue("Language").ToString();
        }

        // whether or not the program should display a window
        private static bool silent = false;

        // arguments to be sent to steam
        private static string steamargs = null;

        // max time we will wait for steam friends list to be detected in seconds
        private static readonly int timeout = 300; // 5 minutes

        private static readonly string friendsString = FindFriendsListString();

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

        private static void CreateConsole()
        {
            if (!silent)
            {
                AllocConsole();
                try
                {
                    // Console.OpenStandardOutput eventually calls into GetStdHandle. As per MSDN documentation of GetStdHandle: http://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx will return the redirected handle and not the allocated console:
                    // "The standard handles of a process may be redirected by a call to  SetStdHandle, in which case  GetStdHandle returns the redirected handle. If the standard handles have been redirected, you can specify the CONIN$ value in a call to the CreateFile function to get a handle to a console's input buffer. Similarly, you can specify the CONOUT$ value to get a handle to a console's active screen buffer."
                    // Get the handle to CONOUT$.
                    IntPtr stdHandle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);
                    SafeFileHandle safeFileHandle = new SafeFileHandle(stdHandle, true);
                    FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                    Encoding encoding = System.Text.Encoding.GetEncoding(MY_CODE_PAGE);
                    StreamWriter standardOutput = new StreamWriter(fileStream, encoding)
                    {
                        AutoFlush = true
                    };
                    Version ver = Assembly.GetEntryAssembly().GetName().Version;
                    Console.Title = "Steam Friends Skin Patcher v" + ver.Major + "." + ver.Minor + "." + ver.Build; ;
                    Console.SetOut(standardOutput);
                }
                catch (Exception) { }
            }
        }

        static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
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
                    Println("Steam translation file not found, checking for friends class name only.");
                int countdown = timeout;
                while (!FindFriendsWindow())
                {
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://open/friends");
                    Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/online");
                    countdown--;
                }

                if (countdown == 0)
                {
                    Println("Friends list could not be found.");
                    Println("If your friends list is open, please report this to the developer.");
                    Println("Otherwise, open your friends list and restart the program.");
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
            if (Windows.FindWindowLike.Find(0, friendsString, "SDL_app").Count() > 0)
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

        private static void Println(string message = null)
        {
            if (!silent)
                Console.WriteLine(message);

            Debug.WriteLine(message);
        }

        private static void ProcessCacheFile(string friendscachefile, byte[] decompressedcachefile)
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

            Println("Finished! Put your custom css in " + steamDir + "\\clientui\\friends.custom.css");
            Println("Close and reopen your Steam friends window to see changes.");
            Println("Run this program again if your changes disappear as it likely means Valve updated the friends css file.");
            PromptForExit();
    }

        private static void PatchCacheFile()
        {
            string cachepath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "Steam\\htmlcache\\Cache\\");
            Println("Downloading latest friends.css from Steam...");
            byte[] originalcss = GetLatestFriendsCSS();
            Println("Download successful.");
            Println("Finding list of possible cache files...");
            if (!Directory.Exists(cachepath))
            {
                Println("Cache folder does not exist.");
                Println("Please confirm that Steam is running and that the friends list is open and try again.");
                PromptForExit();
            }
            double maxKbFileSize = 100;
            List<string> validFiles = Directory.EnumerateFiles(cachepath, "f_*", SearchOption.TopDirectoryOnly).Where(file => file.Length / 1024d < maxKbFileSize).ToList();
            Println("Found " + validFiles.Count() + " possible cache files");
            if (validFiles.Count() == 0)
            {
                Println("Cache files have not been generated yet.");
                Println("Please confirm that Steam is running and that the friends list is open and try again.");
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
                        Println("Success! Matching friends.css found at " + s);
                        friendscachefile = s;
                        ProcessCacheFile(friendscachefile, decompressedcachefile);
                        //Task.Factory.StartNew(() => ProcessCacheFile(friendscachefile, decompressedcachefile));
                    }
                }
            });

            if (string.IsNullOrEmpty(friendscachefile))
            {
                if (silent)
                    PromptForExit();
                bool validresponse = false;
                ConsoleKeyInfo cki;
                string keypressed = null;
                while (!validresponse)
                {
                    Println("friends.css location not found, would you like to clear your Steam cache and try again? Y/n");
                    cki = Console.ReadKey();
                    keypressed = cki.Key.ToString().ToLower();
                    Println();
                    if (keypressed == "y")
                    {
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
                                Println("Could not successfully shutdown Steam, please manually shutdown Steam and try again.");
                                PromptForExit();
                            }
                        }

                        if (!Directory.Exists(steamDir))
                        {
                            Println("Cannot find Steam directory to shutdown, please specify correct Steam path with -sp and try again.");
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

                        PatchCacheFile();
                    }
                    if (keypressed == "n")
                    {
                        validresponse = true;
                        Println("Could not find friends.css, please clear your Steam cache and try again or contact the developer.");
                        PromptForExit();
                    }
                }
            }
        }
        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        private const int MY_CODE_PAGE = 437;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);
    }
}