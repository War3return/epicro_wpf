using System;
using System.Runtime.InteropServices;

namespace epicro_wpf.Helpers
{
    public static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        public static RECT GetWindowRect(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out var rect);
            return rect;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}