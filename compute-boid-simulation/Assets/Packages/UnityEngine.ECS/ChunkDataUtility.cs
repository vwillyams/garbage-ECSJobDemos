using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ECS
{
    internal unsafe static class ChunkDataUtility
    {
        static public int GetIndexInTypeArray(Archetype* archetype, int typeIndex)
        {
            ComponentType* types = archetype->types;
            int typeCount = archetype->typesCount;
            for (int i = 0; i != typeCount; i++)
            {
                if (typeIndex == types[i].typeIndex)
                    return i;
            }

            return -1;
        }

        public static IntPtr GetComponentDataWithType(Chunk* chunk, int index, int typeIndex)
        {
            int indexInTypeArray = GetIndexInTypeArray(chunk->archetype, typeIndex);

            int offset = chunk->archetype->offsets[indexInTypeArray];
            int stride = chunk->archetype->strides[indexInTypeArray];

            return chunk->buffer + (offset + stride * index);
        }

        public static IntPtr GetComponentData(Chunk* chunk, int index, int indexInTypeArray)
        {
            int offset = chunk->archetype->offsets[indexInTypeArray];
            int stride = chunk->archetype->strides[indexInTypeArray];

            return chunk->buffer + (offset + stride * index);
        }


        public static void Copy(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex)
        {
            Archetype* arch = srcChunk->archetype;

            for (int i = 0; i != arch->typesCount; i++)
            {
                IntPtr src = GetComponentData(srcChunk, srcIndex, i);
                IntPtr dst = GetComponentData(dstChunk, dstIndex, i);
                UnsafeUtility.MemCpy(dst, src, arch->sizeOfs[i]);
            }
        }

        public static void Copy(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstIndex, int count)
        {
            for (int i = 0; i < count;i++)
            {
                Copy(srcChunk, srcIndex + i, dstChunk, dstIndex + i);
            }
        }


        public static void ClearComponents(Chunk* dstChunk, int dstIndex, int count)
        {
            Archetype* arch = dstChunk->archetype;

            for (int e = 0; e < count;e++)
            {
                for (int t = 1; t != arch->typesCount; t++)
                {
                    IntPtr dst = GetComponentData(dstChunk, dstIndex + e, t);
                    UnsafeUtility.MemClear(dst, arch->sizeOfs[t]);
                }
            }
        }

        public static void MemCpyReplicate(IntPtr dst, IntPtr src, int size, int count)
        {
            UnsafeUtility.MemCpy(dst, src, size);
            if (count == 1)
                return;
            src = dst;
            dst = dst + size;
            int copySize = size;
            int remainSize = size * (count - 1);

            while (remainSize > copySize)
            {
                UnsafeUtility.MemCpy(dst, src, copySize);
                dst += copySize;
                remainSize -= copySize;
                copySize += copySize;
            }

            UnsafeUtility.MemCpy(dst, src, remainSize);
        }

        public static void ReplicateComponents(Chunk* srcChunk, int srcIndex, Chunk* dstChunk, int dstBaseIndex, int count)
        {
            Archetype* arch = srcChunk->archetype;
            // assumes fully strided data
            IntPtr src = GetComponentData(srcChunk, srcIndex, 0);
            IntPtr dst = GetComponentData(dstChunk, dstBaseIndex, 0);
            MemCpyReplicate(dst, src, arch->bytesPerInstance, count);
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
                    IntPtr src = srcChunk->buffer + srcArch->offsets[srcI] + srcIndex * srcArch->strides[srcI];
                    IntPtr dst = dstChunk->buffer + dstArch->offsets[dstI] + dstIndex * dstArch->strides[dstI];
                    UnsafeUtility.MemCpy(dst, src, srcArch->sizeOfs[srcI]);
                    ++srcI;
                    ++dstI;
                }
            }
        }

        public static void CopyManagedObjects(TypeManager typeMan, Chunk* srcChunk, int srcStartIndex, Chunk* dstChunk, int dstStartIndex, int count)
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
        public static void ClearManagedObjects(TypeManager typeMan, Chunk* chunk, int index, int count)
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
