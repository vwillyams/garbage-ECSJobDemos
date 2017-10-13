﻿using UnityEngine;
using System;
using UnityEngine.Jobs;
using UnityEngine.Collections;

namespace UnityEngine.Jobs
{
    public interface IJobParallelForFilter
    {
        bool Execute(int index);
    }

    public static class JobParallelIndexListExtensions
    {
        struct JobStructProduce<T> where T : struct, IJobParallelForFilter
        {
            public struct JobDataWithFiltering
            {
                [NativeDisableParallelForRestriction]
                public NativeList<int> outputIndices;
                public int appendCount;
                public T data;
            }

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobDataWithFiltering), typeof(T), (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobDataWithFiltering data, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int beginIndex, int count);

            // @TODO: Use parallel for job... (Need to expose combine jobs)

            public unsafe static void Execute(ref JobDataWithFiltering jobData, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int startIndex, int count)
            {
                if (jobData.appendCount == -1)
                    ExecuteFilter(ref jobData, bufferRangePatchData);
                else
                    ExecuteAppend(ref jobData, bufferRangePatchData);
            }

            public unsafe static void ExecuteAppend(ref JobDataWithFiltering jobData, System.IntPtr bufferRangePatchData)
            {
                int oldLength = jobData.outputIndices.Length;
                jobData.outputIndices.Capacity = math.max (jobData.appendCount + oldLength, jobData.outputIndices.Capacity);

                int* outputPtr = (int*)jobData.outputIndices.UnsafePtr;
                int outputIndex = oldLength;

				//@TODO: Doesn't work in p2gc yet...
                //JobsUtility.PatchBufferMinMaxRanges (bufferRangePatchData, UnsafeUtility.AddressOf (ref jobData), 0, jobData.appendCount);

                for (int i = 0;i != jobData.appendCount;i++)
                {
					if (jobData.data.Execute (i))
					{
						outputPtr[outputIndex] = i;
						outputIndex++;
					}
                }

                jobData.outputIndices.ResizeUninitialized(outputIndex);
            }

            public unsafe static void ExecuteFilter(ref JobDataWithFiltering jobData, System.IntPtr bufferRangePatchData)
            {
                int* outputPtr = (int*)jobData.outputIndices.UnsafePtr;
                int inputLength = jobData.outputIndices.Length;

                int outputCount = 0;
                for (int i = 0;i != inputLength;i++)
                {
                    int inputIndex = outputPtr[i];
					//@TODO: Doesn't work in p2gc yet...
					//JobsUtility.PatchBufferMinMaxRanges (bufferRangePatchData, UnsafeUtility.AddressOf (ref jobData), inputIndex, 1);

                    if (jobData.data.Execute(inputIndex))
                    {
                        outputPtr[outputCount] = inputIndex;
                        outputCount++;
                    }
                }

                jobData.outputIndices.ResizeUninitialized(outputCount);
            }
        }

        static public JobHandle ScheduleAppend<T>(this T jobData, NativeList<int> indices, int arrayLength, int minIndicesPerJobCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForFilter
        {
            JobStructProduce<T>.JobDataWithFiltering fullData;
            fullData.data = jobData;
            fullData.outputIndices = indices;
            fullData.appendCount = arrayLength;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStructProduce<T>.Initialize(), dependsOn, ScheduleMode.Batched);

            return JobsUtility.Schedule(ref scheduleParams, IntPtr.Zero);
        }

        static public JobHandle ScheduleFilter<T>(this T jobData, NativeList<int> indices, int minIndicesPerJobCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForFilter
        {
            JobStructProduce<T>.JobDataWithFiltering fullData;
            fullData.data = jobData;
            fullData.outputIndices = indices;
            fullData.appendCount = -1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStructProduce<T>.Initialize(), dependsOn, ScheduleMode.Batched);

            return JobsUtility.Schedule(ref scheduleParams, IntPtr.Zero);
        }

        //@TODO: RUN
    }
}
