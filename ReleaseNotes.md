# 0.0.1

## New Features
* Burst Compiler Preview
    * Used to compile an C# jobs, simply put  [ComputeCompiler] on each job 
    * Editor only for now, it is primarily meant to give you an idea of the performance you can expect when we ship the full AOT burst compiler
    * Compiles asynchronously, once compilation of the job completes. The runtime switches to using the burst compiled code.
* EntityTransaction API added to allow for creating entities from a job

## Improvements
* NativeQueue is now block based and always have a dynamic capacity which cannot be manually set or queried.
* Worlds have names and there is now a full list of them
* SharedComponentData API is now robust, performs automatic ref counting, and no longer leaks memory. SharedComponent API redesigned.
* Optimization for iterating component data arrays and EntityFromComponentData
* EntityManager.Instantiate, EntityManager.Destroy, CreateEntity optimizations


## Fixes
Fix a deadlock in system order update
