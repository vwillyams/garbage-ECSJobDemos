using UnityEngine;
using Unity.ECS;

public struct NetworkIdCompmonentData : IComponentData
{
    [SerializeField]
    public int id;
    public NetworkIdCompmonentData(int id)
    {
        this.id = id;
    }
}
