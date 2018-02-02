using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    public unsafe struct EntityCommandBuffer
    {
        private const int kMinimumChunkSize = 64 * 1024;

        [StructLayout(LayoutKind.Sequential)]
        struct Chunk
        {
            //public byte* Base;
            internal int Used;
            internal int Size;
            internal Chunk* Next;
            internal Chunk* Prev;

            internal int Capacity => Size - Used;

            internal int Bump(int size)
            {
                int off = Used;
                Used += size;
                return off;
            }
        }

        private Chunk* m_Tail;
        private Chunk* m_Head;

        [StructLayout(LayoutKind.Sequential)]
        struct BasicCommand
        {
            public int CommandType;
            public int TotalSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct EntityCommand
        {
            public BasicCommand Header;
            public Entity Entity;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct EntityComponentCommand
        {
            public EntityCommand Header;
            public int ComponentTypeIndex;
            public int ComponentSize;
            // Data follows if command has an associated component payload
        }

        private void AllocChunk(int size)
        {
            Chunk* c = (Chunk*) UnsafeUtility.Malloc(sizeof(Chunk) + size, 16, Unity.Collections.Allocator.Temp);
            c->Next = null;
            c->Prev = m_Tail != null ? m_Tail : null;
            c->Used = 0;
            c->Size = size;

            if (m_Tail != null)
            {
                m_Tail->Next = c;
            }

            if (m_Head == null)
            {
                m_Head = c;
            }

            m_Tail = c;

        }

        private byte* Reserve(int size)
        {
            if (m_Tail == null || m_Tail->Capacity < size)
            {
                AllocChunk(Math.Max(size, kMinimumChunkSize));
            }

            int offset = m_Tail->Bump(size);
            byte* ptr = ((byte*)m_Tail) + sizeof(Chunk) + offset;
            return ptr;
        }

        private void AddBasicCommand(Command op)
        {
            BasicCommand* data = (BasicCommand*) Reserve(sizeof(BasicCommand));

            data->CommandType = (int) op;
            data->TotalSize = sizeof(BasicCommand);
        }

        private void AddEntityCommand(Command op, Entity e)
        {
            EntityCommand* data = (EntityCommand*) Reserve(sizeof(EntityCommand));

            data->Header.CommandType = (int) op;
            data->Header.TotalSize = sizeof(EntityCommand);
            data->Entity = e;
        }

        private void AddEntityComponentCommand<T>(Command op, Entity e, T component) where T : struct, IComponentData
        {
            int typeSize = UnsafeUtility.SizeOf<T>();
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int sizeNeeded = Align(sizeof(EntityComponentCommand) + typeSize, 8);

            EntityComponentCommand* data = (EntityComponentCommand*) Reserve(sizeNeeded);

            data->Header.Header.CommandType = (int) op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Entity = e;
            data->ComponentTypeIndex = typeIndex;
            data->ComponentSize = typeSize;

            UnsafeUtility.CopyStructureToPtr (ref component, (byte*) (data + 1));
        }

        private static int Align(int size, int alignmentPowerOfTwo)
        {
            return (size + alignmentPowerOfTwo - 1) & ~(alignmentPowerOfTwo - 1);
        }

        private void AddEntityComponentTypeCommand<T>(Command op, Entity e) where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            int sizeNeeded = Align(sizeof(EntityComponentCommand), 8);

            EntityComponentCommand* data = (EntityComponentCommand*) Reserve(sizeNeeded);

            data->Header.Header.CommandType = (int) op;
            data->Header.Header.TotalSize = sizeNeeded;
            data->Header.Entity = e;
            data->ComponentTypeIndex = typeIndex;
        }

        public void Dispose()
        {
            while (m_Tail != null)
            {
                Chunk* prev = m_Tail->Prev;
                UnsafeUtility.Free(m_Tail, Unity.Collections.Allocator.Temp);
                m_Tail = prev;
            }
            m_Head = null;
        }

        public void CreateEntity()
        {
            AddBasicCommand(Command.CreateEntityImplicit);
        }

        public void RemoveEntity(Entity e)
        {
            AddEntityCommand(Command.RemoveEntityExplicit, e);
        }

        public void AddComponent<T>(Entity e, T component) where T: struct, IComponentData
        {
            AddEntityComponentCommand(Command.AddComponentExplicit, e, component);
        }

        public void SetComponent<T>(Entity e, T component) where T: struct, IComponentData
        {
            AddEntityComponentCommand(Command.SetComponentExplicit, e, component);
        }

        public void RemoveComponent<T>(Entity e) where T: struct, IComponentData
        {
            AddEntityComponentTypeCommand<T>(Command.RemoveComponentExplicit, e);
        }

        public void AddComponent<T>(T component) where T: struct, IComponentData
        {
            AddEntityComponentCommand(Command.AddComponentImplicit, Entity.Null, component);
        }

        private enum Command
        {
            // Commands that operate on a known entity
            RemoveEntityExplicit,
            AddComponentExplicit,
            RemoveComponentExplicit,
            SetComponentExplicit,

            // Commands that either create a new entity or operate implicitly on a just-created entity (which doesn't yet exist when the command is buffered)
            CreateEntityImplicit,
            AddComponentImplicit,
        }

        public void Playback(EntityManager mgr)
        {
            Chunk* head = m_Head;
            Entity lastEntity = new Entity();

            while (head != null)
            {
                int off = 0;
                byte* buf = ((byte*)head) + sizeof(Chunk);

                while (off < head->Used)
                {
                    BasicCommand* header = (BasicCommand*)(buf + off);

                    switch ((Command) header->CommandType)
                    {
                        case Command.CreateEntityImplicit:
                            lastEntity = mgr.CreateEntity();
                            break;

                        case Command.RemoveEntityExplicit:
                            mgr.DestroyEntity(((EntityCommand*)header)->Entity);
                            break;

                        case Command.AddComponentExplicit:
                            {
                                EntityComponentCommand* cmd = (EntityComponentCommand*)header;
                                var componentType = (ComponentType)TypeManager.GetType(cmd->ComponentTypeIndex);
                                mgr.AddComponent(cmd->Header.Entity, componentType);
                                mgr.SetComponentRaw(cmd->Header.Entity, cmd->ComponentTypeIndex, (cmd + 1), cmd->ComponentSize);
                            }
                            break;

                        case Command.AddComponentImplicit:
                            {
                                EntityComponentCommand* cmd = (EntityComponentCommand*)header;
                                var componentType = (ComponentType)TypeManager.GetType(cmd->ComponentTypeIndex);
                                mgr.AddComponent(lastEntity, componentType);
                                mgr.SetComponentRaw(lastEntity, cmd->ComponentTypeIndex, (cmd + 1), cmd->ComponentSize);
                            }
                            break;

                        case Command.RemoveComponentExplicit:
                            {
                                EntityComponentCommand* cmd = (EntityComponentCommand*)header;
                                mgr.RemoveComponent(cmd->Header.Entity, TypeManager.GetType(cmd->ComponentTypeIndex));
                            }
                            break;

                        case Command.SetComponentExplicit:
                            {
                                EntityComponentCommand* cmd = (EntityComponentCommand*)header;
                                mgr.SetComponentRaw(cmd->Header.Entity, cmd->ComponentTypeIndex, (cmd + 1), cmd->ComponentSize);
                            }
                            break;
                    }

                    off += header->TotalSize;
                }

                head = head->Next;
            }
        }

        private object PullEntity(byte* v)
        {
            throw new NotImplementedException();
        }
    }
}
