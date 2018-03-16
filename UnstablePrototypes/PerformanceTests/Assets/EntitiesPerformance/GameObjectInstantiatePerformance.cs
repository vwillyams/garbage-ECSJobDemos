using UnityEngine;
using UnityEngine.Profiling;

public class GameObjectInstantiatePerformance : MonoBehaviour
{
	CustomSampler createSampler;
	CustomSampler destroySampler;

	void Awake()
	{
		createSampler = CustomSampler.Create("Object.Instantiate");
		destroySampler = CustomSampler.Create("DestroyImmediate(Instantiate)");
	}

	void Update()
	{
        var temp = new GameObject ("");
        temp.AddComponent(typeof(MonoBehaviour64Bytes));
        temp.AddComponent(typeof(MonoBehaviour64Bytes));
        temp.AddComponent(typeof(MonoBehaviour64Bytes));

        GameObject[] array = new GameObject[PerformanceTestConfiguration.InstanceCount];

		createSampler.Begin ();
		for (int i = 0; i != array.Length; i++)
			array[i] = Instantiate(temp);
		createSampler.End ();

		destroySampler.Begin ();
		for (int i = 0; i != array.Length; i++)
			DestroyImmediate (array[i]);
		destroySampler.End ();

		DestroyImmediate (temp);
	}
}
