# introduction

The aim of this document is to introduce the ECS paradigm, how it has been implemented within Unity, and how you can take advantage of it to improve your game.

Unity ECS adds an [Entity-Component-System](https://en.wikipedia.org/wiki/Entity%E2%80%93component%E2%80%93system) design pattern that enables many new features and typically much better performance compared to traditional MonoBehavior code. This is acheived under-the-hood through better memory management and [multi-threading](https://en.wikipedia.org/wiki/Thread_(computing)).

Depending on what you are trying to achieve, you can often  see around 100 times improvement in performance speeds using ECS - if not more.

## Code the Right Way From the Start

Even if performance isn't your number one concern, ECS is still a better way of programming and structuring your code. It makes it more scalable and maintainable in the long run.

## Analogies

### Multi-Threading

**The Loom Analogy**

![A Loom](https://cdn.pixabay.com/photo/2017/08/02/11/53/weaving-loom-2571179_1280.jpg)

The old way of coding with MonoBehaviour is like a person knitting with one thread of wool at a time, whereas ECS is more like a loom working on multiple threads at once. In the same way the loom revolutionalised productivity when the Industrial Age hit, ECS allows you to do a lot more with less resources.

**The Building Block Analogy**

![Building Blocks](https://cdn.pixabay.com/photo/2016/04/22/08/25/bricks-1345327_1280.jpg)

Although ECS comes with less intuitive "building blocks" they are designed with the whole picture in mind. For example, there are all kinds of Lego kits that are simple and they can work together in all sorts of ways, but you wouldn't build a real house out of them. The actual building blocks of a real house are bigger and more complicated but they are designed with a the physical building in mind. This is the same with ECS.

### Memory Management

**The Warehouse Analogy**

As well as multi-threading improvements ECS also manages memory better to begin with. 

ToDo - fill this out

For a more in-depth description of this analogy see the post _[Data Locality](http://gameprogrammingpatterns.com/data-locality.html)_ by Robert Nystrom.


## Pre-requisites

We would expect readers to have programming experince, the basics of writing code in [MonoBehaviour](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html), and some knowledge of how [GameObjects](https://docs.unity3d.com/ScriptReference/GameObject.html) and [Components](https://docs.unity3d.com/ScriptReference/Component.html) work together. 

If you are familiar with the ECS paradigm, take a moment to read about our [Naming Conventions](https://hackmd.io/EwBgHGDGBsDMYFoCGATApgMwQFhWAnMiCigpDNAIywYDs0kGYQA=) to avoid confusion with Unity's implementation of ECS.

You can also read more about the [Principles](https://hackmd.io/GYEwxgnBCMBGYFoDsAGFsEBYBM0IIlkwDYEUBDEgZiU1nOGIA4g=?edit) we are trying to follow when designing and writing code in ECS.