using System;
using System.Collections.Generic;
using System.Linq;
using Kitchen;
using KitchenData;
using KitchenMods;
using PlateUpLayoutEditor.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KitchenLayoutEditor
{
    /// <summary>
    /// Host-side executor. Every restaurant change made by the editor happens here, on the host's
    /// simulation, so the game's own view replication keeps every client in sync.
    /// </summary>
    public class LayoutEditorSystem : GameSystemBase, IModSystem
    {
        private const int MaxUndo = 10;
        private const int PendingTimeoutFrames = 600;

        private struct PendingSpawn
        {
            public Entity Entity;
            public int Rotation;
            public ApplianceState? State;
            public int Frames;
        }

        private static readonly Queue<(int input, LayoutCommand command)> Incoming = new Queue<(int, LayoutCommand)>();
        private static readonly List<LayoutResult> Results = new List<LayoutResult>();
        private static int _resultSequence;

        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
        private readonly List<string> _undo = new List<string>();
        private EntityQuery _appliances;

        /// <summary>Most recent results, newest last. Read by the view system to broadcast.</summary>
        internal static IReadOnlyList<LayoutResult> RecentResults => Results;

        internal static void Enqueue(int input, LayoutCommand command)
        {
            if (command == null) return;
            Incoming.Enqueue((input, command));
        }

        protected override void Initialise()
        {
            base.Initialise();
            _appliances = GetEntityQuery(new QueryHelper().All(typeof(CAppliance), typeof(CPosition)));
        }

        protected override void OnUpdate()
        {
            FinishPendingSpawns();
            if (Incoming.Count == 0) return;

            bool inKitchen = GameInfo.CurrentScene == SceneType.Kitchen;
            while (Incoming.Count > 0)
            {
                var (input, command) = Incoming.Dequeue();
                if (!inKitchen)
                {
                    Respond(input, command, false, "Open a restaurant first (the editor only works inside a kitchen).");
                    continue;
                }
                try
                {
                    Execute(input, command);
                }
                catch (Exception e)
                {
                    Main.LogError("Command " + command.Kind + " failed: " + e);
                    Respond(input, command, false, command.Kind + " failed: " + e.Message);
                }
            }
        }

        private void Execute(int input, LayoutCommand command)
        {
            if (command.Kind != CommandKind.Export && !GameInfo.IsPreparationTime && !command.AllowDuringDay)
            {
                Respond(input, command, false, "Changes are only allowed during preparation (enable 'Allow during day' to override).");
                return;
            }
            switch (command.Kind)
            {
                case CommandKind.Export: Export(input, command); break;
                case CommandKind.Apply: Apply(input, command); break;
                case CommandKind.Delete: Delete(input, command); break;
                case CommandKind.Rotate: RotateInPlace(input, command); break;
                case CommandKind.Move: Move(input, command); break;
                case CommandKind.Undo: Undo(input, command); break;
                default: Respond(input, command, false, "Unknown command"); break;
            }
        }

        // ----- Reading the restaurant -----

        private sealed class LiveAppliance
        {
            public Entity Entity;
            public PlacedAppliance Info = new PlacedAppliance();
        }

        private List<LiveAppliance> Snapshot()
        {
            var list = new List<LiveAppliance>();
            var bounds = PlayableBounds();
            using (var entities = _appliances.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    if (!IsLayoutAppliance(entity)) continue;
                    var appliance = EntityManager.GetComponentData<CAppliance>(entity);
                    var position = EntityManager.GetComponentData<CPosition>(entity);
                    // Hidden/held appliances sit far outside the map; never query tiles there.
                    if (!bounds.Contains(position.Position) || GetTile(position.Position).RoomID == 0) continue;
                    var (x, y) = GridMath.ToTile(position.Position);
                    list.Add(new LiveAppliance
                    {
                        Entity = entity,
                        Info = new PlacedAppliance
                        {
                            Key = GridMath.Key(entity),
                            ApplianceId = appliance.ID,
                            X = x,
                            Y = y,
                            Rotation = GridMath.ToDegrees(position.Rotation),
                            Layer = appliance.Layer.ToString(),
                            Locked = Has<CImmovable>(entity),
                            State = ApplianceStateCodec.Capture(EntityManager, entity)
                        }
                    });
                }
            }
            return list;
        }

        /// <summary>Placed, player-owned appliances only: not held, not blueprints/letters, not chairs
        /// generated by tables, not temporary night-time appliances and not mid-creation.</summary>
        private bool IsLayoutAppliance(Entity entity)
        {
            return !Has<CHeldBy>(entity) && !Has<CHeldAppliance>(entity) && !Has<CCreateAppliance>(entity) &&
                   !Has<CApplianceBlueprint>(entity) && !Has<CLetter>(entity) &&
                   !Has<CApplianceGhostChair>(entity) && !Has<CApplianceChair>(entity) &&
                   !Has<CDestroyApplianceAtDay>(entity);
        }

        private LayoutDocument BuildDocument(List<LiveAppliance> live, bool includeFloor)
        {
            var doc = new LayoutDocument { Kind = LayoutKinds.Restaurant, Name = "Restaurant" };
            int n = 1;
            foreach (var a in live.OrderBy(a => a.Info.Y).ThenBy(a => a.Info.X))
            {
                doc.Appliances.Add(new ApplianceRecord
                {
                    Id = "a" + n++,
                    ApplianceId = a.Info.ApplianceId,
                    Type = ApplianceCatalog.TryGet(a.Info.ApplianceId, out var entry) ? entry.Code : "",
                    X = a.Info.X,
                    Y = a.Info.Y,
                    Rotation = a.Info.Rotation,
                    Layer = a.Info.Layer == Layers.Default ? null : a.Info.Layer,
                    State = a.Info.State
                });
            }
            if (includeFloor) AddFloorPlan(doc);
            return doc;
        }

        private Bounds PlayableBounds()
        {
            var bounds = Bounds;
            bounds.Expand(0.5f);
            return bounds;
        }

        private void AddFloorPlan(LayoutDocument doc)
        {
            var bounds = PlayableBounds();
            int minX = Mathf.CeilToInt(bounds.min.x), maxX = Mathf.FloorToInt(bounds.max.x);
            int minY = Mathf.CeilToInt(bounds.min.z), maxY = Mathf.FloorToInt(bounds.max.z);
            doc.Bounds = new GridRect(minX, minY, maxX, maxY);
            doc.Tiles = new List<FloorTile>();
            doc.Walls = new List<WallEdge>();
            var rules = new Rules(this);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var world = GridMath.ToWorld(x, y);
                    var tile = GetTile(world);
                    doc.Tiles.Add(new FloorTile
                    {
                        X = x, Y = y, Room = tile.RoomID, Type = tile.Type.ToString(),
                        Placeable = rules.CanPlace(x, y, out _)
                    });
                    if (tile.RoomID == 0) continue;
                    if (x < maxX && GetTile(GridMath.ToWorld(x + 1, y)).RoomID != 0 && !CanReach(world, GridMath.ToWorld(x + 1, y)))
                        doc.Walls.Add(new WallEdge { X = x, Y = y, Dir = "E" });
                    if (y < maxY && GetTile(GridMath.ToWorld(x, y + 1)).RoomID != 0 && !CanReach(world, GridMath.ToWorld(x, y + 1)))
                        doc.Walls.Add(new WallEdge { X = x, Y = y, Dir = "N" });
                }
            }
        }

        private sealed class Rules : IPlacementRules
        {
            private readonly LayoutEditorSystem _system;
            private readonly Bounds _bounds;
            private readonly Vector3 _door;
            private readonly Vector3 _doorOutside;

            public Rules(LayoutEditorSystem system)
            {
                _system = system;
                _bounds = system.PlayableBounds();
                _door = system.GetFrontDoor();
                _doorOutside = system.GetFrontDoor(true);
            }

            public bool CanPlace(int x, int y, out string reason)
            {
                var world = GridMath.ToWorld(x, y);
                if (!_bounds.Contains(world)) { reason = "outside the map"; return false; }
                if (_system.GetTile(world).RoomID == 0) { reason = "not inside the restaurant"; return false; }
                if (_door.IsSameTile(world) || _doorOutside.IsSameTile(world)) { reason = "front door tile"; return false; }
                reason = "";
                return true;
            }
        }

        // ----- Commands -----

        private void Export(int input, LayoutCommand command)
        {
            var live = Snapshot();
            LayoutDocument doc;
            if (command.Region != null)
            {
                doc = LayoutTransform.ExtractModule(BuildDocument(live, false), command.Region, command.Name ?? "Module");
            }
            else
            {
                doc = BuildDocument(live, true);
                if (!string.IsNullOrEmpty(command.Name)) doc.Name = command.Name!;
            }
            Respond(input, command, true, "Exported " + doc.Appliances.Count + " appliances", LayoutJson.Serialize(doc));
        }

        private void Apply(int input, LayoutCommand command)
        {
            var doc = LayoutJson.Parse(command.LayoutJson ?? "");
            List<ApplianceRecord> desired;
            GridRect? footprint = null;
            if (command.AsModule)
            {
                var source = doc.Bounds ?? BoundsOf(doc.Appliances);
                desired = LayoutTransform.Place(doc.Appliances, command.OriginX, command.OriginY, command.QuarterTurns, source);
                if (source != null)
                {
                    bool swap = ((command.QuarterTurns % 2) + 2) % 2 == 1;
                    int w = swap ? source.Height : source.Width, h = swap ? source.Width : source.Height;
                    footprint = new GridRect(command.OriginX, command.OriginY, command.OriginX + w - 1, command.OriginY + h - 1);
                }
            }
            else desired = doc.Appliances.Select(a => a.Clone()).ToList();

            foreach (var record in desired) ApplianceCatalog.Resolve(record);

            var options = new ApplyOptions
            {
                Clear = command.Clear,
                ClearRegion = command.Clear == ClearScope.Region ? (command.Region ?? footprint) : null,
                Conflicts = command.Conflicts,
                RestoreState = command.RestoreState
            };
            var live = Snapshot();
            var plan = ApplyPlanner.Plan(live.Select(l => l.Info).ToList(), desired, new Rules(this), options);
            if (plan.Spawns.Count == 0 && plan.Removals.Count == 0 && plan.Updates.Count == 0)
            {
                Respond(input, command, plan.Skipped.Count == 0, "Nothing to change. " + plan.Summary + SkipDetails(plan));
                return;
            }
            PushUndo(live);
            Execute(plan, live);
            Respond(input, command, true, "Applied: " + plan.Summary + SkipDetails(plan));
        }

        private static GridRect? BoundsOf(List<ApplianceRecord> records)
        {
            if (records.Count == 0) return null;
            return new GridRect(records.Min(r => r.X), records.Min(r => r.Y), records.Max(r => r.X), records.Max(r => r.Y));
        }

        private static string SkipDetails(LayoutPlan plan)
        {
            if (plan.Skipped.Count == 0) return "";
            var details = plan.Skipped.Take(4).Select(s =>
                (string.IsNullOrEmpty(s.Record.Type) ? "#" + s.Record.ApplianceId : s.Record.Type) + " at " + s.Record.X + "," + s.Record.Y + ": " + s.Reason);
            return "\nSkipped: " + string.Join("; ", details) + (plan.Skipped.Count > 4 ? "; ..." : "");
        }

        private void Execute(LayoutPlan plan, List<LiveAppliance> live)
        {
            var byKey = live.ToDictionary(l => l.Info.Key);
            foreach (var key in plan.Removals)
                if (byKey.TryGetValue(key, out var target)) RemoveAppliance(target.Entity);
            foreach (var update in plan.Updates)
            {
                if (!byKey.TryGetValue(update.Key, out var target)) continue;
                SetFacing(target.Entity, update.Rotation);
                ApplianceStateCodec.Restore(EntityManager, target.Entity, update.State);
            }
            foreach (var spawn in plan.Spawns) SpawnAppliance(spawn);
        }

        private void Delete(int input, LayoutCommand command)
        {
            if (command.Region == null) { Respond(input, command, false, "No region selected"); return; }
            var live = Snapshot();
            var victims = live.Where(l => !l.Info.Locked && command.Region.Contains(l.Info.X, l.Info.Y)).ToList();
            if (victims.Count == 0) { Respond(input, command, false, "No removable appliance in the selection"); return; }
            PushUndo(live);
            foreach (var v in victims) RemoveAppliance(v.Entity);
            Respond(input, command, true, "Removed " + victims.Count + " appliances");
        }

        private void RotateInPlace(int input, LayoutCommand command)
        {
            if (command.Region == null) { Respond(input, command, false, "No region selected"); return; }
            var live = Snapshot();
            var targets = live.Where(l => !l.Info.Locked && command.Region.Contains(l.Info.X, l.Info.Y)).ToList();
            if (targets.Count == 0) { Respond(input, command, false, "No rotatable appliance in the selection"); return; }
            PushUndo(live);
            int turns = command.QuarterTurns == 0 ? 1 : command.QuarterTurns;
            foreach (var t in targets) SetFacing(t.Entity, Rotations.Normalize(t.Info.Rotation + 90 * turns));
            Respond(input, command, true, "Rotated " + targets.Count + " appliances");
        }

        /// <summary>Moves entities (keeping held items and state) by an offset, optionally rotating the block.</summary>
        private void Move(int input, LayoutCommand command)
        {
            var region = command.Region;
            if (region == null) { Respond(input, command, false, "No region selected"); return; }
            var live = Snapshot();
            var moving = live.Where(l => region.Contains(l.Info.X, l.Info.Y)).ToList();
            if (moving.Count == 0) { Respond(input, command, false, "Nothing to move in the selection"); return; }
            var locked = moving.FirstOrDefault(m => m.Info.Locked);
            if (locked != null)
            {
                Respond(input, command, false, ApplianceCatalog.DisplayName(locked.Info.ApplianceId) + " at " + locked.Info.X + "," + locked.Info.Y + " cannot be moved");
                return;
            }

            var records = moving.Select((m, i) => new ApplianceRecord
            {
                Id = i.ToString(), ApplianceId = m.Info.ApplianceId, X = m.Info.X - region.MinX, Y = m.Info.Y - region.MinY,
                Rotation = m.Info.Rotation, Layer = m.Info.Layer
            }).ToList();
            var placed = LayoutTransform.Place(records, region.MinX + command.Dx, region.MinY + command.Dy, command.QuarterTurns,
                new GridRect(0, 0, region.Width - 1, region.Height - 1));

            var rules = new Rules(this);
            var movingKeys = new HashSet<long>(moving.Select(m => m.Info.Key));
            // Not Enumerable.ToHashSet: Unity's Mono class library may not have it.
            var occupied = new HashSet<(int, int, string)>(live.Where(l => !movingKeys.Contains(l.Info.Key))
                .Select(l => (l.Info.X, l.Info.Y, l.Info.Layer)));
            foreach (var p in placed)
            {
                if (!rules.CanPlace(p.X, p.Y, out var reason)) { Respond(input, command, false, "Cannot move to " + p.X + "," + p.Y + ": " + reason); return; }
                if (occupied.Contains((p.X, p.Y, p.LayerOrDefault))) { Respond(input, command, false, "Cannot move to " + p.X + "," + p.Y + ": occupied"); return; }
            }

            PushUndo(live);
            foreach (var m in moving)
            {
                var appliance = EntityManager.GetComponentData<CAppliance>(m.Entity);
                var position = EntityManager.GetComponentData<CPosition>(m.Entity);
                if (GetOccupant(position.Position, appliance.Layer) == m.Entity)
                    SetOccupant(position.Position, default(Entity), appliance.Layer);
            }
            foreach (var p in placed)
            {
                var entity = moving[int.Parse(p.Id)].Entity;
                var appliance = EntityManager.GetComponentData<CAppliance>(entity);
                var position = new CPosition(GridMath.ToWorld(p.X, p.Y));
                position.Rotation = GridMath.ToRotation(p.Rotation);
                EntityManager.SetComponentData(entity, position);
                SetOccupant(position.Position, entity, appliance.Layer);
                RefreshView(entity);
            }
            Respond(input, command, true, "Moved " + moving.Count + " appliances");
        }

        private void Undo(int input, LayoutCommand command)
        {
            if (_undo.Count == 0) { Respond(input, command, false, "Nothing to undo"); return; }
            var json = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            var doc = LayoutJson.Parse(json);
            foreach (var record in doc.Appliances) ApplianceCatalog.Resolve(record);
            var live = Snapshot();
            var plan = ApplyPlanner.Plan(live.Select(l => l.Info).ToList(), doc.Appliances, new Rules(this),
                new ApplyOptions { Clear = ClearScope.Everything, Conflicts = ConflictPolicy.Replace, RestoreState = true });
            Execute(plan, live);
            Respond(input, command, true, "Undone: " + plan.Summary + " (" + _undo.Count + " more undo steps)");
        }

        private void PushUndo(List<LiveAppliance> live)
        {
            _undo.Add(LayoutJson.Serialize(BuildDocument(live, false)));
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        // ----- Entity operations -----

        private void RemoveAppliance(Entity entity)
        {
            if (!EntityManager.Exists(entity)) return;
            if (Require(entity, out CItemHolder holder) && holder.HeldItem != default(Entity) && EntityManager.Exists(holder.HeldItem))
                EntityManager.DestroyEntity(holder.HeldItem);
            if (Require(entity, out CAppliance appliance) && Require(entity, out CPosition position) &&
                GetOccupant(position.Position, appliance.Layer) == entity)
                SetOccupant(position.Position, default(Entity), appliance.Layer);
            EntityManager.DestroyEntity(entity);
        }

        private void SpawnAppliance(ApplianceRecord record)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new CCreateAppliance { ID = record.ApplianceId });
            var position = new CPosition(GridMath.ToWorld(record.X, record.Y));
            position.Rotation = GridMath.ToRotation(record.Rotation);
            EntityManager.AddComponentData(entity, position);
            _pending.Add(new PendingSpawn { Entity = entity, Rotation = record.Rotation, State = record.State });
        }

        /// <summary>
        /// The game's appliance creation turns the CCreateAppliance request into a full appliance on a
        /// later frame. Once that happened, register the tile occupant and restore the saved state
        /// (which must happen after the default component values were written).
        /// </summary>
        private void FinishPendingSpawns()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                if (!EntityManager.Exists(pending.Entity)) { _pending.RemoveAt(i); continue; }
                if (!Has<CAppliance>(pending.Entity) || Has<CCreateAppliance>(pending.Entity))
                {
                    pending.Frames++;
                    if (pending.Frames > PendingTimeoutFrames)
                    {
                        Main.LogWarning("Appliance creation did not complete for entity " + pending.Entity.Index);
                        _pending.RemoveAt(i);
                    }
                    else _pending[i] = pending;
                    continue;
                }
                var appliance = EntityManager.GetComponentData<CAppliance>(pending.Entity);
                var position = EntityManager.GetComponentData<CPosition>(pending.Entity);
                if (GridMath.ToDegrees(position.Rotation) != pending.Rotation)
                {
                    position.Rotation = GridMath.ToRotation(pending.Rotation);
                    EntityManager.SetComponentData(pending.Entity, position);
                    RefreshView(pending.Entity);
                }
                if (GetOccupant(position.Position, appliance.Layer) != pending.Entity)
                    SetOccupant(position.Position, pending.Entity, appliance.Layer);
                ApplianceStateCodec.Restore(EntityManager, pending.Entity, pending.State);
                _pending.RemoveAt(i);
            }
        }

        private void SetFacing(Entity entity, int degrees)
        {
            if (!Require(entity, out CPosition position)) return;
            position.Rotation = GridMath.ToRotation(degrees);
            EntityManager.SetComponentData(entity, position);
            RefreshView(entity);
        }

        /// <summary>Recreates the appliance view so it picks up the new transform (same approach as
        /// the drag-and-drop designer mod uses when dropping an appliance).</summary>
        private void RefreshView(Entity entity)
        {
            if (!Has<CRemoveView>(entity)) EntityManager.AddComponent<CRemoveView>(entity);
            if (Require(entity, out CRequiresView view)) EntityManager.SetComponentData(entity, view);
            else EntityManager.AddComponentData(entity, new CRequiresView { Type = ViewType.Appliance });
        }

        // ----- Results -----

        private static void Respond(int input, LayoutCommand command, bool ok, string message, string? layoutJson = null)
        {
            if (ok) Main.LogInfo(message); else Main.LogWarning(message);
            Results.Add(new LayoutResult
            {
                Sequence = ++_resultSequence,
                TargetInput = input,
                RequestId = command.RequestId,
                Ok = ok,
                Message = message,
                LayoutJson = layoutJson
            });
            while (Results.Count > 3) Results.RemoveAt(0);
            // Only the newest result carries a layout, to keep the broadcast view data small.
            for (int i = 0; i < Results.Count - 1; i++) Results[i].LayoutJson = null;
        }
    }
}
