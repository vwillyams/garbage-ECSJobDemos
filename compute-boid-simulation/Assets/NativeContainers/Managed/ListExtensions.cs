using System;
using System.Collections.Generic;

namespace UnityEngine.Collections
{
    public static class ListExtensions
    {
        public static void RemoveAtSwapBack<T>(this List<T> list, int index)
        {
            int lastIndex = list.Count - 1;
            list[index] = list[lastIndex];
            list.RemoveAt(lastIndex);
        }
    }
}
