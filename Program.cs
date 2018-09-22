using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using System.Text;
//using System.Collections.Generic;


namespace EnableNewSteamFriendsSkin
{
    class Program
    {
        static void Main(string[] args)
        {
            string regex = "(?<=-p=)(.*)|(?<=--pass=)(.*)";
            foreach (string s in args)
                if (s.Contains("-p") || s.Contains("--pass"))
                    steamargs = Regex.Match(s, regex).Value;

            if (args.Contains("--silent") || args.Contains("-s"))
            {
                silent = true;
                if (Process.GetProcessesByName("Steam").Length == 0)
                {
                    Process.Start(steamDir + "\\Steam.exe", steamargs);
                    while (FindWindow("SDL_app", friendsString) == 0)
                        Thread.Sleep(1000);
                }
            }
            /* In case of emergency break glass (Possible hacky solution to pattern searching the window title)
            IntPtr hwnd = (IntPtr)FindWindow("SDL_app", null);
            int length = GetWindowTextLength(hwnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            Println(sb.ToString());
            PromptForExit();*/
            CreateConsole();
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

        static readonly string steamLang = FindSteamLang();
        static string FindSteamLang()
        {
            using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam"))
                return registryKey?.GetValue("Language").ToString();
        }

        static bool silent = false;
        static string steamargs = null;

        static readonly string friendsString = FindFriendsListString();
        static string FindFriendsListString()
        {
            string tracker = File.ReadAllText(steamDir + "\\friends\\trackerui_" + steamLang + ".txt");
            string regex = "(?<=\"Friends_InviteInfo_FriendsList\"\\t{1,}\")(.*?)(?=\")";
            return Regex.Match(tracker, regex).Value;

        }

        static void CreateConsole()
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
        static extern int FindWindow(string lpClassName, string lpWindowName);

        /*[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);*/

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_CLOSE = 0xF060;

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        private const int MY_CODE_PAGE = 437;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

        static void PromptForExit()
        {
            Println("Press any key to exit.");
            if(!silent)
                Console.ReadKey();
            Environment.Exit(0);
        }

        static void Println(string message = null)
        {
            if(!silent)
                Console.WriteLine(message);
        }

        static void PatchCacheFile()
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
            if(files.Length == 0)
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
                            while (Process.GetProcessesByName("Steam").Length > 0 && count <= 5)
                            {
                                Thread.Sleep(1000);
                                count++;
                            }
                            if (count > 5)
                            {
                                Println("Could not successfully shutdown Steam, please manually shutdown Steam and try again.");
                                PromptForExit();
                            }
                        }

                        Println("Deleting cache files...");
                        Directory.Delete(cachepath, true);

                        Println("Restarting Steam...");
                        Process.Start(steamDir + "\\Steam.exe", steamargs);

                        Println("Waiting for friends list to open...");
                        while (FindWindow("SDL_app", friendsString) == 0)
                            Thread.Sleep(1000);

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
