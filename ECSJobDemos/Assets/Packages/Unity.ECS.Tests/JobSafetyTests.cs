using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.TestTools;

//@TODO: We should really design systems / jobs / exceptions / errors 
//       so that an error in one system does not affect the next system.
//       Right now failure to set dependencies correctly in one system affects other code,
//       this makes the error messages significantly less useful...
//       So need to redo all tests accordingly

namespace UnityEngine.ECS.Tests
{
    public class JobSafetyTests : ECSTestsFixture
	{
        public JobSafetyTests()
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled, "JobDebugger must be enabled for these tests");
        }

        struct TestIncrementJob : IJob
        {
            public ComponentDataArray<EcsTestData> data;
            public void Execute()
            {
                for (int i = 0; i != data.Length; i++)
                {
                    var d = data[i];
                    d.value++;
                    data[i] = d;
                }
            }
        }

		
		
        [Test]
        public void ComponentAccessAfterScheduledJobThrows()
        {
            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponent(entity, new EcsTestData(42));

            var job = new TestIncrementJob();
            job.data = group.GetComponentDataArray<EcsTestData>();
            Assert.AreEqual(42, job.data[0].value);

            var fence = job.Schedule();
            
            Assert.Throws<System.InvalidOperationException>(() => { var f = job.data[0].value; });

            fence.Complete();
            Assert.AreEqual(43, job.data[0].value);
        }

        [Test]
        public void GetComponentCompletesJob()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.data = group.GetComponentDataArray<EcsTestData>();
            group.AddDependency(job.Schedule());

            // Implicit Wait for job, returns value after job has completed.
            Assert.AreEqual(1, m_Manager.GetComponent<EcsTestData>(entity).value);
        }

        [Test]
        public void DestroyEntityCompletesScheduledJobs()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData));
            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.data = group.GetComponentDataArray<EcsTestData>();
            group.AddDependency(job.Schedule());

            m_Manager.DestroyEntity(entity);

            // @TODO: This is maybe a little bit dodgy way of determining if the job has been completed...
            //        Probably should expose api to inspector job debugger state...
            Assert.AreEqual(1, group.GetComponentDataArray<EcsTestData>().Length);
            Assert.AreEqual(1, group.GetComponentDataArray<EcsTestData>()[0].value);
        }

        [Test]
        public void EntityManagerDestructionDetectsUnregisteredJob()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("job is still running"));

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.data = group.GetComponentDataArray<EcsTestData>();
            job.Schedule();

            TearDown();
        }

        [Test]
        public void DestroyEntityDetectsUnregisteredJob()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.data = group.GetComponentDataArray<EcsTestData>();
            var fence = job.Schedule();

            Assert.Throws<System.InvalidOperationException>(() => { m_Manager.DestroyEntity(entity); });

            fence.Complete();
        }

        [Test]
        public void GetComponentDetectsUnregisteredJob()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.data = group.GetComponentDataArray<EcsTestData>();
            var jobHandle = job.Schedule();

            Assert.Throws<System.InvalidOperationException>(() => { m_Manager.GetComponent<EcsTestData>(entity); });

            jobHandle.Complete();
        }


        [Test]
        [Ignore("TODO")]
        public void ForgetAddJobDependencyIsCaughtInComponentSystem()
        {
            throw new System.NotImplementedException();
            // * Give error immediately about missing AddDependency call?
            // * Sync against other job even if it was forgotten.
        }
    }
}