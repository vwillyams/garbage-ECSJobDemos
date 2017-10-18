using System.Collections;
using System.Collections.Generic;
using UnityEngine.Collections;
using UnityEngine;
using System.Reflection;
using System;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
	//@TODO: Checks to ensure base override is always called.
	[AttributeUsage(AttributeTargets.Class)]
	public class UpdateAfter : System.Attribute
	{
		public Type  SystemType { get; set; }

		public UpdateAfter(Type systemType)
		{
		    SystemType = systemType;
		}
	}

	public class ScriptBehaviourManager
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
				manager.OnUpdate();
				UnityEngine.Jobs.JobHandle.ScheduleBatchedJobs();
			}
		}
		private static HashSet<ScriptBehaviourManager> s_ActiveManagers = new HashSet<ScriptBehaviourManager>();

		internal static void CreateInstance(ScriptBehaviourManager manager, int capacity)
		{
			manager.OnCreateManager(capacity);
		}

		internal static void DestroyInstance(ScriptBehaviourManager inst)
		{
			inst.OnDestroyManager ();
		}

		// NOTE: The comments for behaviour below are how it is supposed to work.
		//       In this prototype several things don't work that way yet...


		/// <summary>
		/// Called when the ScriptBehaviourManager is created.
		/// When a new domain is loaded, OnCreate on the necessary manager will be invoked
		/// before the ScriptBehaviour will receive its first OnCreate() call.
		/// capacity can be configured in Edit -> Configure Memory
		/// </summary>
		/// <param name="capacity">Capacity describes how many objects will register with the manager. This lets you reduce realloc calls while the game is running.</param>
		protected virtual void OnCreateManager(int capacity)
		{
			s_ActiveManagers.Add(this);

			DependencyManager.DependencyInject (this);

			UpdatePlayerLoop();
		}

		/// <summary>
		/// Called when the ScriptBehaviourManager is destroyed.
		/// Before Playmode exits or scripts are reloaded OnDestroy will be called on all created ScriptBehaviourManagers.
		/// </summary>
		protected virtual void OnDestroyManager()
		{
			s_ActiveManagers.Remove(this);
			UpdatePlayerLoop();
		}


		/// <summary>
		/// Called once per frame
		/// </summary>
		public virtual void OnUpdate()
		{
			
		}
		static private int FindPlayerLoopInsertionPoint(Type dependency, List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem> systemList)
		{
			if (dependency == null)
				return 0;
			int insertIndex = -1;
			// Run after an update phase
			for (int i = 0; i < systemList.Count; ++i)
			{
				if (systemList[i].type == dependency)
					insertIndex = i+1;
			}
			return insertIndex;
		}



		private UpdateAfter GetManagerDependency(Type managerType)
		{
			var attribs = managerType.GetCustomAttributes(typeof(UpdateAfter), true);
			if (attribs.Length == 0)
				return new UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreUpdate));
			return attribs[0] as UpdateAfter;
		}
		private void UpdatePlayerLoop()
		{
			// Create buckets for all s_ActiveManagers with same dependency
			var dependencyBuckets = new Dictionary<Type, List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>>();
			var dummyBuckets = new List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>();
			foreach (var manager in s_ActiveManagers)
			{
				// TODO: get dependency from attribute, with default value
				var dependency = GetManagerDependency(manager.GetType());
				List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem> managerList = dummyBuckets;
				if (dependency.SystemType != null)
				{
					if (!dependencyBuckets.TryGetValue(dependency.SystemType, out managerList))
					{
						managerList = new List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>();
						dependencyBuckets.Add(dependency.SystemType, managerList);
					}
				}
				var system = new UnityEngine.Experimental.LowLevel.PlayerLoopSystem();
				system.type = manager.GetType();
				var tmp = new DummyDelagateWrapper(manager);
				system.updateDelegate = tmp.TriggerUpdate;
				//system.updateDelegate = manager.OnUpdate;
				managerList.Add(system);
			}
			if (dummyBuckets.Count > 0)
				dependencyBuckets.Add(null, dummyBuckets);
			// Insert the buckets at the appropriate place in the player loop
			var defaultLoop = UnityEngine.Experimental.LowLevel.PlayerLoop.GetDefaultPlayerLoop();
			if (dependencyBuckets.Count == 0)
			{
				UnityEngine.Experimental.LowLevel.PlayerLoop.SetPlayerLoop(defaultLoop);
				return;
			}
			var systemList = new List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>(defaultLoop.subSystemList);
			var subSystemLists = new List<List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>>(defaultLoop.subSystemList.Length);
			for (int i = 0; i < systemList.Count; ++i)
			{
				subSystemLists.Add(new List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>(defaultLoop.subSystemList[i].subSystemList));
			}

			bool addedMore = true;
			while (dependencyBuckets.Count != 0 && addedMore)
			{
				addedMore = false;
				for (int i = 0; i < systemList.Count; ++i)
				{
					List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem> manList;
					if (dependencyBuckets.TryGetValue(systemList[i].type, out manList))
					{
						systemList.InsertRange(i+1, manList);
						dependencyBuckets.Remove(systemList[i].type);
						for (int sl = 0; sl < manList.Count; ++sl)
							subSystemLists.Insert(i+1, new List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>());
						addedMore = true;
					}
					var subSystemList = subSystemLists[i];
					for (int subsys = 0; subsys < subSystemList.Count; ++subsys)
					{
						if (dependencyBuckets.TryGetValue(subSystemList[subsys].type, out manList))
						{
							subSystemList.InsertRange(subsys+1, manList);
							dependencyBuckets.Remove(subSystemList[subsys].type);
							addedMore = true;
						}						
					}
				}
			}
			foreach(KeyValuePair<Type, List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>> entry in dependencyBuckets)
			{
				// FIXME: print warning?
				int insertIndex = FindPlayerLoopInsertionPoint(typeof(UnityEngine.Experimental.PlayerLoop.PreUpdate), systemList);
				if (insertIndex < 0)
					insertIndex = 0;

				systemList.InsertRange(insertIndex, entry.Value);
				for (int sl = 0; sl < entry.Value.Count; ++sl)
					subSystemLists.Insert(insertIndex, new List<UnityEngine.Experimental.LowLevel.PlayerLoopSystem>());
			}
			
			var ecsLoop = new UnityEngine.Experimental.LowLevel.PlayerLoopSystem();
			ecsLoop.type = null;
			ecsLoop.subSystemList = systemList.ToArray();
			for (int i = 0; i < systemList.Count; ++i)
				ecsLoop.subSystemList[i].subSystemList = subSystemLists[i].ToArray();
			UnityEngine.Experimental.LowLevel.PlayerLoop.SetPlayerLoop(ecsLoop);
		}
	}
}