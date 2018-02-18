using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace Unity.Transforms.Hybrid
{
    [DisableSystemWhenEmpty]
    public class CopyInitialTransformFromGameObjectSystem : JobComponentSystem
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
                    rotation       = new quaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w),
                    position       = transform.position,
                    localRotation  = new quaternion(transform.localRotation.x, transform.localRotation.y, transform.localRotation.z, transform.localRotation.w),
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
            public NativeQueue<Entity>.Concurrent removeComponentQueue;

            public void Execute()
            {
                for (int index=0;index<transformStashes.Length;index++)
                {
                    var transformStash = transformStashes[index];
                    var entity = transformStashes[index].entity;
                    if (positions.Exists(entity))
                    {
                        positions[entity] = new Position { position = transformStash.position };
                    }
                    if (rotations.Exists(entity))
                    {
                        rotations[entity] = new Rotation { value = transformStash.rotation };
                    }
                    if (localPositions.Exists(entity))
                    {
                        localPositions[entity] = new LocalPosition { position = transformStash.localPosition };
                    }
                    if (localRotations.Exists(entity))
                    {
                        localRotations[entity] = new LocalRotation { value = transformStash.localRotation };
                    }
                    removeComponentQueue.Enqueue(entity);
                }
            }
        }

        [Inject] DeferredEntityChangeSystem m_DeferredEntityChangeSystem;

        ComponentGroup m_InitialTransformGroup;
        
        protected override void OnCreateManager(int capacity)
        {
            m_InitialTransformGroup = GetComponentGroup(ComponentType.ReadOnly(typeof(CopyInitialTransformFromGameObject)),typeof(UnityEngine.Transform));
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var transforms = m_InitialTransformGroup.GetTransformAccessArray();
            var entities = m_InitialTransformGroup.GetEntityArray();

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
                removeComponentQueue = m_DeferredEntityChangeSystem.GetRemoveComponentQueue<CopyInitialTransformFromGameObject>()
            };

            return copyTransformsJob.Schedule(stashTransformsJobHandle);
        }
    }
}
