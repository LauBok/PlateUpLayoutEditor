using System;
using System.Collections.Generic;
using System.Linq;

namespace PlateUpLayoutEditor.Core
{
    /// <summary>An appliance currently placed in the restaurant, as read by the game adapter.</summary>
    public sealed class PlacedAppliance
    {
        /// <summary>Opaque handle the game adapter uses to find the entity again.</summary>
        public long Key { get; set; }
        public int ApplianceId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Rotation { get; set; }
        public string Layer { get; set; } = Layers.Default;
        /// <summary>True for appliances the planner must never remove or replace (immovable, structural).</summary>
        public bool Locked { get; set; }
        public ApplianceState? State { get; set; }
    }

    public interface IPlacementRules
    {
        /// <summary>Returns false (with a reason) when nothing may be placed on this tile.</summary>
        bool CanPlace(int x, int y, out string reason);
    }

    public enum ClearScope
    {
        /// <summary>Keep every existing appliance that is not in the way.</summary>
        None,
        /// <summary>Remove every unlocked appliance that the layout does not keep (full restaurant import).</summary>
        Everything,
        /// <summary>Remove unlocked appliances inside <see cref="ApplyOptions.ClearRegion"/> that the layout does not keep.</summary>
        Region
    }

    public enum ConflictPolicy
    {
        /// <summary>Leave the existing appliance and skip the incoming one.</summary>
        Skip,
        /// <summary>Remove the existing (unlocked) appliance and place the incoming one.</summary>
        Replace
    }

    public sealed class ApplyOptions
    {
        public ClearScope Clear { get; set; } = ClearScope.None;
        public GridRect? ClearRegion { get; set; }
        public ConflictPolicy Conflicts { get; set; } = ConflictPolicy.Replace;
        public bool RestoreState { get; set; } = true;
    }

    public sealed class UpdateOperation
    {
        public long Key { get; set; }
        public int Rotation { get; set; }
        public ApplianceState? State { get; set; }
    }

    public sealed class SkippedRecord
    {
        public ApplianceRecord Record { get; set; } = new ApplianceRecord();
        public string Reason { get; set; } = "";
    }

    public sealed class LayoutPlan
    {
        public List<long> Removals { get; } = new List<long>();
        public List<UpdateOperation> Updates { get; } = new List<UpdateOperation>();
        public List<ApplianceRecord> Spawns { get; } = new List<ApplianceRecord>();
        public List<SkippedRecord> Skipped { get; } = new List<SkippedRecord>();
        public int Unchanged { get; set; }

        public string Summary =>
            $"{Spawns.Count} to spawn, {Removals.Count} to remove, {Updates.Count} to update, {Unchanged} unchanged, {Skipped.Count} skipped";
    }

    public static class ApplyPlanner
    {
        /// <param name="desired">Records in world coordinates with ApplianceId resolved (0 = unknown, skipped).</param>
        public static LayoutPlan Plan(IReadOnlyList<PlacedAppliance> current, IReadOnlyList<ApplianceRecord> desired,
            IPlacementRules rules, ApplyOptions options)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (desired == null) throw new ArgumentNullException(nameof(desired));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            options = options ?? new ApplyOptions();

            var plan = new LayoutPlan();
            var byCell = new Dictionary<(int, int, string), PlacedAppliance>();
            foreach (var p in current)
            {
                var cell = (p.X, p.Y, p.Layer ?? Layers.Default);
                // Two entities registered on one cell is a game-side inconsistency; keep the first.
                if (!byCell.ContainsKey(cell)) byCell.Add(cell, p);
            }

            var kept = new HashSet<long>();
            var removed = new HashSet<long>();
            var claimed = new HashSet<(int, int, string)>();

            foreach (var record in desired)
            {
                var cell = (record.X, record.Y, record.LayerOrDefault);
                if (record.ApplianceId == 0)
                {
                    plan.Skipped.Add(new SkippedRecord { Record = record, Reason = "unknown appliance '" + record.Type + "'" });
                    continue;
                }
                if (!claimed.Add(cell))
                {
                    plan.Skipped.Add(new SkippedRecord { Record = record, Reason = "another record already targets this tile" });
                    continue;
                }
                if (!rules.CanPlace(record.X, record.Y, out var reason))
                {
                    plan.Skipped.Add(new SkippedRecord { Record = record, Reason = reason });
                    continue;
                }

                if (byCell.TryGetValue(cell, out var existing))
                {
                    if (existing.ApplianceId == record.ApplianceId)
                    {
                        // Same appliance already here: keep the entity (and whatever it holds), only fix facing/state.
                        kept.Add(existing.Key);
                        var rotation = Rotations.Normalize(record.Rotation);
                        var wantState = options.RestoreState && record.State != null && !record.State.IsEmpty;
                        bool rotationDiffers = Rotations.Normalize(existing.Rotation) != rotation;
                        bool stateDiffers = wantState && !ApplianceState.AreEquivalent(existing.State, record.State);
                        if (existing.Locked && (rotationDiffers || stateDiffers))
                        {
                            plan.Skipped.Add(new SkippedRecord { Record = record, Reason = "existing appliance is locked" });
                        }
                        else if (rotationDiffers || stateDiffers)
                        {
                            plan.Updates.Add(new UpdateOperation { Key = existing.Key, Rotation = rotation, State = wantState ? record.State!.Clone() : null });
                        }
                        else plan.Unchanged++;
                        continue;
                    }
                    if (existing.Locked)
                    {
                        kept.Add(existing.Key);
                        plan.Skipped.Add(new SkippedRecord { Record = record, Reason = "tile holds a locked appliance" });
                        continue;
                    }
                    bool clearsAnyway = WouldClear(existing, options);
                    if (options.Conflicts == ConflictPolicy.Skip && !clearsAnyway)
                    {
                        kept.Add(existing.Key);
                        plan.Skipped.Add(new SkippedRecord { Record = record, Reason = "tile is occupied" });
                        continue;
                    }
                    if (removed.Add(existing.Key)) plan.Removals.Add(existing.Key);
                }

                var spawn = record.Clone();
                spawn.Rotation = Rotations.Normalize(spawn.Rotation);
                if (!options.RestoreState) spawn.State = null;
                plan.Spawns.Add(spawn);
            }

            foreach (var p in current)
            {
                if (p.Locked || kept.Contains(p.Key) || removed.Contains(p.Key)) continue;
                if (WouldClear(p, options))
                {
                    removed.Add(p.Key);
                    plan.Removals.Add(p.Key);
                }
            }
            return plan;
        }

        private static bool WouldClear(PlacedAppliance p, ApplyOptions options)
        {
            switch (options.Clear)
            {
                case ClearScope.Everything: return !p.Locked;
                case ClearScope.Region: return !p.Locked && options.ClearRegion != null && options.ClearRegion.Contains(p.X, p.Y);
                default: return false;
            }
        }
    }
}
