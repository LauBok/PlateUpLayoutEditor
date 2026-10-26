using System;
using System.Collections.Generic;
using System.Linq;

namespace PlateUpLayoutEditor.Core
{
    public static class LayoutTransform
    {
        /// <summary>
        /// Rotates a set of appliances clockwise (seen from above) by quarterTurns * 90 degrees around
        /// the minimum corner of their bounding box, then moves that corner to (originX, originY).
        /// The rotated footprint is re-normalised so the result's minimum corner is exactly the origin.
        /// </summary>
        /// <param name="footprint">Optional module bounds; when given, empty margins inside it are kept.</param>
        public static List<ApplianceRecord> Place(IEnumerable<ApplianceRecord> source, int originX, int originY, int quarterTurns, GridRect? footprint = null)
        {
            var items = source.Select(a => a.Clone()).ToList();
            if (items.Count == 0) return items;
            int turns = ((quarterTurns % 4) + 4) % 4;
            foreach (var a in items)
            {
                var (x, y) = RotatePoint(a.X, a.Y, turns);
                a.X = x; a.Y = y;
                a.Rotation = Rotations.Normalize(a.Rotation + 90 * turns);
            }
            int minX = items.Min(a => a.X), minY = items.Min(a => a.Y);
            if (footprint != null)
            {
                var c1 = RotatePoint(footprint.MinX, footprint.MinY, turns);
                var c2 = RotatePoint(footprint.MaxX, footprint.MaxY, turns);
                minX = Math.Min(c1.x, c2.x);
                minY = Math.Min(c1.y, c2.y);
            }
            foreach (var a in items)
            {
                a.X = a.X - minX + originX;
                a.Y = a.Y - minY + originY;
            }
            return items;
        }

        /// <summary>Clockwise rotation about the origin in a y-up grid: (x, y) -> (y, -x).</summary>
        public static (int x, int y) RotatePoint(int x, int y, int quarterTurns)
        {
            int turns = ((quarterTurns % 4) + 4) % 4;
            for (int i = 0; i < turns; i++) { var t = x; x = y; y = -t; }
            return (x, y);
        }

        /// <summary>Copies the appliances inside a rectangle into a module whose minimum corner is (0, 0).</summary>
        public static LayoutDocument ExtractModule(LayoutDocument source, GridRect region, string name)
        {
            var picked = source.Appliances.Where(a => region.Contains(a.X, a.Y)).Select(a => a.Clone()).ToList();
            var module = new LayoutDocument { Kind = LayoutKinds.Module, Name = name ?? "" };
            foreach (var a in picked)
            {
                a.X -= region.MinX;
                a.Y -= region.MinY;
            }
            module.Appliances = picked;
            module.Bounds = new GridRect(0, 0, region.Width - 1, region.Height - 1);
            return module;
        }

        /// <summary>Re-numbers record IDs so pasted copies never collide with existing ones.</summary>
        public static void AssignFreshIds(IList<ApplianceRecord> records, ISet<string> taken, string prefix = "a")
        {
            int n = 1;
            foreach (var a in records)
            {
                while (taken.Contains(prefix + n)) n++;
                a.Id = prefix + n;
                taken.Add(a.Id);
            }
        }
    }
}
