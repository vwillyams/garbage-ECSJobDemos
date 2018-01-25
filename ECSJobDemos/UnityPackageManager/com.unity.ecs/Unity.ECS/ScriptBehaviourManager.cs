using System.Collections.Generic;
using System;
using System.Reflection;

namespace UnityEngine.ECS
{
	//@TODO: Checks to ensure base override is always called.
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	sealed public class DisableAutoCreationAttribute : System.Attribute
	{
	}
	
	public abstract class ScriptBehaviourManager
	{	
		static void ValidateNoStaticInjectDependencies(Type type)
        {
#if UNITY_EDITOR
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                var hasInject = field.GetCustomAttributes(typeof(InjectAttribute), true).Length != 0;
                if (hasInject && field.GetValue(null) == null)
                    Debug.LogError(string.Format("{0}.{1} InjectDependency may not be used on static variables", type, field.Name));
            }
#endif
        }

		void InjectConstructorDependencies(World world)
		{
			var type = GetType();
			var fields = type.GetFields (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (var field in fields)
			{
				var hasInject = field.GetCustomAttributes (typeof(InjectAttribute), true).Length != 0;
				if (hasInject)
				{
					if (field.FieldType.IsSubclassOf(typeof(ScriptBehaviourManager)))
					{
						field.SetValue(this, world.GetOrCreateManager(field.FieldType));
					}
					else
					{
						Debug.LogErrorFormat("[Inject] can not be applied to type: {0}", field.FieldType);
					}
				}
			}
		}

		internal void CreateInstance(World world, int capacity)
		{
			InjectConstructorDependencies(world);
			ValidateNoStaticInjectDependencies(GetType());
			
			OnCreateManagerInternal(capacity);
			OnCreateManager(capacity);
		}

		internal void DestroyInstance()
		{
			OnDestroyManager();
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