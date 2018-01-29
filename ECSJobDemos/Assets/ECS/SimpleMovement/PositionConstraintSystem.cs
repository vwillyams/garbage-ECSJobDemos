﻿using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class PositionConstraintSystem : JobComponentSystem
    {
        struct PositionConstraintsGroup
        {
            [ReadOnly] public ComponentDataArray<PositionConstraint> positionConstraints;
            [ReadOnly] public EntityArray                            entities;
            public int                                               Length;
        }

        [Inject] private PositionConstraintsGroup           m_PositionContraintsGroup;
        [Inject] ComponentDataFromEntity<TransformPosition> m_TransformPositions;

        [ComputeJobOptimization]
        struct ContrainPositions : IJob
        {
            public ComponentDataFromEntity<TransformPosition>        positions;
            [ReadOnly] public ComponentDataArray<PositionConstraint> positionConstraints;
            [ReadOnly] public EntityArray                            positionConstraintEntities;

            public void Execute()
            {
                for (int i = 0; i < positionConstraints.Length; i++)
                {
                    var childEntity    = positionConstraintEntities[i];
                    var parentEntity   = positionConstraints[i].parentEntity;
                    var childPosition  = positions[childEntity].position;
                    var parentPosition = positions[parentEntity].position;

                    float3 d   = childPosition - parentPosition;
                    float  len = math.length(d);
                    float  nl  = math.min(math.max(len, positionConstraints[i].minDistance), positionConstraints[i].maxDistance);

                    positions[childEntity] = new TransformPosition
                    {
                        position = parentPosition + ((d * nl) / len)
                    };
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var constrainPositionsJob = new ContrainPositions
            {
                positions = m_TransformPositions,
                positionConstraints = m_PositionContraintsGroup.positionConstraints,
                positionConstraintEntities = m_PositionContraintsGroup.entities
            };
            
            return constrainPositionsJob.Schedule(inputDeps);
        }
    }
}
