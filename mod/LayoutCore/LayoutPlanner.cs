using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PlateUpLayoutEditor.Core
{
    /// <summary>Pure planning layer. Does not mutate PlateUp! entities.</summary>
    public static class LayoutPlanner
    {
        public static LayoutPlan Plan(LayoutDocument current, LayoutDocument desired, ApplyMode mode, int offsetX = 0, int offsetY = 0)
        {
            var result = new LayoutPlan();
            if (current == null || desired == null) { result.Errors.Add("Documents cannot be null"); return result; }
            Validate(current, "current", result.Errors);
            Validate(desired, "desired", result.Errors);
            if (!result.IsValid) return result;

            // A Paste never reuses identities from an imported template.
            var incoming = desired.Appliances.Select(a => Copy(a, a.X + offsetX, a.Y + offsetY)).ToList();
            var oldById = current.Appliances.ToDictionary(a => a.Id, StringComparer.Ordinal);
            var newIds = new HashSet<string>(incoming.Select(a => a.Id), StringComparer.Ordinal);
            var removals = new List<ApplianceRecord>();
            var changes = new List<LayoutOperation>();

            if (mode == ApplyMode.Replace)
            {
                removals.AddRange(current.Appliances.Where(a => !newIds.Contains(a.Id)));
            }

            foreach (var a in incoming)
            {
                if (mode != ApplyMode.Paste && oldById.TryGetValue(a.Id, out var previous))
                {
                    if (!String.Equals(previous.Type, a.Type, StringComparison.Ordinal))
                    {
                        // Changing appliance type requires remove/create, not an in-place state mutation.
                        removals.Add(previous);
                        changes.Add(new LayoutOperation { Kind = OperationKind.Create, After = a });
                    }
                    else if (previous.X != a.X || previous.Y != a.Y)
                    {
                        changes.Add(new LayoutOperation { Kind = OperationKind.Move, Before = previous, After = a });
                    }
                    else if (previous.Rotation != a.Rotation || !SameState(previous, a))
                    {
                        changes.Add(new LayoutOperation { Kind = OperationKind.Update, Before = previous, After = a });
                    }
                }
                else
                {
                    if (mode == ApplyMode.Paste && oldById.ContainsKey(a.Id))
                    {
                        // Templates must never overwrite existing entity identities.
                        a.Id = UniqueId(a.Id, oldById.Keys.Concat(changes.Select(o => o.After!.Id)));
                    }
                    changes.Add(new LayoutOperation { Kind = OperationKind.Create, After = a });
                }
            }

            var removedIds = new HashSet<string>(removals.Select(a => a.Id), StringComparer.Ordinal);
            var changingIds = new HashSet<string>(changes.Where(c => c.Before != null).Select(c => c.Before!.Id), StringComparer.Ordinal);
            var final = current.Appliances.Where(a => !removedIds.Contains(a.Id) && !changingIds.Contains(a.Id))
                .Concat(changes.Select(c => c.After!)).ToList();
            foreach (var group in final.GroupBy(a => (a.X, a.Y)).Where(g => g.Count() > 1))
                result.Errors.Add("Destination collision at " + group.Key.X + "," + group.Key.Y);
            foreach (var a in removals) result.Operations.Add(new LayoutOperation { Kind = OperationKind.Remove, Before = a });
            result.Operations.AddRange(changes);
            return result;
        }

        private static string UniqueId(string baseId, IEnumerable<string> occupied)
        {
            var ids = new HashSet<string>(occupied, StringComparer.Ordinal);
            int n = 1;
            while (ids.Contains(baseId + "-copy-" + n)) n++;
            return baseId + "-copy-" + n;
        }

        private static ApplianceRecord Copy(ApplianceRecord a, int x, int y) =>
            new ApplianceRecord { Id = a.Id, Type = a.Type, X = x, Y = y, Rotation = a.Rotation, State = new Dictionary<string, object>(a.State) };

        private static bool SameState(ApplianceRecord a, ApplianceRecord b) =>
            JsonSerializer.Serialize(a.State.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value)) ==
            JsonSerializer.Serialize(b.State.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value));

        private static void Validate(LayoutDocument doc, string label, List<string> errors)
        {
            if (doc.Version != 1) errors.Add(label + ": unsupported schema version");
            if (doc.Appliances == null) { errors.Add(label + ": missing appliances"); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<(int, int)>();
            foreach (var a in doc.Appliances)
            {
                if (a == null) { errors.Add(label + ": null appliance"); continue; }
                if (String.IsNullOrWhiteSpace(a.Id) || !ids.Add(a.Id)) errors.Add(label + ": duplicate or empty ID");
                if (String.IsNullOrWhiteSpace(a.Type)) errors.Add(label + ": empty appliance type");
                if (a.Rotation != 0 && a.Rotation != 90 && a.Rotation != 180 && a.Rotation != 270) errors.Add(label + ": invalid rotation");
                if (a.State == null) errors.Add(label + ": null state");
                if (!cells.Add((a.X, a.Y))) errors.Add(label + ": overlapping appliances");
            }
        }
    }
}
