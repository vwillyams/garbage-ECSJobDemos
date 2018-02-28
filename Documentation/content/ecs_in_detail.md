# ECS Features in Detail

## Entity

__Entity__ is an ID. You can think of it as a super lightweight __GameObject__ that does not even have a name by default.

You can add and remove __Components__ from Entities at runtime. Entity ID's are stable. In fact they are the only stable way to store a reference to another Component or Entity.

## IComponentData

Traditional Unity components (including __MonoBehaviour__) are [object-oriented](https://en.wikipedia.org/wiki/Object-oriented_programming) classes which contain data and methods for behavior. __IComponentData__ is a pure ECS-style Component, meaning that it defines no behavior, only data. IComponentDatas are structs rather than classes, meaning that they are copied [by value instead of by reference](https://stackoverflow.com/questions/373419/whats-the-difference-between-passing-by-reference-vs-passing-by-value?answertab=votes#tab-top) by default. You will usually need to use the following pattern to modify data:

```C#
var transform = group.transform[index]; // Read

transform.heading = playerInput.move; // Modify
transform.position += deltaTime * playerInput.move * settings.playerMoveSpeed;

group.transform[index] = transform; // Write
```

> Note: ECS will soon use a C#7 based compiler, using [ref returns](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/ref-returns) it makes the extra assignment unnecessary.

IComponentData structs may not contain references to managed objects. Since the all component data lives in simple non-garbage-collected tracked [chunk memory](https://en.wikipedia.org/wiki/Chunking_(computing)).

## EntityArchetype

An __EntityArchetype__ is a unique array of __ComponentType__. __EntityManager__ uses EntityArchetypes to group all Entities using the same ComponentTypes in chunks.

```
// Using typeof to create an EntityArchetype from a set of components
EntityArchetype archetype = EntityManager.CreateArchetype(typeof(MyComponentData), typeof(MySharedComponent));

// Same API but slightly more efficient
EntityArchetype archetype = EntityManager.CreateArchetype(ComponentType.Create<MyComponentData>(), ComponentType.Create<MySharedComponent>());

// Create an Entity from an EntityArchetype
var entity = EntityManager.CreateEntity(archetype);

// Implicitly create an EntityArchetype for convenience
var entity = EntityManager.CreateEntity(typeof(MyComponentData), typeof(MySharedComponent));

```

## EntityManager

The EntityManager owns __EntityData__, EntityArchetypes, __SharedComponentData__ and __ComponentGroup__.

EntityManager is where you find APIs to create Entities, check if an Entity is still alive, instantiate Entities and add or remove components.

```cs
// Create an Entity with no components on it
var entity = EntityManager.CreateEntity();

// Adding a component at runtime
EntityManager.AddComponent(entity, new MyComponentData());

// Get the ComponentData
MyComponentData myData = EntityManager.GetComponentData<MyComponentData>(entity);

// Set the ComponentData
EntityManager.SetComponentData(entity, myData);

// Removing a component at runtime
EntityManager.RemoveComponent<MyComponentData>(entity);

// Does the Entity exist and does it have the component?
bool has = EntityManager.HasComponent<MyComponentData>(entity);

// Is the Entity still alive?
bool has = EntityManager.Exists(entity);

// Instantiate the Entity
var instance = EntityManager.Instantiate(entity);

// Destroy the created instance
EntityManager.DestroyEntity(instance);
```

```cs
// EntityManager also provides batch APIs
// to create and destroy many Entities in one call. 
// They are significantly faster 
// and should be used where ever possible
// for performance reasons.

// Instantiate 500 Entities and write the resulting Entity IDs to the instances array
var instances = new NativeArray<Entity>(500, Allocator.Temp);
EntityManager.Instantiate(entity, instances);

// Destroy all 500 entities
EntityManager.DestroyEntity(instances);
```

## Chunk - implementation detail

The ComponentData for each Entity is stored in what we internally refer to as a chunk. ComponentData is laid out by stream. Meaning all components of type A, are tightly packed in an array. Followed by all components of type B etc.

A chunk is always linked to a specific EntityArchetype. Thus all Entities in one chunk follow the exact same memory layout. When iterating over components, memory access of components within a chunk is always completely linear, with no waste loaded into cache lines. This is a hard guarantee.

__ComponentDataArray__ is essentially an iterator over all EntityArchetypes compatible with the set of required components; for each EntityArchetype iterating over all chunks compatible with it and for each chunk iterating over all Entities in that chunk.

Once all Entities of a chunk have been visited, we find the next matching chunk and iterate through those Entities.

When Entities are destroyed, we move up other Entities into its place and then update the Entity table accordingly. This is required to make a hard guarantee on linear iteration of Entities. The code moving the component data into memory is highly optimized.

## World

A __World__ owns both an EntityManager and a set of __ComponentSystems__. You can create as many Worlds as you like. Commonly you would create a simulation World and rendering or presentation World.

By default we create a single World when entering Play Mode and populate it with all available ComponentSystems in the project, but you can disable the default World creation and replace it with your own code via a global define.

* [Default World creation code](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.ecs/Unity.ECS/Injection/DefaultWorldInitialization.cs)
* [Automatic bootstrap entry point](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.ecs/Unity.ECS/Injection/AutomaticWorldBootstrap.cs) 

> Note: We are currently working on multiplayer demos, that will show how to work in a setup with separate simulation & presentation Worlds. This is a work in progress, so right now have no clear guidelines and are likely missing features in ECS to enable it. 

## Automatic job dependency management (JobComponentSystem)

Managing dependencies is hard. This is why in __JobComponentSystem__ we are doing it automatically for you. The rules are simple: jobs from different systems can read from IComponentData of the same type in parallel. If one of the jobs is writing to the data then they can't run in parallel and will be scheduled with a dependency between the jobs.

```cs
public class RotationSpeedSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct RotationSpeedRotation : IJobProcessComponentData<Rotation, RotationSpeed>
    {
        public float dt;

        public void Execute(ref Rotation rotation, [ReadOnly]ref RotationSpeed speed)
        {
            rotation.value = math.mul(math.normalize(rotation.value), math.axisAngle(math.up(), speed.speed * dt));
        }
    }

    // Any previously scheduled jobs reading/writing from Rotation or writing to RotationSpeed 
    // will automatically be included in the inputDeps dependency.
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var job = new RotationSpeedRotation() { dt = Time.deltaTime };
        return job.Schedule(this, 64, inputDeps);
    } 
}
```

### How does this work?

All jobs and thus systems declare what ComponentTypes they read or write to. As a result when a JobComponentSystem returns a __JobHandle__ it is automatically registered with the EntityManager and all the types including the information about if it is reading or writing.

Thus if a system writes to component A, and another system later on reads from component A, then the JobComponentSystem looks through the list of types it is reading from and thus passes you a dependency against the job from the first system.

So JobComponentSystem simply chains jobs as dependencies where needed and thus causes no stalls on the main thread. But what happens if a non-job ComponentSystem accesses the same data? Because all access is declared, the ComponentSystem automatically __Completes__ all jobs running against ComponentTypes that the system uses before invoking __OnUpdate__.

#### Dependency management is conservative & deterministic

Dependency management is conservative. ComponentSystem simply tracks all ComponentGroups ever used and stores which types are being written or read based on that. (So if you inject ComponentDataArrays or use __IJobProcessComponentData__ once but skip using it sometimes, then we will create dependencies against all ComponentGroups that have ever been used by that ComponentSystem.)

Also when scheduling multiple jobs in a single system, dependencies must be passed to all jobs even though different jobs may need less dependencies. If that proves to be a performance issue the best solution is to split a system into two.

The dependency management approach is conservative. It allows for deterministic and correct behaviour while providing a very simple API.

### Sync points

All structural changes have hard sync points. __CreateEntity__, __Instantiate__, __Destroy__, __AddComponent__, __RemoveComponent__, __SetSharedComponentData__ all have a hard sync point. Meaning all jobs scheduled through JobComponentSystem will be completed before creating the Entity, for example. This happens automatically. So for instance: calling __EntityManager.CreateEntity__ in the middle of the frame might result in a large stall waiting for all previously scheduled jobs in the World to complete.

See [EntityCommandBuffer](#entitycommandbuffer) for more on avoiding sync points when creating Entities during game play.

### Multiple Worlds

Every World has its own EntityManager and thus a seperate set of Job handle dependency management. A hard sync point in one world will not affect the other world. As a result for streaming and proc-gen use cases it is useful to create entities in one World and then move them to another world in one transaction at the beginning of the frame. 

See [ExclusiveEntityTransaction](#exclusiveentitytransaction) for more on avoiding sync points for Proc-gen & Streaming use cases.


## Shared ComponentData
IComponentData is appropriate for data that varies between Entities, such as storing a world position. __ISharedComponentData__ is useful when many Entities have something in common, for example in the boid demo we instantiate many Entities from the same Prefab and thus the __InstanceRenderer__ between many boid Entities is exactly the same. 

```cs
[System.Serializable]
public struct InstanceRenderer : ISharedComponentData
{
    public Mesh                 mesh;
    public Material             material;

    public ShadowCastingMode    castShadows;
    public bool                 receiveShadows;
}
```

In the boid demo we never change the InstanceRenderer component, but we do move all the Entities __TransformMatrix__ every frame.

The great thing about ISharedComponentData is that there is literally zero memory cost on a per Entity basis.

We use ISharedComponentData to group all entities using the same InstanceRenderer data together and then efficiently extract all matrices for rendering. The resulting code is simple & efficient because the data is laid out exactly as it is accessed.

* [InstanceRendererSystem.cs](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/Assets/ECS/InstanceRenderer/InstanceRendererSystem.cs)

**Some important notes about SharedComponentData:**

* Entities with the same SharedComponentData are grouped together in the same chunks. The index to the SharedComponentData is stored once per chunk, not per Entity. As a result SharedComponentData have zero memory overhead on a per Entity basis. 
* Using ComponentGroup we can iterate over all Entities with the same type.
* Additionally we can use __ComponentGroup.GetVariation()__ to iterate specifically over Entities that have a specific SharedComponentData value. Due to the data layout this iteration has low overhead.
* Using __EntityManager.GetAllUniqueSharedComponents__ we can retrieve all unique SharedComponentData that is added to any alive Entities.
* SharedComponentData are automatically [reference counted](https://en.wikipedia.org/wiki/Reference_counting).
* SharedComponentData should change rarely. Changing a SharedComponentData involves using [memcpy](https://msdn.microsoft.com/en-us/library/aa246468(v=vs.60).aspx) on the all ComponentData for that Entity into a different chunk.

## ComponentSystem

## JobComponentSystem

# Iterating entities

Iterating over all Entities that have a matching set of components, is at the center of the ECS architecture.

## ComponentDataArray

## ComponentArray

## EntityArray

## Injection

## ComponentGroup

ComponentGroup is the class on top of which all iteration logic is based.

Essentially a ComponentGroup is constructed with a set of required components, subtractive components, or specific SharedComponentData values. 

The ComponentGroup has APIs to extract individual arrays. All these arrays are guaranteed to be in sync (same length, index of each array refers to the same Entity).

We do not recommend to use ComponentGroup directly. The injection pattern is a simpler way of doing the same thing.

## ComponentDataFromEntity

The Entity struct identifies an Entity. If you need to access component data on another Entity, the only stable way of referencing that component data is via the Entity ID. EntityManager provides a simple get & set component data API for it.
```cs
Entity myEntity = ...;
var position = EntityManager.SetComponentData<LocalPosition>(entity);
```

However EntityManager can't be used on a C# job. __ComponentDataFromEntity__ gives you a simple API that can also be safely used in a job.

```cs
// ComponentDataFromEntity can be automatically injected
[Inject]
ComponentDataFromEntity<LocalPosition> m_LocalPositions;

Entity myEntity = ...;
var position = m_LocalPositions[myEntity];
```

## ExclusiveEntityTransaction

EntityTransaction is an API to create & destroy entities from a job. The purpose is to enable procedural generation scenarios where construction of massive scale instantiation must happen on jobs. This API is very much a work in progress.

## EntityCommandBuffer


## GameObjectEntity

ECS ships with the __GameObjectEntity__ component. It is a MonoBehaviour. In __OnEnable__, the GameObjectEntity component creates an Entity with all components on the GameObject. As a result the full GameObject and all its components are now iterable by ComponentSystems.

TODO: what do you mean by "the GameObjectEntity component creates an Entity with all components on the GameObject" in the sentence above? do you mean all required components that this GameObject should have in that particular case? or is there a pre-defined set of components that will always be added? It's a little unclear.

> Note: for the time being, you must add a GameObjectEntity component on each GameObject that you want to be visible / iterable from the ComponentSystem.
