using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;

namespace ECS
{
	public class ECSTests
	{
		#if false
	    [Test]
	    public void TestEcs()
	    {
			var manager = DependencyManager.GetBehaviourManager(typeof(SystemRotator)) as SystemRotator;

			var go1 = new GameObject("test1", typeof(LightRotatorHolder));
			var go2 = new GameObject("test2", typeof(LightRotatorHolder));
			var go3 = new GameObject("test3", typeof(LightRotatorHolder));

			Assert.AreEqual(3, manager.m_Transforms.Count);
			Assert.AreEqual(3, manager.m_Rotators.Length);

			Assert.AreEqual(go1.transform, manager.m_Transforms[0]);
			Assert.AreEqual(0, manager.m_Rotators[0].speed);

			Assert.AreEqual(go2.transform, manager.m_Transforms[1]);
			Assert.AreEqual(0, manager.m_Rotators[1].speed);

			Assert.AreEqual(go3.transform, manager.m_Transforms[2]);
			Assert.AreEqual(0, manager.m_Rotators[2].speed);

			Object.DestroyImmediate (go2);
			try
			{
				Assert.AreEqual(2, manager.m_Transforms.Count);
				Assert.AreEqual(2, manager.m_Rotators.Length);

				Assert.AreEqual(go1.transform, manager.m_Transforms[0]);
				Assert.AreEqual(0, manager.m_Rotators[0].speed);

				Assert.AreEqual(go3.transform, manager.m_Transforms[1]);
				Assert.AreEqual(0, manager.m_Rotators[1].speed);
			}
			finally
			{
				Object.DestroyImmediate (go1);
				Object.DestroyImmediate (go3);
			}
	    }
		#endif

		[Test]
		public void TestEcsGO()
		{
			var gos = DependencyManager.GetBehaviourManager<LightweightGameObjectManager> ();

			var go = new GameObject ("test", typeof(RotationSpeedDataComponent));
			go.GetComponent<RotationSpeedDataComponent> ().Value = new RotationSpeed(50);

			var instances = gos.Instantiate (go, 3);

			Assert.AreEqual (3, instances.Length);
			Assert.AreEqual (50, gos.GetLightweightComponent<RotationSpeed> (instances [0]).speed);
			Assert.AreEqual (50, gos.GetLightweightComponent<RotationSpeed> (instances [1]).speed);
			Assert.AreEqual (50, gos.GetLightweightComponent<RotationSpeed> (instances [2]).speed);
			Assert.AreEqual (50, go.GetComponent<RotationSpeedDataComponent> ().Value.speed);

			go.GetComponent<RotationSpeedDataComponent> ().Value = new RotationSpeed(-1);
			gos.SetLightweightComponent<RotationSpeed> (instances [0], new RotationSpeed (0));
			gos.SetLightweightComponent<RotationSpeed> (instances [1], new RotationSpeed (100));
			gos.SetLightweightComponent<RotationSpeed> (instances [2], new RotationSpeed (200));

			gos.Destroy (instances[1]);

			Assert.AreEqual (-1, go.GetComponent<RotationSpeedDataComponent> ().Value.speed);
			Assert.AreEqual (0, gos.GetLightweightComponent<RotationSpeed> (instances [0]).speed);
			Assert.AreEqual (200, gos.GetLightweightComponent<RotationSpeed> (instances [2]).speed);

	//@TODO: Add this test... Currently can't be done because 
	//		Assert.Throws<System.InvalidOperationException> (() => {
	//			gos.GetLightweightComponent<RotationSpeed> (instances [1]);
	//		});
			
			gos.Destroy (instances[2]);
			gos.Destroy (instances[0]);

			instances.Dispose ();

			Object.DestroyImmediate (go);
		}

	}
}