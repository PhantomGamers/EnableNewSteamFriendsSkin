using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowSearch
{
    internal class FindWindowLike
    {
        internal class Window
        {
            internal string Title;
            internal string Class;
            internal int Handle;
        }

        [DllImport("user32")]
        private static extern int GetWindow(int hwnd, int wCmd);

        [DllImport("user32")]
        private static extern int GetDesktopWindow();

        [DllImport("user32", EntryPoint = "GetWindowLongA")]
        private static extern int GetWindowLong(int hwnd, int nIndex);

        [DllImport("user32")]
        private static extern int GetParent(int hwnd);

        [DllImport("user32", EntryPoint = "GetClassNameA")]
        private static extern int GetClassName(
          int hWnd, [Out] StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32", EntryPoint = "GetWindowTextA")]
        private static extern int GetWindowText(
          int hWnd, [Out] StringBuilder lpString, int nMaxCount);

        private const int GWL_ID = (-12);
        private const int GW_HWNDNEXT = 2;
        private const int GW_CHILD = 5;

        public static Window[] Find(int hwndStart, string findText, string findClassName)
        {
            ArrayList windows = DoSearch(hwndStart, findText, findClassName);

            return (Window[])windows.ToArray(typeof(Window));
        } //Find

        private static ArrayList DoSearch(int hwndStart, string findText, string findClassName)
        {
            ArrayList list = new ArrayList();

            if (hwndStart == 0)
                hwndStart = GetDesktopWindow();

            int hwnd = GetWindow(hwndStart, GW_CHILD);

            while (hwnd != 0)
            {
                // Recursively search for child windows.
                list.AddRange(DoSearch(hwnd, findText, findClassName));

                StringBuilder text = new StringBuilder(255);
                int rtn = GetWindowText(hwnd, text, 255);
                string windowText = text.ToString();
                windowText = windowText.Substring(0, rtn);

                StringBuilder cls = new StringBuilder(255);
                rtn = GetClassName(hwnd, cls, 255);
                string className = cls.ToString();
                className = className.Substring(0, rtn);

                if (GetParent(hwnd) != 0)
                    rtn = GetWindowLong(hwnd, GWL_ID);

                if (windowText.Length > 0 && windowText.StartsWith(findText) &&
                  (className.Length == 0 || className.StartsWith(findClassName)))
                {
                    Window currentWindow = new Window
                    {
                        Title = windowText,
                        Class = className,
                        Handle = hwnd
                    };

                    list.Add(currentWindow);
                }

                hwnd = GetWindow(hwnd, GW_HWNDNEXT);
            }

            return list;
        }
    }
}