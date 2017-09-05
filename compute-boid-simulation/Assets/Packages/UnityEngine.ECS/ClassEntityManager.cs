using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{
#if ECS_ENTITY_CLASS
    public struct Entity
    {
    	internal int debugManagerIndex;
    	internal int index;

    	internal Entity(int debugManagerIndex, int index)
    	{
    		this.debugManagerIndex = debugManagerIndex;
    		this.index = index;
    	}
    }

	struct EntityData
	{
		public int classIdx;
		public int entityIdxInClass;
	}

    public class EntityManager : ScriptBehaviourManager
    {
		struct ComponentTypeCache<T>
		{
			public static int index;
		}

        static List<Type>                                 ms_ComponentTypes;
        static List<EntityManager>                        ms_AllEntityManagers;

        List<IComponentDataManager>                       m_ComponentManagers;

		List<EntityClass>								  m_EntityClasses = new List<EntityClass>();
		Dictionary<NativeList<int>, int>							  m_EntityClassForComponentTypes = new Dictionary<NativeList<int>, int>(new EntityClassEqualityComparer());

		HashSet<EntityGroup>  m_EntityGroups = new HashSet<EntityGroup>();

		NativeList<int> m_ComponentTypesTemp;
		private void InsertSort(NativeList<int> list, int val)
		{
			int index = 0;
			while (index < list.Length && list[index] < val)
				++index;
			list.Add(0);
			for (int i = list.Length-1; i > index; --i)
			{
				list[i] = list[i-1];
			}
			list[index] = val;
		}
		private int GetEntityClass(NativeList<int> componentTypes, bool hasTransform)
		{
			if (hasTransform)
				componentTypes.Add(-1);
			int classIndex;
			if (m_EntityClassForComponentTypes.TryGetValue(componentTypes, out classIndex))
				return classIndex;
			if (hasTransform)
				componentTypes.RemoveAtSwapBack(componentTypes.Length-1);
			var newClass = new EntityClass();
			newClass.hasTransform = hasTransform;
			newClass.componentTypes = new NativeList<int>(componentTypes.Length, Allocator.Persistent);
			for (int i = 0; i < componentTypes.Length; ++i)
				newClass.componentTypes.Add(componentTypes[i]);
			newClass.entities = new NativeList<Entity>(0, Allocator.Persistent);
			newClass.componentDataIndices = new NativeList<int>(Allocator.Persistent);
			classIndex = m_EntityClasses.Count;
			m_EntityClasses.Add(newClass);
			if (hasTransform)
			{
				newClass.transformKeyHack = new NativeList<int>(componentTypes.Length+1, Allocator.Persistent);
				for (int i = 0; i < componentTypes.Length; ++i)
					newClass.transformKeyHack.Add(componentTypes[i]);
				newClass.transformKeyHack.Add(-1);
				m_EntityClassForComponentTypes.Add(newClass.transformKeyHack, classIndex);				
			}
			else
			{
				m_EntityClassForComponentTypes.Add(newClass.componentTypes, classIndex);
			}
			// make sure all classes interested in this class are notified
			foreach (var tuple in m_EntityGroups)
			{
				tuple.AddClassIfMatching(newClass);
			}
			return classIndex;
		}

		NativeHashMap<int, EntityData> m_EntityIdToEntityData;

    	int 											  m_InstanceIDAllocator = -1;
    	int 										      m_DebugManagerID;

    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

    		m_EntityIdToEntityData = new NativeHashMap<int, EntityData>(capacity, Allocator.Persistent);
			m_DebugManagerID = 1;
			m_ComponentTypesTemp = new NativeList<int>(128, Allocator.Persistent);

            if (ms_ComponentTypes == null)
            {
                ms_ComponentTypes = new List<Type> ();
                ms_ComponentTypes.Add (null);

                ms_AllEntityManagers = new List<EntityManager> ();
            }

            m_ComponentManagers = new List<IComponentDataManager>(ms_ComponentTypes.Count);
            foreach (var type in ms_ComponentTypes)
            {
                m_ComponentManagers.Add (null);
            }
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
			m_ComponentTypesTemp.Dispose();
			m_EntityIdToEntityData.Dispose ();
			for (int i = 0; i < m_EntityClasses.Count; ++i)
			{
				if (m_EntityClasses[i].transformKeyHack.IsCreated)
					m_EntityClasses[i].transformKeyHack.Dispose();
				m_EntityClasses[i].componentTypes.Dispose();
				m_EntityClasses[i].componentDataIndices.Dispose();
				m_EntityClasses[i].entities.Dispose();
			}

            ms_AllEntityManagers.Remove (this);
    	}

		internal Type GetTypeFromIndex(int index)
		{
			return ms_ComponentTypes[index];
		}

		internal int GetTypeIndex<T>()
		{
			int typeIndex = ComponentTypeCache<T>.index;
			if (typeIndex != 0)
				return typeIndex;

			typeIndex = GetTypeIndex (typeof(T));
			ComponentTypeCache<T>.index = typeIndex;
			return typeIndex;
		}

    	internal int GetTypeIndex(Type type)
    	{
    		//@TODO: Initialize with all types on startup instead? why continously populate...
    		for (int i = 0; i < ms_ComponentTypes.Count; i++)
    		{
    			if (ms_ComponentTypes [i] == type)
    				return i;
    		}

    		if (!typeof(IComponentData).IsAssignableFrom (type))
				throw new ArgumentException (string.Format("{0} must be a IComponentData to be used when create a lightweight game object", type));
    		
    		ms_ComponentTypes.Add (type);

			var managerType = typeof(ComponentDataManager<>).MakeGenericType(new Type[] { type });
			var manager = DependencyManager.GetBehaviourManager (managerType) as IComponentDataManager;
			m_ComponentManagers.Add (manager);

    		return ms_ComponentTypes.Count - 1;
    	}

		internal int GetComponentIndex(Entity entity, int typeIndex)
		{
			EntityData entityData;
			if (!m_EntityIdToEntityData.IsCreated || !m_EntityIdToEntityData.TryGetValue(entity.index, out entityData))
				return -1;
			if (entityData.classIdx < 0)
				return -1;
			var entityClass = m_EntityClasses[entityData.classIdx];
			// Special case for empty class
			if (entityData.classIdx < 0)
				return -1;
			int offset = 0;
			while (offset < entityClass.componentTypes.Length)
			{
				if (entityClass.componentTypes[offset] == typeIndex)
					return entityClass.componentDataIndices[entityData.entityIdxInClass * entityClass.componentTypes.Length + offset];
				++offset;
			}
			return -1;
		}

    	public int GetComponentIndex<T>(Entity entity) where T : IComponentData
    	{
    		return GetComponentIndex (entity, GetTypeIndex<T>());
    	}

    	public int GetComponentIndex(Entity entity, Type type)
    	{
    		return GetComponentIndex (entity, GetTypeIndex(type));
    	}

		public bool HasComponent<T>(Entity entity) where T : IComponentData
		{
			return GetComponentIndex (entity, GetTypeIndex<T>()) != -1;
		}

		public bool HasComponent(Entity entity, Type type)
		{
			return GetComponentIndex (entity, GetTypeIndex(type)) != -1;
		}

		public Entity AllocateEntity()
		{
			var go = new Entity(m_DebugManagerID, m_InstanceIDAllocator);
			m_InstanceIDAllocator -= 2;

			return go;
		}

		public void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
		{
			Assert.IsFalse (HasComponent<T>(entity));

			// Add to manager
			int componentTypeIndex = GetTypeIndex<T> ();
            var manager = GetComponentManager<T>(componentTypeIndex);
			int index = manager.AddElement (componentData);

			EntityData entityData;
			if (!m_EntityIdToEntityData.TryGetValue(entity.index, out entityData))
			{
				entityData.classIdx = -1;
				entityData.entityIdxInClass = -1;
			}
			m_ComponentTypesTemp.Clear();

			int oldClassIdx = entityData.classIdx;
			var fullGameObject = UnityEditor.EditorUtility.InstanceIDToObject (entity.index) as GameObject;
			if (entityData.classIdx >= 0)
			{
				var oldEntityClass = m_EntityClasses[entityData.classIdx];
				for (int i = 0; i < oldEntityClass.componentTypes.Length; ++i)
				{
					m_ComponentTypesTemp.Add(oldEntityClass.componentTypes[i]);
				}
			}
			InsertSort(m_ComponentTypesTemp, componentTypeIndex);
			var entityClassIdx = GetEntityClass(m_ComponentTypesTemp, fullGameObject != null);
			var entityClass = m_EntityClasses[entityClassIdx];
			if (oldClassIdx < 0)
			{
				entityData.classIdx = entityClassIdx;
				entityData.entityIdxInClass = entityClass.entities.Length;
				m_EntityIdToEntityData.Remove(entity.index);
				m_EntityIdToEntityData.TryAdd(entity.index, entityData);
				entityClass.entities.Add(entity);
				entityClass.componentDataIndices.Add(index);
			}
			else
			{
				m_EntityClasses[oldClassIdx].MoveTo(m_EntityIdToEntityData, entityClassIdx, entityClass, entityData.entityIdxInClass, componentTypeIndex, index);
			}
			// FIXME: mark class as changed
		}
			
    	public T GetComponent<T>(Entity entity) where T : struct, IComponentData
    	{
    		int index = GetComponentIndex<T> (entity);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

            var manager = GetComponentManager<T> (GetTypeIndex<T>());
			manager.CompleteForReading ();
			return manager.m_Data[index];
    	}

    	public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
    	{
	   		int index = GetComponentIndex<T> (entity);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

            var manager = GetComponentManager<T> (GetTypeIndex<T>());
			manager.CompleteForWriting ();
    		manager.m_Data[index] = componentData;
    	}

        ComponentDataManager<T> GetComponentManager<T>(int typeIndex) where T: struct, IComponentData
        {
            if (m_ComponentManagers [typeIndex] == null)
                GetComponentManager(typeIndex);

            return (ComponentDataManager<T>)m_ComponentManagers[typeIndex];
        }

        IComponentDataManager GetComponentManager(int typeIndex)
        {
            if (m_ComponentManagers[typeIndex] == null)
            {
                var managerType = typeof(ComponentDataManager<>).MakeGenericType(new Type[] { ms_ComponentTypes[typeIndex] });
                m_ComponentManagers[typeIndex] = DependencyManager.GetBehaviourManager (managerType) as IComponentDataManager;
            }

            return m_ComponentManagers[typeIndex];
        }

		NativeArray<Entity> InstantiateCompleteCreation (int entityClassIdx, EntityClass entityClass, NativeArray<int> allComponentIndices, int numberOfInstances)
		{
    		//@TODO: Temp alloc
			var entities = new NativeArray<Entity> (numberOfInstances, Allocator.Persistent);
			EntityData entityData;
			entityData.classIdx = entityClassIdx;
			for (int i = 0; i < entities.Length; i++)
			{
				entities[i] = new Entity (m_DebugManagerID, m_InstanceIDAllocator - i * 2);
				entityData.entityIdxInClass = entityClass.entities.Length;
				entityClass.entities.Add(entities[i]);
				m_EntityIdToEntityData.TryAdd(entities[i].index, entityData);
			}

			m_InstanceIDAllocator -= numberOfInstances * 2;
			return entities;
		}
    	public NativeArray<Entity> Instantiate (Entity entity, int numberOfInstances)
    	{
    		if (numberOfInstances < 1)
    			throw new System.ArgumentException ("Number of instances must be greater than 1");

			EntityData sourceEntity;
			if (!m_EntityIdToEntityData.TryGetValue(entity.index, out sourceEntity))
    			throw new System.ArgumentException ("Invalid entity");
			EntityClass entityClass = m_EntityClasses[sourceEntity.classIdx];

			//@TODO: Temp alloc
			var allComponentIndices = new NativeArray<int> (numberOfInstances * entityClass.componentTypes.Length, Allocator.Temp);

			for (int t = 0;t != entityClass.componentTypes.Length;t++)
			{
                var manager = GetComponentManager(entityClass.componentTypes[t]);
				manager.AddElements (GetComponentIndex(entity, entityClass.componentTypes[t]), new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
			}

			var entities = InstantiateCompleteCreation(sourceEntity.classIdx, entityClass, allComponentIndices, numberOfInstances);
			for (int i = 0; i < numberOfInstances; ++i)
				for (int j = 0; j < entityClass.componentTypes.Length; ++j)
					entityClass.componentDataIndices.Add(allComponentIndices[i+j*numberOfInstances]);

			allComponentIndices.Dispose();

			// FIXME: mark class as changed
    		return entities;
    	}
    	//@TODO: Need overload with the specific components to clone somehow???
    	public NativeArray<Entity> Instantiate (GameObject gameObject, int numberOfInstances)
    	{
    		if (numberOfInstances < 1)
    			throw new System.ArgumentException ("Number of instances must be greater than 1");

    		var components = gameObject.GetComponents<ComponentDataWrapperBase> ();
			var componentDataTypes = new NativeArray<int> (components.Length, Allocator.Temp);
			m_ComponentTypesTemp.Clear();

            for (int t = 0;t != components.Length;t++)
			{
				componentDataTypes[t] = GetTypeIndex(components[t].GetIComponentDataType());
				InsertSort(m_ComponentTypesTemp, componentDataTypes[t]);
			}

			var allComponentIndices = new NativeArray<int> (numberOfInstances * components.Length, Allocator.Temp);

			int entityClassIdx = GetEntityClass(m_ComponentTypesTemp, false);
			var entityClass = m_EntityClasses[entityClassIdx];
    		for (int t = 0;t != components.Length;t++)
    		{
                var manager = GetComponentManager(componentDataTypes[t]);
				manager.AddElements (gameObject, new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
    		}

			var entities = InstantiateCompleteCreation(entityClassIdx, entityClass, allComponentIndices, numberOfInstances);
			for (int i = 0; i < allComponentIndices.Length; ++i)
				entityClass.componentDataIndices.Add(allComponentIndices[i]);

			allComponentIndices.Dispose();
			componentDataTypes.Dispose ();

			// FIXME: mark class as changed
    		return entities;
    	}

		public void Destroy (NativeArray<Entity> entities)
    	{
			for (var i = 0;i != entities.Length;i++)
	    		Destroy(entities[i]);
    	}

    	public Entity GameObjectToEntity(GameObject go)
    	{
    		Entity light;
    		light.debugManagerIndex = m_DebugManagerID;
			light.index = go.GetInstanceID();

    		return light;
    	}

		public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
		{
			Assert.IsTrue (HasComponent<T>(entity));

			int componentTypeIndex = GetTypeIndex<T> ();

			EntityData entityData;
			if (!m_EntityIdToEntityData.TryGetValue(entity.index, out entityData))
				throw new InvalidOperationException("Invalid entity id");
			m_ComponentTypesTemp.Clear();
			int oldClassIdx = entityData.classIdx;
			var oldClass = m_EntityClasses[entityData.classIdx];
			int componentIndex = -1;
			for (int i = 0; i < oldClass.componentTypes.Length; ++i)
			{
				if (oldClass.componentTypes[i] != componentTypeIndex)
				{
					for (int j = 0; j < oldClass.componentTypes.Length; ++j)
					m_ComponentTypesTemp.Add(oldClass.componentTypes[j]);
				}
				else
					componentIndex = oldClass.componentDataIndices[entityData.entityIdxInClass * oldClass.componentTypes.Length + i];
			}

			m_ComponentManagers[componentTypeIndex].RemoveElement (componentIndex);

			int entityClassIdx = GetEntityClass(m_ComponentTypesTemp, oldClass.hasTransform);
			var entityClass = m_EntityClasses[entityClassIdx];
			oldClass.MoveTo(m_EntityIdToEntityData, entityClassIdx, entityClass, entityData.entityIdxInClass);
			// FIXME: mark class as changed
		}

    	public void Destroy (Entity entity)
    	{
			//@TODO: Validate manager index...

			EntityData entityData;
			if (!m_EntityIdToEntityData.TryGetValue(entity.index, out entityData))
				throw new InvalidOperationException("Invalid entity id");
			if (entityData.classIdx < 0)
			{
				m_EntityIdToEntityData.Remove(entity.index);
				return;
			}
			var entityClass = m_EntityClasses[entityData.classIdx];
			var offset = entityData.entityIdxInClass * entityClass.componentTypes.Length;
			for (int i = 0; i < entityClass.componentTypes.Length; ++i)
			{
				m_ComponentManagers[entityClass.componentTypes[i]].RemoveElement(entityClass.componentDataIndices[offset+i]);
			}
			entityClass.Remove(m_EntityIdToEntityData, entityData.entityIdxInClass);
			// FIXME: mark class as changed
    	}

		internal void RegisterTuple(int componentTypeIndex, EntityGroup tuple, int tupleSystemIndex)
		{
			if (m_EntityGroups.Contains(tuple))
				return;
			for (int i = 0; i < m_EntityClasses.Count; ++i)
			{
				tuple.AddClassIfMatching(m_EntityClasses[i]);
			}
			m_EntityGroups.Add(tuple);
		}
    }
	#endif // ECS_ENTITY_CLASS
}