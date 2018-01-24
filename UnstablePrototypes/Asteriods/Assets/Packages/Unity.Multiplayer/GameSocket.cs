using System;
using Unity.Collections;
using Unity.Multiplayer.Native;

using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Multiplayer
{

    [StructLayout(LayoutKind.Sequential)]
    public struct SocketConfiguration
    {
        // TODO: change to ints for sizes
        public ushort SendBufferSize;
        public ushort RecvBufferSize;
        public uint Timeout;
        public uint MaximumConnections;
    }

    public enum GameSocketEventType
    {
        Empty,
        Data,
        Connect,
        Disconnect
    }

    public enum GameSocketError
    {
        InternalSocketError     = -1000,
        BadConnectionId         = -1001,
        ConnectionWrongState    = -1002,
        Success                 = 0
    }

    public class GameSocketException : Exception
    {
        public GameSocketError Error { get; internal set; }
        public int InternalError { get; internal set; }
        public GameSocketException(string message)
        : base(message)
        {

        }

        public GameSocketException(string message, GameSocketError error)
        : base(message)
        {
            Error = error;
        }
    }

    /* ********************************************************************** */

    public unsafe struct GameSocket : IDisposable
    {
        public struct Constants
        {
            public static readonly int HeaderSize = 14;
            public static readonly int MaxPacketSize = 1472;
        }
        void* m_Socket;
        NativeArray<byte> m_Buffer;

        //AtomicSafetyHandle m_Safety;
        //DisposeSentinel m_DisposeSentinel;

        /* ****************************************************************** */

        // (Main thread only)
        /* ****************************************************************** */

        public GameSocket(string address, ushort port, SocketConfiguration configuration)
        {
            Listening = false;
            m_Socket = NativeBindings.gamesocket_create(address, port, configuration);
            if (m_Socket == null)
            {
                var e = new GameSocketException("Create GameSocket failed");
                e.InternalError = NativeBindings.gamesocket_get_last_error();
                throw e;
            }
            m_Buffer = new NativeArray<byte>(Constants.MaxPacketSize, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (m_Socket != null)
            {
                NativeBindings.gamesocket_destroy(m_Socket);
                m_Socket = null;
            }
            if (m_Buffer.IsCreated)
            {
                m_Buffer.Dispose();
            }
        }

        public int Close()
        {
            return 0;
        }

        /* ****************************************************************** */

        public bool Listening { get; internal set; }

        public void ListenForConnections()
        {
            NativeBindings.gamesocket_listen(m_Socket);
            Listening = true;
        }

        public int Connect(string address, ushort port)
        {
            int result = NativeBindings.gamesocket_connect(m_Socket, address, port);
            if (result < 0)
            {
                var code = (((GameSocketError)result == GameSocketError.InternalSocketError) ? NativeBindings.gamesocket_get_last_socket_error(m_Socket) : result);
                var e = new GameSocketException("Error during connect " + code);
                e.InternalError = code;
                throw e;
            }
            return result;
        }

        public void Disconnect(int connection)
        {
            NativeBindings.gamesocket_disconnect(m_Socket, connection);
        }

        public int SendData(NativeSlice<byte> buffer, int connection)
        {
            var result = NativeBindings.gamesocket_send_to(m_Socket, NativeSliceUnsafeUtility.GetUnsafePtr(buffer), (ushort)buffer.Length, connection);
            if (result < 0)
            {
                var code = (((GameSocketError)result == GameSocketError.InternalSocketError) ? NativeBindings.gamesocket_get_last_socket_error(m_Socket) : result);
                var e = new GameSocketException("Error during send " + code);
                e.InternalError = code;
                throw e;
            }
            return result;
        }

        public int SendData(NativeSlice<byte> buffer, NativeSlice<int> connections)
        {
            var result = NativeBindings.gamesocket_send(m_Socket, NativeSliceUnsafeUtility.GetUnsafePtr(buffer), (ushort)buffer.Length, NativeSliceUnsafeUtility.GetUnsafePtr(connections), connections.Length);
            if (result < 0)
            {
                var code = (((GameSocketError)result == GameSocketError.InternalSocketError) ? NativeBindings.gamesocket_get_last_socket_error(m_Socket) : result);
                var e = new GameSocketException("Error during send " + code);
                e.InternalError = code;
                throw e;
            }
            return result;
        }

        GameSocketEventType ReceiveEvent(NativeSlice<byte> buffer, out int readBytes, out int connection, out ulong packetSequence, out GameSocketError error)
        {
            throw new NotImplementedException();
        }

        public GameSocketEventType ReceiveEvent(out NativeSlice<byte> slice, out int connection)
        {
            ushort bufferSize = (ushort)m_Buffer.Length;
            int result = 0;
            if ((result = NativeBindings.gamesocket_receive_event_supplied_buffer(m_Socket, m_Buffer.GetUnsafePtr(), out bufferSize, out connection)) < 0)
            {
               var code = (((GameSocketError)result == GameSocketError.InternalSocketError) ? NativeBindings.gamesocket_get_last_socket_error(m_Socket) : result);
                var e = new GameSocketException("Error during recv " + code);
                e.InternalError = code;
                throw e;
            }

            if ((GameSocketEventType)result == GameSocketEventType.Data)
                slice = new NativeSlice<byte>(m_Buffer, 0, bufferSize);
            else
                slice = default(NativeSlice<byte>);
            return (GameSocketEventType)result;
        }

        public unsafe GameSocketEventType ReceiveEventSuppliedBuffer(NativeSlice<byte> slice, out int connection, out int size)
        {
            ushort bufferSize = (ushort)slice.Length;
            int result = 0;
            void* ptr = slice.GetUnsafePtr();
            if ((result = NativeBindings.gamesocket_receive_event_supplied_buffer(m_Socket, ptr, out bufferSize, out connection)) < 0)
            {
                Debug.Log("unsafe ptr = " + new IntPtr(ptr).ToString());
                var code = (((GameSocketError)result == GameSocketError.InternalSocketError) ? NativeBindings.gamesocket_get_last_socket_error(m_Socket) : result);
                var e = new GameSocketException("Error during recv " + code);
                e.InternalError = code;
                throw e;
            }
            size = bufferSize;
            return (GameSocketEventType)result;
        }
    }
}
