using UnityEngine.ECS;
using NUnit.Framework;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;
using System;

namespace UnityEngine.ECS.Tests
{
	public struct EcsTestData2 : IComponentData
	{
		public int value0;
		public int value1;

		public EcsTestData2(int inValue) { value1 = value0 = inValue; }
	}

	public struct EcsTestData3 : IComponentData
	{
		public int value0;
		public int value1;
		public int value2;

		public EcsTestData3(int inValue) { value2 = value1 = value0 = inValue; }
	}



	public class ECSCreateAndDestroy : ECSFixture
	{


        [Test]
        unsafe public void CreateAndDestroyOne()
        {
            var entity = CreateEntityWithDefaultData(10);
            m_Manager.DestroyEntity(entity);
            AssertDoesNotExist(entity);
        }

        [Test]
        unsafe public void EmptyEntityIsNull()
        {
            CreateEntityWithDefaultData(10);
            Assert.IsFalse(m_Manager.Exists(new Entity()));
        }

        [Test]
        unsafe public void CreateAndDestroyTwo()
        {
            var entity0 = CreateEntityWithDefaultData(10);
            var entity1 = CreateEntityWithDefaultData(11);

            m_Manager.DestroyEntity(entity0);

            AssertDoesNotExist(entity0);
            AssertComponentData(entity1, 11);

            m_Manager.DestroyEntity(entity1);
            AssertDoesNotExist(entity0);
            AssertDoesNotExist(entity1);
        }

        [Test]
        unsafe public void CreateAndDestroyThree()
        {
            var entity0 = CreateEntityWithDefaultData(10);
            var entity1 = CreateEntityWithDefaultData(11);

            m_Manager.DestroyEntity(entity0);

            var entity2 = CreateEntityWithDefaultData(12);


            AssertDoesNotExist(entity0);

            AssertComponentData(entity1, 11);
            AssertComponentData(entity2, 12);
        }

        [Test]
        unsafe public void CreateAndDestroyStressTest()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);

