using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
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
            var entityMan = World.GetOrCreateManager<EntityManager> ();

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
            var entityMan = World.GetOrCreateManager<EntityManager>();

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

        unsafe struct MyEntity
        {
            public Light              light;
            public Rigidbody          rigidbody;
            
            public EcsTestData*       testData;
            public EcsTestData2*      testData2;
        }

        [Test]
        [Ignore("TODO")]
        public void ComponentEnumeratorInvalidChecks()
        {
            //* Check for string in MyEntity and other illegal constructs...
        }

        [Test]
        [Ignore("TODO")]
        public void AddComponentDuringForeachProtection()
        {
            //* Check for string in MyEntity and other illegal constructs...
        }
        [Test]
        unsafe public void ComponentEnumerator()
        {
            var entityManager = World.GetOrCreateManager<EntityManager>();

            var go = new GameObject();
            go.AddComponent<Rigidbody>();
            go.AddComponent<Light>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.AddComponent<GameObjectEntity>().OnEnable();

            var entity = go.GetComponent<GameObjectEntity>().Entity;
            entityManager.AddComponent(entity, new EcsTestData(5));
            entityManager.AddComponent(entity, new EcsTestData2(6));

            int iterations = 0;
            var enumerator = new ComponentGroupArray<MyEntity>(entityManager);
            foreach (var e in enumerator)
            {
                Assert.AreEqual(5, e.testData->value);
                Assert.AreEqual(6, e.testData2->value0);
                Assert.AreEqual(go.GetComponent<Light>(), e.light);
                Assert.AreEqual(go.GetComponent<Rigidbody>(), e.rigidbody);
                iterations++;
            }
            Assert.AreEqual(1, iterations);

            Object.DestroyImmediate(go);
        }
    }
}