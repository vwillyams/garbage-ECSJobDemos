using System;
using Unity.Entities;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct LocalBounce : IComponentData
    {
        [NonSerialized] public float t;
        public float speed;
        public float3 height;
    }

    public class LocalBounceComponent : ComponentDataWrapper<LocalBounce> { } 
}
