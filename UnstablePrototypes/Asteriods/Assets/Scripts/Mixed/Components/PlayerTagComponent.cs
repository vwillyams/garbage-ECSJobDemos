using UnityEngine.ECS;

public struct PlayerTagComponentData : IComponentData
{
}

public class PlayerTagComponent : ComponentDataWrapper<PlayerTagComponentData> { }
