using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace LineInTray
{
    public sealed class Settings
    {
        public string CaptureEndpointId = "";
        public string CaptureNameHint = "线路输入";
        public string PlaybackEndpointId = "";
        public int LatencyMs = 120;
        public bool HotkeyEnabled = true;
        public bool AutoMonitorOnStart = false;
        public bool MonitoringEnabled = true;
        public string Theme = "system";
        public string Language = "system";
        public string QuickPanelTheme = "system";

        public static Settings Load()
        {
            Settings settings = new Settings();

            try
            {
                if (File.Exists(AppInfo.SettingsPath))
                {
                    string section = "";
                    string[] lines = File.ReadAllLines(AppInfo.SettingsPath, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                        {
                            continue;
                        }

                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                            continue;
                        }

                        int equals = line.IndexOf('=');
                        if (equals <= 0)
                        {
                            continue;
                        }

                        string key = line.Substring(0, equals).Trim().ToLowerInvariant();
                        string value = line.Substring(equals + 1).Trim();
                        settings.SetValue(section, key, value);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("读取设置失败", ex);
            }

            settings.Normalize();
            return settings;
        }

        public void Save()
        {
            try
            {
                AppInfo.EnsureAppDataDir();
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("; LineInTray settings");
                builder.AppendLine("; 删除本文件可恢复默认设置。");
                builder.AppendLine();
                builder.AppendLine("[capture]");
                builder.AppendLine("endpoint_id=" + CaptureEndpointId);
                builder.AppendLine("name_hint=" + CaptureNameHint);
                builder.AppendLine();
                builder.AppendLine("[playback]");
                builder.AppendLine("endpoint_id=" + PlaybackEndpointId);
                builder.AppendLine();
                builder.AppendLine("[behavior]");
                builder.AppendLine("latency_ms=" + LatencyMs.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("hotkey_enabled=" + (HotkeyEnabled ? "1" : "0"));
                builder.AppendLine("auto_monitor_on_start=" + (AutoMonitorOnStart ? "1" : "0"));
                builder.AppendLine("monitoring_enabled=" + (MonitoringEnabled ? "1" : "0"));
                builder.AppendLine();
                builder.AppendLine("[appearance]");
                builder.AppendLine("theme=" + Theme);
                builder.AppendLine("language=" + Language);
                builder.AppendLine("quick_panel_theme=" + QuickPanelTheme);
                builder.AppendLine();
                builder.AppendLine("[meta]");
                builder.AppendLine("version=2");

                string temporary = AppInfo.SettingsPath + ".tmp";
                File.WriteAllText(temporary, builder.ToString(), new UTF8Encoding(false));

                if (File.Exists(AppInfo.SettingsPath))
                {
                    try
                    {
                        File.Replace(temporary, AppInfo.SettingsPath, null);
                    }
                    catch
                    {
                        File.Copy(temporary, AppInfo.SettingsPath, true);
                        File.Delete(temporary);
                    }
                }
                else
                {
                    File.Move(temporary, AppInfo.SettingsPath);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("保存设置失败", ex);
            }
        }

        public void Normalize()
        {
            if (CaptureEndpointId == null)
            {
                CaptureEndpointId = "";
            }
            if (PlaybackEndpointId == null)
            {
                PlaybackEndpointId = "";
            }
            if (string.IsNullOrWhiteSpace(CaptureNameHint))
            {
                CaptureNameHint = "线路输入";
            }
            if (LatencyMs < 50)
            {
                LatencyMs = 50;
            }
            if (LatencyMs > 500)
            {
                LatencyMs = 500;
            }
        }

        private void SetValue(string section, string key, string value)
        {
            if (section == "capture" && key == "endpoint_id")
            {
                CaptureEndpointId = value;
            }
            else if (section == "capture" && key == "name_hint")
            {
                CaptureNameHint = value;
            }
            else if (section == "playback" && key == "endpoint_id")
            {
                PlaybackEndpointId = value;
            }
            else if (section == "behavior" && key == "latency_ms")
            {
                int parsed;
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    LatencyMs = parsed;
                }
            }
            else if (section == "behavior" && key == "hotkey_enabled")
            {
                HotkeyEnabled = ParseBool(value, true);
            }
            else if (section == "behavior" && key == "auto_monitor_on_start")
            {
                AutoMonitorOnStart = ParseBool(value, false);
            }
            else if (section == "behavior" && key == "monitoring_enabled")
            {
                MonitoringEnabled = ParseBool(value, true);
            }
            else if (section == "appearance" && key == "theme")
            {
                Theme = value;
            }
            else if (section == "appearance" && key == "quick_panel_theme")
            {
                QuickPanelTheme = value;
            }
            else if (section == "appearance" && key == "language")
            {
                Language = value;
            }
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on")
            {
                return true;
            }
            if (normalized == "0" || normalized == "false" || normalized == "no" || normalized == "off")
            {
                return false;
            }
            return defaultValue;
        }
    }
}




