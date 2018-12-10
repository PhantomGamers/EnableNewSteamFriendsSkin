namespace EnableNewSteamFriendsSkin
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;

    using Microsoft.Win32.SafeHandles;

    using GZipStream = Ionic.Zlib.GZipStream;

    /// <summary>
    /// General utility functions
    /// </summary>
    internal class Util
    {
        private const int MYCODEPAGE = 437;
        private const uint GENERICWRITE = 0x40000000;
        private const uint GENERICREAD = 0x80000000;
        private const uint ENABLEVIRTUALTERMINALINPUT = 0x0200;
        private const uint ENABLEVIRTUALTERMINALPROCESSING = 0x0004;
        private const uint DISABLENEWLINEAUTORETURN = 0x0008;
        private const int STDINPUTHANDLE = -10;
        private const int STDOUTPUTHANDLE = -11;
        private const uint FILESHAREWRITE = 0x2;
        private const uint OPENEXISTING = 0x3;
        private static Mutex m;

        // GZIP utility methods.

        /// <summary>
        /// Checks the first two bytes in a GZIP file, which must be 31 and 139.
        /// </summary>
        /// <param name="arr">The byte array to check</param>
        /// <returns>Whether or not the byte array contains a GZip Header</returns>
        internal static bool IsGZipHeader(byte[] arr)
        {
            return arr.Length >= 2 &&
                arr[0] == 31 &&
                arr[1] == 139;
        }

        /// <summary>
        /// Decompresses byte array to new byte array.
        /// </summary>
        /// <param name="gzip">Gzipped byte array to decompress</param>
        /// <returns>Returns a decompressed byte array</returns>
        internal static byte[] Decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(
                new MemoryStream(gzip),
                Ionic.Zlib.CompressionMode.Decompress))
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

        /// <summary>
        /// Creates a console window
        /// </summary>
        internal static void CreateConsole()
        {
            if (!Program.Silent)
            {
                AllocConsole();
                try
                {
                    // Console.OpenStandardOutput eventually calls into GetStdHandle. As per MSDN documentation of GetStdHandle: http://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx will return the redirected handle and not the allocated console:
                    // "The standard handles of a process may be redirected by a call to  SetStdHandle, in which case  GetStdHandle returns the redirected handle. If the standard handles have been redirected, you can specify the CONIN$ value in a call to the CreateFile function to get a handle to a console's input buffer. Similarly, you can specify the CONOUT$ value to get a handle to a console's active screen buffer."
                    // Get the handle to CONOUT$.
                    IntPtr stdHandle = CreateFile("CONOUT$", GENERICWRITE | GENERICREAD, FILESHAREWRITE, 0, OPENEXISTING, 0, 0);
                    SafeFileHandle safeFileHandle = new SafeFileHandle(stdHandle, true);
                    SetStdHandle(STDOUTPUTHANDLE, stdHandle);
                    var iStdIn = GetStdHandle(STDINPUTHANDLE);
                    FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                    StreamWriter standardOutput = new StreamWriter(fileStream)
                    {
                        AutoFlush = true
                    };
                    Version ver = Assembly.GetEntryAssembly().GetName().Version;
                    Console.Title = $"Steam Friends Skin Patcher v{ver.Major}.{ver.Minor}{(ver.Build > 0 ? ("." + ver.Build) : string.Empty)}";
                    Console.SetOut(standardOutput);
                    if (GetConsoleMode(stdHandle, out var cMode))
                    {
                        SetConsoleMode(stdHandle, cMode | ENABLEVIRTUALTERMINALPROCESSING | DISABLENEWLINEAUTORETURN);
                    }

                    if (GetConsoleMode(iStdIn, out uint cModeIn))
                    {
                        SetConsoleMode(iStdIn, cModeIn | ENABLEVIRTUALTERMINALINPUT);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Compares two byte arrays for a match.
        /// </summary>
        /// <param name="b1">First byte array</param>
        /// <param name="b2">Second byte array</param>
        /// <returns>Returns whether or not the two arrays match</returns>
        internal static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.
            return b1.Length == b2.Length && Memcmp(b1, b2, b1.Length) == 0;
        }

        /// <summary>
        /// Determines whether this is the only instance of the program running
        /// </summary>
        /// <returns>Returns whether or not this is the only instance of the program running</returns>
        internal static bool IsSingleInstance()
        {
            try
            {
                // Try to open existing mutex.
                Mutex.OpenExisting("EnableNewSteamFriendsSkin");
            }
            catch
            {
                // If exception occurred, there is no such mutex.
                Program.m = new Mutex(true, "EnableNewSteamFriendsSkin");

                // Only one instance.
                return true;
            }

            // More than one instance.
            return false;
        }

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("msvcrt.dll", EntryPoint = "memcmp", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Memcmp(byte[] b1, byte[] b2, long count);
    }
}