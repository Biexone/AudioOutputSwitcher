using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LineInTray.WinUI
{
    internal static class Loc
    {
        private sealed class Entry
        {
            public string Key = "";
            public string Zh = "";
            public string En = "";
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Dictionary<string, Entry> ByKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Entry> ByText = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static bool _english;

        static Loc()
        {
            Add("lang.system", "跟随系统", "Use system language");
            Add("lang.english", "English", "English");
            Add("lang.chinese", "简体中文", "简体中文");
            Add("theme.system", "跟随系统", "Use system");
            Add("theme.light", "浅色", "Light");
            Add("theme.dark", "深色", "Dark");
            Add("panel.title", "音频输出", "Audio Output");
            Add("panel.monitor.start", "开始监听", "Start monitoring");
            Add("panel.monitor.stop", "停止监听", "Stop monitoring");
            Add("panel.section.quick", "快速切换", "Quick switch");
            Add("panel.manage", "管理", "Manage");
            Add("panel.open", "打开主界面", "Open app");
            Add("panel.exit", "退出", "Exit");
            Add("panel.settings", "设置", "Settings");
            Add("panel.close", "关闭", "Close");
            Add("panel.empty", "还没有快捷方式，点击“管理”添加。", "No shortcuts yet. Click Manage to add one.");
            Add("panel.duplicate", "提示：多个快捷方式指向同一音频输出，切换不会改变声音。点“管理”修改设备。", "Note: multiple shortcuts point to the same output; switching them will not change the sound. Use Manage to edit them.");
            Add("main.title", "音频输出快捷切换", "Audio Output Switcher");
            Add("main.subtitle", "音频输出快捷切换，线路输入监听可选", "Quick output switching with optional line-in monitoring");
            Add("main.openpanel", "打开快捷面板", "Open quick panel");
            Add("main.exit", "退出", "Exit");
            Add("main.monitor", "监听控制", "Monitoring");
            Add("main.defaultcapture", "Windows 默认录音：", "Windows default recording: ");
            Add("main.latency", "延迟", "Latency");
            Add("main.capture", "录音设备", "Recording device");
            Add("main.playback", "播放到", "Play to");
            Add("main.device.enable", "启用设备", "Enable device");
            Add("main.device.disable", "禁用设备", "Disable device");
            Add("main.refresh", "刷新设备", "Refresh devices");
            Add("main.autostart", "开机自动启动", "Start with Windows");
            Add("main.automonitor", "启动时自动监听", "Start monitoring on launch");
            Add("main.quick", "快速切换音频输出", "Quick output switching");
            Add("main.add", "+ 添加快捷方式", "+ Add shortcut");
            Add("main.appearance", "外观", "Appearance");
            Add("main.theme", "应用主题", "App theme");
            Add("main.quicktheme", "快捷面板主题", "Quick panel theme");
            Add("main.language", "界面语言", "Language");
            Add("hotkeys.title", "快捷键", "Keyboard shortcuts");
            Add("hotkeys.output", "切换音频输出", "Switch audio output");
            Add("hotkeys.monitor", "开关音频监听", "Toggle audio monitoring");
            Add("hotkeys.hint", "点击输入框后按下组合键；Backspace/Delete 清除。", "Click a box and press a key combination; Backspace/Delete clears it.");
            Add("main.other", "其他", "Other");
            Add("main.sound", "打开 Windows 声音设置", "Open Windows sound settings");
            Add("main.data", "打开数据目录", "Open data folder");
            Add("main.log", "查看日志", "View log");
            Add("main.tray", "显示托盘图标", "Show tray icon");
            Add("main.note", "提示：Windows 没有公开 API 可以把自定义控件插入音量弹窗；Win+A 面板始终显示快速切换，启用监听后才会多出监听控制。", "Note: Windows has no public API for embedding custom controls in the volume flyout. The Win+A panel always shows quick switching; monitoring controls appear only when monitoring is enabled.");
            Add("status.monitoring", "正在监听", "Monitoring");
            Add("status.monitoring.disabled", "监听功能已关闭", "Monitoring is off");
            Add("panel.monitoring.disabled", "线路输入监听已关闭", "Line-in monitoring is off");
            Add("main.monitoring.enable", "启用线路输入监听", "Enable line-in monitoring");
            Add("main.monitoring.off", "监听功能已关闭。快速切换和 Win+A 面板仍然可用。", "Monitoring is off. Quick switching and the Win+A panel are still available.");
            Add("status.notmonitoring", "未监听", "Not monitoring");
            Add("status.error", "错误：", "Error: ");
            Add("status.monitor.on", "监听中", "Live");
            Add("preset.active", "使用中", "Active");
            Add("preset.use", "使用", "Use");
            Add("preset.edit", "编辑", "Edit");
            Add("preset.delete", "删除", "Delete");
            Add("preset.add.title", "添加快捷方式", "Add shortcut");
            Add("preset.edit.title", "编辑快捷方式", "Edit shortcut");
            Add("preset.name", "名称", "Name");
            Add("preset.name.hint", "例如：耳机 / 音响 / 线路输入", "e.g. Headphones / Speakers / Line in");
            Add("preset.output", "音频输出", "Audio output");
            Add("preset.icon", "图标", "Icon");
            Add("preset.default", "切换时设为 Windows 默认播放设备", "Set as Windows default playback device");
            Add("preset.save", "保存", "Save");
            Add("preset.cancel", "取消", "Cancel");
            Add("icon.linein", "线路输入", "Line in");
            Add("icon.mic", "麦克风", "Microphone");
            Add("icon.headphone", "耳机", "Headphones");
            Add("icon.speaker", "音响", "Speakers");
            Add("icon.music", "音乐", "Music");
            Add("icon.game", "游戏", "Game");
            Add("icon.phone", "手机", "Phone");
            Add("icon.aux", "AUX", "AUX");
            Add("device.active", "已启用", "Enabled");
            Add("device.disabled", "已禁用", "Disabled");
            Add("device.notpresent", "不存在", "Not present");
            Add("device.unplugged", "未插入", "Unplugged");
            Add("device.unknown", "未知", "Unknown");
            Add("device.default", "设为默认", "Set as default");
            Add("msg.noinput", "找不到线路输入设备", "Line-in device not found");
            Add("msg.nooutput", "找不到播放设备", "Playback device not found");
            Add("msg.missingoutput", "输出设备不存在：", "Output device not found: ");
            Add("msg.inactiveoutput", "输出设备当前不可用：", "Output device is unavailable: ");
            Add("msg.missingdevice", "设备不存在：", "Device not found: ");
            Add("msg.monitorstarted", "开始监听", "Monitoring started");
            Add("msg.monitorstopped", "停止监听", "Monitoring stopped");
            Add("tray.openpanel", "打开快捷面板", "Open quick panel");
            Add("tray.openmain", "打开主界面", "Open app");
            Add("tray.toggle", "开始/停止监听", "Start/Stop monitoring");
            Add("tray.exit", "退出", "Exit");
            Add("tray.tooltip", "线路输入监听 - 音频输出快捷切换", "Line-in monitoring - audio output switcher");
            Add("playback.default", "默认播放设备", "Default playback device");
        }

        public static bool IsEnglish => _english;

        public static string LanguageCode => _english ? "en-US" : "zh-CN";

        public static void SetLanguage(string? setting)
        {
            if (string.Equals(setting, "en-US", StringComparison.OrdinalIgnoreCase) || string.Equals(setting, "en", StringComparison.OrdinalIgnoreCase))
            {
                _english = true;
            }
            else if (string.Equals(setting, "zh-CN", StringComparison.OrdinalIgnoreCase) || string.Equals(setting, "zh", StringComparison.OrdinalIgnoreCase))
            {
                _english = false;
            }
            else
            {
                _english = !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string T(string key)
        {
            Entry? entry;
            return ByKey.TryGetValue(key, out entry) ? (_english ? entry.En : entry.Zh) : key;
        }

        public static string State(string state)
        {
            switch (state)
            {
                case "已启用": return T("device.active");
                case "已禁用": return T("device.disabled");
                case "不存在": return T("device.notpresent");
                case "未插入": return T("device.unplugged");
                case "未知": return T("device.unknown");
                default: return state;
            }
        }

        public static void Apply(DependencyObject? root)
        {
            if (root == null) return;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock text) text.Text = Translate(text.Text);
                if (child is ContentControl content && content.Content is string value) content.Content = Translate(value);
                if (child is ComboBox combo && combo.Header is string header) combo.Header = Translate(header);
                if (child is TextBox textBox)
                {
                    if (textBox.Header is string textHeader) textBox.Header = Translate(textHeader);
                    if (!string.IsNullOrEmpty(textBox.PlaceholderText)) textBox.PlaceholderText = Translate(textBox.PlaceholderText);
                }
                if (child is NumberBox numberBox && numberBox.Header is string numberHeader) numberBox.Header = Translate(numberHeader);
                object? tip = ToolTipService.GetToolTip(child);
                if (tip is string tipText) ToolTipService.SetToolTip(child, Translate(tipText));
                Apply(child);
            }
        }

        public static string Translate(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            Entry? entry;
            if (ByText.TryGetValue(text, out entry))
            {
                string value = _english ? entry.En : entry.Zh;
                return string.IsNullOrEmpty(value) ? text : value;
            }
            return text;
        }

        private static void Add(string key, string zh, string en)
        {
            Entry entry = new Entry { Key = key, Zh = zh, En = en };
            Entries.Add(entry);
            ByKey[key] = entry;
            if (!ByText.ContainsKey(zh)) ByText[zh] = entry;
            if (!ByText.ContainsKey(en)) ByText[en] = entry;
        }
    }
}







