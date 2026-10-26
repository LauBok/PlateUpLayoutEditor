using Unity.Mathematics;
using UnityEngine;

namespace KitchenLayoutEditor
{
    /// <summary>Conversions between layout grid coordinates and PlateUp world space.</summary>
    internal static class GridMath
    {
        public static (int x, int y) ToTile(Vector3 world) => (Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));

        public static Vector3 ToWorld(int x, int y) => new Vector3(x, 0f, y);

        /// <summary>Facing in degrees clockwise from +Z, snapped to a quarter turn.</summary>
        public static int ToDegrees(quaternion rotation)
        {
            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float degrees = math.degrees(math.atan2(forward.x, forward.z));
            int snapped = Mathf.RoundToInt(degrees / 90f) * 90;
            return ((snapped % 360) + 360) % 360;
        }

        public static quaternion ToRotation(int degrees) => quaternion.RotateY(math.radians(degrees));

        public static long Key(Unity.Entities.Entity entity) => ((long)entity.Index << 32) | (uint)entity.Version;
    }
}
