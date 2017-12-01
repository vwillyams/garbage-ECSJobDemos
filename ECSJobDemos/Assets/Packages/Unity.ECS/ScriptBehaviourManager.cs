using System.Collections.Generic;
using System;

namespace UnityEngine.ECS
{
	//@TODO: Checks to ensure base override is always called.
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	sealed public class DisableAutoCreationAttribute : System.Attribute
	{
	}
	
	public abstract class ScriptBehaviourManager
	{
		internal static void CreateInstance(ScriptBehaviourManager manager, int capacity)
		{
			World.DependencyInject(manager);

			manager.OnCreateManagerInternal(capacity);

			manager.OnCreateManager(capacity);
		}

		internal static void DestroyInstance(ScriptBehaviourManager inst)
		{
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

		internal abstract void InternalUpdate();
		/// <summary>
		/// Execute the manager immediately.
		/// </summary>
		public void Update() { InternalUpdate(); }
	}
}