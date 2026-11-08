using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace LineInTray.WinUI.Interop
{
    public static class WindowInterop
    {
        public static IntPtr GetHandle(Window window) => WindowNative.GetWindowHandle(window);

        public static AppWindow GetAppWindow(Window window) =>
            AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(GetHandle(window)));

        public static void MakeFlyout(Window window, bool noActivate, bool border = true)
        {
            AppWindow appWindow = GetAppWindow(window);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(border, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }
            appWindow.IsShownInSwitchers = false;

            IntPtr hwnd = GetHandle(window);
            long style = (long)Native.GetWindowLongPtr(hwnd, Native.GwlExStyle);
            style |= Native.WsExToolWindow | Native.WsExTopmost;
            if (noActivate) style |= Native.WsExNoActivate;
            Native.SetWindowLongPtr(hwnd, Native.GwlExStyle, (IntPtr)style);

            if (!border)
            {
                long windowStyle = (long)Native.GetWindowLongPtr(hwnd, Native.GwlStyle);
                windowStyle &= ~(Native.WsCaption | Native.WsThickFrame | Native.WsBorder
                    | Native.WsDlgFrame | Native.WsSysMenu | Native.WsMinimizeBox | Native.WsMaximizeBox);
                windowStyle |= Native.WsPopup;
                Native.SetWindowLongPtr(hwnd, Native.GwlStyle, (IntPtr)windowStyle);
                Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    Native.SwpNoMove | Native.SwpNoSize | Native.SwpNoZOrder | Native.SwpNoActivate | Native.SwpFrameChanged);
            }

            int corner = Native.DwmwcpRound;
            Native.DwmSetWindowAttribute(hwnd, Native.DwmwaWindowCornerPreference, ref corner, sizeof(int));
        }

        public static void MakeLayered(Window window)
        {
            IntPtr hwnd = GetHandle(window);
            long style = (long)Native.GetWindowLongPtr(hwnd, Native.GwlExStyle);
            Native.SetWindowLongPtr(hwnd, Native.GwlExStyle, (IntPtr)(style | Native.WsExLayered));
            Native.SetLayeredWindowAttributes(hwnd, 0, 255, Native.LwaAlpha);
        }

        public static void SetWindowAlpha(Window window, byte alpha) =>
            Native.SetLayeredWindowAttributes(GetHandle(window), 0, alpha, Native.LwaAlpha);

        public static void MoveAndResize(Window window, PixelRect rect) =>
            Native.SetWindowPos(GetHandle(window), IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height,
                Native.SwpNoZOrder | Native.SwpNoActivate);

        public static void MoveTo(Window window, int x, int y) =>
            Native.SetWindowPos(GetHandle(window), IntPtr.Zero, x, y, 0, 0,
                Native.SwpNoSize | Native.SwpNoZOrder | Native.SwpNoActivate);

        public static void BringToForeground(Window window) =>
            Native.SetForegroundWindow(GetHandle(window));

        public static void EnsureTopmost(Window window) =>
            Native.SetWindowPos(GetHandle(window), Native.HwndTopmost, 0, 0, 0, 0,
                Native.SwpNoMove | Native.SwpNoSize | Native.SwpNoActivate);

        public static PixelRect GetWindowBounds(Window window)
        {
            Native.GetWindowRect(GetHandle(window), out Native.Rect rect);
            return PixelRect.FromLtrb(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static void MoveAndResize(Window window, int x, int y, int width, int height) =>
            Native.SetWindowPos(GetHandle(window), IntPtr.Zero, x, y, width, height,
                Native.SwpNoZOrder | Native.SwpNoActivate);

        public static DisplayArea GetDisplayAreaForPoint(int x, int y) =>
            DisplayArea.GetFromPoint(new PointInt32(x, y), DisplayAreaFallback.Primary);

        public static double ScaleForPoint(int x, int y) =>
            GetDisplayAreaForPoint(x, y).OuterBounds.Width > 0
                ? 1.0
                : 1.0;
    }
}


