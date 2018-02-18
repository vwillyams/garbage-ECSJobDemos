using System;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    [Serializable]
    public struct LocalPosition : IComponentData
    {
        public float3 position;
    }

    public class LocalPositionComponent : ComponentDataWrapper<LocalPosition> { } 
}
