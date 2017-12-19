using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
	interface IManagedObjectModificationListener
	{
		void OnManagedObjectModified();
	}

	struct ComponentTypeInArchetype
	{
		public int typeIndex;
		public int sharedComponentIndex;
		public int FixedArrayLength;

		public bool IsFixedArray 			   { get { return FixedArrayLength != -1; } }
		public int  FixedArrayLengthMultiplier { get { return FixedArrayLength != -1 ? FixedArrayLength : 1; } }

		public ComponentTypeInArchetype(ComponentType type)
		{
			typeIndex = type.typeIndex;
			sharedComponentIndex = type.sharedComponentIndex;
			FixedArrayLength = type.FixedArrayLength;
		}

		static public bool operator ==(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
		{
			return lhs.typeIndex == rhs.typeIndex && lhs.sharedComponentIndex == rhs.sharedComponentIndex &&  lhs.FixedArrayLength == rhs.FixedArrayLength;
		}
		static public bool operator !=(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
		{
			return lhs.typeIndex != rhs.typeIndex || lhs.sharedComponentIndex != rhs.sharedComponentIndex || lhs.FixedArrayLength != rhs.FixedArrayLength;
		}
		static public bool operator <(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
		{
			return lhs.typeIndex != rhs.typeIndex ? lhs.typeIndex < rhs.typeIndex : lhs.sharedComponentIndex < rhs.sharedComponentIndex;
		}
		static public bool operator >(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
		{
			return lhs.typeIndex != rhs.typeIndex ? lhs.typeIndex > rhs.typeIndex : lhs.sharedComponentIndex > rhs.sharedComponentIndex;
		}

		public static unsafe bool CompareArray(ComponentTypeInArchetype* type1, int typeCount1, ComponentTypeInArchetype* type2, int typeCount2)
		{
			if (typeCount1 != typeCount2)
				return false;
			for (int i = 0; i < typeCount1; ++i)
			{
				if (type1[i] != type2[i])
					return false;
			}
			return true;
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		public override string ToString()
		{
			ComponentType type;
			type.FixedArrayLength = FixedArrayLength;
			type.typeIndex = typeIndex;
			type.accessMode = ComponentType.AccessMode.ReadWrite;
			type.sharedComponentIndex = sharedComponentIndex;
			return type.ToString();
		}
#endif

	}

    unsafe struct Chunk
    {
        public const int kChunkSize = 16 * 1024;

        public UnsafeLinkedListNode  chunkListNode;
        public UnsafeLinkedListNode  chunkListWithEmptySlotsNode;

        public Archetype*      archetype;
        public byte* 		   buffer;

        public int             managedArrayIndex;

        // This is meant as read-only.
        // ArchetypeManager.SetChunkCount should be used to change the count.
        public int 		       count;
        public int 		       capacity;
    }

	unsafe struct Archetype
	{
	    public UnsafeLinkedListNode         chunkList;
	    public UnsafeLinkedListNode         chunkListWithEmptySlots;

		public int                          entityCount;
		public int                          chunkCapacity;

        public ComponentTypeInArchetype*    types;
        public int              		    typesCount;

		// Index matches archetype types
        public int*   	                    offsets;
        public int*                         sizeOfs;

        public int*                         managedArrayOffset;
		public int                          numManagedArrays;

	    public int*                         sharedComponentOffset;
	    public int                          numSharedComponents;
		public int                          managedObjectListenerIndex;

		public Archetype*                   prevArchetype;
	}

    unsafe class ArchetypeManager : IDisposable
	{
		NativeMultiHashMap<uint, IntPtr>    m_TypeLookup;
		ChunkAllocator                      m_ArchetypeChunkAllocator;
		internal Archetype*                 m_LastArchetype;

	    public UnsafeLinkedListNode*        m_EmptyChunkPool;


		unsafe struct ManagedArrayStorage
		{
			// For patching when we start releasing chunks
			public Chunk*    chunk;
			public object[]  managedArray;
		}
		List<ManagedArrayStorage> m_ManagedArrays = new List<ManagedArrayStorage>();
		struct ManagedArrayListeners
		{
			public Archetype* archetype;
			public List<IManagedObjectModificationListener>[] managedObjectListeners;
		}
		List<ManagedArrayListeners> m_ManagedArrayListeners = new List<ManagedArrayListeners>();

		public ArchetypeManager()
		{
			m_TypeLookup = new NativeMultiHashMap<uint, IntPtr>(256, Allocator.Persistent);
		    m_EmptyChunkPool = (UnsafeLinkedListNode*)UnsafeUtility.Malloc(sizeof(UnsafeLinkedListNode), UnsafeUtility.AlignOf<UnsafeLinkedListNode>(), Allocator.Persistent);
		    UnsafeLinkedListNode.InitializeList(m_EmptyChunkPool);
		}

		public void Dispose()
		{
			// Free all allocated chunks for all allocated archetypes
			while (m_LastArchetype != null)
			{
				while (!m_LastArchetype->chunkList.IsEmpty)
				{
				    var chunk = m_LastArchetype->chunkList.Begin();
				    chunk->Remove();
					UnsafeUtility.Free(chunk, Allocator.Persistent);
				}
				m_LastArchetype = m_LastArchetype->prevArchetype;
			}

		    // And all pooled chunks
		    while (!m_EmptyChunkPool->IsEmpty)
		    {
		        var chunk = m_EmptyChunkPool->Begin();
		        chunk->Remove();
		        UnsafeUtility.Free(chunk, Allocator.Persistent);
		    }

		    UnsafeUtility.Free(m_EmptyChunkPool, Allocator.Persistent);

			m_TypeLookup.Dispose();
			m_ArchetypeChunkAllocator.Dispose();
		}

		public unsafe void AddManagedObjectModificationListener(Archetype* archetype, int typeIdx, IManagedObjectModificationListener listener)
		{
			ManagedArrayListeners listeners;
			if (archetype->managedObjectListenerIndex < 0)
			{
				// Allocate new listener
				archetype->managedObjectListenerIndex = m_ManagedArrayListeners.Count;
				listeners = new ManagedArrayListeners();
				listeners.archetype = archetype;
				listeners.managedObjectListeners = new List<IManagedObjectModificationListener>[archetype->typesCount];
				m_ManagedArrayListeners.Add(listeners);
			}
			listeners = m_ManagedArrayListeners[archetype->managedObjectListenerIndex];
			if (listeners.managedObjectListeners[typeIdx] == null)
				listeners.managedObjectListeners[typeIdx] = new List<IManagedObjectModificationListener>();
			listeners.managedObjectListeners[typeIdx].Add(listener);
		}
		public unsafe void RemoveManagedObjectModificationListener(Archetype* archetype, int typeIdx, IManagedObjectModificationListener listener)
		{
			var listeners = m_ManagedArrayListeners[archetype->managedObjectListenerIndex];
			listeners.managedObjectListeners[typeIdx].Remove(listener);
		}

		[System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		public void AssertArchetypeComponents(ComponentTypeInArchetype* types, int count)
		{
			if (count < 1)
				throw new System.ArgumentException($"Invalid component count");
			if (types[0].typeIndex != TypeManager.GetTypeIndex<Entity>())
				throw new System.ArgumentException($"The Entity ID must always be the first component");

			for (int i = 1; i < count; i++)
			{
				if (!TypeManager.IsValidComponentTypeForArchetype(types[i].typeIndex, types[i].IsFixedArray))
					throw new ArgumentException($"{types[i]} is not a valid component type.");
				else if (types[i - 1].typeIndex == types[i].typeIndex)
					throw new ArgumentException($"It is not allowed to have two components of the same type on the same entity. ({types[i-1]} and {types[i]})");
			}
		}

        public Archetype* GetArchetype(ComponentTypeInArchetype* types, int count, EntityGroupManager groupManager, SharedComponentDataManager sharedComponentManager)
		{
			uint hash = HashUtility.fletcher32((ushort*)types, count * sizeof(ComponentTypeInArchetype) / sizeof(ushort));
			IntPtr typePtr;
			Archetype* type;
			NativeMultiHashMapIterator<uint> it;
			if (m_TypeLookup.TryGetFirstValue(hash, out typePtr, out it))
			{
				do
				{
					type = (Archetype*)typePtr;
					if (ComponentTypeInArchetype.CompareArray(type->types, type->typesCount, types, count))
						return type;
				} while (m_TypeLookup.TryGetNextValue(out typePtr, ref it));
			}

			AssertArchetypeComponents(types, count);

			// This is a new archetype, allocate it and add it to the hash map
            type = (Archetype*)m_ArchetypeChunkAllocator.Allocate(sizeof(Archetype), 8);
			type->typesCount = count;
            type->types = (ComponentTypeInArchetype*)m_ArchetypeChunkAllocator.Construct(sizeof(ComponentTypeInArchetype) * count, 4, types);
			type->entityCount = 0;

		    type->numSharedComponents = 0;
		    type->sharedComponentOffset = null;

		    for (int i = 0; i < count; ++i)
		    {
		        if (TypeManager.GetComponentType(types[i].typeIndex).category == TypeManager.TypeCategory.ISharedComponentData)
		            ++type->numSharedComponents;
		    }

            int chunkDataSize = GetChunkBufferSize(type->numSharedComponents);

            // FIXME: proper alignment
            type->offsets = (int*)m_ArchetypeChunkAllocator.Allocate(sizeof(int) * count, 4);
			type->sizeOfs = (int*)m_ArchetypeChunkAllocator.Allocate(sizeof(int) * count, 4);

            int bytesPerInstance = 0;

            for (int i = 0; i < count; ++i)
            {
                TypeManager.ComponentType cType = TypeManager.GetComponentType(types[i].typeIndex);
                int sizeOf = cType.sizeInChunk * types[i].FixedArrayLengthMultiplier;
                type->sizeOfs[i] = sizeOf;

                bytesPerInstance += sizeOf;
            }

            type->chunkCapacity = chunkDataSize / bytesPerInstance;

            int usedBytes = 0;
            for (int i = 0; i < count; ++i)
            {
                int sizeOf = type->sizeOfs[i];

                type->offsets[i] = usedBytes;

                usedBytes += sizeOf * type->chunkCapacity;
            }
            type->managedObjectListenerIndex = -1;
            type->numManagedArrays = 0;
            type->managedArrayOffset = null;

            for (int i = 0; i < count; ++i)
            {
                if (TypeManager.GetComponentType(types[i].typeIndex).category == TypeManager.TypeCategory.Class)
                    ++type->numManagedArrays;
            }

            if (type->numManagedArrays > 0)
			{
				type->managedArrayOffset = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);
				int mi = 0;
				for (int i = 0; i < count; ++i)
				{
               		TypeManager.ComponentType cType = TypeManager.GetComponentType(types[i].typeIndex);
					if (cType.category == TypeManager.TypeCategory.Class)
						type->managedArrayOffset[i] = mi++;
					else
						type->managedArrayOffset[i] = -1;
				}
			}



		    if (type->numSharedComponents > 0)
		    {
		        type->sharedComponentOffset = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);
		        int mi = 0;
		        for (int i = 0; i < count; ++i)
		        {
		            TypeManager.ComponentType cType = TypeManager.GetComponentType(types[i].typeIndex);
		            if (cType.category == TypeManager.TypeCategory.ISharedComponentData)
		                type->sharedComponentOffset[i] = mi++;
		            else
		                type->sharedComponentOffset[i] = -1;
		        }
		    }

			// Update the list of all created archetypes
			type->prevArchetype = m_LastArchetype;
			m_LastArchetype = type;

			UnsafeLinkedListNode.InitializeList(&type->chunkList);
		    UnsafeLinkedListNode.InitializeList(&type->chunkListWithEmptySlots);

			m_TypeLookup.Add(hash, (IntPtr)type);

			groupManager.OnArchetypeAdded(type);
            sharedComponentManager.OnArchetypeAdded(type->types, type->typesCount);

			return type;
		}

		static int GetBufferOffset(int numSharedComponents)
	    {
	        int headerSize = sizeof(Chunk) + numSharedComponents * sizeof(int);
	        return (headerSize + 63) & ~63;
	    }
        public static int GetChunkBufferSize(int numSharedComponents)
        {
            int bufferOffset = GetBufferOffset(numSharedComponents);
            int bufferSize = Chunk.kChunkSize - bufferOffset;

            return bufferSize;
        }

        public Chunk* AllocateChunk(Archetype* archetype)
        {
            IntPtr buffer = UnsafeUtility.Malloc(kChunkSize, 64, Allocator.Persistent);

            int bufferOffset = GetBufferOffset(archetype->numSharedComponents);

            var chunk = (Chunk*)buffer;
            chunk->archetype = archetype;

            chunk->buffer = (IntPtr)(((byte*)chunk) + bufferOffset);
            chunk->count = 0;
            chunk->capacity = archetype->chunkCapacity;
            chunk->chunkListNode = new UnsafeLinkedListNode();
            chunk->chunkListWithEmptySlotsNode = new UnsafeLinkedListNode();

            archetype->chunkList.Add(&chunk->chunkListNode);
            archetype->chunkListWithEmptySlots.Add(&chunk->chunkListWithEmptySlotsNode);

            Assert.IsTrue(!archetype->chunkList.IsEmpty);
            Assert.IsTrue(!archetype->chunkListWithEmptySlots.IsEmpty);

            Assert.IsTrue(chunk == (Chunk*)(archetype->chunkList.Back()));
            Assert.IsTrue(chunk == GetChunkFromEmptySlotNode(archetype->chunkListWithEmptySlots.Back()));

			if (archetype->numManagedArrays > 0)
			{
				chunk->managedArrayIndex = m_ManagedArrays.Count;
				var man = new ManagedArrayStorage();
				man.chunk = chunk;
				man.managedArray = new object[archetype->numManagedArrays * chunk->capacity];
				m_ManagedArrays.Add(man);
			}
			else
				chunk->managedArrayIndex = -1;


            if (archetype->numSharedComponents > 0)
            {
                int* sharedComponentValueArray = Chunk.GetSharedComponentValueArray(chunk);
                int numSharedComponents = chunk->archetype->numSharedComponents;
                for (int i = 0; i < numSharedComponents; ++i)
                {
                    sharedComponentValueArray[i] = 0;
                }
            }

			return chunk;
        }

	    bool ChuckHasDefaultSharedComponents(Chunk* chunk)
	    {
	        int* sharedComponentValueArray = Chunk.GetSharedComponentValueArray(chunk);
	        int numSharedComponents = chunk->archetype->numSharedComponents;
	        for (int i = 0; i < numSharedComponents; ++i)
	        {
	            if (sharedComponentValueArray[i] != 0)
	            {
	                return false;
	            }
	        }
	        return true;
	    }

        public Chunk* GetChunkWithEmptySlots(Archetype* archetype)
        {
            // Try existing archetype chunks
            if (!archetype->chunkListWithEmptySlots.IsEmpty)
            {
                var chunk = GetChunkFromEmptySlotNode(archetype->chunkListWithEmptySlots.Begin());
                Assert.AreNotEqual(chunk->count, chunk->capacity);
                return chunk;
            }
            // Try empty chunk pool
            else if (!m_EmptyChunkPool->IsEmpty)
            {
                Chunk* pooledChunk = (Chunk*)m_EmptyChunkPool->Begin();
                pooledChunk->chunkListNode.Remove();

                ConstructChunk(archetype, pooledChunk);
                return pooledChunk;
            }
            else
            {
                // Allocate new chunk
                return AllocateChunk (archetype);
            }
        }

        public int AllocateIntoChunk (Chunk* chunk, int count, out int outIndex)
        {
            int allocatedCount = Math.Min(chunk->capacity - chunk->count, count);
            outIndex = chunk->count;
            SetChunkCount(chunk, chunk->count + allocatedCount);
			chunk->archetype->entityCount += allocatedCount;
            return allocatedCount;
        }

        public int AllocateIntoChunk(Chunk* chunk)
        {
            Assertions.Assert.AreNotEqual(chunk->capacity, chunk->count);

            int chunkCount = chunk->count;
            SetChunkCount(chunk, chunkCount  + 1);

			++chunk->archetype->entityCount;
            return chunkCount;
        }

        public void SetChunkCount(Chunk* chunk, int newCount)
        {
            Assert.AreNotEqual(newCount, chunk->count);
            int capacity = chunk->capacity;

            // Chunk released to empty chunk pool
            if (newCount == 0)
            {
                //@TODO: Support pooling when there are managed arrays...
                if (chunk->archetype->numManagedArrays == 0)
                {
                    chunk->chunkListNode.Remove();
                    chunk->chunkListWithEmptySlotsNode.Remove();

                    m_EmptyChunkPool->Add(&chunk->chunkListNode);
                }
            }
            // Chunk is now full
            else if (newCount == capacity)
            {
                chunk->chunkListWithEmptySlotsNode.Remove();
            }
            // Chunk is no longer full
            else if (chunk->count == capacity)
            {
                Assert.IsTrue(newCount < chunk->count);

                chunk->archetype->chunkListWithEmptySlots.Add(&chunk->chunkListWithEmptySlotsNode);
            }

            chunk->count = newCount;
        }

        public object GetManagedObject(Chunk* chunk, ComponentType type, int index)
        {
            int typeOfs = ChunkDataUtility.GetIndexInTypeArray(chunk->archetype, type.typeIndex);
            if (typeOfs < 0 || chunk->archetype->managedArrayOffset[typeOfs] < 0)
                throw new InvalidOperationException("Trying to get managed object for non existing component");
            return GetManagedObject(chunk, typeOfs, index);
        }

        internal object GetManagedObject(Chunk* chunk, int type, int index)
		{
			int managedStart = chunk->archetype->managedArrayOffset[type] * chunk->capacity;
			return m_ManagedArrays[chunk->managedArrayIndex].managedArray[index + managedStart];
		}

		public object[] GetManagedObjectRange(Chunk* chunk, int type, out int rangeStart, out int rangeLength)
		{
			rangeStart = chunk->archetype->managedArrayOffset[type] * chunk->capacity;
			rangeLength = chunk->count;
			return m_ManagedArrays[chunk->managedArrayIndex].managedArray;
		}
		public void SetManagedObject(Chunk* chunk, int type, int index, object val)
		{
			int managedStart = chunk->archetype->managedArrayOffset[type] * chunk->capacity;
			m_ManagedArrays[chunk->managedArrayIndex].managedArray[index + managedStart] = val;

			if (chunk->archetype->managedObjectListenerIndex >= 0)
			{
				var listeners = m_ManagedArrayListeners[chunk->archetype->managedObjectListenerIndex];
				if (listeners.managedObjectListeners[type] != null)
				{
					foreach (var listener in listeners.managedObjectListeners[type])
						listener.OnManagedObjectModified();
				}
			}
		}
		public void SetManagedObject(Chunk* chunk, ComponentType type, int index, object val)
		{
			int typeOfs = ChunkDataUtility.GetIndexInTypeArray(chunk->archetype, type.typeIndex);
			if (typeOfs < 0 || chunk->archetype->managedArrayOffset[typeOfs] < 0)
				throw new InvalidOperationException("Trying to set managed object for non existing component");
			SetManagedObject(chunk, typeOfs, index, val);
		}
    }



}
