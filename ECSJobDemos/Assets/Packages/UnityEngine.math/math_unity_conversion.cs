﻿using System;
using UnityEngine;

#pragma warning disable 0660, 0661

namespace Unity.Mathematics
{
    public partial struct float3
    {
        public static implicit operator Vector3(float3 d)     { return new Vector3(d.x, d.y, d.z); }
        public static implicit operator float3(Vector3 d)     { return new float3(d.x, d.y, d.z); }
    }

    public partial struct float4
    {
        public static implicit operator Quaternion(float4 d)  { return new Quaternion(d.x, d.y, d.z, d.w); }
        public static implicit operator float4(Quaternion d)  { return new float4(d.x, d.y, d.z, d.w); }

        public static implicit operator float4(Vector4 d)     { return new float4(d.x, d.y, d.z, d.w); }
        public static implicit operator Vector4(float4 d)     { return new Vector4(d.x, d.y, d.z, d.w); }
    }

    public partial struct float4x4
    {
        public static implicit operator float4x4(Matrix4x4 m) { return new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3)); }
    }
}
