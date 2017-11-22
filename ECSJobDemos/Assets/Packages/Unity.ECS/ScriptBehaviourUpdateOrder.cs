using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.Experimental.LowLevel;

namespace UnityEngine.ECS
{
	// Updating before or after an engine system guarantees it is in the same update phase as the dependency
	// Update After a phase means in that pase but after all engine systems, Before a phase means in that phase but before all engine systems
	[AttributeUsage(AttributeTargets.Class)]
	public class UpdateBefore : System.Attribute
	{
		public Type  SystemType { get; set; }

		public UpdateBefore(Type systemType)
		{
		    SystemType = systemType;
		}
	}
	[AttributeUsage(AttributeTargets.Class)]
	public class UpdateAfter : System.Attribute
	{
		public Type  SystemType { get; set; }

		public UpdateAfter(Type systemType)
		{
		    SystemType = systemType;
		}
	}
	// Updating in a group means all dependencies from that group are inherited. A system can be in multiple goups
	[AttributeUsage(AttributeTargets.Class)]
	public class UpdateInGroup : System.Attribute
	{
		public Type  GroupType { get; set; }

		public UpdateInGroup(Type groupType)
		{
		    GroupType = groupType;
		}
	}

	public struct UpdateOrderConstraint
	{
		Type systemBefore;
		Type systemAfter;
	}

	public class ScriptBehaviourUpdateOrder
	{
		List<UpdateOrderConstraint> m_UpdateOrderConstraints = new List<UpdateOrderConstraint>();

		public List<UpdateOrderConstraint> UpdateOrderConstraints {get{return m_UpdateOrderConstraints;}}

		// FIXME: HACK! - mono 4.6 has problems invoking virtual methods as delegates from native, so wrap the invocation in a non-virtual class
		public class DummyDelagateWrapper
		{
			public DummyDelagateWrapper(ScriptBehaviourManager man)
			{
				manager = man;
			}
			private ScriptBehaviourManager manager;
			public void TriggerUpdate()
			{
				manager.OnUpdate();
				Unity.Jobs.JobHandle.ScheduleBatchedJobs();
			}
		}

		class ScriptBehaviourGroup
		{
			public ScriptBehaviourGroup(Type grpType, Dictionary<Type, ScriptBehaviourGroup> allGroups, HashSet<Type> circularCheck = null)
			{
				groupType = grpType;
				managers = new List<Type>();
				groups = new List<ScriptBehaviourGroup>();
				parents = new List<ScriptBehaviourGroup>();
				updateBefore = new HashSet<Type>();
				updateAfter = new HashSet<Type>();

				var attribs = grpType.GetCustomAttributes(typeof(UpdateAfter), true);
				for (int i = 0; i < attribs.Length; ++i)
				{
					var attribDep = attribs[i] as UpdateAfter;
					updateAfter.Add(attribDep.SystemType);
				}
				attribs = grpType.GetCustomAttributes(typeof(UpdateBefore), true);
				for (int i = 0; i < attribs.Length; ++i)
				{
					var attribDep = attribs[i] as UpdateAfter;
					updateBefore.Add(attribDep.SystemType);
				}

				allGroups.Add(groupType, this);

				attribs = groupType.GetCustomAttributes(typeof(UpdateInGroup), true);
				for (int grpIdx = 0; grpIdx < attribs.Length; ++grpIdx)
				{
					if (circularCheck == null)
					{
						circularCheck = new HashSet<Type>();
						circularCheck.Add(groupType);
					}
					var parentGrp = attribs[grpIdx] as UpdateInGroup;
					if (!circularCheck.Add(parentGrp.GroupType))
					{
						// Found circular dependency
						string msg = "Found circular chain in update groups involving: ";
						bool firstType = true;
						foreach (var circularType in circularCheck)
						{
							msg += (firstType ? ", " : "") + circularType;
							firstType = false;
						}
						UnityEngine.Debug.LogError(msg);
					}
					ScriptBehaviourGroup parentGroupData;
					if (!allGroups.TryGetValue(parentGrp.GroupType, out parentGroupData))
						parentGroupData = new ScriptBehaviourGroup(parentGrp.GroupType, allGroups, circularCheck);
					circularCheck.Remove(parentGrp.GroupType);
					parentGroupData.groups.Add(this);
					parents.Add(parentGroupData);

					foreach (var dep in parentGroupData.updateBefore)
						updateBefore.Add(dep);
					foreach (var dep in parentGroupData.updateAfter)
						updateAfter.Add(dep);
				}
			}

