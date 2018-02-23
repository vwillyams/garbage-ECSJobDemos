﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Unity.Transforms.Hybrid
{
    [DisableSystemWhenEmpty]
    public class CopyTransformFromGameObjectSystem : JobComponentSystem
    {
        [Inject] ComponentDataFromEntity<LocalPosition> m_LocalPositions;
        [Inject] ComponentDataFromEntity<LocalRotation> m_LocalRotations;
        [Inject] ComponentDataFromEntity<Position> m_Positions;
        [Inject] ComponentDataFromEntity<Rotation> m_Rotations;

        struct TransformStash
        {
            public float3 localPosition;
            public float3 position;
            public quaternion localRotation;
            public quaternion rotation;
            public Entity entity;
        }

        [ComputeJobOptimization]
        struct StashTransforms : IJobParallelForTransform
        {
            public NativeArray<TransformStash> transformStashes;
            public EntityArray entities;

            public void Execute(int index, TransformAccess transform)
            {
                transformStashes[index] = new TransformStash
                {
                    localPosition  = transform.localPosition,
                    rotation       = transform.rotation,
                    position       = transform.position,
                    localRotation  = transform.localRotation,
                    entity         = entities[index]
                };
            }
        }

        [ComputeJobOptimization]
        struct CopyTransforms : IJob
        {
            public ComponentDataFromEntity<LocalPosition> localPositions;
            public ComponentDataFromEntity<LocalRotation> localRotations;
            public ComponentDataFromEntity<Position> positions;
            public ComponentDataFromEntity<Rotation> rotations;
            [DeallocateOnJobCompletion] public NativeArray<TransformStash> transformStashes;

            public void Execute()
            {
                for (int index=0;index<transformStashes.Length;index++)
                {
                    var transformStash = transformStashes[index];
                    var entity = transformStashes[index].entity;
                    if (positions.Exists(entity))
                    {
                        positions[entity] = new Position { Value = transformStash.position };
                    }
                    if (rotations.Exists(entity))
                    {
                        rotations[entity] = new Rotation { Value = transformStash.rotation };
                    }
                    if (localPositions.Exists(entity))
                    {
                        localPositions[entity] = new LocalPosition { Value = transformStash.localPosition };
                    }
                    if (localRotations.Exists(entity))
                    {
                        localRotations[entity] = new LocalRotation { Value = transformStash.localRotation };
                    }
                }
            }
        }

        ComponentGroup m_TransformGroup;

        protected override void OnCreateManager(int capacity)
        {
            m_TransformGroup = GetComponentGroup(ComponentType.ReadOnly(typeof(CopyTransformFromGameObject)),typeof(UnityEngine.Transform));
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var transforms = m_TransformGroup.GetTransformAccessArray();
            var entities = m_TransformGroup.GetEntityArray();

            var transformStashes = new NativeArray<TransformStash>(transforms.Length, Allocator.TempJob);
            var stashTransformsJob = new StashTransforms
            {
                transformStashes = transformStashes,
                entities = entities
            };

            var stashTransformsJobHandle = stashTransformsJob.Schedule(transforms, inputDeps);

            var copyTransformsJob = new CopyTransforms
            {
                positions = m_Positions,
                rotations = m_Rotations,
                localPositions = m_LocalPositions,
                localRotations = m_LocalRotations,
                transformStashes = transformStashes,
            };

            return copyTransformsJob.Schedule(stashTransformsJobHandle);
        }
    }
}
