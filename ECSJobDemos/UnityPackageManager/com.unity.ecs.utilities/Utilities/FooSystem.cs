using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityEngine.ECS.Utilities
{
    public interface IComponentFloatValue
    {
        float Value { get; set; }
    }
    public interface IComponentIntValue
    {
        int Value { get; set; }
    }
    public interface IComponentFloat3Value
    {
        float3 Value { get; set; }
    }
    
    public class FooSystem<TSource> : JobComponentSystem
        where TSource : struct, IComponentData
    {
        // [ComputeJobOptimization] #BURST-GENERICS https://gitlab.internal.unity3d.com/burst/burst/issues/9
        struct IterateJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<TSource> data;
            public void Execute(int index)
            {
                TSource foo = data[index];
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var maingroup = GetComponentGroup( ComponentType.ReadOnly(typeof(TSource)) );
            var data = maingroup.GetComponentDataArray<TSource>();

            var maingroupJobHandle = maingroup.GetDependency();
            var iterateJob = new IterateJob {data = data};
            var iterateJobHandle = iterateJob.Schedule(data.Length, 64, maingroupJobHandle);
            maingroup.AddDependency(iterateJobHandle);
            maingroup.Dispose();
            
            return inputDeps;
        }
    }
}
