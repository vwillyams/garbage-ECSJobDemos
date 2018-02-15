using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Properties.Serialization;
using UnityEngine;
using UnityEngine.ECS;
using Debug = UnityEngine.Debug;

namespace Unity.Properties.ECS.Test
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
    
    [TestFixture]
    public sealed class EntitySerializationTests
    {
        private World 			m_PreviousWorld;
        private World 			m_World;
        private EntityManager   m_Manager;
        private JsonVisitor     m_Visitor;

        [SetUp]
        public void Setup()
        {
            m_PreviousWorld = World.Active;
            m_World = World.Active = new World ("Test World");

            m_Manager = m_World.GetOrCreateManager<EntityManager> ();
            m_Visitor = new JsonVisitor();
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
        
        [Test]
        public void SimpleFlat()
        {
            var entity = m_Manager.CreateEntity(typeof(TestComponent), typeof(TestComponent2));
            
            var testComponent = m_Manager.GetComponentData<TestComponent>(entity);
            testComponent.x = 123f;
            m_Manager.SetComponentData(entity, testComponent);
            
            var container = new EntityContainer();
            
            container.Setup(m_Manager, entity);

            var json = JsonPropertyContainerWriter.Write(container, m_Visitor);
            
            Debug.Log(json);
        }
        
        public struct NestedComponent : IComponentData
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
            
            var container = new EntityContainer();
            
            container.Setup(m_Manager, entity);

            var json = JsonPropertyContainerWriter.Write(container, m_Visitor);
            
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
            
            var container = new EntityContainer();
            
            container.Setup(m_Manager, entity);

            var json = JsonPropertyContainerWriter.Write(container, m_Visitor);
            
            Debug.Log(json);
        }

        public struct BlitMe
        {
            public float x;
            public double y;
            public sbyte z;
        }

        public struct BlitComponent : IComponentData
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
            
            m_Manager.SetComponentData(entity, comp);
            
            var container = new EntityContainer();
            
            container.Setup(m_Manager, entity);

            var json = JsonPropertyContainerWriter.Write(container, m_Visitor);
            
            Debug.Log(json);
        }
        
        
        [Test]
        [Ignore("Slow")]
        public void Perf()
        {
            const int kCount = 100000;

            for (int i = 0; i < kCount; ++i)
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
                var sw = new Stopwatch();
                sw.Start();
                var container = new EntityContainer();
                string json = string.Empty;
                
                var setupTimer = new Stopwatch();
                
                foreach (var entity in entities)
                {
                    setupTimer.Start();
                    container.Setup(m_Manager, entity);
                    setupTimer.Stop();
                    
                    json = JsonPropertyContainerWriter.Write(container, m_Visitor);
                }
                
                sw.Stop();
                Debug.Log($"Serialized {kCount} entities in {sw.Elapsed}. Size per entity = {json.Length} bytes, total size = {json.Length * kCount} bytes");
                Debug.Log($"Setting up proxy containers took {setupTimer.Elapsed}, or {100.0 * ((double)setupTimer.ElapsedMilliseconds / sw.ElapsedMilliseconds)}%");
                Debug.Log(json);

                sw.Reset();
                sw.Start();

                var nThreads = Math.Max(1, Environment.ProcessorCount - 1);
                TypeTreeParallelVisit(entities, nThreads);
                sw.Stop();
                
                Debug.Log($"Threaded serialization using {nThreads} threads in {sw.Elapsed}");
            }
        }
        
        private struct WorkerThreadContext
        {
            public NativeArray<Entity> Entities;
            public int StartIndex;
            public int EndIndex;
        }

        private void TypeTreeParallelVisit(NativeArray<Entity> list, int numThreads)
        {   
            var kCount = list.Length;

            var threadCount = numThreads;
            var count = kCount / threadCount + 1;
            var threads = new Thread[threadCount];

            for (int begin = 0, index = 0; begin < kCount; begin += count, index++)
            {
                var context = new WorkerThreadContext
                {
                    Entities = list,
                    StartIndex = begin,
                    EndIndex = Mathf.Min(begin + count, kCount)
                };

                var thread = new Thread(obj =>
                {
                    var container = new EntityContainer();
                    var visitor = new JsonVisitor()
                    {
                        StringBuffer = new StringBuffer(4096)
                    };
                    var c = (WorkerThreadContext) obj;
                    for (int p = c.StartIndex, end = c.EndIndex; p < end; p++)
                    {
                        var entity = c.Entities[p];
                        container.Setup(m_Manager, entity);
                        container.PropertyBag.Visit(container, visitor);
                        visitor.StringBuffer.Clear();
                    }
                }) {IsBackground = true};
                thread.Start(context);
                threads[index] = thread;
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }
    }

}