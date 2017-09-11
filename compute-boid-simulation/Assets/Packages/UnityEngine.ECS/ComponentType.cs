using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace UnityEngine.ECS
{
    public struct ComponentType
    {
        public ComponentType(Type type)
        {
            typeIndex = RealTypeManager.GetTypeIndex(type);
        }
        public ComponentType(Type type, int numElements)
        {
            typeIndex = RealTypeManager.CreateArrayType(type, numElements);
        }

        public static implicit operator ComponentType(Type type)
        {
            return new ComponentType(type);
        }

        public int typeIndex;
    }
}