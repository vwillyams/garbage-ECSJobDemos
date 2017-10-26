namespace UnityEngine
{
    public struct float4x4
    {
        public float4 m0;
        public float4 m1;
        public float4 m2;
        public float4 m3;

        public float4x4(float4 m0, float4 m1, float4 m2, float4 m3)
        {
            this.m0 = m0;
            this.m1 = m1;
            this.m2 = m2;
            this.m3 = m3;
        }
    }

    public struct float2x2
    {
        public float2 m0;
        public float2 m1;

        public float2x2(float2 m0, float2 m1)
        {
            this.m0 = m0;
            this.m1 = m1;
        }
    }

    public struct float3x3
    {
        public float3 m0;
        public float3 m1;
        public float3 m2;

        public float3x3(float3 m0, float3 m1, float3 m2)
        {
            this.m0 = m0;
            this.m1 = m1;
            this.m2 = m2;
        }
    }

    partial class math
    {
        public static float4 mul(float4x4 x, float4 v)
        {
            return mad(x.m0, v.x, x.m1 * v.y) + mad(x.m2, v.z, x.m3 * v.w);
        }

        public static float4x4 mul(float4x4 a, float4x4 b)
        {
            return new float4x4(mul(a,b.m0), mul(a,b.m1), mul(a,b.m2), mul(a,b.m3));
        }

        public static float2 mul(float2x2 x, float2 v)
        {
            return mad(x.m0, v.x, x.m1 * v.y);
        }

        public static float2x2 mul(float2x2 a, float2x2 b)
        {
            return new float2x2(mul(a, b.m0), mul(a, b.m1));
        }

        public static float3 mul(float3x3 x, float3 v)
        {
            return mad(x.m2, v.z, mad(x.m0, v.x, x.m1 * v.y));
        }

        public static float3x3 mul(float3x3 a, float3x3 b)
        {
            return new float3x3(mul(a,b.m0), mul(a,b.m1), mul(a,b.m2));
        }

        static public float3x3 transpose(float3x3 x)
        {
            return new float3x3(
                new float3(x.m0.x, x.m1.x, x.m2.x),
                new float3(x.m0.y, x.m1.y, x.m2.y),
                new float3(x.m0.z, x.m1.z, x.m2.z));
        }
    }
}