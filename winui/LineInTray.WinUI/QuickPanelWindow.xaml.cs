using System;
using System.Numerics;
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
    public sealed partial class QuickPanelWindow : Window
    {
        private const double PanelWidth = 360;
        private const double SlideDip = 12;
        private AppController _app = null!;
        private PanelContent _content = null!;
        private bool _hiding;
        private DateTime _shownAt;

        public QuickPanelWindow()
        {
            InitializeComponent();
        }

        public QuickPanelWindow(AppController app) : this()
        {
            _app = app;
            Title = "LineInTray";
            SystemBackdrop = new DesktopAcrylicBackdrop();
            _content = new PanelContent(app, showClose: true);
            _content.SettingsRequested += (_, _) => App.CurrentApp.ShowMainWindow();
            _content.ManageRequested += (_, _) => App.CurrentApp.ShowMainWindow();
            _content.OpenMainRequested += (_, _) => App.CurrentApp.ShowMainWindow();
            _content.CloseRequested += (_, _) => HidePanel();
            _content.ExitRequested += (_, _) => App.CurrentApp.ExitApp();
            Root.Children.Add(_content);
            _content.SizeChanged += (_, _) => ResizeToContent();
            WindowInterop.MakeFlyout(this, noActivate: false, border: false);
            Activated += OnActivated;
        }

        public bool IsShown { get; private set; }

        public void ShowNearTray()
        {
            if (IsShown)
            {
                HidePanel();
                return;
            }

            double scale = _content.XamlRoot?.RasterizationScale ?? 1.0;
            if (scale <= 0) scale = 1.0;
            _content.Measure(new Size(PanelWidth, double.PositiveInfinity));
            int width = (int)Math.Ceiling(PanelWidth * scale);
            int height = (int)Math.Ceiling(_content.DesiredSize.Height * scale) + 2;

            DisplayArea area = DisplayArea.Primary;
            RectInt32 work = area.WorkArea;
            int x = work.X + work.Width - width - (int)Math.Round(12 * scale);
            int y = work.Y + work.Height - height - (int)Math.Round(12 * scale);
            WindowInterop.MoveAndResize(this, new PixelRect(x, y, width, height));
            WindowInterop.GetAppWindow(this).Show(true);
            Activate();
            WindowInterop.BringToForeground(this);
            IsShown = true;
            _shownAt = DateTime.UtcNow;
            DispatcherQueue.TryEnqueue(() =>
            {
                ResizeToContent();
                AnimateIn();
            });
        }

        public void ApplyLanguage()
        {
            Loc.Apply(Root);
            Title = Loc.T("main.title");
            _content.ApplyLanguage();
        }

        public void ResizeToContent()
        {
            if (!IsShown) return;
            double scale = _content.XamlRoot?.RasterizationScale ?? 1.0;
            if (scale <= 0) scale = 1.0;
            _content.Measure(new Size(PanelWidth, double.PositiveInfinity));
            int width = (int)Math.Ceiling(PanelWidth * scale);
            int height = (int)Math.Ceiling(_content.DesiredSize.Height * scale) + 2;
            DisplayArea area = DisplayArea.Primary;
            RectInt32 work = area.WorkArea;
            int x = work.X + work.Width - width - (int)Math.Round(12 * scale);
            int y = work.Y + work.Height - height - (int)Math.Round(12 * scale);
            WindowInterop.MoveAndResize(this, new PixelRect(x, y, width, height));
        }

        public void HideImmediately()
        {
            _hiding = false;
            IsShown = false;
            Root.Opacity = 1;
            WindowInterop.GetAppWindow(this).Hide();
        }

        public void HidePanel()
        {
            if (!IsShown || _hiding) return;
            AnimateOut();
        }

        private void AnimateIn()
        {
            _hiding = false;
            if (!IsShown) return;
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

        private void AnimateOut()
        {
            _hiding = true;
            if (!IsShown) return;
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
                IsShown = false;
                Root.Opacity = 1;
                ElementCompositionPreview.GetElementVisual(Root).StopAnimation("Opacity");
                ElementCompositionPreview.GetElementVisual(Root).StopAnimation("Translation");
                WindowInterop.GetAppWindow(this).Hide();
            };
            timer.Start();
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && !_hiding
                && (DateTime.UtcNow - _shownAt).TotalMilliseconds > 500)
            {
                HidePanel();
            }
        }
    }
}










