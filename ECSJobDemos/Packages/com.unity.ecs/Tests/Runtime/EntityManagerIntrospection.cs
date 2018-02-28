using NUnit.Framework;
using Unity.Entities;
using UnityEditor;

namespace UnityEngine.ECS.Tests
{
    public class EntityManagerIntrospection : ECSTestsFixture
    {
        [Test]
        public void GetAllEntitiesWorks()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));

            var entities = m_Manager.GetAllEntities();
            Assert.AreEqual(entity0, entities[0]);
            Assert.AreEqual(entity1, entities[1]);
            
            entities.Dispose();
        }
        
        [Test]
        public void GetComponentTypesWorks()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData));

            var types0 = m_Manager.GetComponentTypes(entity0);
            Assert.AreEqual(1, types0.Length);
            Assert.AreEqual(ComponentType.Create<EcsTestData>(), types0[0]);
            types0.Dispose();
            
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var types1 = m_Manager.GetComponentTypes(entity1);
            Assert.AreEqual(2, types1.Length);
            Assert.AreEqual(ComponentType.Create<EcsTestData>(), types1[0]);
            Assert.AreEqual(ComponentType.Create<EcsTestData2>(), types1[1]);
            types1.Dispose();
        }
        
        [Test]
        public void GetComponentBoxed()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));

            var boxed = m_Manager.GetComponentBoxed(entity, typeof(EcsTestData));
            var boxCasted = (EcsTestData) boxed;
                
            Assert.AreEqual(42, boxCasted.value);
        }

        [Test]
        public void SetComponentBoxed()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            m_Manager.SetComponentBoxed(entity, typeof(EcsTestData), new EcsTestData(42));
                
            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }
    }
}