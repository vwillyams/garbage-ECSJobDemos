using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Threading;

namespace UnityEngine.Collections
{
	public struct NativeMultiHashMapIterator<TKey>
		where TKey: struct
	{
		internal TKey key;
		internal int NextEntryIndex;
		//@TODO: Make unnecessary, is only used by SetValue API...
		internal int EntryIndex;
	}

	[StructLayout (LayoutKind.Sequential)]
	internal unsafe struct NativeHashMapData
	{
		public System.IntPtr	values;
		public System.IntPtr	keys;
		public System.IntPtr	next;
		public System.IntPtr	buckets;
		public int				capacity;
		public int				bucketCapacity;
		// Add padding between fields to ensure they are on separate cache-lines
		// FIXME: p2gc does not like fixed arrays in structs, so expand them manually
		//private fixed byte 		padding1[60];
		private int padding1_01;
		private int padding1_02;
		private int padding1_03;
		private int padding1_04;
		private int padding1_05;
		private int padding1_06;
		private int padding1_07;
		private int padding1_08;
		private int padding1_09;
		private int padding1_10;
		private int padding1_11;
		private int padding1_12;
		private int padding1_13;
		private int padding1_14;
		private int padding1_15;
		public int				firstFree;
		public int				approximateFreeListSize;
		// FIXME: p2gc does not like fixed arrays in structs, so expand them manually
		//private fixed byte 		padding2[60];
		private int padding2_01;
		private int padding2_02;
		private int padding2_03;
		private int padding2_04;
		private int padding2_05;
		private int padding2_06;
		private int padding2_07;
		private int padding2_08;
		private int padding2_09;
		private int padding2_10;
		private int padding2_11;
		private int padding2_12;
		private int padding2_13;
		private int padding2_14;
		private int padding2_15;
		public int				allocatedIndexLength;

		// 64 is the cache line size on x86, arm usually has 32 - so it is possible to save some memory there
		private const int cacheLineSize = 64;
		private const int bucketSizeMultiplier = 2;

		public static int GetBucketSize(int capacity)
		{
			return capacity * 2;
		}

		public static int GrowCapacity(int capacity)
		{
			if (capacity == 0)
				return 1;
			return capacity * 2;
		}


