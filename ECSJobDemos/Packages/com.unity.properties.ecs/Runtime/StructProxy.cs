using System;

namespace Unity.Properties.Entities
{
    public unsafe struct StructProxy : IPropertyContainer
    {
        public IVersionStorage VersionStorage => PassthroughVersionStorage.Instance;
        public IPropertyBag PropertyBag => bag;

        public byte* data;
        public PropertyBag bag;
        public Type type;
    }
}
