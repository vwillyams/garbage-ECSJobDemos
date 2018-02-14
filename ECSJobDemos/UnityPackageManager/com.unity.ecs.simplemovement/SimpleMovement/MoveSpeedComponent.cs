using System;
using Unity.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct MoveSpeed : IComponentData
    {
        public float speed;
    }

    public class MoveSpeedComponent : ComponentDataWrapper<MoveSpeed> { } 
}
