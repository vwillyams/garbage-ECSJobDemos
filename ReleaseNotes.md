# 0.0.3

## Changes
* UnityPackageManager -> Packages folder. (Unity 2018.1 beta 7 introduces this change and we reflected it in the sample project)

* EntityManager.CreateComponentGroup should be replaced with ComponentSystem.GetComponentGroup.
It automatically associates & caches the ComponentGroup with the system (It is automatically disposed by ComponentSystem) and thus input dependencies will be setup correctly.

Additionally ComponentSystem.GetComponentGroup should not be called in OnUpdate() (Create and cache in OnCreateManager instead). ComponentSystem.GetComponentGroup allocates GC memory because the input is a param ComponentType[]...

* [DisableSystemWhenEmpty] disables a system (OnUpdate will not be called) if all it's used ComponentGroups have zero entities. It is recommended to use this attribute on all systems unless they absolutely have to run every frame for performance reasons (We measured 5 - 10x speedup for empty systems using this attribute)

* EntityManager.GetComponentFromEntity/GetFixedArrayFromEntity have been moved to JobComponentSystem.GetComponentFromEntity. This way they can be safely used in jobs with the correct dependencies passed via the OnUpdate (JobHandle dependency)

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
