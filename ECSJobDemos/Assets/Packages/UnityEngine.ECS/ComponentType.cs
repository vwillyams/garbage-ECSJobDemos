using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace UnityEngine.ECS
{
    public struct ComponentType
    {
        public static ComponentType Create<T>()
        {
            ComponentType type;
            type.typeIndex = TypeManager.GetTypeIndex<T>();
            type.sharedComponentIndex = -1;
            type.readOnly = 0;
            return type;
        }

        public static ComponentType ReadOnly(Type type)
        {
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex(type);
            t.sharedComponentIndex = -1;
            t.readOnly = 1;
            return t;
        }
        public static ComponentType ReadOnly<T>()
        {
            ComponentType t;
            t.typeIndex = TypeManager.GetTypeIndex<T>();
            t.sharedComponentIndex = -1;
            t.readOnly = 1;
            return t;
        }

        public ComponentType(Type type, bool isReadOnly = false)
        {
            typeIndex = TypeManager.GetTypeIndex(type);
            sharedComponentIndex = -1;
            readOnly = isReadOnly ? 1 : 0;
        }
        
        public static ComponentType FixedArray(Type type, int numElements)
        {
            ComponentType t;
            t.typeIndex = TypeManager.CreateArrayType(type, numElements);
            t.sharedComponentIndex = -1;
            t.readOnly = 0;
            return t;
        }

        internal bool RequiresJobDependency
        {
            get
            {
                var type = GetManagedType();
                return typeof(IComponentData).IsAssignableFrom(type);
            }
        }

        public Type GetManagedType()
        {
            return TypeManager.GetType(typeIndex);
        }

        public static implicit operator ComponentType(Type type)
        {
            return new ComponentType(type, false);
        }

        //@TODO: We should remove all the comparison operators, make it very explicit.
        //       What you are comparing...
        //       eg. comparing readonly doesn't make sense in many cases... Should be explicit what we want to compare...
        
        
        public override bool Equals(object other)
        {
            return this == (ComponentType)other;
        }

        public override int GetHashCode()
        {
            return (typeIndex * 397) ^ sharedComponentIndex;
        }


        static public bool operator ==(ComponentType lhs, ComponentType rhs)
        {
            return lhs.typeIndex == rhs.typeIndex && lhs.sharedComponentIndex == rhs.sharedComponentIndex;
        }
        static public bool operator !=(ComponentType lhs, ComponentType rhs)
        {
            return lhs.typeIndex != rhs.typeIndex || lhs.sharedComponentIndex != rhs.sharedComponentIndex;
        }

        static public bool operator <(ComponentType lhs, ComponentType rhs)
        {
            return lhs.typeIndex != rhs.typeIndex ? lhs.typeIndex < rhs.typeIndex : lhs.sharedComponentIndex < rhs.sharedComponentIndex;
        }
        static public bool operator >(ComponentType lhs, ComponentType rhs)
        {
            return lhs.typeIndex != rhs.typeIndex ? lhs.typeIndex > rhs.typeIndex : lhs.sharedComponentIndex > rhs.sharedComponentIndex;
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


        public int typeIndex;
        public int readOnly;
        public int sharedComponentIndex;
    }
}