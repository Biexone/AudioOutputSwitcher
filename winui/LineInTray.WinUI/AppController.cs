using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using LineInTray;
using NAudio.CoreAudioApi;

namespace LineInTray.WinUI
{
    public enum AppTheme
    {
        System,
        Light,
        Dark
    }

    public sealed class AppController : IDisposable
    {
        private readonly DispatcherQueue _dispatcher;
        private readonly AudioEndpointCatalog _catalog;
        private readonly Settings _settings;
        private List<AudioPreset> _presets;
        private AudioMonitor? _monitor;
        private bool _disposed;
        private string _lastError = "";
        private readonly SemaphoreSlim _audioGate = new SemaphoreSlim(1, 1);
        private readonly object _cacheLock = new object();
        private List<EndpointInfo> _captureCache = new List<EndpointInfo>();
        private List<EndpointInfo> _renderCache = new List<EndpointInfo>();
        private EndpointInfo? _defaultRenderCache;

        public AppController(DispatcherQueue dispatcher)
        {
            _dispatcher = dispatcher;
            _settings = Settings.Load();
            _catalog = new AudioEndpointCatalog();
            _presets = PresetStore.Load();
            RefreshCaches();
            Theme = ParseTheme(_settings.Theme);
            QuickTheme = ParseTheme(_settings.QuickPanelTheme);
            if (_presets.Count == 0)
            {
                EndpointInfo? output = _defaultRenderCache;
                if (output != null)
                {
                    _presets.Add(new AudioPreset
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = output.FriendlyName,
                        EndpointId = output.Id,
                        Icon = "builtin:speaker",
                        SetDefault = true
                    });
                    PresetStore.Save(_presets);
                }
            }
        }

        public event Action? StateChanged;
        public event Action? PresetsChanged;
        public event Action<AppTheme, AppTheme>? ThemeChanged;

        public Settings Settings => _settings;
        public AppTheme Theme { get; private set; }
        public AppTheme QuickTheme { get; private set; }
        public IReadOnlyList<AudioPreset> Presets => _presets;
        public bool IsMonitoring => _monitor != null && _monitor.IsRunning;
        public string LastError => _lastError;
        public string CurrentInputId => _settings.CaptureEndpointId;
        public string CurrentInputName
        {
            get
            {
                EndpointInfo? ep = FindEndpoint(_settings.CaptureEndpointId);
                return ep != null ? ep.FriendlyName : _settings.CaptureNameHint;
            }
        }

        public string StatusText
        {
            get
            {
                if (IsMonitoring) return "正在监听";
                if (!string.IsNullOrEmpty(_lastError)) return "错误：" + _lastError;
                return "未监听";
            }
        }

        public IReadOnlyList<EndpointInfo> CaptureEndpoints()
        {
            lock (_cacheLock) return new List<EndpointInfo>(_captureCache);
        }

        public IReadOnlyList<EndpointInfo> RenderEndpoints()
        {
            lock (_cacheLock) return new List<EndpointInfo>(_renderCache);
        }

