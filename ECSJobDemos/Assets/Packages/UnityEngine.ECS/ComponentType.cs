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
            return type;
        }

        public ComponentType(Type type)
        {
            typeIndex = TypeManager.GetTypeIndex(type);
            sharedComponentIndex = -1;
        }

        public ComponentType(Type type, int numElements)
        {
            typeIndex = TypeManager.CreateArrayType(type, numElements);
            sharedComponentIndex = -1;
        }

        public Type GetManagedType()
        {
            return TypeManager.GetType(typeIndex);
        }

        public static implicit operator ComponentType(Type type)
        {
            return new ComponentType(type);
        }

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
        public int sharedComponentIndex;
    }
}