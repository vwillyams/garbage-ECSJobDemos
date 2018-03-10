﻿using Unity.Collections;
using Unity.Entities;
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

        // +Rotation +Position -TransformMatrix
        struct RootRotTransNoTransformGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public EntityArray entities;
            [ReadOnly] public SubtractiveComponent<TransformMatrix> transforms;
            public int Length;
        }
        [Inject] RootRotTransNoTransformGroup m_RootRotTransNoTransformGroup;
        
        // +Rotation +Position +TransformMatrix
        struct RootRotTransTransformGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public EntityArray entities;
            public ComponentDataArray<TransformMatrix> transforms;
            public int Length;
        }
        [Inject] RootRotTransTransformGroup m_RootRotTransTransformGroup;

        // +Rotation -Position -TransformMatrix
        struct RootRotNoTransformGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public SubtractiveComponent<Position> positions;
            [ReadOnly] public EntityArray entities;
            [ReadOnly] public SubtractiveComponent<TransformMatrix> transforms;
            public int Length;
        }
        [Inject] RootRotNoTransformGroup m_RootRotNoTransformGroup;
        
        // +Rotation -Position +TransformMatrix
        struct RootRotTransformGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public SubtractiveComponent<Position> positions;
            [ReadOnly] public EntityArray entities;
            public ComponentDataArray<TransformMatrix> transforms;
            public int Length;
        }
        [Inject] RootRotTransformGroup m_RootRotTransformGroup;
        
        // -Rotation +Position -TransformMatrix
        struct RootTransNoTransformGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public SubtractiveComponent<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public EntityArray entities;
            [ReadOnly] public SubtractiveComponent<TransformMatrix> transforms;
            public int Length;
        }
        [Inject] RootTransNoTransformGroup m_RootTransNoTransformGroup;
        
        // -Rotation +Position +TransformMatrix
        struct RootTransTransformGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public SubtractiveComponent<Rotation> rotations;
            [ReadOnly] public SubtractiveComponent<TransformParent> parents;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public EntityArray entities;
            public ComponentDataArray<TransformMatrix> transforms;
            public int Length;
        }
        [Inject] RootTransTransformGroup m_RootTransTransformGroup;

        struct ParentGroup
        {
            [ReadOnly] public SubtractiveComponent<TransformExternal> transfromExternal;
            [ReadOnly] public ComponentDataArray<TransformParent> transformParents;
            [ReadOnly] public EntityArray entities;
            public int Length;
        }
        [Inject] ParentGroup m_ParentGroup;
        
        [ComputeJobOptimization]
        struct UpdateRotTransTransformRoots : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float4x4> matrices;
            public ComponentDataArray<TransformMatrix> transforms;

            public void Execute(int index)
            {
                float4x4 matrix = math.rottrans(rotations[index].Value, positions[index].Value);
                matrices[index] = matrix;
                transforms[index] = new TransformMatrix {Value = matrix};
            }
        }

        [ComputeJobOptimization]
        struct UpdateRotTransNoTransformRoots : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float4x4> matrices;

            public void Execute(int index)
            {
                float4x4 matrix = math.rottrans(rotations[index].Value, positions[index].Value);
                matrices[index] = matrix;
            }
        }
        
        [ComputeJobOptimization]
        struct UpdateRotTransformRoots : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            public NativeArray<float4x4> matrices;
            public ComponentDataArray<TransformMatrix> transforms;

            public void Execute(int index)
            {
                float4x4 matrix = math.rottrans(rotations[index].Value, new float3());
                matrices[index] = matrix;
                transforms[index] = new TransformMatrix {Value = matrix};
            }
        }

        [ComputeJobOptimization]
        struct UpdateRotNoTransformRoots : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            public NativeArray<float4x4> matrices;

            public void Execute(int index)
            {
                float4x4 matrix = math.rottrans(rotations[index].Value, new float3());
                matrices[index] = matrix;
            }
        }

        [ComputeJobOptimization]
        struct UpdateTransTransformRoots : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float4x4> matrices;
            public ComponentDataArray<TransformMatrix> transforms;

            public void Execute(int index)
            {
                float4x4 matrix = math.translate(positions[index].Value);
                matrices[index] = matrix;
                transforms[index] = new TransformMatrix {Value = matrix};
            }
        }

        [ComputeJobOptimization]
        struct UpdateTransNoTransformRoots : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float4x4> matrices;

            public void Execute(int index)
            {
                float4x4 matrix = math.translate(positions[index].Value);
                matrices[index] = matrix;
            }
        }
        
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
        struct DisposeMatrices : IJob
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float4x4> rootMatrices;
            
            public void Execute()
            {
            }
        }
        
        [ComputeJobOptimization]
        struct UpdateSubHierarchy : IJob
        {
            [ReadOnly] public NativeMultiHashMap<Entity, Entity> hierarchy;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> roots;
            [ReadOnly] public ComponentDataFromEntity<LocalPosition> localPositions;
            [ReadOnly] public ComponentDataFromEntity<LocalRotation> localRotations;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float4x4> rootMatrices;

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
                    var parentRotation = math.matrixToQuat(parentMatrix.m0.xyz, parentMatrix.m1.xyz, parentMatrix.m2.xyz);
                    var localRotation = localRotations[entity].Value;
                    rotation = math.mul(parentRotation, localRotation);
                    if (rotations.Exists(entity))
                    {
                        rotations[entity] = new Rotation { Value = rotation };
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
                for (int i = 0; i < roots.Length; i++)
                {
                    Entity entity = roots[i];
                    float4x4 matrix = rootMatrices[i];
                    Entity child;
                    NativeMultiHashMapIterator<Entity> iterator;
                    bool found = hierarchy.TryGetFirstValue(entity, out child, out iterator);
                    while (found)
                    {
                        TransformTree(child,matrix);
                        found = hierarchy.TryGetNextValue(out child, ref iterator);
                    }
                }
            }
        }

        [ComputeJobOptimization]
        struct ClearHierarchy : IJob
        {
            public  NativeMultiHashMap<Entity, Entity> hierarchy;

            public void Execute()
            {
                hierarchy.Clear();
            }
        }

        NativeMultiHashMap<Entity, Entity> m_Hierarchy;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            int rootCount = m_RootRotTransTransformGroup.Length + m_RootRotTransNoTransformGroup.Length +
                            m_RootRotTransformGroup.Length + m_RootRotNoTransformGroup.Length +
                            m_RootTransTransformGroup.Length + m_RootTransNoTransformGroup.Length;
            if (rootCount == 0)
            {
                return inputDeps;
            }
            
            //
            // Update Roots
            //

            var updateRootsDeps = inputDeps;
            var updateRootsBarrierJobHandle = new JobHandle();

            if (m_ParentGroup.Length > 0)
            {
                m_Hierarchy.Capacity = math.max(m_ParentGroup.Length + rootCount,m_Hierarchy.Capacity);
                m_Hierarchy.Clear();
                
                var buildHierarchyJob = new BuildHierarchy
                {
                    hierarchy = m_Hierarchy,
                    transformParents = m_ParentGroup.transformParents,
                    entities = m_ParentGroup.entities
                };
                var buildHierarchyJobHandle = buildHierarchyJob.Schedule(m_ParentGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = buildHierarchyJobHandle;
            }

            NativeArray<float4x4>? rotTransTransformRootMatrices = null;
            if (m_RootRotTransTransformGroup.Length > 0)
            {
                rotTransTransformRootMatrices = new NativeArray<float4x4>(m_RootRotTransTransformGroup.Length, Allocator.TempJob);
                var updateRotTransTransformRootsJob = new UpdateRotTransTransformRoots
                {
                    rotations = m_RootRotTransTransformGroup.rotations,
                    positions = m_RootRotTransTransformGroup.positions,
                    matrices = rotTransTransformRootMatrices.Value,
                    transforms = m_RootRotTransTransformGroup.transforms
                };
                var updateRotTransTransformRootsJobHandle = updateRotTransTransformRootsJob.Schedule(m_RootRotTransTransformGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = JobHandle.CombineDependencies(updateRootsBarrierJobHandle, updateRotTransTransformRootsJobHandle);
            }
            
            NativeArray<float4x4>? rotTransNoTransformRootMatrices = null;
            if (m_RootRotTransNoTransformGroup.Length > 0)
            {
                rotTransNoTransformRootMatrices = new NativeArray<float4x4>(m_RootRotTransNoTransformGroup.Length, Allocator.TempJob);
                var updateRotTransNoTransformRootsJob = new UpdateRotTransNoTransformRoots
                {
                    rotations = m_RootRotTransNoTransformGroup.rotations,
                    positions = m_RootRotTransNoTransformGroup.positions,
                    matrices = rotTransNoTransformRootMatrices.Value
                };
                var updateRotTransNoTransformRootsJobHandle = updateRotTransNoTransformRootsJob.Schedule(m_RootRotTransNoTransformGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = JobHandle.CombineDependencies(updateRootsBarrierJobHandle, updateRotTransNoTransformRootsJobHandle);
            }
            
            NativeArray<float4x4>? rotTransformRootMatrices = null;
            if (m_RootRotTransformGroup.Length > 0)
            {
                rotTransformRootMatrices = new NativeArray<float4x4>(m_RootRotTransformGroup.Length, Allocator.TempJob);
                var updateRotTransformRootsJob = new UpdateRotTransformRoots
                {
                    rotations = m_RootRotTransformGroup.rotations,
                    matrices = rotTransformRootMatrices.Value,
                    transforms = m_RootRotTransformGroup.transforms
                };
                var updateRotTransformRootsJobHandle = updateRotTransformRootsJob.Schedule(m_RootRotTransformGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = JobHandle.CombineDependencies(updateRootsBarrierJobHandle, updateRotTransformRootsJobHandle);
            }
            
            NativeArray<float4x4>? rotNoTransformRootMatrices = null;
            if (m_RootRotNoTransformGroup.Length > 0)
            {
                rotNoTransformRootMatrices = new NativeArray<float4x4>(m_RootRotNoTransformGroup.Length, Allocator.TempJob);
                var updateRotNoTransformRootsJob = new UpdateRotNoTransformRoots
                {
                    rotations = m_RootRotNoTransformGroup.rotations,
                    matrices = rotNoTransformRootMatrices.Value
                };
                var updateRotNoTransformRootsJobHandle = updateRotNoTransformRootsJob.Schedule(m_RootRotNoTransformGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = JobHandle.CombineDependencies(updateRootsBarrierJobHandle, updateRotNoTransformRootsJobHandle);
            }
            
            NativeArray<float4x4>? transTransformRootMatrices = null;
            if (m_RootTransTransformGroup.Length > 0)
            {
                transTransformRootMatrices = new NativeArray<float4x4>(m_RootTransTransformGroup.Length, Allocator.TempJob);
                var updateTransTransformRootsJob = new UpdateTransTransformRoots
                {
                    positions = m_RootTransTransformGroup.positions,
                    matrices = transTransformRootMatrices.Value,
                    transforms = m_RootTransTransformGroup.transforms
                };
                var updateTransTransformRootsJobHandle = updateTransTransformRootsJob.Schedule(m_RootTransTransformGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = JobHandle.CombineDependencies(updateRootsBarrierJobHandle, updateTransTransformRootsJobHandle);
            }
            
            NativeArray<float4x4>? transNoTransformRootMatrices = null;
            if (m_RootTransNoTransformGroup.Length > 0)
            {
                transNoTransformRootMatrices = new NativeArray<float4x4>(m_RootTransNoTransformGroup.Length, Allocator.TempJob);
                var updateTransNoTransformRootsJob = new UpdateTransNoTransformRoots
                {
                    positions = m_RootTransNoTransformGroup.positions,
                    matrices = transNoTransformRootMatrices.Value
                };
                var updateTransNoTransformRootsJobHandle = updateTransNoTransformRootsJob.Schedule(m_RootTransNoTransformGroup.Length, 64, updateRootsDeps);
                updateRootsBarrierJobHandle = JobHandle.CombineDependencies(updateRootsBarrierJobHandle, updateTransNoTransformRootsJobHandle);
            }

            if (m_ParentGroup.Length == 0)
            {
                // 
                // Dispose matrices if there are no hierarchies. #todo Don't allocate them in that case 
                //
                
                var disposeMatricesDeps = updateRootsBarrierJobHandle;
                var disposeMatricesBarrierJobHandle = new JobHandle();

                if (m_RootRotTransTransformGroup.Length > 0)
                {
                    var disposeRotTransTransformMatricesJob = new DisposeMatrices
                    {
                        rootMatrices = rotTransTransformRootMatrices.Value
                    };
                    var disposeRotTransTransformMatricesJobHandle = disposeRotTransTransformMatricesJob.Schedule(disposeMatricesDeps);
                    disposeMatricesBarrierJobHandle = JobHandle.CombineDependencies(disposeMatricesBarrierJobHandle, disposeRotTransTransformMatricesJobHandle);
                }
                
                if (m_RootRotTransNoTransformGroup.Length > 0)
                {
                    var disposeRotTransNoTransformMatricesJob = new DisposeMatrices
                    {
                        rootMatrices = rotTransNoTransformRootMatrices.Value
                    };
                    var disposeRotTransNoTransformMatricesJobHandle = disposeRotTransNoTransformMatricesJob.Schedule(disposeMatricesDeps);
                    disposeMatricesBarrierJobHandle = JobHandle.CombineDependencies(disposeMatricesBarrierJobHandle, disposeRotTransNoTransformMatricesJobHandle);
                }
                
                if (m_RootRotTransformGroup.Length > 0)
                {
                    var disposeRotTransformMatricesJob = new DisposeMatrices
                    {
                        rootMatrices = rotTransformRootMatrices.Value
                    };
                    var disposeRotTransformMatricesJobHandle = disposeRotTransformMatricesJob.Schedule(disposeMatricesDeps);
                    disposeMatricesBarrierJobHandle = JobHandle.CombineDependencies(disposeMatricesBarrierJobHandle, disposeRotTransformMatricesJobHandle);
                }
                
                if (m_RootRotNoTransformGroup.Length > 0)
                {
                    var disposeRotNoTransformMatricesJob = new DisposeMatrices
                    {
                        rootMatrices = rotNoTransformRootMatrices.Value
                    };
                    var disposeRotNoTransformMatricesJobHandle = disposeRotNoTransformMatricesJob.Schedule(disposeMatricesDeps);
                    disposeMatricesBarrierJobHandle = JobHandle.CombineDependencies(disposeMatricesBarrierJobHandle, disposeRotNoTransformMatricesJobHandle);
                }
                
                if (m_RootTransTransformGroup.Length > 0)
                {
                    var disposeTransTransformMatricesJob = new DisposeMatrices
                    {
                        rootMatrices = transTransformRootMatrices.Value
                    };
                    var disposeTransTransformMatricesJobHandle = disposeTransTransformMatricesJob.Schedule(disposeMatricesDeps);
                    disposeMatricesBarrierJobHandle = JobHandle.CombineDependencies(disposeMatricesBarrierJobHandle, disposeTransTransformMatricesJobHandle);
                }
                
                if (m_RootTransNoTransformGroup.Length > 0)
                {
                    var disposeTransNoTransformMatricesJob = new DisposeMatrices
                    {
                        rootMatrices = transNoTransformRootMatrices.Value
                    };
                    var disposeTransNoTransformMatricesJobHandle = disposeTransNoTransformMatricesJob.Schedule(disposeMatricesDeps);
                    disposeMatricesBarrierJobHandle = JobHandle.CombineDependencies(disposeMatricesBarrierJobHandle, disposeTransNoTransformMatricesJobHandle);
                }

                return disposeMatricesBarrierJobHandle;
            }
            
            //
            // Copy Root Entities for Sub Hierarchy Transform
            //

            var copyRootEntitiesDeps = updateRootsBarrierJobHandle;
            var copyRootEntitiesBarrierJobHandle = new JobHandle();

            NativeArray<Entity>? rotTransTransformRoots;
            if (m_RootRotTransTransformGroup.Length > 0)
            {
                rotTransTransformRoots = new NativeArray<Entity>(m_RootRotTransTransformGroup.Length, Allocator.TempJob);
                var copyRotTransTransformRootsJob = new CopyEntities
                {
                    source = m_RootRotTransTransformGroup.entities,
                    results = rotTransTransformRoots.Value
                };
                var copyRotTransTransformRootsJobHandle = copyRotTransTransformRootsJob.Schedule(m_RootRotTransTransformGroup.Length, 64, copyRootEntitiesDeps);
                copyRootEntitiesBarrierJobHandle = JobHandle.CombineDependencies(copyRootEntitiesBarrierJobHandle,copyRotTransTransformRootsJobHandle);
            }
            
            NativeArray<Entity>? rotTransNoTransformRoots;
            if (m_RootRotTransNoTransformGroup.Length > 0)
            {
                rotTransNoTransformRoots = new NativeArray<Entity>(m_RootRotTransNoTransformGroup.Length, Allocator.TempJob);
                var copyRotTransNoTransformRootsJob = new CopyEntities
                {
                    source = m_RootRotTransNoTransformGroup.entities,
                    results = rotTransNoTransformRoots.Value
                };
                var copyRotTransNoTransformRootsJobHandle = copyRotTransNoTransformRootsJob.Schedule(m_RootRotTransNoTransformGroup.Length, 64, copyRootEntitiesDeps);
                copyRootEntitiesBarrierJobHandle = JobHandle.CombineDependencies(copyRootEntitiesBarrierJobHandle, copyRotTransNoTransformRootsJobHandle);
            }
            
            NativeArray<Entity>? rotTransformRoots;
            if (m_RootRotTransformGroup.Length > 0)
            {
                rotTransformRoots = new NativeArray<Entity>(m_RootRotTransformGroup.Length, Allocator.TempJob);
                var copyRotTransformRootsJob = new CopyEntities
                {
                    source = m_RootRotTransformGroup.entities,
                    results = rotTransformRoots.Value
                };
                var copyRotTransformRootsJobHandle = copyRotTransformRootsJob.Schedule(m_RootRotTransformGroup.Length, 64, copyRootEntitiesDeps);
                copyRootEntitiesBarrierJobHandle = JobHandle.CombineDependencies(copyRootEntitiesBarrierJobHandle,copyRotTransformRootsJobHandle);
            }
            
            NativeArray<Entity>? rotNoTransformRoots;
            if (m_RootRotNoTransformGroup.Length > 0)
            {
                rotNoTransformRoots = new NativeArray<Entity>(m_RootRotNoTransformGroup.Length, Allocator.TempJob);
                var copyRotNoTransformRootsJob = new CopyEntities
                {
                    source = m_RootRotNoTransformGroup.entities,
                    results = rotNoTransformRoots.Value
                };
                var copyRotNoTransformRootsJobHandle = copyRotNoTransformRootsJob.Schedule(m_RootRotNoTransformGroup.Length, 64, copyRootEntitiesDeps);
                copyRootEntitiesBarrierJobHandle = JobHandle.CombineDependencies(copyRootEntitiesBarrierJobHandle, copyRotNoTransformRootsJobHandle);
            }
            
            NativeArray<Entity>? transTransformRoots;
            if (m_RootTransTransformGroup.Length > 0)
            {
                transTransformRoots = new NativeArray<Entity>(m_RootTransTransformGroup.Length, Allocator.TempJob);
                var copyTransTransformRootsJob = new CopyEntities
                {
                    source = m_RootTransTransformGroup.entities,
                    results = transTransformRoots.Value
                };
                var copyTransTransformRootsJobHandle = copyTransTransformRootsJob.Schedule(m_RootTransTransformGroup.Length, 64, copyRootEntitiesDeps);
                copyRootEntitiesBarrierJobHandle = JobHandle.CombineDependencies(copyRootEntitiesBarrierJobHandle,copyTransTransformRootsJobHandle);
            }
            
            NativeArray<Entity>? transNoTransformRoots;
            if (m_RootTransNoTransformGroup.Length > 0)
            {
                transNoTransformRoots = new NativeArray<Entity>(m_RootTransNoTransformGroup.Length, Allocator.TempJob);
                var copyTransNoTransformRootsJob = new CopyEntities
                {
                    source = m_RootTransNoTransformGroup.entities,
                    results = transNoTransformRoots.Value
                };
                var copyTransNoTransformRootsJobHandle = copyTransNoTransformRootsJob.Schedule(m_RootTransNoTransformGroup.Length, 64, copyRootEntitiesDeps);
                copyRootEntitiesBarrierJobHandle = JobHandle.CombineDependencies(copyRootEntitiesBarrierJobHandle, copyTransNoTransformRootsJobHandle);
            }
            
            //
            // Update Sub Hierarchy
            //

            var updateSubHierarchyDeps = copyRootEntitiesBarrierJobHandle;
            var updateSubHierarchyBarrierJobHandle = new JobHandle();
            
            if (m_RootRotTransTransformGroup.Length > 0)
            {
                var updateRotTransTransformHierarchyJob = new UpdateSubHierarchy
                {
                    hierarchy = m_Hierarchy,
                    roots = rotTransTransformRoots.Value,
                    rootMatrices = rotTransTransformRootMatrices.Value,
                    localPositions = m_LocalPositions,
                    localRotations = m_LocalRotations,
                    positions = m_Positions,
                    rotations = m_Rotations,
                    transformMatrices = m_TransformMatrices
                };
                var updateRotTransTransformHierarchyJobHandle = updateRotTransTransformHierarchyJob.Schedule(updateSubHierarchyDeps);
                updateSubHierarchyBarrierJobHandle = JobHandle.CombineDependencies(updateSubHierarchyBarrierJobHandle,updateRotTransTransformHierarchyJobHandle);
            }
            
            if (m_RootRotTransNoTransformGroup.Length > 0)
            {
                var updateRotTransNoTransformHierarchyJob = new UpdateSubHierarchy
                {
                    hierarchy = m_Hierarchy,
                    roots = rotTransNoTransformRoots.Value,
                    rootMatrices = rotTransNoTransformRootMatrices.Value,
                    localPositions = m_LocalPositions,
                    localRotations = m_LocalRotations,
                    positions = m_Positions,
                    rotations = m_Rotations,
                    transformMatrices = m_TransformMatrices
                };
                var updateRotTransNoTransformHierarchyJobHandle = updateRotTransNoTransformHierarchyJob.Schedule(updateSubHierarchyDeps);
                updateSubHierarchyBarrierJobHandle = JobHandle.CombineDependencies(updateSubHierarchyBarrierJobHandle,updateRotTransNoTransformHierarchyJobHandle);
            }
            
            if (m_RootRotTransformGroup.Length > 0)
            {
                var updateRotTransformHierarchyJob = new UpdateSubHierarchy
                {
                    hierarchy = m_Hierarchy,
                    roots = rotTransformRoots.Value,
                    rootMatrices = rotTransformRootMatrices.Value,
                    localPositions = m_LocalPositions,
                    localRotations = m_LocalRotations,
                    positions = m_Positions,
                    rotations = m_Rotations,
                    transformMatrices = m_TransformMatrices
                };
                var updateRotTransformHierarchyJobHandle = updateRotTransformHierarchyJob.Schedule(updateSubHierarchyDeps);
                updateSubHierarchyBarrierJobHandle = JobHandle.CombineDependencies(updateSubHierarchyBarrierJobHandle,updateRotTransformHierarchyJobHandle);
            }
            
            if (m_RootRotNoTransformGroup.Length > 0)
            {
                var updateRotNoTransformHierarchyJob = new UpdateSubHierarchy
                {
                    hierarchy = m_Hierarchy,
                    roots = rotNoTransformRoots.Value,
                    rootMatrices = rotNoTransformRootMatrices.Value,
                    localPositions = m_LocalPositions,
                    localRotations = m_LocalRotations,
                    positions = m_Positions,
                    rotations = m_Rotations,
                    transformMatrices = m_TransformMatrices
                };
                var updateRotNoTransformHierarchyJobHandle = updateRotNoTransformHierarchyJob.Schedule(updateSubHierarchyDeps);
                updateSubHierarchyBarrierJobHandle = JobHandle.CombineDependencies(updateSubHierarchyBarrierJobHandle,updateRotNoTransformHierarchyJobHandle);
            }
            
            if (m_RootTransTransformGroup.Length > 0)
            {
                var updateTransTransformHierarchyJob = new UpdateSubHierarchy
                {
                    hierarchy = m_Hierarchy,
                    roots = transTransformRoots.Value,
                    rootMatrices = transTransformRootMatrices.Value,
                    localPositions = m_LocalPositions,
                    localRotations = m_LocalRotations,
                    positions = m_Positions,
                    rotations = m_Rotations,
                    transformMatrices = m_TransformMatrices
                };
                var updateTransTransformHierarchyJobHandle = updateTransTransformHierarchyJob.Schedule(updateSubHierarchyDeps);
                updateSubHierarchyBarrierJobHandle = JobHandle.CombineDependencies(updateSubHierarchyBarrierJobHandle,updateTransTransformHierarchyJobHandle);
            }
            
            if (m_RootTransNoTransformGroup.Length > 0)
            {
                var updateTransNoTransformHierarchyJob = new UpdateSubHierarchy
                {
                    hierarchy = m_Hierarchy,
                    roots = transNoTransformRoots.Value,
                    rootMatrices = transNoTransformRootMatrices.Value,
                    localPositions = m_LocalPositions,
                    localRotations = m_LocalRotations,
                    positions = m_Positions,
                    rotations = m_Rotations,
                    transformMatrices = m_TransformMatrices
                };
                var updateTransNoTransformHierarchyJobHandle = updateTransNoTransformHierarchyJob.Schedule(updateSubHierarchyDeps);
                updateSubHierarchyBarrierJobHandle = JobHandle.CombineDependencies(updateSubHierarchyBarrierJobHandle,updateTransNoTransformHierarchyJobHandle);
            }

            return updateSubHierarchyBarrierJobHandle;
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
