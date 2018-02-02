# tutorial_3

# Two-Stick Shooter ECS Tutorial

In this series of posts, we're going to make a simple game using the Unity ECS and jobs as much as possible. The game type we picked for this was a simple two-stick shooter, something everyone can imagine building in a traditional way pretty easily.

Post 1 based on commit `82dc183d4e84baea4ced6256ee58181ce9fa8711`

## Post 1: The genesis
 
### Step 1: Scene Setup

We're going to spawn everything in ECS ourselves. Therefore the scene we need for this tutorial is almost empty. The only things we use the scene for are:

* A camera
* A light source
* Template objects that hold parameters we'll use to spawn ECS entities

### Step 2: Bootstrapping

How do you bootstrap your game when using ECS? After all, you need something to insert those initial entities in the system before anything can update.

One simple answer is to just run some code when the project starts playing. In the project, there's a class `TwoStickMain` which comes with two methods. The first one initializes early, and creates the core `EntityManager` we're going to use to interact with ECS.

TODO - insert a screenshot or some code of TwoStickMain

It also creates _archetypes_ which you can think of as blueprints for what components will be attached to an entity later on when it is instantiated. This step is optional, but avoids reallocating memory and moving objects later when they are spawned, because they will be created with the correct memory layout right away. 

For our player entity, I've chosen these components:

