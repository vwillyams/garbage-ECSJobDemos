﻿using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using UnityEngine.Profiling;
using Unity.Transforms;

public struct ProceduralChunkScene : ISharedComponentData
{
    public int2 Position;
}

public class ProceduralSpawnSystem : JobComponentSystem
{
    public GameObject prefab;

    const float                        GridSize = 1.0F;
    const float                        PlantDensity = 0.2F;
    const int                          MaxCreateChunksPerFrame = 8;
    const int                          MaxDestroyChunksPerFrame = 12;
    const bool                         Deterministic = false;


    //@TODO: This would be better expressed with a hashmap,
    //       however we currently can't iterate over all elements...
    NativeList<int2> m_CreatedChunks;


    NativeList<SpawnData>[] m_SpawnLocationCaches;



    struct CameraEntity
    {
        public Transform             Transform;
        public ProceduralSpawnView   View;
    }

    unsafe struct CollisionMeshInstances
    {
        [ReadOnly]
        public ComponentDataArray<CollisionMeshInstance> Instances;
    }

    [Inject] CollisionMeshInstances m_CollisionMeshes;


    World m_ConstructionWorld;
    EntityManager m_ConstructionManager;
    ComponentGroup m_AllChunksGroup;


    protected override void OnCreateManager(int capacity)
    {
        m_AllChunksGroup = GetComponentGroup(typeof(ProceduralChunkScene));

        m_CreatedChunks = new NativeList<int2>(capacity, Allocator.Persistent);
        m_SpawnLocationCaches = new NativeList<SpawnData>[MaxCreateChunksPerFrame];

        for (int i = 0;i != m_SpawnLocationCaches.Length;i++)
            m_SpawnLocationCaches[i] = new NativeList<SpawnData>(1024, Allocator.Persistent);

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VegetationAssets/ECSVegetation0.prefab");


        //@TODO: how does leak detection work for world creation?
        m_ConstructionWorld = new World("Procedural construction world");
        m_ConstructionManager = m_ConstructionWorld.GetOrCreateManager<EntityManager>();
        //@TODO: Would be nice if this wasn't necessary and transactions could increase capacity...
        m_ConstructionManager.EntityCapacity = 1000 * 1000;
    }

    protected override void OnDestroyManager()
    {
        m_ConstructionWorld.Dispose();

        m_CreatedChunks.Dispose();
        for (int i = 0;i != m_SpawnLocationCaches.Length;i++)
            m_SpawnLocationCaches[i].Dispose();
    }
    // * Calculate all grid positions within a distance to camera position
    // * Create delta against to be visible grid positions from currently loaded grid positions
    // * Create the chunks in the delta grids
    //   - Place a set of vegetation for each grid chunk, based on a raycast towards the closest mesh


    // 1. How do i instantiate from an existing entity. (Not safe)
    // 2. SharedComponentData model is wrong
    // 3.

    public static void CalculateVisibleGridPositions(float2 position, float maxDistance, float gridSize, NativeList<int2> visible)
    {
        //@TODO: Cast is inconvenient...
        int2 minGrid = (int2)(math.floor(position - new float2(maxDistance)) / gridSize);
        int2 maxGrid = (int2)(math.ceil (position + new float2(maxDistance)) / gridSize);

        for (int y = minGrid.y; y != maxGrid.y; y++)
        {
            for (int x = minGrid.x; x != maxGrid.x; x++)
            {
                var minRect = new float2(x, y) * gridSize;
                var maxRect = new float2(x+1, y+1) * gridSize;
                if (GeometryUtility.CircleIntersectsRectangle(position, maxDistance, minRect, maxRect))
                    visible.Add(new int2(x, y));
            }
        }
    }

    static int GetToBeCreatedGridPositions(NativeArray<int2> createdChunks, NativeArray<int2> visible, NativeArray<int2> outGridPositions)
    {
        //@TODO: Foreach support
        // foreach (var gridPos in visible)
        int count = 0;
        for (int i = 0;i != visible.Length && count != outGridPositions.Length;i++)
        {
            var gridPos = visible[i];

            if (!createdChunks.Contains(gridPos))
                outGridPositions[count++] = gridPos;
        }

        return count;
    }

    static int GetToBeDestroyedGridPositions(NativeArray<int2> createdChunks, NativeArray<int2> visible, NativeArray<int2> toBeDestroyed)
    {
        int count = 0;
        for (int i = 0; i != createdChunks.Length && count != toBeDestroyed.Length; i++)
        {
            if (!visible.Contains(createdChunks[i]))
                toBeDestroyed[count++] = createdChunks[i];
        }
        return count;
    }

    struct SpawnData
    {
        public float3 Position;
    }

    [ComputeJobOptimization]
    struct DoubleBufferCollisionInstancesJob : IJobParallelFor
    {
        //@TODO: How do we treat refcounts of the blob asset reference here????
        [ReadOnly]
        public ComponentDataArray<CollisionMeshInstance> SrcCollisionInstances;
        public NativeArray<CollisionMeshInstance>        DstCollisionInstances;

