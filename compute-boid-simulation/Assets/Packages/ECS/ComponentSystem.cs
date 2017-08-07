using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace ECS
{

    public abstract class ComponentSystem : ScriptBehaviourManager
    {
        TupleSystem[] 							m_Tuples;
    	internal ILightweightComponentManager[]	m_JobDependencyManagers;

    	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    	static void Initialize()
    	{
			foreach (var ass in AppDomain.CurrentDomain.GetAssemblies ())
			{
				var types = ass.GetTypes().Where(t => t.IsSubclassOf(typeof(ComponentSystem)) && !t.IsAbstract);
				foreach (var type in types)
					DependencyManager.GetBehaviourManager (type);	
			}
    	}
    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);
    		InjectTuples.CreateTuplesInjection (GetType(), this, out m_Tuples, out m_JobDependencyManagers);
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
    		for (int i = 0; i != m_Tuples.Length; i++)
    		{
    			if (m_Tuples[i] != null)
    				m_Tuples[i].Dispose ();
    		}
    		m_Tuples = null;
    		m_JobDependencyManagers = null;
    	}

    	protected void UpdateInjectedTuples()
    	{
    		foreach (var tuple in m_Tuples)
    			InjectTuples.UpdateInjection (tuple, this);
    	}

    	override protected void OnUpdate()
    	{
			OnUpdateDontCompleteDependencies ();

			foreach (var dep in m_JobDependencyManagers)
				dep.CompleteWriteDependency ();
    	}

		internal void OnUpdateDontCompleteDependencies()
		{
			base.OnUpdate ();
			UpdateInjectedTuples ();
		}
    }

	public abstract class JobComponentSystem : ComponentSystem
	{
		override protected void OnUpdate()
		{
			OnUpdateDontCompleteDependencies ();
		}

		//@TODO: Need utility methods for dependency chaining
		//       Right now doing Complete on main thread for multi-dependencies
		//       Also use attribute to differentiate read write dependencies
		public JobHandle GetDependency ()
		{
			if (m_JobDependencyManagers.Length == 1)
			{
				return m_JobDependencyManagers[0].GetWriteDependency();
			}
			else
			{
				CompleteDependency();
				return new JobHandle ();
			}
		}

		public void CompleteDependency ()
		{
			foreach (var dep in m_JobDependencyManagers)
				dep.CompleteWriteDependency ();
		}

		public void AddDependency (JobHandle handle)
		{
			foreach (var dep in m_JobDependencyManagers)
				dep.AddWriteDependency (handle);
		}
	}

}