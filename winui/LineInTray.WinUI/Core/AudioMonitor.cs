using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LineInTray
{
    internal sealed class AudioMonitorFaultEventArgs : EventArgs
    {
        public AudioMonitorFaultEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public string Message { get; private set; }
        public Exception Exception { get; private set; }
    }

    internal sealed class AudioMonitorException : Exception
    {
        public AudioMonitorException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    internal sealed class AudioMonitor : IDisposable
    {
        private readonly object _sync = new object();
        private readonly MMDevice _captureDevice;
        private readonly MMDevice _playbackDevice;
        private readonly int _latencyMs;
        private readonly string _captureId;
        private readonly string _playbackId;

        private WasapiCapture _capture;
        private WasapiOut _output;
        private BufferedWaveProvider _buffer;
        private volatile bool _running;
        private bool _stopping;
        private bool _disposed;
        private int _faultRaised;

        public AudioMonitor(MMDevice captureDevice, MMDevice playbackDevice, int latencyMs)
        {
            if (captureDevice == null)
            {
                throw new ArgumentNullException("captureDevice");
            }
            if (playbackDevice == null)
            {
                throw new ArgumentNullException("playbackDevice");
            }

            _captureDevice = captureDevice;
            _playbackDevice = playbackDevice;
            _latencyMs = latencyMs < 50 ? 50 : (latencyMs > 500 ? 500 : latencyMs);
            _captureId = captureDevice.ID;
            _playbackId = playbackDevice.ID;
        }

        public event EventHandler<AudioMonitorFaultEventArgs> Faulted;

        public string CaptureId
        {
            get { return _captureId; }
        }

        public string PlaybackId
        {
            get { return _playbackId; }
        }

        public bool IsRunning
        {
            get { return _running && _capture != null && _output != null; }
        }

        public string CaptureFormatDescription
        {
            get
            {
                try
                {
                    return _capture == null || _capture.WaveFormat == null
                        ? "-"
                        : Describe(_capture.WaveFormat);
                }
                catch
                {
                    return "-";
                }
            }
        }

        public string PlaybackFormatDescription
        {
            get
            {
                try
                {
                    return _output == null || _output.OutputWaveFormat == null
                        ? "-"
                        : Describe(_output.OutputWaveFormat);
                }
                catch
                {
                    return "-";
                }
            }
        }

        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("AudioMonitor");
            }

            lock (_sync)
            {
                if (_running)
                {
                    return;
                }

                _stopping = false;
                Interlocked.Exchange(ref _faultRaised, 0);

                try
                {
                    _capture = new WasapiCapture(_captureDevice);
                    _capture.ShareMode = AudioClientShareMode.Shared;
                    _capture.DataAvailable += OnDataAvailable;
                    _capture.RecordingStopped += OnRecordingStopped;

                    _buffer = new BufferedWaveProvider(_capture.WaveFormat);
                    _buffer.BufferDuration = TimeSpan.FromMilliseconds(500);
                    _buffer.DiscardOnBufferOverflow = true;
                    _buffer.ReadFully = true;

                    try
                    {
                        StartOutput(true);
                    }
                    catch (Exception first)
                    {
                        Log.Exception("事件驱动播放初始化失败，尝试定时驱动", first);
                        DisposeOutput();
                        StartOutput(false);
                    }

                    _running = true;
                    _capture.StartRecording();

                    if (!_running)
                    {
                        throw new AudioMonitorException("音频流启动后立即停止。", null);
                    }

                    Log.Info("监听已启动，采集=" + _captureId + "，播放=" + _playbackId
                        + "，采集格式=" + CaptureFormatDescription
                        + "，播放格式=" + PlaybackFormatDescription);
                }
                catch (Exception ex)
                {
                    _running = false;
                    CleanupLocked();
                    if (ex is AudioMonitorException)
                    {
                        throw;
                    }
                    throw new AudioMonitorException("启动监听失败: " + ex.Message, ex);
                }
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_stopping)
                {
                    return;
                }
                _stopping = true;
                _running = false;
                CleanupLocked();
                Log.Info("监听已停止");
            }
        }

        private void StartOutput(bool useEventSync)
        {
            _output = new WasapiOut(
                _playbackDevice,
                AudioClientShareMode.Shared,
                useEventSync,
                _latencyMs);
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_buffer);
            _output.Play();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs args)
        {
            try
            {
                if (_running && _buffer != null && args.BytesRecorded > 0)
                {
                    _buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("写入监听缓冲失败", ex);
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs args)
        {
            HandleStopped("播放", args == null ? null : args.Exception);
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs args)
        {
            HandleStopped("采集", args == null ? null : args.Exception);
        }

        private void HandleStopped(string source, Exception exception)
        {
            if (_stopping || _disposed)
            {
                return;
            }

            bool wasRunning = _running;
            _running = false;

            if (!wasRunning && exception == null)
            {
                return;
            }

            string message = source + "音频流已停止";
            if (exception != null)
            {
                message += ": " + exception.Message;
            }

            Log.Error(message);
            if (exception != null)
            {
                Log.Exception("音频流异常", exception);
            }

            if (Interlocked.Exchange(ref _faultRaised, 1) != 0)
            {
                return;
            }

            EventHandler<AudioMonitorFaultEventArgs> handler = Faulted;
            if (handler != null)
            {
                try
                {
                    handler(this, new AudioMonitorFaultEventArgs(message, exception));
                }
                catch (Exception ex)
                {
                    Log.Exception("处理音频故障事件失败", ex);
                }
            }
        }

        private void CleanupLocked()
        {
            if (_capture != null)
            {
                try
                {
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    _capture.StopRecording();
                }
                catch
                {
                }
                try
                {
                    _capture.Dispose();
                }
                catch
                {
                }
                _capture = null;
            }

            DisposeOutput();
            _buffer = null;
        }

        private void DisposeOutput()
        {
            if (_output == null)
            {
                return;
            }

            try
            {
                _output.PlaybackStopped -= OnPlaybackStopped;
                _output.Stop();
            }
            catch
            {
            }
            try
            {
                _output.Dispose();
            }
            catch
            {
            }
            _output = null;
        }

        private static string Describe(WaveFormat format)
        {
            if (format == null)
            {
                return "-";
            }
            return format.SampleRate + "Hz/" + format.Channels + "ch/"
                + format.BitsPerSample + "bit/" + format.Encoding;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            try
            {
                Stop();
            }
            catch
            {
            }

            try
            {
                _captureDevice.Dispose();
            }
            catch
            {
            }
            try
            {
                _playbackDevice.Dispose();
            }
            catch
            {
            }
        }
    }
}
