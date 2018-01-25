using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using UnityEngine.Profiling;

public struct ProceduralChunkScene : ISharedComponentData
{
    public int2 Position;
}

public class ProceduralSpawnSystem : JobComponentSystem
{
    public GameObject prefab;

    const float                        GridSize = 1.0F;
    const float                        PlantDensity = 0.5F;
    const int                          MaxCreateChunksPerFrame = 8;
    const int                          MaxDestroyChunksPerFrame = 12;
    const bool                         Deterministic = false;

    struct GridChunk : IEquatable<int2>
    {
        public int2             Pos;
        public ComponentType    ChunkSceneType;

        public bool Equals(int2 pos)
        {
            return Pos.Equals(pos);
        }
    }


    //@TODO: This would be better expressed with a hashmap,
    //       however we currently can't iterate over all elements...
    NativeList<GridChunk> m_CreatedChunks;


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

    [InjectComponentGroup] CollisionMeshInstances m_CollisionMeshes;


    World m_ConstructionWorld;
    EntityManager m_ConstructionManager;


    protected override void OnCreateManager(int capacity)
    {
        m_CreatedChunks = new NativeList<GridChunk>(capacity, Allocator.Persistent);
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

    static int GetToBeCreatedGridPositions(NativeArray<GridChunk> createdChunks, NativeArray<int2> visible, NativeArray<int2> outGridPositions)
    {
        //@TODO: Foreach support
        // foreach (var gridPos in visible)
        int count = 0;
        for (int i = 0;i != visible.Length && count != outGridPositions.Length;i++)
        {
            var gridPos = visible[i];
            //@TODO: Need Contains method here...

            ComponentType type;
            if (!createdChunks.Contains(gridPos))
                outGridPositions[count++] = gridPos;
        }

        return count;
    }

    static int GetToBeDestroyedGridPositions(NativeArray<GridChunk> createdChunks, NativeArray<int2> visible, NativeArray<int2> toBeDestroyed)
    {
        int count = 0;
        for (int i = 0; i != createdChunks.Length && count != toBeDestroyed.Length; i++)
        {
            if (!visible.Contains(createdChunks[i].Pos))
                toBeDestroyed[count++] = createdChunks[i].Pos;
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
                        SpawnLocations.Add(spawn);
                    }
                }
            }
        }
    }

    [ComputeJobOptimization]
    struct CalculateToCreateAndDestroyChunks : IJob
    {
        //@TODO: Would be nice if this could be a local variable in the job...
        public NativeList<int2> visibleGridPositions;

        public NativeArray<GridChunk>  createdChunks;

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
        //@TODO: This is just a hack. No data from the prefab gets transported except for ISharedComponentData
        public EntityArchetype      Archetype;

        public EntityTransaction    EntityTransaction;
        public int2                 ChunkPosition;

        [ReadOnly]
        //@TODO: Not supported. This is super annoying...
        //[DeallocateOnJobCompletion]
        public NativeList<SpawnData> SpawnLocations;


        public void Execute()
        {
            var entities = new NativeArray<Entity>(SpawnLocations.Length, Allocator.Temp);
            EntityTransaction.CreateEntity(Archetype, entities);

            for (int i = 0; i != entities.Length; i++)
            {
                InstanceRendererTransform transform;
                transform.matrix = Matrix4x4.Translate(SpawnLocations[i].Position);
                EntityTransaction.SetComponent(entities[i], transform);
            }

            entities.Dispose();
        }
    }

    void DestroyChunk(int2 position)
    {
        var chunkScene = new ProceduralChunkScene();
        chunkScene.Position = position;

        Profiler.BeginSample("DestroyChunk.CreateComponentGroup");
        var chunkSceneType = EntityManager.CreateSharedComponentType(chunkScene);
        var group = EntityManager.CreateComponentGroup(chunkSceneType);
        Profiler.EndSample();

        //@TODO: This is highly inconvenient...
        Profiler.BeginSample("DestroyChunk.GetEntities");
        var entityGroupArray = group.GetEntityArray();
        var entityArray = new NativeArray<Entity>(entityGroupArray.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        entityGroupArray.CopyTo(entityArray);
        Profiler.EndSample();

        Profiler.BeginSample("DestroyChunk.DestroyEntity");
        if (entityArray.Length != 0)
            EntityManager.DestroyEntity(entityArray);
        Profiler.EndSample();

        entityArray.Dispose();

        group.Dispose();

        //@TODO: Need value based search function...
        m_CreatedChunks.RemoveAtSwapBack(m_CreatedChunks.IndexOf(position));
    }

    //@TODO: Wow this is just insane amounts of code necessary for the simplest possible thing...
    void GetArchetype(int2 chunkPosition, out EntityArchetype archetype, out ComponentType chunkSceneType)
    {
        ComponentType[] types;
        Component[] components;
        GameObjectEntity.GetComponents(m_ConstructionManager, prefab, false, out types, out components);

        ComponentType[] typesWithChunk = new ComponentType[types.Length + 1];
        types.CopyTo(typesWithChunk, 0);

        var chunkSceneComponent = new ProceduralChunkScene() { Position = chunkPosition };
        chunkSceneType = m_ConstructionManager.CreateSharedComponentType(chunkSceneComponent);
        typesWithChunk[types.Length ] = chunkSceneType;

        archetype = m_ConstructionManager.CreateArchetype(typesWithChunk);
    }


    protected override JobHandle OnUpdate(JobHandle dependency)
    {
        //@TODO: When is a good time for this to be called?
        Profiler.BeginSample("CommitTransaction");

        bool scheduleCreation = Deterministic || m_ConstructionManager.EntityTransactionDependency.IsCompleted;
        if (scheduleCreation)
            EntityManager.MoveEntitiesFrom(m_ConstructionManager);
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

            //@TODO: Error message produced from this seems wrong...
            //toBeCreatedChunks.Dispose();
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

        // Extract all archetypes first, because extracting the archetype will CommitTransaction...
        // @TODO: When Simons SharedComponentData refactor is ready this should be unnecessary...
        var archetypes = new NativeArray<EntityArchetype>(toBeCreatedChunks.Length, Allocator.Temp);
        var chunkSceneTypes = new NativeArray<ComponentType>(toBeCreatedChunks.Length, Allocator.Temp);
        for (int i = 0; i != toBeCreatedChunks.Length; i++)
        {
            ComponentType type;
            EntityArchetype arch;
            GetArchetype(toBeCreatedChunks[i], out arch, out type);
            chunkSceneTypes[i] = type;
            archetypes[i] = arch;
        }

        var spawnDependencies = new NativeArray<JobHandle>(toBeCreatedChunks.Length, Allocator.Temp);

        var transaction = m_ConstructionManager.BeginTransaction();
        for (int i = 0; i != toBeCreatedChunks.Length; i++)
        {
            var createPos = toBeCreatedChunks[i];

            var spawnLocationJob = new CalculateChunkSpawnLocationsJob();
            spawnLocationJob.ChunkPosition = createPos;
            spawnLocationJob.CollisionInstances = doubleBufferCollision.DstCollisionInstances;
            spawnLocationJob.SpawnLocations = m_SpawnLocationCaches[i];
            spawnLocationJob.SpawnLocations.Clear();

            spawnDependencies[i] = spawnLocationJob.Schedule(dependency);
        }

        for (int i = 0; i != toBeCreatedChunks.Length; i++)
        {
            var createPos = toBeCreatedChunks[i];

            GridChunk gridChunk;
            gridChunk.Pos = createPos;
            gridChunk.ChunkSceneType = chunkSceneTypes[i];

            PopulateChunk chunkJob;
            chunkJob.EntityTransaction = transaction;
            chunkJob.ChunkPosition = createPos;
            chunkJob.SpawnLocations = m_SpawnLocationCaches[i];
            chunkJob.Archetype = archetypes[i];

            m_CreatedChunks.Add(gridChunk);

            var chunkDependency = JobHandle.CombineDependencies(spawnDependencies[i], m_ConstructionManager.EntityTransactionDependency);
            m_ConstructionManager.EntityTransactionDependency = chunkJob.Schedule(chunkDependency);
        }

        spawnDependencies.Dispose();
        archetypes.Dispose();
        chunkSceneTypes.Dispose();

        var deallocateCollisionJob = new DeallocateCollisionInstancesJob();
        deallocateCollisionJob.CollisionInstances = doubleBufferCollision.DstCollisionInstances;
        m_ConstructionManager.EntityTransactionDependency = deallocateCollisionJob.Schedule(deallocateCollisionJob.CollisionInstances.Length, 16, m_ConstructionManager.EntityTransactionDependency);

        return dependency;
    }
}
