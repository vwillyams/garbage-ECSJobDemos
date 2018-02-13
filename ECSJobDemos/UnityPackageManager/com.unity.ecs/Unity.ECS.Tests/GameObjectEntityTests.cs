using System;
using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.ECS;
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
        public void GameObjectEntityNotAdded()
        {
            var go = new GameObject("test", typeof(GameObjectEntity));
            var entity = GameObjectEntity.AddToEntityManager(m_Manager, go);
            Assert.Throws<ArgumentException>(() => { m_Manager.HasComponent<GameObjectEntity>(entity); });
        }
        
        [Test]
        public void ComponentDataAndTransformArray()
        {
            var go = new GameObject("test", typeof(EcsTestComponent));
            var entity = GameObjectEntity.AddToEntityManager(m_Manager, go);
            
            m_Manager.SetComponentData(entity, new EcsTestData(5));
            
			var grp = m_Manager.CreateComponentGroup(typeof(Transform), typeof(EcsTestData));
			var arr = grp.GetComponentArray<Transform>();
            
			Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(go.transform, arr[0]);
            Assert.AreEqual(5, grp.GetComponentDataArray<EcsTestData>()[0].value);

            Object.DestroyImmediate (go);
        }

        [Test]
        public void RigidbodyComponentArray()
        {
            var go = new GameObject("test", typeof(Rigidbody));
            /*var entity =*/ GameObjectEntity.AddToEntityManager(m_Manager, go);

            var grp = m_Manager.CreateComponentGroup(typeof(Rigidbody));

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
            var go = new GameObject("test", typeof(Rigidbody), typeof(Light));
            var entity = GameObjectEntity.AddToEntityManager(m_Manager, go);

            m_Manager.AddComponentData(entity, new EcsTestData(5));
            m_Manager.AddComponentData(entity, new EcsTestData2(6));

            var cache = new ComponentGroupArrayStaticCache(typeof(MyEntity), m_Manager);
            
            var array = new ComponentGroupArray<MyEntity>(cache);
            int iterations = 0;
            foreach (var e in array )
            {
                Assert.AreEqual(5, e.testData->value);
                Assert.AreEqual(6, e.testData2->value0);
                Assert.AreEqual(go.GetComponent<Light>(), e.light);
                Assert.AreEqual(go.GetComponent<Rigidbody>(), e.rigidbody);
                iterations++;
            }
            Assert.AreEqual(1, iterations);

            cache.Dispose();
            Object.DestroyImmediate(go);
        }
    }
}