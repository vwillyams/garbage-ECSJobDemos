using System;
using Unity.ECS;

namespace UnityEngine.ECS.SpawnerShim
{
    [Serializable]
    public struct SpawnRandomInSphere : ISharedComponentData
    {
        public GameObject prefab;
        public float radius;
        public int count;
    }

    public class SpawnRandomInSphereComponent : SharedComponentDataWrapper<SpawnRandomInSphere> { }
}
