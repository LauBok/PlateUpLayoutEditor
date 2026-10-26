using System.Collections.Generic;
using System.Linq;
using PlateUpLayoutEditor.Core;
using Xunit;

namespace LayoutCore.Tests
{
    public class CoreTests
    {
        private sealed class AllowAll : IPlacementRules
        {
            public HashSet<(int, int)> Blocked { get; } = new HashSet<(int, int)>();
            public bool CanPlace(int x, int y, out string reason)
            {
                reason = Blocked.Contains((x, y)) ? "blocked" : "";
                return !Blocked.Contains((x, y));
            }
        }

        private static ApplianceRecord R(string id, int appliance, int x, int y, int rot = 0) =>
            new ApplianceRecord { Id = id, ApplianceId = appliance, Type = "T" + appliance, X = x, Y = y, Rotation = rot };

        private static PlacedAppliance P(long key, int appliance, int x, int y, int rot = 0, bool locked = false) =>
            new PlacedAppliance { Key = key, ApplianceId = appliance, X = x, Y = y, Rotation = rot, Locked = locked };

        [Fact]
        public void RoundTripsJson()
        {
            var doc = new LayoutDocument { Name = "x", Appliances = { R("a1", 5, 1, 2, 90) } };
            doc.Appliances[0].State = new ApplianceState { PushDirection = "Left", SpecificType = 7, SpecificComponents = new List<int> { 1, 2 } };
            var parsed = LayoutJson.Parse(LayoutJson.Serialize(doc));
            Assert.Equal(5, parsed.Appliances[0].ApplianceId);
            Assert.Equal(90, parsed.Appliances[0].Rotation);
            Assert.Equal("Left", parsed.Appliances[0].State!.PushDirection);
            Assert.Equal(new[] { 1, 2 }, parsed.Appliances[0].State!.SpecificComponents);
        }

        [Fact]
        public void ParsesVersion1PrototypeFiles()
        {
            var json = "{\"version\":1,\"name\":\"old\",\"origin\":{\"x\":0,\"y\":0},\"appliances\":[{\"id\":\"1\",\"type\":\"Counter\",\"x\":2,\"y\":3,\"rotation\":270,\"state\":{}}]}";
            var doc = LayoutJson.Parse(json);
            Assert.Equal(2, doc.Version);
            Assert.Equal("Counter", doc.Appliances[0].Type);
            Assert.Equal(0, doc.Appliances[0].ApplianceId);
        }

        [Fact]
        public void ReadsLegacyGrabDirectionAsPushDirection()
        {
            var json = "{\"version\":2,\"appliances\":[{\"id\":\"a\",\"applianceId\":1,\"x\":0,\"y\":0,\"rotation\":0,\"state\":{\"grabDirection\":\"Left\"}}]}";
            var doc = LayoutJson.Parse(json);
            Assert.Equal("Left", doc.Appliances[0].State!.PushDirection);
            Assert.Contains("\"pushDirection\": \"Left\"", LayoutJson.Serialize(doc));
            Assert.DoesNotContain("grabDirection", LayoutJson.Serialize(doc));
        }

        [Fact]
        public void RejectsOverlaps()
        {
            var json = "{\"version\":2,\"appliances\":[{\"id\":\"a\",\"applianceId\":1,\"x\":0,\"y\":0,\"rotation\":0},{\"id\":\"b\",\"applianceId\":2,\"x\":0,\"y\":0,\"rotation\":0}]}";
            Assert.Throws<LayoutFormatException>(() => LayoutJson.Parse(json));
        }

        [Fact]
        public void AllowsSameTileOnDifferentLayers()
        {
            var json = "{\"version\":2,\"appliances\":[{\"id\":\"a\",\"applianceId\":1,\"x\":0,\"y\":0,\"rotation\":0},{\"id\":\"b\",\"applianceId\":2,\"x\":0,\"y\":0,\"rotation\":0,\"layer\":\"Floor\"}]}";
            Assert.Equal(2, LayoutJson.Parse(json).Appliances.Count);
        }

        [Fact]
        public void RotatesClockwise()
        {
            // A two-tile line pointing east, rotated once clockwise, points south.
            var placed = LayoutTransform.Place(new[] { R("a", 1, 0, 0, 90), R("b", 1, 1, 0, 90) }, 10, 20, 1);
            var a = placed.Single(p => p.Id == "a");
            var b = placed.Single(p => p.Id == "b");
            Assert.Equal((10, 21), (a.X, a.Y));
            Assert.Equal((10, 20), (b.X, b.Y));
            Assert.Equal(180, a.Rotation);
        }

        [Fact]
        public void FourRotationsAreIdentity()
        {
            var src = new[] { R("a", 1, 0, 0, 0), R("b", 2, 2, 1, 270), R("c", 3, 1, 3, 90) };
            var once = src.AsEnumerable();
            for (int i = 0; i < 4; i++) once = LayoutTransform.Place(once, 0, 0, 1);
            foreach (var s in src)
            {
                var r = once.Single(p => p.Id == s.Id);
                Assert.Equal((s.X, s.Y, s.Rotation), (r.X, r.Y, r.Rotation));
            }
        }

