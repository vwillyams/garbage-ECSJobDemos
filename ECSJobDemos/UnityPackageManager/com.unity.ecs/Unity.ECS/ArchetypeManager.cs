using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        public int FixedArrayLength;

        public bool IsFixedArray 			   { get { return FixedArrayLength != -1; } }
        public int  FixedArrayLengthMultiplier { get { return FixedArrayLength != -1 ? FixedArrayLength : 1; } }

        public ComponentTypeInArchetype(ComponentType type)
        {
            typeIndex = type.typeIndex;
            FixedArrayLength = type.FixedArrayLength;
        }

        static public bool operator ==(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
        {
            return lhs.typeIndex == rhs.typeIndex && lhs.FixedArrayLength == rhs.FixedArrayLength;
        }
        static public bool operator !=(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
        {
            return lhs.typeIndex != rhs.typeIndex || lhs.FixedArrayLength != rhs.FixedArrayLength;
        }
        static public bool operator <(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
        {
            return lhs.typeIndex != rhs.typeIndex ? lhs.typeIndex < rhs.typeIndex : lhs.FixedArrayLength < rhs.FixedArrayLength;
        }
        static public bool operator >(ComponentTypeInArchetype lhs, ComponentTypeInArchetype rhs)
        {
            return lhs.typeIndex != rhs.typeIndex ? lhs.typeIndex > rhs.typeIndex : lhs.FixedArrayLength > rhs.FixedArrayLength;
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

        public ComponentType ToComponentType()
        {
            ComponentType type;
            type.FixedArrayLength = FixedArrayLength;
            type.typeIndex = typeIndex;
            type.accessMode = ComponentType.AccessMode.ReadWrite;
            return type;
        }
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public override string ToString()
        {
            return ToComponentType().ToString();
        }
#endif
        public override bool Equals(object obj)
        {
            if (obj is ComponentTypeInArchetype)
            {
                return (ComponentTypeInArchetype) obj == this;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (typeIndex * 5819) ^ FixedArrayLength;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct Chunk
    {
        // NOTE: Order of the UnsafeLinkedListNode is required to be this in order
        //       to allow for casting & grabbing Chunk* from nodes...
        public UnsafeLinkedListNode  chunkListNode;
        public UnsafeLinkedListNode  chunkListWithEmptySlotsNode;

        public Archetype*            archetype;
        public int* 		         sharedComponentValueArray;

        // This is meant as read-only.
        // ArchetypeManager.SetChunkCount should be used to change the count.
        public int 		             count;
        public int 		             capacity;

        public int                   managedArrayIndex;
        
        public int                   padding0;
        
        // Component data buffer
        public fixed byte 		     buffer[4];
        
        
        public const int kChunkSize = 16 * 1024;
        public const int kMaximumEntitiesPerChunk = kChunkSize / 8;

        public static int GetChunkBufferSize(int numSharedComponents)
        {
            int bufferSize = Chunk.kChunkSize - (sizeof(Chunk) - 4 + numSharedComponents * sizeof(int));
            return bufferSize;
        }

        public static int GetSharedComponentOffset(int numSharedComponents)
        {
            return Chunk.kChunkSize - numSharedComponents * sizeof(int);
        }
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
        SharedComponentDataManager          m_SharedComponentManager;
        internal Archetype*                 m_LastArchetype;

        UnsafeLinkedListNode*               m_EmptyChunkPool;


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

        public ArchetypeManager(SharedComponentDataManager sharedComponentManager)
        {
            m_SharedComponentManager = sharedComponentManager;
            m_SharedComponentManager.Retain();
            m_TypeLookup = new NativeMultiHashMap<uint, IntPtr>(256, Allocator.Persistent);

            m_EmptyChunkPool = (UnsafeLinkedListNode*)m_ArchetypeChunkAllocator.Allocate(sizeof(UnsafeLinkedListNode), UnsafeUtility.AlignOf<UnsafeLinkedListNode>());
            UnsafeLinkedListNode.InitializeList(m_EmptyChunkPool);

            // Buffer should be 16 byte aligned to ensure component data layout itself can gurantee being aligned
            int offset = UnsafeUtility.GetFieldOffset(typeof(Chunk).GetField("buffer"));
            Assert.AreEqual(0, offset % 16);
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

            m_TypeLookup.Dispose();
            m_ArchetypeChunkAllocator.Dispose();
            m_SharedComponentManager.Release();
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

        public Archetype* GetExistingArchetype(ComponentTypeInArchetype* types, int count)
        {
            IntPtr typePtr;
            Archetype* type;
            NativeMultiHashMapIterator<uint> it;
            if (m_TypeLookup.TryGetFirstValue(GetHash(types, count), out typePtr, out it))
            {
                do
                {
                    type = (Archetype*)typePtr;
                    if (ComponentTypeInArchetype.CompareArray(type->types, type->typesCount, types, count))
                        return type;
                }
                while (m_TypeLookup.TryGetNextValue(out typePtr, ref it));
            }

            return null;
        }

        static uint GetHash(ComponentTypeInArchetype* types, int count)
        {
            uint hash = HashUtility.fletcher32((ushort*)types, count * sizeof(ComponentTypeInArchetype) / sizeof(ushort));
            return hash;
        }

        public Archetype* GetOrCreateArchetype(ComponentTypeInArchetype* types, int count, EntityGroupManager groupManager)
        {
            Archetype* type = GetExistingArchetype(types, count);
            if (type != null)
                return type;

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

            int chunkDataSize = Chunk.GetChunkBufferSize(type->numSharedComponents);

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
            Assert.IsTrue(Chunk.kMaximumEntitiesPerChunk >= type->chunkCapacity);
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

            m_TypeLookup.Add(GetHash(types, count), (IntPtr)type);

            groupManager.OnArchetypeAdded(type);

            return type;
        }
	    
        public static Chunk* GetChunkFromEmptySlotNode(UnsafeLinkedListNode* node)
        {
            return (Chunk*) (node - 1);
        }

        unsafe public Chunk* AllocateChunk(Archetype* archetype, int* sharedComponentDataIndices)
        {
            byte* buffer = (byte*) UnsafeUtility.Malloc(Chunk.kChunkSize, 64, Allocator.Persistent);
            var chunk = (Chunk*)buffer;
            ConstructChunk(archetype, chunk, sharedComponentDataIndices);
            return chunk;
        }

        public static void CopySharedComponentDataIndexArray(int* dest, int* src, int count)
        {
            if (src == null)
            {
                for (int i = 0; i < count; ++i)
                {
                    dest[i] = 0;
                }
            }
            else
            {
                for (int i = 0; i < count; ++i)
                {
                    dest[i] = src[i];
                }
            }
        }

        unsafe public void ConstructChunk(Archetype* archetype, Chunk* chunk, int* sharedComponentDataIndices)
        {
            chunk->archetype = archetype;

            chunk->count = 0;
            chunk->capacity = archetype->chunkCapacity;
            chunk->chunkListNode = new UnsafeLinkedListNode();
            chunk->chunkListWithEmptySlotsNode = new UnsafeLinkedListNode();
            chunk->sharedComponentValueArray = (int*)((byte*)(chunk) + Chunk.GetSharedComponentOffset(archetype->numSharedComponents));
	        
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
                int* sharedComponentValueArray = chunk->sharedComponentValueArray;
                CopySharedComponentDataIndexArray(sharedComponentValueArray, sharedComponentDataIndices, chunk->archetype->numSharedComponents);

                if (sharedComponentDataIndices != null)
                {
                    for (int i = 0; i < archetype->numSharedComponents; ++i)
                    {
                        m_SharedComponentManager.AddReference(sharedComponentValueArray[i]);
                    }
                }
            }
        }

        bool ChunkHasSharedComponents(Chunk* chunk, int* sharedComponentDataIndices)
        {
            int* sharedComponentValueArray = chunk->sharedComponentValueArray;
            int numSharedComponents = chunk->archetype->numSharedComponents;
            if (sharedComponentDataIndices == null)
            {
                for (int i = 0; i < numSharedComponents; ++i)
                {
                    if (sharedComponentValueArray[i] != 0)
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int i = 0; i < numSharedComponents; ++i)
                {
                    if (sharedComponentValueArray[i] != sharedComponentDataIndices[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public Chunk* GetChunkWithEmptySlots(Archetype* archetype, int* sharedComponentDataIndices)
        {
            // Try existing archetype chunks
            if (!archetype->chunkListWithEmptySlots.IsEmpty)
            {
                if (archetype->numSharedComponents == 0)
                {
                    var chunk = GetChunkFromEmptySlotNode(archetype->chunkListWithEmptySlots.Begin());
                    Assert.AreNotEqual(chunk->count, chunk->capacity);
                    return chunk;
                }

                var end = archetype->chunkListWithEmptySlots.End();
                for (var it = archetype->chunkListWithEmptySlots.Begin(); it != end; it = it->next)
                {
                    var chunk = GetChunkFromEmptySlotNode(it);
                    Assert.AreNotEqual(chunk->count, chunk->capacity);
                    if (ChunkHasSharedComponents(chunk, sharedComponentDataIndices))
                    {
                        return chunk;
                    }
                }
            }

            // Try empty chunk pool
            if (!m_EmptyChunkPool->IsEmpty)
            {
                Chunk* pooledChunk = (Chunk*)m_EmptyChunkPool->Begin();
                pooledChunk->chunkListNode.Remove();

                ConstructChunk(archetype, pooledChunk, sharedComponentDataIndices);
                return pooledChunk;
            }
            else
            {
                // Allocate new chunk
                return AllocateChunk(archetype, sharedComponentDataIndices);
            }
        }

        public int AllocateIntoChunk(Chunk* chunk)
        {
            int outIndex;
            int res = AllocateIntoChunk(chunk, 1, out outIndex);
            Assert.AreEqual(1, res);            
            return outIndex;
        }
        
        public int AllocateIntoChunk(Chunk* chunk, int count, out int outIndex)
        {
            int allocatedCount = Math.Min(chunk->capacity - chunk->count, count);
            outIndex = chunk->count;
            SetChunkCount(chunk, chunk->count + allocatedCount);
            chunk->archetype->entityCount += allocatedCount;
            return allocatedCount;
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
                    //Remove references to shared components
                    if (chunk->archetype->numSharedComponents > 0)
                    {
                        int* sharedComponentValueArray = chunk->sharedComponentValueArray;

                        for (int i = 0; i < chunk->archetype->numSharedComponents; ++i)
                        {
                            m_SharedComponentManager.RemoveReference(sharedComponentValueArray[i]);
                        }
                    }

                    chunk->archetype = null;
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

        public static void MoveChunks(ArchetypeManager srcArchetypeManager, EntityDataManager* srcEntityDataManager, ArchetypeManager dstArchetypeManager, EntityGroupManager dstGroupManager, SharedComponentDataManager dstSharedComponentDataManager, EntityDataManager* dstEntityDataManager)
        {
            var entitiesArray = new NativeArray<Entity>(Chunk.kMaximumEntitiesPerChunk, Allocator.Temp);
            var entitiesPtr = (Entity*) entitiesArray.GetUnsafePtr();

            var srcArchetype = srcArchetypeManager.m_LastArchetype;
            while (srcArchetype != null)
            {
                if (srcArchetype->entityCount != 0)
                {
                    if (srcArchetype->numManagedArrays != 0)
                        throw new System.ArgumentException("MoveEntitiesFrom is not supported with managed arrays");
                    Archetype* dstArchetype = dstArchetypeManager.GetOrCreateArchetype(srcArchetype->types, srcArchetype->typesCount, dstGroupManager);

                    for (var c = srcArchetype->chunkList.Begin();c != srcArchetype->chunkList.End();c = c->next)
                    {
                        Chunk* chunk = (Chunk*) c;

                        EntityDataManager.FreeDataEntitiesInChunk(srcEntityDataManager, chunk, chunk->count);
                        dstEntityDataManager->AllocateEntities(dstArchetype, chunk, 0, chunk->count, entitiesPtr);

                        chunk->archetype = dstArchetype;
                    }

                    //@TODO: Patch Entity references in IComponentData...

                    UnsafeLinkedListNode.InsertListBefore(dstArchetype->chunkList.End(), &srcArchetype->chunkList);
                    UnsafeLinkedListNode.InsertListBefore(dstArchetype->chunkListWithEmptySlots.End(), &srcArchetype->chunkListWithEmptySlots);

                    dstArchetype->entityCount += srcArchetype->entityCount;
                    srcArchetype->entityCount = 0;
                }

                srcArchetype = srcArchetype->prevArchetype;
            }
            entitiesArray.Dispose();
        }

        public int CheckInternalConsistency()
        {
            var archetype = m_LastArchetype;
            int totalCount = 0;
            while (archetype != null)
            {
                int countInArchetype = 0;
                for (var c = archetype->chunkList.Begin();c != archetype->chunkList.End();c = c->next)
                {
                    Chunk* chunk = (Chunk*) c;
                    Assert.IsTrue(chunk->archetype == archetype);
                    Assert.IsTrue(chunk->capacity >= chunk->count);
                    Assert.AreEqual(chunk->chunkListWithEmptySlotsNode.IsInList, chunk->capacity != chunk->count);

                    countInArchetype += chunk->count;
                }

                Assert.AreEqual(countInArchetype, archetype->entityCount);

                totalCount += countInArchetype;
                archetype = archetype->prevArchetype;
            }

            return totalCount;
        }

        internal SharedComponentDataManager GetSharedComponentDataManager()
        {
            return m_SharedComponentManager;
        }
    }
}
