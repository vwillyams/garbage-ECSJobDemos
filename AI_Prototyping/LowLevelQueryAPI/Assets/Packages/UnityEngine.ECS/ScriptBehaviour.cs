using System.Collections;
using System.Collections.Generic;
using UnityEngine.Collections;
using UnityEngine;
using System.Reflection;
using System;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
	public class ScriptBehaviour : MonoBehaviour
	{
		DefaultUpdateManager 		m_DefaultManager; 
		int 			      		m_DefaultIndex = -1;

		// NOTE: The comments for behaviour below are how it is supposed to work.
		//       In this prototype several things don't work that way yet...


		/// <summary>
		/// Invoked when the object is created (SceneLoad, Instantiate, new GameObject(), AddComponent)
		/// OnCreate is also called on both active and inactive scene objects when it is created.
		/// This is useful if you want to perform work on the main thread at load time that can safely be kept during multiple activate / deactivate calls.
		/// OnCreate is called once during the lifetime of each ScriptBehaviour from the perspective of a single domain reload.
		/// However when entering playmode or when scripts are reloaded, we will call OnDestroy before unloading and OnCreate after reloading the domain again.
		/// Conceptually it appears as if the scene with the script behaviour is unloaded (OnDestroy on all invoked) & then loaded again (OnCreate is invoked on all ScriptBehaviours)
		/// * OnCreate is always balanced with OnDestroy. If OnCreate is called, then OnDestroy will be called before the object is destroyed or the application is shut down
		/// * OnCreate is not called on prefabs (prefabs exist only as data)
		/// * Execution order is sorted by type for each atomic batch operation (SceneLoad, Instantiate, new GameObject, AddComponent)
		/// * Not called in EditMode unless marked [ExecuteInEditMode]
		/// </summary>
		protected virtual void OnCreate()
		{
			// @TODO: All of DependencyManager.DependencyInject could probably be moved to the loading thread
			//        if tightly integrated in C++ loading code
			//        Hence would have no impact on Awake / integration time.
			m_DefaultManager = DependencyManager.DependencyInject (this);
		}

		/// <summary>
		/// Invoked when the gameobject becomes active
		/// either because it was explicitly made active, or just created/loaded as an active object.
		/// This method is typically not used a lot, but you'd use it when you want to implement an object pool.
		/// You'd typically "reset" your component here to a beginning state. For example, if you implement a trail renderer, you'd want the trail cleared here.
		/// * OnActivate may be called multiple times during lifetime of the object, it is always balanced with OnDeactivate.
		/// * OnActivate is not called on prefabs since they are never active
		/// * Execution order is sorted by type for each atomic batch operation (SceneLoad, Instantiate, new GameObject, AddComponent)
		/// * Not called in EditMode unless marked [ExecuteInEditMode]
		/// </summary>
		protected virtual void OnActivate()
		{
			throw new System.NotImplementedException ();
		}

		/// <summary>
		/// Invoked when the gameobject becomes active & the ScriptBehaviour enabled. (isActiveAndEnabled)
		/// either because it was explicitly made active & enabled, or just created/loaded as an active object.
		/// This is the most commonly used lifecycle function, between OnEnable / OnDisable the ScriptBehaviour is actively doing its work.
		/// OnUpdate is only called between balanced calls of OnEnable / OnDisable
		/// OnEnable is also a common place to register with a manager, start listening to other events etc
		/// * Execution order is sorted by type for each atomic batch operation (SceneLoad, Instantiate, new GameObject, AddComponent)
		/// * Not called in EditMode unless marked [ExecuteInEditMode]
		/// </summary>
		protected virtual void OnEnable()
		{
			if (m_DefaultManager != null)
			{
				m_DefaultIndex = m_DefaultManager.m_Behaviours.Count;
				m_DefaultManager.m_Behaviours.Add (this);
			}
		}


		/// <summary>
		/// Invoked once per frame if the ScriptBehaviour is enabled and the game object is active.
		/// OnUpdate is the most commonly used function to implement any kind of game behaviour.
		/// * Execution order is per type
		/// * Not called in EditMode unless marked [ExecuteInEditMode]
		/// </summary>
		protected virtual void OnUpdate() { }

		protected virtual void OnDisable()
		{
			if (m_DefaultManager != null)
			{
				var lastBehaviour = m_DefaultManager.m_Behaviours[m_DefaultManager.m_Behaviours.Count - 1];
				lastBehaviour.m_DefaultIndex = m_DefaultIndex;
				m_DefaultManager.m_Behaviours.RemoveAtSwapBack (m_DefaultIndex);
				m_DefaultIndex = -1;
			}
		}

		/// <summary>
		/// Invoked when the gameobject becomes inactive
		/// Either just before destruction or simply due to a call to gameObject.SetSelfActive(false);
		/// * OnDeactivate may be called multiple times during lifetime of the object, it is always balanced with a previous call to OnActivate.
		/// * Execution order is sorted by type for each atomic batch operation (SceneLoad, Instantiate, new GameObject, AddComponent)
		/// * Not called in EditMode unless marked [ExecuteInEditMode]
		/// </summary>
		protected virtual void OnDeactivate()
		{
			throw new System.NotImplementedException ();
		}

		/// <summary>
		/// Invoked when the object is about to be destroyed (Unload scene, Destroy(gameobject), Destroy(behaviour), shutdown)
		/// OnDestroy is also called on both active and inactive scene objects when it is created.
		/// This is useful to perform any kind of cleanup of data.
		/// OnDestroy is called once during the lifetime of each ScriptBehaviour from the perspective of a single domain reload.
		/// However when entering playmode or when scripts are reloaded, we will call OnDestroy before unloading and OnCreate after reloading the domain again.
		/// Conceptually it appears as if the scene with the script behaviour is unloaded (OnDestroy on all invoked) & then loaded again (OnCreate is invoked on all ScriptBehaviours)
		/// * OnDestroy is always balanced with previous call to OnCreate.
		/// * OnCreate is not called on prefabs (prefabs exist only as data)
		/// * Execution order is sorted by type for each atomic batch operation (Scene Unload, DestroyImmediate call, shutdown)
		/// * Not called in EditMode unless marked [ExecuteInEditMode]
		/// </summary>
		protected virtual void OnDestroy() { }


		protected virtual void OnValidate ()
		{
			
		}

		static internal void Execute(List<ScriptBehaviour> behaviours)
		{
			for (int i = 0; i < behaviours.Count; i++)
			{
				behaviours[i].OnUpdate ();
			}
		}


		//@TODO: Close enough behaviour for now, but unfortunately it is totally not balanced with OnDestroy
		void Awake()
		{
			OnCreate ();
		}
	}
}