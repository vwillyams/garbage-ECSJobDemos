using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

namespace Asteriods.Client
{
    public class SnapshotSystem : ComponentSystem
    {
        public NativeQueue<MovementData> movementUpdates;
        struct Spaceships
        {
            public int Length;
            public ComponentDataArray<PlayerTagComponentData> input;
            public ComponentArray<Transform> transform;
        }

        [InjectComponentGroup]
        Spaceships spaceships;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            movementUpdates = new NativeQueue<MovementData>(128, Allocator.Persistent);
            Debug.Assert(movementUpdates.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (movementUpdates.IsCreated)
                movementUpdates.Dispose();
        }

        override protected void OnUpdate()
        {
            if (movementUpdates.Count < 1)
                return;
            for (int i = 0; i < spaceships.transform.Length; i++)
            {
                var data = movementUpdates.Dequeue();
                var pos = new Vector3(data.position.x, data.position.y, 0);
                var rot = new Vector3(0f, 0f, data.rotation.angle);

                spaceships.transform[i].position = pos;
                spaceships.transform[i].rotation = Quaternion.Euler(rot);
            }
        }
    }
}