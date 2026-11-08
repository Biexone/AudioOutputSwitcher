using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Hosting;
using LineInTray.WinUI.Controls;
using LineInTray.WinUI.Interop;
using Windows.Foundation;
using Windows.Graphics;

namespace LineInTray.WinUI
{
    public sealed partial class QuickSettingsPanelWindow : Window
    {
        private const double CardMargin = 12;
        private const double SlideDip = 16;
        private const double EnterMs = 300;
        private const double ExitMs = 160;
        private AppController _app = null!;
        private PanelContent _content = null!;
        private readonly Stopwatch _clock = new();
        private PixelRect _target;
        private double _y;
        private double _alpha;
        private double _fromY;
        private double _toY;
        private double _fromAlpha;
        private double _toAlpha;
        private double _duration;
        private Action? _completed;
        private bool _visible;
        private bool _leaving;

        public QuickSettingsPanelWindow()
        {
            InitializeComponent();
        }

        public QuickSettingsPanelWindow(AppController app) : this()
        {
            _app = app;
            Title = "LineInTray";
            SystemBackdrop = new DesktopAcrylicBackdrop();
            _content = new PanelContent(app, showClose: false);
            _content.ShowCloseButton = false;
            _content.ShowExitButton = false;
            _content.SettingsRequested += (_, _) => App.CurrentApp.ShowMainWindow();
            _content.ManageRequested += (_, _) => App.CurrentApp.ShowMainWindow();
            _content.OpenMainRequested += (_, _) => App.CurrentApp.ShowMainWindow();
            Root.Children.Add(_content);
            WindowInterop.MakeFlyout(this, noActivate: true, border: true);
            WindowInterop.MakeLayered(this);
            WindowInterop.SetWindowAlpha(this, 0);
        }

        public bool IsShown => _visible;

        public void Track(PixelRect cards)
        {
            if (_visible && _leaving) _leaving = false;
            double scale = _content.XamlRoot?.RasterizationScale ?? 1.0;
            if (scale <= 0) scale = 1.0;
            int margin = (int)Math.Round(CardMargin * scale);
            int width = cards.Width - 2 * margin;
            if (width < 120) { Leave(); return; }
            _content.Measure(new Size(width / scale, double.PositiveInfinity));
            int height = (int)Math.Ceiling(_content.DesiredSize.Height * scale) + 2;
            if (height < 80) { Leave(); return; }

            DisplayArea area = WindowInterop.GetDisplayAreaForPoint(cards.X + cards.Width / 2, cards.Y + cards.Height / 2);
            RectInt32 work = area.WorkArea;
            int x = cards.X + margin;
            int y = cards.Y - height;
            if (y < work.Y + margin)
            {
                y = cards.Bottom + margin;
                if (y + height > work.Y + work.Height - margin)
                {
                    y = cards.Y - height;
                    if (y < work.Y + margin) { Leave(); return; }
                }
            }

            PixelRect target = new PixelRect(x, y, width, height);
            if (!_visible)
            {
                _target = target;
                _y = target.Y;
                _alpha = 1;
                WindowInterop.MoveAndResize(this, target);
                WindowInterop.SetWindowAlpha(this, 255);
                WindowInterop.GetAppWindow(this).Show(false);
                WindowInterop.EnsureTopmost(this);
                _visible = true;
                _leaving = false;
                AnimateContentIn();
                return;
            }

            WindowInterop.EnsureTopmost(this);
            bool moved = target.X != _target.X || target.Y != _target.Y || target.Width != _target.Width || target.Height != _target.Height;
            _target = target;
            _leaving = false;
            if (moved)
            {
                WindowInterop.MoveAndResize(this, target);
            }
        }

        public void Leave()
        {
            if (!_visible || _leaving) return;
            AnimateContentOut();
        }

        private void AnimateContentIn()
        {
            if (!_visible) return;
            var visual = ElementCompositionPreview.GetElementVisual(Root);
            ElementCompositionPreview.SetIsTranslationEnabled(Root, true);
            var compositor = visual.Compositor;
            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, ease);
            fade.Duration = TimeSpan.FromMilliseconds(280);
            visual.StartAnimation("Opacity", fade);
            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0f, new Vector3(0f, 12f, 0f));
            move.InsertKeyFrame(1f, Vector3.Zero, ease);
            move.Duration = TimeSpan.FromMilliseconds(300);
            visual.StartAnimation("Translation", move);
        }

        private void AnimateContentOut()
        {
            _leaving = true;
            var visual = ElementCompositionPreview.GetElementVisual(Root);
            ElementCompositionPreview.SetIsTranslationEnabled(Root, true);
            var compositor = visual.Compositor;
            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.8f, 0.2f));
            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 1f);
            fade.InsertKeyFrame(1f, 0f, ease);
            fade.Duration = TimeSpan.FromMilliseconds(150);
            visual.StartAnimation("Opacity", fade);
            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0f, Vector3.Zero);
            move.InsertKeyFrame(1f, new Vector3(0f, 10f, 0f), ease);
            move.Duration = TimeSpan.FromMilliseconds(160);
            visual.StartAnimation("Translation", move);
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(170);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                HideNow();
            };
            timer.Start();
        }
        private void HideNow()
        {
            _visible = false;
            _leaving = false;
            WindowInterop.SetWindowAlpha(this, 0);
            WindowInterop.GetAppWindow(this).Hide();
        }

        private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
        private static double EaseInCubic(double t) => t * t * t;
    }
}















