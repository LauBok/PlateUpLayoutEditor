using System;
using System.IO;
using Kitchen;
using KitchenData;
using Unity.Entities;
using UnityEngine;

namespace KitchenPlateUpLayoutEditor
{
    /// <summary>
    /// Experimental proof of game mutation using the game's blueprint creation components.
    /// F8 creates a free blueprint at a configurable world position.
    /// This deliberately does not spawn a placed appliance or perform a layout Apply.
    /// </summary>
    internal static class BlueprintSpawnProbe
    {
        private static bool loaded;
        private static int applianceId;
        private static Vector3 position;
        private static bool enabled;
        private static string configPath;

        internal static void Initialize()
        {
            if (loaded) return;
            loaded = true;
            configPath = Path.Combine(Path.GetDirectoryName(typeof(BlueprintSpawnProbe).Assembly.Location), "spawn-probe.txt");
            try
            {
                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath,
                        "# Experimental F8 blueprint spawn. Run in a disposable SOLO restaurant.\n" +
                        "# Set a verified numeric PlateUp appliance ID. Zero disables spawning.\n" +
                        "applianceId=0\nworldX=-2\nworldZ=-4\n");
                }
                var x = -2f;
                var z = -4f;
                foreach (var line in File.ReadAllLines(configPath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (key == "applianceId") int.TryParse(value, out applianceId);
                    if (key == "worldX") float.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out x);
                    if (key == "worldZ") float.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out z);
                }
                position = new Vector3(x, 0, z);
                enabled = applianceId != 0;
                Debug.Log("[PlateUp Layout Editor] Spawn probe " + (enabled ? "configured" : "disabled") + ": " + configPath);
            }
            catch (Exception e) { enabled = false; Debug.LogError("[PlateUp Layout Editor] Probe config: " + e); }
        }

        internal static void Update()
        {
            if (!enabled || !Input.GetKeyDown(KeyCode.F8)) return;
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) { Debug.LogWarning("[PlateUp Layout Editor] No ECS world"); return; }
                var manager = world.EntityManager;
                var entity = manager.CreateEntity();
                manager.AddComponentData(entity, new CCreateAppliance { ID = ItemIdReference._BlueprintLetter });
                manager.AddComponentData(entity, new CPosition(position));
                manager.AddComponentData(entity, default(CLetter));
                manager.AddComponentData(entity, new CLetterBlueprint
                {
                    BlueprintID = AssetReference.Blueprint,
                    ApplianceID = applianceId,
                    Price = 0
                });
                manager.AddComponentData(entity, default(CShopEntity));
                Debug.Log("[PlateUp Layout Editor] Created blueprint request for appliance ID " + applianceId);
            }
            catch (Exception e) { Debug.LogError("[PlateUp Layout Editor] Blueprint spawn failed: " + e); }
        }
    }
}
