using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.ECS;

namespace Unity.ECS
{
    internal static unsafe class ChunkDataUtility
    {
        public static int GetIndexInTypeArray(Archetype* archetype, int typeIndex)
        {
            var types = archetype->types;
            var typeCount = archetype->typesCount;
            for (var i = 0; i != typeCount; i++)
            {
                if (typeIndex == types[i].typeIndex)
                    return i;
            }

            return -1;
        }

        public static void GetIndexInTypeArray(Archetype* archetype, int typeIndex, ref int typeLookupCache)
        {
            var types = archetype->types;
            var typeCount = archetype->typesCount;

            if (typeLookupCache < typeCount && types[typeLookupCache].typeIndex == typeIndex)
                return;

            for (var i = 0; i != typeCount; i++)
            {
                if (typeIndex != types[i].typeIndex)
                    continue;

                typeLookupCache = i;
                return;
            }

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            throw new System.InvalidOperationException("Shouldn't happen");
            #endif
        }

        public static void GetComponentDataWithTypeAndFixedArrayLength(Chunk* chunk, int index, int typeIndex, out byte* outPtr, out int outArrayLength)
        {
            var archetype = chunk->archetype;
            var indexInTypeArray = GetIndexInTypeArray(archetype, typeIndex);

            var offset = archetype->offsets[indexInTypeArray];
            var sizeOf = archetype->sizeOfs[indexInTypeArray];

            outPtr = chunk->buffer + (offset + sizeOf * index);
            outArrayLength = archetype->types[indexInTypeArray].FixedArrayLength;
        }

        public static byte* GetComponentDataWithType(Chunk* chunk, int index, int typeIndex, ref int typeLookupCache)
        {
            var archetype = chunk->archetype;
            GetIndexInTypeArray(archetype, typeIndex, ref typeLookupCache);
            var indexInTypeArray = typeLookupCache;

            var offset = archetype->offsets[indexInTypeArray];
            var sizeOf = archetype->sizeOfs[indexInTypeArray];

            return chunk->buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentDataWithType(Chunk* chunk, int index, int typeIndex)
        {
            var indexInTypeArray = GetIndexInTypeArray(chunk->archetype, typeIndex);

            var offset = chunk->archetype->offsets[indexInTypeArray];
            var sizeOf = chunk->archetype->sizeOfs[indexInTypeArray];

            return chunk->buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentData(Chunk* chunk, int index, int indexInTypeArray)
        {
            var offset = chunk->archetype->offsets[indexInTypeArray];
            var sizeOf = chunk->archetype->sizeOfs[indexInTypeArray];

            return chunk->buffer + (offset + sizeOf * index);
        }

        public static void Copy(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex, int count)
        {
            Assert.IsTrue(srcChunk->archetype == dstChunk->archetype);

            var arch = srcChunk->archetype;
            var srcBuffer = srcChunk->buffer;
            var dstBuffer = dstChunk->buffer;
            var offsets = arch->offsets;
            var sizeOfs = arch->sizeOfs;
            var typesCount = arch->typesCount;

            for (var t = 0; t < typesCount; t++)
            {
                var offset = offsets[t];
                var sizeOf = sizeOfs[t];
                var src = srcBuffer + (offset + sizeOf * srcIndex);
                var dst = dstBuffer + (offset + sizeOf * dstIndex);

                UnsafeUtility.MemCpy(dst, src, sizeOf * count);
            }
        }

        public static void ClearComponents(Chunk* dstChunk, int dstIndex, int count)
        {
            var arch = dstChunk->archetype;

            var offsets = arch->offsets;
            var sizeOfs = arch->sizeOfs;
            var dstBuffer = dstChunk->buffer;
            var typesCount = arch->typesCount;

            for (var t = 1; t != typesCount; t++)
            {
                var offset = offsets[t];
                var sizeOf = sizeOfs[t];
                var dst = dstBuffer + (offset + sizeOf * dstIndex);

                UnsafeUtility.MemClear(dst, sizeOf * count);
            }
        }

        public static void ReplicateComponents(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstBaseIndex, int count)
        {
            Assert.IsTrue(srcChunk->archetype == dstChunk->archetype);

            var arch = srcChunk->archetype;
            var srcBuffer = srcChunk->buffer;
            var dstBuffer = dstChunk->buffer;
            var offsets = arch->offsets;
            var sizeOfs = arch->sizeOfs;
            var typesCount = arch->typesCount;
            // type[0] is always Entity, and will be patched up later, so just skip
            for (var t = 1; t != typesCount; t++)
            {
                var offset = offsets[t];
                var sizeOf = sizeOfs[t];
                var src = srcBuffer + (offset + sizeOf * srcIndex);
                var dst = dstBuffer + (offset + sizeOf * dstBaseIndex);

                UnsafeUtility.MemCpyReplicate(dst, src, sizeOf, count);
            }
        }

        public static void Convert(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex)
        {
            var srcArch = srcChunk->archetype;
            var dstArch = dstChunk->archetype;

            var srcI = 0;
            var dstI = 0;
            while (srcI < srcArch->typesCount && dstI < dstArch->typesCount)
            {
                if (srcArch->types[srcI] < dstArch->types[dstI])
                    ++srcI;
                else if (srcArch->types[srcI] > dstArch->types[dstI])
                    ++dstI;
                else
                {
                    var src = srcChunk->buffer + srcArch->offsets[srcI] + srcIndex * srcArch->sizeOfs[srcI];
                    var dst = dstChunk->buffer + dstArch->offsets[dstI] + dstIndex * dstArch->sizeOfs[dstI];
                    UnsafeUtility.MemCpy(dst, src, srcArch->sizeOfs[srcI]);
                    ++srcI;
                    ++dstI;
                }
            }
        }

        public static void CopyManagedObjects(ArchetypeManager typeMan, Chunk* srcChunk, int srcStartIndex, Chunk* dstChunk, int dstStartIndex, int count)
        {
            var srcArch = srcChunk->archetype;
            var dstArch = dstChunk->archetype;

            var srcI = 0;
            var dstI = 0;
            while (srcI < srcArch->typesCount && dstI < dstArch->typesCount)
            {
                if (srcArch->types[srcI] < dstArch->types[dstI])
                    ++srcI;
                else if (srcArch->types[srcI] > dstArch->types[dstI])
                    ++dstI;
                else
                {
                    if (srcArch->managedArrayOffset[srcI] >= 0)
                    {
                        for (var i = 0; i < count; ++i)
                        {
                            var obj = typeMan.GetManagedObject(srcChunk, srcI, srcStartIndex+i);
                            typeMan.SetManagedObject(dstChunk, dstI, dstStartIndex+i, obj);
                        }
                    }
                    ++srcI;
                    ++dstI;
                }
            }
        }
        public static void ClearManagedObjects(ArchetypeManager typeMan, Chunk* chunk, int index, int count)
        {
            var arch = chunk->archetype;

            for (var type = 0; type < arch->typesCount; ++type)
            {
                if (arch->managedArrayOffset[type] < 0)
                    continue;

                for (var i = 0; i < count; ++i)
                    typeMan.SetManagedObject(chunk, type, index+i, null);
            }
        }
    }
}
