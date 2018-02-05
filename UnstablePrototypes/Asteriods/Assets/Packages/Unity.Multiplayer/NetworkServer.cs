using UnityEngine;

using Unity.Collections;
using Unity.Multiplayer;

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
    public class NetworkServer
    {
        public NetworkServer(string address, int port, int maximumConnections = 10)
        {
            SocketConfiguration configuration = new SocketConfiguration
            {
                Timeout = uint.MaxValue,
                MaximumConnections = (uint)maximumConnections
            };
            m_Socket = new GameSocket(address, (ushort)port, configuration);
            m_Socket.ListenForConnections();

            m_ConnectQueue = new NativeQueue<NetworkConnection>(Allocator.Persistent);
            m_DisconnectQueue = new NativeQueue<NetworkConnection>(Allocator.Persistent);

            m_Buffer = new NativeArray<byte>(1024 * 1024, Allocator.Persistent);
            m_DataQueue = new NativeQueue<SliceInformation>(Allocator.Persistent);

            m_Connections = new NativeList<int>(maximumConnections, Allocator.Persistent);
        }

        public bool IsCreated
        {
            get { return m_Connections.IsCreated || m_ConnectQueue.IsCreated || m_DisconnectQueue.IsCreated || m_Buffer.IsCreated || m_DataQueue.IsCreated; }
        }

        public void Dispose()
        {
            m_Socket.Dispose();

            if (m_Connections.IsCreated)
                m_Connections.Dispose();
            if (m_ConnectQueue.IsCreated)
                m_ConnectQueue.Dispose();
            if (m_DisconnectQueue.IsCreated)
                m_DisconnectQueue.Dispose();

            if (m_DataQueue.IsCreated)
                m_DataQueue.Dispose();
            if (m_Buffer.IsCreated)
                m_Buffer.Dispose();
        }

        public void Update()
        {
            Debug.Assert(m_Socket.Listening);
            m_Offset = 0;

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
                            var connection = new NetworkConnection(connectionId);
                            m_Connections.Add(connectionId);
                            m_ConnectQueue.Enqueue(connection);
                        }
                        break;
                    case GameSocketEventType.Data:
                        {
                            var info = new SliceInformation()
                            {
                                offset = m_Offset,
                                length = receivedSize,
                                id = connectionId
                            };
                            m_Offset += receivedSize;
                            m_DataQueue.Enqueue(info);
                        }
                        break;
                    case GameSocketEventType.Disconnect:
                        {
                            var connection = new NetworkConnection(connectionId);
                            m_DisconnectQueue.Enqueue(connection);

                            // remove from the connection list
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

        public bool TryPopConnectionQueue(out NetworkConnection connection)
        {
            return m_ConnectQueue.TryDequeue(out connection);
        }

        public bool TryPopDisconnectionQueue(out NetworkConnection connection)
        {
            return m_DisconnectQueue.TryDequeue(out connection);
        }

        public bool ReadMessage(out NativeSlice<byte> message, out int connectionId)
        {
            if (m_DataQueue.Count == 0)
            {
                message = default(NativeSlice<byte>);
                connectionId = -1;
                return false;
            }
            var info = m_DataQueue.Dequeue();
            message = m_Buffer.Slice(info.offset, info.length);
            connectionId = info.id;
            return true;
        }

        public void WriteMessage(NativeSlice<byte> message, int connection)
        {
            m_Socket.SendData(message, connection);
        }

        public void WriteMessage(NativeSlice<byte> message)
        {
            m_Socket.SendData(message, ((NativeArray<int>)m_Connections).Slice());
        }

        struct SliceInformation
        {
            public int offset;
            public int length;
            public int id;
        }


        int m_Offset;
        NativeArray<byte> m_Buffer;
        NativeList<int> m_Connections;
        NativeQueue<SliceInformation> m_DataQueue;
        GameSocket m_Socket;
        NativeQueue<NetworkConnection> m_ConnectQueue;
        NativeQueue<NetworkConnection> m_DisconnectQueue;
    }
}
