# Custom Job types
On the very lowest level of the JobSystem jobs are scheduled by calling one of the Schedule functions in JobsUtility. The currently existing job types all use these functions, but it is also possible to create specialized job types using the same APIs.

These API's use unsafe code and have to be crafted carefully since they can easily introduce unwanted race conditions. If you add your own job types, we strongly recommend to aim for full test coverage.

As an example we have a custom job type [IJobParalelForBatch](https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/UnityPackageManager/com.unity.jobs/Unity.Jobs/IJobParallelForBatch.cs)
 
It works like IJobParallelFor but instead of calling a single execute function per index it calls one execute function per batch being executed.

# Custom NativeContainers
When writing jobs the data communication between jobs is one of the hardest parts to get right. Just using NativeArray is very limiting. Using NativeQueue, NativeHashMap and NativeMultiHashMap and their concurrent versions solves most use-cases.

For the remaining use cases it is possible to write your own custom NativeContainers.
When writing custom containers for thread synchonization it is very important to write correct code. We strongly recommend full test coverage for any new containers you add.

As a very simple example of this we will create a NativeCounter which can be incremented in a ParallelFor job through NativeCounter.Concurrent and read in a later job or on the main thread.

Let's start with the basic container type.
```C#
// Mark this struct as a NativeContainer, usually this would be a generic struct for containers, but a counter does not need to be generic
[StructLayout(LayoutKind.Sequential)]
[NativeContainer]
unsafe public struct NativeCounter
{
    // The actual pointer to the allocated count needs to have restrictions relaxed so jobs can be schedled with this container
    [NativeDisableUnsafePtrRestriction]
    int* m_Counter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    AtomicSafetyHandle m_Safety;
    // The dispose sentinel tracks memory leaks. It is a managed type so it is cleared to null when scheduling a job
    // The job cannot dispose the container, and no one else can dispose it until the job has run so it is ok to not pass it along
    [NativeSetClassTypeToNullOnSchedule]
    DisposeSentinel m_DisposeSentinel;
#endif

    // Keep track of where the memory for this was allocated
    Allocator m_AllocatorLabel;

    public unsafe NativeCounter(Allocator label)
    {
        // This check is redundant since we always use an int which is blittable.
        // It is here as an example of how to check for type correctness for generic types.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (!UnsafeUtility.IsBlittable<int>())
            throw new ArgumentException(string.Format("{0} used in NativeQueue<{0}> must be blittable", typeof(int)));
#endif
        m_AllocatorLabel = label;

        // Allocate native memory for a single integer
        m_Counter = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>(), 4, label);

        // Create a dispose sentinel to track memory leaks. This also creates the AtomicSafetyHandle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
        // Initialize the count to 0 to avoid uninitialized data
        Count = 0;
    }

    unsafe public void Increment()
    {
        // Verify that the caller has write permission on this data. 
        // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        (*m_Counter)++;
    }

    unsafe public int Count
    {
        get
        {
            // Verify that the caller has read permission on this data. 
            // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return *m_Counter;
        }
        set
        {
            // Verify that the caller has write permission on this data. This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            *m_Counter = value;
        }
    }

    public bool IsCreated
    {
        get { return m_Counter != null; }
    }

    public void Dispose()
    {
        // Let the dispose sentinel know that the data has been freed so it does not report any memory leaks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif

        UnsafeUtility.Free(m_Counter, m_AllocatorLabel);
        m_Counter = null;
    }
}
```
With this we have a simple NativeContainer where we can get, set and increment the count. This container can be passed to a job, but it has the same restrictions as NativeArray which means it cannot be passed to a ParallelFor job with write access.

The next step is to make it usable in ParallelFor. In order to avoid race conditions we want to make sure no-one else is reading it while the ParallelFor is writing to it. To achieve this we create a separate inner struct called Concurrent which can handle multiple writers but no readers. We make sure NativeCounter.Concurrent can be assigned to from a normal NativeCounter since it is not possible for it to live separately from a NativeCounter.
```C#
[NativeContainer]
// This attribute is what makes it possible to use NativeCounter.Concurrent in a ParallelFor job
[NativeContainerIsAtomicWriteOnly]
unsafe public struct Concurrent
{
    // Copy of the pointer from the full NativeCounter
    [NativeDisableUnsafePtrRestriction]
    int* 	m_Counter;

    // Copy of the AtomicSafetyHandle from the full NativeCounter. The dispose sentinel is not copied since this inner struct does not own the memory and is not responsible for freeing it
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    AtomicSafetyHandle m_Safety;
#endif

    // This is what makes it possible to assign to NativeCounter.Concurrent from NativeCounter
    unsafe public static implicit operator NativeCounter.Concurrent (NativeCounter cnt)
    {
        NativeCounter.Concurrent concurrent;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(cnt.m_Safety);
        concurrent.m_Safety = cnt.m_Safety;
        AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
#endif

        concurrent.m_Counter = cnt.m_Counter;
        return concurrent;
    }

    unsafe public void Increment()
    {
        // Increment still needs to check for write permissions
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        // The actual increment is implemented with an atomic since it can be incremented by multiple threads at the same time
        Interlocked.Increment(ref *m_Counter);
    }
}
```

