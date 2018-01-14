using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

static public class NativeArrayExtensions
{
    public static bool Contains<T>(this NativeArray<T> array, T value) where T : struct, IEquatable<T>
    {
        return IndexOf(array, value) != -1;
    }

    public unsafe static int IndexOf<T>(this NativeArray<T> array, T value) where T : struct, IEquatable<T>
    {
        return IndexOf(array.GetUnsafePtr(), array.Length, value);
    }

    static unsafe int IndexOf<T>(void* ptr, int size, T value) where T : struct, IEquatable<T>
    {
        for (int i = 0; i != size; i++)
        {
            if (value.Equals(UnsafeUtility.ReadArrayElement<T>(ptr, i)))
                return i;
        }
        return -1;
    }
}