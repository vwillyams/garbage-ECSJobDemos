# index

![Unity](https://unity3d.com/files/images/ogimg.jpg?1)
# ECS Easily Explained

* [How ECS Works](content/getting_started.md)
    * [Naming Conventions](content/ecs_concepts.md)
    * [ECS Principles](content/ecs_principles_and_vision.md)
* [Is ECS For You?](content/is_ecs_for_you.md)
* [ECS Features in Detail](content/ecs_in_detail.md)
* [ECS and Asset Store Compatibility]

# Job System Overview

* [How the Job System Works](content/job_system.md)
* [Low Level Overview - Creating Containers & Custom Job Types](content/custom_job_types.md)
* [How to Optimize for the Burst Compiler](content/burst_optimization.md)

# Tutorials

* [Tutorial Walk Through #1: MonoBehaviour vs Hybrid ECS](content/tutorial_1.md)
* [Tutorial Walk Through #2: MonoBehaviour vs Pure ECS](content/tutorial_2.md)
* [Tutorial Walk Through #3: Two-Stick Shooter in ECS](content/tutorial_3.md)

# Further Information

*Unite Austin 2017 - Writing High Performance C# Scripts*
[![Unite Austin 2017 - Writing High Performance C# Scripts](http://img.youtube.com/vi/tGmnZdY5Y-E/0.jpg)](http://www.youtube.com/watch?v=tGmnZdY5Y-E)

*Unite Austin 2017 Keynote - Performance Demo ft. Nordeus*
[![Unite Austin 2017 Keynote - Performance Demo ft. Nordeus](http://img.youtube.com/vi/0969LalB7vw/0.jpg)](http://www.youtube.com/watch?v=0969LalB7vw)

---

# Status of ECS

* Entity iteration
    * We have implemented various approaches (Foreach, vs arrays, injection vs API). Right now we just expose all possible ways of doing it, so that users can give us feedback on which one they like by actually trying them. Later on we will decide on the best way and delete all others.
    * Performance in burst is not quite perfect. Need some mark up for alias analysis.

* Job API using ECS
    * We believe we can make it signficantly simpler. Next thing to try out is Async / Await and see if there are some nice patterns there that are both fast & simple.

* Switch to C#7 / ref returns (In progress - Dom & joe)

* Support for hierarchies (In Progress - Mike)
    * TODO: MIKE

* EntityTransaction
    * Basic Transaction support for creating entities from job (done)
    * Exclusive mode Transaction support (Destroy, AddComponent/RemoveComponent from job) - NOT YET STARTED
* Entity Editing - NOT YET STARTED
Our Goal is to be able to make entities editable just like game objects are. Scenes are either full of entities or full of game objects. Right now we have no tooling for  editing entities without game objects.
    * Display & Edit entities in hierarchy window + inspector
    * Save scene / Open scene / Prefabs for entities
    * Turn ComponentSystems on and off for debuging performance in profiler in UI

* Import pipeline / BlobAssets - (Research - Bogdan / Jason)


---

## Andreas's Notes from TwoStick Example

- Adding a new World requires a lot of cleanup/setup boilerplate. We should standardize/document this.
- Why can you add ISharedComponentData to an archetype? No exception was thrown, but then `SetSharedComponentData` threw, because it wasn't really there anyway.
- We need a chapter on how to render basic stuff.
- We need a chapter on how to get resources in from the regular Unity scene into the ECS space:
  - How do you best import settings for an `InstanceRenderComponent`?
- Bootstrapping simple things, how is it best done?
  - How to add the player entity (in twostick I do it in code now, based on a template gameobject that has mesh data on it which I then throw away)
- How do you control the player loop?
- How do you work with system dependencies?

---

## Further Sources of Content:

Screenshots from rotator ECS demo, spaceship example by Daniel, two stick shooter + other ECS Demos? (which ever ones we use)
