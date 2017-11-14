public struct BlittableBool
{
    byte m_Value;

    public static implicit operator BlittableBool(bool value)
    {
        return new BlittableBool
        {
            m_Value = (byte)(value ? 1 : 0)
        };
    }

    public static implicit operator bool(BlittableBool navMeshBool)
    {
        return navMeshBool.m_Value != 0;
    }
}
