using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Properties.Serialization;
using UnityEngine;
using Unity.Properties;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Unity.Entities.Properties.Tests
{
    public struct TestComponent : IComponentData
    {
        public float x;
    }

    public struct TestComponent2 : IComponentData
    {
        public int x;
        public byte b;
    }

    /// <summary>
    /// Helper class for high level use cases
    /// @NOTE This will be included in the Properties API package eventually.
    /// </summary>
    public static class JsonSerializer
    {
        public static string Serialize<TContainer>(TContainer container)
            where TContainer : struct, IPropertyContainer
        {
            var visitor = new JsonVisitor { StringBuffer = new StringBuffer(4096) };
            Visit(container, visitor);
            return visitor.StringBuffer.ToString();
        }

        public static void Visit<TContainer>(TContainer container, JsonVisitor visitor)
            where TContainer : struct, IPropertyContainer
        {
            WritePrefix(visitor);
            container.PropertyBag.VisitStruct(ref container, visitor);
            WriteSuffix(visitor);
        }

        /// <summary>
        /// Writes the BeginObject scope
        /// </summary>
        /// <param name="visitor"></param>
        /// <returns></returns>
        private static void WritePrefix(JsonPropertyVisitor visitor)
        {
            var buffer = visitor.StringBuffer;
            buffer.Append(' ', JsonPropertyVisitor.Style.Space * visitor.Indent);
            buffer.Append("{\n");
            visitor.Indent++;
        }

        /// <summary>
        /// Writes the CloseObject scope
        /// </summary>
        private static void WriteSuffix(JsonPropertyVisitor visitor)
        {
            var buffer = visitor.StringBuffer;
            visitor.Indent--;

            buffer.Length -= 2;
            buffer.Append("\n");
            buffer.Append(' ', JsonPropertyVisitor.Style.Space * visitor.Indent);
            buffer.Append("}");
        }
    }

    [TestFixture]
    public sealed class EntitySerializationTests
    {
        private World 			m_PreviousWorld;
        private World 			m_World;
        private EntityManager   m_Manager;

        [SetUp]
        public void Setup()
        {
            m_PreviousWorld = World.Active;
            m_World = World.Active = new World ("Test World");
            m_Manager = m_World.GetOrCreateManager<EntityManager> ();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Manager != null)
            {
                m_World.Dispose();
                m_World = null;

                World.Active = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = null;
            }
        }

        /// <summary>
        /// Writes an entity to json
        /// </summary>
        [Test]
        public void SimpleFlat()
        {
            var entity = m_Manager.CreateEntity(typeof(TestComponent), typeof(TestComponent2));

            var testComponent = m_Manager.GetComponentData<TestComponent>(entity);
            testComponent.x = 123f;
            m_Manager.SetComponentData(entity, testComponent);

            var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entity));
            Debug.Log(json);
        }

        private struct NestedComponent : IComponentData
        {
            public TestComponent test;
        }

        [Test]
        public void SimpleNested()
        {
            var entity = m_Manager.CreateEntity(typeof(NestedComponent));

            var nestedComponent = m_Manager.GetComponentData<NestedComponent>(entity);
            nestedComponent.test.x = 123f;
            m_Manager.SetComponentData(entity, nestedComponent);

            var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entity));
            Debug.Log(json);
        }

        public struct MathComponent : IComponentData
        {
            public float2 v2;
            public float3 v3;
            public float4 v4;
            public float2x2 m2;
            public float3x3 m3;
            public float4x4 m4;
        }

        [Test]
        public void MathOverrides()
        {
            var entity = m_Manager.CreateEntity(typeof(MathComponent));

            var math = m_Manager.GetComponentData<MathComponent>(entity);
            math.v2 = new float2(1f, 2f);
            math.v3 = new float3(1f, 2f, 3f);
            math.v4 = new float4(1f, 2f, 3f, 4f);
            m_Manager.SetComponentData(entity, math);

            var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entity));
            Debug.Log(json);
        }

        public struct BlitMe
        {
            public float x;
            public double y;
            public sbyte z;
        }

        private struct BlitComponent : IComponentData
        {
            public BlitMe blit;
            public float flt;
        }

        [Test]
        public void BlittableTest()
        {
            var entity = m_Manager.CreateEntity(typeof(BlitComponent));

            var comp = m_Manager.GetComponentData<BlitComponent>(entity);
            comp.blit.x = 123f;
            comp.blit.y = 456.789;
            comp.blit.z = -12;
            comp.flt = 0.01f;

            var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entity));
            Debug.Log(json);
        }

        /// <summary>
        /// Serializes 100,000 entities as json
        /// </summary>
        [Test]
        public void SerializationPerformance()
        {
            const int kCount = 100000;

            // Create kCount entities and assign some arbitrary component data
            for (var i = 0; i < kCount; ++i)
            {
                var entity = m_Manager.CreateEntity(typeof(TestComponent), typeof(TestComponent2), typeof(MathComponent), typeof(BlitComponent));

                var comp = m_Manager.GetComponentData<BlitComponent>(entity);
                comp.blit.x = 123f;
                comp.blit.y = 456.789;
                comp.blit.z = -12;
                comp.flt = 0.01f;

                m_Manager.SetComponentData(entity, comp);
            }

            // Create a reusable string buffer and JsonVisitor
            var buffer = new StringBuffer(4096);
            var visitor = new JsonVisitor { StringBuffer = buffer };

            using (var entities = m_Manager.GetAllEntities())
            {
                // Since we are testing raw serialization performance we rre warm the property type bag
                // This builds a property tree for each type
                // This is done on demand for newly discovered types
                // @NOTE This json string will also be used to debug the size for a single entity
                var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entities[0]));

                var totalTimer = new Stopwatch();
                totalTimer.Start();

                foreach (var entity in entities)
                {
                    // Visit and write to the underlying StringBuffer, this is the raw json serialization
                    JsonSerializer.Visit(new EntityContainer(m_Manager, entity), visitor);
                    // @NOTE at this point we can call Write(buffer.Buffer, 0, buffer.Length)
                    buffer.Clear();
                }

                totalTimer.Stop();

                Debug.Log($"Serialized {kCount} entities in {totalTimer.Elapsed}. Size per entity = {json.Length} bytes, total size = {json.Length * kCount} bytes");
                Debug.Log(json);
            }
        }

        private struct SerializationJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<Entity> Entities;

            public void Execute(int startIndex, int count)
            {
                // @HACK need a reliable way having the entity manage for the given entities
                var manager = World.Active.GetExistingManager<EntityManager>();
                var buffer = new StringBuffer(4096);
                var visitor = new JsonVisitor { StringBuffer = buffer };

                var end = startIndex + count;
                for (var i = startIndex; i < end; i++)
                {
                    JsonSerializer.Visit(new EntityContainer(manager, Entities[i]), visitor);
                    // @NOTE at this point we can call Write(buffer.Buffer, 0, buffer.Length)
                    buffer.Clear();
                }
            }
        }

        private struct WorkerThreadContext
        {
            public NativeArray<Entity> Entities;
            public int StartIndex;
            public int EndIndex;
            public string Output;
        }

        /// <summary>
        /// Serializes 100,000 entities as json using manual thread management
        ///
        /// This test exists as an example to quickly test stuff on the thread that is not supported by C# job system
        /// (e.g. disc I/O, managed objects, strings etc)
        /// </summary>
        [Test]
        public void SerializationPerformanceThreaded()
        {
            const int kCount = 100000;

            // Create kCount entities and assign some arbitrary component data
            for (var i = 0; i < kCount; ++i)
            {
                var entity = m_Manager.CreateEntity(typeof(TestComponent), typeof(TestComponent2), typeof(MathComponent), typeof(BlitComponent));

                var comp = m_Manager.GetComponentData<BlitComponent>(entity);
                comp.blit.x = 123f;
                comp.blit.y = 456.789;
                comp.blit.z = -12;
                comp.flt = 0.01f;

                m_Manager.SetComponentData(entity, comp);
            }

            using (var entities = m_Manager.GetAllEntities())
            {
                // Since we are testing raw serialization performance we rre warm the property type bag
                // This builds a property tree for each type
                // This is done on demand for newly discovered types
                // @NOTE This json string will also be used to debug the size for a single entity
                var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entities[0]));

                var totalTimer = new Stopwatch();

                totalTimer.Start();

                var numThreads = Math.Max(1, Environment.ProcessorCount - 1);
                var threadCount = numThreads;
                var countPerThread = entities.Length / threadCount + 1;
                var threads = new Thread[threadCount];

                // Split the workload 'evenly' across numThreads (IJobParallelForBatch)
                for (int begin = 0, index = 0; begin < entities.Length; begin += countPerThread, index++)
                {
                    var context = new WorkerThreadContext
                    {
                        Entities = entities,
                        StartIndex = begin,
                        EndIndex = Mathf.Min(begin + countPerThread, entities.Length)
                    };

                    var thread = new Thread(obj =>
                    {
                        var buffer = new StringBuffer(4096);
                        var visitor = new JsonVisitor { StringBuffer = buffer };

                        var c = (WorkerThreadContext) obj;
                        for (int p = c.StartIndex, end = c.EndIndex; p < end; p++)
                        {
                            var entity = c.Entities[p];
                            JsonSerializer.Visit(new EntityContainer(m_Manager, entity), visitor);
                            // @NOTE at this point we can call Write(buffer.Buffer, 0, buffer.Length)
                            buffer.Clear();
                        }
                    }) {IsBackground = true};
                    thread.Start(context);
                    threads[index] = thread;
                }

                foreach (var thread in threads)
                {
                    thread.Join();
                }

                totalTimer.Stop();
                Debug.Log($"Serialized {kCount} entities in {totalTimer.Elapsed}. Size per entity = {json.Length} bytes, total size = {json.Length * kCount} bytes");
            }
        }

        /// <summary>
        /// Serializes 100,000 entities as json using the C# job system
        /// </summary>
        [Test]
        public void SerializationPerformanceJob()
        {
            const int kCount = 100000;

            // Create kCount entities and assign some arbitrary component data
            for (var i = 0; i < kCount; ++i)
            {
                var entity = m_Manager.CreateEntity(typeof(TestComponent), typeof(TestComponent2), typeof(MathComponent), typeof(BlitComponent));

                var comp = m_Manager.GetComponentData<BlitComponent>(entity);
                comp.blit.x = 123f;
                comp.blit.y = 456.789;
                comp.blit.z = -12;
                comp.flt = 0.01f;

                m_Manager.SetComponentData(entity, comp);
            }

            using (var entities = m_Manager.GetAllEntities())
            {
                // Since we are testing raw serialization performance we rre warm the property type bag
                // This builds a property tree for each type
                // This is done on demand for newly discovered types
                // @NOTE This json string will also be used to debug the size for a single entity
                var json = JsonSerializer.Serialize(new EntityContainer(m_Manager, entities[0]));

                var job = new SerializationJob
                {
                    Entities = entities
                };

                var totalTimer = new Stopwatch();
                totalTimer.Start();

                var handle = job.ScheduleBatch(entities.Length, 10000);
                handle.Complete();

                totalTimer.Stop();
                Debug.Log($"Serialized {kCount} entities in {totalTimer.Elapsed}. Size per entity = {json.Length} bytes, total size = {json.Length * kCount} bytes");
            }
        }
    }
}
