# ECS Features in Detail

## Entity
Entity is an ID. You can think of it as a super lightweight GameObject that doesn't even have a name by default.

You can add and remove components from Entities at runtime. Entity ID's are stable. In fact they are the only stable way to store a reference to another component or entity.

## IComponentData
Traditional Unity Components (including MonoBehaviour) are object-oriented classes which contain data and methods for behaviour. IComponentData is a pure ECS-style Component, meaning that it defines no behaviour, only data. IComponentDatas are structs rather than classes, meaning that they are copied by value instead of by reference by default. You will usually need to use the following pattern to modify data:

```C#
var transform = group.transform[index]; // Read

transform.heading = playerInput.move; // Modify
transform.position += deltaTime * playerInput.move * settings.playerMoveSpeed;

group.transform[index] = transform; // Write
```

*NOTE: ECS will soon use a C#7 based compiler, using ref returns it makes the extra assignment unncessary.*

IComponentData structs may not contain references to managed objects. Since the all component data lives in simple non-GC tracked chunk memory.



## EntityArchetype
An archetype is a unique array of ComponentType. EntityManager uses Archetypes to group all entities using the same component types in chunks.

```
// Using typeof to create an archetype from a set of components
EntityArchetype archetype = EntityManager.CreateArchetype(typeof(MyComponentData), typeof(MySharedComponent));

// Same API but slightly more efficient
EntityArchetype archetype = EntityManager.CreateArchetype(ComponentType.Create<MyComponentData>(), ComponentType.Create<MySharedComponent>());

// Create an entity from an archetype
var entity = EntityManager.CreateEntity(archetype);

// Implicitly create an archetype for convenience
var entity = EntityManager.CreateEntity(typeof(MyComponentData), typeof(MySharedComponent));

```

## EntityManager

The EntityManager owns all EntityData, EntityArchetypes, SharedComponentData and ComponentGroup.

EntityManager is where you find API's to create entities, check if an entity is still alive, instantiate entities and add or remove components.


```cs
// Create an entity with no components on it
var entity = EntityManager.CreateEntity();

// Adding a component at runtime
EntityManager.AddComponent(entity, new MyComponentData());

// Get the component data
MyComponentData myData = EntityManager.GetComponentData<MyComponentData>(entity);

// Set the componentData
EntityManager.SetComponentData(entity, myData);

// Removing a component at runtime
EntityManager.RemoveComponent<MyComponentData>(entity);

// Does the entity exist and does it have the component?
bool has = EntityManager.HasComponent<MyComponentData>(entity);

// Is the entity still alive?
bool has = EntityManager.Exists(entity);

// Instantiate the entity
var instance = EntityManager.Instantiate(entity);

// Destroy the created instance
EntityManager.DestroyEntity(instance);
```

```cs
// EntityManager also provides batch API's
// to create and destroy many entities in one call. 
// They are significantly faster 
// and should be used where ever possible
// for performance reasons.

// Instantiate 500 entities and write the resulting entity ids to the instances array
var instances = new NativeArray<Entity>(500, Allocator.Temp);
EntityManager.Instantiate(entity, instances);

// Destroy all 500 entities
EntityManager.DestroyEntity(instances);
```

## Chunk - Implementation detail

The component data for each entity is stored in what we internally refer to as a Chunk. Component data is laid out by stream. Meaning all components of type A, are tightly packed in an array. Followed by all components of Type B etc.

A chunk is always linked to a specfic archetype. Thus all entities in one chunk follow the exact same memory layout. When iterating over components, Memory access of components within a chunk is always completely linear,  with no waste loaded into cache lines. This is a hard gurantee.


ComponentDataArray is essentially an iterator over all archetypes compatible with the set of required components. For each archetype, iterating over all chunks compatible with it. And for each chunk iterating over all entities in that chunk.

Once all entities of a chunk have been visited, we find the next matching chunk and iterate through those entities.


When entities are destroyed, we move another entities in its place and update the Entitytable accordingly. This is required to make a hard gurantee on linear iteration of entities. The code moving the component data in memory is highly optimized.

## World

A World owns both an EntityManager and a set of ComponentSystems. You can create as many worlds as you like. Commonly you would create a simulation world and rendering or presentation world.

By default we create a single world when entering playmode and populate it with all available ComponentSystems in the project, but you can disable the default world creation and replace it with your own code via a global define.

[Default world creation code](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.ecs/Unity.ECS/Injection/DefaultWorldInitialization.cs)
[Automatic bootstrap entry point](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.ecs/Unity.ECS/Injection/AutomaticWorldBootstrap.cs) 

*We are currently working on multiplayer demos, that will show how to work in a setup with seperate simulation & presentation worlds. This is WIP, so right now have no clear guidelines and likely are also missing features in ECS to enable it.* 

## Automatic Job dependency Management (JobComponentSystem)

Managing dependencies is hard. This is why in JobComponentSystem we are doing it automatically for you. The rules are simple. Jobs from different systems can read from IComponentData of the same type in parallel. If one of the jobs is writing to the data then they can't run in parallel and will be scheduled with a dependency between the jobs.

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
All jobs and thus systems declare what component types they read or write to. As a result when a JobComponentSystem returns a JobHandle it is automatically registered with the EntityManager and all the types including the information about if it is reading / writing.

