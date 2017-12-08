using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

namespace Asteriods.Client
{
    public class NetworkEventSystem : ComponentSystem
    {
        // HACK (2017-12-08, lifetime 4 weeks or until proper protocol implemented.)
        public static NativeQueue<SpawnCommand> spawnEventQueue;
        public static NativeQueue<MovementData> movementEventQueue;

        [Inject]
        SpawnSystem m_SpawnSystem;

        [Inject]
        SnapshotSystem m_SnapshotSystem;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            spawnEventQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            movementEventQueue = new NativeQueue<MovementData>(128, Allocator.Persistent);
            Debug.Assert(spawnEventQueue.IsCreated);
            Debug.Assert(movementEventQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (spawnEventQueue.IsCreated)
                spawnEventQueue.Dispose();
            if (movementEventQueue.IsCreated)
                movementEventQueue.Dispose();
        }
        override protected void OnUpdate()
        {
            for (int i = 0, c = spawnEventQueue.Count; i < c; ++i)
            {
                m_SpawnSystem.spawnQueue.Enqueue(spawnEventQueue.Dequeue());
            }

            for (int i = 0, c = movementEventQueue.Count; i < c; ++i)
            {
                m_SnapshotSystem.movementUpdates.Enqueue(movementEventQueue.Dequeue());
            }
        }
    }
}