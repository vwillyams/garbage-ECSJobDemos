using System.Runtime.InteropServices;
using UnityEngine;

using Unity.Mathematics;

namespace Unity.Multiplayer
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntFloat
    {
        [FieldOffset(0)]
        public float floatValue;

        [FieldOffset(0)]
        public uint intValue;

        [FieldOffset(0)]
        public double doubleValue;

        [FieldOffset(0)]
        public ulong longValue;
    }

    public class ByteWriter
    {
        public unsafe ByteWriter(void* data, int bytes)
        {
            m_Writer = new BitWriter(data, bytes);
        }
        public ByteWriter(BitWriter writer)
        {
            m_Writer = writer;
        }

        public void Write(byte value)
        {
            m_Writer.WriteBits((uint)value, sizeof(byte) * 8);
        }

        public void Write(short value)
        {
            m_Writer.WriteBits((uint)value, sizeof(short) * 8);
        }
        public void Write(ushort value)
        {
            m_Writer.WriteBits(value, sizeof(ushort) * 8);
        }

        public void Write(int value)
        {
            m_Writer.WriteBits((uint)value, sizeof(int) * 8);
        }

        public void Write(uint value)
        {
            m_Writer.WriteBits(value, sizeof(uint) * 8);
        }

        public void Write(int3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        public void Write(float value)
        {
            UIntFloat uf = new UIntFloat();
            uf.floatValue = value;
            Write((int)uf.intValue);
        }
        public void Write(float2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        public void Write(float3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        public int GetBytesWritten()
        {
            return m_Writer.GetBytesWritten();
        }

        BitWriter m_Writer;
    }
}