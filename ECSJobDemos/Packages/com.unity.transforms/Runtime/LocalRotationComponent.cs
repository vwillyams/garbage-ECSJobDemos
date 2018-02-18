using System;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    [Serializable]
    public struct LocalRotation : IComponentData
    {
        public quaternion value;
    }

    public class LocalRotationComponent : ComponentDataWrapper<LocalRotation> { }
}