			public void AddUpdateBeforeToAllChildBehaviours(Type dep, Dictionary<Type, DependantBehavior> dependencies)
			{
				foreach (var manager in managers)
				{
					DependantBehavior managerDep;
					if (dependencies.TryGetValue(manager, out managerDep))
						managerDep.updateBefore.Add(dep);
				}
				foreach (var group in groups)
					group.AddUpdateBeforeToAllChildBehaviours(dep, dependencies);
			}
			public void AddUpdateAfterToAllChildBehaviours(Type dep, Dictionary<Type, DependantBehavior> dependencies)
			{
				foreach (var manager in managers)
				{
					DependantBehavior managerDep;
					if (dependencies.TryGetValue(manager, out managerDep))
						managerDep.updateAfter.Add(dep);
				}
				foreach (var group in groups)
					group.AddUpdateAfterToAllChildBehaviours(dep, dependencies);
			}

			public Type groupType;
			public List<Type> managers;
			public List<ScriptBehaviourGroup> groups;
			public List<ScriptBehaviourGroup> parents;

			public HashSet<Type> updateBefore;
			public HashSet<Type> updateAfter;
		}

		class DependantBehavior : IComparable
		{
			public DependantBehavior(ScriptBehaviourManager man)
			{
				manager = man;
				updateBefore = new HashSet<Type>();
				updateAfter = new HashSet<Type>();
				minInsertPos = 0;
				maxInsertPos = 0;
				spawnsJobs = false;

				unvalidatedSystemsUpdatingBefore = 0;
				longestSystemsUpdatingBeforeChain = 0;
			}
			public int CompareTo(object other)
			{
				return longestSystemsUpdatingBeforeChain - (other as DependantBehavior).longestSystemsUpdatingBeforeChain;
			}
			public ScriptBehaviourManager manager;
			public HashSet<Type> updateBefore;
			public HashSet<Type> updateAfter;
			public int minInsertPos;
			public int maxInsertPos;
			public bool spawnsJobs;

			public int unvalidatedSystemsUpdatingBefore;
			public int longestSystemsUpdatingBeforeChain;
		}

		void UpdateInsertionPos(DependantBehavior target, Type dep, PlayerLoopSystem defaultPlayerLoop, bool after)
		{
			int pos = 0;
			foreach (var sys in defaultPlayerLoop.subSystemList)
			{
				++pos;
				if (sys.type == dep)
				{
					if (after)
					{
						pos += sys.subSystemList.Length;
						if (target.minInsertPos < pos)
							target.minInsertPos = pos;
						if (target.maxInsertPos == 0 || target.maxInsertPos > pos)
							target.maxInsertPos = pos;						
					}
					else
					{
						if (target.minInsertPos < pos)
							target.minInsertPos = pos;
						if (target.maxInsertPos == 0 || target.maxInsertPos > pos)
							target.maxInsertPos = pos;
					}
					return;
				}

				int beginPos = pos;
				int endPos = pos + sys.subSystemList.Length;
				foreach (var subsys in sys.subSystemList)
				{
					if (subsys.type == dep)
					{
						if (after)
						{
							++pos;
							if (target.minInsertPos < pos)
								target.minInsertPos = pos;
							if (target.maxInsertPos == 0 || target.maxInsertPos > endPos)
								target.maxInsertPos = endPos;
						}
						else
						{
							if (target.minInsertPos < beginPos)
								target.minInsertPos = beginPos;
							if (target.maxInsertPos == 0 || target.maxInsertPos > pos)
								target.maxInsertPos = pos;
						}
						return;
					}
					++pos;
				}
			}
			// System was not found
		}

