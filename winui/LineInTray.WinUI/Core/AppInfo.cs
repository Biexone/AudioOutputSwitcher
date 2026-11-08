using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace LineInTray
{
    internal static class AppInfo
    {
        public const string Title = "线路输入监听";
        public const string Version = "2.2.0";
        public const string MutexName = "Local\\LineInTray.SingleInstance";
        public const string RunValueName = "LineInTray";

        private static string _dataDir;

        public static string AppDataDir
        {
            get
            {
                if (_dataDir == null)
                {
                    _dataDir = ResolveDataDir();
                }
                return _dataDir;
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(AppDataDir, "settings.ini"); }
        }

        public static string LogPath
        {
            get { return Path.Combine(AppDataDir, "lineintray.log"); }
        }

        public static string DiagnosePath
        {
            get { return Path.Combine(AppDataDir, "diagnose.txt"); }
        }

        public static string SmokeTestPath
        {
            get { return Path.Combine(AppDataDir, "smoke-test.txt"); }
        }

        public static string PresetsPath
        {
            get { return Path.Combine(AppDataDir, "presets.xml"); }
        }

        public static string IconsDir
        {
            get { return Path.Combine(AppDataDir, "icons"); }
        }

        public static string ExePath
        {
            get
            {
                string location = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(location))
                {
                    try { location = Process.GetCurrentProcess().MainModule?.FileName ?? ""; } catch { }
                }
                if (string.IsNullOrEmpty(location))
                {
                    location = Assembly.GetExecutingAssembly().Location;
                }
                if (string.IsNullOrEmpty(location))
                {
                    location = Path.Combine(Environment.CurrentDirectory, "LineInTrayWinUI.exe");
                }
                return location;
            }
        }

        public static void EnsureAppDataDir()
        {
            Directory.CreateDirectory(AppDataDir);
        }

        private static string ResolveDataDir()
        {
            string overrideDir = Environment.GetEnvironmentVariable("LINEINTRAY_DATA_DIR");
            if (!string.IsNullOrEmpty(overrideDir) && TryPrepareDirectory(overrideDir))
            {
                return Path.GetFullPath(overrideDir);
            }

            string roaming = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LineInTray");
            if (TryPrepareDirectory(roaming))
            {
                return roaming;
            }

            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LineInTray");
            if (TryPrepareDirectory(local))
            {
                return local;
            }

            string exeDir = Path.GetDirectoryName(ExePath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                string portable = Path.Combine(exeDir, "LineInTray-data");
                if (TryPrepareDirectory(portable))
                {
                    return portable;
                }
            }

            string temp = Path.Combine(Path.GetTempPath(), "LineInTray");
            if (TryPrepareDirectory(temp))
            {
                return temp;
            }

            return roaming;
        }

        private static bool TryPrepareDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                string probe = Path.Combine(path, ".write-test");
                using (FileStream stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.WriteByte(0);
                }
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

