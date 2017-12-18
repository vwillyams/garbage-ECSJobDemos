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
            Assert.AreEqual(0, bytes % 4);

            m_Data = (uint*)data;
            m_NumWords = bytes / 4;
            m_NumBits = m_NumWords * 32;

            m_BitsWritten = 0;
            m_ScratchBuffer = 0;
            m_BitIndex = 0;
            m_WordIndex = 0;
            m_Overflow = false;
            UnsafeUtility.MemClear(m_Data, bytes);
        }

        public unsafe void WriteBits(uint value, int bits)
        {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(m_BitsWritten + bits <= m_NumBits);
            Assert.AreEqual(value, value & ((1ULL << bits) - 1), "WriteBits value is larger than the bits");

            if (m_BitsWritten + bits > m_NumBits)
            {
                m_Overflow = true;
                return;
            }

            m_ScratchBuffer |= (ulong)(value) << (64 - m_BitIndex - bits);

            m_BitIndex += bits;

            if (m_BitIndex >= 32)
            {
                Assert.IsTrue(m_WordIndex < m_NumWords);
                m_Data[m_WordIndex] = (uint)(m_ScratchBuffer >> 32);
                m_ScratchBuffer <<= 32;
                m_BitIndex -= 32;
                m_WordIndex++;
            }

            m_BitsWritten += bits;
        }

        public void WriteAlign()
        {
            int remainderBits = m_BitsWritten % 8;
            if (remainderBits != 0)
            {
                uint zero = 0;
                WriteBits(zero, 8 - remainderBits);
                Assert.AreEqual(0, m_BitsWritten % 8);
            }
        }

        public unsafe void WriteBytes(byte* data, int bytes)
        {
            Assert.AreEqual(0, GetAlignBits());
            if (m_BitsWritten + bytes * 8 >= m_NumBits)
            {
                m_Overflow = true;
                return;
            }

            Assert.IsTrue(m_BitIndex == 0 || m_BitIndex == 8 || m_BitIndex == 16 || m_BitIndex == 24);

            int headBytes = (4 - m_BitIndex / 8) % 4;
            if (headBytes > bytes)
                headBytes = bytes;
            for (int i = 0; i < headBytes; ++i)
                WriteBits(data[i], 8);
            if (headBytes == bytes)
                return;

            Assert.AreEqual(0, GetAlignBits());

            int numWords = (bytes - headBytes) / 4;
            if (numWords > 0)
            {
                Assert.AreEqual(0, m_BitIndex);
                UnsafeUtility.MemCpy((m_Data + m_WordIndex), (data + headBytes), numWords * 4);
                m_BitsWritten += numWords * 32;
                m_WordIndex += numWords;
                m_ScratchBuffer = 0;
            }

            Assert.AreEqual(0, GetAlignBits());

            int tailStart = headBytes + numWords * 4;
            int tailBytes = bytes - tailStart;
            Assert.IsTrue(tailBytes >= 0 && tailBytes < 4);
            for (int i = 0; i < tailBytes; ++i)
                WriteBits(data[tailStart + i], 8);

            Assert.AreEqual(0, GetAlignBits());
            Assert.AreEqual(bytes, headBytes + numWords * 4 + tailBytes);
        }

        public unsafe void FlushBits()
        {
            if (m_BitIndex != 0)
            {
                Assert.IsTrue(m_WordIndex < m_NumWords);
                if (m_WordIndex >= m_NumWords)
                {
                    m_Overflow = true;
                    return;
                }
                m_Data[m_WordIndex++] = (uint)(m_ScratchBuffer >> 32);
            }
        }

        public int GetAlignBits()
        {
            return (8 - m_BitsWritten % 8) % 8;
        }

        public int GetBitsWritten()
        {
            return m_BitsWritten;
        }

        public int GetBitsAvailable()
        {
            return m_NumBits - m_BitsWritten;
        }

        public unsafe byte* GetData()
        {
            return (byte*)m_Data;
        }

        public int GetBytesWritten()
        {
            return m_WordIndex * 4;
        }

        public int GetTotalBytes()
        {
            return m_NumWords * 4;
        }

        public bool IsOverflow()
        {
            return m_Overflow;
        }

        unsafe uint* m_Data;
        ulong m_ScratchBuffer;
        int m_NumBits;
        int m_NumWords;
        int m_BitsWritten;
        int m_BitIndex;
        int m_WordIndex;
        bool m_Overflow;
    }
}