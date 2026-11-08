using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using LineInTray;

namespace LineInTray.WinUI.Controls
{
    public sealed partial class PanelContent : UserControl
    {
        private readonly AppController? _app;
        private readonly bool _showClose;

        public PanelContent()
        {
            _showClose = true;
            InitializeComponent();
        }

        public PanelContent(AppController app, bool showClose)
        {
            _app = app;
            _showClose = showClose;
            InitializeComponent();
            CloseButton.Visibility = showClose ? Visibility.Visible : Visibility.Collapsed;
            ExitButton.Visibility = showClose ? Visibility.Visible : Visibility.Collapsed;
            _app.StateChanged += Refresh;
            _app.PresetsChanged += Refresh;
            Refresh();
        }

        public event EventHandler? SettingsRequested;
        public event EventHandler? ManageRequested;
        public event EventHandler? OpenMainRequested;
        public event EventHandler? CloseRequested;
        public event EventHandler? ExitRequested;

        public bool ShowCloseButton
        {
            get => CloseButton.Visibility == Visibility.Visible;
            set => CloseButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool ShowExitButton
        {
            get => ExitButton.Visibility == Visibility.Visible;
            set => ExitButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Detach()
        {
            if (_app == null) return;
            _app.StateChanged -= Refresh;
            _app.PresetsChanged -= Refresh;
        }

        public void Refresh()
        {
            if (_app == null) return;
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(Refresh);
                return;
            }

            string status = _app.IsMonitoring ? "正在监听" : _app.StatusText;
            StatusText.Text = _app.CurrentInputName + " · " + status;
            StatusText.Foreground = _app.IsMonitoring ? SuccessBrush() : SecondaryBrush();

            bool monitoring = _app.IsMonitoring;
            MonitorText.Text = monitoring ? "停止监听" : "开始监听";
            MonitorIcon.Glyph = monitoring ? "\uE71A" : "\uE768";
            if (monitoring)
            {
                MonitorButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
            }
            else
            {
                MonitorButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }

            PresetList.Children.Clear();
            IReadOnlyList<AudioPreset> presets = _app.Presets;
            if (presets.Count == 0)
            {
                PresetList.Children.Add(new TextBlock
                {
                    Text = "还没有快捷方式，点击“管理”添加。",
                    Foreground = SecondaryBrush(),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 2, 2, 6)
                });
                return;
            } 
            foreach (AudioPreset preset in presets)
            {
                PresetList.Children.Add(CreatePresetButton(preset));
            }

            if (_app.HasDuplicatePresetEndpoints)
            {
                PresetList.Children.Add(new TextBlock
                {
                    Text = "提示：多个快捷方式指向同一音频输出，切换不会改变声音。点“管理”修改设备。",
                    Foreground = SecondaryBrush(),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Margin = new Thickness(2, 2, 2, 0)
                });
            }
        }

        private Button CreatePresetButton(AudioPreset preset)
        {
            bool active = _app.IsPresetActive(preset);
            Button button = new Button
            {
                Style = (Style)Application.Current.Resources["PanelCardButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = preset
            };
            if (active)
            {
                button.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                button.BorderThickness = new Thickness(1.5);
            }

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
                Text = string.IsNullOrEmpty(preset.Name) ? "未命名输入" : preset.Name,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock
            {
                Text = _app.PresetSubtitle(preset),
                Style = (Style)Application.Current.Resources["PanelSubtitleTextStyle"],
                Foreground = SecondaryBrush(),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            if (active)
            {
                Border badge = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 3, 7, 3),
                    Background = (Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = "使用中",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);
            }
            else
            {
                FontIcon chevron = new FontIcon
                {
                    Glyph = "\uE76C",
                    FontSize = 12,
                    Foreground = SecondaryBrush(),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(chevron, 2);
                grid.Children.Add(chevron);
            }

            button.Content = grid;
            button.Click += (_, _) => _app.ActivatePreset(preset);
            return button;
        }

        private Brush SecondaryBrush()
        {
            ElementTheme theme = (XamlRoot?.Content as FrameworkElement)?.ActualTheme ?? ActualTheme;
            return new SolidColorBrush(theme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 200, 200, 200)
                : Windows.UI.Color.FromArgb(255, 96, 96, 96));
        }

        private static Brush SuccessBrush()
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 190, 120));
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

        private void OnMonitorClick(object sender, RoutedEventArgs e) => _app.ToggleMonitor();
        private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        private void OnManageClick(object sender, RoutedEventArgs e) => ManageRequested?.Invoke(this, EventArgs.Empty);
        private void OnOpenMainClick(object sender, RoutedEventArgs e)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lineintray-winui.log"), DateTime.Now + " OpenMain clicked" + Environment.NewLine); } catch { }
            OpenMainRequested?.Invoke(this, EventArgs.Empty);
        }
        private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
        private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}









