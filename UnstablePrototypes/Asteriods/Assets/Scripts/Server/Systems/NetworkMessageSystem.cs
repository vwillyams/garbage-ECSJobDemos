using UnityEngine;
using Unity.Entities;

using Unity.Multiplayer;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using PlayerState = PlayerStateComponentData.PlayerState;

namespace Asteriods.Server
{
    public class NetworkMessageSystem : ComponentSystem
    {
        public NativeQueue<DespawnCommand> DespawnQueue;

        [Inject]
        NetworkStateSystem m_NetworkStateSystem;

        [Inject]
        SpawnSystem m_SpawnSystem;

        NetworkServer m_NetworkServer;
        NativeArray<byte> m_Buffer;

        // Should be per connection
        int m_Sequence;
        NativeArray<byte> m_FragmentBuffer;
        Fragmenter m_Fragmenter;

        struct NetworkedItems
        {
            public int Length;
            public ComponentDataArray<NetworkIdCompmonentData> ids;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            public ComponentDataArray<EntityTypeComponentData> types;
        }

        [Inject]
        NetworkedItems networkedItems;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_NetworkServer = ServerSettings.Instance().networkServer;

            DespawnQueue = new NativeQueue<DespawnCommand>(Allocator.Persistent);
            Debug.Assert(DespawnQueue.IsCreated);


            m_Fragmenter = new Fragmenter();
            m_Buffer = new NativeArray<byte>(1024*1024, Allocator.Persistent);
        }

        override protected void OnDestroyManager()
        {
            base.OnDestroyManager();

            if (DespawnQueue.IsCreated)
                DespawnQueue.Dispose();
            if (m_Buffer.IsCreated)
                m_Buffer.Dispose();

            m_Fragmenter.Dispose();
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

                // TODO (michalb): super hacky, move everything to one stream!
                var bw = new ByteWriter(m_Buffer.GetUnsafePtr(), m_Buffer.Length);
                bw.Write((byte)AsteroidsProtocol.Snapshot);
                snapshot.Serialize(ref bw);

                var slice = m_Buffer.Slice(0, bw.GetBytesWritten());
                // m_NetworkServer.WriteMessage(slice);
                WriteFragmented(slice, -1);
            }


            int id;
            Entity e;
            while (m_NetworkStateSystem.PlayerTryGetReady(out id, out e))
            {
                var bw = new ByteWriter(m_Buffer.GetUnsafePtr(), m_Buffer.Length);
                bw.Write((byte)AsteroidsProtocol.ReadyRsp);
                var rsp = new ReadyRsp();

                var nid = EntityManager.GetComponentData<NetworkIdCompmonentData>(e);
                rsp.NetworkId = nid.id;
                Debug.Log("responding to netid " + nid.id + " con id " + id);

                rsp.Serialize(ref bw);

                //m_NetworkServer.WriteMessage(m_Buffer.Slice(0, bw.GetBytesWritten()), id);
                WriteFragmented(m_Buffer.Slice(0, bw.GetBytesWritten()), id);

                m_SpawnSystem.SpawnPlayer(e);
                EntityManager.SetComponentData<PlayerStateComponentData>(e, new PlayerStateComponentData(PlayerState.Playing));
            }
        }
        unsafe void WriteFragmented(NativeSlice<byte> slice, int id)
        {
            m_Fragmenter.FragmentPacket(slice, m_Sequence++);

            var packet = new NativeArray<byte>(1400, Allocator.Temp);
            while (m_Fragmenter.fragmentedOutgoing.Count > 0)
            {
                var pw = new ByteWriter(packet.GetUnsafePtr(), packet.Length);
                var fragment = m_Fragmenter.fragmentedOutgoing.Dequeue();
                pw.Write(fragment.ID);
                pw.Write(fragment.SequenceNum);
                pw.Write(fragment.SequenceCnt);
                pw.Write(fragment.packetData.Length);
                pw.WriteBytes((byte*)fragment.packetData.GetUnsafePtr(), fragment.packetData.Length);

                if (id == -1)
                    m_NetworkServer.WriteMessage(packet.Slice(0, pw.GetBytesWritten()));
                else
                    m_NetworkServer.WriteMessage(packet.Slice(0, pw.GetBytesWritten()), id);
            }
            packet.Dispose();
        }
    }
}
