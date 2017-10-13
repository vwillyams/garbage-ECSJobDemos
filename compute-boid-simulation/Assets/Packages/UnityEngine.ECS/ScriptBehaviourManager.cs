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
		public string NativeSystem { get; set; }
		public Type  ManagedSystem { get; set; }

		public UpdateAfter(string nativeSystem)
		{
		    NativeSystem = nativeSystem;
			ManagedSystem = null;
		}
		public UpdateAfter(Type managedSystem)
		{
			NativeSystem = null;
		    ManagedSystem = managedSystem;
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
		static private int GetFixedListIndex(List<UnityEngine.PlayerLoopSystem> defaultLoop)
		{
			for (int i = 0;i != defaultLoop.Count;i++)
			{
				if (defaultLoop[i].name == "FixedUpdate")
					return i;
			}

			throw new ArgumentException ("No fixed update loop found");
		}

		static private int FindPlayerLoopInsertionPoint(string dependency, List<UnityEngine.PlayerLoopSystem> systemList)
		{
			if (dependency == null || dependency == "")
				return 0;
			int insertIndex = -1;
			if (dependency.IndexOf('.') < 0)
			{
				// Run after an update phase
				for (int i = 0; i < systemList.Count; ++i)
				{
					if (systemList[i].name.StartsWith(dependency))
						insertIndex = i+1;
				}
			}
			else
			{
				// Run after a specific step
				for (int i = 0; i < systemList.Count; ++i)
				{
					if (systemList[i].name == dependency)
						insertIndex = i+1;
				}
			}
			return insertIndex;
		}



		private UpdateAfter GetManagerDependency(Type managerType)
		{
			var attribs = managerType.GetCustomAttributes(typeof(UpdateAfter), true);
			if (attribs.Length == 0)
				return new UpdateAfter("PreUpdate");
			return attribs[0] as UpdateAfter;
		}
		private void UpdatePlayerLoop()
		{
			// Create buckets for all s_ActiveManagers with same dependency
			var nativeDependencyBuckets = new Dictionary<string, List<UnityEngine.PlayerLoopSystem>>();
			var managedDependencyBuckets = new Dictionary<string, List<UnityEngine.PlayerLoopSystem>>();
			foreach (var manager in s_ActiveManagers)
			{
				// TODO: get dependency from attribute, with default value
				var dependency = GetManagerDependency(manager.GetType());
				List<UnityEngine.PlayerLoopSystem> managerList;
				if (dependency.ManagedSystem != null)
				{
					if (!managedDependencyBuckets.TryGetValue("ECS."+dependency.ManagedSystem, out managerList))
					{
						managerList = new List<UnityEngine.PlayerLoopSystem>();
						managedDependencyBuckets.Add("ECS."+dependency.ManagedSystem, managerList);
					}
				}
				else
				{
					if (!nativeDependencyBuckets.TryGetValue(dependency.NativeSystem, out managerList))
					{
						managerList = new List<UnityEngine.PlayerLoopSystem>();
						nativeDependencyBuckets.Add(dependency.NativeSystem, managerList);
					}
				}
				var system = new UnityEngine.PlayerLoopSystem();
				system.name = "ECS." + manager.GetType();
				var tmp = new DummyDelagateWrapper(manager);
				system.updateDelegate = tmp.TriggerUpdate;
				//system.updateDelegate = manager.OnUpdate;
				managerList.Add(system);
			}
			// Insert the buckets at the appropriate place in the player loop
			var defaultLoop = UnityEngine.PlayerLoop.GetDefaultPlayerLoop();
			if (nativeDependencyBuckets.Count == 0)
			{
				UnityEngine.PlayerLoop.SetPlayerLoop(defaultLoop);
				return;
			}
			var systemList = new List<UnityEngine.PlayerLoopSystem>(defaultLoop.subSystemList);
			var fixedSystemList = new List<UnityEngine.PlayerLoopSystem>(defaultLoop.subSystemList[GetFixedListIndex(systemList)].subSystemList);

			foreach(KeyValuePair<string, List<UnityEngine.PlayerLoopSystem>> entry in nativeDependencyBuckets)
			{
				int insertIndex = FindPlayerLoopInsertionPoint(entry.Key, systemList);
				int fixedInsertIndex = FindPlayerLoopInsertionPoint(entry.Key, fixedSystemList);

				if (insertIndex < 0 && fixedInsertIndex < 0)
				{
					Debug.LogWarning(string.Format("{0} UpdateAfter on non-existing system {1}", entry.Value[0].name, entry.Key));
					insertIndex = FindPlayerLoopInsertionPoint("PreUpdate", systemList);
					if (insertIndex < 0)
						insertIndex = 0;
				}

				if (fixedInsertIndex >= 0)
					fixedSystemList.InsertRange(fixedInsertIndex, entry.Value);
				else
					systemList.InsertRange(insertIndex, entry.Value);
			}
			bool addedMore = true;
			while (managedDependencyBuckets.Count != 0 && addedMore)
			{
				addedMore = false;
				for (int i = 0; i < systemList.Count; ++i)
				{
					List<UnityEngine.PlayerLoopSystem> manList;
					if (managedDependencyBuckets.TryGetValue(systemList[i].name, out manList))
					{
						systemList.InsertRange(i+1, manList);
						managedDependencyBuckets.Remove(systemList[i].name);
						addedMore = true;
					}
				}
			
				for (int i = 0; i < fixedSystemList.Count; ++i)
				{
					List<UnityEngine.PlayerLoopSystem> manList;
					if (managedDependencyBuckets.TryGetValue(fixedSystemList[i].name, out manList))
					{
						fixedSystemList.InsertRange(i+1, manList);
						managedDependencyBuckets.Remove(fixedSystemList[i].name);
						addedMore = true;
					}
				}

			}
			if (managedDependencyBuckets.Count != 0)
			{
				foreach(KeyValuePair<string, List<UnityEngine.PlayerLoopSystem>> entry in managedDependencyBuckets)
				{
					//Debug.LogWarning(string.Format("{0} UpdateAfter on non-existing system {1}", entry.Value[0].name, entry.Key));
					systemList.InsertRange(0, entry.Value);
				}
				
			}
			
			var ecsLoop = new UnityEngine.PlayerLoopSystem();
			ecsLoop.name = "ECSPlayerLoop";
			ecsLoop.subSystemList = systemList.ToArray();
			ecsLoop.subSystemList[GetFixedListIndex (systemList)].subSystemList = fixedSystemList.ToArray ();
			UnityEngine.PlayerLoop.SetPlayerLoop(ecsLoop);
		}
	}
}