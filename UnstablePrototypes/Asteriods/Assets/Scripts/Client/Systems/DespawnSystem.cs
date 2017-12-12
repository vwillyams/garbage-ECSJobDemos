using UnityEngine.ECS;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Client
{
    [UpdateAfter(typeof(SnapshotSystem))]
    public class DespawnSystem : ComponentSystem
    {
        [Inject]
        EntityManager m_EntityManager;

        [Inject]
        SpawnSystem m_SpawnSystem;

        public NativeQueue<int> DespawnQueue;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            DespawnQueue = new NativeQueue<int>(128, Allocator.Persistent);
            Debug.Assert(DespawnQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (DespawnQueue.IsCreated)
                DespawnQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            for (int i = 0, c = DespawnQueue.Count; i < c; ++i)
            {
                var id = DespawnQueue.Dequeue();
                Entity e;
                if (m_SpawnSystem.NetworkIdLookup.TryGetValue(id, out e) && EntityManager.Exists(e))
                {
                    Debug.Log("despawn for " + id + " received");
                    m_EntityManager.DestroyEntity(e);
                }
            }
        }
    }
}
