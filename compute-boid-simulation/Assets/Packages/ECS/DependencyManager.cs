﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine.Collections;
using UnityEngine;
using System.Reflection;
using System;
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

		List<ScriptBehaviourManager> 	ms_BehaviourManagers = new List<ScriptBehaviourManager> ();
		Dictionary<Type, Dependencies> 	ms_InstanceDependencies = new Dictionary<Type, Dependencies>();

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


		static int GetCapacityForType(Type type)
		{
			//@TODO:
			return 10000;
		}

		public void Dispose()
		{
			foreach (var behaviourManager in ms_BehaviourManagers)
				ScriptBehaviourManager.DestroyInstance (behaviourManager);

			ms_BehaviourManagers.Clear();
		}


		internal static DefaultUpdateManager CreateDefaultUpdateManager (System.Type type)
		{
			var method = type.GetMethod ("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			if (method.DeclaringType == typeof(ScriptBehaviour))
				return null;
			else
				return ScriptBehaviourManager.CreateInstance(typeof(DefaultUpdateManager), GetCapacityForType(type)) as DefaultUpdateManager;
		}

		public static T GetBehaviourManager<T> () where T : ScriptBehaviourManager
		{
			return (T)GetBehaviourManager (typeof(T));
		}

		public static ScriptBehaviourManager GetBehaviourManager (System.Type type)
		{
			var root = AutoRoot;
			foreach(var behaviourManager in root.ms_BehaviourManagers)
			{
				if (behaviourManager.GetType() == type || behaviourManager.GetType().IsSubclassOf(type))
					return behaviourManager;
			}

			//@TODO: Check that type inherit from ScriptBehaviourManager

			var obj = ScriptBehaviourManager.CreateInstance(type, GetCapacityForType(type));
			root.ms_BehaviourManagers.Add(obj);

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

		static Dependencies CreateDependencyInjection(Type type)
		{
			var managers = new List<Dependencies.Manager>();
			var getComponents = new List<Dependencies.GetComponent>();

			var fields = type.GetFields (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (var field in fields)
			{
				var hasInject = field.GetCustomAttributes (typeof(InjectDependencyAttribute), true).Length != 0;
				if (hasInject)
				{
					if (field.FieldType.IsSubclassOf(typeof(Component)))
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

			var defaultManager = CreateDefaultUpdateManager (type);

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
			return AutoRoot.DependencyInjectInstance (behaviour);
		}

		DefaultUpdateManager DependencyInjectInstance(ScriptBehaviour behaviour)
		{
			var type = behaviour.GetType ();
			Dependencies deps;
			if (!ms_InstanceDependencies.TryGetValue (type, out deps))
			{
				deps = CreateDependencyInjection (type);
				ms_InstanceDependencies.Add (type, deps);

				PerformStaticDependencyInjection (type);
			}

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