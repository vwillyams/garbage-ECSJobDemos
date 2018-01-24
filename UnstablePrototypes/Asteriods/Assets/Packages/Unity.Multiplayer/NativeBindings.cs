using System;
using System.Runtime.InteropServices;

namespace Unity.Multiplayer.Native
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct NativeAddress
    {
        const int size = 16;
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(2)] public ushort Port;
        [FieldOffset(4)] public uint   Address;
        [FieldOffset(4)] internal fixed byte GS_EVENT_DATA[size];
        public byte[] Buffer
        {
            get
            {
                byte[] buffer = new byte[16];
                fixed (byte* b = GS_EVENT_DATA)
                {
                    byte* it = b;
                    for (int i = 0; i < buffer.Length; ++i)
                    {
                        buffer[i] = *it++;
                    }
                }
                return buffer;
            }
            set
            {
                fixed (byte* b = GS_EVENT_DATA)
                {
                    byte* it = b;
                    for (int i = 0; i < value.Length; ++i)
                    {
                        *it++ = value[i];
                    }
                }
            }
        }
    }

    public enum AddressType
    {
        kAddressIp4,
        kAddressIp6,
    }

    public static unsafe class NativeBindings
    {
        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void* gamesocket_create(string address, ushort port, SocketConfiguration config);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern void gamesocket_destroy(void* socket);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int gamesocket_connect(void* socket, string address, ushort port);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_disconnect(void* socket, int id);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_listen(void* socket);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_send_to(void* socket, void* buffer, ushort buffer_size, int connection);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_send(void* socket, void* buffer, ushort buffer_size, void* connections, int connection_size);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_receive_event_supplied_buffer(void* socket, void* buffer, out ushort buffer_size, out int connection);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_receive_event(void* socket, void* buffer, out ushort buffer_size, out int connection);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_get_last_socket_error(void* socket);

        [DllImport("gamesocket.native", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gamesocket_get_last_error();
    }
}
