using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Transform
{
    public class CopyInitialTransformRotationFromGameObjectSystem : JobComponentSystem
    {
        struct InitialTransformGroup
        {
            [ReadOnly] public ComponentDataArray<CopyInitialTransformRotationFromGameObject> copyInitialTransformFromGameObjects;
            public TransformAccessArray transforms;
            public ComponentDataArray<Rotation> transformRotations;
            public EntityArray entities;
        }

        [Inject] private InitialTransformGroup m_InitialTransformGroup;
        [Inject] private DeferredEntityChangeSystem m_DeferredEntityChangeSystem;
            
        // [ComputeJobOptimization]
        struct CopyInitialTransformRotations : IJobParallelForTransform
        {
            public ComponentDataArray<Rotation> rotations;
            public EntityArray entities;
            public NativeQueue<Entity>.Concurrent removeComponentQueue;

            public void Execute(int i, TransformAccess transform)
            {
                rotations[i] = new Rotation
                {
                    rotation = new quaternion(
                        transform.rotation.x, 
                        transform.rotation.y, 
                        transform.rotation.z, 
                        transform.rotation.w )
                };
                removeComponentQueue.Enqueue(entities[i]);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var CopyInitialTransformRotationsJob = new CopyInitialTransformRotations
            {
                rotations = m_InitialTransformGroup.transformRotations,
                entities = m_InitialTransformGroup.entities,
                removeComponentQueue = m_DeferredEntityChangeSystem
                    .GetRemoveComponentQueue<CopyInitialTransformRotationFromGameObject>()
            };

            return CopyInitialTransformRotationsJob.Schedule(m_InitialTransformGroup.transforms, inputDeps);
        }
    }
}
