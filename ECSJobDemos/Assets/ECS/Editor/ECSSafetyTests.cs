﻿using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Jobs;
using System;
using UnityEngine.TestTools;

//@TODO: We should really design systems / jobs / exceptions / errors 
//       so that an error in one system does not affect the next system.
//       Right now failure to set dependencies correctly in one system affects other code,
//       this makes the error messages significantly less useful...
//       So need to redo all tests accordingly

namespace UnityEngine.ECS.Tests
{
    public class ECSSafetyTests : ECSFixture
	{
	    [Test]
	    public void ComponentArrayChunkSliceOutOfBoundsThrowsException()
	    {
	        for (int i = 0;i<10;i++)
	            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

	        var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
	        var testData = group.GetComponentDataArray<EcsTestData>();

	        Assert.AreEqual(0, testData.GetChunkSlice(5, 0).Length);
	        Assert.AreEqual(10, testData.GetChunkSlice(0, 10).Length);

	        Assert.Throws<IndexOutOfRangeException>(() => { testData.GetChunkSlice(-1, 1); });
	        Assert.Throws<IndexOutOfRangeException>(() => { testData.GetChunkSlice(5, 6); });
	        Assert.Throws<IndexOutOfRangeException>(() => { testData.GetChunkSlice(10, 1); });
	    }
	    
	    
        [Test]
        public void ReadOnlyComponentDataArray()
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
            arr = group.GetReadOnlyComponentDataArray<EcsTestData>();
            Assert.AreEqual(1, arr.Length);
            Assert.Throws<System.InvalidOperationException>(() => { arr[0] = new EcsTestData(0); });
            arr = group.GetComponentDataArray<EcsTestData>();
            Assert.AreEqual(1, arr.Length);
            arr[0] = new EcsTestData(0);
            Assert.AreEqual(0, arr[0].value);

            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void AccessComponentArrayAfterCreationThrowsException()
        {
            CreateEntityWithDefaultData(0);

            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
            var arr = group.GetComponentDataArray<EcsTestData>();

            CreateEntityWithDefaultData(1);

            Assert.Throws<InvalidOperationException>(() => { var value = arr[0]; });
        }

        [Test]
        public void CreateEntityInvalidatesArray()
        {
            CreateEntityWithDefaultData(0);

            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
            var arr = group.GetComponentDataArray<EcsTestData>();

            CreateEntityWithDefaultData(1);

            Assert.Throws<InvalidOperationException>(() => { var value = arr[0]; });
        }

        [Test]
        public void GetSetComponentThrowsIfNotExist()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var destroyedEntity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.DestroyEntity(destroyedEntity);

            Assert.Throws<System.ArgumentException>(() => { m_Manager.SetComponent(entity, new EcsTestData2()); });
            Assert.Throws<System.ArgumentException>(() => { m_Manager.SetComponent(destroyedEntity, new EcsTestData2()); });

            Assert.Throws<System.ArgumentException>(() => { m_Manager.GetComponent<EcsTestData2>(entity); });
            Assert.Throws<System.ArgumentException>(() => { m_Manager.GetComponent<EcsTestData2>(destroyedEntity); });
        }

        [Test]
        public void ComponentDataArrayFromEntityThrowsIfNotExist()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var destroyedEntity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.DestroyEntity(destroyedEntity);

            var data = m_Manager.GetComponentDataArrayFromEntity<EcsTestData2>();

            Assert.Throws<System.ArgumentException>(() => { data[entity] = new EcsTestData2(); });
            Assert.Throws<System.ArgumentException>(() => { data[destroyedEntity] = new EcsTestData2(); });

            Assert.Throws<System.ArgumentException>(() => { var p = data[entity]; });
            Assert.Throws<System.ArgumentException>(() => { var p = data[destroyedEntity]; });
        }

        [Test]
        public void AddComponentTwiceThrows()
        {
            var entity = m_Manager.CreateEntity();

            m_Manager.AddComponent(entity, new EcsTestData(1));
            Assert.Throws<System.InvalidOperationException>(() => { m_Manager.AddComponent(entity, new EcsTestData(1)); });
        }

        [Test]
        public void AddRemoveComponentOnDestroyedEntityThrows()
        {
            var destroyedEntity = m_Manager.CreateEntity();
            m_Manager.DestroyEntity(destroyedEntity);

            Assert.Throws<System.ArgumentException>(() => { m_Manager.AddComponent(destroyedEntity, new EcsTestData(1)); });
            Assert.Throws<System.ArgumentException>(() => { m_Manager.RemoveComponent<EcsTestData>(destroyedEntity); });
        }

        [Test]
        public void RemoveComponentOnEntityWithoutComponent()
        {
            var entity = m_Manager.CreateEntity();
            Assert.Throws<System.ArgumentException>(() => { m_Manager.RemoveComponent<EcsTestData>(entity); });
        }

        [Test]
        public void CreateDestroyEmptyEntity()
        {
            var entity = m_Manager.CreateEntity();
            Assert.IsTrue(m_Manager.Exists(entity));
            m_Manager.DestroyEntity(entity);
            Assert.IsFalse(m_Manager.Exists(entity));
        }
    }
}