        [Fact]
        public void FootprintKeepsEmptyMargin()
        {
            // Module 3 wide x 1 tall with a single appliance at its east end.
            var placed = LayoutTransform.Place(new[] { R("a", 1, 2, 0) }, 0, 0, 2, new GridRect(0, 0, 2, 0));
            Assert.Equal((0, 0), (placed[0].X, placed[0].Y));
            var noTurn = LayoutTransform.Place(new[] { R("a", 1, 2, 0) }, 5, 5, 0, new GridRect(0, 0, 2, 0));
            Assert.Equal((7, 5), (noTurn[0].X, noTurn[0].Y));
        }

        [Fact]
        public void ExtractsModuleRelativeToRegion()
        {
            var doc = new LayoutDocument { Appliances = { R("a", 1, 3, 4), R("b", 2, 5, 6), R("c", 3, 9, 9) } };
            var module = LayoutTransform.ExtractModule(doc, new GridRect(3, 4, 5, 6), "m");
            Assert.Equal(LayoutKinds.Module, module.Kind);
            Assert.Equal(2, module.Appliances.Count);
            Assert.Contains(module.Appliances, a => a.Id == "a" && a.X == 0 && a.Y == 0);
            Assert.Contains(module.Appliances, a => a.Id == "b" && a.X == 2 && a.Y == 2);
        }

        [Fact]
        public void FullReplaceKeepsMatchingAndRemovesOthers()
        {
            var current = new[] { P(1, 10, 0, 0), P(2, 11, 1, 0), P(3, 99, 5, 5, locked: true) };
            var desired = new[] { R("a", 10, 0, 0, 90), R("b", 12, 2, 0) };
            var plan = ApplyPlanner.Plan(current, desired, new AllowAll(), new ApplyOptions { Clear = ClearScope.Everything });
            Assert.Equal(new long[] { 2 }, plan.Removals);
            Assert.Single(plan.Updates);
            Assert.Equal(90, plan.Updates[0].Rotation);
            Assert.Single(plan.Spawns);
            Assert.Equal(12, plan.Spawns[0].ApplianceId);
        }

        [Fact]
        public void MergeSkipPolicyLeavesOccupiedTiles()
        {
            var current = new[] { P(1, 10, 0, 0) };
            var desired = new[] { R("a", 11, 0, 0), R("b", 11, 1, 0) };
            var plan = ApplyPlanner.Plan(current, desired, new AllowAll(), new ApplyOptions { Conflicts = ConflictPolicy.Skip });
            Assert.Empty(plan.Removals);
            Assert.Single(plan.Spawns);
            Assert.Single(plan.Skipped);
        }

        [Fact]
        public void MergeReplacePolicyReplacesButNeverLocked()
        {
            var current = new[] { P(1, 10, 0, 0), P(2, 10, 1, 0, locked: true) };
            var desired = new[] { R("a", 11, 0, 0), R("b", 11, 1, 0) };
            var plan = ApplyPlanner.Plan(current, desired, new AllowAll(), new ApplyOptions { Conflicts = ConflictPolicy.Replace });
            Assert.Equal(new long[] { 1 }, plan.Removals);
            Assert.Single(plan.Spawns);
            Assert.Equal("tile holds a locked appliance", plan.Skipped.Single().Reason);
        }

        [Fact]
        public void RulesAndUnknownAppliancesAreSkipped()
        {
            var rules = new AllowAll();
            rules.Blocked.Add((3, 3));
            var desired = new[] { R("a", 11, 3, 3), R("b", 0, 1, 1) };
            var plan = ApplyPlanner.Plan(new PlacedAppliance[0], desired, rules, new ApplyOptions());
            Assert.Empty(plan.Spawns);
            Assert.Equal(2, plan.Skipped.Count);
        }

        [Fact]
        public void RegionClearOnlyTouchesRegion()
        {
            var current = new[] { P(1, 10, 0, 0), P(2, 10, 8, 8) };
            var plan = ApplyPlanner.Plan(current, new ApplianceRecord[0], new AllowAll(),
                new ApplyOptions { Clear = ClearScope.Region, ClearRegion = new GridRect(0, 0, 2, 2) });
            Assert.Equal(new long[] { 1 }, plan.Removals);
        }

        [Fact]
        public void StateChangeProducesUpdate()
        {
            var current = new[] { new PlacedAppliance { Key = 1, ApplianceId = 10, State = new ApplianceState { SpecificType = 1 } } };
            var desired = new[] { new ApplianceRecord { Id = "a", ApplianceId = 10, State = new ApplianceState { SpecificType = 2 } } };
            var plan = ApplyPlanner.Plan(current, desired, new AllowAll(), new ApplyOptions());
            Assert.Equal(2, plan.Updates.Single().State!.SpecificType);
            var noState = ApplyPlanner.Plan(current, desired, new AllowAll(), new ApplyOptions { RestoreState = false });
            Assert.Equal(1, noState.Unchanged);
        }
    }
}
