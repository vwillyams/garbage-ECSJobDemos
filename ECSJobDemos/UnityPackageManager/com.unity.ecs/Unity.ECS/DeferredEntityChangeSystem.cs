using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using System.ComponentModel;
using UnityEngine.ECS;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
    public struct AddComponentPayload<T> where T : struct, IComponentData
    {
        public Entity entity;
        public T component;

        public AddComponentPayload(Entity entity, T component)
        {
            this.entity = entity;
            this.component = component;
        }
    }

    [UpdateBefore(typeof(UnityEngine.Experimental.PlayerLoop.Initialization))]
    public class DeferredEntityChangeSystem : ComponentSystem
    {
        interface IApplyQueue
        {
            void Apply(EntityManager manager);
        }
        interface IDisposeQueue
        {
            void Dispose();
        }

        class AddQueueOwner<T> : IApplyQueue, IDisposeQueue where T : struct, IComponentData
        {
            public NativeQueue<AddComponentPayload<T> > queue;

            public void Dispose()
            {
                if (queue.IsCreated)
                {
                    queue.Dispose();
                }
            }
            public void Apply(EntityManager manager)
            {
                var count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var payload = queue.Dequeue();
                    var entity = payload.entity;
                    var component = payload.component;
                    if (!manager.HasComponent<T>(entity))
                    {
                        manager.AddComponentData(entity, component);
                    }
                }
            }
        }

        class RemoveQueueOwner<T> : IApplyQueue, IDisposeQueue where T : struct, IComponentData
        {
            public NativeQueue<Entity> queue;

            public void Dispose()
            {
                if (queue.IsCreated)
                {
                    queue.Dispose();
                }
            }
            public void Apply(EntityManager manager)
            {
                var count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var entity = queue.Dequeue();
                    manager.RemoveComponent<T>(entity);
                }
            }
        }

        public object[] m_AddComponentQueue;
        public object[] m_RemoveComponentQueue;

        public void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            GetAddComponentQueue<T>().Enqueue(new AddComponentPayload<T>(entity, componentData));
        }

        public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
        {
            GetRemoveComponentQueue<T>().Enqueue(entity);
        }

        public NativeQueue<AddComponentPayload<T>> GetAddComponentQueue<T>() where T : struct, IComponentData
        {
            int index = TypeManager.GetTypeIndex<T>();
            if ((m_AddComponentQueue.Length <= index) || (m_AddComponentQueue[index] == null))
            {
                var newOwner = new AddQueueOwner<T>();
                newOwner.queue = new NativeQueue<AddComponentPayload<T>>(Allocator.Persistent);
                m_AddComponentQueue[index] = newOwner;
            }
            var owner = (AddQueueOwner<T>) m_AddComponentQueue[index];
            return owner.queue;
        }

        public NativeQueue<Entity> GetRemoveComponentQueue<T>() where T : struct, IComponentData
        {
            int index = TypeManager.GetTypeIndex<T>();
            if ((m_RemoveComponentQueue.Length <= index) || (m_RemoveComponentQueue[index] == null))
            {
                var newOwner = new RemoveQueueOwner<T>();
                newOwner.queue = new NativeQueue<Entity>(Allocator.Persistent);
                m_RemoveComponentQueue[index] = newOwner;
            }
            var owner = (RemoveQueueOwner<T>) m_RemoveComponentQueue[index];
            return owner.queue;
        }

        protected override void OnCreateManager(int capacity)
        {
            m_AddComponentQueue = new object[1024];
            m_RemoveComponentQueue = new object[1024];
        }

        protected override void OnDestroyManager()
        {
            for (var i = 0; i < m_AddComponentQueue.Length; i++)
            {
                if (m_AddComponentQueue[i] == null)
                {
                    continue;
                }
                IDisposeQueue owner = m_AddComponentQueue[i] as IDisposeQueue;
                owner.Dispose();
            }
            for (var i = 0; i < m_RemoveComponentQueue.Length; i++)
            {
                if (m_RemoveComponentQueue[i] == null)
                {
                    continue;
                }
                IDisposeQueue owner = m_RemoveComponentQueue[i] as IDisposeQueue;
                owner.Dispose();
            }

        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteAllJobs();
            for (var i = 0; i < m_AddComponentQueue.Length; i++)
            {
                if (m_AddComponentQueue[i] == null)
                {
                    continue;
                }
                IApplyQueue owner = m_AddComponentQueue[i] as IApplyQueue;
                owner.Apply( EntityManager );
            }
            for (var i = 0; i < m_RemoveComponentQueue.Length; i++)
            {
                if (m_RemoveComponentQueue[i] == null)
                {
                    continue;
                }
                IApplyQueue owner = m_RemoveComponentQueue[i] as IApplyQueue;
                owner.Apply( EntityManager );
            }
        }

    }
}
