using System;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;

using Unity.Multiplayer;
using Unity.Mathematics;

using Unity.Collections.LowLevel.Unsafe;


namespace Unity.Multiplayer.Tests
{
    public class NetworkByteWriterTest
    {
        NativeArray<byte> m_NativeArray;
        NativeSlice<byte> m_NativeSlice;
        const int k_Arraysize = 32000;

        [SetUp]
        public void Setup()
        {
            m_NativeArray = new NativeArray<byte>(k_Arraysize, Allocator.Persistent);
            m_NativeSlice = new NativeSlice<byte>(m_NativeArray);
        }

        [TearDown]
        public void TearDown()
        {
            m_NativeArray.Dispose();
        }

        [Test]
        public unsafe void TestWrite_AFewValues_ShouldReadAFewValues()
        {
            BitWriter bw = new BitWriter();
            ByteWriter writer = new ByteWriter(m_NativeArray.GetUnsafePtr(), m_NativeArray.Length);

            writer.Write((int)100);

            float3 p = new float3()
            {
                x = 1.0f,
                y = 2.0f,
                z = 3.0f
            };

            float3 f = new float3()
            {
                x = 1.1f,
                y = 2.2f,
                z = 3.3f
            };

            for (int i = 0; i < 100; i++)
            {
                writer.Write((int)i);
                writer.Write(p);
                writer.Write(f);
            }

            int size = writer.GetBytesWritten();

            ByteReader reader = new ByteReader(m_NativeArray.GetUnsafePtr(), size);

            int i100 = reader.ReadInt();
            Assert.AreEqual(i100, 100);

            for (int i = 0; i < 100; i++)
            {
                int idx1 = reader.ReadInt();
                float3 p1 = reader.ReadFloat3();
                float3 f1 = reader.ReadFloat3();
                Assert.AreEqual(idx1, i);
                Assert.AreEqual(p1, p);
                Assert.AreEqual(f1, f);
            }

        }

        [Test]
        public unsafe void TestAlignWriteBytes()
        {
            ByteWriter writer = new ByteWriter(m_NativeArray.GetUnsafePtr(), m_NativeArray.Length);
            writer.Write((byte)1);
            writer.Write((byte)1);

            Assert.AreEqual(16, writer.GetBitsWritten());
            Assert.AreEqual(2, writer.GetBytesWritten());
        }

        [Test]
        public unsafe void TestWriteBytesWritten()
        {
            ByteWriter writer = new ByteWriter(m_NativeArray.GetUnsafePtr(), m_NativeArray.Length);

            Assert.AreEqual(0, writer.GetBytesWritten());

            writer.Write(100);
            Assert.AreEqual(4, writer.GetBytesWritten());

            writer.Write(200);
            Assert.AreEqual(8, writer.GetBytesWritten());

            writer.Write(300U);
            Assert.AreEqual(12, writer.GetBytesWritten());

            writer.Write(400);
            Assert.AreEqual(16, writer.GetBytesWritten());

            ByteReader reader = new ByteReader(m_NativeArray.GetUnsafePtr(), m_NativeArray.Length);

            Assert.AreEqual(0, reader.GetBytesRead());

            Assert.AreEqual(100, reader.ReadInt());
            Assert.AreEqual(4, reader.GetBytesRead());

            Assert.AreEqual(200, reader.ReadInt());
            Assert.AreEqual(8, reader.GetBytesRead());

            Assert.AreEqual(300U, reader.ReadUInt());
            Assert.AreEqual(12, reader.GetBytesRead());

            Assert.AreEqual(400, reader.ReadInt());
            Assert.AreEqual(16, reader.GetBytesRead());
        }
    }
}
