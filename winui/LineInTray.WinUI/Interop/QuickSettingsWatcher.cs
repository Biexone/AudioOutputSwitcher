using System;
using System.Runtime.InteropServices;
using System.Text;
using LineInTray.WinUI.Interop;

namespace LineInTray.WinUI.Interop
{
    /// <summary>
    /// Watches ShellHost's ControlCenterWindow with out-of-context WinEvent hooks. The events
    /// arrive on the UI thread; the panel follows the window region of the Quick Settings cards.
    /// </summary>
    public sealed class QuickSettingsWatcher : IDisposable
    {
        private const string WindowClass = "ControlCenterWindow";
        private const uint EventSystemForeground = 0x0003;
        private const uint EventObjectDestroy = 0x8001;
        private const uint EventObjectShow = 0x8002;
        private const uint EventObjectHide = 0x8003;
        private const uint EventObjectCloaked = 0x8017;
        private const uint EventObjectUncloaked = 0x8018;
        private const uint WineventOutOfContext = 0x0000;
        private const uint WineventSkipOwnProcess = 0x0002;
        private const int ObjidWindow = 0;

        private readonly WinEventDelegate _callback;
        private readonly System.Collections.Generic.List<IntPtr> _hooks = new();
        private IntPtr _window;
        private bool _open;
        private bool _leaving;
        private long _openedAt;

        public QuickSettingsWatcher()
        {
            _callback = OnWinEvent;
        }

        public event EventHandler? Opened;
        public event EventHandler? Closing;
        public bool IsOpen => _open && !_leaving;
        public bool IsRunning => _hooks.Count > 0;
        public static bool IsSupported => Environment.OSVersion.Version.Build >= 26100;

        public void Start()
        {
            if (IsRunning || !IsSupported) return;
            Hook(EventSystemForeground, EventSystemForeground);
            Hook(EventObjectDestroy, EventObjectHide);
            Hook(EventObjectCloaked, EventObjectUncloaked);
            if (FindOpenWindow() is { } open) Handle(EventObjectShow, open);
        }

        public void Stop()
        {
            foreach (IntPtr hook in _hooks) UnhookWinEvent(hook);
            _hooks.Clear();
            _window = IntPtr.Zero;
            _open = false;
            _leaving = false;
        }

        public PixelRect? GetCards()
        {
            if (!IsOpen || _window == IntPtr.Zero) return null;
            if (!Native.GetWindowRect(_window, out Native.Rect window)) return null;
            if (Native.GetWindowRgnBox(_window, out Native.Rect box) <= 0) return null;
            PixelRect cards = PixelRect.FromLtrb(
                window.Left + box.Left,
                window.Top + box.Top,
                window.Left + box.Right,
                window.Top + box.Bottom);
            return cards.Width > 40 && cards.Height > 40 ? cards : null;
        }

        private void Hook(uint first, uint last)
        {
            IntPtr hook = SetWinEventHook(first, last, IntPtr.Zero, _callback, 0, 0,
                WineventOutOfContext | WineventSkipOwnProcess);
            if (hook != IntPtr.Zero) _hooks.Add(hook);
        }

        private void OnWinEvent(IntPtr hook, uint winEvent, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != ObjidWindow || idChild != 0 || hwnd == IntPtr.Zero) return;
            try { Handle(winEvent, hwnd); }
            catch (Exception ex) { Log.Exception("Quick Settings 事件处理失败", ex); }
        }

        private void Handle(uint winEvent, IntPtr hwnd)
        {
            switch (winEvent)
            {
                case EventSystemForeground:
                    if (!_open) return;
                    if (hwnd == _window)
                    {
                        if (!_leaving) return;
                        _leaving = false;
                        Opened?.Invoke(this, EventArgs.Empty);
                    }
                    else if (!_leaving && Environment.TickCount64 - _openedAt > 250)
                    {
                        _leaving = true;
                        Closing?.Invoke(this, EventArgs.Empty);
                    }
                    return;

                case EventObjectShow:
                case EventObjectUncloaked:
                    if (hwnd != _window && !IsQuickSettings(hwnd)) return;
                    if (!IsOnScreen(hwnd)) return;
                    _window = hwnd;
                    if (IsOpen) return;
                    _open = true;
                    _leaving = false;
                    _openedAt = Environment.TickCount64;
                    Opened?.Invoke(this, EventArgs.Empty);
                    return;

                case EventObjectHide:
                case EventObjectCloaked:
                case EventObjectDestroy:
                    if (hwnd != _window || !_open) return;
                    bool announced = _leaving;
                    _open = false;
                    _leaving = false;
                    if (winEvent == EventObjectDestroy) _window = IntPtr.Zero;
                    if (!announced) Closing?.Invoke(this, EventArgs.Empty);
                    return;
            }
        }

        private static bool IsQuickSettings(IntPtr hwnd)
        {
            StringBuilder name = new StringBuilder(WindowClass.Length + 2);
            int length = Native.GetClassName(hwnd, name, name.Capacity);
            return length == WindowClass.Length && name.ToString() == WindowClass;
        }

        private static bool IsOnScreen(IntPtr hwnd)
        {
            if (!Native.IsWindowVisible(hwnd)) return false;
            return Native.DwmGetWindowAttribute(hwnd, Native.DwmwaCloaked, out int cloaked, sizeof(int)) != 0 || cloaked == 0;
        }

        private static IntPtr? FindOpenWindow()
        {
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows((hwnd, _) =>
            {
                if (IsQuickSettings(hwnd) && IsOnScreen(hwnd))
                {
                    found = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found == IntPtr.Zero ? null : found;
        }

        private delegate void WinEventDelegate(IntPtr hook, uint winEvent, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventDelegate callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        public void Dispose() => Stop();
    }
}

