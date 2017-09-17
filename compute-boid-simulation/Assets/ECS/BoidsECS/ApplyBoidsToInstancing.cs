using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using UnityEngine.ECS;

namespace BoidSimulations
{

	class matrix_math_util
	{
		const float epsilon = 0.000001F;

		public static float3x3 identity3
		{
			get { return new float3x3 (new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1)); }
		}
		public static float4x4 identity4
		{
			get { return new float4x4 (new float4(1, 0, 0, 0), new float4(0, 1, 0, 0), new float4(0, 0, 1, 0), new float4(0, 0, 0, 1)); }
		}

		public static float4x4 LookRotationToMatrix(float3 position, float3 forward, float3 up)
		{
			float3x3 rot = LookRotationToMatrix (forward, up);

			float4x4 matrix;
			matrix.m0 = new float4 (rot.m0, 0.0F);
			matrix.m1 = new float4 (rot.m1, 0.0F);
			matrix.m2 = new float4 (rot.m2, 0.0F);
			matrix.m3 = new float4 (position, 1.0F);

			return matrix;
		}

		public static float3x3 LookRotationToMatrix(float3 forward, float3 up)
		{
			float3 z = forward;
			// compute u0
			float mag = math.length(z);
			if (mag < epsilon)
				return identity3;
			z /= mag;

			float3 x = math.cross(up, z);
			mag = math.length(x);
			if (mag < epsilon)
				return identity3;
			x /= mag;

			float3 y = math.cross(z, x);
			float yLength = math.length (y);
			if (yLength < 0.9F || yLength > 1.1F)
				return identity3;

			return new float3x3 (x, y, z);
		}
	}

    [UpdateAfter(typeof(BoidSimulationSystem))]
    public class ApplyBoidsToInstancing : JobComponentSystem
	{
		ComputeBuffer 	m_MatrixBuffer0;
		ComputeBuffer 	m_ArgsBuffer;
		float4x4[] 				m_MatricesArray;
		NativeArray<float4x4> 	m_Matrices;

		Material 		m_InstanceMaterial;
		Mesh 			m_InstanceMesh;
		Bounds 			m_Bounds = new Bounds(new Vector3(0.0F, 0.0F, 0.0F), new Vector3(10000.0F, 10000.0F, 10000.0F));

		[InjectTuples]
		ComponentDataArray<BoidData> m_Boids;

		[InjectTuples]
		ComponentDataArray<BoidInstanceRenderer> m_Instancing;

		[ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
		struct BoidToMatricesJob : IJobParallelFor 
		{
			public ComponentDataArray<BoidData> 	boids;
			public NativeArray<float4x4> 				matrices;

			public void Execute(int index)
			{
				var boid = boids [index];
				matrices [index] = matrix_math_util.LookRotationToMatrix (boid.position, boid.forward, new float3(0, 1, 0));
			}
		}

		public override void  OnUpdate()
		{
			base.OnUpdate();

			if (m_Boids.Length == 0)
				return;

			if (m_InstanceMesh == null)
			{
				Debug.LogError ("Boid instance renderer system has not been configured");
				return;
			}

			if (m_MatricesArray == null || m_Boids.Length != m_MatricesArray.Length)
				InitializeBatch (m_Boids.Length);

			var job = new BoidToMatricesJob ()
			{
				boids = m_Boids,
				matrices = m_Matrices
			};

			var jobFence = job.Schedule (m_Boids.Length, 512, GetDependency());
			jobFence.Complete ();

			m_Matrices.CopyTo (m_MatricesArray);
			m_MatrixBuffer0.SetData (m_MatricesArray);
			m_InstanceMaterial.SetBuffer ("matrixBuffer", m_MatrixBuffer0);
				
			Graphics.DrawMeshInstancedIndirect(m_InstanceMesh, 0, m_InstanceMaterial, m_Bounds, m_ArgsBuffer);
		}

		void CleanupBatch()
		{
			if (m_MatrixBuffer0 != null)
				m_MatrixBuffer0.Release ();
			m_MatrixBuffer0 = null;

			if (m_ArgsBuffer != null)
				m_ArgsBuffer.Release ();
			m_ArgsBuffer = null;

			if (m_Matrices.IsCreated)
				m_Matrices.Dispose ();
		}

		public void InitializeInstanceRenderer (GameObject prefab)
		{
			CleanupInstanceRenderer ();
			CleanupBatch ();

			m_InstanceMaterial = Object.Instantiate(prefab.GetComponent<MeshRenderer> ().sharedMaterial);
			m_InstanceMaterial.hideFlags = HideFlags.HideAndDontSave;

			m_InstanceMesh = prefab.GetComponent<MeshFilter> ().sharedMesh;
		}

		public void CleanupInstanceRenderer ()
		{
			Object.DestroyImmediate (m_InstanceMaterial);
			m_InstanceMaterial = null;

			m_InstanceMesh = null;

			CleanupBatch ();
		}

		void InitializeBatch (int instanceCount)
		{
			CleanupBatch ();

			m_MatricesArray = new float4x4[instanceCount];
			m_Matrices = new NativeArray<float4x4> (instanceCount, Allocator.Persistent);

			m_MatrixBuffer0 = new ComputeBuffer(instanceCount, 64);

			var args = new uint[5] { 0, 0, 0, 0, 0 };

			uint numIndices = (m_InstanceMesh != null) ? (uint)m_InstanceMesh.GetIndexCount(0) : 0;
			args[0] = numIndices;
			args[1] = (uint)m_MatricesArray.Length;

			m_ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
			m_ArgsBuffer.SetData(args);
		}

		protected override void OnCreateManager (int capacity)
		{
			base.OnCreateManager (capacity);
		}

		protected override void OnDestroyManager ()
		{
			base.OnDestroyManager ();

			CleanupInstanceRenderer ();
		}
	}
}