using System;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;

using Unity.Multiplayer;
using Unity.Mathematics;


using Random = UnityEngine.Random;
using Unity.Collections.LowLevel.Unsafe;


namespace Unity.Multiplayer.Tests
{
    public class PacketBufferTests
    {
        const int k_ChunkSize = 8;
        const int k_Arraysize = 64;

        PacketBuffer m_Buffer;

        [SetUp]
        public void Setup()
        {
            m_Buffer = new PacketBuffer(k_ChunkSize, k_Arraysize);
        }

        [TearDown]
        public void TearDown()
        {
            m_Buffer.Dispose();
        }

        [Test]
        public unsafe void TestAddRemoveDataToPacketBuffer()
        {

            for (int i = 0; i < 10000; ++i)
            {
                var outSlice = m_Buffer.Reserve();
                var bw = new ByteWriter(outSlice.GetUnsafePtr(), outSlice.Length);

                bw.Write(1);
                bw.Write((byte)1);

                m_Buffer.Commit(bw.GetBytesWritten(), 0);

                int id;
                NativeSlice<byte> inSlice;
                Assert.True(m_Buffer.TryPeek(out inSlice, out id));
                var br = new ByteReader(inSlice.GetUnsafePtr(), inSlice.Length);

                Assert.AreEqual(1, br.ReadInt());
                Assert.AreEqual((byte)1, br.ReadByte());

                m_Buffer.Pop();
            }
        }

        [Test]
        public unsafe void TestPacketBufferCanGetFull()
        {
            int packetsize = 3;
            int it = ((k_ChunkSize * k_Arraysize - k_ChunkSize) / packetsize) + 1;
            for (int i = 0; i < it; ++i)
            {
                var outSlice = m_Buffer.Reserve();
                var bw = new ByteWriter(outSlice.GetUnsafePtr(), outSlice.Length);

                //bw.Write(1.0f);
                bw.Write((byte)1.0);
                bw.Write((byte)1.0);
                bw.Write((byte)1.0);

                m_Buffer.Commit(bw.GetBytesWritten(), 0);
            }
            var slice = m_Buffer.Reserve();
            Debug.Log(slice.Length);
        }

        [Test]
        public unsafe void TestPacketBufferStress(
            [NUnit.Framework.Range(1,8,1)] int packetSize)
        {
            int id = 0;
            int max = ((k_ChunkSize * k_Arraysize - k_ChunkSize) / packetSize) + 1;
            for (int i = 0; i < 10000; ++i)
            {
                var slice = m_Buffer.Reserve();

                // Reserve Failed
                if (slice.Length == 0)
                {
                    var count = Random.Range(0, m_Buffer.Count);
                    for (int j = 0; j < count; j++)
                    {
                        NativeSlice<byte> inSlice;
                        Assert.True(m_Buffer.TryPeek(out inSlice, out id));
                        var br = new ByteReader(inSlice.GetUnsafePtr(), inSlice.Length);
                        ReadBytes(packetSize, ref br);
                        m_Buffer.Pop();
                    }
                }
                else
                {
                    var bw = new ByteWriter(slice.GetUnsafePtr(), slice.Length);
                    WriteBytes(packetSize, ref bw);
                    m_Buffer.Commit(packetSize, id);
                }
            }
            for (int j = 0; j < m_Buffer.Count; j++)
            {
                NativeSlice<byte> inSlice;
                Assert.True(m_Buffer.TryPeek(out inSlice, out id));
                var br = new ByteReader(inSlice.GetUnsafePtr(), inSlice.Length);
                ReadBytes(packetSize, ref br);
                m_Buffer.Pop();
            }
        }

        void WriteBytes(int length, ref ByteWriter bw)
        {
            for (int i = 0; i < length; ++i)
            {
                bw.Write((byte)0xfe);
            }
        }
        void ReadBytes(int length, ref ByteReader br)
        {
            for (int i = 0; i < length; ++i)
            {
                Assert.AreEqual((byte)0xfe, br.ReadByte());
            }
        }

    }
}
