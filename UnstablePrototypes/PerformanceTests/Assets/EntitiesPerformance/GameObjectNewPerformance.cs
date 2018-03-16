using UnityEngine;
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
        GameObject[] array = new GameObject[PerformanceTestConfiguration.InstanceCount];

		createSampler.Begin ();
		for (int i = 0; i != array.Length; i++)
            array [i] = new GameObject ("", typeof(MonoBehaviour64Bytes), typeof(MonoBehaviour64Bytes), typeof(MonoBehaviour64Bytes));
		createSampler.End ();

		destroySampler.Begin ();
		for (int i = 0; i != array.Length; i++)
			DestroyImmediate (array [i]);
		destroySampler.End ();
	}
}
