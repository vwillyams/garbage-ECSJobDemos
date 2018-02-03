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
        struct TransGroup
        {
            public ComponentDataArray<TransformMatrix> matrices;
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            [ReadOnly] public SubtractiveComponent<TransformRotation> rotations;
            public int Length;
        }
        
        [Inject] private TransGroup m_TransGroup;
        
        struct RotTransGroup
        {
            public ComponentDataArray<TransformMatrix> matrices;
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            [ReadOnly] public ComponentDataArray<TransformRotation> rotations;
            public int Length;
        }
        
        [Inject] private RotTransGroup m_RotTransGroup;
    
        [ComputeJobOptimization]
        struct TransToMatrix : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            public ComponentDataArray<TransformMatrix> matrices;
        
            public void Execute(int i)
            {
                var position = positions[i].position;
                matrices[i] = new TransformMatrix
                {
                    matrix = math.translate(position)
                };
            }
        }
        
        [ComputeJobOptimization]
        struct RotTransToMatrix : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            [ReadOnly] public ComponentDataArray<TransformRotation> rotations;
            public ComponentDataArray<TransformMatrix> matrices;
        
            public void Execute(int i)
            {
                float3 position = positions[i].position;
                matrices[i] = new TransformMatrix
                {
                    matrix = math.rottrans( math.normalize(rotations[i].rotation), position )
                };
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var transToMatrixJob = new TransToMatrix();
            transToMatrixJob.positions = m_TransGroup.positions;
            transToMatrixJob.matrices = m_TransGroup.matrices;
            var transToMatrixJobHandle = transToMatrixJob.Schedule(m_TransGroup.Length, 64, inputDeps);
            
            var rotTransToMatrixJob = new RotTransToMatrix();
            rotTransToMatrixJob.positions = m_RotTransGroup.positions;
            rotTransToMatrixJob.matrices = m_RotTransGroup.matrices;
            rotTransToMatrixJob.rotations = m_RotTransGroup.rotations;
            // var rotTransToMatrixJobHandle = rotTransToMatrixJob.Schedule(m_RotTransGroup.Length, 64, inputDeps);
            // return JobHandle.CombineDependencies(rotTransToMatrixJobHandle, transToMatrixJobHandle);
            return rotTransToMatrixJob.Schedule(m_RotTransGroup.Length, 64, transToMatrixJobHandle);
        } 
    }
}
