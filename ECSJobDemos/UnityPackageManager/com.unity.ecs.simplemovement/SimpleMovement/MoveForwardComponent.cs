namespace UnityEngine.ECS.SimpleMovement
{
    public struct MoveForward : ISharedComponentData { }

    public class MoveForwardComponent : SharedComponentDataWrapper<MoveForward> { } 
}
