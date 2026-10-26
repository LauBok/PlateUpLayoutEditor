using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kitchen;
using KitchenData;
using Newtonsoft.Json;
using PlateUpLayoutEditor.Core;

namespace KitchenLayoutEditor
{
    internal sealed class CatalogEntry
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("code")] public string Code { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("layer")] public string Layer { get; set; } = Layers.Default;
        [JsonProperty("purchasable")] public bool Purchasable { get; set; }
        [JsonProperty("immovable")] public bool Immovable { get; set; }
    }

    /// <summary>All appliances known to the running game (including modded ones).</summary>
    internal static class ApplianceCatalog
    {
        private static List<CatalogEntry>? _entries;
        private static Dictionary<int, CatalogEntry>? _byId;
        private static Dictionary<string, CatalogEntry>? _byName;

        private static readonly List<CatalogEntry> Empty = new List<CatalogEntry>();
        private static readonly Dictionary<int, CatalogEntry> EmptyById = new Dictionary<int, CatalogEntry>();
        private static readonly Dictionary<string, CatalogEntry> EmptyByName = new Dictionary<string, CatalogEntry>();

        public static IReadOnlyList<CatalogEntry> Entries
        {
            get { EnsureLoaded(); return _entries ?? Empty; }
        }

        public static bool TryGet(int id, out CatalogEntry entry)
        {
            EnsureLoaded();
            return (_byId ?? EmptyById).TryGetValue(id, out entry!);
        }

        public static string DisplayName(int id) => TryGet(id, out var e) ? e.Name : "#" + id;

        /// <summary>Fills ApplianceId and Layer of a record; ApplianceId becomes 0 when unknown.</summary>
        public static void Resolve(ApplianceRecord record)
        {
            EnsureLoaded();
            CatalogEntry? entry = null;
            if (record.ApplianceId != 0) (_byId ?? EmptyById).TryGetValue(record.ApplianceId, out entry);
            if (entry == null && !string.IsNullOrEmpty(record.Type)) (_byName ?? EmptyByName).TryGetValue(Normalize(record.Type), out entry);
            if (entry == null) { record.ApplianceId = 0; return; }
            record.ApplianceId = entry.Id;
            if (string.IsNullOrEmpty(record.Type)) record.Type = entry.Code;
            record.Layer = entry.Layer;
        }

        public static string ToJson() => JsonConvert.SerializeObject(Entries, Formatting.Indented);

        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            var list = new List<CatalogEntry>();
            // Game data not loaded yet: behave as an empty catalog and try again next time.
            if (GameData.Main == null) return;
            foreach (var appliance in GameData.Main.Get<Appliance>())
            {
                if (appliance == null) continue;
                list.Add(new CatalogEntry
                {
                    Id = appliance.ID,
                    Code = appliance.name,
                    Name = string.IsNullOrEmpty(appliance.Name) ? appliance.name : appliance.Name,
                    Layer = appliance.Layer.ToString(),
                    Purchasable = appliance.IsPurchasable,
                    Immovable = appliance.Properties != null && appliance.Properties.Any(p => p is CImmovable)
                });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var byId = new Dictionary<int, CatalogEntry>();
            var byName = new Dictionary<string, CatalogEntry>();
            foreach (var e in list)
            {
                byId[e.Id] = e;
                var code = Normalize(e.Code);
                if (code.Length > 0 && !byName.ContainsKey(code)) byName[code] = e;
            }
            foreach (var e in list)
            {
                var name = Normalize(e.Name);
                if (name.Length > 0 && !byName.ContainsKey(name)) byName[name] = e;
            }
            _entries = list;
            _byId = byId;
            _byName = byName;
        }

        private static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }
    }
}
