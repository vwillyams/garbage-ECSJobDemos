# ECS Concepts
If you are familiar with ECS concepts, you might see the potential for naming conflicts with Unity's existing GameObject/Component setup. Here are how ECS concepts map to Unity's implemenation:

### Entity → **Entity**
Unity didn't have an Entity, so the structure is simply named after the concept. Entities are like super lightweight GameObjects, in that they don't do much on their own, and they don't store any data (not even a name!).

You can add Components to entities, similar to how you can add components to game objects.

### Component → **ComponentData**
We are introducing a new high-performance component type. 

```
struct MyComponent: IComponentData
{} 
```

The EntityManager manages the memory and makes hard guarantees about linear memory access when iterating over a set of components. It also has zero overhead on a per entity basis beyond the size of the struct itself.

In order to differentiate it from the existing Component types (such as MonoBehaviours), the name refers directly to the fact that it only stores data. ComponentData can be added and removed from Entities.

### System → **ComponentSystem**
There are a lot of "Systems" in Unity, so the name includes the umbrella term, "Component" as well. ComponentSystems define your game's behaviour, and can operate on several types of data: traditional GameObjects and Components, or pure ECS ComponentData and Entity structs.
