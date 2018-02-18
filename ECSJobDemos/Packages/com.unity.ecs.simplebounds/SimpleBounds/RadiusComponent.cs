using System;
using Unity.ECS;

namespace UnityEngine.ECS.SimpleBounds
{
    [Serializable]
    public struct Radius : IComponentData
    {
        public float radius;
    }

    public class RadiusComponent : ComponentDataWrapper<Radius> { } 
}