		void AddDependencies(DependantBehavior targetSystem, Dictionary<Type, DependantBehavior> dependencies, Dictionary<Type, ScriptBehaviourGroup> allGroups, PlayerLoopSystem defaultPlayerLoop)
		{
			var target = targetSystem.manager.GetType();
			var attribs = target.GetCustomAttributes(typeof(UpdateAfter), true);
			for (int i = 0; i < attribs.Length; ++i)
			{
				var attribDep = attribs[i] as UpdateAfter;
				DependantBehavior otherSystem;
				ScriptBehaviourGroup otherGroup;
				if (dependencies.TryGetValue(attribDep.SystemType, out otherSystem))
				{
					targetSystem.updateAfter.Add(attribDep.SystemType);
					otherSystem.updateBefore.Add(target);
				}
				else if (allGroups.TryGetValue(attribDep.SystemType, out otherGroup))
				{
					targetSystem.updateAfter.Add(attribDep.SystemType);
					otherGroup.AddUpdateBeforeToAllChildBehaviours(target, dependencies);
				}
				else
				{
					UpdateInsertionPos(targetSystem, attribDep.SystemType, defaultPlayerLoop, true);
				}
			}
			attribs = target.GetCustomAttributes(typeof(UpdateBefore), true);
			for (int i = 0; i < attribs.Length; ++i)
			{
				var attribDep = attribs[i] as UpdateAfter;
				DependantBehavior otherSystem;
				ScriptBehaviourGroup otherGroup;
				if (dependencies.TryGetValue(attribDep.SystemType, out otherSystem))
				{
					targetSystem.updateBefore.Add(attribDep.SystemType);
					otherSystem.updateAfter.Add(target);
				}
				else if (allGroups.TryGetValue(attribDep.SystemType, out otherGroup))
				{
					targetSystem.updateBefore.Add(attribDep.SystemType);
					otherGroup.AddUpdateAfterToAllChildBehaviours(target, dependencies);
				}
				else
				{
					UpdateInsertionPos(targetSystem, attribDep.SystemType, defaultPlayerLoop, false);
				}
			}
			attribs = target.GetCustomAttributes(typeof(UpdateInGroup), true);
			for (int i = 0; i < attribs.Length; ++i)
			{
				var attribDep = attribs[i] as UpdateInGroup;
				ScriptBehaviourGroup group;
				if (allGroups.TryGetValue(attribDep.GroupType, out group))
				{
					DependantBehavior otherSystem;
					ScriptBehaviourGroup otherGroup;
					foreach (var dep in group.updateAfter)
					{
						if (dependencies.TryGetValue(dep, out otherSystem))
						{
							targetSystem.updateAfter.Add(dep);
							otherSystem.updateBefore.Add(target);
						}
						else if (allGroups.TryGetValue(dep, out otherGroup))
						{
							targetSystem.updateAfter.Add(dep);
							otherGroup.AddUpdateBeforeToAllChildBehaviours(target, dependencies);
						}
						else
						{
							UpdateInsertionPos(targetSystem, dep, defaultPlayerLoop, true);
						}
					}
					foreach (var dep in group.updateBefore)
					{
						if (dependencies.TryGetValue(dep, out otherSystem))
						{
							targetSystem.updateBefore.Add(dep);
							otherSystem.updateAfter.Add(target);
						}
						else if (allGroups.TryGetValue(dep, out otherGroup))
						{
							targetSystem.updateBefore.Add(dep);
							otherGroup.AddUpdateAfterToAllChildBehaviours(target, dependencies);
						}
						else
						{
							UpdateInsertionPos(targetSystem, dep, defaultPlayerLoop, false);
						}
					}
				}
			}
		}

