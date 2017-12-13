using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

namespace Asteriods.Server
{
    public class NetworkMessageSystem : ComponentSystem
    {
        public NativeQueue<DespawnCommand> DespawnQueue;

        [Inject]
        SpawnSystem m_SpawnSystem;

        [Inject]
        DamageSystem m_DamageSystem;

        struct NetworkedItems
        {
            public int Length;
            public ComponentDataArray<NetworkIdCompmonentData> ids;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
        }

        [InjectComponentGroup]
        NetworkedItems networkedItems;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            DespawnQueue = new NativeQueue<DespawnCommand>(128, Allocator.Persistent);
            Debug.Assert(DespawnQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            base.OnDestroyManager();

            if (DespawnQueue.IsCreated)
                DespawnQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            // HACK (2017-12-11, lifetime 4 weeks or until proper protocol implemented.)
            for (int i = 0, c = m_SpawnSystem.OutgoingSpawnQueue.Count; i < c; ++i)
            {
                Asteriods.Client.NetworkEventSystem.SpawnEventQueue.Enqueue(m_SpawnSystem.OutgoingSpawnQueue.Dequeue());
            }

            for (int i = 0, c = networkedItems.Length; i < c; ++i)
            {
                var m = new MovementData(networkedItems.ids[i].id, networkedItems.positions[i], networkedItems.rotations[i]);
                Asteriods.Client.NetworkEventSystem.MovementEventQueue.Enqueue(m);
            }

            for (int i = 0, c = DespawnQueue.Count; i < c; ++i)
            {
                Asteriods.Client.NetworkEventSystem.DespawnEventQueue.Enqueue(DespawnQueue.Dequeue());
            }
        }
    }
}