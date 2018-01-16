using UnityEngine;
using UnityEngine.ECS;

using Unity.Multiplayer;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Asteriods.Server
{
    public class NetworkMessageSystem : ComponentSystem
    {
        public NativeQueue<DespawnCommand> DespawnQueue;

        [Inject]
        SpawnSystem m_SpawnSystem;

        NetworkServer m_NetworkServer;
        NativeArray<byte> m_Buffer;

        struct NetworkedItems
        {
            public int Length;
            public ComponentDataArray<NetworkIdCompmonentData> ids;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            public ComponentDataArray<EntityTypeComponentData> types;
        }

        [InjectComponentGroup]
        NetworkedItems networkedItems;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_NetworkServer = ServerSettings.Instance().networkServer;

            DespawnQueue = new NativeQueue<DespawnCommand>(Allocator.Persistent);
            m_Buffer = new NativeArray<byte>(GameSocket.Constants.MaxPacketSize, Allocator.Persistent);
            Debug.Assert(DespawnQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            base.OnDestroyManager();

            if (DespawnQueue.IsCreated)
                DespawnQueue.Dispose();
            if (m_Buffer.IsCreated)
                m_Buffer.Dispose();
        }

        unsafe override protected void OnUpdate()
        {
            using (var snapshot = new Snapshot(0, Allocator.Temp))
            {
                for (int i = 0, c = m_SpawnSystem.OutgoingSpawnQueue.Count; i < c; ++i)
                {
                    snapshot.SpawnCommands.Add(m_SpawnSystem.OutgoingSpawnQueue.Dequeue());
                }

                for (int i = 0, c = networkedItems.Length; i < c; ++i)
                {
                    var m = new MovementData(networkedItems.ids[i].id, networkedItems.types[i].Type, networkedItems.positions[i], networkedItems.rotations[i]);
                    snapshot.MovementDatas.Add(m);
                }

                for (int i = 0, c = DespawnQueue.Count; i < c; ++i)
                {
                    snapshot.DespawnCommands.Add(DespawnQueue.Dequeue());
                }

                var bw = new ByteWriter(m_Buffer.GetUnsafePtr(), m_Buffer.Length);
                bw.Write((byte)AsteroidsProtocol.Snapshot);
                snapshot.Serialize(ref bw);

                //Debug.Log(bw.GetBytesWritten());
                var slice = m_Buffer.Slice(0, bw.GetBytesWritten());
                m_NetworkServer.WriteMessage(slice);
            }
        }
    }
}
