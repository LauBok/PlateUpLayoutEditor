using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlateUpLayoutEditor.Core
{
    public sealed class LayoutDocument
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("origin")] public GridPoint Origin { get; set; } = new GridPoint();
        [JsonPropertyName("appliances")] public List<ApplianceRecord> Appliances { get; set; } = new List<ApplianceRecord>();
    }

    public sealed class GridPoint
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
    }

    public sealed class ApplianceRecord
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("rotation")] public int Rotation { get; set; }
        // Opaque state is deliberately left to a game-specific adapter.
        [JsonPropertyName("state")] public Dictionary<string, object> State { get; set; } = new Dictionary<string, object>();
    }

    public enum ApplyMode { Replace, Merge, Paste }
    public enum OperationKind { Remove, Create, Move, Update }

    public sealed class LayoutOperation
    {
        public OperationKind Kind { get; set; }
        public ApplianceRecord? Before { get; set; }
        public ApplianceRecord? After { get; set; }
    }

    public sealed class LayoutPlan
    {
        public List<LayoutOperation> Operations { get; } = new List<LayoutOperation>();
        public List<string> Errors { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }
}
