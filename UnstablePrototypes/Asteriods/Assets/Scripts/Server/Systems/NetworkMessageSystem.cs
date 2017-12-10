using UnityEngine;
using UnityEngine.ECS;

namespace Asteriods.Server
{
    public class NetworkMessageSystem : ComponentSystem
    {
        [Inject]
        SpawnSystem m_SpawnSystem;
        struct SerializableData
        {
            public int Length;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            ComponentDataArray<PlayerTagComponentData> __tag;
        }

        [InjectComponentGroup]
        SerializableData data;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnDestroyManager()
        {
            base.OnDestroyManager();
        }

        override protected void OnUpdate()
        {
            for (int i = 0, c = m_SpawnSystem.spawnQueue.Count; i < c; ++i)
            {
                // HACK (2017-12-08, lifetime 4 weeks or until proper protocol implemented.)
                Asteriods.Client.NetworkEventSystem.spawnEventQueue.Enqueue(m_SpawnSystem.spawnQueue.Dequeue());
            }

            for (int i = 0, c = data.Length; i < c; ++i)
            {
                var m = new MovementData(0, data.positions[i], data.rotations[i]);
                Asteriods.Client.NetworkEventSystem.movementEventQueue.Enqueue(m);
            }
        }
    }
}