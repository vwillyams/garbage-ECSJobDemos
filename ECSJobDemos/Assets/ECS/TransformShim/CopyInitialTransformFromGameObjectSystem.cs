using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.TransformShim
{
    public class CopyInitialTransformPositionFromGameObjectSystem : JobComponentSystem
    {
        [Inject] private ComponentDataFromEntity<LocalPosition> m_LocalPositions;
        [Inject] private ComponentDataFromEntity<LocalRotation> m_LocalRotations;
        [Inject] private ComponentDataFromEntity<Position> m_Positions;
        [Inject] private ComponentDataFromEntity<Rotation> m_Rotations;
 
        struct InitialTransformGroup
        {
            [ReadOnly] public ComponentDataArray<CopyInitialTransformFromGameObject> copyInitialTransformFromGameObjects;
            public TransformAccessArray transforms;
            public EntityArray entities;
            public int Length;
        }

        [Inject] private InitialTransformGroup m_InitialTransformGroup;
        [Inject] private DeferredEntityChangeSystem m_DeferredEntityChangeSystem;

        struct TransformStash
        {
            public float3 localPosition;
            public float3 position;
            public quaternion localRotation;
            public quaternion rotation;
        }

        [ComputeJobOptimization]
        struct CopyTransforms : IJobParallelForTransform
        {
            public NativeArray<TransformStash> transformStashes;

            public void Execute(int index, TransformAccess transform)
            {
                transformStashes[index] = new TransformStash
                {
                    localPosition = transform.localPosition,
                    rotation = new quaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w),
                    position = transform.position,
                    localRotation= new quaternion(transform.localRotation.x, transform.localRotation.y, transform.localRotation.z, transform.localRotation.w)
                };
            }
        }
            
        // [ComputeJobOptimization]
        struct CopyInitialTransformPositions : IJob
        {
            public ComponentDataFromEntity<LocalPosition> localPositions;
            public ComponentDataFromEntity<LocalRotation> localRotations;
            public ComponentDataFromEntity<Position> positions;
            public ComponentDataFromEntity<Rotation> rotations;
            [DeallocateOnJobCompletion] public NativeArray<TransformStash> transformStashes;
            public EntityArray entities;
            public NativeQueue<Entity>.Concurrent removeComponentQueue;

            public void Execute()
            {
                for (int index=0;index<transformStashes.Length;index++)
                {
                    var transformStash = transformStashes[index];
                    var entity = entities[index];
                    if (positions.Exists(entity))
                    {
                        positions[entity] = new Position
                        {
                            position = transformStash.position
                        };
                    }
                    if (rotations.Exists(entity))
                    {
                        rotations[entity] = new Rotation
                        {
                            rotation = transformStash.rotation
                        };
                    }
                    if (localPositions.Exists(entity))
                    {
                        localPositions[entity] = new LocalPosition
                        {
                            position = transformStash.localPosition
                        };
                    }
                    if (localRotations.Exists(entity))
                    {
                        localRotations[entity] = new LocalRotation
                        {
                            rotation = transformStash.localRotation
                        };
                    }
                    removeComponentQueue.Enqueue(entities[index]);
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var transformStashes = new NativeArray<TransformStash>(m_InitialTransformGroup.Length, Allocator.TempJob);
            var copyTransformsJob = new CopyTransforms
            {
                transformStashes = transformStashes
            };
            var copyTransformsJobHandle = copyTransformsJob.Schedule(m_InitialTransformGroup.transforms, inputDeps);
            
            var copyInitialTransformPositionsJob = new CopyInitialTransformPositions
            {
                positions = m_Positions,
                rotations = m_Rotations,
                localPositions = m_LocalPositions,
                localRotations = m_LocalRotations,
                transformStashes = transformStashes,
                entities = m_InitialTransformGroup.entities,
                removeComponentQueue = m_DeferredEntityChangeSystem.GetRemoveComponentQueue<CopyInitialTransformFromGameObject>()
            };

            return copyInitialTransformPositionsJob.Schedule(copyTransformsJobHandle);
        }
    }
}
