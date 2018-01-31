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
An archetype is a unique array of ComponentType. EntityManager uses Archetypes to group all entities using the same Component types in chunks.

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

// Instantiate the entity
var instance = EntityManager.Instantiate(entity);

// Destroy the created instance
EntityManager.DestroyEntity(instance);
```

```cs
// EntityManager also provides batch API's
// to create and destroy many entities in one call. 
// They are significantly faster 
// and should always be used 
// if you want to create many instances in a loop

// Instantiate 500 entities and write the resulting entity ids to the instances array
var instances = new NativeArray<Entity>(500, Allocator.Temp);
EntityManager.Instantiate(entity, instances);

// Destroy all 500 entities
EntityManager.DestroyEntity(instances);
```

## Chunk - Implementation detail

The component data for each entity is stored in what we internally refer to as a chunk. Component data is laid out by stream. Meaning all components of type A, are tightly packed in an array. Followed by all components of Type B etc.

Thus iterating over entities using ComponentDataArray, we gurantee that within a chunk memory access is completely linear with no waste loaded into cache lines. Once all entities of a chunk have been iterated, we find the next matching chunk and iterate through those.

When entities are destroyed, we move another entities in its place and update the Entitytable accordingly.

## World

A World owns both an EntityManager and a set of ComponentSystems. You can create as many worlds as you like. Commonly you would create a simulation world and rendering or presentation world.

By default we create a single world when entering playmode and populate it with all available ComponentSystems in the project, but you can disable the default world creation and replace it with your own code via a global define.

[Default world creation code](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.ecs/Unity.ECS/Injection/DefaultWorldInitialization.cs)
[Automatic bootstrap entry point](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.ecs/Unity.ECS/Injection/AutomaticWorldBootstrap.cs) 

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

We use ISharedComponentData to group all entities using the same InstanceRenderer data together and then efficiently extract all matrices for rendering. The resulting code is simple & efficient due to optimal memory layout.
[InstanceRendererSystem.cs](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/Assets/ECS/InstanceRenderer/InstanceRendererSystem.cs)

**Some important notes about SharedComponentData:**

* Entities with the same SharedComponentData are grouped together in the same chunks. Thus the index to the shared data is stored once per chunk. As a result SharedComponentData have zero memory overhead on a per entity basis. 
* Using ComponentGroup we can iterate over all entities with the same type
* Additionally we can use ComponentGroup.GetVariation() to iterate specifically over entities that use a specific SharedComponentData. Due to the data layout this iteration has very low overhead.
* Using EntityManager.GetAllUniqueSharedComponents we can retrieve all unique shared component data that are added to any alive entities.
* SharedComponentData are automatically refcounted
* SharedComponentData should change rarely (Changing a shared component data requires memcpying the entire entities component data into a different chunk)

## ComponentSystem

## JobComponentSystem

## ComponentGroup

## ComponentDataArray

## EntityTransaction
