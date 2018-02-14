# 0.0.2

## New Features

## Changes
* [InjectComponentGroup] and [InjectComponentFromEntity] were replaced by simply [Inject] handling all injection cases.
* EntityManager component naming consistency renaming
	EntityManager can access both components and component data thus:
	- HasComponent(ComponentType type), RemoveComponent(ComponentType type), AddComponent(ComponentType type)
	- AddComponentData(Entity entity, T componentData) where T : struct, IComponentData

	* EntityManager.RemoveComponentData -> EntityManager.RemoveComponent
	* EntityManager.AddComponent(...) : IComponentData -> EntityManager.AddComponentData(...) : IComponentData
	* EntityManager.AddSharedComponent -> EntityManager.AddSharedComponentData
	* EntityManager.SetSharedComponent -> EntityManager.SetSharedComponentData
	* EntityManager.SetComponent -> EntityManager.SetComponentData
	* EntityManager.GetAllUniqueSharedComponents -> EntityManager.GetAllUniqueSharedComponentDatas


## Fixes

# 0.0.1

## New Features
* Burst Compiler Preview
    * Used to compile an C# jobs, simply put  [ComputeJobOptimization] on each job 
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
