using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;

namespace Unity.ECS
{
    public interface ISingleValue<T>
    {
       T Value { get; set; } 
    }
    
    // [ComputeJobOptimization]
    public struct CopyComponentData<TSource,TDestination> : IJobParallelFor
    where TSource : struct, IComponentData, ISingleValue<TDestination>
    where TDestination : struct
    {
        [ReadOnly] public ComponentDataArray<TSource> source;
        public NativeArray<TDestination> results;

        public void Execute(int index)
        {
            results[index] = source[index].Value;
        }
    }
}
