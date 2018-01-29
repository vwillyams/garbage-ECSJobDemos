﻿using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Tests
{
	public class InjectComponentGroupTests : ECSTestsFixture
	{
		[DisableAutoCreation]
		public class PureEcsTestSystem : ComponentSystem
		{
			public struct DataAndEntites
			{
				public ComponentDataArray<EcsTestData> Data;
				public EntityArray                     Entities;
				public int                             Length;
			}

			[InjectComponentGroup] 
			public DataAndEntites Group;

			protected override void OnUpdate()
			{
			}
		}

		[DisableAutoCreation]
		public class PureReadOnlySystem : ComponentSystem
		{
			public struct Datas
			{
				[ReadOnly]
				public ComponentDataArray<EcsTestData> Data;
			}

			[InjectComponentGroup] 
			public Datas Group;

			protected override void OnUpdate()
			{
			}
		}

		[DisableAutoCreation]
		public class SubtractiveSystem : ComponentSystem
		{
			public struct Datas
			{
				public ComponentDataArray<EcsTestData> Data;
				public SubtractiveComponent<EcsTestData2> Data2;
			}

			[InjectComponentGroup] 
			public Datas Group;

			protected override void OnUpdate()
			{
			}
		}

		
		
		[Test]
        public void ReadOnlyComponentDataArray()
        {
            var readOnlySystem = World.GetOrCreateManager<PureReadOnlySystem> ();

            var go = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go, new EcsTestData(2));

            readOnlySystem.Update ();
            Assert.AreEqual (2, readOnlySystem.Group.Data[0].value);
            Assert.Throws<System.InvalidOperationException>(()=> { readOnlySystem.Group.Data[0] = new EcsTestData(); });
        }

		[Test]
        public void SubtractiveComponent()
        {
            var subtractiveSystem = World.GetOrCreateManager<SubtractiveSystem> ();

            var go = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go, new EcsTestData(2));

            subtractiveSystem.Update ();
            Assert.AreEqual (1, subtractiveSystem.Group.Data.Length);
            Assert.AreEqual (2, subtractiveSystem.Group.Data[0].value);
            m_Manager.AddComponent (go, new EcsTestData2());
            subtractiveSystem.Update ();
            Assert.AreEqual (0, subtractiveSystem.Group.Data.Length);
        }
        
        [Test]
        public void RemoveComponentGroupTracking()
        {
            var pureSystem = World.GetOrCreateManager<PureEcsTestSystem> ();

            var go0 = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go0, new EcsTestData(10));

            var go1 = m_Manager.CreateEntity ();
            m_Manager.AddComponent (go1, new EcsTestData(20));

            pureSystem.Update ();
            Assert.AreEqual (2, pureSystem.Group.Length);
            Assert.AreEqual (10, pureSystem.Group.Data[0].value);
            Assert.AreEqual (20, pureSystem.Group.Data[1].value);

            m_Manager.RemoveComponent<EcsTestData> (go0);

            pureSystem.Update ();
            Assert.AreEqual (1, pureSystem.Group.Length);
            Assert.AreEqual (20, pureSystem.Group.Data[0].value);

            m_Manager.RemoveComponent<EcsTestData> (go1);
            pureSystem.Update ();
            Assert.AreEqual (0, pureSystem.Group.Length);
        }

        [Test]
        public void EntityGroupTracking()
        {
            var pureSystem = World.GetOrCreateManager<PureEcsTestSystem> ();

            var go = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go, new EcsTestData(2));

            pureSystem.Update ();
            Assert.AreEqual (1, pureSystem.Group.Length);
            Assert.AreEqual (1, pureSystem.Group.Data.Length);
            Assert.AreEqual (1, pureSystem.Group.Entities.Length);
            Assert.AreEqual (2, pureSystem.Group.Data[0].value);
            Assert.AreEqual (go, pureSystem.Group.Entities[0]);
        }
		
		[DisableAutoCreation]
		public class FromEntitySystemIncrementInJob : JobComponentSystem
		{
			public struct IncrementValueJob : IJob
			{
				public Entity entity;
				
				public ComponentDataFromEntity<EcsTestData> ecsTestDataFromEntity;
				public FixedArrayFromEntity<int> intArrayFromEntity;

				public void Execute()
				{
					var array = intArrayFromEntity[entity];
					for (int i = 0;i<array.Length;i++)
						array[i]++;

					var value = ecsTestDataFromEntity[entity];
					value.value++;
					ecsTestDataFromEntity[entity] = value;
				}
			}

			[InjectComponentFromEntity]
			FixedArrayFromEntity<int> intArrayFromEntity;
			
			[InjectComponentFromEntity]
			ComponentDataFromEntity<EcsTestData> ecsTestDataFromEntity;

			public Entity entity;
			
			protected override JobHandle OnUpdate(JobHandle inputDeps)
			{
				var job = new IncrementValueJob();
				job.entity = entity;
				job.ecsTestDataFromEntity = ecsTestDataFromEntity;
				job.intArrayFromEntity = intArrayFromEntity;

				return job.Schedule(inputDeps);
			}
		}
		
		[Test]
		public void FromEntitySystemIncrementInJobWorks()
		{
			var system = World.GetOrCreateManager<FromEntitySystemIncrementInJob> ();

			var entity = m_Manager.CreateEntity (typeof(EcsTestData), ComponentType.FixedArray(typeof(int), 5));
			system.entity = entity;
			system.Update();
			system.Update();

			Assert.AreEqual(2, m_Manager.GetComponent<EcsTestData>(entity).value);
			Assert.AreEqual(2, m_Manager.GetFixedArray<int>(entity)[0]);
		}

		[DisableAutoCreation]
	    public class OnDestroyManagerJobsHaveCompletedJobSystem : JobComponentSystem
	    {
	        struct Job : IJob
	        {
	            public ComponentDataArray<EcsTestData> data;

	            public void Execute()
	            {
	                data[0] = new EcsTestData(42);
	            }
	        }
        
	        public struct Group
	        {
	            public ComponentDataArray<EcsTestData> Data;
	        }

	        [InjectComponentGroup] 
	        public Group group;

	        protected override void OnDestroyManager()
	        {
		        UpdateInjectedComponentGroups();
		        
	            var job = new Job();
	            job.data = group.Data;
	            AddDependencyInternal(job.Schedule());

	            base.OnDestroyManager();
            
	            // base.OnDestroyManager(); will wait for the job
	            // and ensure that you can safely access the injected groups
	            Assert.AreEqual(42, group.Data[0].value);   
	        }
	    }

		[Test]
		public void OnDestroyManagerJobsHaveCompleted()
		{
			m_Manager.CreateEntity (typeof(EcsTestData));
			World.GetOrCreateManager<OnDestroyManagerJobsHaveCompletedJobSystem>();
			TearDown();
		}

		[DisableAutoCreation]
		public class OnCreateManagerComponentGroupInjectionSystem : JobComponentSystem
		{
			public struct Group
			{
				public ComponentDataArray<EcsTestData> Data;
			}

			[InjectComponentGroup] 
			public Group group;

			protected override void OnCreateManager(int capacity)
			{
				// base.OnCreateManager should inject the component group,
				// so that any code in OnCreateManager can access them
				base.OnCreateManager(capacity);
				
				Assert.AreEqual(1, group.Data.Length);
				Assert.AreEqual(42, group.Data[0].value);
			}
		}
		
		[Test]
		public void OnCreateManagerComponentGroupInjectionWorks()
		{
			var entity = m_Manager.CreateEntity (typeof(EcsTestData));
			m_Manager.SetComponent(entity, new EcsTestData(42));
			World.GetOrCreateManager<OnCreateManagerComponentGroupInjectionSystem>();
		}
	}
}