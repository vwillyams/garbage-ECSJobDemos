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
    public struct PacketBuffer
    {
        struct SliceInformation
        {
            public int offset;
            public int length;
            public int id;
        }

        NativeQueue<SliceInformation> m_DataQueue;
        NativeArray<byte> m_Buffer;

        int m_Tail, m_Head, m_Length;
        int m_CommitedHead, m_Count;
        int m_ChunkSize;

        public int Count
        {
            get { return m_Count; }
        }

        public PacketBuffer(int chunkSize, int capacity)
        {
            m_ChunkSize = chunkSize;
            m_Length = capacity * m_ChunkSize;
            m_Buffer = new NativeArray<byte>(m_Length, Allocator.Persistent);
            m_DataQueue = new NativeQueue<SliceInformation>(Allocator.Persistent);

            m_Count = m_CommitedHead = m_Head = m_Tail = 0;
        }

        public void Dispose()
        {
            if (m_Buffer.IsCreated)
                m_Buffer.Dispose();
            if (m_DataQueue.IsCreated)
                m_DataQueue.Dispose();
        }

        public NativeSlice<byte> Reserve()
        {
            Debug.Assert(m_CommitedHead == m_Head);

            int offset = 0;

            if ((m_Head + m_ChunkSize > m_Length && m_ChunkSize > m_Tail) ||
                (m_Head + m_ChunkSize == m_Tail && m_Count > 0))
                return default(NativeSlice<byte>);
            else if ((m_Head >= m_Tail && m_Head + m_ChunkSize <= m_Length) ||
                     (m_Head < m_Tail && m_Head + m_ChunkSize < m_Tail))
                offset = m_Head;

            var slice = m_Buffer.Slice(offset, m_ChunkSize);
            m_Head = offset + m_ChunkSize;
            return slice;
        }

        public void Commit(int length, int id)
        {
            if (length == 0)
            {
                m_Head = m_CommitedHead;
                return;
            }

            int offset = m_Head - m_ChunkSize;
            var info = new SliceInformation()
            {
                offset = offset,
                length = length,
                id = id
            };

            m_Head = offset + length;
            m_CommitedHead = m_Head;
            m_DataQueue.Enqueue(info);
            m_Count++;
        }

        public bool TryPeek(out NativeSlice<byte> message, out int id)
        {
            if (m_DataQueue.Count == 0)
            {
                message = default(NativeSlice<byte>);
                id = -1;
                return false;
            }
            var info = m_DataQueue.Peek();
            message = m_Buffer.Slice(info.offset, info.length);
            id = info.id;
            return true;
        }

        public void Pop()
        {
            Debug.Assert(m_DataQueue.Count > 0);
            var info = m_DataQueue.Dequeue();
            m_Tail = info.offset + info.length;
            m_Count--;
        }

    }

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
        PacketBuffer m_PacketBuffer;
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

            m_PacketBuffer = new PacketBuffer(GameSocket.Constants.MaxPacketSize, 100);
        }

        public void Update()
        {
            m_Offset = 0; 
            int connectionId;
            GameSocketEventType eventType;
            int receivedLength = 0;

            bool done = false;
            while (!done)
            {
                var slice = m_PacketBuffer.Reserve();

                //var slice = m_Buffer.Slice(m_Offset, GameSocket.Constants.MaxPacketSize);
                eventType = m_Socket.ReceiveEventSuppliedBuffer(slice, out connectionId, out receivedLength);

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
                            receivedLength = 0;
                        }
                        break;
                    case GameSocketEventType.Data:
                        {
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
                            receivedLength = 0;
                        }
                        break;
                    case GameSocketEventType.Empty:
                        {
                            done = true;
                            receivedLength = 0;
                        }
                        break;
                    default:
                        {
                            Debug.Assert(false);
                        }
                        break;
                }
                m_PacketBuffer.Commit(receivedLength, connectionId);
            }
        }

        public void Connect(string address, int port)
        {
            var id = m_Socket.Connect(address, (ushort)port);
            m_Connection = new NetworkConnection(id);
        }

        //public bool ReadMessage(out NativeSlice<byte> message)
        public bool PeekMessage(out NativeSlice<byte> message)
        {
            int id;
            return m_PacketBuffer.TryPeek(out message, out id);
        }

        public void PopMessage()
        {
            m_PacketBuffer.Pop();
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
            m_PacketBuffer.Dispose();

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