With this setup we can schedule ParallelFor with write access to a NativeCounter through the inner Concurrect struct, like this.
```C#
struct CountZeros : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<int> input;
    public NativeCounter.Concurrent counter;
    public void Execute(int i)
    {
        if (input[i] == 0)
        {
            counter.Increase();
        }
    }
}
```
```C#
var counter = new NativeCounter(Allocator.Temp);
var jobData = new CountZeros();
jobData.input = input;
jobData.counter = counter;
counter.Count = 0;

var handle = jobData.Schedule(input.Length, 8);
handle.Complete();

Debug.Log("The array countains " + counter.Count + " zeros");
counter.Dispose();
```
## Better cache usage
The NativeCounter from the previous section is a working implementation of a counter, but all jobs in the ParallelFor will access the same atomic to increment the value. This is not optimal since it means the same cache line is used by all threads.
The way this generally solved in NativeContainers is to have a local cache per worker thread, which is stored on its own cache line.

The [NativeContainerNeedsThreadIndex] attribute can inject a worker thread index into the container, the index is guranteed to be unique while accessing the container from the parallel jobs.

In order to make such an optimization here we need to change a few things. The first thing we need to change is the data layout. For performance reasons we need one full cache line per worker thread, rather than a single int to avoid [false sharing](https://en.wikipedia.org/wiki/False_sharing). 

We start by adding a constant for the number of ints on a cache line.
```C#
public const int IntsPerCacheLine = JobsUtility.CacheLineSize / sizeof(int);
```
Next we change the amount of memory allocated.
```C#
// One full cache line (integers per cacheline * size of integer) for each potential worker index, JobsUtility.MaxJobThreadCount
m_Counter = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>()*IntsPerCacheLine*JobsUtility.MaxJobThreadCount, 4, label);
```

When accessing the counter from the main, non-concurrent, version there can only be one writer so the increment function is fine with the new memory layout.
For get and set of the count we need to loop over all potential worker indices.
```C#
unsafe public int Count
{
    get
    {
        // Verify that the caller has read permission on this data. 
        // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        int count = 0;
        for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            count += m_Counter[IntsPerCacheLine * i];
        return count;
    }
    set
    {
        // Verify that the caller has write permission on this data. 
        // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        // Clear all locally cached counts, 
        // set the first one to the required value
        for (int i = 1; i < JobsUtility.MaxJobThreadCount; ++i)
            m_Counter[IntsPerCacheLine * i] = 0;
        *m_Counter = value;
    }
}
```

The final change is the inner Concurrent struct which now needs to get the worker index injected into it. Since each worker only runs one job at the time there is no longer any need to use atomics when only accessing the local count.
```C#
[NativeContainer]
[NativeContainerIsAtomicWriteOnly]
// Let the JobSystem know that it should inject the current worker index into this container
[NativeContainerNeedsThreadIndex]
unsafe public struct Concurrent
{
    [NativeDisableUnsafePtrRestriction]
    int* 	m_Counter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    AtomicSafetyHandle m_Safety;
#endif

    // The current worker thread index, it must use this exact name since it is injected
    int m_ThreadIndex;

    unsafe public static implicit operator NativeCacheCounter.Concurrent (NativeCacheCounter cnt)
    {
        NativeCacheCounter.Concurrent concurrent;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(cnt.m_Safety);
        concurrent.m_Safety = cnt.m_Safety;
        AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
#endif

        concurrent.m_Counter = cnt.m_Counter;
        concurrent.m_ThreadIndex = 0;
        return concurrent;
    }

    unsafe public void Increment()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        // No need for atomics any more since we are just incrementing the local count
        ++m_Counter[IntsPerCacheLine*m_ThreadIndex];
    }
}
```
Writing the counter this way significantly reduces the overhead of having multiple threads writing to it. It does however come at a price. The cost of getting the count on the main thread has increased significantly since it now needs to check all local caches and sum them up. If you are aware of this and make sure to cache the return values it is usually worth it, but you need to know the limitations of your data structures. So we strongly recommend documenting the performance characteristics.
## Tests
The NativeCounter is not complete, the only thing left os to add tests for it to make sure it is correct and that it does not break in the future. When writing tests you should try to cover as many edge cases as possible. It is also a good idea to add some kind of stress test using jobs to detect race conditions, even if it is unlikely to find all race conditions. The NativeCounter API is very small so the set to test is not huge.
Both versions of the counter from this document are available at https://github.com/Unity-Technologies/ECSJobDemos/tree/master/ECSJobDemos/Assets/NativeCounterDemo .
The tests for them are in https://github.com/Unity-Technologies/ECSJobDemos/blob/master/ECSJobDemos/Assets/NativeCounterDemo/Editor/NativeCounterTests.cs .
