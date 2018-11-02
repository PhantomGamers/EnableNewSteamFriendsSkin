using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace EnableNewSteamFriendsSkin
{
    internal class Util
    {
        #region GZIP Proccessing from dotnetperls

        /// <summary>
        /// GZIP utility methods.
        /// </summary>
        ///

        /// <summary>
        /// Checks the first two bytes in a GZIP file, which must be 31 and 139.
        /// </summary>

        internal static bool IsGZipHeader(byte[] arr)
        {
            return arr.Length >= 2 &&
                arr[0] == 31 &&
                arr[1] == 139;
        }

        /// <summary>
        /// Compresses byte array to new byte array.
        /// </summary>
        internal static byte[] Compress(byte[] raw)
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

        internal static byte[] Decompress(byte[] gzip)
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

        #endregion GZIP Proccessing from dotnetperls

        #region Custom Console

        internal static void CreateConsole()
        {
            if (!Program.silent)
            {
                AllocConsole();
                try
                {
                    // Console.OpenStandardOutput eventually calls into GetStdHandle. As per MSDN documentation of GetStdHandle: http://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx will return the redirected handle and not the allocated console:
                    // "The standard handles of a process may be redirected by a call to  SetStdHandle, in which case  GetStdHandle returns the redirected handle. If the standard handles have been redirected, you can specify the CONIN$ value in a call to the CreateFile function to get a handle to a console's input buffer. Similarly, you can specify the CONOUT$ value to get a handle to a console's active screen buffer."
                    // Get the handle to CONOUT$.
                    IntPtr stdHandle = CreateFile("CONOUT$", GENERIC_WRITE | GENERIC_READ, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);
                    SafeFileHandle safeFileHandle = new SafeFileHandle(stdHandle, true);
                    SetStdHandle(STD_OUTPUT_HANDLE, stdHandle);
                    var iStdIn = GetStdHandle(STD_INPUT_HANDLE);
                    FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                    //Encoding encoding = System.Text.Encoding.GetEncoding(MY_CODE_PAGE);
                    StreamWriter standardOutput = new StreamWriter(fileStream)
                    {
                        AutoFlush = true
                    };
                    Version ver = Assembly.GetEntryAssembly().GetName().Version;
                    Console.Title = "Steam Friends Skin Patcher v" + ver.Major + "." + ver.Minor + "." + ver.Build;
                    Console.SetOut(standardOutput);
                    if (GetConsoleMode(stdHandle, out var cMode))
                        SetConsoleMode(stdHandle, cMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN);
                    if (GetConsoleMode(iStdIn, out uint cModeIn))
                        SetConsoleMode(iStdIn, cModeIn | ENABLE_VIRTUAL_TERMINAL_INPUT);
                }
                catch (Exception) { }
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        [DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
        [DllImport("kernel32.dll")]
        static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        private const int MY_CODE_PAGE = 437;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint GENERIC_READ = 0x80000000;
        private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
        private const int STD_INPUT_HANDLE = -10;
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

        #endregion Custom Console

        #region ByteArrayCompare

        internal static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.
            return b1.Length == b2.Length && Memcmp(b1, b2, b1.Length) == 0;
        }

        [DllImport("msvcrt.dll", EntryPoint = "memcmp", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Memcmp(byte[] b1, byte[] b2, long count);

        #endregion ByteArrayCompare
    }
}