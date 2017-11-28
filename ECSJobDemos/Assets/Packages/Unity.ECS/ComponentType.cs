using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace UnityEngine.ECS
{
    public struct SubtractiveComponent<T> where T : struct, IComponentData
    {}

    public struct ComponentType
    {
        public enum AccessMode
        {
            ReadWrite,
            ReadOnly,
            Subtractive
        }
        public static ComponentType Create<T>()
        {
            ComponentType type;
            type.typeIndex = TypeManager.GetTypeIndex<T>();
            type.sharedComponentIndex = -1;
            type.readOnly = 0;
            type.subtractive = 0;
            type.FixedArrayLength = -1;
            return type;
        }

        public static ComponentType ReadOnly(Type type)
        {
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex(type);
            t.sharedComponentIndex = -1;
            t.readOnly = 1;
            t.subtractive = 0;
            t.FixedArrayLength = -1;
            return t;
        }
        public static ComponentType ReadOnly<T>()
        {
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex<T>();
            t.sharedComponentIndex = -1;
            t.readOnly = 1;
            t.subtractive = 0;
            t.FixedArrayLength = -1;
            return t;
        }

        public static ComponentType Subtractive(Type type)
        {
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex(type);
            t.sharedComponentIndex = -1;
            t.readOnly = 0;
            t.subtractive = 1;
            t.FixedArrayLength = -1;
            return t;
        }
        public static ComponentType Subtractive<T>()
        {
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex<T>();
            t.sharedComponentIndex = -1;
            t.readOnly = 0;
            t.subtractive = 1;
            t.FixedArrayLength = -1;
            return t;
        }

        public ComponentType(Type type, AccessMode accessMode = AccessMode.ReadWrite)
        {
            typeIndex = TypeManager.GetTypeIndex(type);
            sharedComponentIndex = -1;
            readOnly = (accessMode == AccessMode.ReadOnly) ? 1 : 0;
            subtractive = (accessMode == AccessMode.Subtractive) ? 1 : 0;
            FixedArrayLength = -1;
        }
        
        public static ComponentType FixedArray(Type type, int numElements)
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (numElements < 0)
                throw new System.ArgumentException("FixedArray length must be 0 or larger");
            #endif
            
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex(type);
            t.sharedComponentIndex = -1;
            t.readOnly = 0;
            t.subtractive = 0;
            t.FixedArrayLength = numElements;
            return t;
        }

        internal bool RequiresJobDependency
        {
            get
            {
                var type = GetManagedType();
                //@TODO: This is wrong... Not right for fixed array, think about Entity array?
                return typeof(IComponentData).IsAssignableFrom(type);
            }
        }

        public Type GetManagedType()
        {
            return TypeManager.GetType(typeIndex);
        }

        public static implicit operator ComponentType(Type type)
        {
            return new ComponentType(type, ComponentType.AccessMode.ReadWrite);
        }

        static public bool operator <(ComponentType lhs, ComponentType rhs)
        {
            if (lhs.typeIndex == rhs.typeIndex)
                return lhs.sharedComponentIndex != rhs.sharedComponentIndex ? lhs.sharedComponentIndex < rhs.sharedComponentIndex : lhs.readOnly < rhs.readOnly;
            else
                return lhs.typeIndex < rhs.typeIndex;

        }
        static public bool operator >(ComponentType lhs, ComponentType rhs)
        {
            return rhs < lhs;
        }

        static public bool operator ==(ComponentType lhs, ComponentType rhs)
        {
            return lhs.typeIndex == rhs.typeIndex && lhs.sharedComponentIndex == rhs.sharedComponentIndex && lhs.readOnly == rhs.readOnly;
        }
        static public bool operator !=(ComponentType lhs, ComponentType rhs)
        {
            return lhs.typeIndex != rhs.typeIndex || lhs.sharedComponentIndex != rhs.sharedComponentIndex && lhs.readOnly == rhs.readOnly;
        }

        unsafe static internal bool CompareArray(ComponentType* type1, int typeCount1, ComponentType* type2, int typeCount2)
        {
            if (typeCount1 != typeCount2)
                return false;
            for (int i = 0; i < typeCount1; ++i)
            {
                if (type1[i] != type2[i])
                    return false;
            }
            return true;
        }

        public bool IsFixedArray { get { return FixedArrayLength != -1; } }

        public int typeIndex;
        public int readOnly;
        public int subtractive;
        public int sharedComponentIndex;
        public int FixedArrayLength;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public override string ToString()
        {
            if (IsFixedArray)
                return $"FixedArray(typeof({GetManagedType()}, {FixedArrayLength}))";
            else
                return GetManagedType().ToString();
        }
#endif
    }
}