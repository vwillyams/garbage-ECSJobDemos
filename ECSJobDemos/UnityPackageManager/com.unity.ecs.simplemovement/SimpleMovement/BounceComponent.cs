using System;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct Bounce : IComponentData
    {
        [NonSerialized] public float t;
        public float speed;
        public float3 height;
    }

    public class BounceComponent : ComponentDataWrapper<Bounce> { } 
}
