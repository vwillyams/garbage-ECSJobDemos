using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

namespace Asteriods.Server
{
    [UpdateAfter(typeof(DamageSystem))]
    public class NetworkEventSystem : ComponentSystem
    {
        // HACK (2017-12-08, lifetime 4 weeks or until proper protocol implemented.)
        public static NativeQueue<PlayerInputComponentData> inputEventQueue;

        [Inject]
        SteeringSystem m_SteeringSystem;

        [Inject]
        SpawnSystem m_SpawnSystem;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            inputEventQueue = new NativeQueue<PlayerInputComponentData>(128, Allocator.Persistent);
            Debug.Assert(inputEventQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (inputEventQueue.IsCreated)
                inputEventQueue.Dispose();
        }
        override protected void OnUpdate()
        {
            for (int i = 0, c = inputEventQueue.Count; i < c; ++i)
            {
                var e = inputEventQueue.Dequeue();
                m_SteeringSystem.playerInputQueue.Enqueue(e);

                if (e.shoot == 1)
                {
                    m_SpawnSystem.IncommingSpawnQueue.Enqueue(new SpawnCommand(0, (int)SpawnType.Bullet, default(PositionComponentData), default(RotationComponentData)));
                }
            }
        }
    }
}