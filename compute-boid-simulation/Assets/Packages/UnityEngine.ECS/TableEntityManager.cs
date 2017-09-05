using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;
using UnityEngine.Profiling;

namespace UnityEngine.ECS
{
#if ECS_ENTITY_TABLE
    public struct Entity
    {
    	internal int debugManagerIndex;
    	internal int index;
    	internal int version;

    	internal Entity(int debugManagerIndex, int index, int version)
    	{
    		this.debugManagerIndex = debugManagerIndex;
    		this.index = index;
    		this.version = version;

    	}
    }

	unsafe public class EntityManager : ScriptBehaviourManager
    {
		struct ComponentTypeCache<T>
		{
			public static int index;
		}

		struct LightWeightComponentInfo
		{
			public int  componentTypeIndex;
			// Index of the component in the LightWeightComponentManager
			public int 	index;
		}
		
		struct EntityData
		{
			public int 		 			version;

            public int 	     			groupCount;
            public EntityGroupData* 	groupData;

            public int 		 			componentCount;
            public EntityComponentData* componentData;
		}

		struct EntityComponentData
		{
            public int typeIndex;
            public int componentIndex;
		}

		struct EntityGroupData
		{
            public int 			groupIndex;
            public int 			indexInGroup;
		}

		EntityData* 		m_Entities;
		int 				m_EntitiesCapacity;
		int 				m_EntitiesFreeIndex;

        NativePoolAllocator m_PoolAllocator;

		static List<Type> 								  ms_ComponentTypes;
		static List<EntityManager> 						  ms_AllEntityManagers;

		List<IComponentDataManager> 					  m_ComponentManagers;
        List<List<EntityGroup.RegisteredTuple> >          m_EntityGroupsForComponent;
        List<EntityGroup>                                 m_EntityGroups;

    	int 											  m_InstanceIDAllocator = -1;
    	int 										      m_DebugManagerID;

		CustomSampler m_AddToEntityComponentTable;
		CustomSampler m_AddToEntityGroup;
		CustomSampler m_AddComponentManagerElements;
		CustomSampler m_AddToEntityGroup1;
		CustomSampler m_AddToEntityGroup2;


    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

    		m_DebugManagerID = 1;

			if (ms_ComponentTypes == null)
			{
				ms_ComponentTypes = new List<Type> ();
				ms_ComponentTypes.Add (null);

				ms_AllEntityManagers = new List<EntityManager> ();
			}

			ms_AllEntityManagers.Add (this);

			m_ComponentManagers = new List<IComponentDataManager>(ms_ComponentTypes.Count);
			m_EntityGroupsForComponent = new List<List<EntityGroup.RegisteredTuple> >(ms_ComponentTypes.Count);
            m_EntityGroups = new List<EntityGroup> ();

			foreach (var type in ms_ComponentTypes)
			{
				m_ComponentManagers.Add (null);
				m_EntityGroupsForComponent.Add (new List<EntityGroup.RegisteredTuple>());
			}

			//@TODO: proper capacity management
			m_EntitiesCapacity = 1000000;
            m_Entities = (EntityData*)UnsafeUtility.Malloc(m_EntitiesCapacity * sizeof(EntityData), 64, Allocator.Persistent);
			for (int i = 0;i != m_EntitiesCapacity-1;i++)
				m_Entities[i].groupCount = i + 1;
			m_Entities[m_EntitiesCapacity-1].groupCount = -1;
			m_EntitiesFreeIndex = 0;

            // @TODO: Dont crash when exceeding 10 component count...
            m_PoolAllocator = new NativePoolAllocator (1000000, sizeof(EntityComponentData) * 10, 64, Allocator.Persistent);

			m_AddToEntityComponentTable = CustomSampler.Create ("AddToEntityComponentTable"); ;
			m_AddToEntityGroup = CustomSampler.Create ("AddToEntityGroup"); 
			m_AddComponentManagerElements = CustomSampler.Create ("AddComponentManagerElements"); 
			m_AddToEntityGroup1 = CustomSampler.Create ("1"); 
			m_AddToEntityGroup2 = CustomSampler.Create ("2"); 
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
			m_ComponentManagers = null;
			m_EntityGroupsForComponent = null;

            UnsafeUtility.Free ((IntPtr)m_Entities, Allocator.Persistent);
            m_Entities = (EntityData*)IntPtr.Zero;
            m_EntitiesCapacity = 0;

            m_PoolAllocator.Dispose ();

			ms_AllEntityManagers.Remove (this);
    	}

