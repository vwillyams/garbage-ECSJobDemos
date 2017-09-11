﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Jobs;
using UnityEngine.ECS;
using UnityEngine.Collections;

namespace BoidSimulations
{
	public class BoidsSpawner : ScriptBehaviour
	{
		public GameObject prefab;
		public GameObject lightweightPrefab;

		public float radius = 4.0F;
		public int transformsPerHierarchy = 500;
		public bool lightweightInstantiate;

		private List<GameObject> roots = new List<GameObject>();

		[InjectDependency]
		EntityManager m_LightweightGameObjects;

		public bool performanceDemoMode = false;

		public int initialCount = 2000;
		public int step1Count = 8000;
		public int step2Count = 190000;

		public int normalInstantiateCount = 50000;


		int stateIndex = 0;

		void Instantiate(int count)
		{
			Profiler.BeginSample ("Spawn '" + prefab.name + "'");

			if (lightweightInstantiate)
			{
				var gos = new NativeArray<Entity>(count, Allocator.Temp);
				m_LightweightGameObjects.Instantiate (lightweightPrefab, gos);

				for (int i = 0; i != gos.Length; i++)
				{
					var boid = new BoidData ();
					boid.position = Random.insideUnitSphere + transform.position;
					boid.forward = Random.onUnitSphere;
					m_LightweightGameObjects.SetComponent(gos[i], boid);
				}

				gos.Dispose ();

				DependencyManager.GetBehaviourManager<ApplyBoidsToInstancing> ().InitializeInstanceRenderer (lightweightPrefab);
			}
			else
			{
				GameObject root = null;
				for (int i = 0; i != count; i++)
				{
					if (transformsPerHierarchy != 0 && i % transformsPerHierarchy == 0)
					{
						root = new GameObject("Chunk "+i);
						root.transform.hierarchyCapacity = transformsPerHierarchy;
						roots.Add (root);
					}

					Instantiate (prefab, Random.insideUnitSphere + transform.position, Random.rotation, root.transform);
				}
			}	

			Profiler.EndSample ();
		}

		protected override void OnEnable()
		{
			base.OnEnable ();

			if (performanceDemoMode)
			{
				JobsUtility.SetAllowUsingJobCompiler(false);
				Instantiate (initialCount);
			}
			else
			{
				Instantiate (normalInstantiateCount);
			}
		}

		protected override void OnDisable()
		{
			base.OnDisable ();
			JobsUtility.SetAllowUsingJobCompiler(true);
		}


		public override void OnUpdate ()
		{
			base.OnUpdate ();

			if (!Input.GetKeyDown("space"))
				return;
			if (!performanceDemoMode)
				return;
				
			// 10k with no jobs and no compiler
			if (stateIndex == 0)
			{
				Instantiate (step1Count);
			}
			// 200k with jobs and no compiler
			else if (stateIndex == 1)
			{
				Instantiate (step2Count);
			}
			// 200k with jobs and compiler
			else if (stateIndex == 2)
			{
				JobsUtility.SetAllowUsingJobCompiler(true);
			}
			stateIndex++;
		}
	}
}