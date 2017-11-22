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
	[AttributeUsage(AttributeTargets.Class)]
	sealed public class DisableAutoCreationAttribute : System.Attribute
	{
	}
	
	public class ScriptBehaviourManager
	{
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

		private void UpdatePlayerLoop()
		{
			var defaultLoop = UnityEngine.Experimental.LowLevel.PlayerLoop.GetDefaultPlayerLoop();
			var ecsLoop = ScriptBehaviourUpdateOrder.InsertManagersInPlayerLoop(s_ActiveManagers, defaultLoop);
			UnityEngine.Experimental.LowLevel.PlayerLoop.SetPlayerLoop(ecsLoop);
		}
	}
}