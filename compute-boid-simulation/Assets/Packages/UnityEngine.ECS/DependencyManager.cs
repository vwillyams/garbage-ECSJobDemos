using System.Collections;
using System.Collections.Generic;
using UnityEngine.Collections;
using UnityEngine;
using System.Reflection;
using System;
using System.Collections.ObjectModel;
using UnityEngine.Assertions;
using UnityEngine.ECS;

namespace UnityEngine.ECS
{
	//@TODO: Checks to ensure base override is always called.

	public class InjectDependencyAttribute : System.Attribute
	{}


	public class DependencyManager : IDisposable
	{
		class Dependencies
		{
			public struct Manager
			{
				public ScriptBehaviourManager 	manager;
				public FieldInfo 				field;
			}

			public struct GetComponent
			{
				public Type type;
				public FieldInfo 				field;
			}

			public DefaultUpdateManager	defaultManager;
			public Manager[] 				managers;
			public GetComponent[] getComponents;
		}

		public ReadOnlyCollection<ScriptBehaviourManager> BehaviourManagers
		{
			get {
				return new ReadOnlyCollection<ScriptBehaviourManager>(ms_BehaviourManagers);
			}
		}
		List<ScriptBehaviourManager> 	ms_BehaviourManagers = new List<ScriptBehaviourManager> ();
		Dictionary<Type, ScriptBehaviourManager> 	ms_BehaviourManagerLookup = new Dictionary<Type, ScriptBehaviourManager> ();
		Dictionary<Type, Dependencies> 	ms_InstanceDependencies = new Dictionary<Type, Dependencies>();
		int 							ms_DefaultCapacity = 10;

		static DependencyManager m_Root = null;
		static bool 			 m_DidInitialize = false;

		static public DependencyManager Root
		{
			get
			{
				return m_Root;
			}
			set
			{
				if (!m_DidInitialize)
				{
					PlayerLoopManager.RegisterDomainUnload (DomainUnloadShutdown);
					m_DidInitialize = true; 
				}

				m_Root = value;
			}
		}

		static DependencyManager AutoRoot
		{
			get
			{
				if (m_Root == null)
					Root = new DependencyManager();
				return m_Root;
			}
		}

		static void DomainUnloadShutdown()
		{
			if (Root != null)
			{
				Root.Dispose ();
				Root = null;
			}
		}

		int GetCapacityForType(Type type)
		{
			return ms_DefaultCapacity;
		}

		public static void SetDefaultCapacity(int value)
		{
			AutoRoot.ms_DefaultCapacity = value;
		}


		public void Dispose()
		{
			foreach (var behaviourManager in ms_BehaviourManagers)
				ScriptBehaviourManager.DestroyInstance (behaviourManager);

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


		internal static DefaultUpdateManager CreateDefaultUpdateManager (System.Type type)
		{
			var method = type.GetMethod ("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			if (method.DeclaringType == typeof(ScriptBehaviour))
				return null;
			else
			{
				var root = AutoRoot;
				return root.CreateAndRegisterManager (typeof(DefaultUpdateManager), root.GetCapacityForType (type)) as DefaultUpdateManager;
			}
		}

		public static T GetBehaviourManager<T> () where T : ScriptBehaviourManager
		{
			return (T)GetBehaviourManager (typeof(T));
		}

		public static ScriptBehaviourManager GetBehaviourManager (System.Type type)
		{
			var root = AutoRoot;
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

		void PerformStaticDependencyInjection(Type type)
		{
			var fields = type.GetFields (BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);

			foreach (var field in fields)
			{
				var hasInject = field.GetCustomAttributes (typeof(InjectDependencyAttribute), true).Length != 0;
				if (hasInject && field.GetValue(null) == null)
					field.SetValue (null, GetBehaviourManager(field.FieldType));
			}
		}

		static Dependencies CreateDependencyInjection(Type type, bool isComponent)
		{
			var managers = new List<Dependencies.Manager>();
			var getComponents = new List<Dependencies.GetComponent>();

			var fields = type.GetFields (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (var field in fields)
			{
				var hasInject = field.GetCustomAttributes (typeof(InjectDependencyAttribute), true).Length != 0;
				if (hasInject)
				{
					if (isComponent && field.FieldType.IsSubclassOf(typeof(Component)))
					{
						var com = new Dependencies.GetComponent();
						com.type = field.FieldType;
						com.field = field;
						getComponents.Add(com);
					}
					else if (field.FieldType.IsSubclassOf(typeof(ScriptBehaviourManager)))
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

			var defaultManager = isComponent ? CreateDefaultUpdateManager (type) : null;

			if (managers.Count != 0 || getComponents.Count != 0 || defaultManager != null)
			{
				var deps = new Dependencies ();
				deps.defaultManager = defaultManager;
				deps.getComponents = getComponents.ToArray ();
				deps.managers = managers.ToArray ();
				return deps;
			}
			else
				return null;
		}


		internal static DefaultUpdateManager DependencyInject(ScriptBehaviour behaviour)
		{
			var deps = AutoRoot.PrepareDependendencyInjectionStatic (behaviour, true);

			if (deps != null)
			{
				for (int i = 0; i != deps.getComponents.Length; i++)
					deps.getComponents[i].field.SetValue (behaviour, behaviour.GetComponent(deps.getComponents[i].type));

				for (int i = 0; i != deps.managers.Length; i++)
					deps.managers[i].field.SetValue (behaviour, deps.managers[i].manager);

				return deps.defaultManager;
			}
			else
				return null;		
		}

		internal static void DependencyInject(ScriptBehaviourManager manager)
		{
			var deps = AutoRoot.PrepareDependendencyInjectionStatic (manager, false);
		
			if (deps != null)
			{
				for (int i = 0; i != deps.managers.Length; i++)
					deps.managers[i].field.SetValue (manager, deps.managers[i].manager);
			}
		}

		Dependencies PrepareDependendencyInjectionStatic(object behaviour, bool isComponent)
		{
			var type = behaviour.GetType ();
			Dependencies deps;
			if (!ms_InstanceDependencies.TryGetValue (type, out deps))
			{
				deps = CreateDependencyInjection (type, isComponent);
				ms_InstanceDependencies.Add (type, deps);

				PerformStaticDependencyInjection (type);
			}

			return deps;
		}
	}
		
	class DefaultUpdateManager : ScriptBehaviourManager
	{
		internal List<ScriptBehaviour> m_Behaviours;

		protected override void OnCreateManager(int capacity)
		{
			base.OnCreateManager (capacity);

			m_Behaviours = new List<ScriptBehaviour>();
		}

		protected override void OnUpdate()
		{
			ScriptBehaviour.Execute (m_Behaviours);
		}
	}
}