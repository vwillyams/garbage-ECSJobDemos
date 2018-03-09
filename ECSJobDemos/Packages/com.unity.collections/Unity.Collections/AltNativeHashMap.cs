using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Packages.com.unity.collections.Unity.Collections
{
    [StructLayout (LayoutKind.Sequential)]
    unsafe struct Overflow
    {
        internal Overflow*  NextAllocated;
        internal Overflow*  NextOverflow;
        internal int    HashCount;
        internal int*   Hashes;
        internal int*  Values;
    }
    
    [NativeContainer]
    [StructLayout (LayoutKind.Sequential)]
    public unsafe struct AltNativeHashMap : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle 		m_Safety;
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel			m_DisposeSentinel;
#endif
        
        private Allocator  AllocatorLabel;
        private int        ValueCount;
        private int        BucketCount;
        private int        BucketValueCount;
        private int        BucketIndexMask;
        
        [NativeDisableUnsafePtrRestriction]
        private int*       BucketHashCount;
        
        [NativeDisableUnsafePtrRestriction]
        private Overflow** BucketOverflow;
        [NativeDisableUnsafePtrRestriction]
        private int*       Hashes;
        [NativeDisableUnsafePtrRestriction]
        private int*       Values;
        [NativeDisableUnsafePtrRestriction]
        private Overflow*  FirstOverflowBucket;
        
        public AltNativeHashMap(int bucketCount, int valueCount, Allocator allocatorLabel)
        {
            ValueCount           = math.ceil_pow2(valueCount);
            BucketCount          = math.ceil_pow2(bucketCount);
            BucketValueCount = 2; // (ValueCount / BucketCount)*2;
            BucketIndexMask      = BucketCount - 1;

            var bucketDataLength = BucketCount * BucketValueCount;
            
            BucketHashCount      = (int*)UnsafeUtility.Malloc(BucketCount * sizeof(int), JobsUtility.CacheLineSize, allocatorLabel);
            BucketOverflow       = (Overflow**)UnsafeUtility.Malloc(BucketCount * sizeof(Overflow*), JobsUtility.CacheLineSize, allocatorLabel);
            Hashes               = (int*)UnsafeUtility.Malloc(bucketDataLength * sizeof(int), JobsUtility.CacheLineSize, allocatorLabel);
            Values               = (int*)UnsafeUtility.Malloc(bucketDataLength * sizeof(int), JobsUtility.CacheLineSize, allocatorLabel);
            
            FirstOverflowBucket  = null;
            AllocatorLabel       = allocatorLabel; 
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
            
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < BucketCount; i++)
            {
                BucketHashCount[i] = 0;
                BucketOverflow[i]  = null;
            }
        }

        public void Dispose()
        {
            Overflow* nextOverflowBucket = (Overflow*)FirstOverflowBucket;
            while (nextOverflowBucket != null)
            {
                UnsafeUtility.Free(nextOverflowBucket->Hashes,AllocatorLabel);
                UnsafeUtility.Free(nextOverflowBucket->Values,AllocatorLabel);
                
                var nextNextOverflowBucket = nextOverflowBucket->NextAllocated;
                
                UnsafeUtility.Free(nextOverflowBucket,AllocatorLabel);
                
                nextOverflowBucket = (Overflow*)nextNextOverflowBucket;
            }
            
            UnsafeUtility.Free(BucketHashCount,AllocatorLabel);
            UnsafeUtility.Free(BucketOverflow,AllocatorLabel);
            UnsafeUtility.Free(Hashes,AllocatorLabel);
            UnsafeUtility.Free(Values,AllocatorLabel);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
        }

        public bool TryGet(int hash, out int value)
        {
            var hashBucketIndex = hash & BucketIndexMask;
            var bucketHashCount = BucketHashCount[hashBucketIndex];
            
            value = 0;
            for (int i = bucketHashCount-1; i >=0; i--)
            {
                var hashIndex = (BucketValueCount * hashBucketIndex) + i;
                if (Hashes[hashIndex] == hash)
                {
                    value = Values[hashIndex];
                    return true;
                }
            }
            var nextBucketOverflow = (Overflow*)BucketOverflow[hashBucketIndex];
            while (nextBucketOverflow != null)
            {
                bucketHashCount = nextBucketOverflow->HashCount;
                for (int i = 0; i < bucketHashCount; i++)
                {
                    if (nextBucketOverflow->Hashes[i] == hash)
                    {
                        value = nextBucketOverflow->Values[i];
                        return true;
                    }
                }
                
                nextBucketOverflow = (Overflow*)nextBucketOverflow->NextOverflow;
            }
            return false;
        }

        public void Add(int hash, int value)
        {
            var hashBucketIndex = hash & BucketIndexMask;
            var bucketHashCount = BucketHashCount[hashBucketIndex];
            if (bucketHashCount < BucketValueCount)
            {
                var hashIndex = (BucketValueCount * hashBucketIndex) + bucketHashCount;
                Hashes[hashIndex] = hash;
                UnsafeUtility.WriteArrayElement(Values, hashIndex, value);
                
                BucketHashCount[hashBucketIndex]++;
                return;
            }
            Overflow* nextBucketOverflow = (Overflow*)BucketOverflow[hashBucketIndex];
            while (nextBucketOverflow != null)
            {
                bucketHashCount = nextBucketOverflow->HashCount;
                if (bucketHashCount < BucketValueCount)
                {
                    nextBucketOverflow->Hashes[bucketHashCount] = hash;
                    UnsafeUtility.WriteArrayElement(nextBucketOverflow->Values, bucketHashCount, value);
                    nextBucketOverflow->HashCount++;
                    return;
                }
                nextBucketOverflow = (Overflow*)nextBucketOverflow->NextOverflow;
            }
            nextBucketOverflow = (Overflow*)UnsafeUtility.Malloc(sizeof(Overflow), JobsUtility.CacheLineSize, AllocatorLabel);
            nextBucketOverflow->Hashes = (int*)UnsafeUtility.Malloc(BucketValueCount * sizeof(int), JobsUtility.CacheLineSize, AllocatorLabel);
            nextBucketOverflow->Values = (int*)UnsafeUtility.Malloc(BucketValueCount * sizeof(int), JobsUtility.CacheLineSize, AllocatorLabel);
            nextBucketOverflow->NextOverflow = BucketOverflow[hashBucketIndex];
            nextBucketOverflow->NextAllocated = FirstOverflowBucket;
            
            UnsafeUtility.WriteArrayElement(nextBucketOverflow->Values, 0, value);
            nextBucketOverflow->HashCount = 1;

            BucketOverflow[hashBucketIndex] = nextBucketOverflow;
            FirstOverflowBucket             = nextBucketOverflow;
        }
    }
}