* `WorldPos` - The location/heading of the player in the world
* `PlayerInput` - Captures player [input](https://docs.unity3d.com/ScriptReference/Input.html)
* `TransformMatrix` - Required as a storage endpoint for [4x4 matrices](https://docs.unity3d.com/ScriptReference/Matrix4x4.html) read by the instance rendering system that works with ECS

You can think of the ECS player-controlled entity as a combination of those three components.

```c#
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
public static void Initialize()
{
    var entityManager = World.Active.GetOrCreateManager<EntityManager>();

    PlayerArchetype = entityManager.CreateArchetype(typeof(WorldPos), typeof(PlayerInput), typeof(TransformMatrix));
}
```

The next initialization method runs after the scene has loaded, because it needs to access a blueprint object from the scene:

```c#
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
public static void InitializeWithScene()
{
    var entityManager = World.Active.GetOrCreateManager<EntityManager>();

    // Create the player's position
    Entity player = entityManager.CreateEntity(PlayerArchetype);
    WorldPos initialPos;
    initialPos.Position = new float2(0, 0);
    initialPos.Heading = new float2(0, 1);

    entityManager.SetComponent(player, initialPos);

    // Fill in the player's render data from blueprint
    var playerRenderPrototype = GameObject.Find("PlayerRenderPrototype");
    var wrapper = playerRenderPrototype.GetComponent<InstanceRendererComponent>();
    entityManager.AddSharedComponent(player, wrapper.Value);
    // Discard blueprint
    Object.Destroy(playerRenderPrototype);
}
```

### Step 3: Systems

We need a few data transformations to happen to render a frame. 

* We need to fill in the `TransformMatrix` output for anything that needs to be rendered. You can find this code in `TwoStickRenderer.cs`.
* We need to update the player's WorldPos based on input. You can find this code in `PlayerMoveSystem.cs`.
* We need to compute player input based on controller input. You can find this code in `PlayerInputSystem.cs`.

## Post 2: Shooting

(Based on commit `954d2aa7157f4c16aeb274ec69b715b724704dfb`)

We need our hero to shoot bullets against the hordes of enemies we'll be adding next.

### Accessing Configuration Data

First, let's clean up interfacing the parameters of things like player movement, bullet time-to-live and similar things into the Unity editor where we can tweak them.

Pure ECS data isn't supported in a great way in the editor yet, so we'll take two approaches in the interim to configure our game:

1. For things like asset references, we'll create a couple of _prototype_ game objects in the scene, where we can add wrapped `IComponentData` types. This is the approach we've taken to customize the appearance of the hero and the shots. Once we've fished out the configuration, we can discard these prototype objects.

2. For "bags of settings", it's convenient to retain a traditional Unity component on an empty game object, because that allows you to tweak the values in play mode. The example project uses a component `TwoStickExampleSettings` for this purpose which we put on an empty game object called `Settings`. This allows us to fetch the component and keep it around globally in our app as well as to receive updates when values are tweaked.

### Spawning Shots

In this design, when the player shoots, it doesn't directly create a shot entity. The reason is that we wanted to create all the shots centrally, all at once. Therefore, when the player fires we create a new entity and attach a `ShotSpawnData` on it. This records where the shot should appear, it's life time and a few other parameters, but nothing _happens_ right away. You can think of this pattern as a type of event.

Next, the `ShotSpawnSystem` picks up any shot spawn requests and repurposes the entity. It removes the `ShotSpawnData` and adds the components that are required to make the entity a simulating shot, well as the instance rendering information.

Shots all move in batch via the `ShotMoveSystem`. This simply does linear movement of the shots.

Finally, the `ShotDestroySystem` is responsible for decrementing the time-to-live on shots and destroys the shots that have expired.

## Post 3: Enemies!

(Based on commit `43d97bc6c8c4f95f3e24beffdb1486ede044a8a6`)

We need something for our hero to shoot at, so next we'll be adding some enemies.

First let's define what data an enemy needs to carry around. For this simple example, all we really need to keep track of is the health of the enemy. The combination of health, a 2D transform and a 3D matrix for rendering is enough to start moving some enemies across the screen. We'll need more data later to make them shoot and move in formation, but this will do for now.

There are three systems that deal with enemies:

* `EnemySpawnSystem` will spawn new enemies in random locations
* `EnemyMoveSystem` simply moves enemies across the screen and flags them for destruction when they go too far out of bounds
* `EnemyDestroySystem` will (unsurprisingly) destroy enemies that have a zero or negative health

In this ECS design, when we interact with the `EntityManager` to create or destroy entities, we're using a main thread `ComponentSystem` class. The movement code however can run in jobs as it doesn't interface with the entity manager, and therefore it can be a `JobComponentSystem`.

### Spawning, ECS style

Of the three systems, the most interesting one is the spawning system. It needs to keep track of when to spawn an enemy, but we don't want to put that state in the system itself. One of the design principles of ECS is that it shouldn't prevent you from recording component state and playing it back later to reconstruct a scene. So storing a bunch of state variables that carry meaning from frame to frame will break that contract.

The spawning system instead stores it state in a singleton component, attached to a singleton entity. We create the entity and the initial values for this component in `OnCreateManager`. Here we also initialize a random seed and store that along with the rest of the data, so that games will predictably spawn enemies in the same pattern every time, regardless of frame rate or if something fancy like state replay is happening.

Due to `ref return` not being implemented yet, we have to take a copy of our system's state from the singular `State` array, and then when we've modified it for next frame, we store it back in to the component.

A further quirk is that we have to put off actually spawning an entity until we've completed the above step of storing back our component state, because touching the entity manager will immediately invalidate all injected arrays (including the one where our state is kept!)

This may look like a lot of boilerplate (and it is) but it's also kind interesting to think about this in a different way. What if we renamed `State` to `Wave` and update more than one of them at a time, orchestrated by some other system? We would get multiple simultaneous waves spawning and updating in concert. ECS makes these sort of transformations much easier and cleaner than if we had used global data attached to the system.

## Post 4: Shooting and Damage

(Based on commit `31cb6bdd133f01fd9185d851406c720aa78c22e1`)

To get some bullets flying from enemies, we add a system that deals with a cooldown time for each enemy, and creates `ShotSpawnData` when it needs to shoot. We use the player position (of the first player!) to aim. It's worth calling out that multiplayer concerns are front and center in an ECS style: we always have an array of players.

That wasn't too bad - but now we need to start making sense of the gameplay of shooting bullets around. To track that, we'll introduce the contept of a `Health` component, which is simply a number. We define that entities with a zero health component should be deleted, and repurpose the system that destroyed off screen enemies to just remove anything that has a zero health. This will include players too!

The other concept we need is that of a `Faction`, which is just an integer of 0 (meaning "players") or 1 (meaning "enemies"). This is because we don't want shots spawned from the player to hurt the player, and vice versa.

Now we're ready to put it all together and deal damage in the `DamageSystem`. This is a very simple, unoptimized affair that collides all shots against anything that can be damaged. It's worth while noting that logical concepts appear in the code, without having a concrete class they belong to:

* In `DamageSystem`, a _receiver_ is something that has `Health`, `Faction` and `Transform2D`
* In `DestroyDead` system, what we operate on is the concept of something having just a `Health` component.

The final piece of the flair for this post is to display the player's health on the screen, as the game ends when they are destroyed. For this, we have to reach outside the ECS and use a UI component.