		internal static Type GetTypeFromIndex(int index)
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
    			if (ms_ComponentTypes[i] == type)
    				return i;
    		}

    		if (!typeof(IComponentData).IsAssignableFrom (type))
				throw new ArgumentException (string.Format("{0} must be a IComponentData to be used when create a lightweight game object", type));

			ms_ComponentTypes.Add (type);

			foreach (var manager in ms_AllEntityManagers)
			{
				manager.m_EntityGroupsForComponent.Add (new List<EntityGroup.RegisteredTuple>());
				manager.m_ComponentManagers.Add (null);
			}

    		return ms_ComponentTypes.Count - 1;
    	}

		internal int GetComponentIndex(Entity entity, int typeIndex)
		{
			EntityData* data = m_Entities + entity.index;
			if (data->version != entity.version)
				return -1;

			EntityComponentData* componentData = data->componentData;
			int componentCount = data->componentCount;

            for (int i = 0;i != componentCount;i++)
			{
				if (componentData[i].typeIndex == typeIndex)
					return componentData[i].componentIndex;
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

		bool HasComponent(Entity entity, int typeIndex)
		{
			return GetComponentIndex (entity, typeIndex) != -1;
		}

		void GetComponentTypes(Entity entity, NativeList<int> componentTypes)
		{
			EntityData* data = m_Entities + entity.index;
			if (data->version != entity.version)
				return;

			EntityComponentData* componentData = data->componentData;
			int componentCount = data->componentCount;

            for (int i = 0;i != componentCount;i++)
				componentTypes.Add(componentData[i].typeIndex);
		}

        void AllocateEntities(Entity* entities, int count)
        {
            int index = m_EntitiesFreeIndex;

            for (int i = 0; i != count; i++)
            {
                EntityData* entity = m_Entities + index;

                entities[i] = new Entity(0, index, entity->version);
                index = entity->groupCount;

                entity->componentData = (EntityComponentData*)m_PoolAllocator.Allocate ();
                entity->groupData = (EntityGroupData*)m_PoolAllocator.Allocate ();
                entity->componentCount = 0;
                entity->groupCount = 0;
            }

            m_EntitiesFreeIndex = index;
        }


		public Entity AllocateEntity()
		{
            Entity entity;
            AllocateEntities (&entity, 1);
            return entity;
		}

		void DeallocateEntity(Entity entity)
		{
			Assert.AreEqual(entity.version, m_Entities[entity.index].version);

			m_Entities[entity.index].version++;
			m_Entities[entity.index].groupCount = m_EntitiesFreeIndex;
            m_EntitiesFreeIndex = entity.index;
		}

        public bool Exists(Entity entity)
        {
            return m_Entities [entity.index].version == entity.version;
        }

		public void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
		{
			int typeIndex = GetTypeIndex<T>();
			Assert.IsFalse (HasComponent(entity, typeIndex));
            Assert.IsTrue (Exists(entity));

			// Add to manager
			var manager = GetComponentManager<T> (typeIndex);
			int index = manager.AddElement (componentData);

            EntityComponentData* componentInfo = m_Entities[entity.index].componentData;
            componentInfo += m_Entities[entity.index].componentCount;
            m_Entities[entity.index].componentCount++;

            componentInfo->componentIndex = index;
            componentInfo->typeIndex = typeIndex;

			//var fullGameObject = UnityEditor.EditorUtility.InstanceIDToObject (entity.index) as GameObject;
            GameObject fullGameObject = null;

			// tuple management
			foreach (var tuple in m_EntityGroupsForComponent[typeIndex])
				tuple.tupleSystem.AddTupleIfSupported(fullGameObject, entity);
		}
			
    	public T GetComponent<T>(Entity entity) where T : struct, IComponentData
    	{
			int typeIndex = GetTypeIndex<T> ();

			int index = GetComponentIndex (entity, typeIndex);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> (typeIndex);
			manager.CompleteForReading ();
			return manager.m_Data[index];
    	}

    	public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
    	{
			int typeIndex = GetTypeIndex<T> ();

			int index = GetComponentIndex (entity, typeIndex);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> (typeIndex);
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

        internal int GetIndexInGroup(Entity entity, int groupIndex)
        {
            EntityData* entityData = m_Entities + entity.index;

            EntityGroupData* groupData = entityData->groupData;
            int groupCount = entityData->groupCount;
            for (int i = 0; i != groupCount; i++)
            {
                if (groupData[i].groupIndex == groupIndex)
                    return groupData[i].indexInGroup;
            }

            return -1;
        }

        internal void AddEntityToGroup(Entity* entity, int entityCount, int groupIndex, int baseIndexInGroup)
        {
            for (int i = 0; i != entityCount; i++)
            {
                EntityData* entityData = m_Entities + entity[i].index;
                EntityGroupData* groupData = entityData->groupData + entityData->groupCount;
                groupData->indexInGroup = baseIndexInGroup + i;
                groupData->groupIndex = groupIndex;
                entityData->groupCount++;
            }
        }

        internal void RemoveGroupFromEntity(Entity entity, int groupIndex)
        {
            EntityData* entityData = m_Entities + entity.index;

            EntityGroupData* groupData = entityData->groupData;
            int groupCount = entityData->groupCount;
            for (int i = 0; i != groupCount; i++)
            {
                if (groupData[i].groupIndex == groupIndex)
                {
                    groupData [i] = groupData[groupCount - 1];
                    entityData->groupCount = groupCount - 1;
                    return;
                }
            }

            throw new System.InvalidOperationException ();
        }

        internal void UpdateIndexInGroup(Entity entity, int groupIndex, int indexInGroup)
        {
            EntityData* entityData = m_Entities + entity.index;

            EntityGroupData* groupData = entityData->groupData;
            int groupCount = entityData->groupCount;
            for (int i = 0; i != groupCount; i++)
            {
                if (groupData[i].groupIndex == groupIndex)
                {
                    groupData[i].indexInGroup = indexInGroup;
                    return;
                }
            }

            throw new System.InvalidOperationException ();

        }



		NativeArray<Entity> InstantiateCompleteCreation (NativeArray<int> componentDataTypes, NativeArray<int> allComponentIndices, int numberOfInstances)
		{
			m_AddToEntityComponentTable.Begin ();

			var entitiesArray = new NativeArray<Entity> (numberOfInstances, Allocator.Temp);

            Entity* entitiesPtr = (Entity*)entitiesArray.UnsafePtr;
            AllocateEntities (entitiesPtr, numberOfInstances);    

            int* componentDataTypesPtr = (int*)componentDataTypes.UnsafePtr;
            int componentDataTypesCount = componentDataTypes.Length;
            int* allComponentIndicesPtr = (int*)allComponentIndices.UnsafePtr;
            for (int i = 0; i < numberOfInstances; i++)
            {
                int entityIndex = entitiesPtr[i].index;
                m_Entities[entityIndex].componentCount = componentDataTypes.Length;

                EntityComponentData* componentData = m_Entities[entityIndex].componentData;
                for (int t = 0; t != componentDataTypesCount; t++)
                {
                    componentData[t].componentIndex = allComponentIndicesPtr[i + t * numberOfInstances];
                    componentData[t].typeIndex = componentDataTypesPtr[t];
                }
            }
            m_AddToEntityComponentTable.End ();

			m_AddToEntityGroup.Begin ();

			// Collect all tuples that support the created game object schema
			var tuples = new HashSet<EntityGroup> ();
			for (int t = 0;t != componentDataTypes.Length;t++)
				CollectComponentDataTupleSet (componentDataTypes[t], componentDataTypes, tuples);

			foreach (var tuple in tuples)
			{
				m_AddToEntityGroup1.Begin ();
				for (int t = 0; t != componentDataTypes.Length; t++)
					tuple.AddTuplesComponentDataPartial(componentDataTypes[t], new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
				m_AddToEntityGroup1.End ();

				m_AddToEntityGroup2.Begin ();
				tuple.AddTuplesEntityIDPartial (entitiesArray);
				m_AddToEntityGroup2.End ();
			}

			m_AddToEntityGroup.End ();
				
			return entitiesArray;
		}


		public NativeArray<Entity> Instantiate (Entity entity, int numberOfInstances)
		{
			if (numberOfInstances < 1)
				throw new System.ArgumentException ("Number of instances must be greater or equal to 1");

			var componentDataTypesList = new NativeList<int> (10, Allocator.Temp);
			GetComponentTypes (entity, componentDataTypesList);

			//@TODO: derived NativeArray is not allowed to be deallocated...
			NativeArray<int> componentDataTypes = componentDataTypesList;

			//@TODO: Temp alloc
			var allComponentIndices = new NativeArray<int> (numberOfInstances * componentDataTypes.Length, Allocator.Temp);

			for (int t = 0;t != componentDataTypes.Length;t++)
			{
				var manager = GetComponentManager (componentDataTypes[t]);
				manager.AddElements (GetComponentIndex(entity, componentDataTypes[t]), new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
			}

			var entities = InstantiateCompleteCreation (componentDataTypes, allComponentIndices, numberOfInstances);

			allComponentIndices.Dispose();
			componentDataTypesList.Dispose ();

			return entities;
		}

    	//@TODO: Need overload with the specific components to clone somehow???
    	public NativeArray<Entity> Instantiate (GameObject gameObject, int numberOfInstances)
    	{
    		if (numberOfInstances < 1)
    			throw new System.ArgumentException ("Number of instances must be greater or equal to 1");

    		var components = gameObject.GetComponents<ComponentDataWrapperBase> ();
			var componentDataTypes = new NativeArray<int> (components.Length, Allocator.Temp);
    		for (int t = 0;t != components.Length;t++)
				componentDataTypes[t] = GetTypeIndex(components[t].GetIComponentDataType());

			var allComponentIndices = new NativeArray<int> (numberOfInstances * components.Length, Allocator.Temp);

			m_AddComponentManagerElements.Begin ();
    		for (int t = 0;t != components.Length;t++)
    		{
				var manager = GetComponentManager (componentDataTypes [t]);
				manager.AddElements (gameObject, new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
    		}
			m_AddComponentManagerElements.End ();

			var entities = InstantiateCompleteCreation (componentDataTypes, allComponentIndices, numberOfInstances);

			allComponentIndices.Dispose();
			componentDataTypes.Dispose ();

			return entities;
    	}

		void CollectComponentDataTupleSet(int typeIndex, NativeArray<int> requiredComponentTypes, HashSet<EntityGroup> tuples)
    	{
			foreach (var tuple in m_EntityGroupsForComponent[typeIndex])
    		{
				if (tuple.tupleSystem.IsComponentDataTypesSupported(requiredComponentTypes))
    				tuples.Add (tuple.tupleSystem);
    		}
    	}

		public void Destroy (NativeArray<Entity> entities)
    	{
			for (var i = 0;i != entities.Length;i++)
	    		Destroy(entities[i]);
    	}

    	public Entity GameObjectToEntity(GameObject go)
    	{
    		//Entity light;
    		//light.debugManagerIndex = m_DebugManagerID;
			//light.index = go.GetInstanceID();

            return new Entity();
    	}

		int RemoveComponentFromEntityTable(Entity entity, int typeIndex)
		{
            EntityComponentData* componentData = m_Entities[entity.index].componentData;
            int componentCount = m_Entities[entity.index].componentCount;
            for (int i = 0; i != componentCount; i++)
            {
                if (typeIndex == componentData[i].typeIndex)
                {
                    int componentIndex = componentData [i].componentIndex;
                    componentData[i] = componentData[componentCount - 1];
                    m_Entities [entity.index].componentCount = componentCount - 1;
                    return componentIndex;
                }
            }

            return -1;
		}

		// * NOTE: Does not modify m_EntityToComponent
		void RemoveEntityFromTuples (Entity entity, int componentTypeIndex)
		{
			foreach (var tuple in m_EntityGroupsForComponent[componentTypeIndex])
				tuple.tupleSystem.RemoveSwapBackComponentData(entity);
		}

		public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
		{
			Assert.IsTrue (HasComponent<T>(entity));

			int componentTypeIndex = GetTypeIndex<T> ();
			int componentIndex = RemoveComponentFromEntityTable (entity, componentTypeIndex);

			m_ComponentManagers[componentTypeIndex].RemoveElement (componentIndex);
			RemoveEntityFromTuples (entity, componentTypeIndex);
		}

    	public void Destroy (Entity entity)
    	{
            Assert.IsTrue (Exists (entity));

            EntityComponentData* componentData = m_Entities[entity.index].componentData;
            int componentCount = m_Entities[entity.index].componentCount;
            for (int i = 0; i != componentCount; i++)
            {
                m_ComponentManagers[componentData[i].typeIndex].RemoveElement(componentData[i].componentIndex);
            }

            EntityGroupData* groupData = m_Entities[entity.index].groupData;
            int groupCount = m_Entities[entity.index].groupCount;

            for (int i = 0; i != groupCount; i++)
                m_EntityGroups [groupData [i].groupIndex].RemoveSwapBackTupleIndex (groupData [i].indexInGroup, false);

            DeallocateEntity (entity);
    	}

		internal void RegisterTuple(int componentTypeIndex, EntityGroup tuple, int tupleSystemIndex)
		{
			m_EntityGroupsForComponent [componentTypeIndex].Add (new EntityGroup.RegisteredTuple (tuple, tupleSystemIndex));
		}

        internal int AddEntityGroup(EntityGroup group)
        {
            m_EntityGroups.Add (group);
            return m_EntityGroups.Count - 1;
        }
    }
#endif // !ECS_ENTITY_TABLE
}