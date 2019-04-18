namespace EnableNewSteamFriendsSkin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using Steam4NET;

    using GZipStream = Ionic.Zlib.GZipStream;

    /// <summary>
    /// Main class for finding and patching friends.css
    /// </summary>
    internal class Program : Util
    {
        // Declare Steam Client Variables
        private static ISteamClient017 steamclient;

        private static ISteamFriends015 steamfriends;
        private static bool firstRun = false;

        // Update Checker
        private static Task checkForUpdate = new Task(() => { UpdateChecker(); });

        // max time we will wait for steam friends list to be detected in seconds
        private static readonly int Timeout = 300; // 5 minutes

        // object to lock when writing to console to maintain thread safety
        private static readonly object MessageLock = new object();

        // location of steam directory
        private static string steamDir = FindSteamDir();

        // current language set on steam
        private static string steamLang = FindSteamLang();

        // location of file containing translations for set language
        private static string steamLangFile = steamDir + "\\friends\\trackerui_" + steamLang + ".txt";

        // title of friends window in set language
        private static readonly string FriendsString = FindFriendsListString();

        // arguments to be sent to steam
        private static string steamargs = null;

        // friends.css etag
        private static string etag = null;

        /// <summary>
        /// Gets or sets a value indicating whether not the program should display a window
        /// </summary>
        internal static bool Silent { get; set; } = false;

        private static void Main(string[] args)
        {
            if (!IsSingleInstance())
            {
                return;
            }

            string silentregex = "-s$|--silent$";
            string passregex = "(?<=-p=)(.*)|(?<=--pass=)(.*)";
            string steampathregex = "(?<=-sp=)(.*)|(?<=--steampath=)(.*)";
            string steamlangregex = "(?<=-sl=)(.*)|(?<=--steamlang=)(.*)";
            foreach (string s in args)
            {
                if (Regex.Match(s, passregex).Success)
                {
                    steamargs = Regex.Match(s, passregex).Value;
                }

                if (Regex.Match(s, steampathregex).Success)
                {
                    steamDir = Regex.Match(s, steampathregex).Value;
                }

                if (Regex.Match(s, steamlangregex).Success)
                {
                    steamLang = Regex.Match(s, steamlangregex).Value;
                    steamLangFile = steamDir + "\\friends\\trackerui_" + steamLang + ".txt";
                }

                if (Regex.Match(s, silentregex).Success)
                {
                    Silent = true;
                }
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

            checkForUpdate.Start();

            StartAndWaitForSteam();

            FindCacheFile();
        }

        private static byte[] GetLatestFriendsCSS()
        {
            Uri latestURI = new Uri("https://google.com/");
            WebClient wc = new WebClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            string steamChat = wc.DownloadString("https://steam-chat.com/chat/clientui/?l=&build=&cc");
            string eTagRegex = "(?<=<link href=\"https:\\/\\/steamcommunity-a.akamaihd.net\\/public\\/css\\/webui\\/friends.css\\?v=)(.*?)(?=\")";
            etag = Regex.Match(steamChat, eTagRegex).Value;
            if (!string.IsNullOrEmpty(etag))
            {
                return wc.DownloadData("https://steamcommunity-a.akamaihd.net/public/css/webui/friends.css?v=" + etag);
            }

            return wc.DownloadData("https://steamcommunity-a.akamaihd.net/public/css/webui/friends.css");
        }

        private static bool UpdateChecker()
        {
            Uri latestURI = new Uri("https://www.google.com/");
            WebClient wc = new WebClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            wc.Headers.Add("user-agent", "EnableNewSteamFriendsSkin");
            string latestver = wc.DownloadString("https://api.github.com/repos/phantomgamers/enablenewsteamfriendsskin/releases/latest");
            string verregex = "(?<=\"tag_name\":\")(.*?)(?=\")";
            string latestvervalue = Regex.Match(latestver, verregex).Value;
            if (!string.IsNullOrEmpty(latestvervalue))
            {
                Version localver = Assembly.GetEntryAssembly().GetName().Version;
                Version remotever = new Version(latestvervalue);
                if (remotever > localver)
                {
                    if (System.Windows.Forms.MessageBox.Show("Update available. Download now?", "Steam Friends Skin Patcher Update Available", System.Windows.Forms.MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                    {
                        Process.Start("https://github.com/PhantomGamers/EnableNewSteamFriendsSkin/releases/latest");
                    }

                    return true;
                }
            }

            return false;
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
            {
                return registryKey?.GetValue("Language").ToString();
            }
        }

        private static string FindFriendsListString()
        {
            string regex = "(?<=\"Friends_InviteInfo_FriendsList\"\\t{1,}\")(.*?)(?=\")";
            string s = null;
            string tracker = null;
            string smatch = null;
            if (File.Exists(steamLangFile))
            {
                tracker = File.ReadAllText(steamLangFile);
            }

            if (!string.IsNullOrEmpty(tracker))
            {
                smatch = Regex.Match(tracker, regex).Value;
            }

            if (!string.IsNullOrEmpty(smatch))
            {
                s = smatch;
            }

            return s;
        }

        private static void StartAndWaitForSteam()
        {
            if (Process.GetProcessesByName("Steam").Length == 0 && Directory.Exists(steamDir))
            {
                Environment.SetEnvironmentVariable("SteamAppId", "000");
                Stopwatch stopwatch = null;
                if (!Steamworks.Load(true))
                {
                    Println("Steamworks could not be loaded, falling back to older method...", "warning");
                    LegacyStartAndWaitForSteam();
                    return;
                }

                if (!firstRun)
                {
                    steamclient = Steamworks.CreateInterface<ISteamClient017>();
                }

                if (steamclient == null)
                {
                    Println("Steamworks could not be loaded, falling back to older method...", "warning");
                    LegacyStartAndWaitForSteam();
                    return;
                }

                int pipe = steamclient.CreateSteamPipe();
                if (pipe == 0)
                {
                    Println("Starting Steam...");
                    Process.Start(steamDir + "\\Steam.exe", steamargs);
                    Println("Waiting for friends list to connect...");
                    stopwatch = Stopwatch.StartNew();
                    while (pipe == 0 && stopwatch.Elapsed.Seconds < Timeout)
                    {
                        pipe = steamclient.CreateSteamPipe();
                        Thread.Sleep(100);
                    }

                    stopwatch.Stop();
                    if (stopwatch.Elapsed.Seconds >= Timeout && pipe == 0)
                    {
                        Println("Steamworks could not be loaded, falling back to older method...", "warning");
                        steamclient.BShutdownIfAllPipesClosed();
                        LegacyStartAndWaitForSteam();
                        return;
                    }
                }

                int user = steamclient.ConnectToGlobalUser(pipe);
                if (user == 0 || user == -1)
                {
                    Println("Steamworks could not be loaded, falling back to older method...", "warning");
                    steamclient.BReleaseSteamPipe(pipe);
                    steamclient.BShutdownIfAllPipesClosed();
                    LegacyStartAndWaitForSteam();
                    return;
                }

                if (!firstRun)
                {
                    steamfriends = steamclient.GetISteamFriends<ISteamFriends015>(user, pipe);
                    firstRun = true;
                }

                if (steamfriends == null)
                {
                    Println("Steamworks could not be loaded, falling back to older method...", "warning");
                    steamclient.BReleaseSteamPipe(pipe);
                    steamclient.BShutdownIfAllPipesClosed();
                    LegacyStartAndWaitForSteam();
                    return;
                }

                CallbackMsg_t callbackMsg = default(CallbackMsg_t);
                bool stateChangeDetected = false;
                stopwatch = Stopwatch.StartNew();
                while (!stateChangeDetected && stopwatch.Elapsed.Seconds < Timeout)
                {
                    while (Steamworks.GetCallback(pipe, ref callbackMsg) && !stateChangeDetected && stopwatch.Elapsed.Seconds < Timeout)
                    {
                        if (callbackMsg.m_iCallback == PersonaStateChange_t.k_iCallback)
                        {
                            PersonaStateChange_t onPersonaStateChange = (PersonaStateChange_t)Marshal.PtrToStructure(callbackMsg.m_pubParam, typeof(PersonaStateChange_t));
                            if (onPersonaStateChange.m_nChangeFlags.HasFlag(EPersonaChange.k_EPersonaChangeComeOnline))
                            {
                                stateChangeDetected = true;
                                Println("Friends list connected!", "success");
                                break;
                            }
                        }

                        Steamworks.FreeLastCallback(pipe);
                    }

                    Thread.Sleep(100);
                }

                stopwatch.Stop();
                Steamworks.FreeLastCallback(pipe);
                steamclient.ReleaseUser(pipe, user);
                steamclient.BReleaseSteamPipe(pipe);
                steamclient.BShutdownIfAllPipesClosed();
                if (stopwatch.Elapsed.Seconds >= Timeout)
                {
                    Println("Steamworks could not be loaded, falling back to older method...", "warning");
                    LegacyStartAndWaitForSteam();
                    return;
                }
            }
        }

        private static void LegacyStartAndWaitForSteam()
        {
            if (Process.GetProcessesByName("Steam").Length == 0 && Directory.Exists(steamDir))
            {
                Println("Starting Steam...");
                Process.Start(steamDir + "\\Steam.exe", steamargs);
                Println("Waiting for friends list to open...");
                Println("If friends list does not open automatically, please open manually.");
                if (string.IsNullOrEmpty(FriendsString))
                {
                    Println("Steam translation file not found, checking for friends class name only.", "warning");
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                while (!FindFriendsWindow() && stopwatch.Elapsed.Seconds < Timeout)
                {
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://open/friends");
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/online");
                }

                stopwatch.Stop();
                if (stopwatch.Elapsed.Seconds >= Timeout && !FindFriendsWindow())
                {
                    Println("Friends list could not be found.", "error");
                    Println("If your friends list is open, please report this to the developer.", "error");
                    Println("Otherwise, open your friends list and restart the program.", "error");
                    PromptForExit();
                }
            }
            else if (Directory.Exists(steamDir))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (!FindFriendsWindow() && stopwatch.Elapsed.Seconds < Timeout)
                {
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://open/friends");
                    Thread.Sleep(1000);
                    Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/online");
                }

                stopwatch.Stop();
                if (stopwatch.Elapsed.Seconds >= Timeout && !FindFriendsWindow())
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
            // custom only
            // string appendText = "@import url(\"https://steamloopback.host/friends.custom.css\");\n";

            // custom overrides original (!important tags not needed)
            string appendText = "@import url(\"https://steamloopback.host/friends.original.css\");\n@import url(\"https://steamloopback.host/friends.custom.css\");\n{";

            // original overrides custom (!important tags needed, this is the original behavior)
            // string appendText = "@import url(\"https://steamloopback.host/friends.custom.css\");\n@import url(\"https://steamloopback.host/friends.original.css\");\n{";

            // load original from Steam CDN, not recommended because of infinite matching
            // string appendText = "@import url(\"https://steamcommunity-a.akamaihd.net/public/css/webui/friends.css\");\n@import url(\"https://steamloopback.host/friends.custom.css\");\n";
            byte[] append = Encoding.ASCII.GetBytes(appendText);

            byte[] output = append.Concat(file).Concat(Encoding.ASCII.GetBytes("}")).ToArray();
            return output;
        }

        private static bool FindFriendsWindow()
        {
            if (!string.IsNullOrEmpty(FriendsString) && WindowSearch.FindWindowLike.Find(0, FriendsString, "SDL_app").Count() > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void PromptForExit()
        {
            Println("Press any key to exit.");
            if (!Silent)
            {
                Console.ReadKey();
            }

            while (true)
            {
                if (checkForUpdate.IsCompleted || checkForUpdate.IsCanceled || checkForUpdate.IsFaulted)
                {
                    Environment.Exit(0);
                }

                Thread.Sleep(1000);
            }
        }

        private static void Println(string message = null, string messagetype = "info")
        {
            int releaseId = int.Parse(Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", 0).ToString());
            if (!Silent)
            {
                lock (MessageLock)
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        Console.WriteLine();
                        return;
                    }

                    if (messagetype == "error")
                    {
                        if (releaseId >= 1511)
                        {
                            Console.Write("\u001b[91m[ERROR] ");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("[ERROR] ");
                        }
                    }

                    if (messagetype == "warning")
                    {
                        if (releaseId >= 1511)
                        {
                            Console.Write("\u001b[93m[WARNING]\u001b[97m ");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("[WARNING] ");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                    }

                    if (messagetype == "info")
                    {
                        if (releaseId >= 1511)
                        {
                            Console.Write("\u001b[97m");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                    }

                    if (messagetype == "success")
                    {
                        if (releaseId >= 1511)
                        {
                            Console.Write("\u001b[92m");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                        }
                    }

                    if (releaseId >= 1511)
                    {
                        Console.WriteLine(message + "\u001b[97m");
                    }
                    else
                    {
                        Console.WriteLine(message);
                        Console.ResetColor();
                    }
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
            using (FileStream file = new FileStream(friendscachefile, FileMode.Create))
            using (GZipStream gzip = new GZipStream(file, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.Level7))
            {
                Println("Overwriting original friends.css...");
                gzip.Write(decompressedcachefile, 0, decompressedcachefile.Length);
            }

            if (!File.Exists(steamDir + "\\clientui\\friends.custom.css"))
            {
                File.Create(steamDir + "\\clientui\\friends.custom.css").Dispose();
            }

            if (Process.GetProcessesByName("Steam").Length > 0 && Directory.Exists(steamDir) && File.Exists(steamDir + "\\clientui\\friends.custom.css") && FindFriendsWindow())
            {
                Println("Reloading friends window...");
                Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/offline");
                Thread.Sleep(1000);
                Process.Start(steamDir + "\\Steam.exe", @"steam://friends/status/online");
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
                {
                    Println("Download successful.", "success");
                }
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

            // List<string> validFiles = Directory.EnumerateFiles(cachepath, "f_*", SearchOption.TopDirectoryOnly).Where(file => file.Length / 1024d < maxKbFileSize).ToList();
            var validFiles = new DirectoryInfo(cachepath).EnumerateFiles("f_*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Length <= originalcss.Length)
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => f.FullName)
                .ToList();
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
                    if (decompressedcachefile.Length == originalcss.Length && ByteArrayCompare(decompressedcachefile, originalcss))
                    {
                        state.Stop();
                        Println("Success! Matching friends.css found at " + s, "success");
                        File.WriteAllBytes(steamDir + "\\clientui\\friends.original.css", Encoding.ASCII.GetBytes("/*" + etag + "*/\n").Concat(decompressedcachefile).ToArray());
                        friendscachefile = s;
                        PatchCacheFile(friendscachefile, decompressedcachefile);
                    }
                }
            });

            if (string.IsNullOrEmpty(friendscachefile))
            {
                if (Silent)
                {
                    PromptForExit();
                }

                bool validresponse = false;
                while (!validresponse)
                {
                    Println("friends.css location not found, would you like to clear your Steam cache and try again? Y/n", "error");
                    if (Process.GetProcessesByName("Steam").Length > 0 && Directory.Exists(steamDir))
                    {
                        Println("(Steam will be restarted automatically.)", "warning");
                    }

                    if (!Silent)
                    {
                        Console.Write("\u001b[97m> ");
                    }

                    var cki = Console.ReadKey();
                    var keypressed = cki.KeyChar.ToString().ToLower();
                    Println();
                    if (keypressed == "n")
                    {
                        validresponse = true;
                        Println("Could not find friends.css", "error");
                        Println("If Steam is not already patched please clear your Steam cache and try again or contact the developer.", "error");
                        PromptForExit();
                    }
                    else
                    {
                        validresponse = true;
                        if (Process.GetProcessesByName("Steam").Length > 0 && Directory.Exists(steamDir))
                        {
                            Println("Shutting down Steam to clear cache...");
                            Process.Start(steamDir + "\\Steam.exe", "-shutdown");

                            Stopwatch stopwatch = Stopwatch.StartNew();
                            while (Process.GetProcessesByName("Steam").Length > 0 && stopwatch.Elapsed.Seconds < 10)
                            {
                                Thread.Sleep(100);
                            }

                            stopwatch.Stop();
                            if (Process.GetProcessesByName("Steam").Length > 0 && stopwatch.Elapsed.Seconds >= 10)
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
                            {
                                Thread.Sleep(100);
                            }
                        }

                        FindCacheFile();
                    }
                }
            }
        }
    }
}