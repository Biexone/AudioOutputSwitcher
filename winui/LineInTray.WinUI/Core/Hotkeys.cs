using System;
using LineInTray.WinUI.Interop;

namespace LineInTray.WinUI
{
    internal sealed class HotkeyBinding
    {
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModWin = 0x0008;

        public uint Modifiers { get; private set; }
        public uint Key { get; private set; }
        public bool IsEmpty => Key == 0;

        public static bool TryParse(string? text, out HotkeyBinding binding)
        {
            binding = new HotkeyBinding();
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            uint modifiers = 0;
            uint key = 0;
            foreach (string raw in parts)
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control": modifiers |= ModControl; break;
                    case "alt": modifiers |= ModAlt; break;
                    case "shift": modifiers |= ModShift; break;
                    case "win":
                    case "windows": modifiers |= ModWin; break;
                    default:
                        if (key != 0) return false;
                        key = ParseKey(part);
                        if (key == 0) return false;
                        break;
                }
            }

            if (key == 0) return false;
            bool functionKey = key >= 0x70 && key <= 0x87;
            if (modifiers == 0 && !functionKey) return false;
            binding.Modifiers = modifiers;
            binding.Key = key;
            return true;
        }

        public bool IsDown()
        {
            if (IsEmpty) return false;
            if ((Modifiers & ModControl) != 0 && !IsKeyDown(0x11)) return false;
            if ((Modifiers & ModAlt) != 0 && !IsKeyDown(0x12)) return false;
            if ((Modifiers & ModShift) != 0 && !IsKeyDown(0x10)) return false;
            if ((Modifiers & ModWin) != 0 && !IsKeyDown(0x5B) && !IsKeyDown(0x5C)) return false;
            return IsKeyDown(Key);
        }

        public static string Format(uint modifiers, uint key)
        {
            string text = "";
            if ((modifiers & ModControl) != 0) text += "Ctrl+";
            if ((modifiers & ModAlt) != 0) text += "Alt+";
            if ((modifiers & ModShift) != 0) text += "Shift+";
            if ((modifiers & ModWin) != 0) text += "Win+";
            return text + (KeyName(key) ?? "");
        }

        public static string? KeyName(uint key)
        {
            if (key >= 0x41 && key <= 0x5A) return ((char)key).ToString();
            if (key >= 0x30 && key <= 0x39) return ((char)key).ToString();
            if (key >= 0x70 && key <= 0x87) return "F" + (key - 0x6F).ToString();
            switch (key)
            {
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Escape";
                case 0x08: return "Backspace";
                case 0x2E: return "Delete";
                case 0x2D: return "Insert";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                default: return null;
            }
        }

        private static uint ParseKey(string part)
        {
            if (part.Length == 1)
            {
                char c = char.ToUpperInvariant(part[0]);
                if (c >= 'A' && c <= 'Z') return c;
                if (c >= '0' && c <= '9') return c;
            }
            if (part.Length >= 2 && (part[0] == 'F' || part[0] == 'f'))
            {
                int number;
                if (int.TryParse(part.Substring(1), out number) && number >= 1 && number <= 24)
                {
                    return (uint)(0x6F + number);
                }
            }
            switch (part.ToLowerInvariant())
            {
                case "space": return 0x20;
                case "tab": return 0x09;
                case "enter":
                case "return": return 0x0D;
                case "escape":
                case "esc": return 0x1B;
                case "backspace": return 0x08;
                case "delete":
                case "del": return 0x2E;
                case "insert":
                case "ins": return 0x2D;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup":
                case "pgup": return 0x21;
                case "pagedown":
                case "pgdn": return 0x22;
                case "left": return 0x25;
                case "up": return 0x26;
                case "right": return 0x27;
                case "down": return 0x28;
                default: return 0;
            }
        }

        private static bool IsKeyDown(uint key) => (Native.GetAsyncKeyState((int)key) & 0x8000) != 0;
    }
}
