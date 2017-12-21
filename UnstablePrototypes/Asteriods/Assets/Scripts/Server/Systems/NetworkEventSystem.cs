using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;
using Unity.Multiplayer;

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

        GameSocket m_Socket;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            inputEventQueue = new NativeQueue<PlayerInputComponentData>(Allocator.Persistent);
            Debug.Assert(inputEventQueue.IsCreated);

            m_Socket = ServerSettings.Instance().socket;
        }

        override protected void OnDestroyManager()
        {
            if (inputEventQueue.IsCreated)
                inputEventQueue.Dispose();
        }

        override protected void OnUpdate()
        {

            NativeSlice<byte> outBuffer;
            GameSocketEventType eventType;
            int connectionId;

            if ((eventType = m_Socket.ReceiveEvent(out outBuffer, out connectionId)) == GameSocketEventType.Connect)
            {
                Debug.Log("OnConnect: ConnectionId = " + connectionId);
            }


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
