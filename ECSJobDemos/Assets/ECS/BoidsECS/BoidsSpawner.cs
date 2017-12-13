using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Jobs;
using UnityEngine.ECS;
using Unity.Collections;

namespace BoidSimulations
{
	public class BoidsSpawner : MonoBehaviour
	{
		public GameObject prefab;
		public GameObject[] lightweightPrefabs;

		public float radius = 4.0F;
		public int transformsPerHierarchy = 500;
		public bool lightweightInstantiate;

		private List<GameObject> roots = new List<GameObject>();

        EntityManager m_EntityManager;

		public int initialCount = 2000;
		public int additionalCount = 5000;

		void Instantiate(int count)
		{
			Profiler.BeginSample ("Spawn '" + prefab.name + "'");

			if (lightweightInstantiate)
			{
				var gos = new NativeArray<Entity>(count / lightweightPrefabs.Length, Allocator.Temp);
                for (int p = 0; p < lightweightPrefabs.Length;p++)
                {
                    m_EntityManager.Instantiate(lightweightPrefabs[p], gos);

                    for (int i = 0; i != gos.Length; i++)
                    {
                        var boid = new BoidData();
                        boid.position = Random.insideUnitSphere * radius + transform.position;
                        boid.forward = Random.onUnitSphere;
                        m_EntityManager.SetComponent(gos[i], boid);
                    }
                }

				gos.Dispose ();
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

                    BoidData val;
					val.position = Random.insideUnitSphere * radius + transform.position;
					val.forward = Random.onUnitSphere;
                    var go = Instantiate (prefab, val.position, Quaternion.identity, root.transform) as GameObject;

					m_EntityManager.SetComponent<BoidData>(go.GetComponent<GameObjectEntity>().Entity, val);
				}
			}	

			Profiler.EndSample ();
		}

		void OnEnable()
		{
            m_EntityManager = World.Active.GetOrCreateManager<EntityManager>();
			Instantiate (initialCount);
		}

		void Update()
		{
			if (Input.GetKeyDown("space"))
    			Instantiate (additionalCount);
		}
	}
}