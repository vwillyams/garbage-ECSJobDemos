# Getting Started 

**What are we trying to solve?**
When making games with GameObject/MonoBehaviour, it is easy to write code that ends up being difficult to read, maintain and optimize. This is the result of a combination of factors: [object-oriented model](https://en.wikipedia.org/wiki/Object-oriented_programming), non-optimal machine code compiled from Mono, [garbage collection](https://en.wikipedia.org/wiki/Garbage_collection_(computer_science)) and single threaded code.

**ECS to the rescue**
ECS is a way of writing code that focuses on the actual problems you are solving: the data and behavior that make up your game.

In addition to being a better way of approaching game programming for design reasons, using ECS puts you in an ideal position to leverage Unity's job system and burst compiler, letting you take full advantage of today's multi-core processors.

We have exposed the Native Unity Job system so that users can gain the benefits of multithreaded batch processing from within their ECS C# scripts. The Native Job system has built in safety features for detecting [race conditions](https://en.wikipedia.org/wiki/Race_condition).

However we need to introduce a new way of thinking and coding to take full advantage of the Job System.

# What is ECS?

## MonoBehavior - A dear old friend

MonoBehaviours contain both the data and the behaviour. This component will simply rotate the transform component every frame.

```C#
class Rotator : MonoBehaviour
{
    // The data - editable in the inspector
    public float speed;
    
    // The behaviour - Reads the speed value from this component 
    // and changes the rotation of the transform component.
    void Update()
    {
        transform.rotation *= Quaternion.AxisAngle(Time.deltaTime * speed, Vector3.up);
    }
}
```

However MonoBehaviour inherts from a number of other classes; each containing their own set of data - none of which are in use by the script above. Therefore we have just wasted a bunch of memory for no good reason. So we need to think about what data we really need to optimse the code. 

## Component System - A step into a new era

In the new model the Component only contains the data.

The ComponentSystem has the behaviour. One ComponentSystem is responsible for updating all GameObjects with the matching set of components.

```C#
class Rotator : MonoBehaviour
{
    // The data - editable in the inspector
    public float Speed;
}

class RotatorSystem : ComponentSystem
{
    struct Group
    {
        Transform Transform;
        Rotator   Rotator;
    }
    
    override protected OnUpdate()
    {
        // We can immediately see a first optimization.
        // We know delta time is the same between all rotators,
        // so we can simply keep it in a local variable 
        // to get better performance.
        float deltaTime = Time.deltaTime;
        
        // ComponentSystem.GetEntities<Group> 
        // lets us efficiently iterate over all game objects
        // that have both a Transform & Rotator component 
        // (as defined above in Group struct).
        foreach (var e in GetEntities<Group>())
        {
            e.Transform.rotation *= Quaternion.AxisAngle(e.Rotator.Speed * deltaTime, Vector3.up);
        }
    }
}
```

# Hybrid ECS: Using ComponentSystem to work with existing GameObject & Components

There is a lot of existing code based on MonoBehaviour, GameObject and friends. We want to make it easy to work with existing GameObjects and existing components. But make it easy to transition one piece at a time to the ComponentSystem style approach.

In the example above you can see that we simply iterate over all components that contain both Rotator and Transform components.

**How does ECS know about Rotator and Transform?**
In order to iterate over components like in the Rotator example, those entities have to be known to the EntityManager.

ECS ships with the GameObjectEntity component. On OnEnable, the GameObjectEntity component creates an entity with all components on the game object. As a result the full game object and all its components are now iteratable by ComponentSystems.

*Thus for the time being you must add a GameObjectEntity component on each game object that you want to be visible / iteratable from the ComponentSystem.*

**What does this mean for my game?**
It means that you can one by one, convert behaviour from MonoBehaviour.Update methods into ComponentSystems. You can in fact keep all your data in a MonoBehaviour, and this is in fact a very simple way of starting the transition to ECS style code.

So your scene data remains in game objects & components. You continue to use GameObject.Instantiate to create instances etc.

You simply move the contents of your MonoBehaviour.Update into a ComponentSystem.OnUpdate method. The data is kept in the same MonoBehaviour or other components.

**What you get:**
+ Seperation of data & behaviour resulting in cleaner code
+ Systems operate on many objects in batch, avoiding per object virtual calls. It is easy to apply optimizations in batch. (See deltaTime optimization above)
+ You can continue to use existing inspectors, editor tools etc

**What you don't get:**
- Instantiation time will not improve
- Load time will not improve
- Data is accessed randomly, no linear memory access gurantees
- No multicore
- No SIMD


So using ComponentSystem, GameObject and MonoBehaviour is a great first step to writing ECS code. And it gives you some quick performance benefits. But it also doesn't give you the full performance benefits.

# Pure ECS: Full-on performance: IComponentData & Jobs

One motivation to use ECS is because you want your game to have optimal performance. (By optimal performance we mean that if you were to hand write all of your code using SIMD intrinsics, custom data layouts for each loop, then you would end up with the similar performance as what you get when writing simple ECS code.)

The C# Job System TODO LINK does not support managed class types only structs and Native Containers. Thus only IComponentData can be safely accessed in a C# Job.

The EntityManager makes hard guarantees about linear memory layout of the component data. This is a very important part of the great performance you can achieve with C# jobs using IComponentData.


ECS provides a wide variety of ways of iterating over the relevant entities and components. [Here is all of them](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/Assets/ECS/BoidsECS/BoidToInstanceRendererTransform.cs)

From that we have selected a simple foreach style simple iteration example and a optimal jobified code example.

In the boid simulation we represent the boids simulation state as (BoidData). The renderer however needs a matrix to render the instances, the sample code shows how you can express this data transformation using ECS.

```cs
// BoidData is the actual per fish simulation state.
public struct BoidData : IComponentData
{
    public float3  position;
    public float3  forward;
}

// This component data is provided by the instance renderer. The instance renderer expects the matrix to be up to date when it begins rendering.
public struct TransformMatrix : IComponentData
{
    public float4x4 matrix;
}
```


```cs
// Uses GetEntities<Group> to foreach iterate over all entities
// and produce a matrix from the BoidData state. Running all on the main thread.
[DisableAutoCreation]
class BoidToInstanceRendererTransform_GetEntities : ComponentSystem
{
    unsafe struct Group
    {
        [ReadOnly] 
        public BoidData*        Boid;
        public TransformMatrix* Transform;
    }

    unsafe protected override void OnUpdate()
    {
        // GetEntities<Group> lets us iterates over all entities
        // that have both a BoidData and TransformMatrix components attached.
        foreach (var e in GetEntities<Group>())
        {
            var boid = e.Boid;
            e.Transform->matrix = matrix_math_util.LookRotationToMatrix(boid->position, boid->forward, new float3(0, 1, 0));
        }
    }
}
```

```cs
// Using IJobProcessComponentData to iterate of all entities matching the required component types.
// Processing of entities happens in parallel. The Main thread only schedules jobs.
[DisableAutoCreation]
class BoidToInstanceRendererTransform_IJobProcessComponentData : JobComponentSystem
{
    struct Group
    {
        [ReadOnly]
        public ComponentDataArray<BoidData>        Boids;
        public ComponentDataArray<TransformMatrix> RendererTransforms;
    }

    // [Inject] creates a ComponentGroup, setting up the two ComponentDataArrays so
    // that we can iterate over all entities containing both BoidData & TransformMatrix.    
    [Inject] 
    Group m_Group;

    // Instead of IJobParallelFor, we use IJobProcessComponentData
    // It is more efficient than IJobParallelFor and more convenient
    // * ComponentDataArray has one early out branch per index lookup
    // * IJobProcessComponentData innerloop does a straight BoidData* array iteration, with zero checks. 
    [ComputeJobOptimization]
    struct TransformJob : IJobProcessComponentData<BoidData, TransformMatrix>
    {
        public void Execute(ref BoidData boid, ref TransformMatrix transformMatrix)
        {
            transformMatrix.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
        }
    }

    // We derive from JobComponentSystem, as a result ECS hands us the required dependencies for our jobs.
    //
    // This is possible because we declare using the Group struct, which components we read & write from.
    // Also declares what data is being read & written to in this ComponentSystem by declaring it in the Group struct.
    // Because it is declared the JobComponentSystem can give us a Job dependency, which contains all previously scheduled
    // jobs that write to any BoidData or RendererTransforms.
    // We also have to return the dependency so any job we schedule will get registered against the types for the next System that might run
    // This approach means:
    // * No waiting on main thread, just scheduling jobs with dependencies (Jobs only start when dependencies have completed)
    // * Dependencies are figured out automatically for us, so we can write modular multithreaded code
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var job = new TransformJob();
        return job.Schedule(m_Group.Boids, m_Group.RendererTransforms, 16, inputDeps);
    }
}
```