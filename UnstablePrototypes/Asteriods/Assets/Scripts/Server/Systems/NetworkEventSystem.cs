using UnityEngine;
using Unity.Entities;

using Unity.Collections;
using Unity.Multiplayer;

using Unity.Collections.LowLevel.Unsafe;

using PlayerState = PlayerStateComponentData.PlayerState;

namespace Asteriods.Server
{
    //[UpdateAfter(typeof(DamageSystem))]
    public class NetworkEventSystem : ComponentSystem
    {
        [Inject]
        NetworkStateSystem m_NetworkStateSystem;

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
            if (!m_NetworkServer.IsCreated)
            {
                return;
            }
            m_NetworkServer.Update();

            NetworkConnection connection;
            while (m_NetworkServer.TryPopDisconnectionQueue(out connection))
            {
                m_NetworkStateSystem.PlayerDestroy(connection);
                Debug.Log("OnDisconnect: ConnectionId = " + connection.Id);
            }

            while (m_NetworkServer.TryPopConnectionQueue(out connection))
            {
                m_NetworkStateSystem.PlayerCreate(connection);
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
                            m_NetworkStateSystem.PlayerUpdate(id, e);
                            //m_SteeringSystem.playerInputQueue.Enqueue(e);

                            //if (e.shoot == 1)
                            //{
                                //m_SpawnSystem.IncommingSpawnQueue.Enqueue(new SpawnCommand(0, (int)SpawnType.Bullet, default(PositionComponentData), default(RotationComponentData)));
                            //}
                        }
                    }
                }
                else if (type == (byte)AsteroidsProtocol.ReadyReq)
                {
                    Debug.Log("ready req from " + id);
                    var nc = new NetworkConnection(id);
                    m_NetworkStateSystem.PlayerSetReady(nc);
                }

                int read_bytes = br.GetBytesRead();
                Debug.Assert(message.Length == read_bytes);
            }
        }
    }

    public class NetworkStateSystem : ComponentSystem
    {
        [Inject]
        NetworkMessageSystem m_NetworkMessageSystem;

        int m_NetworkId;

        NativeHashMap<int, Entity> m_Connections;
        NativeQueue<int> m_ReadyConnections;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_Connections = new NativeHashMap<int, Entity>(10, Allocator.Persistent);
            m_ReadyConnections = new NativeQueue<int>(Allocator.Persistent);
        }

        override protected void OnDestroyManager()
        {
            if (m_Connections.IsCreated)
                m_Connections.Dispose();

            if (m_ReadyConnections.IsCreated)
                m_ReadyConnections.Dispose();
        }

        unsafe override protected void OnUpdate()
        {
        }

        public int GetNextNetworkId()
        {
            return m_NetworkId++;
        }

        public void PlayerDestroy(NetworkConnection nc)
        {
            Entity e;
            if (!m_Connections.TryGetValue(nc.Id, out e))
            {
                return;
            }

            //m_NetworkMessageSystem.DespawnQueue.;
            var nid = EntityManager.GetComponentData<NetworkIdCompmonentData>(e);
            m_NetworkMessageSystem.DespawnQueue.Enqueue(new DespawnCommand(nid.id));

            m_Connections.Remove(nc.Id);
            EntityManager.DestroyEntity(e);
        }

        public void PlayerCreate(NetworkConnection nc)
        {
            var e = EntityManager.CreateEntity(ServerSettings.Instance().playerArchetype);

            var id = GetNextNetworkId();

            EntityManager.SetComponentData<EntityTypeComponentData>(e, new EntityTypeComponentData() { Type = (int)SpawnType.Ship });
            EntityManager.SetComponentData<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(id));
            EntityManager.SetComponentData<PlayerStateComponentData>(e, new PlayerStateComponentData(PlayerState.Loading));

            m_Connections.TryAdd(nc.Id, e);
        }

        public void PlayerSetReady(NetworkConnection nc)
        {
            Entity e;
            if (!m_Connections.TryGetValue(nc.Id, out e))
                return;

            var s = EntityManager.GetComponentData<PlayerStateComponentData>(e);
            if (s.State >= (int)PlayerState.Ready)
                return;

            EntityManager.SetComponentData<PlayerStateComponentData>(e, new PlayerStateComponentData(PlayerState.Ready));
            m_ReadyConnections.Enqueue(nc.Id);
        }

        public void PlayerUpdate(int nid, PlayerInputComponentData input)
        {
            Entity e;
            if (!m_Connections.TryGetValue(nid, out e))
                return;

            EntityManager.SetComponentData<PlayerInputComponentData>(e, input);
        }

        public bool PlayerTryGetReady(out int id, out Entity e)
        {
            if (!m_ReadyConnections.TryDequeue(out id))
            {
                e = Entity.Null;
                return false;
            }
            return m_Connections.TryGetValue(id, out e);
        }
    }
}
