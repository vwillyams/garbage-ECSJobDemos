using System.Reflection;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    struct InjectionData
    {
        public int                 fieldOffset;
        public ComponentType       componentType;
        public int                 indexInComponentGroup;
        public bool                isReadOnly;

        public InjectionData(FieldInfo field, Type genericType, bool isReadOnly)
        {
            this.indexInComponentGroup = -1;
            this.fieldOffset = UnsafeUtility.GetFieldOffset(field);

            var accessMode = isReadOnly ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite;
            this.componentType = new ComponentType(genericType, accessMode);
            this.isReadOnly = isReadOnly;
        }
    }
}
