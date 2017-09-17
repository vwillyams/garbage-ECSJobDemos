using System;

#pragma warning disable 0660, 0661

namespace UnityEngine
{
    public partial struct float3
    {
        public static implicit operator Vector3(float3 d) { return new Vector3(d.x, d.y, d.z); }
        public static implicit operator float3(Vector3 d) { return new float3(d.x, d.y, d.z); }
    }

    public partial struct float4
    {
        public static implicit operator Quaternion(float4 d) { return new Quaternion(d.x, d.y, d.z, d.w); }
        public static implicit operator float4(Quaternion d) { return new float4(d.x, d.y, d.z, d.w); }
    }}

