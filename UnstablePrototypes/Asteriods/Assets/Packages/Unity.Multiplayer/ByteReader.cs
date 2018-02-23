using UnityEngine;

using Unity.Mathematics;

namespace Unity.Multiplayer
{
    public class ByteReader
    {
        public unsafe ByteReader(void* data, int bytes)
        {
            m_Reader = new BitReader(data, bytes);
        }

        public ByteReader(BitReader reader)
        {
            m_Reader = reader;
        }

        public byte ReadByte()
        {
            return (byte)m_Reader.ReadBits(sizeof(byte) * 8);
        }

        public unsafe void ReadBytes(byte* data, int length)
        {
            m_Reader.ReadBytes(data, length);
        }

        public short ReadShort()
        {
            return (short)m_Reader.ReadBits(sizeof(short) * 8);
        }

        public int ReadInt()
        {
            return (int)m_Reader.ReadBits(sizeof(int) * 8);
        }
        public uint ReadUInt()
        {
            return m_Reader.ReadBits(sizeof(int) * 8);
        }
        public float ReadFloat()
        {
            UIntFloat uf = new UIntFloat();
            uf.intValue = (uint)ReadInt();
            return uf.floatValue;
        }

        public int3 ReadInt3()
        {
            int3 i3;
            i3.x = ReadInt();
            i3.y = ReadInt();
            i3.z = ReadInt();
            return i3;
        }

        public float2 ReadFloat2()
        {
            float2 f2;
            f2.x = ReadFloat();
            f2.y = ReadFloat();
            return f2;
        }
        public float3 ReadFloat3()
        {
            float3 f3;
            f3.x = ReadFloat();
            f3.y = ReadFloat();
            f3.z = ReadFloat();
            return f3;
        }

        public int GetBytesRead()
        {
            return m_Reader.GetBytesRead();
        }

        BitReader m_Reader;
    }
}
