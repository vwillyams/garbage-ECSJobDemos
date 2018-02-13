using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    public class BurstECSTests
    {

    public struct SimpleComponentData : IComponentData
    {
        public int val;
    }

    [ComputeJobOptimization(Accuracy = Accuracy.Low, Support = Support.Relaxed)]
    public struct SimpleComponentDataTest : IJob
    {
        [ReadOnly]
        public ComponentDataArray<SimpleComponentData> a;

        public ComponentDataArray<SimpleComponentData> b;

        public void Execute()
        {
            for (int i = 0; i < a.Length; ++i)
            {
                var c = b[i];
                c.val = a[i].val + 1;
                b[i] = c;
            }
        }
    }

    }
}
