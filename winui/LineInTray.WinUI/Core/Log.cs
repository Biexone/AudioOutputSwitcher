using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace LineInTray
{
    internal static class Log
    {
        private const long MaxSize = 1024 * 1024;
        private static readonly object Sync = new object();
        private static StreamWriter _writer;
        private static long _size;

        public static void Initialize()
        {
            lock (Sync)
            {
                try
                {
                    AppInfo.EnsureAppDataDir();
                    OpenWriter();
                    WriteRaw("INFO", "==== LineInTray " + AppInfo.Version + " starting ====");
                }
                catch
                {
                }
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Exception(string context, Exception ex)
        {
            if (ex == null)
            {
                Write("ERROR", context);
                return;
            }

            Write("ERROR", context + ": " + ex.GetType().FullName + ": " + ex.Message);
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                WriteRaw("ERROR", ex.StackTrace);
            }

            Exception inner = ex.InnerException;
            while (inner != null)
            {
                WriteRaw("ERROR", "Inner: " + inner.GetType().FullName + ": " + inner.Message);
                inner = inner.InnerException;
            }
        }

        public static string Tail(int lines)
        {
            try
            {
                if (!File.Exists(AppInfo.LogPath))
                {
                    return "";
                }

                string[] all;
                using (FileStream stream = new FileStream(
                    AppInfo.LogPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    all = content.Replace("\r\n", "\n").Split('\n');
                }

                int start = Math.Max(0, all.Length - lines);
                StringBuilder builder = new StringBuilder();
                for (int i = start; i < all.Length; i++)
                {
                    if (all[i].Length > 0)
                    {
                        builder.AppendLine(all[i]);
                    }
                }
                return builder.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static void Write(string level, string message)
        {
            lock (Sync)
            {
                try
                {
                    if (_writer == null)
                    {
                        OpenWriter();
                    }
                    WriteRaw(level, message);
                }
                catch
                {
                }
            }
        }

        private static void WriteRaw(string level, string message)
        {
            if (_writer == null)
            {
                return;
            }

            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + " [" + level + "] " + message;
            _writer.WriteLine(line);
            _writer.Flush();
            _size += Encoding.UTF8.GetByteCount(line) + 2;

            if (_size > MaxSize)
            {
                Rotate();
            }
        }

        private static void OpenWriter()
        {
            AppInfo.EnsureAppDataDir();
            FileStream stream = new FileStream(
                AppInfo.LogPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            _writer = new StreamWriter(stream, new UTF8Encoding(false));
            _writer.AutoFlush = true;
            _size = stream.Length;
        }

        private static void Rotate()
        {
            try
            {
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }

                string oldPath = AppInfo.LogPath + ".1";
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
                if (File.Exists(AppInfo.LogPath))
                {
                    File.Move(AppInfo.LogPath, oldPath);
                }

                OpenWriter();
                WriteRaw("INFO", "log rotated");
            }
            catch
            {
            }
        }
    }
}
