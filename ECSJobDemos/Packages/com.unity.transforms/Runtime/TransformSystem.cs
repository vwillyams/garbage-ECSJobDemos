using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Transforms
{
    public class TransformSystem : JobComponentSystem
    {
        [Inject] [ReadOnly] ComponentDataFromEntity<LocalPosition> m_LocalPositions;
        [Inject] [ReadOnly] ComponentDataFromEntity<LocalRotation> m_LocalRotations;
        [Inject] ComponentDataFromEntity<Position> m_Positions;
        [Inject] ComponentDataFromEntity<Rotation> m_Rotations;
        [Inject] ComponentDataFromEntity<TransformMatrix> m_TransformMatrices;

        struct RootTransGroup
        {
            [ReadOnly] public SubtractiveComponent<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public EntityArray entities;
            public int Length;
        }

        [Inject] RootTransGroup m_RootTransGroup;
        
        struct RootRotGroup
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public SubtractiveComponent<Position> positions;
            [ReadOnly] public EntityArray entities;
            public int Length;
        }

        [Inject] RootRotGroup m_RootRotGroup;
        
        struct RootRotTransGroup
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public EntityArray entities;
            public int Length;
        }

        [Inject] RootRotTransGroup m_RootRotTransGroup;

        struct ParentGroup
        {
            [ReadOnly] public ComponentDataArray<TransformParent> transformParents;
            [ReadOnly] public EntityArray entities;
            public int Length;
        }

        [Inject] ParentGroup m_ParentGroup;

        [ComputeJobOptimization]
        struct BuildHierarchy : IJobParallelFor
        {
            public NativeMultiHashMap<Entity, Entity>.Concurrent hierarchy;
            [ReadOnly] public ComponentDataArray<TransformParent> transformParents;
            [ReadOnly] public EntityArray entities;

            public void Execute(int index)
            {
                hierarchy.Add(transformParents[index].Value,entities[index]);
            }
        }
        
        [ComputeJobOptimization]
        struct CopyEntities : IJobParallelFor
        {
            [ReadOnly] public EntityArray source;
            public NativeArray<Entity> result;

            public void Execute(int index)
            {
                result[index] = source[index];
            }
        }

        [ComputeJobOptimization]
        struct UpdateHierarchy : IJob
        {
            [ReadOnly] public NativeMultiHashMap<Entity, Entity> hierarchy;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> roots;
            [ReadOnly] public ComponentDataFromEntity<LocalPosition> localPositions;
            [ReadOnly] public ComponentDataFromEntity<LocalRotation> localRotations;

            public ComponentDataFromEntity<Position> positions;
            public ComponentDataFromEntity<Rotation> rotations;
            public ComponentDataFromEntity<TransformMatrix> transformMatrices;

            void TransformTree(Entity entity,float4x4 parentMatrix)
            {
                var position = new float3();
                var rotation = quaternion.identity;
                
                if (positions.Exists(entity))
                {
                    position = positions[entity].Value;
                }
                
                if (rotations.Exists(entity))
                {
                    rotation = rotations[entity].Value;
                }
                
                if (localPositions.Exists(entity))
                {
                    var worldPosition = math.mul(parentMatrix,new float4(localPositions[entity].Value,1.0f));
                    position = new float3(worldPosition.x,worldPosition.y,worldPosition.z);
                    if (positions.Exists(entity))
                    {
                        positions[entity] = new Position {Value = position};
                    }
                }
                
                if (localRotations.Exists(entity))
                {
                    var parentRotation = math.matrixToQuat(
                        new float3(parentMatrix.m0.x, parentMatrix.m0.y, parentMatrix.m0.z),
                        new float3(parentMatrix.m1.x, parentMatrix.m1.y, parentMatrix.m1.z),
                        new float3(parentMatrix.m2.x, parentMatrix.m2.y, parentMatrix.m2.z) );
                    var localRotation = localRotations[entity].value;
                    rotation = math.mul(parentRotation, localRotation);
                    if (rotations.Exists(entity))
                    {
                        rotations[entity] = new Rotation {Value = rotation};
                    }
                }

                float4x4 matrix = math.rottrans(rotation, position);
                if (transformMatrices.Exists(entity))
                {
                    transformMatrices[entity] = new TransformMatrix {Value = matrix};
                }

                Entity child;
                NativeMultiHashMapIterator<Entity> iterator;
                bool found = hierarchy.TryGetFirstValue(entity, out child, out iterator);
                while (found)
                {
                    TransformTree(child,matrix);
                    found = hierarchy.TryGetNextValue(out child, ref iterator);
                }
            }

            public void Execute()
            {
                float4x4 identity = float4x4.identity;
                for (int i = 0; i < roots.Length; i++)
                {
                    TransformTree(roots[i],identity);
                }
            }
        }

        NativeMultiHashMap<Entity, Entity> m_Hierarchy;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            int rootCount = m_RootRotGroup.Length + m_RootTransGroup.Length + m_RootRotTransGroup.Length;
            if (rootCount == 0)
            {
                return inputDeps;
            }
            
            var transRoots = new NativeArray<Entity>(m_RootTransGroup.Length, Allocator.TempJob);
            var rotTransRoots = new NativeArray<Entity>(m_RootRotTransGroup.Length, Allocator.TempJob);
            var rotRoots = new NativeArray<Entity>(m_RootRotGroup.Length, Allocator.TempJob);
            
            m_Hierarchy.Capacity = math.max(m_ParentGroup.Length + rootCount,m_Hierarchy.Capacity);
            m_Hierarchy.Clear();

            var copyTransRootsJob = new CopyEntities
            {
                source = m_RootTransGroup.entities,
                result = transRoots
            };
            var copyTransRootsJobHandle = copyTransRootsJob.Schedule(m_RootTransGroup.Length, 64, inputDeps);
            
            var copyRotTransRootsJob = new CopyEntities
            {
                source = m_RootRotTransGroup.entities,
                result = rotTransRoots
            };
            var copyRotTransRootsJobHandle = copyRotTransRootsJob.Schedule(m_RootRotTransGroup.Length, 64, inputDeps);
            
            var copyRotRootsJob = new CopyEntities
            {
                source = m_RootRotGroup.entities,
                result = rotTransRoots
            };
            var copyRotRootsJobHandle = copyRotRootsJob.Schedule(m_RootRotGroup.Length, 64, inputDeps);
        
            var buildHierarchyJob = new BuildHierarchy
            {
                hierarchy = m_Hierarchy,
                transformParents = m_ParentGroup.transformParents,
                entities = m_ParentGroup.entities
            };
            var buildHierarchyJobHandle = buildHierarchyJob.Schedule(m_ParentGroup.Length, 64, inputDeps);

            var jh0 = JobHandle.CombineDependencies(copyTransRootsJobHandle, copyRotTransRootsJobHandle);
            var jh1 = JobHandle.CombineDependencies(copyRotRootsJobHandle, buildHierarchyJobHandle);
            var jh2 = JobHandle.CombineDependencies(jh0, jh1);

            var updateTransHierarchyJob = new UpdateHierarchy
            {
                hierarchy = m_Hierarchy,
                roots = transRoots,
                localPositions = m_LocalPositions,
                localRotations = m_LocalRotations,
                positions = m_Positions,
                rotations = m_Rotations,
                transformMatrices = m_TransformMatrices
            };
            var updateTransHierarchyJobHandle = updateTransHierarchyJob.Schedule(jh2);
            
            var updateRotTransHierarchyJob = new UpdateHierarchy
            {
                hierarchy = m_Hierarchy,
                roots = rotTransRoots,
                localPositions = m_LocalPositions,
                localRotations = m_LocalRotations,
                positions = m_Positions,
                rotations = m_Rotations,
                transformMatrices = m_TransformMatrices
            };
            var updateRotTransHierarchyJobHandle = updateRotTransHierarchyJob.Schedule(updateTransHierarchyJobHandle);
            
            var updateRotHierarchyJob = new UpdateHierarchy
            {
                hierarchy = m_Hierarchy,
                roots = rotRoots,
                localPositions = m_LocalPositions,
                localRotations = m_LocalRotations,
                positions = m_Positions,
                rotations = m_Rotations,
                transformMatrices = m_TransformMatrices
            };
            var updateRotHierarchyJobHandle = updateRotHierarchyJob.Schedule(updateRotTransHierarchyJobHandle);

            return updateRotHierarchyJobHandle;
        } 
        
        protected override void OnCreateManager(int capacity)
        {
            m_Hierarchy = new NativeMultiHashMap<Entity, Entity>(capacity, Allocator.Persistent);
        }

        protected override void OnDestroyManager()
        {
            m_Hierarchy.Dispose();
        }
        
    }
}
