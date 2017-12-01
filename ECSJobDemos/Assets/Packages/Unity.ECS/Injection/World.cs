using System.Collections.Generic;
using System.Reflection;
using System;
using System.Collections.ObjectModel;

namespace UnityEngine.ECS
{
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
				return new ReadOnlyCollection<ScriptBehaviourManager>(m_BehaviourManagers);
			}
		}
		List<ScriptBehaviourManager> 				m_BehaviourManagers = new List<ScriptBehaviourManager> ();
		//@TODO: What about multiple managers of the same type...
		Dictionary<Type, ScriptBehaviourManager> 	m_BehaviourManagerLookup = new Dictionary<Type, ScriptBehaviourManager> ();
		Dictionary<Type, Dependencies> 				m_InstanceDependencies = new Dictionary<Type, Dependencies>();
		int 										m_DefaultCapacity = 10;

		bool 										m_AllowGetManager = true;

		public static World 				        Active { get; set; }


		
		int GetCapacityForType(Type type)
		{
			return m_DefaultCapacity;
		}

		public void SetDefaultCapacity(int value)
		{
			m_DefaultCapacity = value;
		}

        public World()
        {
//			Debug.Log("Create World");
        }


		public void Dispose()
		{
//			Debug.Log("Dispose World");

			// Destruction should happen in reverse order to construction
			m_BehaviourManagers.Reverse();

			///@TODO: Crazy hackery to make EntityManager be destroyed last.
			foreach (var behaviourManager in m_BehaviourManagers)
			{
				if (behaviourManager is EntityManager)
				{
					m_BehaviourManagers.Remove(behaviourManager);
					m_BehaviourManagers.Add(behaviourManager);
					break;
				}
			}

			m_AllowGetManager = false;
			foreach (var behaviourManager in m_BehaviourManagers)
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
			m_AllowGetManager = true;

			m_BehaviourManagers.Clear();
			m_BehaviourManagerLookup.Clear();
		}

		ScriptBehaviourManager CreateManagerInternal (Type type, int capacity, object[] constructorArgumnents)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (!m_AllowGetManager)
				throw new ArgumentException("During destruction of a system you are not allowed to create more systems.");

			if (constructorArgumnents != null && constructorArgumnents.Length != 0)
			{
				var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (constructors.Length == 1 && constructors[0].IsPrivate)
					throw new MissingMethodException($"Constructing {type} failed because the constructor was private, it must be public.");
			}
#endif			
			//@TODO: disallow creating managers during constructor. Only possible after constructor has been called.
			var manager = Activator.CreateInstance(type, constructorArgumnents) as ScriptBehaviourManager;

			m_BehaviourManagers.Add (manager);
			m_BehaviourManagerLookup.Add(type, manager);

			ScriptBehaviourManager.CreateInstance (manager, capacity);

			return manager;
		}
		
		ScriptBehaviourManager GetOrCreateManagerInternal (Type type)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (!m_AllowGetManager)
				throw new ArgumentException("During destruction of a system you are not allowed to get or create more systems.");
#endif
			
			ScriptBehaviourManager manager;
			if (m_BehaviourManagerLookup.TryGetValue(type, out manager))
				return manager;
			foreach(var behaviourManager in m_BehaviourManagers)
			{
				if (behaviourManager.GetType() == type || behaviourManager.GetType().IsSubclassOf(type))
				{
					// We will never create a new or more specialized version of this since this is the only place creating managers
					m_BehaviourManagerLookup.Add(type, behaviourManager);
					return behaviourManager;
				}
			}

			//@TODO: Check that type inherit from ScriptBehaviourManager
			var obj = CreateManagerInternal(type, GetCapacityForType(type), null);

			return obj;
		}
		
		public ScriptBehaviourManager CreateManager(Type type, params object[] constructorArgumnents)
		{
			return CreateManagerInternal(type, GetCapacityForType(type), constructorArgumnents);
		}

		public T CreateManager<T>(params object[] constructorArgumnents) where T : ScriptBehaviourManager
		{
			return (T)CreateManagerInternal(typeof(T), GetCapacityForType(typeof(T)), constructorArgumnents);
		}
		
		public T GetOrCreateManager<T> () where T : ScriptBehaviourManager
		{
			return (T)GetOrCreateManagerInternal (typeof(T));
		}

		public ScriptBehaviourManager GetOrCreateManager(Type type)
		{
			return GetOrCreateManagerInternal (type);
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

		Dependencies CreateDependencyInjection(Type type)
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
						manager.manager = GetOrCreateManager(field.FieldType);
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
			if (!m_InstanceDependencies.TryGetValue (type, out deps))
			{
				deps = CreateDependencyInjection (type);
				m_InstanceDependencies.Add (type, deps);
			}

			return deps;
		}
	}
}