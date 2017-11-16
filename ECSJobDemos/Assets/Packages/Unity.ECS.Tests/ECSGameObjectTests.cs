    using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    //@TODO: Test for prevent adding Wrapper component to type system...

	public class ECS_GameObject : ECSFixture
    {
        [Test]
        [Ignore("not implemented")]
        public void ComponentArrayWithParentClass() { }


        [Test]
        public void TransformAccessArrayTests()
        {
            
        }

        [Test]
        public void ComponentDataAndTransformArray()
        {
            var entityMan = DependencyManager.GetBehaviourManager<EntityManager> ();

            var go = new GameObject ();
            go.AddComponent<EcsTestComponent> ();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            entityMan.SetComponent(go.GetComponent<GameObjectEntity>().Entity, new EcsTestData(5));

			var grp = entityMan.CreateComponentGroup(typeof(Transform), typeof(EcsTestData));

			var arr = grp.GetComponentArray<Transform>();
			Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(go.transform, arr[0]);
            Assert.AreEqual(5, grp.GetComponentDataArray<EcsTestData>()[0].value);

            Object.DestroyImmediate (go);
        }

        [Test]
        public void RigidbodyComponentArray()
        {
            var entityMan = DependencyManager.GetBehaviourManager<EntityManager>();

            var go = new GameObject();
            go.AddComponent<Rigidbody>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.AddComponent<GameObjectEntity>().OnEnable();

            var grp = entityMan.CreateComponentGroup(typeof(Rigidbody));

            var arr = grp.GetComponentArray<Rigidbody>();
            Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(go.GetComponent<UnityEngine.Rigidbody>(), arr[0]);

            Object.DestroyImmediate(go);
        }
    }
}