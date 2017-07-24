using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using System.Collections.Generic;

namespace ECS
{
	[UpdateAfter("")]
	public class BoidSimulationSystem : JobComponentSystem
	{
		[InjectTuples(0)]
		ComponentDataArray<BoidData> 				m_BoidData;


		[InjectTuples(1)]
		ComponentDataArray<BoidTarget> 				m_BoidTargets;
		[InjectTuples(1)]
		ComponentArray<Transform> 					m_BoidTargetsTransforms;


		[InjectTuples(2)]
		ComponentDataArray<BoidSimulationSettings> 	m_BoidSimulationSettings;


		[InjectTuples(3)]
		ComponentDataArray<BoidGround> 				m_BoidGrounds;
		[InjectTuples(3)]
		ComponentArray<Transform> 					m_BoidGroundsTransforms;


		[InjectTuples(4)]
		ComponentDataArray<BoidObstacle> 			m_BoidObstacles;
		[InjectTuples(4)]
		ComponentArray<Transform> 					m_BoidObstacleTransforms;

		NativeMultiHashMap<int, int> 				m_Cells;

		public static bool                          UseJobs = true;

		[ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
		struct PrepareParallelBoidsJob : IJobParallelFor
		{
			[ReadOnly]
			public ComponentDataArray<BoidData> 						src;

			public NativeArray<BoidData> 								dst;

			public NativeMultiHashMap<int, int>.Concurrent 			outputCells;

			public float 												cellRadius;

			public void Execute(int index)
			{
				var boidData = src[index];
				dst[index] = boidData;

				BoidSimulationSettings.AddHashCell(boidData, index, cellRadius, outputCells);
			}
		}

		[ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
		struct PrepareBoidsJob : IJob
		{
			[ReadOnly]
			public ComponentDataArray<BoidData> 						src;

			public NativeArray<BoidData> 								dst;

			public NativeMultiHashMap<int, int> 						outputCells;

			public float 												cellRadius;

			public void Execute()
			{
				for (int index = 0; index != src.Length; index++)
				{
					var boidData = src[index];
					dst[index] = boidData;

					BoidSimulationSettings.AddHashCell(boidData, index, cellRadius, outputCells);
				}
			}
		}

        [ComputeJobOptimization(Accuracy.Med, Support.Relaxed)]
        struct SimulateBoidsJob : IJobParallelFor
		{
			public BoidSimulationSettings 								simulationSettings;
			public BoidSimulationState 									simulationState;

			public ComponentDataArray<BoidData>							outputBoids;

			[ReadOnly]
			[DeallocateOnJobCompletionAttribute]
			public NativeArray<BoidObstacle>							obstacles;

			[ReadOnly]
			[DeallocateOnJobCompletionAttribute]
			public NativeArray<BoidData> 								boids;

			[ReadOnly]
			[DeallocateOnJobCompletionAttribute]
			public NativeArray<float3> 									targetPositions;

			[ReadOnly]
			public NativeMultiHashMap<int, int>   					cells;

			[ReadOnly]
			[DeallocateOnJobCompletionAttribute]
			public NativeArray<int> 									cellOffsetsTable;

			public void Execute(int index)
			{
				var resultBoid = simulationSettings.Steer(index, simulationState, boids, obstacles, targetPositions, cells, cellOffsetsTable);

				outputBoids[index] = resultBoid;
			}
		}

		override protected void OnUpdate()
		{
			base.OnUpdate();

			if (m_BoidData.Length == 0)
				return;

			if (m_BoidSimulationSettings.Length != 1)
				return;

			CompleteDependency ();

			m_Cells.Capacity = math.max (m_Cells.Capacity, m_BoidData.Length);
			m_Cells.Clear ();

			var boids = new NativeArray<BoidData> (m_BoidData.Length, Allocator.TempJob);
			var cellOffsetsTable = new NativeArray<int>(HashUtility.cellOffsets, Allocator.TempJob);
			
			// Simulation
			var simulateJob = new SimulateBoidsJob
			{
				boids = boids,
				cells = m_Cells,
				cellOffsetsTable = cellOffsetsTable,
				simulationSettings = m_BoidSimulationSettings[0],
				outputBoids = m_BoidData,
				obstacles = new NativeArray<BoidObstacle> (m_BoidObstacles.Length, Allocator.TempJob),
			};

			simulateJob.simulationState.deltaTime = Time.deltaTime;

			for (int i = 0;i != simulateJob.obstacles.Length;i++)
			{
				var obstacle = m_BoidObstacles[i];
				obstacle.position = m_BoidObstacleTransforms[i].position;
				simulateJob.obstacles[i] = obstacle;
			}
			if (m_BoidTargetsTransforms.Length == 0)
				simulateJob.simulationSettings.targetWeight = 0;
			
			if (m_BoidGroundsTransforms.Length >= 1)
				simulateJob.simulationState.ground = m_BoidGroundsTransforms[0].position;

			simulateJob.targetPositions = new NativeArray<float3> (m_BoidTargetsTransforms.Length, Allocator.TempJob);
			for (int i = 0; i != simulateJob.targetPositions.Length; i++)
				simulateJob.targetPositions[i] = m_BoidTargetsTransforms[i].position;

			JobHandle prepareJobHandle = new JobHandle();
			// Single threaded HashTable write
			if (true)
			{
				var preparejob = new PrepareBoidsJob
				{
					src = m_BoidData,
					dst = boids,
					outputCells = m_Cells,
					cellRadius = m_BoidSimulationSettings[0].cellRadius
				};

				if (UseJobs)
				{
					prepareJobHandle = preparejob.Schedule (GetDependency ());
				}
				else
				{
					CompleteDependency ();
					preparejob.Run ();
				}
			}
			// Parallell HashTable write
			else
			{
				var prepareParallelJob = new PrepareParallelBoidsJob
				{
					src = m_BoidData,
					dst = boids,
					outputCells = m_Cells,
					cellRadius = m_BoidSimulationSettings[0].cellRadius
				};

				if (UseJobs)
				{
					prepareJobHandle = prepareParallelJob.Schedule(boids.Length, 512, GetDependency());
				}
				else
				{
					CompleteDependency ();
					prepareParallelJob.Run (boids.Length);
				}
			}

			if (UseJobs)
			{
				var simulationJobHandle = simulateJob.Schedule (boids.Length, 512, prepareJobHandle);
				AddDependency (simulationJobHandle);
			}
			else
			{
				simulateJob.Run (boids.Length);
			}

			JobHandle.ScheduleBatchedJobs ();
		}

		protected override void OnCreateManager(int capacity)
		{
			base.OnCreateManager(capacity);
			m_Cells = new NativeMultiHashMap<int, int>(capacity, Allocator.Persistent);
		}

		protected override void OnDestroyManager()
		{
			base.OnDestroyManager();
			m_Cells.Dispose ();
		}
	}
}