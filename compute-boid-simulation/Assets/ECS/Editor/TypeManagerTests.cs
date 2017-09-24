using UnityEngine.ECS;
using NUnit.Framework;

namespace UnityEngine.ECS.Tests
{
    public class TypeManagerTests : ECSFixture
	{
		struct TestType1
		{
			int empty;
		}
		struct TestType2
		{
			int empty;
		}
		[Test]
		unsafe public void CreateArchetypes()
		{
            var archetype1 = m_Manager.CreateArchetype(ComponentType.Create<TestType1>(), ComponentType.Create<TestType2>());
            var archetype1Same = m_Manager.CreateArchetype(ComponentType.Create<TestType1>(), ComponentType.Create<TestType2>());
            Assert.AreEqual(archetype1, archetype1Same);

            var archetype2 = m_Manager.CreateArchetype(ComponentType.Create<TestType1>());
            var archetype2Same = m_Manager.CreateArchetype(ComponentType.Create<TestType1>());
            Assert.AreEqual(archetype2Same, archetype2Same);

            Assert.AreNotEqual(archetype1, archetype2);
		}

        [Test]
        unsafe public void TestTypeManager()
        {
            RealTypeManager.Initialize();

            int entity = RealTypeManager.GetTypeIndex<Entity>();
            int testData = RealTypeManager.GetTypeIndex<EcsTestData>();

            Assert.AreEqual(entity, RealTypeManager.GetTypeIndex<Entity>());
            Assert.AreEqual(testData, RealTypeManager.GetTypeIndex<EcsTestData>());
            Assert.AreNotEqual(RealTypeManager.GetTypeIndex<Entity>(), RealTypeManager.GetTypeIndex<EcsTestData>());

            Assert.AreEqual(typeof(Entity), RealTypeManager.GetComponentType(RealTypeManager.GetTypeIndex<Entity>()).type);
            Assert.AreEqual(sizeof(Entity), RealTypeManager.GetComponentType(RealTypeManager.GetTypeIndex<Entity>()).sizeInChunk);
        }
    }
}