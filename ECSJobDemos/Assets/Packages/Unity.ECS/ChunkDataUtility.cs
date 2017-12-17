using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
    internal unsafe static class ChunkDataUtility
    {
        static public int GetIndexInTypeArray(Archetype* archetype, int typeIndex)
        {
            ComponentTypeInArchetype* types = archetype->types;
            int typeCount = archetype->typesCount;
            for (int i = 0; i != typeCount; i++)
            {
                if (typeIndex == types[i].typeIndex)
                    return i;
            }

            return -1;
        }

        public static void GetComponentDataWithTypeAndFixedArrayLength(Chunk* chunk, int index, int typeIndex, out byte* outPtr, out int outArrayLength)
        {
            int indexInTypeArray = GetIndexInTypeArray(chunk->archetype, typeIndex);

            int offset = chunk->archetype->offsets[indexInTypeArray];
            int sizeOf = chunk->archetype->sizeOfs[indexInTypeArray];

            outPtr = chunk->buffer + (offset + sizeOf * index);
            outArrayLength = chunk->archetype->types[indexInTypeArray].FixedArrayLength;
        }


        public static byte* GetComponentDataWithType(Chunk* chunk, int index, int typeIndex)
        {
            int indexInTypeArray = GetIndexInTypeArray(chunk->archetype, typeIndex);

            int offset = chunk->archetype->offsets[indexInTypeArray];
            int sizeOf = chunk->archetype->sizeOfs[indexInTypeArray];

            return chunk->buffer + (offset + sizeOf * index);
        }

        public static byte* GetComponentData(Chunk* chunk, int index, int indexInTypeArray)
        {
            int offset = chunk->archetype->offsets[indexInTypeArray];
            int sizeOf = chunk->archetype->sizeOfs[indexInTypeArray];

            return chunk->buffer + (offset + sizeOf * index);
        }

        public static void Copy(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex, int count)
        {
            Assert.IsTrue(srcChunk->archetype == dstChunk->archetype);

            Archetype* arch = srcChunk->archetype;
            byte* srcBuffer = srcChunk->buffer;
            byte* dstBuffer = dstChunk->buffer;
            int* offsets = arch->offsets;
            int* sizeOfs = arch->sizeOfs;
            int typesCount = arch->typesCount;

            for (int t = 0; t < typesCount; t++)
            {
                int offset = offsets[t];
                int sizeOf = sizeOfs[t];
                byte* src = srcBuffer + (offset + sizeOf * srcIndex);
                byte* dst = dstBuffer + (offset + sizeOf * dstIndex);

                UnsafeUtility.MemCpy(dst, src, sizeOf * count);
            }
        }

        public static void ClearComponents(Chunk* dstChunk, int dstIndex, int count)
        {
            Archetype* arch = dstChunk->archetype;

            int* offsets = arch->offsets;
            int* sizeOfs = arch->sizeOfs;
            byte* dstBuffer = dstChunk->buffer;
            int typesCount = arch->typesCount;

            for (int t = 1; t != typesCount; t++)
            {
                int offset = offsets[t];
                int sizeOf = sizeOfs[t];
                byte* dst = dstBuffer + (offset + sizeOf * dstIndex);

                UnsafeUtility.MemClear(dst, sizeOf * count);
            }
        }

        public static void ReplicateComponents(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstBaseIndex, int count)
        {
            Assert.IsTrue(srcChunk->archetype == dstChunk->archetype);

            Archetype* arch = srcChunk->archetype;
            byte* srcBuffer = srcChunk->buffer;
            byte* dstBuffer = dstChunk->buffer;
            int* offsets = arch->offsets;
            int* sizeOfs = arch->sizeOfs;
            int typesCount = arch->typesCount;
            // type[0] is always Entity, and will be patched up later, so just skip
            for (int t = 1; t != typesCount; t++)
            {
                int offset = offsets[t];
                int sizeOf = sizeOfs[t];
                byte* src = srcBuffer + (offset + sizeOf * srcIndex);
                byte* dst = dstBuffer + (offset + sizeOf * dstBaseIndex);

                UnsafeUtility.MemCpyReplicate(dst, src, sizeOf, count);
            }
        }

        public static void Convert(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex)
        {
            Archetype* srcArch = srcChunk->archetype;
            Archetype* dstArch = dstChunk->archetype;

            int srcI = 0;
            int dstI = 0;
            while (srcI < srcArch->typesCount && dstI < dstArch->typesCount)
            {
                if (srcArch->types[srcI] < dstArch->types[dstI])
                    ++srcI;
                else if (srcArch->types[srcI] > dstArch->types[dstI])
                    ++dstI;
                else
                {
                    byte* src = srcChunk->buffer + srcArch->offsets[srcI] + srcIndex * srcArch->sizeOfs[srcI];
                    byte* dst = dstChunk->buffer + dstArch->offsets[dstI] + dstIndex * dstArch->sizeOfs[dstI];
                    UnsafeUtility.MemCpy(dst, src, srcArch->sizeOfs[srcI]);
                    ++srcI;
                    ++dstI;
                }
            }
        }

        public static void CopyManagedObjects(ArchetypeManager typeMan, Chunk* srcChunk, int srcStartIndex, Chunk* dstChunk, int dstStartIndex, int count)
        {
            Archetype* srcArch = srcChunk->archetype;
            Archetype* dstArch = dstChunk->archetype;

            int srcI = 0;
            int dstI = 0;
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
                        for (int i = 0; i < count; ++i)
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
            Archetype* arch = chunk->archetype;

            for (int type = 0; type < arch->typesCount; ++type)
            {
                if (arch->managedArrayOffset[type] >= 0)
                {
                    for (int i = 0; i < count; ++i)
                        typeMan.SetManagedObject(chunk, type, index+i, null);
                }
            }
        }
    }
}
