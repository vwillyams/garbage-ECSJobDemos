using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.Jobs.Tests
{
    [ComputeJobOptimization]
    public struct AliasTest1 : IJob
    {
        private int field1;
        private int pad0;
        private int pad1;
        private int pad2;
        private int pad3;
        private int field2;

        public void DoTheThing(ref int x)
        {
            x = x + 1;
        }

        public void Execute()
        {
            field1 = 17;
            field2 = field1 + 1;
            DoTheThing(ref field2);
            field1 = field2 + 1;
        }
    }

    [ComputeJobOptimization]
    public struct AliasTest2 : IJob
    {
        [ReadOnly]
        public NativeArray<int> a;

        public NativeArray<int> b;

        public void Execute()
        {
            for (int i = 0; i < a.Length; ++i)
            {
                b[i] = a[i];
            }
        }
    }

    [ComputeJobOptimization]
    public struct AliasTest3 : IJob
    {
        [ReadOnly]
        public NativeArray<int> a;

        public NativeArray<int> b;

        public void Execute()
        {
            NativeArray<int> acopy = a;
            for (int i = 0; i < acopy.Length; ++i)
            {
                b[i] = acopy[i];
            }
        }
    }

    [ComputeJobOptimization]
    public struct AliasTest4 : IJob
    {
        public NativeArray<int> a;
        public int test1;
        public int test2;

        public void Execute()
        {
            test1 = 12;
            a[0] = 13;
            test2 = test1;
        }
    }
}
