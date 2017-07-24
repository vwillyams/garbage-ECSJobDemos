using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class RandomSpawner : ScriptBehaviour
{
	public GameObject prefab;
	public int count = 10000;
	public float radius = 4.0F;
	public int transformsPerHierarchy = 500;
	public enum ActivateMode{ None, ActivateDeactivateAll }
	public ActivateMode activateMode;
	public bool lightweightInstantiate;

	private List<GameObject> roots = new List<GameObject>();

	[InjectDependency]
	ECS.LightweightGameObjectManager m_LightweightGameObjects;

	protected override void OnEnable()
	{
		base.OnEnable ();
		Profiler.BeginSample ("Spawn '" + prefab.name + "'");
		GameObject root = null;

		if (lightweightInstantiate)
		{
			var gos = m_LightweightGameObjects.Instantiate (prefab, count);

			for (int i = 0; i != gos.Length; i++)
			{
				var boid = new ECS.BoidData ();
				boid.position = Random.insideUnitSphere + transform.position;
				boid.forward = Random.onUnitSphere;
				m_LightweightGameObjects.SetLightweightComponent(gos[i], boid);
			}

			gos.Dispose ();
		}
		else
		{
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

	protected override void OnUpdate ()
	{
		base.OnUpdate ();

		if (activateMode == ActivateMode.ActivateDeactivateAll)
		{
			foreach (var go in roots)
				go.SetActive (false);
			foreach (var go in roots)
				go.SetActive (true);
		}
	}
}
