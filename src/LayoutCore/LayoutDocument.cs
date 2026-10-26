using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlateUpLayoutEditor.Core
{
    /// <summary>
    /// Portable layout file shared by the browser editor and the game mod.
    /// Coordinates are world tiles: x = world X, y = world Z (north is +y, the front door is at low y).
    /// Rotation is the facing direction in degrees, clockwise when seen from above: 0 = +y, 90 = +x.
    /// </summary>
    public sealed class LayoutDocument
    {
        public const string FormatName = "plateup-layout";
        public const int CurrentVersion = 2;

        [JsonProperty("format")] public string Format { get; set; } = FormatName;
        [JsonProperty("version")] public int Version { get; set; } = CurrentVersion;
        /// <summary>"restaurant" for a full kitchen export, "module" for a relocatable fragment.</summary>
        [JsonProperty("kind")] public string Kind { get; set; } = LayoutKinds.Restaurant;
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("bounds", NullValueHandling = NullValueHandling.Ignore)] public GridRect? Bounds { get; set; }
        /// <summary>Optional read-only floor plan captured from the game, used by the editor as background.</summary>
        [JsonProperty("tiles", NullValueHandling = NullValueHandling.Ignore)] public List<FloorTile>? Tiles { get; set; }
        /// <summary>Optional blocked edges between adjacent tiles (walls), captured from the game.</summary>
        [JsonProperty("walls", NullValueHandling = NullValueHandling.Ignore)] public List<WallEdge>? Walls { get; set; }
        [JsonProperty("appliances")] public List<ApplianceRecord> Appliances { get; set; } = new List<ApplianceRecord>();
    }

    public static class LayoutKinds
    {
        public const string Restaurant = "restaurant";
        public const string Module = "module";
    }

    public sealed class ApplianceRecord
    {
        /// <summary>Document-local identifier; not a game entity ID.</summary>
        [JsonProperty("id")] public string Id { get; set; } = "";
        /// <summary>PlateUp GameDataObject ID of the appliance (authoritative when non-zero).</summary>
        [JsonProperty("applianceId")] public int ApplianceId { get; set; }
        /// <summary>Human readable name; used to resolve the appliance when applianceId is 0.</summary>
        [JsonProperty("type")] public string Type { get; set; } = "";
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        [JsonProperty("rotation")] public int Rotation { get; set; }
        /// <summary>Occupancy layer name ("Default", "Floor", ...). Two appliances may share a tile on different layers.</summary>
        [JsonProperty("layer", NullValueHandling = NullValueHandling.Ignore)] public string? Layer { get; set; }
        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)] public ApplianceState? State { get; set; }

        public ApplianceRecord Clone() => new ApplianceRecord
        {
            Id = Id, ApplianceId = ApplianceId, Type = Type, X = X, Y = Y, Rotation = Rotation,
            Layer = Layer, State = State?.Clone()
        };

        public string LayerOrDefault => string.IsNullOrEmpty(Layer) ? Layers.Default : Layer!;
    }

    public static class Layers
    {
        public const string Default = "Default";
    }

    /// <summary>
    /// Appliance configuration that affects automation. Every field is optional; only fields that
    /// the game appliance actually has are written or restored.
    /// </summary>
    public sealed class ApplianceState
    {
        /// <summary>Rotating grabber target (CConveyPushRotatable.Target), relative to the appliance facing.</summary>
        [JsonProperty("grabDirection", NullValueHandling = NullValueHandling.Ignore)] public string? GrabDirection { get; set; }
        /// <summary>Smart grabber filter enabled (CConveyPushItems.GrabSpecificType).</summary>
        [JsonProperty("grabSpecificType", NullValueHandling = NullValueHandling.Ignore)] public bool? GrabSpecificType { get; set; }
        /// <summary>Smart grabber filter item (CConveyPushItems.SpecificType).</summary>
        [JsonProperty("specificType", NullValueHandling = NullValueHandling.Ignore)] public int? SpecificType { get; set; }
        /// <summary>Smart grabber filter item components (CConveyPushItems.SpecificComponents).</summary>
        [JsonProperty("specificComponents", NullValueHandling = NullValueHandling.Ignore)] public List<int>? SpecificComponents { get; set; }

        [JsonIgnore]
        public bool IsEmpty => GrabDirection == null && GrabSpecificType == null && SpecificType == null && SpecificComponents == null;

        public ApplianceState Clone() => new ApplianceState
        {
            GrabDirection = GrabDirection,
            GrabSpecificType = GrabSpecificType,
            SpecificType = SpecificType,
            SpecificComponents = SpecificComponents == null ? null : new List<int>(SpecificComponents)
        };

        public static bool AreEquivalent(ApplianceState? a, ApplianceState? b)
        {
            if (a == null || a.IsEmpty) return b == null || b.IsEmpty;
            if (b == null) return a.IsEmpty;
            if (a.GrabDirection != b.GrabDirection || a.GrabSpecificType != b.GrabSpecificType || a.SpecificType != b.SpecificType)
                return false;
            var ac = a.SpecificComponents ?? new List<int>();
            var bc = b.SpecificComponents ?? new List<int>();
            if (ac.Count != bc.Count) return false;
            var sortedA = new List<int>(ac); sortedA.Sort();
            var sortedB = new List<int>(bc); sortedB.Sort();
            for (int i = 0; i < sortedA.Count; i++) if (sortedA[i] != sortedB[i]) return false;
            return true;
        }
    }

    public sealed class GridRect
    {
        [JsonProperty("minX")] public int MinX { get; set; }
        [JsonProperty("minY")] public int MinY { get; set; }
        [JsonProperty("maxX")] public int MaxX { get; set; }
        [JsonProperty("maxY")] public int MaxY { get; set; }

        public GridRect() { }
        public GridRect(int minX, int minY, int maxX, int maxY)
        {
            MinX = System.Math.Min(minX, maxX); MaxX = System.Math.Max(minX, maxX);
            MinY = System.Math.Min(minY, maxY); MaxY = System.Math.Max(minY, maxY);
        }

        public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
    }

    public sealed class FloorTile
    {
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        /// <summary>Room identifier; 0 means outside the restaurant.</summary>
        [JsonProperty("room")] public int Room { get; set; }
        /// <summary>Game room type name, informational.</summary>
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)] public string? Type { get; set; }
        /// <summary>True when the mod would allow placing appliances here.</summary>
        [JsonProperty("placeable")] public bool Placeable { get; set; }
    }

    /// <summary>A blocked edge between (x,y) and its neighbour in direction "E" (x+1) or "N" (y+1).</summary>
    public sealed class WallEdge
    {
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        [JsonProperty("dir")] public string Dir { get; set; } = "E";
    }
}
