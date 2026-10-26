using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlateUpLayoutEditor.Core
{
    public static class LayoutJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static string Serialize(LayoutDocument document) => JsonConvert.SerializeObject(document, Settings);

        /// <summary>Parses and validates a layout. Throws <see cref="LayoutFormatException"/> with a readable message.</summary>
        public static LayoutDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new LayoutFormatException("Layout file is empty");
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException e) { throw new LayoutFormatException("Invalid JSON: " + e.Message); }

            var version = root.Value<int?>("version") ?? 0;
            if (version != 1 && version != LayoutDocument.CurrentVersion)
                throw new LayoutFormatException("Unsupported layout version " + version);

            LayoutDocument doc;
            try { doc = root.ToObject<LayoutDocument>(JsonSerializer.Create(Settings)) ?? new LayoutDocument(); }
            catch (JsonException e) { throw new LayoutFormatException("Invalid layout: " + e.Message); }

            if (version == 1)
            {
                // Version 1 (the first browser prototype) only had illustrative type names.
                doc.Format = LayoutDocument.FormatName;
                doc.Kind = LayoutKinds.Restaurant;
            }
            doc.Version = LayoutDocument.CurrentVersion;
            if (doc.Appliances == null) doc.Appliances = new List<ApplianceRecord>();
            var errors = Validate(doc);
            if (errors.Count > 0) throw new LayoutFormatException(string.Join("; ", errors));
            return doc;
        }

        public static List<string> Validate(LayoutDocument doc)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<(int, int, string)>();
            for (int i = 0; i < doc.Appliances.Count; i++)
            {
                var a = doc.Appliances[i];
                var label = "appliance #" + (i + 1);
                if (a == null) { errors.Add(label + " is null"); continue; }
                if (string.IsNullOrWhiteSpace(a.Id)) a.Id = "a" + (i + 1);
                if (!ids.Add(a.Id)) errors.Add(label + ": duplicate id '" + a.Id + "'");
                if (a.ApplianceId == 0 && string.IsNullOrWhiteSpace(a.Type)) errors.Add(label + ": needs applianceId or type");
                a.Rotation = Rotations.Normalize(a.Rotation);
                if (a.Rotation % 90 != 0) errors.Add(label + ": rotation must be a multiple of 90");
                if (!cells.Add((a.X, a.Y, a.LayerOrDefault)))
                    errors.Add(label + ": overlaps another appliance at " + a.X + "," + a.Y);
            }
            return errors;
        }
    }

    public sealed class LayoutFormatException : Exception
    {
        public LayoutFormatException(string message) : base(message) { }
    }

    public static class Rotations
    {
        public static int Normalize(int degrees) => ((degrees % 360) + 360) % 360;
    }
}
