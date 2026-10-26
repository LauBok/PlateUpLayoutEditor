using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Kitchen;
using KitchenData;
using Newtonsoft.Json;
using UnityEngine;

namespace KitchenLayoutEditor
{
    /// <summary>
    /// Renders every appliance prefab to a small PNG and writes them to thumbnails.json, which the
    /// browser editor imports to show real pictures instead of text tiles. Rendering uses the game's
    /// own snapshot helper with the same parameters as IcedMilo's "Stuff on Demand" appliance previews.
    /// </summary>
    internal static class ThumbnailExporter
    {
        public const string FileName = "thumbnails.json";
        private const int Size = 128;
        private const int PerFrame = 6;
        private static readonly int NightFade = Shader.PropertyToID("_NightFade");

        public static bool Running { get; private set; }

        private sealed class ThumbnailFile
        {
            [JsonProperty("format")] public string Format = "plateup-thumbnails";
            [JsonProperty("version")] public int Version = 1;
            [JsonProperty("size")] public int Size = ThumbnailExporter.Size;
            /// <summary>Appliance ID (as string) to a PNG data URL.</summary>
            [JsonProperty("images")] public Dictionary<string, string> Images = new Dictionary<string, string>();
        }

        public static IEnumerator Export(Action<string> report)
        {
            if (Running) yield break;
            Running = true;
            var file = new ThumbnailFile();
            int failed = 0, index = 0;
            var entries = new List<CatalogEntry>(ApplianceCatalog.Entries);
            foreach (var entry in entries)
            {
                index++;
                try
                {
                    if (GameData.Main.TryGet(entry.Id, out Appliance appliance) && appliance.Prefab != null)
                    {
                        var png = Render(appliance.Prefab);
                        if (png != null) file.Images[entry.Id.ToString()] = "data:image/png;base64," + Convert.ToBase64String(png);
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    Main.LogWarning("Thumbnail for " + entry.Code + " failed: " + e.Message);
                }
                if (index % PerFrame == 0)
                {
                    report("Rendering thumbnails " + index + "/" + entries.Count + "...");
                    yield return null;
                }
            }

            try
            {
                var path = Path.Combine(LayoutFiles.Folder, FileName);
                File.WriteAllText(path, JsonConvert.SerializeObject(file));
                report("Wrote " + file.Images.Count + " thumbnails to " + FileName + (failed > 0 ? " (" + failed + " failed)" : ""));
            }
            catch (Exception e) { report("Could not write thumbnails: " + e.Message); }
            Running = false;
        }

        private static byte[]? Render(GameObject prefab)
        {
            // Night-time darkening would otherwise end up in the pictures.
            float fade = Shader.GetGlobalFloat(NightFade);
            Shader.SetGlobalFloat(NightFade, 0f);
            try
            {
                var rotation = Quaternion.LookRotation(new Vector3(1f, -1f, 1f), new Vector3(0f, 1f, 1f));
                var snapshot = Snapshot.RenderPrefabToTexture(Size, Size, prefab, rotation, 0.5f, 0.5f, -10f, 10f, 0.5f,
                    -0.25f * new Vector3(0f, 1f, 1f));
                var texture = snapshot.Snapshot;
                if (texture == null) return null;
                var png = texture.EncodeToPNG();
                UnityEngine.Object.Destroy(texture);
                return png;
            }
            finally
            {
                Shader.SetGlobalFloat(NightFade, fade);
            }
        }
    }
}