            m_Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < entities.Length; i++)
                AssertComponentData(entities[i], 0);

            m_Manager.DestroyEntity(entities);
            entities.Dispose();
        }

        [Test]
        unsafe public void CreateAndDestroyShuffleStressTest()
		{
            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length;i++)
            {
                entities[i] = CreateEntityWithDefaultData(i);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                    m_Manager.DestroyEntity(entities[i]);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                {
                    AssertDoesNotExist(entities[i]);
                }
                else
                {
                    AssertComponentData(entities[i], i);
                }
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 1)
                    m_Manager.DestroyEntity(entities[i]);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                AssertDoesNotExist(entities[i]);
            }
        }


        [Test]
        unsafe public void InstantiateStressTest()
        {
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);
            var srcEntity = CreateEntityWithDefaultData(5);

            m_Manager.Instantiate(srcEntity, entities);

            for (int i = 0; i < entities.Length; i++)
                AssertComponentData(entities[i], 5);

            m_Manager.DestroyEntity(entities);
            entities.Dispose();
        }

		[Test]
		public void AddRemoveComponent()
		{
			var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

			var entity = m_Manager.CreateEntity(archetype);
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
			Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));

			m_Manager.AddComponent<EcsTestData3>(entity, new EcsTestData3(3));
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entity));

            Assert.AreEqual(3, m_Manager.GetComponent<EcsTestData3>(entity).value0);
            Assert.AreEqual(3, m_Manager.GetComponent<EcsTestData3>(entity).value1);
            Assert.AreEqual(3, m_Manager.GetComponent<EcsTestData3>(entity).value2);

			m_Manager.RemoveComponent<EcsTestData2>(entity);
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
			Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
			Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entity));

            Assert.AreEqual(3, m_Manager.GetComponent<EcsTestData3>(entity).value0);
            Assert.AreEqual(3, m_Manager.GetComponent<EcsTestData3>(entity).value1);
            Assert.AreEqual(3, m_Manager.GetComponent<EcsTestData3>(entity).value2);

			m_Manager.DestroyEntity(entity);
		}

		[Test]
		public void CreateComponentGroup()
		{
			var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

			var group = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(EcsTestData2));
			var arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(0, arr.Length);

			var entity = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponent(entity, new EcsTestData(42));
			arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(1, arr.Length);
			Assert.AreEqual(42, arr[0].value);

			m_Manager.DestroyEntity(entity);
		}

		struct TempComponentNeverInstantiated : IComponentData
		{}
		[Test]
		public void IterateEmptyArchetype()
		{
			var group = m_Manager.CreateComponentGroup(typeof(TempComponentNeverInstantiated));
			var arr = group.GetComponentDataArray<TempComponentNeverInstantiated>();
			Assert.AreEqual(0, arr.Length);

			var archetype = m_Manager.CreateArchetype(typeof(TempComponentNeverInstantiated));
			arr = group.GetComponentDataArray<TempComponentNeverInstantiated>();
			Assert.AreEqual(0, arr.Length);

			Entity ent = m_Manager.CreateEntity(archetype);
			arr = group.GetComponentDataArray<TempComponentNeverInstantiated>();
			Assert.AreEqual(1, arr.Length);
			m_Manager.DestroyEntity(ent);
			arr = group.GetComponentDataArray<TempComponentNeverInstantiated>();
			Assert.AreEqual(0, arr.Length);
		}
		[Test]
		public void IterateChunkedComponentGroup()
		{
			var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
			var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

			var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
			var arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(0, arr.Length);

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length/2;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype1);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }
            for (int i = entities.Length/2; i < entities.Length;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype2);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }

			arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(entities.Length, arr.Length);
			HashSet<int> values = new HashSet<int>();
            for (int i = 0; i < arr.Length;i++)
			{
				int val = arr[i].value;
				Assert.IsFalse(values.Contains(i));
				Assert.IsTrue(val >= 0);
				Assert.IsTrue(val < entities.Length);
				values.Add(i);
			}

            for (int i = 0; i < entities.Length;i++)
				m_Manager.DestroyEntity(entities[i]);
		}
		[Test]
		public void IterateChunkedComponentGroupBackwards()
		{
			var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
			var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

			var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
			var arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(0, arr.Length);

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length/2;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype1);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }
            for (int i = entities.Length/2; i < entities.Length;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype2);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }

			arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(entities.Length, arr.Length);
			HashSet<int> values = new HashSet<int>();
            for (int i = arr.Length-1; i >= 0;i--)
			{
				int val = arr[i].value;
				Assert.IsFalse(values.Contains(i));
				Assert.IsTrue(val >= 0);
				Assert.IsTrue(val < entities.Length);
				values.Add(i);
			}

            for (int i = 0; i < entities.Length;i++)
				m_Manager.DestroyEntity(entities[i]);
		}



		[Test]
		public void IterateChunkedComponentGroupAfterDestroy()
		{
			var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
			var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

			var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
			var arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(0, arr.Length);

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length/2;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype1);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }
            for (int i = entities.Length/2; i < entities.Length;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype2);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }
            for (int i = 0; i < entities.Length;i++)
			{
				if (i%2 != 0)
				{
					m_Manager.DestroyEntity(entities[i]);
				}
			}

			arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(entities.Length/2, arr.Length);
			HashSet<int> values = new HashSet<int>();
            for (int i = 0; i < arr.Length;i++)
			{
				int val = arr[i].value;
				Assert.IsFalse(values.Contains(i));
				Assert.IsTrue(val >= 0);
				Assert.IsTrue(val%2 == 0);
				Assert.IsTrue(val < entities.Length);
				values.Add(i);
			}

            for (int i = entities.Length/2; i < entities.Length;i++)
            {
				if (i%2 == 0)
					m_Manager.RemoveComponent<EcsTestData>(entities[i]);
            }
			arr = group.GetComponentDataArray<EcsTestData>();
			Assert.AreEqual(entities.Length/4, arr.Length);
			values = new HashSet<int>();
            for (int i = 0; i < arr.Length;i++)
			{
				int val = arr[i].value;
				Assert.IsFalse(values.Contains(i));
				Assert.IsTrue(val >= 0);
				Assert.IsTrue(val%2 == 0);
				Assert.IsTrue(val < entities.Length/2);
				values.Add(i);
			}

            for (int i = 0; i < entities.Length;i++)
			{
				if (i%2 == 0)
					m_Manager.DestroyEntity(entities[i]);
			}
		}

        [Test]
        unsafe public void CreateAndDestroyFixedArray()
        {
            var fixedArrayType = new ComponentType(typeof(int), 64);
            var entities = new NativeArray<Entity>(100, Allocator.Persistent);
            m_Manager.CreateEntity(m_Manager.CreateArchetype(fixedArrayType), entities);

            var group = m_Manager.CreateComponentGroup(fixedArrayType);

            var fixedArray = group.GetComponentDataFixedArray<int>(fixedArrayType);

            Assert.AreEqual(64, fixedArray[0].Length);
            for (int i = 0; i < entities.Length;i++)
            {
                Assert.AreEqual(0, fixedArray[i][3]);
                NativeArray<int > array = fixedArray[i];
                array[3] = i;
            }

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.AreEqual(i, fixedArray[i][3]);
            }

            m_Manager.DestroyEntity(entities);

            entities.Dispose();
        }
		[Test]
		public void IterateEntityArray()
		{
			var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
			var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

			var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
			var arr = group.GetEntityArray();
			Assert.AreEqual(0, arr.Length);

            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length/2;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype1);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }
            for (int i = entities.Length/2; i < entities.Length;i++)
            {
				entities[i] = m_Manager.CreateEntity(archetype2);
				m_Manager.SetComponent(entities[i], new EcsTestData(i));
            }

			arr = group.GetEntityArray();
			Assert.AreEqual(entities.Length, arr.Length);
			var values = new HashSet<Entity>();
            for (int i = 0; i < arr.Length;i++)
			{
				Entity val = arr[i];
				Assert.IsFalse(values.Contains(val));
				values.Add(val);
			}

            for (int i = 0; i < entities.Length;i++)
				m_Manager.DestroyEntity(entities[i]);
		}
	}
}