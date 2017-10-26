using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static partial class math
    {
        public static int count_bits(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        // Packs components with an enabled mask (LSB) to the left
        // The value of components after the last packed component are undefined.
        // Returns the number of enabled mask bits. (0 ... 4)
        public static unsafe int compress(int* output, int index, int4 val, int4 mask)
        {
            int4 outputValue = new int4(0);
            if (mask.x < 0)
                output[index++] = val.x;
            if (mask.y < 0)
                output[index++] = val.y;
            if (mask.z < 0)
                output[index++] = val.z;
            if (mask.w < 0)
                output[index++] = val.w;

            return index;
        }

        // radians (convert from degrees to radians)
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float  radians(float  degrees)     { return degrees * 0.0174532925f; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float2 radians(float2 degrees)     { return degrees * 0.0174532925f; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float3 radians(float3 degrees)     { return degrees * 0.0174532925f; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float4 radians(float4 degrees)     { return degrees * 0.0174532925f; }

        // radians (convert from radians to degrees)
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float  degrees(float  radians)     { return radians * 57.295779513f; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float2 degrees(float2 radians)     { return radians * 57.295779513f; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float3 degrees(float3 radians)     { return radians * 57.295779513f; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float4 degrees(float4 radians)     { return radians * 57.295779513f; }


        // cmin - returns the smallest component of the vector
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmin(float  a)           { return a; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmin(float2 a)           { return min(a.x, a.y); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmin(float3 a)           { return min(min(a.x, a.y), a.z); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmin(float4 a)           { return min(min(min(a.x, a.y), a.z), a.w); }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmin(int    a)           { return a; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmin(int2   a)           { return min(a.x, a.y); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmin(int3   a)           { return min(min(a.x, a.y), a.z); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmin(int4   a)           { return min(min(min(a.x, a.y), a.z), a.w); }

        // cmax - returns the largest component of the vector
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmax(float  a)           { return a; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmax(float2 a)           { return max(a.x, a.y); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmax(float3 a)           { return max(max(a.x, a.y), a.z); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     cmax(float4 a)           { return max(max(max(a.x, a.y), a.z), a.w); }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmax(int    a)           { return a; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmax(int2   a)           { return max(a.x, a.y); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmax(int3   a)           { return max(max(a.x, a.y), a.z); }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       cmax(int4   a)           { return max(max(max(a.x, a.y), a.z), a.w); }

        // csum - sums all components of the vector
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     csum(float  a)           { return a; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     csum(float2 a)           { return a.x + a.y; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     csum(float3 a)           { return a.x + a.y + a.z; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float     csum(float4 a)           { return a.x + a.y + a.z + a.w; }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       csum(int    a)           { return a; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       csum(int2   a)           { return a.x + a.y; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       csum(int3   a)           { return a.x + a.y + a.z; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int       csum(int4   a)           { return a.x + a.y + a.z + a.w; }

        // A numeric optimization fence.
        // prevents the compiler from optimizing operators.
        // Some algorithms are written in specific ways to get more precision.
        // For example: https://en.wikipedia.org/wiki/Kahan_summation_algorithm
        // this gives the programmer a tool to prevent specific optimization.
        // example:
        // var c = math.nfence(a + b) * c;
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float  nfence(float value)          { return value; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float2 nfence(float2 value)         { return value; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float3 nfence(float3 value)         { return value; }
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static float4 nfence(float4 value)         { return value; }
    }
}