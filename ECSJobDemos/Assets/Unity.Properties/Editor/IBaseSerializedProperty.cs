using UnityEditor;

namespace Unity.Properties.Editor
{
    public interface IBaseSerializedProperty : IProperty
    {
        SerializedProperty Property { get; }
    }
}