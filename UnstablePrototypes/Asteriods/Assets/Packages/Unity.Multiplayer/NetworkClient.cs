using UnityEngine;

using Unity.Collections;
using Unity.Multiplayer;
using Unity.Mathematics;

/// thoughts
/*
    transport.PopData?
    reads a slice? do we store the slices or do we have a queue of data for each connection
    how do we avoid the fact that we might have to copy data?

    problem statement here is that we dont know to whom data is received on the wire.
    so we cant make a good non-copy pass? would it be benefitial to save the data in a big
    queue and just report the indicies to the clients?

    how do we do this on the server? questions are do we read into a buffer and handle it?
    do we loop over each connection and let it handle data? what if we have 10k connections but
    only 1k is receiving data each frame. what do we do then?

    how about using the connections as entities? e.g. each entity is in essence a connection
    and has components like connection id, buffer, snapshots ...

    entity flow:
        on connect a entity is created, a id is registrated as a component.
        on data the 'server' fills in the buffer.
        on process data something reads the buffer and updates the snapshot component?
        on disconnect the entity is just being removed?

    as a simple beginning maybe just have the connection id component as a reference.
    have a small queue on each client for now.
*/

namespace Unity.Multiplayer
{
    // make it an IComponentData ?
    public struct NetworkConnection
    {
        public NetworkConnection(int id)
        {
            Id = id;
        }

        public int Id;
    }

    public class NetworkClient
    {
        public NetworkClient()
        {
            SocketConfiguration configuration = new SocketConfiguration
            {
                Timeout = uint.MaxValue,
                MaximumConnections = 1
            };
            m_Connection.Id = -1;
            m_Socket = new GameSocket("127.0.0.1", 0, configuration);
            m_ConnectionArray = new NativeArray<int>(1, Allocator.Persistent);

            m_Buffer = new NativeArray<byte>(1024 * 1024, Allocator.Persistent);
            m_DataQueue = new NativeQueue<SliceInformation>(Allocator.Persistent);
        }

        public void Update()
        {
            int connectionId;
            GameSocketEventType eventType;
            int receivedSize = 0;

            bool done = false;
            while (!done)
            {
                var slice = m_Buffer.Slice(m_Offset, GameSocket.Constants.MaxPacketSize);
                eventType = m_Socket.ReceiveEventSuppliedBuffer(slice, out connectionId, out receivedSize);

                switch (eventType)
                {
                    case GameSocketEventType.Connect:
                        {
                            if (m_Connection.Id != -1 && m_Connection.Id == connectionId)
                            {
                                m_State = ConnectionState.Connected;
                                m_ConnectionArray[0] = connectionId;
                            }
                            else
                                throw new System.Exception("ConnectionId does not match");
                        }
                        break;
                    case GameSocketEventType.Data:
                        {
                            var info = new SliceInformation()
                            {
                                offset = m_Offset,
                                length = receivedSize
                            };
                            m_Offset += receivedSize;
                            m_DataQueue.Enqueue(info);
                        }
                        break;
                    case GameSocketEventType.Disconnect:
                        {
                            if (m_Connection.Id != -1 && m_Connection.Id == connectionId)
                            {
                                m_State = ConnectionState.Disconnected;
                            }
                            else
                                throw new System.Exception("ConnectionId does not match");
                        }
                        break;
                    case GameSocketEventType.Empty:
                        {
                            done = true;
                        }
                        break;
                    default:
                        {
                            Debug.Assert(false);
                        }
                        break;
                }
            }
        }

        public void Connect(string address, int port)
        {
            var id = m_Socket.Connect(address, (ushort)port);
            m_Connection = new NetworkConnection(id);
        }

        public bool ReadMessage(out NativeSlice<byte> message)
        {
            if (m_DataQueue.Count == 0)
            {
                message = default(NativeSlice<byte>);
                return false;
            }
            var info = m_DataQueue.Dequeue();
            message = m_Buffer.Slice(info.offset, info.length);
            return true;
        }

        public bool WriteMessage(NativeSlice<byte> message)
        {
            var length = m_Socket.SendData(message, m_ConnectionArray.Slice());
            return message.Length == length;
        }

        public bool Connected
        {
            get { return  m_State == ConnectionState.Connected; }
        }

        public bool IsCreated
        {
            get { return m_ConnectionArray.IsCreated; }
        }

        public void Dispose()
        {
            m_Socket.Dispose();
            if (m_ConnectionArray.IsCreated)
                m_ConnectionArray.Dispose();

            if (m_Buffer.IsCreated)
                m_Buffer.Dispose();
            if (m_DataQueue.IsCreated)
                m_DataQueue.Dispose();
        }

        enum ConnectionState
        {
            Connected,
            Connecting,
            Disconnected
        }

        ConnectionState m_State;
        NetworkConnection m_Connection;

        struct SliceInformation
        {
            public int offset;
            public int length;
        }

        NativeQueue<SliceInformation> m_DataQueue;

        NativeArray<int> m_ConnectionArray;
        GameSocket m_Socket;

        // TODO (michalb): create a circular buffer implementation, as we are duplicating this once again.
        int m_Offset;
        NativeArray<byte> m_Buffer;
    }
}
