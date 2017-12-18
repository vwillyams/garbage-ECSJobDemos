using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;
using Unity.Multiplayer;
using Unity.GameCode;

namespace Asteriods.Client
{
    public class NetworkEventSystem : ComponentSystem
    {
        // HACK (2017-12-08, lifetime 4 weeks or until proper protocol implemented.)
        public static NativeQueue<SpawnCommand> SpawnEventQueue;
        public static NativeQueue<DespawnCommand> DespawnEventQueue;
        public static NativeQueue<MovementData> MovementEventQueue;

        [Inject]
        SpawnSystem m_SpawnSystem;

        [Inject]
        DespawnSystem m_DespawnSystem;

        [Inject]
        SnapshotSystem m_SnapshotSystem;

        GameSocket m_Socket;
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

            SpawnEventQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            MovementEventQueue = new NativeQueue<MovementData>(128, Allocator.Persistent);
            DespawnEventQueue = new NativeQueue<DespawnCommand>(128, Allocator.Persistent);
            Debug.Assert(SpawnEventQueue.IsCreated);
            Debug.Assert(MovementEventQueue.IsCreated);
            Debug.Assert(DespawnEventQueue.IsCreated);

            m_Socket = ClientSettings.Instance().socket;

            m_StateMachine = new StateMachine<ClientState>();
            m_StateMachine.Add(ClientState.Connecting, EnterConnectingState, UpdateConnectingState, null);
            m_StateMachine.Add(ClientState.Loading, null, UpdateLoadingState, null);
            m_StateMachine.Add(ClientState.Playing, null, UpdatePlayState, null);

            m_StateMachine.SwitchTo(ClientState.Connecting);
        }

        override protected void OnDestroyManager()
        {
            if (SpawnEventQueue.IsCreated)
                SpawnEventQueue.Dispose();
            if (MovementEventQueue.IsCreated)
                MovementEventQueue.Dispose();
            if (DespawnEventQueue.IsCreated)
                DespawnEventQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            m_StateMachine.Update();
        }

        void EnterConnectingState()
        {
            m_Socket.Connect(ClientSettings.Instance().serverAddress, ClientSettings.Instance().serverPort);
        }

        void UpdateConnectingState()
        {
            NativeSlice<byte> outBuffer;
            GameSocketEventType eventType;
            int connectionId;
            if ((eventType = m_Socket.ReceiveEvent(out outBuffer, out connectionId)) == GameSocketEventType.Connect)
            {
                m_StateMachine.SwitchTo(ClientState.Loading);
            }
        }

        void UpdateLoadingState()
        {
            m_StateMachine.SwitchTo(ClientState.Playing);
        }

        void UpdatePlayState()
        {
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
        }
    }
}