Thus if a system writes to Component A, and another System later on reads from Component A, then the JobComponentSystem looks through the list of types it is reading from and thus passes you a dependency against the job from the first System.

So JobComponentSystem simply chains jobs as dependencies where needed and thus causes no stalls on the main thread. But what happens if non-job ComponentSystem accesses the same data? Because all access is declared, the ComponentSystem automatically Completes all jobs running against component types the system uses before invoking OnUpdate.

#### Dependency management is conservative & deterministic
Dependency managment is conservative. ComponentSystem simply tracks all ComponentGroups ever used and stores which types are being written / read based on that. (So if you inject ComponentDataArrays or use IJobProcessComponentData once but skip using it sometimes, then we will create dependencies against all ComponentGroups that have ever been used by that ComponentSystem.)

Also when scheduling multiple jobs in a single system, dependencies must be passed to all jobs even though different jobs may need less dependencies. If that proves to be a performance issue the best solution is to split a system into two.

The dependency management approach is conservative. It allows for deterministic and correct behaviour while providing a very simple API.

### Sync Points
All structural changes have hard sync points. CreateEntity, Instantiate, Destroy, AddComponent, RemoveComponent, SetSharedComponentData all have a hard sync point. Meaning all jobs scheduled through JobComponentSystem will be completed before Creating the entity for example. This happens automatically. So for example calling EntityManager.CreateEntity in the middle of the frame might result in a large stall waiting for all previously scheduled jobs in the World to complete.

See [EntityCommandBuffer](#entitycommandbuffer) for more on avoiding sync points when creating entities during gameplay.

### Multiple worlds
Every World has its own EntityManager and thus a seperate set of Job handle dependency management. A hard sync point in one world will not affect the other world. As a result for streaming and proc-gen use cases it is useful to create entities in one World and then move them to another world in one transaction at the beginning of the frame. 

See [ExclusiveEntityTransaction](#exclusiveentitytransaction) for more on avoiding sync points for Proc-gen & Streaming use cases.


## Shared component data
IComponentData is appropriate for data that varies from entity to entity, such as storing a world position. ISharedComponentData is useful when many entities have something in common, for example in the boid demo we instantiate many entities from the same prefab and thus the InstanceRenderer between many boid entities is exactly the same. 

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
In the boid demo we never change the InstanceRenderer component, but we do move all the entities TransformMatrix every frame.

The great thing about ISharedComponentData is that there is literally zero memory cost on a per entity basis for ISharedComponentData.

We use ISharedComponentData to group all entities using the same InstanceRenderer data together and then efficiently extract all matrices for rendering. The resulting code is simple & efficient because the data is laid out exactly as it is accessed.
[InstanceRendererSystem.cs](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/Assets/ECS/InstanceRenderer/InstanceRendererSystem.cs)

**Some important notes about SharedComponentData:**

* Entities with the same SharedComponentData are grouped together in the same chunks. The index to the shared data is stored once per chunk, not per entity. As a result SharedComponentData have zero memory overhead on a per entity basis. 
* Using ComponentGroup we can iterate over all entities with the same type.
* Additionally we can use ComponentGroup.GetVariation() to iterate specifically over entities that have a specific SharedComponentData value. Due to the data layout this iteration has very low overhead.
* Using EntityManager.GetAllUniqueSharedComponents we can retrieve all unique shared component data that are added to any alive entities.
* SharedComponentData are automatically refcounted
* SharedComponentData should change rarely. Changing a shared component data involes memcpy'ing the all component data for that entity into a different chunk)

## ComponentSystem

## JobComponentSystem

# Iterating entities

Iterating over all entities that have a matching set of components, is at the center of the ECS architecture.

## ComponentDataArray
## ComponentArray
## EntityArray

## Injection


## ComponentGroup

ComponentGroup is the class on top of which all iteration logic is based.

Essentially a ComponentGroup is constructed with a set of required components, subtractive components or specific shared component data values. 

The ComponentGroup has API's to extract individual arrays. All these arrays are guranteed to be in sync (Same length, index of each array refers to the same entity)

We do not recommend to use ComponentGroup directly. The injection pattern is a simpler way of doing the same thing.


## ComponentDataFromEntity
The Entity struct identifies an entity. If you need to access component data on another entity, the only stable way of referencing that component data is via the Entity ID. EntityManager provides a simple Get & Set component data API for it.
```cs
Entity myEntity = ...;
var position = EntityManager.SetComponentData<LocalPosition>(entity);
```

However EntityManager can't be used on a C# job, ComponentDataFromEntity gives you a simple API that can also be safely used in a job.

```cs
// ComponentDataFromEntity can be automatically injected
[Inject]
ComponentDataFromEntity<LocalPosition> m_LocalPositions;

Entity myEntity = ...;
var position = m_LocalPositions[myEntity];
```

## ExclusiveEntityTransaction


EntityTransaction is an API to create & destroy entities from a job. The purpose is to enable procedural generation use cases where construction of massive scale instantiation must happen on jobs. This API is very much work in progress

## EntityCommandBuffer


## GameObjectEntity
ECS ships with GameObjectEntity component. It is a MonoBehaviour. In OnEnable, the GameObjectEntity component creates an entity with all components on the game object. As a result the full game object and all its components are now iteratable by ComponentSystems.

*Thus for the time being you must add a GameObjectEntity component on each game object that you want to be visible / iteratable from the ComponentSystem.*
