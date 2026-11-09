using System;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LineInTray.WinUI.Interop;

namespace LineInTray.WinUI
{
    public partial class App : Application
    {
        private MainWindow? _main;
        private QuickPanelWindow? _quickPanel;
        private QuickSettingsPanelWindow? _quickSettings;
        private QuickSettingsWatcher? _watcher;
        private DispatcherQueueTimer? _followTimer;
        private TaskbarIcon? _tray;
        private DispatcherQueueTimer? _hotkeyTimer;
        private bool _hotkeyDown;
        private bool _switchHotkeyDown;
        private bool _monitorHotkeyDown;

        public static App CurrentApp => (App)Current;
        public AppController Controller { get; private set; } = null!;

        public App()
        {
            InitializeComponent();
            UnhandledException += (_, e) =>
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui-crash.txt"), DateTime.Now + " UI: " + e.Exception + Environment.NewLine); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui-crash.txt"), DateTime.Now + " DOMAIN: " + e.ExceptionObject + Environment.NewLine); } catch { }
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
            DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
            Controller = new AppController(dispatcher);
            Controller.ThemeChanged += (_, _) => ApplyTheme();
            Controller.LanguageChanged += ApplyLanguageAll;
            Controller.MonitoringEnabledChanged += OnMonitoringEnabledChanged;
            _quickPanel = new QuickPanelWindow(Controller);

            CreateTray();
            StartQuickSettings();
            StartHotkey();

            ApplyTheme();
            if (Controller.Settings.AutoMonitorOnStart && Controller.MonitoringEnabled)
            {
                Controller.StartMonitor();
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--main") >= 0)
                {
                    ShowMainWindow();
                }
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "--autostart") < 0)
                {
                    _quickPanel.ShowNearTray();
                }
            }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui-crash.txt"), DateTime.Now + " LAUNCH: " + ex + Environment.NewLine); } catch { }
                throw;
            }
        }

        public void ShowMainWindow()
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui.log"), DateTime.Now + " ShowMainWindow enter" + Environment.NewLine); } catch { }
            if (_main == null)
            {
                _main = new MainWindow(Controller);
                _main.Closed += (_, _) => _main = null;
                ApplyTheme(_main, Controller.Theme);
            }
            _main.Activate();
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui.log"), DateTime.Now + " ShowMainWindow activated" + Environment.NewLine); } catch { }
            _quickPanel?.HidePanel();
            _quickSettings?.Leave();
        }

        public void ShowQuickPanel()
        {
            _quickPanel?.ShowNearTray();
        }

        public void ExitApp()
        {
            _watcher?.Dispose();
            _followTimer?.Stop();
            _tray?.Dispose();
            Controller.Dispose();
            Environment.Exit(0);
        }

        private void StartHotkey()
        {
            _hotkeyTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _hotkeyTimer.Interval = TimeSpan.FromMilliseconds(100);
            _hotkeyTimer.Tick += (_, _) => CheckHotkeys();
            _hotkeyTimer.Start();
        }

        private void CheckHotkeys()
        {
            HotkeyBinding.TryParse("Ctrl+Alt+Shift+L", out HotkeyBinding panel);
            bool panelDown = panel.IsDown();
            if (panelDown && !_hotkeyDown) ShowQuickPanel();
            _hotkeyDown = panelDown;

            HotkeyBinding.TryParse(Controller.Settings.HotkeySwitchOutput, out HotkeyBinding output);
            bool outputDown = output.IsDown();
            if (outputDown && !_switchHotkeyDown) Controller.CycleOutputPreset();
            _switchHotkeyDown = outputDown;

            HotkeyBinding.TryParse(Controller.Settings.HotkeyToggleMonitor, out HotkeyBinding monitor);
            bool monitorDown = monitor.IsDown();
            if (monitorDown && !_monitorHotkeyDown) Controller.ToggleMonitor();
            _monitorHotkeyDown = monitorDown;
        }

        private MenuFlyout? _trayMenu;

        private static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "LineInTray.ico");
                if (System.IO.File.Exists(path)) return new System.Drawing.Icon(path);
            }
            catch { }
            try
            {
                using System.IO.Stream? stream = typeof(App).Assembly.GetManifestResourceStream("LineInTray.WinUI.Assets.LineInTray.ico");
                if (stream != null) return new System.Drawing.Icon(stream);
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        private MenuFlyout BuildTrayMenu()
        {
            MenuFlyout menu = new MenuFlyout();
            MenuFlyoutItem open = new MenuFlyoutItem { Text = Loc.T("tray.openpanel"), Command = new RelayCommand(ShowQuickPanel) };
            MenuFlyoutItem main = new MenuFlyoutItem { Text = Loc.T("tray.openmain"), Command = new RelayCommand(ShowMainWindow) };
            MenuFlyoutItem toggle = new MenuFlyoutItem { Text = Loc.T("tray.toggle"), IsEnabled = Controller.MonitoringEnabled, Command = new RelayCommand(Controller.ToggleMonitor) };
            MenuFlyoutItem exit = new MenuFlyoutItem { Text = Loc.T("tray.exit"), Command = new RelayCommand(ExitApp) };
            menu.Items.Add(open);
            menu.Items.Add(main);
            menu.Items.Add(toggle);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        private void CreateTray()
        {
            _trayMenu = BuildTrayMenu();
            _tray = new TaskbarIcon
            {
                ToolTipText = Loc.T("tray.tooltip"),
                Icon = LoadAppIcon(),
                ContextMenuMode = ContextMenuMode.PopupMenu,
                ContextFlyout = _trayMenu,
                LeftClickCommand = new RelayCommand(ShowQuickPanel)
            };
            _tray.ForceCreate(enablesEfficiencyMode: false);
            Controller.TrayIcon = _tray;
        }

        private void ShowNativeTrayMenu()
        {
            IntPtr menu = Native.CreatePopupMenu();
            try
            {
                Native.AppendMenuW(menu, Native.MfString, new UIntPtr(1), Loc.T("tray.openpanel"));
                Native.AppendMenuW(menu, Native.MfString, new UIntPtr(2), Loc.T("tray.openmain"));
                if (Controller.MonitoringEnabled)
                {
                    Native.AppendMenuW(menu, Native.MfString, new UIntPtr(3), Loc.T("tray.toggle"));
                }
                Native.AppendMenuW(menu, Native.MfSeparator, UIntPtr.Zero, "");
                Native.AppendMenuW(menu, Native.MfString, new UIntPtr(4), Loc.T("tray.exit"));

                IntPtr owner = Native.GetForegroundWindow();
                if (owner == IntPtr.Zero)
                {
                    try { if (_quickPanel != null) owner = WindowInterop.GetHandle(_quickPanel); } catch { }
                }
                if (owner != IntPtr.Zero) Native.SetForegroundWindow(owner);
                Native.GetCursorPos(out Native.Point point);
                uint command = Native.TrackPopupMenuEx(menu,
                    Native.TpmReturnCmd | Native.TpmRightButton,
                    point.X, point.Y, owner, IntPtr.Zero);
                if (owner != IntPtr.Zero) Native.PostMessage(owner, 0, IntPtr.Zero, IntPtr.Zero);

                switch (command)
                {
                    case 1: ShowQuickPanel(); break;
                    case 2: ShowMainWindow(); break;
                    case 3: Controller.ToggleMonitor(); break;
                    case 4: ExitApp(); break;
                }
            }
            finally
            {
                Native.DestroyMenu(menu);
            }
        }
        private void OnMonitoringEnabledChanged()
        {
            _trayMenu = BuildTrayMenu();
            if (_tray != null) _tray.ContextFlyout = _trayMenu;
        }

        private void ApplyLanguageAll()
        {
            _quickPanel?.ApplyLanguage();
            _quickSettings?.ApplyLanguage();
            _main?.ApplyLanguage();
            _trayMenu = BuildTrayMenu();
            if (_tray != null)
            {
                _tray.ContextFlyout = _trayMenu;
                _tray.ToolTipText = Loc.T("tray.tooltip");
            }
        }
        private void StartQuickSettings()
        {
            if (!QuickSettingsWatcher.IsSupported) return;
            _watcher = new QuickSettingsWatcher();
            _followTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _followTimer.Interval = TimeSpan.FromMilliseconds(50);
            _followTimer.Tick += (_, _) => FollowQuickSettings();
            _watcher.Opened += (_, _) =>
            {
                _quickPanel?.HideImmediately();
                _followTimer.Start();
                FollowQuickSettings();
            };
            _watcher.Closing += (_, _) =>
            {
                _followTimer.Stop();
                _quickSettings?.Leave();
            };
            _watcher.Start();
        }

        private void FollowQuickSettings()
        {
            if (_watcher == null || !_watcher.IsOpen)
            {
                _quickSettings?.Leave();
                return;
            }
            PixelRect? cards = _watcher.GetCards();
            if (cards == null)
            {
                _quickSettings?.Leave();
                return;
            }
            if (_quickSettings == null)
            {
                _quickSettings = new QuickSettingsPanelWindow(Controller);
                ApplyTheme(_quickSettings, Controller.QuickTheme);
            }
            _quickSettings.Track(cards.Value);
        }

        private void ApplyTheme()
        {
            ApplyTheme(_quickPanel, Controller.Theme);
            ApplyTheme(_quickSettings, Controller.QuickTheme);
            ApplyTheme(_main, Controller.Theme);
        }

        private static void ApplyTheme(Window? window, AppTheme theme)
        {
            if (window?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    AppTheme.Light => ElementTheme.Light,
                    AppTheme.Dark => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }
    }

    internal sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}




















