using System;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    [Serializable]
    public struct LocalPosition : IComponentData
    {
        public float3 Value;
    }

    public class LocalPositionComponent : ComponentDataWrapper<LocalPosition> { } 
}
