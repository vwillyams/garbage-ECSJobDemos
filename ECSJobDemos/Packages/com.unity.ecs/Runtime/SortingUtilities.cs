using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Entities
{
    struct SortingUtilities
    {
        public static unsafe void InsertSorted(ComponentType* data, int length, ComponentType newValue)
        {
            while (length > 0 && newValue < data[length-1])
            {
                data[length] = data[length-1];
                --length;
            }
            data[length] = newValue;
        }

        public static unsafe void InsertSorted(ComponentTypeInArchetype* data, int length, ComponentType newValue)
        {
            var newVal= new ComponentTypeInArchetype(newValue);
            while (length > 0 && newVal < data[length-1])
            {
                data[length] = data[length-1];
                --length;
            }
            data[length] = newVal;
        }

    }
    
    struct NativeArraySharedValues<T> : IDisposable
        where T : struct, IComparable
    {
        private NativeArray<int> m_Buffer;
        [ReadOnly] private NativeArray<T> m_Source;
        private int m_SortedBuffer;

        public NativeArraySharedValues(NativeArray<T> source,Allocator allocator)
        {
            m_Buffer = new NativeArray<int>((source.Length*4)+1,allocator);
            m_Source = source;
            m_SortedBuffer = 0;
        }

        struct InitializeIndices : IJobParallelFor
        {
            public NativeArray<int> buffer;
            
            public void Execute(int index)
            {
                buffer[index] = index;
            }
        }

        struct MergeSortedPairs : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<int> buffer;
            [ReadOnly] public NativeArray<T> source;
            public int sortedCount;
            public int outputBuffer; 

            public void Execute(int index)
            {
                int mergedCount = sortedCount * 2;
                int offset = (index * mergedCount);
                int inputOffset = (outputBuffer ^ 1) * source.Length;
                int outputOffset = (outputBuffer) * source.Length;
                int leftCount = sortedCount;
                int rightCount = sortedCount;
                var leftNext = 0;
                var rightNext = 0;

                for (int i = 0; i < mergedCount; i++)
                {
                    if ((leftNext < leftCount) && (rightNext < rightCount))
                    {
                        var leftIndex = buffer[inputOffset + offset + leftNext];
                        var rightIndex = buffer[inputOffset + offset + leftCount + rightNext];
                        var leftValue = source[leftIndex];
                        var rightValue = source[rightIndex];
                    
                        if (rightValue.CompareTo(leftValue) < 0)
                        {
                            buffer[outputOffset+ offset + i] = rightIndex;
                            rightNext++;
                        }
                        else
                        {
                            buffer[outputOffset + offset + i] = leftIndex;
                            leftNext++;
                        }
                    }
                    else if (leftNext < leftCount)
                    {
                        var leftIndex = buffer[inputOffset + offset + leftNext];
                        buffer[outputOffset + offset + i] = leftIndex;
                        leftNext++;
                    }
                    else
                    {
                        var rightIndex = buffer[inputOffset + offset + leftCount + rightNext];
                        buffer[outputOffset+ offset + i] = rightIndex;
                        rightNext++;
                    }
                }
            }
        }
        
        struct MergeRemainderPair : IJob
        {
            public NativeArray<int> buffer;
            [ReadOnly] public NativeArray<T> source;
            public int leftCount;
            public int rightCount;
            public int startIndex;
            public int outputBuffer; 

            public void Execute()
            {
                int offset = startIndex;
                int mergedCount = leftCount + rightCount;
                int inputOffset = (outputBuffer ^ 1) * source.Length;
                int outputOffset = (outputBuffer) * source.Length;
                var leftNext = 0;
                var rightNext = 0;

                for (int i = 0; i < mergedCount; i++)
                {
                    if ((leftNext < leftCount) && (rightNext < rightCount))
                    {
                        var leftIndex = buffer[inputOffset + offset + leftNext];
                        var rightIndex = buffer[inputOffset + offset + leftCount + rightNext];
                        var leftValue = source[leftIndex];
                        var rightValue = source[rightIndex];
                    
                        if (rightValue.CompareTo(leftValue) < 0)
                        {
                            buffer[outputOffset+ offset + i] = rightIndex;
                            rightNext++;
                        }
                        else
                        {
                            buffer[outputOffset + offset + i] = leftIndex;
                            leftNext++;
                        }
                    }
                    else if (leftNext < leftCount)
                    {
                        var leftIndex = buffer[inputOffset + offset + leftNext];
                        buffer[outputOffset + offset + i] = leftIndex;
                        leftNext++;
                    }
                    else
                    {
                        var rightIndex = buffer[inputOffset + offset + leftCount + rightNext];
                        buffer[outputOffset+ offset + i] = rightIndex;
                        rightNext++;
                    }
                }
            }
        }

        struct CopyRemainder : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<int> buffer;
            [ReadOnly] public NativeArray<T> source;
            public int startIndex;
            public int outputBuffer;

            public void Execute(int index)
            {
                int inputOffset = (outputBuffer ^ 1) * source.Length;
                int outputOffset = (outputBuffer) * source.Length;
                int outputIndex = outputOffset + startIndex + index;
                int inputIndex = inputOffset + startIndex + index;
                int valueIndex = buffer[inputIndex];
                buffer[outputIndex] = valueIndex;
            }
        }

        JobHandle MergeSortedLists(JobHandle inputDeps, int sortedCount, int outputBuffer)
        {
            var pairCount = m_Source.Length / (sortedCount*2);
            var mergeSortedPairsJob = new MergeSortedPairs
            {
                buffer = m_Buffer,
                source = m_Source,
                sortedCount = sortedCount,
                outputBuffer = outputBuffer
            };
            var mergeSortedPairsJobHandle = mergeSortedPairsJob.Schedule(pairCount, 64, inputDeps);
            var remainder = m_Source.Length - (pairCount * sortedCount * 2);
            if (remainder > sortedCount)
            {
                var mergeRemainderPairJob = new MergeRemainderPair
                {
                    startIndex = pairCount * sortedCount * 2,
                    buffer = m_Buffer,
                    source = m_Source,
                    leftCount = sortedCount,
                    rightCount = remainder-sortedCount,
                    outputBuffer = outputBuffer
                };
                
                // There's no overlap, but write to the same array, so extra dependency:
                var mergeRemainderPairJobHandle = mergeRemainderPairJob.Schedule(mergeSortedPairsJobHandle);
                return mergeRemainderPairJobHandle;
            }
            
            if (remainder > 0)
            {
                var copyRemainderPairJob = new CopyRemainder
                {
                    startIndex = pairCount * sortedCount * 2,
                    buffer = m_Buffer,
                    source = m_Source,
                    outputBuffer = outputBuffer
                };
                
                // There's no overlap, but write to the same array, so extra dependency:
                var copyRemainderPairJobHandle = copyRemainderPairJob.Schedule(remainder,64,mergeSortedPairsJobHandle);
                return copyRemainderPairJobHandle;
            }
            
            return mergeSortedPairsJobHandle;
        }
        
        struct AssignSharedValues : IJob
        {
            public NativeArray<int> buffer;
            [ReadOnly] public NativeArray<T> source;
            public int sortedBuffer;
            
            public void Execute()
            {
                int sortedIndicesOffset = sortedBuffer * source.Length;
                int sharedValueIndicesOffset = (sortedBuffer ^ 1) * source.Length;
                int sharedValueIndexCountOffset = 2 * source.Length;
                int sharedValueStartIndicesOffset = 3 * source.Length;
                int sharedValueCountOffset = 4 * source.Length;
                
                int index = 0;
                int valueIndex = buffer[sortedIndicesOffset + index];
                var sharedValue = source[valueIndex];
                int sharedValueCount = 1;
                buffer[sharedValueIndicesOffset+valueIndex] = 0;
                buffer[sharedValueStartIndicesOffset + (sharedValueCount-1)] = index;
                buffer[sharedValueIndexCountOffset + (sharedValueCount - 1)] = 1;
                index++;

                while (index < source.Length)
                {
                    valueIndex = buffer[sortedIndicesOffset + index];
                    var value = source[valueIndex];
                    if (value.CompareTo(sharedValue) != 0)
                    {
                        sharedValueCount++;
                        sharedValue = value;
                        buffer[sharedValueStartIndicesOffset + (sharedValueCount-1)] = index;
                        buffer[sharedValueIndexCountOffset + (sharedValueCount - 1)] = 1;
                        buffer[sharedValueIndicesOffset + valueIndex] = sharedValueCount - 1;
                    }
                    else
                    {
                        buffer[sharedValueIndexCountOffset + (sharedValueCount - 1)]++;
                        buffer[sharedValueIndicesOffset + valueIndex] = sharedValueCount - 1;
                    }
                    
                    index++;
                }

                buffer[sharedValueCountOffset] = sharedValueCount;
            }
        }
        
        JobHandle Sort(JobHandle inputDeps)
        {
            int sortedCount = 1;
            int outputBuffer = 1;
            do
            {
                inputDeps = MergeSortedLists(inputDeps, sortedCount, outputBuffer);
                sortedCount *= 2;
                outputBuffer ^= 1;
            } while (sortedCount < m_Source.Length);
            m_SortedBuffer = outputBuffer ^ 1;

            return inputDeps;
        }

        JobHandle ResolveSharedGroups(JobHandle inputDeps)
        {
            var assignSharedValuesJob = new AssignSharedValues
            {
                buffer = m_Buffer,
                source = m_Source,
                sortedBuffer = m_SortedBuffer
            };
            var assignSharedValuesJobHandle = assignSharedValuesJob.Schedule(inputDeps);
            return assignSharedValuesJobHandle;
        }
            
        public JobHandle Schedule(JobHandle inputDeps)
        {
            var initializeIndicesJob = new InitializeIndices
            {
                buffer = m_Buffer
            };
            var initializeIndicesJobHandle = initializeIndicesJob.Schedule(m_Source.Length, 64, inputDeps);
            var sortJobHandle = Sort(initializeIndicesJobHandle);
            var resolveSharedGroupsJobHandle = ResolveSharedGroups(sortJobHandle);
            return resolveSharedGroupsJobHandle;
        }

        public unsafe NativeArray<int> GetSortedIndices()
        {
            int* rawIndices = ((int*) m_Buffer.GetUnsafePtr()) + (m_SortedBuffer * m_Source.Length);
            var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(rawIndices,m_Source.Length,Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(m_Buffer));
#endif
            return arr;
        }

        public int SharedValueCount => m_Buffer[m_Source.Length * 4];
        
        public NativeArray<int> GetSharedValueIndicesBySourceIndex(int index)
        {
            int sharedValueIndicesOffset = (m_SortedBuffer^ 1) * m_Source.Length;
            int sharedValueIndex = m_Buffer[sharedValueIndicesOffset + index];
            return GetSharedValueIndicesBySharedValueIndex(sharedValueIndex);
        }
        
        public unsafe NativeArray<int> GetSharedValueIndicesBySharedValueIndex(int index)
        {
            int sharedValueIndexCountOffset = 2 * m_Source.Length;
            int sharedValueIndexCount = m_Buffer[sharedValueIndexCountOffset + index];
            int sharedValueStartIndicesOffset = 3 * m_Source.Length;
            int sharedValueStartIndex = m_Buffer[sharedValueStartIndicesOffset + index];
            int sortedValueOffset = m_SortedBuffer * m_Source.Length;

            int* rawIndices = ((int*) m_Buffer.GetUnsafePtr()) + (sortedValueOffset + sharedValueStartIndex);
            var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(rawIndices,sharedValueIndexCount,Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(m_Buffer));
#endif
            return arr;
        }

        public void Dispose()
        {
            m_Buffer.Dispose();
        }
    }
}