        public EndpointInfo? FindEndpoint(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_cacheLock)
            {
                foreach (EndpointInfo endpoint in _captureCache)
                {
                    if (string.Equals(endpoint.Id, id, StringComparison.OrdinalIgnoreCase)) return endpoint;
                }
            }
            return null;
        }

        private EndpointInfo? FindRenderById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_cacheLock)
            {
                foreach (EndpointInfo endpoint in _renderCache)
                {
                    if (string.Equals(endpoint.Id, id, StringComparison.OrdinalIgnoreCase)) return endpoint;
                }
            }
            return null;
        }

        private void RefreshCaches()
        {
            try
            {
                List<EndpointInfo> captures = _catalog.GetCaptureEndpoints(true);
                List<EndpointInfo> renders = _catalog.GetRenderEndpoints();
                EndpointInfo? defaultRender = _catalog.GetDefaultRender();
                lock (_cacheLock)
                {
                    _captureCache = captures;
                    _renderCache = renders;
                    _defaultRenderCache = defaultRender;
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
            }
        }

        public void RefreshDevices()
        {
            _ = RefreshDevicesAsync();
        }

        private async Task RefreshDevicesAsync()
        {
            await Task.Run(() =>
            {
                _catalog.Refresh();
                RefreshCaches();
                _lastError = "";
            });
            Notify();
        }

        public void ToggleMonitor()
        {
            _ = ToggleMonitorAsync();
        }

        private async Task ToggleMonitorAsync()
        {
            if (IsMonitoring) await StopMonitorAsync();
            else await StartMonitorAsync();
        }

        public void StartMonitor()
        {
            _ = StartMonitorAsync();
        }

        private async Task StartMonitorAsync()
        {
            if (IsMonitoring) return;
            await _audioGate.WaitAsync();
            try
            {
                await Task.Run(StartMonitorCore);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Log.Exception("开始监听失败", ex);
                try { _monitor?.Dispose(); } catch { }
                _monitor = null;
            }
            finally
            {
                _audioGate.Release();
                Notify();
            }
        }

        private void StartMonitorCore()
        {
            EndpointInfo? capture = _catalog.FindCapture(_settings.CaptureEndpointId, _settings.CaptureNameHint);
            if (capture == null)
            {
                _lastError = "找不到线路输入设备";
                return;
            }
            if (capture.IsDisabled)
            {
                PolicyConfig.SetVisibility(capture.Id, true);
                Thread.Sleep(300);
                _catalog.Refresh();
                capture = _catalog.FindCaptureById(capture.Id) ?? capture;
            }
            EndpointInfo? playback = _catalog.ResolvePlayback(_settings.PlaybackEndpointId);
            if (playback == null)
            {
                _lastError = "找不到播放设备";
                return;
            }

            _settings.CaptureEndpointId = capture.Id;
            _settings.CaptureNameHint = capture.FriendlyName;
            _settings.Save();
            MMDevice captureDevice = _catalog.OpenDevice(capture.Id);
            MMDevice playbackDevice = _catalog.OpenDevice(playback.Id);
            _monitor = new AudioMonitor(captureDevice, playbackDevice, _settings.LatencyMs);
            _monitor.Faulted += OnMonitorFaulted;
            _monitor.Start();
            _lastError = "";
            Log.Info("开始监听: " + capture.DisplayName + " -> " + playback.DisplayName);
        }

        public void StopMonitor()
        {
            _ = StopMonitorAsync();
        }

        private async Task StopMonitorAsync()
        {
            await _audioGate.WaitAsync();
            try
            {
                await Task.Run(StopMonitorCore);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Log.Exception("停止监听失败", ex);
            }
            finally
            {
                _audioGate.Release();
                Notify();
            }
        }

        private void StopMonitorCore()
        {
            if (_monitor == null) return;
            _monitor.Faulted -= OnMonitorFaulted;
            _monitor.Dispose();
            _monitor = null;
            _lastError = "";
            Log.Info("停止监听");
        }

        private void OnMonitorFaulted(object? sender, AudioMonitorFaultEventArgs args)
        {
            _lastError = args.Message;
            Log.Exception("监听异常", args.Exception);
            _dispatcher.TryEnqueue(Notify);
        }

        public void ActivatePreset(AudioPreset preset)
        {
            _ = ActivatePresetAsync(preset);
        }

        private async Task ActivatePresetAsync(AudioPreset? preset)
        {
            if (preset == null) return;
            try
            {
                EndpointInfo? endpoint = FindRenderById(preset.EndpointId);
                if (endpoint == null)
                {
                    _lastError = "输出设备不存在：" + preset.Name;
                    Notify();
                    return;
                }
                if (!endpoint.IsActive)
                {
                    _lastError = "输出设备当前不可用：" + endpoint.DisplayName;
                    Notify();
                    return;
                }
                if (preset.SetDefault)
                {
                    string endpointId = endpoint.Id;
                    await Task.Run(() => PolicyConfig.SetDefaultPlayback(endpointId));
                }
                bool wasMonitoring = IsMonitoring;
                if (wasMonitoring) await StopMonitorAsync();
                _settings.PlaybackEndpointId = endpoint.Id;
                _settings.Save();
                _lastError = "";
                Log.Info("切换音频输出: " + endpoint.DisplayName + (preset.SetDefault ? "（设为默认）" : ""));
                if (wasMonitoring) await StartMonitorAsync();
                Notify();
                NotifyPresets();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Log.Exception("切换音频输入失败", ex);
                Notify();
            }
        }

        public void SetDefaultCapture(EndpointInfo endpoint)
        {
            _ = SetDefaultCaptureAsync(endpoint);
        }

        private async Task SetDefaultCaptureAsync(EndpointInfo? endpoint)
        {
            if (endpoint == null) return;
            try
            {
                PolicyConfig.SetDefaultCapture(endpoint.Id);
                _settings.CaptureEndpointId = endpoint.Id;
                _settings.CaptureNameHint = endpoint.FriendlyName;
                _settings.Save();
                if (IsMonitoring)
                {
                    await StopMonitorAsync();
                    await StartMonitorAsync();
                }
                _lastError = "";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Log.Exception("设置默认录音设备失败", ex);
            }
            Notify();
        }

        public void SetPlayback(string endpointId)
        {
            _ = SetPlaybackAsync(endpointId);
        }

        private async Task SetPlaybackAsync(string endpointId)
        {
            _settings.PlaybackEndpointId = endpointId ?? "";
            _settings.Save();
            if (IsMonitoring)
            {
                await StopMonitorAsync();
                await StartMonitorAsync();
            }
            Notify();
        }

        public void SetLatency(int milliseconds)
        {
            _ = SetLatencyAsync(milliseconds);
        }

        private async Task SetLatencyAsync(int milliseconds)
        {
            _settings.LatencyMs = Math.Clamp(milliseconds, 50, 500);
            _settings.Save();
            if (IsMonitoring)
            {
                await StopMonitorAsync();
                await StartMonitorAsync();
            }
            Notify();
        }
        public void AddPreset(AudioPreset preset)
        {
            if (preset == null) return;
            preset.Id = Guid.NewGuid().ToString("N");
            _presets.Add(preset);
            PresetStore.Save(_presets);
            PresetsChanged?.Invoke();
            Notify();
        }

        public void UpdatePreset(AudioPreset preset)
        {
            if (preset == null) return;
            for (int i = 0; i < _presets.Count; i++)
            {
                if (_presets[i].Id == preset.Id)
                {
                    _presets[i] = preset;
                    break;
                }
            }
            PresetStore.Save(_presets);
            PresetsChanged?.Invoke();
            Notify();
        }

        public void DeletePreset(AudioPreset preset)
        {
            if (preset == null) return;
            _presets.RemoveAll(p => p.Id == preset.Id);
            PresetStore.Save(_presets);
            PresetsChanged?.Invoke();
            Notify();
        }

        public string PresetSubtitle(AudioPreset preset)
        {
            EndpointInfo? ep = FindRenderById(preset.EndpointId);
            if (ep == null) return "输出设备当前不可用";
            string text = ep.DisplayName + " · " + ep.StateText;
            if (preset.SetDefault) text += " · 设为默认";
            return text;
        }

        public bool HasDuplicatePresetEndpoints
        {
            get
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (AudioPreset preset in _presets)
                {
                    if (!string.IsNullOrEmpty(preset.EndpointId) && !seen.Add(preset.EndpointId)) return true;
                }
                return false;
            }
        }

        public bool IsPresetActive(AudioPreset preset)
        {
            string current = _settings.PlaybackEndpointId;
            if (string.IsNullOrEmpty(current))
            {
                current = _defaultRenderCache?.Id ?? "";
            }
            return string.Equals(preset.EndpointId, current, StringComparison.OrdinalIgnoreCase);
        }

        public void SetTheme(AppTheme theme, bool quick)
        {
            if (quick)
            {
                QuickTheme = theme;
                _settings.QuickPanelTheme = theme.ToString().ToLowerInvariant();
            }
            else
            {
                Theme = theme;
                _settings.Theme = theme.ToString().ToLowerInvariant();
            }
            _settings.Save();
            ThemeChanged?.Invoke(Theme, QuickTheme);
        }

        public bool IsAutoStartEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue(AppInfo.RunValueName) != null;
            }
            catch { return false; }
        }

        public void SetAutoStart(bool enabled)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (enabled)
                {
                    key.SetValue(AppInfo.RunValueName, "\"" + AppInfo.ExePath + "\" --autostart");
                }
                else
                {
                    key.DeleteValue(AppInfo.RunValueName, false);
                }
            }
            catch (Exception ex)
            {
                _lastError = "无法修改开机启动项：" + ex.Message;
                Notify();
            }
        }

        public void ReassertTray()
        {
            TrayIcon?.ForceCreate(enablesEfficiencyMode: false);
        }

        public H.NotifyIcon.TaskbarIcon? TrayIcon { get; set; }

        private void Notify()
        {
            if (_dispatcher.HasThreadAccess)
            {
                StateChanged?.Invoke();
            }
            else
            {
                _dispatcher.TryEnqueue(() => StateChanged?.Invoke());
            }
        }

        private void NotifyPresets()
        {
            if (_dispatcher.HasThreadAccess)
            {
                PresetsChanged?.Invoke();
            }
            else
            {
                _dispatcher.TryEnqueue(() => PresetsChanged?.Invoke());
            }
        }

        private static AppTheme ParseTheme(string? text)
        {
            if (string.Equals(text, "light", StringComparison.OrdinalIgnoreCase)) return AppTheme.Light;
            if (string.Equals(text, "dark", StringComparison.OrdinalIgnoreCase)) return AppTheme.Dark;
            return AppTheme.System;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { StopMonitor(); } catch { }
            _catalog.Dispose();
            TrayIcon?.Dispose();
        }
    }
}







