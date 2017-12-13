using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

namespace Asteriods.Client
{
    public class NetworkEventSystem : ComponentSystem
    {
        // HACK (2017-12-08, lifetime 4 weeks or until proper protocol implemented.)
        public static NativeQueue<SpawnCommand> SpawnEventQueue;
        public static NativeQueue<DespawnCommand> DespawnEventQueue;
        public static NativeQueue<MovementData> MovementEventQueue;

        [Inject]
        SpawnSystem m_SpawnSystem;

        [Inject]
        DespawnSystem m_DespawnSystem;

        [Inject]
        SnapshotSystem m_SnapshotSystem;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            SpawnEventQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            MovementEventQueue = new NativeQueue<MovementData>(128, Allocator.Persistent);
            DespawnEventQueue = new NativeQueue<DespawnCommand>(128, Allocator.Persistent);
            Debug.Assert(SpawnEventQueue.IsCreated);
            Debug.Assert(MovementEventQueue.IsCreated);
            Debug.Assert(DespawnEventQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (SpawnEventQueue.IsCreated)
                SpawnEventQueue.Dispose();
            if (MovementEventQueue.IsCreated)
                MovementEventQueue.Dispose();
            if (DespawnEventQueue.IsCreated)
                DespawnEventQueue.Dispose();
        }
        override protected void OnUpdate()
        {
            for (int i = 0, c = SpawnEventQueue.Count; i < c; ++i)
            {
                m_SpawnSystem.SpawnQueue.Enqueue(SpawnEventQueue.Dequeue());
            }

            for (int i = 0, c = MovementEventQueue.Count; i < c; ++i)
            {
                m_SnapshotSystem.MovementUpdates.Enqueue(MovementEventQueue.Dequeue());
            }

            for (int i = 0, c = DespawnEventQueue.Count; i < c; ++i)
            {
                m_DespawnSystem.DespawnQueue.Enqueue(DespawnEventQueue.Dequeue().id);
            }
        }
    }
}