using System;
using System.Runtime.InteropServices;

namespace LineInTray.WinUI.Interop
{
    public readonly record struct PixelRect(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public static PixelRect FromLtrb(int left, int top, int right, int bottom) =>
            new PixelRect(left, top, right - left, bottom - top);
    }

    public static class Native
    {
        public const int GwlExStyle = -20;
        public const int GwlStyle = -16;
        public const long WsExToolWindow = 0x00000080;
        public const long WsExNoActivate = 0x08000000;
        public const long WsExTopmost = 0x00000008;
        public const long WsExLayered = 0x00080000;
        public const long WsPopup = unchecked((long)0x80000000);
        public const long WsCaption = 0x00C00000;
        public const long WsBorder = 0x00800000;
        public const long WsDlgFrame = 0x00400000;
        public const long WsSysMenu = 0x00080000;
        public const long WsThickFrame = 0x00040000;
        public const long WsMinimizeBox = 0x00020000;
        public const long WsMaximizeBox = 0x00010000;
        public const uint LwaAlpha = 0x2;
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpFrameChanged = 0x0020;
        public const int DwmwaWindowCornerPreference = 33;
        public const int DwmwcpRound = 2;
        public const int DwmwaCloaked = 14;
        public static readonly IntPtr HwndTopmost = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hwnd, ref Point point);

        [DllImport("user32.dll")]
        public static extern int GetWindowRgnBox(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string? className, string? windowName);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int count);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int key);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X, Y;
        }
    }
}


