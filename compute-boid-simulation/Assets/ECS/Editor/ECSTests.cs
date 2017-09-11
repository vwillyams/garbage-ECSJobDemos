using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using NUnit.Framework;
using UnityEngine.Collections;

namespace UnityEngine.ECS.Tests
{
	//@TODO: Tests for misconfiguring attributes...

	public class ECSFixture
	{
		protected DependencyManager m_DependencyManager;
		protected EntityManager m_Manager;

		[SetUp]
		public void Setup()
		{
			m_DependencyManager = DependencyManager.Root;
			DependencyManager.Root = new DependencyManager ();

			m_Manager = DependencyManager.GetBehaviourManager<EntityManager> ();
		}

		[TearDown]
		public void TearDown()
		{
			DependencyManager.Root.Dispose ();
			DependencyManager.Root = m_DependencyManager;
		}
	}

	public class ECS_Pure : ECSFixture
	{
		[Test]
		public void ECSCreateAndDestroy()
		{
			var go0 = m_Manager.CreateEntity ();
			var go1 = m_Manager.CreateEntity ();
			var go2 = m_Manager.CreateEntity ();

			Assert.IsFalse (m_Manager.HasComponent<EcsTestData>(go0));
			Assert.IsFalse (m_Manager.HasComponent<EcsTestData>(go1));
			Assert.IsFalse (m_Manager.HasComponent<EcsTestData>(go2));
			m_Manager.AddComponent (go0, new EcsTestData(0));
			m_Manager.AddComponent (go1, new EcsTestData(1));
			m_Manager.AddComponent (go2, new EcsTestData(2));

			Assert.IsTrue (m_Manager.HasComponent<EcsTestData>(go0));
			Assert.IsTrue (m_Manager.HasComponent<EcsTestData>(go1));
			Assert.IsTrue (m_Manager.HasComponent<EcsTestData>(go2));

			Assert.AreEqual (0, m_Manager.GetComponent<EcsTestData>(go0).value);
			Assert.AreEqual (1, m_Manager.GetComponent<EcsTestData>(go1).value);
			Assert.AreEqual (2, m_Manager.GetComponent<EcsTestData>(go2).value);

			m_Manager.Destroy (go1);

			Assert.IsTrue (m_Manager.HasComponent<EcsTestData>(go0));
			Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(go1));
			Assert.IsTrue (m_Manager.HasComponent<EcsTestData>(go2));

			Assert.AreEqual (0, m_Manager.GetComponent<EcsTestData>(go0).value);
			Assert.AreEqual (2, m_Manager.GetComponent<EcsTestData>(go2).value);

			m_Manager.Destroy (go0);
			m_Manager.Destroy (go2);

			Assert.IsFalse (m_Manager.HasComponent<EcsTestData>(go0));
			Assert.IsFalse (m_Manager.HasComponent<EcsTestData>(go1));
			Assert.IsFalse (m_Manager.HasComponent<EcsTestData>(go2));
		}

		[Test]
		public void SetComponentData()
		{
			var go = m_Manager.CreateEntity ();

			m_Manager.AddComponent (go, new EcsTestData(0));
			m_Manager.SetComponent (go, new EcsTestData(1));

			Assert.AreEqual (1, m_Manager.GetComponent<EcsTestData>(go).value);

			m_Manager.Destroy (go);
		}

		[Test]
		public void SetComponentDataOnDeletedEntity()
		{
			var go = m_Manager.CreateEntity ();
			m_Manager.AddComponent (go, new EcsTestData(0));
			m_Manager.Destroy (go);

			Assert.Throws<System.InvalidOperationException>(()=> { m_Manager.SetComponent (go, new EcsTestData(0)); });
			Assert.Throws<System.InvalidOperationException>(()=> { m_Manager.GetComponent<EcsTestData> (go); });
		}

		[Test]
		public void EntityTupleTracking()
		{
			var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();
			var ecsAndTransformArray = DependencyManager.GetBehaviourManager<EcsTestAndTransformArraySystem> ();

			var go = m_Manager.CreateEntity ();
			m_Manager.AddComponent (go, new EcsTestData(2));

			pureSystem.OnUpdate ();
			Assert.AreEqual (1, pureSystem.m_Data.Length);
			Assert.AreEqual (1, pureSystem.m_Entities.Length);
			Assert.AreEqual (2, pureSystem.m_Data[0].value);
			Assert.AreEqual (go, pureSystem.m_Entities[0]);

			ecsAndTransformArray.OnUpdate ();
			Assert.AreEqual (0, ecsAndTransformArray.m_Data.Length);
		}

