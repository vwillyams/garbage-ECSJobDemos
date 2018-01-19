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
        struct Players
        {
            public int Length;
            public ComponentDataArray<PlayerStateComponentData> state;
        }

        [InjectComponentGroup]
        Players players;


        [Inject]
        SteeringSystem m_SteeringSystem;

        [Inject]
        SpawnSystem m_SpawnSystem;

        NetworkServer m_NetworkServer;

        NativeHashMap<int, Entity> m_Connections;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            this.m_NetworkServer = ServerSettings.Instance().networkServer;
            m_Connections = new NativeHashMap<int, Entity>(10, Allocator.Persistent);
        }

        override protected void OnDestroyManager()
        {
            if (m_Connections.IsCreated)
                m_Connections.Dispose();
        }


        unsafe override protected void OnUpdate()
        {
            var readyPlayers = new NativeList<int>(Allocator.Temp);
            if (!m_NetworkServer.IsCreated)
            {
                return;
            }
            m_NetworkServer.Update();

            NetworkConnection connection;
            while (m_NetworkServer.TryPopConnectionQueue(out connection))
            {
                m_Connections.TryAdd(connection.Id, m_SpawnSystem.CreatePlayer(connection));
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
                else if (type == (byte)AsteroidsProtocol.ReadyReq)
                {
                    Entity e;

                    var buffer = new NativeArray<byte>(16, Allocator.Temp);

                    var bw = new ByteWriter(buffer.GetUnsafePtr(), buffer.Length);
                    bw.Write((byte)AsteroidsProtocol.ReadyRsp);

                    //Debug.Log(bw.GetBytesWritten());
                    m_NetworkServer.WriteMessage(buffer.Slice(0, bw.GetBytesWritten()));

                    buffer.Dispose();

                    m_Connections.TryGetValue(id, out e);
                    m_SpawnSystem.SpawnPlayer(e);
                }

                int read_bytes = br.GetBytesRead();
                Debug.Assert(message.Length == read_bytes);
            }

            readyPlayers.Dispose();
        }

    }
}
