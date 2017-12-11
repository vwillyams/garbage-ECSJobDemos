using UnityEngine;
using UnityEngine.ECS;

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

            MovementUpdates = new NativeQueue<MovementData>(128, Allocator.Persistent);
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
                    var pos = new Vector3(update.position.x, update.position.y, 0);
                    var rot = new Vector3(0f, 0f, update.rotation.angle);

                    var transform = EntityManager.GetComponentObject<Transform>(e);

                    transform.position = pos;
                    transform.rotation = Quaternion.Euler(rot);
                }
            }
        }
    }
}