# Next Release

## Changes

* An `EntityCommandBuffer` that plays back automically after a `ComponentSystem`'s update is
  available as `PostUpdateCommands`

* Can now create entities/components from jobs and merge them into
  the world later via command buffers from injected `BarrierSystem`s
* `DeferredEntityChangeSystem` replaced by `EndFrameBarrier` (Note: This removes support for concurrent add/remove components. You'll need to change to IJob to add/remove components.)
  
* `NativeArraySharedValues<T>` for creating index tables of shared/unique values in a NativeArray.
* `NearestTargetPositionSystem<TNearestTarget,TTarget>` demonstrates how to use generics in JobComponentSystem
* `CopyComponentData<TSource,TDestination>` utility to copy ISingleValue ComponentData to NativeArray


* UnityPackageManager -> Packages folder. (Unity 2018.1 beta 7 introduces this change and we reflected it in the sample project)

* EntityManager.CreateComponentGroup should be replaced with ComponentSystem.GetComponentGroup.
It automatically associates & caches the ComponentGroup with the system (It is automatically disposed by ComponentSystem) and thus input dependencies will be setup correctly.

Additionally ComponentSystem.GetComponentGroup should not be called in OnUpdate() (Create and cache in OnCreateManager instead). ComponentSystem.GetComponentGroup allocates GC memory because the input is a param ComponentType[]...

* Systems are automatically disabled when all ComponentGroups have zero entities.
[AlwaysUpdateSystem] can be used to always force update a system.
(We measured 5 - 10x speedup for empty systems)

* EntityManager.GetComponentFromEntity/GetFixedArrayFromEntity have been moved to JobComponentSystem.GetComponentFromEntity. This way they can be safely used in jobs with the correct dependencies passed via the OnUpdate (JobHandle dependency)

* EntityManager.GetComponentFromEntity/GetFixedArrayFromEntity have been moved to JobComponentSystem.GetComponentFromEntity. This way they can be safely used in jobs with the correct dependencies passed via the OnUpdate (JobHandle dependency)

* Removed IAutoComponentSystemJob support

* Various namespace refactoring. Unity.ECS -> Unity.Entities.

* Optimizations for NativeHashMap and NativeMultiHashMap

* Can now get an array of shared component data from a component group (ComponentGroup.GetSharedComponentDataArray)
  SharedComponentDataArray<T> can also be injected similar to ComponentDataArray<T>
  Access through SharedComponentDataArray is always read only

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
