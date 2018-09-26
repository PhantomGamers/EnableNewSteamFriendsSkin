using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

//using System.Collections.Generic;

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
                    steamLang = Regex.Match(s, steamlangregex).Value;

                if (Regex.Match(s, silentregex).Success)
                    silent = true;
            }

            CreateConsole();

            if (!Directory.Exists(steamDir))
            {
                Println("Steam directory not found. Please specify correct Steam path with the -sp argument.");
                Println("For example: -sp=\"C:/Program Files (x86)/Steam/\"");
                PromptForExit();
            }

            if (!File.Exists(steamDir + "\\friends\\trackerui_" + steamLang + ".txt"))
            {
                Println("Steam language file not found. Please specify correct language with the -sl argument.");
                Println("If your language is english this would be -sl=\"english\"");
                PromptForExit();
            }

            /* In case of emergency break glass (Possible hacky solution to pattern searching the window title)
            IntPtr hwnd = (IntPtr)FindWindow("SDL_app", null);
            int length = GetWindowTextLength(hwnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            Println(sb.ToString());
            PromptForExit();*/

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
            string tracker = File.ReadAllText(steamDir + "\\friends\\trackerui_" + steamLang + ".txt");
            string regex = "(?<=\"Friends_InviteInfo_FriendsList\"\\t{1,}\")(.*?)(?=\")";
            return Regex.Match(tracker, regex).Value;
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
                    Console.Title = "Steam Friends Skin Patcher";
                    Console.SetOut(standardOutput);
                }
                catch (Exception) { }
            }
        }

        private static void StartAndWaitForSteam()
        {
            if (Process.GetProcessesByName("Steam").Length == 0)
            {
                Println("Starting Steam...");
                Process.Start(steamDir + "\\Steam.exe", steamargs);
                Println("Waiting for friends list to open...");
                Println("If friends list does not open automatically, please open manually.");
                int countdown = timeout;
                while (FindWindow("SDL_app", friendsString) == 0 && countdown > 0)
                {
                    Thread.Sleep(1000);
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

        /*
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
        }*/

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        private static extern int FindWindow(string lpClassName, string lpWindowName);

        /*[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);*/

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        private const int MY_CODE_PAGE = 437;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

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
            string[] files = Directory.GetFiles(cachepath, "f_*");
            Println("Found " + files.Length + " possible cache files");
            if (files.Length == 0)
            {
                Println("Cache files have not been generated yet.");
                Println("Please confirm that Steam is running and that the friends list is open and try again.");
                PromptForExit();
            }
            byte[] cachefile;
            byte[] decompressedcachefile;
            string friendscachefilelocation = null;
            string friendscachefilename = null;

            Println("Checking cache files for match...");
            foreach (string s in files)
            {
                cachefile = File.ReadAllBytes(s);

                if (IsGZipHeader(cachefile) && cachefile.Length < 100000)
                {
                    decompressedcachefile = Decompress(cachefile);
                    if (decompressedcachefile.SequenceEqual(originalcss))
                    {
                        Println("Success! Matching friends.css found at " + s);
                        friendscachefilelocation = s;
                        friendscachefilename = Path.GetFileName(s);
                        Println("Writing friends.css to disk");
                        File.WriteAllBytes(friendscachefilename + "-tmp", decompressedcachefile);
                        break;
                    }
                }
            }

            if (friendscachefilelocation == null || friendscachefilename == null)
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
                        if (Process.GetProcessesByName("Steam").Length > 0)
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

                        Println("Deleting cache files...");
                        Directory.Delete(cachepath, true);

                        StartAndWaitForSteam();

                        if (!Directory.Exists(cachepath))
                        {
                            Println("Waiting for cache folder to be created...");
                            while (!Directory.Exists(cachepath))
                                Thread.Sleep(1000);
                        }

                        Thread.Sleep(5000);

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

            Println("Adding import line to friends.css...");
            string importtext = "@import url(\"https://steamloopback.host/friends.custom.css\");\n";
            File.WriteAllText(friendscachefilename, importtext + File.ReadAllText(friendscachefilename + "-tmp"));

            Println("Recompressing friends.css...");
            cachefile = Compress(File.ReadAllBytes(friendscachefilename));

            Println("Overwriting original friends.css...");
            File.WriteAllBytes(friendscachefilelocation, cachefile);

            Println("Cleaning up...");
            File.Delete(friendscachefilename);
            File.Delete(friendscachefilename + "-tmp");

            if (Process.GetProcessesByName("Steam").Length > 0)
            {
                Println("Trying to reopen friends window...");
                int iHandle = FindWindow("SDL_app", friendsString);
                if (iHandle > 0)
                    SendMessage(iHandle, WM_SYSCOMMAND, SC_CLOSE, 0);
                else
                    Println("Can't find friends window.");
                Process.Start(steamDir + "\\Steam.exe", @"steam://open/friends/");
            }

            Println("Finished! Put your custom css in " + steamDir + "\\clientui\\friends.custom.css");
            Println("Close and reopen your Steam friends window to see changes.");
            Println("Run this program again if your changes disappear as it likely means Valve updated the friends css file.");
            PromptForExit();
        }
    }
}