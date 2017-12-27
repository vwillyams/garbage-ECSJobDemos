using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
struct JSONSerializedElement
{
    [SerializeField]
    public string typeName;

    [SerializeField]
    public string data;
}

[Serializable]
class CopyPasteData<T> : ISerializationCallbackReceiver where T : class
{
    [NonSerialized]
    List<T> m_Nodes = new List<T>();

    [SerializeField]
    List<JSONSerializedElement> m_SerializedElements = new List<JSONSerializedElement>();

    public CopyPasteData(IEnumerable<T> elements)
    {
        m_Nodes = new List<T>(elements);
    }

    public List<T> GetNodes()
    {
        return m_Nodes;
    }

    public virtual void OnBeforeSerialize()
    {
        m_SerializedElements = new List<JSONSerializedElement>();
        foreach (var node in m_Nodes)
        {
            if (node == null)
                continue;

            string typeName = node.GetType().AssemblyQualifiedName;
            string data = JsonUtility.ToJson(node);

            m_SerializedElements.Add(new JSONSerializedElement() { typeName = typeName, data = data });
        }
    }

    public virtual void OnAfterDeserialize()
    {
        m_Nodes = new List<T>(m_SerializedElements.Count);
        foreach (JSONSerializedElement e in m_SerializedElements)
        {
            var t = Type.GetType(e.typeName);
            T instance = Activator.CreateInstance(t) as T;
            if (instance != null)
            {
                JsonUtility.FromJsonOverwrite(e.data, instance);
                m_Nodes.Add(instance);
            }
        }
        m_SerializedElements = null;
    }
}
