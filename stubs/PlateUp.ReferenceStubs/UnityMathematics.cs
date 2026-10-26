// Unity.Mathematics subset (public Unity package; real signatures).
namespace Unity.Mathematics
{
    public struct float3
    {
        public float x, y, z;
        public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static implicit operator float3(UnityEngine.Vector3 v) => new float3(v.x, v.y, v.z);
        public static implicit operator UnityEngine.Vector3(float3 v) => new UnityEngine.Vector3(v.x, v.y, v.z);
    }

    public struct quaternion
    {
        public static readonly quaternion identity = default;
        public static quaternion RotateY(float angle) => default;
        public static implicit operator quaternion(UnityEngine.Quaternion q) => default;
        public static implicit operator UnityEngine.Quaternion(quaternion q) => default;
    }

    public static class math
    {
        public static float3 mul(quaternion q, float3 v) => v;
        public static quaternion mul(quaternion a, quaternion b) => a;
        public static float atan2(float y, float x) => (float)System.Math.Atan2(y, x);
        public static float degrees(float x) => x * 57.29578f;
        public static float radians(float x) => x * 0.01745329f;
    }
}
