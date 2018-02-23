using Unity.Entities;

namespace UnityEngine.ECS.SimpleMovement
{
    public struct Gravity : ISharedComponentData { }

    public class GravityComponent : SharedComponentDataWrapper<Gravity> { } 
}
