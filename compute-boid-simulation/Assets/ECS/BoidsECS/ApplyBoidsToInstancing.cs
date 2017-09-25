using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using UnityEngine.ECS;
using System.Collections.Generic;

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
        class Batch
        {
            ComponentGroup             m_Group;
            ComputeBuffer           m_MatrixBuffer;
            ComputeBuffer           m_ArgsBuffer;
            float4x4[]              m_MatricesArray;
            NativeArray<float4x4>   m_Matrices;
            Material                m_MaterialCopy;
            Mesh                    m_Mesh;

            public Batch (BoidInstanceRenderer renderer, ComponentGroup group)
            {
                m_Group = group;

                int length = group.Length;

                m_MatricesArray = new float4x4[length];
                m_Matrices = new NativeArray<float4x4>(length, Allocator.Persistent);

                m_MatrixBuffer = new ComputeBuffer(length, 64);

                var args = new uint[5] { 0, 0, 0, 0, 0 };

                uint numIndices = (renderer.mesh != null) ? (uint)renderer.mesh.GetIndexCount(0) : 0;
                args[0] = numIndices;
                args[1] = (uint)length;

                m_ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                m_ArgsBuffer.SetData(args);

                m_MaterialCopy = Object.Instantiate(renderer.material);
                m_MaterialCopy.hideFlags = HideFlags.HideAndDontSave;
                m_MaterialCopy.SetBuffer("matrixBuffer", m_MatrixBuffer);

                m_Mesh = renderer.mesh;
            }

            public bool IsValid()
            {
                return m_Group.Length == m_Matrices.Length;
            }

            public void Render(JobHandle dependency)
            {
                var job = new BoidToMatricesJob()
                {
                    boids = m_Group.GetComponentDataArray<BoidData>(),
                    matrices = m_Matrices
                };

                var jobFence = job.Schedule(m_Matrices.Length, 512, dependency);
                jobFence.Complete();

                m_Matrices.CopyTo(m_MatricesArray);
                m_MatrixBuffer.SetData(m_MatricesArray);

                var bounds = new Bounds(new Vector3(0.0F, 0.0F, 0.0F), new Vector3(10000.0F, 10000.0F, 10000.0F));

                Graphics.DrawMeshInstancedIndirect(m_Mesh, 0, m_MaterialCopy, bounds, m_ArgsBuffer);
            }

            public void Dispose()
            {
                m_MatrixBuffer.Release();
                m_ArgsBuffer.Release();
                m_Matrices.Dispose();
                Object.DestroyImmediate(m_MaterialCopy);
            }
        }

        Dictionary<ComponentType, Batch> m_ComponentToBatch = new Dictionary<ComponentType, Batch>();

		[InjectTuples]
		ComponentDataArray<BoidData> m_Boids;

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

            var uniqueRendererTypes = new NativeList<ComponentType>(10, Allocator.TempJob);
            EntityManager.GetAllUniqueSharedComponents(typeof(BoidInstanceRenderer), uniqueRendererTypes);

            //@TODO: Do cleanup when renderer type is no longer being used...

            for (int i = 0;i != uniqueRendererTypes.Length;i++)
            {
                var uniqueType = uniqueRendererTypes[i];
                Batch batch;
                if (m_ComponentToBatch.TryGetValue(uniqueType, out batch))
                {
                    if (batch.IsValid())
                    {
                        batch.Render(GetDependency());
                        continue;
                    }
                    else
                    {
                        batch.Dispose();
                        m_ComponentToBatch.Remove(uniqueType);
                    }
                }

                var group = EntityManager.CreateComponentGroup(uniqueType, ComponentType.Create<BoidData>());

                batch = new Batch(EntityManager.GetSharedComponentData<BoidInstanceRenderer>(uniqueType), group);
                batch.Render(GetDependency());
                m_ComponentToBatch.Add(uniqueType, batch);
            }

            uniqueRendererTypes.Dispose();
		}
		protected override void OnCreateManager (int capacity)
		{
			base.OnCreateManager (capacity);
		}

		protected override void OnDestroyManager ()
		{
			base.OnDestroyManager ();

            foreach(var batch in m_ComponentToBatch)
                batch.Value.Dispose();
            m_ComponentToBatch.Clear();
		}
	}
}