using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Experimental.LowLevel;

namespace UnityEngine.ECS
{
	// Updating before or after an engine system guarantees it is in the same update phase as the dependency
	// Update After a phase means in that pase but after all engine systems, Before a phase means in that phase but before all engine systems
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class UpdateBefore : System.Attribute
	{
		public Type  SystemType { get; set; }

		public UpdateBefore(Type systemType)
		{
		    SystemType = systemType;
		}
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class UpdateAfter : System.Attribute
	{
		public Type  SystemType { get; set; }

		public UpdateAfter(Type systemType)
		{
		    SystemType = systemType;
		}
	}
	// Updating in a group means all dependencies from that group are inherited. A system can be in multiple goups
	// There is nothing preventing systems from being in multiple groups, it can be added if there is a use-case for it
	[AttributeUsage(AttributeTargets.Class)]
	public class UpdateInGroup : System.Attribute
	{
		public Type  GroupType { get; set; }

		public UpdateInGroup(Type groupType)
		{
		    GroupType = groupType;
		}
	}

	public static class ScriptBehaviourUpdateOrder
	{
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
				manager.Update();
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
							msg += (firstType ? "" : ", ") + circularType;
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

			public void AddUpdateBeforeToAllChildBehaviours(DependantBehavior target, Dictionary<Type, DependantBehavior> dependencies)
			{
                Type dep = target.manager.GetType();
				foreach (var manager in managers)
				{
					DependantBehavior managerDep;
					if (dependencies.TryGetValue(manager, out managerDep))
                    {
                        target.updateAfter.Add(manager);
						managerDep.updateBefore.Add(dep);
                    }
				}
				foreach (var group in groups)
					group.AddUpdateBeforeToAllChildBehaviours(target, dependencies);
			}
			public void AddUpdateAfterToAllChildBehaviours(DependantBehavior target, Dictionary<Type, DependantBehavior> dependencies)
			{
                Type dep = target.manager.GetType();
				foreach (var manager in managers)
				{
					DependantBehavior managerDep;
					if (dependencies.TryGetValue(manager, out managerDep))
                    {
                        target.updateBefore.Add(manager);
						managerDep.updateAfter.Add(dep);
                    }
				}
				foreach (var group in groups)
					group.AddUpdateAfterToAllChildBehaviours(target, dependencies);
			}

			public Type groupType;
			public List<Type> managers;
			public List<ScriptBehaviourGroup> groups;
			public List<ScriptBehaviourGroup> parents;

			public HashSet<Type> updateBefore;
			public HashSet<Type> updateAfter;
		}

		class DependantBehavior
		{
			public DependantBehavior(ScriptBehaviourManager man)
			{
				manager = man;
				updateBefore = new HashSet<Type>();
				updateAfter = new HashSet<Type>();
				minInsertPos = 0;
				maxInsertPos = 0;
				spawnsJobs = false;
				waitsForJobs = false;

				unvalidatedSystemsUpdatingBefore = 0;
				longestSystemsUpdatingBeforeChain = 0;
				longestSystemsUpdatingAfterChain = 0;
			}
			public ScriptBehaviourManager manager;
			public HashSet<Type> updateBefore;
			public HashSet<Type> updateAfter;
			public int minInsertPos;
			public int maxInsertPos;
			public bool spawnsJobs;
			public bool waitsForJobs;

			public int unvalidatedSystemsUpdatingBefore;
			public int longestSystemsUpdatingBeforeChain;
			public int longestSystemsUpdatingAfterChain;
		}

		// Try to find a system of the specified type in the default playerloop and update the min / max insertion position
		static void UpdateInsertionPos(DependantBehavior target, Type dep, PlayerLoopSystem defaultPlayerLoop, bool after)
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

		static void AddDependencies(DependantBehavior targetSystem, Dictionary<Type, DependantBehavior> dependencies, Dictionary<Type, ScriptBehaviourGroup> allGroups, PlayerLoopSystem defaultPlayerLoop)
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
					otherGroup.AddUpdateBeforeToAllChildBehaviours(targetSystem, dependencies);
				}
				else
				{
					UpdateInsertionPos(targetSystem, attribDep.SystemType, defaultPlayerLoop, true);
				}
			}
			attribs = target.GetCustomAttributes(typeof(UpdateBefore), true);
			for (int i = 0; i < attribs.Length; ++i)
			{
				var attribDep = attribs[i] as UpdateBefore;
				DependantBehavior otherSystem;
				ScriptBehaviourGroup otherGroup;
				if (dependencies.TryGetValue(attribDep.SystemType, out otherSystem))
				{
					targetSystem.updateBefore.Add(attribDep.SystemType);
					otherSystem.updateAfter.Add(target);
				}
				else if (allGroups.TryGetValue(attribDep.SystemType, out otherGroup))
				{
					otherGroup.AddUpdateAfterToAllChildBehaviours(targetSystem, dependencies);
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
							otherGroup.AddUpdateBeforeToAllChildBehaviours(targetSystem, dependencies);
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
							otherGroup.AddUpdateAfterToAllChildBehaviours(targetSystem, dependencies);
						}
						else
						{
							UpdateInsertionPos(targetSystem, dep, defaultPlayerLoop, false);
						}
					}
				}
			}
		}

		static Dictionary<Type, DependantBehavior> BuildSystemGraph(IEnumerable<ScriptBehaviourManager> activeManagers, PlayerLoopSystem defaultPlayerLoop)
		{
			// Collect all groups and create empty dependency data
			Dictionary<Type, ScriptBehaviourGroup> allGroups = new Dictionary<Type, ScriptBehaviourGroup>();
			Dictionary<Type, DependantBehavior> dependencies = new Dictionary<Type, DependantBehavior>();
			foreach (var manager in activeManagers)
			{
				var attribs = manager.GetType().GetCustomAttributes(typeof(UpdateInGroup), true);
				for (int grpIdx = 0; grpIdx < attribs.Length; ++grpIdx)
				{
					var grp = attribs[grpIdx] as UpdateInGroup;
					ScriptBehaviourGroup groupData;
					if (!allGroups.TryGetValue(grp.GroupType, out groupData))
						groupData = new ScriptBehaviourGroup(grp.GroupType, allGroups);
					groupData.managers.Add(manager.GetType());
				}

				DependantBehavior dep = new DependantBehavior(manager);
				dependencies.Add(manager.GetType(), dep);
			}

			// @TODO: apply additional sideloaded constraints here

			// Apply the update before / after dependencies
			foreach (var manager in dependencies)
			{
				// @TODO: need to deal with extracting dependencies for GenericProcessComponentSystem
				AddDependencies(manager.Value, dependencies, allGroups, defaultPlayerLoop);
			}

			ValidateAndFixSystemGraph(dependencies);

			return dependencies;
		}

		static void ValidateAndFixSystemGraph(Dictionary<Type, DependantBehavior> dependencyGraph)
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
				system.longestSystemsUpdatingAfterChain = 0;
			}

			// Check for circular dependencies, start with all systems updating first, mark all systems it updates after as having one more validated dep and start over
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
				}
			}

			// Validate that the chains are not over constrained with combinations of system and engine dependencies
			foreach (var typeAndSystem in dependencyGraph)
			{
				var system = typeAndSystem.Value;
				if (system.updateBefore.Count == 0)
				{
					ValidateAndFixSingleChainMaxPos(system, dependencyGraph, system.maxInsertPos);
				}
				if (system.updateAfter.Count == 0)
				{
					ValidateAndFixSingleChainMinPos(system, dependencyGraph, system.minInsertPos);
				}
			}
		}
		static void ValidateAndFixSingleChainMinPos(DependantBehavior system, Dictionary<Type, DependantBehavior> dependencyGraph, int minInsertPos)
		{
			foreach (var nextInChain in system.updateBefore)
			{
				var nextSys = dependencyGraph[nextInChain];
				if (system.longestSystemsUpdatingBeforeChain >= nextSys.longestSystemsUpdatingBeforeChain)
					nextSys.longestSystemsUpdatingBeforeChain = system.longestSystemsUpdatingBeforeChain + 1;
				if (nextSys.minInsertPos < minInsertPos)
					nextSys.minInsertPos = minInsertPos;
				if (nextSys.maxInsertPos > 0 && nextSys.maxInsertPos < nextSys.minInsertPos)
				{
					Debug.LogError(string.Format("{0} is over constrained with engine and system containts - ignoring dependencies", nextInChain));
					nextSys.maxInsertPos = nextSys.minInsertPos;
				}
				ValidateAndFixSingleChainMinPos(nextSys, dependencyGraph, nextSys.minInsertPos);
			}
		}
		static void ValidateAndFixSingleChainMaxPos(DependantBehavior system, Dictionary<Type, DependantBehavior> dependencyGraph, int maxInsertPos)
		{
			foreach (var prevInChain in system.updateAfter)
			{
				var prevSys = dependencyGraph[prevInChain];
				if (system.longestSystemsUpdatingAfterChain >= prevSys.longestSystemsUpdatingAfterChain)
					prevSys.longestSystemsUpdatingAfterChain = system.longestSystemsUpdatingAfterChain + 1;
				if (prevSys.maxInsertPos == 0 || prevSys.maxInsertPos > maxInsertPos)
					prevSys.maxInsertPos = maxInsertPos;
				if (prevSys.maxInsertPos > 0 && prevSys.maxInsertPos < prevSys.minInsertPos)
				{
					Debug.LogError(string.Format("{0} is over constrained with engine and system containts - ignoring dependencies", prevInChain));
					prevSys.minInsertPos = prevSys.maxInsertPos;
				}
				ValidateAndFixSingleChainMaxPos(prevSys, dependencyGraph, prevSys.maxInsertPos);
			}
		}

		class InsertionBucket : IComparable
		{
			public InsertionBucket()
			{
				insertPos = 0;
				insertSubPos = 0;
				systems = new List<DependantBehavior>();
			}
			public int insertPos;
			public int insertSubPos;
			public List<DependantBehavior> systems;

			public int CompareTo(object other)
			{
				var otherBucket = other as InsertionBucket;
				if (insertPos == otherBucket.insertPos)
					return insertSubPos - otherBucket.insertSubPos;
				return insertPos - otherBucket.insertPos;
			}
		}

		static void MarkSchedulingAndWaitingJobs(Dictionary<Type, DependantBehavior> dependencyGraph)
		{
			// @TODO: sync rules for read-only
			HashSet<DependantBehavior> schedulers = new HashSet<DependantBehavior>();
			foreach (var systemKeyValue in dependencyGraph)
			{
				var system = systemKeyValue.Value;
				// @TODO: GenericProcessComponentSystem
				// @TODO: attribute
				if (!(system is JobComponentSystem))
					continue;
				system.spawnsJobs = true;
				schedulers.Add(system);
			}
			foreach (var systemKeyValue in dependencyGraph)
			{
				var system = systemKeyValue.Value;
				// @TODO: attribute for sync
				if (!(system is ComponentSystem))
					continue;
				HashSet<Type> waitComponent = new HashSet<Type>();
				foreach (var componentGroup in (system.manager as ComponentSystem).ComponentGroups)
				{
					foreach (var type in componentGroup.Types)
						waitComponent.Add(type);
				}
				foreach (var scheduler in schedulers)
				{
					// Check if the component groups overlaps
					HashSet<Type> scheduleComponent = new HashSet<Type>();
					foreach (var componentGroup in (scheduler.manager as ComponentSystem).ComponentGroups)
					{
						foreach (var type in componentGroup.Types)
							scheduleComponent.Add(type);
					}
					bool overlap = false;
					foreach (var waitComp in waitComponent)
					{
						if (scheduleComponent.Contains(waitComp))
						{
							overlap = true;
							break;
						}
					}
					if (overlap)
					{
						system.waitsForJobs = true;
						break;
					}
				}
			}
		}

		public static PlayerLoopSystem InsertWorldManagersInPlayerLoop(PlayerLoopSystem defaultPlayerLoop, params World[] worlds)
        {
            List<InsertionBucket> systemList = new List<InsertionBucket>();
            foreach (var world in worlds)
            {
                if (world.BehaviourManagers.Count() == 0)
                    continue;
                systemList.AddRange(CreateSystemDependencyList(world.BehaviourManagers, defaultPlayerLoop));
            }
            return CreatePlayerLoop(systemList, defaultPlayerLoop);
        }

		public static PlayerLoopSystem InsertManagersInPlayerLoop(IEnumerable<ScriptBehaviourManager> activeManagers, PlayerLoopSystem defaultPlayerLoop)
        {
			if (activeManagers.Count() == 0)
				return defaultPlayerLoop;

            var list = CreateSystemDependencyList(activeManagers, defaultPlayerLoop);
            return CreatePlayerLoop(list, defaultPlayerLoop);
        }

        static List<InsertionBucket> CreateSystemDependencyList(IEnumerable<ScriptBehaviourManager> activeManagers, PlayerLoopSystem defaultPlayerLoop)
		{
			Dictionary<Type, DependantBehavior> dependencyGraph = BuildSystemGraph(activeManagers, defaultPlayerLoop);

			MarkSchedulingAndWaitingJobs(dependencyGraph);

			// Figure out which systems should be inserted early or late
			HashSet<DependantBehavior> earlyUpdates = new HashSet<DependantBehavior>();
			HashSet<DependantBehavior> normalUpdates = new HashSet<DependantBehavior>();
			HashSet<DependantBehavior> lateUpdates = new HashSet<DependantBehavior>();
			foreach (var dependency in dependencyGraph)
			{
				var system = dependency.Value;
				if (system.spawnsJobs)
					earlyUpdates.Add(system);
				else if (system.waitsForJobs)
					lateUpdates.Add(system);
				else
					normalUpdates.Add(system);
			}
			List<DependantBehavior> depsToAdd = new List<DependantBehavior>();
			while (true)
			{
				foreach (var sys in earlyUpdates)
				{
					foreach (var depType in sys.updateAfter)
					{
						var depSys = dependencyGraph[depType];
						if (normalUpdates.Remove(depSys) || lateUpdates.Remove(depSys))
							depsToAdd.Add(depSys);
					}
				}
				if (depsToAdd.Count == 0)
					break;
				foreach (var dep in depsToAdd)
					earlyUpdates.Add(dep);
			}
			while (true)
			{
				foreach (var sys in lateUpdates)
				{
					foreach (var depType in sys.updateBefore)
					{
						var depSys = dependencyGraph[depType];
						if (normalUpdates.Remove(depSys))
							depsToAdd.Add(depSys);
					}
				}
				if (depsToAdd.Count == 0)
					break;
				foreach (var dep in depsToAdd)
					lateUpdates.Add(dep);
			}

			int defaultPos = 0;
			foreach (var sys in defaultPlayerLoop.subSystemList)
			{
				defaultPos += 1 + sys.subSystemList.Length;
				if (sys.type == typeof(UnityEngine.Experimental.PlayerLoop.Update))
					break;
			}
			Dictionary<int, InsertionBucket> insertionBucketDict = new Dictionary<int, InsertionBucket>();
			// increase the number of dependencies allowed by 1, starting from 0 and add all systems with that many at the first or last possible pos
			// bucket idx is insertion point << 2 | 0,1,2
			// When adding propagate min or max through the chain
			int processedChainLength = 0;
			while (earlyUpdates.Count > 0 || lateUpdates.Count > 0)
			{
				foreach (var sys in earlyUpdates)
				{
					if (sys.longestSystemsUpdatingBeforeChain == processedChainLength)
					{
						if (sys.minInsertPos == 0)
							sys.minInsertPos = defaultPos;
						sys.maxInsertPos = sys.minInsertPos;
						depsToAdd.Add(sys);
						foreach (var nextSys in sys.updateBefore)
						{
							if (dependencyGraph[nextSys].minInsertPos < sys.minInsertPos)
								dependencyGraph[nextSys].minInsertPos = sys.minInsertPos;
						}
					}
				}
				foreach (var sys in lateUpdates)
				{
					if (sys.longestSystemsUpdatingAfterChain == processedChainLength)
					{
						if (sys.maxInsertPos == 0)
							sys.maxInsertPos = defaultPos;
						sys.minInsertPos = sys.maxInsertPos;
						depsToAdd.Add(sys);
						foreach (var prevSys in sys.updateAfter)
						{
							if (dependencyGraph[prevSys].maxInsertPos == 0 || dependencyGraph[prevSys].maxInsertPos > sys.maxInsertPos)
								dependencyGraph[prevSys].maxInsertPos = sys.maxInsertPos;
						}
					}
				}

				foreach (var sys in depsToAdd)
				{
					earlyUpdates.Remove(sys);
					bool isLate = lateUpdates.Remove(sys);
					int subIndex = isLate ? 2 : 0;

					// Bucket to insert in is minPos == maxPos
					int bucketIndex = (sys.minInsertPos << 2) | subIndex;
					InsertionBucket bucket;
					if (!insertionBucketDict.TryGetValue(bucketIndex, out bucket))
					{
						bucket = new InsertionBucket();
						bucket.insertPos = sys.minInsertPos;
						bucket.insertSubPos = subIndex;
						insertionBucketDict.Add(bucketIndex, bucket);
					}
					bucket.systems.Add(sys);
				}

				depsToAdd.Clear();
				++processedChainLength;
			}
			processedChainLength = 0;
			while (normalUpdates.Count > 0)
			{
				foreach (var sys in normalUpdates)
				{
					if (sys.longestSystemsUpdatingBeforeChain == processedChainLength)
					{
						if (sys.minInsertPos == 0)
							sys.minInsertPos = defaultPos;
						sys.maxInsertPos = sys.minInsertPos;
						depsToAdd.Add(sys);
						foreach (var nextSys in sys.updateBefore)
						{
							if (dependencyGraph[nextSys].minInsertPos < sys.minInsertPos)
								dependencyGraph[nextSys].minInsertPos = sys.minInsertPos;
						}
					}
				}

				foreach (var sys in depsToAdd)
				{
					normalUpdates.Remove(sys);
					int subIndex = 1;

					// Bucket to insert in is minPos == maxPos
					int bucketIndex = (sys.minInsertPos << 2) | subIndex;
					InsertionBucket bucket;
					if (!insertionBucketDict.TryGetValue(bucketIndex, out bucket))
					{
						bucket = new InsertionBucket();
						bucket.insertPos = sys.minInsertPos;
						bucket.insertSubPos = subIndex;
						insertionBucketDict.Add(bucketIndex, bucket);
					}
					bucket.systems.Add(sys);
				}

				depsToAdd.Clear();
				++processedChainLength;
			}
            return new List<InsertionBucket>(insertionBucketDict.Values);
		}

        static PlayerLoopSystem CreatePlayerLoop(List<InsertionBucket> insertionBuckets, PlayerLoopSystem defaultPlayerLoop)
        {
			insertionBuckets.Sort();

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
					if (bucket.insertPos >= firstPos && bucket.insertPos <= lastPos)
						systemsToInsert += bucket.systems.Count;
				}
				ecsPlayerLoop.subSystemList[i] = defaultPlayerLoop.subSystemList[i];
				if (systemsToInsert > 0)
				{
					ecsPlayerLoop.subSystemList[i].subSystemList = new PlayerLoopSystem[defaultPlayerLoop.subSystemList[i].subSystemList.Length + systemsToInsert];
					int dstPos = 0;
					for (int srcPos = 0; srcPos < defaultPlayerLoop.subSystemList[i].subSystemList.Length; ++srcPos, ++dstPos)
					{
						while (currentBucket < insertionBuckets.Count && insertionBuckets[currentBucket].insertPos <= firstPos+srcPos)
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
					while (currentBucket < insertionBuckets.Count && insertionBuckets[currentBucket].insertPos <= lastPos)
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
				currentPos = lastPos;
			}

			return ecsPlayerLoop;
        }

    }
}
