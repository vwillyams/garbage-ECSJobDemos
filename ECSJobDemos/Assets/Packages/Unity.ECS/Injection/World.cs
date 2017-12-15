using System.Collections.Generic;
using System.Reflection;
using System;
using System.Collections.ObjectModel;
using UnityEngine.Experimental.LowLevel;

namespace UnityEngine.ECS
{
	public class World : IDisposable
	{
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
					behaviourManager.DestroyInstance ();
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

			manager.CreateInstance (this, capacity);

			return manager;
		}
		
		ScriptBehaviourManager GetExistingManagerInternal (Type type)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (!m_AllowGetManager)
				throw new ArgumentException("During destruction of a system you are not allowed to get or create more systems.");
#endif
			
			ScriptBehaviourManager manager = null;
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

			return null;
		}
		
		ScriptBehaviourManager GetOrCreateManagerInternal (Type type)
		{
			var manager = GetExistingManagerInternal(type);

			if (manager != null)
				return manager;
			else
				return  CreateManagerInternal(type, GetCapacityForType(type), null);
		}
		
		void DestroyManagerInteral(ScriptBehaviourManager manager)
		{
			if (!m_BehaviourManagers.Remove(manager))
				throw new System.ArgumentException($"manager does not exist in the world");

			var type = manager.GetType();
			while (type != typeof(ScriptBehaviourManager))
			{
				m_BehaviourManagerLookup.Remove(type);
				type = type.BaseType;
			}

			manager.DestroyInstance();	
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

		public T GetExistingManager<T> () where T : ScriptBehaviourManager
		{
			return (T)GetExistingManagerInternal (typeof(T));
		}

		public ScriptBehaviourManager GetExistingManager(Type type)
		{
			return GetExistingManagerInternal (type);
		}

		public void DestroyManager(ScriptBehaviourManager manager)
		{
			DestroyManagerInteral(manager);
		}

		//@TODO: This should take an array of worlds...
		public static void UpdatePlayerLoop(params World[] worlds)
		{
			var defaultLoop = UnityEngine.Experimental.LowLevel.PlayerLoop.GetDefaultPlayerLoop();

			if (worlds.Length > 0)
			{
				var ecsLoop = ScriptBehaviourUpdateOrder.InsertWorldManagersInPlayerLoop(defaultLoop, worlds);
				SetPlayerLoopAndNotify(ecsLoop);
			}
			else
			{
				SetPlayerLoopAndNotify(defaultLoop);
			}
		}
		
		public static event System.Action<PlayerLoopSystem> OnSetPlayerLoop;

		public static void SetPlayerLoopAndNotify(PlayerLoopSystem playerLoop)
		{
			UnityEngine.Experimental.LowLevel.PlayerLoop.SetPlayerLoop(playerLoop);
			OnSetPlayerLoop?.Invoke(playerLoop);
		}
	}
}