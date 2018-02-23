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

            m_Data = (byte*)data;
            m_Length = bytes * 8;

            m_ScratchBuffer = m_Data[0];
        }

        public unsafe uint ReadBits(int bits)
        {
            while (m_BitIndex < 32)
            {
                m_ScratchBuffer |= (ulong)m_Data[m_ByteIndex++] << m_BitIndex;
                m_BitIndex += 8;
            }
            return ReadBitsInternal(bits);
        }

        uint ReadBitsInternal(int bits)
        {
            var data = m_ScratchBuffer & (((ulong)1 << bits) - 1);
            m_ScratchBuffer >>= bits;
            m_BitIndex -= bits;
            return (uint)data;
        }

        public void ReadAlign()
        {
            int remainderBits = m_BitIndex % 8;
            if (remainderBits != 0)
            {
                var value = ReadBitsInternal(remainderBits);
            }
            m_ByteIndex -= m_BitIndex / 8;
            m_BitIndex = 0;
            m_ScratchBuffer = 0;
        }

        public unsafe void ReadBytes(byte* data, int length)
        {
            if (GetBytesRead() + length < (m_Length * 8))
            {
                throw new System.ArgumentOutOfRangeException();
            }
            ReadAlign();
            UnsafeUtility.MemCpy(data, m_Data + m_ByteIndex, length);
            m_ByteIndex += length;
        }

        public int GetAlignBits()
        {
            return (8 - GetBitsRead() % 8) % 8;
        }

        public int GetBitsRead()
        {
            return m_ByteIndex * 8 - m_BitIndex;
        }

        public int GetBytesRead()
        {
            return AlignToPowerOfTwo(GetBitsRead(), 8) / 8;
        }

        static int AlignToPowerOfTwo(int value, int alignment)
        {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        unsafe byte* m_Data;
        int m_Length;
        ulong m_ScratchBuffer;
        int m_ByteIndex;
        int m_BitIndex;
    }
}
