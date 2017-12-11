using UnityEngine;
using UnityEngine.ECS;

public struct NetworkIdCompmonentData : IComponentData
{
    [SerializeField]
    public int id;
    public NetworkIdCompmonentData(int id)
    {
        this.id = id;
    }
}
public class NetworkIdComponent : ComponentDataWrapper<NetworkIdCompmonentData> { }