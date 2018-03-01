# Scheduling a job from a job - Why not?

We have a couple of important principles that drive our design.

* Determinism by default - Determinism enables Networked games, Replay and Debugging tools.
* Safe - Race conditions are immediately reported, this makes writing jobified code significantly more approachable and simple.

These two principles applied result in some choices / restrictions that we enforce.

## Jobs can only be completed on the main thread - But why?

If you were to call JobHandle.Complete, that will lead to impossible to solve job scheduler dead locks.
(We have tried this over the last couple years with Unity C++ code base, and every single case has resulted in tears and us reverting such patterns in our code.) The dead-locks are rare but provably impossible to solve in all cases, they are heavily dependent on timing of jobs.

## Jobs can only be scheduled on the main thread - But why?

If you were to simply schedule a job from another job but not call JobHandle.Complete from the job, then there is no way to gurantee determinism. The main thread has to call JobHandle.Complete() but who passes that JobHandle to the main thread and how do you know the job that schedules the job has already executed?

In summary, first instinct is to simply schedule jobs from jobs and wait for jobs from a job.
But experience tells us that this is always a bad idea. So the C# job system does not support it.

## Ok, but how do I process workloads where I don't know the exact size upfront?

Its totally fine to schedule jobs conservatively. And then simply early out and do nothing if it turns out the number of actual elements to process when the job executes is much less than the conservative number of elements that we determined at schedule time. 

In fact this way of doing it leads to deterministic execution and if the early out can skip a whole batch of operations its not really a performance issue.
Also there is no possibility of causing internal job scheduler dead locks.

For this purpose using IJobParallelForBatch as opposed to IJobParallelFor can be very useful since you can early out on a whole batch.
```
    public interface IJobParallelForBatch
    {
        void Execute(int startIndex, int count);
    }
```
TODO: CODE EXAMPLE for sorting?
