using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.Collections;

namespace BoidcloudSimulation
{
	public class BoidCloud : MonoBehaviour
	{
		[ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
		public struct BoidsJob : IJobParallelFor
		{
			public BoidSimulationSettings 						simulationSettings;
			public BoidSimulationState 							simulationState;

			[ReadOnly]
			public NativeArray<BoidData> 						boids;
			[ReadOnly]
			public NativeMultiHashMap<int, int>   			cells;

			[ReadOnly]
			[DeallocateOnJobCompletionAttribute]
			public NativeArray<int> 							cellOffsetsTable;

			public NativeArray<BoidData>						outputBoids;
			public NativeMultiHashMap<int, int>.Concurrent	outputCells;

			public void Execute (int index)
			{
				var resultBoid = simulationSettings.Steer(index, simulationState, boids, cells, cellOffsetsTable);

				BoidSimulationSettings.AddHashCell (resultBoid, index, simulationSettings.radius, outputCells);
				outputBoids[index] = resultBoid;
			}
		}

		public struct BoidsTransformJob : IJobParallelForTransform
		{
			[ReadOnly]
			public NativeArray<BoidData> 	boidsBuffer;

			public void Execute (int index, TransformAccess transform)
			{
				var boid = boidsBuffer[index];
				transform.position = boid.position;
				transform.rotation = Quaternion.LookRotation(boid.forward);
			}
		}


		public int 					m_InitialBoidsCount;
		public GameObject 			m_BoidPrefab;
		public float 				m_DistributionRadius = 3;
		public Transform 			m_Target1;
		public Transform 			m_Target2;
		public Transform 			m_Ground;
		public Transform 			m_Obstacle1;
		public BoidSimulationSettings 		m_SimulationSettings;

		JobHandle 					m_SPFence;
		JobHandle 					m_SimulationFence;
		NativeList<BoidData> 		m_BoidsData0;
		NativeList<BoidData> 		m_BoidsData1;
		bool 						m_FrameIndex;
		TransformAccessArray 		m_TransformsArray;
	    NativeMultiHashMap<int, int>         m_Cells0;
		NativeMultiHashMap<int, int>         m_Cells1;

		BoidCloud()
		{
			m_SimulationSettings.Init ();
		}

		void AddBoids (int additionalBoidCount)
		{
			m_SimulationFence.Complete ();

			const int indicesPerJob = 200;

			int newRootCount = additionalBoidCount / indicesPerJob;

			var roots = new Transform[newRootCount];
			for (int i = 0; i < newRootCount; i++)
			{
				roots[i] = new GameObject ("BoidSimulationChunk").transform;
				roots[i].hierarchyCapacity = additionalBoidCount / newRootCount;
			}

			int instanceCycleOffsetProp = Shader.PropertyToID ("_InstanceCycleOffset");

			m_BoidsData0.Capacity = m_BoidsData0.Length + additionalBoidCount;
			m_BoidsData1.Capacity = m_BoidsData0.Length + additionalBoidCount;
			m_TransformsArray.Capacity = m_TransformsArray.Length + additionalBoidCount;

			for (int i = 0; i < additionalBoidCount; ++i)
			{
				Transform parent = null;
				if (newRootCount != 0)
					parent = roots[(i * newRootCount) / additionalBoidCount];
				var g = GameObject.Instantiate(m_BoidPrefab, parent) as GameObject;

				m_TransformsArray.Add(g.transform);
				var block = new MaterialPropertyBlock ();
				block.SetFloat(instanceCycleOffsetProp, Random.Range(0.0F, 2.0F));
				g.GetComponentInChildren<MeshRenderer> ().SetPropertyBlock (block);

				BoidData thisBoid;
				thisBoid.position = Random.insideUnitSphere * m_DistributionRadius;
				thisBoid.forward = new float3(0, 0, 1);

				m_BoidsData0.Add (thisBoid);
				m_BoidsData1.Add (thisBoid);
			}

			m_Cells0.Dispose ();
			m_Cells1.Dispose ();
			m_Cells0 = new NativeMultiHashMap<int, int>(m_BoidsData0.Length, Allocator.Persistent);
			m_Cells1 = new NativeMultiHashMap<int, int>(m_BoidsData0.Length, Allocator.Persistent);
		}


		void OnEnable ()
		{
			m_TransformsArray = new TransformAccessArray (m_InitialBoidsCount, -1);
			m_Cells0 = new NativeMultiHashMap<int, int>(m_InitialBoidsCount, Allocator.Persistent);
			m_Cells1 = new NativeMultiHashMap<int, int>(m_InitialBoidsCount, Allocator.Persistent);
			m_BoidsData0 = new NativeList<BoidData>(Allocator.Persistent);
			m_BoidsData1 = new NativeList<BoidData>(Allocator.Persistent);

			AddBoids (m_InitialBoidsCount);

			var lookat = FindObjectOfType<LookAt> ();

			if (lookat != null)
				lookat.target = m_TransformsArray[m_TransformsArray.Length - 1];
		}

		void OnDisable()
		{
			m_SimulationFence.Complete ();
			m_TransformsArray.Dispose();
	        m_Cells0.Dispose();
	        m_Cells1.Dispose();
			m_BoidsData0.Dispose ();
			m_BoidsData1.Dispose ();
		}
			
		NativeArray<BoidData> GetActiveBoidBuffer()
		{
			return m_FrameIndex ? m_BoidsData0 : m_BoidsData1;
		}

		NativeArray<BoidData> GetPreviousBoidBuffer()
		{
			return m_FrameIndex ? m_BoidsData1 : m_BoidsData0;
		}

		// Update is called once per frame
		void Update ()
		{

			if (Input.GetKeyDown ("a"))
			{
				AddBoids (1000);
			}

			m_SimulationFence.Complete();
			if (m_FrameIndex)
	        	m_Cells0.Clear();
			else
	        	m_Cells1.Clear();

			// boid job
			BoidsJob boidsJob = new BoidsJob();
			boidsJob.outputBoids = GetActiveBoidBuffer();
			boidsJob.outputCells = m_FrameIndex ? m_Cells0 : m_Cells1;

			boidsJob.boids = GetPreviousBoidBuffer();
			boidsJob.cells = m_FrameIndex ? m_Cells1 : m_Cells0;
			boidsJob.cellOffsetsTable = new NativeArray<int>(HashUtility.cellOffsets, Allocator.TempJob);

			boidsJob.simulationState.deltaTime = Time.deltaTime;

			boidsJob.simulationSettings = m_SimulationSettings;

			// transform job
			BoidsTransformJob boidsTransformJob = new BoidsTransformJob();
			boidsTransformJob.boidsBuffer = boidsJob.outputBoids;

			m_FrameIndex = !m_FrameIndex;

			if (m_Target1 == null && m_Target2 == null)
			{
				boidsJob.simulationSettings.targetWeight = 0;
			}
			else
			{
				if (m_Target1 != null)
					boidsJob.simulationState.target1 = m_Target1.position;
				if (m_Target2 != null)
					boidsJob.simulationState.target2 = m_Target2.position;
			}

			if (m_Ground != null)
				boidsJob.simulationState.ground = m_Ground.position;
			if (m_Obstacle1 != null)
				boidsJob.simulationState.obstacle1 = m_Obstacle1.position;

			var fence = boidsJob.Schedule (boidsJob.outputBoids.Length, 512);
			m_SimulationFence = boidsTransformJob.Schedule(m_TransformsArray, fence);
		}
	}
}