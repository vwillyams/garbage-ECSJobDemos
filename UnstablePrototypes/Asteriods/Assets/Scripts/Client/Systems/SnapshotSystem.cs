using UnityEngine;
using Unity.Entities;

using Unity.Collections;

namespace Asteriods.Client
{
    [UpdateAfter(typeof(SpawnSystem))]
    public class SnapshotSystem : ComponentSystem
    {
        [Inject]
        SpawnSystem m_SpawnSystem;

        public NativeQueue<MovementData> MovementUpdates;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            MovementUpdates = new NativeQueue<MovementData>(Allocator.Persistent);
            Debug.Assert(MovementUpdates.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (MovementUpdates.IsCreated)
                MovementUpdates.Dispose();
        }

        override protected void OnUpdate()
        {
            for (int i = 0, c = MovementUpdates.Count; i < c; ++i)
            {
                var update = MovementUpdates.Dequeue();
                Entity e;
                if (m_SpawnSystem.NetworkIdLookup.TryGetValue(update.id, out e) && EntityManager.Exists(e))
                {
                    if (!EntityManager.HasComponent<PositionComponentData>(e))
                    {
                        Debug.Log("Missing component Position ComponentData on " + update.id);
                        continue;
                    }
                    EntityManager.SetComponentData(e, new PositionComponentData(update.position.x, update.position.y));
                    EntityManager.SetComponentData(e, new RotationComponentData(update.rotation.angle));
                }
                else
                {
                    m_SpawnSystem.SpawnQueue.Enqueue(new SpawnCommand(update.id, update.type, update.position, update.rotation));
                }
            }
        }
    }
}
