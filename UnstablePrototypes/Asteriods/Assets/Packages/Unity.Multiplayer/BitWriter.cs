using UnityEngine;
using UnityEngine.Assertions;

using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Multiplayer
{
    public struct BitWriter
    {
        unsafe public BitWriter(void* data, int bytes)
        {
            Assert.IsTrue(data != null);

            m_Data = (byte*)data;
            m_Length = bytes * 8;

            m_ScratchBuffer = 0;
            m_BitIndex = 0;
            m_ByteIndex = 0;
            UnsafeUtility.MemClear(m_Data, bytes);
        }

        public unsafe void WriteBits(uint value, int bits)
        {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Debug.Assert(GetBitsWritten() + bits <= m_Length);
            Assert.AreEqual(value, value & ((1ULL << bits) - 1), "WriteBits value is larger than the bits");
            if (GetBitsWritten() + bits > m_Length)
                return;

            m_ScratchBuffer |= (ulong)(value) << m_BitIndex;
            m_BitIndex += bits;

            while (m_BitIndex >= 8)
            {
                m_Data[m_ByteIndex++] = (byte)(m_ScratchBuffer & 0xff);
                m_ScratchBuffer >>= 8;
                m_BitIndex -= 8;
            }
        }

        public void WriteAlign()
        {
            if (m_BitIndex > 0)
                WriteBits(0, 8 - m_BitIndex);
        }

        public unsafe void WriteBytes(byte* data, int bytes)
        {
        }

        public int GetAlignBits()
        {
            return (8 - GetBitsWritten() % 8) % 8;
        }

        public int GetBitsWritten()
        {
            return m_ByteIndex * 8 - m_BitIndex;
        }

        public int GetBitsAvailable()
        {
            return m_Length - GetBitsWritten();
        }

        public int GetBytesWritten()
        {
            return (GetBitsWritten() + 7) / 8;
        }

        unsafe byte* m_Data;
        int m_Length;
        ulong m_ScratchBuffer;
        int m_ByteIndex;
        int m_BitIndex;
    }
}
