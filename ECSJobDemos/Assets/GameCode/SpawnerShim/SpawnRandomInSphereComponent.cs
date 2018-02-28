using System;
using Unity.Entities;

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
