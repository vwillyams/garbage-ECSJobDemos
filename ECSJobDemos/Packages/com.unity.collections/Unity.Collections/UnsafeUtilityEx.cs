#if CSHARP_7_OR_LATER
namespace Unity.Collections.LowLevel.Unsafe
{
    unsafe public static class UnsafeUtilityEx
    {
        public static ref T AsRef<T>(void* ptr) where T : struct
        {
            return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>(ptr);
        }
    
        public static ref T ArrayElementAsRef<T>(void* ptr, int index) where T : struct
        {
            return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>((byte*)ptr + index * UnsafeUtility.SizeOf<T>());
        }
    }
}
#endif