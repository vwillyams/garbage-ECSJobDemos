using UnityEngine;
using UnityEngine.Assertions;

using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Multiplayer
{
    public class BitReader
    {
        public unsafe BitReader(void* data, int bytes)
        {
            Assert.IsTrue(data != null);
            Assert.AreEqual(0, bytes % 4);

            m_Data = (uint*)data;
            m_NumWords = bytes / 4;

            m_NumBits = m_NumWords * 32;
            m_BitsRead = 0;
            m_BitIndex = 0;
            m_WordIndex = 0;
            m_ScratchBuffer = m_Data[0];
            m_Overflow = false;
        }

        public unsafe uint ReadBits(int bits)
        {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(m_BitsRead + bits <= m_NumBits);

            if (m_BitsRead + bits > m_NumBits)
            {
                m_Overflow = true;
                return 0;
            }

            m_BitsRead += bits;

            Assert.IsTrue(m_BitIndex < 32);

            if (m_BitIndex + bits < 32)
            {
                m_ScratchBuffer <<= bits;
                m_BitIndex += bits;
            }
            else
            {
                m_WordIndex++;
                Assert.IsTrue(m_WordIndex <= m_NumWords);
                int a = 32 - m_BitIndex;
                int b = bits - a;
                Assert.IsTrue(a >= 0);
                Assert.IsTrue(b >= 0);
                m_ScratchBuffer <<= a;
                m_ScratchBuffer |= m_Data[m_WordIndex];
                m_ScratchBuffer <<= b;
                m_BitIndex = b;
            }

            uint output = (uint)(m_ScratchBuffer >> 32);

            m_ScratchBuffer &= 0xFFFFFFFF;

            return output;
        }

        public void ReadAlign()
        {
            int remainderBits = m_BitsRead % 8;
            if (remainderBits != 0)
            {
#if DEBUG
                ReadBits(8 - remainderBits);
#else
			uint32_t value = ReadBits( 8 - remainderBits );
			assert( value == 0 );
			assert( m_bitsRead % 8 == 0 );
#endif
            }
        }

        public unsafe void ReadBytes(uint* data, int bytes)
        {
            Assert.AreEqual(0, GetAlignBits());

            if (m_BitsRead + bytes * 8 >= m_NumBits)
            {
                UnsafeUtility.MemClear(data, bytes);
                m_Overflow = true;
                return;
            }

            Assert.IsTrue(m_BitIndex == 0 || m_BitIndex == 8 || m_BitIndex == 16 || m_BitIndex == 24);

            int headBytes = (4 - m_BitIndex / 8) % 4;
            if (headBytes > bytes)
                headBytes = bytes;
            for (int i = 0; i < headBytes; ++i)
                data[i] = ReadBits(8);
            if (headBytes == bytes)
                return;

            Assert.AreEqual(0, GetAlignBits());

            int numWords = (bytes - headBytes) / 4;
            if (numWords > 0)
            {
                Assert.AreEqual(0, m_BitIndex);
                UnsafeUtility.MemCpy((data + headBytes), (m_Data + m_WordIndex), numWords * 4);
                m_BitsRead += numWords * 32;
                m_WordIndex += numWords;
                m_ScratchBuffer = m_Data[m_WordIndex];
            }

            Assert.AreEqual(0, GetAlignBits());

            int tailStart = headBytes + numWords * 4;
            int tailBytes = bytes - tailStart;
            Assert.IsTrue(tailBytes >= 0 && tailBytes < 4);
            for (int i = 0; i < tailBytes; ++i)
                data[tailStart + i] = ReadBits(8);

            Assert.AreEqual(0, GetAlignBits());

            Assert.IsTrue(headBytes + numWords * 4 + tailBytes == bytes);
        }

        public int GetAlignBits()
        {
            return (8 - m_BitsRead % 8) % 8;
        }

        public int GetBitsRead()
        {
            return m_BitsRead;
        }

        public int GetBytesRead()
        {
            return AlignToPowerOfTwo(m_BitsRead, 8) / 8;
        }

        public int GetBitsRemaining()
        {
            return m_NumBits - m_BitsRead;
        }

        public int GetTotalBits()
        {
            return m_NumBits;
        }

        public int GetTotalBytes()
        {
            return m_NumBits * 8;
        }

        public bool IsOverflow()
        {
            return m_Overflow;
        }

        static int AlignToPowerOfTwo(int value, int alignment)
        {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        unsafe uint* m_Data;
        ulong m_ScratchBuffer;
        int m_NumBits;
        int m_NumWords;
        int m_BitsRead;
        int m_BitIndex;
        int m_WordIndex;
        bool m_Overflow;
    }
}