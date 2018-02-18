﻿using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace Unity.Transforms.Hybrid
{
    public class CopyTransformPositionToGameObjectSystem : JobComponentSystem
    {
        struct PositionGroup
        {
            public ComponentDataArray<CopyTransformPositionToGameObject> copyTransformPositionToGameObjects;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public TransformAccessArray transforms;
            public int Length;
        }

        [Inject] PositionGroup m_PositionGroup;

        [ComputeJobOptimization]
        struct PositionToMatrix : IJobParallelForTransform
        {
            [ReadOnly] public ComponentDataArray<Position> positions;

            public void Execute(int i, TransformAccess transform)
            {
                transform.position = positions[i].position;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var worldPositionToMatrixJob = new PositionToMatrix();
            worldPositionToMatrixJob.positions = m_PositionGroup.positions;
            return worldPositionToMatrixJob.Schedule(m_PositionGroup.transforms, inputDeps);
        }
    }
}
