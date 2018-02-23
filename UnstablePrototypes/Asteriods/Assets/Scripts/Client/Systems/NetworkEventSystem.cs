using UnityEngine;
using Unity.Entities;

using Unity.Multiplayer;
using Unity.GameCode;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using PlayerState = PlayerStateComponentData.PlayerState;

namespace Asteriods.Client
{
    public class NetworkEventSystem : ComponentSystem
    {
        struct Player
        {
            public int Length;
            public ComponentDataArray<PlayerStateComponentData> state;
            public ComponentDataArray<NetworkIdCompmonentData> nid;
            public ComponentDataArray<ShipInfoComponentData> ship;
            public ComponentDataArray<PlayerTagComponentData> self;
        }

        [Inject]
        Player player;

        [Inject]
        SpawnSystem m_SpawnSystem;

        [Inject]
        DespawnSystem m_DespawnSystem;

        [Inject]
        SnapshotSystem m_SnapshotSystem;

        NetworkClient m_NetworkClient;
        StateMachine<PlayerStateComponentData.PlayerState> m_StateMachine;
        Fragmenter m_Fragmenter;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            this.m_NetworkClient = ClientSettings.Instance().networkClient;

            m_StateMachine = new StateMachine<PlayerState>();
            m_StateMachine.Add(PlayerState.None, null, UpdateIdleState, null);
            m_StateMachine.Add(PlayerState.Connecting, EnterConnectingState, UpdateConnectingState, null);
            m_StateMachine.Add(PlayerState.Loading, null, UpdateLoadingState, null);
            m_StateMachine.Add(PlayerState.Playing, null, UpdatePlayState, null);

            var player = EntityManager.CreateEntity(ClientSettings.Instance().playerClientArchetype);
            var ship = EntityManager.CreateEntity(ClientSettings.Instance().playerBaseShipArchetype);

            EntityManager.SetComponentData<ShipInfoComponentData>(player, new ShipInfoComponentData(ship));

            m_StateMachine.SwitchTo(PlayerState.None);
            m_Fragmenter = new Fragmenter();
        }

        override protected void OnDestroyManager()
        {
            m_Fragmenter.Dispose();
        }

        void SwitchState(PlayerState state)
        {
            Debug.Assert(player.Length == 1);
            m_StateMachine.SwitchTo(state);
            player.state[0] = new PlayerStateComponentData(state);
        }

        override protected void OnUpdate()
        {
            m_NetworkClient.Update();
            m_StateMachine.Update();
        }

        void UpdateIdleState()
        {

            SwitchState(PlayerState.Connecting);
        }

        void EnterConnectingState()
        {
            m_NetworkClient.Connect(ClientSettings.Instance().serverAddress, ClientSettings.Instance().serverPort);
        }

        void UpdateConnectingState()
        {
            if (m_NetworkClient.Connected)
            {
                SwitchState(PlayerState.Loading);
            }
        }

        void UpdateLoadingState()
        {
            PollNetwork();
        }

        unsafe void UpdatePlayState()
        {
            PollNetwork();
        }

        int count;
        unsafe void PollNetwork()
        {
            NativeSlice<byte> message;
            while (m_NetworkClient.PeekMessage(out message))
            {
                ByteReader br = new ByteReader(message.GetUnsafePtr(), message.Length);
                var fragment = new FragmentedPacket();
                fragment.ID = br.ReadInt();
                fragment.SequenceNum = br.ReadInt();
                fragment.SequenceCnt = br.ReadInt();
                var length = br.ReadInt();

                fragment.packetData = message.Slice(br.GetBytesRead(), length);

                NativeSlice<byte> packet;
                if (m_Fragmenter.DefragmentPacket(fragment, out packet))
                {
                    ByteReader reader = new ByteReader(packet.GetUnsafePtr(), packet.Length);
                    var type = reader.ReadByte();
                    if (type == (byte)AsteroidsProtocol.Snapshot)
                    {
                        var snapshot = new Snapshot(0, Allocator.Temp);
                        {
                            snapshot.Deserialize(ref reader);
                            HandleSnapshot(ref snapshot);
                        }
                        snapshot.Dispose();
                    }
                    else if (type == (byte)AsteroidsProtocol.ReadyRsp &&
                            m_StateMachine.CurrentState() == PlayerState.Loading)
                    {
                        var msg = new ReadyRsp();
                        msg.Deserialize(ref reader);

                        player.nid[0] = new NetworkIdCompmonentData(msg.NetworkId);
                        m_SpawnSystem.SetPlayerShip(player.ship[0].entity, msg.NetworkId);

                        //player.ship[0] = new ShipInfoComponentData(m_SpawnSystem.SpawnPlayerShip(msg.NetworkId));

                        SwitchState(PlayerState.Playing);
                    }
                    if ((count++ % 60) == 0)
                    {
                        Debug.Log("packet received." + packet.Length);
                    }
                }
                else
                {
                    //Debug.Log("fragment received.");
                }


                m_NetworkClient.PopMessage();
            }

        }

        void HandleSnapshot(ref Snapshot snapshot)
        {
            if (m_StateMachine.CurrentState() != PlayerState.Playing)
                return;

            for (int i = 0, c = snapshot.SpawnCommands.Length; i < c; ++i)
            {
                m_SpawnSystem.SpawnQueue.Enqueue(snapshot.SpawnCommands[i]);
                var sc = snapshot.SpawnCommands[i];
                Debug.Log("spawn = " + sc.id + " of type " + ((SpawnType)sc.type).ToString());
            }

            for (int i = 0, c = snapshot.DespawnCommands.Length; i < c; ++i)
            {
                m_DespawnSystem.DespawnQueue.Enqueue(snapshot.DespawnCommands[i].id);
            }

            for (int i = 0, c = snapshot.MovementDatas.Length; i < c; ++i)
            {
                m_SnapshotSystem.MovementUpdates.Enqueue(snapshot.MovementDatas[i]);
            }
        }
    }
}
