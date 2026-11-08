using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace LineInTray
{
    public sealed class EndpointInfo
    {
        public string Id;
        public string FriendlyName;
        public string Description;
        public DeviceState State;
        public DataFlow Flow;

        public bool IsActive
        {
            get { return State == DeviceState.Active; }
        }

        public bool IsDisabled
        {
            get { return State == DeviceState.Disabled; }
        }

        public string DisplayName
        {
            get
            {
                string name = string.IsNullOrEmpty(FriendlyName) ? "(未知设备)" : FriendlyName;
                if (!string.IsNullOrEmpty(Description)
                    && name.IndexOf(Description, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return name + " (" + Description + ")";
                }
                return name;
            }
        }

        public string StateText
        {
            get
            {
                if (State == DeviceState.Active)
                {
                    return "已启用";
                }
                if (State == DeviceState.Disabled)
                {
                    return "已禁用";
                }
                if (State == DeviceState.NotPresent)
                {
                    return "不存在";
                }
                if (State == DeviceState.Unplugged)
                {
                    return "未插入";
                }
                return "未知";
            }
        }
    }

    internal sealed class AudioEndpointCatalog : IDisposable
    {
        private MMDeviceEnumerator _enumerator;
        private bool _disposed;

        public AudioEndpointCatalog()
        {
            _enumerator = new MMDeviceEnumerator();
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            DisposeEnumerator();
            _enumerator = new MMDeviceEnumerator();
        }

        public List<EndpointInfo> GetCaptureEndpoints(bool includeInactive)
        {
            ThrowIfDisposed();
            DeviceState state = includeInactive ? DeviceState.All : DeviceState.Active;
            return GetEndpoints(DataFlow.Capture, state);
        }

        public List<EndpointInfo> GetRenderEndpoints()
        {
            ThrowIfDisposed();
            return GetEndpoints(DataFlow.Render, DeviceState.Active);
        }

        public EndpointInfo FindCapture(string endpointId, string nameHint)
        {
            ThrowIfDisposed();

            if (!string.IsNullOrEmpty(endpointId))
            {
                List<EndpointInfo> all = GetCaptureEndpoints(true);
                EndpointInfo exact = FindById(all, endpointId);
                if (exact != null)
                {
                    return exact;
                }
            }

            List<EndpointInfo> active = GetCaptureEndpoints(false);
            EndpointInfo found = FindBest(active, null, nameHint);
            if (found != null)
            {
                return found;
            }

            List<EndpointInfo> allEndpoints = GetCaptureEndpoints(true);
            return FindBest(allEndpoints, null, nameHint);
        }

        public EndpointInfo FindCaptureById(string endpointId)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(endpointId))
            {
                return null;
            }

            List<EndpointInfo> all = GetCaptureEndpoints(true);
            for (int i = 0; i < all.Count; i++)
            {
                if (string.Equals(all[i].Id, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }
            return null;
        }

        public EndpointInfo? FindRenderById(string endpointId)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(endpointId)) return null;
            foreach (EndpointInfo endpoint in GetRenderEndpoints())
            {
                if (string.Equals(endpoint.Id, endpointId, StringComparison.OrdinalIgnoreCase)) return endpoint;
            }
            return null;
        }

        public EndpointInfo ResolvePlayback(string endpointId)
        {
            ThrowIfDisposed();

            if (!string.IsNullOrEmpty(endpointId))
            {
                List<EndpointInfo> render = GetRenderEndpoints();
                for (int i = 0; i < render.Count; i++)
                {
                    if (string.Equals(render[i].Id, endpointId, StringComparison.OrdinalIgnoreCase))
                    {
                        return render[i];
                    }
                }
            }

            return GetDefaultRender();
        }

        public EndpointInfo GetDefaultRender()
        {
            ThrowIfDisposed();
            MMDevice device = null;
            try
            {
                device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return ReadDevice(device);
            }
            catch (Exception ex)
            {
                Log.Exception("获取默认播放设备失败", ex);
                return null;
            }
            finally
            {
                if (device != null)
                {
                    device.Dispose();
                }
            }
        }

        public EndpointInfo GetDefaultCapture()
        {
            ThrowIfDisposed();
            MMDevice device = null;
            try
            {
                device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                return ReadDevice(device);
            }
            catch (Exception ex)
            {
                Log.Exception("获取默认录音设备失败", ex);
                return null;
            }
            finally
            {
                if (device != null)
                {
                    device.Dispose();
                }
            }
        }

        public MMDevice OpenDevice(string endpointId)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(endpointId))
            {
                throw new ArgumentException("端点 ID 不能为空。", "endpointId");
            }

            MMDevice device = _enumerator.GetDevice(endpointId);
            if (device == null)
            {
                throw new InvalidOperationException("无法打开音频端点: " + endpointId);
            }
            return device;
        }

        private List<EndpointInfo> GetEndpoints(DataFlow flow, DeviceState state)
        {
            List<EndpointInfo> result = new List<EndpointInfo>();
            MMDeviceCollection collection = _enumerator.EnumerateAudioEndPoints(flow, state);

            for (int i = 0; i < collection.Count; i++)
            {
                MMDevice device = null;
                try
                {
                    device = collection[i];
                    result.Add(ReadDevice(device));
                }
                catch (Exception ex)
                {
                    Log.Exception("读取音频端点失败", ex);
                }
                finally
                {
                    if (device != null)
                    {
                        device.Dispose();
                    }
                }
            }

            result.Sort(CompareEndpoints);
            return result;
        }

        private static EndpointInfo FindBest(List<EndpointInfo> endpoints, string endpointId, string nameHint)
        {
            if (!string.IsNullOrEmpty(endpointId))
            {
                EndpointInfo exact = FindById(endpoints, endpointId);
                if (exact != null)
                {
                    return exact;
                }
            }

            EndpointInfo best = null;
            int bestScore = 0;
            for (int i = 0; i < endpoints.Count; i++)
            {
                int score = Score(endpoints[i], nameHint);
                if (score > bestScore)
                {
                    best = endpoints[i];
                    bestScore = score;
                }
            }
            return bestScore > 0 ? best : null;
        }

        private static EndpointInfo FindById(List<EndpointInfo> endpoints, string endpointId)
        {
            if (string.IsNullOrEmpty(endpointId))
            {
                return null;
            }

            for (int i = 0; i < endpoints.Count; i++)
            {
                if (string.Equals(endpoints[i].Id, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    return endpoints[i];
                }
            }
            return null;
        }

        private static int Score(EndpointInfo endpoint, string nameHint)
        {
            string friendly = endpoint.FriendlyName ?? "";
            string description = endpoint.Description ?? "";
            int score = 0;
            int nameScore = 0;

            if (Contains(friendly, "线路输入") || Contains(friendly, "line in")
                || Contains(friendly, "line-in") || Contains(friendly, "linein"))
            {
                nameScore += 80;
            }

            if (!string.IsNullOrEmpty(nameHint))
            {
                if (Contains(friendly, nameHint))
                {
                    nameScore += 40;
                }
                if (string.Equals(friendly, nameHint, StringComparison.OrdinalIgnoreCase))
                {
                    nameScore += 20;
                }
            }

            if (nameScore == 0)
            {
                return 0;
            }
            score += nameScore;

            if (endpoint.State == DeviceState.Active)
            {
                score += 100;
            }
            else if (endpoint.State == DeviceState.Disabled)
            {
                score += 20;
            }
            else
            {
                score -= 50;
            }

            if (description.IndexOf("Realtek", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 30;
            }

            return score;
        }

        private static bool Contains(string value, string part)
        {
            return value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static EndpointInfo ReadDevice(MMDevice device)
        {
            EndpointInfo info = new EndpointInfo();
            info.Id = device.ID;
            info.State = device.State;
            info.Flow = device.DataFlow;

            try
            {
                info.FriendlyName = device.FriendlyName;
            }
            catch
            {
                info.FriendlyName = "(未知设备)";
            }

            try
            {
                info.Description = device.DeviceFriendlyName;
            }
            catch
            {
                info.Description = "";
            }

            return info;
        }

        private static int CompareEndpoints(EndpointInfo left, EndpointInfo right)
        {
            if (left.IsActive != right.IsActive)
            {
                return left.IsActive ? -1 : 1;
            }

            bool leftRealtek = Contains(left.Description ?? "", "Realtek");
            bool rightRealtek = Contains(right.Description ?? "", "Realtek");
            if (leftRealtek != rightRealtek)
            {
                return leftRealtek ? -1 : 1;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        }

        private void DisposeEnumerator()
        {
            if (_enumerator != null)
            {
                try
                {
                    _enumerator.Dispose();
                }
                catch
                {
                }
                _enumerator = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("AudioEndpointCatalog");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            DisposeEnumerator();
        }
    }
}


