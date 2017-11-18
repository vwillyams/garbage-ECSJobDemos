using System.Reflection;
using System;

namespace UnityEngine.ECS
{
    interface IUpdateInjection
    {
        void UpdateInjection(object targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection);
    }
    
    struct InjectionData
    {
        public FieldInfo           field;
        public Type                containerType;
        public Type                genericType;
        public bool                isReadOnly;

        internal IUpdateInjection  injection;

        public InjectionData(FieldInfo field, Type containerType, Type genericType, bool isReadOnly)
        {
            this.field = field;
            this.containerType = containerType;
            this.genericType = genericType;
            this.isReadOnly = isReadOnly;
            this.injection = null;
        }
    }
}