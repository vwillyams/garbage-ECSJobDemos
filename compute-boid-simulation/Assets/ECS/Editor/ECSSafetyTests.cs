using UnityEngine.ECS;
using NUnit.Framework;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;
using System;

namespace UnityEngine.ECS.Tests
{
    public class ECSSafetyTests : ECSFixture
	{
        [Test]
        public void ReadOnlyComponentDataArray()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(EcsTestData2));
            var arr = group.GetComponentDataArray<EcsTestData>();
            Assert.AreEqual(0, arr.Length);

            var entity = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponent(entity, new EcsTestData(42));
            arr = group.GetComponentDataArray<EcsTestData>();
            Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(42, arr[0].value);
            arr = group.GetComponentDataArray<EcsTestData>(true);
            Assert.AreEqual(1, arr.Length);
            Assert.Throws<System.InvalidOperationException>(() => { arr[0] = new EcsTestData(0); });
            arr = group.GetComponentDataArray<EcsTestData>();
            Assert.AreEqual(1, arr.Length);
            arr[0] = new EcsTestData(0);
            Assert.AreEqual(0, arr[0].value);

            m_Manager.DestroyEntity(entity);
        }

        struct TestComponentWriteJob : IJob
        {
            public ComponentDataArray<EcsTestData> data;
            public void Execute()
            { }
        }
        [Test]
        public void ComponentDataArrayJobSafety()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(EcsTestData2));
            var entity = m_Manager.CreateEntity(archetype);
            var arr = group.GetComponentDataArray<EcsTestData>();
            m_Manager.SetComponent(entity, new EcsTestData(42));

            var job = new TestComponentWriteJob();
            job.data = arr;
            Assert.AreEqual(42, arr[0].value);
            var fence = job.Schedule();
            Assert.Throws<System.InvalidOperationException>(() => { var f = arr[0].value; });
            fence.Complete();
            Assert.AreEqual(42, arr[0].value);

            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void AccessComponentArrayAfterCreationThrowsException()
        {
            CreateEntityWithDefaultData(0);

            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
            var arr = group.GetComponentDataArray<EcsTestData>();

            CreateEntityWithDefaultData(1);

            Assert.Throws<InvalidOperationException>(() => { var value = arr[0]; });
        }

        [Test]
        [Ignore("TODO")]
        public void ForgetAddJobDependencyIsCaughtInComponentSystem()
        {
            throw new System.NotImplementedException();
            // * Give error immediately about missing AddDependency call?
            // * Sync against other job even if it was forgotten.
        }

        [Test]
        [Ignore("Figure out how we can get a stress test going, covering various functionality and testing that nothing gets corrupted")]
        public void DestroyEntityWhileJobIsRunning()
        {
        }
    }
}