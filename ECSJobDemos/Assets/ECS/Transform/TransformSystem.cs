using Boo.Lang;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.Transform
{
    public class TransformSystem : JobComponentSystem
    {
        struct WorldPositionGroup
        {
            [ReadOnly]
            public ComponentDataArray<TransformPosition> positions;
            public ComponentDataArray<TransformMatrix> matrices;
            public int Length;
        }
        
        [InjectComponentGroup] private WorldPositionGroup m_WorldPositionGroup;
    
        [ComputeJobOptimization]
        struct WorldPositionToMatrix : IJobParallelFor
        {
            [ReadOnly]
            public ComponentDataArray<TransformPosition> positions;
            public ComponentDataArray<TransformMatrix> matrices;
        
            public void Execute(int i)
            {
                var result = new TransformMatrix();
                var matrix = new float4x4();
                
                matrix = math.translate(positions[i].position);
                result.matrix = matrix;
                matrices[i] = result;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var worldPositionToMatrixJob = new WorldPositionToMatrix();
            worldPositionToMatrixJob.positions = m_WorldPositionGroup.positions;
            worldPositionToMatrixJob.matrices = m_WorldPositionGroup.matrices;
            return worldPositionToMatrixJob.Schedule(m_WorldPositionGroup.Length, 64, inputDeps);
        } 
    }
}
