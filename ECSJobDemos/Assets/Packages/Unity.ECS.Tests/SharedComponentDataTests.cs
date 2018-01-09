using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UnityEngine.ECS;
using Unity.Collections;

namespace UnityEngine.ECS.Tests
{
    public class SharedComponentDataTests : ECSTestsFixture
    {
        struct SharedData1 : ISharedComponentData
        {
            public int value;

            public SharedData1(int val) { value = val; }
        }

        struct SharedData2 : ISharedComponentData
        {
            public int value;

            public SharedData2(int val) { value = val; }
        }


        //@TODO: No tests for invalid shared components / destroyed shared component data
        //@TODO: No tests for if we leak shared data when last entity is destroyed...
        //@TODO: No tests for invalid shared component type?

        [Test]
        public void SetSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData), typeof(SharedData2));

            var group1 = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData1));
            var group2 = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData2));
            var group12 = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData2), typeof(SharedData1));

            Assert.AreEqual(0, group1.CalculateLength());
            Assert.AreEqual(0, group2.CalculateLength());
            Assert.AreEqual(0, group12.CalculateLength());

            var group1_filter_0 = group1.GetVariation(new SharedData1(0));
            var group1_filter_20 = group1.GetVariation(new SharedData1(20));
            Assert.AreEqual(0, group1_filter_0.CalculateLength());
            Assert.AreEqual(0, group1_filter_20.CalculateLength());

            Entity e1 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponent(e1, new EcsTestData(117));
            Entity e2 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponent(e2, new EcsTestData(243));

            Assert.AreEqual(2, group1_filter_0.CalculateLength());
            Assert.AreEqual(0, group1_filter_20.CalculateLength());
            Assert.AreEqual(117, group1_filter_0.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(243, group1_filter_0.GetComponentDataArray<EcsTestData>()[1].value);

            m_Manager.SetSharedComponent(e1, new SharedData1(20));

            Assert.AreEqual(1, group1_filter_0.CalculateLength());
            Assert.AreEqual(1, group1_filter_20.CalculateLength());
            Assert.AreEqual(117, group1_filter_20.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(243, group1_filter_0.GetComponentDataArray<EcsTestData>()[0].value);

            m_Manager.SetSharedComponent(e2, new SharedData1(20));

            Assert.AreEqual(0, group1_filter_0.CalculateLength());
            Assert.AreEqual(2, group1_filter_20.CalculateLength());
            Assert.AreEqual(117, group1_filter_20.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(243, group1_filter_20.GetComponentDataArray<EcsTestData>()[1].value);


            group1.Dispose();
            group2.Dispose();
            group12.Dispose();
            group1_filter_0.Dispose();
            group1_filter_20.Dispose();
        }


//        [Test]
//        public void GetAllUniqueSharedComponents()
//        {
//            var sharedType20 = m_Manager.CreateSharedComponentType(new SharedData(20));
//            var sharedType30 = m_Manager.CreateSharedComponentType(new SharedData(30));
//
//            var unique = new NativeList<ComponentType>(0, Allocator.Persistent);
//            m_Manager.GetAllUniqueSharedComponents(typeof(SharedData), unique);
//
//            Assert.AreEqual(2, unique.Length);
//            Assert.AreEqual(20, m_Manager.GetSharedComponentData<SharedData>(unique[0]).value);
//            Assert.AreEqual(30, m_Manager.GetSharedComponentData<SharedData>(unique[1]).value);
//
//            Assert.AreEqual(20, m_Manager.GetSharedComponentData<SharedData>(sharedType20).value);
//            Assert.AreEqual(30, m_Manager.GetSharedComponentData<SharedData>(sharedType30).value);
//
//            unique.Dispose();
//        }
//
//
//        [Test]
//        public void HasComponentData()
//        {
//            var sharedType20 = m_Manager.CreateSharedComponentType(new SharedData(20));
//            var sharedType30 = m_Manager.CreateSharedComponentType(new SharedData(30));
//
//            var entity = m_Manager.CreateEntity(sharedType20);
//
//            Assert.IsTrue(m_Manager.HasComponent<SharedData>(entity));
//            Assert.IsTrue(m_Manager.HasComponent(entity, typeof(SharedData)));
//            Assert.IsTrue(m_Manager.HasComponent(entity, sharedType20));
//
//            Assert.IsFalse(m_Manager.HasComponent(entity, sharedType30));
//        }
//
//        [Test]
//        public void ArchetypesOfSameDataEqual()
//        {
//            var sharedTypeA = m_Manager.CreateSharedComponentType(new SharedData(20));
//            var sharedTypeB = m_Manager.CreateSharedComponentType(new SharedData(20));
//
//            Assert.AreEqual(sharedTypeA, sharedTypeB);
//
//            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), sharedTypeA);
//            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), sharedTypeB);
//
//            Assert.AreEqual(archetypeA, archetypeB);
//        }
//
//
//        [Test]
//        public void ArchetypesOfDifferentDataDoNotEqual()
//        {
//            var sharedType20 = m_Manager.CreateSharedComponentType(new SharedData(20));
//            var sharedType30 = m_Manager.CreateSharedComponentType(new SharedData(30));
//
//            Assert.AreNotEqual(sharedType20, sharedType30);
//
//            var archetype30 = m_Manager.CreateArchetype(typeof(EcsTestData), sharedType30);
//            var archetype20 = m_Manager.CreateArchetype(typeof(EcsTestData), sharedType20);
//
//            Assert.AreNotEqual(archetype20, archetype30);
//        }
//
//        [Test]
//        public void FindSharedComponentDatas()
//        {
//            var sharedType20 = m_Manager.CreateSharedComponentType(new SharedData(20));
//            var sharedType30 = m_Manager.CreateSharedComponentType(new SharedData(30));
//
//            var archetype30 = m_Manager.CreateArchetype(typeof(EcsTestData), sharedType30);
//            var archetype20 = m_Manager.CreateArchetype(typeof(EcsTestData), sharedType20);
//
//            var entity = m_Manager.CreateEntity(archetype20);
//
//            Assert.AreEqual(20, m_Manager.GetSharedComponentData<SharedData>(entity).value);
//
//            var groupExists = m_Manager.CreateComponentGroup(typeof(EcsTestData), sharedType20);
//            Assert.AreEqual(1, groupExists.GetComponentDataArray<EcsTestData>().Length);
//
//            var groupNotExists = m_Manager.CreateComponentGroup(typeof(EcsTestData), sharedType30);
//            Assert.AreEqual(0, groupNotExists.GetComponentDataArray<EcsTestData>().Length);
//
//            var groupAll = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData));
//            Assert.AreEqual(1, groupAll.GetComponentDataArray<EcsTestData>().Length);
//        }
//
//
//        [Test]
//        [Ignore("Failing currently, behaviour could either by to throw exception in CreateEntity or making it return the default SharedData. Not sure which is preferrable")]
//        public void AddComponentWithDefaultSharedComponentType()
//        {
//            var entity = m_Manager.CreateEntity(typeof(SharedData));
//            Assert.AreEqual(0, m_Manager.GetSharedComponentData<SharedData>(entity).value);
//        }
    }
}
