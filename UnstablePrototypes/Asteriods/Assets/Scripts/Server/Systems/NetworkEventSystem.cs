using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;
using Unity.Multiplayer;

using Unity.Collections.LowLevel.Unsafe;

namespace Asteriods.Server
{
    [UpdateAfter(typeof(DamageSystem))]
    public class NetworkEventSystem : ComponentSystem
    {
        [Inject]
        SteeringSystem m_SteeringSystem;

        [Inject]
        SpawnSystem m_SpawnSystem;

        NetworkServer m_NetworkServer;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            this.m_NetworkServer = ServerSettings.Instance().networkServer;
        }

        override protected void OnDestroyManager()
        {
        }

        unsafe override protected void OnUpdate()
        {
            m_NetworkServer.Update();

            NetworkConnection connection;
            while (m_NetworkServer.TryPopConnectionQueue(out connection))
            {
                Debug.Log("OnConnect: ConnectionId = " + connection.Id);
            }

            int id;
            NativeSlice<byte> message;
            while (m_NetworkServer.ReadMessage(out message, out id))
            {
                ByteReader br = new ByteReader(message.GetUnsafePtr(), message.Length);
                var type = br.ReadByte();

                if (type == (byte)AsteroidsProtocol.Command)
                {
                    using (var command = new Command(0, Allocator.Temp))
                    {
                        command.Deserialize(ref br);

                        for (int i = 0, s = command.InputCommands.Length; i < s; ++i)
                        {
                            var e = command.InputCommands[i];
                            m_SteeringSystem.playerInputQueue.Enqueue(e);

                            if (e.shoot == 1)
                            {
                                m_SpawnSystem.IncommingSpawnQueue.Enqueue(new SpawnCommand(0, (int)SpawnType.Bullet, default(PositionComponentData), default(RotationComponentData)));
                            }
                        }
                    }
                }
                int read_bytes = br.GetBytesRead();
                Debug.Assert(message.Length == read_bytes);
            }
/*
            for (int i = 0, c = inputEventQueue.Count; i < c; ++i)
            {
                var e = inputEventQueue.Dequeue();
                m_SteeringSystem.playerInputQueue.Enqueue(e);

                if (e.shoot == 1)
                {
                    m_SpawnSystem.IncommingSpawnQueue.Enqueue(new SpawnCommand(0, (int)SpawnType.Bullet, default(PositionComponentData), default(RotationComponentData)));
                }
            }
*/
        }
    }
}
