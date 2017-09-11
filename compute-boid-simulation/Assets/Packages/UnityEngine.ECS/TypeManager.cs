using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;

namespace UnityEngine.ECS
{
	public unsafe struct Chunk
	{
		public Archetype*   archetype;
        public IntPtr 		buffer;
//        object[]            componentArray;

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

		// TODO: preferred stride/stream layout
		// TODO: Linkage to other archetype via Add/Remove Component
		public Archetype* prevArchetype;
	}
	public unsafe class TypeManager : IDisposable
	{
		NativeMultiHashMap<uint, IntPtr>		m_TypeLookup;
		ChunkAllocator m_ArchetypeChunkAllocator;
		internal Archetype* m_LastArchetype;

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

            int bytesPerInstance = 0;
			for (int i = 0; i < count; ++i)
			{
                RealTypeManager.ComponentType cType = RealTypeManager.GetComponentType(typeIndices[i]);
                int sizeOf = cType.size;

                type->types[i] = typeIndices [i];
                type->offsets[i] = bytesPerInstance;
                type->sizeOfs[i] = sizeOf;

                bytesPerInstance += sizeOf;
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
    }



}