using UnityEngine.ECS;
using UnityEngine.ECS.Experimental.Slow;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    //@TODO: Test for prevent adding Wrapper component to type system...

	public class GameObjectEntityTests : ECSTestsFixture
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


        struct MyEntity
        {
            public Light      light;
            public Rigidbody  rigidbody;
        }

        [Test]
        public void ComponentEnumerator()
        {
            var entityMan = DependencyManager.GetBehaviourManager<EntityManager>();

            var go = new GameObject();
            go.AddComponent<Rigidbody>();
            go.AddComponent<Light>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.AddComponent<GameObjectEntity>().OnEnable();

            int iterations = 0;
            var enumerator = new ComponentGroupEnumerable<MyEntity>(entityMan);
            foreach (var entity in enumerator)
            {
                Assert.AreEqual(go.GetComponent<Light>(), entity.light);
                Assert.AreEqual(go.GetComponent<Rigidbody>(), entity.rigidbody);
                iterations++;
            }
            Assert.AreEqual(1, iterations);

            Object.DestroyImmediate(go);
        }
    }
}