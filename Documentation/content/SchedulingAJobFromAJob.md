** Why can't I schedule a job from within a job?

We have a couple of important principles that drive our design.

1. Determinism by default - Determinism enables Networked games, Replay and Debugging tools.
2. Safe - Race conditions are immediately reported, this makes writing jobified significantly more approachable and simple.

These two principles applied result in some choices / restrictions that we enforce.

Jobs can only be scheduled & Completed on the main thread. But why?
- If you were to schedule a job from another job and call JobHandle.Complete, that will lead to impossible to solve job scheduler dead locks.
(We have tried this over the last couple years with Unity C++ code, and every single case has resulted in tears and us reverting such patterns in our code. The race conditions are rare but provably impossible to solve in all cases)
- If you were to simply schedule a job from another job but not call JobHandle.Complete from the job, then there is no way to gurantee determinism. The main thread has to the JobHandle.Complete() call but who gives it to the main thread to wait on it?

In summary, it's the first instinct to want to schedule jobs and wait for jobs from a job.
But experience tells us that this is always a bad idea. So the C# job system does not support it.


Ok but how do I process workloads where I don't know the exact size upfront?

Its totally fine to schedule some jobs, conservatively. And then simply early out and do nothing if it turns out the number of actual elements to operate on is only calculated in a previous job and not known at schedule time.
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
