using UnityEngine.ECS;
using NUnit.Framework;

namespace UnityEngine.ECS.Tests
{
	public class TypeManagerTests
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
			RealTypeManager.Initialize();
			EntityGroupManager groupMan = new EntityGroupManager(null);
			int[] typeSet = new int[2];
			typeSet[0] = RealTypeManager.GetTypeIndex<TestType1>();
			typeSet[1] = RealTypeManager.GetTypeIndex<TestType2>();
			TypeManager typeMan = new TypeManager();
			fixed(int* p = &typeSet[0])
			{				
				Archetype* type1 = typeMan.GetArchetype(p, 1, groupMan);
				Archetype* cachedType1 = typeMan.GetArchetype(p, 1, groupMan);
				Assert.IsFalse(type1 == null);
				Assert.IsTrue(type1 == cachedType1);
				Archetype* type2 = typeMan.GetArchetype(p, 2, groupMan);
				Archetype* cachedType2 = typeMan.GetArchetype(p, 2, groupMan);
				Assert.IsFalse(type2 == null);
				Assert.IsTrue(type2 == cachedType2);
				Assert.IsFalse(type1 == type2);
			}
			typeMan.Dispose();
			groupMan.Dispose();
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
            Assert.AreEqual(sizeof(Entity), RealTypeManager.GetComponentType(RealTypeManager.GetTypeIndex<Entity>()).size);
        }
    }
}