using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    public class IJobProcessComponentDataTests :ECSTestsFixture
    {
        [DisableAutoCreation]
        public class ProxySystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return inputDeps;
            }
        }
        
        struct ProcessSimple : IJobProcessComponentData<EcsTestData, EcsTestData2>
        {
            public void Execute([ReadOnly]ref EcsTestData src, ref EcsTestData2 dst)
            {
                dst.value1 = dst.value0 = src.value;
            }
        }
        
        [Test]
        public void JobProcessSimple()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            m_Manager.SetComponentData(entity, new EcsTestData(42));

            new ProcessSimple().Schedule(World.GetOrCreateManager<ProxySystem>(), 64).Complete();
            
            //@TODO: Check that the created ComponentGroup is actually readonly
            // system.ComponentGroups[0].GetComponentDataArray<EcsTestData>()

            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
        }
        
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void JobWithMissingDependency()
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled, "JobDebugger must be enabled for these tests");

            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var system = World.GetOrCreateManager<ProxySystem>();
            var job = new ProcessSimple().Schedule(system, 64);
            Assert.Throws<InvalidOperationException>(() => { new ProcessSimple().Schedule(system, 64); });
            
            job.Complete();
        }
#endif
        
        [RequireSubtractiveComponent(typeof(EcsTestData3))]
        [RequireComponentTag(typeof(EcsTestData4))]
        struct ProcessTagged : IJobProcessComponentData<EcsTestData, EcsTestData2>
        {
            public void Execute(ref EcsTestData src, ref EcsTestData2 dst)
            {
                dst.value1 = dst.value0 = src.value;
            }
        }
        
        void Test(bool didProcess, Entity entity)
        {
            m_Manager.SetComponentData(entity, new EcsTestData(42));

            new ProcessTagged().Schedule(World.GetOrCreateManager<ProxySystem>(), 64).Complete();

            if (didProcess)
                Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
            else
                Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
        }

        [Test]
        public void JobProcessAdditionalRequirements()
        {
            var entityIgnore0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            Test(false, entityIgnore0);
            
            var entityIgnore1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            Test(false, entityIgnore1);

            var entityProcess = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData4));
            Test(true, entityProcess);
        }


        [Test]
        [Ignore("TODO")]
        public void TestCoverageFor_ComponentSystemBase_InjectNestedIJobProcessComponentDataJobs()
        {
        }
    }
}