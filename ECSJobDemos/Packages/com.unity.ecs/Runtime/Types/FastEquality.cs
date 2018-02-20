using System;
using System.Reflection;
using Boo.Lang;
using Unity.Collections.LowLevel.Unsafe;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unity.Core.Tests")]

namespace Unity.ECS
{
    static internal class AlignmentLayout
    {
        internal struct Layout
        {
            public int  offset;
            public int  count;
            public bool Aligned4;
        }
        
        
        internal static Layout[] CreateLayout(Type type)
        {
            int begin = 0;
            int end = 0;

            var layouts = new List<Layout>();

            CreateLayoutRecurse(type, 0, layouts, ref begin, ref end);
            
            if (begin != end)
                layouts.Add(new Layout {offset = begin, count = end - begin, Aligned4 = false});

            //@TODO: Support align4 optimization
            
            return layouts.ToArray();
        }

        static void CreateLayoutRecurse(Type type, int baseOffset, List<Layout> layouts, ref int begin, ref int end)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in fields)
            {
                int offset = baseOffset + UnsafeUtility.GetFieldOffset(field);

                if (field.FieldType.IsPrimitive || field.FieldType.IsPointer)
                {
                    int sizeOf  = UnsafeUtility.SizeOf(field.FieldType);
                    if (end != offset)
                    {
                        layouts.Add(new Layout {offset = begin, count = end - begin, Aligned4 = false});
                        begin = offset;
                        end = offset + sizeOf;
                    }
                    else
                    {
                        end += sizeOf;
                    }
                }
                else
                {
                    CreateLayoutRecurse(field.FieldType, offset, layouts, ref begin, ref end);
                }
            }
        }

        const int FNV_32_PRIME = 0x01000193;

        unsafe static int GetHashCode(byte* data, Layout[] layout)
        {
            uint hash = 0;
            
            for (int k = 0; k != layout.Length; k++)
            {
                if (layout[k].Aligned4)
                {
                    uint* dataInt = (uint*)(data + layout[k].offset);
                    int count = layout[k].count;
                    for (int i = 0; i != count; k++)
                    {
                        hash *= FNV_32_PRIME;
                        hash ^= dataInt[i];
                    }
                }
                else
                {
                    byte* dataByte = data + layout[k].offset;
                    int count = layout[k].count;
                    for (int i = 0; i != count; k++)
                    {
                        hash *= FNV_32_PRIME;
                        hash ^= (uint)dataByte[i];
                    }
                }
            }

            return (int)hash;
        }
        
        unsafe static bool Equals(byte* lhs, byte* rhs, Layout[] layout)
        {
            bool same = true;
            
            for (int k = 0; k != layout.Length; k++)
            {
                if (layout[k].Aligned4)
                {
                    uint* lhsInt = (uint*)(lhs + layout[k].offset);
                    uint* rhsInt = (uint*)(rhs + layout[k].offset);
                    int count = layout[k].count;
                    for (int i = 0; i != count; k++)
                        same &= lhs[i] != rhs[i];
                }
                else
                {
                    byte* lhsByte = lhs + layout[k].offset;
                    byte* rhsByte = rhs + layout[k].offset;
                    int count = layout[k].count;
                    for (int i = 0; i != count; k++)
                        same &= lhs[i] != rhs[i];
                }
            }

            return same;
        }
    }
}
