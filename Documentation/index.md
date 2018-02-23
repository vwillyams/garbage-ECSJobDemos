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
* [Scheduling a Job from a job - Why not?](content/SchedulingAJobFromAJob.md)

# Tutorials

* [Tutorial Walk Through #1: MonoBehaviour vs Hybrid ECS](content/tutorial_1.md)
* [Tutorial Walk Through #2: MonoBehaviour vs Pure ECS](content/tutorial_2.md)
* [Tutorial Walk Through #3: Two-Stick Shooter in ECS](content/tutorial_3.md)

# Simple Examples

* [RotationExample.unity](content/rotation_example.md): Loop to change component if entity position is inside a moving sphere.

# Further Information

*Unite Austin 2017 - Writing High Performance C# Scripts*
[![Unite Austin 2017 - Writing High Performance C# Scripts](http://img.youtube.com/vi/tGmnZdY5Y-E/0.jpg)](http://www.youtube.com/watch?v=tGmnZdY5Y-E)

*Unite Austin 2017 Keynote - Performance Demo ft. Nordeus*
[![Unite Austin 2017 Keynote - Performance Demo ft. Nordeus](http://img.youtube.com/vi/0969LalB7vw/0.jpg)](http://www.youtube.com/watch?v=0969LalB7vw)

---

# Status of ECS

* Entity iteration
* We have implemented various approaches (Foreach, vs arrays, injection vs API). Right now we just expose all possible ways of doing it, so that users can give us feedback on which one they like by actually trying them. Later on we will decide on the best way and delete all others.

* Job API using ECS
* We believe we can make it signficantly simpler. Next thing to try out is Async / Await and see if there are some nice patterns there that are both fast & simple.

Our goal is to be able to make entities editable just like game objects are. Scenes are either full of entities or full of game objects. Right now we have no tooling for  editing entities without game objects.
* Display & Edit entities in hierarchy window + inspector
* Save scene / Open scene / Prefabs for entities

