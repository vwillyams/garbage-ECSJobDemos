# Two-Stick Shooter ECS Tutorial

In this series of posts, we're going to make a simple game using the Unity ECS and jobs as much as possible. The game type we picked for this was a simple two-stick shooter, something everyone can imagine building in a traditional way pretty easily.

## Scene Setup
 
For this tutorial, we're going to use the ECS as much as possible.
The scene we need for this tutorial is almost empty as there are very few
`GameObject`s involved. The only things we use the scene for are:

* A camera
* A light source
* Template objects that hold parameters we'll use to spawn ECS entities
* A couple of UI objects to start the game and display health

The tutorial project is located in `Assets/ECS/TwoStickShooterPure`.

## Bootstrapping

How do you bootstrap your game when using ECS? After all, you need something to insert
those initial entities into system before anything can update.

One simple answer is to just run some code when the project starts playing. In this project,
there's a class `TwoStickBootsrap` which comes with two methods. The first one initializes
early and creates the core `EntityManager` we're going to use to interact with ECS.

Overall, here's what the bootstrapping code achieves:

* It creates an _entity manager_, a key ECS abtraction we use to create and modify
  entities and their components

* It creates _archetypes_, which you can think of as blueprints for what components
  will be attached to an entity later on when it is created. This step is optional,
  but avoids reallocating memory and moving objects later when they are spawned, because
  they will be created with the correct memory layout right away. 

* It pulls out some prototypes and settings from the scene.

### Scene Data

Pure ECS data isn't supported in a great way in the editor yet, so we'll take two
approaches in the interim to configure our game:

1. For things like asset references, we'll create a couple of _prototype_ game objects
   in the scene, where we can add wrapped `IComponentData` types. This is the approach
   we've taken to customize the appearance of the hero and the shots. Once we've fished
   out the configuration, we can discard these prototype objects.
   
2. For "bags of settings", it's convenient to retain a traditional Unity component on an
   empty game object, because that allows you to tweak the values in play mode. The
   example project uses a component `TwoStickExampleSettings` for this purpose which we
   put on an empty game object called `Settings`. This allows us to fetch the component
   and keep it around globally in our app as well as to receive updates when values are
   tweaked.
  
### Archetypes

As this is a very small game, we can describe all the entity archetypes we need directly
in the bootstrap code. To make an archetype, you simply list all the component types that
you need to go on an instance of that archetype when it is created. 

Let's look at one archetype - `PlayerArchetype` - which is for creating, well, players:

```c#
PlayerArchetype = entityManager.CreateArchetype(
    typeof(Position2D), typeof(Heading2D), typeof(PlayerInput),
    typeof(Faction), typeof(Health), typeof(TransformMatrix));
```

