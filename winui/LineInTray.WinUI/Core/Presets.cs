using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace LineInTray
{
    [XmlType("preset")]
    public sealed class AudioPreset
    {
        [XmlAttribute("id")]
        public string Id = "";

        [XmlAttribute("name")]
        public string Name = "";

        [XmlAttribute("endpoint")]
        public string EndpointId = "";

        [XmlAttribute("icon")]
        public string Icon = "builtin:linein";

        [XmlAttribute("setDefault")]
        public bool SetDefault = true;

        public AudioPreset Clone()
        {
            AudioPreset copy = new AudioPreset();
            copy.Id = Id;
            copy.Name = Name;
            copy.EndpointId = EndpointId;
            copy.Icon = Icon;
            copy.SetDefault = SetDefault;
            return copy;
        }
    }

    [XmlRoot("presets")]
    public sealed class PresetCollection
    {
        [XmlElement("preset")]
        public List<AudioPreset> Items = new List<AudioPreset>();
    }

    internal static class PresetStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(PresetCollection));

        public static List<AudioPreset> Load()
        {
            if (!File.Exists(AppInfo.PresetsPath))
            {
                return new List<AudioPreset>();
            }

            try
            {
                using (FileStream stream = new FileStream(
                    AppInfo.PresetsPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    PresetCollection collection = (PresetCollection)Serializer.Deserialize(stream);
                    if (collection == null || collection.Items == null)
                    {
                        return new List<AudioPreset>();
                    }
                    for (int i = 0; i < collection.Items.Count; i++)
                    {
                        Normalize(collection.Items[i]);
                    }
                    return collection.Items;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("读取音频输入预设失败", ex);
                return new List<AudioPreset>();
            }
        }

        public static void Save(IList<AudioPreset> presets)
        {
            try
            {
                AppInfo.EnsureAppDataDir();
                PresetCollection collection = new PresetCollection();
                if (presets != null)
                {
                    for (int i = 0; i < presets.Count; i++)
                    {
                        if (presets[i] != null)
                        {
                            Normalize(presets[i]);
                            collection.Items.Add(presets[i]);
                        }
                    }
                }

                string temporary = AppInfo.PresetsPath + ".tmp";
                using (FileStream stream = new FileStream(
                    temporary,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    Serializer.Serialize(writer, collection);
                }

                if (File.Exists(AppInfo.PresetsPath))
                {
                    try
                    {
                        File.Replace(temporary, AppInfo.PresetsPath, null);
                    }
                    catch
                    {
                        File.Copy(temporary, AppInfo.PresetsPath, true);
                        File.Delete(temporary);
                    }
                }
                else
                {
                    File.Move(temporary, AppInfo.PresetsPath);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("保存音频输入预设失败", ex);
            }
        }

        private static void Normalize(AudioPreset preset)
        {
            if (preset == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(preset.Id))
            {
                preset.Id = Guid.NewGuid().ToString("N");
            }
            if (preset.Name == null)
            {
                preset.Name = "";
            }
            if (preset.EndpointId == null)
            {
                preset.EndpointId = "";
            }
            if (string.IsNullOrEmpty(preset.Icon))
            {
                preset.Icon = "builtin:mic";
            }
        }
    }
}
