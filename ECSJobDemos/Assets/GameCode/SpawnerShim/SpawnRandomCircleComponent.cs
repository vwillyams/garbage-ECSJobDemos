using System;
using Unity.Entities;

namespace UnityEngine.ECS.SpawnerShim
{
    [Serializable]
    public struct SpawnRandomCircle : ISharedComponentData
    {
        public GameObject prefab;
        public bool spawnLocal;
        public float radius;
        public int count;
    }

    public class SpawnRandomCircleComponent : SharedComponentDataWrapper<SpawnRandomCircle> { }
}
