using UnityEngine;
using UnityEngine.ECS;

using Unity.Multiplayer;
using Unity.GameCode;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Asteriods.Client
{
    public class NetworkEventSystem : ComponentSystem
    {
        [Inject]
        SpawnSystem m_SpawnSystem;

        [Inject]
        DespawnSystem m_DespawnSystem;

        [Inject]
        SnapshotSystem m_SnapshotSystem;

        NetworkClient m_NetworkClient;
        StateMachine<ClientState> m_StateMachine;

        enum ClientState
        {
            Connecting,
            Loading,
            Playing
        }

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            this.m_NetworkClient = ClientSettings.Instance().networkClient;

            m_StateMachine = new StateMachine<ClientState>();
            m_StateMachine.Add(ClientState.Connecting, EnterConnectingState, UpdateConnectingState, null);
            m_StateMachine.Add(ClientState.Loading, null, UpdateLoadingState, null);
            m_StateMachine.Add(ClientState.Playing, null, UpdatePlayState, null);

            m_StateMachine.SwitchTo(ClientState.Connecting);
        }

        override protected void OnDestroyManager()
        {
        }

        override protected void OnUpdate()
        {
            m_NetworkClient.Update();
            m_StateMachine.Update();
        }

        void EnterConnectingState()
        {
            m_NetworkClient.Connect(ClientSettings.Instance().serverAddress, ClientSettings.Instance().serverPort);
        }

        void UpdateConnectingState()
        {
            if (m_NetworkClient.Connected)
            {
                m_StateMachine.SwitchTo(ClientState.Loading);
            }
        }

        void UpdateLoadingState()
        {
            m_StateMachine.SwitchTo(ClientState.Playing);
        }

        unsafe void UpdatePlayState()
        {
            NativeSlice<byte> message;
            while (m_NetworkClient.ReadMessage(out message))
            {
                ByteReader br = new ByteReader(message.GetUnsafePtr(), message.Length);
                var type = br.ReadByte();

                if (type == (byte)AsteroidsProtocol.Snapshot)
                {
                    using (var snapshot = new Snapshot(0, Allocator.Temp))
                    {
                        snapshot.Deserialize(ref br);

                        for (int i = 0, c = snapshot.SpawnCommands.Length; i < c; ++i)
                        {
                            m_SpawnSystem.SpawnQueue.Enqueue(snapshot.SpawnCommands[i]);
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
                int read_bytes = br.GetBytesRead();
                Debug.Assert(message.Length == read_bytes);
            }

            /*
            for (int i = 0, c = SpawnEventQueue.Count; i < c; ++i)
            {
                m_SpawnSystem.SpawnQueue.Enqueue(SpawnEventQueue.Dequeue());
            }

            for (int i = 0, c = MovementEventQueue.Count; i < c; ++i)
            {
                m_SnapshotSystem.MovementUpdates.Enqueue(MovementEventQueue.Dequeue());
            }

            for (int i = 0, c = DespawnEventQueue.Count; i < c; ++i)
            {
                m_DespawnSystem.DespawnQueue.Enqueue(DespawnEventQueue.Dequeue().id);
            }
            */
        }
    }
}
