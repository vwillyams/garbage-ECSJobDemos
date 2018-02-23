using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct MoveSpeed : IComponentData
    {
        public float speed;
    }

    public class MoveSpeedComponent : ComponentDataWrapper<MoveSpeed> { } 
}
