using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;

namespace ECS
{
	//@TODO: Tests for misconfiguring attributes...

	public class ECSTests
	{
		DependencyManager m_DependencyManager;
		LightweightGameObjectManager m_Manager;

		[SetUp]
		public void Setup()
		{
			m_DependencyManager = DependencyManager.Root;
			DependencyManager.Root = new DependencyManager ();

			m_Manager = DependencyManager.GetBehaviourManager<LightweightGameObjectManager> ();
		}

		[TearDown]
		public void TearDown()
		{
			DependencyManager.Root.Dispose ();
			DependencyManager.Root = m_DependencyManager;
		}

		[Test]
		public void ECSCreateAndDestroy()
		{
			var go0 = m_Manager.AllocateGameObject ();
			var go1 = m_Manager.AllocateGameObject ();
			var go2 = m_Manager.AllocateGameObject ();

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
			var go = m_Manager.AllocateGameObject ();

			m_Manager.AddComponent (go, new EcsTestData(0));
			m_Manager.SetComponent (go, new EcsTestData(1));

			Assert.AreEqual (1, m_Manager.GetComponent<EcsTestData>(go).value);

			m_Manager.Destroy (go);
		}

		[Test]
		public void SetComponentDataOnDeletedGameObject()
		{
			var go = m_Manager.AllocateGameObject ();
			m_Manager.AddComponent (go, new EcsTestData(0));
			m_Manager.Destroy (go);

			Assert.Throws<System.InvalidOperationException>(()=> { m_Manager.SetComponent (go, new EcsTestData(0)); });
			Assert.Throws<System.InvalidOperationException>(()=> { m_Manager.GetComponent<EcsTestData> (go); });
		}

		[Test]
		public void LightWeightGameObjectTupleTracking()
		{
			var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();
			var ecsAndTransformArray = DependencyManager.GetBehaviourManager<EcsTestAndTransformArraySystem> ();

			var go = m_Manager.AllocateGameObject ();
			m_Manager.AddComponent (go, new EcsTestData(2));

			pureSystem.OnUpdate ();
			Assert.AreEqual (1, pureSystem.m_Data.Length);
			Assert.AreEqual (2, pureSystem.m_Data[0].value);

			ecsAndTransformArray.OnUpdate ();
			Assert.AreEqual (0, ecsAndTransformArray.m_Data.Length);
		}

		[Test]
		public void RemoveComponentTupleTracking()
		{
			var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();

			var go0 = m_Manager.AllocateGameObject ();
			m_Manager.AddComponent (go0, new EcsTestData(10));

			var go1 = m_Manager.AllocateGameObject ();
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
				Assert.AreEqual (9, pureSystem.m_Data[i].value);

			instances.Dispose ();
		}
	}
}