using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PlateUpLayoutEditor.Core;
using UnityEngine;

namespace KitchenLayoutEditor
{
    internal sealed class EditorSettings
    {
        [JsonProperty("allowClients")] public bool AllowClients = true;
        [JsonProperty("conflicts")] public ConflictPolicy Conflicts { get; set; } = ConflictPolicy.Replace;
        [JsonProperty("restoreState")] public bool RestoreState = true;
        [JsonProperty("allowDuringDay")] public bool AllowDuringDay;
        [JsonProperty("toggleKey")] public string ToggleKey { get; set; } = "F7";

        public static EditorSettings Current { get; private set; } = new EditorSettings();

        public static void Load()
        {
            try
            {
                var path = LayoutFiles.SettingsPath;
                if (File.Exists(path)) Current = JsonConvert.DeserializeObject<EditorSettings>(File.ReadAllText(path)) ?? new EditorSettings();
            }
            catch (Exception e) { Main.LogWarning("Could not read settings: " + e.Message); }
        }

        public static void Save()
        {
            try { File.WriteAllText(LayoutFiles.SettingsPath, JsonConvert.SerializeObject(Current, Formatting.Indented)); }
            catch (Exception e) { Main.LogWarning("Could not save settings: " + e.Message); }
        }
    }

    /// <summary>Layout files live in the game's persistent data folder so they never touch save files.</summary>
    internal static class LayoutFiles
    {
        public static string Folder
        {
            get
            {
                var folder = Path.Combine(Application.persistentDataPath, "LayoutEditor");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static string SettingsPath => Path.Combine(Folder, "settings.json");

        public static List<string> List() =>
            Directory.GetFiles(Folder, "*.json")
                .Where(f => !string.Equals(Path.GetFileName(f), "settings.json", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(Path.GetFileName(f), "catalog.json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Select(Path.GetFileName)
                .ToList();

        public static string Save(string baseName, string json)
        {
            var name = Sanitize(baseName);
            if (string.IsNullOrEmpty(name)) name = "layout";
            var path = Path.Combine(Folder, name + ".json");
            File.WriteAllText(path, json);
            return Path.GetFileName(path);
        }

        public static string Read(string fileName) => File.ReadAllText(Path.Combine(Folder, Path.GetFileName(fileName)));

        public static LayoutDocument Load(string fileName) => LayoutJson.Parse(Read(fileName));

        public static void WriteCatalog()
        {
            try { File.WriteAllText(Path.Combine(Folder, "catalog.json"), ApplianceCatalog.ToJson()); }
            catch (Exception e) { Main.LogWarning("Could not write catalog: " + e.Message); }
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((name ?? "").Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
