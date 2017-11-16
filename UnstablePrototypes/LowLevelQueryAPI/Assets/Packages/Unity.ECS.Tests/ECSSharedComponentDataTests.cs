using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UnityEngine.ECS;
using Unity.Collections;

namespace UnityEngine.ECS.Tests
{
    public class ECSSharedComponentDataTests : ECSFixture
    {
        struct SharedData : ISharedComponentData
        {
            public int value;

            public SharedData(int val) { value = val; }
        }


        //@TODO: No tests for invalid shared components / destroyed shared component data
        //@TODO: No tests for if we leak shared data when last entity is destroyed...

        [Test]
        public void GetAllUniqueSharedComponents()
        {
            var sharedType20 = m_Manager.CreateSharedComponentType(new SharedData(20));
            var sharedType30 = m_Manager.CreateSharedComponentType(new SharedData(30));

            var unique = new NativeList<ComponentType>(0, Allocator.Persistent);
            m_Manager.GetAllUniqueSharedComponents(typeof(SharedData), unique);

            Assert.AreEqual(2, unique.Length);
            Assert.AreEqual(20, m_Manager.GetSharedComponentData<SharedData>(unique[0]).value);
            Assert.AreEqual(30, m_Manager.GetSharedComponentData<SharedData>(unique[1]).value);

            Assert.AreEqual(20, m_Manager.GetSharedComponentData<SharedData>(sharedType20).value);
            Assert.AreEqual(30, m_Manager.GetSharedComponentData<SharedData>(sharedType30).value);

            unique.Dispose();
        }


        [Test]
        public void FindSharedComponentDatas()
        {
            var sharedType20 = m_Manager.CreateSharedComponentType(new SharedData(20));
            var sharedType30 = m_Manager.CreateSharedComponentType(new SharedData(30));

            Assert.AreNotEqual(sharedType20, sharedType30);

            var archetype30 = m_Manager.CreateArchetype(typeof(EcsTestData), sharedType30);
            var archetype20 = m_Manager.CreateArchetype(typeof(EcsTestData), sharedType20);

            Assert.AreNotEqual(archetype20, archetype30);

            var entity = m_Manager.CreateEntity(archetype20);

            Assert.IsTrue(m_Manager.HasComponent<SharedData>(entity));
            Assert.IsTrue(m_Manager.HasComponent(entity, sharedType20));
            Assert.IsFalse(m_Manager.HasComponent(entity, sharedType30));

            Assert.AreEqual(20, m_Manager.GetSharedComponentData<SharedData>(entity).value);

            var groupExists = m_Manager.CreateComponentGroup(typeof(EcsTestData), sharedType20);
            Assert.AreEqual(1, groupExists.GetComponentDataArray<EcsTestData>().Length);

            var groupNotExists = m_Manager.CreateComponentGroup(typeof(EcsTestData), sharedType30);
            Assert.AreEqual(0, groupNotExists.GetComponentDataArray<EcsTestData>().Length);

            var groupAll = m_Manager.CreateComponentGroup(typeof(EcsTestData), ComponentType.Create<SharedData>());
            Assert.AreEqual(1, groupAll.GetComponentDataArray<EcsTestData>().Length);
        }
    }  
}