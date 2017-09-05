using System.Collections.Generic;
using UnityEngine.Collections;

namespace UnityEngine.ECS
{
	#if ECS_ENTITY_CLASS
	public class EntityClassEqualityComparer : IEqualityComparer<NativeList<int>>
	{
		public bool Equals(NativeList<int> a, NativeList<int> b)
		{
			if (a.Length != b.Length)
				return false;
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] != b[i])
					return false;
			}
			return true;
		}

		public int GetHashCode(NativeList<int> a)
		{
			int hash = a.Length;
			for (int i = 0; i < a.Length; i++)
			{
				hash = hash ^ a[i];
			}
			return hash;
		}
	}

	internal struct EntityClass
	{
		public bool hasTransform;
		public NativeList<int> componentTypes;
		public NativeList<int> transformKeyHack;
		public NativeList<Entity> entities;
		public NativeList<int> componentDataIndices;

		public void Remove(NativeHashMap<int, EntityData> entityDataMap, int index)
		{
			int removedEnt = entities[index].index;
			int offset = index * componentTypes.Length;
			int lastOffset = componentDataIndices.Length - componentTypes.Length;
			for (int i = componentTypes.Length-1; i >= 0; --i)
			{
				componentDataIndices[offset+i] = componentDataIndices[lastOffset+i];
				componentDataIndices.RemoveAtSwapBack(lastOffset+i);
			}
			int lastIndex = entities.Length-1;
			entities[index] = entities[lastIndex];
			entities.RemoveAtSwapBack(lastIndex);

			EntityData entityData;
			if (entities.Length > 0 && index != lastIndex && entityDataMap.TryGetValue(entities[index].index, out entityData))
			{
				entityDataMap.Remove(entities[index].index);
				entityData.entityIdxInClass = index;
				entityDataMap.TryAdd(entities[index].index, entityData);
			}
			entityDataMap.Remove(removedEnt);
		}
		public int MoveTo(NativeHashMap<int, EntityData> entityDataMap, int targetClassIndex, EntityClass target, int index, int newType = -1, int newIndex = -1)
		{
			int sourceIdx = 0;
			int targetIdx = 0;
			int offset = index * componentTypes.Length;
			while (targetIdx < target.componentTypes.Length)
			{
				if (sourceIdx < componentTypes.Length && target.componentTypes[targetIdx] == componentTypes[sourceIdx])
				{
					target.componentDataIndices.Add(componentDataIndices[offset+sourceIdx]);
					++sourceIdx;
					++targetIdx;
				}
				else if (target.componentTypes[targetIdx] == newType)
				{
					target.componentDataIndices.Add(newIndex);
					++targetIdx;
				}
				else
				{
					++sourceIdx;
				}
			}
			targetIdx = target.entities.Length;
			target.entities.Add(entities[index]);
			Remove(entityDataMap, index);
			EntityData entityData;
			entityData.classIdx = targetClassIndex;
			entityData.entityIdxInClass = targetIdx;
			entityDataMap.TryAdd(target.entities[targetIdx].index, entityData);
			return targetIdx;
		}
	}
	#endif // ECS_ENTITY_CLASS
}
