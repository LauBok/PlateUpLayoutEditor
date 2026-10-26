using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PlateUpLayoutEditor.Core
{
    /// <summary>Snapshot of a game appliance, supplied by a version-specific game adapter.</summary>
    public sealed class ApplianceSnapshot
    {
        public string StableId { get; set; } = "";
        public string ApplianceType { get; set; } = "";
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int RotationDegrees { get; set; }
        public Dictionary<string, object> State { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>Read-only contract implemented by the PlateUp ECS integration.</summary>
    public interface IReadOnlyRestaurantSource
    {
        IEnumerable<ApplianceSnapshot> ReadAppliances();
    }

    /// <summary>
    /// Export is pure and cannot change the game. The adapter is responsible for
    /// resolving game entity identifiers, positions and appliance-specific state.
    /// </summary>
    public static class ExportPipeline
    {
        public static LayoutDocument Capture(IReadOnlyRestaurantSource source, string name)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var appliances = source.ReadAppliances().ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var occupied = new HashSet<(int, int)>();
            foreach (var item in appliances)
            {
                if (item == null) throw new InvalidOperationException("Null appliance");
                if (String.IsNullOrWhiteSpace(item.StableId) || !ids.Add(item.StableId))
                    throw new InvalidOperationException("Missing or duplicate appliance ID");
                if (String.IsNullOrWhiteSpace(item.ApplianceType))
                    throw new InvalidOperationException("Missing appliance type");
                if (!occupied.Add((item.GridX, item.GridY)))
                    throw new InvalidOperationException("Multiple appliances in one grid tile");
                if (item.RotationDegrees != 0 && item.RotationDegrees != 90 &&
                    item.RotationDegrees != 180 && item.RotationDegrees != 270)
                    throw new InvalidOperationException("Unsupported appliance rotation");
                if (item.State == null) throw new InvalidOperationException("Null appliance state");
            }
            return new LayoutDocument
            {
                Name = name ?? "",
                Version = 1,
                Origin = new GridPoint { X = 0, Y = 0 },
                Appliances = appliances.Select(item => new ApplianceRecord
                {
                    Id = item.StableId,
                    Type = item.ApplianceType,
                    X = item.GridX,
                    Y = item.GridY,
                    Rotation = item.RotationDegrees,
                    State = new Dictionary<string, object>(item.State)
                }).ToList()
            };
        }

        public static string ToJson(LayoutDocument document) =>
            JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }
}