		Dictionary<Type, DependantBehavior> BuildSystemGraph(HashSet<ScriptBehaviourManager> activeManagers, PlayerLoopSystem defaultPlayerLoop)
		{
			// Collect all groups and create empty dependency data
			Dictionary<Type, ScriptBehaviourGroup> allGroups = new Dictionary<Type, ScriptBehaviourGroup>();
			Dictionary<Type, DependantBehavior> dependencies = new Dictionary<Type, DependantBehavior>();
			foreach (var manager in activeManagers)
			{
				var attribs = manager.GetType().GetCustomAttributes(typeof(UpdateInGroup), true);
				for (int grpIdx = 0; grpIdx < attribs.Length; ++grpIdx)
				{
					var grp = attribs[0] as UpdateInGroup;
					ScriptBehaviourGroup groupData;
					if (!allGroups.TryGetValue(grp.GroupType, out groupData))
						groupData = new ScriptBehaviourGroup(grp.GroupType, allGroups);
					groupData.managers.Add(manager.GetType());
				}

				DependantBehavior dep = new DependantBehavior(manager);
				// @TODO: should have an attribute for spawns jobs, or syncs jobs spawned by
				// @TODO: need to deal with extracting dependencies for GenericProcessComponentSystem
				dep.spawnsJobs = (manager is JobComponentSystem);// || (manager is GenericProcessComponentSystem);
				dependencies.Add(manager.GetType(), dep);				
			}

			// @TODO: apply additional sideloaded constraints here

			// Apply the update before / after dependencies
			foreach (var manager in dependencies)
			{
				AddDependencies(manager.Value, dependencies, allGroups, defaultPlayerLoop);
			}

			ValidateAndFixSystemGraph(dependencies);

			return dependencies;
		}

		void ValidateAndFixSystemGraph(Dictionary<Type, DependantBehavior> dependencyGraph)
		{
			// Check for simple over constraints on engine systems
			foreach (var typeAndSystem in dependencyGraph)
			{
				var system = typeAndSystem.Value;
				if (system.minInsertPos > system.maxInsertPos)
				{
					Debug.LogError(string.Format("{0} is over constrained with engine containts - ignoring dependencies", system.manager.GetType()));
					system.minInsertPos = system.maxInsertPos = 0;
				}
				system.unvalidatedSystemsUpdatingBefore = system.updateAfter.Count;
				system.longestSystemsUpdatingBeforeChain = 0;
			}

			// Check for circular dependencies, start with all systems updateing last, mark all systems it updates after as having one more validated dep and start over
			bool progress = true;
			while (progress)
			{
				progress = false;
				foreach (var typeAndSystem in dependencyGraph)
				{
					var system = typeAndSystem.Value;
					if (system.unvalidatedSystemsUpdatingBefore == 0)
					{
						system.unvalidatedSystemsUpdatingBefore = -1;
						foreach (var nextInChain in system.updateBefore)
						{
							if (system.longestSystemsUpdatingBeforeChain >= dependencyGraph[nextInChain].longestSystemsUpdatingBeforeChain)
								dependencyGraph[nextInChain].longestSystemsUpdatingBeforeChain = system.longestSystemsUpdatingBeforeChain + 1;
							--dependencyGraph[nextInChain].unvalidatedSystemsUpdatingBefore;
							progress = true;
						}
					}
				}
			}
			// If some systems were found to have circular dependencies, drop all of them. This is a bit over aggressive - but it only happens on badly setup dependency chains
			foreach (var typeAndSystem in dependencyGraph)
			{
				var system = typeAndSystem.Value;
				if (system.unvalidatedSystemsUpdatingBefore > 0)
				{
					Debug.LogError(string.Format("{0} is in a chain of circular dependencies - ignoring dependencies", system.manager.GetType()));
					foreach (var after in system.updateAfter)
					{
						dependencyGraph[after].updateBefore.Remove(system.manager.GetType());
					}
					system.updateAfter.Clear();
					system.longestSystemsUpdatingBeforeChain = 0;
				}
			}

			// Validate that the chains are not over constrained with combinations of system and engine dependencies
			foreach (var typeAndSystem in dependencyGraph)
			{
				var system = typeAndSystem.Value;
				if (system.updateAfter.Count == 0)
				{
					ValidateAndFixSingleChain(system, dependencyGraph, system.minInsertPos);
				}
			}	
		}
		void ValidateAndFixSingleChain(DependantBehavior system, Dictionary<Type, DependantBehavior> dependencyGraph, int minInsertPos)
		{
			foreach (var after in system.updateAfter)
			{
				var afterSys = dependencyGraph[after];
				if (afterSys.minInsertPos < minInsertPos)
					afterSys.minInsertPos = minInsertPos;
				if (afterSys.maxInsertPos > 0 && afterSys.maxInsertPos < afterSys.minInsertPos)
				{
					Debug.LogError(string.Format("{0} is over constrained with engine and system containts - ignoring dependencies", after));
					afterSys.maxInsertPos = 0;
				}
				ValidateAndFixSingleChain(afterSys, dependencyGraph, afterSys.minInsertPos);
			}
		}

