using System;

namespace UnityEngine.ECS.SpawnerShim
{
    [Serializable]
    public struct SpawnChain : ISharedComponentData
    {
        public GameObject prefab;
        public float minDistance;
        public float maxDistance;
        public int count;
    }

    public class SpawnChainComponent : SharedComponentDataWrapper<SpawnChain> { }
}