		public unsafe static void AllocateHashMap<TKey, TValue>(int length, int bucketLength, Allocator label, out IntPtr outBuf)
			where TKey : struct
			where TValue : struct
		{
			outBuf = UnsafeUtility.Malloc (sizeof(NativeHashMapData), UnsafeUtility.AlignOf<NativeHashMapData>(), label);

			NativeHashMapData* data = (NativeHashMapData*)outBuf;

			data->capacity = length;
			data->bucketCapacity = bucketLength;

			int keyOffset, nextOffset, bucketOffset;
			int totalSize = CalculateDataSize<TKey, TValue>(length, bucketLength, out keyOffset, out nextOffset, out bucketOffset);
			
			data->values = UnsafeUtility.Malloc (totalSize, cacheLineSize, label);
			data->keys = (IntPtr)((byte*)data->values + keyOffset);
			data->next = (IntPtr)((byte*)data->values + nextOffset);
			data->buckets = (IntPtr)((byte*)data->values + bucketOffset);
		}
		public unsafe static void ReallocateHashMap<TKey, TValue>(NativeHashMapData* data, int newCapacity, int newBucketCapacity, Allocator label)
			where TKey : struct
			where TValue : struct
		{
			if (data->capacity == newCapacity && data->bucketCapacity == newBucketCapacity)
				return;

			if (data->capacity > newCapacity)
				throw new System.Exception("Shrinking a hash map is not supported");
		
			int keyOffset, nextOffset, bucketOffset;
			int totalSize = CalculateDataSize<TKey, TValue>(newCapacity, newBucketCapacity, out keyOffset, out nextOffset, out bucketOffset);
			
			IntPtr newData = UnsafeUtility.Malloc (totalSize, cacheLineSize, label);
			IntPtr newKeys = (IntPtr)((byte*)newData + keyOffset);
			IntPtr newNext = (IntPtr)((byte*)newData + nextOffset);
			IntPtr newBuckets = (IntPtr)((byte*)newData + bucketOffset);

			// The items are taken from a free-list and might not be tightly packed, copy all of the old capcity
			UnsafeUtility.MemCpy (newData, data->values, data->capacity * UnsafeUtility.SizeOf<TValue>());
			UnsafeUtility.MemCpy (newKeys, data->keys, data->capacity * UnsafeUtility.SizeOf<TKey>());
			UnsafeUtility.MemCpy (newNext, data->next, data->capacity * UnsafeUtility.SizeOf<int>());
			for (int emptyNext = data->capacity; emptyNext < newCapacity; ++emptyNext)
				((int*)newNext)[emptyNext] = -1;

			// re-hash the buckets, first clear the new bucket list, then insert all values from the old list
			for (int bucket = 0; bucket < newBucketCapacity; ++bucket)
				((int*)newBuckets)[bucket] = -1;
			for (int bucket = 0; bucket < data->bucketCapacity; ++bucket)
			{
				int* buckets = (int*)data->buckets;
				int* nextPtrs = (int*)newNext;
				while (buckets[bucket] >= 0)
				{
					int curEntry = buckets[bucket];
					buckets[bucket] = nextPtrs[curEntry];
					int newBucket = Math.Abs(UnsafeUtility.ReadArrayElement<TKey> (data->keys, curEntry).GetHashCode()) % newBucketCapacity;
					nextPtrs[curEntry] = ((int*)newBuckets)[newBucket];
					((int*)newBuckets)[newBucket] = curEntry;
				}
			}

			UnsafeUtility.Free (data->values, label);
			if (data->allocatedIndexLength > data->capacity)
				data->allocatedIndexLength = data->capacity;
			data->values = newData;
			data->keys = newKeys;
			data->next = newNext;
			data->buckets = newBuckets;
			data->capacity = newCapacity;
			data->bucketCapacity = newBucketCapacity;
		}
		public unsafe static void DeallocateHashMap(IntPtr buffer, Allocator allocation)
		{
			NativeHashMapData* data = (NativeHashMapData*)buffer;
			UnsafeUtility.Free (data->values, allocation);
			data->values = IntPtr.Zero;
			data->keys = IntPtr.Zero;
			data->next = IntPtr.Zero;
			data->buckets = IntPtr.Zero;
			UnsafeUtility.Free (buffer, allocation);
		}
        static private int CalculateDataSize<TKey, TValue>(int length, int bucketLength, out int keyOffset, out int nextOffset, out int bucketOffset)
			where TKey : struct
			where TValue : struct
		{
			int elementSize = UnsafeUtility.SizeOf<TValue> ();
			int keySize = UnsafeUtility.SizeOf<TKey> ();

			// Offset is rounded up to be an even cacheLineSize
			keyOffset = (elementSize * length + cacheLineSize - 1);
			keyOffset -= keyOffset % cacheLineSize;

			nextOffset = (keyOffset + keySize * length + cacheLineSize - 1);
			nextOffset -= nextOffset % cacheLineSize;

			bucketOffset = (nextOffset + UnsafeUtility.SizeOf<int>() * length + cacheLineSize - 1);
			bucketOffset -= bucketOffset % cacheLineSize;

			int totalSize = bucketOffset + UnsafeUtility.SizeOf<int>() * bucketLength;
			return totalSize;			
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	internal struct NativeHashMapBase<TKey, TValue>
		where TKey : struct, System.IEquatable<TKey>
		where TValue : struct
	{
		static unsafe public void Clear(NativeHashMapData* data)
		{
			int* buckets = (int*)data->buckets;
			for (int i = 0; i < data->bucketCapacity; ++i)
				buckets[i] = -1;
			int* nextPtrs = (int*)data->next;
			for (int i = 0; i < data->capacity; ++i)
				nextPtrs[i] = -1;
			data->firstFree = -1;
			data->approximateFreeListSize = 0;
			data->allocatedIndexLength = 0;
		}
		
		static unsafe private int AllocEntry(NativeHashMapData* data)
		{
			int idx;
			int* nextPtrs = (int*)data->next;
			// Try once to get an item from the free list
			idx = data->firstFree;
			if (idx >= 0 && Interlocked.CompareExchange(ref data->firstFree, nextPtrs[idx], idx) == idx)
			{
				Interlocked.Decrement(ref data->approximateFreeListSize);
				nextPtrs[idx] = -1;
				return idx;
			}
			// If it failed try to get one from the never-allocated array
			if (data->allocatedIndexLength < data->capacity)
			{
				idx = Interlocked.Increment(ref data->allocatedIndexLength)-1;
				if (idx < data->capacity)
					return idx;
			}
			// If that also failed hammer on the free list until one is found
			do
			{
				idx = data->firstFree;
				if (idx < 0 || idx >= data->capacity)
					throw new System.InvalidOperationException("HashMap is full");
			}
			while (Interlocked.CompareExchange(ref data->firstFree, nextPtrs[idx], idx) != idx);
			Interlocked.Decrement(ref data->approximateFreeListSize);
			nextPtrs[idx] = -1;
			return idx;			
		}
		static unsafe public bool TryAddAtomic(NativeHashMapData* data, TKey key, TValue item, bool isMultiHashMap)
		{
			TValue tempItem;
			NativeMultiHashMapIterator<TKey> tempIt;
			if (!isMultiHashMap && TryGetFirstValueAtomic(data, key, out tempItem, out tempIt))
				return false;
			// Allocate an entry from the free list
			int idx = AllocEntry(data);

			// Write the new value to the entry
			UnsafeUtility.WriteArrayElement (data->keys, idx, key);
			UnsafeUtility.WriteArrayElement (data->values, idx, item);

			int bucket = Math.Abs(key.GetHashCode()) % data->bucketCapacity;
			// Add the index to the hash-map
			int* buckets = (int*)data->buckets;
			if (Interlocked.CompareExchange(ref buckets[bucket], idx, -1) != -1)
			{
				int* nextPtrs = (int*)data->next;
				do
				{
					nextPtrs[idx] = buckets[bucket];
					if (!isMultiHashMap && TryGetFirstValueAtomic(data, key, out tempItem, out tempIt))
					{
						// Put back the entry in the free list if someone else added it while trying to add
						do
						{
							nextPtrs[idx] = data->firstFree;
						} 
						while (Interlocked.CompareExchange(ref data->firstFree, idx, nextPtrs[idx]) != nextPtrs[idx]);
						Interlocked.Increment(ref data->approximateFreeListSize);

						return false;
					}
				}
				while (Interlocked.CompareExchange(ref buckets[bucket], idx, nextPtrs[idx]) != nextPtrs[idx]);
			}
			return true;
		}
		static unsafe public bool TryAdd(NativeHashMapData* data, TKey key, TValue item, bool isMultiHashMap, Allocator allocation)
		{
			TValue tempItem;
			NativeMultiHashMapIterator<TKey> tempIt;
			if (!isMultiHashMap && TryGetFirstValueAtomic(data, key, out tempItem, out tempIt))
				return false;
			// Allocate an entry from the free list
			int idx;

			if (data->allocatedIndexLength >= data->capacity && data->firstFree < 0)
			{
				int newCap = NativeHashMapData.GrowCapacity(data->capacity);
				NativeHashMapData.ReallocateHashMap<TKey, TValue>(data, newCap, NativeHashMapData.GetBucketSize(newCap), allocation);
			}
			idx = data->firstFree;
			if (idx >= 0)
			{
				data->firstFree = ((int*)data->next)[idx];
				--data->approximateFreeListSize;
			}
			else
				idx = data->allocatedIndexLength++;

			if (idx < 0 || idx >= data->capacity)
				throw new System.InvalidOperationException("Internal HashMap error");

			// Write the new value to the entry
			UnsafeUtility.WriteArrayElement (data->keys, idx, key);
			UnsafeUtility.WriteArrayElement (data->values, idx, item);

			int bucket = Math.Abs(key.GetHashCode()) % data->bucketCapacity;
			// Add the index to the hash-map
			int* buckets = (int*)data->buckets;
			int* nextPtrs = (int*)data->next;

			nextPtrs[idx] = buckets[bucket];
			buckets[bucket] = idx;

			return true;
		}

		static unsafe public void Remove(NativeHashMapData* data, TKey key, bool isMultiHashMap)
		{
			// First find the slot based on the hash
			int* buckets = (int*)data->buckets;
			int* nextPtrs = (int*)data->next;
			int bucket = Math.Abs(key.GetHashCode()) % data->bucketCapacity;

			int prevEntry = -1;
			int entryIdx = buckets[bucket];
			while (entryIdx >= 0 && entryIdx < data->capacity)
			{
				if (UnsafeUtility.ReadArrayElement<TKey> (data->keys, entryIdx).Equals(key))
				{
					// Found matching element, remove it
					if (prevEntry < 0)
						buckets[bucket] = nextPtrs[entryIdx];
					else
						nextPtrs[prevEntry] = nextPtrs[entryIdx];
					// And free the index
					int nextIdx = nextPtrs[entryIdx];
					++data->approximateFreeListSize;
					nextPtrs[entryIdx] = data->firstFree;
					data->firstFree = entryIdx;
					entryIdx = nextIdx;
					// Can only be one hit in regular hashmaps, so return
					if (!isMultiHashMap)
						return;
				}
				else
				{
					prevEntry = entryIdx;
					entryIdx = nextPtrs[entryIdx];
				}
			}
		}
		static unsafe public void Remove(NativeHashMapData* data, NativeMultiHashMapIterator<TKey> it)
		{
			// First find the slot based on the hash
			int* buckets = (int*)data->buckets;
			int* nextPtrs = (int*)data->next;
			int bucket = Math.Abs(it.key.GetHashCode()) % data->bucketCapacity;

			int prevEntry = -1;
			int entryIdx = buckets[bucket];
			if (entryIdx == it.EntryIndex)
			{
				buckets[bucket] = nextPtrs[entryIdx];
			}
			else
			{
				while (entryIdx >= 0 && nextPtrs[entryIdx] != it.EntryIndex)
					entryIdx = nextPtrs[entryIdx];
				if (entryIdx < 0)
					throw new InvalidOperationException("Invalid iterator passed to HashMap remove");
				nextPtrs[entryIdx] = nextPtrs[it.EntryIndex];
			}
			// And free the index
			++data->approximateFreeListSize;
			nextPtrs[it.EntryIndex] = data->firstFree;
			data->firstFree = it.EntryIndex;
		}

		static unsafe public bool TryGetFirstValueAtomic(NativeHashMapData* data, TKey key, out TValue item, out NativeMultiHashMapIterator<TKey> it)
		{
			it.key = key;
			if (data->allocatedIndexLength <= 0)
			{
				it.EntryIndex = it.NextEntryIndex = -1;
				item = default(TValue);
				return false;
			}
			// First find the slot based on the hash
			int* buckets = (int*)data->buckets;
			int bucket = Math.Abs(key.GetHashCode()) % data->bucketCapacity;
			it.EntryIndex = it.NextEntryIndex = buckets[bucket];
			return TryGetNextValueAtomic(data, out item, ref it);
		}
		
		static unsafe public bool TryGetNextValueAtomic(NativeHashMapData* data, out TValue item, ref NativeMultiHashMapIterator<TKey> it)
		{
			int entryIdx = it.NextEntryIndex;
			it.NextEntryIndex = -1;
			it.EntryIndex = -1;
			item = default(TValue);
			if (entryIdx < 0 || entryIdx >= data->capacity)
				return false;
			int* nextPtrs = (int*)data->next;
			while (!UnsafeUtility.ReadArrayElement<TKey> (data->keys, entryIdx).Equals(it.key))
			{
				entryIdx = nextPtrs[entryIdx];
				if (entryIdx < 0 || entryIdx >= data->capacity)
					return false;
			}
			it.NextEntryIndex = nextPtrs[entryIdx];
			it.EntryIndex = entryIdx;

			// Read the value
			item = UnsafeUtility.ReadArrayElement<TValue>(data->values, entryIdx);

			return true;
		}

		static unsafe public bool SetValue(NativeHashMapData* data, ref NativeMultiHashMapIterator<TKey> it, ref TValue item)
		{
			int entryIdx = it.EntryIndex;
			if (entryIdx < 0 || entryIdx >= data->capacity)
				return false;

			UnsafeUtility.WriteArrayElement(data->values, entryIdx, item);
			return true;
		}

	}

	[StructLayout (LayoutKind.Sequential)]
	[NativeContainer]
	public struct NativeHashMap<TKey, TValue>
		where TKey : struct, System.IEquatable<TKey>
		where TValue : struct
	{
		System.IntPtr 			m_Buffer;

		#if ENABLE_NATIVE_ARRAY_CHECKS
		AtomicSafetyHandle 		m_Safety;
		DisposeSentinel			m_DisposeSentinel;
		#endif

		Allocator 				m_AllocatorLabel;

		public unsafe NativeHashMap(int capacity, Allocator label)
		{
			m_AllocatorLabel = label;
			// Bucket size if bigger to reduce collisions
			NativeHashMapData.AllocateHashMap<TKey, TValue> (capacity, capacity*2, label, out m_Buffer);

			#if ENABLE_NATIVE_ARRAY_CHECKS
			DisposeSentinel.Create(m_Buffer, label, out m_Safety, out m_DisposeSentinel, 0, NativeHashMapData.DeallocateHashMap);
			#endif

			Clear();
		}

		unsafe public int Length
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
				return Math.Min(data->capacity, data->allocatedIndexLength) - data->approximateFreeListSize;
			}
		}
		unsafe public int Capacity
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
				return data->capacity;
			}
			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif

				NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
				NativeHashMapData.ReallocateHashMap<TKey, TValue>(data, value, NativeHashMapData.GetBucketSize(value), m_AllocatorLabel);
			}
		}

		unsafe public void Clear()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			NativeHashMapBase<TKey, TValue>.Clear((NativeHashMapData*)m_Buffer);
		}
		
		unsafe public bool TryAdd(TKey key, TValue item)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			return NativeHashMapBase<TKey, TValue>.TryAdd((NativeHashMapData*)m_Buffer, key, item, false, m_AllocatorLabel);
		}

		unsafe public void Remove(TKey key)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			NativeHashMapBase<TKey, TValue>.Remove((NativeHashMapData*)m_Buffer, key, false);
		}
		
		unsafe public bool TryGetValue(TKey key, out TValue item)
		{
			NativeMultiHashMapIterator<TKey> tempIt;
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			#endif
			return NativeHashMapBase<TKey, TValue>.TryGetFirstValueAtomic((NativeHashMapData*)m_Buffer, key, out item, out tempIt);
		}

		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		public void Dispose()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS            
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
			#endif

			NativeHashMapData.DeallocateHashMap(m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}

		[NativeContainer]
		[NativeContainerIsAtomicWriteOnly]
		public struct Concurrent
		{
			IntPtr 	m_Buffer;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle m_Safety;
			#endif

			unsafe public static implicit operator NativeHashMap<TKey, TValue>.Concurrent (NativeHashMap<TKey, TValue> hashMap)
			{
				NativeHashMap<TKey, TValue>.Concurrent concurrent;
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(hashMap.m_Safety);
				concurrent.m_Safety = hashMap.m_Safety;
				AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
				#endif

				concurrent.m_Buffer = hashMap.m_Buffer;
				return concurrent;
			}

			unsafe public int Capacity
			{
				get
				{
					#if ENABLE_NATIVE_ARRAY_CHECKS
					AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
					#endif

					NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
					return data->capacity;
				}
			}

			unsafe public bool TryAdd(TKey key, TValue item)
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif
				return NativeHashMapBase<TKey, TValue>.TryAddAtomic((NativeHashMapData*)m_Buffer, key, item, false);
			}
		}
	}
		
	[StructLayout (LayoutKind.Sequential)]
	[NativeContainer]
	public struct NativeMultiHashMap<TKey, TValue>
		where TKey : struct, System.IEquatable<TKey>
		where TValue : struct
	{
		System.IntPtr 			m_Buffer;

		#if ENABLE_NATIVE_ARRAY_CHECKS
		AtomicSafetyHandle 		m_Safety;
		DisposeSentinel			m_DisposeSentinel;
		#endif

		Allocator 				m_AllocatorLabel;

		public NativeMultiHashMap(int capacity, Allocator label)
		{
			m_AllocatorLabel = label;
			// Bucket size if bigger to reduce collisions
			NativeHashMapData.AllocateHashMap<TKey, TValue> (capacity, capacity*2, label, out m_Buffer);

			#if ENABLE_NATIVE_ARRAY_CHECKS
			DisposeSentinel.Create(m_Buffer, label, out m_Safety, out m_DisposeSentinel, 0, NativeHashMapData.DeallocateHashMap);
			#endif

			Clear();
		}

		unsafe public int Length
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
				return Math.Min(data->capacity, data->allocatedIndexLength) - data->approximateFreeListSize;
			}
		}
		unsafe public int Capacity
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
				return data->capacity;
			}
			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif

				NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
				NativeHashMapData.ReallocateHashMap<TKey, TValue>(data, value, NativeHashMapData.GetBucketSize(value), m_AllocatorLabel);
			}
		}

		unsafe public void Clear()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			NativeHashMapBase<TKey, TValue>.Clear((NativeHashMapData*)m_Buffer);
		}

		unsafe public void Add(TKey key, TValue item)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			NativeHashMapBase<TKey, TValue>.TryAdd((NativeHashMapData*)m_Buffer, key, item, true, m_AllocatorLabel);
		}

		unsafe public void Remove(TKey key)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			NativeHashMapBase<TKey, TValue>.Remove((NativeHashMapData*)m_Buffer, key, true);
		}
		unsafe public void Remove(NativeMultiHashMapIterator<TKey> it)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			NativeHashMapBase<TKey, TValue>.Remove((NativeHashMapData*)m_Buffer, it);
		}

		unsafe public bool TryGetFirstValue(TKey key, out TValue item, out NativeMultiHashMapIterator<TKey> it)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			#endif
			return NativeHashMapBase<TKey, TValue>.TryGetFirstValueAtomic((NativeHashMapData*)m_Buffer, key, out item, out it);
		}

		unsafe public bool TryGetNextValue(out TValue item, ref NativeMultiHashMapIterator<TKey> it)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			#endif
			return NativeHashMapBase<TKey, TValue>.TryGetNextValueAtomic((NativeHashMapData*)m_Buffer, out item, ref it);
		}

		unsafe public bool SetValue(TValue item, NativeMultiHashMapIterator<TKey> it)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			return NativeHashMapBase<TKey, TValue>.SetValue((NativeHashMapData*)m_Buffer, ref it, ref item);
		}


		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		unsafe public void Dispose()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS            
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
			#endif

			NativeHashMapData.DeallocateHashMap(m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}

		[NativeContainer]
		[NativeContainerIsAtomicWriteOnly]
		public struct Concurrent
		{
			IntPtr 	m_Buffer;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle m_Safety;
			#endif

			unsafe public static implicit operator NativeMultiHashMap<TKey, TValue>.Concurrent (NativeMultiHashMap<TKey, TValue> multiHashMap)
			{
				NativeMultiHashMap<TKey, TValue>.Concurrent concurrent;
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(multiHashMap.m_Safety);
				concurrent.m_Safety = multiHashMap.m_Safety;
				AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
				#endif

				concurrent.m_Buffer = multiHashMap.m_Buffer;
				return concurrent;
			}

			unsafe public int Capacity
			{
				get
				{
					#if ENABLE_NATIVE_ARRAY_CHECKS
					AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
					#endif

					NativeHashMapData* data = (NativeHashMapData*)m_Buffer;
					return data->capacity;
				}
			}

			unsafe public void Add(TKey key, TValue item)
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif
				NativeHashMapBase<TKey, TValue>.TryAddAtomic((NativeHashMapData*)m_Buffer, key, item, true);
			}
		}
	}
}
