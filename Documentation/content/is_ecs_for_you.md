# Is ECS for you?
If you're considering Unity's ECS, you probably fall into one of two camps:
## You already have a game, but it's slow
You've heard about the massive performance boosts you can get from using ECS and the Job System, and you're wondering if they apply to your game. Improving performance always starts with profiling to find the slowest parts of your game. If there are Update() calls taking a lot of time in the profiler, then these are probably good candidates for optimization using ECS. If your game spends most of its time in physics or rendering, then you should consider reducing those costs separately first. 

## You want to try ECS in your new game
You've heard that ECS not only improves performance, but helps you write cleaner, clearer and more maintainable code. You'd like to see how it works for you in practice.
This is a fun scenario, because you get to write straightforward code from the beginning. There are a few things to keep in mind:
### You will probably want to use hybrid mode at first
Right now, most of Unity's existing systems are still only designed to be used with GameObjects. This means that making a "pure" ECS game requires that you write a lot of what you need yourself. To get started, it is often useful to have Unity's built-in physics, audio and rendering systems. To do this, you will need GameObjects with Colliders, Rigidbodies, and (crucially) GameObjectEntity scripts.
The important thing to remember is that this is totally fine, and Unity's ECS was designed to work with traditional GameObject/Component setups, as well as lightweight Entity/ComponentData ones. You won't be able to use the Job System with traditional GameObject stuff, but you'll probably find there are lots of places where you are using just Entities anyway.

## Quick Reference

### Pros and Cons List
TODO
Here is a quick overview of things to consider before trying out ECS:

* Pros:
    * Process collections of components in batches asyncronosly instead of one by one.
    * You can use the Mono "old way" at the same time as introducing the "new way" of coding with ECS.
    * Multithreading
    * Makes debugging performance easier in profiler when you disable parts of your code to check their load.
    * etc...

* Cons: 
    * Limitations on what entities/components are supported.
    * Need to refactor codebase to get optimal performance.
    * etc...