		[Test]
		public void RemoveComponentTupleTracking()
		{
			var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();

			var go0 = m_Manager.CreateEntity ();
			m_Manager.AddComponent (go0, new EcsTestData(10));

			var go1 = m_Manager.CreateEntity ();
			m_Manager.AddComponent (go1, new EcsTestData(20));

			pureSystem.OnUpdate ();
			Assert.AreEqual (2, pureSystem.m_Data.Length);
			Assert.AreEqual (10, pureSystem.m_Data[0].value);
			Assert.AreEqual (20, pureSystem.m_Data[1].value);

			m_Manager.RemoveComponent<EcsTestData> (go0);

			pureSystem.OnUpdate ();
			Assert.AreEqual (1, pureSystem.m_Data.Length);
			Assert.AreEqual (20, pureSystem.m_Data[0].value);

			m_Manager.RemoveComponent<EcsTestData> (go1);
			pureSystem.OnUpdate ();
			Assert.AreEqual (0, pureSystem.m_Data.Length);
		}


		[Test]
		public void ReadOnlyTuples()
		{
			var readOnlySystem = DependencyManager.GetBehaviourManager<PureReadOnlySystem> ();

			var go = m_Manager.CreateEntity ();
			m_Manager.AddComponent (go, new EcsTestData(2));

			readOnlySystem.OnUpdate ();
			Assert.AreEqual (2, readOnlySystem.m_Data [0].value);
			Assert.Throws<System.InvalidOperationException>(()=> { readOnlySystem.m_Data[0] = new EcsTestData(); });
		}

		[Test]
		public void GroupChangeSystem()
		{
			var changeSystem = DependencyManager.GetBehaviourManager<GroupChangeSystem> ();

			var go = m_Manager.CreateEntity ();

			m_Manager.AddComponent (go, new EcsTestData(2));
			changeSystem.ExpectDidAddElements (1);
			m_Manager.Destroy (go);
			changeSystem.ExpectDidRemoveSwapBack (0);
		}

		[Test]
		[Ignore("Failing")]
		public void EcsUnalignedBoolTest()
		{
			var group = m_Manager.CreateEntityGroup (typeof(EcsBoolTestData));

			for (int i = 0; i != 1000; i++)
				m_Manager.AddComponent (m_Manager.CreateEntity (), new EcsBoolTestData (true));

			var bools = group.GetComponentDataArray<EcsBoolTestData> ();
			for (int i = 0; i != 1000; i++)
			{
				Assert.AreEqual (true, bools[i].value);
			}
		}
	}

    public class ECS_GameObject : ECSFixture
    {
        [Test]
        public void GameObjectTupleTracking()
        {
            var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();
            var ecsAndTransformArray = DependencyManager.GetBehaviourManager<EcsTestAndTransformArraySystem> ();

            var go = new GameObject ();
            var com = go.AddComponent<EcsTestComponent> ();
            com.Value = new EcsTestData(9);

            pureSystem.OnUpdate ();
            Assert.AreEqual (1, pureSystem.m_Data.Length);
            Assert.AreEqual (9, pureSystem.m_Data[0].value);

            ecsAndTransformArray.OnUpdate ();
            Assert.AreEqual (9, ecsAndTransformArray.m_Data[0].value);
            Assert.AreEqual (go.transform, ecsAndTransformArray.m_Transforms[0]);

            Object.DestroyImmediate (go);
        }

        [Test]
        public void GameObjectComponentArrayTupleTracking()
        {
            var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();
            var ecsAndComponent = DependencyManager.GetBehaviourManager<EcsTestAndTransformComponentSystem> ();

            var go = new GameObject ();
            var com = go.AddComponent<EcsTestComponent> ();
            com.Value = new EcsTestData(9);

            pureSystem.OnUpdate ();
            Assert.AreEqual (1, pureSystem.m_Data.Length);
            Assert.AreEqual (9, pureSystem.m_Data[0].value);

            ecsAndComponent.OnUpdate ();
            Assert.AreEqual (9, ecsAndComponent.m_Data[0].value);
            Assert.AreEqual (go.transform, ecsAndComponent.m_Transforms[0]);

            Object.DestroyImmediate (go);
        }

        [Test]
        public void LightInstantiateTupleTracking()
        {
            var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();

            //@TODO: Try out instantiate game object activate / deactivate
            var go = new GameObject ();
            go.SetActive (false);
            var com = go.AddComponent<EcsTestComponent> ();
            com.Value = new EcsTestData(9);

            pureSystem.OnUpdate ();
            Assert.AreEqual (0, pureSystem.m_Data.Length);

            var instances = m_Manager.Instantiate (go, 10);

            pureSystem.OnUpdate ();
            Assert.AreEqual (10, pureSystem.m_Data.Length);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual (9, pureSystem.m_Data [i].value);
                Assert.AreEqual (instances[i], pureSystem.m_Entities[i]);
            }

            instances.Dispose ();
        }
    }
}