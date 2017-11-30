using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System.Reflection;
using System;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
	//@TODO: Checks to ensure base override is always called.
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	sealed public class DisableAutoCreationAttribute : System.Attribute
	{
	}
	
	public abstract class ScriptBehaviourManager
	{
		//@TODO: So wrong... remove it
		private static HashSet<ScriptBehaviourManager> s_ActiveManagers = new HashSet<ScriptBehaviourManager>();

		internal static void CreateInstance(ScriptBehaviourManager manager, int capacity)
		{
			s_ActiveManagers.Add(manager);

			World.DependencyInject(manager);

			//@TODO: So wrong, move this to upper layer / delay calling it until many systems have been created...
			UpdatePlayerLoop();

			manager.OnCreateManagerInternal(capacity);

			manager.OnCreateManager(capacity);
		}

		internal static void DestroyInstance(ScriptBehaviourManager inst)
		{
			s_ActiveManagers.Remove(inst);
			UpdatePlayerLoop();
			inst.OnDestroyManager();
		}

		protected abstract void OnCreateManagerInternal(int capacity);

		/// <summary>
		/// Called when the ScriptBehaviourManager is created.
		/// When a new domain is loaded, OnCreate on the necessary manager will be invoked
		/// before the ScriptBehaviour will receive its first OnCreate() call.
		/// capacity can be configured in Edit -> Configure Memory
		/// </summary>
		/// <param name="capacity">Capacity describes how many objects will register with the manager. This lets you reduce realloc calls while the game is running.</param>
		protected virtual void OnCreateManager(int capacity)
		{
		}

		/// <summary>
		/// Called when the ScriptBehaviourManager is destroyed.
		/// Before Playmode exits or scripts are reloaded OnDestroy will be called on all created ScriptBehaviourManagers.
		/// </summary>
		protected virtual void OnDestroyManager()
		{
		}

		/// <summary>
		/// Called once per frame
		/// </summary>
		internal abstract void InternalUpdate();

		private static void UpdatePlayerLoop()
		{
			var defaultLoop = UnityEngine.Experimental.LowLevel.PlayerLoop.GetDefaultPlayerLoop();
			var ecsLoop = ScriptBehaviourUpdateOrder.InsertManagersInPlayerLoop(s_ActiveManagers, defaultLoop);
			UnityEngine.Experimental.LowLevel.PlayerLoop.SetPlayerLoop(ecsLoop);
		}
	}
}