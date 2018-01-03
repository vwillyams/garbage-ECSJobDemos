using System.Linq;
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
    const int                          MaxChunksPerFrame = 4;

    struct GridChunk
    {
        public int2             Pos;
        public ComponentType    ChunkSceneType;
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


    protected override void OnCreateManager(int capacity)
    {
        m_CreatedChunks = new NativeList<GridChunk>(capacity, Allocator.Persistent);
        m_SpawnLocationCaches = new NativeList<SpawnData>[MaxChunksPerFrame];

        for (int i = 0;i != m_SpawnLocationCaches.Length;i++)
            m_SpawnLocationCaches[i] = new NativeList<SpawnData>(1024, Allocator.Persistent);

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VegetationAssets/ECSVegetation0.prefab");
    }

    protected override void OnDestroyManager()
    {
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

    public int GetToBeCreatedGridPositions(NativeArray<int2> visible, NativeArray<int2> outGridPositions)
    {
        //@TODO: Foreach support
        // foreach (var gridPos in visible)
        int count = 0;
        for (int i = 0;i != visible.Length && count != outGridPositions.Length;i++)
        {
            var gridPos = visible[i];
            //@TODO: Need Contains method here...

            ComponentType type;
            if (!HasChunk(gridPos))
                outGridPositions[count++] = gridPos;
        }

        return count;
    }

    public void GetToBeDestroyedGridPositions(NativeArray<int2> visible, NativeList<int2> toBeDestroyed)
    {
        for (int i = 0; i != m_CreatedChunks.Length; i++)
        {
            if (!visible.Contains(m_CreatedChunks[i].Pos))
                toBeDestroyed.Add(m_CreatedChunks[i].Pos);
        }
    }

    public bool HasChunk(int2 gridPos)
    {
        return GetCreatedChunksIndex(gridPos) != -1;
    }

    public int GetCreatedChunksIndex(int2 gridPos)
    {
        for (int i = 0; i != m_CreatedChunks.Length; i++)
        {
            //@TODO: Thats a bit annoying...
            if (math.all(m_CreatedChunks[i].Pos == gridPos))
                return i;
        }

        return -1;
    }

    struct SpawnData
    {
        public float3 Position;
    }

    struct CalculateChunkSpawnLocationsJob : IJob
    {
        public int2                 ChunkPosition;
        [ReadOnly]
        public ComponentDataArray<CollisionMeshInstance> CollisionInstances;

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

    void Destroy(int2 position)
    {
        var chunkScene = new ProceduralChunkScene();
        chunkScene.Position = position;

        Profiler.BeginSample("Destroy.CreateComponentGroup");
        var chunkSceneType = EntityManager.CreateSharedComponentType(chunkScene);
        var group = EntityManager.CreateComponentGroup(chunkSceneType);
        Profiler.EndSample();

        //@TODO: This is highly inconvenient...
        Profiler.BeginSample("Destroy.GetEntities");
        var entityGroupArray = group.GetEntityArray();
        var entityArray = new NativeArray<Entity>(entityGroupArray.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        entityGroupArray.CopyTo(entityArray);
        Profiler.EndSample();

        Profiler.BeginSample("Destroy.DestroyEntity");
        EntityManager.DestroyEntity(entityArray);
        Profiler.EndSample();

        entityArray.Dispose();

        group.Dispose();

        //@TODO: Need value based search function...
        m_CreatedChunks.RemoveAtSwapBack(GetCreatedChunksIndex(position));
    }

    //@TODO: Wow this is just insane amounts of code necessary for the simplest possible thing...
    EntityArchetype GetArchetype(int2 chunkPosition, out ComponentType chunkSceneType)
    {
        ComponentType[] types;
        Component[] components;
        GameObjectEntity.GetComponents(EntityManager, prefab, false, out types, out components);

        ComponentType[] typesWithChunk = new ComponentType[types.Length + 1];
        types.CopyTo(typesWithChunk, 0);

        var chunkSceneComponent = new ProceduralChunkScene() { Position = chunkPosition };
        chunkSceneType = EntityManager.CreateSharedComponentType(chunkSceneComponent);
        typesWithChunk[types.Length ] = chunkSceneType;

        return EntityManager.CreateArchetype(typesWithChunk);
    }


    protected override JobHandle OnUpdate(JobHandle dependency)
    {
        //@TODO: When is a good time for this to be called?
        Profiler.BeginSample("CommitTransaction");
        EntityManager.CommitTransaction();
        Profiler.EndSample();

        Profiler.BeginSample("CalculateVisible");
        // Calculate all grid positions that should be visible
        var visibleGridPositions = new NativeList<int2>(0, Allocator.Temp);
        {
            var cameras = GetEntities<CameraEntity>();
            if (cameras.Length != 0)
            {
                float3 cameraPos = cameras[0].Transform.position;
                float cullingDistance = cameras[0].View.Distance;
                CalculateVisibleGridPositions(cameraPos.xz, cullingDistance, GridSize, visibleGridPositions);
            }
        }
        Profiler.EndSample();

        Profiler.BeginSample("DestroyChunk");
        // Destroys any invisible grid positions
        var destroyGridPositions = new NativeList<int2>(0, Allocator.Temp);
        GetToBeDestroyedGridPositions(visibleGridPositions, destroyGridPositions);
        for (int i = 0; i != destroyGridPositions.Length; i++)
            Destroy(destroyGridPositions[i]);
        Profiler.EndSample();


        Profiler.BeginSample("Schedule Chunk Creation");
        // Schedule jobs for each grid chunk that became visible


        var toBeCreatedChunks = new NativeArray<int2>(MaxChunksPerFrame, Allocator.Temp);
        int toBeCreatedCount = GetToBeCreatedGridPositions(visibleGridPositions, toBeCreatedChunks);

        if (toBeCreatedCount != 0)
        {
            // Extract all archetypes first, because extracting the archetype will CommitTransaction...
            // @TODO: When Simons SharedComponentData refactor is ready this should be unnecessary...
            var archetypes = new NativeArray<EntityArchetype>(toBeCreatedCount, Allocator.Temp);
            var chunkSceneTypes = new NativeArray<ComponentType>(toBeCreatedCount, Allocator.Temp);
            for (int i = 0; i != toBeCreatedCount; i++)
            {
                ComponentType type;
                archetypes[i] = GetArchetype(toBeCreatedChunks[i], out type);
                chunkSceneTypes[i] = type;
            }

            UpdateInjectedComponentGroups();

            var spawnInputDependency = dependency;
            dependency = JobHandle.CombineDependencies(EntityManager.EntityTransactionDependency, dependency);

            var transaction = EntityManager.BeginTransaction();
            for (int i=0;i != toBeCreatedCount;i++)
            {
                var createPos = toBeCreatedChunks[i];

                var spawnLocationJob = new CalculateChunkSpawnLocationsJob();
                spawnLocationJob.ChunkPosition = createPos;
                spawnLocationJob.CollisionInstances = m_CollisionMeshes.Instances;
                spawnLocationJob.SpawnLocations = m_SpawnLocationCaches[i];
                spawnLocationJob.SpawnLocations.Clear();

                var spawnDependency = spawnLocationJob.Schedule(spawnInputDependency);

                var chunkJob = new PopulateChunk();
                chunkJob.Archetype = archetypes[i];
                chunkJob.EntityTransaction = transaction;
                chunkJob.ChunkPosition = createPos;
                chunkJob.SpawnLocations = spawnLocationJob.SpawnLocations;

                m_CreatedChunks.Add(new GridChunk(){ Pos = createPos, ChunkSceneType = chunkSceneTypes[i] });

                dependency = chunkJob.Schedule(JobHandle.CombineDependencies(spawnDependency, dependency));
            }

            EntityManager.EntityTransactionDependency = dependency;

            archetypes.Dispose();
            chunkSceneTypes.Dispose();
        }
        toBeCreatedChunks.Dispose();
        Profiler.EndSample();

        destroyGridPositions.Dispose();
        visibleGridPositions.Dispose();

        return dependency;
    }
}
