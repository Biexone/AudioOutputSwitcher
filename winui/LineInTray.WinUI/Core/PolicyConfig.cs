using System;
using System.Runtime.InteropServices;

namespace LineInTray
{
    internal static class PolicyConfig
    {
        public static void SetVisibility(string endpointId, bool visible)
        {
            if (string.IsNullOrEmpty(endpointId))
            {
                throw new ArgumentException("端点 ID 不能为空。", "endpointId");
            }

            IPolicyConfig policy = null;
            try
            {
                policy = (IPolicyConfig)new CPolicyConfigClient();
                int result = policy.SetEndpointVisibility(endpointId, visible ? 1 : 0);
                if (result < 0)
                {
                    Marshal.ThrowExceptionForHR(result);
                }
            }
            finally
            {
                if (policy != null)
                {
                    Marshal.ReleaseComObject(policy);
                }
            }
        }

        public static void SetDefaultEndpoint(string endpointId, int role)
        {
            if (string.IsNullOrEmpty(endpointId))
            {
                throw new ArgumentException("端点 ID 不能为空。", "endpointId");
            }

            IPolicyConfig policy = null;
            try
            {
                policy = (IPolicyConfig)new CPolicyConfigClient();
                int result = policy.SetDefaultEndpoint(endpointId, (uint)role);
                if (result < 0)
                {
                    Marshal.ThrowExceptionForHR(result);
                }
            }
            finally
            {
                if (policy != null)
                {
                    Marshal.ReleaseComObject(policy);
                }
            }
        }

        public static void SetDefaultPlayback(string endpointId)
        {
            SetDefaultForAllRoles(endpointId);
        }

        public static void SetDefaultCapture(string endpointId)
        {
            SetDefaultForAllRoles(endpointId);
        }

        private static void SetDefaultForAllRoles(string endpointId)
        {
            int[] roles = new int[] { 0, 1, 2 };
            Exception? lastError = null;
            bool anySucceeded = false;

            for (int i = 0; i < roles.Length; i++)
            {
                try
                {
                    SetDefaultEndpoint(endpointId, roles[i]);
                    anySucceeded = true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Log.Exception("设置默认设备失败 (role=" + roles[i] + ")", ex);
                }
            }

            if (!anySucceeded && lastError != null)
            {
                throw lastError;
            }
        }

        [ComImport]
        [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        private class CPolicyConfigClient
        {
        }

        [ComImport]
        [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig]
            int GetMixFormat(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                out IntPtr format);

            [PreserveSig]
            int GetDeviceFormat(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                int isDefault,
                out IntPtr format);

            [PreserveSig]
            int ResetDeviceFormat(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName);

            [PreserveSig]
            int SetDeviceFormat(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                IntPtr endpointFormat,
                IntPtr mixFormat);

            [PreserveSig]
            int GetProcessingPeriod(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                int isDefault,
                out long defaultPeriod,
                out long minimumPeriod);

            [PreserveSig]
            int SetProcessingPeriod(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                ref long period);

            [PreserveSig]
            int GetShareMode(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                out int mode);

            [PreserveSig]
            int SetShareMode(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                int mode);

            [PreserveSig]
            int GetPropertyValue(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                IntPtr key,
                IntPtr value);

            [PreserveSig]
            int SetPropertyValue(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                IntPtr key,
                IntPtr value);

            [PreserveSig]
            int SetDefaultEndpoint(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                uint role);

            [PreserveSig]
            int SetEndpointVisibility(
                [MarshalAs(UnmanagedType.LPWStr)] string deviceName,
                int visible);
        }
    }
}