The player archetype has the following component types:
  * `Position2D` and `Heading2D` - These stock ECS components allow the player's avator to be
     positioned and automatically rendered using built-in 2D->3D transformations.
  * `PlayerInput` is a component we fill in every frame based on player [input](https://docs.unity3d.com/ScriptReference/Input.html)
  * `Faction` describes the "team" the player is on. It'll come in useful later when we need
    to have shots just hit the opposing team.
  * `Health` simply contains a hit point counter
  * Finally, I've added another stock component `TransformMatrix` which is required as a
    storage endpoint for [4x4 matrices](https://docs.unity3d.com/ScriptReference/Matrix4x4.html)
    read by the instance rendering system that works with ECS.

You can think of the ECS player-controlled entity as a combination of these components.
  
The other archetypes are set up similarly.

The next initialization method runs after the scene has loaded, because it needs to access a blueprint object from the scene:

### Extracting configuration from the scene

Once the scene has been loaded, our `InitializeWithScene` method is going to be called. Here,
we pull out a few objects from the scene, including a `Settings` object we can use to tweak
the ECS code while it's running.

### Starting a new game

To start a game, we have to put a player entity in the world. This is accomplished with this code:

```c#
public static void NewGame()
{
    // Access the ECS entity manager
    var entityManager = World.Active.GetOrCreateManager<EntityManager>();
    
    // Create an entity based on the player archetype. It will get
    // default values for all the component types we listed.
    Entity player = entityManager.CreateEntity(PlayerArchetype);
    
    // We can tweak a few components to make more sense like this.
    entityManager.SetComponentData(player, new Position2D {Value = new float2(0.0f, 0.0f)});
    entityManager.SetComponentData(player, new Heading2D  {Value = new float2(0.0f, 1.0f)});
    entityManager.SetComponentData(player, new Faction { Value = Faction.kPlayer });
    entityManager.SetComponentData(player, new Health { Value = Settings.playerInitialHealth });
    
    // Finally we add a shared component which dictates the rendered look
    entityManager.AddSharedComponentData(player, PlayerLook);
}
```

## Systems!

We need a few data transformations to happen to render a frame. 

* We sample player input (`PlayerInputSystem`)
* We move the player and allow them to shoot (`PlayerMoveSystem`)
* We need to sometimes spawn new enemies (`EnemySpawnSystem`)
* The enemies need to move (`EnemyMoveSystem`)
* The enemies need to shoot (`EnemyShootSystem`)
* We need to spawn new shots based on player or enemy action (`ShotSpawnSystem`)
* We need a way to clean up old shots when they time out (`ShotDestroySystem`)
* We need to deal damage from shots (`ShotDamageSystem`)
* We need to cull any entities that no health left (`RemoveDeadSystem`)
* We need to push some data to the UI objects (`UpdatePlayerHUD`)

### Player Input, Movement and Shooting

It's worth calling out that multiplayer concerns are front and center in an ECS style
of writing code: we always have an array of players.

`PlayerInputSystem` is responsible for fetching input from the regular Unity input API and
inserting that data into a `PlayerInput` component. It also counts down the _fire cooldown_,
that is, the waiting period before the player can fire again.

`PlayerMoveSystem` handles basic movement and shooting based on the input from the input
system. It is relatively straight forward except for how it creates a shot in case the
player has fired. Rather than spawning a shot directly, it creates a `ShotSpawnData`
component which instructs a different system to do that work later. This separation
of concerns solves several problems:

1. `PlayerMoveSystem` doesn't need to know what components need to go on an entity to make a
   working shot.
2. `ShotSpawnSystem` (which spawns shots from both enemies and players) doesn't need
   to know all the reasons shots get fired.
3. We can spawn the shots into the world all at once at some later, well defined
   point in time.

This setup achieves something similar to a delayed event in a traditional component
architecture.

### Enemy Spawning, Moving and Shooting

It wouldn't be a very challenging game without enemies shooting back at you, so naturally
there are a few systems dedicated to this.

Of the enemy systems, the most interesting one is the spawning system. It needs to keep
track of when to spawn an enemy, but we don't want to put that state in the system itself.
One of the design principles of ECS is that it shouldn't prevent you from recording
component state and playing it back later to reconstruct a scene. Storing a bunch of
state variables that carry meaning from frame to frame will break that contract.

The spawning system instead stores its state in a singleton component, attached to a
singleton entity. We create the entity and the initial values for this component in
a setup function `EnemySpawner.SetupComponentData()`. Here we also initialize a random
seed and store that along with the rest of the data, so that games will predictably spawn
enemies in the same pattern every time, regardless of frame rate or if something fancy like
state replay is happening.

Inside the system, due to `ref return` not being implemented yet, we have to take a copy
of our system's state from the singular `State` array, and then when we've modified it for
next frame, we store it back in to the component.

This may look like a lot of boilerplate (and it is) but it's also kind interesting to think
about this in a different way. What if we renamed `State` to `Wave` and update more than one
of them at a time, orchestrated by some other system? We would get multiple simultaneous
waves spawning and updating in concert. ECS makes these sort of transformations much easier
and cleaner than if we had used global data attached to the system. 

One quirk is that we have to put off actually spawning an entity until we've completed
the above step of storing back our component state, because touching the entity manager will
immediately invalidate all injected arrays (including the one where our state is kept!) Our
solution to this is command buffers (via `EntityCommandBuffer` - but command buffers don't
yet support `ISharedComponentData` which is needed here to set the rendered look.)

Enemies move automatically using the stock `MoveForward` component, so that's taken
care of.

We need them to shoot however, and `EnemyShootSystem` does just that. It creates
entities with `ShotSpawnData` data on them which will be converted to shots later, together
with any player shots.

Finally we also need a way to get rid of enemies that go off screen. `EnemyRemovalSystem`
goes through all enemy positions and kills off-screen enemies by setting their health to -1.

### Handling Shots

`ShotSpawnSystem` deals with creating actual shots from the requests dropped into the ECS by
players and enemies. This is a simple straight forward affair that just loops over all 
`ShotSpawnData` and converts them into shots.

More interesting is `ShotDamageSystem` which intersects bullets and targets and deals
damage. This uses 4 injected groups:

* Players
* Shots fired by players
* Enemies
* Shots fired by enemies

This way it can kick off two jobs:

* Players vs Enemy Shots
* Enemies vs Player Shots

It uses a very simplistic point against circle collision test.

We also need to get rid of shots that didn't hit anything and just fly off. When their time to
live go to zero, we let `ShotDestroySystem` remove them.

### Final Pieces

We need something that culls dead objects from the world, and `RemoveDeadSystem` does just
that.

Finally, we want to display some data about the player's health on the screen
and `UpdatePlayerHUD` accomplishes this task.

