using UnityEngine.ECS;
using NUnit.Framework;

namespace UnityEngine.ECS.Tests
{
	public class ECSTestsFixture
	{
		protected World 			m_PreviousWorld;
		protected World 			World;
		protected EntityManager     m_Manager;

        [SetUp]
		public void Setup()
		{
			m_PreviousWorld = World.Active;
			World = World.Active = new World ();

			m_Manager = World.GetOrCreateManager<EntityManager> ();
		}

		[TearDown]
		public void TearDown()
		{
            if (m_Manager != null)
            {
	            World.Dispose();
	            World = null;
	            
                World.Active = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = null;
            }
		}

        public void AssertDoesNotExist(Entity entity)
        {
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsFalse(m_Manager.Exists(entity));
        }

        public void AssertComponentData(Entity entity, int index)
        {
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsTrue(m_Manager.Exists(entity));

            Assert.AreEqual(-index, m_Manager.GetComponent<EcsTestData2>(entity).value0);
            Assert.AreEqual(-index, m_Manager.GetComponent<EcsTestData2>(entity).value1);
            Assert.AreEqual(index, m_Manager.GetComponent<EcsTestData>(entity).value);
        }

        public Entity CreateEntityWithDefaultData(int index)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);

            // HasComponent & Exists setup correctly
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsTrue(m_Manager.Exists(entity));

            // Create must initialize values to zero
            Assert.AreEqual(0, m_Manager.GetComponent<EcsTestData2>(entity).value0);
            Assert.AreEqual(0, m_Manager.GetComponent<EcsTestData2>(entity).value1);
            Assert.AreEqual(0, m_Manager.GetComponent<EcsTestData>(entity).value);

            // Setup some non zero default values
            m_Manager.SetComponent(entity, new EcsTestData2(-index));
            m_Manager.SetComponent(entity, new EcsTestData(index));

            AssertComponentData(entity, index);

            return entity;
        }
	}
}