using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;

namespace UnityEngine.ECS
{
	public interface IManagedObjectModificationListener
	{
		void OnManagedObjectModified();
	}
	public unsafe struct Chunk
	{
		public Archetype*   archetype;
        public IntPtr 		buffer;

		public int managedArrayIndex;

        public int 		    count;
        public int 		    capacity;

		public Chunk*  	next;
	}

	public unsafe struct Archetype
	{
		public Chunk*  first;
		public Chunk*  last;

		public int entityCount;

		public int*    types;
		public int     typesCount;

		// Index matches archetype types
        public int*   		offsets;
        public int*         strides;
        public int*         sizeOfs;
		public int          bytesPerInstance;

		public int* managedArrayOffset;
		public int numManagedArrays;

		public int managedObjectListenerIndex;

		// TODO: preferred stride/stream layout
		// TODO: Linkage to other archetype via Add/Remove Component
		public Archetype* prevArchetype;
	}
	public unsafe class TypeManager : IDisposable
	{
		NativeMultiHashMap<uint, IntPtr>		m_TypeLookup;
		ChunkAllocator m_ArchetypeChunkAllocator;
		internal Archetype* m_LastArchetype;

		unsafe struct ManagedArrayStorage
		{
			// For patching when we start releasing chunks
			public Chunk* chunk;
			public object[] managedArray;
		}
		List<ManagedArrayStorage> m_ManagedArrays = new List<ManagedArrayStorage>();
		struct ManagedArrayListeners
		{
			public Archetype* archetype;
			public List<IManagedObjectModificationListener>[] managedObjectListeners;
		}
		List<ManagedArrayListeners> m_ManagedArrayListeners = new List<ManagedArrayListeners>();

		public TypeManager()
		{
			m_TypeLookup = new NativeMultiHashMap<uint, IntPtr>(256, Allocator.Persistent);
		}

		public void Dispose()
		{
			// Free all allocated chunks for all allocated archetypes
			while (m_LastArchetype != null)
			{
				while (m_LastArchetype->first != null)
				{
					Chunk* nextChunk = m_LastArchetype->first->next;
					UnsafeUtility.Free((IntPtr)m_LastArchetype->first, Allocator.Persistent);
					m_LastArchetype->first = nextChunk;
				}
				m_LastArchetype = m_LastArchetype->prevArchetype;
			}
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


		bool CompareArchetypeType(int* type1, int typeCount1, int* type2, int typeCount2)
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
		public Archetype* GetArchetype(int* typeIndices, int count, EntityGroupManager groupManager)
		{
			uint hash = HashUtility.fletcher32((ushort*)typeIndices, count*2);
			IntPtr typePtr;
			Archetype* type;
			NativeMultiHashMapIterator<uint> it;
			if (m_TypeLookup.TryGetFirstValue(hash, out typePtr, out it))
			{
				do
				{
					type = (Archetype*)typePtr;
					if (CompareArchetypeType(type->types, type->typesCount, typeIndices, count))
						return type;
				} while (m_TypeLookup.TryGetNextValue(out typePtr, ref it));
			}
			// This is a new archetype, allocate it and add it to the hash map
            type = (Archetype*)m_ArchetypeChunkAllocator.Allocate(sizeof(Archetype), 8);
			type->typesCount = count;
            type->types = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);

			type->entityCount = 0;

			// FIXME: proper alignment
			type->offsets = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);
			type->strides = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);
            type->sizeOfs = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);

			type->managedObjectListenerIndex = -1;
			type->numManagedArrays = 0;
			type->managedArrayOffset = null;
            int bytesPerInstance = 0;
			for (int i = 0; i < count; ++i)
			{
                RealTypeManager.ComponentType cType = RealTypeManager.GetComponentType(typeIndices[i]);
                int sizeOf = cType.size;

                type->types[i] = typeIndices [i];
                type->offsets[i] = bytesPerInstance;
                type->sizeOfs[i] = sizeOf;

                bytesPerInstance += sizeOf;

				if (!(cType.type is IComponentData))
					++type->numManagedArrays;
            }
			if (type->numManagedArrays > 0)
			{
				type->managedArrayOffset = (int*)m_ArchetypeChunkAllocator.Allocate (sizeof(int) * count, 4);
				int mi = 0;
				for (int i = 0; i < count; ++i)
				{
               		RealTypeManager.ComponentType cType = RealTypeManager.GetComponentType(typeIndices[i]);
					if (!(cType.type is IComponentData))
						type->managedArrayOffset[i] = mi++;
					else
						type->managedArrayOffset[i] = -1;
				}
			}
			for (int i = 0; i < count; ++i)
				type->strides[i] = bytesPerInstance;
            type->bytesPerInstance = bytesPerInstance;

			// Update the list of all created archetypes
			type->prevArchetype = m_LastArchetype;
			m_LastArchetype = type;

			type->first = null;
			type->last = null;

			m_TypeLookup.Add(hash, (IntPtr)type);

			groupManager.OnArchetypeAdded(type);

			return type;
		}

        public Chunk* AllocateChunk(Archetype* archetype)
        {
			const int chunkSize = 16 * 1024;
            IntPtr buffer = UnsafeUtility.Malloc(chunkSize, 64, Allocator.Persistent);

            var chunk = (Chunk*)buffer;
            chunk->archetype = archetype;
			int bufferOffset = (sizeof(Chunk) + 63) & ~63;
			chunk->buffer = (IntPtr)(((byte*)chunk) + bufferOffset);
			chunk->count = 0;
			int bufferSize = chunkSize - bufferOffset;
			chunk->capacity = bufferSize / archetype->bytesPerInstance;
			chunk->next = null;
			if (archetype->last != null)
				archetype->last->next = chunk;
			else
				archetype->first = chunk;
			archetype->last = chunk;
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
			return chunk;
        }

        public Chunk* GetChunkWithEmptySlots(Archetype* archetype)
        {
            Chunk* chunk = archetype->last;
            if (chunk == null || chunk->count == chunk->capacity)
                chunk = AllocateChunk (archetype);

            return chunk;
        }

        public static int AllocateIntoChunk (Chunk* chunk, int count, out int outIndex)
        {
            int allocatedCount = Math.Min(chunk->capacity - chunk->count, count);
            outIndex = chunk->count;
            chunk->count += allocatedCount;
			chunk->archetype->entityCount += allocatedCount;
            return allocatedCount;
        }

        public static int AllocateIntoChunk(Chunk* chunk)
        {
            Assertions.Assert.AreNotEqual(chunk->capacity, chunk->count);
			++chunk->archetype->entityCount;
            return chunk->count++;
        }


        /*public void DeallocateChunk(Chunk* chunk)
        {
            
        }*/

		public object GetManagedObject(Chunk* chunk, int type, int index)
		{
			int managedStart = chunk->archetype->managedArrayOffset[type] * chunk->capacity;
			return m_ManagedArrays[chunk->managedArrayIndex].managedArray[index + managedStart];
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