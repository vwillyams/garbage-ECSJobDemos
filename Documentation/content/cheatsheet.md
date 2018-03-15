# ECS Cheat Sheet

Here is a quick reference of the common classes, interfaces, and structs that have been introduced in this documentation by [ECS](#ecs-related) and the [C# job system](#c#-job-system-related). 

> Note: This is not an exhaustive list and can be added to over time as ECS, and its related documentation, expands.

## C# job system related

| Namespace     | Name          | Type  |
| :-------------: |:-------------:| :-----:|
| Unity.Collections | [NativeContainer](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerAttribute.html) | Class | 
| Unity.Collections | [NativeArray](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.NativeArray_1.html)  | Struct |
| Unity.Collections | [NativeSlice](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.NativeSlice_1.html) | Struct | 
| Unity.Jobs | [IJob](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Jobs.IJob.html) | Interface | 
| Unity.Jobs | [IJobParallelFor](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Jobs.IJobParallelFor.html) | Interface |
| Unity.Jobs | [JobHandle](hhttps://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Jobs.JobHandle.html) | Interface |

## ECS related

| Namespace     | Name          | Type  | 
| :-------------: |:-------------:| :-----:| 
| Unity.Collections | [NativeHashMap](../../ECSJobDemos/Packages/com.unity.collections/Unity.Collections/NativeHashMap.cs) | Struct |
| Unity.Collections | [NativeList](../../ECSJobDemos/Packages/com.unity.collections/Unity.Collections/NativeList.cs) | Struct |
| Unity.Collections | [NativeQueue](../../ECSJobDemos/Packages/com.unity.collections/Unity.Collections/NativeQueue.cs) | Struct |
| Unity.Entities | [IComponentData](../../ECSJobDemos/Packages/com.unity.entities/Unity.Entities/IComponentData.cs) | Interface |
| Unity.Entities | [ISharedComponentData](../../ECSJobDemos/Packages/com.unity.entities/Unity.Entities/IComponentData.cs) | Interface |
| Unity.Entities | [ComponentSystem](../../ECSJobDemos/Packages/com.unity.entities/Unity.Entities/ComponentSystem.cs)  | Abstract Class |
| Unity.Jobs | [IJobParallelForBatch](../../ECSJobDemos/Packages/com.unity.jobs/Unity.Jobs/IJobParallelForBatch.cs)  | Interface |
| Unity.Jobs | [IJobParallelForFilter](../../ECSJobDemos/Packages/com.unity.jobs/Unity.Jobs/IJobParallelForFilter.cs)  | Interface |

### Attributes

* [[NativeContainer]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerAttribute.html)
* [[NativeContainerIsAtomicWriteOnly]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerIsAtomicWriteOnlyAttribute.html) 
* [[NativeContainerSupportsMinMaxWriteRestriction]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerSupportsMinMaxWriteRestrictionAttribute.html) 
* [[NativeContainerNeedsThreadIndex]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerNeedsThreadIndexAttribute.html)
* [[NativeContainerSupportsDeallocateOnJobCompletion]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeContainerSupportsDeallocateOnJobCompletionAttribute.html)
* [[NativeDisableUnsafePtrRestriction]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute.html)
* [[NativeSetClassTypeToNullOnSchedule]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeSetClassTypeToNullOnScheduleAttribute.html)
* [[ReadOnly]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.ReadOnlyAttribute.html)
* [[WriteOnly]](https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Unity.Collections.WriteOnlyAttribute.html)

### Other

* \#if ENABLE_UNITY_COLLECTIONS_CHECKS ... #endif