        public void Execute(int index)
        {
            DstCollisionInstances[index] = SrcCollisionInstances[index];
            //@TODO: Doing this manually seems a bit annoying...
            DstCollisionInstances[index].CollisionMesh.Retain();
        }
    }

    [ComputeJobOptimization]
    struct DeallocateCollisionInstancesJob : IJobParallelFor
    {
        [DeallocateOnJobCompletion]
        public NativeArray<CollisionMeshInstance>        CollisionInstances;

        public void Execute(int index)
        {
            //@TODO: Doing this manually seems a bit annoying...
            CollisionInstances[index].CollisionMesh.Release();
        }
    }

    [ComputeJobOptimization]
    struct CalculateChunkSpawnLocationsJob : IJob
    {
        public int2                 ChunkPosition;
        [ReadOnly]
        public NativeArray<CollisionMeshInstance> CollisionInstances;

        public NativeList<SpawnData>        SpawnLocations;

        public void Execute()
        {
            SpawnLocations.Clear();

            //@TODO: Why is this cast necessary? Seems wrong...
            float2 min = ChunkPosition * (int)GridSize;
            float2 max = min + new float2(GridSize);

            for (float y = min.y; y < max.y; y+=PlantDensity)
            {
                for (float x = min.x; x < max.x; x+=PlantDensity)
                {
                    var origin = new float3(x, 1000, y);
                    float3 intersection;
                    if (GeometryUtility.RayIntersectsWorld(origin, new float3(0, -1, 0), CollisionInstances, out intersection))
                    {
                        SpawnData spawn;
                        spawn.Position = intersection;
                        for (int i = 0;i<10;i++)
                            SpawnLocations.Add(spawn);
                    }
                }
            }
        }
    }

    //@TODO: For some reason this don't run in Burst..
    //[ComputeJobOptimization]
    struct CalculateToCreateAndDestroyChunks : IJob
    {
        //@TODO: Would be nice if this could be a local variable in the job...
        public NativeList<int2> visibleGridPositions;

        public NativeArray<int2>  createdChunks;

        public NativeList<int2> destroyGridPositions;
        public NativeList<int2> createGridPositions;

        public float2 CameraPosition;
        public float  MaxDistance;
        public float  GridSize;

        
        public void Execute()
        {
            CalculateVisibleGridPositions(CameraPosition, MaxDistance, GridSize, visibleGridPositions);

            // Destroys any invisible grid positions
            destroyGridPositions.ResizeUninitialized(destroyGridPositions.Capacity);
            destroyGridPositions.ResizeUninitialized(GetToBeDestroyedGridPositions(createdChunks, visibleGridPositions, destroyGridPositions));

            createGridPositions.ResizeUninitialized(createGridPositions.Capacity);
            createGridPositions.ResizeUninitialized(GetToBeCreatedGridPositions(createdChunks, visibleGridPositions, createGridPositions));
        }
    }


    struct PopulateChunk : IJob
    {
        public ExclusiveEntityTransaction   EntityTransaction;
        public int2                         ChunkPosition;
        public Entity                       Prefab;
        public bool                         DestroyPrefab;

        [ReadOnly]
        //@TODO: Not supported. This is super annoying...
        //[DeallocateOnJobCompletion]
        public NativeList<SpawnData> SpawnLocations;

        public void Execute()
        {
            var entities = new NativeArray<Entity>(SpawnLocations.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            EntityTransaction.AddComponent(Prefab, ComponentType.Create<ProceduralChunkScene>());
            EntityTransaction.SetSharedComponentData(Prefab, new ProceduralChunkScene() { Position = ChunkPosition });

            EntityTransaction.Instantiate(Prefab, entities);

            for (int i = 0; i != entities.Length; i++)
            {
                TransformMatrix transform;
                transform.Value = Matrix4x4.Translate(SpawnLocations[i].Position);
                EntityTransaction.SetComponentData(entities[i], transform);
            }

            if (DestroyPrefab)
                EntityTransaction.DestroyEntity(Prefab);
            else
                EntityTransaction.RemoveComponent(Prefab, ComponentType.Create<ProceduralChunkScene>());

            entities.Dispose();
        }
    }

    void DestroyChunk(int2 position)
    {
        var chunkScene = new ProceduralChunkScene();
        chunkScene.Position = position;

        Profiler.BeginSample("DestroyChunk.SetFilter");
        m_AllChunksGroup.SetFilter(chunkScene);
        Profiler.EndSample();

        Profiler.BeginSample("DestroyChunk.DestroyEntity");
        EntityManager.DestroyEntity(m_AllChunksGroup);
        Profiler.EndSample();

        m_CreatedChunks.RemoveAtSwapBack(m_CreatedChunks.IndexOf(position));
    }

