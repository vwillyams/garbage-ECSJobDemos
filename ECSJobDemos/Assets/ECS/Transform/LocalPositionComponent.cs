using System;
using Unity.Mathematics;

namespace UnityEngine.ECS.Transform
{
    [Serializable]
    public struct LocalPosition : IComponentData
    {
        public float3 position;
    }

    public class LocalPositionComponent : ComponentDataWrapper<LocalPosition> { } 
}
