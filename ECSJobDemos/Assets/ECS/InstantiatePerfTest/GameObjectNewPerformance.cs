using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;

public class GameObjectNewPerformance : MonoBehaviour
{
	CustomSampler createSampler;
	CustomSampler destroySampler;

	void Awake()
	{
		createSampler = CustomSampler.Create("new GameObject");
		destroySampler = CustomSampler.Create("DestroyImmediate(new GameObject)");
	}

	void Update()
	{
		GameObject[] array = new GameObject[1000];

		createSampler.Begin ();
		for (int i = 0; i != array.Length; i++)
			array [i] = new GameObject ("", typeof(EmptyMonoBehaviour));
		createSampler.End ();

		destroySampler.Begin ();
		for (int i = 0; i != array.Length; i++)
			DestroyImmediate (array [i]);
		destroySampler.End ();
	}
}