		class InsertionBucket : IComparable
		{
			public InsertionBucket()
			{
				minInsertPos = 0;
				maxInsertPos = 0;
				systems = new List<DependantBehavior>();
			}
			public int minInsertPos;
			public int maxInsertPos;
			public List<DependantBehavior> systems;

			public int CompareTo(object other)
			{
				return minInsertPos - (other as InsertionBucket).minInsertPos;
			}

			public void IncludeAllEarlierJobs(DependantBehavior behave, Dictionary<Type, DependantBehavior> dependencyGraph, HashSet<DependantBehavior> remainingSystems)
			{
				foreach (var sysType in behave.updateAfter)
				{
					var sys = dependencyGraph[sysType];
					if (remainingSystems.Contains(sys))
					{
						systems.Add(sys);
						remainingSystems.Remove(sys);
					}
				}
			}
			public void IncludeAllLaterJobs(DependantBehavior behave, Dictionary<Type, DependantBehavior> dependencyGraph, HashSet<DependantBehavior> remainingSystems)
			{
				foreach (var sysType in behave.updateBefore)
				{
					var sys = dependencyGraph[sysType];
					if (remainingSystems.Contains(sys))
					{
						systems.Add(sys);
						remainingSystems.Remove(sys);
					}
				}
			}
		}
		public PlayerLoopSystem InsertManagersInPlayerLoop(HashSet<ScriptBehaviourManager> activeManagers, PlayerLoopSystem defaultPlayerLoop)
		{
			Dictionary<Type, DependantBehavior> dependencyGraph = BuildSystemGraph(activeManagers, defaultPlayerLoop);

			// Locate all insertion points
			List<InsertionBucket> insertionBuckets = new List<InsertionBucket>();
			HashSet<DependantBehavior> remainingSystems = new HashSet<DependantBehavior>();
			foreach (var sys in dependencyGraph)
			{
				int minPos = sys.Value.minInsertPos;
				int maxPos = sys.Value.maxInsertPos;
				if (minPos != 0 || maxPos != 0)
				{
					// Check if this bucket already exists
					bool added = false;
					foreach (var bucket in insertionBuckets)
					{
						if (bucket.minInsertPos == minPos && bucket.maxInsertPos == maxPos)
						{
							bucket.systems.Add(sys.Value);
							added = true;
							break;
						}
					}
					if (!added)
					{
						var bucket = new InsertionBucket();
						bucket.minInsertPos = minPos;
						bucket.maxInsertPos = maxPos;
						bucket.systems.Add(sys.Value);
					}
				}
				else
					remainingSystems.Add(sys.Value);
			}
			// Sort the buckets, start with "first"
			insertionBuckets.Sort();
			// Merge all subsequent buckets which can be merged to get as few as possible
			for (int i = 0; i < insertionBuckets.Count; ++i)
			{
				var bucket = insertionBuckets[i];
				while (i+1 < insertionBuckets.Count && insertionBuckets[i+1].minInsertPos <= bucket.maxInsertPos)
				{
					// Merge with the other bucket
					bucket.minInsertPos = insertionBuckets[i+1].minInsertPos;
					bucket.maxInsertPos = Math.Min(bucket.maxInsertPos, insertionBuckets[i+1].maxInsertPos);
					foreach (var sys in insertionBuckets[i+1].systems)
						bucket.systems.Add(sys);
					insertionBuckets.RemoveAt(i+1);
				}
			}

			// Pull in everything in the updateAfter list which is not already in a bucket
			foreach (var bucket in insertionBuckets)
			{
				for (int i = 0; i < bucket.systems.Count; ++i)
				{
					var sys = bucket.systems[i];
					bucket.IncludeAllEarlierJobs(sys, dependencyGraph, remainingSystems);
				}
			}
			// Pull in eveything in the updateBefore list which is not already in a bucket
			foreach (var bucket in insertionBuckets)
			{
				for (int i = 0; i < bucket.systems.Count; ++i)
				{
					var sys = bucket.systems[i];
					bucket.IncludeAllLaterJobs(sys, dependencyGraph, remainingSystems);
				}
			}
			// Create a default bucket for all remaining systems
			int defaultPos = 0;
			foreach (var sys in defaultPlayerLoop.subSystemList)
			{
				defaultPos += 1 + sys.subSystemList.Length;
				if (sys.type == typeof(UnityEngine.Experimental.PlayerLoop.Update))
					break;
			}
			// Check if the default pos can be merged with anything
			foreach (var bucket in insertionBuckets)
			{
				if (bucket.minInsertPos <= defaultPos && bucket.maxInsertPos >= defaultPos)
				{
					bucket.minInsertPos = defaultPos;
					bucket.maxInsertPos = defaultPos;
					foreach (var sys in remainingSystems)
						bucket.systems.Add(sys);
					remainingSystems.Clear();
					break;
				}
			}
			if (remainingSystems.Count > 0)
			{
				var bucket = new InsertionBucket();
				bucket.minInsertPos = defaultPos;
				bucket.maxInsertPos = defaultPos;
				foreach (var sys in remainingSystems)
					bucket.systems.Add(sys);
				insertionBuckets.Add(bucket);
				insertionBuckets.Sort();
			}

			// Sort the systems in each bucket while optimizing for giving jobs time to run
			foreach (var bucket in insertionBuckets)
				bucket.systems.Sort();

			// Insert the buckets at the appropriate place
			int currentPos = 0;
			var ecsPlayerLoop = new PlayerLoopSystem();
			ecsPlayerLoop.subSystemList = new PlayerLoopSystem[defaultPlayerLoop.subSystemList.Length];
			int currentBucket = 0;
			for (int i = 0; i < defaultPlayerLoop.subSystemList.Length; ++i)
			{
				int firstPos = currentPos + 1;
				int lastPos = firstPos + defaultPlayerLoop.subSystemList[i].subSystemList.Length;
				// Find all new things to insert here
				int systemsToInsert = 0;
				foreach (var bucket in insertionBuckets)
				{
					if (bucket.minInsertPos >= firstPos && bucket.minInsertPos <= lastPos)
						systemsToInsert += bucket.systems.Count;
				}
				ecsPlayerLoop.subSystemList[i] = defaultPlayerLoop.subSystemList[i];
				if (systemsToInsert > 0)
				{
					ecsPlayerLoop.subSystemList[i].subSystemList = new PlayerLoopSystem[defaultPlayerLoop.subSystemList[i].subSystemList.Length + systemsToInsert];
					int dstPos = 0;
					for (int srcPos = 0; srcPos < defaultPlayerLoop.subSystemList[i].subSystemList.Length; ++srcPos, ++dstPos)
					{
						while (insertionBuckets[currentBucket].minInsertPos <= firstPos+srcPos)
						{
							foreach (var insert in insertionBuckets[currentBucket].systems)
							{
								ecsPlayerLoop.subSystemList[i].subSystemList[dstPos].type = insert.manager.GetType();
								var tmp = new DummyDelagateWrapper(insert.manager);
								ecsPlayerLoop.subSystemList[i].subSystemList[dstPos].updateDelegate = tmp.TriggerUpdate;
								++dstPos;
							}
							++currentBucket;
						}
						ecsPlayerLoop.subSystemList[i].subSystemList[dstPos] = defaultPlayerLoop.subSystemList[i].subSystemList[srcPos];
					}
					while (insertionBuckets[currentBucket].minInsertPos <= lastPos)
					{
						foreach (var insert in insertionBuckets[currentBucket].systems)
						{
							ecsPlayerLoop.subSystemList[i].subSystemList[dstPos].type = insert.manager.GetType();
							var tmp = new DummyDelagateWrapper(insert.manager);
							ecsPlayerLoop.subSystemList[i].subSystemList[dstPos].updateDelegate = tmp.TriggerUpdate;
							++dstPos;
						}
						++currentBucket;
					}
				}
			}

			return defaultPlayerLoop;
		}
		
    }
}
