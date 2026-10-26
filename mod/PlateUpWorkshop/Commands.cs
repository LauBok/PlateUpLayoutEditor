using System.Collections.Generic;
using Newtonsoft.Json;
using PlateUpLayoutEditor.Core;

namespace KitchenLayoutEditor
{
    public enum CommandKind
    {
        /// <summary>Apply a layout or module (also used for single-appliance spawns).</summary>
        Apply,
        /// <summary>Export the whole restaurant, or a region as a module.</summary>
        Export,
        /// <summary>Remove every unlocked appliance in a region.</summary>
        Delete,
        /// <summary>Rotate the appliances in a region in place (each keeps its tile).</summary>
        Rotate,
        /// <summary>Move (and optionally rotate) the appliances in a region, keeping their entities.</summary>
        Move,
        /// <summary>Restore the snapshot taken before the last apply/delete/move.</summary>
        Undo
    }

    /// <summary>A request from any player's editor window to the host. Sent as JSON inside the view response.</summary>
    public sealed class LayoutCommand
    {
        [JsonProperty("kind")] public CommandKind Kind { get; set; }
        [JsonProperty("id")] public int RequestId { get; set; }

        /// <summary>Layout JSON for Apply.</summary>
        [JsonProperty("layout", NullValueHandling = NullValueHandling.Ignore)] public string? LayoutJson { get; set; }
        /// <summary>Apply: true = the layout is a module, placed with Origin and QuarterTurns. False = world coordinates.</summary>
        [JsonProperty("asModule")] public bool AsModule { get; set; }
        [JsonProperty("originX")] public int OriginX { get; set; }
        [JsonProperty("originY")] public int OriginY { get; set; }
        [JsonProperty("turns")] public int QuarterTurns { get; set; }
        [JsonProperty("clear")] public ClearScope Clear { get; set; }
        [JsonProperty("conflicts")] public ConflictPolicy Conflicts { get; set; } = ConflictPolicy.Replace;
        [JsonProperty("restoreState")] public bool RestoreState { get; set; } = true;

        /// <summary>Region for Export (null = whole restaurant), Delete, Rotate and Move.</summary>
        [JsonProperty("region", NullValueHandling = NullValueHandling.Ignore)] public GridRect? Region { get; set; }
        /// <summary>Move offset.</summary>
        [JsonProperty("dx")] public int Dx { get; set; }
        [JsonProperty("dy")] public int Dy { get; set; }

        /// <summary>Allow edits outside the preparation phase.</summary>
        [JsonProperty("allowDuringDay")] public bool AllowDuringDay { get; set; }
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string? Name { get; set; }
    }

    /// <summary>The host's answer, broadcast through the view; each client keeps only results addressed to it.</summary>
    public sealed class LayoutResult
    {
        [JsonProperty("seq")] public int Sequence { get; set; }
        [JsonProperty("input")] public int TargetInput { get; set; }
        [JsonProperty("id")] public int RequestId { get; set; }
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("message")] public string Message { get; set; } = "";
        /// <summary>Export result, when the command was an export.</summary>
        [JsonProperty("layout", NullValueHandling = NullValueHandling.Ignore)] public string? LayoutJson { get; set; }
    }

    public sealed class HostStatus
    {
        [JsonProperty("results")] public List<LayoutResult> Results { get; set; } = new List<LayoutResult>();
        [JsonProperty("clientsAllowed")] public bool ClientsAllowed { get; set; }
        [JsonProperty("inKitchen")] public bool InKitchen { get; set; }
        [JsonProperty("prep")] public bool IsPreparation { get; set; }
    }

    internal static class Wire
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static string Write<T>(T value) => JsonConvert.SerializeObject(value, Settings);
        public static T? Read<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonConvert.DeserializeObject<T>(json, Settings); }
            catch (JsonException) { return null; }
        }
    }
}
