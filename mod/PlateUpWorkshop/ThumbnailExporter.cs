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
    /// Renders every appliance prefab straight from above, framed to exactly one tile, and writes the
    /// pictures to thumbnails.json. The browser editor draws them on the grid rotated by each
    /// appliance's facing, so the plan looks like the restaurant seen from above in game.
    /// Picture "up" is the appliance's forward (+Z) direction.
    /// The camera set-up follows IcedMilo's "Stuff on Demand" SnapshotUtils (snapshot layer, orthographic
    /// camera looking down), but clears to transparent and frames one tile instead of an iso preview.
    /// </summary>
    internal static class ThumbnailExporter
    {
        public const string FileName = "thumbnails.json";
        private const int Size = 128;
        private const int PerFrame = 4;
        /// <summary>Far away from the restaurant so nothing else can end up in the picture.</summary>
        private static readonly Vector3 Stage = new Vector3(500f, 0f, 500f);
        private static readonly int NightFade = Shader.PropertyToID("_NightFade");

        public static bool Running { get; private set; }

        private sealed class ThumbnailFile
        {
            [JsonProperty("format")] public string Format = "plateup-thumbnails";
            [JsonProperty("version")] public int Version = 2;
            /// <summary>"top": orthographic from above, one tile, picture up = appliance forward.</summary>
            [JsonProperty("view")] public string View = "top";
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
            Camera? camera = null;
            try { camera = CreateCamera(); }
            catch (Exception e) { report("Cannot create snapshot camera: " + e.Message); Running = false; yield break; }

            foreach (var entry in entries)
            {
                index++;
                try
                {
                    if (GameData.Main.TryGet(entry.Id, out Appliance appliance) && appliance.Prefab != null)
                    {
                        var png = Render(camera, appliance.Prefab);
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
            UnityEngine.Object.Destroy(camera.gameObject);

            try
            {
                var path = Path.Combine(LayoutFiles.Folder, FileName);
                File.WriteAllText(path, JsonConvert.SerializeObject(file));
                report("Wrote " + file.Images.Count + " thumbnails to " + FileName + (failed > 0 ? " (" + failed + " failed)" : ""));
            }
            catch (Exception e) { report("Could not write thumbnails: " + e.Message); }
            Running = false;
        }

        private static int SnapshotLayer
        {
            get
            {
                int layer = LayerMask.NameToLayer("Snapshot Camera");
                return layer >= 0 ? layer : 31;
            }
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("LayoutEditorThumbnailCamera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 0.5f;          // half height: exactly one tile
            camera.aspect = 1f;
            camera.cullingMask = 1 << SnapshotLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            // Looking straight down; picture up = world +Z = appliance forward.
            camera.transform.position = Stage + new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            return camera;
        }

        private static byte[]? Render(Camera camera, GameObject prefab)
        {
            // Night-time darkening would otherwise end up in the pictures.
            float fade = Shader.GetGlobalFloat(NightFade);
            Shader.SetGlobalFloat(NightFade, 0f);
            var instance = UnityEngine.Object.Instantiate(prefab, Stage, Quaternion.identity);
            var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            try
            {
                SetLayer(instance, SnapshotLayer);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply(false);
                var png = texture.EncodeToPNG();
                UnityEngine.Object.Destroy(texture);
                return png;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(instance);
                Shader.SetGlobalFloat(NightFade, fade);
            }
        }

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