    protected override JobHandle OnUpdate(JobHandle dependency)
    {
        //@TODO: When is a good time for this to be called?
        Profiler.BeginSample("CommitTransaction");

        bool scheduleCreation = Deterministic || m_ConstructionManager.ExclusiveEntityTransactionDependency.IsCompleted;
        if (scheduleCreation)
        {
            m_ConstructionManager.EndExclusiveEntityTransaction();
            EntityManager.MoveEntitiesFrom(m_ConstructionManager);
        }


        Profiler.EndSample();

        Profiler.BeginSample("CalculateVisible");
        var calculateVisibleJob = new CalculateToCreateAndDestroyChunks();
        calculateVisibleJob.createdChunks = m_CreatedChunks;
        calculateVisibleJob.visibleGridPositions = new NativeList<int2>(512, Allocator.Temp);

        // Calculate all grid positions that should be visible
        calculateVisibleJob.createGridPositions = new NativeList<int2>(MaxCreateChunksPerFrame, Allocator.Temp);
        calculateVisibleJob.destroyGridPositions = new NativeList<int2>(MaxDestroyChunksPerFrame, Allocator.Temp);
        {
            var cameras = GetEntities<CameraEntity>();
            //@TODO: What if camera doesnt exist
            if (cameras.Length != 0)
            {
                float3 cameraPos = cameras[0].Transform.position;
                calculateVisibleJob.CameraPosition = cameraPos.xz;
                calculateVisibleJob.GridSize = GridSize;
                calculateVisibleJob.MaxDistance = cameras[0].View.Distance;
            }
        }
        Profiler.EndSample();


        calculateVisibleJob.Run();

        Profiler.BeginSample("DestroyChunk");
        {
           for (int i = 0; i != calculateVisibleJob.destroyGridPositions.Length; i++)
                DestroyChunk(calculateVisibleJob.destroyGridPositions[i]);
        }
        Profiler.EndSample();

        // Schedule jobs for each grid chunk that became visible
        if (scheduleCreation)
        {
            if (calculateVisibleJob.createGridPositions.Length != 0)
            {
                UpdateInjectedComponentGroups();

                Profiler.BeginSample("CreateChunks");
                dependency = ScheduleCreateChunks(dependency, calculateVisibleJob.createGridPositions);
                Profiler.EndSample();
            }
        }

        calculateVisibleJob.createGridPositions.Dispose();
        calculateVisibleJob.destroyGridPositions.Dispose();
        calculateVisibleJob.visibleGridPositions.Dispose();

        return dependency;
    }

    JobHandle ScheduleCreateChunks(JobHandle dependency, NativeArray<int2> toBeCreatedChunks)
    {
        // Double buffer collision instances so that this job can run truly async
        var doubleBufferCollision = new DoubleBufferCollisionInstancesJob();
        doubleBufferCollision.DstCollisionInstances = new NativeArray<CollisionMeshInstance>(m_CollisionMeshes.Instances.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        doubleBufferCollision.SrcCollisionInstances = m_CollisionMeshes.Instances;
        dependency = doubleBufferCollision.Schedule(m_CollisionMeshes.Instances.Length, 16, dependency);

        Profiler.BeginSample("Instantiate__");
        var prefabEntity = m_ConstructionManager.Instantiate(prefab);
        Profiler.EndSample();

        var spawnDependencies = new NativeArray<JobHandle>(toBeCreatedChunks.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        Profiler.BeginSample("BeginTransaction__");
        var transaction = m_ConstructionManager.BeginExclusiveEntityTransaction();
        Profiler.EndSample();

        Profiler.BeginSample("1__");
        for (int i = 0; i != toBeCreatedChunks.Length; i++)
        {
            var createPos = toBeCreatedChunks[i];

            CalculateChunkSpawnLocationsJob spawnLocationJob;
            spawnLocationJob.ChunkPosition = createPos;
            spawnLocationJob.CollisionInstances = doubleBufferCollision.DstCollisionInstances;
            spawnLocationJob.SpawnLocations = m_SpawnLocationCaches[i];

            spawnDependencies[i] = spawnLocationJob.Schedule(dependency);
        }
        Profiler.EndSample();

        Profiler.BeginSample("2__");

        for (int i = 0; i != toBeCreatedChunks.Length; i++)
        {
            var createPos = toBeCreatedChunks[i];

            PopulateChunk chunkJob;
            chunkJob.EntityTransaction = transaction;
            chunkJob.ChunkPosition = createPos;
            chunkJob.SpawnLocations = m_SpawnLocationCaches[i];
            chunkJob.Prefab = prefabEntity;
            chunkJob.DestroyPrefab = i == toBeCreatedChunks.Length - 1;

            m_CreatedChunks.Add(createPos);
            var chunkDependency = JobHandle.CombineDependencies(spawnDependencies[i], m_ConstructionManager.ExclusiveEntityTransactionDependency);
            m_ConstructionManager.ExclusiveEntityTransactionDependency = chunkJob.Schedule(chunkDependency);
        }
        Profiler.EndSample();

        spawnDependencies.Dispose();

        var deallocateCollisionJob = new DeallocateCollisionInstancesJob();
        deallocateCollisionJob.CollisionInstances = doubleBufferCollision.DstCollisionInstances;
        m_ConstructionManager.ExclusiveEntityTransactionDependency = deallocateCollisionJob.Schedule(deallocateCollisionJob.CollisionInstances.Length, 16, m_ConstructionManager.ExclusiveEntityTransactionDependency);

        return dependency;
    }
}
