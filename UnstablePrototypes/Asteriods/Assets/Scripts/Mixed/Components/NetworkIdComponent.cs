using UnityEngine;
using Unity.Entities;

public struct NetworkIdCompmonentData : IComponentData
{
    [SerializeField]
    public int id;
    public NetworkIdCompmonentData(int id)
    {
        this.id = id;
    }
}
