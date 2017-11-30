using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace BoidSimulations
{
	public class BoidSimulationSystem : JobComponentSystem
	{
		struct Boids
		{
			public ComponentDataArray<BoidData> boids;
			public int 							Length;
		}
		[InjectComponentGroup] Boids m_Boids;


		struct BoidTargets
		{
			[ReadOnly] 
			public ComponentDataArray<BoidTarget> boidTargets;
			public ComponentArray<Transform>      transforms;
			public int 							  Length;
		}
		[InjectComponentGroup] BoidTargets m_Targets;

		struct Settings
		{
			[ReadOnly] 
			public ComponentDataArray<BoidSimulationSettings> settings;
			public int  									  Length;
		}
		[InjectComponentGroup] Settings m_Settings;

		struct Grounds
		{
			[ReadOnly] 
			public ComponentDataArray<BoidGround> grounds;
			public ComponentArray<Transform> 	  transforms;
			public int 							  Length;

		}
		[InjectComponentGroup] Grounds m_Ground;

		struct BoidObstacles
		{
			[ReadOnly]
			public ComponentDataArray<BoidObstacle> obstacles;
			public ComponentArray<Transform> 		transforms;
			public int 							    Length;

		}
		[InjectComponentGroup] BoidObstacles m_BoidObstacles;

		NativeMultiHashMap<int, int> 				m_Cells;

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
				for (int i = 0;i != src.Length;i++)
				{
					var boidData = src[i];
					dst[i] = boidData;

					BoidSimulationSettings.AddHashCell(boidData, i, cellRadius, outputCells);
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
			public NativeMultiHashMap<int, int>   					    cells;

			[ReadOnly]
			[DeallocateOnJobCompletionAttribute]
			public NativeArray<int> 									cellOffsetsTable;

			public void Execute(int index)
			{
				var resultBoid = simulationSettings.Steer(index, simulationState, boids, obstacles, targetPositions, cells, cellOffsetsTable);

				outputBoids[index] = resultBoid;
			}
		}

		override public JobHandle OnUpdateForJob(JobHandle inputDeps)
		{
			if (m_Boids.Length == 0)
				return inputDeps;

			if (m_Settings.Length != 1)
				return inputDeps;

			m_Cells.Capacity = math.max (m_Cells.Capacity, m_Boids.Length);
			m_Cells.Clear ();

			var boids = new NativeArray<BoidData> (m_Boids.Length, Allocator.TempJob);
			var cellOffsetsTable = new NativeArray<int>(HashUtility.cellOffsets, Allocator.TempJob);
			
			// Simulation
			var simulateJob = new SimulateBoidsJob
			{
				boids = boids,
				cells = m_Cells,
				cellOffsetsTable = cellOffsetsTable,
				simulationSettings = m_Settings.settings[0],
				outputBoids = m_Boids.boids,
				obstacles = new NativeArray<BoidObstacle> (m_BoidObstacles.Length, Allocator.TempJob),
			};

			simulateJob.simulationState.deltaTime = Time.deltaTime;

			for (int i = 0;i != m_BoidObstacles.Length;i++)
			{
				var obstacle = m_BoidObstacles.obstacles[i];
				obstacle.position = m_BoidObstacles.transforms[i].position;
				simulateJob.obstacles[i] = obstacle;
			}
			if (m_Targets.Length == 0)
				simulateJob.simulationSettings.targetWeight = 0;
			
			if (m_Ground.Length >= 1)
				simulateJob.simulationState.ground = m_Ground.transforms[0].position;

			simulateJob.targetPositions = new NativeArray<float3> (m_Targets.Length, Allocator.TempJob);
			for (int i = 0; i != m_Targets.Length; i++)
				simulateJob.targetPositions[i] = m_Targets.transforms[i].position;

			JobHandle prepareJobHandle; 
			bool concurrentHashmap = false;
			if (concurrentHashmap)
			{
				var prepareParallelJob = new PrepareParallelBoidsJob
				{
					src = m_Boids.boids,
					dst = boids,
					outputCells = m_Cells,
					cellRadius = m_Settings.settings[0].cellRadius
				};
				prepareJobHandle = prepareParallelJob.Schedule(boids.Length, 32, inputDeps);
			}
			else
			{
				var prepareJob = new PrepareBoidsJob
				{
					src = m_Boids.boids,
					dst = boids,
					outputCells = m_Cells,
					cellRadius = m_Settings.settings[0].cellRadius
				};
				prepareJobHandle = prepareJob.Schedule(inputDeps);
			}
			
			return simulateJob.Schedule (boids.Length, 32, prepareJobHandle);
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