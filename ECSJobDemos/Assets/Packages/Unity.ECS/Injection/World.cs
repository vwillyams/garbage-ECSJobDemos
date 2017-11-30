using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System.Reflection;
using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;
using UnityEngine.ECS;

[assembly:InternalsVisibleTo("Unity.ECS.Tests")]

namespace UnityEngine.ECS
{
	//@TODO: Checks to ensure base override is always called.


	public class World : IDisposable
	{
		class Dependencies
		{
			public struct Manager
			{
				public ScriptBehaviourManager 	manager;
				public FieldInfo 				field;
			}

			public Manager[] 				managers;
		}

		public ReadOnlyCollection<ScriptBehaviourManager> BehaviourManagers
		{
			get
            {
				return new ReadOnlyCollection<ScriptBehaviourManager>(ms_BehaviourManagers);
			}
		}
		List<ScriptBehaviourManager> 	ms_BehaviourManagers = new List<ScriptBehaviourManager> ();
		Dictionary<Type, ScriptBehaviourManager> 	ms_BehaviourManagerLookup = new Dictionary<Type, ScriptBehaviourManager> ();
		Dictionary<Type, Dependencies> 	ms_InstanceDependencies = new Dictionary<Type, Dependencies>();
		int 							ms_DefaultCapacity = 10;

		static World 					ms_Active = null;
		static bool  					ms_DidInitialize = false;

		static public World Active
		{
			get
			{
				return ms_Active;
			}
			set
			{
				ms_Active = value;

				if (!ms_DidInitialize)
				{
					ms_DidInitialize = true; 
				}

			}
		}



		int GetCapacityForType(Type type)
		{
			return ms_DefaultCapacity;
		}

		public static void SetDefaultCapacity(int value)
		{
			Active.ms_DefaultCapacity = value;
		}

        public World()
        {
//			Debug.Log("Create World");
        }


		public void Dispose()
		{
//			Debug.Log("Dispose World");

			// Destruction should happen in reverse order to construction
			ms_BehaviourManagers.Reverse();

			///@TODO: Crazy hackery to make EntityManager be destroyed last.
			foreach (var behaviourManager in ms_BehaviourManagers)
			{
				if (behaviourManager is EntityManager)
				{
					ms_BehaviourManagers.Remove(behaviourManager);
					ms_BehaviourManagers.Add(behaviourManager);
					break;
				}
			}

			foreach (var behaviourManager in ms_BehaviourManagers)
			{
				try
				{
					ScriptBehaviourManager.DestroyInstance (behaviourManager);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}

			ms_BehaviourManagers.Clear();
			ms_BehaviourManagerLookup.Clear();
		}

		ScriptBehaviourManager CreateAndRegisterManager (System.Type type, int capacity)
		{
			var manager = Activator.CreateInstance(type) as ScriptBehaviourManager;

			ms_BehaviourManagers.Add (manager);
			ms_BehaviourManagerLookup.Add(type, manager);

			ScriptBehaviourManager.CreateInstance (manager, capacity);

			return manager;
		}

		public static T GetBehaviourManager<T> () where T : ScriptBehaviourManager
		{
			return (T)GetBehaviourManager (typeof(T));
		}

		public static ScriptBehaviourManager GetBehaviourManager (System.Type type)
		{
			var root = Active;
			ScriptBehaviourManager manager;
			if (root.ms_BehaviourManagerLookup.TryGetValue(type, out manager))
				return manager;
			foreach(var behaviourManager in root.ms_BehaviourManagers)
			{
				if (behaviourManager.GetType() == type || behaviourManager.GetType().IsSubclassOf(type))
				{
					// We will never create a new or more specialized version of this since this is the only place creating managers
					root.ms_BehaviourManagerLookup.Add(type, behaviourManager);
					return behaviourManager;
				}
			}

			//@TODO: Check that type inherit from ScriptBehaviourManager
			var obj = root.CreateAndRegisterManager(type, root.GetCapacityForType(type));

			return obj;
		}

        static void ValidateNoStaticInjectDependency(Type type)
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

		static Dependencies CreateDependencyInjection(Type type)
		{
			var managers = new List<Dependencies.Manager>();

			var fields = type.GetFields (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (var field in fields)
			{
				var hasInject = field.GetCustomAttributes (typeof(InjectAttribute), true).Length != 0;
				if (hasInject)
				{
					if (field.FieldType.IsSubclassOf(typeof(ScriptBehaviourManager)))
					{
						var manager = new Dependencies.Manager();
						manager.manager = GetBehaviourManager(field.FieldType);
						manager.field = field;
						managers.Add(manager);
					}
					else
					{
						Debug.LogErrorFormat("[InjectDependency] can not be applied to type: {0}", field.FieldType);
					}
				}
			}

            ValidateNoStaticInjectDependency(type);

			if (managers.Count != 0)
			{
				var deps = new Dependencies ();
				deps.managers = managers.ToArray ();
				return deps;
			}
			else
				return null;
		}

		internal static void DependencyInject(ScriptBehaviourManager manager)
		{
			var deps = Active.PrepareDependendencyInjectionStatic (manager);
		
			if (deps != null)
			{
				for (int i = 0; i != deps.managers.Length; i++)
					deps.managers[i].field.SetValue (manager, deps.managers[i].manager);
			}
		}

		Dependencies PrepareDependendencyInjectionStatic(object behaviour)
		{
			var type = behaviour.GetType ();
			Dependencies deps;
			if (!ms_InstanceDependencies.TryGetValue (type, out deps))
			{
				deps = CreateDependencyInjection (type);
				ms_InstanceDependencies.Add (type, deps);
			}

			return deps;
		}
	}
}