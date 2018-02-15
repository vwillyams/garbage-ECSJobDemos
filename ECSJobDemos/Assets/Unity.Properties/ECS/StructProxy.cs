using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Properties.ECS
{
    public unsafe struct StructProxy : IPropertyContainer
    {
        public IVersionStorage VersionStorage => PassthroughVersionStorage.Instance;
        public IPropertyBag PropertyBag => bag;

        public byte* data;
        public PropertyBag bag;
        public Type type;
    }

    public abstract class StructProxyList : IList<StructProxy>
    {
        public IEnumerator<StructProxy> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(StructProxy item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(StructProxy item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(StructProxy[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(StructProxy item)
        {
            throw new NotImplementedException();
        }

        public abstract int Count { get; }
        
        public bool IsReadOnly => true;
        public int IndexOf(StructProxy item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, StructProxy item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public abstract StructProxy this[int index] { get; set; }
    }
}