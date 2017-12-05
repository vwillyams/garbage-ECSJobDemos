using NUnit.Framework;
using Unity.Jobs;
using Unity.Collections;

namespace UnityEngine.ECS.Tests
{
    public class ComponentGroupArrayTests : ECSTestsFixture
	{
        public ComponentGroupArrayTests()
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled, "JobDebugger must be enabled for these tests");
        }

		struct TestCopy1To2Job : IJob
		{
			public ComponentGroupArray<TestEntity> entities;
			unsafe public void Execute()
			{
				foreach (var e in entities)
					e.testData2->value0 = e.testData->value; 
			}
		}
	    
		struct TestReadOnlyJob : IJob
		{
			public ComponentGroupArray<TestEntityReadOnly> entities;
			public void Execute()
			{
				foreach (var e in entities)
					;
			}
		}
		
	    //@TODO: Test for Entity setup with same component twice...
	    //@TODO: Test for subtractive components
	    //@TODO: Test for process ComponentGroupArray in job
	    
	    unsafe struct TestEntity
	    {
	        [ReadOnly]
	        public EcsTestData* testData;
	        public EcsTestData2* testData2;
	    }

		unsafe struct TestEntityReadOnly
		{
			[ReadOnly]
			public EcsTestData* testData;
			[ReadOnly]
			public EcsTestData2* testData2;
		}
		
		
	    [Test]
	    public void ComponentAccessAfterScheduledJobThrowsEntityArray()
	    {
	        var entityArrayCache = new ComponentGroupArrayStaticCache(typeof(TestEntity), m_Manager);
	        var group = m_Manager.CreateComponentGroup(typeof(EcsTestData));
	        m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

	        var job = new TestCopy1To2Job();
		    job.entities = new ComponentGroupArray<TestEntity>(entityArrayCache);

	        var fence = job.Schedule();
            
	        var entityArray = new ComponentGroupArray<TestEntity>(entityArrayCache);
	        Assert.Throws<System.InvalidOperationException>(() => { var temp = entityArray[0]; });

	        fence.Complete();
	    }

	    [Test]
	    public void ComponentGroupArrayJobScheduleDetectsWriteDependency()
	    {
	        var entityArrayCache = new ComponentGroupArrayStaticCache(typeof(TestEntity), m_Manager);
	        var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
	        m_Manager.SetComponent(entity, new EcsTestData(42));

	        var job = new TestCopy1To2Job();
	        job.entities = new ComponentGroupArray<TestEntity>(entityArrayCache);

	        var fence = job.Schedule();
			Assert.Throws<System.InvalidOperationException>(() => { job.Schedule(); });
			
	        fence.Complete();

		    entityArrayCache.Dispose();
	    }
		
		[Test]
		public void ComponentGroupArrayJobScheduleReadOnlyParallelIsAllowed()
		{
			var entityArrayCache = new ComponentGroupArrayStaticCache(typeof(TestEntityReadOnly), m_Manager);
			var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
			m_Manager.SetComponent(entity, new EcsTestData(42));

			var job = new TestReadOnlyJob();
			job.entities = new ComponentGroupArray<TestEntityReadOnly>(entityArrayCache);

			var fence = job.Schedule();
			var fence2 = job.Schedule();
			
			JobHandle.CompleteAll(ref fence, ref fence2);
			entityArrayCache.Dispose();
		}
    }
}