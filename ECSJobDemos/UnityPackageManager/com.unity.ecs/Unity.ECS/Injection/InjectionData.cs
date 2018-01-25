using System.Reflection;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    interface IUpdateInjection
    {
        unsafe void UpdateInjection(byte* targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection);
    }

    struct InjectionData
    {
        public int                 fieldOffset;
        public Type                genericType;
        public bool                isReadOnly;

        internal IUpdateInjection  injection;

        public InjectionData(FieldInfo field, Type containerType, Type genericType, bool isReadOnly)
        {
            this.fieldOffset = UnsafeUtility.GetFieldOffset(field);
            this.genericType = genericType;
            this.isReadOnly = isReadOnly;
            this.injection = null;
        }
    }
}
