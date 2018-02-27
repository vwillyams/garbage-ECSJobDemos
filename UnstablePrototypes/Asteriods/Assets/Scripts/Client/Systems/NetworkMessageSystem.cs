using UnityEngine;
using Unity.Entities;

using Unity.Multiplayer;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using PlayerState = PlayerStateComponentData.PlayerState;

namespace Asteriods.Client
{
    public class NetworkMessageSystem : ComponentSystem
    {
        struct SerializableData
        {
            public int Length;
            public ComponentDataArray<PlayerTagComponentData> self;
            public ComponentDataArray<PlayerInputComponentData> input;
        }

        struct Player
        {
            public int Length;
            public ComponentDataArray<PlayerStateComponentData> state;
            public ComponentDataArray<PlayerTagComponentData> self;
        }

        [Inject]
        Player player;

        [Inject]
        SerializableData data;

        NetworkClient m_NetworkClient;
        NativeArray<byte> m_Buffer;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_NetworkClient = ClientSettings.Instance().networkClient;
            m_Buffer = new NativeArray<byte>(GameSocket.Constants.MaxPacketSize, Allocator.Persistent);
        }

        override protected void OnDestroyManager()
        {
            base.OnDestroyManager();
            if (m_Buffer.IsCreated)
                m_Buffer.Dispose();
        }

        unsafe override protected void OnUpdate()
        {
            if (player.Length == 0 || player.state[0].State < (int)PlayerState.Loading)
                return;

            if (player.state[0].State == (int)PlayerState.Loading)
            {
                var bw = new ByteWriter(m_Buffer.GetUnsafePtr(), m_Buffer.Length);
                bw.Write((byte)AsteroidsProtocol.ReadyReq);

                m_NetworkClient.WriteMessage(m_Buffer.Slice(0, bw.GetBytesWritten()));
                return;
            }

            if (data.Length == 0)
                return;

            using (var command = new Command(0, Allocator.Temp))
            {
                for (int i = 0; i < data.Length; ++i)
                {
                    /*
                    if (data.input[i].left == 0 &&
                        data.input[i].right == 0 &&
                        data.input[i].shoot == 0 &&
                        data.input[i].thrust == 0)
                        continue;
                    */
                    command.InputCommands.Add(data.input[i]);
                }
                if (command.InputCommands.Length == 0)
                    return;

                var bw = new ByteWriter(m_Buffer.GetUnsafePtr(), m_Buffer.Length);
                bw.Write((byte)AsteroidsProtocol.Command);
                command.Serialize(ref bw);

                var slice = m_Buffer.Slice(0, bw.GetBytesWritten());
                m_NetworkClient.WriteMessage(slice);
            }
        }
    }
}
