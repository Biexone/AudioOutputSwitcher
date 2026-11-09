using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using LineInTray.WinUI.Interop;
using LineInTray;
using Windows.Graphics;

namespace LineInTray.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private AppController _app = null!;
        private bool _updating;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(AppController app) : this()
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui.log"), DateTime.Now + " MainWindow ctor" + Environment.NewLine); } catch { }
            _app = app;
            Loc.Apply(Root);
            Title = Loc.T("main.title");
            try
            {
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "LineInTray.ico");
                if (System.IO.File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
            }
            catch { }
            Root.Loaded += (_, _) =>
            {
                Loc.Apply(Root);
                Refresh();
                MainScroll.ChangeView(0, 0, null);
            };
            SystemBackdrop = new MicaBackdrop();
            AppWindow.Resize(new SizeInt32(1000, 820));
            AppWindow.Closing += OnClosing;
            _app.StateChanged += Refresh;
            _app.PresetsChanged += Refresh;
            Refresh();
        }

        private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            sender.Hide();
        }

        private void Refresh()
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(Refresh);
                return;
            }

            _updating = true;
            try
            {
                MonitoringEnabledCheck.IsChecked = _app.MonitoringEnabled;
                MonitorControls.Visibility = _app.MonitoringEnabled ? Visibility.Visible : Visibility.Collapsed;
                MonitoringOffText.Visibility = _app.MonitoringEnabled ? Visibility.Collapsed : Visibility.Visible;
                DefaultCaptureText.Text = Loc.T("main.defaultcapture") + _app.CurrentInputName;
                bool monitoring = _app.IsMonitoring;
                MonitorText.Text = monitoring ? Loc.T("panel.monitor.stop") : Loc.T("panel.monitor.start");
                MonitorIcon.Glyph = monitoring ? "\uE71A" : "\uE768";
                MonitorButton.Style = (Style)Application.Current.Resources[monitoring ? "DefaultButtonStyle" : "AccentButtonStyle"];
                StatusText.Text = _app.StatusText;
                StatusDot.Fill = (Brush)Application.Current.Resources[monitoring ? "SystemFillColorSuccessBrush" : "TextFillColorSecondaryBrush"];
                LatencyBox.Value = _app.Settings.LatencyMs;
                OutputHotkeyBox.Text = _app.Settings.HotkeySwitchOutput;
                MonitorHotkeyBox.Text = _app.Settings.HotkeyToggleMonitor;
                AutoStartCheck.IsChecked = _app.IsAutoStartEnabled();
                AutoMonitorCheck.IsChecked = _app.Settings.AutoMonitorOnStart;

                List<EndpointItem> captures = new List<EndpointItem>();
                EndpointInfo? current = null;
                foreach (EndpointInfo endpoint in _app.CaptureEndpoints())
                {
                    captures.Add(new EndpointItem(endpoint));
                    if (string.Equals(endpoint.Id, _app.Settings.CaptureEndpointId, StringComparison.OrdinalIgnoreCase))
                    {
                        current = endpoint;
                    }
                }
                CaptureCombo.ItemsSource = captures;
                CaptureCombo.SelectedItem = FindItem(captures, current);
                DeviceButton.Content = current != null && current.IsActive ? Loc.T("main.device.disable") : Loc.T("main.device.enable");

                List<PlaybackItem> playback = new List<PlaybackItem>();
                playback.Add(new PlaybackItem(null));
                foreach (EndpointInfo endpoint in _app.RenderEndpoints())
                {
                    playback.Add(new PlaybackItem(endpoint));
                }
                PlaybackCombo.ItemsSource = playback;
                PlaybackItem? selectedPlayback = null;
                foreach (PlaybackItem item in playback)
                {
                    if (item.Endpoint != null && string.Equals(item.Endpoint.Id, _app.Settings.PlaybackEndpointId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedPlayback = item;
                    }
                }
                PlaybackCombo.SelectedItem = selectedPlayback ?? playback[0];

                ThemeCombo.ItemsSource = new[] { Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark") };
                ThemeCombo.SelectedIndex = ThemeIndex(_app.Theme);
                QuickThemeCombo.ItemsSource = new[] { Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark") };
                QuickThemeCombo.SelectedIndex = ThemeIndex(_app.QuickTheme);
                LanguageCombo.ItemsSource = new[] { Loc.T("lang.system"), Loc.T("lang.english"), Loc.T("lang.chinese") };
                LanguageCombo.SelectedIndex = LanguageIndex(_app.Language);

                PresetList.Children.Clear();
                foreach (AudioPreset preset in _app.Presets)
                {
                    PresetList.Children.Add(CreatePresetRow(preset));
                }
                if (_app.Presets.Count == 0)
                {
                    PresetList.Children.Add(new TextBlock
                    {
                        Text = "还没有快捷方式。点击“+ 添加快捷方式”创建。",
                        Style = (Style)Application.Current.Resources["PanelSubtitleTextStyle"]
                    });
                }
            }
            finally
            {
                _updating = false;
            }
        }

        private static EndpointItem? FindItem(List<EndpointItem> items, EndpointInfo? endpoint)
        {
            if (endpoint == null) return items.Count > 0 ? items[0] : null;
            foreach (EndpointItem item in items)
            {
                if (item.Endpoint.Id == endpoint.Id) return item;
            }
            return null;
        }

        private static int LanguageIndex(string language)
        {
            if (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) || string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase) || string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }

        private static string LanguageFromIndex(int index) => index == 1 ? "en-US" : index == 2 ? "zh-CN" : "system";

        private static int ThemeIndex(AppTheme theme) => theme == AppTheme.Light ? 1 : theme == AppTheme.Dark ? 2 : 0;

        private static AppTheme ThemeFromIndex(int index) => index == 1 ? AppTheme.Light : index == 2 ? AppTheme.Dark : AppTheme.System;

        private UIElement CreatePresetRow(AudioPreset preset)
        {
            bool active = _app.IsPresetActive(preset);
            Border card = new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources[active ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(active ? 1.5 : 1)
            };
            Grid grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border tile = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            tile.Child = new FontIcon
            {
                Glyph = IconGlyph(preset.Icon),
                FontSize = 20,
                Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
            };
            grid.Children.Add(tile);

            StackPanel text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(preset.Name) ? "Unnamed input" : Loc.Translate(preset.Name),
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            });
            text.Children.Add(new TextBlock
            {
                Text = _app.PresetSubtitle(preset),
                Style = (Style)Application.Current.Resources["PanelSubtitleTextStyle"]
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            if (active)
            {
                actions.Children.Add(new TextBlock
                {
                    Text = Loc.T("preset.active"),
                    Style = (Style)Application.Current.Resources["PanelSubtitleTextStyle"],
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                });
            }
            Button use = new Button { Content = Loc.T("preset.use"), MinWidth = 0, Padding = new Thickness(10, 4, 10, 4), MinHeight = 0 };
            use.Click += (_, _) => _app.ActivatePreset(preset);
            Button edit = new Button { Content = Loc.T("preset.edit"), MinWidth = 0, Padding = new Thickness(10, 4, 10, 4), MinHeight = 0 };
            edit.Click += (_, _) => _ = EditPresetAsync(preset);
            Button delete = new Button { Content = Loc.T("preset.delete"), MinWidth = 0, Padding = new Thickness(10, 4, 10, 4), MinHeight = 0 };
            delete.Click += (_, _) => _app.DeletePreset(preset);
            actions.Children.Add(use);
            actions.Children.Add(edit);
            actions.Children.Add(delete);
            Grid.SetColumn(actions, 2);
            grid.Children.Add(actions);

            card.Child = grid;
            return card;
        }

        private static string IconGlyph(string? icon)
        {
            string value = icon ?? "builtin:linein";
            switch (value)
            {
                case "builtin:mic": return "\uE720";
                case "builtin:headphone": return "\uE7F6";
                case "builtin:speaker": return "\uE767";
                case "builtin:music": return "\uE8D6";
                case "builtin:game": return "\uE7FC";
                case "builtin:phone": return "\uE8EA";
                case "builtin:aux": return "\uE7F4";
                default: return "\uE7F5";
            }
        }

        private async void OnMonitoringEnabledClick(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            await _app.SetMonitoringEnabledAsync(MonitoringEnabledCheck.IsChecked == true);
        }

        private void OnMonitorClick(object sender, RoutedEventArgs e) => _app.ToggleMonitor();

        private void OnCaptureChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating) return;
            if (CaptureCombo.SelectedItem is EndpointItem item)
            {
                _app.SetDefaultCapture(item.Endpoint);
            }
        }

        private void OnPlaybackChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating) return;
            if (PlaybackCombo.SelectedItem is PlaybackItem item)
            {
                _app.SetPlayback(item.Endpoint?.Id ?? "");
            }
        }

        private void OnToggleDeviceClick(object sender, RoutedEventArgs e)
        {
            if (!(CaptureCombo.SelectedItem is EndpointItem item)) return;
            try
            {
                PolicyConfig.SetVisibility(item.Endpoint.Id, !item.Endpoint.IsActive);
                System.Threading.Thread.Sleep(250);
                _app.RefreshDevices();
            }
            catch (Exception ex)
            {
                Log.Exception("切换设备状态失败", ex);
            }
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e) => _app.RefreshDevices();

        private void OnLatencyChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_updating || double.IsNaN(args.NewValue)) return;
            _app.SetLatency((int)args.NewValue);
        }

        private void OnHotkeyBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not TextBox box) return;
            uint key = (uint)e.Key;
            if (key == 0) return;
            if (key == 0x10 || key == 0x11 || key == 0x12 || key == 0x5B || key == 0x5C) return;

            uint modifiers = 0;
            if ((Native.GetAsyncKeyState(0x11) & 0x8000) != 0) modifiers |= HotkeyBinding.ModControl;
            if ((Native.GetAsyncKeyState(0x12) & 0x8000) != 0) modifiers |= HotkeyBinding.ModAlt;
            if ((Native.GetAsyncKeyState(0x10) & 0x8000) != 0) modifiers |= HotkeyBinding.ModShift;
            if (((Native.GetAsyncKeyState(0x5B) | Native.GetAsyncKeyState(0x5C)) & 0x8000) != 0) modifiers |= HotkeyBinding.ModWin;

            if ((key == 0x08 || key == 0x2E) && modifiers == 0)
            {
                box.Text = "";
                SaveHotkeys();
                e.Handled = true;
                return;
            }

            string? name = HotkeyBinding.KeyName(key);
            bool functionKey = key >= 0x70 && key <= 0x87;
            if (name == null || (modifiers == 0 && !functionKey)) return;

            box.Text = HotkeyBinding.Format(modifiers, key);
            SaveHotkeys();
            e.Handled = true;
        }

        private void SaveHotkeys()
        {
            _app.SetHotkeys(OutputHotkeyBox.Text, MonitorHotkeyBox.Text);
        }
        private void OnAutoStartClick(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _app.SetAutoStart(AutoStartCheck.IsChecked == true);
        }

        private void OnAutoMonitorClick(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _app.Settings.AutoMonitorOnStart = AutoMonitorCheck.IsChecked == true;
            _app.Settings.Save();
        }

        public void ApplyLanguage()
        {
            Loc.Apply(Root);
            Title = Loc.T("main.title");
            try
            {
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "LineInTray.ico");
                if (System.IO.File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
            }
            catch { }
            Root.Loaded += (_, _) =>
            {
                Loc.Apply(Root);
                Refresh();
                MainScroll.ChangeView(0, 0, null);
            };
            Refresh();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating) return;
            _app.SetLanguage(LanguageFromIndex(LanguageCombo.SelectedIndex));
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating) return;
            _app.SetTheme(ThemeFromIndex(ThemeCombo.SelectedIndex), quick: false);
        }

        private void OnQuickThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating) return;
            _app.SetTheme(ThemeFromIndex(QuickThemeCombo.SelectedIndex), quick: true);
        }

        private async void OnAddPresetClick(object sender, RoutedEventArgs e)
        {
            await EditPresetAsync(null);
        }

        private async System.Threading.Tasks.Task EditPresetAsync(AudioPreset? existing)
        {
            AudioPreset preset = existing?.Clone() ?? new AudioPreset { Icon = "builtin:linein", SetDefault = true };

            TextBox name = new TextBox { Header = Loc.T("preset.name"), Text = preset.Name, PlaceholderText = Loc.T("preset.name.hint") };
            ComboBox endpoints = new ComboBox { Header = Loc.T("preset.output"), HorizontalAlignment = HorizontalAlignment.Stretch };
            List<EndpointItem> items = new List<EndpointItem>();
            foreach (EndpointInfo endpoint in _app.RenderEndpoints())
            {
                items.Add(new EndpointItem(endpoint));
            }
            endpoints.ItemsSource = items;
            if (!string.IsNullOrEmpty(preset.EndpointId))
            {
                foreach (EndpointItem item in items)
                {
                    if (item.Endpoint.Id == preset.EndpointId) endpoints.SelectedItem = item;
                }
            }
            if (endpoints.SelectedItem == null && items.Count > 0)
            {
                HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (AudioPreset existingPreset in _app.Presets)
                {
                    if (!string.IsNullOrEmpty(existingPreset.EndpointId)) used.Add(existingPreset.EndpointId);
                }
                EndpointItem? preferred = null;
                foreach (EndpointItem item in items)
                {
                    if (item.Endpoint.IsActive && !used.Contains(item.Endpoint.Id)) { preferred = item; break; }
                }
                endpoints.SelectedItem = preferred ?? items[0];
            }

            ComboBox icon = new ComboBox { Header = Loc.T("preset.icon"), HorizontalAlignment = HorizontalAlignment.Stretch };
            string[] names = { Loc.T("icon.linein"), Loc.T("icon.mic"), Loc.T("icon.headphone"), Loc.T("icon.speaker"), Loc.T("icon.music"), Loc.T("icon.game"), Loc.T("icon.phone"), Loc.T("icon.aux") };
            string[] icons = { "builtin:linein", "builtin:mic", "builtin:headphone", "builtin:speaker", "builtin:music", "builtin:game", "builtin:phone", "builtin:aux" };
            icon.ItemsSource = names;
            icon.SelectedIndex = Math.Max(0, Array.IndexOf(icons, preset.Icon));
            CheckBox setDefault = new CheckBox { Content = Loc.T("preset.default"), IsChecked = preset.SetDefault };

            StackPanel content = new StackPanel { Spacing = 10, Width = 360 };
            content.Children.Add(name);
            content.Children.Add(endpoints);
            content.Children.Add(icon);
            content.Children.Add(setDefault);

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot,
                Title = existing == null ? Loc.T("preset.add.title") : Loc.T("preset.edit.title"),
                PrimaryButtonText = Loc.T("preset.save"),
                CloseButtonText = Loc.T("preset.cancel"),
                DefaultButton = ContentDialogButton.Primary,
                Content = content
            };
            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            if (!(endpoints.SelectedItem is EndpointItem chosen))
            {
                return;
            }
            preset.Name = string.IsNullOrWhiteSpace(name.Text) ? chosen.Endpoint.FriendlyName : name.Text.Trim();
            preset.EndpointId = chosen.Endpoint.Id;
            preset.Icon = icons[Math.Max(0, icon.SelectedIndex)];
            preset.SetDefault = setDefault.IsChecked == true;
            if (existing == null) _app.AddPreset(preset);
            else _app.UpdatePreset(preset);
        }

        private void OnOpenPanelClick(object sender, RoutedEventArgs e)
        {
            AppWindow.Hide();
            App.CurrentApp.ShowQuickPanel();
        }

        private void OnExitClick(object sender, RoutedEventArgs e) => App.CurrentApp.ExitApp();

        private void OnOpenSoundClick(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
            catch (Exception ex) { Log.Exception("打开声音设置失败", ex); }
        }

        private void OnOpenDataClick(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", AppInfo.AppDataDir) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Exception("打开数据目录失败", ex); }
        }

        private void OnOpenLogClick(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(AppInfo.LogPath) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Exception("打开日志失败", ex); }
        }

        private void OnReassertTrayClick(object sender, RoutedEventArgs e) => _app.ReassertTray();
    }

    public sealed class EndpointItem
    {
        public EndpointItem(EndpointInfo endpoint) => Endpoint = endpoint;
        public EndpointInfo Endpoint { get; }
        public override string ToString() => Endpoint.DisplayName + " · " + Loc.State(Endpoint.StateText);
    }

    public sealed class PlaybackItem
    {
        public PlaybackItem(EndpointInfo? endpoint) => Endpoint = endpoint;
        public EndpointInfo? Endpoint { get; }
        public override string ToString() => Endpoint == null ? Loc.T("playback.default") : Endpoint.DisplayName;
    }
}














