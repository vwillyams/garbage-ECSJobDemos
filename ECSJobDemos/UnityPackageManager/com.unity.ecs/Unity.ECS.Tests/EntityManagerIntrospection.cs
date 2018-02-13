using NUnit.Framework;

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
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var types0 = m_Manager.GetComponentTypes(entity0);
            Assert.AreEqual(1, types0.Length);
            Assert.AreEqual(ComponentType.Create<EcsTestData>(), types0[0]);
            types0.Dispose();
            
            var types1 = m_Manager.GetComponentTypes(entity1);
            Assert.AreEqual(2, types1.Length);
            Assert.AreEqual(ComponentType.Create<EcsTestData>(), types1[0]);
            Assert.AreEqual(ComponentType.Create<EcsTestData2>(), types1[1]);
            types1.Dispose();
        }
    }
}